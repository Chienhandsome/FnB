using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Dựng DataSet ("Data" / "Table0") và tham số cho 4 mẫu in màn "Thống kê doanh thu"
    /// từ đúng danh sách hóa đơn đang hiện trên lưới (không truy vấn lại, không tự tính tổng,
    /// không truyền tham số tổng để các mẫu FastReport tự cộng đúng như app gốc).
    /// </summary>
    internal static class RevenueReportPrintDataBuilder
    {
        public static PrintJobData Build(
            IList<RevenueBillRow> visibleBills,
            DateTime fromDate,
            DateTime toDate,
            CompanyPrintInfo companyInfo)
        {
            var ds = new DataSet("Data");
            DataTable table = CreateTable0Schema();
            ds.Tables.Add(table);

            if (visibleBills != null)
            {
                for (int i = 0; i < visibleBills.Count; i++)
                {
                    RevenueBillRow row = visibleBills[i];
                    if (row == null)
                        continue;

                    DataRow dr = table.NewRow();
                    dr["ID"] = row.Id ?? "";
                    dr["NOTE"] = row.Note ?? "";
                    dr["TILEGIAMGIA"] = 0m;
                    dr["NAME"] = row.Name ?? "";
                    dr["NGAY"] = ToDbDateTime(row.Ngay);
                    dr["DKHACHHANGID"] = DBNull.Value;
                    dr["DKHACHHANG_NAME"] = row.CustomerName ?? "";
                    dr["DKHACHHANG_MAKHACH"] = row.CustomerCode ?? "";
                    dr["DKHACHHANG_DIACHI"] = row.CustomerAddress ?? "";
                    dr["DKHACHHANG_DIENTHOAI"] = DBNull.Value;
                    dr["TONGCONG"] = row.TongCong;
                    dr["TIENGIAMGIA"] = row.TienGiamGia;
                    dr["TIENTHUE"] = row.TienThue;
                    dr["TILETHUE"] = 0m;
                    dr["DIENGIAI"] = DBNull.Value;
                    dr["GIOTHANHTOAN"] = ToDbDateTime(row.PaidTime);
                    dr["KHACHDUA"] = 0m;
                    dr["TRALAI"] = 0m;
                    dr["TDATHANGID"] = DBNull.Value;
                    dr["TDATHANG_NAME"] = DBNull.Value;
                    dr["USERTHANHTOANID"] = DBNull.Value;
                    dr["SUSER_NAME"] = row.PaidBy ?? "";
                    dr["DNHANVIENXUATID"] = DBNull.Value;
                    dr["DNHANVIEN_NAME"] = DBNull.Value;
                    dr["VOUCHER"] = row.Voucher;
                    dr["THETRATRUOC"] = row.TheTraTruoc;
                    dr["DTHETRATRUOCID"] = DBNull.Value;
                    dr["DTHETRATRUOC_NAME"] = DBNull.Value;
                    dr["TRUTICHLUY"] = row.TruTichLuy;
                    dr["TIENMAT"] = row.TienMat;
                    dr["CHUYENKHOAN"] = row.ChuyenKhoan;
                    dr["THE"] = row.The;
                    dr["BATDAU"] = ToDbDateTime(row.StartTime);
                    dr["KETTHUC"] = ToDbDateTime(row.EndTime);
                    dr["TIENGIO"] = row.TienGio;
                    dr["TILEGIAMGIAGIO"] = 0m;
                    dr["TIENGIAMGIAGIO"] = row.TienGiamGiaGio;
                    dr["SOKHACH"] = 0;
                    dr["PHIDICHVU"] = row.PhiDichVu;
                    dr["TILEPHIDICHVU"] = 0m;
                    dr["DATTRUOC"] = row.DatTruoc;
                    dr["DIEUCHINH"] = 0m;
                    dr["CONGNO"] = row.CongNo;
                    dr["TIENHANGCHUAGIAM"] = row.TienHangChuaGiam;
                    dr["GIAMGIAMATHANG"] = row.GiamGiaMatHang;
                    dr["DBANID"] = DBNull.Value;
                    dr["DBAN_NAME"] = row.TableName ?? "";
                    table.Rows.Add(dr);
                }
            }

            CompanyPrintInfo company = companyInfo ?? new CompanyPrintInfo();
            var job = new PrintJobData
            {
                DataSet = ds,
                DefaultFileName = "ThongKeDoanhThu",
                PreviewTitle = PrintConstants.DefaultPreviewTitle
            };

            job.Parameters[PrintConstants.ConfigCompanyName] = company.CompanyName ?? "";
            job.Parameters[PrintConstants.ConfigCompanyAddress] = company.CompanyAddress ?? "";
            job.Parameters[PrintConstants.ConfigCompanyPhone] = company.CompanyPhone ?? "";
            job.Parameters[PrintConstants.ConfigCompanyEmail] = company.CompanyEmail ?? "";
            job.Parameters["TuNgay"] = fromDate.Date;
            job.Parameters["DenNgay"] = toDate.Date;

            return job;
        }

        private static object ToDbDateTime(DateTime? value)
        {
            return value.HasValue ? (object)value.Value : DBNull.Value;
        }

        private static DataTable CreateTable0Schema()
        {
            var table = new DataTable("Table0");
            table.Columns.Add("ID", typeof(string));
            table.Columns.Add("NOTE", typeof(string));
            table.Columns.Add("TILEGIAMGIA", typeof(decimal));
            table.Columns.Add("NAME", typeof(string));
            table.Columns.Add("NGAY", typeof(DateTime));
            table.Columns.Add("DKHACHHANGID", typeof(string));
            table.Columns.Add("DKHACHHANG_NAME", typeof(string));
            table.Columns.Add("DKHACHHANG_MAKHACH", typeof(string));
            table.Columns.Add("DKHACHHANG_DIACHI", typeof(string));
            table.Columns.Add("DKHACHHANG_DIENTHOAI", typeof(string));
            table.Columns.Add("TONGCONG", typeof(decimal));
            table.Columns.Add("TIENGIAMGIA", typeof(decimal));
            table.Columns.Add("TIENTHUE", typeof(decimal));
            table.Columns.Add("TILETHUE", typeof(decimal));
            table.Columns.Add("DIENGIAI", typeof(string));
            table.Columns.Add("GIOTHANHTOAN", typeof(DateTime));
            table.Columns.Add("KHACHDUA", typeof(decimal));
            table.Columns.Add("TRALAI", typeof(decimal));
            table.Columns.Add("TDATHANGID", typeof(string));
            table.Columns.Add("TDATHANG_NAME", typeof(string));
            table.Columns.Add("USERTHANHTOANID", typeof(string));
            table.Columns.Add("SUSER_NAME", typeof(string));
            table.Columns.Add("DNHANVIENXUATID", typeof(string));
            table.Columns.Add("DNHANVIEN_NAME", typeof(string));
            table.Columns.Add("VOUCHER", typeof(decimal));
            table.Columns.Add("THETRATRUOC", typeof(decimal));
            table.Columns.Add("DTHETRATRUOCID", typeof(string));
            table.Columns.Add("DTHETRATRUOC_NAME", typeof(string));
            table.Columns.Add("TRUTICHLUY", typeof(decimal));
            table.Columns.Add("TIENMAT", typeof(decimal));
            table.Columns.Add("CHUYENKHOAN", typeof(decimal));
            table.Columns.Add("THE", typeof(decimal));
            table.Columns.Add("BATDAU", typeof(DateTime));
            table.Columns.Add("KETTHUC", typeof(DateTime));
            table.Columns.Add("TIENGIO", typeof(decimal));
            table.Columns.Add("TILEGIAMGIAGIO", typeof(decimal));
            table.Columns.Add("TIENGIAMGIAGIO", typeof(decimal));
            table.Columns.Add("SOKHACH", typeof(int));
            table.Columns.Add("PHIDICHVU", typeof(decimal));
            table.Columns.Add("TILEPHIDICHVU", typeof(decimal));
            table.Columns.Add("DATTRUOC", typeof(decimal));
            table.Columns.Add("DIEUCHINH", typeof(decimal));
            table.Columns.Add("CONGNO", typeof(decimal));
            table.Columns.Add("TIENHANGCHUAGIAM", typeof(decimal));
            table.Columns.Add("GIAMGIAMATHANG", typeof(decimal));
            table.Columns.Add("DBANID", typeof(string));
            table.Columns.Add("DBAN_NAME", typeof(string));
            return table;
        }
    }
}
