using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc dữ liệu hộp Chi lương (TBANGLUONGTONGHOP + tổng hợp phiếu chi lương TTHUCHI theo TBANGLUONGID)
    /// và tra cứu lý do chi lương gần nhất.
    /// Mọi thao tác ghi/sửa/xóa/sinh số phiếu dùng lại trực tiếp <see cref="VoucherRepository"/> (không nhân bản SQL).
    /// </summary>
    public sealed class PayrollPayoutRepository
    {
        private const int ActiveStatus = VoucherRepository.ActiveStatus;

        private readonly string _databasePath;

        public PayrollPayoutRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>
        /// Nạp danh sách nhân viên có dòng tổng hợp TBANGLUONGTONGHOP của bảng lương và các phiếu chi lương đã lập
        /// (TTHUCHI: TBANGLUONGID = bảng, DNHANVIENID = nhân viên, LOAI = 1, STATUS = 30).
        /// </summary>
        public PayrollPayoutSheet LoadPayoutSheet(string payrollId)
        {
            if (string.IsNullOrEmpty(payrollId))
                return null;

            return DbHelper.Read(_databasePath, conn =>
            {
                PayrollPeriodRow period = PayrollRepository.LoadPeriodByIdIn(conn, null, payrollId);
                if (period == null)
                    return null;

                var sheet = new PayrollPayoutSheet { Period = period };

                DataTable dtSum = DbHelper.Table(conn, null,
                    "SELECT T.ID, T.DNHANVIENID, COALESCE(NV.CODE, '') AS EMPCODE, COALESCE(NV.NAME, '') AS EMPNAME, " +
                    "COALESCE(T.THUCNHAN, 0) AS THUCNHAN " +
                    "FROM TBANGLUONGTONGHOP T " +
                    "INNER JOIN DNHANVIEN NV ON NV.ID = T.DNHANVIENID " +
                    "WHERE T.TBANGLUONGID = @Id " +
                    "ORDER BY NV.NAME, T.TIMECREATED, T.ID",
                    "@Id", payrollId);

                var byEmp = new Dictionary<string, PayrollPayoutRow>(StringComparer.Ordinal);
                foreach (DataRow r in dtSum.Rows)
                {
                    string empId = DbHelper.Str(r["DNHANVIENID"]) ?? "";
                    if (empId.Length == 0 || byEmp.ContainsKey(empId))
                        continue;

                    decimal net = DbHelper.Dec(r["THUCNHAN"]);
                    var row = new PayrollPayoutRow
                    {
                        SummaryId = DbHelper.Str(r["ID"]) ?? "",
                        EmployeeId = empId,
                        EmployeeCode = DbHelper.Str(r["EMPCODE"]) ?? "",
                        EmployeeName = DbHelper.Str(r["EMPNAME"]) ?? "",
                        NetSalary = net,
                        PaidAmount = 0m,
                        RemainingAmount = net,
                        VoucherNumbersDisplay = ""
                    };
                    byEmp[empId] = row;
                    sheet.Rows.Add(row);
                }

                DataTable dtVouchers = DbHelper.Table(conn, null,
                    "SELECT T.ID, T.NAME, T.NGAY, T.DNHANVIENID, COALESCE(T.CHI, 0) AS CHI, " +
                    "COALESCE(T.CHUYENKHOAN, 0) AS CHUYENKHOAN, T.DTAIKHOANNGANHANGID, T.DLYDOTHUCHIID, " +
                    "T.DCUAHANGID, T.DIENGIAI, T.TIMECREATED, " +
                    "COALESCE(TK.NAME, '') AS BANKNAME, " +
                    "COALESCE(LD.NAME, '') AS REASONNAME, " +
                    "COALESCE(CH.NAME, '') AS STORENAME " +
                    "FROM TTHUCHI T " +
                    "LEFT JOIN DTAIKHOANNGANHANG TK ON TK.ID = T.DTAIKHOANNGANHANGID " +
                    "LEFT JOIN DLYDOTHUCHI LD ON LD.ID = T.DLYDOTHUCHIID " +
                    "LEFT JOIN DCUAHANG CH ON CH.ID = T.DCUAHANGID " +
                    "WHERE T.TBANGLUONGID = @Id " +
                    "AND COALESCE(T.LOAI, 0) = 1 " +
                    "AND COALESCE(T.STATUS, 0) = @St " +
                    "ORDER BY T.NGAY, T.TIMECREATED, T.NAME",
                    "@Id", payrollId, "@St", ActiveStatus);

                foreach (DataRow r in dtVouchers.Rows)
                {
                    string empId = DbHelper.Str(r["DNHANVIENID"]) ?? "";
                    PayrollPayoutRow target;
                    if (empId.Length == 0 || !byEmp.TryGetValue(empId, out target))
                        continue;

                    var info = new PayrollPayoutVoucherInfo
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Number = DbHelper.Str(r["NAME"]) ?? "",
                        Date = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                        Amount = DbHelper.Dec(r["CHI"]),
                        Transfer = DbHelper.Int(r["CHUYENKHOAN"]) != 0,
                        BankAccountId = DbHelper.Str(r["DTAIKHOANNGANHANGID"]),
                        BankAccountName = DbHelper.Str(r["BANKNAME"]) ?? "",
                        ReasonId = DbHelper.Str(r["DLYDOTHUCHIID"]),
                        ReasonName = DbHelper.Str(r["REASONNAME"]) ?? "",
                        StoreId = DbHelper.Str(r["DCUAHANGID"]),
                        StoreName = DbHelper.Str(r["STORENAME"]) ?? "",
                        Description = DbHelper.Str(r["DIENGIAI"]) ?? "",
                        CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"])
                    };
                    target.Vouchers.Add(info);
                    target.PaidAmount += info.Amount;
                }

                decimal sumNet = 0m;
                decimal sumPaid = 0m;
                decimal sumRemaining = 0m;
                int totalVouchers = 0;

                foreach (PayrollPayoutRow row in sheet.Rows)
                {
                    row.RemainingAmount = row.NetSalary - row.PaidAmount;
                    if (row.Vouchers.Count > 0)
                    {
                        var nums = new List<string>(row.Vouchers.Count);
                        foreach (PayrollPayoutVoucherInfo v in row.Vouchers)
                        {
                            string n = (v.Number ?? "").Trim();
                            if (n.Length > 0)
                                nums.Add(n);
                        }
                        row.VoucherNumbersDisplay = string.Join(", ", nums.ToArray());
                    }
                    sumNet += row.NetSalary;
                    sumPaid += row.PaidAmount;
                    sumRemaining += row.RemainingAmount;
                    totalVouchers += row.Vouchers.Count;
                }

                sheet.TotalNetSalary = sumNet;
                sheet.TotalPaidAmount = sumPaid;
                sheet.TotalRemainingAmount = sumRemaining;
                sheet.TotalVoucherCount = totalVouchers;
                return sheet;
            });
        }

        /// <summary>
        /// Lý do chi (DLYDOTHUCHIID) của phiếu chi lương gần nhất (TBANGLUONGID IS NOT NULL, LALYDOTHU &lt;&gt; 30);
        /// null nếu chưa có phiếu chi lương nào.
        /// </summary>
        public string LoadLatestPayoutReasonId()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null,
                    "SELECT FIRST 1 T.DLYDOTHUCHIID FROM TTHUCHI T " +
                    "LEFT JOIN DLYDOTHUCHI L ON L.ID = T.DLYDOTHUCHIID " +
                    "WHERE T.TBANGLUONGID IS NOT NULL AND T.TBANGLUONGID <> '' " +
                    "AND COALESCE(T.LOAI, 0) = 1 " +
                    "AND COALESCE(T.STATUS, 0) = @St " +
                    "AND T.DLYDOTHUCHIID IS NOT NULL AND T.DLYDOTHUCHIID <> '' " +
                    "AND COALESCE(L.LALYDOTHU, 0) <> 30 " +
                    "ORDER BY T.TIMECREATED DESC",
                    "@St", ActiveStatus);
                return DbHelper.Str(v);
            });
        }

        /// <summary>Kiểm tra nhân viên có dòng tổng hợp trong bảng lương không và trả tên nhân viên (kể cả đã khóa STATUS &lt;&gt; 30).</summary>
        public static string LoadEmployeeNameInPayrollIn(FbConnection conn, FbTransaction tx, string payrollId, string employeeId)
        {
            if (string.IsNullOrWhiteSpace(payrollId) || string.IsNullOrWhiteSpace(employeeId))
                return null;
            object v = DbHelper.Scalar(conn, tx,
                "SELECT FIRST 1 COALESCE(NV.NAME, '') " +
                "FROM TBANGLUONGTONGHOP T " +
                "INNER JOIN DNHANVIEN NV ON NV.ID = T.DNHANVIENID " +
                "WHERE T.TBANGLUONGID = @Bl AND T.DNHANVIENID = @Nv",
                "@Bl", payrollId, "@Nv", employeeId);
            return v == null || v is DBNull ? null : Convert.ToString(v);
        }
    }
}
