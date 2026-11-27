using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    public sealed partial class ServiceOrderRepository
    {
        // ------------------------------------------------------------------ Khóa bàn nhiều máy trạm (§3.2, §6.5)

        public static bool IsTableLockedByOtherIn(
            FbConnection conn,
            FbTransaction tx,
            string tableId,
            string currentUserName,
            out string lockedByAccount)
        {
            lockedByAccount = "";
            if (string.IsNullOrEmpty(tableId))
                return false;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT TAIKHOANGIU FROM DBAN WHERE ID = @I " +
                "AND TAIKHOANGIU IS NOT NULL AND TRIM(TAIKHOANGIU) <> '' " +
                "AND GIULUC > DATEADD(@Sec SECOND TO CURRENT_TIMESTAMP)",
                "@I", tableId, "@Sec", -ServiceOrderConstants.TableLockExpireSeconds);
            if (dt.Rows.Count == 0)
                return false;
            string holder = (DbHelper.Str(dt.Rows[0]["TAIKHOANGIU"]) ?? "").Trim();
            if (holder.Length == 0 || string.Equals(holder, (currentUserName ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
            lockedByAccount = holder;
            return true;
        }

        public bool IsTableLockedByOther(string tableId, string currentUserName, out string lockedByAccount)
        {
            string holder = "";
            bool locked = DbHelper.Read(_databasePath, conn => IsTableLockedByOtherIn(conn, null, tableId, currentUserName, out holder));
            lockedByAccount = holder;
            return locked;
        }

        /// <summary>Giữ/gia hạn khóa bàn (TAIKHOANGIU = tài khoản, GIULUC = CURRENT_TIMESTAMP, §6.5).</summary>
        public void LockTable(string tableId, string userName)
        {
            if (string.IsNullOrEmpty(tableId) || string.IsNullOrEmpty(userName))
                return;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                DbHelper.Exec(conn, tx,
                    "UPDATE DBAN SET TAIKHOANGIU = @U, GIULUC = CURRENT_TIMESTAMP WHERE ID = @I",
                    "@U", userName, "@I", tableId);
            });
        }

        /// <summary>Nhả khóa bàn (chỉ nhả nếu đang thuộc đúng tài khoản hiện tại).</summary>
        public void UnlockTable(string tableId, string userName)
        {
            if (string.IsNullOrEmpty(tableId))
                return;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                if (string.IsNullOrEmpty(userName))
                {
                    DbHelper.Exec(conn, tx, "UPDATE DBAN SET TAIKHOANGIU = NULL, GIULUC = NULL WHERE ID = @I", "@I", tableId);
                }
                else
                {
                    DbHelper.Exec(conn, tx,
                        "UPDATE DBAN SET TAIKHOANGIU = NULL, GIULUC = NULL WHERE ID = @I AND UPPER(COALESCE(TAIKHOANGIU, '')) = UPPER(@U)",
                        "@I", tableId, "@U", userName);
                }
            });
        }

        // ------------------------------------------------------------------ Kiểm tồn kho (§6.6) & In bếp (§3.5)

        /// <summary>
        /// Tồn kho dùng chung (phieu-xuat-chuyen-kho.md §4): SUM(SLNHAP - SLXUAT) của kho trên phiếu STATUS = 30
        /// với chứng từ LOAI &lt;&gt; 0 hoặc bill bán đã thanh toán (LOAI = 0 AND DATHANHTOAN = 30), loại trừ phiếu hiện tại.
        /// </summary>
        public static decimal GetStockBalanceIn(
            FbConnection conn,
            FbTransaction tx,
            string productId,
            string warehouseId,
            string excludeOrderId)
        {
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(warehouseId))
                return 0m;
            object v = DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(C.SLNHAP, 0) - COALESCE(C.SLXUAT, 0)), 0) " +
                "FROM TDONHANGCHITIET C INNER JOIN TDONHANG H ON H.ID = C.TDONHANGID " +
                "WHERE C.DMATHANGID = @P AND C.DKHOHANGID = @W " +
                "AND COALESCE(H.STATUS, 0) = 30 " +
                "AND (COALESCE(H.LOAI, 0) <> 0 OR (COALESCE(H.LOAI, 0) = 0 AND COALESCE(H.DATHANHTOAN, 0) = 30)) " +
                "AND H.ID <> @Ex",
                "@P", productId, "@W", warehouseId, "@Ex", excludeOrderId ?? "");
            return DbHelper.Dec(v);
        }

        public static decimal GetPrintedKitchenQtyIn(FbConnection conn, FbTransaction tx, string billId, string itemName)
        {
            if (string.IsNullOrEmpty(billId) || string.IsNullOrEmpty(itemName))
                return 0m;
            object v = DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(SUM(COALESCE(SOLUONG, 0)), 0) FROM TINCHEBIEN WHERE TDONHANGID = @B AND TENHANG = @N",
                "@B", billId, "@N", itemName);
            return DbHelper.Dec(v);
        }

        /// <summary>
        /// Kiểm tra bill còn mở trên bàn hay đã thanh toán (IsDaThanhToan §3.4):
        /// bill mất, bàn không còn trỏ vào bill, hoặc DATHANHTOAN = 30 -> false.
        /// </summary>
        public static bool IsBillEditableIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string tableId,
            out int tempPrintCount)
        {
            tempPrintCount = 0;
            if (string.IsNullOrEmpty(billId))
                return false;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT COALESCE(H.DATHANHTOAN, 0) AS PAID, COALESCE(H.SOLANINTAMTINH, 0) AS PRINTS, B.TDONHANGID AS TABLEBILL " +
                "FROM TDONHANG H LEFT JOIN DBAN B ON B.ID = COALESCE(NULLIF(CAST(@T AS VARCHAR(36)), ''), H.DBANID) " +
                "WHERE H.ID = @B",
                "@B", billId, "@T", tableId ?? "");
            if (dt.Rows.Count == 0)
                return false;
            int paid = DbHelper.Int(dt.Rows[0]["PAID"]);
            string tableBill = DbHelper.Str(dt.Rows[0]["TABLEBILL"]) ?? "";
            tempPrintCount = DbHelper.Int(dt.Rows[0]["PRINTS"]);
            return paid == 0 && string.Equals(tableBill, billId, StringComparison.Ordinal);
        }

        public static string ResolveDefaultStoreIdIn(FbConnection conn, FbTransaction tx, string preferredStoreId)
        {
            if (!string.IsNullOrEmpty(preferredStoreId))
                return preferredStoreId;
            return DbHelper.Str(DbHelper.Scalar(conn, tx,
                "SELECT FIRST 1 ID FROM DCUAHANG WHERE STATUS = @S ORDER BY NAME",
                "@S", ActiveStatus));
        }

        public static string ResolveWarehouseIdIn(FbConnection conn, FbTransaction tx, string areaWarehouseId)
        {
            if (!string.IsNullOrEmpty(areaWarehouseId))
                return areaWarehouseId;
            return DbHelper.Str(DbHelper.Scalar(conn, tx,
                "SELECT FIRST 1 ID FROM DKHOHANG WHERE STATUS = @S ORDER BY NAME",
                "@S", ActiveStatus));
        }

        /// <summary>Kiểm tra bàn đang có bill chưa thanh toán không (theo DBAN.TDONHANGID).</summary>
        public static bool HasActiveBillOnTableIn(FbConnection conn, FbTransaction tx, string tableId)
        {
            string existing = DbHelper.Str(DbHelper.Scalar(conn, tx,
                "SELECT H.ID FROM DBAN B INNER JOIN TDONHANG H ON H.ID = B.TDONHANGID " +
                "WHERE B.ID = @T AND COALESCE(H.DATHANHTOAN, 0) = 0",
                "@T", tableId));
            return !string.IsNullOrEmpty(existing);
        }

        /// <summary>Mặt hàng mặc định của khu vực (DMATHANGMACDINH, §3.3).</summary>
        public static List<AreaDefaultItem> LoadAreaDefaultItemsIn(FbConnection conn, FbTransaction tx, string areaId)
        {
            var list = new List<AreaDefaultItem>();
            if (string.IsNullOrEmpty(areaId))
                return list;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT DMATHANGID, COALESCE(SOLUONG, 0) AS SOLUONG FROM DMATHANGMACDINH " +
                "WHERE DKHUVUCID = @A AND STATUS = @S ORDER BY TIMECREATED, ID",
                "@A", areaId, "@S", ActiveStatus);
            foreach (DataRow r in dt.Rows)
            {
                string pid = DbHelper.Str(r["DMATHANGID"]);
                decimal qty = DbHelper.Dec(r["SOLUONG"]);
                if (!string.IsNullOrEmpty(pid) && qty > 0m)
                    list.Add(new AreaDefaultItem { ProductId = pid, Quantity = qty });
            }
            return list;
        }

        // ------------------------------------------------------------------ Ghi TDONHANG và TDONHANGCHITIET (một transaction)

        /// <summary>
        /// Ghi hóa đơn mới (TDONHANG). Theo P3 đã chốt: KHÔNG cập nhật trực tiếp DBAN.TDONHANGID —
        /// trigger TDONHANG_TRIGGER của Firebird tự động gắn DBAN.TDONHANGID = header.Id.
        /// </summary>
        public static void InsertBillHeaderIn(FbConnection conn, FbTransaction tx, ServiceOrderHeader h, string actorId, DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANG (" +
                "ID, NAME, SOHD, LOAI, STATUS, DATHANHTOAN, NGAY, BATDAU, BATDAUPHONGCUOI, " +
                "DBANID, DKHACHHANGID, DCUAHANGID, SOKHACH, TDATHANGID, NHOMGUID, NOTE, " +
                "TIENHANG, TIENHANGCHUAGIAM, GIAMGIAMATHANG, " +
                "TILEGIAMGIA, TIENGIAMGIA, GIAMTHEOTIEN, TILEGIAMGIATONG, TIENGIAMGIATONG, " +
                "TILEGIAMGIAGIO, TIENGIAMGIAGIO, TIENGIO, " +
                "TILEPHIDICHVU, PHIDICHVU, PHIDICHVUTHEOTIEN, " +
                "TILETHUE, TIENTHUE, TONGCONG, SOLANINTAMTINH, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @NAME, @SOHD, 0, @ST, 0, @NGAY, @BATDAU, @BATDAU, " +
                "@DBANID, @DKHACHHANGID, @DCUAHANGID, @SOKHACH, @TDATHANGID, @NHOMGUID, @NOTE, " +
                "@TIENHANG, @TIENHANGCHUAGIAM, @GIAMGIAMATHANG, " +
                "@TILEGIAMGIA, @TIENGIAMGIA, @GIAMTHEOTIEN, @TILEGIAMGIA, @TIENGIAMGIA, " +
                "0, 0, 0, " +
                "@TILEPHIDICHVU, @PHIDICHVU, @PHIDICHVUTHEOTIEN, " +
                "@TILETHUE, @TIENTHUE, @TONGCONG, 0, " +
                "@U, @T, NULL, NULL)",
                "@ID", h.Id,
                "@NAME", h.Name,
                "@SOHD", h.SoHd,
                "@ST", ActiveStatus,
                "@NGAY", h.Ngay.Date,
                "@BATDAU", h.BatDau,
                "@DBANID", h.TableId,
                "@DKHACHHANGID", string.IsNullOrEmpty(h.CustomerId) ? null : h.CustomerId,
                "@DCUAHANGID", string.IsNullOrEmpty(h.StoreId) ? null : h.StoreId,
                "@SOKHACH", Math.Max(1, h.GuestCount),
                "@TDATHANGID", string.IsNullOrEmpty(h.BookingId) ? null : h.BookingId,
                "@NHOMGUID", string.IsNullOrEmpty(h.GroupGuid) ? null : h.GroupGuid,
                "@NOTE", string.IsNullOrEmpty(h.Note) ? null : h.Note,
                "@TIENHANG", h.TienHang,
                "@TIENHANGCHUAGIAM", h.TienHangChuaGiam,
                "@GIAMGIAMATHANG", h.GiamGiaMatHang,
                "@TILEGIAMGIA", h.TileGiamGia,
                "@TIENGIAMGIA", h.TienGiamGia,
                "@GIAMTHEOTIEN", h.GiamTheoTien ? ActiveStatus : 0,
                "@TILEPHIDICHVU", h.TilePhiDichVu,
                "@PHIDICHVU", h.PhiDichVu,
                "@PHIDICHVUTHEOTIEN", h.PhiDichVuTheoTien ? ActiveStatus : 0,
                "@TILETHUE", h.TileThue,
                "@TIENTHUE", h.TienThue,
                "@TONGCONG", h.TongCong,
                "@U", actorId ?? "",
                "@T", now);
        }

        public static void UpdateBillTotalsAndMetaIn(FbConnection conn, FbTransaction tx, ServiceOrderHeader h, string actorId, DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET " +
                "DKHACHHANGID = @DKHACHHANGID, SOKHACH = @SOKHACH, NOTE = @NOTE, " +
                "TIENHANG = @TIENHANG, TIENHANGCHUAGIAM = @TIENHANGCHUAGIAM, GIAMGIAMATHANG = @GIAMGIAMATHANG, " +
                "TILEGIAMGIA = @TILEGIAMGIA, TIENGIAMGIA = @TIENGIAMGIA, GIAMTHEOTIEN = @GIAMTHEOTIEN, " +
                "TILEGIAMGIATONG = @TILEGIAMGIA, TIENGIAMGIATONG = @TIENGIAMGIA, " +
                "TILEPHIDICHVU = @TILEPHIDICHVU, PHIDICHVU = @PHIDICHVU, PHIDICHVUTHEOTIEN = @PHIDICHVUTHEOTIEN, " +
                "TILETHUE = @TILETHUE, TIENTHUE = @TIENTHUE, TONGCONG = @TONGCONG, " +
                "USERMODIFIEDID = @U, TIMEMODIFIED = @T " +
                "WHERE ID = @ID AND COALESCE(DATHANHTOAN, 0) = 0",
                "@DKHACHHANGID", string.IsNullOrEmpty(h.CustomerId) ? null : h.CustomerId,
                "@SOKHACH", Math.Max(1, h.GuestCount),
                "@NOTE", string.IsNullOrEmpty(h.Note) ? null : h.Note,
                "@TIENHANG", h.TienHang,
                "@TIENHANGCHUAGIAM", h.TienHangChuaGiam,
                "@GIAMGIAMATHANG", h.GiamGiaMatHang,
                "@TILEGIAMGIA", h.TileGiamGia,
                "@TIENGIAMGIA", h.TienGiamGia,
                "@GIAMTHEOTIEN", h.GiamTheoTien ? ActiveStatus : 0,
                "@TILEPHIDICHVU", h.TilePhiDichVu,
                "@PHIDICHVU", h.PhiDichVu,
                "@PHIDICHVUTHEOTIEN", h.PhiDichVuTheoTien ? ActiveStatus : 0,
                "@TILETHUE", h.TileThue,
                "@TIENTHUE", h.TienThue,
                "@TONGCONG", h.TongCong,
                "@U", actorId ?? "",
                "@T", now,
                "@ID", h.Id);
        }

        public static void InsertLineIn(FbConnection conn, FbTransaction tx, ServiceOrderLine l, string actorId, DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANGCHITIET (" +
                "ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, " +
                "SLXUAT, SLXUATCHUAQUYDOI, SLNHAP, SLNHAPCHUAQUYDOI, " +
                "DONGIA, GIAVON, TILEGIAMGIA, TIENGIAMGIA, GIAMTHEOTIEN, THANHTIEN, " +
                "NOTE, COMBOID, COMBOPARENTID, COMBOSL, XUATVATTU, STATUS, " +
                "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (" +
                "@ID, @OID, @PID, @NAME, @UID, @WID, " +
                "@QTY, @QTY, 0, 0, " +
                "@PRICE, @COST, @DRATE, @DAMT, @DMONEY, @AMT, " +
                "@NOTE, @CID, @CPID, @CQTY, 0, @ST, " +
                "@U, @T, NULL, NULL)",
                "@ID", l.Id,
                "@OID", l.OrderId,
                "@PID", string.IsNullOrEmpty(l.ProductId) ? null : l.ProductId,
                "@NAME", l.ItemName ?? "",
                "@UID", string.IsNullOrEmpty(l.UnitId) ? null : l.UnitId,
                "@WID", string.IsNullOrEmpty(l.WarehouseId) ? null : l.WarehouseId,
                "@QTY", l.Quantity,
                "@PRICE", l.UnitPrice,
                "@COST", l.CostPrice,
                "@DRATE", l.DiscountRate,
                "@DAMT", l.DiscountAmount,
                "@DMONEY", l.DiscountByMoney ? ActiveStatus : 0,
                "@AMT", l.Amount,
                "@NOTE", string.IsNullOrEmpty(l.Note) ? null : l.Note,
                "@CID", string.IsNullOrEmpty(l.ComboId) ? null : l.ComboId,
                "@CPID", string.IsNullOrEmpty(l.ComboParentId) ? null : l.ComboParentId,
                "@CQTY", l.ComboQty,
                "@ST", ActiveStatus,
                "@U", actorId ?? "",
                "@T", now);
        }

        public static void UpdateLineIn(FbConnection conn, FbTransaction tx, ServiceOrderLine l, string actorId, DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "UPDATE TDONHANGCHITIET SET " +
                "SLXUAT = @QTY, SLXUATCHUAQUYDOI = @QTY, DONGIA = @PRICE, " +
                "TILEGIAMGIA = @DRATE, TIENGIAMGIA = @DAMT, GIAMTHEOTIEN = @DMONEY, " +
                "THANHTIEN = @AMT, NOTE = @NOTE, USERMODIFIEDID = @U, TIMEMODIFIED = @T " +
                "WHERE ID = @ID",
                "@QTY", l.Quantity,
                "@PRICE", l.UnitPrice,
                "@DRATE", l.DiscountRate,
                "@DAMT", l.DiscountAmount,
                "@DMONEY", l.DiscountByMoney ? ActiveStatus : 0,
                "@AMT", l.Amount,
                "@NOTE", string.IsNullOrEmpty(l.Note) ? null : l.Note,
                "@U", actorId ?? "",
                "@T", now,
                "@ID", l.Id);
        }

        public static void UpdateComboChildrenQtyIn(FbConnection conn, FbTransaction tx, string billId, string comboId, decimal parentQty, string actorId, DateTime now)
        {
            if (string.IsNullOrEmpty(comboId))
                return;
            DbHelper.Exec(conn, tx,
                "UPDATE TDONHANGCHITIET SET SLXUAT = COMBOSL * @Q, SLXUATCHUAQUYDOI = COMBOSL * @Q, " +
                "USERMODIFIEDID = @U, TIMEMODIFIED = @T " +
                "WHERE TDONHANGID = @B AND COMBOPARENTID = @C",
                "@Q", parentQty, "@U", actorId ?? "", "@T", now, "@B", billId, "@C", comboId);
        }

        public static void DeleteLineAndComboChildrenIn(FbConnection conn, FbTransaction tx, string billId, string lineId, string comboId)
        {
            if (!string.IsNullOrEmpty(comboId))
            {
                DbHelper.Exec(conn, tx,
                    "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @B AND COMBOPARENTID = @C",
                    "@B", billId, "@C", comboId);
            }
            DbHelper.Exec(conn, tx,
                "DELETE FROM TDONHANGCHITIET WHERE ID = @I AND TDONHANGID = @B",
                "@I", lineId, "@B", billId);
        }

        /// <summary>Chạy một khối công việc ghi DB trong một transaction duy nhất (P1).</summary>
        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public List<LookupItem> LoadUnits()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DDONVITINH WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }
    }
}
