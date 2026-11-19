using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace Kazuko_FnB.Data
{
    /// <summary>Một chức năng (SFUNCTION) hiển thị trong ma trận phân quyền.</summary>
    public sealed class FunctionRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
        /// <summary>Chức năng chỉ có quyền Có/Không (SFUNCTION.NOTE = 'View'): chỉ có cột Khóa + Xem.</summary>
        public bool ViewOnly { get; set; }
    }

    /// <summary>Một báo cáo hoặc thư mục báo cáo (SREPORT) cho phân quyền xem báo cáo.</summary>
    public sealed class ReportRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public bool IsFolder { get; set; }
    }

    /// <summary>
    /// Phân quyền theo nhóm: quyền sử dụng chức năng (SGROUPROLE.MODE = cờ bit Xem 0x10, Sửa 0x20, Thêm 0x40, Xóa 0x80,
    /// 0 = khóa) và quyền xem báo cáo (SREPORTROLE, có dòng MODE = 30 là được xem).
    /// </summary>
    public sealed class RoleRepository
    {
        public const int ReportAllowed = 30;

        private readonly string _databasePath;

        public RoleRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public List<FunctionRow> LoadFunctions()
        {
            var list = new List<FunctionRow>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, GROUPNAME, NOTE FROM SFUNCTION WHERE STATUS = 30 ORDER BY GROUPNAME, NAME");
                foreach (DataRow r in dt.Rows)
                    list.Add(new FunctionRow
                    {
                        Id = DbHelper.Str(r[0]),
                        Name = DbHelper.Str(r[1]) ?? "",
                        GroupName = DbHelper.Str(r[2]) ?? "",
                        ViewOnly = string.Equals((DbHelper.Str(r[3]) ?? "").Trim(), "View", StringComparison.OrdinalIgnoreCase)
                    });
            }
            return list;
        }

        /// <summary>Cờ quyền của nhóm theo từng chức năng (không có dòng = 0).</summary>
        public Dictionary<string, int> LoadModes(string groupId)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT SFUNCTIONID, MAX(COALESCE(MODE, 0)) FROM SGROUPROLE WHERE SGROUPUSERID = @G GROUP BY SFUNCTIONID", "@G", groupId);
                foreach (DataRow r in dt.Rows)
                    map[DbHelper.Str(r[0])] = DbHelper.Int(r[1]);
            }
            return map;
        }

        /// <summary>Ghi cờ quyền của nhóm cho các chức năng (cập nhật dòng có sẵn, chưa có thì thêm). Một transaction.</summary>
        public void SaveModes(string groupId, IDictionary<string, int> modes, string actorId)
        {
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DateTime now = DbHelper.Now();
                foreach (KeyValuePair<string, int> kv in modes)
                {
                    int n = DbHelper.Exec(conn, tx,
                        "UPDATE SGROUPROLE SET MODE = @M, USERMODIFIEDID = @A, TIMEMODIFIED = @T WHERE SGROUPUSERID = @G AND SFUNCTIONID = @F",
                        "@M", kv.Value, "@A", actorId, "@T", now, "@G", groupId, "@F", kv.Key);
                    if (n == 0 && kv.Value != 0)
                    {
                        DbHelper.Exec(conn, tx,
                            "INSERT INTO SGROUPROLE (ID, STATUS, USERCREATEDID, TIMECREATED, SGROUPUSERID, SFUNCTIONID, MODE) VALUES (@I, 30, @A, @T, @G, @F, @M)",
                            "@I", DbHelper.NewId(), "@A", actorId, "@T", now, "@G", groupId, "@F", kv.Key, "@M", kv.Value);
                    }
                }
            });
        }

        // ------------------------------------------------------------------ xem báo cáo

        public List<ReportRow> LoadReports()
        {
            var list = new List<ReportRow>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, PARENTID, ITEMTYPE FROM SREPORT WHERE STATUS = 30 ORDER BY SORTORDER, NAME");
                foreach (DataRow r in dt.Rows)
                    list.Add(new ReportRow
                    {
                        Id = DbHelper.Str(r[0]),
                        Name = DbHelper.Str(r[1]) ?? "",
                        ParentId = DbHelper.Str(r[2]) ?? "",
                        IsFolder = DbHelper.Int(r[3]) == 1
                    });
            }
            return list;
        }

        /// <summary>ID các báo cáo mà nhóm được phép xem.</summary>
        public HashSet<string> LoadAllowedReports(string groupId)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT SREPORTID FROM SREPORTROLE WHERE SGROUPUSERID = @G AND COALESCE(MODE, 0) = " + ReportAllowed, "@G", groupId);
                foreach (DataRow r in dt.Rows)
                    set.Add(DbHelper.Str(r[0]));
            }
            return set;
        }

        /// <summary>Cho/không cho nhóm xem các báo cáo. Cho xem = có dòng MODE 30; không cho = xóa dòng.</summary>
        public void SetReportsAllowed(string groupId, IEnumerable<string> reportIds, bool allowed, string actorId)
        {
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DateTime now = DbHelper.Now();
                foreach (string rid in reportIds)
                {
                    DbHelper.Exec(conn, tx, "DELETE FROM SREPORTROLE WHERE SGROUPUSERID = @G AND SREPORTID = @R", "@G", groupId, "@R", rid);
                    if (allowed)
                    {
                        DbHelper.Exec(conn, tx,
                            "INSERT INTO SREPORTROLE (ID, STATUS, MODE, USERCREATEDID, TIMECREATED, SGROUPUSERID, SREPORTID) VALUES (@I, 30, @M, @A, @T, @G, @R)",
                            "@I", DbHelper.NewId(), "@M", ReportAllowed, "@A", actorId, "@T", now, "@G", groupId, "@R", rid);
                    }
                }
            });
        }
    }
}
