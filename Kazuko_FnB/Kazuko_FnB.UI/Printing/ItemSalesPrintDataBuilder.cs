using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Dựng DataSet ("Data" / "Table0") và tham số cho mẫu in màn "Thống kê mặt hàng bán"
    /// từ đúng dữ liệu lưới và khối tổng đang hiện trên màn (không truy vấn lại, không tính lại).
    /// Chốt quyền Xem giá nhập: gọi thẳng khi không có quyền sẽ bị từ chối, không dựng dữ liệu giá vốn.
    /// </summary>
    internal static class ItemSalesPrintDataBuilder
    {
        public static PrintJobData Build(
            IList<ItemSalesRow> visibleRows,
            IDictionary<string, string> groupNames,
            ItemSalesSummary summary,
            DateTime fromDate,
            DateTime toDate,
            CompanyPrintInfo companyInfo,
            bool canViewCost)
        {
            if (!canViewCost)
                throw new UnauthorizedAccessException(PrintConstants.NoCostPermissionPrintMessage);

            var ds = new DataSet("Data");
            var table = CreateTable0Schema();
            ds.Tables.Add(table);

            if (visibleRows != null)
            {
                for (int i = 0; i < visibleRows.Count; i++)
                {
                    ItemSalesRow row = visibleRows[i];
                    if (row == null)
                        continue;

                    string groupName = "";
                    if (groupNames != null && !string.IsNullOrEmpty(row.GroupId))
                    {
                        string found;
                        if (groupNames.TryGetValue(row.GroupId, out found) && found != null)
                            groupName = found;
                    }

                    DataRow dr = table.NewRow();
                    dr["TENHANG"] = row.ItemName ?? "";
                    dr["SLXUATCHUAQUYDOI"] = row.Quantity;
                    dr["DDONVITINH_NAME"] = row.UnitName ?? "";
                    dr["DONGIA"] = row.UnitPrice;
                    dr["TIENGIAMGIA"] = row.LineDiscountAmount;
                    dr["THANHTIEN"] = row.SalesAmount;
                    dr["GIAVON"] = row.UnitCost;
                    dr["DMATHANG_GIAVON"] = row.UnitCost;
                    dr["TILEGIAMGIA"] = row.DiscountRate;
                    dr["THANHTIENNHAP"] = row.CostAmount;
                    dr["LAI"] = row.Profit;
                    dr["TILELAI"] = row.ProfitRate;
                    dr["DMATHANGID"] = row.ProductId ?? "";
                    dr["DMATHANG_CODE"] = row.ProductCode ?? "";
                    dr["DMATHANG_MASANCO"] = row.Barcode ?? "";
                    dr["DNHOMMATHANGID"] = row.GroupId ?? "";
                    dr["NHOMHANG"] = groupName;
                    table.Rows.Add(dr);
                }
            }

            CompanyPrintInfo company = companyInfo ?? new CompanyPrintInfo();
            ItemSalesSummary sum = summary ?? new ItemSalesSummary();

            var job = new PrintJobData
            {
                DataSet = ds,
                DefaultFileName = "ThongKeMatHangBan",
                PreviewTitle = PrintConstants.DefaultPreviewTitle
            };

            job.Parameters[PrintConstants.ConfigCompanyName] = company.CompanyName ?? "";
            job.Parameters[PrintConstants.ConfigCompanyAddress] = company.CompanyAddress ?? "";
            job.Parameters[PrintConstants.ConfigCompanyPhone] = company.CompanyPhone ?? "";
            job.Parameters[PrintConstants.ConfigCompanyEmail] = company.CompanyEmail ?? "";

            job.Parameters["TuNgay"] = fromDate.Date;
            job.Parameters["DenNgay"] = toDate.Date;
            job.Parameters["TongTienBan"] = sum.TotalSalesBeforeDiscount;
            job.Parameters["TongTienNhap"] = sum.TotalCost;
            job.Parameters["TongGiamGia"] = sum.TotalDiscount;
            job.Parameters["TongLai"] = sum.GrossSalesProfit;
            job.Parameters["ThucLai"] = sum.NetProfit;

            return job;
        }

        private static DataTable CreateTable0Schema()
        {
            var table = new DataTable("Table0");
            table.Columns.Add("TENHANG", typeof(string));
            table.Columns.Add("SLXUATCHUAQUYDOI", typeof(decimal));
            table.Columns.Add("DDONVITINH_NAME", typeof(string));
            table.Columns.Add("DONGIA", typeof(decimal));
            table.Columns.Add("TIENGIAMGIA", typeof(decimal));
            table.Columns.Add("THANHTIEN", typeof(decimal));
            table.Columns.Add("GIAVON", typeof(decimal));
            table.Columns.Add("DMATHANG_GIAVON", typeof(decimal));
            table.Columns.Add("TILEGIAMGIA", typeof(decimal));
            table.Columns.Add("THANHTIENNHAP", typeof(decimal));
            table.Columns.Add("LAI", typeof(decimal));
            table.Columns.Add("TILELAI", typeof(decimal));
            table.Columns.Add("DMATHANGID", typeof(string));
            table.Columns.Add("DMATHANG_CODE", typeof(string));
            table.Columns.Add("DMATHANG_MASANCO", typeof(string));
            table.Columns.Add("DNHOMMATHANGID", typeof(string));
            table.Columns.Add("NHOMHANG", typeof(string));
            return table;
        }
    }
}
