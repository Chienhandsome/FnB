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
        private static void PromotionUiTests(string db)
        {
            var svc = new PromotionService(AppSession.Current);
            var repo = new PromotionRepository(db);
            var grpRepo = new ProductGroupRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            DateTime today = svc.GetServerDate();

            var seededTypeIds = new List<string>();
            var createdPromoIds = new List<string>();
            string unitId = null, grpParentId = null, grpChildId = null, prodAId = null, prodBId = null;
            string xlsPath = Path.Combine(Path.GetTempPath(), "kazuko-km-export-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                Dictionary<PromotionKind, string> types = EnsureAllPromotionTypesForTest(db, seededTypeIds);

                unitId = DbHelper.NewId();
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_kmui', @T)",
                    "@I", unitId, "@N", "Cốc KM " + tag, "@T", DbHelper.Now());
                string gErr;
                grpParentId = grpRepo.Save(new ProductGroupRow { Name = "Nhóm KM UI Cha " + tag, DrinkTypeId = "1" }, "selftest_kmui", out gErr);
                grpChildId = grpRepo.Save(new ProductGroupRow { Name = "Nhóm KM UI Con " + tag, ParentId = grpParentId, DrinkTypeId = "1" }, "selftest_kmui", out gErr);

                prodAId = DbHelper.NewId();
                prodBId = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @G, @U, '0', 45000, 30, 'selftest_kmui', @T)",
                    "@I", prodAId, "@C", "KMUIA" + tag, "@N", "Cà Phê Sữa KM " + tag, "@G", grpParentId, "@U", unitId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @G, @U, '0', 30000, 30, 'selftest_kmui', @T)",
                    "@I", prodBId, "@C", "KMUIB" + tag, "@N", "Bánh Ngọt KM " + tag, "@G", grpChildId, "@U", unitId, "@T", DbHelper.Now());

                // 4 đợt đại diện đủ 4 trạng thái (Đang áp dụng, Chưa tới, Hết hạn, Ngừng áp dụng).
                string idActive = svc.Save(new PromotionRow
                {
                    Name = "Ưu Đãi Mùa Hè " + tag, TypeId = types[PromotionKind.TotalBillPercent],
                    FromDate = today.AddDays(-1), ToDate = today.AddDays(10), DiscountPercent = 10m, Note = "Đang chạy"
                }, null).SavedId;
                createdPromoIds.Add(idActive);

                string idUpcoming = svc.Save(new PromotionRow
                {
                    Name = "Sắp Mở Bán " + tag, TypeId = types[PromotionKind.ProductPercent],
                    FromDate = today.AddDays(5), ToDate = today.AddDays(15), Note = "Chưa tới"
                }, new[] { new PromotionDetailRow { ProductId = prodAId, DiscountPercent = 15m } }).SavedId;
                createdPromoIds.Add(idUpcoming);

                string idExpired = svc.Save(new PromotionRow
                {
                    Name = "Đợt Cũ Hết Hạn " + tag, TypeId = types[PromotionKind.ProductGroupPercent],
                    FromDate = today.AddDays(-20), ToDate = today.AddDays(-5), Note = "Đã kết thúc"
                }, new[] { new PromotionDetailRow { ProductGroupId = grpParentId, DiscountPercent = 12m } }).SavedId;
                createdPromoIds.Add(idExpired);

                string idStopped = svc.Save(new PromotionRow
                {
                    Name = "Đợt Tạm Dừng " + tag, TypeId = types[PromotionKind.TotalBillPercent],
                    FromDate = today.AddDays(-1), ToDate = today.AddDays(10), StoppedFlag = 30, DiscountPercent = 20m, Note = "Ngừng"
                }, null).SavedId;
                createdPromoIds.Add(idStopped);

                string promoTableId = new CatalogRepository(db).FindIdByTable(PromotionText.TableName);

                TestPromotionGridFilterSearchExportAndColors(
                    db, promoTableId, types[PromotionKind.TotalBillPercent],
                    idActive, idUpcoming, idExpired, idStopped, tag, xlsPath);
                TestPromotionEditDialogUi(db, repo, types, grpChildId, prodAId, prodBId, idUpcoming, today, tag, createdPromoIds);
                TestPromotionPermissionsMenuAndReadOnly(db, promoTableId, idUpcoming);
                TestPromotionLiveThemeSwitch(db, promoTableId, idActive, idStopped);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string pid in createdPromoIds)
                {
                    if (string.IsNullOrEmpty(pid)) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM DDOTKHUYENMAICHITIET WHERE DDOTKHUYENMAIID = @I", "@I", pid);
                    SafeDeleteById(db, "DDOTKHUYENMAI", pid);
                }
                foreach (string tid in seededTypeIds)
                    SafeDeleteById(db, "DLOAIHINHKHUYENMAI", tid);
                if (prodAId != null) SafeDeleteById(db, "DMATHANG", prodAId);
                if (prodBId != null) SafeDeleteById(db, "DMATHANG", prodBId);
                if (grpChildId != null) SafeDeleteById(db, "DNHOMMATHANG", grpChildId);
                if (grpParentId != null) SafeDeleteById(db, "DNHOMMATHANG", grpParentId);
                if (unitId != null) SafeDeleteById(db, "DDONVITINH", unitId);
                SafeDeleteFile(xlsPath);
            }
        }

        private static void TestPromotionGridFilterSearchExportAndColors(
            string db, string promoTableId, string totalBillTypeId,
            string idActive, string idUpcoming, string idExpired, string idStopped, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, promoTableId, PromotionText.FunctionName)
                {
                    Dock = DockStyle.Fill,
                    SuppressPromotionPopupForTest = true
                };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    string[] expected = { "Tên đợt", "Loại hình", "Từ ngày", "Đến ngày", "Trạng thái", "Ghi chú" };
                    bool colsOk = view.IsPromotionModeForTest && grid.Columns.Count == expected.Length;
                    bool noClipHeader = true;
                    for (int i = 0; colsOk && i < expected.Length; i++)
                    {
                        if (grid.Columns[i].HeaderText != expected[i] || !grid.Columns[i].Visible)
                            colsOk = false;
                        if (TextRenderer.MeasureText(grid.Columns[i].HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width > grid.Columns[i].Width)
                            noClipHeader = false;
                    }
                    Check("KM UI §5.5/§7 (lưới 6 cột & tiêu đề): đủ 6 cột đúng thứ tự, tiêu đề cột không cắt chữ",
                        colsOk && noClipHeader && view.PromoFilterBarForTest.Visible,
                        "cols=" + colsOk + ", noClip=" + noClipHeader + ", count=" + grid.Columns.Count);

                    // Kiểm tra chữ và màu chữ THẬT của từng ô trạng thái trên lưới theo ThemeTokens.
                    ThemeTokens cur = ThemeManager.Current;
                    bool cActive = CheckPromoStatusCell(view, idActive, PromotionText.StatusActive, cur.Success);
                    bool cUp = CheckPromoStatusCell(view, idUpcoming, PromotionText.StatusUpcoming, cur.Info);
                    bool cExp = CheckPromoStatusCell(view, idExpired, PromotionText.StatusExpired, cur.TextMuted);
                    bool cStop = CheckPromoStatusCell(view, idStopped, PromotionText.StatusStopped, cur.Danger);
                    Check("KM UI §5.5/§7 (trạng thái & màu ô thật trên lưới): 4 trạng thái hiện đúng chữ và tô đúng màu ThemeTokens",
                        cActive && cUp && cExp && cStop,
                        "act=" + cActive + ", up=" + cUp + ", exp=" + cExp + ", stop=" + cStop);

                    int allRows = view.VisibleRows;

                    // Lọc theo loại hình (Giảm giá tổng bill) -> chỉ còn idActive và idStopped (trong tag).
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    int tagAll = view.VisibleRows;

                    SelectChoiceByKey(view.PromoTypeFilterForTest, totalBillTypeId);
                    Application.DoEvents();
                    int byType = view.VisibleRows;

                    // Kết hợp lọc theo trạng thái Ngừng áp dụng -> còn đúng 1 dòng (idStopped).
                    SelectChoiceByKey(view.PromoStatusFilterForTest, ((int)PromotionStatus.Stopped).ToString());
                    Application.DoEvents();
                    int byTypeAndStatus = view.VisibleRows;

                    // Trả bộ lọc về tất cả rồi tìm không dấu "uu dai mua he".
                    view.PromoTypeFilterForTest.SelectedIndex = 0;
                    view.PromoStatusFilterForTest.SelectedIndex = 0;
                    view.SearchBoxForTest.Text = "uu dai mua he " + tag;
                    Application.DoEvents();
                    int hitAccent = view.VisibleRows;

                    // Xuất Excel qua GridExcelExporter.
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;

                    view.SearchBoxForTest.Text = "";
                    Application.DoEvents();
                    int restored = view.VisibleRows;

                    FocusControlReal(host, grid);
                    Message f3Msg = new Message();
                    bool f3Handled = InvokeProcessCmdKey(view, ref f3Msg, Keys.F3);
                    bool searchFocused = view.SearchBoxForTest.Inner.Focused || view.SearchBoxForTest.ContainsFocus || host.ActiveControl == view.SearchBoxForTest;

                    Check("KM UI §5.5 (lọc loại/trạng thái, tìm không dấu, F3 & xuất Excel GridExcelExporter): lọc đúng, tìm không dấu 1 dòng, xuất .xls đúng cột và dòng lọc",
                        tagAll == 4 && byType == 2 && byTypeAndStatus == 1 && hitAccent == 1 && restored == allRows
                        && f3Handled && searchFocused
                        && snap != null && rec.PromptSaveCount == 1 && snap.RowCount == 2 && snap.ColCount == 6
                        && XlsSheetContainsSubstring(snap, "Ưu Đãi Mùa Hè " + tag),
                        "tag=" + tagAll + ", type=" + byType + ", both=" + byTypeAndStatus + ", accent=" + hitAccent + ", xls=" + (snap == null ? -1 : snap.RowCount));
                }
                finally { host.Close(); }
            }
        }

        private static bool CheckPromoStatusCell(CatalogView view, string promoId, string expectedText, Color expectedColor)
        {
            if (!view.SelectPromotionRowByIdForTest(promoId) || view.GridForTest.CurrentRow == null)
                return false;
            DataGridViewCell cell = view.GridForTest.CurrentRow.Cells["pm_status"];
            string text = cell.Value == null ? "" : Convert.ToString(cell.Value);
            return text == expectedText && cell.InheritedStyle.ForeColor == expectedColor;
        }

        private static void SelectChoiceByKey(ComboBox combo, string key)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var ch = combo.Items[i] as Choice;
                if (ch != null && string.Equals(ch.Key, key, StringComparison.Ordinal))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static void TestPromotionEditDialogUi(
            string db, PromotionRepository repo, Dictionary<PromotionKind, string> types,
            string grpChildId, string prodAId, string prodBId, string existingWithDetailId,
            DateTime today, string tag, List<string> createdPromoIds)
        {
            // 1. Hộp thêm mới: gọn không cuộn dọc 1366x768, đổi loại cập nhật phần dưới cho cả 9 loại, tiêu đề cột lưới chi tiết không cắt chữ.
            using (var dlg = new PromotionEditDialog(AppSession.Current, null, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    bool labelsOk = dlg.FieldLabelsForTest.Count == 5 && AllLabelsFit(dlg.FieldLabelsForTest);
                    bool noScroll = !dlg.VerticalScroll.Visible && !dlg.AutoScroll && dlg.Height <= 728 && dlg.Width <= 1366;

                    bool allKindsSwitchOk = true;
                    foreach (KeyValuePair<PromotionKind, string> kv in types)
                    {
                        SelectPromoTypeInCombo(dlg.TypeComboForTest, kv.Value);
                        Application.DoEvents();
                        if (kv.Key == PromotionKind.TotalBillPercent)
                        {
                            if (!dlg.TotalBillPanelForTest.Visible || dlg.DetailGridPanelForTest.Visible)
                                allKindsSwitchOk = false;
                        }
                        else
                        {
                            if (dlg.TotalBillPanelForTest.Visible || !dlg.DetailGridPanelForTest.Visible || dlg.DetailGridForTest.Columns.Count < 3)
                                allKindsSwitchOk = false;
                            foreach (DataGridViewColumn c in dlg.DetailGridForTest.Columns)
                            {
                                if (TextRenderer.MeasureText(c.HeaderText, dlg.DetailGridForTest.ColumnHeadersDefaultCellStyle.Font).Width > c.Width)
                                    allKindsSwitchOk = false;
                            }
                        }
                    }

                    // Chọn loại Theo nhóm hàng -> thêm dòng qua cây nhóm (hook) -> sửa % -> lưu qua nút Lưu & thoát thật.
                    SelectPromoTypeInCombo(dlg.TypeComboForTest, types[PromotionKind.ProductGroupPercent]);
                    Application.DoEvents();
                    dlg.GroupPickerOpenerForTest = list =>
                    {
                        foreach (ProductGroupRow g in list)
                            if (g.Id == grpChildId) return g;
                        return null;
                    };
                    dlg.NameBoxForTest.Text = "KM Dialog Nhóm " + tag;
                    dlg.FromDateBoxForTest.NullableValue = today.AddDays(40);
                    dlg.ToDateBoxForTest.NullableValue = today.AddDays(50);
                    dlg.NoteBoxForTest.Text = "Tạo qua hộp thoại";
                    dlg.AddLineButtonForTest.PerformClick();
                    Application.DoEvents();
                    dlg.CommitDetailCellEditForTest(0, "col_pct", "18,5");
                    dlg.SaveCloseButtonForTest.PerformClick();
                    Application.DoEvents();

                    string savedId = dlg.SavedId;
                    if (!string.IsNullOrEmpty(savedId))
                        createdPromoIds.Add(savedId);
                    PromotionRow back = savedId == null ? null : repo.LoadById(savedId);
                    List<PromotionDetailRow> backDetails = savedId == null ? new List<PromotionDetailRow>() : repo.LoadDetails(savedId);

                    Check("KM UI §7/§5.1 (PromotionEditDialog bố cục, chuyển 9 loại & thêm dòng lưu thật): không cắt nhãn/cột, không cuộn dọc 1366×768, không DataError, lưu đúng đầu đợt & chi tiết",
                        labelsOk && noScroll && allKindsSwitchOk && dlg.DetailDataErrorCountForTest == 0
                        && back != null && backDetails.Count == 1 && backDetails[0].ProductGroupId == grpChildId && backDetails[0].DiscountPercent == 18.5m,
                        SingleLine(dlg.LastErrorForTest ?? ("lbl=" + labelsOk + ", noScr=" + noScroll + ", kinds=" + allKindsSwitchOk + ", de=" + dlg.DetailDataErrorCountForTest)));
                }
                finally { dlg.Close(); }
            }

            // 2. Mở sửa đợt đã có chi tiết -> combo Loại hình bị khóa (§5.3).
            using (var dlgEdit = new PromotionEditDialog(AppSession.Current, existingWithDetailId, false))
            {
                dlgEdit.SuppressPopup = true;
                ShowDialogOffscreen(dlgEdit);
                try
                {
                    bool typeLocked = !dlgEdit.TypeComboForTest.Enabled && dlgEdit.DetailsForTest.Count == 1;
                    // Test thêm 1 dòng mặt hàng thứ 2 qua nút Thêm mặt hàng (hook), rồi xóa dòng đó qua nút Xóa dòng thật.
                    dlgEdit.ProductPickerOpenerForTest = (list, title) =>
                    {
                        foreach (PromotionProductItem p in list)
                            if (p.Id == prodBId) return p;
                        return null;
                    };
                    dlgEdit.AddLineButtonForTest.PerformClick();
                    Application.DoEvents();
                    int afterAdd = dlgEdit.DetailsForTest.Count;
                    dlgEdit.DeleteLineButtonForTest.PerformClick();
                    Application.DoEvents();
                    int afterDel = dlgEdit.DetailsForTest.Count;

                    Check("KM UI §5.3 (PromotionEditDialog sửa đợt có chi tiết): khóa combo Loại hình, thêm/xóa dòng trên lưới chi tiết hoạt động đúng",
                        typeLocked && afterAdd == 2 && afterDel == 1,
                        "locked=" + typeLocked + ", add=" + afterAdd + ", del=" + afterDel);
                }
                finally { dlgEdit.Close(); }
            }

            // 3. Kiểm hộp chọn cây nhóm (ProductGroupTreePickerDialog) và hộp chọn mặt hàng (PromotionProductPickerDialog).
            var groups = new ProductGroupRepository(db).LoadAll();
            using (var treeDlg = new ProductGroupTreePickerDialog(groups))
            {
                ShowDialogOffscreen(treeDlg);
                try
                {
                    bool hasNodes = treeDlg.TreeForTest.Nodes.Count > 0;
                    Check("KM UI §2 (ProductGroupTreePickerDialog): nạp cây nhóm DNHOMMATHANG thật", hasNodes);
                }
                finally { treeDlg.Close(); }
            }
            var products = repo.LoadProductsForPicker();
            using (var prodDlg = new PromotionProductPickerDialog(products))
            {
                ShowDialogOffscreen(prodDlg);
                try
                {
                    prodDlg.SearchBoxForTest.Text = "ca phe sua km " + tag;
                    Application.DoEvents();
                    bool foundOne = prodDlg.GridForTest.Rows.Count == 1;
                    Check("KM UI §3 (PromotionProductPickerDialog): tìm không dấu lọc đúng mặt hàng", foundOne, "rows=" + prodDlg.GridForTest.Rows.Count);
                }
                finally { prodDlg.Close(); }
            }
        }

        private static void SelectPromoTypeInCombo(ComboBox combo, string typeId)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as PromotionTypeItem;
                if (item != null && string.Equals(item.Id, typeId, StringComparison.Ordinal))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static void TestPromotionPermissionsMenuAndReadOnly(string db, string promoTableId, string samplePromoId)
        {
            UserSession prev = AppSession.Current;
            try
            {
                var permsView = new FunctionPermissions(false);
                permsView.Add("", PromotionText.FunctionName, RoleModes.View);
                AppSession.Current = new UserSession { UserId = "u_km_view", IsAdmin = false, DatabasePath = db, Permissions = permsView };
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, promoTableId, PromotionText.FunctionName) { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();

                    bool hiddenCrud = !view.AddButtonForTest.Visible && !view.EditButtonForTest.Visible && !view.RemoveButtonForTest.Visible;
                    bool roOpened = false, roLocked = false, roNoSave = false;
                    view.PromotionDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = dlg.NameBoxForTest.ReadOnly && !dlg.TypeComboForTest.Enabled
                                && !dlg.FromDateBoxForTest.Enabled && !dlg.ToDateBoxForTest.Enabled
                                && !dlg.StoppedCheckForTest.Enabled && dlg.NoteBoxForTest.ReadOnly
                                && !dlg.HeaderPercentBoxForTest.Enabled && dlg.DetailGridForTest.ReadOnly
                                && !dlg.AddLineButtonForTest.Visible && !dlg.DeleteLineButtonForTest.Visible;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };
                    if (view.SelectPromotionRowByIdForTest(samplePromoId) && view.GridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.GridForTest, 0, view.GridForTest.CurrentRow.Index);

                    Check("KM UI §5.5 (quyền chỉ Xem): ẩn nút Thêm/Sửa/Xóa, nhấp đôi mở hộp thoại chỉ đọc (khóa mọi ô, ẩn nút Lưu)",
                        hiddenCrud && roOpened && roLocked && roNoSave,
                        "hidden=" + hiddenCrud + ", ro=" + roOpened + ", locked=" + roLocked + ", noSave=" + roNoSave);
                    host.Close();
                }

                var sess0 = new UserSession { UserId = "u_km_0", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
                MenuNode deniedNode = FindMenuLeafByTitle(MainMenuBuilder.Build(sess0).Roots, PromotionText.FunctionName);

                var adminSess = new UserSession { UserId = "u_km_adm", IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true) };
                AppSession.Current = adminSess;
                MenuNode adminNode = FindMenuLeafByTitle(MainMenuBuilder.Build(adminSess).Roots, PromotionText.FunctionName);
                bool openedPromoMode = false;
                if (adminNode != null)
                {
                    using (Control created = ScreenFactory.CreateView(adminNode, adminSess))
                    {
                        var cv = created as CatalogView;
                        openedPromoMode = cv != null && cv.IsPromotionModeForTest;
                    }
                }
                Check("KM UI §5.5 (menu KHÁCH HÀNG > Danh mục đợt khuyến mại): không quyền ẩn menu; Admin mở đúng CatalogView chế độ đợt khuyến mại",
                    deniedNode == null && adminNode != null && openedPromoMode,
                    "deniedNull=" + (deniedNode == null) + ", adminFound=" + (adminNode != null) + ", mode=" + openedPromoMode);
            }
            finally { AppSession.Current = prev; }
        }

        private static void TestPromotionLiveThemeSwitch(string db, string promoTableId, string idActive, string idStopped)
        {
            var themes = new List<ThemeTokens>(BuiltInThemes.All());
            ThemeTokens tA = themes[0];
            ThemeTokens tB = themes.Count > 1 ? themes[1] : themes[0];

            ThemeManager.Apply(tA);
            using (var host = MakeOffscreenHost())
            using (var dlg = new PromotionEditDialog(AppSession.Current, null, false))
            {
                var view = new CatalogView(db, promoTableId, PromotionText.FunctionName) { Dock = DockStyle.Fill };
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
                    bool gridOk = g.BackgroundColor == tB.Surface && g.DefaultCellStyle.SelectionBackColor == tB.Primary;
                    bool statusColorsOk = CheckPromoStatusCell(view, idActive, PromotionText.StatusActive, tB.Success)
                        && CheckPromoStatusCell(view, idStopped, PromotionText.StatusStopped, tB.Danger);
                    bool filterBarOk = view.PromoFilterBarForTest.BackColor == tB.Background
                        && view.PromoTypeFilterForTest.BackColor == tB.Surface;
                    bool dlgOk = dlg.BackColor == tB.Background
                        && dlg.ErrorLabelForTest.ForeColor == tB.Danger
                        && dlg.TypeComboForTest.BackColor == tB.Surface;

                    Check("KM UI §7 (đổi bảng màu trực tiếp): lưới, màu ô trạng thái thật, thanh lọc và PromotionEditDialog đổi đúng ThemeTokens",
                        gridOk && statusColorsOk && filterBarOk && dlgOk,
                        "grid=" + gridOk + ", statusColors=" + statusColorsOk + ", filter=" + filterBarOk + ", dlg=" + dlgOk);
                }
                finally { dlg.Close(); host.Close(); }
            }
        }
    }
}
