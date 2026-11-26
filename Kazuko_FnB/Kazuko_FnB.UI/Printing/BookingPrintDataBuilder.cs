using System;
using System.Data;
using System.IO;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Dựng DataSet ("Data" / "Table0") và các tham số đúng kiểu cho 5 mẫu in Khách đặt hàng
    /// từ <see cref="BookingPrintDocument"/> đã đọc lại từ CSDL (không tính lại số tiền từ màn hình).
    /// </summary>
    internal static class BookingPrintDataBuilder
    {
        public static PrintJobData Build(
            BookingPrintDocument doc,
            CompanyPrintInfo companyInfo,
            string printedBy)
        {
            if (doc == null || doc.Header == null)
                throw new ArgumentNullException("doc");

            BookingRow h = doc.Header;
            DateTime ngay = h.Ngay == DateTime.MinValue ? DateTime.Today : h.Ngay;
            DateTime tuNgay = h.TuNgay == DateTime.MinValue ? ngay.Date : h.TuNgay;
            DateTime denNgay = h.DenNgay == DateTime.MinValue ? ngay.Date : h.DenNgay;
            string roomId = doc.RoomId ?? "";
            string roomNames = doc.RoomNames ?? "";

            var ds = new DataSet("Data");
            DataTable table = CreateTable0Schema();
            ds.Tables.Add(table);

            if (doc.Lines != null)
            {
                for (int i = 0; i < doc.Lines.Count; i++)
                {
                    BookingPrintLine line = doc.Lines[i];
                    if (line == null)
                        continue;

                    DataRow dr = table.NewRow();
                    dr["ID"] = line.Id ?? "";
                    dr["NOTE"] = line.Note ?? "";
                    dr["DMATHANGID"] = line.ProductId ?? "";
                    dr["DMATHANG_NAME"] = line.ProductName ?? "";
                    dr["DDONVITINH_NAME"] = line.UnitName ?? "";
                    dr["DMATHANG_CODE"] = line.ProductCode ?? "";
                    dr["SOLUONG"] = line.Quantity;
                    dr["DONGIA"] = line.Price;
                    dr["THANHTIEN"] = line.Amount;
                    dr["TILEGIAMGIA"] = line.DiscountPct;
                    dr["BAOHANH"] = line.Warranty ?? "";
                    dr["DDONVITINHID"] = line.UnitId ?? "";
                    dr["TDATHANGID"] = string.IsNullOrEmpty(line.BookingId) ? (h.Id ?? "") : line.BookingId;
                    dr["TDATHANG_TUNGAY"] = tuNgay;
                    dr["TDATHANG_DENNGAY"] = denNgay;
                    dr["TDATHANG_DBANID"] = roomId;
                    dr["DBAN_NAME"] = roomNames;
                    table.Rows.Add(dr);
                }
            }

            CompanyPrintInfo company = companyInfo ?? new CompanyPrintInfo();
            string docNumber = h.Name ?? "";
            string safeSuffix = SanitizeFileNamePart(docNumber);

            var job = new PrintJobData
            {
                DataSet = ds,
                DefaultFileName = safeSuffix.Length > 0 ? "Phieu-dat-hang-" + safeSuffix : "Phieu-dat-hang",
                PreviewTitle = PrintConstants.DefaultPreviewTitle
            };

            job.Parameters["NAME"] = docNumber;
            job.Parameters["TENKHACH"] = h.TenKhach ?? "";
            job.Parameters["DIACHI"] = h.DiaChi ?? "";
            job.Parameters["DIENTHOAI"] = h.DienThoai ?? "";
            job.Parameters["NOTE"] = h.Note ?? "";
            job.Parameters["NGAY"] = ngay;
            job.Parameters["TUGIO"] = h.TuGio.HasValue ? (object)h.TuGio.Value : null;
            job.Parameters["TIENHANG"] = h.TienHang;
            job.Parameters["TILEGIAMGIA"] = h.TileGiamGia;
            job.Parameters["TIENGIAMGIA"] = h.TienGiamGia;
            job.Parameters["TILETHUE"] = h.TileThue;
            job.Parameters["TIENTHUE"] = h.TienThue;
            job.Parameters["PHIVANCHUYEN"] = h.PhiVanChuyen;
            job.Parameters["TONGCONG"] = h.TongCong;
            job.Parameters["DatTruoc"] = doc.DepositAmount;
            job.Parameters["DBAN_NAME"] = roomNames;
            job.Parameters[PrintConstants.PrintedByParameterName] = printedBy ?? "";
            job.Parameters[PrintConstants.ConfigCompanyName] = company.CompanyName ?? "";
            job.Parameters[PrintConstants.ConfigCompanyAddress] = company.CompanyAddress ?? "";
            job.Parameters[PrintConstants.ConfigCompanyPhone] = company.CompanyPhone ?? "";
            job.Parameters[PrintConstants.ConfigCompanyEmail] = company.CompanyEmail ?? "";
            job.Parameters[PrintConstants.ConfigCompanyFax] = company.CompanyFax ?? "";

            return job;
        }

        private static DataTable CreateTable0Schema()
        {
            var table = new DataTable("Table0");
            table.Columns.Add("ID", typeof(string));
            table.Columns.Add("NOTE", typeof(string));
            table.Columns.Add("DMATHANGID", typeof(string));
            table.Columns.Add("DMATHANG_NAME", typeof(string));
            table.Columns.Add("DDONVITINH_NAME", typeof(string));
            table.Columns.Add("DMATHANG_CODE", typeof(string));
            table.Columns.Add("SOLUONG", typeof(decimal));
            table.Columns.Add("DONGIA", typeof(decimal));
            table.Columns.Add("THANHTIEN", typeof(decimal));
            table.Columns.Add("TILEGIAMGIA", typeof(decimal));
            table.Columns.Add("BAOHANH", typeof(string));
            table.Columns.Add("DDONVITINHID", typeof(string));
            table.Columns.Add("TDATHANGID", typeof(string));
            table.Columns.Add("TDATHANG_TUNGAY", typeof(DateTime));
            table.Columns.Add("TDATHANG_DENNGAY", typeof(DateTime));
            table.Columns.Add("TDATHANG_DBANID", typeof(string));
            table.Columns.Add("DBAN_NAME", typeof(string));
            return table;
        }

        internal static string SanitizeFileNamePart(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";
            string s = raw.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                s = s.Replace(invalid[i], '-');
            return s;
        }
    }
}
