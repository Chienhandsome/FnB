using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    public sealed partial class ServicePaymentRepository
    {
        private const string ProductTypeBrew = "1";

        // ------------------------------------------------------------------ Xuất vật tư & kiểm tra âm kho (§3.7, §6.6)

        /// <summary>
        /// Tính danh sách vật tư cần xuất (XUATVATTU = 30) cho một bill theo nha-hang.md §8:
        /// - Chỉ xét các dòng có mặt hàng loại định lượng (DLOAIMATHANGID = '1').
        /// - Nhân SLXUATCHUAQUYDOI × DDINHLUONG.SOLUONG, gom theo (Kho, Vật tư).
        /// - Nếu vật tư lại là định lượng ('1'), mở rộng thêm đúng 1 tầng rồi chỉ giữ vật tư KHÔNG phải định lượng.
        /// </summary>
        public static List<MaterialExportItem> ComputeMaterialExportsIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader bill)
        {
            var result = new List<MaterialExportItem>();
            if (bill == null || bill.Lines == null)
                return result;

            var byKey = new Dictionary<string, MaterialExportItem>(StringComparer.Ordinal);
            foreach (ServiceOrderLine line in bill.Lines)
            {
                if (line == null || line.IsMaterialExport || string.IsNullOrEmpty(line.ProductId) || line.Quantity == 0m)
                    continue;
                if (!string.Equals(line.ProductTypeId, ProductTypeBrew, StringComparison.Ordinal))
                    continue;

                string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, line.WarehouseId ?? bill.WarehouseId);
                List<ServiceRecipeComponent> comps = ServiceOrderRepository.LoadRecipeIn(conn, tx, line.ProductId);
                foreach (ServiceRecipeComponent comp in comps)
                {
                    if (string.IsNullOrEmpty(comp.MaterialId) || comp.Quantity == 0m)
                        continue;
                    decimal qty1 = line.Quantity * comp.Quantity;
                    if (qty1 == 0m)
                        continue;

                    if (string.Equals(comp.MaterialTypeId, ProductTypeBrew, StringComparison.Ordinal))
                    {
                        // Mở rộng đúng một tầng nữa, chỉ giữ vật tư không phải định lượng.
                        List<ServiceRecipeComponent> subComps = ServiceOrderRepository.LoadRecipeIn(conn, tx, comp.MaterialId);
                        foreach (ServiceRecipeComponent sub in subComps)
                        {
                            if (string.IsNullOrEmpty(sub.MaterialId) || sub.Quantity == 0m)
                                continue;
                            if (string.Equals(sub.MaterialTypeId, ProductTypeBrew, StringComparison.Ordinal))
                                continue;
                            AccumulateMaterial(byKey, warehouseId, sub, qty1 * sub.Quantity);
                        }
                    }
                    else
                    {
                        AccumulateMaterial(byKey, warehouseId, comp, qty1);
                    }
                }
            }

            foreach (MaterialExportItem item in byKey.Values)
            {
                if (item.Quantity != 0m)
                    result.Add(item);
            }
            return result;
        }

        private static void AccumulateMaterial(
            Dictionary<string, MaterialExportItem> map,
            string warehouseId,
            ServiceRecipeComponent comp,
            decimal qty)
        {
            if (qty == 0m)
                return;
            string key = (warehouseId ?? "") + "|" + comp.MaterialId;
            MaterialExportItem existing;
            if (map.TryGetValue(key, out existing))
            {
                existing.Quantity += qty;
                return;
            }
            map[key] = new MaterialExportItem
            {
                MaterialId = comp.MaterialId,
                MaterialName = comp.MaterialName ?? "",
                UnitId = comp.UnitId,
                WarehouseId = warehouseId,
                Quantity = qty,
                CostPrice = comp.CostPrice,
                HasStock = comp.HasStock
            };
        }

        /// <summary>
        /// Kiểm tra âm kho lúc thanh toán trên toàn bộ các bill trong đợt thanh toán (§6.6):
        /// cộng dồn nhu cầu xuất của mọi mặt hàng theo dõi tồn kho (cả dòng bán trực tiếp, con combo lẫn vật tư xuất từ định lượng).
        /// Nếu tồn kho &lt; cần xuất thì trả thông báo lỗi rõ tên mặt hàng và tồn hiện có; đủ tồn thì trả null.
        /// </summary>
        public static string CheckPaymentStockIn(
            FbConnection conn,
            FbTransaction tx,
            IList<ServiceOrderHeader> bills,
            IDictionary<string, List<MaterialExportItem>> exportsByBill)
        {
            if (bills == null || bills.Count == 0)
                return null;

            var excludeIds = new List<string>(bills.Count);
            var demand = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var prodHasStockCache = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (ServiceOrderHeader bill in bills)
            {
                if (bill == null)
                    continue;
                excludeIds.Add(bill.Id);

                foreach (ServiceOrderLine line in bill.Lines)
                {
                    if (line == null || line.IsMaterialExport || string.IsNullOrEmpty(line.ProductId) || line.Quantity <= 0m)
                        continue;

                    bool hasStock;
                    if (!prodHasStockCache.TryGetValue(line.ProductId, out hasStock))
                    {
                        ServiceMenuItem prod = ServiceOrderRepository.LoadProductIn(conn, tx, line.ProductId);
                        hasStock = prod != null && prod.HasStock;
                        prodHasStockCache[line.ProductId] = hasStock;
                    }
                    if (!hasStock)
                        continue;

                    string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, line.WarehouseId ?? bill.WarehouseId);
                    if (string.IsNullOrEmpty(warehouseId))
                        continue;

                    string key = warehouseId + "|" + line.ProductId;
                    decimal cur;
                    demand.TryGetValue(key, out cur);
                    demand[key] = cur + line.Quantity;
                    names[key] = line.ItemName ?? "";
                }

                List<MaterialExportItem> exports;
                if (exportsByBill != null && exportsByBill.TryGetValue(bill.Id, out exports) && exports != null)
                {
                    foreach (MaterialExportItem exp in exports)
                    {
                        if (exp == null || !exp.HasStock || exp.Quantity <= 0m || string.IsNullOrEmpty(exp.WarehouseId))
                            continue;
                        string key = exp.WarehouseId + "|" + exp.MaterialId;
                        decimal cur;
                        demand.TryGetValue(key, out cur);
                        demand[key] = cur + exp.Quantity;
                        names[key] = exp.MaterialName ?? "";
                    }
                }
            }

            foreach (KeyValuePair<string, decimal> kv in demand)
            {
                int sep = kv.Key.IndexOf('|');
                string warehouseId = kv.Key.Substring(0, sep);
                string productId = kv.Key.Substring(sep + 1);
                decimal available = GetStockBalanceExcludingBillsIn(conn, tx, productId, warehouseId, excludeIds);
                if (available < kv.Value)
                    return ServiceOrderConstants.InsufficientStock(names[kv.Key], available, kv.Value);
            }
            return null;
        }

        public static decimal GetStockBalanceExcludingBillsIn(
            FbConnection conn,
            FbTransaction tx,
            string productId,
            string warehouseId,
            IList<string> excludeBillIds)
        {
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(warehouseId))
                return 0m;

            var args = new List<object> { "@P", productId, "@W", warehouseId, "@Del", DeletedStatus };
            string excludeSql = BuildNotInClause("H.ID", excludeBillIds, args, "@EX");

            object v = DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(C.SLNHAP, 0) - COALESCE(C.SLXUAT, 0)), 0) " +
                "FROM TDONHANGCHITIET C INNER JOIN TDONHANG H ON H.ID = C.TDONHANGID " +
                "WHERE C.DMATHANGID = @P AND C.DKHOHANGID = @W AND COALESCE(H.STATUS, 0) <> @Del " +
                "AND (COALESCE(H.LOAI, 0) <> 0 OR (COALESCE(H.LOAI, 0) = 0 AND COALESCE(H.DATHANHTOAN, 0) = 30))" +
                excludeSql,
                args.ToArray());
            return DbHelper.Dec(v);
        }

        // ------------------------------------------------------------------ Ghi đóng bill, xuất vật tư, xóa bếp, nâng hạng

        public static void DeleteOldMaterialExportsIn(FbConnection conn, FbTransaction tx, string billId)
        {
            DbHelper.Exec(conn, tx,
                "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @B AND COALESCE(XUATVATTU, 0) = 30",
                "@B", billId);
        }

        public static void InsertMaterialExportLineIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            MaterialExportItem item,
            string actorId,
            DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANGCHITIET (" +
                "ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, " +
                "SLXUAT, SLXUATCHUAQUYDOI, SLNHAP, SLNHAPCHUAQUYDOI, " +
                "DONGIA, GIAVON, TILEGIAMGIA, TIENGIAMGIA, GIAMTHEOTIEN, THANHTIEN, " +
                "XUATVATTU, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @OID, @PID, @NAME, @UID, @WID, " +
                "@QTY, @QTY, 0, 0, " +
                "0, @COST, 0, 0, 0, 0, " +
                "30, 30, @U, @T, NULL, NULL)",
                "@ID", DbHelper.NewId(),
                "@OID", billId,
                "@PID", item.MaterialId,
                "@NAME", item.MaterialName ?? "",
                "@UID", string.IsNullOrEmpty(item.UnitId) ? null : item.UnitId,
                "@WID", string.IsNullOrEmpty(item.WarehouseId) ? null : item.WarehouseId,
                "@QTY", item.Quantity,
                "@COST", item.CostPrice,
                "@U", actorId ?? "",
                "@T", now);
        }

        public static void DeleteKitchenPrintIn(FbConnection conn, FbTransaction tx, string billId)
        {
            DbHelper.Exec(conn, tx, "DELETE FROM TINCHEBIEN WHERE TDONHANGID = @B", "@B", billId);
        }

        /// <summary>SOTT tiếp theo trong ngày = MAX(SOTT) + 1 với LOAI = 0 AND DATHANHTOAN = 30 (§7).</summary>
        public static int GetNextSequenceInDayIn(FbConnection conn, FbTransaction tx, DateTime paymentDate)
        {
            int max = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(MAX(SOTT), 0) FROM TDONHANG " +
                "WHERE COALESCE(LOAI, 0) = 0 AND COALESCE(DATHANHTOAN, 0) = 30 AND NGAY = @D",
                "@D", paymentDate.Date));
            return max + 1;
        }

        /// <summary>
        /// Ghi toàn bộ cột thanh toán lên một dòng TDONHANG (DATHANHTOAN = 30).
        /// Không cập nhật trực tiếp DBAN.TDONHANGID — trigger TDONHANG_TRIGGER tự giải phóng bàn (P3).
        /// Trả về số dòng bị cập nhật (phải là 1; 0 nghĩa là bill đã bị thanh toán ở phiên khác).
        /// </summary>
        public static int UpdateBillPaidIn(
            FbConnection conn,
            FbTransaction tx,
            PaymentBillAllocation a,
            DateTime paymentDate,
            DateTime paidTime,
            int sott,
            string actorId,
            string employeeId,
            string orderNumbers)
        {
            var extraSet = "";
            var args = new List<object>
            {
                "@NGAY", paymentDate.Date,
                "@GIO", paidTime,
                "@UTT", actorId ?? "",
                "@SOTT", sott,
                "@KH", string.IsNullOrEmpty(a.FinalCustomerId) ? null : a.FinalCustomerId,
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
                "@TMOD", paidTime
            };

            if (!string.IsNullOrEmpty(employeeId))
            {
                extraSet += ", DNHANVIENXUATID = @NV";
                args.Add("@NV");
                args.Add(employeeId);
            }
            if (!string.IsNullOrWhiteSpace(orderNumbers))
            {
                extraSet += ", SOORDER = @ORD";
                args.Add("@ORD");
                args.Add(orderNumbers.Trim());
            }

            args.Add("@ID");
            args.Add(a.BillId);

            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET " +
                "DATHANHTOAN = 30, NGAY = @NGAY, GIOTHANHTOAN = @GIO, USERTHANHTOANID = @UTT, " +
                "SOTT = @SOTT, LOAITHANHTOAN = 0, DKHACHHANGID = @KH, " +
                "TIENMAT = @TM, CHUYENKHOAN = @CK, THE = @THE, DTAIKHOANNGANHANGID = @TK, " +
                "VOUCHER = @VOU, DVOUCHERID = @VID, THETRATRUOC = @TTT, DTHETRATRUOCID = @TID, " +
                "TRUTICHLUY = @TTL, DIEMGIAM = @DG, DIEM = @DIEM, " +
                "DATTRUOC = @DT, NOCU = @NC, KHACHDUA = @KD, TRALAI = @TL, " +
                "TIENTHANHTOAN = @TTTOT, CONLAI = @CL, CONGNO = @CN, CONNO = @CNF, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TMOD" + extraSet + " " +
                "WHERE ID = @ID AND COALESCE(DATHANHTOAN, 0) = 0",
                args.ToArray());
        }

        public static void ReleaseTableLockIn(FbConnection conn, FbTransaction tx, string tableId)
        {
            if (string.IsNullOrEmpty(tableId))
                return;
            DbHelper.Exec(conn, tx, "UPDATE DBAN SET TAIKHOANGIU = NULL, GIULUC = NULL WHERE ID = @I", "@I", tableId);
        }

        /// <summary>
        /// Gán NHOMGUID mới cho các bill được chọn trong "Thanh toán chung" (§3.7.3).
        /// Nếu bill nào đã thuộc nhóm cũ thì gộp luôn cả nhóm cũ vào nhóm mới; trả về map billId -> oldGroupGuid để hoàn tác khi hủy.
        /// </summary>
        public GroupAssignResult AssignPaymentGroup(IList<string> selectedBillIds, string actorId)
        {
            var result = new GroupAssignResult();
            if (selectedBillIds == null || selectedBillIds.Count < 2)
            {
                result.ErrorMessage = ServicePaymentConstants.GroupMinTables;
                return result;
            }

            string newGroupGuid = DbHelper.NewId();
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                var allIds = new HashSet<string>(StringComparer.Ordinal);
                var existingGroups = new HashSet<string>(StringComparer.Ordinal);

                foreach (string id in selectedBillIds)
                {
                    if (string.IsNullOrEmpty(id))
                        continue;
                    DataTable dt = DbHelper.Table(conn, tx,
                        "SELECT ID, COALESCE(NHOMGUID, '') AS NHOMGUID FROM TDONHANG " +
                        "WHERE ID = @I AND COALESCE(DATHANHTOAN, 0) = 0 AND COALESCE(STATUS, 0) <> @Del",
                        "@I", id, "@Del", DeletedStatus);
                    if (dt.Rows.Count == 0)
                    {
                        result.ErrorMessage = ServiceOrderConstants.BillAlreadyPaid;
                        return;
                    }
                    allIds.Add(id);
                    string oldG = DbHelper.Str(dt.Rows[0]["NHOMGUID"]) ?? "";
                    result.PreviousGroupGuids[id] = oldG;
                    if (oldG.Length > 0)
                        existingGroups.Add(oldG);
                }

                // Bill đã thuộc nhóm cũ thì kéo cả nhóm cũ vào nhóm mới (§3.7.3).
                foreach (string oldG in existingGroups)
                {
                    DataTable dtOld = DbHelper.Table(conn, tx,
                        "SELECT ID, COALESCE(NHOMGUID, '') AS NHOMGUID FROM TDONHANG " +
                        "WHERE NHOMGUID = @G AND COALESCE(DATHANHTOAN, 0) = 0 AND COALESCE(STATUS, 0) <> @Del",
                        "@G", oldG, "@Del", DeletedStatus);
                    foreach (DataRow r in dtOld.Rows)
                    {
                        string bid = DbHelper.Str(r["ID"]);
                        if (!string.IsNullOrEmpty(bid))
                        {
                            allIds.Add(bid);
                            if (!result.PreviousGroupGuids.ContainsKey(bid))
                                result.PreviousGroupGuids[bid] = DbHelper.Str(r["NHOMGUID"]) ?? "";
                        }
                    }
                }

                if (allIds.Count < 2)
                {
                    result.ErrorMessage = ServicePaymentConstants.GroupMinTables;
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                foreach (string bid in allIds)
                {
                    DbHelper.Exec(conn, tx,
                        "UPDATE TDONHANG SET NHOMGUID = @G, USERMODIFIEDID = @U, TIMEMODIFIED = @T " +
                        "WHERE ID = @I AND COALESCE(DATHANHTOAN, 0) = 0",
                        "@G", newGroupGuid, "@U", actorId ?? "", "@T", now, "@I", bid);
                }
                result.GroupGuid = newGroupGuid;
                result.Ok = true;
            });

            return result;
        }

        /// <summary>Khôi phục NHOMGUID về trạng thái trước khi mở Thanh toán chung khi người dùng hủy hộp thanh toán (§3.7.3).</summary>
        public void RestorePaymentGroup(IDictionary<string, string> previousGroupGuids, string actorId)
        {
            if (previousGroupGuids == null || previousGroupGuids.Count == 0)
                return;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                foreach (KeyValuePair<string, string> kv in previousGroupGuids)
                {
                    object prev = string.IsNullOrEmpty(kv.Value) ? (object)null : kv.Value;
                    DbHelper.Exec(conn, tx,
                        "UPDATE TDONHANG SET NHOMGUID = @G, USERMODIFIEDID = @U, TIMEMODIFIED = @T " +
                        "WHERE ID = @I AND COALESCE(DATHANHTOAN, 0) = 0",
                        "@G", prev, "@U", actorId ?? "", "@T", now, "@I", kv.Key);
                }
            });
        }

        /// <summary>
        /// Tự động nâng hạng nhóm khách khi đạt hạn mức điểm (TuDongNangCapThanhVienKhiDatHanMuc = 30, §3.7).
        /// Chỉ nâng lên nhóm có ngưỡng DIEMTICHLUY cao hơn nhóm hiện tại; trả về tên nhóm mới nếu đã nâng, null nếu không đổi.
        /// </summary>
        public static string TryUpgradeCustomerGroupIn(
            FbConnection conn,
            FbTransaction tx,
            string customerId,
            int pointCalcMode,
            int revenuePerPoint,
            string actorId,
            DateTime now)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;

            string custName, custAddr, cardId, currentGroupId;
            if (!CustomerExistsIn(conn, tx, customerId, out custName, out custAddr, out cardId, out currentGroupId))
                return null;

            decimal currentThreshold = 0m;
            if (!string.IsNullOrEmpty(currentGroupId))
            {
                currentThreshold = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(DIEMTICHLUY, 0) FROM DNHOMKHACHHANG WHERE ID = @G",
                    "@G", currentGroupId));
            }

            decimal totalPoints = GetCustomerPointsIn(conn, tx, customerId, pointCalcMode, revenuePerPoint, null);
            if (totalPoints <= 0m)
                return null;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT FIRST 1 ID, NAME, COALESCE(DIEMTICHLUY, 0) AS DIEMTICHLUY " +
                "FROM DNHOMKHACHHANG WHERE STATUS = @S AND COALESCE(DIEMTICHLUY, 0) > 0 AND DIEMTICHLUY <= @P " +
                "ORDER BY DIEMTICHLUY DESC, NAME",
                "@S", ActiveStatus, "@P", totalPoints);
            if (dt.Rows.Count == 0)
                return null;

            string targetId = DbHelper.Str(dt.Rows[0]["ID"]);
            string targetName = DbHelper.Str(dt.Rows[0]["NAME"]) ?? "";
            decimal targetThreshold = DbHelper.Dec(dt.Rows[0]["DIEMTICHLUY"]);

            if (string.IsNullOrEmpty(targetId)
                || string.Equals(targetId, currentGroupId, StringComparison.Ordinal)
                || targetThreshold <= currentThreshold)
            {
                return null;
            }

            DbHelper.Exec(conn, tx,
                "UPDATE DKHACHHANG SET DNHOMKHACHHANGID = @G, USERMODIFIEDID = @U, TIMEMODIFIED = @T WHERE ID = @C",
                "@G", targetId, "@U", actorId ?? "", "@T", now, "@C", customerId);
            return targetName;
        }
    }
}
