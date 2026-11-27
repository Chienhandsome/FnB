using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>Phần sinh số phiếu, kiểm trùng và ghi/sửa/xóa phiếu nhập kho cho <see cref="PurchaseRepository"/>.</summary>
    public sealed partial class PurchaseRepository
    {
        public string GenerateNumber(DateTime? forDate = null)
        {
            DateTime date = (forDate ?? DateTime.Today).Date;
            return DbHelper.Read(_databasePath, conn => GenerateNumberIn(conn, null, date));
        }

        public static string GenerateNumberIn(FbConnection conn, FbTransaction tx, DateTime date)
        {
            string template = PurchaseConstants.DefaultNumberTemplate;
            string prefix = DocumentNumber.LiteralPrefix(template, date);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TDONHANG WHERE NAME STARTING WITH @P",
                "@P", prefix);
            var names = new string[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
                names[i] = DbHelper.Str(dt.Rows[i]["NAME"]);
            return DocumentNumber.Format(template, date, DocumentNumber.NextSequence(names, prefix));
        }

        public static bool IsNumberTakenIn(FbConnection conn, FbTransaction tx, string number, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(number))
                return false;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID FROM TDONHANG WHERE NAME = @N AND COALESCE(STATUS, 0) <> @Del",
                "@N", number.Trim(), "@Del", DeletedStatus);
            foreach (DataRow r in dt.Rows)
            {
                if (!string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static string LoadProductNameIn(FbConnection conn, FbTransaction tx, string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return "";
            return DbHelper.Str(DbHelper.Scalar(conn, tx,
                "SELECT NAME FROM DMATHANG WHERE ID = @I", "@I", productId)) ?? "";
        }

        public static void InsertHeaderIn(FbConnection conn, FbTransaction tx, PurchaseRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANG (" +
                "ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHONHAPID, DCUAHANGID, DNHACUNGCAPID, DNHANVIENNHAPID, " +
                "DIENGIAI, NOTE, TIENHANG, TILEGIAMGIA, TIENGIAMGIA, GIAMTHEOTIEN, " +
                "TILETHUE, TIENTHUE, PHIVANCHUYEN, TONGCONG, " +
                "TIENTHANHTOAN, TIENMAT, CHUYENKHOAN, DTAIKHOANNGANHANGID, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @NAME, @NGAY, @LOAI, @ST, @PAID, @WH, @STORE, @SUPP, @EMP, " +
                "@DESC, @NOTE, @TH, @TLG, @TGG, @GTT, " +
                "@TLT, @TT, @PVC, @TC, " +
                "@TTT, @TM, @CK, @BANK, " +
                "@UC, @TCRE, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@LOAI", OrderType,
                "@ST", ActiveStatus,
                "@PAID", PurchaseConstants.PaidFlag,
                "@WH", NullIfEmpty(row.WarehouseId),
                "@STORE", NullIfEmpty(row.StoreId),
                "@SUPP", NullIfEmpty(row.SupplierId),
                "@EMP", NullIfEmpty(row.EmployeeId),
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
                "@TTT", row.PaidAmount,
                "@TM", row.CashAmount,
                "@CK", row.TransferAmount,
                "@BANK", NullIfEmpty(row.BankAccountId),
                "@UC", row.CreatedById ?? "",
                "@TCRE", row.CreatedAt ?? DbHelper.Now());
        }

        public static int UpdateHeaderIn(FbConnection conn, FbTransaction tx, PurchaseRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET " +
                "NAME = @NAME, NGAY = @NGAY, DKHONHAPID = @WH, DCUAHANGID = @STORE, " +
                "DNHACUNGCAPID = @SUPP, DNHANVIENNHAPID = @EMP, DIENGIAI = @DESC, NOTE = @NOTE, " +
                "TIENHANG = @TH, TILEGIAMGIA = @TLG, TIENGIAMGIA = @TGG, GIAMTHEOTIEN = @GTT, " +
                "TILETHUE = @TLT, TIENTHUE = @TT, PHIVANCHUYEN = @PVC, TONGCONG = @TC, " +
                "TIENTHANHTOAN = @TTT, TIENMAT = @TM, CHUYENKHOAN = @CK, DTAIKHOANNGANHANGID = @BANK, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TMOD " +
                "WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI AND COALESCE(STATUS, 0) <> @Del",
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@WH", NullIfEmpty(row.WarehouseId),
                "@STORE", NullIfEmpty(row.StoreId),
                "@SUPP", NullIfEmpty(row.SupplierId),
                "@EMP", NullIfEmpty(row.EmployeeId),
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
                "@TTT", row.PaidAmount,
                "@TM", row.CashAmount,
                "@CK", row.TransferAmount,
                "@BANK", NullIfEmpty(row.BankAccountId),
                "@UM", row.ModifiedById ?? "",
                "@TMOD", row.ModifiedAt ?? DbHelper.Now(),
                "@ID", row.Id,
                "@LOAI", OrderType,
                "@Del", DeletedStatus);
        }

        public static void DeleteLinesIn(FbConnection conn, FbTransaction tx, string orderId)
        {
            DbHelper.Exec(conn, tx, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @O", "@O", orderId);
        }

        public static void InsertLinesIn(FbConnection conn, FbTransaction tx, string orderId, List<PurchaseLine> lines, string actorId, DateTime baseTime)
        {
            if (lines == null)
                return;
            for (int i = 0; i < lines.Count; i++)
            {
                PurchaseLine l = lines[i];
                string lineId = DbHelper.NewId();
                l.Id = lineId;
                l.OrderId = orderId;
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANGCHITIET (" +
                    "ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, " +
                    "SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, " +
                    "DONGIA, TILEGIAMGIA, THANHTIEN, NOTE, XUATVATTU, STATUS, " +
                    "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                    "VALUES (" +
                    "@ID, @OID, @PID, @NAME, @UID, @WID, " +
                    "@SLN, @SLRAW, 0, 0, " +
                    "@PRICE, @DISC, @AMT, @NOTE, 0, @ST, " +
                    "@UC, @TC, NULL, NULL)",
                    "@ID", lineId,
                    "@OID", orderId,
                    "@PID", NullIfEmpty(l.ProductId),
                    "@NAME", l.ProductName ?? "",
                    "@UID", NullIfEmpty(l.UnitId),
                    "@WID", NullIfEmpty(l.WarehouseId),
                    "@SLN", l.ConvertedQuantity,
                    "@SLRAW", l.RawQuantity,
                    "@PRICE", l.UnitPrice,
                    "@DISC", l.DiscountPct,
                    "@AMT", l.Amount,
                    "@NOTE", NullIfEmpty(l.Note),
                    "@ST", ActiveStatus,
                    "@UC", actorId ?? "",
                    "@TC", baseTime.AddSeconds(i));
            }
        }

        public static int DeleteOrderIn(FbConnection conn, FbTransaction tx, string orderId)
        {
            DeleteLinesIn(conn, tx, orderId);
            return DbHelper.Exec(conn, tx,
                "DELETE FROM TDONHANG WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI",
                "@ID", orderId, "@LOAI", OrderType);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
