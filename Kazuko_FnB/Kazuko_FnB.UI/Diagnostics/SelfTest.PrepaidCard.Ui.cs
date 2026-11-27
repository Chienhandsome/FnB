using System;
using System.Collections.Generic;
using System.Drawing;
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
        private static void PrepaidCardUiTests(string db)
        {
            var cardSvc = new PrepaidCardService(AppSession.Current);
            var grpSvc = new PrepaidCardGroupService(AppSession.Current);
            var pcVoucherSvc = new PrepaidCardVoucherService(AppSession.Current);
            string tag = DbHelper.NewId().Substring(0, 8);
            DateTime today = cardSvc.GetServerDate();

            string storeId = DbHelper.NewId(), incReasonId = DbHelper.NewId(), expReasonId = DbHelper.NewId();
            string parentId = null, childId = null, grpDlgSavedId = null;
            string card1Id = null, card2Id = null, cardLockedId = null, dlgCardId = null;
            string cust1Id = null, cust2Id = null, bill1Id = null;
            var createdVoucherIds = new List<string>();
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-ttt-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_ttt')", "@I", storeId, "@N", "CH UI TTT " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 30, -1, 30, 'selftest_ttt')", "@I", incReasonId, "@N", "Nạp thẻ UI " + tag);
                BookingExec(db, "INSERT INTO DLYDOTHUCHI (ID, NAME, LALYDOTHU, LOAILYDO, STATUS, USERCREATEDID) VALUES (@I, @N, 0, -1, 30, 'selftest_ttt')", "@I", expReasonId, "@N", "Chi trả UI " + tag);
                AppSession.Current.CurrentStore = new StoreRecord { Id = storeId, Name = "CH UI TTT " + tag };

                var parent = PrepaidCardGroupRow.NewDefault(); parent.Name = "Nhóm TTT Cha UI " + tag;
                parentId = grpSvc.Save(parent).SavedId;
                var child = PrepaidCardGroupRow.NewDefault(); child.Name = "Nhóm TTT Con UI " + tag; child.ParentId = parentId;
                childId = grpSvc.Save(child).SavedId;

                card1Id = cardSvc.Save(new PrepaidCardRow { Code = "TTTUI1-" + tag, GroupId = parentId, ExpiryDate = today.AddDays(30), Note = "Thẻ khách VIP đặc biệt " + tag }).SavedId;
                card2Id = cardSvc.Save(new PrepaidCardRow { Code = "TTTUI2-" + tag, GroupId = childId, Note = "Thẻ chưa nạp" }).SavedId;
                cardLockedId = cardSvc.Save(new PrepaidCardRow { Code = "TTTUIL-" + tag, GroupId = childId, Locked = true, ExpiryDate = today.AddDays(-1), Note = "Thẻ đã khóa" }).SavedId;

                cust1Id = DbHelper.NewId(); cust2Id = DbHelper.NewId();
                const string insKhSql = "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DTHETRATRUOCID, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @M, @N, @A, @C, 30, 'selftest_ttt', @T)";
                BookingExec(db, insKhSql, "@I", cust1Id, "@M", "KHT1" + tag, "@N", "Nguyễn Ánh " + tag, "@A", "10 Lê Lợi", "@C", card1Id, "@T", now);
                BookingExec(db, insKhSql, "@I", cust2Id, "@M", "KHT2" + tag, "@N", "Trần Bình " + tag, "@A", "20 Lê Lợi", "@C", card1Id, "@T", now);

                VoucherSaveResult vSeed = pcVoucherSvc.Save(new VoucherRow
                {
                    Name = pcVoucherSvc.GenerateNumber(VoucherKind.Income), Date = today, Kind = VoucherKind.Income,
                    PrepaidCardId = card1Id, StoreId = storeId, ReasonId = incReasonId, Amount = 1000000m, Transfer = false
                });
                if (vSeed.Ok) createdVoucherIds.Add(vSeed.SavedId);

                bill1Id = DbHelper.NewId();
                BookingExec(db, "INSERT INTO TDONHANG (ID, NAME, NGAY, LOAI, TONGCONG, THETRATRUOC, DTHETRATRUOCID, DATHANHTOAN, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @D, 0, 300000, 300000, @C, 30, 30, 'selftest_ttt', @T)",
                    "@I", bill1Id, "@N", "HD-UI-TTT-" + tag, "@D", today, "@C", card1Id, "@T", now);

                MenuNode realMenuNode = TestPrepaidCardMenuConfigVisibility(db);
                TestPrepaidCardCatalogGridFilterTreeAndExport(db, realMenuNode, parent.Name, child.Name, card1Id, tag, xlsPath);
                dlgCardId = TestPrepaidCardEditDialogAndTransactionsUi(storeId, incReasonId, expReasonId, parentId, card1Id, cardLockedId, tag, createdVoucherIds);
                grpDlgSavedId = TestPrepaidCardGroupDialogUi(db, tag);
                TestPrepaidCardReadOnlyPermissionAndThemeUi(db, card1Id);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string vid in createdVoucherIds) { BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid); SafeDeleteById(db, "TTHUCHI", vid); }
                if (bill1Id != null) SafeDeleteById(db, "TDONHANG", bill1Id);
                foreach (string kh in new[] { cust1Id, cust2Id }) if (kh != null) SafeDeleteById(db, "DKHACHHANG", kh);
                foreach (string cid in new[] { dlgCardId, card1Id, card2Id, cardLockedId }) if (cid != null) { BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", cid); SafeDeleteById(db, "DTHETRATRUOC", cid); }
                foreach (string gid in new[] { grpDlgSavedId, childId, parentId }) if (gid != null) { BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", gid); SafeDeleteById(db, "DNHOMTHETRATRUOC", gid); }
                SafeDeleteById(db, "DLYDOTHUCHI", incReasonId);
                SafeDeleteById(db, "DLYDOTHUCHI", expReasonId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static MenuNode TestPrepaidCardMenuConfigVisibility(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { PrepaidCardText.ConfigKey });
            UserSession prev = AppSession.Current;
            MenuNode nodeWhen30 = null;
            try
            {
                var adminSess = new UserSession { UserId = "u_ttt_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
                var deniedSess = new UserSession { UserId = "u_ttt_0", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                AppSession.Current = adminSess;

                SetOpeningConfigInt(db, cfgRepo, cfgBackups, PrepaidCardText.ConfigKey, 0);
                MenuNode node0 = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, PrepaidCardText.FunctionName);

                SetOpeningConfigInt(db, cfgRepo, cfgBackups, PrepaidCardText.ConfigKey, 15);
                MenuNode node15 = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, PrepaidCardText.FunctionName);

                SetOpeningConfigInt(db, cfgRepo, cfgBackups, PrepaidCardText.ConfigKey, 30);
                nodeWhen30 = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, PrepaidCardText.FunctionName);
                MenuNode deniedWhen30 = FindMenuLeafByTitle(MainMenuBuilder.Build(deniedSess).Roots, PrepaidCardText.FunctionName);

                bool openedRealMode = false;
                if (nodeWhen30 != null)
                {
                    using (Control created = ScreenFactory.CreateView(nodeWhen30, adminSess))
                    {
                        var cv = created as CatalogView;
                        openedRealMode = cv != null && cv.IsPrepaidCardModeForTest;
                    }
                }

                bool routeNoAccent = ScreenFactory.IsPrepaidCardCatalogMenu(new MenuNode { Title = "danh muc the tra truoc" }, adminSess)
                    && ScreenFactory.IsPrepaidCardCatalogMenu(new MenuNode { Title = "DANH MỤC THẺ TRẢ TRƯỚC" }, adminSess)
                    && !ScreenFactory.IsPrepaidCardCatalogMenu(new MenuNode { Title = "Danh mục voucher" }, adminSess);

                Check("Thẻ trả trước §5 #10 (menu KHÁCH HÀNG > Danh mục thẻ trả trước & SuDungTheTraTruoc): 0/15 ẩn, 30 hiện & mở đúng CatalogView, không quyền ẩn, nhận diện không dấu",
                    node0 == null && node15 == null && nodeWhen30 != null && deniedWhen30 == null && openedRealMode && routeNoAccent,
                    SingleLine("n0=" + (node0 == null) + ", n15=" + (node15 == null) + ", n30=" + (nodeWhen30 != null)
                        + ", denied=" + (deniedWhen30 == null) + ", mode=" + openedRealMode + ", route=" + routeNoAccent));
            }
            finally
            {
                AppSession.Current = prev;
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
            }
            return nodeWhen30;
        }

        private static void TestPrepaidCardCatalogGridFilterTreeAndExport(
            string db, MenuNode menuNode, string parentName, string childName,
            string card1Id, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                Control created = menuNode != null
                    ? ScreenFactory.CreateView(menuNode, AppSession.Current)
                    : new CatalogView(db, new CatalogRepository(db).FindIdByTable(PrepaidCardText.TableName), PrepaidCardText.FunctionName);
                var view = (CatalogView)created;
                view.Dock = DockStyle.Fill;
                view.SuppressPrepaidCardPopupForTest = true;
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expected = { "Mã thẻ", "Nhóm thẻ", "Khách đang giữ thẻ", "Ngày hết hạn", "Khóa", "Tổng nạp", "Tổng sử dụng", "Còn lại", "Ghi chú" };
                    bool colsOk = view.IsPrepaidCardModeForTest && grid.Columns.Count == expected.Length;
                    bool noClipHeader = true;
                    for (int i = 0; colsOk && i < expected.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expected[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        if (TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width > grid.Columns[i].Width)
                            noClipHeader = false;
                    }

                    bool selParent = view.SelectCategory(parentName);
                    Application.DoEvents();
                    int parentRows = view.VisibleRows; // card1 + card2 + cardLocked = 3
                    decimal pLoaded = view.PrepaidTotalLoadedForTest;
                    decimal pUsed = view.PrepaidTotalUsedForTest;
                    decimal pRem = view.PrepaidTotalRemainingForTest;

                    view.SelectPrepaidCardRowByIdForTest(card1Id);
                    string holdersText = grid.CurrentRow == null ? "" : Convert.ToString(grid.CurrentRow.Cells["pc_holders"].Value);
                    bool holdersJoined = holdersText.IndexOf("Nguyễn Ánh " + tag, StringComparison.Ordinal) >= 0
                        && holdersText.IndexOf("Trần Bình " + tag, StringComparison.Ordinal) >= 0;

                    view.PrepaidHasBalanceCheckForTest.Checked = true;
                    Application.DoEvents();
                    int hasBalRows = view.VisibleRows;

                    view.PrepaidHasBalanceCheckForTest.Checked = false;
                    view.PrepaidUnlockedOnlyCheckForTest.Checked = true;
                    Application.DoEvents();
                    int unlockedRows = view.VisibleRows;

                    view.PrepaidUnlockedOnlyCheckForTest.Checked = false;
                    bool selChild = view.SelectCategory(childName);
                    Application.DoEvents();
                    int childRows = view.VisibleRows;

                    view.SelectCategory("Tất cả");
                    view.SearchBoxForTest.Text = "nguyen anh " + tag;
                    Application.DoEvents();
                    int hitSearch = view.VisibleRows;

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
                    bool searchFocused = view.SearchBoxForTest.Inner.Focused || view.SearchBoxForTest.ContainsFocus;

                    Check("Thẻ trả trước §5 #2/#8 (UI mở bằng menu thật, lưới 9 cột, Khách giữ thẻ nối tên, hàng tổng 3 cột tiền, cây cha/con, lọc còn tiền/chưa khóa, tìm không dấu & xuất Excel): đạt",
                        colsOk && noClipHeader && view.PrepaidTotalBarForTest.Visible
                        && selParent && parentRows == 3 && pLoaded == 1000000m && pUsed == 300000m && pRem == 700000m
                        && holdersJoined && hasBalRows == 1 && unlockedRows == 2 && selChild && childRows == 2
                        && hitSearch == 1 && snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 9 && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "TTTUI1-" + tag) && f3Handled && searchFocused,
                        SingleLine("cols=" + colsOk + ", clip=" + noClipHeader + ", pRows=" + parentRows
                            + ", sum=" + pLoaded + "/" + pUsed + "/" + pRem + ", holders=" + holdersJoined
                            + ", hasBal=" + hasBalRows + ", unlk=" + unlockedRows + ", ch=" + childRows
                            + ", srch=" + hitSearch + ", xls=" + (snap == null ? -1 : snap.RowCount) + "x" + (snap == null ? -1 : snap.ColCount)));
                }
                finally { host.Close(); }
            }
        }

        private static string SaveCardVoucherInDialog(
            PhieuThuDialog vDlg, string storeId, string reasonId, decimal amount, List<string> createdIds)
        {
            vDlg.SuppressPopup = true;
            ShowDialogOffscreen(vDlg);
            try
            {
                vDlg.StoreComboForTest.SelectId(storeId);
                vDlg.ReasonComboForTest.SelectId(reasonId);
                vDlg.AmountBoxForTest.Value = amount;
                vDlg.SaveButtonForTest.PerformClick();
                if (!string.IsNullOrEmpty(vDlg.SavedId))
                    createdIds.Add(vDlg.SavedId);
                return vDlg.SavedId;
            }
            finally { if (!vDlg.IsDisposed) vDlg.Close(); }
        }

        private static string TestPrepaidCardEditDialogAndTransactionsUi(
            string storeId, string incReasonId, string expReasonId,
            string presetGroupId, string card1Id, string cardLockedId, string tag, List<string> createdVoucherIds)
        {
            string savedCardId = null;

            // 1. Hộp thẻ mới: không cuộn dọc 1366x768 @150% DPI, 2 tab, 3 ô tổng 1 hàng, nút Nạp/Chi tắt khi chưa lưu, bật sau khi lưu.
            using (var dlg = new PrepaidCardEditDialog(AppSession.Current, null, presetGroupId, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    dlg.UpdateTransactionButtons();
                    bool labelsOk = AllLabelsFit(dlg.FieldLabelsForTest);
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;
                    float scale = dlg.DeviceDpi / 96f;
                    bool fits150 = (dlg.ClientSize.Height / scale) * 1.5f <= 700f && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;
                    bool oneRowSummary = dlg.TotalLoadedBoxForTest.Top == dlg.TotalUsedBoxForTest.Top
                        && dlg.TotalUsedBoxForTest.Top == dlg.RemainingBoxForTest.Top;
                    bool twoTabs = dlg.TabsForTest.TabPages.Count == 2;
                    bool disabledBeforeSave = !dlg.TopUpButtonForTest.Enabled && !dlg.WithdrawButtonForTest.Enabled;

                    dlg.CodeBoxForTest.Text = "TTTDLG-" + tag;
                    dlg.NoteBoxForTest.Text = "Tạo qua hộp thẻ";
                    dlg.SaveButtonForTest.PerformClick();
                    dlg.UpdateTransactionButtons();
                    savedCardId = dlg.SavedId;
                    bool enabledAfterSave = !string.IsNullOrEmpty(savedCardId)
                        && dlg.TopUpButtonForTest.Enabled && dlg.WithdrawButtonForTest.Enabled;

                    // Nạp tiền 500.000 & Chi trả 150.000 qua PhieuThuDialog thật -> Tổng nạp 350.000, Còn lại 350.000.
                    bool ptModeOk = false;
                    dlg.VoucherDialogOpenerForTest = vDlg =>
                    {
                        ptModeOk = vDlg.IsPrepaidCardModeForTest && vDlg.KindForTest == VoucherKind.Income
                            && vDlg.DescriptionBoxForTest.Text.IndexOf("TTTDLG-" + tag, StringComparison.Ordinal) >= 0;
                        SaveCardVoucherInDialog(vDlg, storeId, incReasonId, 500000m, createdVoucherIds);
                        return DialogResult.OK;
                    };
                    dlg.TopUpButtonForTest.PerformClick();

                    string wdId = null;
                    dlg.VoucherDialogOpenerForTest = vDlg =>
                    {
                        wdId = SaveCardVoucherInDialog(vDlg, storeId, expReasonId, 150000m, createdVoucherIds);
                        return DialogResult.OK;
                    };
                    dlg.WithdrawButtonForTest.PerformClick();
                    dlg.UpdateTransactionButtons();

                    bool summaryAfterTx = dlg.TotalLoadedBoxForTest.Text == "350.000"
                        && dlg.TotalUsedBoxForTest.Text == "0"
                        && dlg.RemainingBoxForTest.Text == "350.000"
                        && dlg.VoucherGridForTest.Rows.Count == 2;

                    // Xóa phiếu chi trả ngay trên tab Lịch sử nạp tiền -> số dư quay lại 500.000.
                    bool selWd = dlg.SelectVoucherByIdForTest(wdId);
                    dlg.ConfirmDeleteVoucherOverrideForTest = msg => true;
                    dlg.DeleteVoucherButtonForTest.PerformClick();
                    bool afterDelWd = selWd && dlg.RemainingBoxForTest.Text == "500.000" && dlg.VoucherGridForTest.Rows.Count == 1;

                    Check("Thẻ trả trước §5 #3/#5/#6 & §6 (hộp thẻ 1366×768 @150% DPI không cuộn dọc, 2 tab, 3 ô tổng 1 hàng, Nạp/Chi/Xóa qua PhieuThuDialog thật): đạt",
                        labelsOk && noScroll && fits150 && oneRowSummary && twoTabs
                        && disabledBeforeSave && enabledAfterSave && ptModeOk && summaryAfterTx && afterDelWd,
                        SingleLine("lbl=" + labelsOk + ", noScr=" + noScroll + ", f150=" + fits150
                            + ", 1row=" + oneRowSummary + ", preDis=" + disabledBeforeSave + ", postEn=" + enabledAfterSave
                            + ", pt=" + ptModeOk + ", tx=" + summaryAfterTx + ", del=" + afterDelWd));
                }
                finally { dlg.Close(); }
            }

            // 2. Thẻ đã khóa & hết hạn: Nạp tiền hiện cảnh báo xác nhận (từ chối -> không mở; đồng ý -> mở được).
            using (var dlgLock = new PrepaidCardEditDialog(AppSession.Current, cardLockedId, null, false))
            {
                dlgLock.SuppressPopup = true;
                ShowDialogOffscreen(dlgLock);
                try
                {
                    bool rejectOpened = false, acceptOpened = false;
                    dlgLock.ConfirmLockedExpiredOverrideForTest = msg => false;
                    dlgLock.VoucherDialogOpenerForTest = vDlg => { rejectOpened = true; return DialogResult.Cancel; };
                    dlgLock.TopUpButtonForTest.PerformClick();
                    string warnMsg = dlgLock.LastConfirmPromptForTest;

                    dlgLock.ConfirmLockedExpiredOverrideForTest = msg => true;
                    dlgLock.VoucherDialogOpenerForTest = vDlg => { acceptOpened = vDlg.IsPrepaidCardModeForTest; return DialogResult.Cancel; };
                    dlgLock.TopUpButtonForTest.PerformClick();

                    Check("Thẻ trả trước §5 #9 (UI cảnh báo xác nhận khi nạp thẻ khóa/hết hạn): từ chối không mở hộp phiếu, đồng ý cho mở",
                        !string.IsNullOrEmpty(warnMsg) && !rejectOpened && acceptOpened,
                        SingleLine("warn=" + SingleLine(warnMsg) + ", rej=" + rejectOpened + ", acc=" + acceptOpened));
                }
                finally { dlgLock.Close(); }
            }

            // 3. Hộp thẻ đã có lịch sử sử dụng: kiểm cả 2 tab nạp & sử dụng hiển thị đúng số.
            using (var dlgHist = new PrepaidCardEditDialog(AppSession.Current, card1Id, null, false))
            {
                dlgHist.SuppressPopup = true;
                ShowDialogOffscreen(dlgHist);
                try
                {
                    bool histOk = dlgHist.TotalLoadedBoxForTest.Text == "1.000.000"
                        && dlgHist.TotalUsedBoxForTest.Text == "300.000"
                        && dlgHist.RemainingBoxForTest.Text == "700.000"
                        && dlgHist.VoucherGridForTest.Rows.Count == 1
                        && dlgHist.UsageGridForTest.Rows.Count == 1;
                    Check("Thẻ trả trước §5 #4/#8 (hộp thẻ tab Lịch sử nạp tiền & Lịch sử sử dụng): nạp 1.000.000, dùng 300.000, còn lại 700.000",
                        histOk,
                        SingleLine("boxes=" + dlgHist.TotalLoadedBoxForTest.Text + "/" + dlgHist.TotalUsedBoxForTest.Text + "/" + dlgHist.RemainingBoxForTest.Text
                            + ", vRows=" + dlgHist.VoucherGridForTest.Rows.Count + ", uRows=" + dlgHist.UsageGridForTest.Rows.Count));
                }
                finally { dlgHist.Close(); }
            }

            return savedCardId;
        }

        private static string TestPrepaidCardGroupDialogUi(string db, string tag)
        {
            string savedId = null;
            using (var dlg = new PrepaidCardGroupEditDialog(AppSession.Current, null, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 2 && AllLabelsFit(dlg.FieldLabelsForTest);
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll;
                    dlg.NameBoxForTest.Text = "Nhóm TTT Dialog " + tag;
                    dlg.NoteBoxForTest.Text = "Ghi chú nhóm TTT";
                    dlg.SaveCloseButtonForTest.PerformClick();
                    savedId = dlg.SavedId;
                    PrepaidCardGroupRow back = savedId == null ? null : new PrepaidCardGroupRepository(db).LoadById(savedId);
                    Check("Thẻ trả trước §5 #2 (PrepaidCardGroupEditDialog): không cắt nhãn/cuộn dọc, lưu nhóm đúng",
                        labelsOk && noScroll && back != null && back.Name == "Nhóm TTT Dialog " + tag,
                        SingleLine("lbl=" + labelsOk + ", noScr=" + noScroll + ", saved=" + (back != null)));
                }
                finally { dlg.Close(); }
            }
            return savedId;
        }

        private static void TestPrepaidCardReadOnlyPermissionAndThemeUi(string db, string sampleCardId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add(PrepaidCardText.FunctionId, PrepaidCardText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_ttt_ro_ui", IsAdmin = false, DatabasePath = db, Permissions = permsView };

                string tableId = new CatalogRepository(db).FindIdByTable(PrepaidCardText.TableName);
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, tableId, PrepaidCardText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible
                        && !view.RemoveButtonForTest.Visible && !view.GroupBarForTest.Visible;

                    bool roFlag = false, fieldsLocked = false, txDisabled = false, saveHidden = false;
                    view.PrepaidCardDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            dlg.UpdateTransactionButtons();
                            roFlag = dlg.IsReadOnlyForTest;
                            fieldsLocked = dlg.CodeBoxForTest.ReadOnly && !dlg.GroupComboForTest.Enabled
                                && !dlg.ExpiryDateBoxForTest.Enabled && !dlg.LockedCheckForTest.Enabled && dlg.NoteBoxForTest.ReadOnly;
                            txDisabled = !dlg.TopUpButtonForTest.Enabled && !dlg.WithdrawButtonForTest.Enabled
                                && !dlg.EditVoucherButtonForTest.Enabled && !dlg.DeleteVoucherButtonForTest.Enabled;
                            saveHidden = !dlg.SaveButtonForTest.Visible && !dlg.SaveCloseButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectPrepaidCardRowByIdForTest(sampleCardId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    Check("Thẻ trả trước §5 #10 (UI quyền chỉ Xem): ẩn CRUD, nhấp đôi mở hộp chỉ đọc, khóa ô & vô hiệu Nạp/Chi/Sửa/Xóa",
                        hiddenCrud && roFlag && fieldsLocked && txDisabled && saveHidden,
                        SingleLine("hid=" + hiddenCrud + ", ro=" + roFlag + ", fld=" + fieldsLocked + ", txDis=" + txDisabled + ", noSave=" + saveHidden));
                    host.Close();
                }

                // Kiểm đổi bảng màu trực tiếp (kiểm màu thật của ô lưới, cây nhóm, hàng tổng và hộp thẻ).
                AppSession.Current = prev;
                var themes = new List<ThemeTokens>(BuiltInThemes.All());
                ThemeTokens tA = themes[0];
                ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];
                ThemeManager.Apply(tA);

                using (var host = MakeOffscreenHost())
                using (var dlg = new PrepaidCardEditDialog(AppSession.Current, sampleCardId, null, false))
                {
                    var view = new CatalogView(db, tableId, PrepaidCardText.FunctionName) { Dock = DockStyle.Fill };
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
                        bool treeOk = view.TreeForTest.BackColor == tB.Surface && view.TreeForTest.ForeColor == tB.Text;
                        bool totalOk = view.PrepaidTotalValuesLabelForTest.ForeColor == tB.Primary;
                        bool dlgOk = dlg.BackColor == tB.Background && dlg.ErrorLabelForTest.ForeColor == tB.Danger
                            && dlg.RemainingBoxForTest.ForeColor == tB.Primary;

                        Check("Thẻ trả trước §6 (đổi bảng màu trực tiếp): màu thật ô lưới, cây nhóm, hàng tổng và hộp thẻ đổi đúng ThemeTokens",
                            cellColorOk && treeOk && totalOk && dlgOk,
                            SingleLine("cell=" + cellColorOk + ", tree=" + treeOk + ", total=" + totalOk + ", dlg=" + dlgOk));
                    }
                    finally { dlg.Close(); host.Close(); }
                }
            }
            finally { AppSession.Current = prev; }
        }
    }
}
