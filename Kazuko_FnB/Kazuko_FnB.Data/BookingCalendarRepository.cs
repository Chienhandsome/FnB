using System;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Repository cho màn "Theo dõi đặt phòng" (spec §3). Chỉ đọc/ghi các bảng đã có
    /// (<c>DLOAIPHONG</c>, <c>DBAN</c>, <c>TDATHANG</c>, <c>DMUCDICHDAT</c>); không DDL.
    /// SQL tham số hóa 100%. Mở kết nối theo mẫu của repo (<c>DbHelper.Read</c> /
    /// <c>DbHelper.InTransaction</c>).
    /// </summary>
    public sealed class BookingCalendarRepository
    {
        private readonly string _databasePath;

        public BookingCalendarRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Các loại phòng STATUS = 30, theo SORTORDER (giống app gốc).</summary>
        public CalendarData LoadTypes(string typeId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                var data = new CalendarData();
                LoadTypesCore(conn, typeId, data);
                return data;
            });
        }

        private static void LoadTypesCore(FbConnection conn, string typeId, CalendarData data)
        {
            using (FbCommand cmd = new FbCommand(
                "SELECT T.ID, COALESCE(T.NAME, ''), COALESCE(T.MAUCHU, 0), COALESCE(T.MAUNEN, 0), " +
                "CASE WHEN T.SORTORDER IS NULL OR TRIM(T.SORTORDER) = '' THEN 0 ELSE CAST(T.SORTORDER AS INTEGER) END " +
                "FROM DLOAIPHONG T WHERE T.STATUS = 30 " +
                (string.IsNullOrEmpty(typeId) ? "" : "AND T.ID = @type ") +
                "ORDER BY CASE WHEN T.SORTORDER IS NULL OR TRIM(T.SORTORDER) = '' THEN 0 ELSE CAST(T.SORTORDER AS INTEGER) END, T.NAME", conn))
            {
                if (!string.IsNullOrEmpty(typeId))
                    cmd.Parameters.Add("@type", FbDbType.VarChar).Value = typeId;
                using (FbDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        data.Types.Add(new CalendarRoomType
                        {
                            Id = r.GetString(0),
                            Name = r.GetString(1),
                            ForeColorArgb = r.GetInt32(2),
                            BackColorArgb = r.GetInt32(3),
                            SortOrder = r.GetInt32(4)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Tải toàn bộ lưới cho một cửa sổ xem [from, from + days − 1] và một loại phòng (rỗng = tất cả).
        /// - Loại: tất cả DLOAIPHONG STATUS = 30 (có lọc thì chỉ loại đó).
        /// - Phòng: DBAN STATUS = 30 có loại phòng STATUS = 30 (phòng không có DLOAIPHONGID
        ///   không hiện trên lưới — quyết định của Chiến 23/11, giữ y hệt app gốc).
        /// - Thanh: TDATHANG STATUS = 30, giữ y hệt điều kiện của app gốc
        ///   (TUNGAY BETWEEN hoặc DENNGAY BETWEEN — phiếu bao trùm cửa sổ không hiện, §5.3).
        /// - Tổng số phòng: COUNT(DBAN STATUS = 30), kể cả phòng không có loại (§3.1).
        /// </summary>
        public CalendarData LoadCalendar(DateTime from, int days, string typeId)
        {
            DateTime to = from.AddDays(days - 1);
            string typeFilter = string.IsNullOrEmpty(typeId) ? "" : "AND B.DLOAIPHONGID = @type ";
            return DbHelper.Read(_databasePath, conn =>
            {
                var data = new CalendarData();
                LoadTypesCore(conn, typeId, data);
                // Phòng (chỉ phòng có loại phòng hợp lệ; sắp theo loại rồi theo tên phòng).
                using (FbCommand cmd = new FbCommand(
                    "SELECT B.ID, B.NAME, B.DLOAIPHONGID, COALESCE(T.NAME, '') " +
                    "FROM DBAN B INNER JOIN DLOAIPHONG T ON T.ID = B.DLOAIPHONGID AND T.STATUS = 30 " +
                    "WHERE B.STATUS = 30 " + typeFilter +
                    "ORDER BY CASE WHEN T.SORTORDER IS NULL OR TRIM(T.SORTORDER) = '' THEN 0 ELSE CAST(T.SORTORDER AS INTEGER) END, T.NAME, B.NAME", conn))
                {
                    if (!string.IsNullOrEmpty(typeId))
                        cmd.Parameters.Add("@type", FbDbType.VarChar).Value = typeId;
                    using (FbDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            data.Rooms.Add(new CalendarRoom
                            {
                                Id = r.GetString(0),
                                Name = r.GetString(1),
                                TypeId = r.IsDBNull(2) ? "" : r.GetString(2),
                                TypeName = r.GetString(3)
                            });
                        }
                    }
                }
                // Thanh đặt phòng (thứ tự trả về quyết định màu ô tên phòng: phiếu đầu tiên của phòng).
                using (FbCommand cmd = new FbCommand(
                    "SELECT A.ID, A.GUID, A.DBANID, COALESCE(B.NAME, ''), COALESCE(A.TENKHACH, ''), " +
                    "A.TUNGAY, A.DENNGAY, COALESCE(A.MAUSAC, 0), COALESCE(P.NAME, ''), COALESCE(A.NOTE, '') " +
                    "FROM TDATHANG A INNER JOIN DBAN B ON B.ID = A.DBANID " +
                    "LEFT JOIN DMUCDICHDAT P ON P.ID = A.DMUCDICHDATID " +
                    "WHERE A.STATUS = 30 AND (A.TUNGAY BETWEEN @from AND @to OR A.DENNGAY BETWEEN @from AND @to) " +
                    typeFilter +
                    "ORDER BY A.TUNGAY, A.DENNGAY, B.NAME", conn))
                {
                    cmd.Parameters.Add("@from", FbDbType.TimeStamp).Value = from;
                    cmd.Parameters.Add("@to", FbDbType.TimeStamp).Value = to;
                    if (!string.IsNullOrEmpty(typeId))
                        cmd.Parameters.Add("@type", FbDbType.VarChar).Value = typeId;
                    using (FbDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            data.Bars.Add(new CalendarBookingBar
                            {
                                Id = r.GetString(0),
                                Guid = r.GetString(1),
                                RoomId = r.GetString(2),
                                RoomName = r.GetString(3),
                                TenKhach = r.GetString(4),
                                TuNgay = r.GetDateTime(5),
                                DenNgay = r.GetDateTime(6),
                                ColorArgb = r.GetInt32(7),
                                PurposeName = r.GetString(8),
                                Note = r.GetString(9)
                            });
                        }
                    }
                }
                // Tổng số phòng (kể cả phòng không có loại).
                using (FbCommand cmd = new FbCommand("SELECT COUNT(*) FROM DBAN WHERE STATUS = 30", conn))
                {
                    data.TotalRooms = Convert.ToInt32(cmd.ExecuteScalar());
                }
                return data;
            });
        }

        /// <summary>
        /// Áp dụng một lần kéo thả trên lưới, trong 1 transaction (spec §3.4):
        /// phiếu bị kéo đổi DBANID + TUNGAY/DENNGAY; các phiếu cùng đoàn (cùng GUID)
        /// chỉ nhận TUNGAY/DENNGAY mới (không đổi bàn của chúng); không đổi TUGIO/DENGIO.
        /// Ghi USERMODIFIEDID/TIMEMODIFIED theo quy ước engine.
        /// </summary>
        public BookingCalendarMoveResult ApplyDragMove(string bookingId, string newRoomId,
            DateTime newTuNgay, DateTime newDenNgay, string userId)
        {
            BookingCalendarMoveResult result = null;
            try
            {
                DbHelper.InTransaction(_databasePath, (conn, tx) =>
                {
                    object g = DbHelper.Scalar(conn, tx,
                        "SELECT \"GUID\" FROM \"TDATHANG\" WHERE \"ID\" = @id", "@id", bookingId);
                    if (g == null)
                    {
                        result = BookingCalendarMoveResult.Fail("Phiếu đặt phòng không còn tồn tại.");
                        return;
                    }
                    string guid = DbHelper.Str(g);
                    int affected = DbHelper.Exec(conn, tx,
                        "UPDATE \"TDATHANG\" SET \"DBANID\" = @room, \"TUNGAY\" = @tu, \"DENNGAY\" = @den, " +
                        "\"USERMODIFIEDID\" = @user, \"TIMEMODIFIED\" = @now WHERE \"ID\" = @id",
                        "@room", newRoomId, "@tu", newTuNgay, "@den", newDenNgay,
                        "@user", userId ?? "", "@now", DbHelper.Now(), "@id", bookingId);
                    if (affected == 0)
                    {
                        result = BookingCalendarMoveResult.Fail("Phiếu đặt phòng không còn tồn tại.");
                        return;
                    }
                    // Cùng đoàn: chỉ đổi ngày, không đổi bàn.
                    DbHelper.Exec(conn, tx,
                        "UPDATE \"TDATHANG\" SET \"TUNGAY\" = @tu, \"DENNGAY\" = @den, " +
                        "\"USERMODIFIEDID\" = @user, \"TIMEMODIFIED\" = @now WHERE \"GUID\" = @guid AND \"ID\" <> @id",
                        "@tu", newTuNgay, "@den", newDenNgay,
                        "@user", userId ?? "", "@now", DbHelper.Now(), "@guid", guid, "@id", bookingId);
                    result = BookingCalendarMoveResult.OkResult();
                });
            }
            catch (DataAccessException ex)
            {
                return BookingCalendarMoveResult.Fail("Không thể cập nhật phiếu đặt phòng: " + ex.Message);
            }
            return result ?? BookingCalendarMoveResult.Fail("Không thể cập nhật phiếu đặt phòng.");
        }
    }
}
