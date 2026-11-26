using System;
using System.Data;
using System.Globalization;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Dựng DataSet ("Data" gồm Table0..Table3) và tham số cho mẫu SREPORTTEMPLATE của báo cáo
    /// "Chi tiết hoạt động ngày" từ dữ liệu đã tải của màn hình (không truy vấn lại, không tự tính tổng
    /// khối tổng hợp để mẫu FastReport tự cộng như app gốc).
    /// </summary>
    internal static class DailyActivityReportDataBuilder
    {
        public static PrintJobData Build(
            DailyActivityReportData data,
            DateTime fromDate,
            DateTime toDate,
            string cashierDisplay,
            CompanyPrintInfo companyInfo)
        {
            DailyActivityReportData src = data ?? new DailyActivityReportData();

            var ds = new DataSet("Data");
            ds.Tables.Add(BuildSaleBillsTable(src));
            ds.Tables.Add(BuildSoldItemsTable(src));
            ds.Tables.Add(BuildCashFlowsTable(src));
            ds.Tables.Add(BuildPurchasesTable(src));

            CompanyPrintInfo company = ResolveCompany(companyInfo, src.Company);
            string cashierParam = string.IsNullOrWhiteSpace(cashierDisplay)
                ? DailyActivityConstants.AllLabel
                : cashierDisplay.Trim();

            var job = new PrintJobData
            {
                DataSet = ds,
                DefaultFileName = "ChiTietHoatDongNgay",
                PreviewTitle = DailyActivityConstants.ReportTitle,
                RaiseStartReportWithData = false
            };

            job.Parameters["GiamGiaTongBill"] = src.BillTotalDiscount;
            job.Parameters["@FromDate"] = fromDate.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            job.Parameters["@ToDate"] = toDate.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            job.Parameters["@USERID"] = cashierParam;
            job.Parameters[PrintConstants.ConfigCompanyName] = company.CompanyName ?? "";
            job.Parameters[PrintConstants.ConfigCompanyAddress] = company.CompanyAddress ?? "";
            job.Parameters[PrintConstants.ConfigCompanyPhone] = company.CompanyPhone ?? "";
            job.Parameters[PrintConstants.ConfigCompanyEmail] = company.CompanyEmail ?? "";

            return job;
        }

        private static CompanyPrintInfo ResolveCompany(CompanyPrintInfo companyInfo, DailyCompanyInfo fallback)
        {
            if (companyInfo != null)
                return companyInfo;
            if (fallback != null)
            {
                return new CompanyPrintInfo
                {
                    CompanyName = fallback.Name ?? "",
                    CompanyAddress = fallback.Address ?? "",
                    CompanyPhone = fallback.Phone ?? "",
                    CompanyEmail = fallback.Email ?? ""
                };
            }
            return new CompanyPrintInfo();
        }

        private static DataTable BuildSaleBillsTable(DailyActivityReportData src)
        {
            var table = new DataTable("Table0");
            table.Columns.Add("NAME", typeof(string));
            table.Columns.Add("TENKHACH", typeof(string));
            table.Columns.Add("TIENHANG", typeof(decimal));
            table.Columns.Add("TIENGIAMGIA", typeof(decimal));
            table.Columns.Add("TIENTHUE", typeof(decimal));
            table.Columns.Add("PHIVANCHUYEN", typeof(decimal));
            table.Columns.Add("TONGCONG", typeof(decimal));
            table.Columns.Add("GIOTHANHTOAN", typeof(DateTime));
            table.Columns.Add("TIENMAT", typeof(decimal));
            table.Columns.Add("CHUYENKHOAN", typeof(decimal));
            table.Columns.Add("TIENTHE", typeof(decimal));
            table.Columns.Add("CONNO", typeof(decimal));
            table.Columns.Add("VOUCHER", typeof(decimal));
            table.Columns.Add("THETRATRUOC", typeof(decimal));
            table.Columns.Add("TRUTICHLUY", typeof(decimal));
            table.Columns.Add("DATTRUOC", typeof(decimal));

            if (src.SaleBills == null)
                return table;

            for (int i = 0; i < src.SaleBills.Count; i++)
            {
                DailySaleBillRow row = src.SaleBills[i];
                if (row == null)
                    continue;
                DataRow dr = table.NewRow();
                dr["NAME"] = row.Name ?? "";
                dr["TENKHACH"] = row.CustomerName ?? "";
                dr["TIENHANG"] = 0m;
                dr["TIENGIAMGIA"] = 0m;
                dr["TIENTHUE"] = 0m;
                dr["PHIVANCHUYEN"] = 0m;
                dr["TONGCONG"] = row.TongCong;
                dr["GIOTHANHTOAN"] = row.PaidTime.HasValue ? (object)row.PaidTime.Value : DBNull.Value;
                dr["TIENMAT"] = row.TienMat;
                dr["CHUYENKHOAN"] = row.ChuyenKhoan;
                dr["TIENTHE"] = row.The;
                dr["CONNO"] = row.CongNo;
                dr["VOUCHER"] = row.Voucher;
                dr["THETRATRUOC"] = row.TheTraTruoc;
                dr["TRUTICHLUY"] = row.TruTichLuy;
                dr["DATTRUOC"] = row.DatTruoc;
                table.Rows.Add(dr);
            }
            return table;
        }

        private static DataTable BuildSoldItemsTable(DailyActivityReportData src)
        {
            var table = new DataTable("Table1");
            table.Columns.Add("TENHANG", typeof(string));
            table.Columns.Add("CODE", typeof(string));
            table.Columns.Add("DVT", typeof(string));
            table.Columns.Add("SOLUONG", typeof(decimal));
            table.Columns.Add("DONGIA", typeof(decimal));
            table.Columns.Add("TILEGIAMGIA", typeof(decimal));
            table.Columns.Add("THANHTIEN", typeof(decimal));

            if (src.SoldItems == null)
                return table;

            for (int i = 0; i < src.SoldItems.Count; i++)
            {
                DailySoldItemRow row = src.SoldItems[i];
                if (row == null)
                    continue;
                DataRow dr = table.NewRow();
                dr["TENHANG"] = row.ItemName ?? "";
                dr["CODE"] = row.ProductCode ?? "";
                dr["DVT"] = row.UnitName ?? "";
                dr["SOLUONG"] = row.Quantity;
                dr["DONGIA"] = row.UnitPrice;
                dr["TILEGIAMGIA"] = row.DiscountRate;
                dr["THANHTIEN"] = row.Amount;
                table.Rows.Add(dr);
            }
            return table;
        }

        private static DataTable BuildCashFlowsTable(DailyActivityReportData src)
        {
            var table = new DataTable("Table2");
            table.Columns.Add("NAME", typeof(string));
            table.Columns.Add("DIENGIAI", typeof(string));
            table.Columns.Add("TENDOITUONG", typeof(string));
            table.Columns.Add("THUTM", typeof(decimal));
            table.Columns.Add("THUCK", typeof(decimal));
            table.Columns.Add("CHITM", typeof(decimal));
            table.Columns.Add("CHICK", typeof(decimal));

            if (src.CashFlows == null)
                return table;

            for (int i = 0; i < src.CashFlows.Count; i++)
            {
                DailyCashFlowRow row = src.CashFlows[i];
                if (row == null)
                    continue;
                DataRow dr = table.NewRow();
                dr["NAME"] = row.Name ?? "";
                dr["DIENGIAI"] = row.Description ?? "";
                dr["TENDOITUONG"] = row.ObjectName ?? "";
                dr["THUTM"] = row.CashIn;
                dr["THUCK"] = row.TransferIn;
                dr["CHITM"] = row.CashOut;
                dr["CHICK"] = row.TransferOut;
                table.Rows.Add(dr);
            }
            return table;
        }

        private static DataTable BuildPurchasesTable(DailyActivityReportData src)
        {
            var table = new DataTable("Table3");
            table.Columns.Add("NAME", typeof(string));
            table.Columns.Add("NHACUNGCAP", typeof(string));
            table.Columns.Add("TONGCONG", typeof(decimal));
            table.Columns.Add("TIENTHANHTOAN", typeof(decimal));
            table.Columns.Add("CONNO", typeof(decimal));

            if (src.Purchases == null)
                return table;

            for (int i = 0; i < src.Purchases.Count; i++)
            {
                DailyPurchaseRow row = src.Purchases[i];
                if (row == null)
                    continue;
                DataRow dr = table.NewRow();
                dr["NAME"] = row.Name ?? "";
                dr["NHACUNGCAP"] = row.SupplierName ?? "";
                dr["TONGCONG"] = row.TongCong;
                dr["TIENTHANHTOAN"] = row.TienThanhToan;
                dr["CONNO"] = DailyActivitySummary.CalculatePurchaseRemainingDebt(row.TongCong, row.TienThanhToan);
                table.Rows.Add(dr);
            }
            return table;
        }
    }
}
