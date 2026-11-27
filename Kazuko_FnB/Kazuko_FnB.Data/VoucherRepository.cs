using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu thu và phiếu chi trong bảng TTHUCHI (docs/specs/phieu-thu-chi.md).
    /// Mọi câu SQL tham số hóa; chỉ dùng các cột đã xác minh trên schema thật; không DDL.
    /// </summary>
    public sealed class VoucherRepository
    {
        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;

        private const string SelectColumns =
            "T.ID, T.NAME, T.NGAY, COALESCE(T.LOAI, 0) AS LOAI, COALESCE(T.LOAIDOITUONG, 0) AS LOAIDOITUONG, " +
            "COALESCE(T.LAPHIEUTHUCONGNO, 0) AS LAPHIEUTHUCONGNO, " +
            "COALESCE(T.KHONGTHAYDOICONGNO, 0) AS KHONGTHAYDOICONGNO, " +
            "COALESCE(T.LATAMUNG, 0) AS LATAMUNG, " +
            "T.TENDOITUONG, T.DIACHI, T.DNHANVIENID, T.DKHACHHANGID, T.DNHACUNGCAPID, " +
            "T.DCUAHANGID, T.DLYDOTHUCHIID, T.DIENGIAI, T.CHUNGTUGOC, T.NOTE, " +
            "COALESCE(T.THU, 0) AS THU, COALESCE(T.CHI, 0) AS CHI, " +
            "COALESCE(T.CHUYENKHOAN, 0) AS CHUYENKHOAN, T.DTAIKHOANNGANHANGID, " +
            "T.TDATHANGID, T.TDONHANGID, T.TBANGLUONGID, T.DTHETRATRUOCID, " +
            "COALESCE(T.STATUS, 0) AS STATUS, " +
            "T.USERCREATEDID, T.TIMECREATED, T.USERMODIFIEDID, T.TIMEMODIFIED, " +
            "COALESCE(CH.NAME, '') AS STORENAME, " +
            "COALESCE(LD.NAME, '') AS REASONNAME, " +
            "COALESCE(TK.NAME, '') AS BANKNAME, " +
            "COALESCE(NV.NAME, '') AS EMPNAME, " +
            "COALESCE(KH.NAME, '') AS CUSTNAME, " +
            "COALESCE(NCC.NAME, '') AS SUPPNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, " +
            "COALESCE(UM.NAME, '') AS MODIFIEDBY";

        private const string SelectFrom =
            "FROM TTHUCHI T " +
            "LEFT JOIN DCUAHANG CH ON CH.ID = T.DCUAHANGID " +
            "LEFT JOIN DLYDOTHUCHI LD ON LD.ID = T.DLYDOTHUCHIID " +
            "LEFT JOIN DTAIKHOANNGANHANG TK ON TK.ID = T.DTAIKHOANNGANHANGID " +
            "LEFT JOIN DNHANVIEN NV ON NV.ID = T.DNHANVIENID " +
            "LEFT JOIN DKHACHHANG KH ON KH.ID = T.DKHACHHANGID " +
            "LEFT JOIN DNHACUNGCAP NCC ON NCC.ID = T.DNHACUNGCAPID " +
            "LEFT JOIN SUSER UC ON UC.ID = T.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = T.USERMODIFIEDID ";

        private readonly string _databasePath;

        public VoucherRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public List<VoucherRow> LoadList(VoucherFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");

            return DbHelper.Read(_databasePath, conn =>
            {
                DateTime from = filter.FromDate.Date;
                DateTime toExcl = filter.ToDate.Date.AddDays(1);
                if (toExcl <= from)
                    toExcl = from.AddDays(1);

                var args = new List<object>
                {
                    "@Loai", (int)filter.Kind,
                    "@Del", DeletedStatus,
                    "@From", from,
                    "@ToExcl", toExcl
                };

                string where =
                    "WHERE COALESCE(T.LOAI, 0) = @Loai " +
                    "AND COALESCE(T.STATUS, 0) <> @Del " +
                    "AND T.NGAY >= @From AND T.NGAY < @ToExcl ";

                if (!string.IsNullOrEmpty(filter.StoreId))
                {
                    where += "AND T.DCUAHANGID = @Store ";
                    args.Add("@Store");
                    args.Add(filter.StoreId);
                }

                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectColumns + " " + SelectFrom + where +
                    "ORDER BY T.NGAY DESC, T.TIMECREATED DESC, T.NAME DESC",
                    args.ToArray());

                HashSet<string> allowed = null;
                if (filter.AllowedStoreIds != null)
                    allowed = new HashSet<string>(filter.AllowedStoreIds, StringComparer.Ordinal);

                var list = new List<VoucherRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    VoucherRow row = RowFrom(r);
                    if (allowed != null && !allowed.Contains(row.StoreId ?? ""))
                        continue;
                    list.Add(row);
                }
                return list;
            });
        }

        public VoucherRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static VoucherRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT " + SelectColumns + " " + SelectFrom + "WHERE T.ID = @I",
                "@I", id);
            return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
        }

        public string GenerateNumber(VoucherKind kind)
        {
            return DbHelper.Read(_databasePath, conn => GenerateNumberIn(conn, null, kind));
        }

        public static string GenerateNumberIn(FbConnection conn, FbTransaction tx, VoucherKind kind)
        {
            string formName = kind == VoucherKind.Income
                ? VoucherConstants.IncomeFormName
                : VoucherConstants.ExpenseFormName;
            string fallback = kind == VoucherKind.Income
                ? VoucherConstants.DefaultIncomeTemplate
                : VoucherConstants.DefaultExpenseTemplate;

            object tpl = DbHelper.Scalar(conn, tx,
                "SELECT FIRST 1 NOTEMPLATE FROM SFORM WHERE NAME = @N AND STATUS = @S",
                "@N", formName, "@S", ActiveStatus);
            string template = DbHelper.Str(tpl);
            if (string.IsNullOrWhiteSpace(template))
                template = fallback;

            DateTime today = DateTime.Today;
            string prefix = DocumentNumber.LiteralPrefix(template, today);
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT NAME FROM TTHUCHI WHERE NAME STARTING WITH @P",
                "@P", prefix);
            var names = new string[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
                names[i] = DbHelper.Str(dt.Rows[i]["NAME"]);

            return DocumentNumber.Format(template, today, DocumentNumber.NextSequence(names, prefix));
        }

        public static bool IsNumberTakenIn(FbConnection conn, FbTransaction tx, string number, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(number))
                return false;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID FROM TTHUCHI WHERE NAME = @N AND COALESCE(STATUS, 0) <> @Del",
                "@N", number.Trim(), "@Del", DeletedStatus);
            foreach (DataRow r in dt.Rows)
            {
                if (!string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static void InsertIn(FbConnection conn, FbTransaction tx, VoucherRow row)
        {
            decimal thu = row.Kind == VoucherKind.Income ? row.Amount : 0m;
            decimal chi = row.Kind == VoucherKind.Expense ? row.Amount : 0m;

            DbHelper.Exec(conn, tx,
                "INSERT INTO TTHUCHI (" +
                "ID, NAME, NGAY, LOAI, LOAIDOITUONG, LAPHIEUTHUCONGNO, KHONGTHAYDOICONGNO, LATAMUNG, " +
                "TENDOITUONG, DIACHI, DNHANVIENID, DKHACHHANGID, DNHACUNGCAPID, DCUAHANGID, DLYDOTHUCHIID, " +
                "DIENGIAI, CHUNGTUGOC, NOTE, THU, CHI, CHUYENKHOAN, DTAIKHOANNGANHANGID, TBANGLUONGID, DTHETRATRUOCID, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NGAY, @LOAI, @OBJ, 0, @NDC, @TU, " +
                "@TEN, @DC, @NV, @KH, @NCC, @CH, @LD, " +
                "@DG, @CTG, @NOTE, @THU, @CHI, @CK, @TK, @BL, @TT, " +
                "@ST, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@LOAI", (int)row.Kind,
                "@OBJ", row.ObjectType,
                "@NDC", row.NoDebtChange ? ActiveStatus : 0,
                "@TU", row.IsAdvance ? ActiveStatus : 0,
                "@TEN", NullIfEmpty(row.ObjectName),
                "@DC", NullIfEmpty(row.Address),
                "@NV", NullIfEmpty(row.EmployeeId),
                "@KH", NullIfEmpty(row.CustomerId),
                "@NCC", NullIfEmpty(row.SupplierId),
                "@CH", NullIfEmpty(row.StoreId),
                "@LD", NullIfEmpty(row.ReasonId),
                "@DG", NullIfEmpty(row.Description),
                "@CTG", NullIfEmpty(row.OriginalDoc),
                "@NOTE", NullIfEmpty(row.Note),
                "@THU", thu,
                "@CHI", chi,
                "@CK", row.Transfer ? ActiveStatus : 0,
                "@TK", row.Transfer ? NullIfEmpty(row.BankAccountId) : null,
                "@BL", NullIfEmpty(row.PayrollId),
                "@TT", NullIfEmpty(row.PrepaidCardId),
                "@ST", ActiveStatus,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt ?? DbHelper.Now());
        }

        public static int UpdateIn(FbConnection conn, FbTransaction tx, VoucherRow row)
        {
            decimal thu = row.Kind == VoucherKind.Income ? row.Amount : 0m;
            decimal chi = row.Kind == VoucherKind.Expense ? row.Amount : 0m;

            return DbHelper.Exec(conn, tx,
                "UPDATE TTHUCHI SET " +
                "NAME = @NAME, NGAY = @NGAY, LOAIDOITUONG = @OBJ, KHONGTHAYDOICONGNO = @NDC, " +
                "TENDOITUONG = @TEN, DIACHI = @DC, DNHANVIENID = @NV, DKHACHHANGID = @KH, DNHACUNGCAPID = @NCC, " +
                "DCUAHANGID = @CH, DLYDOTHUCHIID = @LD, DIENGIAI = @DG, CHUNGTUGOC = @CTG, NOTE = @NOTE, " +
                "THU = @THU, CHI = @CHI, CHUYENKHOAN = @CK, DTAIKHOANNGANHANGID = @TK, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM " +
                "WHERE ID = @ID AND COALESCE(STATUS, 0) <> @Del",
                "@NAME", row.Name,
                "@NGAY", row.Date.Date,
                "@OBJ", row.ObjectType,
                "@NDC", row.NoDebtChange ? ActiveStatus : 0,
                "@TEN", NullIfEmpty(row.ObjectName),
                "@DC", NullIfEmpty(row.Address),
                "@NV", NullIfEmpty(row.EmployeeId),
                "@KH", NullIfEmpty(row.CustomerId),
                "@NCC", NullIfEmpty(row.SupplierId),
                "@CH", NullIfEmpty(row.StoreId),
                "@LD", NullIfEmpty(row.ReasonId),
                "@DG", NullIfEmpty(row.Description),
                "@CTG", NullIfEmpty(row.OriginalDoc),
                "@NOTE", NullIfEmpty(row.Note),
                "@THU", thu,
                "@CHI", chi,
                "@CK", row.Transfer ? ActiveStatus : 0,
                "@TK", row.Transfer ? NullIfEmpty(row.BankAccountId) : null,
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt ?? DbHelper.Now(),
                "@ID", row.Id,
                "@Del", DeletedStatus);
        }

        public static int DeleteIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static VoucherRow RowFrom(DataRow r)
        {
            int rawLoai = DbHelper.Int(r["LOAI"]);
            VoucherKind kind = rawLoai == 1 ? VoucherKind.Expense : VoucherKind.Income;
            decimal thu = DbHelper.Dec(r["THU"]);
            decimal chi = DbHelper.Dec(r["CHI"]);

            return new VoucherRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                Kind = kind,
                RawLoai = rawLoai,
                ObjectType = DbHelper.Int(r["LOAIDOITUONG"]),
                ObjectName = DbHelper.Str(r["TENDOITUONG"]) ?? "",
                Address = DbHelper.Str(r["DIACHI"]) ?? "",
                EmployeeId = DbHelper.Str(r["DNHANVIENID"]),
                CustomerId = DbHelper.Str(r["DKHACHHANGID"]),
                SupplierId = DbHelper.Str(r["DNHACUNGCAPID"]),
                StoreId = DbHelper.Str(r["DCUAHANGID"]),
                ReasonId = DbHelper.Str(r["DLYDOTHUCHIID"]),
                Amount = kind == VoucherKind.Income ? thu : chi,
                Description = DbHelper.Str(r["DIENGIAI"]) ?? "",
                OriginalDoc = DbHelper.Str(r["CHUNGTUGOC"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Transfer = DbHelper.Int(r["CHUYENKHOAN"]) != 0,
                BankAccountId = DbHelper.Str(r["DTAIKHOANNGANHANGID"]),
                NoDebtChange = DbHelper.Int(r["KHONGTHAYDOICONGNO"]) != 0,
                BookingId = DbHelper.Str(r["TDATHANGID"]),
                OrderId = DbHelper.Str(r["TDONHANGID"]),
                PayrollId = DbHelper.Str(r["TBANGLUONGID"]),
                PrepaidCardId = DbHelper.Str(r["DTHETRATRUOCID"]),
                IsDebtReceipt = DbHelper.Int(r["LAPHIEUTHUCONGNO"]) != 0 || rawLoai == 2,
                IsAdvance = DbHelper.Int(r["LATAMUNG"]) != 0,
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                StoreName = DbHelper.Str(r["STORENAME"]) ?? "",
                ReasonName = DbHelper.Str(r["REASONNAME"]) ?? "",
                BankAccountName = DbHelper.Str(r["BANKNAME"]) ?? "",
                EmployeeName = DbHelper.Str(r["EMPNAME"]) ?? "",
                CustomerName = DbHelper.Str(r["CUSTNAME"]) ?? "",
                SupplierName = DbHelper.Str(r["SUPPNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
