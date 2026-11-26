using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void CustomerGroupAndPerfTests(string db)
        {
            CustomerGroupRepositoryTests(db);
            CustomerPerformance1000Tests(db);
        }

        private static void CustomerGroupRepositoryTests(string db)
        {
            var groupRepo = new CustomerGroupRepository(db);
            var custRepo = new CustomerRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            string parentId = null;
            string childId = null;
            string customerId = null;

            try
            {
                // 1. Thêm nhóm cha đầy đủ trường (§4.2).
                var parent = CustomerGroupRow.NewDefault();
                parent.Name = "Nhóm VIP " + tag;
                parent.Note = "Ghi chú nhóm UTF-8";
                parent.LoyaltyPoints = 1500.5m;
                parent.DiscountTotal = 10m;
                parent.DiscountGoods = 8.5m;
                parent.DiscountHour = 5m;
                parent.DiscountFood = 12m;
                parent.DiscountDrink = 15m;
                parent.DiscountService = 3m;
                parent.DiscountOther = 2m;
                CustomerGroupSaveResult pRes = groupRepo.Save(parent, "selftest_kh");
                parentId = pRes.SavedId;
                CustomerGroupRow pBack = pRes.Ok ? groupRepo.Load(parentId) : null;

                Check("KH §6 (thêm nhóm khách hàng §4.2): lưu và đọc lại đủ điểm + 7 tỉ lệ giảm + STATUS=30",
                    pRes.Ok && pBack != null && pBack.Status == 30
                    && pBack.Name == parent.Name && pBack.Note == parent.Note
                    && pBack.LoyaltyPoints == 1500.5m
                    && pBack.DiscountTotal == 10m && pBack.DiscountGoods == 8.5m && pBack.DiscountHour == 5m
                    && pBack.DiscountFood == 12m && pBack.DiscountDrink == 15m
                    && pBack.DiscountService == 3m && pBack.DiscountOther == 2m,
                    SingleLine(pRes.ErrorMessage));

                // 2. Tên nhóm trống hoặc trùng (UNICODE_CI_AI) bị chặn; sửa chính mình thành công.
                CustomerGroupSaveResult emptyGrp = groupRepo.Save(CustomerGroupRow.NewDefault(), "selftest_kh");
                CustomerGroupRow dupGrp = CustomerGroupRow.NewDefault();
                dupGrp.Name = "NHÓM VÌP " + tag;
                CustomerGroupSaveResult dupRes = groupRepo.Save(dupGrp, "selftest_kh");

                pBack.DiscountTotal = 12.5m;
                CustomerGroupSaveResult editRes = groupRepo.Save(pBack, "selftest_edit");
                CustomerGroupRow pAfter = groupRepo.Load(parentId);
                Check("KH §6 (kiểm tra nhóm trống/trùng và sửa nhóm): chặn trống và trùng dấu, sửa chính mình cập nhật đúng",
                    !emptyGrp.Ok && emptyGrp.ErrorMessage == CustomerText.GroupNameEmpty
                    && !dupRes.Ok && dupRes.ErrorMessage == CustomerText.GroupNameDuplicate(dupGrp.Name)
                    && editRes.Ok && pAfter != null && pAfter.DiscountTotal == 12.5m && pAfter.ModifiedById == "selftest_edit",
                    SingleLine(dupRes.ErrorMessage));

                // 3. Tỉ lệ giảm ngoài 0..100 bị chặn ở repository (§9.0 Q9).
                CustomerGroupRow over100 = CustomerGroupRow.NewDefault();
                over100.Name = "Nhóm quá 100 " + tag;
                over100.DiscountTotal = 101m;
                CustomerGroupRow under0 = CustomerGroupRow.NewDefault();
                under0.Name = "Nhóm âm " + tag;
                under0.DiscountDrink = -1m;
                Check("KH §6/§9.0 (tỉ lệ giảm nhóm 0..100 ở DB): chặn > 100% và < 0%",
                    !groupRepo.Save(over100, "selftest_kh").Ok
                    && !groupRepo.Save(under0, "selftest_kh").Ok);

                // 4. Thêm nhóm con → chặn xóa nhóm cha khi còn nhóm con (§9.0.2).
                var child = CustomerGroupRow.NewDefault();
                child.Name = "Nhóm VIP Con " + tag;
                child.ParentId = parentId;
                CustomerGroupSaveResult cRes = groupRepo.Save(child, "selftest_kh");
                childId = cRes.SavedId;
                CustomerGroupRow cBack = cRes.Ok ? groupRepo.Load(childId) : null;

                CustomerGroupDeleteResult delParentWithChild = groupRepo.Delete(parentId);
                Check("KH §6/§9.0.2 (xóa nhóm còn nhóm con): bị chặn, phải xóa nhóm con trước",
                    cRes.Ok && cBack != null && cBack.ParentId == parentId
                    && !delParentWithChild.Ok && delParentWithChild.ChildGroupCount == 1
                    && groupRepo.Load(parentId) != null,
                    SingleLine(delParentWithChild.ErrorMessage));

                // 5. Gán khách vào nhóm con → chặn xóa nhóm còn khách (§9.0 Q2).
                var cust = CustomerRow.NewDefault();
                cust.Code = "KHG" + tag;
                cust.Name = "Khách thuộc nhóm con " + tag;
                cust.GroupId = childId;
                CustomerSaveResult custRes = custRepo.SaveCustomer(cust, "selftest_kh", new CustomerSaveOptions());
                customerId = custRes.SavedId;

                CustomerGroupDeleteResult delChildWithCust = groupRepo.Delete(childId);
                Check("KH §6/§9.0 (xóa nhóm còn khách): bị chặn xóa",
                    custRes.Ok && !delChildWithCust.Ok && delChildWithCust.CustomerCount == 1
                    && groupRepo.Load(childId) != null,
                    SingleLine(delChildWithCust.ErrorMessage));

                // 6. Xóa khách → xóa nhóm con → xóa nhóm cha thành công (xóa cứng).
                CustomerDeleteResult delCust = custRepo.DeleteCustomer(customerId);
                if (delCust.Ok) customerId = null;
                CustomerGroupDeleteResult delChildOk = groupRepo.Delete(childId);
                if (delChildOk.Ok) childId = null;
                CustomerGroupDeleteResult delParentOk = groupRepo.Delete(parentId);
                if (delParentOk.Ok) parentId = null;
                Check("KH §6/§9.0 (xóa nhóm sau khi hết khách và hết nhóm con): xóa cứng thành công",
                    delCust.Ok && delChildOk.Ok && delParentOk.Ok
                    && groupRepo.Load(child.Id) == null && groupRepo.Load(parent.Id) == null);
            }
            finally
            {
                if (customerId != null) SafeDeleteById(db, "DKHACHHANG", customerId);
                if (childId != null) SafeDeleteById(db, "DNHOMKHACHHANG", childId);
                if (parentId != null) SafeDeleteById(db, "DNHOMKHACHHANG", parentId);
            }
        }

        private static void CustomerPerformance1000Tests(string db)
        {
            string tag = "P1K-" + DbHelper.NewId().Substring(0, 6);
            string khTableId = new CatalogRepository(db).FindIdByTable("DKHACHHANG");
            try
            {
                DbHelper.InTransaction(db, (conn, tx) =>
                {
                    DateTime now = DbHelper.Now();
                    for (int i = 1; i <= 1000; i++)
                    {
                        DbHelper.Exec(conn, tx,
                            "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIENTHOAI, DIEMTICHLUYBANDAU, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                            "VALUES (@ID, @CODE, @NAME, @ADDR, @PHONE, @PTS, 0, 30, 'selftest_kh', @TC)",
                            "@ID", DbHelper.NewId(),
                            "@CODE", tag + "-" + i.ToString("D4"),
                            "@NAME", "Khách Tải " + tag + " " + i.ToString("D4"),
                            "@ADDR", "Địa chỉ số " + i,
                            "@PHONE", "090000" + i.ToString("D4"),
                            "@PTS", i,
                            "@TC", now);
                    }
                });

                Stopwatch swOpen = Stopwatch.StartNew();
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, khTableId, "Danh mục khách hàng") { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    swOpen.Stop();

                    int totalLoaded = view.VisibleRows;
                    Stopwatch swFilter = Stopwatch.StartNew();
                    view.SetSearch(tag + "-0750");
                    Application.DoEvents();
                    swFilter.Stop();
                    int afterSearch = view.VisibleRows;
                    host.Close();

                    Check("KH §6 (hiệu năng 1.000 khách): mở màn < 1 giây và lọc mượt",
                        totalLoaded >= 1000 && swOpen.ElapsedMilliseconds < 1000 && afterSearch == 1 && swFilter.ElapsedMilliseconds < 500,
                        "openMs=" + swOpen.ElapsedMilliseconds + ", filterMs=" + swFilter.ElapsedMilliseconds + ", rows=" + totalLoaded + ", hit=" + afterSearch);
                }
            }
            finally
            {
                try
                {
                    BookingExec(db, "DELETE FROM DKHACHHANG WHERE MAKHACH STARTING WITH @P", "@P", tag + "-");
                }
                catch
                {
                }
            }
        }

        private static void TestCustomerLiveThemeSwitch(string db, string khTableId)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new CustomerEditDialog(AppSession.Current, null, null, false))
            {
                var view = new CatalogView(db, khTableId, "Danh mục khách hàng") { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    ThemeManager.Apply(tB);
                    view.ApplyTheme(tB, tB.Background);
                    Application.DoEvents();

                    DataGridView g = view.GridForTest;
                    bool cellColorOk = g.Rows.Count == 0
                        || (g.Rows[0].Cells[0].InheritedStyle.BackColor == tB.Surface
                            && g.Rows[0].Cells[0].InheritedStyle.SelectionBackColor == tB.Primary);
                    bool gridColorOk = g.DefaultCellStyle.SelectionBackColor == tB.Primary
                        && g.BackgroundColor == tB.Surface
                        && cellColorOk;
                    bool treeColorOk = view.TreeForTest.BackColor == tB.Surface
                        && view.TreeForTest.ForeColor == tB.Text;
                    bool dlgColorOk = dlg.BackColor == tB.Background;

                    Check("KH §6 (đổi bảng màu trực tiếp): lưới, ô lưới, cây nhóm và dialog đổi đúng màu thực của theme mới",
                        gridColorOk && treeColorOk && dlgColorOk,
                        "grid=" + gridColorOk + ", cell=" + cellColorOk + ", tree=" + treeColorOk + ", dlg=" + dlgColorOk);
                }
                finally
                {
                    dlg.Close();
                    host.Close();
                }
            }
        }

        private static void SaveCustomerBoolConfigs(string db, ConfigRepository cfgRepo, int giaVal, int theVal)
        {
            cfgRepo.Save(new[]
            {
                new ConfigChange { Name = "SuDungGiaTheoKhach", DataType = 3, IntValue = giaVal },
                new ConfigChange { Name = "SuDungTheTraTruoc", DataType = 3, IntValue = theVal }
            }, "selftest_kh");
            SystemConfig.Reload(db);
        }

        private static void ShowDialogOffscreen(Form dlg)
        {
            dlg.WindowState = FormWindowState.Normal;
            dlg.StartPosition = FormStartPosition.Manual;
            dlg.Location = new Point(-20000, -20000);
            dlg.Show();
            Application.DoEvents();
        }

        private static bool InvokeProcessCmdKey(Control ctl, ref Message msg, Keys keyData)
        {
            var m = typeof(Control).GetMethod("ProcessCmdKey",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object[] args = { msg, keyData };
            bool res = (bool)m.Invoke(ctl, args);
            msg = (Message)args[0];
            return res;
        }

        private static void InvokeGridDoubleClick(DataGridView grid, int colIndex, int rowIndex)
        {
            var m = typeof(DataGridView).GetMethod("OnCellDoubleClick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            m.Invoke(grid, new object[] { new DataGridViewCellEventArgs(colIndex, rowIndex) });
        }

        private static void InvokeTreeNodeDoubleClick(TreeView tree, TreeNode node)
        {
            var m = typeof(TreeView).GetMethod("OnNodeMouseDoubleClick",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            m.Invoke(tree, new object[] { new TreeNodeMouseClickEventArgs(node, MouseButtons.Left, 2, 0, 0) });
        }

        private static MenuNode FindMenuLeafByTitle(List<MenuNode> nodes, string title)
        {
            foreach (MenuNode n in nodes)
            {
                if (n.IsLeaf && string.Equals((n.Title ?? "").Trim(), title, StringComparison.OrdinalIgnoreCase))
                    return n;
                MenuNode hit = FindMenuLeafByTitle(n.Children, title);
                if (hit != null)
                    return hit;
            }
            return null;
        }
    }
}
