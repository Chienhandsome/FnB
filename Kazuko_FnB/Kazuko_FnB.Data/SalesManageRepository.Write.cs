using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    public sealed partial class SalesManageRepository
    {
        /// <summary>
        /// Kiểm tra âm kho cho hóa đơn đã thanh toán đang được cập nhật (§6.6):
        /// cộng ròng SL (cả dòng dương lẫn dòng âm do Trả đồ) theo từng (Kho, Mặt hàng) cho dòng bán + vật tư xuất;
        /// so với tồn kho loại trừ chính bill đang sửa. Nếu tồn &lt; nhu cầu ròng (&gt; 0) thì trả lỗi rõ tên hàng và tồn.
        /// </summary>
        public static string CheckPaidBillUpdateStockIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader workingBill,
            List<MaterialExportItem> exports)
        {
            if (workingBill == null)
                return null;

            var demand = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var hasStockCache = new Dictionary<string, bool>(StringComparer.Ordinal);

            if (workingBill.Lines != null)
            {
                foreach (ServiceOrderLine line in workingBill.Lines)
                {
                    if (line == null || line.IsMaterialExport || string.IsNullOrEmpty(line.ProductId) || line.Quantity == 0m)
                        continue;

                    bool hasStock;
                    if (!hasStockCache.TryGetValue(line.ProductId, out hasStock))
                    {
                        ServiceMenuItem prod = ServiceOrderRepository.LoadProductIn(conn, tx, line.ProductId);
                        hasStock = prod != null && prod.HasStock;
                        hasStockCache[line.ProductId] = hasStock;
                    }
                    if (!hasStock)
                        continue;

                    string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, line.WarehouseId ?? workingBill.WarehouseId);
                    if (string.IsNullOrEmpty(warehouseId))
                        continue;

                    string key = warehouseId + "|" + line.ProductId;
                    decimal cur;
                    demand.TryGetValue(key, out cur);
                    demand[key] = cur + line.Quantity;
                    names[key] = line.ItemName ?? "";
                }
            }

            if (exports != null)
            {
                foreach (MaterialExportItem exp in exports)
                {
                    if (exp == null || !exp.HasStock || exp.Quantity == 0m || string.IsNullOrEmpty(exp.WarehouseId))
                        continue;
                    string key = exp.WarehouseId + "|" + exp.MaterialId;
                    decimal cur;
                    demand.TryGetValue(key, out cur);
                    demand[key] = cur + exp.Quantity;
                    names[key] = exp.MaterialName ?? "";
                }
            }

            var exclude = new[] { workingBill.Id };
            foreach (KeyValuePair<string, decimal> kv in demand)
            {
                if (kv.Value <= 0m)
                    continue;
                int sep = kv.Key.IndexOf('|');
                string warehouseId = kv.Key.Substring(0, sep);
                string productId = kv.Key.Substring(sep + 1);
                decimal available = ServicePaymentRepository.GetStockBalanceExcludingBillsIn(conn, tx, productId, warehouseId, exclude);
                if (available < kv.Value)
                    return ServiceOrderConstants.InsufficientStock(names[kv.Key], available, kv.Value);
            }
            return null;
        }

        /// <summary>
        /// Đồng bộ toàn bộ dòng TDONHANGCHITIET của bill đã thanh toán trong một transaction:
        /// xóa dòng xuất vật tư cũ (XUATVATTU = 30), xóa các dòng đã bỏ, cập nhật dòng cũ, chèn dòng mới
        /// (gồm dòng SL âm do Trả đồ và dòng con combo), rồi chèn lại các dòng xuất vật tư mới (§2.2 bước 3).
        /// </summary>
        public static void SyncBillLinesAndMaterialExportsIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader workingBill,
            List<MaterialExportItem> exports,
            string actorId,
            DateTime now)
        {
            ServicePaymentRepository.DeleteOldMaterialExportsIn(conn, tx, workingBill.Id);

            DataTable existingDt = DbHelper.Table(conn, tx,
                "SELECT ID FROM TDONHANGCHITIET WHERE TDONHANGID = @B AND COALESCE(XUATVATTU, 0) = 0",
                "@B", workingBill.Id);
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DataRow r in existingDt.Rows)
            {
                string id = DbHelper.Str(r["ID"]);
                if (!string.IsNullOrEmpty(id))
                    existingIds.Add(id);
            }

            var keptIds = new HashSet<string>(StringComparer.Ordinal);
            if (workingBill.Lines != null)
            {
                foreach (ServiceOrderLine line in workingBill.Lines)
                {
                    if (line == null || line.IsMaterialExport)
                        continue;
                    if (string.IsNullOrEmpty(line.Id))
                        line.Id = DbHelper.NewId();
                    line.OrderId = workingBill.Id;
                    if (string.IsNullOrEmpty(line.WarehouseId))
                        line.WarehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, workingBill.WarehouseId);

                    if (existingIds.Contains(line.Id))
                    {
                        ServiceOrderRepository.UpdateLineIn(conn, tx, line, actorId, now);
                    }
                    else
                    {
                        ServiceOrderRepository.InsertLineIn(conn, tx, line, actorId, now);
                    }
                    keptIds.Add(line.Id);
                }
            }

            foreach (string oldId in existingIds)
            {
                if (!keptIds.Contains(oldId))
                {
                    DbHelper.Exec(conn, tx,
                        "DELETE FROM TDONHANGCHITIET WHERE ID = @I AND TDONHANGID = @B",
                        "@I", oldId, "@B", workingBill.Id);
                }
            }

            if (exports != null)
            {
                foreach (MaterialExportItem exp in exports)
                {
                    if (exp != null && exp.Quantity != 0m)
                        ServicePaymentRepository.InsertMaterialExportLineIn(conn, tx, workingBill.Id, exp, actorId, now);
                }
            }
        }

        /// <summary>
        /// Cập nhật toàn bộ cột tổng tiền, đầu phiếu và thanh toán của bill đã thanh toán (bill đơn, không NHOMGUID).
        /// GIỮ NGUYÊN USERTHANHTOANID, GIOTHANHTOAN và NGAY (chỉ đổi NGAY khi updateDate = true theo §1.1/§2.2).
        /// Không chạm DBAN.TDONHANGID và không xóa TINCHEBIEN.
        /// </summary>
        public static int UpdatePaidBillHeaderAndPaymentIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader h,
            PaymentBillAllocation a,
            bool updateDate,
            DateTime newDate,
            string employeeId,
            string orderNumbers,
            string actorId,
            DateTime now)
        {
            var extraSet = "";
            var args = new List<object>
            {
                "@KH", string.IsNullOrEmpty(a.FinalCustomerId) ? null : a.FinalCustomerId,
                "@SOKHACH", Math.Max(1, h.GuestCount),
                "@NOTE", string.IsNullOrEmpty(h.Note) ? null : h.Note,
                "@TIENHANG", h.TienHang,
                "@TIENHANGCG", h.TienHangChuaGiam,
                "@GGMH", h.GiamGiaMatHang,
                "@TLGG", h.TileGiamGia,
                "@TGG", h.TienGiamGia,
                "@GTT", h.GiamTheoTien ? ActiveStatus : 0,
                "@TLPDV", h.TilePhiDichVu,
                "@PDV", h.PhiDichVu,
                "@PDVTT", h.PhiDichVuTheoTien ? ActiveStatus : 0,
                "@TLTHUE", h.TileThue,
                "@TTHUE", h.TienThue,
                "@TONGCONG", h.TongCong,
                "@TM", a.Cash,
                "@CK", a.Transfer,
                "@THE", a.Card,
                "@TK", string.IsNullOrEmpty(a.AccountId) ? null : a.AccountId,
                "@VOU", a.Voucher,
                "@VID", string.IsNullOrEmpty(a.VoucherId) ? null : a.VoucherId,
                "@TTT", a.PrepaidCard,
                "@TID", string.IsNullOrEmpty(a.PrepaidCardId) ? null : a.PrepaidCardId,
                "@TTL", a.PointsMoney,
                "@DG", a.PointsUsed,
                "@DIEM", a.EarnedPoints,
                "@DT", a.DatTruoc,
                "@NC", a.NoCu,
                "@KD", a.KhachDua,
                "@TL", a.Change,
                "@TTTOT", a.TienThanhToan,
                "@CL", a.ConLai,
                "@CN", a.CongNo,
                "@CNF", a.ConNoFlag,
                "@UM", actorId ?? "",
                "@TMOD", now
            };

            if (updateDate)
            {
                extraSet += ", NGAY = @NGAY";
                args.Add("@NGAY");
                args.Add(newDate.Date);
            }
            if (employeeId != null)
            {
                extraSet += ", DNHANVIENXUATID = @NV";
                args.Add("@NV");
                args.Add(string.IsNullOrEmpty(employeeId) ? null : employeeId);
            }
            if (orderNumbers != null)
            {
                extraSet += ", SOORDER = @ORD";
                args.Add("@ORD");
                args.Add(string.IsNullOrWhiteSpace(orderNumbers) ? null : orderNumbers.Trim());
            }

            args.Add("@ID");
            args.Add(h.Id);

            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET " +
                "DKHACHHANGID = @KH, SOKHACH = @SOKHACH, NOTE = @NOTE, " +
                "TIENHANG = @TIENHANG, TIENHANGCHUAGIAM = @TIENHANGCG, GIAMGIAMATHANG = @GGMH, " +
                "TILEGIAMGIA = @TLGG, TIENGIAMGIA = @TGG, GIAMTHEOTIEN = @GTT, " +
                "TILEGIAMGIATONG = @TLGG, TIENGIAMGIATONG = @TGG, " +
                "TILEPHIDICHVU = @TLPDV, PHIDICHVU = @PDV, PHIDICHVUTHEOTIEN = @PDVTT, " +
                "TILETHUE = @TLTHUE, TIENTHUE = @TTHUE, TONGCONG = @TONGCONG, " +
                "DATHANHTOAN = 30, LOAITHANHTOAN = 0, " +
                "TIENMAT = @TM, CHUYENKHOAN = @CK, THE = @THE, DTAIKHOANNGANHANGID = @TK, " +
                "VOUCHER = @VOU, DVOUCHERID = @VID, THETRATRUOC = @TTT, DTHETRATRUOCID = @TID, " +
                "TRUTICHLUY = @TTL, DIEMGIAM = @DG, DIEM = @DIEM, " +
                "DATTRUOC = @DT, NOCU = @NC, KHACHDUA = @KD, TRALAI = @TL, " +
                "TIENTHANHTOAN = @TTTOT, CONLAI = @CL, CONGNO = @CN, CONNO = @CNF, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TMOD" + extraSet + " " +
                "WHERE ID = @ID AND COALESCE(DATHANHTOAN, 0) = 30",
                args.ToArray());
        }

        /// <summary>Ghi ngay TDONHANG.GIOTRADO = serverNow khi xác nhận Trả đồ (§2.3).</summary>
        public static int UpdateReturnTimeIn(FbConnection conn, FbTransaction tx, string billId, string actorId, DateTime serverNow)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET GIOTRADO = @GTD, USERMODIFIEDID = @U, TIMEMODIFIED = @GTD " +
                "WHERE ID = @ID AND COALESCE(DATHANHTOAN, 0) = 30",
                "@GTD", serverNow, "@U", actorId ?? "", "@ID", billId);
        }
    }
}
