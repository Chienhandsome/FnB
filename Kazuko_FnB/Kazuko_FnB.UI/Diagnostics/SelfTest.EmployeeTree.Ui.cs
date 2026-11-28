using System;
using System.Collections.Generic;
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
        private static void EmployeeTreeUiTests(string db, UserSession adminSess)
        {
            var svc = new EmployeeService(adminSess);
            var repo = new EmployeeRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            string empTableId = new CatalogRepository(db).FindIdByTable(EmployeeText.TableName);
            var createdIds = new List<string>();
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                // Seed cấu trúc cây: Thư mục cha -> Thư mục con; NV gốc, NV cha, NV con, Phân cách ở cha.
                EmployeeRow fParent = EmployeeRow.NewFolder(null);
                fParent.Name = "Bộ Phận Bếp " + tag;
                EmployeeSaveResult rFP = svc.SaveFolder(fParent);
                if (rFP.Ok) createdIds.Add(rFP.SavedId);

                EmployeeRow fChild = EmployeeRow.NewFolder(rFP.SavedId);
                fChild.Name = "Bếp Nóng " + tag;
                EmployeeSaveResult rFC = svc.SaveFolder(fChild);
                if (rFC.Ok) createdIds.Add(rFC.SavedId);

                EmployeeRow eRoot = EmployeeRow.NewDefault();
                eRoot.Name = "NV UI Gốc " + tag;
                EmployeeSaveResult rER = svc.Save(eRoot);
                if (rER.Ok) createdIds.Add(rER.SavedId);

                EmployeeRow eParent = EmployeeRow.NewDefault();
                eParent.Name = "NV UI Cha " + tag;
                eParent.ParentId = rFP.SavedId;
                EmployeeSaveResult rEP = svc.Save(eParent);
                if (rEP.Ok) createdIds.Add(rEP.SavedId);

                EmployeeRow eChild = EmployeeRow.NewDefault();
                eChild.Name = "NV UI Con " + tag;
                eChild.ParentId = rFC.SavedId;
                EmployeeSaveResult rEC = svc.Save(eChild);
                if (rEC.Ok) createdIds.Add(rEC.SavedId);

                EmployeeSaveResult rSep = svc.AddSeparator(rFP.SavedId);
                if (rSep.Ok) createdIds.Add(rSep.SavedId);

                // §5 #2 & #7: Mở màn 1366x768, kiểm cây trái, lọc cha/con/Tất cả, phân cách, Thêm mới ▾ đủ 4 mục, đổi theme.
                using (var host = MakeOffscreenHost())
                {
                    host.Size = new Size(1366, 768);
                    var view = new CatalogView(db, empTableId, EmployeeText.FunctionName)
                    {
                        Dock = DockStyle.Fill,
                        SuppressEmployeePopupForTest = true
                    };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        // Kiểm 4 mục trong menu "Thêm mới ▾" và trạng thái mờ "Thêm thư mục con" khi ở "Tất cả".
                        view.SelectEmployeeFolderByIdForTest(null);
                        Application.DoEvents();
                        view.UpdateEmployeeAddMenuState();
                        bool addBtnTextOk = view.AddButtonForTest.Text.IndexOf("Thêm mới", StringComparison.Ordinal) >= 0;
                        var menuItems = view.EmployeeAddMenuForTest.Items;
                        bool fourItems = menuItems.Count == 4
                            && menuItems[0].Text == "Thêm nhân viên"
                            && menuItems[1].Text == "Thêm thư mục"
                            && menuItems[2].Text == "Thêm thư mục con"
                            && menuItems[3].Text == "Thêm phân cách";
                        bool subDisabledAtRoot = menuItems.Count >= 3 && !menuItems[2].Enabled;

                        // Chọn Tất cả -> hiện cả NV gốc, NV cha, NV con, phân cách.
                        bool allHasRoot = GridContainsId(view.GridForTest, rER.SavedId);
                        bool allHasParent = GridContainsId(view.GridForTest, rEP.SavedId);
                        bool allHasChild = GridContainsId(view.GridForTest, rEC.SavedId);
                        bool allHasSep = GridContainsId(view.GridForTest, rSep.SavedId);

                        // Chọn thư mục cha -> hiện NV cha, NV con, phân cách của cha; KHÔNG hiện NV gốc; "Thêm thư mục con" bật.
                        bool selParentOk = view.SelectEmployeeFolderByIdForTest(rFP.SavedId);
                        Application.DoEvents();
                        view.UpdateEmployeeAddMenuState();
                        bool subEnabledAtFolder = menuItems.Count >= 3 && menuItems[2].Enabled;
                        bool pHasParent = GridContainsId(view.GridForTest, rEP.SavedId);
                        bool pHasChild = GridContainsId(view.GridForTest, rEC.SavedId);
                        bool pHasSep = GridContainsId(view.GridForTest, rSep.SavedId);
                        bool pExcludesRoot = !GridContainsId(view.GridForTest, rER.SavedId);

                        // Chọn phân cách -> nút Chỉnh sửa bị mờ, nút Xóa vẫn bật.
                        bool selSepRow = view.SelectEmployeeRowByIdForTest(rSep.SavedId);
                        Application.DoEvents();
                        bool sepReadOnlyEdit = selSepRow && !view.EditButtonForTest.Enabled && view.RemoveButtonForTest.Enabled;

                        // Chọn thư mục con -> chỉ hiện NV con, không hiện NV cha, không hiện NV gốc.
                        bool selChildOk = view.SelectEmployeeFolderByIdForTest(rFC.SavedId);
                        Application.DoEvents();
                        bool cHasChild = GridContainsId(view.GridForTest, rEC.SavedId);
                        bool cExcludesParent = !GridContainsId(view.GridForTest, rEP.SavedId);
                        bool cExcludesRoot = !GridContainsId(view.GridForTest, rER.SavedId);

                        Check("Nhân sự cây §5 #2 (cây lọc thư mục cha gồm con, Tất cả hiện hết, phân cách là dòng riêng chỉ xóa được): đạt",
                            allHasRoot && allHasParent && allHasChild && allHasSep
                            && selParentOk && pHasParent && pHasChild && pHasSep && pExcludesRoot
                            && sepReadOnlyEdit
                            && selChildOk && cHasChild && cExcludesParent && cExcludesRoot,
                            SingleLine("all=" + allHasRoot + "/" + allHasParent + "/" + allHasChild + "/" + allHasSep
                                + ", par=" + pHasParent + "/" + pHasChild + "/" + pHasSep + "/" + pExcludesRoot
                                + ", sepRO=" + sepReadOnlyEdit + ", chi=" + cHasChild + "/" + cExcludesParent));

                        // UI xóa mềm qua nút Xóa -> chuyển NV gốc vào Thùng rác, sau đó mở Thùng rác và Khôi phục lại.
                        view.SelectEmployeeFolderByIdForTest(null);
                        Application.DoEvents();
                        bool selER = view.SelectEmployeeRowByIdForTest(rER.SavedId);
                        Application.DoEvents();
                        view.RemoveButtonForTest.PerformClick();
                        Application.DoEvents();
                        EmployeeRow afterUiSoft = repo.LoadById(rER.SavedId);

                        bool trashRestored = false;
                        bool trashNoScroll = false;
                        view.EmployeeTrashDialogOpenerForTest = tdlg =>
                        {
                            tdlg.SuppressPopup = true;
                            ShowDialogOffscreen(tdlg);
                            try
                            {
                                trashNoScroll = !tdlg.VerticalScroll.Visible && !tdlg.AutoScroll
                                    && tdlg.Width <= 1366 && tdlg.Height <= 728;
                                if (tdlg.SelectRowByIdForTest(rER.SavedId))
                                {
                                    tdlg.RestoreButtonForTest.PerformClick();
                                    Application.DoEvents();
                                    EmployeeRow restored = repo.LoadById(rER.SavedId);
                                    trashRestored = restored != null && restored.Status == 30;
                                }
                            }
                            finally { tdlg.Close(); }
                            return DialogResult.OK;
                        };
                        view.EmployeeTrashButtonForTest.PerformClick();
                        Application.DoEvents();

                        Check("Nhân sự cây §5 #4/#5 UI (nút Xóa chuyển Thùng rác & hộp Thùng rác Khôi phục): hoạt động qua sự kiện nút thật",
                            selER && afterUiSoft != null && afterUiSoft.Status == 0 && trashRestored && trashNoScroll,
                            SingleLine("soft=" + (afterUiSoft == null ? -1 : afterUiSoft.Status) + ", restored=" + trashRestored + ", noScr=" + trashNoScroll));

                        // §5 #7: Kiểm giao diện 1366x768 không cuộn dọc, hộp thư mục gọn, đổi theme thật không lỗi.
                        var themes = new List<ThemeTokens>(BuiltInThemes.All());
                        ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];
                        ThemeManager.Apply(tB);
                        view.ApplyTheme(tB, tB.Background);
                        Application.DoEvents();

                        bool noViewScroll = !view.VerticalScroll.Visible && !view.AutoScroll && view.TreeForTest.Visible;
                        bool trashBtnThemeOk = view.EmployeeTrashButtonForTest.Visible
                            && view.EmployeeTrashButtonForTest.FillColor == tB.Surface
                            && view.EmployeeTrashButtonForTest.TextColor == tB.Text;

                        bool folderDlgOk = false;
                        using (var fdlg = new EmployeeFolderEditDialog(adminSess, null, null, false))
                        {
                            fdlg.SuppressPopup = true;
                            ShowDialogOffscreen(fdlg);
                            try
                            {
                                folderDlgOk = !fdlg.VerticalScroll.Visible && !fdlg.AutoScroll
                                    && fdlg.Width <= 1366 && fdlg.Height <= 728
                                    && AllLabelsFit(fdlg.FieldLabelsForTest)
                                    && fdlg.BackColor == tB.Background
                                    && fdlg.NameBoxForTest.BackColor == tB.Surface;
                            }
                            finally { fdlg.Close(); }
                        }

                        // Kiểm quyền chỉ Xem: ẩn nút Thùng rác.
                        bool trashHiddenNoRemove = CheckTrashHiddenWithoutRemovePermission(db, empTableId);

                        Check("Nhân sự cây §5 #7 (giao diện 1366x768 không cuộn dọc, Thêm mới ▾ đủ 4 mục, Thùng rác theo quyền Xóa, đổi theme): đạt",
                            addBtnTextOk && fourItems && subDisabledAtRoot && subEnabledAtFolder
                            && noViewScroll && trashBtnThemeOk && folderDlgOk && trashHiddenNoRemove,
                            SingleLine("4=" + fourItems + ", subDis=" + subDisabledAtRoot + ", subEn=" + subEnabledAtFolder
                                + ", noScr=" + noViewScroll + ", thm=" + trashBtnThemeOk + ", fdlg=" + folderDlgOk + ", noRem=" + trashHiddenNoRemove));
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string eid in createdIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", eid);
                    SafeDeleteById(db, "DNHANVIEN", eid);
                }
            }
        }

        private static bool GridContainsId(DataGridView grid, string id)
        {
            if (grid == null || string.IsNullOrEmpty(id))
                return false;
            foreach (DataGridViewRow row in grid.Rows)
            {
                var drv = row.DataBoundItem as System.Data.DataRowView;
                if (drv != null && string.Equals(Convert.ToString(drv["_id"]), id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool CheckTrashHiddenWithoutRemovePermission(string db, string empTableId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var perms = new FunctionPermissions(false);
                perms.Add("", EmployeeText.FunctionName, RoleModes.View | RoleModes.Add | RoleModes.Edit);
                AppSession.Current = new UserSession
                {
                    UserId = "u_nvcay_norem", IsAdmin = false, DatabasePath = db, Permissions = perms
                };
                using (var host = MakeOffscreenHost())
                {
                    var v = new CatalogView(db, empTableId, EmployeeText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(v);
                    host.Show();
                    Application.DoEvents();
                    bool hidden = !v.EmployeeTrashButtonForTest.Visible && !v.EmployeeTrashBarForTest.Visible;
                    host.Close();
                    return hidden;
                }
            }
            finally { AppSession.Current = prev; }
        }
    }
}
