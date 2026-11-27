using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục tài khoản ngân hàng (DTAIKHOANNGANHANG, docs/specs/phieu-thu-chi.md §3).
    /// Kiểm trùng Tên (UNICODE_CI_AI); xóa cứng chỉ khi không còn dòng nào ở mọi bảng có cột DTAIKHOANNGANHANGID
    /// (không lọc STATUS, dùng ReferenceChecker.Count).
    /// </summary>
    public sealed class BankAccountRepository
    {
        private const int ActiveStatus = 30;

        private const string SelectSql =
            "SELECT B.ID, B.NAME, B.NOTE, COALESCE(B.STATUS, 0) AS STATUS, " +
            "B.USERCREATEDID, B.TIMECREATED, B.USERMODIFIEDID, B.TIMEMODIFIED, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY " +
            "FROM DTAIKHOANNGANHANG B " +
            "LEFT JOIN SUSER UC ON UC.ID = B.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = B.USERMODIFIEDID ";

        private readonly string _databasePath;

        public BankAccountRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public List<BankAccountRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectSql + "WHERE B.STATUS = @S ORDER BY B.NAME",
                    "@S", ActiveStatus);
                var list = new List<BankAccountRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        public BankAccountRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        private static BankAccountRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            DataTable dt = DbHelper.Table(conn, tx, SelectSql + "WHERE B.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        public SimpleCatalogSaveResult Save(BankAccountRow row, string actorId)
        {
            var result = new SimpleCatalogSaveResult();
            if (row == null)
            {
                result.ErrorMessage = VoucherConstants.BankNotFound;
                return result;
            }

            string name = (row.Name ?? "").Trim();
            if (name.Length == 0)
            {
                result.ErrorMessage = VoucherConstants.BankNameEmpty;
                return result;
            }
            if (name.Length > VoucherConstants.MaxTextLength)
            {
                result.ErrorMessage = VoucherConstants.FieldTooLong("Tên tài khoản", VoucherConstants.MaxTextLength);
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
                        result.ErrorMessage = VoucherConstants.BankNameDuplicate(name);
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

                        DbHelper.Exec(conn, tx,
                            "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                            "VALUES (@ID, @NAME, @NOTE, @ST, @UC, @TC)",
                            "@ID", row.Id,
                            "@NAME", row.Name,
                            "@NOTE", note,
                            "@ST", ActiveStatus,
                            "@UC", row.CreatedById,
                            "@TC", now);
                    }
                    else
                    {
                        row.Name = name;
                        row.Note = note;
                        row.ModifiedById = actorId ?? "";
                        row.ModifiedAt = now;

                        int updated = DbHelper.Exec(conn, tx,
                            "UPDATE DTAIKHOANNGANHANG SET NAME = @NAME, NOTE = @NOTE, " +
                            "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                            "@NAME", row.Name,
                            "@NOTE", note,
                            "@UM", row.ModifiedById,
                            "@TM", now,
                            "@ID", row.Id);
                        if (updated == 0)
                        {
                            result.ErrorMessage = VoucherConstants.BankNotFound;
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
                result.ErrorMessage = VoucherConstants.BankNotFound;
                return result;
            }

            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                BankAccountRow existing = LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    result.ErrorMessage = VoucherConstants.BankNotFound;
                    return;
                }

                // Spec §3: kiểm TẤT CẢ bảng có cột DTAIKHOANNGANHANGID, không lọc STATUS.
                int refs = ReferenceChecker.Count(conn, tx, "DTAIKHOANNGANHANGID", id);
                if (refs > 0)
                {
                    result.ReferenceCount = refs;
                    result.ErrorMessage = ReferenceChecker.BlockedMessage("tài khoản ngân hàng", existing.Name, refs);
                    return;
                }

                if (DbHelper.Exec(conn, tx, "DELETE FROM DTAIKHOANNGANHANG WHERE ID = @I", "@I", id) == 0)
                {
                    result.ErrorMessage = VoucherConstants.BankNotFound;
                    return;
                }
                result.Ok = true;
            });
            return result;
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            string target = CustomerText.FoldNoTrim((name ?? "").Trim());
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DTAIKHOANNGANHANG WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(CustomerText.FoldNoTrim((DbHelper.Str(r["NAME"]) ?? "").Trim()), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static BankAccountRow RowFrom(DataRow r)
        {
            return new BankAccountRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
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
