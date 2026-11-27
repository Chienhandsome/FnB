using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn đọc/ghi cho các thao tác Chuyển bàn, Gộp bàn, Chuyển mặt hàng và Hủy bill
    /// (docs/specs/su-dung-dich-vu.md §3.8, docs/specs/danh-muc-hoa-don-huy.md).
    /// Mọi câu SQL đều tham số hóa; không cập nhật trực tiếp DBAN.TDONHANGID (giữ trigger TDONHANG_TRIGGER theo P3).
    /// </summary>
    public sealed class ServiceTransferRepository
    {
        private const int ActiveStatus = 30;
        private const int DeletedStatus = -30;
        private readonly string _databasePath;

        public ServiceTransferRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Đọc bàn đang hoạt động (STATUS = 30) kèm khu vực trong transaction hiện tại.</summary>
        public static ServiceTableInfo LoadTableIn(FbConnection conn, FbTransaction tx, string tableId)
        {
            if (string.IsNullOrEmpty(tableId))
                return null;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT B.ID, B.NAME, B.DKHUVUCID, K.NAME AS AREANAME, K.DKHOHANGID AS AREAKHO, " +
                "K.DGIAMATHANGID AS AREAPRICE, H.ID AS BILLID, COALESCE(H.SOLANINTAMTINH, 0) AS SOLANIN " +
                "FROM DBAN B " +
                "INNER JOIN DKHUVUC K ON K.ID = B.DKHUVUCID AND K.STATUS = @S " +
                "LEFT JOIN TDONHANG H ON H.ID = B.TDONHANGID AND COALESCE(H.DATHANHTOAN, 0) = 0 " +
                "WHERE B.ID = @I AND B.STATUS = @S",
                "@I", tableId, "@S", ActiveStatus);

            if (dt.Rows.Count == 0)
                return null;

            DataRow r = dt.Rows[0];
            return new ServiceTableInfo
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                AreaId = DbHelper.Str(r["DKHUVUCID"]) ?? "",
                AreaName = DbHelper.Str(r["AREANAME"]) ?? "",
                AreaWarehouseId = DbHelper.Str(r["AREAKHO"]),
                AreaPriceLevelId = DbHelper.Str(r["AREAPRICE"]) ?? "0",
                ActiveBillId = DbHelper.Str(r["BILLID"]),
                TempPrintCount = DbHelper.Int(r["SOLANIN"])
            };
        }

        /// <summary>
        /// Kiểm tra bill có phiếu thu tạm ứng còn hiệu lực (TTHUCHI.TDONHANGID = billId, LOAI = 0, THU &gt; 0, STATUS &lt;&gt; -30).
        /// Dùng để chặn gộp bàn (Q19) và chặn hủy bill (Q20); phiếu đã xóa mềm (STATUS = -30) không tính.
        /// </summary>
        public static bool HasActiveAdvanceVoucherIn(FbConnection conn, FbTransaction tx, string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return false;
            int count = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM TTHUCHI " +
                "WHERE TDONHANGID = @B AND COALESCE(LOAI, 0) = 0 " +
                "AND COALESCE(THU, 0) > 0 AND COALESCE(STATUS, 0) <> @Del",
                "@B", billId, "@Del", DeletedStatus));
            return count > 0;
        }

        /// <summary>
        /// Danh sách số phiếu thu tiền cọc đặt trước còn hiệu lực gắn với phiếu đặt hàng
        /// (TTHUCHI.TDATHANGID = bookingId, LOAI = 0, THU &gt; 0, STATUS &lt;&gt; -30).
        /// Dùng để chặn hủy hóa đơn mở từ phiếu đặt còn cọc (A1); phiếu đã xóa mềm (STATUS = -30) không tính.
        /// </summary>
        public static List<string> GetActiveBookingDepositNumbersIn(FbConnection conn, FbTransaction tx, string bookingId)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(bookingId))
                return result;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TTHUCHI " +
                "WHERE TDATHANGID = @BK AND COALESCE(LOAI, 0) = 0 " +
                "AND COALESCE(THU, 0) > 0 AND COALESCE(STATUS, 0) <> @Del " +
                "ORDER BY NGAY, TIMECREATED, NAME",
                "@BK", bookingId.Trim(), "@Del", DeletedStatus);

            foreach (DataRow r in dt.Rows)
            {
                string name = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                result.Add(name.Length > 0 ? name : "(không số)");
            }
            return result;
        }

        /// <summary>
        /// Đổi bàn của hóa đơn (TDONHANG.DBANID). Không cập nhật trực tiếp DBAN.TDONHANGID —
        /// trigger TDONHANG_TRIGGER tự đặt bàn cũ = NULL và bàn mới = billId (P3).
        /// Giữ nguyên NHOMGUID và TDATHANGID.
        /// </summary>
        public static int UpdateBillTableIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string targetTableId,
            string actorId,
            DateTime now)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET DBANID = @T, USERMODIFIEDID = @U, TIMEMODIFIED = @Now " +
                "WHERE ID = @B AND COALESCE(DATHANHTOAN, 0) = 0",
                "@T", targetTableId, "@U", actorId ?? "", "@Now", now, "@B", billId);
        }

        /// <summary>Nhả khóa bàn nguồn và (nếu bật nhiều máy trạm) giữ khóa bàn đích trong cùng transaction.</summary>
        public static void TransferTableLockIn(
            FbConnection conn,
            FbTransaction tx,
            string fromTableId,
            string toTableId,
            string userName,
            bool acquireTargetLock)
        {
            if (!string.IsNullOrEmpty(fromTableId))
            {
                DbHelper.Exec(conn, tx,
                    "UPDATE DBAN SET TAIKHOANGIU = NULL, GIULUC = NULL WHERE ID = @I",
                    "@I", fromTableId);
            }
            if (acquireTargetLock && !string.IsNullOrEmpty(toTableId) && !string.IsNullOrEmpty(userName))
            {
                DbHelper.Exec(conn, tx,
                    "UPDATE DBAN SET TAIKHOANGIU = @U, GIULUC = CURRENT_TIMESTAMP WHERE ID = @I",
                    "@U", userName, "@I", toTableId);
            }
        }

        /// <summary>Chuyển toàn bộ dòng TDONHANGCHITIET và TINCHEBIEN từ bill nguồn sang bill đích khi gộp bàn.</summary>
        public static void MoveAllLinesAndKitchenIn(
            FbConnection conn,
            FbTransaction tx,
            string sourceBillId,
            string targetBillId,
            string actorId,
            DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "UPDATE TDONHANGCHITIET SET TDONHANGID = @Dst, USERMODIFIEDID = @U, TIMEMODIFIED = @Now " +
                "WHERE TDONHANGID = @Src",
                "@Dst", targetBillId, "@U", actorId ?? "", "@Now", now, "@Src", sourceBillId);

            DbHelper.Exec(conn, tx,
                "UPDATE TINCHEBIEN SET TDONHANGID = @Dst WHERE TDONHANGID = @Src",
                "@Dst", targetBillId, "@Src", sourceBillId);
        }

        /// <summary>Chuyển nguyên một dòng (và các dòng con combo của nó nếu có) sang bill đích.</summary>
        public static void MoveSingleLineAndComboChildrenIn(
            FbConnection conn,
            FbTransaction tx,
            string sourceBillId,
            string targetBillId,
            string lineId,
            string comboId,
            string actorId,
            DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "UPDATE TDONHANGCHITIET SET TDONHANGID = @Dst, USERMODIFIEDID = @U, TIMEMODIFIED = @Now " +
                "WHERE ID = @L AND TDONHANGID = @Src",
                "@Dst", targetBillId, "@U", actorId ?? "", "@Now", now, "@L", lineId, "@Src", sourceBillId);

            if (!string.IsNullOrEmpty(comboId))
            {
                DbHelper.Exec(conn, tx,
                    "UPDATE TDONHANGCHITIET SET TDONHANGID = @Dst, USERMODIFIEDID = @U, TIMEMODIFIED = @Now " +
                    "WHERE TDONHANGID = @Src AND COMBOPARENTID = @C",
                    "@Dst", targetBillId, "@U", actorId ?? "", "@Now", now, "@Src", sourceBillId, "@C", comboId);
            }
        }

        /// <summary>Ghi dòng điều chỉnh in chế biến (TINCHEBIEN, SOLUONG dương ở bill đích và âm ở bill nguồn).</summary>
        public static void InsertKitchenPrintAdjustmentIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string itemName,
            decimal qtyDelta,
            string note,
            string unitId,
            string drinkTypeId,
            decimal unitPrice,
            string actorId,
            DateTime now)
        {
            if (qtyDelta == 0m || string.IsNullOrEmpty(billId) || string.IsNullOrEmpty(itemName))
                return;

            DbHelper.Exec(conn, tx,
                "INSERT INTO TINCHEBIEN (" +
                "ID, TDONHANGID, TENHANG, SOLUONG, NOTE, DDONVITINHID, DLOAIDOID, DONGIA, " +
                "STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@ID, @B, @TEN, @SL, @NOTE, @UID, @DL, @DG, @ST, @U, @T)",
                "@ID", DbHelper.NewId(),
                "@B", billId,
                "@TEN", itemName,
                "@SL", qtyDelta,
                "@NOTE", string.IsNullOrEmpty(note) ? null : note,
                "@UID", string.IsNullOrEmpty(unitId) ? null : unitId,
                "@DL", string.IsNullOrEmpty(drinkTypeId) ? "1" : drinkTypeId,
                "@DG", unitPrice,
                "@ST", ActiveStatus,
                "@U", actorId ?? "",
                "@T", now);
        }

        /// <summary>
        /// Ghi bản lưu hóa đơn hủy vào TDONHANGHUY + TDONHANGHUYCHITIET (LuuBillHuy, spec §3.8 &amp; danh-muc-hoa-don-huy.md):
        /// - BAN chụp tên bàn lúc hủy; TIENGIAMGIA ghi đúng số tiền giảm (P4); THANHTOANBOI lấy từ SUSER hoặc rỗng.
        /// - TDONHANGHUYCHITIET CHỈ lưu các dòng bán chính: loại bỏ dòng xuất vật tư (XUATVATTU = 30) và dòng con combo (COMBOPARENTID &lt;&gt; '').
        /// </summary>
        public static string InsertCancelledBillSnapshotIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string fallbackTableName,
            string reason,
            string cashierName,
            string actorId,
            DateTime serverNow)
        {
            DataTable hdrDt = DbHelper.Table(conn, tx,
                "SELECT H.ID, H.NAME, H.NOTE, H.NGAY, " +
                "COALESCE(K.NAME, '') AS KHACHHANG, " +
                "COALESCE(NV.NAME, '') AS NHANVIEN, " +
                "COALESCE(B.NAME, '') AS BAN, " +
                "COALESCE(UTT.NAME, '') AS THANHTOANBOI, " +
                "COALESCE(H.DOITRA, 0) AS DOITRA, " +
                "COALESCE(H.DATHANHTOAN, 0) AS DATHANHTOAN, " +
                "H.GIOTHANHTOAN, " +
                "COALESCE(H.TRALAI, 0) AS TRALAI, " +
                "COALESCE(H.TIENHANG, 0) AS TIENHANG, " +
                "COALESCE(H.TILETHUE, 0) AS TILETHUE, " +
                "COALESCE(H.TIENTHUE, 0) AS TIENTHUE, " +
                "COALESCE(H.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
                "COALESCE(H.TIENGIAMGIA, 0) AS TIENGIAMGIA, " +
                "COALESCE(H.PHIVANCHUYEN, 0) AS PHIVANCHUYEN, " +
                "COALESCE(H.TIENGIO, 0) AS TIENGIO, " +
                "COALESCE(H.PHIDICHVU, 0) AS PHIDICHVU " +
                "FROM TDONHANG H " +
                "LEFT JOIN DKHACHHANG K ON K.ID = H.DKHACHHANGID " +
                "LEFT JOIN DNHANVIEN NV ON NV.ID = H.DNHANVIENXUATID " +
                "LEFT JOIN DBAN B ON B.ID = H.DBANID " +
                "LEFT JOIN SUSER UTT ON UTT.ID = H.USERTHANHTOANID " +
                "WHERE H.ID = @I",
                "@I", billId);

            if (hdrDt.Rows.Count == 0)
                return null;

            DataRow h = hdrDt.Rows[0];
            string huyId = DbHelper.NewId();
            string tableName = DbHelper.Str(h["BAN"]) ?? "";
            if (tableName.Length == 0)
                tableName = fallbackTableName ?? "";

            DateTime? ngay = ReadDateTime(h["NGAY"]);
            DateTime? gioThanhToan = ReadDateTime(h["GIOTHANHTOAN"]);

            DbHelper.Exec(conn, tx,
                "INSERT INTO TDONHANGHUY (" +
                "ID, NAME, STATUS, USERCREATEDID, NOTE, NGAY, KHACHHANG, NHANVEN, THUNGAN, " +
                "GIOHUY, NGAYHUY, DOITRA, DATHANHTOAN, GIOTHANHTOAN, TRALAI, " +
                "TIENHANG, TILETHUE, TIENTHUE, TILEGIAMGIA, TIENGIAMGIA, PHIVANCHUYEN, " +
                "THANHTOANBOI, LYDOHUY, TIENGIO, PHIDICHVU, TDONHANGID, BAN) VALUES (" +
                "@ID, @NAME, @ST, @U, @NOTE, @NGAY, @KH, @NV, @TN, " +
                "@GIOHUY, @NGAYHUY, @DOITRA, @DTT, @GIOTT, @TRALAI, " +
                "@TIENHANG, @TLTHUE, @TTHUE, @TLGG, @TGG, @PVC, " +
                "@TTBOI, @LYDO, @TGIO, @PDV, @ORIG, @BAN)",
                "@ID", huyId,
                "@NAME", DbHelper.Str(h["NAME"]) ?? "",
                "@ST", ActiveStatus,
                "@U", actorId ?? "",
                "@NOTE", string.IsNullOrEmpty(DbHelper.Str(h["NOTE"])) ? null : DbHelper.Str(h["NOTE"]),
                "@NGAY", ngay.HasValue ? (object)ngay.Value.Date : null,
                "@KH", DbHelper.Str(h["KHACHHANG"]) ?? "",
                "@NV", DbHelper.Str(h["NHANVIEN"]) ?? "",
                "@TN", cashierName ?? "",
                "@GIOHUY", serverNow,
                "@NGAYHUY", serverNow,
                "@DOITRA", DbHelper.Dec(h["DOITRA"]),
                "@DTT", DbHelper.Int(h["DATHANHTOAN"]),
                "@GIOTT", gioThanhToan.HasValue ? (object)gioThanhToan.Value : null,
                "@TRALAI", DbHelper.Dec(h["TRALAI"]),
                "@TIENHANG", DbHelper.Dec(h["TIENHANG"]),
                "@TLTHUE", DbHelper.Dec(h["TILETHUE"]),
                "@TTHUE", DbHelper.Dec(h["TIENTHUE"]),
                "@TLGG", DbHelper.Dec(h["TILEGIAMGIA"]),
                "@TGG", DbHelper.Dec(h["TIENGIAMGIA"]),
                "@PVC", DbHelper.Dec(h["PHIVANCHUYEN"]),
                "@TTBOI", DbHelper.Str(h["THANHTOANBOI"]) ?? "",
                "@LYDO", reason ?? "",
                "@TGIO", DbHelper.Dec(h["TIENGIO"]),
                "@PDV", DbHelper.Dec(h["PHIDICHVU"]),
                "@ORIG", billId,
                "@BAN", tableName);

            // Chi tiết: loại bỏ dòng xuất vật tư (XUATVATTU = 30) và dòng con combo (COMBOPARENTID <> '') theo §7.
            DataTable lineDt = DbHelper.Table(conn, tx,
                "SELECT COALESCE(M.CODE, '') AS MAHANG, COALESCE(D.TENHANG, '') AS TENHANG, " +
                "COALESCE(U.NAME, '') AS DVT, " +
                "(COALESCE(D.SLXUAT, 0) - COALESCE(D.SLNHAP, 0)) AS SOLUONG, " +
                "COALESCE(D.DONGIA, 0) AS DONGIA, COALESCE(D.THANHTIEN, 0) AS THANHTIEN, " +
                "D.NOTE, D.TIMECREATED " +
                "FROM TDONHANGCHITIET D " +
                "LEFT JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                "LEFT JOIN DDONVITINH U ON U.ID = D.DDONVITINHID " +
                "WHERE D.TDONHANGID = @B " +
                "AND COALESCE(D.XUATVATTU, 0) = 0 " +
                "AND (D.COMBOPARENTID IS NULL OR TRIM(D.COMBOPARENTID) = '') " +
                "ORDER BY D.TIMECREATED, D.ID",
                "@B", billId);

            foreach (DataRow r in lineDt.Rows)
            {
                DateTime created = ReadDateTime(r["TIMECREATED"]) ?? serverNow;
                string lineNote = DbHelper.Str(r["NOTE"]);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANGHUYCHITIET (" +
                    "ID, STATUS, USERCREATEDID, TIMECREATED, TDONHANGHUYID, " +
                    "MAHANG, TENHANG, DVT, SOLUONG, DONGIA, THANHTIEN, NOTE) VALUES (" +
                    "@ID, @ST, @U, @TC, @PID, @MA, @TEN, @DVT, @SL, @DG, @TT, @NOTE)",
                    "@ID", DbHelper.NewId(),
                    "@ST", ActiveStatus,
                    "@U", actorId ?? "",
                    "@TC", created,
                    "@PID", huyId,
                    "@MA", DbHelper.Str(r["MAHANG"]) ?? "",
                    "@TEN", DbHelper.Str(r["TENHANG"]) ?? "",
                    "@DVT", DbHelper.Str(r["DVT"]) ?? "",
                    "@SL", DbHelper.Dec(r["SOLUONG"]),
                    "@DG", DbHelper.Dec(r["DONGIA"]),
                    "@TT", DbHelper.Dec(r["THANHTIEN"]),
                    "@NOTE", string.IsNullOrEmpty(lineNote) ? null : lineNote);
            }

            return huyId;
        }

        /// <summary>Xóa cứng bill (TINCHEBIEN -> TDONHANGCHITIET -> TDONHANG). Trigger tự trả DBAN.TDONHANGID về NULL.</summary>
        public static void HardDeleteBillIn(FbConnection conn, FbTransaction tx, string billId)
        {
            DbHelper.Exec(conn, tx, "DELETE FROM TINCHEBIEN WHERE TDONHANGID = @B", "@B", billId);
            DbHelper.Exec(conn, tx, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @B", "@B", billId);
            DbHelper.Exec(conn, tx, "DELETE FROM TDONHANG WHERE ID = @B", "@B", billId);
        }

        /// <summary>
        /// Lưu bản hủy (TDONHANGHUY + TDONHANGHUYCHITIET) rồi xóa cứng bill trong transaction đang mở (HuyBill dùng chung).
        /// </summary>
        public static string ArchiveAndDeleteBillIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string fallbackTableName,
            string reason,
            string cashierName,
            string actorId,
            DateTime serverNow)
        {
            string huyId = InsertCancelledBillSnapshotIn(
                conn, tx, billId, fallbackTableName, reason, cashierName, actorId, serverNow);
            if (string.IsNullOrEmpty(huyId))
                return null;
            HardDeleteBillIn(conn, tx, billId);
            return huyId;
        }

        private static DateTime? ReadDateTime(object v)
        {
            if (v == null || v is DBNull)
                return null;
            return Convert.ToDateTime(v);
        }
    }
}
