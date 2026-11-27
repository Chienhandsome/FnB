using System;
using System.Collections.Generic;
using System.IO;
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
        private static void WifiPasswordUiTests(string db)
        {
            var svc = new WifiPasswordService(AppSession.Current);
            var grpSvc = new WifiGroupService(AppSession.Current);
            var repo = new WifiPasswordRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);

            string parentId = null, childId = null;
            string wParentId = null, wChildId = null, wUnsetId = null;
            string dlgSavedId = null, grpDlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-wifi-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                var parent = WifiGroupRow.NewDefault();
                parent.Name = "Nhóm Wifi Cha UI " + tag;
                parentId = grpSvc.Save(parent).SavedId;

                var child = WifiGroupRow.NewDefault();
                child.Name = "Nhóm Wifi Con UI " + tag;
                child.ParentId = parentId;
                childId = grpSvc.Save(child).SavedId;

                wParentId = svc.Save(new WifiPasswordRow { Name = "WFUIP-" + tag, GroupId = parentId, Note = "Mật khẩu tầng trệt đặc biệt " + tag }).SavedId;
                wChildId = svc.Save(new WifiPasswordRow { Name = "WFUIC-" + tag, GroupId = childId, Note = "Mật khẩu phòng VIP " + tag }).SavedId;
                wUnsetId = svc.Save(new WifiPasswordRow { Name = "WFUIU-" + tag, GroupId = null, Note = "Chưa phân nhóm " + tag }).SavedId;

                string wifiTableId = new CatalogRepository(db).FindIdByTable(WifiPasswordText.TableName);

                TestWifiGridTreeSearchExportAndDeleteUi(db, wifiTableId, parent.Name, child.Name, tag, xlsPath);
                dlgSavedId = TestWifiEditDialogUi(db, repo, parentId, tag);
                grpDlgSavedId = TestWifiGroupEditDialogUi(db, tag);
                TestWifiPermissionsMenuAndReadOnly(db, wifiTableId, parent.Name, wParentId);
                TestWifiLiveThemeSwitch(db, wifiTableId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string wid in new[] { dlgSavedId, wParentId, wChildId, wUnsetId })
                {
                    if (wid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", wid);
                    SafeDeleteById(db, "DMATKHAUWIFI", wid);
                }
                foreach (string gid in new[] { grpDlgSavedId, childId, parentId })
                {
                    if (gid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", gid);
                    SafeDeleteById(db, "DNHOMWIFI", gid);
                }
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestWifiGridTreeSearchExportAndDeleteUi(
            string db, string wifiTableId, string parentName, string childName, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, wifiTableId, WifiPasswordText.FunctionName)
                {
                    Dock = DockStyle.Fill,
                    SuppressWifiPopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expectedHeaders = { "Mật khẩu wifi", "Nhóm wifi", "Ghi chú" };
                    bool colsOk = view.IsWifiPasswordModeForTest && grid.Columns.Count == expectedHeaders.Length;
                    bool noClipHeader = true;
                    for (int i = 0; colsOk && i < expectedHeaders.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expectedHeaders[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        if (TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width > grid.Columns[i].Width)
                            noClipHeader = false;
                    }

                    // Chọn nhóm cha -> hiện cả mật khẩu cha và con (2 dòng), hiển thị rõ mật khẩu.
                    int allCount = view.VisibleRows;
                    bool selParent = view.SelectCategory(parentName);
                    Application.DoEvents();
                    int parentRows = view.VisibleRows;
                    bool plainVisible = parentRows == 2
                        && (Convert.ToString(grid.Rows[0].Cells["wp_name"].Value) == "WFUIC-" + tag
                            || Convert.ToString(grid.Rows[0].Cells["wp_name"].Value) == "WFUIP-" + tag);

                    bool selChild = view.SelectCategory(childName);
                    Application.DoEvents();
                    int childRows = view.VisibleRows;

                    bool selUnset = view.SelectCategory("Chưa thiết lập");
                    Application.DoEvents();
                    int unsetRows = view.VisibleRows;

                    bool selAll = view.SelectCategory("Tất cả");
                    Application.DoEvents();
                    int allAfter = view.VisibleRows;

                    // Tìm không dấu & xuất Excel 3 cột.
                    view.SearchBoxForTest.Text = "tang tret dac biet " + tag;
                    Application.DoEvents();
                    int hitAccent = view.VisibleRows;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    view.SearchBoxForTest.Text = "";
                    Application.DoEvents();

                    FocusControlReal(host, grid);
                    Message f3Msg = new Message();
                    bool f3Handled = InvokeProcessCmdKey(view, ref f3Msg, Keys.F3);
                    bool searchActive = view.SearchBoxForTest.Inner.Focused || view.SearchBoxForTest.ContainsFocus || host.ActiveControl == view.SearchBoxForTest;

                    // Chặn xóa nhóm còn mật khẩu trên menu chuột phải cây nhóm.
                    view.SelectCategory(childName);
                    Application.DoEvents();
                    foreach (ToolStripItem item in view.WifiTreeMenuForTest.Items)
                    {
                        var mi = item as ToolStripMenuItem;
                        if (mi != null && mi.Text == "Xóa") { mi.PerformClick(); break; }
                    }
                    Application.DoEvents();
                    string warnGrp = view.LastWifiMessageForTest;

                    Check("Mật khẩu wifi §5 #4 (lưới 3 cột, cây nhóm cha gồm con, mật khẩu rõ, tìm không dấu, F3, xuất Excel, chặn xóa nhóm UI): đạt",
                        colsOk && noClipHeader && selParent && parentRows == 2 && plainVisible
                        && selChild && childRows == 1 && selUnset && unsetRows >= 1 && selAll && allAfter == allCount
                        && hitAccent == 1 && f3Handled && searchActive
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 3 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "WFUIP-" + tag)
                        && !string.IsNullOrEmpty(warnGrp) && warnGrp.IndexOf("nhóm còn 1 mật khẩu", StringComparison.Ordinal) >= 0,
                        "cols=" + colsOk + ", p=" + parentRows + ", c=" + childRows + ", hit=" + hitAccent
                        + ", xls=" + (snap == null ? -1 : snap.RowCount) + "x" + (snap == null ? -1 : snap.ColCount)
                        + ", warn=" + SingleLine(warnGrp));
                }
                finally { host.Close(); }
            }
        }

        private static string TestWifiEditDialogUi(string db, WifiPasswordRepository repo, string presetGroupId, string tag)
        {
            string savedId = null;
            using (var dlg = new WifiPasswordEditDialog(AppSession.Current, null, presetGroupId, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool groupPresetOk = dlg.GroupComboForTest.SelectedId == presetGroupId;
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 3 && AllLabelsFit(dlg.FieldLabelsForTest);
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;
                    bool plainBox = dlg.NameBoxForTest.PasswordChar == '\0' && !dlg.NameBoxForTest.UseSystemPasswordChar;

                    dlg.NameBoxForTest.Text = "   ";
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool blockedEmpty = dlg.LastErrorForTest == WifiPasswordText.NameEmpty;

                    dlg.NameBoxForTest.Text = "WFDLG-" + tag;
                    dlg.NoteBoxForTest.Text = "Tạo qua dialog";
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    WifiPasswordRow back = savedId == null ? null : repo.LoadById(savedId);
                    Check("Mật khẩu wifi §5 #4/#6 (WifiPasswordEditDialog thêm mới): chọn sẵn nhóm, mật khẩu rõ, không cắt nhãn/cuộn dọc 1366×768 @150% DPI, chặn trống, lưu đúng",
                        groupPresetOk && labelsOk && noScroll && fits150 && plainBox && blockedEmpty
                        && back != null && back.Status == 30 && back.Name == "WFDLG-" + tag
                        && back.GroupId == presetGroupId && back.Note == "Tạo qua dialog",
                        "preset=" + groupPresetOk + ", lbl=" + labelsOk + ", fits150=" + fits150 + ", plain=" + plainBox + ", blk=" + blockedEmpty);
                }
                finally { dlg.Close(); }
            }
            return savedId;
        }

        private static string TestWifiGroupEditDialogUi(string db, string tag)
        {
            string savedId = null;
            using (var dlg = new WifiGroupEditDialog(AppSession.Current, null, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 2 && AllLabelsFit(dlg.FieldLabelsForTest);
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                    dlg.NameBoxForTest.Text = "Nhóm WF Dialog " + tag;
                    dlg.NoteBoxForTest.Text = "Ghi chú nhóm WF";
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    WifiGroupRow back = savedId == null ? null : new WifiGroupRepository(db).LoadById(savedId);
                    Check("Mật khẩu wifi §5 #4/#6 (WifiGroupEditDialog thêm mới): không cắt nhãn/cuộn dọc 1366×768 @150% DPI, lưu nhóm đúng",
                        labelsOk && noScroll && fits150 && back != null && back.Status == 30
                        && back.Name == "Nhóm WF Dialog " + tag && back.Note == "Ghi chú nhóm WF",
                        "lbl=" + labelsOk + ", noScr=" + noScroll + ", fits150=" + fits150 + ", ok=" + (back != null));
                }
                finally { dlg.Close(); }
            }
            return savedId;
        }

        private static void TestWifiPermissionsMenuAndReadOnly(
            string db, string wifiTableId, string parentName, string sampleWifiId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add("", WifiPasswordText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_wf_ro", IsAdmin = false, DatabasePath = db, Permissions = permsView };
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, wifiTableId, WifiPasswordText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible
                        && !view.RemoveButtonForTest.Visible && !view.GroupBarForTest.Visible;

                    bool wRo = false, wLocked = false, wNoSave = false, wSaveBlk = false;
                    view.WifiDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            wRo = dlg.IsReadOnlyForTest;
                            wLocked = dlg.NameBoxForTest.ReadOnly && !dlg.GroupComboForTest.Enabled && dlg.NoteBoxForTest.ReadOnly;
                            wNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                            wSaveBlk = !dlg.DoSave();
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectWifiPasswordRowByIdForTest(sampleWifiId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    bool gRo = false, gLocked = false, gNoSave = false;
                    view.WifiGroupDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            gRo = dlg.IsReadOnlyForTest;
                            gLocked = dlg.NameBoxForTest.ReadOnly && dlg.NoteBoxForTest.ReadOnly;
                            gNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    view.SelectCategory(parentName);
                    Application.DoEvents();
                    if (view.TreeForTest.SelectedNode != null)
                        InvokeTreeNodeDoubleClick(view.TreeForTest, view.TreeForTest.SelectedNode);

                    Check("Mật khẩu wifi §5 #4 (quyền chỉ Xem): ẩn CRUD và thanh nhóm, nhấp đôi mở hộp chỉ đọc cho cả mật khẩu lẫn nhóm",
                        hiddenCrud && wRo && wLocked && wNoSave && wSaveBlk && gRo && gLocked && gNoSave,
                        "hid=" + hiddenCrud + ", wRo=" + wRo + ", wBlk=" + wSaveBlk + ", gRo=" + gRo);
                    host.Close();
                }

                var deniedSess = new UserSession { UserId = "u_wf_0", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, WifiPasswordText.FunctionName);

                var viewSess = new UserSession { UserId = "u_wf_vw", IsAdmin = false, DatabasePath = db, Permissions = permsView };
                MenuNode viewNode = FindMenuLeafByTitle(MainMenuBuilder.Build(viewSess).Roots, WifiPasswordText.FunctionName);

                var adminSess = new UserSession { UserId = "u_wf_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
                AppSession.Current = adminSess;
                MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, WifiPasswordText.FunctionName);
                bool openedWifiMode = false;
                if (adminNode != null)
                {
                    using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                    {
                        var cv = created as CatalogView;
                        openedWifiMode = cv != null && cv.IsWifiPasswordModeForTest;
                    }
                }
                bool routeNoAccent = ScreenFactory.IsWifiPasswordMenu(new MenuNode { Title = "danh muc mat khau wifi" }, adminSess)
                    && ScreenFactory.IsWifiPasswordMenu(new MenuNode { Title = "DANH MỤC MẬT KHẨU WIFI" }, adminSess)
                    && !ScreenFactory.IsWifiPasswordMenu(new MenuNode { Title = "Thông báo sửa chữa" }, adminSess);

                Check("Mật khẩu wifi §5 #4 (menu & ScreenFactory): không quyền bị ẩn, có quyền và Admin thấy và mở đúng CatalogView chế độ wifi, nhận diện không dấu",
                    deniedNode == null && viewNode != null && adminNode != null && openedWifiMode && routeNoAccent,
                    "den=" + (deniedNode == null) + ", vw=" + (viewNode != null) + ", adm=" + (adminNode != null) + ", mode=" + openedWifiMode + ", route=" + routeNoAccent);
            }
            finally { AppSession.Current = prev; }
        }

        private static void TestWifiLiveThemeSwitch(string db, string wifiTableId)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new WifiPasswordEditDialog(AppSession.Current, null, null, false))
            using (var grpDlg = new WifiGroupEditDialog(AppSession.Current, null, null, false))
            {
                var view = new CatalogView(db, wifiTableId, WifiPasswordText.FunctionName) { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                dlg.SuppressPopup = true;
                grpDlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                ShowDialogOffscreen(grpDlg);
                try
                {
                    ThemeManager.Apply(tB);
                    view.ApplyTheme(tB, tB.Background);
                    Application.DoEvents();

                    DataGridView g = view.GridForTest;
                    bool cellColorOk = g.Rows.Count == 0
                        || (g.Rows[0].Cells[0].InheritedStyle.BackColor == tB.Surface
                            && g.Rows[0].Cells[0].InheritedStyle.SelectionBackColor == tB.Primary);
                    bool gridColorOk = g.BackgroundColor == tB.Surface && g.DefaultCellStyle.SelectionBackColor == tB.Primary && cellColorOk;
                    bool treeColorOk = view.TreeForTest.BackColor == tB.Surface && view.TreeForTest.ForeColor == tB.Text;
                    bool dlgColorOk = dlg.BackColor == tB.Background && dlg.ErrorLabelForTest.ForeColor == tB.Danger && dlg.GroupComboForTest.BackColor == tB.Surface;
                    bool grpDlgColorOk = grpDlg.BackColor == tB.Background && grpDlg.ErrorLabelForTest.ForeColor == tB.Danger && grpDlg.NameBoxForTest.BackColor == tB.Surface;

                    Check("Mật khẩu wifi §6 (đổi bảng màu trực tiếp): lưới, ô lưới thực tế, cây nhóm và cả 2 hộp thoại đổi đúng màu ThemeTokens",
                        gridColorOk && treeColorOk && dlgColorOk && grpDlgColorOk,
                        "grid=" + gridColorOk + ", tree=" + treeColorOk + ", dlg=" + dlgColorOk + ", grpDlg=" + grpDlgColorOk);
                }
                finally { grpDlg.Close(); dlg.Close(); host.Close(); }
            }
        }
    }
}
