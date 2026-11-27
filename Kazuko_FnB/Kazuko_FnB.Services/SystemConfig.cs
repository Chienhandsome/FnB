using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Cấu hình toàn hệ thống (bảng SCONFIG) đọc có kiểu, cache trong phiên.
    /// Khóa = SCONFIG.NAME. Ô tích: 30 = bật, 0 = tắt. Khóa không có trong DB trả về giá trị mặc định (tắt/0/rỗng).
    /// Gọi <see cref="Reload"/> sau khi lưu cấu hình để mọi nơi thấy giá trị mới ngay.
    /// </summary>
    public static class SystemConfig
    {
        public const int On = 30;

        private static readonly object Lock = new object();
        private static Dictionary<string, ConfigValue> _values;
        private static string _loadedFrom;

        /// <summary>Nạp lại từ DB của phiên hiện tại (hoặc đường dẫn chỉ định).</summary>
        public static void Reload(string databasePath = null)
        {
            string path = databasePath ?? AppSession.DatabasePath;
            lock (Lock)
            {
                if (string.IsNullOrEmpty(path))
                {
                    _values = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);
                    _loadedFrom = null;
                    return;
                }
                try
                {
                    _values = new ConfigRepository(path).LoadValues();
                    _loadedFrom = path;
                }
                catch (DataAccessException)
                {
                    _values = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);
                    _loadedFrom = null;
                }
            }
        }

        private static ConfigValue Find(string name)
        {
            lock (Lock)
            {
                if (_values == null || !string.Equals(_loadedFrom, AppSession.DatabasePath, StringComparison.OrdinalIgnoreCase))
                    Reload();
                ConfigValue v;
                return _values.TryGetValue(name, out v) ? v : null;
            }
        }

        public static int GetInt(string name, int defaultValue = 0)
        {
            ConfigValue v = Find(name);
            return v == null ? defaultValue : v.IntValue;
        }

        public static decimal GetDecimal(string name, decimal defaultValue = 0m)
        {
            ConfigValue v = Find(name);
            if (v == null)
                return defaultValue;
            return v.DecimalValue != 0m ? v.DecimalValue : v.IntValue;
        }

        public static string GetText(string name, string defaultValue = "")
        {
            ConfigValue v = Find(name);
            return v == null || v.TextValue == null ? defaultValue : v.TextValue;
        }

        /// <summary>Ô tích bật khi giá trị = 30.</summary>
        public static bool GetBool(string name)
        {
            return GetInt(name) == On;
        }

        // ---- Khóa hay dùng (thêm dần theo từng chức năng) ----

        public static bool CoTienGio { get { return GetBool("CoTienGio"); } }
        public static bool ChoPhepTrungTenKhachHang { get { return GetBool("ChoPhepTrungTenKhachHang"); } }
        public static bool BatBuocNhapKhachHang { get { return GetBool("BatBuocNhapKhachHang"); } }
        public static bool ChoPhepKhachNo { get { return GetBool("ChoPhepKhachNo"); } }
        public static bool SuDungGiaTheoKhach { get { return GetBool("SuDungGiaTheoKhach"); } }
        public static bool GopChungGiamGiaTienGioVaTienHang { get { return GetBool("GopChungGiamGiaTienGioVaTienHangLamMot"); } }
        public static bool TuDongNangCapThanhVienKhiDatHanMuc { get { return GetBool("TuDongNangCapThanhVienKhiDatHanMuc"); } }
        public static int CachTinhDiem { get { return GetInt("CachTinhDiem"); } }
        public static int DoanhSoTuongUngVoi1Diem { get { return GetInt("DoanhSoTuongUngVoi1Diem"); } }
        public static bool KichHoatLuuVetHoatDong { get { return GetBool("KichHoatLuuVetHoatDong"); } }
        public static bool TuDongSaoLuuDuLieu { get { return GetBool("TuDongSaoLuuDuLieu"); } }

        // ---- Khóa của chức năng Danh mục mặt hàng (spec 3.7) ----

        public static bool SuDungMaHang { get { return GetBool("SuDungMaHang"); } }
        public static bool SuDungMaSanCo { get { return GetBool("SuDungMaSanCo"); } }
        public static bool TuDongSinhMaVach { get { return GetBool("TuDongSinhMaVach"); } }
        public static int ChieuDaiMaVach { get { return GetInt("ChieuDaiMaVach", 7); } }
        public static bool ChoPhepTrungTenMatHang { get { return GetBool("ChoPhepTrungTenMatHang"); } }
        public static bool SuDungTenTiengAnh { get { return GetBool("SuDungTenTiengAnh"); } }
        public static bool SuDungGia2 { get { return GetBool("SuDungGia2"); } }
        public static bool SuDungGia3 { get { return GetBool("SuDungGia3"); } }
        public static bool SuDungGia4 { get { return GetBool("SuDungGia4"); } }
        /// <summary>Nhãn Giá 2/3/4. Chú ý: giá trị seed của DienGiaiGia3 là "Giá 2" (sai chữ của app gốc).</summary>
        public static string DienGiaiGia2 { get { return GetText("DienGiaiGia2", "Giá 2"); } }
        public static string DienGiaiGia3 { get { return GetText("DienGiaiGia3", "Giá 3"); } }
        public static string DienGiaiGia4 { get { return GetText("DienGiaiGia4", "Giá 4"); } }
        public static bool SuDung2DonViTinh { get { return GetBool("SuDung2DonViTinh"); } }
        public static bool KichHoatSuDungCanDienTu { get { return GetBool("KichHoatSuDungCanDienTu"); } }

        // ---- Khách đặt hàng (docs/specs/khach-dat-hang.md §3.8) ----
        /// <summary>Bước làm tròn tiền (LamTron): 1000 theo DB thật; luật chung dùng cho cả nhà hàng + bán lẻ.</summary>
        public static int LamTronTien { get { return GetInt("LamTronTien", 1000); } }
        /// <summary>Loại ứng dụng (TEXTVALUE): "1" = POS; "HOTEL" = khách sạn (không dùng ở F&B thuần).</summary>
        public static string UngDung { get { return GetText("UngDung", "1"); } }
        public static int SoHoaDonQuayVongTheo { get { return GetInt("SoHoaDonQuayVongTheo", 1); } }
        public static bool SuDungTheTraTruoc { get { return GetBool("SuDungTheTraTruoc"); } }
        public static bool CoThueSuat { get { return GetBool("CoThueSuat"); } }
        public static bool CoPhiVanChuyen { get { return GetBool("CoPhiVanChuyen"); } }
        public static bool NhapMotMatHangNhieuLanTrongPhieu { get { return GetBool("NhapMotMatHangNhieuLanTrongPhieu"); } }
        /// <summary>Sắp xếp lưới mặt hàng: 1 = theo CODE, khác = theo NAME.</summary>
        public static int SapXepThuTuTheo { get { return GetInt("SapXepThuTuTheo", 0); } }
        public static bool TuDongCapNhatGiaNhapDinhLuong { get { return GetBool("TuDongCapNhatGiaNhapCuaMatHangDinhLuong"); } }
        public static bool TuDongCapNhatGiaVonDinhLuong { get { return GetBool("TuDongCapNhatGiaVonCuaMatHangDinhLuong"); } }
        public static bool GiaBanLeUsd { get { return GetBool("GiaBanLeUsd"); } }

        // ---- Khóa của chức năng Danh mục bàn khu vực (spec 3.10) ----

        public static bool ChoPhepDungNhieuBangGiaTrenBill { get { return GetBool("ChoPhepDungNhieuBangGiaTrenBill"); } }
        public static bool SuDungGiaTheoGio { get { return GetBool("SuDungGiaTheoGio"); } }
        public static bool SuDungMatHangMacDinh { get { return GetBool("SuDungMatHangMacDinh"); } }
        public static bool PhanQuyenTruyCapTheoKhuVuc { get { return GetBool("PhanQuyenTruyCapTheoKhuVuc"); } }
        public static bool CoSuDungCongTacDienTu { get { return GetBool("CoSuDungCongTacDienTu"); } }
        public static bool SuDungNhieuKho { get { return GetBool("SuDungNhieuKho"); } }

        // ---- Khóa của chức năng Sử dụng dịch vụ (docs/specs/su-dung-dich-vu.md §3.10, §6.5, §6.6) ----

        public static bool HeThongChayNhieuMayTram { get { return GetBool("HeThongChayNhieuMayTram"); } }
        public static bool ChoPhepThayDoiHoaDonSauKhiInTamTinh { get { return GetBool("ChoPhepThayDoiHoaDonSauKhiInTamTinh"); } }
        public static bool KhongDuocGiamDoSauKhiInPhaChe { get { return GetBool("KhongDuocGiamDoSauKhiInPhaChe"); } }
        public static bool NhapMatKhauGiamDo { get { return GetBool("NhapMatKhauGiamDo"); } }
        public static bool NhapLyDoKhiXoaMon { get { return GetBool("NhapLyDoKhiXoaMon"); } }
        public static bool ChoPhepNhapGiamGia { get { return GetBool("ChoPhepNhapGiamGia"); } }
        public static bool CoPhiDichVu { get { return GetBool("CoPhiDichVu"); } }
        public static decimal MacDinhPhiDichVu { get { return GetDecimal("MacDinhPhiDichVu", 0m); } }
        public static decimal MacDinhThueSuat { get { return GetDecimal("MacDinhThueSuat", 0m); } }
        public static decimal MacDinhGiamGia { get { return GetDecimal("MacDinhGiamGia", 0m); } }
        public static int TuDongDoiGioVaoVeTruoc { get { return GetInt("TuDongDoiGioVaoVeTruoc", 0); } }
        public static bool HienThiGhiChuTrenGiaoDienBan { get { return GetBool("HienThiGhiChuTrenGiaoDienBan"); } }
        public static int KichThuocHienThiOGiaoDienDichVu { get { return GetInt("KichThuocHienThiOGiaoDienDichVu", 0); } }
        /// <summary>0 = theo ngày mở bàn, 1 = theo ngày thanh toán (§6.5).</summary>
        public static int CachChonNgayGiaoDich { get { return GetInt("CachChonNgayGiaoDich", 1); } }
        public static bool HienThiCuaSoNhapSoLuongKhiQuetMaVach { get { return GetBool("HienThiCuaSoNhapSoLuongKhiQuetMaVach"); } }
        public static bool BatBuocNhapNhanVienBanHang { get { return GetBool("BatBuocNhapNhanVienBanHang"); } }
        /// <summary>Cấu hình toàn hệ thống cho phép âm kho (§6.6). Mặc định tắt (chặn âm kho).</summary>
        public static bool ChoPhepAmKho { get { return GetBool("ChoPhepAmKho"); } }
        /// <summary>Tự động tính lại giá vốn sau khi lưu/sửa/xóa phiếu nhập kho (docs/specs/tinh-lai-gia-von.md §4). Mặc định tắt.</summary>
        public static bool TuDongTinhGiaVon { get { return GetBool("TuDongTinhGiaVon"); } }
        /// <summary>Tính lương sử dụng máy chấm công (docs/specs/nhan-su-danh-muc.md §4): 30 = bật, rỗng hoặc khác = tắt.</summary>
        public static bool TinhLuongSuDungMayChamCong { get { return GetBool("TinhLuongSuDungMayChamCong"); } }

        // ---- Khóa phần Thanh toán (docs/specs/su-dung-dich-vu.md §3.7, §3.10) ----

        public static bool CoThanhToanThe { get { return GetBool("CoThanhToanThe"); } }
        public static bool CoThanhToanChuyenKhoan { get { return GetBool("CoThanhToanChuyenKhoan"); } }
        public static bool CoThanhToanVoucher { get { return GetBool("CoThanhToanVoucher"); } }
        public static bool LuaChonVoucherTuDanhSach { get { return GetBool("LuaChonVoucherTuDanhSach"); } }
        public static bool SuDungDiemTichLuyDeThanhToan { get { return GetBool("SuDungDiemTichLuyDeThanhToan"); } }
        public static decimal QuyDoi1DiemSangTien { get { return GetDecimal("QuyDoi1DiemSangTien", 0m); } }
        public static bool BatBuocInKhiThanhToan { get { return GetBool("BatBuocInKhiThanhToan"); } }
        public static bool ChoPhepInTamTinh { get { return GetBool("ChoPhepInTamTinh"); } }
        public static bool SuDungChucNangTamUngTrongDonHang { get { return GetBool("SuDungChucNangTamUngTrongDonHang"); } }
        public static bool BatChucNangKiemSoatOrder { get { return GetBool("BatChucNangKiemSoatOrder"); } }

        // ---- Khóa phần Chuyển/Gộp bàn, Chuyển mặt hàng & Hủy bill (docs/specs/su-dung-dich-vu.md §3.8, §3.10) ----

        /// <summary>0 = chỉ nâng giá, 1 = luôn cập nhật theo giá khu vực mới, 2 = không cập nhật (ban-khu-vuc.md §2).</summary>
        public static int GiaMatHangKhiChuyenBan { get { return GetInt("GiaMatHangKhiChuyenBan", 0); } }
        /// <summary>Thời gian tối đa (phút) tính từ BATDAU cho phép hủy bill trên bàn (§3.8, DB mặc định 50).</summary>
        public static int ThoiGianChoPhepHuyBill { get { return GetInt("ThoiGianChoPhepHuyBill", 50); } }
        public static bool NhapLyDoKhiHuyHoaDon { get { return GetBool("NhapLyDoKhiHuyHoaDon"); } }
        public static bool SuDungChucNangInXuongBep { get { return GetBool("SuDungChucNangInXuongBep"); } }
        public static bool InDoAn { get { return GetBool("InDoAn"); } }
        public static bool InDoUong { get { return GetBool("InDoUong"); } }
        public static bool InDichVu { get { return GetBool("InDichVu"); } }
        public static bool InDoKhac { get { return GetBool("InDoKhac"); } }

        // ---- Khóa phần In hóa đơn & In bếp/chế biến (docs/specs/su-dung-dich-vu.md §3.7, §3.10, §6.6) ----

        public static int SoLanInTamTinhToiDa { get { return GetInt("SoLanInTamTinhToiDa", 0); } }
        public static int SoLanInToiDa { get { return GetInt("SoLanInToiDa", 0); } }
        public static bool HienThiTruocKhiIn { get { return GetBool("HienThiTruocKhiIn"); } }
        public static bool LuaChonMauKhiIn { get { return GetBool("LuaChonMauKhiIn"); } }
        public static int SoLanIn { get { return GetInt("SoLanIn", 1); } }
        public static string MauHoaDon { get { return GetText("MauHoaDon", ""); } }
        /// <summary>0 = tại quầy, 1 = theo khu vực, 2 = tạm tính theo KV + HĐ tại quầy, 3 = 1 liên quầy + 1 liên KV.</summary>
        public static int InHoaDonTheoKhuVuc { get { return GetInt("InHoaDonTheoKhuVuc", 0); } }
        public static bool InHoaDonCongGopMatHang { get { return GetBool("InHoaDonCongGopMatHang"); } }
        public static bool HienThiDiemCuaKhachHangTrenHoaDon { get { return GetBool("HienThiDiemCuaKhachHangTrenHoaDon"); } }
        public static string LoiCamOn { get { return GetText("LoiCamOn", ""); } }

        public static bool InRiengDoAnUong { get { return GetBool("InRiengDoAnUong"); } }
        public static bool InPhaCheTheoKhuVuc { get { return GetBool("InPhaCheTheoKhuVuc"); } }
        public static bool InDonGiaTrenMauInBep { get { return GetBool("InDonGiaTrenMauInBep"); } }
        public static int SoLienInCheBien { get { return GetInt("SoLienInCheBien", 1); } }
        public static string MauInCheBien { get { return GetText("MauInCheBien", ""); } }
        public static bool InMoiDoRa1To { get { return GetBool("InMoiDoRa1To"); } }
        public static bool InThem1LienTaiQuay { get { return GetBool("InThem1LienTaiQuay"); } }
        public static bool LuuVetInCheBien { get { return GetBool("LuuVetInCheBien"); } }
        public static bool TuDongInPhaCheSauKhiGoiMon { get { return GetBool("TuDongInPhaCheSauKhiGoiMon"); } }
        public static int ThoiGianTuDongIn { get { return GetInt("ThoiGianTuDongIn", 10); } }
    }
}
