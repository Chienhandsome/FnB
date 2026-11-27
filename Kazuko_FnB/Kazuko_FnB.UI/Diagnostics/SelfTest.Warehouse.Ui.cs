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
        private static void WarehouseUiTests(string db)
        {
            var repo = new WarehouseRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string areaRefId = DbHelper.NewId();
            string wNeg30Id = DbHelper.NewId();
            string wNeg0Id = DbHelper.NewId();
            string dlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-kho-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kho')",
                    "@I", storeId, "@N", "Cửa Hàng UI " + tag);
                BookingExec(db,
                    "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @S, 30, @NT, 30, 'selftest_kho', @T)",
                    "@I", wNeg30Id, "@N", "Kho Đặng Ánh " + tag, "@S", storeId, "@NT", "Ghi chú 30 " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @S, 0, @NT, 30, 'selftest_kho', @T)",
                    "@I", wNeg0Id, "@N", "Kho Bình Minh " + tag, "@S", storeId, "@NT", "Ghi chú 0 " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, STATUS, USERCREATEDID) VALUES (@I, @N, @W, 30, 'selftest_kho')",
                    "@I", areaRefId, "@N", "KV Ref UI " + tag, "@W", wNeg0Id);

                string khoTableId = new CatalogRepository(db).FindIdByTable("DKHOHANG");

                TestWarehouseGridSearchExportAndDeleteBlock(db, khoTableId, wNeg30Id, wNeg0Id, tag, xlsPath);
                dlgSavedId = TestWarehouseEditDialogUi(db, repo, storeId, wNeg30Id, tag);
                TestWarehousePermissionsMenuAndReadOnly(db, khoTableId, wNeg30Id);
                TestWarehouseLiveThemeSwitch(db, khoTableId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                SafeDeleteById(db, "DKHUVUC", areaRefId);
                if (dlgSavedId != null)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", dlgSavedId);
                    SafeDeleteById(db, "DKHOHANG", dlgSavedId);
                }
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", wNeg30Id);
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", wNeg0Id);
                SafeDeleteById(db, "DKHOHANG", wNeg30Id);
                SafeDeleteById(db, "DKHOHANG", wNeg0Id);
                SafeDeleteById(db, "DCUAHANG", storeId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestWarehouseGridSearchExportAndDeleteBlock(
            string db, string khoTableId, string wNeg30Id, string wNeg0Id, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, khoTableId, "Danh mục kho hàng")
                {
                    Dock = DockStyle.Fill,
                    SuppressWarehousePopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expected = { "Tên kho hàng", "Cửa hàng", "Cho phép âm kho", "Ghi chú" };
                    bool colsOk = view.IsWarehouseModeForTest && grid.Columns.Count == expected.Length;
                    bool noClipHeader = true;
                    for (int i = 0; colsOk && i < expected.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expected[i])
                            colsOk = false;
                        int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > grid.Columns[i].Width)
                            noClipHeader = false;
                    }
                    bool chkColOk = grid.Columns.Count >= 3
                        && grid.Columns[2] is DataGridViewCheckBoxColumn
                        && grid.Columns[2].ReadOnly;

                    bool val30True = false, val0False = false;
                    if (view.SelectWarehouseRowByIdForTest(wNeg30Id) && grid.CurrentRow != null)
                        val30True = Equals(grid.CurrentRow.Cells["w_allowneg"].Value, true);
                    if (view.SelectWarehouseRowByIdForTest(wNeg0Id) && grid.CurrentRow != null)
                        val0False = Equals(grid.CurrentRow.Cells["w_allowneg"].Value, false);

                    Check("Kho §7 (AC3/AC6 lưới CatalogView): đúng 4 cột, cột Cho phép âm kho là CheckBox chỉ đọc hiện đúng 30=true/0=false, tiêu đề không cắt chữ",
                        colsOk && chkColOk && noClipHeader && val30True && val0False,
                        "cols=" + colsOk + ", chk=" + chkColOk + ", clip=" + noClipHeader + ", v30=" + val30True + ", v0=" + val0False);

                    int allCount = view.VisibleRows;
                    view.SearchBoxForTest.Text = "dang anh " + tag;
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

                    Check("Kho §7 (AC6 tìm không dấu, F3 và Xuất Excel GridExcelExporter): lọc đúng dòng, F3 vào ô tìm, xuất .xls khớp dòng lọc",
                        hitAccent == 1 && restoredCount == allCount && f3Handled && searchActive
                        && snap != null && rec.PromptSaveCount == 1 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "Kho Đặng Ánh " + tag),
                        "accent=" + hitAccent + ", f3=" + searchActive + ", xlsRows=" + (snap == null ? -1 : snap.RowCount));

                    // Xóa qua nút Xóa thật trên dòng đang bị DKHUVUC tham chiếu -> chặn và hiện thông báo số tham chiếu.
                    bool selRef = view.SelectWarehouseRowByIdForTest(wNeg0Id);
                    Application.DoEvents();
                    view.RemoveButtonForTest.PerformClick();
                    Application.DoEvents();
                    string warn = view.LastWarehouseMessageForTest;
                    bool stillThere = new WarehouseRepository(db).LoadById(wNeg0Id) != null;
                    Check("Kho §7/Q1 (AC4 UI nút Xóa chặn kho có tham chiếu): hiện cảnh báo có số tham chiếu và giữ nguyên dòng",
                        selRef && stillThere && !string.IsNullOrEmpty(warn) && warn.IndexOf("(1)", StringComparison.Ordinal) >= 0,
                        SingleLine(warn));
                }
                finally { host.Close(); }
            }
        }

        private static string TestWarehouseEditDialogUi(
            string db, WarehouseRepository repo, string storeId, string existingNeg30Id, string tag)
        {
            string savedId = null;
            var sess = new UserSession
            {
                UserId = "selftest_kho",
                UserName = "selftest_kho",
                DisplayName = "Selftest Kho",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true),
                CurrentStore = new StoreRecord { Id = storeId, Name = "Cửa Hàng UI " + tag }
            };

            // 1. Mở hộp thoại thêm mới: không có CheckBox âm kho (Q2), chọn sẵn cửa hàng, không cắt chữ nhãn, không cuộn dọc 1366×768.
            using (var dlg = new WarehouseEditDialog(sess, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool hasNoCheckBox = !HasControlOfType<CheckBox>(dlg);
                    bool storePreset = dlg.StoreComboForTest.SelectedId == storeId;
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 4;
                    foreach (Label lbl in dlg.FieldLabelsForTest)
                    {
                        if (TextRenderer.MeasureText(lbl.Text, lbl.Font).Width > lbl.Width)
                            labelsOk = false;
                    }
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                    dlg.NameBoxForTest.Text = "Kho Dialog Mới " + tag;
                    dlg.NoteBoxForTest.Text = "Tạo qua nút Lưu";
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    WarehouseRow back = savedId == null ? null : repo.LoadById(savedId);
                    Check("Kho §7/Q2 (AC1/AC3/AC6 WarehouseEditDialog thêm mới): không có ô âm kho, Cửa hàng chọn sẵn, không cắt nhãn/cuộn dọc 1366×768, bấm Lưu ghi CHOPHEPAMKHO=0",
                        hasNoCheckBox && storePreset && labelsOk && noScroll && fits150
                        && back != null && back.Status == 30 && back.StoreId == storeId && back.AllowNegativeStock == 0,
                        SingleLine(dlg.LastErrorForTest ?? ("noChk=" + hasNoCheckBox + ", preset=" + storePreset + ", lbl=" + labelsOk + ", fits150=" + fits150)));
                }
                finally { dlg.Close(); }
            }

            // 2. Mở hộp thoại sửa kho có CHOPHEPAMKHO = 30 -> bấm Lưu thật -> giữ nguyên 30.
            using (var dlgEdit = new WarehouseEditDialog(sess, existingNeg30Id, false))
            {
                dlgEdit.SuppressPopup = true;
                ShowDialogOffscreen(dlgEdit);
                try
                {
                    dlgEdit.NoteBoxForTest.Text = "Đã sửa qua dialog " + tag;
                    Application.DoEvents();
                    dlgEdit.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();

                    WarehouseRow back30 = repo.LoadById(existingNeg30Id);
                    Check("Kho §7/Q2 (AC3 WarehouseEditDialog sửa kho cũ): bấm Lưu giữ nguyên CHOPHEPAMKHO=30 và cập nhật ghi chú",
                        back30 != null && back30.AllowNegativeStock == 30 && back30.Note == "Đã sửa qua dialog " + tag,
                        SingleLine(dlgEdit.LastErrorForTest ?? ("neg=" + (back30 == null ? -1 : back30.AllowNegativeStock))));
                }
                finally { dlgEdit.Close(); }
            }

            return savedId;
        }

        private static void TestWarehousePermissionsMenuAndReadOnly(string db, string khoTableId, string sampleWarehouseId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                // Mức 112 (Thủ kho): Thêm/Sửa hiện, Xóa ẩn.
                var perms112 = new FunctionPermissions(false);
                perms112.Add(WarehouseText.FunctionId, WarehouseText.FunctionName, RoleModes.View | RoleModes.Edit | RoleModes.Add);
                AppSession.Current = new UserSession { UserId = "u_112", IsAdmin = false, DatabasePath = db, Permissions = perms112 };
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, khoTableId, "Danh mục kho hàng") { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    Check("Kho §7 (AC5 quyền mức 112 Thủ kho trên UI): hiện nút Thêm và Sửa, ẩn nút Xóa",
                        view.AddButtonForTest.Visible && view.EditButtonForTest.Visible && !view.RemoveButtonForTest.Visible,
                        "add=" + view.AddButtonForTest.Visible + ", edit=" + view.EditButtonForTest.Visible + ", del=" + view.RemoveButtonForTest.Visible);
                    host.Close();
                }

                // Mức 16 (Chỉ Xem): ẩn Thêm/Sửa/Xóa; nhấp đôi mở hộp thoại chỉ đọc (Q4).
                var permsView = new FunctionPermissions(false);
                permsView.Add(WarehouseText.FunctionId, WarehouseText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_view", IsAdmin = false, DatabasePath = db, Permissions = permsView };
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, khoTableId, "Danh mục kho hàng") { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible && !view.RemoveButtonForTest.Visible;
                    bool roOpened = false, roLocked = false, roNoSave = false;
                    view.WarehouseDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = dlg.NameBoxForTest.ReadOnly && !dlg.ImageComboForTest.Enabled
                                && !dlg.StoreComboForTest.Enabled && dlg.NoteBoxForTest.ReadOnly;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectWarehouseRowByIdForTest(sampleWarehouseId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    Check("Kho §7/Q4 (AC5 chỉ có quyền Xem): ẩn Thêm/Sửa/Xóa và nhấp đôi mở hộp thoại chỉ đọc",
                        hiddenCrud && roOpened && roLocked && roNoSave,
                        "hidden=" + hiddenCrud + ", ro=" + roOpened + ", locked=" + roLocked + ", noSave=" + roNoSave);
                    host.Close();
                }

                // Mức 0: ẩn menu; Admin: thấy menu KHO HÀNG > Danh mục kho hàng và ScreenFactory mở đúng CatalogView chế độ kho.
                var sess0 = new UserSession { UserId = "u_0", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(sess0).Roots, "Danh mục kho hàng");

                var adminSess = new UserSession { UserId = "u_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
                AppSession.Current = adminSess;
                MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, "Danh mục kho hàng");
                bool openedWarehouseMode = false;
                if (adminNode != null)
                {
                    using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                    {
                        var cv = created as CatalogView;
                        openedWarehouseMode = cv != null && cv.IsWarehouseModeForTest;
                    }
                }
                Check("Kho §7 (AC5 menu KHO HÀNG > Danh mục kho hàng): mức 0 không thấy menu; Admin mở màn kho thật trên CatalogView",
                    deniedNode == null && adminNode != null && openedWarehouseMode,
                    "deniedNull=" + (deniedNode == null) + ", adminNode=" + (adminNode != null) + ", mode=" + openedWarehouseMode);
            }
            finally
            {
                AppSession.Current = prev;
            }
        }

        private static void TestWarehouseLiveThemeSwitch(string db, string khoTableId)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new WarehouseEditDialog(AppSession.Current, null, false))
            {
                var view = new CatalogView(db, khoTableId, "Danh mục kho hàng") { Dock = DockStyle.Fill };
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
                    bool gridColorOk = g.BackgroundColor == tB.Surface
                        && g.DefaultCellStyle.SelectionBackColor == tB.Primary
                        && cellColorOk;
                    bool dlgColorOk = dlg.BackColor == tB.Background
                        && dlg.ErrorLabelForTest.ForeColor == tB.Danger
                        && dlg.StoreComboForTest.BackColor == tB.Surface;

                    Check("Kho §7 (AC6 đổi bảng màu trực tiếp): lưới, ô lưới và WarehouseEditDialog đổi đúng màu thực theo ThemeTokens",
                        gridColorOk && dlgColorOk,
                        "grid=" + gridColorOk + ", cell=" + cellColorOk + ", dlg=" + dlgColorOk);
                }
                finally
                {
                    dlg.Close();
                    host.Close();
                }
            }
        }

        private static bool HasControlOfType<T>(Control root) where T : Control
        {
            foreach (Control c in root.Controls)
            {
                if (c is T)
                    return true;
                if (HasControlOfType<T>(c))
                    return true;
            }
            return false;
        }
    }
}
