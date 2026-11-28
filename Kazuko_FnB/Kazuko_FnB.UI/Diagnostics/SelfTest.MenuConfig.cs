using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        /// <summary>Chiến chốt 28/11: menu có VISIBLECONFIGID hiện/ẩn theo cờ SCONFIG (voucher, khuyến mại, thẻ trả trước...).</summary>
        private static void MenuConfigTests(string db)
        {
            string[] keys = { "LuaChonVoucherTuDanhSach", "KichHoatKhuyenMaiTuDong" };
            var cfgRepo = new ConfigRepository(db);
            var backups = BackupOpeningConfigs(db, keys);
            var session = new UserSession { UserId = "selftest_mc", UserName = "selftest_mc", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
            try
            {
                SetOpeningConfigInt(db, cfgRepo, backups, keys[0], 0);
                SetOpeningConfigInt(db, cfgRepo, backups, keys[1], 0);
                List<MenuNode> off = MainMenuBuilder.Build(session).Roots;
                bool voucherOff = FindMenuLeafByTitle(off, "Danh mục voucher") == null;
                bool promoOff = FindMenuLeafByTitle(off, "Danh mục đợt khuyến mại") == null;

                SetOpeningConfigInt(db, cfgRepo, backups, keys[0], 30);
                SetOpeningConfigInt(db, cfgRepo, backups, keys[1], 30);
                List<MenuNode> on = MainMenuBuilder.Build(session).Roots;
                bool voucherOn = FindMenuLeafByTitle(on, "Danh mục voucher") != null;
                bool promoOn = FindMenuLeafByTitle(on, "Danh mục đợt khuyến mại") != null;

                Check("Menu theo cờ cấu hình (voucher, khuyến mại): cờ 0 ẩn menu, cờ 30 hiện menu",
                    voucherOff && promoOff && voucherOn && promoOn,
                    "vOff=" + voucherOff + ", pOff=" + promoOff + ", vOn=" + voucherOn + ", pOn=" + promoOn);
            }
            finally { RestoreOpeningConfigs(db, cfgRepo, backups); SystemConfig.Reload(db); }
        }

        /// <summary>Chạy nhóm test cần menu hiện: bật cờ cấu hình menu rồi trả lại giá trị cũ.</summary>
        private static void WithMenuFlagsOn(string db, string key, Action body)
        {
            var cfgRepo = new ConfigRepository(db);
            var backups = BackupOpeningConfigs(db, new[] { key });
            try
            {
                SetOpeningConfigInt(db, cfgRepo, backups, key, 30);
                body();
            }
            finally { RestoreOpeningConfigs(db, cfgRepo, backups); SystemConfig.Reload(db); }
        }
    }
}
