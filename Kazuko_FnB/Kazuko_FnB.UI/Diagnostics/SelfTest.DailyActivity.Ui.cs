using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void DailyActivityUiTests(string db, DailyActivityFixture fx)
        {
            var adminSession = new UserSession
            {
                UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };

            // 0. Kiểm đầu báo cáo đọc đúng 4 khóa thật trong SCONFIG (CompanyName, CompanyAddress, CompanyPhone, CompanyEmail) và hoàn lại trong finally.
            DailyActivityCompanyHeaderUiTest(db, adminSession);

            // 1. Màn Admin: đổi ngày trong ô nhưng chưa bấm Tải → đầu báo cáo không đổi; bấm Tải → cập nhật đúng.
            using (var host = MakeOffscreenHost())
            {
                var view = new DailyActivityView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    List<KeyValuePair<string, string>> initialTexts = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                    string initialHeaderFilter = FindObjectText(initialTexts, "txtFilter");

                    // Đổi ô lọc NHƯNG CHƯA bấm Tải dữ liệu → dòng đầu báo cáo trên trang in KHÔNG được đổi!
                    view.FromDateBoxForTest.Inner.Text = "10/07/2004";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "11/07/2004";
                    view.ToDateBoxForTest.Commit();
                    List<KeyValuePair<string, string>> beforeLoadTexts = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                    bool headerUnchangedBeforeLoad = initialHeaderFilter.Length > 0
                        && FindObjectText(beforeLoadTexts, "txtFilter") == initialHeaderFilter;

                    // Bấm nút Tải dữ liệu (PerformClick thật).
                    view.LoadButtonForTest.PerformClick();
                    Application.DoEvents();

                    List<KeyValuePair<string, string>> afterLoadTexts = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                    string afterFilter = FindObjectText(afterLoadTexts, "txtFilter");
                    bool headerUpdatedAfterLoad = afterFilter.IndexOf("10/07/2004", StringComparison.Ordinal) >= 0
                        && afterFilter.IndexOf("11/07/2004", StringComparison.Ordinal) >= 0
                        && afterFilter.IndexOf("Thu ngân: Tất cả", StringComparison.Ordinal) >= 0;
                    Check("Chi tiết HĐ ngày UI (đầu báo cáo phản ánh dữ liệu đã tải): chưa bấm Tải không đổi, bấm Tải mới cập nhật",
                        headerUnchangedBeforeLoad && headerUpdatedAfterLoad, afterFilter.Replace("\r", " ").Replace("\n", " "));
                }
                finally { host.Close(); }
            }

            // 2. Q15 & P2 trên UI thật: User thường 1 CH (chọn sẵn Store A; xóa chọn vẫn chỉ thấy Store A) & User 2 CH (để trống).
            var user1Session = new UserSession
            {
                UserId = fx.Cashier1Id, UserName = "cthd_tn1", DisplayName = "CTHD Thu ngân 1",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };
            using (var host1 = MakeOffscreenHost())
            {
                var view1 = new DailyActivityView(user1Session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host1.Controls.Add(view1);
                host1.Show();
                Application.DoEvents();
                try
                {
                    var selectedStore = view1.StoreComboForTest.SelectedItem as StoreRecord;
                    bool q15SingleOk = view1.StoreComboForTest.Items.Count == 2
                        && view1.StoreComboForTest.SelectedIndex == 1
                        && selectedStore != null && selectedStore.Id == fx.StoreAId;

                    view1.StoreComboForTest.SelectedIndex = 0;
                    view1.FromDateBoxForTest.Inner.Text = "10/07/2004";
                    view1.FromDateBoxForTest.Commit();
                    view1.ToDateBoxForTest.Inner.Text = "11/07/2004";
                    view1.ToDateBoxForTest.Commit();
                    view1.LoadButtonForTest.PerformClick();
                    Application.DoEvents();

                    List<KeyValuePair<string, string>> texts1 = FastReportEngine.ExtractPreparedPageTexts(view1.CurrentReportForTest);
                    List<string> billNames = FindAllObjectTexts(texts1, "Cell13");
                    List<string> purNames = FindAllObjectTexts(texts1, "Cell95");
                    bool p2ClearedStillStoreA = view1.LoadedDataForTest.SaleBills.Count == 2
                        && view1.LoadedDataForTest.Purchases.Count == 3
                        && billNames.Count == 2
                        && purNames.Count == 3;
                    Check("Chi tiết HĐ ngày UI (§7/Q15/P2 user thường 1 CH): mở màn chọn sẵn Store A, xóa chọn vẫn bị ràng buộc Store A",
                        q15SingleOk && p2ClearedStillStoreA,
                        "q15=" + q15SingleOk + ", p2=" + p2ClearedStillStoreA + ", bills=" + billNames.Count + ", pur=" + purNames.Count);
                }
                finally { host1.Close(); }
            }

            var user2Session = new UserSession
            {
                UserId = fx.Cashier2Id, UserName = "cthd_tn2", DisplayName = "CTHD Thu ngân 2",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };
            using (var host2 = MakeOffscreenHost())
            {
                var view2 = new DailyActivityView(user2Session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host2.Controls.Add(view2);
                host2.Show();
                Application.DoEvents();
                try
                {
                    Check("Chi tiết HĐ ngày UI (§7/Q15 user thường 2 CH): mở màn để trống ô Cửa hàng",
                        view2.StoreComboForTest.Items.Count == 3 && view2.StoreComboForTest.SelectedIndex == 0);
                }
                finally { host2.Close(); }
            }
        }

        private static void DailyActivityCompanyHeaderUiTest(string db, UserSession adminSession)
        {
            var cfgRepo = new ConfigRepository(db);
            Dictionary<string, ConfigValue> oldMap = cfgRepo.LoadValues();
            string oldName = ReadCfgText(oldMap, "CompanyName");
            string oldAddr = ReadCfgText(oldMap, "CompanyAddress");
            string oldPhone = ReadCfgText(oldMap, "CompanyPhone");
            string oldEmail = ReadCfgText(oldMap, "CompanyEmail");

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = "CompanyName", DataType = 1, TextValue = "CÔNG TY THỬ CTHD" },
                    new ConfigChange { Name = "CompanyAddress", DataType = 1, TextValue = "99 Trần Hưng Đạo, Q1" },
                    new ConfigChange { Name = "CompanyPhone", DataType = 1, TextValue = "0909123456" },
                    new ConfigChange { Name = "CompanyEmail", DataType = 1, TextValue = "cthd@kazuko.vn" }
                }, "selftest");
                SystemConfig.Reload(db);

                using (var host = MakeOffscreenHost())
                {
                    var view = new DailyActivityView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(view.CurrentReportForTest);
                        string compName = FindObjectText(texts, "txtTenDoanhNghiep");
                        string compAddr = FindObjectText(texts, "txtDiaChi");
                        string compContact = FindObjectText(texts, "txtDienThoai");
                        bool companyOk = compName == "CÔNG TY THỬ CTHD"
                            && compAddr.IndexOf("99 Trần Hưng Đạo, Q1", StringComparison.Ordinal) >= 0
                            && compContact.IndexOf("0909123456", StringComparison.Ordinal) >= 0
                            && compContact.IndexOf("cthd@kazuko.vn", StringComparison.Ordinal) >= 0;
                        Check("Chi tiết HĐ ngày UI (đầu báo cáo đọc 4 khóa thật SCONFIG CompanyName/Address/Phone/Email)",
                            companyOk, compName + " | " + compAddr + " | " + compContact);
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = "CompanyName", DataType = 1, TextValue = oldName },
                    new ConfigChange { Name = "CompanyAddress", DataType = 1, TextValue = oldAddr },
                    new ConfigChange { Name = "CompanyPhone", DataType = 1, TextValue = oldPhone },
                    new ConfigChange { Name = "CompanyEmail", DataType = 1, TextValue = oldEmail }
                }, "selftest");
                SystemConfig.Reload(db);
            }
        }

        private static string ReadCfgText(Dictionary<string, ConfigValue> map, string key)
        {
            ConfigValue v;
            return map != null && map.TryGetValue(key, out v) && v != null ? (v.TextValue ?? "") : "";
        }

        private static Form MakeOffscreenHost()
        {
            return new Form
            {
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                Location = new Point(-20000, -20000),
                ClientSize = new Size(1400, 780)
            };
        }

        private static void SelectCashierById(ComboBox combo, string cashierId)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var c = combo.Items[i] as CashierRecord;
                if (c != null && c.Id == cashierId)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }
    }
}
