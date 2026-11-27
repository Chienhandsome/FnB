using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục lý do thu chi (DLYDOTHUCHI, docs/specs/phieu-thu-chi.md §3).
    /// Kiểm trùng Tên (UNICODE_CI_AI); không cho sửa LOAILYDO hoặc xóa lý do hệ thống;
    /// xóa cứng chỉ khi không còn tham chiếu (TTHUCHI.DLYDOTHUCHIID).
    /// </summary>
    public sealed class CashReasonRepository
    {
        private const int ActiveStatus = 30;

        private const string SelectSql =
            "SELECT L.ID, L.NAME, COALESCE(L.LALYDOTHU, 0) AS LALYDOTHU, L.LOAILYDO, L.NOTE, " +
            "COALESCE(L.STATUS, 0) AS STATUS, L.USERCREATEDID, L.TIMECREATED, L.USERMODIFIEDID, L.TIMEMODIFIED, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DLYDOTHUCHI L " +
            "LEFT JOIN SUSER UC ON UC.ID = L.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = L.USERMODIFIEDID ";

        private readonly string _databasePath;

        public CashReasonRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public List<CashReasonRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE L.STATUS = @S ORDER BY L.NAME",
                    "@S", ActiveStatus);
                var list = new List<CashReasonRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        public CashReasonRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        private static CashReasonRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE L.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        public SimpleCatalogSaveResult Save(CashReasonRow row, string actorId)
        {
            var result = new SimpleCatalogSaveResult();
            if (row == null)
            {
                result.ErrorMessage = VoucherConstants.ReasonNotFound;
                return result;
            }

            string name = (row.Name ?? "").Trim();
            if (name.Length == 0)
            {
                result.ErrorMessage = VoucherConstants.ReasonNameEmpty;
                return result;
            }
            if (name.Length > VoucherConstants.MaxTextLength)
            {
                result.ErrorMessage = VoucherConstants.FieldTooLong("Tên lý do", VoucherConstants.MaxTextLength);
                return result;
            }
            string note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note.Trim();
            if (note != null && note.Length > VoucherConstants.MaxTextLength)
            {
                result.ErrorMessage = VoucherConstants.FieldTooLong("Ghi chú", VoucherConstants.MaxTextLength);
                return result;
            }

            bool isNew = string.IsNullOrEmpty(row.Id);
            try
            {
                DbHelper.InTransaction(_databasePath, (conn, tx) =>
                {
                    if (IsNameTakenIn(conn, tx, name, isNew ? "" : row.Id))
                    {
                        result.ErrorMessage = VoucherConstants.ReasonNameDuplicate(name);
                        return;
                    }

                    DateTime now = DbHelper.Now();
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        row.Name = name;
                        row.Note = note;
                        row.Status = ActiveStatus;
                        row.CreatedById = actorId ?? "";
                        row.CreatedAt = now;
                        int loaiLyDo = row.IsIncome ? -1 : (row.ReasonType ?? -1);
                        row.ReasonType = loaiLyDo;

                        DbHelper.Exec(conn, tx,
                            "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                            "VALUES (@ID, @NAME, @LALDT, @LLD, @NOTE, @ST, @UC, @TC)",
                            "@ID", row.Id,
                            "@NAME", row.Name,
                            "@LALDT", row.IsIncome ? ActiveStatus : 0,
                            "@LLD", loaiLyDo,
                            "@NOTE", note,
                            "@ST", ActiveStatus,
                            "@UC", row.CreatedById,
                            "@TC", now);
                    }
                    else
                    {
                        CashReasonRow existing = LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            result.ErrorMessage = VoucherConstants.ReasonNotFound;
                            return;
                        }
                        if (existing.IsSystem && existing.ReasonType != row.ReasonType)
                        {
                            result.ErrorMessage = VoucherConstants.SystemReasonTypeEditBlocked(existing.Name);
                            return;
                        }

                        row.Name = name;
                        row.Note = note;
                        row.ModifiedById = actorId ?? "";
                        row.ModifiedAt = now;
                        object loaiLyDoVal = existing.IsSystem
                            ? (existing.ReasonType.HasValue ? (object)existing.ReasonType.Value : null)
                            : (row.IsIncome ? (object)(-1) : (row.ReasonType.HasValue ? (object)row.ReasonType.Value : -1));

                        int updated = DbHelper.Exec(conn, tx,
                            "UPDATE DLYDOTHUCHI SET NAME = @NAME, LALYDOTHU = @LALDT, LOAILYDO = @LLD, NOTE = @NOTE, " +
                            "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                            "@NAME", row.Name,
                            "@LALDT", row.IsIncome ? ActiveStatus : 0,
                            "@LLD", loaiLyDoVal,
                            "@NOTE", note,
                            "@UM", row.ModifiedById,
                            "@TM", now,
                            "@ID", row.Id);
                        if (updated == 0)
                        {
                            result.ErrorMessage = VoucherConstants.ReasonNotFound;
                            return;
                        }
                    }

                    result.Ok = true;
                    result.SavedId = row.Id;
                });
            }
            catch
            {
                if (isNew)
                    row.Id = null;
                throw;
            }
            if (!result.Ok && isNew)
                row.Id = null;
            return result;
        }

        public SimpleCatalogDeleteResult Delete(string id)
        {
            var result = new SimpleCatalogDeleteResult();
            if (string.IsNullOrEmpty(id))
            {
                result.ErrorMessage = VoucherConstants.ReasonNotFound;
                return result;
            }

            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                CashReasonRow existing = LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    result.ErrorMessage = VoucherConstants.ReasonNotFound;
                    return;
                }
                if (existing.IsSystem)
                {
                    result.ErrorMessage = VoucherConstants.SystemReasonDeleteBlocked(existing.Name);
                    return;
                }

                int refs = ReferenceChecker.Count(conn, tx, "DLYDOTHUCHIID", id);
                if (refs > 0)
                {
                    result.ReferenceCount = refs;
                    result.ErrorMessage = ReferenceChecker.BlockedMessage("lý do thu chi", existing.Name, refs);
                    return;
                }

                if (DbHelper.Exec(conn, tx, "DELETE FROM DLYDOTHUCHI WHERE ID = @I", "@I", id) == 0)
                {
                    result.ErrorMessage = VoucherConstants.ReasonNotFound;
                    return;
                }
                result.Ok = true;
            });
            return result;
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            string target = CustomerText.FoldNoTrim((name ?? "").Trim());
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DLYDOTHUCHI WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(CustomerText.FoldNoTrim((DbHelper.Str(r["NAME"]) ?? "").Trim()), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static CashReasonRow RowFrom(DataRow r)
        {
            return new CashReasonRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                IsIncome = DbHelper.Int(r["LALYDOTHU"]) == ActiveStatus,
                ReasonType = r["LOAILYDO"] is DBNull ? (int?)null : DbHelper.Int(r["LOAILYDO"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
