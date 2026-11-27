using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest UI, hộp xác nhận XACNHAN, đổi theme sống, phân quyền, menu và hàm trợ giúp dữ liệu mẫu cho Xuất lại định lượng.</summary>
    public static partial class SelfTest
    {
        private static void MaterialRegenUiAndMenuTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupOpeningConfigs(db, new[] { "TuDongTinhGiaVon", "KichHoatLuuVetHoatDong" });
            var lineSnap = new Dictionary<string, decimal>();
            var prodSnap = new Dictionary<string, decimal>();

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string whId = DbHelper.NewId();
            string areaId = DbHelper.NewId();
            string tableId = null;
            string unitId = DbHelper.NewId();
            string prodA = DbHelper.NewId();
            string prodX = DbHelper.NewId();
            string recipeId = DbHelper.NewId();
            var createdOrders = new List<string>();
            ThemeTokens savedTheme = ThemeManager.Current;

            try
            {
                foreach (DataRow r in DbHelper.Read(db, conn => DbHelper.Table(conn, null, "SELECT ID, COALESCE(GIAVON, 0) AS GV FROM TDONHANGCHITIET")).Rows)
                    lineSnap[DbHelper.Str(r["ID"])] = DbHelper.Dec(r["GV"]);
                foreach (DataRow r in DbHelper.Read(db, conn => DbHelper.Table(conn, null, "SELECT ID, COALESCE(GIAVON, 0) AS GV FROM DMATHANG")).Rows)
                    prodSnap[DbHelper.Str(r["ID"])] = DbHelper.Dec(r["GV"]);

                DateTime now = DbHelper.Now();
                DbHelper.InTransaction(db, (conn, tx) =>
                {
                    DbHelper.Exec(conn, tx, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xl')", "@I", storeId, "@N", "CH XL UI " + tag);
                    DbHelper.Exec(conn, tx, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xl', @T)",
                        "@I", whId, "@N", "Kho XL UI " + tag, "@S", storeId, "@T", now);
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZZU', 'selftest_xl', @T)",
                        "@I", areaId, "@N", "KV XL UI " + tag, "@W", whId, "@T", now);
                    tableId = InsertTestTable(conn, tx, "XL-UI-" + tag, areaId, 0, now);
                    DbHelper.Exec(conn, tx, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xl')", "@I", unitId, "@N", "G " + tag);
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @C, @N, '2', @U, 10000, 15000, 10000, 30, 'selftest_xl', @T)",
                        "@I", prodA, "@C", "XLUA-" + tag, "@N", "NL UI A " + tag, "@U", unitId, "@T", now);
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @C, @N, '1', @U, 0, 45000, 20000, 30, 'selftest_xl', @T)",
                        "@I", prodX, "@C", "XLUX-" + tag, "@N", "Món UI X " + tag, "@U", unitId, "@T", now);
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) VALUES (@I, 30, 'selftest_xl', @T, @P, 3, @V)",
                        "@I", recipeId, "@T", now, "@P", prodX, "@V", prodA);
                });

                string billId = MrInsertBill(db, createdOrders, "HD-XLUI-" + tag, new DateTime(2004, 3, 15), 0, 30, 30, tableId, storeId);
                MrInsertDetail(db, billId, prodX, "Món UI X " + tag, unitId, null, 2m, 0, 0m);
                MrInsertDetail(db, billId, prodA, "NL UI A " + tag, unitId, whId, 4m, 30, 0m);

                var adminSess = new UserSession
                {
                    UserId = "selftest_xl_ui", UserName = "selftest_xl_ui", DisplayName = "Admin XL UI",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
                };

                TestMaterialRegenMainUi(db, adminSess, storeId, billId, prodA);
                TestMaterialRegenConfirmDialogUi();
                TestMaterialRegenViewOnlyUi(db, storeId, tag);
                TestMaterialRegenMenuAndPreview(db, adminSess);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                BookingExec(db, "DELETE FROM TLUUVET WHERE CHUCNANG = @F AND USERCREATEDID = 'selftest_xl_ui'",
                    "@F", MaterialRegenConstants.FunctionName);
                foreach (string oid in createdOrders)
                {
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DDINHLUONG", recipeId);
                SafeDeleteById(db, "DMATHANG", prodX);
                SafeDeleteById(db, "DMATHANG", prodA);
                if (!string.IsNullOrEmpty(tableId)) SafeDeleteById(db, "DBAN", tableId);
                SafeDeleteById(db, "DKHUVUC", areaId);
                SafeDeleteById(db, "DKHOHANG", whId);
                SafeDeleteById(db, "DDONVITINH", unitId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                RestoreOpeningConfigs(db, cfgRepo, cfgBackups);
                foreach (KeyValuePair<string, decimal> kv in lineSnap)
                    BookingExec(db, "UPDATE TDONHANGCHITIET SET GIAVON = @G WHERE ID = @I AND COALESCE(GIAVON, 0) <> @G", "@G", kv.Value, "@I", kv.Key);
                foreach (KeyValuePair<string, decimal> kv in prodSnap)
                    BookingExec(db, "UPDATE DMATHANG SET GIAVON = @G WHERE ID = @I AND COALESCE(GIAVON, 0) <> @G", "@G", kv.Value, "@I", kv.Key);
            }
        }

        private static void TestMaterialRegenMainUi(string db, UserSession adminSess, string storeId, string billId, string prodA)
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            using (var host = MakeOffscreenHost())
            {
                var view = new MaterialRegenView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    DateTime defFrom, defTo;
                    MaterialRegenRules.DefaultDateRange(new MaterialRegenRepository(db).GetServerDate(), out defFrom, out defTo);
                    bool defaultMonthOk = view.FromDateBoxForTest.NullableValue == defFrom
                        && view.ToDateBoxForTest.NullableValue == defTo;

                    int titleTextW = TextRenderer.MeasureText(view.TitleLabelForTest.Text, view.TitleLabelForTest.Font).Width;
                    bool titleNotClipped = view.TitleLabelForTest.Width >= titleTextW;

                    for (int i = 0; i < view.StoreComboForTest.Items.Count; i++)
                    {
                        var st = view.StoreComboForTest.Items[i] as StoreRecord;
                        if (st != null && st.Id == storeId)
                        {
                            view.StoreComboForTest.SelectedIndex = i;
                            break;
                        }
                    }

                    // Để trống ngày -> bấm Xem trước bị chặn, không xóa dòng nào.
                    view.ApplyDateTextForTest("", "15/03/2004");
                    view.PreviewButtonForTest.PerformClick();
                    bool emptyDateUiOk = view.LastPreviewForTest != null && !view.LastPreviewForTest.Ok
                        && view.LastPreviewForTest.ErrorMessage == MaterialRegenConstants.DateRequired
                        && MrReadExportQty(db, billId, prodA) == 4m;

                    // Từ ngày sau Đến ngày -> bị chặn.
                    view.ApplyDateTextForTest("20/03/2004", "10/03/2004");
                    view.PreviewButtonForTest.PerformClick();
                    bool reversedDateUiOk = view.LastPreviewForTest != null && !view.LastPreviewForTest.Ok
                        && view.LastPreviewForTest.ErrorMessage == MaterialRegenConstants.DateRangeInvalid;

                    // Chọn khoảng hợp lệ -> bấm Xem trước qua nút thật: đếm đúng 1 HĐ, xóa 1, sinh 1; chưa ghi DB.
                    view.ApplyDateTextForTest("01/03/2004", "31/03/2004");
                    view.PreviewButtonForTest.PerformClick();
                    bool previewUiOk = view.LastPreviewForTest != null && view.LastPreviewForTest.Ok
                        && view.LastPreviewForTest.EligibleBillCount == 1
                        && view.LastPreviewForTest.ExistingExportLineCount == 1
                        && view.LastPreviewForTest.NewExportLineCount == 1
                        && view.ResultLabelForTest.Text.IndexOf("1", StringComparison.Ordinal) >= 0
                        && MrReadExportQty(db, billId, prodA) == 4m;

                    // Gõ sai XACNHAN trong hộp xác nhận thật -> nút Đồng ý vẫn tắt, bấm Hủy -> không chạy.
                    bool wrongWordDialogOk = false;
                    view.ConfirmDialogOpenerForTest = dlg =>
                    {
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            dlg.SetConfirmTextForTest("SAI");
                            wrongWordDialogOk = !dlg.IsConfirmEnabled;
                            dlg.CancelButtonForTest.PerformClick();
                            return dlg.DialogResult;
                        }
                        finally
                        {
                            if (!dlg.IsDisposed)
                                dlg.Close();
                        }
                    };
                    view.RunButtonForTest.PerformClick();
                    bool blockedWrongConfirmOk = wrongWordDialogOk && !view.IsRunning && MrReadExportQty(db, billId, prodA) == 4m;

                    // Gõ đúng "xacnhan" (không phân biệt hoa thường) -> bấm Đồng ý thật -> BackgroundWorker chạy xong, DB cập nhật A = 6!
                    bool rightWordDialogOk = false;
                    DialogResult confirmDr = DialogResult.None;
                    string capturedConfirmWord = "";
                    view.ConfirmDialogOpenerForTest = dlg =>
                    {
                        ShowDialogOffscreen(dlg);
                        try
                        {
                            dlg.SetConfirmTextForTest("xacnhan");
                            rightWordDialogOk = dlg.IsConfirmEnabled;
                            dlg.ConfirmButtonForTest.PerformClick();
                            confirmDr = dlg.DialogResult;
                            capturedConfirmWord = dlg.EnteredText ?? "";
                            return confirmDr;
                        }
                        finally
                        {
                            if (!dlg.IsDisposed)
                                dlg.Close();
                        }
                    };
                    view.RunButtonForTest.PerformClick();

                    var sw = Stopwatch.StartNew();
                    while ((view.IsRunning || (view.LastResultForTest == null && confirmDr == DialogResult.OK && string.IsNullOrEmpty(view.LastWarningMessageForTest)))
                        && sw.ElapsedMilliseconds < 15000)
                    {
                        Application.DoEvents();
                        Thread.Sleep(20);
                    }
                    Application.DoEvents();

                    bool notRunningOk = !view.IsRunning;
                    bool hasResultOk = view.LastResultForTest != null;
                    bool resultOk = hasResultOk && view.LastResultForTest.Ok;
                    int progressVal = view.ProgressForTest.Value;
                    bool progressOk = progressVal == 100;
                    decimal qtyAfterRun = MrReadExportQty(db, billId, prodA);
                    bool qtyOk = qtyAfterRun == 6m;
                    bool executedUiOk = rightWordDialogOk && notRunningOk && hasResultOk && resultOk && progressOk && qtyOk;
                    string runMsg = hasResultOk
                        ? (view.LastResultForTest.Message ?? "")
                        : (view.LastWarningMessageForTest ?? "");

                    // Đổi theme sống -> màu thật của panel, thẻ kết quả và nút nguy hiểm cập nhật theo ThemeTokens.
                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    bool liveThemeOk = view.BackColor == t1.Background
                        && view.ResultCardForTest.BackColor == t1.Surface
                        && view.RunButtonForTest.FillColor == t1.Danger;

                    Check("Xuất lại UI (§2, §4 #4-#6, §5): tháng mặc định, tiêu đề không cắt, chặn ngày trống/ngược, Xem trước, xác nhận XACNHAN & chạy nền BackgroundWorker ra A=6, đổi theme sống",
                        defaultMonthOk && titleNotClipped && emptyDateUiOk && reversedDateUiOk
                        && previewUiOk && blockedWrongConfirmOk && executedUiOk && liveThemeOk,
                        MrOneLine("def=" + defaultMonthOk + ", title=" + titleNotClipped + ", emp=" + emptyDateUiOk
                            + ", rev=" + reversedDateUiOk + ", prev=" + previewUiOk + ", blk=" + blockedWrongConfirmOk
                            + ", dr=" + confirmDr + ", word=" + capturedConfirmWord
                            + ", rightDlg=" + rightWordDialogOk + ", notRun=" + notRunningOk
                            + ", hasRes=" + hasResultOk + ", resOk=" + resultOk
                            + ", prog=" + progressVal + ", qty=" + qtyAfterRun
                            + ", msg=" + runMsg + ", theme=" + liveThemeOk));
                }
                finally { host.Close(); }
            }
        }

        private static void TestMaterialRegenConfirmDialogUi()
        {
            ThemeTokens t0 = BuiltInThemes.Orange;
            ThemeTokens t1 = BuiltInThemes.Ocean;
            ThemeManager.Apply(t0);

            var samplePrev = new MaterialRegenPreview
            {
                Ok = true,
                FromDate = new DateTime(2026, 11, 1),
                ToDate = new DateTime(2026, 11, 30),
                EligibleBillCount = 5,
                ExistingExportLineCount = 12,
                NewExportLineCount = 14
            };
            using (var dlg = new ConfirmMaterialRegenDialog(samplePrev, "Tất cả cửa hàng"))
            {
                ShowDialogOffscreen(dlg);
                try
                {
                    bool defaultCancelOk = dlg.CancelButton == dlg.CancelButtonForTest
                        && dlg.AcceptButton == dlg.CancelButtonForTest;
                    bool initDisabled = !dlg.IsConfirmEnabled;
                    bool textsOk = dlg.SummaryLabelForTest.Text.IndexOf("5", StringComparison.Ordinal) >= 0
                        && dlg.SummaryLabelForTest.Text.IndexOf("12", StringComparison.Ordinal) >= 0
                        && dlg.SummaryLabelForTest.Text.IndexOf("14", StringComparison.Ordinal) >= 0
                        && dlg.WarningLabelForTest.Text == MaterialRegenConstants.ConfirmWarning
                        && dlg.WarningLabelForTest.ForeColor == t0.Danger;

                    dlg.SetConfirmTextForTest("XAC");
                    bool wrongStaysOff = !dlg.IsConfirmEnabled;
                    dlg.SetConfirmTextForTest("XacNhan");
                    bool rightTurnsOn = dlg.IsConfirmEnabled;

                    ThemeManager.Apply(t1);
                    Application.DoEvents();
                    bool dialogThemeOk = dlg.WarningLabelForTest.ForeColor == t1.Danger;

                    Check("Xuất lại UI hộp xác nhận (§2, §4 #5, §5): nút Hủy là mặc định, nêu đủ số liệu & cảnh báo, bắt gõ đúng XACNHAN, màu qua ThemeManager",
                        defaultCancelOk && initDisabled && textsOk && wrongStaysOff && rightTurnsOn && dialogThemeOk,
                        MrOneLine("defCancel=" + defaultCancelOk + ", init=" + initDisabled + ", txt=" + textsOk
                            + ", wrong=" + wrongStaysOff + ", right=" + rightTurnsOn + ", theme=" + dialogThemeOk));
                }
                finally { dlg.Close(); }
            }
        }

        private static void TestMaterialRegenViewOnlyUi(string db, string storeId, string tag)
        {
            var viewPerms = new FunctionPermissions(false);
            viewPerms.Add("fn_xl", MaterialRegenConstants.FunctionName, RoleModes.View);
            var viewSess = new UserSession
            {
                UserId = "u_xl_ro_" + tag, UserName = "u_xl_ro", DisplayName = "Chỉ xem XL",
                IsAdmin = false, DatabasePath = db, Permissions = viewPerms,
                Stores = new List<StoreRecord> { new StoreRecord { Id = storeId, Name = "CH XL" } }
            };

            using (var host = MakeOffscreenHost())
            {
                var view = new MaterialRegenView(viewSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    bool uiPermOk = view.PreviewButtonForTest.Enabled
                        && !view.RunButtonForTest.Enabled
                        && !view.StartExecute();
                    Check("Xuất lại UI §4 #7 (chỉ Xem): nút Xem trước mở, nút Thực hiện bị khóa và không chạy được",
                        uiPermOk,
                        MrOneLine("prevEn=" + view.PreviewButtonForTest.Enabled + ", runEn=" + view.RunButtonForTest.Enabled));
                }
                finally { host.Close(); }
            }
        }

        private static void TestMaterialRegenMenuAndPreview(string db, UserSession adminSess)
        {
            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode regenNode = FindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsMaterialRegenMenu(n));

            bool opensRealView = false;
            if (regenNode != null)
            {
                using (Control v = ScreenFactory.CreateView(regenNode, adminSess))
                    opensRealView = v is MaterialRegenView;
            }

            MenuModel deniedMenu = MainMenuBuilder.Build(new UserSession
            {
                UserId = "u_no_xl", UserName = "u_no_xl", DisplayName = "Không quyền XL",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            });
            bool deniedMenuConsistent = regenNode != null
                && (string.IsNullOrEmpty(regenNode.FunctionId)
                    || FindMenuLeaf(deniedMenu.Roots, n => ScreenFactory.IsMaterialRegenMenu(n)) == null);

            bool previewFormOk = false;
            using (Form pv = ScreenFactory.Create("preview.materialregen", adminSess))
            {
                var f = pv as MaterialRegenPreviewForm;
                if (f != null)
                {
                    ShowDialogOffscreen(f);
                    try
                    {
                        f.Preview("");
                        previewFormOk = f.ViewForTest != null && f.ViewForTest.LastPreviewForTest != null;
                    }
                    finally { f.Close(); }
                }
            }

            Check("Xuất lại §4 #7 (menu): KHO HÀNG > Xuất lại định lượng mở MaterialRegenView thật, ẩn khi không có quyền & preview.materialregen hoạt động",
                regenNode != null && opensRealView && deniedMenuConsistent && previewFormOk,
                MrOneLine("node=" + (regenNode != null) + ", view=" + opensRealView + ", denied=" + deniedMenuConsistent + ", pv=" + previewFormOk));
        }

        // ------------------------------------------------------------------ Hàm trợ giúp dữ liệu mẫu

        private static string MrOneLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text.Replace("\r", " ").Replace("\n", " ");
        }

        private static string MrInsertPurchase(
            string db, List<string> createdOrders, string name, DateTime date, string whId, string storeId,
            string prodId, string unitId, decimal qty, decimal amount)
        {
            string oid = DbHelper.NewId();
            createdOrders.Add(oid);
            DateTime now = DbHelper.Now();
            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DCUAHANGID, TIENHANG, TONGCONG, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, 1, 30, 30, @D, @W, @S, @A, @A, 'selftest_xl', @T)",
                "@I", oid, "@N", name, "@D", date.Date, "@W", whId, "@S", storeId, "@A", amount, "@T", now);
            BookingExec(db,
                "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, DONGIA, THANHTIEN, GIAVON, XUATVATTU, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @O, @P, 'PN-XL', @U, @W, @Q, @Q, 0, 0, @DG, @A, 0, 0, 30, 'selftest_xl', @T)",
                "@I", DbHelper.NewId(), "@O", oid, "@P", prodId, "@U", unitId, "@W", whId,
                "@Q", qty, "@DG", qty > 0m ? amount / qty : 0m, "@A", amount, "@T", now);
            return oid;
        }

        private static string MrInsertBill(
            string db, List<string> createdOrders, string name, DateTime date, int loai, int status, int paid, string tableId, string storeId)
        {
            string oid = DbHelper.NewId();
            createdOrders.Add(oid);
            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DBANID, DCUAHANGID, TONGCONG, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, @L, @ST, @P, @D, @B, @S, 0, 'selftest_xl', @T)",
                "@I", oid, "@N", name, "@L", loai, "@ST", status, "@P", paid, "@D", date.Date,
                "@B", string.IsNullOrEmpty(tableId) ? null : tableId, "@S", storeId, "@T", DbHelper.Now());
            return oid;
        }

        private static string MrInsertDetail(
            string db, string orderId, string prodId, string name, string unitId, string whId, decimal qtyOut, int xuatVatTu, decimal giaVon)
        {
            string lid = DbHelper.NewId();
            BookingExec(db,
                "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, DKHOHANGID, SLNHAP, SLNHAPCHUAQUYDOI, SLXUAT, SLXUATCHUAQUYDOI, DONGIA, THANHTIEN, GIAVON, XUATVATTU, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @O, @P, @N, @U, @W, 0, 0, @Q, @Q, 0, 0, @GV, @X, 30, 'selftest_xl', @T)",
                "@I", lid, "@O", orderId, "@P", prodId, "@N", name, "@U", unitId,
                "@W", string.IsNullOrEmpty(whId) ? null : whId, "@Q", qtyOut, "@GV", giaVon, "@X", xuatVatTu, "@T", DbHelper.Now());
            return lid;
        }

        private static decimal MrReadExportQty(string db, string billId, string prodId)
        {
            return DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                "SELECT COALESCE(SUM(SLXUAT), 0) FROM TDONHANGCHITIET WHERE TDONHANGID = @B AND DMATHANGID = @P AND COALESCE(XUATVATTU, 0) = 30",
                "@B", billId, "@P", prodId)));
        }

        private static string MrReadExportWarehouse(string db, string billId, string prodId)
        {
            return DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null,
                "SELECT FIRST 1 DKHOHANGID FROM TDONHANGCHITIET WHERE TDONHANGID = @B AND DMATHANGID = @P AND COALESCE(XUATVATTU, 0) = 30",
                "@B", billId, "@P", prodId)));
        }

        private static int MrCountAllExportLines(string db, IEnumerable<string> billIds)
        {
            int sum = 0;
            foreach (string id in billIds)
                sum += BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @B AND COALESCE(XUATVATTU, 0) = 30", "@B", id);
            return sum;
        }
    }
}
