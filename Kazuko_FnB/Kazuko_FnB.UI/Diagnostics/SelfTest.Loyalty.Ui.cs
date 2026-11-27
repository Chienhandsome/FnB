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
        private static void LoyaltyUiAndMenuTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupLoyaltyConfigs(db, new[]
            {
                "CachTinhDiem", "DoanhSoTuongUngVoi1Diem", "TuDongNangCapThanhVienKhiDatHanMuc", "KichHoatLuuVetHoatDong"
            });

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string grpParentId = null;
            string grpChildId = null;
            string custMain = DbHelper.NewId();
            string custZero = DbHelper.NewId();
            var orderIds = new List<string>();
            var voucherIds = new List<string>();
            string xlsCustPath = Path.Combine(Path.GetTempPath(), "kazuko-loyalty-cust-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsBillPath = Path.Combine(Path.GetTempPath(), "kazuko-loyalty-bill-" + Guid.NewGuid().ToString("N") + ".xls");
            string xlsVoucherPath = Path.Combine(Path.GetTempPath(), "kazuko-loyalty-vch-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "CachTinhDiem", 1);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "DoanhSoTuongUngVoi1Diem", 20000);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "TuDongNangCapThanhVienKhiDatHanMuc", 0);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "KichHoatLuuVetHoatDong", 30);

                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tt')",
                    "@I", storeId, "@N", "CH TT UI " + tag);

                var grpRepo = new CustomerGroupRepository(db);
                grpParentId = grpRepo.Save(new CustomerGroupRow { Name = "Nhóm Cha TT " + tag }, "selftest_tt").SavedId;
                grpChildId = grpRepo.Save(new CustomerGroupRow { Name = "Nhóm Con TT " + tag, ParentId = grpParentId }, "selftest_tt").SavedId;

                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DNHOMKHACHHANGID, DIACHI, DIENTHOAI, EMAIL, DIEMTICHLUYBANDAU, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @G, '77 Trần Hưng Đạo', '0918777666', 'main@kazuko.vn', 10, 'Khách thân thiết chính', 30, 'selftest_tt', @T)",
                    "@I", custMain, "@C", "KHTTA" + tag, "@N", "Nguyễn Ánh Tuyết " + tag, "@G", grpChildId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DNHOMKHACHHANGID, DIACHI, DIENTHOAI, EMAIL, DIEMTICHLUYBANDAU, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @G, '88 Bạch Mai', '0918000111', 'zero@kazuko.vn', 0, 'Khách 0 điểm', 30, 'selftest_tt', @T)",
                    "@I", custZero, "@C", "KHTTZ" + tag, "@N", "Trần Không Điểm " + tag, "@G", grpParentId, "@T", now);

                string bill1Num = "HD-UITT1-" + tag;
                string bill2Num = "HD-UITT2-" + tag;
                orderIds.Add(InsertLoyaltyOrder(db, bill1Num, 0, 30, 30, custMain, storeId, 100000m, 5m, 0m, DateTime.Today.AddDays(-2)));
                orderIds.Add(InsertLoyaltyOrder(db, bill2Num, 0, 30, 30, custMain, storeId, 140000m, 7m, 2m, DateTime.Today.AddDays(-1)));
                orderIds.Add(InsertLoyaltyOrder(db, "HD-UITT-UNP-" + tag, 0, 30, 0, custMain, storeId, 900000m, 45m, 0m, DateTime.Today));

                var adminSess = new UserSession
                {
                    UserId = "selftest_tt", UserName = "selftest_tt", DisplayName = "Admin TT UI",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH TT UI " + tag }
                };

                var svc = new LoyaltyService(adminSess);
                string seedVoucherNum = "TD-UI1-" + tag;
                LoyaltySaveResult seedV = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = seedVoucherNum,
                    Date = DateTime.Today,
                    CustomerId = custMain,
                    GiftPoints = 20m,
                    ReducePoints = 0m,
                    Reason = "Tặng điểm khởi đầu UI " + tag,
                    Note = "Seed UI"
                });
                if (seedV.Ok) voucherIds.Add(seedV.SavedId);

                TestLoyaltyGridTabsFilterThemeAndExcel(
                    adminSess, grpParentId, grpChildId, custMain, custZero, tag,
                    bill1Num, seedVoucherNum, xlsCustPath, xlsBillPath, xlsVoucherPath, voucherIds);
                TestLoyaltyPermissionsReadOnlyDateLockAndMenu(db, adminSess, custMain, seedV.SavedId);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                foreach (string vid in voucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTANGGIAMDIEM", vid);
                }
                foreach (string oid in orderIds)
                    SafeDeleteById(db, "TDONHANG", oid);
                SafeDeleteById(db, "DKHACHHANG", custMain);
                SafeDeleteById(db, "DKHACHHANG", custZero);
                if (!string.IsNullOrEmpty(grpChildId)) SafeDeleteById(db, "DNHOMKHACHHANG", grpChildId);
                if (!string.IsNullOrEmpty(grpParentId)) SafeDeleteById(db, "DNHOMKHACHHANG", grpParentId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                SafeDeleteFile(xlsCustPath);
                SafeDeleteFile(xlsBillPath);
                SafeDeleteFile(xlsVoucherPath);
                RestoreLoyaltyConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static void TestLoyaltyGridTabsFilterThemeAndExcel(
            UserSession adminSess, string grpParentId, string grpChildId, string custMain, string custZero,
            string tag, string bill1Num, string seedVoucherNum,
            string xlsCustPath, string xlsBillPath, string xlsVoucherPath, List<string> voucherIds)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            using (var host = MakeOffscreenHost())
            {
                var view = new LoyaltyView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DataGridView grid = view.GridForTest;
                    DataGridView billGrid = view.BillDetailGridForTest;
                    DataGridView voucherGrid = view.VoucherDetailGridForTest;

                    bool masterColsOk = CheckHeadersNoClip(grid, new[]
                    {
                        "Tên khách", "Nhóm khách", "Địa chỉ", "Điện thoại", "Email",
                        "Điểm tích lũy ban đầu", "Doanh số", "Số hóa đơn", "Điểm tích lũy", "Ghi chú"
                    });
                    bool billColsOk = CheckHeadersNoClip(billGrid, new[]
                    {
                        "Điểm", "Số phiếu", "Ngày", "Tổng cộng", "Điểm sử dụng"
                    });
                    bool vchColsOk = CheckHeadersNoClip(voucherGrid, new[]
                    {
                        "Số phiếu", "Ngày", "Điểm tặng", "Điểm giảm", "Lý do", "Ghi chú"
                    });

                    // Lọc nhóm cha (hiện cả con) & nhóm con (chỉ hiện custMain) & tìm không dấu & chỉ khách có điểm.
                    view.SearchBoxForTest.Text = tag;
                    Application.DoEvents();
                    bool parentSel = view.SelectGroupTagForTest(grpParentId);
                    Application.DoEvents();
                    bool parentHasBoth = parentSel
                        && view.FindRowByCustomerIdForTest(custMain) != null
                        && view.FindRowByCustomerIdForTest(custZero) != null;

                    view.OnlyWithPointsCheckForTest.Checked = true;
                    Application.DoEvents();
                    bool onlyPointsOk = view.FindRowByCustomerIdForTest(custMain) != null
                        && view.FindRowByCustomerIdForTest(custZero) == null;

                    view.OnlyWithPointsCheckForTest.Checked = false;
                    Application.DoEvents();
                    bool childSel = view.SelectGroupTagForTest(grpChildId);
                    Application.DoEvents();
                    bool childOnlyMain = childSel
                        && view.FindRowByCustomerIdForTest(custMain) != null
                        && view.FindRowByCustomerIdForTest(custZero) == null;

                    view.SelectGroupTagForTest(null);
                    view.SearchBoxForTest.Text = "nguyen anh tuyet " + tag;
                    Application.DoEvents();
                    bool searchNameOk = view.FindRowByCustomerIdForTest(custMain) != null && grid.Rows.Count == 2;

                    view.SearchBoxForTest.Text = "tran hung dao";
                    Application.DoEvents();
                    bool searchAddrOk = view.FindRowByCustomerIdForTest(custMain) != null;

                    view.SearchBoxForTest.Text = "0918777666";
                    Application.DoEvents();
                    bool searchPhoneOk = view.FindRowByCustomerIdForTest(custMain) != null;

                    view.SearchBoxForTest.Text = "khtta" + tag;
                    Application.DoEvents();
                    bool searchCodeOk = view.FindRowByCustomerIdForTest(custMain) != null && grid.Rows.Count == 2;

                    // Kiểm số liệu dòng khách + hàng tổng cộng + màu thật của ô & đổi theme sống.
                    DataGridViewRow custRow = view.FindRowByCustomerIdForTest(custMain);
                    DataGridViewRow sumRow = grid.Rows.Count >= 2 ? grid.Rows[grid.Rows.Count - 1] : null;
                    bool numsOk = custRow != null
                        && Convert.ToDecimal(custRow.Cells["colRevenue"].Value) == 240000m
                        && Convert.ToInt32(custRow.Cells["colBillCount"].Value) == 2
                        && Convert.ToDecimal(custRow.Cells["colPoints"].Value) == 40m
                        && sumRow != null
                        && Convert.ToDecimal(sumRow.Cells["colRevenue"].Value) == 240000m
                        && Convert.ToInt32(sumRow.Cells["colBillCount"].Value) == 2
                        && Convert.ToDecimal(sumRow.Cells["colPoints"].Value) == 40m;

                    bool sumBoldAndColorT0 = sumRow != null
                        && sumRow.Cells["colName"].InheritedStyle.Font.Bold
                        && sumRow.Cells["colName"].InheritedStyle.BackColor == LoyaltyView.SummaryRowBackColor(t0);
                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    bool sumColorT1 = sumRow != null
                        && LoyaltyView.SummaryRowBackColor(t1) != LoyaltyView.SummaryRowBackColor(t0)
                        && sumRow.Cells["colName"].InheritedStyle.BackColor == LoyaltyView.SummaryRowBackColor(t1)
                        && grid.BackgroundColor == t1.Surface;

                    // Tab Hóa đơn & Tab Tặng điểm - tặng quà đúng dòng và hàng tổng.
                    view.SelectCustomerByIdForTest(custMain);
                    Application.DoEvents();
                    DataGridViewRow billSum = billGrid.Rows.Count == 3 ? billGrid.Rows[2] : null;
                    bool billTabOk = billSum != null
                        && Convert.ToDecimal(billSum.Cells["b_points"].Value) == 12m
                        && Convert.ToDecimal(billSum.Cells["b_total"].Value) == 240000m
                        && Convert.ToDecimal(billSum.Cells["b_used"].Value) == 2m;

                    view.DetailTabsForTest.SelectedIndex = 1;
                    Application.DoEvents();
                    DataGridViewRow vchSum = voucherGrid.Rows.Count == 2 ? voucherGrid.Rows[1] : null;
                    bool vchTabOk = vchSum != null
                        && Convert.ToDecimal(vchSum.Cells["v_gift"].Value) == 20m
                        && Convert.ToDecimal(vchSum.Cells["v_reduce"].Value) == 0m;

                    // Xuất Excel cả 3 lưới qua GridExcelExporter (mở đúng tab trước khi bấm nút trên tab).
                    var recCust = new GridExportOutputRecorder { NextSavePath = xlsCustPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = recCust;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snapCust = File.Exists(xlsCustPath) ? ReadFirstXlsSheet(xlsCustPath) : null;
                    bool xlsCustOk = snapCust != null && recCust.PromptSaveCount == 1
                        && snapCust.ColCount == 10 && snapCust.RowCount == CountVisibleGridRows(grid) + 1
                        && HeadersMatch(snapCust, VisibleGridHeaders(grid))
                        && XlsSheetContainsSubstring(snapCust, "Nguyễn Ánh Tuyết");

                    view.DetailTabsForTest.SelectedIndex = 0;
                    Application.DoEvents();
                    var recBill = new GridExportOutputRecorder { NextSavePath = xlsBillPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = recBill;
                    view.ExportBillsButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snapBill = File.Exists(xlsBillPath) ? ReadFirstXlsSheet(xlsBillPath) : null;
                    bool xlsBillOk = snapBill != null && recBill.PromptSaveCount == 1
                        && snapBill.ColCount == 5 && snapBill.RowCount == CountVisibleGridRows(billGrid) + 1
                        && HeadersMatch(snapBill, VisibleGridHeaders(billGrid))
                        && XlsSheetContainsSubstring(snapBill, bill1Num);

                    view.DetailTabsForTest.SelectedIndex = 1;
                    Application.DoEvents();
                    var recVch = new GridExportOutputRecorder { NextSavePath = xlsVoucherPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = recVch;
                    view.ExportVouchersButtonForTest.PerformClick();
                    Application.DoEvents();
                    XlsSheetSnapshot snapVch = File.Exists(xlsVoucherPath) ? ReadFirstXlsSheet(xlsVoucherPath) : null;
                    bool xlsVchOk = snapVch != null && recVch.PromptSaveCount == 1
                        && snapVch.ColCount == 6 && snapVch.RowCount == CountVisibleGridRows(voucherGrid) + 1
                        && HeadersMatch(snapVch, VisibleGridHeaders(voucherGrid))
                        && XlsSheetContainsSubstring(snapVch, seedVoucherNum);

                    Check("Thân thiết §4 #2 (UI lưới/tab/lọc/tìm không dấu/màu hàng tổng & đổi theme/xuất Excel 3 lưới): đủ cột không cắt chữ, cha gồm con, chỉ có điểm, tổng 2 tab, màu ô thật, 3 file Excel đúng cột",
                        masterColsOk && billColsOk && vchColsOk
                        && parentHasBoth && onlyPointsOk && childOnlyMain
                        && searchNameOk && searchAddrOk && searchPhoneOk && searchCodeOk
                        && numsOk && sumBoldAndColorT0 && sumColorT1 && billTabOk && vchTabOk
                        && xlsCustOk && xlsBillOk && xlsVchOk,
                        SingleLine("cols=" + masterColsOk + "/" + billColsOk + "/" + vchColsOk
                            + ", filt=" + parentHasBoth + "/" + onlyPointsOk + "/" + childOnlyMain
                            + ", srch=" + searchNameOk + "/" + searchAddrOk + "/" + searchPhoneOk + "/" + searchCodeOk
                            + ", theme=" + sumBoldAndColorT0 + "/" + sumColorT1
                            + ", tabs=" + billTabOk + "/" + vchTabOk + ", xls=" + xlsCustOk + "/" + xlsBillOk + "/" + xlsVchOk));

                    // Mở hộp tặng/giảm điểm qua nút thật trên tab -> kiểm khóa khách, nhãn không cắt, không cuộn dọc,
                    // chặn thiếu lý do & giảm vượt 40, lưu +15 -> điểm lên 55, rồi bấm nút Xóa thật -> điểm về 40.
                    bool dlgLayoutOk = false, lockedCustOk = false, autoNumOk = false, errReasonOk = false, errOverOk = false;
                    string addedId = null;
                    view.PointDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            float scale = dlg.DeviceDpi / 96f;
                            dlgLayoutOk = dlg.FieldLabelsForTest.Count == 7 && AllLabelsFit(dlg.FieldLabelsForTest)
                                && !dlg.VerticalScroll.Visible && !dlg.AutoScroll
                                && (dlg.ClientSize.Height / scale) * 1.5f <= 700f
                                && (dlg.ClientSize.Width / scale) * 1.5f <= 1300f;
                            lockedCustOk = dlg.CustomerComboForTest.SelectedId == custMain && !dlg.CustomerComboForTest.Enabled;
                            autoNumOk = dlg.NumberBoxForTest.Text.StartsWith(
                                DocumentNumber.LiteralPrefix(LoyaltyConstants.DefaultNumberTemplate, DateTime.Today),
                                StringComparison.Ordinal);

                            // Thiếu lý do -> chặn.
                            dlg.GiftPointsBoxForTest.Value = 15m;
                            dlg.ReasonBoxForTest.Text = "";
                            dlg.SaveCloseButtonForTest.PerformClick();
                            Application.DoEvents();
                            errReasonOk = dlg.LastErrorForTest == LoyaltyConstants.ReasonRequired;

                            // Giảm vượt điểm hiện có (0 tặng, 41 giảm > 40) -> chặn.
                            dlg.GiftPointsBoxForTest.Value = 0m;
                            dlg.ReducePointsBoxForTest.Value = 41m;
                            dlg.ReasonBoxForTest.Text = "Thử giảm quá điểm";
                            dlg.SaveCloseButtonForTest.PerformClick();
                            Application.DoEvents();
                            errOverOk = dlg.LastErrorForTest == LoyaltyConstants.ReduceExceedsPoints(40m);

                            // Lưu tặng 15 điểm hợp lệ.
                            dlg.GiftPointsBoxForTest.Value = 15m;
                            dlg.ReducePointsBoxForTest.Value = 0m;
                            dlg.ReasonBoxForTest.Text = "Tặng từ hộp UI " + tag;
                            dlg.NoteBoxForTest.Text = "Ghi chú UI";
                            Application.DoEvents();
                            dlg.SaveCloseButtonForTest.PerformClick();
                            Application.DoEvents();
                            addedId = dlg.SavedId;
                            if (!string.IsNullOrEmpty(addedId))
                                voucherIds.Add(addedId);
                            return dlg.AnySaved ? DialogResult.OK : DialogResult.Cancel;
                        }
                        finally { dlg.Close(); }
                    };

                    view.VoucherAddButtonForTest.PerformClick();
                    Application.DoEvents();
                    DataGridViewRow afterAddRow = view.FindRowByCustomerIdForTest(custMain);
                    decimal ptsAfterAdd = afterAddRow == null ? -1m : Convert.ToDecimal(afterAddRow.Cells["colPoints"].Value);

                    // Bấm Xóa thật trên phiếu vừa thêm -> điểm quay về 40.
                    view.ConfirmDeleteOverrideForTest = delegate { return true; };
                    bool selAdded = view.SelectVoucherByIdForTest(addedId);
                    view.VoucherDeleteButtonForTest.PerformClick();
                    Application.DoEvents();
                    DataGridViewRow afterDelRow = view.FindRowByCustomerIdForTest(custMain);
                    decimal ptsAfterDel = afterDelRow == null ? -1m : Convert.ToDecimal(afterDelRow.Cells["colPoints"].Value);

                    Check("Thân thiết §4 #3/#4 (UI LoyaltyPointDialog): đặt sẵn & khóa khách, số TD tự sinh, không cắt nhãn/cuộn dọc @150% DPI, chặn thiếu lý do & vượt điểm, lưu +15 (55đ) & xóa về 40đ",
                        dlgLayoutOk && lockedCustOk && autoNumOk && errReasonOk && errOverOk
                        && !string.IsNullOrEmpty(addedId) && ptsAfterAdd == 55m && selAdded && ptsAfterDel == 40m,
                        SingleLine("lay=" + dlgLayoutOk + ", lock=" + lockedCustOk + ", num=" + autoNumOk
                            + ", errR=" + errReasonOk + ", errO=" + errOverOk + ", pts=" + ptsAfterAdd + "->" + ptsAfterDel));
                }
                finally { host.Close(); }
            }
        }

        private static void TestLoyaltyPermissionsReadOnlyDateLockAndMenu(
            string db, UserSession adminSess, string custMain, string seedVoucherId)
        {
            // 1. User chỉ có quyền Xem -> ẩn mọi nút Thêm/Sửa/Xóa; nhấp đôi mở hộp chỉ-đọc; gọi service lưu/xóa bị chặn.
            var viewPerms = new FunctionPermissions(false);
            viewPerms.Add("fn_tt", LoyaltyConstants.FunctionName, RoleModes.View);
            var viewOnlySess = new UserSession
            {
                UserId = "u_tt_view", UserName = "u_tt_view", DisplayName = "Chỉ xem thân thiết",
                IsAdmin = false, DatabasePath = db, Permissions = viewPerms
            };

            bool buttonsHidden = false, roOpened = false, roLocked = false, roNoSave = false;
            using (var host = MakeOffscreenHost())
            {
                var view = new LoyaltyView(viewOnlySess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    buttonsHidden = !view.AddPointButtonForTest.Visible
                        && !view.VoucherAddButtonForTest.Visible
                        && !view.VoucherEditButtonForTest.Visible
                        && !view.VoucherDeleteButtonForTest.Visible;

                    view.SelectCustomerByIdForTest(custMain);
                    view.DetailTabsForTest.SelectedIndex = 1;
                    Application.DoEvents();

                    view.PointDialogOpenerForTest = dlg =>
                    {
                        dlg.SuppressPopup = true;
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            roOpened = dlg.IsReadOnlyForTest;
                            roLocked = !dlg.DateBoxForTest.Enabled && dlg.NumberBoxForTest.ReadOnly
                                && !dlg.CustomerComboForTest.Enabled && !dlg.GiftPointsBoxForTest.Enabled
                                && !dlg.ReducePointsBoxForTest.Enabled && dlg.ReasonBoxForTest.ReadOnly
                                && dlg.NoteBoxForTest.ReadOnly;
                            roNoSave = !dlg.SaveButtonForTest.Visible && !dlg.SaveNewButtonForTest.Visible
                                && !dlg.SaveCloseButtonForTest.Visible && dlg.CloseButtonForTest.Visible;
                        }
                        finally { dlg.Close(); }
                        return DialogResult.Cancel;
                    };

                    if (view.SelectVoucherByIdForTest(seedVoucherId) && view.VoucherDetailGridForTest.CurrentRow != null)
                        InvokeGridDoubleClick(view.VoucherDetailGridForTest, 0, view.VoucherDetailGridForTest.CurrentRow.Index);
                }
                finally { host.Close(); }
            }

            var viewOnlySvc = new LoyaltyService(viewOnlySess);
            bool svcBlockedAdd = !viewOnlySvc.SaveVoucher(new LoyaltyPointVoucherRow
            {
                Number = "TD-DENY", Date = DateTime.Today, CustomerId = custMain, GiftPoints = 1m, Reason = "Deny"
            }).Ok;
            bool svcBlockedDel = !viewOnlySvc.DeleteVoucher(seedVoucherId).Ok;

            // 2. User có quyền Thêm nhưng KHÔNG có quyền "Thay đổi ngày" -> ô Ngày bị khóa.
            var addNoDatePerms = new FunctionPermissions(false);
            addNoDatePerms.Add("fn_tt", LoyaltyConstants.FunctionName, RoleModes.View | RoleModes.Add);
            var addNoDateSess = new UserSession
            {
                UserId = "u_tt_nodate", UserName = "u_tt_nodate", DisplayName = "Không sửa ngày",
                IsAdmin = false, DatabasePath = db, Permissions = addNoDatePerms
            };
            bool dateLockedForNonAdmin = false;
            using (var dlg = new LoyaltyPointDialog(addNoDateSess, null, custMain, true, false))
            {
                dlg.SuppressPopup = true;
                ShowDialogOffscreen(dlg);
                try
                {
                    dateLockedForNonAdmin = !dlg.IsReadOnlyForTest && !dlg.DateBoxForTest.Enabled && dlg.DateBoxForTest.Inner.ReadOnly;
                }
                finally { dlg.Close(); }
            }

            // 3. Menu KHÁCH HÀNG > Khách hàng thân thiết (giống mẫu Công nợ / Danh mục voucher) & preview.loyalty.
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode loyaltyNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsLoyaltyMenu(n));
            bool openViewOk = false;
            if (loyaltyNode != null)
            {
                using (Control c = ScreenFactory.CreateView(loyaltyNode, adminSess))
                    openViewOk = c is LoyaltyView;
            }

            MenuModel deniedMenu = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_no_tt", UserName = "u_no_tt", DisplayName = "Không quyền TT",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            });
            bool deniedHidden = FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsLoyaltyMenu(n)) == null;

            bool pvOk = false;
            using (Form pv = ScreenFactory.Create("preview.loyalty", adminSess))
                pvOk = pv is LoyaltyPreviewForm;

            Check("Thân thiết §4 #6 (quyền chỉ Xem, hộp chỉ-đọc, khóa ngày không có quyền Thay đổi ngày, nối menu KHÁCH HÀNG > Khách hàng thân thiết & preview)",
                buttonsHidden && roOpened && roLocked && roNoSave && svcBlockedAdd && svcBlockedDel
                && dateLockedForNonAdmin
                && loyaltyNode != null && openViewOk && deniedHidden && pvOk,
                SingleLine("hide=" + buttonsHidden + ", ro=" + roOpened + "/" + roLocked + "/" + roNoSave
                    + ", dateLock=" + dateLockedForNonAdmin + ", menu=" + (loyaltyNode != null) + "/" + openViewOk + "/" + deniedHidden + ", pv=" + pvOk));
        }
    }
}
