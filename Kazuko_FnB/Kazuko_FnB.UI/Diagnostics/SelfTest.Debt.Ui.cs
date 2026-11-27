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
        private static void DebtUiAndMenuTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string bankId = DbHelper.NewId();
            string grpParentId = null;
            string grpChildId = null;
            string custA = DbHelper.NewId();
            string custZero = DbHelper.NewId();
            string supA = DbHelper.NewId();
            var orderIds = new List<string>();
            var voucherIds = new List<string>();
            string xlsCustPath = Path.Combine(Path.GetTempPath(), "kazuko-debt-cust-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsSupPath = Path.Combine(Path.GetTempPath(), "kazuko-debt-sup-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cn')",
                    "@I", storeId, "@N", "CH CN UI " + tag);
                BookingExec(db, "INSERT INTO DTAIKHOANNGANHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_cn')",
                    "@I", bankId, "@N", "TK CN UI " + tag);

                var grpRepo = new CustomerGroupRepository(db);
                grpParentId = grpRepo.Save(new CustomerGroupRow { Name = "Nhóm Cha CN " + tag }, "selftest_cn").SavedId;
                grpChildId = grpRepo.Save(new CustomerGroupRow { Name = "Nhóm Con CN " + tag, ParentId = grpParentId }, "selftest_cn").SavedId;

                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DNHOMKHACHHANGID, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @G, '10 Phố Huế', '0912345678', 30, 'selftest_cn', @T)",
                    "@I", custA, "@C", "KHUIA" + tag, "@N", "Đặng Thị Công Nợ " + tag, "@G", grpChildId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DNHOMKHACHHANGID, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @G, '20 Phố Huế', '0987654321', 30, 'selftest_cn', @T)",
                    "@I", custZero, "@C", "KHUIZ" + tag, "@N", "Khách Hết Nợ " + tag, "@G", grpParentId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DNHACUNGCAP (ID, MANHACUNGCAP, NAME, DIACHI, DIENTHOAI, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '99 Giải Phóng', '0911223344', 30, 'selftest_cn', @T)",
                    "@I", supA, "@C", "NCCUIA" + tag, "@N", "NCC Trả Nợ UI " + tag, "@T", now);

                // custA có 2 bill còn nợ 200.000 và 150.000; custZero có bill đã trả đủ (nợ 0).
                orderIds.Add(InsertDebtOrder(db, "HD-UICN1-" + tag, 0, 30, 30, custA, null, storeId, 200000m, 0m, DateTime.Today.AddDays(-2)));
                orderIds.Add(InsertDebtOrder(db, "HD-UICN2-" + tag, 0, 30, 30, custA, null, storeId, 150000m, 0m, DateTime.Today.AddDays(-1)));
                orderIds.Add(InsertDebtOrder(db, "HD-UICNZ-" + tag, 0, 30, 30, custZero, null, storeId, 100000m, 100000m, DateTime.Today));
                // supA có phiếu nhập 800.000 chưa thanh toán.
                orderIds.Add(InsertDebtOrder(db, "PN-UICN1-" + tag, 1, 30, 0, null, supA, storeId, 800000m, 0m, DateTime.Today.AddDays(-1)));

                var adminSess = new UserSession
                {
                    UserId = "selftest_cn", UserName = "selftest_cn", DisplayName = "Admin CN UI",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH CN UI " + tag }
                };

                TestCustomerDebtUi(adminSess, storeId, bankId, grpParentId, grpChildId, custA, custZero, tag, xlsCustPath, voucherIds);
                TestSupplierDebtUi(adminSess, storeId, bankId, supA, tag, xlsSupPath, voucherIds);
                TestDebtPermissionsReadOnlyAndMenu(db, adminSess, custA);
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
                foreach (string oid in orderIds)
                {
                    BookingExec(db, "DELETE FROM TCHITIETTHANHTOAN WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DKHACHHANG", custA);
                SafeDeleteById(db, "DKHACHHANG", custZero);
                SafeDeleteById(db, "DNHACUNGCAP", supA);
                if (!string.IsNullOrEmpty(grpChildId)) SafeDeleteById(db, "DNHOMKHACHHANG", grpChildId);
                if (!string.IsNullOrEmpty(grpParentId)) SafeDeleteById(db, "DNHOMKHACHHANG", grpParentId);
                SafeDeleteById(db, "DTAIKHOANNGANHANG", bankId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                SafeDeleteFile(xlsCustPath);
                SafeDeleteFile(xlsSupPath);
            }
        }

        private static void TestCustomerDebtUi(
            UserSession adminSess, string storeId, string bankId, string grpParentId, string grpChildId,
            string custA, string custZero, string tag, string xlsPath, List<string> createdVouchers)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            using (var host = MakeOffscreenHost())
            {
                var view = new DebtListView(adminSess, DebtPartyKind.Customer) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    bool colsOk = CheckHeadersNoClip(grid, new[] { "Mã khách", "Tên khách hàng", "Địa chỉ", "Điện thoại", "Còn nợ" });
                    bool defaultOnlyDebtOn = view.OnlyDebtCheckForTest.Checked;

                    // Cô lập theo tag: mặc định bật "Chỉ hiện khách còn nợ" -> chỉ thấy custA (350k), ẩn custZero (0).
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    bool onlyDebtDefaultOk = view.FindRowByPartnerIdForTest(custA) != null
                        && view.FindRowByPartnerIdForTest(custZero) == null;

                    // Bỏ tích -> hiện cả custZero; lọc nhóm cha -> thấy cả 2; lọc nhóm con -> chỉ thấy custA.
                    view.OnlyDebtCheckForTest.Checked = false;
                    Application.DoEvents();
                    bool showAllOk = view.FindRowByPartnerIdForTest(custA) != null
                        && view.FindRowByPartnerIdForTest(custZero) != null;

                    bool selParent = view.SelectGroupTagForTest(grpParentId);
                    Application.DoEvents();
                    bool parentOk = selParent && view.FindRowByPartnerIdForTest(custA) != null && view.FindRowByPartnerIdForTest(custZero) != null;

                    bool selChild = view.SelectGroupTagForTest(grpChildId);
                    Application.DoEvents();
                    bool childOk = selChild && view.FindRowByPartnerIdForTest(custA) != null && view.FindRowByPartnerIdForTest(custZero) == null;

                    view.SelectGroupTagForTest(null);
                    view.OnlyDebtCheckForTest.Checked = true;
                    view.SearchBoxForTest.Text = "dang thi cong no " + tag;
                    Application.DoEvents();
                    bool searchAccentOk = view.FindRowByPartnerIdForTest(custA) != null && grid.Rows.Count == 2; // 1 khách + 1 hàng Tổng cộng.

                    // Hàng tổng cộng + màu ô thật + đổi theme sống.
                    DataGridViewRow sumRow = grid.Rows.Count >= 2 ? grid.Rows[grid.Rows.Count - 1] : null;
                    decimal sumDebt = sumRow == null ? -1m : Convert.ToDecimal(sumRow.Cells["colDebt"].Value);
                    bool sumColorT0 = sumRow != null && sumRow.Cells["colName"].InheritedStyle.BackColor == DebtListView.SummaryRowBackColor(t0);
                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    bool sumColorT1 = sumRow != null
                        && DebtListView.SummaryRowBackColor(t1) != DebtListView.SummaryRowBackColor(t0)
                        && sumRow.Cells["colName"].InheritedStyle.BackColor == DebtListView.SummaryRowBackColor(t1)
                        && grid.BackgroundColor == t1.Surface;

                    // Chọn custA -> lưới chi tiết bill hiện đủ 2 hóa đơn.
                    view.SelectPartnerByIdForTest(custA);
                    Application.DoEvents();
                    bool detailBillsOk = view.BillDetailGridForTest.Rows.Count == 2;

                    // Xuất Excel đúng cột.
                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;
                    bool xlsOk = snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 5
                        && HeadersMatch(snap, VisibleGridHeaders(grid))
                        && XlsSheetContainsSubstring(snap, "KHUIA" + tag);

                    // Mở hộp Thu nợ theo đơn qua nút thật -> phân bổ 250k (200k + 50k), thử nhập quá nợ bị chặn, bấm Lưu ghi thật.
                    bool allocUiOk = false, overWarnOk = false, noDataErr = false;
                    string savedReceiptId = null;
                    view.CollectDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            dlg.StoreComboForTest.SelectId(storeId);
                            dlg.AmountBoxForTest.Value = 250000m;
                            Application.DoEvents();
                            decimal pay0 = Convert.ToDecimal(dlg.BillGridForTest.Rows[0].Cells["b_pay"].Value);
                            decimal pay1 = Convert.ToDecimal(dlg.BillGridForTest.Rows[1].Cells["b_pay"].Value);
                            allocUiOk = pay0 == 200000m && pay1 == 50000m;

                            // Thử nhập quá nợ của bill 2 (200.000 > 150.000) -> hiện cảnh báo & giữ lại 50.000, 0 DataError.
                            dlg.CommitPayCellForTest(1, 200000m);
                            overWarnOk = !string.IsNullOrEmpty(dlg.LastWarningForTest)
                                && Convert.ToDecimal(dlg.BillGridForTest.Rows[1].Cells["b_pay"].Value) == 50000m;

                            // Chọn chuyển khoản + tài khoản ngân hàng, bấm nút Lưu thật.
                            dlg.TransferCheckForTest.Checked = true;
                            dlg.BankComboForTest.SelectId(bankId);
                            Application.DoEvents();
                            dlg.SaveButtonForTest.PerformClick();
                            Application.DoEvents();

                            savedReceiptId = dlg.SavedVoucherId;
                            if (!string.IsNullOrEmpty(savedReceiptId))
                                createdVouchers.Add(savedReceiptId);
                            noDataErr = dlg.DataErrorCountForTest == 0;
                            return dlg.SavedOk ? DialogResult.OK : DialogResult.Cancel;
                        }
                        finally { dlg.Close(); }
                    };

                    view.ActionButtonForTest.PerformClick();
                    Application.DoEvents();

                    DataGridViewRow rowAfter = view.FindRowByPartnerIdForTest(custA);
                    decimal debtAfterUi = rowAfter == null ? -1m : Convert.ToDecimal(rowAfter.Cells["colDebt"].Value);
                    bool voucherDetailOk = view.VoucherDetailGridForTest.Rows.Count == 1;

                    Check("Công nợ §6 #5/#6 (UI Công nợ KH: 5 cột không cắt chữ, lọc nhóm/tìm không dấu/chỉ còn nợ, màu hàng tổng & đổi theme, chi tiết, xuất Excel, hộp Thu nợ theo đơn ghi thật & tải lại)",
                        colsOk && defaultOnlyDebtOn && onlyDebtDefaultOk && showAllOk && parentOk && childOk
                        && searchAccentOk && sumDebt == 350000m && sumColorT0 && sumColorT1
                        && detailBillsOk && xlsOk && allocUiOk && overWarnOk && noDataErr
                        && !string.IsNullOrEmpty(savedReceiptId) && debtAfterUi == 100000m && voucherDetailOk,
                        SingleLine("cols=" + colsOk + ", filt=" + onlyDebtDefaultOk + "/" + parentOk + "/" + childOk
                            + ", sum=" + sumDebt + ", theme=" + sumColorT0 + "/" + sumColorT1 + ", xls=" + xlsOk
                            + ", alloc=" + allocUiOk + ", over=" + overWarnOk + ", after=" + debtAfterUi + ", vDet=" + voucherDetailOk));
                }
                finally { host.Close(); }
            }
        }

        private static void TestSupplierDebtUi(
            UserSession adminSess, string storeId, string bankId, string supA, string tag,
            string xlsPath, List<string> createdVouchers)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new DebtListView(adminSess, DebtPartyKind.Supplier) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    bool colsOk = CheckHeadersNoClip(grid, new[] { "Mã NCC", "Tên nhà cung cấp", "Địa chỉ", "Điện thoại", "Còn nợ" });

                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    view.SelectPartnerByIdForTest(supA);
                    Application.DoEvents();
                    bool detailDocOk = view.BillDetailGridForTest.Rows.Count == 1;

                    var rec = new GridExportOutputRecorder { NextSavePath = xlsPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snap = File.Exists(xlsPath) ? ReadFirstXlsSheet(xlsPath) : null;
                    bool xlsOk = snap != null && rec.PromptSaveCount == 1 && snap.ColCount == 5
                        && HeadersMatch(snap, VisibleGridHeaders(grid))
                        && XlsSheetContainsSubstring(snap, "NCCUIA" + tag);

                    // Nút Chi trả nợ mở PhieuThuDialog loại Chi, đặt sẵn đối tượng NCC & số tiền = 800.000 -> trả 300.000 CK.
                    bool presetOk = false;
                    string savedExpenseId = null;
                    view.SupplierPayDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            presetOk = dlg.KindForTest == VoucherKind.Expense
                                && dlg.ObjectTypeComboForTest.SelectedIndex == PhieuThuRules.Supplier
                                && dlg.SupplierComboForTest.SelectedId == supA
                                && dlg.AmountBoxForTest.Value == 800000m;

                            dlg.StoreComboForTest.SelectId(storeId);
                            dlg.AmountBoxForTest.Value = 300000m;
                            dlg.TransferCheckForTest.Checked = true;
                            dlg.BankComboForTest.SelectId(bankId);
                            Application.DoEvents();
                            dlg.SaveButtonForTest.PerformClick();
                            Application.DoEvents();

                            savedExpenseId = dlg.SavedId;
                            if (!string.IsNullOrEmpty(savedExpenseId))
                                createdVouchers.Add(savedExpenseId);
                            return dlg.AnySaved ? DialogResult.OK : DialogResult.Cancel;
                        }
                        finally { dlg.Close(); }
                    };

                    view.ActionButtonForTest.PerformClick();
                    Application.DoEvents();

                    DataGridViewRow supAfter = view.FindRowByPartnerIdForTest(supA);
                    decimal supDebtAfter = supAfter == null ? -1m : Convert.ToDecimal(supAfter.Cells["colDebt"].Value);
                    bool supVoucherDetailOk = view.VoucherDetailGridForTest.Rows.Count == 1;

                    Check("Công nợ §6 #5/#6 (UI Công nợ NCC: 5 cột không cắt chữ, xuất Excel, nút Chi trả nợ mở PhieuThuDialog đặt sẵn NCC & Còn nợ, lưu giảm nợ 800k -> 500k)",
                        colsOk && detailDocOk && xlsOk && presetOk && !string.IsNullOrEmpty(savedExpenseId)
                        && supDebtAfter == 500000m && supVoucherDetailOk,
                        SingleLine("cols=" + colsOk + ", xls=" + xlsOk + ", preset=" + presetOk + ", after=" + supDebtAfter + ", vDet=" + supVoucherDetailOk));
                }
                finally { host.Close(); }
            }
        }

        private static void TestDebtPermissionsReadOnlyAndMenu(string db, UserSession adminSess, string custA)
        {
            // User chỉ có quyền Xem Công nợ KH/NCC, không có quyền phiếu thu/chi -> ẩn nút Thu/Chi, hộp Thu nợ mở chỉ-đọc.
            var viewOnlyPerms = new FunctionPermissions(false);
            viewOnlyPerms.Add("fn_cnkh", DebtConstants.CustomerDebtFunctionName, RoleModes.View);
            viewOnlyPerms.Add("fn_cnncc", DebtConstants.SupplierDebtFunctionName, RoleModes.View);
            var viewOnlySess = new UserSession
            {
                UserId = "u_cn_view", UserName = "u_cn_view", DisplayName = "Chỉ xem công nợ",
                IsAdmin = false, DatabasePath = db, Permissions = viewOnlyPerms
            };

            bool custBtnHidden, supBtnHidden, dlgReadOnly;
            using (var host = MakeOffscreenHost())
            {
                var vCust = new DebtListView(viewOnlySess, DebtPartyKind.Customer) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(vCust);
                host.Show();
                Application.DoEvents();
                custBtnHidden = !vCust.ActionButtonForTest.Visible;
                host.Close();
            }
            using (var host = MakeOffscreenHost())
            {
                var vSup = new DebtListView(viewOnlySess, DebtPartyKind.Supplier) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(vSup);
                host.Show();
                Application.DoEvents();
                supBtnHidden = !vSup.ActionButtonForTest.Visible;
                host.Close();
            }
            using (var dlg = new CustomerDebtCollectDialog(viewOnlySess, new DebtPartnerRow { Id = custA, Name = "KH", RemainingDebt = 100000m }))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    dlgReadOnly = dlg.IsReadOnlyForTest && !dlg.SaveButtonForTest.Visible
                        && dlg.BillGridForTest.ReadOnly && dlg.NumberBoxForTest.ReadOnly;
                }
                finally { dlg.Close(); }
            }

            // Nối menu CÔNG NỢ > Công nợ khách hàng, Công nợ nhà cung cấp + ẩn menu khi không quyền + preview.
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode custNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsCustomerDebtMenu(n));
            MenuNode supNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsSupplierDebtMenu(n));

            bool custViewOk = false, supViewOk = false;
            if (custNode != null)
            {
                using (Control c = ScreenFactory.CreateView(custNode, adminSess))
                    custViewOk = c is DebtListView && ((DebtListView)c).PartyKindForTest == DebtPartyKind.Customer;
            }
            if (supNode != null)
            {
                using (Control c = ScreenFactory.CreateView(supNode, adminSess))
                    supViewOk = c is DebtListView && ((DebtListView)c).PartyKindForTest == DebtPartyKind.Supplier;
            }

            MenuModel deniedMenu = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_no_cn", UserName = "u_no_cn", DisplayName = "Không quyền công nợ",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            });
            bool deniedHidden = FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsCustomerDebtMenu(n)) == null
                && FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsSupplierDebtMenu(n)) == null;

            bool pvOk = false;
            using (Form pvC = ScreenFactory.Create("preview.debtcustomer", adminSess))
            using (Form pvS = ScreenFactory.Create("preview.debtsupplier", adminSess))
            {
                pvOk = pvC is DebtPreviewForm && pvS is DebtPreviewForm;
            }

            Check("Công nợ §5 (phân quyền SFUNCTION, ẩn nút Thu/Chi, hộp chỉ-đọc khi thiếu quyền, nối 2 mục menu CÔNG NỢ & preview): đạt",
                custBtnHidden && supBtnHidden && dlgReadOnly
                && custNode != null && supNode != null && custViewOk && supViewOk && deniedHidden && pvOk,
                SingleLine("hide=" + custBtnHidden + "/" + supBtnHidden + ", ro=" + dlgReadOnly
                    + ", menu=" + custViewOk + "/" + supViewOk + ", denied=" + deniedHidden + ", pv=" + pvOk));
        }
    }
}
