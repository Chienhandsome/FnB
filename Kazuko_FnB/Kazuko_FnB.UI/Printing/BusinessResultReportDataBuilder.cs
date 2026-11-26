using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Dựng DataSet ("Data" / "Table0") và tham số cho mẫu SREPORTTEMPLATE của báo cáo
    /// "Tổng hợp kết quả kinh doanh" từ danh sách BusinessResultRow đã tải (không truy vấn lại,
    /// không chép các cột tỉ lệ đã tính sẵn mà chỉ điền sẵn các ô "100%" như mã gốc để script _StartReport tự tính).
    /// </summary>
    internal static class BusinessResultReportDataBuilder
    {
        public static PrintJobData Build(
            IList<BusinessResultRow> rows,
            DateTime fromDate,
            DateTime toDate,
            CompanyPrintInfo companyInfo)
        {
            var ds = new DataSet("Data");
            DataTable table = CreateTable0Schema();
            ds.Tables.Add(table);

            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    BusinessResultRow row = rows[i];
                    if (row == null)
                        continue;

                    DataRow dr = table.NewRow();
                    dr["LOAI"] = row.Loai;
                    dr["NOIDUNG"] = row.Content ?? "";
                    dr["THANGNAY"] = row.CurrentValue;
                    dr["TILEDT"] = "";
                    dr["TILE"] = row.Loai == BusinessResultConstants.LoaiRevenue
                        ? BusinessResultConstants.RatioFull
                        : "";
                    dr["TILE2"] = IsPreFilledTile2(row.Loai)
                        ? BusinessResultConstants.RatioFull
                        : "";
                    dr["BIEUTUONG"] = WingdingsSymbolOf(row.Loai, row.ChangeAmount);
                    dr["TANGGIAM"] = row.ChangeAmount;
                    dr["THANGTRUOC"] = row.PreviousValue;
                    dr["DT"] = "";
                    table.Rows.Add(dr);
                }
            }

            CompanyPrintInfo company = companyInfo ?? new CompanyPrintInfo();
            var job = new PrintJobData
            {
                DataSet = ds,
                DefaultFileName = "TongHopKetQuaKinhDoanh",
                PreviewTitle = BusinessResultConstants.ReportTitle,
                RaiseStartReportWithData = true
            };

            job.Parameters["@FromDate"] = fromDate.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            job.Parameters["@ToDate"] = toDate.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            job.Parameters[PrintConstants.ConfigCompanyName] = company.CompanyName ?? "";
            job.Parameters[PrintConstants.ConfigCompanyAddress] = company.CompanyAddress ?? "";
            job.Parameters[PrintConstants.ConfigCompanyPhone] = company.CompanyPhone ?? "";
            job.Parameters[PrintConstants.ConfigCompanyEmail] = company.CompanyEmail ?? "";

            return job;
        }

        private static bool IsPreFilledTile2(int loai)
        {
            return loai == BusinessResultConstants.LoaiCost
                || loai == BusinessResultConstants.LoaiFixedCost
                || loai == BusinessResultConstants.LoaiVariableCost
                || loai == BusinessResultConstants.LoaiVariableCostDetail
                || loai == BusinessResultConstants.LoaiOtherCost
                || loai == BusinessResultConstants.LoaiOtherCostDetail;
        }

        internal static string WingdingsSymbolOf(int loai, decimal changeAmount)
        {
            if (changeAmount == 0m)
                return "";

            if (loai == BusinessResultConstants.LoaiRevenue || loai == BusinessResultConstants.LoaiRevenueDetail)
                return changeAmount > 0m ? "B" : "D";

            return changeAmount > 0m ? "\u00E6" : "\u00E7";
        }

        private static DataTable CreateTable0Schema()
        {
            var table = new DataTable("Table0");
            table.Columns.Add("LOAI", typeof(int));
            table.Columns.Add("NOIDUNG", typeof(string));
            table.Columns.Add("THANGNAY", typeof(decimal));
            table.Columns.Add("TILEDT", typeof(string));
            table.Columns.Add("TILE", typeof(string));
            table.Columns.Add("TILE2", typeof(string));
            table.Columns.Add("BIEUTUONG", typeof(string));
            table.Columns.Add("TANGGIAM", typeof(decimal));
            table.Columns.Add("THANGTRUOC", typeof(decimal));
            table.Columns.Add("DT", typeof(string));
            return table;
        }
    }
}
