using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>Phần sinh số phiếu và ghi/sửa/xóa phiếu Xuất khác (LOAI = 2) và Chuyển kho (LOAI = 3).</summary>
    public sealed partial class StockDocumentRepository
    {
        public string GenerateNumber(StockDocumentKind kind, DateTime? forDate = null)
        {
            DateTime date = (forDate ?? DateTime.Today).Date;
            return DbHelper.Read(_databasePath, conn => GenerateNumberIn(conn, null, kind, date));
        }

        public static string GenerateNumberIn(FbConnection conn, FbTransaction tx, StockDocumentKind kind, DateTime date)
        {
            string template = StockDocumentConstants.NumberTemplateOf(kind);
            string prefix = DocumentNumber.LiteralPrefix(template, date);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TDONHANG WHERE NAME STARTING WITH @P",
                "@P", prefix);
            var names = new string[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
                names[i] = DbHelper.Str(dt.Rows[i]["NAME"]);
            return DocumentNumber.Format(template, date, DocumentNumber.NextSequence(names, prefix));
        }

        public static void InsertHeaderIn(FbConnection conn, FbTransaction tx, StockDocumentRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANG (" +
                "ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHOXUATID, DKHONHAPID, DCUAHANGID, " +
                "DNHACUNGCAPID, DNHANVIENXUATID, DNHANVIENNHAPID, DIENGIAI, NOTE, " +
                "TIENHANG, TILEGIAMGIA, TIENGIAMGIA, GIAMTHEOTIEN, " +
                "TILETHUE, TIENTHUE, PHIVANCHUYEN, TONGCONG, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @NAME, @NGAY, @LOAI, @ST, @PAID, @SWH, @DWH, @STORE, " +
                "@SUPP, @EXPE, @IMPE, @DESC, @NOTE, " +
                "@TH, @TLG, @TGG, @GTT, " +
                "@TLT, @TT, @PVC, @TC, " +
                "@UC, @TCRE, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@LOAI", row.Loai,
                "@ST", ActiveStatus,
                "@PAID", StockDocumentConstants.PaidFlag,
                "@SWH", NullIfEmpty(row.SourceWarehouseId),
                "@DWH", NullIfEmpty(row.DestWarehouseId),
                "@STORE", NullIfEmpty(row.StoreId),
                "@SUPP", NullIfEmpty(row.SupplierId),
                "@EXPE", NullIfEmpty(row.ExportEmployeeId),
                "@IMPE", NullIfEmpty(row.ImportEmployeeId),
                "@DESC", NullIfEmpty(row.Description),
                "@NOTE", NullIfEmpty(row.Note),
                "@TH", row.TienHang,
                "@TLG", row.TileGiamGia,
                "@TGG", row.TienGiamGia,
                "@GTT", row.GiamTheoTien ? ActiveStatus : 0,
                "@TLT", row.TileThue,
                "@TT", row.TienThue,
                "@PVC", row.PhiVanChuyen,
                "@TC", row.TongCong,
                "@UC", row.CreatedById ?? "",
                "@TCRE", row.CreatedAt ?? DbHelper.Now());
        }

        public static int UpdateHeaderIn(FbConnection conn, FbTransaction tx, StockDocumentRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET " +
                "NAME = @NAME, NGAY = @NGAY, DKHOXUATID = @SWH, DKHONHAPID = @DWH, DCUAHANGID = @STORE, " +
                "DNHACUNGCAPID = @SUPP, DNHANVIENXUATID = @EXPE, DNHANVIENNHAPID = @IMPE, " +
                "DIENGIAI = @DESC, NOTE = @NOTE, " +
                "TIENHANG = @TH, TILEGIAMGIA = @TLG, TIENGIAMGIA = @TGG, GIAMTHEOTIEN = @GTT, " +
                "TILETHUE = @TLT, TIENTHUE = @TT, PHIVANCHUYEN = @PVC, TONGCONG = @TC, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TMOD " +
                "WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI AND COALESCE(STATUS, 0) <> @Del",
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@SWH", NullIfEmpty(row.SourceWarehouseId),
                "@DWH", NullIfEmpty(row.DestWarehouseId),
                "@STORE", NullIfEmpty(row.StoreId),
                "@SUPP", NullIfEmpty(row.SupplierId),
                "@EXPE", NullIfEmpty(row.ExportEmployeeId),
                "@IMPE", NullIfEmpty(row.ImportEmployeeId),
                "@DESC", NullIfEmpty(row.Description),
                "@NOTE", NullIfEmpty(row.Note),
                "@TH", row.TienHang,
                "@TLG", row.TileGiamGia,
                "@TGG", row.TienGiamGia,
                "@GTT", row.GiamTheoTien ? ActiveStatus : 0,
                "@TLT", row.TileThue,
                "@TT", row.TienThue,
                "@PVC", row.PhiVanChuyen,
                "@TC", row.TongCong,
                "@UM", row.ModifiedById ?? "",
                "@TMOD", row.ModifiedAt ?? DbHelper.Now(),
                "@ID", row.Id,
                "@LOAI", row.Loai,
                "@Del", DeletedStatus);
        }

        /// <summary>Ghi chi tiết phiếu Xuất khác (LOAI = 2): SLXUAT &gt; 0, SLNHAP = 0, DKHOHANGID = kho xuất.</summary>
        public static void InsertExportLinesIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            string sourceWarehouseId,
            List<StockDocumentLine> lines,
            string actorId,
            DateTime baseTime)
        {
            if (lines == null)
                return;
            for (int i = 0; i < lines.Count; i++)
            {
                StockDocumentLine l = lines[i];
                string lineId = DbHelper.NewId();
                l.Id = lineId;
                l.OrderId = orderId;
                l.WarehouseId = sourceWarehouseId;
                InsertSingleLineIn(conn, tx, lineId, orderId, l, sourceWarehouseId,
                    0m, 0m, l.ConvertedQuantity, l.RawQuantity, actorId, baseTime.AddSeconds(i));
            }
        }

        /// <summary>
        /// Ghi chi tiết phiếu Chuyển kho (LOAI = 3, spec §3):
        /// mỗi dòng người dùng nhập tạo 1 dòng nhập (SLNHAP, DKHOHANGID = kho nhập)
        /// và 1 dòng xuất đối ứng (SLXUAT = SLNHAP, SLXUATCHUAQUYDOI = SLNHAPCHUAQUYDOI, DKHOHANGID = kho xuất).
        /// </summary>
        public static void InsertTransferLinesIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            string destWarehouseId,
            string sourceWarehouseId,
            List<StockDocumentLine> lines,
            string actorId,
            DateTime baseTime)
        {
            if (lines == null)
                return;
            for (int i = 0; i < lines.Count; i++)
            {
                StockDocumentLine l = lines[i];
                string importLineId = DbHelper.NewId();
                l.Id = importLineId;
                l.OrderId = orderId;
                l.WarehouseId = destWarehouseId;
                InsertSingleLineIn(conn, tx, importLineId, orderId, l, destWarehouseId,
                    l.ConvertedQuantity, l.RawQuantity, 0m, 0m, actorId, baseTime.AddSeconds(i * 2));

                string mirrorExportId = DbHelper.NewId();
                InsertSingleLineIn(conn, tx, mirrorExportId, orderId, l, sourceWarehouseId,
                    0m, 0m, l.ConvertedQuantity, l.RawQuantity, actorId, baseTime.AddSeconds(i * 2 + 1));
            }
        }

        private static void InsertSingleLineIn(
            FbConnection conn,
            FbTransaction tx,
            string lineId,
            string orderId,
            StockDocumentLine l,
            string warehouseId,
            decimal slNhap,
            decimal slNhapRaw,
            decimal slXuat,
            decimal slXuatRaw,
            string actorId,
            DateTime timeCreated)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANGCHITIET (" +
                "ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, " +
                "SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, " +
                "DONGIA, TILEGIAMGIA, THANHTIEN, NOTE, XUATVATTU, STATUS, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @OID, @PID, @NAME, @UID, @WID, " +
                "@SLN, @SLNRAW, @SLX, @SLXRAW, " +
                "@PRICE, 0, @AMT, @NOTE, 0, @ST, " +
                "@UC, @TC, NULL, NULL)",
                "@ID", lineId,
                "@OID", orderId,
                "@PID", NullIfEmpty(l.ProductId),
                "@NAME", l.ProductName ?? "",
                "@UID", NullIfEmpty(l.UnitId),
                "@WID", NullIfEmpty(warehouseId),
                "@SLN", slNhap,
                "@SLNRAW", slNhapRaw,
                "@SLX", slXuat,
                "@SLXRAW", slXuatRaw,
                "@PRICE", l.UnitPrice,
                "@AMT", l.Amount,
                "@NOTE", NullIfEmpty(l.Note),
                "@ST", ActiveStatus,
                "@UC", actorId ?? "",
                "@TC", timeCreated);
        }

        public static int DeleteOrderIn(FbConnection conn, FbTransaction tx, string orderId, StockDocumentKind kind)
        {
            PurchaseRepository.DeleteLinesIn(conn, tx, orderId);
            return DbHelper.Exec(conn, tx,
                "DELETE FROM TDONHANG WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI",
                "@ID", orderId, "@LOAI", StockDocumentConstants.OrderTypeOf(kind));
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
