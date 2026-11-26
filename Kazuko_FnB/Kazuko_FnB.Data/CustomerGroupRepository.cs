using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi nhóm khách hàng (DNHOMKHACHHANG — 22 cột, spec docs/specs/danh-muc-khach-hang.md §4.2, §9.0, §9.0.2).
    /// Tên nhóm bắt buộc, không trùng (UNICODE_CI_AI, không cắt khoảng trắng); các tỉ lệ giảm trong khoảng 0..100%.
    /// Xóa cứng: chặn khi còn khách hàng thuộc nhóm hoặc còn nhóm con (§9.0, §9.0.2).
    /// Mọi câu SQL tham số hóa; không DDL.
    /// </summary>
    public sealed class CustomerGroupRepository
    {
        private const int ActiveStatus = 30;

        private const string SelectColumns =
            "ID, NAME, NOTE, STATUS, USERMODIFIEDID, TIMEMODIFIED, TIMECREATED, SORTORDER, " +
            "USERCREATEDID, PARENTID, PARENTDIR, ITEMTYPE, AUTOID, SIMAGEID, " +
            "TILEGIAMGIA, DIEMTICHLUY, TILEGIAMGIATIENHANG, TILEGIAMGIATIENGIO, " +
            "TILEGIAMDOAN, TILEGIAMDOUONG, TILEGIAMDICHVU, TILEGIAMDOKHAC";

        private readonly string _databasePath;

        public CustomerGroupRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Mọi nhóm khách hàng đang dùng (STATUS = 30), xếp theo SORTORDER rồi NAME.</summary>
        public List<CustomerGroupRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectColumns + " FROM DNHOMKHACHHANG WHERE STATUS = @S ORDER BY SORTORDER, NAME",
                    "@S", ActiveStatus);
                var list = new List<CustomerGroupRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một nhóm khách hàng theo ID; null nếu không tồn tại.</summary>
        public CustomerGroupRow Load(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadIn(conn, null, id));
        }

        /// <summary>Tên nhóm đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
        public bool IsNameTaken(string name, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return DbHelper.Read(_databasePath, conn => IsNameTakenIn(conn, null, name, excludeId));
        }

        /// <summary>Số khách hàng thuộc nhóm (để chặn xóa nhóm còn khách).</summary>
        public int CountCustomers(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DKHACHHANG WHERE DNHOMKHACHHANGID = @G",
                    "@G", groupId)));
        }

        /// <summary>Số nhóm con trực tiếp của nhóm (để chặn xóa nhóm còn nhóm con — §9.0.2).</summary>
        public int CountChildren(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DNHOMKHACHHANG WHERE PARENTID = @G",
                    "@G", groupId)));
        }

        /// <summary>Lưu nhóm khách hàng (thêm mới/sửa) trong một transaction.</summary>
        public CustomerGroupSaveResult Save(CustomerGroupRow row, string actorId)
        {
            var result = new CustomerGroupSaveResult();
            if (row == null)
            {
                result.ErrorMessage = "Không có dữ liệu nhóm khách hàng.";
                return result;
            }

            bool isNew = string.IsNullOrEmpty(row.Id);
            try
            {
                DbHelper.InTransaction(_databasePath, (conn, tx) =>
                {
                    string excludeId = isNew ? "" : row.Id;
                    string err = ValidateInTransaction(conn, tx, row, excludeId);
                    if (err != null)
                    {
                        result.ErrorMessage = err;
                        return;
                    }

                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        row.Status = ActiveStatus;
                        row.CreatedById = actorId;
                        row.CreatedAt = DbHelper.Now();
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        row.ItemType = 0;
                        row.AutoId = NextAutoId(conn, tx);
                        string parentId = string.IsNullOrEmpty(row.ParentId) ? "" : row.ParentId;
                        row.ParentId = parentId;
                        if (parentId.Length == 0)
                        {
                            row.ParentDir = "";
                            row.SortOrder = "ZZZZ";
                        }
                        else
                        {
                            CustomerGroupRow parent = LoadIn(conn, tx, parentId);
                            row.ParentDir = parent == null ? "" : (parent.ParentDir ?? "") + parent.Id + ":";
                            row.SortOrder = parent == null ? "ZZZZ" : (parent.SortOrder ?? "ZZZZ");
                        }
                        InsertGroup(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = actorId;
                        row.ModifiedAt = DbHelper.Now();
                        if (UpdateGroup(conn, tx, row) == 0)
                        {
                            result.ErrorMessage = "Nhóm khách hàng không còn tồn tại.";
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
                    ResetNew(row);
                throw;
            }
            if (!result.Ok && isNew)
                ResetNew(row);
            return result;
        }

        /// <summary>
        /// Xóa cứng nhóm khách hàng (Q2, §9.0, §9.0.2): chặn khi còn khách hàng thuộc nhóm hoặc còn nhóm con.
        /// Kiểm và xóa trong cùng transaction.
        /// </summary>
        public CustomerGroupDeleteResult Delete(string groupId)
        {
            var result = new CustomerGroupDeleteResult();
            if (string.IsNullOrEmpty(groupId))
            {
                result.ErrorMessage = "Không có nhóm khách hàng để xóa.";
                return result;
            }

            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                string name = DbHelper.Str(DbHelper.Scalar(conn, tx,
                    "SELECT NAME FROM DNHOMKHACHHANG WHERE ID = @I", "@I", groupId));
                if (name == null)
                {
                    result.ErrorMessage = "Nhóm khách hàng không còn tồn tại.";
                    return;
                }

                result.CustomerCount = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COUNT(*) FROM DKHACHHANG WHERE DNHOMKHACHHANGID = @G", "@G", groupId));
                if (result.CustomerCount > 0)
                {
                    result.ErrorMessage = ReferenceChecker.BlockedMessage("nhóm khách hàng", name, result.CustomerCount);
                    return;
                }

                result.ChildGroupCount = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COUNT(*) FROM DNHOMKHACHHANG WHERE PARENTID = @G", "@G", groupId));
                if (result.ChildGroupCount > 0)
                {
                    result.ErrorMessage = ReferenceChecker.BlockedMessage("nhóm khách hàng", name, result.ChildGroupCount);
                    return;
                }

                if (DbHelper.Exec(conn, tx, "DELETE FROM DNHOMKHACHHANG WHERE ID = @I", "@I", groupId) == 0)
                {
                    result.ErrorMessage = "Nhóm khách hàng không còn tồn tại.";
                    return;
                }
                result.Ok = true;
            });
            return result;
        }

        // ------------------------------------------------------------------ Nội bộ trong transaction

        private static string ValidateInTransaction(FbConnection conn, FbTransaction tx, CustomerGroupRow row, string excludeId)
        {
            string name = row.Name ?? "";
            if (string.IsNullOrWhiteSpace(name))
                return CustomerText.GroupNameEmpty;
            if (name.Length > CustomerText.MaxTextLength)
                return CustomerText.FieldTooLong("Tên nhóm khách hàng", CustomerText.MaxTextLength);
            if (row.Note != null && row.Note.Length > CustomerText.MaxTextLength)
                return CustomerText.FieldTooLong("Ghi chú", CustomerText.MaxTextLength);
            if (row.LoyaltyPoints < 0m)
                return CustomerText.GroupPointsInvalid;

            string rateErr = ValidateDiscountRate(row.DiscountTotal, "Tỉ lệ giảm giá")
                ?? ValidateDiscountRate(row.DiscountGoods, "Tỉ lệ giảm giá tiền hàng")
                ?? ValidateDiscountRate(row.DiscountHour, "Tỉ lệ giảm giá tiền giờ")
                ?? ValidateDiscountRate(row.DiscountFood, "Tỉ lệ giảm đồ ăn")
                ?? ValidateDiscountRate(row.DiscountDrink, "Tỉ lệ giảm đồ uống")
                ?? ValidateDiscountRate(row.DiscountService, "Tỉ lệ giảm dịch vụ")
                ?? ValidateDiscountRate(row.DiscountOther, "Tỉ lệ giảm đồ khác");
            if (rateErr != null)
                return rateErr;

            if (IsNameTakenIn(conn, tx, name, excludeId))
                return CustomerText.GroupNameDuplicate(name);
            return null;
        }

        private static string ValidateDiscountRate(decimal value, string label)
        {
            return value < 0m || value > 100m ? CustomerText.DiscountOutOfRange(label) : null;
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            string target = CustomerText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DNHOMKHACHHANG WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(CustomerText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static int NextAutoId(FbConnection conn, FbTransaction tx)
        {
            object v = DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM DNHOMKHACHHANG");
            return v == null ? 1 : Convert.ToInt32(v) + 1;
        }

        private static CustomerGroupRow LoadIn(FbConnection conn, FbTransaction tx, string id)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT " + SelectColumns + " FROM DNHOMKHACHHANG WHERE ID = @I",
                "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        private static void InsertGroup(FbConnection conn, FbTransaction tx, CustomerGroupRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DNHOMKHACHHANG (ID, NAME, NOTE, STATUS, USERCREATEDID, TIMECREATED, " +
                "USERMODIFIEDID, TIMEMODIFIED, SORTORDER, PARENTID, PARENTDIR, ITEMTYPE, AUTOID, SIMAGEID, " +
                "TILEGIAMGIA, DIEMTICHLUY, TILEGIAMGIATIENHANG, TILEGIAMGIATIENGIO, " +
                "TILEGIAMDOAN, TILEGIAMDOUONG, TILEGIAMDICHVU, TILEGIAMDOKHAC) " +
                "VALUES (@ID, @NAME, @NOTE, @STATUS, @UC, @TC, NULL, NULL, @SORT, @PID, @PDIR, @ITYPE, @AUTO, @IMG, " +
                "@DTOTAL, @DIEM, @DGOODS, @DHOUR, @DFOOD, @DDRINK, @DSERVICE, @DOTHER)",
                GroupParams(row));
        }

        private static int UpdateGroup(FbConnection conn, FbTransaction tx, CustomerGroupRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DNHOMKHACHHANG SET NAME = @NAME, NOTE = @NOTE, SIMAGEID = @IMG, " +
                "TILEGIAMGIA = @DTOTAL, DIEMTICHLUY = @DIEM, TILEGIAMGIATIENHANG = @DGOODS, " +
                "TILEGIAMGIATIENGIO = @DHOUR, TILEGIAMDOAN = @DFOOD, TILEGIAMDOUONG = @DDRINK, " +
                "TILEGIAMDICHVU = @DSERVICE, TILEGIAMDOKHAC = @DOTHER, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                GroupParams(row));
        }

        private static object[] GroupParams(CustomerGroupRow row)
        {
            return new object[]
            {
                "@ID", row.Id,
                "@NAME", row.Name ?? "",
                "@NOTE", string.IsNullOrEmpty(row.Note) ? null : row.Note,
                "@STATUS", row.Status,
                "@UC", row.CreatedById,
                "@TC", row.CreatedAt,
                "@UM", row.ModifiedById,
                "@TM", row.ModifiedAt,
                "@SORT", string.IsNullOrEmpty(row.SortOrder) ? "ZZZZ" : row.SortOrder,
                "@PID", string.IsNullOrEmpty(row.ParentId) ? "" : row.ParentId,
                "@PDIR", string.IsNullOrEmpty(row.ParentDir) ? "" : row.ParentDir,
                "@ITYPE", row.ItemType,
                "@AUTO", row.AutoId,
                "@IMG", string.IsNullOrEmpty(row.ImageId) ? null : row.ImageId,
                "@DTOTAL", row.DiscountTotal,
                "@DIEM", row.LoyaltyPoints,
                "@DGOODS", row.DiscountGoods,
                "@DHOUR", row.DiscountHour,
                "@DFOOD", row.DiscountFood,
                "@DDRINK", row.DiscountDrink,
                "@DSERVICE", row.DiscountService,
                "@DOTHER", row.DiscountOther
            };
        }

        private static void ResetNew(CustomerGroupRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }

        private static CustomerGroupRow RowFrom(DataRow r)
        {
            return new CustomerGroupRow
            {
                Id = DbHelper.Str(r["ID"]),
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                SortOrder = DbHelper.Str(r["SORTORDER"]) ?? "",
                ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                ParentDir = DbHelper.Str(r["PARENTDIR"]) ?? "",
                ItemType = DbHelper.Int(r["ITEMTYPE"]),
                AutoId = DbHelper.Int(r["AUTOID"]),
                ImageId = DbHelper.Str(r["SIMAGEID"]),
                DiscountTotal = DbHelper.Dec(r["TILEGIAMGIA"]),
                LoyaltyPoints = DbHelper.Dec(r["DIEMTICHLUY"]),
                DiscountGoods = DbHelper.Dec(r["TILEGIAMGIATIENHANG"]),
                DiscountHour = DbHelper.Dec(r["TILEGIAMGIATIENGIO"]),
                DiscountFood = DbHelper.Dec(r["TILEGIAMDOAN"]),
                DiscountDrink = DbHelper.Dec(r["TILEGIAMDOUONG"]),
                DiscountService = DbHelper.Dec(r["TILEGIAMDICHVU"]),
                DiscountOther = DbHelper.Dec(r["TILEGIAMDOKHAC"])
            };
        }
    }
}
