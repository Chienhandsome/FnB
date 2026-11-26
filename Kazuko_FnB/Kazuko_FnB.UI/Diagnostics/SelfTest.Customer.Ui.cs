using System;
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
        private static void CustomerUiTests(string db)
        {
            var custRepo = new CustomerRepository(db);
            var grpRepo = new CustomerGroupRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            string parentId = null;
            string childId = null;
            string custAId = null;
            string custBId = null;
            string empId = null;
            string provId = null;
            string cardId = null;
            string dlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-kh-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                CustomerGroupRow parent = CustomerGroupRow.NewDefault();
                parent.Name = "Nhóm Cha UI " + tag;
                parentId = grpRepo.Save(parent, "selftest_kh").SavedId;

                CustomerGroupRow child = CustomerGroupRow.NewDefault();
                child.Name = "Nhóm Con UI " + tag;
                child.ParentId = parentId;
                childId = grpRepo.Save(child, "selftest_kh").SavedId;

                CustomerRow cA = CustomerRow.NewDefault();
                cA.Code = "KHUIA" + tag;
                cA.Name = "Đặng Thị Ánh " + tag;
                cA.GroupId = parentId;
                cA.Phone = "0911222333";
                cA.Note = "Ghi chú bí mật " + tag;
                custAId = custRepo.SaveCustomer(cA, "selftest_kh", new CustomerSaveOptions()).SavedId;

                CustomerRow cB = CustomerRow.NewDefault();
                cB.Code = "KHUIB" + tag;
                cB.Name = "Trần Văn Bình " + tag;
                cB.GroupId = childId;
                cB.Phone = "0944555666";
                custBId = custRepo.SaveCustomer(cB, "selftest_kh", new CustomerSaveOptions()).SavedId;

                empId = DbHelper.NewId();
                provId = DbHelper.NewId();
                cardId = DbHelper.NewId();
                BookingExec(db, "INSERT INTO DNHANVIEN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kh')", "@I", empId, "@N", "NV UI " + tag);
                BookingExec(db, "INSERT INTO DTINHTHANH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kh')", "@I", provId, "@N", "Đà Nẵng " + tag);
                BookingExec(db, "INSERT INTO DTHETRATRUOC (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kh')", "@I", cardId, "@N", "Thẻ UI " + tag);

                string khTableId = new CatalogRepository(db).FindIdByTable("DKHACHHANG");

                TestCustomerGridSearchTreeAndExport(db, khTableId, parent.Name, child.Name, tag, xlsPath);
                TestCustomerPermissionsAndReadOnlyDialogs(db, khTableId, parent.Name, tag);
                dlgSavedId = TestCustomerEditDialogUi(db, parentId, empId, provId, cardId, tag);
                TestCustomerGroupEditDialogUi(db);
                TestCustomerLiveThemeSwitch(db, khTableId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                if (dlgSavedId != null) SafeDeleteById(db, "DKHACHHANG", dlgSavedId);
                if (custAId != null) SafeDeleteById(db, "DKHACHHANG", custAId);
                if (custBId != null) SafeDeleteById(db, "DKHACHHANG", custBId);
                if (childId != null) SafeDeleteById(db, "DNHOMKHACHHANG", childId);
                if (parentId != null) SafeDeleteById(db, "DNHOMKHACHHANG", parentId);
                if (empId != null) SafeDeleteById(db, "DNHANVIEN", empId);
                if (provId != null) SafeDeleteById(db, "DTINHTHANH", provId);
                if (cardId != null) SafeDeleteById(db, "DTHETRATRUOC", cardId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestCustomerGridSearchTreeAndExport(
            string db, string khTableId, string parentName, string childName, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, khTableId, "Danh mục khách hàng") { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    string[] expectedHeaders =
                    {
                        "Tên khách hàng", "Nhóm khách hàng", "Mã khách", "Địa chỉ", "Điện thoại",
                        "Email", "Mã số thuế", "Nhân viên", "Điểm tích lũy ban đầu",
                        "Ngày thành lập/sinh nhật", "Ghi chú"
                    };
                    DataGridView grid = view.GridForTest;
                    bool colsOk = grid.Columns.Count == expectedHeaders.Length;
                    for (int i = 0; colsOk && i < expectedHeaders.Length; i++)
                    {
                        bool expectVis = i < expectedHeaders.Length - 1;
                        if (grid.Columns[i].HeaderText != expectedHeaders[i] || grid.Columns[i].Visible != expectVis)
                            colsOk = false;
                    }
                    Check("KH §6 (lưới 11 cột đúng thứ tự §4.1, Ghi chú mặc định ẩn)",
                        colsOk, "count=" + grid.Columns.Count);

                    int allCount = view.VisibleRows;
                    view.SearchBoxForTest.Text = "dang thi anh " + tag;
                    Application.DoEvents();
                    int hitAccent = view.VisibleRows;

                    view.SearchBoxForTest.Text = "bi mat " + tag;
                    Application.DoEvents();
                    int hitNoteHidden = view.VisibleRows;
                    view.ToggleCustomerColumnForTest("c_note", true);
                    Application.DoEvents();
                    int hitNoteVisible = view.VisibleRows;
                    view.ToggleCustomerColumnForTest("c_note", false);
                    view.SearchBoxForTest.Text = "";
                    Application.DoEvents();
                    int restoredCount = view.VisibleRows;

                    FocusControlReal(host, grid);
                    Message f3Msg = new Message();
                    bool f3Handled = InvokeProcessCmdKey(view, ref f3Msg, Keys.F3);
                    bool searchActive = view.SearchBoxForTest.Inner.Focused
                        || view.SearchBoxForTest.ContainsFocus
                        || host.ActiveControl == view.SearchBoxForTest;

                    Check("KH §6 (tìm không dấu, bật cột Ghi chú, xóa chữ hiện lại, F3 vào ô tìm)",
                        hitAccent == 1 && hitNoteHidden == 0 && hitNoteVisible == 1
                        && restoredCount == allCount && f3Handled && searchActive,
                        "accent=" + hitAccent + ", noteOff=" + hitNoteHidden + ", noteOn=" + hitNoteVisible + ", f3=" + searchActive);

                    bool selParent = view.SelectCategory(parentName);
                    Application.DoEvents();
                    int parentRows = view.VisibleRows;
                    bool selChild = view.SelectCategory(childName);
                    Application.DoEvents();
                    int childRows = view.VisibleRows;
                    bool selAll = view.SelectCategory("Tất cả");
                    Application.DoEvents();
                    int allAfterTree = view.VisibleRows;

                    Check("KH §6 (lọc cây nhóm): chọn cha hiện cả cha+con, chọn con chỉ hiện con, Tất cả hiện hết",
                        selParent && parentRows == 2 && selChild && childRows == 1 && selAll && allAfterTree == allCount,
                        "parent=" + parentRows + ", child=" + childRows + ", all=" + allAfterTree);

                    view.SetSearch("KHUIA" + tag);
                    Application.DoEvents();
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;
                    Check("KH (Xuất excel qua GridExcelExporter): xuất đúng cột hiển thị và dòng đang lọc, không xuất cột Ghi chú ẩn",
                        snap != null && rec.PromptSaveCount == 1
                        && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "KHUIA" + tag)
                        && !XlsSheetContainsSubstring(snap, "Ghi chú"),
                        snap == null ? "no-file" : "rows=" + snap.RowCount + ", cols=" + snap.ColCount);
                }
                finally { host.Close(); }
            }
        }

        private static void TestCustomerPermissionsAndReadOnlyDialogs(string db, string khTableId, string parentName, string tag)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var mgrPerms = new FunctionPermissions(false);
                mgrPerms.Add(CustomerText.FunctionId, CustomerText.FunctionName, RoleModes.View | RoleModes.Edit | RoleModes.Add | RoleModes.Remove);
                AppSession.Current = new UserSession { UserId = "u_mgr", IsAdmin = false, DatabasePath = db, Permissions = mgrPerms };
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, khTableId, "Danh mục khách hàng") { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    Check("KH §6/§9.0 (quyền 240 Quản lý/Kế toán): hiện đủ nút Thêm/Sửa/Xóa và nút nhóm",
                        view.AddButtonForTest.Visible && view.EditButtonForTest.Visible && view.RemoveButtonForTest.Visible
                        && view.GroupAddButtonForTest.Visible && view.GroupEditButtonForTest.Visible);
                    host.Close();
                }

                var viewPerms = new FunctionPermissions(false);
                viewPerms.Add(CustomerText.FunctionId, CustomerText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_view", IsAdmin = false, DatabasePath = db, Permissions = viewPerms };
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, khTableId, "Danh mục khách hàng") { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool buttonsHidden = !view.AddButtonForTest.Visible
                        && !view.EditButtonForTest.Visible
                        && !view.RemoveButtonForTest.Visible
                        && !view.GroupAddButtonForTest.Visible
                        && !view.GroupEditButtonForTest.Visible
                        && !view.GroupBarForTest.Visible;
                    Check("KH §6/§9.0 (quyền 0x10 chỉ xem): ẩn hoàn toàn các nút Thêm/Sửa/Xóa khách và nhóm",
                        buttonsHidden,
                        "add=" + view.AddButtonForTest.Visible + ", edit=" + view.EditButtonForTest.Visible + ", del=" + view.RemoveButtonForTest.Visible);

                    bool custRoOpened = false, custRoLocked = false, custNoSaveBtns = false;
                    view.CustomerDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            custRoOpened = dlg.IsReadOnlyForTest;
                            custRoLocked = dlg.NameBoxForTest.ReadOnly && dlg.CodeBoxForTest.ReadOnly
                                && !dlg.GroupComboForTest.Enabled && !dlg.PointsBoxForTest.Enabled;
                            custNoSaveBtns = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    view.SetSearch("KHUIA" + tag);
                    Application.DoEvents();
                    if (view.GridForTest.Rows.Count > 0)
                    {
                        view.GridForTest.CurrentCell = view.GridForTest.Rows[0].Cells[0];
                        InvokeGridDoubleClick(view.GridForTest, 0, 0);
                    }
                    Check("KH §9.0.2 Q10 (chỉ xem nhấp đôi khách hàng): mở hộp thoại chỉ đọc, khóa ô, không có nút Lưu",
                        custRoOpened && custRoLocked && custNoSaveBtns,
                        "ro=" + custRoOpened + ", locked=" + custRoLocked + ", noSave=" + custNoSaveBtns);

                    bool grpRoOpened = false, grpRoLocked = false, grpNoSaveBtns = false;
                    view.CustomerGroupDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            grpRoOpened = dlg.IsReadOnlyForTest;
                            grpRoLocked = dlg.NameBoxForTest.ReadOnly && !dlg.DiscountTotalBoxForTest.Enabled && !dlg.PointsBoxForTest.Enabled;
                            grpNoSaveBtns = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    view.SelectCategory(parentName);
                    Application.DoEvents();
                    if (view.TreeForTest.SelectedNode != null)
                        InvokeTreeNodeDoubleClick(view.TreeForTest, view.TreeForTest.SelectedNode);
                    Check("KH §9.0.2 Q10 (chỉ xem nhấp đôi nhóm): mở hộp thoại nhóm chỉ đọc, khóa ô, không có nút Lưu",
                        grpRoOpened && grpRoLocked && grpNoSaveBtns,
                        "ro=" + grpRoOpened + ", locked=" + grpRoLocked + ", noSave=" + grpNoSaveBtns);
                    host.Close();
                }

                var lockedSession = new UserSession { UserId = "u_lock", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                MenuNode lockedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(lockedSession).Roots, "Danh mục khách hàng");

                var adminSession = new UserSession { UserId = "u_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
                AppSession.Current = adminSession;
                MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSession).Roots, "Danh mục khách hàng");
                Control createdView = adminNode == null ? null : ScreenFactory.CreateView(adminNode, adminSession);
                var catalog = createdView as CatalogView;
                bool openedCustomerMode = catalog != null && catalog.IsCustomerModeForTest;
                if (createdView != null) createdView.Dispose();

                Check("KH §6 (menu và phím tắt Ctrl+K): Mode 0 ẩn menu; Admin có Ctrl+K (131147) và mở màn Danh mục khách hàng thật",
                    lockedNode == null && adminNode != null
                    && adminNode.Shortcut == (int)(Keys.Control | Keys.K)
                    && openedCustomerMode,
                    "lockedNull=" + (lockedNode == null) + ", shortcut=" + (adminNode == null ? -1 : adminNode.Shortcut) + ", mode=" + openedCustomerMode);
            }
            finally
            {
                AppSession.Current = prev;
            }
        }

        private static string TestCustomerEditDialogUi(
            string db, string groupId, string empId, string provId, string cardId, string tag)
        {
            var cfgRepo = new ConfigRepository(db);
            int oldPriceCfg = SystemConfig.GetInt("SuDungGiaTheoKhach");
            int oldCardCfg = SystemConfig.GetInt("SuDungTheTraTruoc");
            string savedId = null;

            try
            {
                SaveCustomerBoolConfigs(db, cfgRepo, 0, 0);
                using (var dlg = new CustomerEditDialog(AppSession.Current, null, groupId, false))
                {
                    dlg.SuppressPopup = true;
                    ShowDialogOffscreen(dlg);
                    try
                    {
                        bool lockedOff = !dlg.PriceTypeComboForTest.Enabled && !dlg.PrepaidCardComboForTest.Enabled;
                        bool presetOk = dlg.GroupComboForTest.SelectedId == groupId;
                        bool noScroll = !dlg.VerticalScroll.Visible && dlg.Height <= 728 && dlg.Width <= 1366;
                        float scale = dlg.DeviceDpi / 96f;
                        bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                        dlg.PointsBoxForTest.Value = 40m;
                        AssignEditTextIntoNumberBox(dlg, dlg.PointsBoxForTest, "12.5");
                        FocusControlReal(dlg, dlg.NameBoxForTest);
                        decimal afterDecimal = dlg.PointsBoxForTest.Value;

                        AssignEditTextIntoNumberBox(dlg, dlg.PointsBoxForTest, "-5");
                        FocusControlReal(dlg, dlg.NameBoxForTest);
                        decimal afterNeg = dlg.PointsBoxForTest.Value;

                        Check("KH §6/§9.0 (CustomerEditDialog cấu hình tắt + Điểm NumberBox + không cuộn dọc 1366×768 @100-150% DPI)",
                            lockedOff && presetOk && noScroll && fits150 && afterDecimal == 40m && afterNeg >= 0m,
                            "locked=" + lockedOff + ", preset=" + presetOk + ", fits150=" + fits150 + ", dec=" + afterDecimal + ", neg=" + afterNeg);
                    }
                    finally { dlg.Close(); }
                }

                SaveCustomerBoolConfigs(db, cfgRepo, 30, 30);
                using (var dlg = new CustomerEditDialog(AppSession.Current, null, groupId, false))
                {
                    dlg.SuppressPopup = true;
                    ShowDialogOffscreen(dlg);
                    try
                    {
                        bool enabledOn = dlg.PriceTypeComboForTest.Enabled && dlg.PrepaidCardComboForTest.Enabled;
                        dlg.CodeBoxForTest.Text = "KHUID" + tag;
                        dlg.NameBoxForTest.Text = "Khách Dialog 15T " + tag;
                        dlg.GroupComboForTest.SelectId(groupId);
                        dlg.EmployeeComboForTest.SelectId(empId);
                        dlg.PhoneBoxForTest.Text = "0977888999";
                        dlg.EmailBoxForTest.Text = "dlg@kazuko.vn";
                        dlg.AddressBoxForTest.Text = "456 Nguyễn Huệ";
                        dlg.ProvinceComboForTest.SelectId(provId);
                        dlg.TaxCodeBoxForTest.Text = "0312345678";
                        dlg.FacebookBoxForTest.Text = "fb.com/dlg";
                        dlg.BirthdayBoxForTest.NullableValue = new DateTime(1998, 5, 12);
                        dlg.PointsBoxForTest.Value = 88m;
                        dlg.PriceTypeComboForTest.SelectedIndex = 3;
                        dlg.PrepaidCardComboForTest.SelectId(cardId);
                        dlg.NoteBoxForTest.Text = "Ghi chú từ dialog";
                        Application.DoEvents();

                        dlg.SaveButtonForTest.PerformClick();
                        Application.DoEvents();
                        savedId = dlg.SavedId;

                        CustomerRow back = savedId == null ? null : new CustomerRepository(db).LoadCustomer(savedId);
                        Check("KH §6/§9.0 (CustomerEditDialog cấu hình bật): mở Giá bán + Thẻ trả trước, bấm Lưu ghi đủ 15 trường",
                            enabledOn && back != null && back.PriceType == 3 && back.PrepaidCardId == cardId
                            && back.EmployeeId == empId && back.ProvinceId == provId
                            && back.InitialPoints == 88 && back.Birthday == new DateTime(1998, 5, 12)
                            && back.Note == "Ghi chú từ dialog",
                            SingleLine(dlg.LastErrorForTest ?? "ok"));
                    }
                    finally { dlg.Close(); }
                }
            }
            finally
            {
                SaveCustomerBoolConfigs(db, cfgRepo, oldPriceCfg, oldCardCfg);
            }
            return savedId;
        }

        private static void TestCustomerGroupEditDialogUi(string db)
        {
            using (var dlg = new CustomerGroupEditDialog(AppSession.Current, null, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    dlg.ApplyDiscountVisibility(false, false);
                    bool s1 = dlg.DiscountTotalBoxForTest.Visible && dlg.DiscountGoodsBoxForTest.Visible && !dlg.DiscountHourBoxForTest.Visible;
                    dlg.ApplyDiscountVisibility(true, false);
                    bool s2 = dlg.DiscountTotalBoxForTest.Visible && dlg.DiscountGoodsBoxForTest.Visible && dlg.DiscountHourBoxForTest.Visible;
                    dlg.ApplyDiscountVisibility(true, true);
                    bool s3 = dlg.DiscountTotalBoxForTest.Visible && !dlg.DiscountGoodsBoxForTest.Visible && !dlg.DiscountHourBoxForTest.Visible;

                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                    Check("KH §6/§9.0 A2 (CustomerGroupEditDialog): ẩn/hiện đúng ở 3 trạng thái cấu hình, Tỉ lệ giảm giá luôn hiện, không cuộn dọc 1366×768",
                        s1 && s2 && s3 && !dlg.VerticalScroll.Visible && fits150,
                        "s1=" + s1 + ", s2=" + s2 + ", s3=" + s3 + ", fits150=" + fits150);
                }
                finally { dlg.Close(); }
            }
        }
    }
}
