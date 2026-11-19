using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>Đọc/ghi SCONFIG và SCONFIGGROUP (bảng khóa–giá trị; khóa = NAME).</summary>
    public sealed class ConfigRepository
    {
        private static readonly HashSet<string> HiddenGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ẩn", "Ứng dụng", "Mẫu hóa đơn", "Số phiếu"
        };

        private readonly string _databasePath;

        public ConfigRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Mọi khóa → giá trị (cho SystemConfig). Bỏ qua cột blob.</summary>
        public Dictionary<string, ConfigValue> LoadValues()
        {
            var map = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT NAME, DATATYPE, INTVALUE, DECIMALVALUE, TEXTVALUE FROM SCONFIG WHERE NAME IS NOT NULL");
                foreach (DataRow r in dt.Rows)
                {
                    string name = DbHelper.Str(r[0]);
                    if (string.IsNullOrEmpty(name))
                        continue;
                    map[name] = new ConfigValue
                    {
                        DataType = DbHelper.Int(r[1]),
                        IntValue = DbHelper.Int(r[2]),
                        DecimalValue = DbHelper.Dec(r[3]),
                        TextValue = DbHelper.Str(r[4])
                    };
                }
            }
            return map;
        }

        /// <summary>Nhóm hiển thị trên màn Cấu hình (đang dùng, trừ nhóm "Ẩn"), theo thứ tự.</summary>
        public List<ConfigGroup> LoadVisibleGroups()
        {
            var list = new List<ConfigGroup>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, SORTORDER FROM SCONFIGGROUP WHERE STATUS = 30 ORDER BY SORTORDER, NAME");
                foreach (DataRow r in dt.Rows)
                {
                    string name = DbHelper.Str(r[1]);
                    // "Ẩn": cờ nội bộ. "Ứng dụng": không có trên màn gốc. "Mẫu hóa đơn": khu của nhà phát triển (khóa bằng
                    // mật khẩu). "Số phiếu": định dạng đánh số chứng từ — làm riêng vì ảnh hưởng đánh số hóa đơn.
                    if (HiddenGroups.Contains(name))
                        continue;
                    list.Add(new ConfigGroup { Id = DbHelper.Str(r[0]), Name = name, SortOrder = DbHelper.Str(r[2]) });
                }
            }
            return list;
        }

        /// <summary>Các khóa có nhãn (CAPTION) thuộc nhóm hiển thị, theo nhóm/tab/thứ tự. Khóa nội bộ (không nhãn) bị loại.</summary>
        public List<ConfigItem> LoadItems()
        {
            var list = new List<ConfigItem>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, CAPTION, SCONFIGGROUPID, TAB, SORTORDER, DATATYPE, CONTROLTYPE, " +
                    "INTVALUE, DECIMALVALUE, TEXTVALUE, REFTABLEID, MOREDETAIL, OTHERCONFIG " +
                    "FROM SCONFIG WHERE CAPTION IS NOT NULL AND CAPTION <> '' AND SCONFIGGROUPID IS NOT NULL " +
                    "ORDER BY SCONFIGGROUPID, SORTORDER");
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new ConfigItem
                    {
                        Id = DbHelper.Str(r[0]),
                        Name = DbHelper.Str(r[1]),
                        Caption = DbHelper.Str(r[2]),
                        GroupId = DbHelper.Str(r[3]),
                        Tab = DbHelper.Int(r[4]),
                        SortOrder = DbHelper.Int(r[5]),
                        DataType = DbHelper.Int(r[6]),
                        ControlType = DbHelper.Int(r[7]),
                        IntValue = DbHelper.Int(r[8]),
                        DecimalValue = DbHelper.Dec(r[9]),
                        TextValue = DbHelper.Str(r[10]),
                        RefTableId = DbHelper.Str(r[11]),
                        MoreDetail = DbHelper.Str(r[12]),
                        OtherConfig = DbHelper.Str(r[13])
                    });
                }
            }
            return list;
        }

        private static readonly System.Text.RegularExpressions.Regex IdentifierRx =
            new System.Text.RegularExpressions.Regex("^[A-Za-z][A-Za-z0-9_]*$");
        private static readonly System.Text.RegularExpressions.Regex FilterRx =
            new System.Text.RegularExpressions.Regex("^[A-Za-z][A-Za-z0-9_]* = '[0-9a-fA-F-]{36}'$");

        /// <summary>
        /// Danh sách chọn (ID, NAME) cho ô kiểu "Lookup": bảng lấy từ STABLEDESC theo REFTABLEID, có thể lọc theo
        /// OtherConfig dạng CỘT = 'guid'. Tên bảng/bộ lọc không đúng khuôn thì bỏ qua (siêu dữ liệu lạ không được chạy).
        /// </summary>
        public List<KeyValuePair<string, string>> LoadLookup(string refTableId, string filter)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(refTableId))
                return list;
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                string table = DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT NAME FROM STABLEDESC WHERE ID = @I", "@I", refTableId));
                if (string.IsNullOrEmpty(table) || !IdentifierRx.IsMatch(table))
                    return list;
                string where = !string.IsNullOrEmpty(filter) && FilterRx.IsMatch(filter.Trim()) ? " WHERE " + filter.Trim() : "";
                DataTable dt;
                try
                {
                    dt = DbHelper.Table(conn, null, "SELECT ID, NAME FROM " + table + where + " ORDER BY NAME");
                }
                catch (DataAccessException)
                {
                    return list;
                }
                foreach (DataRow r in dt.Rows)
                    list.Add(new KeyValuePair<string, string>(DbHelper.Str(r[0]), DbHelper.Str(r[1]) ?? ""));
            }
            return list;
        }

        /// <summary>Ảnh (blob) của một khóa, ví dụ Logo. Null nếu chưa có.</summary>
        public byte[] LoadBlob(string name)
        {
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                object v = DbHelper.Scalar(conn, null, "SELECT BLOBVALUE FROM SCONFIG WHERE NAME = @N", "@N", name);
                return v as byte[];
            }
        }

        /// <summary>
        /// Ghi các thay đổi trong MỘT transaction, theo NAME. Khóa chưa có dòng thì bỏ qua (không tự tạo).
        /// Trả về số khóa đã ghi.
        /// </summary>
        public int Save(IEnumerable<ConfigChange> changes, string userId)
        {
            int written = 0;
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DateTime now = DbHelper.Now();
                foreach (ConfigChange c in changes)
                {
                    int n;
                    if (c.IsBlob)
                    {
                        n = DbHelper.Exec(conn, tx,
                            "UPDATE SCONFIG SET BLOBVALUE = @V, TIMEMODIFIED = @T, USERMODIFIEDID = @U WHERE NAME = @N",
                            "@V", c.BlobValue, "@T", now, "@U", userId, "@N", c.Name);
                    }
                    else if (c.DataType == 1)
                    {
                        n = DbHelper.Exec(conn, tx,
                            "UPDATE SCONFIG SET TEXTVALUE = @V, TIMEMODIFIED = @T, USERMODIFIEDID = @U WHERE NAME = @N",
                            "@V", c.TextValue, "@T", now, "@U", userId, "@N", c.Name);
                    }
                    else
                    {
                        n = DbHelper.Exec(conn, tx,
                            "UPDATE SCONFIG SET INTVALUE = @V, DECIMALVALUE = @D, TIMEMODIFIED = @T, USERMODIFIEDID = @U WHERE NAME = @N",
                            "@V", c.IntValue, "@D", c.DecimalValue, "@T", now, "@U", userId, "@N", c.Name);
                    }
                    if (n > 0)
                        written++;
                }
            });
            return written;
        }
    }
}
