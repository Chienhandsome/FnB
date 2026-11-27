using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>Phần sinh số phiếu (KKyy/*****) và ghi/sửa/xóa phiếu Kiểm kê kho (TDONHANG LOAI = 4).</summary>
    public sealed partial class StockTakeRepository
    {
        public string GenerateNumber(DateTime? forDate = null)
        {
            DateTime date = (forDate ?? DateTime.Today).Date;
            return DbHelper.Read(_databasePath, conn => GenerateNumberIn(conn, null, date));
        }

        public static string GenerateNumberIn(FbConnection conn, FbTransaction tx, DateTime date)
        {
            string template = StockTakeConstants.DefaultNumberTemplate;
            string prefix = DocumentNumber.LiteralPrefix(template, date);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TDONHANG WHERE NAME STARTING WITH @P",
                "@P", prefix);
            var names = new string[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
                names[i] = DbHelper.Str(dt.Rows[i]["NAME"]);
            return DocumentNumber.Format(template, date, DocumentNumber.NextSequence(names, prefix));
        }

        public static void InsertHeaderIn(FbConnection conn, FbTransaction tx, StockTakeRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANG (" +
                "ID, NAME, NGAY, LOAI, STATUS, DATHANHTOAN, DKHONHAPID, DCUAHANGID, " +
                "DIENGIAI, NOTE, " +
                "TIENHANG, TILEGIAMGIA, TIENGIAMGIA, GIAMTHEOTIEN, " +
                "TILETHUE, TIENTHUE, PHIVANCHUYEN, TONGCONG, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @NAME, @NGAY, @LOAI, @ST, @PAID, @WH, @STORE, " +
                "@DESC, @NOTE, " +
                "0, 0, 0, 0, " +
                "0, 0, 0, 0, " +
                "@UC, @TCRE, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@LOAI", OrderType,
                "@ST", ActiveStatus,
                "@PAID", StockTakeConstants.PaidFlag,
                "@WH", NullIfEmpty(row.WarehouseId),
                "@STORE", NullIfEmpty(row.StoreId),
                "@DESC", NullIfEmpty(row.Description),
                "@NOTE", NullIfEmpty(row.Note),
                "@UC", row.CreatedById ?? "",
                "@TCRE", row.CreatedAt ?? DbHelper.Now());
        }

        public static int UpdateHeaderIn(FbConnection conn, FbTransaction tx, StockTakeRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET " +
                "NAME = @NAME, NGAY = @NGAY, DKHONHAPID = @WH, DCUAHANGID = @STORE, " +
                "DIENGIAI = @DESC, NOTE = @NOTE, " +
                "TIENHANG = 0, TILEGIAMGIA = 0, TIENGIAMGIA = 0, GIAMTHEOTIEN = 0, " +
                "TILETHUE = 0, TIENTHUE = 0, PHIVANCHUYEN = 0, TONGCONG = 0, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TMOD " +
                "WHERE ID = @ID AND COALESCE(LOAI, 0) = @LOAI AND COALESCE(STATUS, 0) <> @Del",
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@WH", NullIfEmpty(row.WarehouseId),
                "@STORE", NullIfEmpty(row.StoreId),
                "@DESC", NullIfEmpty(row.Description),
                "@NOTE", NullIfEmpty(row.Note),
                "@UM", row.ModifiedById ?? "",
                "@TMOD", row.ModifiedAt ?? DbHelper.Now(),
                "@ID", row.Id,
                "@LOAI", OrderType,
                "@Del", DeletedStatus);
        }

        /// <summary>
        /// Ghi chi tiết phiếu Kiểm kê (spec §3):
        /// - SLHETHONG (tồn hệ thống), SLTHUCTE (thực tế đã quy đổi về ĐVT cơ sở), DKHOHANGID = kho kiểm kê.
        /// - SLNHAP = SLNHAPCHUAQUYDOI = max(0, SLTHUCTE - SLHETHONG).
        /// - SLXUAT = SLXUATCHUAQUYDOI = max(0, SLHETHONG - SLTHUCTE).
        /// - Dòng chênh lệch = 0 vẫn ghi đầy đủ để biết đã kiểm.
        /// </summary>
        public static void InsertLinesIn(
            FbConnection conn,
            FbTransaction tx,
            string orderId,
            string warehouseId,
            List<StockTakeLine> lines,
            string actorId,
            DateTime baseTime)
        {
            if (lines == null)
                return;
            for (int i = 0; i < lines.Count; i++)
            {
                StockTakeLine l = lines[i];
                string lineId = DbHelper.NewId();
                l.Id = lineId;
                l.OrderId = orderId;
                l.WarehouseId = warehouseId;
                string persistedUnitId = !string.IsNullOrEmpty(l.BaseUnitId) ? l.BaseUnitId : l.UnitId;

                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANGCHITIET (" +
                    "ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, " +
                    "SLHETHONG, SLTHUCTE, " +
                    "SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, " +
                    "DONGIA, TILEGIAMGIA, THANHTIEN, NOTE, XUATVATTU, STATUS, " +
                    "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                    "VALUES (" +
                    "@ID, @OID, @PID, @NAME, @UID, @WID, " +
                    "@SLHT, @SLTT, " +
                    "@SLN, @SLN, @SLX, @SLX, " +
                    "0, 0, 0, @NOTE, 0, @ST, " +
                    "@UC, @TC, NULL, NULL)",
                    "@ID", lineId,
                    "@OID", orderId,
                    "@PID", NullIfEmpty(l.ProductId),
                    "@NAME", l.ProductName ?? "",
                    "@UID", NullIfEmpty(persistedUnitId),
                    "@WID", NullIfEmpty(warehouseId),
                    "@SLHT", l.SystemQuantity,
                    "@SLTT", l.ActualQuantityBase,
                    "@SLN", l.ImportQty,
                    "@SLX", l.ExportQty,
                    "@NOTE", NullIfEmpty(l.Note),
                    "@ST", ActiveStatus,
                    "@UC", actorId ?? "",
                    "@TC", baseTime.AddSeconds(i));
            }
        }

        public static int DeleteOrderIn(FbConnection conn, FbTransaction tx, string orderId)
        {
            PurchaseRepository.DeleteLinesIn(conn, tx, orderId);
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
