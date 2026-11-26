using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi khách hàng (DKHACHHANG — 21 cột POS TAP, spec docs/specs/danh-muc-khach-hang.md).
    /// Kiểm trùng Mã/Tên theo collation UNICODE_CI_AI (không phân biệt hoa thường và dấu, không cắt khoảng trắng)
    /// trong cùng transaction lưu; xóa cứng khi chưa có phát sinh ở 7 bảng §4.4, chặn khi có phát sinh (§9.0).
    /// Mọi câu SQL tham số hóa; không DDL.
    /// </summary>
    public sealed class CustomerRepository
    {
        private const int ActiveStatus = 30;

        /// <summary>7 bảng tham chiếu DKHACHHANGID theo §4.4 (đã xác minh trên DATA.FDB).</summary>
        public static readonly string[] ReferenceTables =
        {
            "TDONHANG",
            "TDATHANG",
            "TBAOGIA",
            "TTHUCHI",
            "TTANGGIAMDIEM",
            "TLUUTAM",
            "DVOUCHER"
        };

        private const string SelectList =
            "K.ID, K.MAKHACH, K.NAME, K.DNHOMKHACHHANGID, K.DIACHI, K.DIENTHOAI, K.EMAIL, " +
            "K.MASOTHUE, K.DNHANVIENID, K.NGAYSINH, K.DIEMTICHLUYBANDAU, K.GIABAN, " +
            "K.DTINHTHANHID, K.FACEBOOK, K.DTHETRATRUOCID, K.NOTE, K.STATUS, " +
            "K.USERCREATEDID, K.TIMECREATED, K.USERMODIFIEDID, K.TIMEMODIFIED, " +
            "G.NAME AS GROUPNAME, E.NAME AS EMPLOYEENAME, P.NAME AS PROVINCENAME, " +
            "C.NAME AS CARDNAME, UC.NAME AS CREATEDBY, UM.NAME AS MODIFIEDBY";

        private const string SelectFrom =
            "FROM DKHACHHANG K " +
            "LEFT JOIN DNHOMKHACHHANG G ON G.ID = K.DNHOMKHACHHANGID " +
            "LEFT JOIN DNHANVIEN E ON E.ID = K.DNHANVIENID " +
            "LEFT JOIN DTINHTHANH P ON P.ID = K.DTINHTHANHID " +
            "LEFT JOIN DTHETRATRUOC C ON C.ID = K.DTHETRATRUOCID " +
            "LEFT JOIN SUSER UC ON UC.ID = K.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = K.USERMODIFIEDID ";

        private readonly string _databasePath;

        public CustomerRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Mọi khách hàng đang dùng (STATUS = 30), xếp theo tên — dữ liệu cho lưới danh mục.</summary>
        public List<CustomerRow> LoadCustomers()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectList + " " + SelectFrom + "WHERE K.STATUS = @S ORDER BY K.NAME",
                    "@S", ActiveStatus);
                var list = new List<CustomerRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một khách hàng theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public CustomerRow LoadCustomer(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectList + " " + SelectFrom + "WHERE K.ID = @I",
                    "@I", id);
                return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
            });
        }

        /// <summary>
        /// Danh sách chọn khách dùng chung cho hóa đơn/phiếu thu/công nợ (§3, §4.1: SHOWONLOOKUP = 30:
        /// Mã khách, Tên khách hàng, Địa chỉ, Điện thoại).
        /// </summary>
        public List<CustomerLookupItem> LoadLookup()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, MAKHACH, NAME, DIACHI, DIENTHOAI, DNHOMKHACHHANGID, GIABAN, DTHETRATRUOCID " +
                    "FROM DKHACHHANG WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<CustomerLookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new CustomerLookupItem
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Code = DbHelper.Str(r["MAKHACH"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        Address = DbHelper.Str(r["DIACHI"]) ?? "",
                        Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                        GroupId = DbHelper.Str(r["DNHOMKHACHHANGID"]),
                        PriceType = DbHelper.Int(r["GIABAN"]),
                        PrepaidCardId = DbHelper.Str(r["DTHETRATRUOCID"])
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Mã khách tự sinh kế tiếp (§9.0.2 Q3): số lớn nhất trong các mã dạng KH + chữ số, cộng 1, đệm 5 chữ số.
        /// Bảng trống → KH00001; mã khác dạng (vd. 001) bị bỏ qua.
        /// </summary>
        public string NextCode()
        {
            return DbHelper.Read(_databasePath, conn => NextCodeIn(conn, null));
        }

        /// <summary>Mã khách đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
        public bool IsCodeTaken(string code, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            return DbHelper.Read(_databasePath, conn => IsCodeTakenIn(conn, null, code, excludeId));
        }

        /// <summary>Tên khách hàng đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
        public bool IsNameTaken(string name, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return DbHelper.Read(_databasePath, conn => IsNameTakenIn(conn, null, name, excludeId));
        }

        /// <summary>
        /// Lưu khách hàng (thêm mới hoặc sửa) trong MỘT transaction; kiểm bắt buộc, độ dài, điểm &gt;= 0,
        /// loại giá bán 0..4, và trùng Tên/Mã (lỗi Tên trước, gộp chung một thông báo khi trùng cả hai — §9.0.2).
        /// </summary>
        public CustomerSaveResult SaveCustomer(CustomerRow row, string actorId, CustomerSaveOptions options)
        {
            var result = new CustomerSaveResult();
            if (row == null)
            {
                result.ErrorMessage = "Không có dữ liệu khách hàng.";
                return result;
            }
            if (options == null)
                options = new CustomerSaveOptions();

            bool isNew = string.IsNullOrEmpty(row.Id);
            try
            {
                DbHelper.InTransaction(_databasePath, (conn, tx) =>
                {
                    string excludeId = isNew ? "" : row.Id;
                    List<string> errors = ValidateInTransaction(conn, tx, row, excludeId, options, result);
                    if (errors.Count > 0)
                    {
                        result.ErrorMessage = string.Join(Environment.NewLine, errors.ToArray());
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
                        InsertCustomer(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = actorId;
                        row.ModifiedAt = DbHelper.Now();
                        if (UpdateCustomer(conn, tx, row) == 0)
                        {
                            result.ErrorMessage = "Khách hàng không còn tồn tại.";
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
        /// Đếm tham chiếu ở 7 bảng §4.4 (TDONHANG, TDATHANG, TBAOGIA, TTHUCHI, TTANGGIAMDIEM, TLUUTAM, DVOUCHER).
        /// Trả đủ 7 mục (kể cả bảng có 0 dòng).
        /// </summary>
        public List<ReferenceBlock> CheckReferences(string id)
        {
            return DbHelper.Read(_databasePath, conn => CheckReferencesIn(conn, null, id));
        }

        /// <summary>
        /// Xóa cứng khách hàng (Q2, §9.0): chặn nếu còn phát sinh ở bất kỳ bảng nào trong 7 bảng §4.4;
        /// kiểm và xóa trong cùng transaction.
        /// </summary>
        public CustomerDeleteResult DeleteCustomer(string id)
        {
            var result = new CustomerDeleteResult { Blocks = new List<ReferenceBlock>() };
            if (string.IsNullOrEmpty(id))
            {
                result.ErrorMessage = "Không có khách hàng để xóa.";
                return result;
            }

            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                string name = DbHelper.Str(DbHelper.Scalar(conn, tx,
                    "SELECT NAME FROM DKHACHHANG WHERE ID = @I", "@I", id));
                if (name == null)
                {
                    result.ErrorMessage = "Khách hàng không còn tồn tại.";
                    return;
                }

                result.Blocks = CheckReferencesIn(conn, tx, id);
                int totalRefs = 0;
                foreach (ReferenceBlock b in result.Blocks)
                    totalRefs += b.Count;
                if (totalRefs > 0)
                {
                    result.ErrorMessage = ReferenceChecker.BlockedMessage("khách hàng", name, totalRefs);
                    return;
                }

                if (DbHelper.Exec(conn, tx, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", id) == 0)
                {
                    result.ErrorMessage = "Khách hàng không còn tồn tại.";
                    return;
                }
                result.Ok = true;
            });
            return result;
        }

        // ------------------------------------------------------------------ Nội bộ trong transaction

        private static List<string> ValidateInTransaction(
            FbConnection conn,
            FbTransaction tx,
            CustomerRow row,
            string excludeId,
            CustomerSaveOptions options,
            CustomerSaveResult result)
        {
            var errors = new List<string>();
            string name = row.Name ?? "";
            string code = row.Code ?? "";

            bool nameEmpty = string.IsNullOrWhiteSpace(name);
            bool codeEmpty = string.IsNullOrWhiteSpace(code);
            if (nameEmpty)
            {
                result.HasNameError = true;
                errors.Add(CustomerText.NameEmpty);
            }
            if (codeEmpty)
            {
                result.HasCodeError = true;
                errors.Add(CustomerText.CodeEmpty);
            }

            CheckLength(errors, name, "Tên khách hàng", result, true, false);
            CheckLength(errors, code, "Mã khách", result, false, true);
            CheckLength(errors, row.Address, "Địa chỉ", result, false, false);
            CheckLength(errors, row.Phone, "Điện thoại", result, false, false);
            CheckLength(errors, row.Email, "Email", result, false, false);
            CheckLength(errors, row.TaxCode, "Mã số thuế", result, false, false);
            CheckLength(errors, row.Facebook, "Facebook", result, false, false);
            CheckLength(errors, row.Note, "Ghi chú", result, false, false);

            if (row.InitialPoints < 0)
                errors.Add(CustomerText.PointsInvalid);
            if (row.PriceType < 0 || row.PriceType >= CustomerText.PriceTypeNames.Length)
                errors.Add(CustomerText.PriceTypeInvalid);

            if (errors.Count > 0)
                return errors;

            // Kiểm trùng Tên trước, Mã sau — hai lỗi gộp chung một hộp (§9.0.2 Q4).
            bool dupName = !options.AllowDuplicateName && IsNameTakenIn(conn, tx, name, excludeId);
            bool dupCode = IsCodeTakenIn(conn, tx, code, excludeId);
            if (dupName)
            {
                result.HasNameError = true;
                errors.Add(CustomerText.NameDuplicate(name));
            }
            if (dupCode)
            {
                result.HasCodeError = true;
                errors.Add(CustomerText.CodeDuplicate(code));
            }
            return errors;
        }

        private static void CheckLength(List<string> errors, string value, string label,
            CustomerSaveResult result, bool isName, bool isCode)
        {
            if (value != null && value.Length > CustomerText.MaxTextLength)
            {
                if (isName) result.HasNameError = true;
                if (isCode) result.HasCodeError = true;
                errors.Add(CustomerText.FieldTooLong(label, CustomerText.MaxTextLength));
            }
        }

        private static string NextCodeIn(FbConnection conn, FbTransaction tx)
        {
            long max = 0;
            DataTable dt = DbHelper.Table(conn, tx, "SELECT MAKHACH FROM DKHACHHANG WHERE MAKHACH IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                long n;
                if (TryParseKhNumber(DbHelper.Str(r["MAKHACH"]), out n) && n > max)
                    max = n;
            }
            return "KH" + (max + 1).ToString("D5", CultureInfo.InvariantCulture);
        }

        private static bool TryParseKhNumber(string code, out long number)
        {
            number = 0;
            if (string.IsNullOrEmpty(code) || code.Length < 3)
                return false;
            if ((code[0] != 'K' && code[0] != 'k') || (code[1] != 'H' && code[1] != 'h'))
                return false;
            for (int i = 2; i < code.Length; i++)
            {
                if (code[i] < '0' || code[i] > '9')
                    return false;
            }
            return long.TryParse(code.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        private static bool IsCodeTakenIn(FbConnection conn, FbTransaction tx, string code, string excludeId)
        {
            string target = CustomerText.FoldNoTrim(code);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, MAKHACH FROM DKHACHHANG WHERE MAKHACH IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(CustomerText.FoldNoTrim(DbHelper.Str(r["MAKHACH"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            string target = CustomerText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DKHACHHANG WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(CustomerText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static List<ReferenceBlock> CheckReferencesIn(FbConnection conn, FbTransaction tx, string id)
        {
            var blocks = new List<ReferenceBlock>(ReferenceTables.Length);
            foreach (string table in ReferenceTables)
            {
                int count = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COUNT(*) FROM " + table + " WHERE DKHACHHANGID = @I", "@I", id));
                blocks.Add(new ReferenceBlock
                {
                    TableName = table,
                    ColumnName = "DKHACHHANGID",
                    Count = count
                });
            }
            return blocks;
        }

        private static void InsertCustomer(FbConnection conn, FbTransaction tx, CustomerRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DNHOMKHACHHANGID, DIACHI, DIENTHOAI, EMAIL, " +
                "MASOTHUE, DNHANVIENID, NGAYSINH, DIEMTICHLUYBANDAU, GIABAN, DTINHTHANHID, FACEBOOK, " +
                "DTHETRATRUOCID, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @MAKHACH, @NAME, @NHOM, @DIACHI, @DIENTHOAI, @EMAIL, " +
                "@MASOTHUE, @NHANVIEN, @NGAYSINH, @DIEM, @GIABAN, @TINH, @FACEBOOK, " +
                "@THE, @NOTE, @STATUS, @UC, @TC, NULL, NULL)",
                CustomerParams(row));
        }

        private static int UpdateCustomer(FbConnection conn, FbTransaction tx, CustomerRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DKHACHHANG SET MAKHACH = @MAKHACH, NAME = @NAME, DNHOMKHACHHANGID = @NHOM, " +
                "DIACHI = @DIACHI, DIENTHOAI = @DIENTHOAI, EMAIL = @EMAIL, MASOTHUE = @MASOTHUE, " +
                "DNHANVIENID = @NHANVIEN, NGAYSINH = @NGAYSINH, DIEMTICHLUYBANDAU = @DIEM, GIABAN = @GIABAN, " +
                "DTINHTHANHID = @TINH, FACEBOOK = @FACEBOOK, DTHETRATRUOCID = @THE, NOTE = @NOTE, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                CustomerParams(row));
        }

        private static object[] CustomerParams(CustomerRow row)
        {
            return new object[]
            {
                "@ID", row.Id,
                "@MAKHACH", row.Code ?? "",
                "@NAME", row.Name ?? "",
                "@NHOM", NullIfEmpty(row.GroupId),
                "@DIACHI", NullIfEmpty(row.Address),
                "@DIENTHOAI", NullIfEmpty(row.Phone),
                "@EMAIL", NullIfEmpty(row.Email),
                "@MASOTHUE", NullIfEmpty(row.TaxCode),
                "@NHANVIEN", NullIfEmpty(row.EmployeeId),
                "@NGAYSINH", row.Birthday.HasValue ? (object)row.Birthday.Value.Date : null,
                "@DIEM", row.InitialPoints,
                "@GIABAN", row.PriceType,
                "@TINH", NullIfEmpty(row.ProvinceId),
                "@FACEBOOK", NullIfEmpty(row.Facebook),
                "@THE", NullIfEmpty(row.PrepaidCardId),
                "@NOTE", NullIfEmpty(row.Note),
                "@STATUS", row.Status,
                "@UC", row.CreatedById,
                "@TC", row.CreatedAt,
                "@UM", row.ModifiedById,
                "@TM", row.ModifiedAt
            };
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static void ResetNew(CustomerRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }

        private static CustomerRow RowFrom(DataRow r)
        {
            return new CustomerRow
            {
                Id = DbHelper.Str(r["ID"]),
                Code = DbHelper.Str(r["MAKHACH"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                GroupId = DbHelper.Str(r["DNHOMKHACHHANGID"]),
                Address = DbHelper.Str(r["DIACHI"]) ?? "",
                Phone = DbHelper.Str(r["DIENTHOAI"]) ?? "",
                Email = DbHelper.Str(r["EMAIL"]) ?? "",
                TaxCode = DbHelper.Str(r["MASOTHUE"]) ?? "",
                EmployeeId = DbHelper.Str(r["DNHANVIENID"]),
                Birthday = r["NGAYSINH"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["NGAYSINH"]),
                InitialPoints = DbHelper.Int(r["DIEMTICHLUYBANDAU"]),
                PriceType = DbHelper.Int(r["GIABAN"]),
                ProvinceId = DbHelper.Str(r["DTINHTHANHID"]),
                Facebook = DbHelper.Str(r["FACEBOOK"]) ?? "",
                PrepaidCardId = DbHelper.Str(r["DTHETRATRUOCID"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                GroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                EmployeeName = DbHelper.Str(r["EMPLOYEENAME"]) ?? "",
                ProvinceName = DbHelper.Str(r["PROVINCENAME"]) ?? "",
                PrepaidCardName = DbHelper.Str(r["CARDNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
