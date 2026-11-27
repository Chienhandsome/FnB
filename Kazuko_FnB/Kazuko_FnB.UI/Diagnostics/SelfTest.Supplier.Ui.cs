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
        private static void SupplierUiTests(string db)
        {
            var svc = new SupplierService(AppSession.Current);
            var grpSvc = new SupplierGroupService(AppSession.Current);
            var repo = new SupplierRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            string parentId = null;
            string childId = null;
            string supAId = null;
            string supBId = null;
            string supUnsetId = null;
            string refVoucherId = null;
            string dlgSavedId = null;
            string grpDlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-ncc-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                var parent = SupplierGroupRow.NewDefault();
                parent.Name = "Nhóm NCC Cha UI " + tag;
                parentId = grpSvc.Save(parent).SavedId;

                var child = SupplierGroupRow.NewDefault();
                child.Name = "Nhóm NCC Con UI " + tag;
                child.ParentId = parentId;
                childId = grpSvc.Save(child).SavedId;

                var sA = new SupplierRow
                {
                    Code = "NCCUIA" + tag,
                    Name = "Đặng Thị Ánh NCC " + tag,
                    GroupId = parentId,
                    Address = "10 Lê Lợi",
                    Phone = "0911000111",
                    Email = "anh@ncc.vn",
                    Website = "https://anh.vn",
                    Note = "Ghi chú A " + tag
                };
                supAId = svc.Save(sA).SavedId;

                var sB = new SupplierRow
                {
                    Code = "NCCUIB" + tag,
                    Name = "Trần Văn Bình NCC " + tag,
                    GroupId = childId,
                    Phone = "0922000222"
                };
                supBId = svc.Save(sB).SavedId;

                var sU = new SupplierRow
                {
                    Code = "NCCUIU" + tag,
                    Name = "NCC Chưa Gán Nhóm " + tag
                };
                supUnsetId = svc.Save(sU).SavedId;

                refVoucherId = DbHelper.NewId();
                InsertWarehouseRefRow(db, "TTHUCHI", "DNHACUNGCAPID", refVoucherId, supBId, "PC-UI-NCC-" + tag);

                string nccTableId = new CatalogRepository(db).FindIdByTable("DNHACUNGCAP");

                TestSupplierGridTreeSearchExportAndDeleteUi(
                    db, nccTableId, parent.Name, child.Name, supBId, tag, xlsPath);
                dlgSavedId = TestSupplierEditDialogUi(db, repo, parentId, tag);
                grpDlgSavedId = TestSupplierGroupEditDialogUi(db, tag);
                TestSupplierPermissionsMenuAndReadOnly(db, nccTableId, parent.Name, supAId);
                TestSupplierLiveThemeSwitch(db, nccTableId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                if (refVoucherId != null) SafeDeleteById(db, "TTHUCHI", refVoucherId);
                foreach (string sid in new[] { dlgSavedId, supAId, supBId, supUnsetId })
                {
                    if (sid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", sid);
                    SafeDeleteById(db, "DNHACUNGCAP", sid);
                }
                foreach (string gid in new[] { grpDlgSavedId, childId, parentId })
                {
                    if (gid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", gid);
                    SafeDeleteById(db, "DNHOMNHACUNGCAP", gid);
                }
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestSupplierGridTreeSearchExportAndDeleteUi(
            string db, string nccTableId, string parentName, string childName,
            string referencedSupId, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, nccTableId, "Danh mục nhà cung cấp")
                {
                    Dock = DockStyle.Fill,
                    SuppressSupplierPopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expectedHeaders =
                    {
                        "Mã nhà cung cấp", "Tên nhà cung cấp", "Nhóm nhà cung cấp",
                        "Địa chỉ", "Điện thoại", "Email", "Website", "Ghi chú"
                    };
                    bool colsOk = view.IsSupplierModeForTest && grid.Columns.Count == expectedHeaders.Length;
                    bool noClipHeader = true;
                    for (int i = 0; colsOk && i < expectedHeaders.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expectedHeaders[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > grid.Columns[i].Width)
                            noClipHeader = false;
                    }
                    Check("NCC §7 (AC5/AC8 lưới CatalogView 8 cột): đúng thứ tự cột, tiêu đề không cắt chữ",
                        colsOk && noClipHeader,
                        "cols=" + colsOk + ", clip=" + noClipHeader + ", count=" + grid.Columns.Count);

                    // AC5: Chọn nhóm trên cây lọc đúng (cha gồm cả con, con chỉ con, Chưa thiết lập, Tất cả hiện hết).
                    int allCount = view.VisibleRows;
                    bool selParent = view.SelectCategory(parentName);
                    Application.DoEvents();
                    int parentRows = view.VisibleRows;
                    bool selChild = view.SelectCategory(childName);
                    Application.DoEvents();
                    int childRows = view.VisibleRows;
                    bool selUnset = view.SelectCategory("Chưa thiết lập");
                    Application.DoEvents();
                    int unsetRows = view.VisibleRows;
                    bool selAll = view.SelectCategory("Tất cả");
                    Application.DoEvents();
                    int allAfterTree = view.VisibleRows;

                    Check("NCC §7 (AC5 lọc cây nhóm): chọn cha hiện cả cha+con, chọn con chỉ hiện con, Chưa thiết lập lọc đúng, Tất cả hiện hết",
                        selParent && parentRows == 2
                        && selChild && childRows == 1
                        && selUnset && unsetRows >= 1
                        && selAll && allAfterTree == allCount,
                        "parent=" + parentRows + ", child=" + childRows + ", unset=" + unsetRows + ", all=" + allAfterTree);

                    // Tìm không dấu, F3, Xuất Excel qua GridExcelExporter.
                    view.SearchBoxForTest.Text = "dang thi anh ncc " + tag;
                    Application.DoEvents();
                    int hitAccent = view.VisibleRows;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    view.SearchBoxForTest.Text = "";
                    Application.DoEvents();
                    int restoredCount = view.VisibleRows;

                    FocusControlReal(host, grid);
                    Message f3Msg = new Message();
                    bool f3Handled = InvokeProcessCmdKey(view, ref f3Msg, Keys.F3);
                    bool searchActive = view.SearchBoxForTest.Inner.Focused
                        || view.SearchBoxForTest.ContainsFocus
                        || host.ActiveControl == view.SearchBoxForTest;

                    Check("NCC §7 (AC5 tìm không dấu, F3 và Xuất Excel GridExcelExporter): lọc đúng dòng, F3 vào ô tìm, xuất .xls khớp dòng lọc",
                        hitAccent == 1 && restoredCount == allCount && f3Handled && searchActive
                        && snap != null && rec.PromptSaveCount == 1 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "NCCUIA" + tag),
                        "accent=" + hitAccent + ", f3=" + searchActive + ", xlsRows=" + (snap == null ? -1 : snap.RowCount));

                    // AC6 UI: bấm nút Xóa thật trên dòng đang bị TTHUCHI tham chiếu -> chặn, báo (1), giữ dòng.
                    bool selRef = view.SelectSupplierRowByIdForTest(referencedSupId);
                    Application.DoEvents();
                    view.RemoveButtonForTest.PerformClick();
                    Application.DoEvents();
                    string warnSup = view.LastSupplierMessageForTest;
                    bool supStillThere = new SupplierRepository(db).LoadById(referencedSupId) != null;

                    // AC6 UI: xóa nhóm còn thành viên qua mục "Xóa" trên menu cây -> chặn, báo (1), giữ nhóm.
                    view.SelectCategory(childName);
                    Application.DoEvents();
                    ToolStripMenuItem delTreeItem = null;
                    foreach (ToolStripItem item in view.SupplierTreeMenuForTest.Items)
                    {
                        var mi = item as ToolStripMenuItem;
                        if (mi != null && mi.Text == "Xóa")
                        {
                            delTreeItem = mi;
                            break;
                        }
                    }
                    if (delTreeItem != null)
                        delTreeItem.PerformClick();
                    Application.DoEvents();
                    string warnGrp = view.LastSupplierMessageForTest;

                    Check("NCC §7/Q1 (AC6 UI nút Xóa chặn NCC có tham chiếu và nhóm còn NCC): hiện cảnh báo có số tham chiếu và giữ dữ liệu",
                        selRef && supStillThere
                        && !string.IsNullOrEmpty(warnSup) && warnSup.IndexOf("nhà cung cấp", StringComparison.Ordinal) >= 0 && warnSup.IndexOf("(1)", StringComparison.Ordinal) >= 0
                        && !string.IsNullOrEmpty(warnGrp) && warnGrp.IndexOf("nhóm nhà cung cấp", StringComparison.Ordinal) >= 0 && warnGrp.IndexOf("(1)", StringComparison.Ordinal) >= 0,
                        SingleLine((warnSup ?? "") + " | " + (warnGrp ?? "")));
                }
                finally { host.Close(); }
            }
        }

        private static string TestSupplierEditDialogUi(
            string db, SupplierRepository repo, string presetGroupId, string tag)
        {
            string savedId = null;
            using (var dlg = new SupplierEditDialog(AppSession.Current, null, presetGroupId, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool autoCodeOk = dlg.CodeBoxForTest.Text.StartsWith("NCC", StringComparison.Ordinal);
                    bool groupPresetOk = dlg.GroupComboForTest.SelectedId == presetGroupId;
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 8;
                    foreach (Label lbl in dlg.FieldLabelsForTest)
                    {
                        if (TextRenderer.MeasureText(lbl.Text, lbl.Font).Width > lbl.Width)
                            labelsOk = false;
                    }
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll
                        && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f
                        && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                    dlg.NameBoxForTest.Text = "NCC Dialog Mới " + tag;
                    dlg.CodeBoxForTest.Text = "NCCDLG" + tag;
                    dlg.AddressBoxForTest.Text = "88 Hai Bà Trưng";
                    dlg.PhoneBoxForTest.Text = "0988111222";
                    dlg.EmailBoxForTest.Text = "dlg@ncc.vn";
                    dlg.WebsiteBoxForTest.Text = "https://dlg.ncc.vn";
                    dlg.NoteBoxForTest.Text = "Tạo qua nút Lưu thật";
                    Application.DoEvents();

                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    SupplierRow back = savedId == null ? null : repo.LoadById(savedId);
                    Check("NCC §7 (AC4/AC8 SupplierEditDialog thêm mới qua nút Lưu): mã tự sinh, chọn sẵn nhóm, không cắt nhãn/cuộn dọc 1366×768 @100-150% DPI, lưu đủ 8 trường",
                        autoCodeOk && groupPresetOk && labelsOk && noScroll && fits150
                        && back != null && back.Status == 30
                        && back.Code == "NCCDLG" + tag && back.Name == "NCC Dialog Mới " + tag
                        && back.GroupId == presetGroupId && back.Address == "88 Hai Bà Trưng"
                        && back.Phone == "0988111222" && back.Email == "dlg@ncc.vn"
                        && back.Website == "https://dlg.ncc.vn" && back.Note == "Tạo qua nút Lưu thật",
                        SingleLine(dlg.LastErrorForTest ?? ("code=" + autoCodeOk + ", lbl=" + labelsOk + ", fits150=" + fits150)));
                }
                finally { dlg.Close(); }
            }
            return savedId;
        }

        private static string TestSupplierGroupEditDialogUi(string db, string tag)
        {
            string savedId = null;
            using (var dlg = new SupplierGroupEditDialog(AppSession.Current, null, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 3;
                    foreach (Label lbl in dlg.FieldLabelsForTest)
                    {
                        if (TextRenderer.MeasureText(lbl.Text, lbl.Font).Width > lbl.Width)
                            labelsOk = false;
                    }
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll
                        && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f
                        && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                    dlg.NameBoxForTest.Text = "Nhóm Dialog Mới " + tag;
                    dlg.NoteBoxForTest.Text = "Ghi chú nhóm dialog";
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    SupplierGroupRow back = savedId == null ? null : new SupplierGroupRepository(db).LoadById(savedId);
                    Check("NCC §7 (AC8 SupplierGroupEditDialog thêm mới qua nút Lưu): không cắt nhãn, không cuộn dọc 1366×768 @100-150% DPI, lưu nhóm đúng",
                        labelsOk && noScroll && fits150
                        && back != null && back.Status == 30
                        && back.Name == "Nhóm Dialog Mới " + tag && back.Note == "Ghi chú nhóm dialog",
                        SingleLine(dlg.LastErrorForTest ?? ("lbl=" + labelsOk + ", fits150=" + fits150)));
                }
                finally { dlg.Close(); }
            }
            return savedId;
        }

        private static void TestSupplierPermissionsMenuAndReadOnly(
            string db, string nccTableId, string parentName, string sampleSupplierId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                // Mức 80 (Thủ kho): Xem + Thêm, KHÔNG Sửa, KHÔNG Xóa -> hiện nút Thêm & thêm nhóm, ẩn Sửa/Xóa; nhấp đôi mở chỉ đọc.
                var perms80 = new FunctionPermissions(false);
                perms80.Add(SupplierText.FunctionId, SupplierText.FunctionName, RoleModes.View | RoleModes.Add);
                AppSession.Current = new UserSession { UserId = "u_80", IsAdmin = false, DatabasePath = db, Permissions = perms80 };
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, nccTableId, "Danh mục nhà cung cấp") { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool btnState80 = view.AddButtonForTest.Visible
                        && !view.EditButtonForTest.Visible
                        && !view.RemoveButtonForTest.Visible
                        && view.GroupAddButtonForTest.Visible
                        && !view.GroupEditButtonForTest.Visible;

                    bool supRo = false, supLocked = false, supNoSave = false;
                    view.SupplierDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            supRo = dlg.IsReadOnlyForTest;
                            supLocked = dlg.NameBoxForTest.ReadOnly && dlg.CodeBoxForTest.ReadOnly
                                && !dlg.GroupComboForTest.Enabled && dlg.AddressBoxForTest.ReadOnly
                                && dlg.PhoneBoxForTest.ReadOnly && dlg.EmailBoxForTest.ReadOnly
                                && dlg.WebsiteBoxForTest.ReadOnly && dlg.NoteBoxForTest.ReadOnly;
                            supNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectSupplierRowByIdForTest(sampleSupplierId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    bool grpRo = false, grpLocked = false, grpNoSave = false;
                    view.SupplierGroupDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            grpRo = dlg.IsReadOnlyForTest;
                            grpLocked = dlg.NameBoxForTest.ReadOnly && !dlg.ImageComboForTest.Enabled && dlg.NoteBoxForTest.ReadOnly;
                            grpNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    view.SelectCategory(parentName);
                    Application.DoEvents();
                    if (view.TreeForTest.SelectedNode != null)
                        InvokeTreeNodeDoubleClick(view.TreeForTest, view.TreeForTest.SelectedNode);

                    Check("NCC §7 (AC7 quyền mức 80 Thủ kho trên UI): hiện Thêm, ẩn Sửa/Xóa; nhấp đôi mở hộp thoại chỉ đọc cả NCC và nhóm",
                        btnState80 && supRo && supLocked && supNoSave && grpRo && grpLocked && grpNoSave,
                        "btns=" + btnState80 + ", supRo=" + supRo + ", grpRo=" + grpRo);
                    host.Close();
                }

                // Mức 16 (Chỉ xem): ẩn toàn bộ nút CRUD; Mức 0 (Phục vụ): ẩn menu; Admin: thấy menu KHO HÀNG > Danh mục nhà cung cấp và mở màn thật.
                var perms16 = new FunctionPermissions(false);
                perms16.Add(SupplierText.FunctionId, SupplierText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_16", IsAdmin = false, DatabasePath = db, Permissions = perms16 };
                bool hidden16;
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, nccTableId, "Danh mục nhà cung cấp") { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    hidden16 = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible
                        && !view.RemoveButtonForTest.Visible && !view.GroupBarForTest.Visible;
                    host.Close();
                }

                var sess0 = new UserSession { UserId = "u_0", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(sess0).Roots, "Danh mục nhà cung cấp");

                var adminSess = new UserSession { UserId = "u_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
                AppSession.Current = adminSess;
                MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, "Danh mục nhà cung cấp");
                bool openedSupplierMode = false;
                if (adminNode != null)
                {
                    using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                    {
                        var cv = created as CatalogView;
                        openedSupplierMode = cv != null && cv.IsSupplierModeForTest;
                    }
                }
                Check("NCC §7 (AC7 quyền mức 16 Chỉ xem, mức 0 ẩn menu và nối menu KHO HÀNG > Danh mục nhà cung cấp): mức 16 ẩn nút CRUD, mức 0 ẩn menu, Admin mở CatalogView chế độ NCC",
                    hidden16 && deniedNode == null && adminNode != null && openedSupplierMode,
                    "hidden16=" + hidden16 + ", deniedNull=" + (deniedNode == null) + ", mode=" + openedSupplierMode);
            }
            finally
            {
                AppSession.Current = prev;
            }
        }

        private static void TestSupplierLiveThemeSwitch(string db, string nccTableId)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new SupplierEditDialog(AppSession.Current, null, null, false))
            using (var grpDlg = new SupplierGroupEditDialog(AppSession.Current, null, null, false))
            {
                var view = new CatalogView(db, nccTableId, "Danh mục nhà cung cấp") { Dock = DockStyle.Fill };
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
                    bool gridColorOk = g.BackgroundColor == tB.Surface
                        && g.DefaultCellStyle.SelectionBackColor == tB.Primary
                        && cellColorOk;
                    bool treeColorOk = view.TreeForTest.BackColor == tB.Surface
                        && view.TreeForTest.ForeColor == tB.Text;
                    bool dlgColorOk = dlg.BackColor == tB.Background
                        && dlg.ErrorLabelForTest.ForeColor == tB.Danger
                        && dlg.GroupComboForTest.BackColor == tB.Surface;
                    bool grpDlgColorOk = grpDlg.BackColor == tB.Background
                        && grpDlg.ErrorLabelForTest.ForeColor == tB.Danger
                        && grpDlg.ImageComboForTest.BackColor == tB.Surface;

                    Check("NCC §7 (AC8 đổi bảng màu trực tiếp): lưới, ô lưới thực tế, cây nhóm và cả 2 hộp thoại đổi đúng màu ThemeTokens",
                        gridColorOk && treeColorOk && dlgColorOk && grpDlgColorOk,
                        "grid=" + gridColorOk + ", cell=" + cellColorOk + ", tree=" + treeColorOk + ", dlg=" + dlgColorOk + ", grpDlg=" + grpDlgColorOk);
                }
                finally
                {
                    grpDlg.Close();
                    dlg.Close();
                    host.Close();
                }
            }
        }
    }
}
