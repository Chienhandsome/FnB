using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Nhóm người dùng (SGROUPUSER). AUTOID = số lớn nhất + 1, SORTORDER = "ZZZ" + số thứ tự 3 chữ số (theo dữ liệu hiện có
    /// của app gốc), ITEMTYPE = 0, PARENTID/PARENTDIR rỗng.
    /// </summary>
    public sealed class GroupRepository
    {
        private readonly string _databasePath;

        public GroupRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public List<GroupRow> LoadAll()
        {
            var list = new List<GroupRow>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, NOTE, SIMAGEID FROM SGROUPUSER WHERE STATUS = 30 ORDER BY SORTORDER, NAME");
                foreach (DataRow r in dt.Rows)
                    list.Add(new GroupRow { Id = DbHelper.Str(r[0]), Name = DbHelper.Str(r[1]) ?? "", Note = DbHelper.Str(r[2]) ?? "", ImageId = DbHelper.Str(r[3]) });
            }
            return list;
        }

        public bool IsNameTaken(string name, string excludeId)
        {
            string v = (name ?? "").Trim();
            foreach (GroupRow g in LoadAll())
                if (g.Id != excludeId && string.Equals(g.Name.Trim(), v, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public string Save(GroupRow g, string actorId)
        {
            bool isNew = string.IsNullOrEmpty(g.Id);
            string id = isNew ? DbHelper.NewId() : g.Id;
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DateTime now = DbHelper.Now();
                if (isNew)
                {
                    int autoId = DbHelper.Int(DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM SGROUPUSER")) + 1;
                    int seq = 0;
                    foreach (DataRow r in DbHelper.Table(conn, tx, "SELECT SORTORDER FROM SGROUPUSER").Rows)
                    {
                        string so = DbHelper.Str(r[0]) ?? "";
                        int n;
                        if (so.StartsWith("ZZZ", StringComparison.Ordinal) && int.TryParse(so.Substring(3), out n))
                            seq = Math.Max(seq, n);
                    }
                    string sort = "ZZZ" + (seq + 1).ToString("000");
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO SGROUPUSER (ID, NAME, NOTE, STATUS, USERCREATEDID, TIMECREATED, SORTORDER, PARENTID, PARENTDIR, ITEMTYPE, AUTOID, SIMAGEID) " +
                        "VALUES (@I, @N, @NO, 30, @A, @T, @S, '', '', 0, @AI, @IM)",
                        "@I", id, "@N", g.Name.Trim(), "@NO", g.Note ?? "", "@A", actorId, "@T", now, "@S", sort, "@AI", autoId,
                        "@IM", string.IsNullOrEmpty(g.ImageId) ? null : g.ImageId);
                }
                else
                {
                    DbHelper.Exec(conn, tx,
                        "UPDATE SGROUPUSER SET NAME = @N, NOTE = @NO, SIMAGEID = @IM, USERMODIFIEDID = @A, TIMEMODIFIED = @T WHERE ID = @I",
                        "@N", g.Name.Trim(), "@NO", g.Note ?? "", "@IM", string.IsNullOrEmpty(g.ImageId) ? null : g.ImageId,
                        "@A", actorId, "@T", now, "@I", id);
                }
            });
            return id;
        }

        /// <summary>Số tài khoản còn thuộc nhóm (dòng quyền SGROUPROLE/SREPORTROLE đi theo nhóm nên không tính).</summary>
        public int CountReferences(string groupId)
        {
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
                return ReferenceChecker.Count(conn, null, "SGROUPUSERID", groupId, "SGROUPROLE", "SREPORTROLE");
        }

        /// <summary>Xóa nhóm cùng toàn bộ dòng quyền của nó. Người gọi kiểm tra CountReferences trước.</summary>
        public void Delete(string groupId)
        {
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DbHelper.Exec(conn, tx, "DELETE FROM SGROUPROLE WHERE SGROUPUSERID = @G", "@G", groupId);
                DbHelper.Exec(conn, tx, "DELETE FROM SREPORTROLE WHERE SGROUPUSERID = @G", "@G", groupId);
                DbHelper.Exec(conn, tx, "DELETE FROM SGROUPUSER WHERE ID = @G", "@G", groupId);
            });
        }
    }
}
