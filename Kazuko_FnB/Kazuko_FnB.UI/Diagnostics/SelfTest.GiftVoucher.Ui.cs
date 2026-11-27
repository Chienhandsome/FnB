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
        private static void GiftVoucherUiTests(string db)
        {
            var svc = new GiftVoucherService(AppSession.Current);
            var grpSvc = new GiftVoucherGroupService(AppSession.Current);
            var repo = new GiftVoucherRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            DateTime today = svc.GetServerDate();

            string parentId = null, childId = null, partnerId = null;
            string vUsable1Id = null, vUsable2Id = null, vUsedId = null, vExpiredId = null, vFutureId = null;
            string unpaidBillId = null, paidBillId = null, dlgSavedId = null, grpDlgSavedId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-voucher-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                var parent = GiftVoucherGroupRow.NewDefault();
                parent.Name = "Nhóm VC Cha UI " + tag;
                parentId = grpSvc.Save(parent).SavedId;

                var child = GiftVoucherGroupRow.NewDefault();
                child.Name = "Nhóm VC Con UI " + tag;
                child.ParentId = parentId;
                childId = grpSvc.Save(child).SavedId;

                partnerId = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, 30, 'selftest', @T)",
                    "@I", partnerId, "@C", "DTVC" + tag, "@N", "Đối tác Ánh Dương " + tag, "@T", DateTime.Now);

                vUsable1Id = svc.Save(new GiftVoucherRow { Code = "VCUIA-" + tag, Value = 100000m, IssueDate = today.AddDays(-1), ExpiryDate = today.AddDays(30), GroupId = parentId, PartnerId = partnerId, Note = "Phiếu ưu đãi đặc biệt " + tag }).SavedId;
                vUsable2Id = svc.Save(new GiftVoucherRow { Code = "VCUIB-" + tag, Value = 250000m, IssueDate = today, ExpiryDate = today.AddDays(15), GroupId = childId, Note = "Gắn bill chưa TT" }).SavedId;
                unpaidBillId = DbHelper.NewId();
                InsertGiftVoucherBillRef(db, unpaidBillId, "HD-UI-UNP-" + tag, vUsable2Id, 0, 30);

                vUsedId = svc.Save(new GiftVoucherRow { Code = "VCUIU-" + tag, Value = 50000m, IssueDate = today.AddDays(-5), ExpiryDate = today.AddDays(15), GroupId = parentId, Note = "Đã dùng trên bill" }).SavedId;
                paidBillId = DbHelper.NewId();
                string paidBillNo = "HD-UI-PAID-" + tag;
                InsertGiftVoucherBillRef(db, paidBillId, paidBillNo, vUsedId, 30, 30);

                vExpiredId = svc.Save(new GiftVoucherRow { Code = "VCUIE-" + tag, Value = 75000m, IssueDate = today.AddDays(-20), ExpiryDate = today.AddDays(-2), GroupId = null, Note = "Quá hạn" }).SavedId;
                vFutureId = svc.Save(new GiftVoucherRow { Code = "VCUIF-" + tag, Value = 125000m, IssueDate = today.AddDays(3), ExpiryDate = today.AddDays(30), GroupId = parentId, Note = "Chưa phát hành" }).SavedId;

                string voucherTableId = new CatalogRepository(db).FindIdByTable(GiftVoucherText.TableName);

                TestGiftVoucherGridTreeFilterTotalAndDeleteUi(db, voucherTableId, parent.Name, child.Name, vUsable2Id, vUsedId, paidBillNo, tag, xlsPath);
                dlgSavedId = TestGiftVoucherEditDialogUi(db, repo, parentId, vUsedId, today, tag);
                grpDlgSavedId = TestGiftVoucherGroupEditDialogUi(db, tag);
                TestGiftVoucherPermissionsMenuAndReadOnly(db, voucherTableId, parent.Name, vUsable1Id);
                TestGiftVoucherLiveThemeSwitch(db, voucherTableId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                if (unpaidBillId != null) SafeDeleteById(db, "TDONHANG", unpaidBillId);
                if (paidBillId != null) SafeDeleteById(db, "TDONHANG", paidBillId);
                foreach (string vid in new[] { dlgSavedId, vUsable1Id, vUsable2Id, vUsedId, vExpiredId, vFutureId })
                {
                    if (vid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "DVOUCHER", vid);
                }
                foreach (string gid in new[] { grpDlgSavedId, childId, parentId })
                {
                    if (gid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", gid);
                    SafeDeleteById(db, "DNHOMVOUCHER", gid);
                }
                if (partnerId != null) SafeDeleteById(db, "DKHACHHANG", partnerId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestGiftVoucherGridTreeFilterTotalAndDeleteUi(
            string db, string voucherTableId, string parentName, string childName,
            string unpaidVoucherId, string usedVoucherId, string paidBillNo, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, voucherTableId, GiftVoucherText.FunctionName) { Dock = DockStyle.Fill, SuppressGiftVoucherPopupForTest = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expectedHeaders = { "Mã voucher", "Giá trị", "Ngày phát hành", "Hạn sử dụng", "Nhóm voucher", "Đối tác phân phối", "Đã dùng", "Ghi chú" };
                    bool colsOk = view.IsGiftVoucherModeForTest && grid.Columns.Count == expectedHeaders.Length;
                    bool noClipHeader = true;
                    for (int i = 0; colsOk && i < expectedHeaders.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expectedHeaders[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        if (TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width > grid.Columns[i].Width)
                            noClipHeader = false;
                    }
                    Check("Voucher UI §4 (lưới 8 cột & tiêu đề): đủ 8 cột đúng thứ tự, tiêu đề không cắt chữ, có hàng tổng cộng",
                        colsOk && noClipHeader && view.VoucherTotalBarForTest.Visible,
                        "cols=" + colsOk + ", clip=" + noClipHeader + ", count=" + grid.Columns.Count);

                    string usedCellUnpaid = ReadVoucherGridUsedCell(view, unpaidVoucherId);
                    string usedCellPaid = ReadVoucherGridUsedCell(view, usedVoucherId);
                    Check("Voucher UI §4 (cột Đã dùng): bill chưa thanh toán hiện Không, bill đã thanh toán hiện Có kèm số hóa đơn",
                        usedCellUnpaid == "Không" && usedCellPaid == "Có (" + paidBillNo + ")",
                        SingleLine("unpaid=" + usedCellUnpaid + ", paid=" + usedCellPaid));

                    int allCount = view.VisibleRows;
                    bool selParent = view.SelectCategory(parentName);
                    Application.DoEvents();
                    int parentRows = view.VisibleRows;
                    decimal parentSum = view.VoucherTotalValueForTest;

                    view.VoucherUsableOnlyCheckForTest.Checked = true;
                    Application.DoEvents();
                    int usableRows = view.VisibleRows;
                    decimal usableSum = view.VoucherTotalValueForTest;

                    view.VoucherUsableOnlyCheckForTest.Checked = false;
                    Application.DoEvents();
                    int restoredParentRows = view.VisibleRows;

                    bool selChild = view.SelectCategory(childName);
                    Application.DoEvents();
                    int childRows = view.VisibleRows;
                    decimal childSum = view.VoucherTotalValueForTest;

                    bool selUnset = view.SelectCategory("Chưa thiết lập");
                    Application.DoEvents();
                    int unsetRows = view.VisibleRows;

                    bool selAll = view.SelectCategory("Tất cả");
                    Application.DoEvents();
                    int allAfter = view.VisibleRows;

                    Check("Voucher UI §4 (cây nhóm, lọc còn dùng được & hàng tổng cộng): chọn cha gồm con, lọc còn dùng được chỉ giữ voucher hợp lệ, tổng giá trị cập nhật đúng",
                        selParent && parentRows == 4 && parentSum == 525000m
                        && usableRows == 2 && usableSum == 350000m && restoredParentRows == 4
                        && selChild && childRows == 1 && childSum == 250000m
                        && selUnset && unsetRows >= 1 && selAll && allAfter == allCount,
                        "parent=" + parentRows + "/" + parentSum + ", usable=" + usableRows + "/" + usableSum + ", child=" + childRows + "/" + childSum);

                    view.SearchBoxForTest.Text = "doi tac anh duong " + tag;
                    Application.DoEvents();
                    int hitAccent = view.VisibleRows;
                    decimal searchSum = view.VoucherTotalValueForTest;

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
                    bool searchActive = view.SearchBoxForTest.Inner.Focused || view.SearchBoxForTest.ContainsFocus || host.ActiveControl == view.SearchBoxForTest;

                    Check("Voucher UI §4 (tìm không dấu, F3 & Xuất Excel GridExcelExporter): tìm đối tác không dấu lọc đúng 1 dòng, F3 vào ô tìm, xuất .xls đúng dòng lọc",
                        hitAccent == 1 && searchSum == 100000m && restoredCount == allCount && f3Handled && searchActive
                        && snap != null && rec.PromptSaveCount == 1 && snap.RowCount == 2 && XlsSheetContainsSubstring(snap, "VCUIA-" + tag),
                        "accent=" + hitAccent + ", f3=" + searchActive + ", xlsRows=" + (snap == null ? -1 : snap.RowCount));

                    bool selUsed = view.SelectGiftVoucherRowByIdForTest(usedVoucherId);
                    Application.DoEvents();
                    view.RemoveButtonForTest.PerformClick();
                    Application.DoEvents();
                    string warnVoucher = view.LastGiftVoucherMessageForTest;
                    bool usedStillThere = new GiftVoucherRepository(db).LoadById(usedVoucherId) != null;

                    view.SelectCategory(childName);
                    Application.DoEvents();
                    foreach (ToolStripItem item in view.GiftVoucherTreeMenuForTest.Items)
                    {
                        var mi = item as ToolStripMenuItem;
                        if (mi != null && mi.Text == "Xóa") { mi.PerformClick(); break; }
                    }
                    Application.DoEvents();
                    string warnGrp = view.LastGiftVoucherMessageForTest;

                    Check("Voucher UI §4 (chặn xóa trên UI): bấm Xóa chặn voucher đã dùng (kèm số HĐ) và chặn xóa nhóm còn voucher",
                        selUsed && usedStillThere && warnVoucher == GiftVoucherText.DeleteBlockedByBill(paidBillNo)
                        && !string.IsNullOrEmpty(warnGrp) && warnGrp.IndexOf("nhóm còn 1 voucher", StringComparison.Ordinal) >= 0,
                        SingleLine((warnVoucher ?? "") + " | " + (warnGrp ?? "")));
                }
                finally { host.Close(); }
            }
        }

        private static string ReadVoucherGridUsedCell(CatalogView view, string voucherId)
        {
            if (!view.SelectGiftVoucherRowByIdForTest(voucherId) || view.GridForTest.CurrentRow == null)
                return null;
            object val = view.GridForTest.CurrentRow.Cells["gv_used"].Value;
            return val == null ? "" : Convert.ToString(val);
        }

        private static string TestGiftVoucherEditDialogUi(
            string db, GiftVoucherRepository repo, string presetGroupId, string usedVoucherId, DateTime today, string tag)
        {
            string savedId = null;
            using (var dlg = new GiftVoucherEditDialog(AppSession.Current, null, presetGroupId, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool groupPresetOk = dlg.GroupComboForTest.SelectedId == presetGroupId;
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 7 && AllLabelsFit(dlg.FieldLabelsForTest);
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                    dlg.CodeBoxForTest.Text = "VCDLG-" + tag;
                    dlg.ValueBoxForTest.Value = 0m;
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool blockedZero = dlg.LastErrorForTest == GiftVoucherText.ValueInvalid;

                    dlg.ValueBoxForTest.Value = 200000m;
                    dlg.IssueDateBoxForTest.NullableValue = today;
                    dlg.ExpiryDateBoxForTest.NullableValue = today.AddDays(60);
                    dlg.NoteBoxForTest.Text = "Tạo từ hộp thoại";
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    GiftVoucherRow back = savedId == null ? null : repo.LoadById(savedId);
                    Check("Voucher UI §4 (GiftVoucherEditDialog thêm mới): chọn sẵn nhóm, không cắt nhãn/cuộn dọc 1366×768 @100-150% DPI, chặn giá trị 0, lưu đủ trường",
                        groupPresetOk && labelsOk && noScroll && fits150 && blockedZero
                        && back != null && back.Status == 30 && back.Code == "VCDLG-" + tag
                        && back.Value == 200000m && back.GroupId == presetGroupId && back.Note == "Tạo từ hộp thoại",
                        SingleLine(dlg.LastErrorForTest ?? ("lbl=" + labelsOk + ", fits150=" + fits150)));
                }
                finally { dlg.Close(); }
            }

            using (var dlgUsed = new GiftVoucherEditDialog(AppSession.Current, usedVoucherId, null, false))
            {
                dlgUsed.SuppressPopup = true;
                ShowDialogOffscreen(dlgUsed);
                try
                {
                    bool lockedOk = dlgUsed.IsLockedByBillForTest && dlgUsed.CodeBoxForTest.ReadOnly
                        && !dlgUsed.ValueBoxForTest.Enabled && !dlgUsed.IssueDateBoxForTest.Enabled
                        && !dlgUsed.ExpiryDateBoxForTest.Enabled && !dlgUsed.GroupComboForTest.Enabled
                        && !dlgUsed.PartnerComboForTest.Enabled && !dlgUsed.NoteBoxForTest.ReadOnly;
                    dlgUsed.NoteBoxForTest.Text = "Sửa ghi chú trên dialog OK " + tag;
                    Application.DoEvents();
                    dlgUsed.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    GiftVoucherRow after = repo.LoadById(usedVoucherId);
                    Check("Voucher UI §4 (GiftVoucherEditDialog voucher đã dùng): khóa mọi trường chính, cho phép sửa và lưu ghi chú",
                        lockedOk && after != null && after.Note == "Sửa ghi chú trên dialog OK " + tag,
                        SingleLine(dlgUsed.LastErrorForTest ?? ("locked=" + lockedOk)));
                }
                finally { dlgUsed.Close(); }
            }
            return savedId;
        }

        private static string TestGiftVoucherGroupEditDialogUi(string db, string tag)
        {
            string savedId = null;
            using (var dlg = new GiftVoucherGroupEditDialog(AppSession.Current, null, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 2 && AllLabelsFit(dlg.FieldLabelsForTest);
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;

                    dlg.NameBoxForTest.Text = "Nhóm VC Dialog " + tag;
                    dlg.NoteBoxForTest.Text = "Ghi chú nhóm VC";
                    Application.DoEvents();
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();
                    savedId = dlg.SavedId;

                    GiftVoucherGroupRow back = savedId == null ? null : new GiftVoucherGroupRepository(db).LoadById(savedId);
                    Check("Voucher UI §4 (GiftVoucherGroupEditDialog thêm mới): không cắt nhãn/cuộn dọc 1366×768 @100-150% DPI, lưu nhóm đúng",
                        labelsOk && noScroll && fits150 && back != null && back.Status == 30
                        && back.Name == "Nhóm VC Dialog " + tag && back.Note == "Ghi chú nhóm VC",
                        SingleLine(dlg.LastErrorForTest ?? ("lbl=" + labelsOk + ", fits150=" + fits150)));
                }
                finally { dlg.Close(); }
            }
            return savedId;
        }

        private static void TestGiftVoucherPermissionsMenuAndReadOnly(
            string db, string voucherTableId, string parentName, string sampleVoucherId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add("", GiftVoucherText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_vc_view", IsAdmin = false, DatabasePath = db, Permissions = permsView };
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, voucherTableId, GiftVoucherText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible
                        && !view.RemoveButtonForTest.Visible && !view.GroupBarForTest.Visible;

                    bool vRo = false, vLocked = false, vNoSave = false;
                    view.GiftVoucherDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            vRo = dlg.IsReadOnlyForTest;
                            vLocked = dlg.CodeBoxForTest.ReadOnly && !dlg.ValueBoxForTest.Enabled
                                && !dlg.IssueDateBoxForTest.Enabled && !dlg.ExpiryDateBoxForTest.Enabled
                                && !dlg.GroupComboForTest.Enabled && !dlg.PartnerComboForTest.Enabled && dlg.NoteBoxForTest.ReadOnly;
                            vNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectGiftVoucherRowByIdForTest(sampleVoucherId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    bool grpRo = false, grpLocked = false, grpNoSave = false;
                    view.GiftVoucherGroupDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            grpRo = dlg.IsReadOnlyForTest;
                            grpLocked = dlg.NameBoxForTest.ReadOnly && dlg.NoteBoxForTest.ReadOnly;
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

                    Check("Voucher UI §4 (quyền chỉ Xem): ẩn nút CRUD, nhấp đôi mở hộp thoại chỉ đọc cho cả voucher và nhóm",
                        hiddenCrud && vRo && vLocked && vNoSave && grpRo && grpLocked && grpNoSave,
                        "hidden=" + hiddenCrud + ", vRo=" + vRo + ", grpRo=" + grpRo);
                    host.Close();
                }

                var sess0 = new UserSession { UserId = "u_vc_0", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(sess0).Roots, GiftVoucherText.FunctionName);

                var adminSess = new UserSession { UserId = "u_vc_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
                AppSession.Current = adminSess;
                MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, GiftVoucherText.FunctionName);
                bool openedVoucherMode = false;
                if (adminNode != null)
                {
                    using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                    {
                        var cv = created as CatalogView;
                        openedVoucherMode = cv != null && cv.IsGiftVoucherModeForTest;
                    }
                }
                Check("Voucher UI §4 (menu KHÁCH HÀNG > Danh mục voucher): người không quyền bị ẩn menu, Admin mở đúng CatalogView chế độ voucher",
                    deniedNode == null && adminNode != null && openedVoucherMode,
                    "deniedNull=" + (deniedNode == null) + ", adminFound=" + (adminNode != null) + ", mode=" + openedVoucherMode);
            }
            finally { AppSession.Current = prev; }
        }

        private static void TestGiftVoucherLiveThemeSwitch(string db, string voucherTableId)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new GiftVoucherEditDialog(AppSession.Current, null, null, false))
            using (var grpDlg = new GiftVoucherGroupEditDialog(AppSession.Current, null, null, false))
            {
                var view = new CatalogView(db, voucherTableId, GiftVoucherText.FunctionName) { Dock = DockStyle.Fill };
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
                    bool totalBarOk = view.VoucherTotalValueLabelForTest.ForeColor == tB.Primary;
                    bool dlgColorOk = dlg.BackColor == tB.Background && dlg.ErrorLabelForTest.ForeColor == tB.Danger && dlg.GroupComboForTest.BackColor == tB.Surface;
                    bool grpDlgColorOk = grpDlg.BackColor == tB.Background && grpDlg.ErrorLabelForTest.ForeColor == tB.Danger && grpDlg.NameBoxForTest.BackColor == tB.Surface;

                    Check("Voucher UI §4 (đổi bảng màu trực tiếp): lưới, ô lưới thực tế, cây nhóm, hàng tổng cộng và cả 2 hộp thoại đổi đúng màu ThemeTokens",
                        gridColorOk && treeColorOk && totalBarOk && dlgColorOk && grpDlgColorOk,
                        "grid=" + gridColorOk + ", tree=" + treeColorOk + ", total=" + totalBarOk + ", dlg=" + dlgColorOk + ", grpDlg=" + grpDlgColorOk);
                }
                finally { grpDlg.Close(); dlg.Close(); host.Close(); }
            }
        }

        private static bool AllLabelsFit(List<Label> labels)
        {
            foreach (Label lbl in labels)
                if (TextRenderer.MeasureText(lbl.Text, lbl.Font).Width > lbl.Width)
                    return false;
            return true;
        }
    }
}
