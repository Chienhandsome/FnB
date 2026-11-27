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
    /// <summary>Selftest UI, phân quyền, xuất Excel và menu cho Công nợ ban đầu (spec §4 #8, §5).</summary>
    public static partial class SelfTest
    {
        private static void OpeningDebtUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string custA = DbHelper.NewId();
            string custB = DbHelper.NewId();
            string custZero = DbHelper.NewId();
            string supA = DbHelper.NewId();
            var voucherIds = new List<string>();
            string xlsCustPath = Path.Combine(Path.GetTempPath(), "kazuko-openingdebt-cust-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsSupPath = Path.Combine(Path.GetTempPath(), "kazuko-openingdebt-sup-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cnd')",
                    "@I", storeId, "@N", "CH CND UI " + tag);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '10 Nguyễn Huệ', '090111111', 30, 'selftest_cnd', @T)",
                    "@I", custA, "@C", "CNDKHA-" + tag, "@N", "Đặng Thị Ánh " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '20 Lê Lợi', '090222222', 30, 'selftest_cnd', @T)",
                    "@I", custB, "@C", "CNDKHB-" + tag, "@N", "Phạm Văn Bình " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '30 Hai Bà Trưng', '090333333', 30, 'selftest_cnd', @T)",
                    "@I", custZero, "@C", "CNDKHZ-" + tag, "@N", "Vũ Không Nợ " + tag, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHACUNGCAP (ID, MANHACUNGCAP, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '99 Giải Phóng', '090999999', 30, 'selftest_cnd', @T)",
                    "@I", supA, "@C", "CNDNCCA-" + tag, "@N", "Công Ty Sữa Đà Lạt " + tag, "@T", now);

                var adminSess = new UserSession
                {
                    UserId = "selftest_cnd",
                    UserName = "selftest_cnd",
                    DisplayName = "Admin CND UI",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH CND UI " + tag }
                };

                TestOpeningDebtCustomerUi(adminSess, storeId, custA, custB, custZero, tag, xlsCustPath, voucherIds);
                TestOpeningDebtSupplierUi(adminSess, supA, tag, xlsSupPath);
                TestOpeningDebtPermissionsAndDateLockUi(db, tag);
                TestOpeningDebtMenuAndPreview(db, adminSess);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string vid in voucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    BookingExec(db, "DELETE FROM TCHITIETTHANHTOAN WHERE TTHUCHIID = @I", "@I", vid);
                    SafeDeleteById(db, "TTHUCHI", vid);
                }
                CleanupOpeningDebtPartnerOrders(db, DebtPartyKind.Customer, new[] { custA, custB, custZero });
                CleanupOpeningDebtPartnerOrders(db, DebtPartyKind.Supplier, new[] { supA });
                SafeDeleteById(db, "DKHACHHANG", custA);
                SafeDeleteById(db, "DKHACHHANG", custB);
                SafeDeleteById(db, "DKHACHHANG", custZero);
                SafeDeleteById(db, "DNHACUNGCAP", supA);
                SafeDeleteById(db, "DCUAHANG", storeId);
                SafeDeleteFile(xlsCustPath);
                SafeDeleteFile(xlsSupPath);
            }
        }

        private static void TestOpeningDebtCustomerUi(
            UserSession adminSess, string storeId, string custA, string custB, string custZero,
            string tag, string xlsPath, List<string> createdVouchers)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            using (var host = MakeOffscreenHost())
            {
                var view = new OpeningDebtView(adminSess, DebtPartyKind.Customer) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    bool colsOk = CheckHeadersNoClip(grid, new[] { "Mã khách", "Tên khách hàng", "Địa chỉ", "Điện thoại", "Nợ đầu kỳ" });
                    bool defaultDateOk = view.DateBoxForTest.Value.Date == OpeningDebtRules.DefaultOpeningDate(DateTime.Today);

                    // Cô lập 3 khách seed (+ 1 hàng Tổng cộng = 4 dòng).
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    DataGridViewRow rA = view.FindRowByPartnerIdForTest(custA);
                    DataGridViewRow rB = view.FindRowByPartnerIdForTest(custB);

                    // Nhập nợ âm trên UI -> cảnh báo có tên khách, giữ nguyên 0, 0 DataError.
                    if (rA != null)
                        view.CommitCellEditForTest(rA.Index, -15000m);
                    bool negWarnOk = view.LastWarningMessageForTest != null
                        && view.LastWarningMessageForTest.IndexOf("Đặng Thị Ánh " + tag, StringComparison.Ordinal) >= 0
                        && rA != null && Convert.ToDecimal(rA.Cells["colOpeningDebt"].Value) == 0m;

                    // Nhập số thật qua NumericEditGridCell + nhấn Enter thật -> chuyển sang dòng kế tiếp và không nhảy vào hàng Tổng cộng.
                    bool enterMoveOk = false;
                    if (grid.Rows.Count >= 4)
                    {
                        FocusControlReal(host, grid);
                        TextBox ed0 = BeginGridEditReal(grid, 0, "colOpeningDebt");
                        if (ed0 != null)
                        {
                            TypeCharsIntoGridEditor(ed0, "120000", true);
                            bool e1 = PressEnterOnGridReal(grid);
                            bool movedNext = e1
                                && !grid.IsCurrentCellInEditMode
                                && Convert.ToDecimal(grid.Rows[0].Cells["colOpeningDebt"].Value) == 120000m
                                && grid.CurrentCell != null
                                && grid.CurrentCell.RowIndex == 1
                                && grid.CurrentCell.OwningColumn.Name == "colOpeningDebt";

                            grid.CurrentCell = grid.Rows[2].Cells["colOpeningDebt"];
                            bool eLast = PressEnterOnGridReal(grid);
                            bool stayOffSummary = eLast && grid.CurrentCell != null && grid.CurrentCell.RowIndex == 2;
                            enterMoveOk = movedNext && stayOffSummary;
                        }
                        view.CommitCellEditForTest(0, 0m);
                    }

                    // Nhập A = 500.000, B = 300.000 -> hàng Tổng cộng = 800.000, đậm, đúng màu ThemeTokens + đổi theme sống.
                    if (rA != null && rB != null)
                    {
                        view.CommitCellEditForTest(rA.Index, 500000m);
                        view.CommitCellEditForTest(rB.Index, 300000m);
                        Application.DoEvents();
                    }

                    DataGridViewRow sumRow = grid.Rows.Count >= 4 ? grid.Rows[grid.Rows.Count - 1] : null;
                    bool totalsAndColorOk = sumRow != null
                        && Convert.ToDecimal(sumRow.Cells["colOpeningDebt"].Value) == 800000m
                        && sumRow.Cells["colName"].InheritedStyle.Font.Bold
                        && sumRow.Cells["colName"].InheritedStyle.BackColor == OpeningDebtView.SummaryRowBackColor(t0);

                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    bool liveThemeOk = sumRow != null
                        && OpeningDebtView.SummaryRowBackColor(t1) != OpeningDebtView.SummaryRowBackColor(t0)
                        && sumRow.Cells["colName"].InheritedStyle.BackColor == OpeningDebtView.SummaryRowBackColor(t1)
                        && grid.BackgroundColor == t1.Surface;

                    // Cảnh báo khi đóng còn thay đổi chưa lưu.
                    bool dirtyBeforeSave = view.IsDirtyForTest;
                    view.ConfirmUnsavedOverrideForTest = delegate { return false; };
                    bool blockedCloseWhenDirty = !view.ConfirmClose() && view.UnsavedPromptTriggeredForTest;

                    // Bấm Lưu thật -> ghi DB, hết dirty, 0 DataError.
                    view.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool savedUiOk = view.LastSaveResultForTest != null && view.LastSaveResultForTest.Ok
                        && !view.IsDirtyForTest
                        && view.LastSaveResultForTest.CreatedCount == 2
                        && view.DataErrorCountForTest == 0;

                    // Lọc tích "Chỉ hiện đối tác có nợ đầu" + tìm không dấu "dang thi anh".
                    view.OnlyWithDebtCheckForTest.Checked = true;
                    Application.DoEvents();
                    bool onlyDebtUiOk = view.FindRowByPartnerIdForTest(custA) != null
                        && view.FindRowByPartnerIdForTest(custB) != null
                        && view.FindRowByPartnerIdForTest(custZero) == null;

                    view.SearchBoxForTest.Text = "dang thi anh " + tag;
                    Application.DoEvents();
                    bool accentSearchOk = view.FindRowByPartnerIdForTest(custA) != null
                        && view.FindRowByPartnerIdForTest(custB) == null;

                    // Thu 200.000 vào nợ đầu của A, tải lại màn, thử sửa A xuống 100.000 -> bị chặn trên UI.
                    var debtSvc = new DebtService(adminSess);
                    List<DebtDocumentRow> openBills = debtSvc.LoadOpenCustomerBills(custA);
                    DebtDocumentRow openA = openBills.Find(b => b.Loai == 99);
                    bool uiGuardBelowCollectedOk = false;
                    if (openA != null)
                    {
                        DebtCollectResult colRes = debtSvc.CollectCustomerDebt(new DebtCollectRequest
                        {
                            CustomerId = custA,
                            VoucherNumber = "PT-CNDUI-" + tag,
                            Date = DateTime.Today,
                            StoreId = storeId,
                            Allocations = new List<DebtAllocationLine>
                            {
                                new DebtAllocationLine
                                {
                                    BillId = openA.Id, BillNumber = openA.Number, BillDate = openA.Date,
                                    BillTotal = openA.TotalAmount, BillRemaining = openA.RemainingDebt, PayAmount = 200000m
                                }
                            }
                        });
                        if (colRes.Ok && !string.IsNullOrEmpty(colRes.SavedVoucherId))
                            createdVouchers.Add(colRes.SavedVoucherId);

                        view.ReloadAll(true);
                        Application.DoEvents();
                        DataGridViewRow rAAfter = view.FindRowByPartnerIdForTest(custA);
                        if (rAAfter != null)
                        {
                            view.CommitCellEditForTest(rAAfter.Index, 100000m);
                            uiGuardBelowCollectedOk = view.LastWarningMessageForTest == OpeningDebtConstants.BelowCollectedBlocked("Đặng Thị Ánh " + tag, 200000m)
                                && Convert.ToDecimal(rAAfter.Cells["colOpeningDebt"].Value) == 500000m;
                        }
                    }

                    // Xuất Excel qua nút thật -> đọc lại file .xls đúng 5 cột.
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;
                    bool xlsOk = snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 5
                        && HeadersMatch(snap, VisibleGridHeaders(grid))
                        && XlsSheetContainsSubstring(snap, "CNDKHA-" + tag);

                    Check("Công nợ đầu P2 §4 #8 & §5 (UI KH: 5 cột không cắt chữ, ngày 31/12, chặn âm, Enter chuyển dòng, 0 DataError, màu hàng tổng & đổi theme, cảnh báo đóng chưa lưu, lưu thật, lọc/tìm không dấu, chặn giảm dưới đã thu, xuất Excel)",
                        colsOk && defaultDateOk && negWarnOk && enterMoveOk
                        && totalsAndColorOk && liveThemeOk && dirtyBeforeSave && blockedCloseWhenDirty
                        && savedUiOk && onlyDebtUiOk && accentSearchOk && uiGuardBelowCollectedOk && xlsOk,
                        SingleLine("cols=" + colsOk + ", enter=" + enterMoveOk + ", theme=" + liveThemeOk
                            + ", dirty=" + dirtyBeforeSave + "/" + blockedCloseWhenDirty + ", save=" + savedUiOk
                            + ", filt=" + onlyDebtUiOk + "/" + accentSearchOk + ", guard=" + uiGuardBelowCollectedOk + ", xls=" + xlsOk));
                }
                finally { host.Close(); }
            }
        }

        private static void TestOpeningDebtSupplierUi(UserSession adminSess, string supA, string tag, string xlsPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new OpeningDebtView(adminSess, DebtPartyKind.Supplier) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    bool colsOk = CheckHeadersNoClip(grid, new[] { "Mã NCC", "Tên nhà cung cấp", "Địa chỉ", "Điện thoại", "Nợ đầu kỳ" });

                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    DataGridViewRow rSup = view.FindRowByPartnerIdForTest(supA);
                    if (rSup != null)
                        view.CommitCellEditForTest(rSup.Index, 400000m);

                    view.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool savedOk = view.LastSaveResultForTest != null && view.LastSaveResultForTest.Ok
                        && ReadOpeningDebtAmount(adminSess.DatabasePath, DebtPartyKind.Supplier, supA) == 400000m
                        && view.DataErrorCountForTest == 0;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;
                    bool xlsOk = snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 5
                        && HeadersMatch(snap, VisibleGridHeaders(grid))
                        && XlsSheetContainsSubstring(snap, "CNDNCCA-" + tag);

                    Check("Công nợ đầu P2 §4 #8 & §5 (UI NCC: 5 cột không cắt chữ, lưu thật tạo LOAI=98, xuất Excel đúng 5 cột)",
                        colsOk && savedOk && xlsOk,
                        SingleLine("cols=" + colsOk + ", save=" + savedOk + ", xls=" + xlsOk));
                }
                finally { host.Close(); }
            }
        }

        private static void TestOpeningDebtPermissionsAndDateLockUi(string db, string tag)
        {
            // 1. User chỉ có quyền Xem (cả tên đầy đủ lẫn tên viết thiếu dấu "ban đâu") -> ẩn nút Lưu, lưới ReadOnly, khóa ô Ngày.
            var viewPerms = new FunctionPermissions(false);
            viewPerms.Add("fn_kh", OpeningDebtConstants.CustomerFunctionNameAlt, RoleModes.View);
            viewPerms.Add("fn_ncc", OpeningDebtConstants.SupplierFunctionName, RoleModes.View);
            var viewSess = new UserSession
            {
                UserId = "u_cnd_v_" + tag, UserName = "u_cnd_v", DisplayName = "Chỉ xem CND",
                IsAdmin = false, DatabasePath = db, Permissions = viewPerms
            };

            bool roCustOk, roSupOk;
            using (var host = MakeOffscreenHost())
            {
                var v = new OpeningDebtView(viewSess, DebtPartyKind.Customer) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(v);
                host.Show();
                Application.DoEvents();
                roCustOk = v.IsReadOnlyForTest && !v.SaveButtonForTest.Visible && v.GridForTest.ReadOnly
                    && !v.DateBoxForTest.Enabled && v.DateBoxForTest.Inner.ReadOnly;
                host.Close();
            }
            using (var host = MakeOffscreenHost())
            {
                var v = new OpeningDebtView(viewSess, DebtPartyKind.Supplier) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(v);
                host.Show();
                Application.DoEvents();
                roSupOk = v.IsReadOnlyForTest && !v.SaveButtonForTest.Visible && v.GridForTest.ReadOnly
                    && !v.DateBoxForTest.Enabled && v.DateBoxForTest.Inner.ReadOnly;
                host.Close();
            }

            // 2. Có quyền Sửa nhưng KHÔNG có quyền Thay đổi ngày -> nút Lưu hiện, ô Ngày khóa.
            var editNoDatePerms = new FunctionPermissions(false);
            editNoDatePerms.Add("fn_kh", OpeningDebtConstants.CustomerFunctionName, RoleModes.View | RoleModes.Edit);
            var editNoDateSess = new UserSession
            {
                UserId = "u_cnd_nd_" + tag, UserName = "u_cnd_nd", DisplayName = "Sửa không đổi ngày",
                IsAdmin = false, DatabasePath = db, Permissions = editNoDatePerms
            };
            bool editDateLockedOk;
            using (var host = MakeOffscreenHost())
            {
                var v = new OpeningDebtView(editNoDateSess, DebtPartyKind.Customer) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(v);
                host.Show();
                Application.DoEvents();
                editDateLockedOk = !v.IsReadOnlyForTest && v.SaveButtonForTest.Visible && !v.GridForTest.ReadOnly
                    && !v.DateBoxForTest.Enabled && v.DateBoxForTest.Inner.ReadOnly;
                host.Close();
            }

            // 3. Có thêm quyền Thay đổi ngày -> ô Ngày mở khóa.
            var editDatePerms = new FunctionPermissions(false);
            editDatePerms.Add("fn_kh", OpeningDebtConstants.CustomerFunctionName, RoleModes.View | RoleModes.Edit);
            editDatePerms.Add("fn_dt", OpeningDebtConstants.FunctionDateChange, RoleModes.View);
            var editDateSess = new UserSession
            {
                UserId = "u_cnd_dt_" + tag, UserName = "u_cnd_dt", DisplayName = "Sửa có đổi ngày",
                IsAdmin = false, DatabasePath = db, Permissions = editDatePerms
            };
            bool editDateUnlockedOk;
            using (var host = MakeOffscreenHost())
            {
                var v = new OpeningDebtView(editDateSess, DebtPartyKind.Customer) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(v);
                host.Show();
                Application.DoEvents();
                editDateUnlockedOk = v.DateBoxForTest.Enabled && !v.DateBoxForTest.Inner.ReadOnly;
                host.Close();
            }

            Check("Công nợ đầu P2 §4 #8 (UI phân quyền: chỉ Xem mở chỉ-đọc cả KH & NCC, khóa/mở ô Ngày theo quyền Thay đổi ngày)",
                roCustOk && roSupOk && editDateLockedOk && editDateUnlockedOk,
                SingleLine("ro=" + roCustOk + "/" + roSupOk + ", dtLock=" + editDateLockedOk + ", dtOpen=" + editDateUnlockedOk));
        }

        private static void TestOpeningDebtMenuAndPreview(string db, UserSession adminSess)
        {
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode custNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsOpeningCustomerDebtMenu(n));
            MenuNode supNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsOpeningSupplierDebtMenu(n));

            bool opensCustView = false, opensSupView = false;
            if (custNode != null)
            {
                using (Control v = ScreenFactory.CreateView(custNode, adminSess))
                    opensCustView = v is OpeningDebtView && ((OpeningDebtView)v).PartyKindForTest == DebtPartyKind.Customer;
            }
            if (supNode != null)
            {
                using (Control v = ScreenFactory.CreateView(supNode, adminSess))
                    opensSupView = v is OpeningDebtView && ((OpeningDebtView)v).PartyKindForTest == DebtPartyKind.Supplier;
            }

            // Nhận diện không dấu, không phân biệt hoa thường (kể cả tên gốc thiếu dấu "ban đâu").
            bool matchOk = ScreenFactory.IsOpeningCustomerDebtMenu(new MenuNode { Title = "Công nợ khách hàng ban đâu" })
                && ScreenFactory.IsOpeningCustomerDebtMenu(new MenuNode { Title = "Công nợ khách hàng ban đầu" })
                && ScreenFactory.IsOpeningCustomerDebtMenu(new MenuNode { Title = "cong no khach hang ban dau" })
                && ScreenFactory.IsOpeningCustomerDebtMenu(new MenuNode { Title = "CÔNG NỢ KHÁCH HÀNG BAN ĐẦU" })
                && !ScreenFactory.IsOpeningCustomerDebtMenu(new MenuNode { Title = "Công nợ khách hàng" })
                && ScreenFactory.IsOpeningSupplierDebtMenu(new MenuNode { Title = "Công nợ nhà cung cấp ban đầu" })
                && ScreenFactory.IsOpeningSupplierDebtMenu(new MenuNode { Title = "cong no nha cung cap ban dau" })
                && ScreenFactory.IsOpeningSupplierDebtMenu(new MenuNode { Title = "CÔNG NỢ NHÀ CUNG CẤP BAN ĐẦU" })
                && !ScreenFactory.IsOpeningSupplierDebtMenu(new MenuNode { Title = "Công nợ nhà cung cấp" });

            MenuModel deniedMenu = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_no_cnd", UserName = "u_no_cnd", DisplayName = "Không quyền CND",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            });
            bool deniedConsistent = custNode != null && supNode != null
                && (string.IsNullOrEmpty(custNode.FunctionId) || FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsOpeningCustomerDebtMenu(n)) == null)
                && (string.IsNullOrEmpty(supNode.FunctionId) || FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsOpeningSupplierDebtMenu(n)) == null);

            bool previewOk = false;
            using (Form pvC = ScreenFactory.Create("preview.openingdebtcustomer", adminSess))
            using (Form pvS = ScreenFactory.Create("preview.openingdebtsupplier", adminSess))
            {
                var fc = pvC as OpeningDebtPreviewForm;
                var fs = pvS as OpeningDebtPreviewForm;
                if (fc != null && fs != null)
                {
                    ShowDialogOffscreen(fc);
                    ShowDialogOffscreen(fs);
                    try
                    {
                        fc.Preview("");
                        fs.Preview("");
                        previewOk = fc.ViewForTest != null && fc.ViewForTest.PartyKindForTest == DebtPartyKind.Customer && fc.ViewForTest.GridForTest.Columns.Count == 5
                            && fs.ViewForTest != null && fs.ViewForTest.PartyKindForTest == DebtPartyKind.Supplier && fs.ViewForTest.GridForTest.Columns.Count == 5;
                    }
                    finally
                    {
                        fc.Close();
                        fs.Close();
                    }
                }
            }

            Check("Công nợ đầu P2 §4 #8 (nối 2 menu SMENU kể cả tên thiếu dấu 'ban đâu', mở đúng chế độ KH/NCC & preview.openingdebtcustomer/supplier)",
                custNode != null && supNode != null && opensCustView && opensSupView && matchOk && deniedConsistent && previewOk,
                SingleLine("nodes=" + (custNode != null) + "/" + (supNode != null)
                    + ", views=" + opensCustView + "/" + opensSupView
                    + ", match=" + matchOk + ", denied=" + deniedConsistent + ", pv=" + previewOk));
        }
    }
}
