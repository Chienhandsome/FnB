using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void StoreUiTests(string db)
        {
            var repo = new StoreRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            string whId = DbHelper.NewId();
            string whRefId = DbHelper.NewId();
            string tplGoodId = DbHelper.NewId();
            string tplOtherId = DbHelper.NewId();
            string imgId = DbHelper.NewId();
            string storeAId = DbHelper.NewId();
            string storeBId = DbHelper.NewId();
            string dlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-ch-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ch')",
                    "@I", whId, "@N", "Kho UI CH " + tag);
                BookingExec(db, "INSERT INTO SIMAGE (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ch')",
                    "@I", imgId, "@N", "Ảnh UI CH " + tag);
                InsertStoreTemplateRow(db, tplGoodId, "Mẫu HĐ UI Đúng " + tag, StoreText.InvoiceTemplateFormId);
                InsertStoreTemplateRow(db, tplOtherId, "Mẫu HĐ UI Sai " + tag, "22222222-2222-2222-2222-222222222222");

                BookingExec(db,
                    "INSERT INTO DCUAHANG (ID, CODE, NAME, SIMAGEID, DIACHI, DIENTHOAI, DKHOHANGID, STEMPLATEID, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @IMG, @DC, @DT, @WH, @TPL, @NT, 30, 'selftest_ch', @T)",
                    "@I", storeAId, "@C", "CHUIA-" + tag, "@N", "Cửa Hàng Đặng Ánh " + tag, "@IMG", imgId,
                    "@DC", "45 Nguyễn Huệ", "@DT", "0912345678", "@WH", whId, "@TPL", tplGoodId,
                    "@NT", "Ghi chú đặc biệt " + tag, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DCUAHANG (ID, CODE, NAME, DIACHI, DIENTHOAI, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @DC, @DT, @NT, 30, 'selftest_ch', @T)",
                    "@I", storeBId, "@C", "CHUIB-" + tag, "@N", "Cửa Hàng Bình Minh " + tag,
                    "@DC", "99 Hai Bà Trưng", "@DT", "0988777666", "@NT", "Chi nhánh 2", "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, STATUS, USERCREATEDID) VALUES (@I, @N, @S, 30, 'selftest_ch')",
                    "@I", whRefId, "@N", "Kho Tham Chiếu UI " + tag, "@S", storeBId);

                string storeTableId = new CatalogRepository(db).FindIdByTable(StoreText.TableName);

                TestStoreMenuAndVisibleConfigId(db);
                TestStoreGridSearchExportAndDeleteBlock(db, storeTableId, storeAId, storeBId, tag, xlsPath);
                dlgSavedId = TestStoreEditDialogUi(db, repo, whId, tplGoodId, tplOtherId, storeAId, imgId, tag);
                TestStoreReadOnlyPermissionUi(db, repo, storeTableId, storeAId);
                TestStoreLiveThemeSwitch(db, storeTableId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                SafeDeleteById(db, "DKHOHANG", whRefId);
                if (dlgSavedId != null)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", dlgSavedId);
                    SafeDeleteById(db, "DCUAHANG", dlgSavedId);
                }
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", storeAId);
                BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", storeBId);
                SafeDeleteById(db, "DCUAHANG", storeAId);
                SafeDeleteById(db, "DCUAHANG", storeBId);
                SafeDeleteById(db, "STEMPLATE", tplGoodId);
                SafeDeleteById(db, "STEMPLATE", tplOtherId);
                SafeDeleteById(db, "SIMAGE", imgId);
                SafeDeleteById(db, "DKHOHANG", whId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestStoreMenuAndVisibleConfigId(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            int origVal = DbHelper.Read(db, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT INTVALUE FROM SCONFIG WHERE NAME = @N", "@N", StoreText.ConfigMultiStoreKey)));
            UserSession prev = AppSession.Current;
            try
            {
                var adminSess = new UserSession { UserId = "u_ch_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
                var deniedSess = new UserSession { UserId = "u_ch_0", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                AppSession.Current = adminSess;

                // NhieuCuaHang = 0 -> ẩn menu.
                cfgRepo.Save(new[] { new ConfigChange { Name = StoreText.ConfigMultiStoreKey, DataType = 3, IntValue = 0 } }, "selftest_ch");
                SystemConfig.Reload(db);
                MenuNode nodeWhen0 = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, StoreText.FunctionName);

                // NhieuCuaHang = 15 (khác 30) -> vẫn ẩn menu.
                cfgRepo.Save(new[] { new ConfigChange { Name = StoreText.ConfigMultiStoreKey, DataType = 3, IntValue = 15 } }, "selftest_ch");
                SystemConfig.Reload(db);
                MenuNode nodeWhen15 = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, StoreText.FunctionName);

                // NhieuCuaHang = 30 -> Admin thấy menu và mở màn CatalogView chế độ cửa hàng; user không quyền vẫn ẩn.
                cfgRepo.Save(new[] { new ConfigChange { Name = StoreText.ConfigMultiStoreKey, DataType = 3, IntValue = 30 } }, "selftest_ch");
                SystemConfig.Reload(db);
                MenuNode nodeWhen30 = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, StoreText.FunctionName);
                MenuNode deniedWhen30 = FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, StoreText.FunctionName);

                bool openedStoreMode = false;
                if (nodeWhen30 != null)
                {
                    using (Control created = ScreenFactory.CreateView(nodeWhen30, adminSess))
                    {
                        var cv = created as CatalogView;
                        openedStoreMode = cv != null && cv.IsStoreModeForTest;
                    }
                }

                // Kiểm nhận diện ScreenFactory theo tên không dấu, không phân biệt hoa thường.
                bool routeNoAccent = ScreenFactory.IsStoreCatalogMenu(new MenuNode { Title = "danh muc cua hang" }, adminSess)
                    && ScreenFactory.IsStoreCatalogMenu(new MenuNode { Title = "DANH MỤC CỬA HÀNG" }, adminSess)
                    && !ScreenFactory.IsStoreCatalogMenu(new MenuNode { Title = "Danh mục kho hàng" }, adminSess);

                Check("Cửa hàng §7 (AC1 menu HOẠT ĐỘNG > Danh mục cửa hàng & VISIBLECONFIGID NhieuCuaHang): 0/15 ẩn, 30 hiện cho Admin & mở màn thật, không quyền vẫn ẩn, ScreenFactory nhận diện không dấu",
                    nodeWhen0 == null && nodeWhen15 == null && nodeWhen30 != null && deniedWhen30 == null
                    && openedStoreMode && routeNoAccent,
                    "n0=" + (nodeWhen0 == null) + ", n15=" + (nodeWhen15 == null) + ", n30=" + (nodeWhen30 != null)
                    + ", denied=" + (deniedWhen30 == null) + ", mode=" + openedStoreMode + ", route=" + routeNoAccent);
            }
            finally
            {
                AppSession.Current = prev;
                cfgRepo.Save(new[] { new ConfigChange { Name = StoreText.ConfigMultiStoreKey, DataType = 3, IntValue = origVal } }, "selftest_ch");
                SystemConfig.Reload(db);
            }
        }

        private static void TestStoreGridSearchExportAndDeleteBlock(
            string db, string storeTableId, string storeAId, string storeBId, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, storeTableId, StoreText.FunctionName)
                {
                    Dock = DockStyle.Fill,
                    SuppressStorePopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expected = { "Mã cửa hàng", "Tên cửa hàng", "Địa chỉ", "Điện thoại", "Kho mặc định", "Ghi chú" };
                    bool colsOk = view.IsStoreModeForTest && grid.Columns.Count == expected.Length;
                    bool noClipHeader = true;
                    for (int i = 0; colsOk && i < expected.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expected[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        int need = TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
                        if (need > grid.Columns[i].Width)
                            noClipHeader = false;
                    }
                    Check("Cửa hàng §7 (AC9 lưới CatalogView 6 cột): đúng thứ tự cột theo giao-dien.md, tiêu đề không cắt chữ",
                        colsOk && noClipHeader,
                        "cols=" + colsOk + ", clip=" + noClipHeader + ", count=" + grid.Columns.Count);

                    int allCount = view.VisibleRows;

                    // Tìm không dấu theo tên, địa chỉ, điện thoại, mã, ghi chú.
                    view.SearchBoxForTest.Text = "dang anh " + tag;
                    Application.DoEvents();
                    int hitName = view.VisibleRows;

                    view.SearchBoxForTest.Text = "nguyen hue";
                    Application.DoEvents();
                    int hitAddr = view.VisibleRows;

                    view.SearchBoxForTest.Text = "0912345678";
                    Application.DoEvents();
                    int hitPhone = view.VisibleRows;

                    view.SearchBoxForTest.Text = "dac biet " + tag;
                    Application.DoEvents();
                    int hitNote = view.VisibleRows;

                    view.SearchBoxForTest.Text = "chuia-" + tag;
                    Application.DoEvents();
                    int hitCode = view.VisibleRows;

                    // Xuất Excel đúng cột đang hiện và dòng đang lọc qua GridExcelExporter.
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

                    Check("Cửa hàng §7 (tìm không dấu Mã/Tên/Địa chỉ/SĐT/Ghi chú, F3 và Xuất Excel GridExcelExporter): lọc đúng từng trường, F3 vào ô tìm, xuất .xls đúng 6 cột và dòng lọc",
                        hitName == 1 && hitAddr >= 1 && hitPhone >= 1 && hitNote == 1 && hitCode == 1
                        && restoredCount == allCount && f3Handled && searchActive
                        && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 6 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "Cửa Hàng Đặng Ánh " + tag),
                        "n=" + hitName + ", a=" + hitAddr + ", p=" + hitPhone + ", nt=" + hitNote + ", c=" + hitCode
                        + ", f3=" + searchActive + ", xls=" + (snap == null ? -1 : snap.RowCount) + "x" + (snap == null ? -1 : snap.ColCount));

                    // Bấm Xóa trên dòng còn kho tham chiếu -> chặn, thông báo nêu "1 kho" và giữ nguyên dòng.
                    bool selRef = view.SelectStoreRowByIdForTest(storeBId);
                    Application.DoEvents();
                    view.RemoveButtonForTest.PerformClick();
                    Application.DoEvents();
                    string warn = view.LastStoreMessageForTest;
                    bool stillThere = new StoreRepository(db).LoadById(storeBId) != null;
                    Check("Cửa hàng §7/Q1 (AC7 UI nút Xóa chặn cửa hàng còn kho): hiện cảnh báo nêu rõ '1 kho' và giữ nguyên dòng",
                        selRef && stillThere && !string.IsNullOrEmpty(warn) && warn.IndexOf("1 kho", StringComparison.Ordinal) >= 0,
                        SingleLine(warn));
                }
                finally { host.Close(); }
            }
        }

        private static string TestStoreEditDialogUi(
            string db, StoreRepository repo, string whId, string tplGoodId, string tplOtherId,
            string existingStoreAId, string origImageId, string tag)
        {
            string savedId = null;
            using (var dlg = new StoreEditDialog(AppSession.Current, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool noImageCombo = !HasControlOfType<ImageCombo>(dlg);
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 7 && AllLabelsFit(dlg.FieldLabelsForTest);
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                    bool hasGoodTpl = ComboContainsLookupId(dlg.TemplateComboForTest, tplGoodId);
                    bool hasOtherTpl = ComboContainsLookupId(dlg.TemplateComboForTest, tplOtherId);

                    // Thử nhập Mã 37 ký tự -> bấm Lưu bị chặn.
                    dlg.CodeBoxForTest.Text = new string('Z', 37);
                    dlg.NameBoxForTest.Text = "CH Thử Dài " + tag;
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool blocked37 = dlg.SavedId == null && !string.IsNullOrEmpty(dlg.LastErrorForTest);

                    // Nhập đủ trường hợp lệ -> bấm Lưu & thoát.
                    dlg.CodeBoxForTest.Text = "CHDLG-" + tag;
                    dlg.NameBoxForTest.Text = "Cửa Hàng Dialog " + tag;
                    dlg.AddressBoxForTest.Text = "77 Trần Phú";
                    dlg.PhoneBoxForTest.Text = "0933444555";
                    dlg.WarehouseComboForTest.SelectId(whId);
                    dlg.TemplateComboForTest.SelectId(tplGoodId);
                    dlg.NoteBoxForTest.Text = "Tạo qua nút Lưu";
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    StoreRow back = savedId == null ? null : repo.LoadById(savedId);
                    Check("Cửa hàng §7 (AC4/AC5/AC9 StoreEditDialog thêm mới): không ô ảnh, 7 nhãn không cắt, không cuộn dọc 1366×768 @150% DPI, Mẫu HĐ lọc đúng SFORMID, chặn Mã 37, lưu khớp mọi trường",
                        noImageCombo && labelsOk && noScroll && fits150 && hasGoodTpl && !hasOtherTpl && blocked37
                        && back != null && back.Status == 30 && back.Code == "CHDLG-" + tag
                        && back.Name == "Cửa Hàng Dialog " + tag && back.Address == "77 Trần Phú"
                        && back.Phone == "0933444555" && back.WarehouseId == whId
                        && back.TemplateId == tplGoodId && back.Note == "Tạo qua nút Lưu",
                        SingleLine(dlg.LastErrorForTest ?? ("noImg=" + noImageCombo + ", lbl=" + labelsOk + ", fits150=" + fits150 + ", tpl=" + hasGoodTpl + "/" + hasOtherTpl + ", b37=" + blocked37)));
                }
                finally { dlg.Close(); }
            }

            // Sửa cửa hàng có sẵn SIMAGEID trên dialog -> lưu xong vẫn giữ nguyên SIMAGEID cũ.
            using (var dlgEdit = new StoreEditDialog(AppSession.Current, existingStoreAId, false))
            {
                dlgEdit.SuppressPopup = true;
                ShowDialogOffscreen(dlgEdit);
                try
                {
                    dlgEdit.NoteBoxForTest.Text = "Đã sửa qua dialog " + tag;
                    Application.DoEvents();
                    dlgEdit.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    StoreRow back = repo.LoadById(existingStoreAId);
                    Check("Cửa hàng §7 (AC5 StoreEditDialog sửa giữ nguyên SIMAGEID): cập nhật ghi chú và giữ nguyên SIMAGEID cũ",
                        back != null && back.Note == "Đã sửa qua dialog " + tag && back.ImageId == origImageId,
                        "img=" + (back == null ? "null" : (back.ImageId ?? "null")));
                }
                finally { dlgEdit.Close(); }
            }

            return savedId;
        }

        private static void TestStoreReadOnlyPermissionUi(string db, StoreRepository repo, string storeTableId, string sampleStoreId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add(StoreText.FunctionId, StoreText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_ch_ro", IsAdmin = false, DatabasePath = db, Permissions = permsView };

                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, storeTableId, StoreText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible && !view.RemoveButtonForTest.Visible;
                    bool roOpened = false, roLocked = false, roNoSave = false, saveBlocked = false;
                    string origName = repo.LoadById(sampleStoreId).Name;

                    view.StoreDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = dlg.CodeBoxForTest.ReadOnly && dlg.NameBoxForTest.ReadOnly
                                && dlg.AddressBoxForTest.ReadOnly && dlg.PhoneBoxForTest.ReadOnly
                                && !dlg.WarehouseComboForTest.Enabled && !dlg.TemplateComboForTest.Enabled
                                && dlg.NoteBoxForTest.ReadOnly;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                            dlg.NameBoxForTest.Text = "Cố tình sửa khi chỉ xem";
                            saveBlocked = !dlg.DoSave();
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectStoreRowByIdForTest(sampleStoreId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    string afterName = repo.LoadById(sampleStoreId).Name;
                    Check("Cửa hàng §7/Q4 (AC8 chỉ có quyền Xem): ẩn Thêm/Sửa/Xóa, nhấp đôi mở hộp chỉ đọc, khóa mọi ô và không lưu được",
                        hiddenCrud && roOpened && roLocked && roNoSave && saveBlocked && afterName == origName,
                        "hidden=" + hiddenCrud + ", ro=" + roOpened + ", locked=" + roLocked + ", noSave=" + roNoSave + ", blk=" + saveBlocked);
                    host.Close();
                }
            }
            finally
            {
                AppSession.Current = prev;
            }
        }

        private static void TestStoreLiveThemeSwitch(string db, string storeTableId)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new StoreEditDialog(AppSession.Current, null, false))
            {
                var view = new CatalogView(db, storeTableId, StoreText.FunctionName) { Dock = DockStyle.Fill };
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
                        && dlg.WarehouseComboForTest.BackColor == tB.Surface
                        && dlg.TemplateComboForTest.BackColor == tB.Surface;

                    Check("Cửa hàng §7 (AC9 đổi bảng màu trực tiếp): lưới, ô lưới thực tế và StoreEditDialog đổi đúng màu ThemeTokens",
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

        private static bool ComboContainsLookupId(LookupCombo combo, string id)
        {
            if (combo == null || string.IsNullOrEmpty(id))
                return false;
            foreach (object obj in combo.Items)
            {
                var item = obj as LookupItem;
                if (item != null && string.Equals(item.Id, id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
