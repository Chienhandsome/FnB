using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu báo giá bán hàng (TBAOGIA + TBAOGIACHITIET) theo docs/specs/bao-gia-ban-hang.md.
    /// SQL tham số hóa 100%; lưu phiếu + kiểm trùng số phiếu + chi tiết trong MỘT transaction;
    /// xóa cứng TBAOGIACHITIET + TBAOGIA trong MỘT transaction, không để bản ghi mồ côi.
    /// </summary>
    public sealed class QuoteRepository
    {
        private readonly string _databasePath;

        public QuoteRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        // ------------------------------------------------------------------ Đọc danh sách & chi tiết

        public List<QuoteListRow> LoadQuotes(QuoteFilter filter)
        {
            if (filter == null)
                filter = new QuoteFilter { FromDate = DateTime.Today.AddDays(-30), ToDate = DateTime.Today };
            return DbHelper.Read(_databasePath, conn =>
            {
                string sql =
                    "SELECT Q.\"ID\", Q.\"NGAY\", Q.\"NAME\", Q.\"DKHACHHANGID\", Q.\"TENKHACH\", Q.\"DIACHI\", " +
                    "Q.\"DIENTHOAI\", Q.\"EMAIL\", Q.\"TIENHANG\", Q.\"TIENGIAMGIA\", Q.\"TILEGIAMGIA\", " +
                    "Q.\"TIENTHUE\", Q.\"PHIVANCHUYEN\", Q.\"TONGCONG\", Q.\"NOTE\", " +
                    "C.\"NAME\" AS CUSTOMERNAME, " +
                    "Q.\"TIMECREATED\" AS TCREATED, Q.\"TIMEMODIFIED\" AS TMODIFIED, " +
                    "UC.\"NAME\" AS CREATEDBY, UM.\"NAME\" AS MODIFIEDBY " +
                    "FROM \"TBAOGIA\" Q " +
                    "LEFT JOIN \"DKHACHHANG\" C ON C.\"ID\" = Q.\"DKHACHHANGID\" " +
                    "LEFT JOIN \"SUSER\" UC ON UC.\"ID\" = Q.\"USERCREATEDID\" " +
                    "LEFT JOIN \"SUSER\" UM ON UM.\"ID\" = Q.\"USERMODIFIEDID\" " +
                    "WHERE Q.\"STATUS\" = @status AND Q.\"NGAY\" >= @from AND Q.\"NGAY\" < @toNext";
                var args = new List<object>
                {
                    "@status", QuoteConstants.ActiveStatus,
                    "@from", filter.FromDate.Date,
                    "@toNext", filter.ToDate.Date.AddDays(1)
                };
                if (!string.IsNullOrWhiteSpace(filter.NumberText))
                {
                    sql += " AND Q.\"NAME\" CONTAINING @num";
                    args.Add("@num");
                    args.Add(filter.NumberText.Trim());
                }
                if (!string.IsNullOrEmpty(filter.CustomerId))
                {
                    sql += " AND Q.\"DKHACHHANGID\" = @cust";
                    args.Add("@cust");
                    args.Add(filter.CustomerId);
                }
                sql += " ORDER BY Q.\"NGAY\" DESC, Q.\"NAME\" DESC";

                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                var list = new List<QuoteListRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new QuoteListRow
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Ngay = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        CustomerId = DbHelper.Str(r["DKHACHHANGID"]),
                        CustomerName = DbHelper.Str(r["CUSTOMERNAME"]) ?? "",
                        TenKhach = DbHelper.Str(r["TENKHACH"]) ?? "",
                        DiaChi = DbHelper.Str(r["DIACHI"]) ?? "",
                        DienThoai = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                        Email = DbHelper.Str(r["EMAIL"]) ?? "",
                        TienHang = DbHelper.Dec(r["TIENHANG"]),
                        TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                        TileGiamGia = DbHelper.Dec(r["TILEGIAMGIA"]),
                        TienThue = DbHelper.Dec(r["TIENTHUE"]),
                        PhiVanChuyen = DbHelper.Dec(r["PHIVANCHUYEN"]),
                        TongCong = DbHelper.Dec(r["TONGCONG"]),
                        Note = DbHelper.Str(r["NOTE"]) ?? "",
                        CreatedAt = r["TCREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TCREATED"]),
                        ModifiedAt = r["TMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TMODIFIED"]),
                        CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                        ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
                    });
                }
                return list;
            });
        }

        public QuoteRow LoadQuote(string quoteId)
        {
            if (string.IsNullOrEmpty(quoteId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT \"ID\", \"NAME\", \"NGAY\", \"DKHACHHANGID\", \"TENKHACH\", \"DIACHI\", \"DIENTHOAI\", " +
                    "\"EMAIL\", \"NOTE\", \"TIENHANG\", \"TILEGIAMGIA\", \"TIENGIAMGIA\", \"GIAMTHEOTIEN\", " +
                    "\"TILETHUE\", \"TIENTHUE\", \"PHIVANCHUYEN\", \"TONGCONG\", \"LOAI\", \"STATUS\", " +
                    "\"USERCREATEDID\", \"TIMECREATED\", \"USERMODIFIEDID\", \"TIMEMODIFIED\" " +
                    "FROM \"TBAOGIA\" WHERE \"ID\" = @id AND \"STATUS\" = @status",
                    "@id", quoteId, "@status", QuoteConstants.ActiveStatus);
                if (dt.Rows.Count == 0)
                    return null;
                DataRow r = dt.Rows[0];
                var row = new QuoteRow
                {
                    Id = DbHelper.Str(r["ID"]),
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Ngay = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                    CustomerId = DbHelper.Str(r["DKHACHHANGID"]),
                    TenKhach = DbHelper.Str(r["TENKHACH"]) ?? "",
                    DiaChi = DbHelper.Str(r["DIACHI"]) ?? "",
                    DienThoai = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                    Email = DbHelper.Str(r["EMAIL"]) ?? "",
                    Note = DbHelper.Str(r["NOTE"]) ?? "",
                    TienHang = DbHelper.Dec(r["TIENHANG"]),
                    TileGiamGia = DbHelper.Dec(r["TILEGIAMGIA"]),
                    TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                    GiamTheoTien = DbHelper.Int(r["GIAMTHEOTIEN"]) == QuoteConstants.FlagOn,
                    TileThue = DbHelper.Dec(r["TILETHUE"]),
                    TienThue = DbHelper.Dec(r["TIENTHUE"]),
                    PhiVanChuyen = DbHelper.Dec(r["PHIVANCHUYEN"]),
                    TongCong = DbHelper.Dec(r["TONGCONG"]),
                    Loai = DbHelper.Int(r["LOAI"]),
                    Status = DbHelper.Int(r["STATUS"]),
                    CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                    CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                    ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                    ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"])
                };
                row.Lines = LoadLinesCore(conn, null, quoteId);
                return row;
            });
        }

        public List<QuoteLine> LoadLines(string quoteId)
        {
            if (string.IsNullOrEmpty(quoteId))
                return new List<QuoteLine>();
            return DbHelper.Read(_databasePath, conn => LoadLinesCore(conn, null, quoteId));
        }

        public List<QuoteSaleItem> LoadSaleItems(bool sortByCode)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                string orderBy = sortByCode ? "P.\"CODE\", P.\"NAME\"" : "P.\"NAME\", P.\"CODE\"";
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT P.\"ID\", P.\"CODE\", P.\"NAME\", P.\"DDONVITINHID\", U1.\"NAME\" AS UNITNAME, " +
                    "P.\"DDONVITINHCHANID\", U2.\"NAME\" AS BIGUNITNAME, P.\"QUYDOI\", " +
                    "P.\"GIABAN\", P.\"GIABANCHAN\", P.\"BAOHANH\" " +
                    "FROM \"DMATHANG\" P " +
                    "LEFT JOIN \"DDONVITINH\" U1 ON U1.\"ID\" = P.\"DDONVITINHID\" " +
                    "LEFT JOIN \"DDONVITINH\" U2 ON U2.\"ID\" = P.\"DDONVITINHCHANID\" " +
                    "WHERE P.\"STATUS\" = @status ORDER BY " + orderBy,
                    "@status", QuoteConstants.ActiveStatus);
                var list = new List<QuoteSaleItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new QuoteSaleItem
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Code = DbHelper.Str(r["CODE"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        UnitId = DbHelper.Str(r["DDONVITINHID"]),
                        UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                        BigUnitId = DbHelper.Str(r["DDONVITINHCHANID"]),
                        BigUnitName = DbHelper.Str(r["BIGUNITNAME"]) ?? "",
                        QuyDoi = DbHelper.Dec(r["QUYDOI"]),
                        SellPrice = DbHelper.Dec(r["GIABAN"]),
                        BigSellPrice = DbHelper.Dec(r["GIABANCHAN"]),
                        Warranty = DbHelper.Str(r["BAOHANH"]) ?? ""
                    });
                }
                return list;
            });
        }

        public List<LookupItem> LoadUnits()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT \"ID\", \"NAME\" FROM \"DDONVITINH\" WHERE \"STATUS\" = @status ORDER BY \"NAME\"",
                    "@status", QuoteConstants.ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new LookupItem
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Name = DbHelper.Str(r["NAME"]) ?? ""
                    });
                }
                return list;
            });
        }

        public QuoteCustomerInfo LoadCustomer(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT \"ID\", \"NAME\", \"DIACHI\", \"DIENTHOAI\", \"EMAIL\" " +
                    "FROM \"DKHACHHANG\" WHERE \"ID\" = @id AND \"STATUS\" = @status",
                    "@id", customerId, "@status", QuoteConstants.ActiveStatus);
                if (dt.Rows.Count == 0)
                    return null;
                DataRow r = dt.Rows[0];
                return new QuoteCustomerInfo
                {
                    Id = DbHelper.Str(r["ID"]),
                    Name = DbHelper.Str(r["NAME"]) ?? "",
                    Address = DbHelper.Str(r["DIACHI"]) ?? "",
                    Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                    Email = DbHelper.Str(r["EMAIL"]) ?? ""
                };
            });
        }

        // ------------------------------------------------------------------ Sinh số phiếu

        /// <summary>Sinh số phiếu báo giá theo mẫu STABLEDESC.NOTEMPLATE ("BG(yy)/(*****)"), quay vòng theo năm.</summary>
        public string GenerateNumber(DateTime? forDate = null)
        {
            DateTime date = (forDate ?? DateTime.Today).Date;
            return DbHelper.Read(_databasePath, conn => GenerateNumberCore(conn, null, date));
        }

        private static string GenerateNumberCore(FbConnection conn, FbTransaction tx, DateTime date)
        {
            object tpl = DbHelper.Scalar(conn, tx,
                "SELECT \"NOTEMPLATE\" FROM \"STABLEDESC\" WHERE \"NAME\" = @tbl",
                "@tbl", QuoteConstants.TableName);
            string template = DbHelper.Str(tpl);
            if (string.IsNullOrWhiteSpace(template))
                template = QuoteConstants.DefaultNumberTemplate;
            string prefix = DocumentNumber.LiteralPrefix(template, date);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT \"NAME\" FROM \"TBAOGIA\" WHERE \"NAME\" STARTING WITH @p",
                "@p", prefix);
            var names = new string[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
                names[i] = DbHelper.Str(dt.Rows[i]["NAME"]);
            int seq = DocumentNumber.NextSequence(names, prefix);
            return DocumentNumber.Format(template, date, seq);
        }

        // ------------------------------------------------------------------ Lưu & Xóa (1 transaction)

        public QuoteSaveResult SaveQuote(QuoteRow row, List<QuoteLine> lines, string userId)
        {
            if (row == null)
                throw new ArgumentNullException("row");
            if (lines == null)
                lines = new List<QuoteLine>();

            QuoteSaveResult result = null;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                DateTime now = DbHelper.Now();
                bool isNew = string.IsNullOrEmpty(row.Id);
                string number = (row.Name ?? "").Trim();

                if (isNew)
                {
                    object dup = DbHelper.Scalar(conn, tx,
                        "SELECT FIRST 1 \"ID\" FROM \"TBAOGIA\" WHERE \"NAME\" = @n",
                        "@n", number);
                    if (dup != null)
                    {
                        result = QuoteSaveResult.Fail(QuoteText.NumberDuplicate(number));
                        return;
                    }
                    string newId = DbHelper.NewId();
                    InsertHeader(conn, tx, newId, number, row, userId, now);
                    InsertLines(conn, tx, newId, lines, userId, now);
                    row.Id = newId;
                    row.Name = number;
                    result = new QuoteSaveResult { Ok = true, SavedId = newId };
                }
                else
                {
                    object exists = DbHelper.Scalar(conn, tx,
                        "SELECT \"ID\" FROM \"TBAOGIA\" WHERE \"ID\" = @id",
                        "@id", row.Id);
                    if (exists == null)
                    {
                        result = QuoteSaveResult.Fail(QuoteText.NotFound);
                        return;
                    }
                    object dup = DbHelper.Scalar(conn, tx,
                        "SELECT FIRST 1 \"ID\" FROM \"TBAOGIA\" WHERE \"NAME\" = @n AND \"ID\" <> @id",
                        "@n", number, "@id", row.Id);
                    if (dup != null)
                    {
                        result = QuoteSaveResult.Fail(QuoteText.NumberDuplicate(number));
                        return;
                    }
                    int affected = UpdateHeader(conn, tx, row.Id, number, row, userId, now);
                    if (affected == 0)
                    {
                        result = QuoteSaveResult.Fail(QuoteText.NotFound);
                        return;
                    }
                    DbHelper.Exec(conn, tx,
                        "DELETE FROM \"TBAOGIACHITIET\" WHERE \"TBAOGIAID\" = @id",
                        "@id", row.Id);
                    InsertLines(conn, tx, row.Id, lines, userId, now);
                    row.Name = number;
                    result = new QuoteSaveResult { Ok = true, SavedId = row.Id };
                }
            });
            return result ?? QuoteSaveResult.Fail("Không ghi được dữ liệu.");
        }

        /// <summary>Xóa cứng TBAOGIACHITIET và TBAOGIA trong cùng một transaction.</summary>
        public QuoteDeleteResult DeleteQuote(string quoteId)
        {
            if (string.IsNullOrEmpty(quoteId))
                return new QuoteDeleteResult { Ok = true };
            QuoteDeleteResult result = null;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                DbHelper.Exec(conn, tx,
                    "DELETE FROM \"TBAOGIACHITIET\" WHERE \"TBAOGIAID\" = @id",
                    "@id", quoteId);
                DbHelper.Exec(conn, tx,
                    "DELETE FROM \"TBAOGIA\" WHERE \"ID\" = @id",
                    "@id", quoteId);
                result = new QuoteDeleteResult { Ok = true };
            });
            return result ?? QuoteDeleteResult.Fail("Không xóa được dữ liệu.");
        }

        // ------------------------------------------------------------------ Nội bộ

        private static List<QuoteLine> LoadLinesCore(FbConnection conn, FbTransaction tx, string quoteId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT L.\"ID\", L.\"DMATHANGID\", L.\"DDONVITINHID\", L.\"SOLUONG\", L.\"DONGIA\", " +
                "L.\"TILEGIAMGIA\", L.\"THANHTIEN\", L.\"NOTE\", " +
                "P.\"CODE\" AS PRODUCTCODE, P.\"NAME\" AS PRODUCTNAME, U.\"NAME\" AS UNITNAME " +
                "FROM \"TBAOGIACHITIET\" L " +
                "LEFT JOIN \"DMATHANG\" P ON P.\"ID\" = L.\"DMATHANGID\" " +
                "LEFT JOIN \"DDONVITINH\" U ON U.\"ID\" = L.\"DDONVITINHID\" " +
                "WHERE L.\"TBAOGIAID\" = @id AND L.\"STATUS\" = @status " +
                "ORDER BY L.\"TIMECREATED\", L.\"ID\"",
                "@id", quoteId, "@status", QuoteConstants.ActiveStatus);
            var list = new List<QuoteLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new QuoteLine
                {
                    Id = DbHelper.Str(r["ID"]),
                    QuoteId = quoteId,
                    ProductId = DbHelper.Str(r["DMATHANGID"]),
                    ProductCode = DbHelper.Str(r["PRODUCTCODE"]) ?? "",
                    ProductName = DbHelper.Str(r["PRODUCTNAME"]) ?? "",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]),
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

        private static void InsertHeader(FbConnection conn, FbTransaction tx, string id, string number,
            QuoteRow row, string userId, DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO \"TBAOGIA\" (\"ID\", \"NAME\", \"NGAY\", \"DKHACHHANGID\", \"TENKHACH\", \"DIACHI\", " +
                "\"DIENTHOAI\", \"EMAIL\", \"NOTE\", \"TIENHANG\", \"TILEGIAMGIA\", \"TIENGIAMGIA\", \"GIAMTHEOTIEN\", " +
                "\"TILETHUE\", \"TIENTHUE\", \"PHIVANCHUYEN\", \"TONGCONG\", \"LOAI\", \"STATUS\", " +
                "\"USERCREATEDID\", \"TIMECREATED\") " +
                "VALUES (@id, @name, @ngay, @cust, @ten, @diachi, @dt, @email, @note, @tienhang, @tilegg, @tiengg, " +
                "@gtt, @tilethue, @tienthue, @phi, @tong, @loai, @status, @user, @now)",
                "@id", id,
                "@name", number,
                "@ngay", row.Ngay.Date,
                "@cust", string.IsNullOrEmpty(row.CustomerId) ? null : row.CustomerId,
                "@ten", row.TenKhach ?? "",
                "@diachi", row.DiaChi ?? "",
                "@dt", row.DienThoai ?? "",
                "@email", row.Email ?? "",
                "@note", row.Note ?? "",
                "@tienhang", row.TienHang,
                "@tilegg", row.TileGiamGia,
                "@tiengg", row.TienGiamGia,
                "@gtt", row.GiamTheoTien ? QuoteConstants.FlagOn : QuoteConstants.FlagOff,
                "@tilethue", row.TileThue,
                "@tienthue", row.TienThue,
                "@phi", row.PhiVanChuyen,
                "@tong", row.TongCong,
                "@loai", 0,
                "@status", QuoteConstants.ActiveStatus,
                "@user", userId ?? "",
                "@now", now);
        }

        private static int UpdateHeader(FbConnection conn, FbTransaction tx, string id, string number,
            QuoteRow row, string userId, DateTime now)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE \"TBAOGIA\" SET \"NAME\" = @name, \"NGAY\" = @ngay, \"DKHACHHANGID\" = @cust, " +
                "\"TENKHACH\" = @ten, \"DIACHI\" = @diachi, \"DIENTHOAI\" = @dt, \"EMAIL\" = @email, " +
                "\"NOTE\" = @note, \"TIENHANG\" = @tienhang, \"TILEGIAMGIA\" = @tilegg, \"TIENGIAMGIA\" = @tiengg, " +
                "\"GIAMTHEOTIEN\" = @gtt, \"TILETHUE\" = @tilethue, \"TIENTHUE\" = @tienthue, " +
                "\"PHIVANCHUYEN\" = @phi, \"TONGCONG\" = @tong, \"LOAI\" = @loai, " +
                "\"USERMODIFIEDID\" = @user, \"TIMEMODIFIED\" = @now " +
                "WHERE \"ID\" = @id",
                "@name", number,
                "@ngay", row.Ngay.Date,
                "@cust", string.IsNullOrEmpty(row.CustomerId) ? null : row.CustomerId,
                "@ten", row.TenKhach ?? "",
                "@diachi", row.DiaChi ?? "",
                "@dt", row.DienThoai ?? "",
                "@email", row.Email ?? "",
                "@note", row.Note ?? "",
                "@tienhang", row.TienHang,
                "@tilegg", row.TileGiamGia,
                "@tiengg", row.TienGiamGia,
                "@gtt", row.GiamTheoTien ? QuoteConstants.FlagOn : QuoteConstants.FlagOff,
                "@tilethue", row.TileThue,
                "@tienthue", row.TienThue,
                "@phi", row.PhiVanChuyen,
                "@tong", row.TongCong,
                "@loai", 0,
                "@user", userId ?? "",
                "@now", now,
                "@id", id);
        }

        private static void InsertLines(FbConnection conn, FbTransaction tx, string quoteId,
            List<QuoteLine> lines, string userId, DateTime now)
        {
            // Tăng TIMECREATED 1 giây mỗi dòng để giữ đúng thứ tự gốc trên màn hình khi mở lại (không đổi schema).
            for (int i = 0; i < lines.Count; i++)
            {
                QuoteLine line = lines[i];
                DateTime lineTime = now.AddSeconds(i);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO \"TBAOGIACHITIET\" (\"ID\", \"TBAOGIAID\", \"DMATHANGID\", \"DDONVITINHID\", " +
                    "\"SOLUONG\", \"DONGIA\", \"TILEGIAMGIA\", \"THANHTIEN\", \"NOTE\", \"STATUS\", " +
                    "\"USERCREATEDID\", \"TIMECREATED\") " +
                    "VALUES (@id, @quote, @prod, @unit, @qty, @price, @disc, @amt, @note, @status, @user, @now)",
                    "@id", DbHelper.NewId(),
                    "@quote", quoteId,
                    "@prod", line.ProductId,
                    "@unit", line.UnitId,
                    "@qty", line.Quantity,
                    "@price", line.Price,
                    "@disc", line.DiscountPct,
                    "@amt", line.Amount,
                    "@note", line.Note ?? "",
                    "@status", QuoteConstants.ActiveStatus,
                    "@user", userId ?? "",
                    "@now", lineTime);
            }
        }
    }
}
