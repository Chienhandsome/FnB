using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc danh sách và tra cứu cho màn Tạm ứng lương (TTHUCHI LOAI = 1, LATAMUNG = 30, TBANGLUONGID IS NULL).
    /// Ghi/xóa/sinh số phiếu dùng lại trực tiếp các hàm tĩnh của <see cref="VoucherRepository"/> (không nhân bản SQL).
    /// </summary>
    public sealed class SalaryAdvanceRepository
    {
        private const int ActiveStatus = VoucherRepository.ActiveStatus;

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
            "COALESCE(NV.NAME, '') AS EMPNAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, " +
            "COALESCE(UM.NAME, '') AS MODIFIEDBY";

        private const string SelectFrom =
            "FROM TTHUCHI T " +
            "LEFT JOIN DCUAHANG CH ON CH.ID = T.DCUAHANGID " +
            "LEFT JOIN DLYDOTHUCHI LD ON LD.ID = T.DLYDOTHUCHIID " +
            "LEFT JOIN DNHANVIEN NV ON NV.ID = T.DNHANVIENID " +
            "LEFT JOIN SUSER UC ON UC.ID = T.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = T.USERMODIFIEDID ";

        private readonly string _databasePath;

        public SalaryAdvanceRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public List<VoucherRow> LoadList(SalaryAdvanceFilter filter)
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
                    "@ST", ActiveStatus,
                    "@From", from,
                    "@ToExcl", toExcl
                };

                string where =
                    "WHERE COALESCE(T.LOAI, 0) = 1 " +
                    "AND COALESCE(T.LATAMUNG, 0) = 30 " +
                    "AND T.STATUS = @ST " +
                    "AND (T.TBANGLUONGID IS NULL OR T.TBANGLUONGID = '') " +
                    "AND T.NGAY >= @From AND T.NGAY < @ToExcl ";

                if (!string.IsNullOrEmpty(filter.EmployeeId))
                {
                    where += "AND T.DNHANVIENID = @Emp ";
                    args.Add("@Emp");
                    args.Add(filter.EmployeeId);
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

        /// <summary>Lý do chi của phiếu tạm ứng lương (LATAMUNG = 30) gần nhất; null nếu chưa có.</summary>
        public string LoadLatestAdvanceReasonId()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null,
                    "SELECT FIRST 1 T.DLYDOTHUCHIID FROM TTHUCHI T " +
                    "LEFT JOIN DLYDOTHUCHI L ON L.ID = T.DLYDOTHUCHIID " +
                    "WHERE COALESCE(T.LOAI, 0) = 1 AND COALESCE(T.LATAMUNG, 0) = 30 " +
                    "AND T.STATUS = @ST " +
                    "AND T.DLYDOTHUCHIID IS NOT NULL AND T.DLYDOTHUCHIID <> '' " +
                    "AND COALESCE(L.LALYDOTHU, 0) <> 30 " +
                    "ORDER BY T.TIMECREATED DESC",
                    "@ST", ActiveStatus);
                return DbHelper.Str(v);
            });
        }

        public List<LookupItem> LoadActiveEmployees()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DNHANVIEN WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        public int CountActiveStores()
        {
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DCUAHANG WHERE STATUS = @S",
                    "@S", ActiveStatus)));
        }

        public static string LoadActiveEmployeeNameIn(FbConnection conn, FbTransaction tx, string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                return null;
            object v = DbHelper.Scalar(conn, tx,
                "SELECT NAME FROM DNHANVIEN WHERE ID = @I AND STATUS = @S",
                "@I", employeeId, "@S", ActiveStatus);
            return DbHelper.Str(v);
        }

        public static bool IsExpenseReasonIn(FbConnection conn, FbTransaction tx, string reasonId)
        {
            if (string.IsNullOrWhiteSpace(reasonId))
                return true;
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DLYDOTHUCHI WHERE ID = @I AND COALESCE(LALYDOTHU, 0) <> 30",
                "@I", reasonId));
            return n > 0;
        }

        private static VoucherRow RowFrom(DataRow r)
        {
            return new VoucherRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                Kind = VoucherKind.Expense,
                RawLoai = DbHelper.Int(r["LOAI"]),
                ObjectType = DbHelper.Int(r["LOAIDOITUONG"]),
                ObjectName = DbHelper.Str(r["TENDOITUONG"]) ?? "",
                Address = DbHelper.Str(r["DIACHI"]) ?? "",
                EmployeeId = DbHelper.Str(r["DNHANVIENID"]),
                CustomerId = DbHelper.Str(r["DKHACHHANGID"]),
                SupplierId = DbHelper.Str(r["DNHACUNGCAPID"]),
                StoreId = DbHelper.Str(r["DCUAHANGID"]),
                ReasonId = DbHelper.Str(r["DLYDOTHUCHIID"]),
                Amount = DbHelper.Dec(r["CHI"]),
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
                IsDebtReceipt = DbHelper.Int(r["LAPHIEUTHUCONGNO"]) != 0,
                IsAdvance = DbHelper.Int(r["LATAMUNG"]) != 0,
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"]),
                StoreName = DbHelper.Str(r["STORENAME"]) ?? "",
                ReasonName = DbHelper.Str(r["REASONNAME"]) ?? "",
                EmployeeName = DbHelper.Str(r["EMPNAME"]) ?? "",
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? ""
            };
        }
    }
}
