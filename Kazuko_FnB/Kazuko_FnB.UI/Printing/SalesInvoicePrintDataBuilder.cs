using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Dựng DataSet ("Data" gồm Table0, ChiTietGio, ComboChild, InCongGop và Relation1) cùng toàn bộ
    /// tham số đúng tên/đúng kiểu cho 10 mẫu hóa đơn STEMPLATE của màn Hóa đơn bán hàng (SFORMID 5f029a5b...).
    /// Mọi số tiền lấy nguyên từ <see cref="ServiceInvoicePrintDocument"/> đọc từ CSDL (không tính lại).
    /// </summary>
    internal static class SalesInvoicePrintDataBuilder
    {
        public static PrintJobData Build(
            ServiceInvoicePrintDocument doc,
            CompanyPrintInfo companyInfo,
            InvoicePrintBuildOptions options)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");

            InvoicePrintBuildOptions opt = options ?? new InvoicePrintBuildOptions();
            CompanyPrintInfo company = companyInfo ?? new CompanyPrintInfo();

            var ds = new DataSet("Data");
            DataTable table0 = CreateLineTableSchema("Table0", false);
            DataTable chiTietGio = CreateChiTietGioSchema();
            DataTable comboChild = CreateLineTableSchema("ComboChild", true);
            DataTable inCongGop = CreateInCongGopSchema();

            var usedComboIds = new HashSet<string>(StringComparer.Ordinal);
            if (doc.ParentLines != null)
            {
                for (int i = 0; i < doc.ParentLines.Count; i++)
                {
                    ServiceInvoicePrintLine line = doc.ParentLines[i];
                    if (line == null)
                        continue;

                    string effectiveComboId = (line.ComboId ?? "").Trim();
                    if (effectiveComboId.Length == 0 || !usedComboIds.Add(effectiveComboId))
                    {
                        effectiveComboId = !string.IsNullOrEmpty(line.Id) ? line.Id : Guid.NewGuid().ToString();
                        while (!usedComboIds.Add(effectiveComboId))
                            effectiveComboId = Guid.NewGuid().ToString();
                    }
                    AddLineRow(table0, line, effectiveComboId, false);
                }
            }

            if (doc.ComboChildLines != null)
            {
                for (int i = 0; i < doc.ComboChildLines.Count; i++)
                {
                    ServiceInvoicePrintLine child = doc.ComboChildLines[i];
                    if (child == null)
                        continue;
                    AddLineRow(comboChild, child, child.ComboId ?? "", true);
                }
            }

            if (doc.ConsolidatedLines != null)
            {
                for (int i = 0; i < doc.ConsolidatedLines.Count; i++)
                {
                    ServiceInvoiceConsolidatedLine g = doc.ConsolidatedLines[i];
                    if (g == null)
                        continue;
                    DataRow dr = inCongGop.NewRow();
                    dr["TENHANG"] = g.ItemName ?? "";
                    dr["DVT"] = g.UnitName ?? "";
                    dr["SOLUONG"] = g.Quantity;
                    dr["DONGIA"] = g.UnitPrice;
                    dr["THANHTIEN"] = g.Amount;
                    inCongGop.Rows.Add(dr);
                }
            }

            ds.Tables.Add(table0);
            ds.Tables.Add(chiTietGio);
            ds.Tables.Add(comboChild);
            ds.Tables.Add(inCongGop);

            // Tạo quan hệ cha-con giống mẫu hd_80.frx / hd_a4.frx (createConstraints = false để không lỗi khi dòng con mồ côi).
            ds.Relations.Add(new DataRelation(
                "Relation1",
                table0.Columns["COMBOID"],
                comboChild.Columns["COMBOPARENTID"],
                false));

            DateTime ngay = doc.Ngay == DateTime.MinValue ? DateTime.Today : doc.Ngay;
            DateTime batDau = doc.BatDau == DateTime.MinValue ? DateTime.Now : doc.BatDau;
            DateTime ketThuc = doc.KetThuc ?? doc.GioThanhToan ?? DateTime.Now;
            if (ketThuc < batDau)
                ketThuc = batDau;
            TimeSpan duration = ketThuc - batDau;

            decimal traLaiCoc = ComputeRefundDeposit(doc);
            decimal noMoi = opt.IsTempPrint ? 0m : (doc.TongCong + doc.NoCu - doc.TienThanhToan);
            bool hienGiamTong = opt.GopChungChietKhau && doc.TienGiamGiaTong > 0m;
            bool hienGiamMatHang = !opt.GopChungChietKhau && doc.TienGiamGia > 0m;
            bool hienGiamGio = !opt.GopChungChietKhau && doc.TienGiamGiaGio > 0m;
            int hienTienHang = (doc.TienGiamGia == 0m && doc.TienGio == 0m && doc.TienGiamGiaGio == 0m
                && doc.TienGiamGiaTong == 0m && doc.PhiDichVu == 0m && doc.TienThue == 0m) ? 0 : 30;
            int hienThiDiem = (opt.ShowCustomerPoints && !string.IsNullOrEmpty(doc.CustomerId)) ? 30 : 0;

            string billNo = doc.BillName ?? "";
            string safeSuffix = BookingPrintDataBuilder.SanitizeFileNamePart(billNo);
            string filePrefix = opt.IsTempPrint ? "Phieu-tam-tinh-" : "Hoa-don-";

            var job = new PrintJobData
            {
                DataSet = ds,
                DefaultFileName = safeSuffix.Length > 0 ? filePrefix + safeSuffix : (opt.IsTempPrint ? "Phieu-tam-tinh" : "Hoa-don"),
                PreviewTitle = opt.IsTempPrint ? ServicePrintConstants.InvoiceTitleTemp : PrintConstants.DefaultPreviewTitle
            };

            // Đầu phiếu & tên đối tượng
            job.Parameters["NAME"] = billNo;
            job.Parameters["NOTE"] = doc.Note ?? "";
            job.Parameters["NGAY"] = ngay;
            job.Parameters["BATDAU"] = batDau;
            job.Parameters["KETTHUC"] = ketThuc;
            job.Parameters["GIOTHANHTOAN"] = doc.GioThanhToan ?? ketThuc;
            job.Parameters["DBAN_NAME"] = doc.TableName ?? "";
            job.Parameters["DKHUVUC_NAME"] = doc.AreaName ?? "";
            job.Parameters["DKHACHHANG_NAME"] = doc.CustomerName ?? "";
            job.Parameters["DNHANVIEN_NAME"] = doc.EmployeeName ?? "";
            job.Parameters["SOKHACH"] = doc.GuestCount;
            job.Parameters["SOTT"] = doc.SequenceInDay;

            // Số tiền lấy thẳng từ bill đã lưu
            job.Parameters["TIENHANG"] = doc.TienHang;
            job.Parameters["TIENHANGCHUAGIAM"] = doc.TienHangChuaGiam;
            job.Parameters["GIAMGIAMATHANG"] = doc.GiamGiaMatHang;
            job.Parameters["TIENGIO"] = doc.TienGio;
            job.Parameters["TIENGIAMGIA"] = doc.TienGiamGia;
            job.Parameters["TIENGIAMGIAGIO"] = doc.TienGiamGiaGio;
            job.Parameters["TIENGIAMGIATONG"] = doc.TienGiamGiaTong;
            job.Parameters["TILEGIAMGIA"] = doc.TileGiamGia;
            job.Parameters["TILEGIAMGIAGIO"] = doc.TileGiamGiaGio;
            job.Parameters["TILEGIAMGIATONG"] = doc.TileGiamGiaTong;
            job.Parameters["TILEPHIDICHVU"] = doc.TilePhiDichVu;
            job.Parameters["PHIDICHVU"] = doc.PhiDichVu;
            job.Parameters["TILETHUE"] = doc.TileThue;
            job.Parameters["TIENTHUE"] = doc.TienThue;
            job.Parameters["TONGCONG"] = doc.TongCong;
            job.Parameters["VOUCHER"] = doc.Voucher;
            job.Parameters["THE"] = doc.The;
            job.Parameters["CHUYENKHOAN"] = doc.ChuyenKhoan;
            job.Parameters["THETRATRUOC"] = doc.TheTraTruoc;
            job.Parameters["TRUTICHLUY"] = doc.TruTichLuy;
            job.Parameters["TIENMAT"] = doc.TienMat;
            job.Parameters["KHACHDUA"] = doc.KhachDua;
            job.Parameters["TRALAI"] = doc.TraLai;
            job.Parameters["TIENTHANHTOAN"] = doc.TienThanhToan;
            job.Parameters["CONLAI"] = doc.ConLai;
            job.Parameters["CONGNO"] = doc.CongNo;
            job.Parameters["DIEM"] = doc.Diem;
            job.Parameters["DIEMGIAM"] = doc.DiemGiam;
            job.Parameters["DOITRA"] = doc.DoiTra;
            job.Parameters["NOCU"] = doc.NoCu;
            job.Parameters["DATTRUOC"] = doc.DatTruoc;

            // Tham số đặc biệt theo goc-PrintInvoice-nhahang.cs.txt & Dictionary hd_80/hd_a4
            job.Parameters["Trả lại tiền cọc dư"] = traLaiCoc;
            job.Parameters["Nợ mới"] = noMoi;
            job.Parameters["Đặt trước"] = doc.DatTruoc;
            job.Parameters["Tạm tính"] = opt.IsTempPrint ? 30 : 0;
            job.Parameters["SOLANINTAMTINH"] = doc.TempPrintCount;
            job.Parameters["GIOTRADO"] = ketThuc;
            job.Parameters["Điểm tích lũy"] = doc.CustomerTotalPoints;
            job.Parameters["Hiện giảm tổng"] = hienGiamTong;
            job.Parameters["Hiện giảm mặt hàng"] = hienGiamMatHang;
            job.Parameters["Hiện giảm giờ"] = hienGiamGio;
            job.Parameters["Hiện tiền hàng"] = hienTienHang;
            job.Parameters["HienThiDiemCuaKhachHangTrenHoaDon"] = hienThiDiem;
            job.Parameters["Tiêu đề"] = opt.IsTempPrint
                ? ServicePrintConstants.InvoiceTitleTemp
                : ServicePrintConstants.InvoiceTitleOfficial;
            job.Parameters["Tổng số ngày"] = duration.Days;
            job.Parameters["Tổng số giờ"] = duration.Hours;
            job.Parameters["Tổng số phút"] = duration.Minutes;
            job.Parameters["LoiCamOn"] = opt.LoiCamOn ?? "";
            job.Parameters["PASSWIFI"] = doc.PassWifi ?? "";

            // Thông tin người in & công ty
            job.Parameters[PrintConstants.PrintedByParameterName] = opt.PrintedBy ?? "";
            job.Parameters[PrintConstants.ConfigCompanyName] = company.CompanyName ?? "";
            job.Parameters[PrintConstants.ConfigCompanyAddress] = company.CompanyAddress ?? "";
            job.Parameters[PrintConstants.ConfigCompanyPhone] = company.CompanyPhone ?? "";
            job.Parameters[PrintConstants.ConfigCompanyEmail] = company.CompanyEmail ?? "";
            job.Parameters[PrintConstants.ConfigCompanyFax] = company.CompanyFax ?? "";

            return job;
        }

        internal static decimal ComputeRefundDeposit(ServiceInvoicePrintDocument doc)
        {
            if (doc == null || doc.DatTruoc <= 0m)
                return 0m;
            decimal tongKhachDua = doc.Voucher + doc.DatTruoc + doc.ChuyenKhoan + doc.The + doc.TheTraTruoc + doc.TruTichLuy;
            if (doc.Voucher > doc.TongCong)
                return doc.DatTruoc;
            return Math.Max(0m, tongKhachDua - doc.TongCong);
        }

        internal static string FormatQuantityStr(decimal slXuat, string unitName, string productTypeId)
        {
            if (string.Equals(productTypeId, "7", StringComparison.Ordinal))
            {
                int soGio = (int)slXuat;
                int soPhut = (int)((slXuat - soGio) * 60m);
                return (soGio > 0 ? (soGio.ToString(CultureInfo.InvariantCulture) + "h ") : "") + soPhut.ToString(CultureInfo.InvariantCulture) + "'";
            }
            string num = slXuat == 0m
                ? "0"
                : slXuat.ToString("###,##0.##", CultureInfo.InvariantCulture);
            string dvt = (unitName ?? "").Trim();
            return dvt.Length > 0 ? (num + " " + dvt) : num;
        }

        private static void AddLineRow(DataTable table, ServiceInvoicePrintLine line, string comboId, bool includeChildExtras)
        {
            DataRow dr = table.NewRow();
            dr["ID"] = line.Id ?? "";
            dr["NOTE"] = line.Note ?? "";
            dr["DMATHANGID"] = line.ProductId ?? "";
            dr["DMATHANG_NAME"] = line.ProductName ?? "";
            dr["DDONVITINH_NAME"] = line.UnitName ?? "";
            dr["DMATHANG_CODE"] = line.ProductCode ?? "";
            dr["BAOHANH"] = line.Warranty ?? "";
            dr["TILEGIAMGIA"] = line.DiscountRate;
            dr["TIENGIAMGIA"] = line.DiscountAmount;
            dr["THANHTIEN"] = line.Amount;
            dr["DONGIA"] = line.UnitPrice;
            dr["SLNHAP"] = line.SlNhap;
            dr["SLXUAT"] = line.SlXuat;
            dr["SLTHUCTE"] = 0m;
            dr["SLHETHONG"] = 0m;
            dr["DDONVITINHID"] = line.UnitId ?? "";
            dr["SLNHAPCHUAQUYDOI"] = line.SlNhapChuaQuyDoi;
            dr["SLXUATCHUAQUYDOI"] = line.SlXuatChuaQuyDoi;
            dr["COMBOID"] = comboId ?? "";
            dr["COMBOPARENTID"] = line.ComboParentId ?? "";
            dr["TENHANG"] = line.ItemName ?? "";
            dr["DMATHANG_TENTIENGANH"] = line.EnglishName ?? "";
            dr["DMATHANG_DLOAIMATHANGID"] = line.ProductTypeId ?? "0";
            dr["TUGIO"] = line.TuGio.HasValue ? (object)line.TuGio.Value : DBNull.Value;
            dr["DENGIO"] = line.DenGio.HasValue ? (object)line.DenGio.Value : DBNull.Value;
            dr["SOLUONGSTR"] = FormatQuantityStr(line.SlXuat, line.UnitName, line.ProductTypeId);

            if (includeChildExtras)
            {
                dr["DVT"] = line.UnitName ?? "";
                dr["SOLUONG"] = line.SlXuatChuaQuyDoi;
            }
            table.Rows.Add(dr);
        }

        private static DataTable CreateLineTableSchema(string tableName, bool includeChildExtras)
        {
            var t = new DataTable(tableName);
            t.Columns.Add("ID", typeof(string));
            t.Columns.Add("NOTE", typeof(string));
            t.Columns.Add("DMATHANGID", typeof(string));
            t.Columns.Add("DMATHANG_NAME", typeof(string));
            t.Columns.Add("DDONVITINH_NAME", typeof(string));
            t.Columns.Add("DMATHANG_CODE", typeof(string));
            t.Columns.Add("BAOHANH", typeof(string));
            t.Columns.Add("TILEGIAMGIA", typeof(decimal));
            t.Columns.Add("TIENGIAMGIA", typeof(decimal));
            t.Columns.Add("THANHTIEN", typeof(decimal));
            t.Columns.Add("DONGIA", typeof(decimal));
            t.Columns.Add("SLNHAP", typeof(decimal));
            t.Columns.Add("SLXUAT", typeof(decimal));
            t.Columns.Add("SLTHUCTE", typeof(decimal));
            t.Columns.Add("SLHETHONG", typeof(decimal));
            t.Columns.Add("DDONVITINHID", typeof(string));
            t.Columns.Add("SLNHAPCHUAQUYDOI", typeof(decimal));
            t.Columns.Add("SLXUATCHUAQUYDOI", typeof(decimal));
            t.Columns.Add("COMBOID", typeof(string));
            t.Columns.Add("COMBOPARENTID", typeof(string));
            t.Columns.Add("TENHANG", typeof(string));
            t.Columns.Add("DMATHANG_TENTIENGANH", typeof(string));
            t.Columns.Add("DMATHANG_DLOAIMATHANGID", typeof(string));
            t.Columns.Add("TUGIO", typeof(DateTime));
            t.Columns.Add("DENGIO", typeof(DateTime));
            t.Columns.Add("SOLUONGSTR", typeof(string));
            if (includeChildExtras)
            {
                t.Columns.Add("DVT", typeof(string));
                t.Columns.Add("SOLUONG", typeof(decimal));
            }
            return t;
        }

        private static DataTable CreateChiTietGioSchema()
        {
            var t = new DataTable("ChiTietGio");
            t.Columns.Add("DONGIA", typeof(decimal));
            t.Columns.Add("BANGGIA", typeof(string));
            t.Columns.Add("TUGIO", typeof(DateTime));
            t.Columns.Add("DENGIO", typeof(DateTime));
            t.Columns.Add("BAN", typeof(string));
            t.Columns.Add("THANHTIEN", typeof(decimal));
            t.Columns.Add("THOIGIAN", typeof(string));
            t.Columns.Add("SOGIO", typeof(int));
            t.Columns.Add("SOPHUT", typeof(int));
            t.Columns.Add("CACHTINHGIA", typeof(int));
            t.Columns.Add("DIENGIAI", typeof(string));
            t.Columns.Add("DBANGGIAID", typeof(string));
            t.Columns.Add("SONGAY", typeof(int));
            return t;
        }

        private static DataTable CreateInCongGopSchema()
        {
            var t = new DataTable("InCongGop");
            t.Columns.Add("TENHANG", typeof(string));
            t.Columns.Add("DVT", typeof(string));
            t.Columns.Add("SOLUONG", typeof(decimal));
            t.Columns.Add("DONGIA", typeof(decimal));
            t.Columns.Add("THANHTIEN", typeof(decimal));
            return t;
        }
    }
}
