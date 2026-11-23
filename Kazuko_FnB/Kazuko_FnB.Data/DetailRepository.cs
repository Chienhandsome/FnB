using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Các tab liên quan dưới lưới danh mục (pane dưới), dựng theo đúng cơ chế app gốc
    /// (No1Lib.Sys No1TreeGrid.LoadReferences, đã decompile): mọi bảng có cột SCOLUMN.REFTABLEID trỏ tới bảng
    /// đang xem là một ứng viên; bảng có SFORM (FORMTYPE = 0) thì mỗi form một tab (tên form, LOAI), không thì
    /// một tab tên STABLEDESC.DESCRIPTION; bảng con của bảng KHÁC thì lấy form của bảng cha; REFCONFIG của bảng
    /// đang xem là danh sách tab BỊ ẨN (ID bảng + LOAI). Dữ liệu từng tab: SQL cố định theo bảng bên dưới
    /// (tham số hóa, tên cột khóa ngoại chỉ nhận từ danh sách cho phép).
    /// </summary>
    public sealed class DetailRepository
    {
        private const int ActiveStatus = 30;
        private const int TableTypeMaster = 3;
        private const int TableTypeDetail = 4;
        private static readonly Regex KeyPattern = new Regex("^[A-Z][A-Z0-9_]*$", RegexOptions.Compiled);

        private readonly string _databasePath;

        public DetailRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        // ------------------------------------------------------------------ Danh sách tab

        /// <summary>Các tab liên quan của bảng <paramref name="masterTable"/> (tên bảng, vd. DBAN, TDATHANG).</summary>
        public List<DetailTab> LoadTabs(string masterTable)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                var tabs = new List<DetailTab>();
                string masterId = DbHelper.Str(DbHelper.Scalar(conn, null,
                    "SELECT ID FROM STABLEDESC WHERE NAME = @N", "@N", masterTable));
                if (string.IsNullOrEmpty(masterId))
                    return tabs;

                HashSet<string> hidden = LoadHidden(conn, masterId);
                DataTable refs = DbHelper.Table(conn, null,
                    "SELECT D.TABLETYPE, C.NAME AS COLNAME, D.MASTERTABLEID, D.ID, D.NAME, D.DESCRIPTION " +
                    "FROM SCOLUMN C INNER JOIN STABLEDESC D ON D.ID = C.STABLEDESCID " +
                    "WHERE C.REFTABLEID = @M ORDER BY D.DESCRIPTION", "@M", masterId);

                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (DataRow r in refs.Rows)
                {
                    string id = DbHelper.Str(r["ID"]);
                    if (string.IsNullOrEmpty(id) || !seen.Add(id))
                        continue; // mỗi bảng một lần (app gốc cũng loại trùng theo STABLEDESC.ID)
                    int type = DbHelper.Int(r["TABLETYPE"]);
                    if (type != TableTypeMaster && type != TableTypeDetail)
                        continue; // loại LINK và các loại khác: app gốc bỏ qua
                    string name = DbHelper.Str(r["NAME"]);
                    string description = DbHelper.Str(r["DESCRIPTION"]);
                    if (string.IsNullOrEmpty(description))
                        description = name;
                    string key = DbHelper.Str(r["COLNAME"]);

                    // Bảng con của một bảng KHÁC bảng đang xem: tab đặt tên theo các form của bảng cha
                    // (vd. TDONHANGGIO là con của TDONHANG → lặp lại các tab Hóa đơn/Phiếu nhập/xuất/...).
                    string formOwner = id;
                    string parentId = DbHelper.Str(r["MASTERTABLEID"]);
                    if (type == TableTypeDetail && !string.IsNullOrEmpty(parentId) && parentId != masterId)
                        formOwner = parentId;

                    DataTable forms = DbHelper.Table(conn, null,
                        "SELECT NAME, LOAI FROM SFORM WHERE STABLEDESCID = @T AND FORMTYPE = 0 AND STATUS = @S ORDER BY LOAI, NAME",
                        "@T", formOwner, "@S", ActiveStatus);
                    if (forms.Rows.Count == 0)
                    {
                        AddTab(tabs, hidden, id, name, key, description, -1);
                        continue;
                    }
                    foreach (DataRow f in forms.Rows)
                        AddTab(tabs, hidden, id, name, key, DbHelper.Str(f["NAME"]) ?? description, DbHelper.Int(f["LOAI"]));
                }
                return tabs;
            });
        }

        private static void AddTab(List<DetailTab> tabs, HashSet<string> hidden, string tableId, string tableName,
            string key, string title, int loai)
        {
            if (hidden.Contains(tableId + "|" + loai))
                return; // REFCONFIG liệt kê tab bị ẩn
            tabs.Add(new DetailTab
            {
                Title = title,
                TableName = tableName,
                TableId = tableId,
                KeyColumn = key,
                Loai = loai,
                Columns = DetailColumns.For(tableName)
            });
        }

        /// <summary>STABLEDESC.REFCONFIG (XML: &lt;Data&gt;&lt;ID/&gt;&lt;LOAI/&gt;&lt;/Data&gt;…) → tập "idBảng|LOAI" bị ẩn.</summary>
        private static HashSet<string> LoadHidden(FirebirdSql.Data.FirebirdClient.FbConnection conn, string masterId)
        {
            var hidden = new HashSet<string>(StringComparer.Ordinal);
            object blob = DbHelper.Scalar(conn, null, "SELECT REFCONFIG FROM STABLEDESC WHERE ID = @I", "@I", masterId);
            string xml = null;
            var bytes = blob as byte[];
            if (bytes != null && bytes.Length > 0)
                xml = Encoding.UTF8.GetString(bytes);
            else if (blob is string)
                xml = (string)blob;
            if (string.IsNullOrWhiteSpace(xml))
                return hidden;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml.TrimStart('﻿'));
                foreach (XmlNode data in doc.SelectNodes("//Data"))
                {
                    XmlNode idNode = data.SelectSingleNode("ID");
                    XmlNode loaiNode = data.SelectSingleNode("LOAI");
                    int loai;
                    if (idNode != null && loaiNode != null && int.TryParse(loaiNode.InnerText, out loai))
                        hidden.Add(idNode.InnerText.Trim() + "|" + loai);
                }
            }
            catch (XmlException)
            {
                // REFCONFIG hỏng: coi như không ẩn tab nào.
            }
            return hidden;
        }

        // ------------------------------------------------------------------ Dữ liệu một tab

        /// <summary>Dòng của một tab cho dòng chủ <paramref name="masterId"/>; bảng chưa hỗ trợ trả bảng rỗng.</summary>
        public DataTable LoadRows(DetailTab tab, string masterId)
        {
            if (tab == null || string.IsNullOrEmpty(masterId))
                return new DataTable();
            string key = tab.KeyColumn;
            if (key == null || !KeyPattern.IsMatch(key) || (key != "DBANID" && key != "TDATHANGID"))
                return new DataTable();
            string sql = SqlFor(tab.TableName, key, tab.Loai >= 0);
            if (sql == null)
                return new DataTable();
            return DbHelper.Read(_databasePath, conn =>
            {
                var args = new List<object> { "@M", masterId };
                if (tab.Loai >= 0)
                {
                    args.Add("@L");
                    args.Add(tab.Loai);
                }
                return DbHelper.Table(conn, null, sql, args.ToArray());
            });
        }

        /// <summary>SQL cố định theo bảng (key đã kiểm ở trên, không đến từ người dùng).</summary>
        private static string SqlFor(string table, string key, bool byLoai)
        {
            switch (table)
            {
                case "DBANGGIATHEOBAN":
                    return "SELECT P.NAME AS PRICENAME, B.NOTE FROM DBANGGIATHEOBAN B " +
                        "LEFT JOIN DBANGGIA P ON P.ID = B.DBANGGIAID WHERE B." + key + " = @M AND B.STATUS = 30 ORDER BY P.NAME";
                case "TDATHANG":
                    return "SELECT NGAY, NAME, TENKHACH, DIACHI, DIENTHOAI, EMAIL, TONGCONG, GIODAT, NOTE FROM TDATHANG " +
                        "WHERE " + key + " = @M AND STATUS = 30 ORDER BY NGAY DESC, NAME";
                case "TDONHANG":
                    return "SELECT NGAY, NAME, TIENHANG, TIENGIAMGIA, TONGCONG, DATHANHTOAN, NOTE FROM TDONHANG " +
                        "WHERE " + key + " = @M AND STATUS = 30" + (byLoai ? " AND LOAI = @L" : "") + " ORDER BY NGAY DESC, NAME";
                case "TDONHANGGIO":
                    return "SELECT O.NAME AS ORDERNAME, G.TUGIO, G.DENGIO, G.DONGIA, G.THANHTIEN, G.NOTE FROM TDONHANGGIO G " +
                        "INNER JOIN TDONHANG O ON O.ID = G.TDONHANGID WHERE G." + key + " = @M AND G.STATUS = 30" +
                        (byLoai ? " AND O.LOAI = @L" : "") + " ORDER BY G.TUGIO";
                case "TSUACHUA":
                    return "SELECT NGAY, NAME, NOIDUNG, DASUAXONG, NOTE FROM TSUACHUA " +
                        "WHERE " + key + " = @M AND STATUS = 30 ORDER BY NGAY DESC";
                case "TTHUCHI":
                    return "SELECT NGAY, NAME, TENDOITUONG, DIENGIAI, THU, CHI, NOTE FROM TTHUCHI " +
                        "WHERE " + key + " = @M AND STATUS = 30" + (byLoai ? " AND LOAI = @L" : "") + " ORDER BY NGAY DESC, NAME";
                case "TDATHANGCHITIET":
                    return "SELECT M.NAME AS ITEMNAME, U.NAME AS UNITNAME, D.SOLUONG, D.DONGIA, D.TILEGIAMGIA, D.THANHTIEN, D.NOTE " +
                        "FROM TDATHANGCHITIET D LEFT JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                        "LEFT JOIN DDONVITINH U ON U.ID = D.DDONVITINHID WHERE D.TDATHANGID = @M AND D.STATUS = 30 ORDER BY D.TIMECREATED";
                default:
                    return null;
            }
        }
    }

    /// <summary>Cột hiển thị của lưới từng bảng tab (khớp danh sách cột SELECT trong DetailRepository.SqlFor).</summary>
    internal static class DetailColumns
    {
        private static DetailColumn C(string field, string header, DetailKind kind, int width)
        {
            return new DetailColumn { Field = field, Header = header, Kind = kind, Width = width };
        }

        public static List<DetailColumn> For(string table)
        {
            switch (table)
            {
                case "DBANGGIATHEOBAN":
                    return new List<DetailColumn> { C("PRICENAME", "Bảng giá", DetailKind.Text, 220), C("NOTE", "Ghi chú", DetailKind.Text, 260) };
                case "TDATHANG":
                    return new List<DetailColumn>
                    {
                        C("NGAY", "Ngày", DetailKind.Date, 90), C("NAME", "Số phiếu", DetailKind.Text, 110),
                        C("TENKHACH", "Tên khách", DetailKind.Text, 160), C("DIACHI", "Địa chỉ", DetailKind.Text, 170),
                        C("DIENTHOAI", "Điện thoại", DetailKind.Text, 110), C("EMAIL", "Email", DetailKind.Text, 150),
                        C("TONGCONG", "Tổng cộng", DetailKind.Money, 110), C("GIODAT", "Giờ đặt", DetailKind.DateTime, 130),
                        C("NOTE", "Ghi chú", DetailKind.Text, 180)
                    };
                case "TDONHANG":
                    return new List<DetailColumn>
                    {
                        C("NGAY", "Ngày", DetailKind.Date, 90), C("NAME", "Số phiếu", DetailKind.Text, 120),
                        C("TIENHANG", "Tiền hàng", DetailKind.Money, 110), C("TIENGIAMGIA", "Tiền giảm giá", DetailKind.Money, 110),
                        C("TONGCONG", "Tổng cộng", DetailKind.Money, 110), C("DATHANHTOAN", "Đã thanh toán", DetailKind.Bool, 100),
                        C("NOTE", "Ghi chú", DetailKind.Text, 200)
                    };
                case "TDONHANGGIO":
                    return new List<DetailColumn>
                    {
                        C("ORDERNAME", "Số phiếu", DetailKind.Text, 120), C("TUGIO", "Từ giờ", DetailKind.DateTime, 130),
                        C("DENGIO", "Đến giờ", DetailKind.DateTime, 130), C("DONGIA", "Đơn giá", DetailKind.Money, 100),
                        C("THANHTIEN", "Thành tiền", DetailKind.Money, 110), C("NOTE", "Ghi chú", DetailKind.Text, 200)
                    };
                case "TSUACHUA":
                    return new List<DetailColumn>
                    {
                        C("NGAY", "Ngày", DetailKind.Date, 90), C("NAME", "Số phiếu", DetailKind.Text, 110),
                        C("NOIDUNG", "Nội dung", DetailKind.Text, 240), C("DASUAXONG", "Đã sửa xong", DetailKind.Bool, 100),
                        C("NOTE", "Ghi chú", DetailKind.Text, 200)
                    };
                case "TTHUCHI":
                    return new List<DetailColumn>
                    {
                        C("NGAY", "Ngày", DetailKind.Date, 90), C("NAME", "Số phiếu", DetailKind.Text, 110),
                        C("TENDOITUONG", "Đối tượng", DetailKind.Text, 160), C("DIENGIAI", "Diễn giải", DetailKind.Text, 200),
                        C("THU", "Thu", DetailKind.Money, 100), C("CHI", "Chi", DetailKind.Money, 100),
                        C("NOTE", "Ghi chú", DetailKind.Text, 160)
                    };
                case "TDATHANGCHITIET":
                    return new List<DetailColumn>
                    {
                        C("ITEMNAME", "Mặt hàng", DetailKind.Text, 220), C("UNITNAME", "Đơn vị tính", DetailKind.Text, 90),
                        C("SOLUONG", "Số lượng", DetailKind.Money, 90), C("DONGIA", "Đơn giá", DetailKind.Money, 100),
                        C("TILEGIAMGIA", "Tỉ lệ giảm giá", DetailKind.Money, 100), C("THANHTIEN", "Thành tiền", DetailKind.Money, 110),
                        C("NOTE", "Ghi chú", DetailKind.Text, 180)
                    };
                default:
                    return new List<DetailColumn> { C("NOTE", "Ghi chú", DetailKind.Text, 300) };
            }
        }
    }
}
