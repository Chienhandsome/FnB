using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc dữ liệu phiếu Khách đặt hàng (TDATHANG) và Báo giá (TBAOGIA) để đổ vào mẫu in FastReport.
    /// Luôn đọc lại trực tiếp từ CSDL bằng SQL tham số hóa (không tính lại số tiền từ màn hình).
    /// </summary>
    public sealed class DocumentPrintRepository
    {
        private readonly string _databasePath;

        public DocumentPrintRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>
        /// Đọc đầy đủ đầu phiếu, tên phòng/bàn (đoàn nhiều phòng nối bằng ", "), tiền đặt trước từ phiếu thu
        /// và danh sách chi tiết phiếu đặt hàng theo đúng thứ tự lưu. Null nếu phiếu không còn.
        /// </summary>
        public BookingPrintDocument LoadBookingForPrint(string bookingId)
        {
            if (string.IsNullOrEmpty(bookingId))
                return null;

            BookingRow header = new BookingRepository(_databasePath).LoadBooking(bookingId);
            if (header == null)
                return null;

            BookingDepositInfo deposit = new PhieuThuRepository(_databasePath).LoadDeposit(bookingId);

            return DbHelper.Read(_databasePath, conn =>
            {
                string currentRoomId;
                string roomNames;
                LoadBookingRoomInfo(conn, bookingId, header.Guid, out currentRoomId, out roomNames);

                List<BookingPrintLine> lines = LoadBookingPrintLines(conn, bookingId);
                return new BookingPrintDocument
                {
                    Header = header,
                    RoomId = currentRoomId,
                    RoomNames = roomNames,
                    DepositAmount = deposit == null ? 0m : deposit.Amount,
                    Lines = lines
                };
            });
        }

        /// <summary>
        /// Đọc đầy đủ đầu phiếu và danh sách chi tiết báo giá theo đúng thứ tự lưu. Null nếu phiếu không còn.
        /// </summary>
        public QuotePrintDocument LoadQuoteForPrint(string quoteId)
        {
            if (string.IsNullOrEmpty(quoteId))
                return null;

            QuoteRow header = new QuoteRepository(_databasePath).LoadQuote(quoteId);
            if (header == null)
                return null;

            return DbHelper.Read(_databasePath, conn => new QuotePrintDocument
            {
                Header = header,
                Lines = LoadQuotePrintLines(conn, quoteId)
            });
        }

        private static void LoadBookingRoomInfo(
            FbConnection conn,
            string bookingId,
            string groupGuid,
            out string currentRoomId,
            out string roomNames)
        {
            currentRoomId = "";
            roomNames = "";

            DataTable cur = DbHelper.Table(conn, null,
                "SELECT B.\"DBANID\", R.\"NAME\" AS ROOMNAME " +
                "FROM \"TDATHANG\" B " +
                "LEFT JOIN \"DBAN\" R ON R.\"ID\" = B.\"DBANID\" " +
                "WHERE B.\"ID\" = @id AND B.\"STATUS\" = @status",
                "@id", bookingId,
                "@status", PrintConstants.ActiveStatus);

            string currentRoomName = "";
            if (cur.Rows.Count > 0)
            {
                currentRoomId = DbHelper.Str(cur.Rows[0]["DBANID"]) ?? "";
                currentRoomName = (DbHelper.Str(cur.Rows[0]["ROOMNAME"]) ?? "").Trim();
            }

            if (string.IsNullOrEmpty(groupGuid))
            {
                roomNames = currentRoomName;
                return;
            }

            DataTable grp = DbHelper.Table(conn, null,
                "SELECT B.\"ID\", R.\"NAME\" AS ROOMNAME " +
                "FROM \"TDATHANG\" B " +
                "LEFT JOIN \"DBAN\" R ON R.\"ID\" = B.\"DBANID\" " +
                "WHERE B.\"GUID\" = @guid AND B.\"STATUS\" = @status " +
                "ORDER BY B.\"NAME\", R.\"NAME\"",
                "@guid", groupGuid,
                "@status", PrintConstants.ActiveStatus);

            var names = new List<string>(grp.Rows.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in grp.Rows)
            {
                string rn = (DbHelper.Str(r["ROOMNAME"]) ?? "").Trim();
                if (rn.Length > 0 && seen.Add(rn))
                    names.Add(rn);
            }

            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (names.Count == 0 && currentRoomName.Length > 0)
                names.Add(currentRoomName);
            roomNames = string.Join(", ", names.ToArray());
        }

        private static List<BookingPrintLine> LoadBookingPrintLines(FbConnection conn, string bookingId)
        {
            DataTable dt = DbHelper.Table(conn, null,
                "SELECT L.\"ID\", L.\"TDATHANGID\", L.\"DMATHANGID\", L.\"DDONVITINHID\", " +
                "L.\"SOLUONG\", L.\"DONGIA\", L.\"TILEGIAMGIA\", L.\"THANHTIEN\", L.\"BAOHANH\", L.\"NOTE\", " +
                "P.\"CODE\" AS PRODUCTCODE, P.\"NAME\" AS PRODUCTNAME, U.\"NAME\" AS UNITNAME " +
                "FROM \"TDATHANGCHITIET\" L " +
                "LEFT JOIN \"DMATHANG\" P ON P.\"ID\" = L.\"DMATHANGID\" " +
                "LEFT JOIN \"DDONVITINH\" U ON U.\"ID\" = L.\"DDONVITINHID\" " +
                "WHERE L.\"TDATHANGID\" = @id AND L.\"STATUS\" = @status " +
                "ORDER BY L.\"TIMECREATED\", L.\"ID\"",
                "@id", bookingId,
                "@status", PrintConstants.ActiveStatus);

            var list = new List<BookingPrintLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new BookingPrintLine
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    BookingId = DbHelper.Str(r["TDATHANGID"]) ?? bookingId,
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    ProductCode = DbHelper.Str(r["PRODUCTCODE"]) ?? "",
                    ProductName = DbHelper.Str(r["PRODUCTNAME"]) ?? "",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]) ?? "",
                    UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                    Quantity = DbHelper.Dec(r["SOLUONG"]),
                    Price = DbHelper.Dec(r["DONGIA"]),
                    DiscountPct = DbHelper.Dec(r["TILEGIAMGIA"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"]),
                    Warranty = DbHelper.Str(r["BAOHANH"]) ?? "",
                    Note = DbHelper.Str(r["NOTE"]) ?? ""
                });
            }
            return list;
        }

        private static List<QuotePrintLine> LoadQuotePrintLines(FbConnection conn, string quoteId)
        {
            DataTable dt = DbHelper.Table(conn, null,
                "SELECT L.\"ID\", L.\"DMATHANGID\", L.\"DDONVITINHID\", " +
                "L.\"SOLUONG\", L.\"DONGIA\", L.\"TILEGIAMGIA\", L.\"THANHTIEN\", L.\"NOTE\", " +
                "P.\"CODE\" AS PRODUCTCODE, P.\"NAME\" AS PRODUCTNAME, P.\"DDONVITINHID\" AS PRODUCTUNITID, " +
                "U.\"NAME\" AS UNITNAME " +
                "FROM \"TBAOGIACHITIET\" L " +
                "LEFT JOIN \"DMATHANG\" P ON P.\"ID\" = L.\"DMATHANGID\" " +
                "LEFT JOIN \"DDONVITINH\" U ON U.\"ID\" = L.\"DDONVITINHID\" " +
                "WHERE L.\"TBAOGIAID\" = @id AND L.\"STATUS\" = @status " +
                "ORDER BY L.\"TIMECREATED\", L.\"ID\"",
                "@id", quoteId,
                "@status", QuoteConstants.ActiveStatus);

            var list = new List<QuotePrintLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string prodUnitId = DbHelper.Str(r["PRODUCTUNITID"]);
                if (string.IsNullOrEmpty(prodUnitId))
                    prodUnitId = DbHelper.Str(r["DDONVITINHID"]) ?? "";
                list.Add(new QuotePrintLine
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    ProductCode = DbHelper.Str(r["PRODUCTCODE"]) ?? "",
                    ProductName = DbHelper.Str(r["PRODUCTNAME"]) ?? "",
                    ProductUnitId = prodUnitId,
                    UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                    Quantity = DbHelper.Dec(r["SOLUONG"]),
                    Price = DbHelper.Dec(r["DONGIA"]),
                    DiscountPct = DbHelper.Dec(r["TILEGIAMGIA"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"]),
                    Note = DbHelper.Str(r["NOTE"]) ?? ""
                });
            }
            return list;
        }
    }
}
