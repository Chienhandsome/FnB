using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc danh mục dùng chung (chỉ đọc): metadata <c>STABLEDESC</c>/<c>SCOLUMN</c>/<c>SGRID</c> và dữ liệu bảng danh mục.
    /// Tên bảng/cột lấy từ metadata nên luôn được kiểm tra: đúng dạng định danh và có thật trong schema trước khi ghép vào SQL.
    /// Chỉ lấy dòng đang dùng (STATUS = 30). Không ghi, không đổi schema.
    /// </summary>
    public sealed class CatalogRepository
    {
        public const int ActiveStatus = 30;

        private static readonly Regex Ident = new Regex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant);

        private static readonly HashSet<string> SystemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ID", "STATUS", "USERMODIFIEDID", "TIMEMODIFIED", "TIMECREATED", "USERCREATEDID",
            "SORTORDER", "PARENTID", "PARENTDIR", "ITEMTYPE", "AUTOID", "SIMAGEID"
        };

        private readonly string _databasePath;

        public CatalogRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Mục menu này có dùng được màn danh mục chung không (bảng nhập liệu kiểu phẳng hoặc cây nhóm).</summary>
        public bool IsSupported(string stableDescId)
        {
            if (string.IsNullOrEmpty(stableDescId))
                return false;
            return DbHelper.Read(_databasePath, conn =>
            {
                object t = DbHelper.Scalar(conn, null, "SELECT TABLETYPE FROM STABLEDESC WHERE ID = @id", "@id", stableDescId);
                int type = t == null ? -1 : DbHelper.Int(t);
                return type == 0 || type == 2;
            });
        }

        /// <summary>Tìm STABLEDESC.ID theo tên bảng (dùng cho --render-screen và kiểm thử); null nếu không có.</summary>
        public string FindIdByTable(string tableName)
        {
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT FIRST 1 ID FROM STABLEDESC WHERE NAME = @n", "@n", tableName)));
        }

        public CatalogDefinition LoadDefinition(string stableDescId)
        {
            return DbHelper.Read(_databasePath, conn => LoadDefinition(conn, stableDescId));
        }

        private static CatalogDefinition LoadDefinition(FbConnection conn, string id)
        {
            DataTable head = DbHelper.Table(conn, null,
                "SELECT NAME, DESCRIPTION, TABLETYPE, CATEGORYCOL FROM STABLEDESC WHERE ID = @id", "@id", id);
            if (head.Rows.Count == 0)
                return null;

            DataRow h = head.Rows[0];
            var def = new CatalogDefinition
            {
                StableDescId = id,
                TableName = Upper(DbHelper.Str(h["NAME"])),
                Caption = DbHelper.Str(h["DESCRIPTION"]),
                TableType = h["TABLETYPE"] is DBNull ? -1 : DbHelper.Int(h["TABLETYPE"])
            };
            RequireTable(conn, def.TableName);
            List<string> tableCols = ReadTableColumns(conn, def.TableName);

            // Cột cấu hình trong SCOLUMN (bỏ trùng tên, giữ dòng đầu).
            DataTable sc = DbHelper.Table(conn, null,
                "SELECT c.NAME, c.CAPTION, c.CONTROLTYPE, c.FORMAT, " +
                "(SELECT r.NAME FROM STABLEDESC r WHERE r.ID = c.REFTABLEID) AS REFNAME " +
                "FROM SCOLUMN c WHERE c.STABLEDESCID = @id AND c.STATUS = 30", "@id", id);
            var candidates = new Dictionary<string, CatalogColumn>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in sc.Rows)
            {
                string name = Upper(DbHelper.Str(r["NAME"]));
                string control = DbHelper.Str(r["CONTROLTYPE"]) ?? "Text";
                if (name == null || candidates.ContainsKey(name) || !tableCols.Contains(name))
                    continue;
                if (SystemColumns.Contains(name))
                    continue;
                if (control == "NotShow" || control == "Image")
                    continue;
                string refTable = Upper(DbHelper.Str(r["REFNAME"]));
                bool lookup = control == "Lookup" && !string.IsNullOrEmpty(refTable) && Ident.IsMatch(refTable);
                candidates[name] = new CatalogColumn
                {
                    Name = name,
                    Caption = string.IsNullOrWhiteSpace(DbHelper.Str(r["CAPTION"])) ? name : DbHelper.Str(r["CAPTION"]).Trim(),
                    ControlType = control,
                    Format = (DbHelper.Str(r["FORMAT"]) ?? "").Trim(),
                    RefTable = lookup ? refTable : null,
                    ClrType = ClrTypeOf(control)
                };
            }

            // Thứ tự/độ rộng: dùng cấu hình lưới SGRID của app gốc nếu có (khách hàng, mặt hàng);
            // không thì NAME trước, rồi thứ tự cột trong bảng, NOTE cuối.
            List<GridColumnSpec> grid = FindGridSpec(conn, candidates);
            if (grid != null)
            {
                foreach (GridColumnSpec g in grid)
                {
                    if (!g.Visible)
                        continue;
                    CatalogColumn c = candidates[g.Name];
                    c.Width = g.Width;
                    def.Columns.Add(c);
                }
            }
            else
            {
                var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (candidates.ContainsKey("NAME"))
                {
                    def.Columns.Add(candidates["NAME"]);
                    done.Add("NAME");
                }
                foreach (string col in tableCols)
                {
                    if (col == "NOTE" || done.Contains(col) || !candidates.ContainsKey(col))
                        continue;
                    def.Columns.Add(candidates[col]);
                    done.Add(col);
                }
                if (candidates.ContainsKey("NOTE") && !done.Contains("NOTE"))
                    def.Columns.Add(candidates["NOTE"]);
            }

            // Cây nhóm (chỉ kiểu 2 có CATEGORYCOL và cột đó là Lookup tới một bảng cây).
            string categoryCol = Upper(DbHelper.Str(h["CATEGORYCOL"]));
            if (def.TableType == 2 && !string.IsNullOrEmpty(categoryCol) && tableCols.Contains(categoryCol))
            {
                DataTable cat = DbHelper.Table(conn, null,
                    "SELECT (SELECT r.NAME FROM STABLEDESC r WHERE r.ID = c.REFTABLEID) AS REFNAME " +
                    "FROM SCOLUMN c WHERE c.STABLEDESCID = @id AND c.NAME = @col AND c.STATUS = 30",
                    "@id", id, "@col", categoryCol);
                string treeTable = cat.Rows.Count == 0 ? null : Upper(DbHelper.Str(cat.Rows[0]["REFNAME"]));
                if (!string.IsNullOrEmpty(treeTable) && Ident.IsMatch(treeTable) && TableExists(conn, treeTable))
                {
                    List<string> treeCols = ReadTableColumns(conn, treeTable);
                    if (treeCols.Contains("ID") && treeCols.Contains("NAME") && treeCols.Contains("PARENTID") && treeCols.Contains("STATUS"))
                    {
                        def.CategoryColumn = categoryCol;
                        def.CategoryTable = treeTable;
                    }
                }
            }
            return def;
        }

        public CatalogData Load(CatalogDefinition def)
        {
            return DbHelper.Read(_databasePath, conn => Load(conn, def));
        }

        private static CatalogData Load(FbConnection conn, CatalogDefinition def)
        {
            RequireTable(conn, def.TableName);
            var data = new CatalogData();

            // Tên hiển thị của các cột Lookup.
            var lookups = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (CatalogColumn c in def.Columns)
                if (c.IsLookup && !lookups.ContainsKey(c.RefTable))
                    lookups[c.RefTable] = ReadNames(conn, c.RefTable);

            if (def.CategoryTable != null)
                data.Nodes.AddRange(ReadTree(conn, def.CategoryTable));

            var sql = new StringBuilder("SELECT \"ID\"");
            foreach (CatalogColumn c in def.Columns)
                sql.Append(", \"").Append(c.Name).Append('"');
            if (def.CategoryColumn != null)
                sql.Append(", \"").Append(def.CategoryColumn).Append('"');
            sql.Append(" FROM \"").Append(def.TableName).Append("\" WHERE STATUS = @status");

            using (FbCommand cmd = DbHelper.Command(conn, null, sql.ToString(), "@status", ActiveStatus))
            using (FbDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var row = new CatalogRow { Id = Convert.ToString(r[0]), Values = new object[def.Columns.Count] };
                    for (int i = 0; i < def.Columns.Count; i++)
                    {
                        CatalogColumn c = def.Columns[i];
                        object v = r[i + 1];
                        if (v is DBNull)
                            v = null;
                        if (v != null && c.IsLookup)
                        {
                            string key = Convert.ToString(v);
                            string shown;
                            v = lookups[c.RefTable].TryGetValue(key, out shown) ? shown : "";
                        }
                        else if (v != null)
                        {
                            v = Coerce(v, c);
                        }
                        row.Values[i] = v;
                    }
                    if (def.CategoryColumn != null)
                    {
                        object cv = r[def.Columns.Count + 1];
                        row.CategoryId = cv is DBNull ? null : Convert.ToString(cv);
                    }
                    data.Rows.Add(row);
                }
            }
            return data;
        }

        // ------------------------------------------------------------------ Hỗ trợ

        private static object Coerce(object v, CatalogColumn c)
        {
            if (c.ClrType == typeof(bool))
            {
                if (v is bool)
                    return v;
                decimal d;
                return decimal.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d) && d != 0m;
            }
            if (c.ClrType == typeof(decimal))
                return Convert.ToDecimal(v, CultureInfo.InvariantCulture);
            if (c.ClrType == typeof(DateTime))
                return v is DateTime ? v : (object)null;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static Type ClrTypeOf(string control)
        {
            switch (control)
            {
                case "Number": return typeof(decimal);
                case "CheckBox": return typeof(bool);
                case "Date": return typeof(DateTime);
                default: return typeof(string);
            }
        }

        /// <summary>Tên bảng/cột an toàn để ghép vào SQL (chữ hoa, số, gạch dưới).</summary>
        internal static bool IsIdentifier(string name)
        {
            return name != null && Ident.IsMatch(name);
        }

        private static string Upper(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
        }

        internal static bool TableExists(FbConnection conn, string table)
        {
            object n = DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = @t AND (RDB$SYSTEM_FLAG = 0 OR RDB$SYSTEM_FLAG IS NULL) AND RDB$VIEW_BLR IS NULL",
                "@t", table);
            return DbHelper.Int(n) > 0;
        }

        private static void RequireTable(FbConnection conn, string table)
        {
            if (table == null || !Ident.IsMatch(table) || !TableExists(conn, table))
                throw new InvalidDataException("Bảng danh mục không hợp lệ: " + table);
        }

        internal static List<string> ReadTableColumns(FbConnection conn, string table)
        {
            var list = new List<string>();
            DataTable t = DbHelper.Table(conn, null,
                "SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = @t ORDER BY RDB$FIELD_POSITION",
                "@t", table);
            foreach (DataRow r in t.Rows)
                list.Add(Convert.ToString(r[0]).Trim());
            return list;
        }

        private static Dictionary<string, string> ReadNames(FbConnection conn, string table)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Ident.IsMatch(table) || !TableExists(conn, table))
                return map;
            List<string> cols = ReadTableColumns(conn, table);
            if (!cols.Contains("ID") || !cols.Contains("NAME"))
                return map;
            using (FbCommand cmd = DbHelper.Command(conn, null, "SELECT \"ID\", \"NAME\" FROM \"" + table + "\""))
            using (FbDataReader r = cmd.ExecuteReader())
                while (r.Read())
                    map[Convert.ToString(r[0])] = r[1] is DBNull ? "" : Convert.ToString(r[1]);
            return map;
        }

        private static List<CatalogNode> ReadTree(FbConnection conn, string table)
        {
            var nodes = new List<CatalogNode>();
            bool hasSort = ReadTableColumns(conn, table).Contains("SORTORDER");
            string sql = "SELECT \"ID\", \"NAME\", \"PARENTID\"" + (hasSort ? ", \"SORTORDER\"" : "") +
                         " FROM \"" + table + "\" WHERE STATUS = @status";
            using (FbCommand cmd = DbHelper.Command(conn, null, sql, "@status", ActiveStatus))
            using (FbDataReader r = cmd.ExecuteReader())
                while (r.Read())
                    nodes.Add(new CatalogNode
                    {
                        Id = Convert.ToString(r[0]),
                        Name = r[1] is DBNull ? "" : Convert.ToString(r[1]),
                        ParentId = r[2] is DBNull ? null : Convert.ToString(r[2]),
                        SortOrder = hasSort && !(r[3] is DBNull) ? Convert.ToString(r[3]) : ""
                    });
            return nodes;
        }

        /// <summary>
        /// SGRID lưu cấu hình lưới dạng "TÊNCỘT|rộng|hiện|TÊNCỘT|rộng|hiện|…" và không có cột liên kết tới bảng,
        /// nên chọn dòng nào có mọi tên cột đều thuộc bảng này (và ít nhất 3 cột). Không có thì trả null.
        /// </summary>
        private static List<GridColumnSpec> FindGridSpec(FbConnection conn, Dictionary<string, CatalogColumn> candidates)
        {
            List<GridColumnSpec> best = null;
            DataTable t = DbHelper.Table(conn, null, "SELECT NAME FROM SGRID");
            foreach (DataRow r in t.Rows)
            {
                List<GridColumnSpec> spec = ParseGrid(DbHelper.Str(r[0]));
                if (spec.Count < 3)
                    continue;
                bool all = true;
                foreach (GridColumnSpec g in spec)
                    if (!candidates.ContainsKey(g.Name))
                    {
                        all = false;
                        break;
                    }
                if (all && (best == null || spec.Count > best.Count))
                    best = spec;
            }
            return best;
        }

        /// <summary>Tách chuỗi cấu hình lưới SGRID (bộ ba tên|rộng|hiện).</summary>
        public static List<GridColumnSpec> ParseGrid(string text)
        {
            var list = new List<GridColumnSpec>();
            if (string.IsNullOrWhiteSpace(text))
                return list;
            string[] p = text.Split('|');
            for (int i = 0; i + 2 < p.Length; i += 3)
            {
                string name = p[i].Trim().ToUpperInvariant();
                if (name.Length == 0)
                    continue;
                int w;
                int.TryParse(p[i + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out w);
                list.Add(new GridColumnSpec
                {
                    Name = name,
                    Width = w,
                    Visible = string.Equals(p[i + 2].Trim(), "true", StringComparison.OrdinalIgnoreCase)
                });
            }
            return list;
        }

        // ------------------------------------------------------------------ Tìm kiếm và lọc (không đụng DB)

        /// <summary>Chữ thường, bỏ dấu tiếng Việt (đ → d) để tìm "dang" ra "Đặng".</summary>
        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            string d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char ch in d)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark)
                    continue;
                if (ch == 'đ' || ch == 'Đ')
                    sb.Append('d');
                else
                    sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>Thoát ký tự đặc biệt để ghép vào DataView.RowFilter dạng LIKE '%...%'.</summary>
        public static string EscapeLike(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s ?? "")
            {
                switch (ch)
                {
                    case '[':
                    case ']':
                    case '*':
                    case '%':
                        sb.Append('[').Append(ch).Append(']');
                        break;
                    case '\'':
                        sb.Append("''");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Chuỗi đường đi "|gốc|...|nhóm|" của một nhóm, để lọc "nhóm này và mọi nhóm con".</summary>
        public static string CategoryPath(IDictionary<string, CatalogNode> nodes, string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId))
                return "";
            var parts = new List<string>();
            string cur = categoryId;
            int guard = 0;
            while (!string.IsNullOrEmpty(cur) && guard++ < 64)
            {
                parts.Add(cur);
                CatalogNode n;
                if (!nodes.TryGetValue(cur, out n) || n.ParentId == cur)
                    break;
                cur = n.ParentId;
            }
            parts.Reverse();
            return "|" + string.Join("|", parts.ToArray()) + "|";
        }
    }
}
