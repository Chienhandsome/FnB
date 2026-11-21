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
    }
}
