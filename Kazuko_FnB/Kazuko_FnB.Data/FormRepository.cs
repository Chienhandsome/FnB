using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc metadata của form (layout) và danh sách cho các ô tra cứu (chỉ đọc).
    /// Tên bảng/cột đi vào SQL luôn được kiểm tra dạng định danh và có thật trong schema.
    /// </summary>
    public sealed class FormRepository
    {
        private readonly string _databasePath;

        public FormRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Layout (<c>SFORM.AELAYOUT</c>) của form theo tên; null nếu không có.</summary>
        public ScreenLayout LoadLayout(string formName)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null,
                    "SELECT FIRST 1 AELAYOUT FROM SFORM WHERE NAME = @n AND STATUS = 30", "@n", formName);
                string xml = v is byte[] ? Encoding.UTF8.GetString((byte[])v) : DbHelper.Str(v);
                return string.IsNullOrWhiteSpace(xml) ? null : LayoutParser.Parse(xml);
            });
        }

        /// <summary>Tên bảng của một bảng nhập liệu (STABLEDESC.NAME) theo ID; null nếu không có.</summary>
        public string TableNameOf(string stableDescId)
        {
            if (string.IsNullOrEmpty(stableDescId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                string n = DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT NAME FROM STABLEDESC WHERE ID = @id", "@id", stableDescId));
                return string.IsNullOrWhiteSpace(n) ? null : n.Trim().ToUpperInvariant();
            });
        }

        /// <summary>Cột nào của bảng trỏ tới bảng nào (SCOLUMN kiểu Lookup): tên cột → tên bảng tham chiếu.</summary>
        public Dictionary<string, string> LookupTables(string table)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                DataTable t = DbHelper.Table(conn, null,
                    "SELECT c.NAME, (SELECT r.NAME FROM STABLEDESC r WHERE r.ID = c.REFTABLEID) AS REFNAME " +
                    "FROM SCOLUMN c JOIN STABLEDESC d ON d.ID = c.STABLEDESCID " +
                    "WHERE d.NAME = @t AND c.STATUS = 30 AND c.CONTROLTYPE = 'Lookup'", "@t", table);
                foreach (DataRow r in t.Rows)
                {
                    string col = DbHelper.Str(r["NAME"]);
                    string refTable = DbHelper.Str(r["REFNAME"]);
                    if (!string.IsNullOrWhiteSpace(col) && !string.IsNullOrWhiteSpace(refTable))
                        map[col.Trim()] = refTable.Trim().ToUpperInvariant();
                }
                return map;
            });
        }

        // ------------------------------------------------------------------ Danh sách chọn

        /// <summary>Mọi dòng đang dùng của bảng (STATUS = 30), xếp theo tên.</summary>
        public List<LookupItem> LoadAll(string table)
        {
            return Load(table, null, null);
        }

        /// <summary>Lý do THU (DLYDOTHUCHI có LALYDOTHU = 30), đúng bộ lọc của form Phiếu thu gốc.</summary>
        public List<LookupItem> LoadIncomeReasons()
        {
            return Load("DLYDOTHUCHI", "LALYDOTHU = 30", null);
        }

        /// <summary>Lý do CHI (DLYDOTHUCHI có LALYDOTHU &lt;&gt; 30), đúng spec phiếu thu chi §2.</summary>
        public List<LookupItem> LoadExpenseReasons()
        {
            return Load("DLYDOTHUCHI", "COALESCE(LALYDOTHU, 0) <> 30", null);
        }

        /// <summary>Cửa hàng mà người dùng được gán (TNGUOIDUNGTHEOCUAHANG), dùng cho người không phải admin.</summary>
        public List<LookupItem> LoadStoresOfUser(string userId)
        {
            return Load("DCUAHANG", "ID IN (SELECT DCUAHANGID FROM TNGUOIDUNGTHEOCUAHANG WHERE SUSERID = @user)", userId);
        }

        // Điều kiện thêm chỉ nhận chuỗi cố định do mã gọi truyền vào (không bao giờ từ dữ liệu hay người dùng).
        private List<LookupItem> Load(string table, string fixedCondition, string userId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                string name = (table ?? "").Trim().ToUpperInvariant();
                if (!CatalogRepository.IsIdentifier(name) || !CatalogRepository.TableExists(conn, name))
                    throw new System.IO.InvalidDataException("Bảng không hợp lệ: " + table);
                List<string> cols = CatalogRepository.ReadTableColumns(conn, name);
                if (!cols.Contains("ID") || !cols.Contains("NAME"))
                    throw new System.IO.InvalidDataException("Bảng không có ID/NAME: " + name);
                bool hasAddress = cols.Contains("DIACHI");

                string sql = "SELECT \"ID\", \"NAME\"" + (hasAddress ? ", \"DIACHI\"" : "") +
                             " FROM \"" + name + "\" WHERE STATUS = @status" +
                             (fixedCondition == null ? "" : " AND " + fixedCondition);
                var list = new List<LookupItem>();
                using (FbCommand cmd = userId == null
                    ? DbHelper.Command(conn, null, sql, "@status", CatalogRepository.ActiveStatus)
                    : DbHelper.Command(conn, null, sql, "@status", CatalogRepository.ActiveStatus, "@user", userId))
                using (FbDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(new LookupItem
                        {
                            Id = Convert.ToString(r[0]),
                            Name = r[1] is DBNull ? "" : Convert.ToString(r[1]),
                            Address = hasAddress && !(r[2] is DBNull) ? Convert.ToString(r[2]) : (hasAddress ? "" : null)
                        });
                }
                CompareInfo vi = new CultureInfo("vi-VN").CompareInfo;
                list.Sort(delegate (LookupItem a, LookupItem b) { return vi.Compare(a.Name, b.Name, CompareOptions.IgnoreCase); });
                return list;
            });
        }
    }
}
