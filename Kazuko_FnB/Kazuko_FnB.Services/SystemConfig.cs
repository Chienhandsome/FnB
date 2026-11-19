using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
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
                catch (FbException)
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
    }
}
