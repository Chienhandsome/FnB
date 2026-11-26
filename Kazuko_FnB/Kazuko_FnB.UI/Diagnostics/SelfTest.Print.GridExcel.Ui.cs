using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP3aGridExcelUiTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            var adminSession = new UserSession
            {
                UserId = "selftest_p3a",
                UserName = "selftest_p3a",
                DisplayName = "Selftest P3a",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            AppSession.Current = adminSession;

            P2PrintFixture fx = null;
            string pBooking = Path.Combine(Path.GetTempPath(), "kazuko-p3a-booking-" + Guid.NewGuid().ToString("N") + ".xls");
            string pQuote = Path.Combine(Path.GetTempPath(), "kazuko-p3a-quote-" + Guid.NewGuid().ToString("N") + ".xls");
            string pCatalogAdmin = Path.Combine(Path.GetTempPath(), "kazuko-p3a-cat-adm-" + Guid.NewGuid().ToString("N") + ".xls");
            string pCatalogNoBuy = Path.Combine(Path.GetTempPath(), "kazuko-p3a-cat-nobuy-" + Guid.NewGuid().ToString("N") + ".xls");
            string pCatalogRoom = Path.Combine(Path.GetTempPath(), "kazuko-p3a-cat-room-" + Guid.NewGuid().ToString("N") + ".xls");
            string pBookingEdit = Path.Combine(Path.GetTempPath(), "kazuko-p3a-bedit-" + Guid.NewGuid().ToString("N") + ".xls");
            string pQuoteEdit = Path.Combine(Path.GetTempPath(), "kazuko-p3a-qedit-" + Guid.NewGuid().ToString("N") + ".xls");
            try
            {
                fx = CreateP2PrintFixture(db);
                // Gán giá nhập đặc trưng cho Cà phê sữa để kiểm quyền Xem giá nhập trên CatalogView.
                BookingExec(db, "UPDATE \"DMATHANG\" SET \"GIANHAP\" = 777777 WHERE \"ID\" = @id", "@id", fx.CoffeeId);

                // Tạo 1 phiếu đặt hàng mẫu (2 dòng) và 1 phiếu báo giá mẫu (2 dòng).
                string bookingId = CreateP3aSampleBooking(db, fx);
                string quoteId = CreateP3aSampleQuote(adminSession, fx);

                string dbSnapBefore = SnapshotP3aDbState(db);
                Dictionary<string, string> tplSnapBefore = SnapshotStemplate(db);

                TestBookingListExcelButton(db, bookingId, pBooking);
                TestQuoteListExcelButton(adminSession, pQuote);
                TestCatalogViewExcelButtons(db, adminSession, pCatalogAdmin, pCatalogNoBuy, pCatalogRoom);
                TestBookingEditExcelButton(adminSession, bookingId, pBookingEdit);
                TestQuoteEditExcelButton(adminSession, quoteId, pQuoteEdit);

                string dbSnapAfter = SnapshotP3aDbState(db);
                Dictionary<string, string> tplSnapAfter = SnapshotStemplate(db);
                Check("In P3a (ix/T5 CSDL không đổi sau khi xuất Excel): số bản ghi các bảng nghiệp vụ và STEMPLATE giữ nguyên",
                    dbSnapBefore == dbSnapAfter && SnapshotsEqual(tplSnapBefore, tplSnapAfter),
                    "db=" + (dbSnapBefore == dbSnapAfter) + ",tpl=" + SnapshotsEqual(tplSnapBefore, tplSnapAfter));
            }
            finally
            {
                CleanupP2PrintFixture(db, fx);
                AppSession.Current = savedSession;
                SafeDeleteFile(pBooking);
                SafeDeleteFile(pQuote);
                SafeDeleteFile(pCatalogAdmin);
                SafeDeleteFile(pCatalogNoBuy);
                SafeDeleteFile(pCatalogRoom);
                SafeDeleteFile(pBookingEdit);
                SafeDeleteFile(pQuoteEdit);
            }
        }

        private static string CreateP3aSampleBooking(string db, P2PrintFixture fx)
        {
            var row = new BookingRow
            {
                Name = "DH26/P3A01",
                Ngay = DateTime.Today,
                GioDat = DateTime.Now,
                TuGio = DateTime.Today.AddHours(18),
                DenGio = DateTime.Today.AddHours(20),
                TuNgay = DateTime.Today,
                DenNgay = DateTime.Today,
                TenKhach = "Khách P3a Đặt Bàn",
                DiaChi = "12 Nguyễn Trãi",
                DienThoai = "0909888777",
                TienHang = 110000m,
                TongCong = 110000m,
                Note = "Ghi chú P3a"
            };
            var lines = new List<BookingLine>
            {
                new BookingLine { ProductId = fx.CoffeeId, ProductName = "Cà phê sữa", UnitId = fx.UnitId, UnitName = "Ly", Quantity = 2m, Price = 25000m, Amount = 50000m },
                new BookingLine { ProductId = fx.PeachTeaId, ProductName = "Trà đào", UnitId = fx.UnitId, UnitName = "Ly", Quantity = 2m, Price = 30000m, Amount = 60000m }
            };
            BookingSaveResult res = new BookingRepository(db).SaveBooking(row, lines, new List<string> { fx.Room1Id }, "selftest_p3a");
            if (res.Ok)
                fx.CreatedBookingIds.Add(res.SavedId);
            return res.SavedId;
        }

        private static string CreateP3aSampleQuote(UserSession adminSession, P2PrintFixture fx)
        {
            var q = QuoteRow.NewDefault();
            q.Name = "BG26/P3A01";
            q.Ngay = DateTime.Today;
            q.TenKhach = "Khách P3a Báo Giá";
            q.DiaChi = "34 Hai Bà Trưng";
            q.DienThoai = "0909111222";
            q.TienHang = 80000m;
            q.TongCong = 80000m;
            var lines = new List<QuoteLine>
            {
                new QuoteLine { ProductId = fx.CoffeeId, UnitId = fx.UnitId, Quantity = 2m, Price = 25000m, Amount = 50000m, Note = "Ít đá" },
                new QuoteLine { ProductId = fx.PeachTeaId, UnitId = fx.UnitId, Quantity = 1m, Price = 30000m, Amount = 30000m, Note = "Nhiều đào" }
            };
            QuoteSaveResult res = new QuoteService(adminSession).SaveQuote(q, lines);
            if (res.Ok)
                fx.CreatedQuoteIds.Add(res.SavedId);
            return res.SavedId;
        }

        private static void TestBookingListExcelButton(string db, string bookingId, string outPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new BookingListView(db) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.FromDateBoxForTest.Value = DateTime.Today.AddDays(-1);
                    view.ToDateBoxForTest.Value = DateTime.Today.AddDays(1);
                    view.NumberBoxForTest.Text = "DH26/P3A01";
                    view.ReloadForTest();
                    Application.DoEvents();

                    var rec = new GridExportOutputRecorder { NextSavePath = outPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();

                    XlsSheetSnapshot snap = File.Exists(outPath) ? ReadFirstXlsSheet(outPath) : null;
                    bool ok = !string.IsNullOrEmpty(bookingId)
                        && rec.PromptSaveCount == 1 && rec.LastDefaultFileName == ""
                        && rec.ConfirmOpenCount == 1 && rec.OpenFileCount == 0
                        && snap != null
                        && HeadersMatch(snap, VisibleGridHeaders(view.GridForTest))
                        && snap.RowCount == CountVisibleGridRows(view.GridForTest) + 1
                        && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "DH26/P3A01")
                        && XlsSheetContainsSubstring(snap, "Khách P3a Đặt Bàn");

                    Check("In P3a (viii E2E BookingListView nút Xuất excel): xuất đúng lưới đang lọc & tiêu đề cột, tên file mặc định rỗng",
                        ok, snap == null ? "no-file" : ("rows=" + snap.RowCount + ",cols=" + snap.ColCount));
                }
                finally { host.Close(); }
            }
        }

        private static void TestQuoteListExcelButton(UserSession adminSession, string outPath)
        {
            using (var host = MakeOffscreenHost())
            {
                var view = new QuoteListView(adminSession) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.NumberBoxForTest.Text = "BG26/P3A01";
                    view.FilterButtonForTest.PerformClick();
                    Application.DoEvents();

                    var rec = new GridExportOutputRecorder { NextSavePath = outPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelButtonForTest.PerformClick();
                    Application.DoEvents();

                    XlsSheetSnapshot snap = File.Exists(outPath) ? ReadFirstXlsSheet(outPath) : null;
                    bool ok = rec.PromptSaveCount == 1 && rec.LastDefaultFileName == ""
                        && rec.ConfirmOpenCount == 1 && rec.OpenFileCount == 0
                        && snap != null
                        && HeadersMatch(snap, VisibleGridHeaders(view.GridForTest))
                        && snap.RowCount == CountVisibleGridRows(view.GridForTest) + 1
                        && snap.RowCount == 2
                        && XlsSheetContainsSubstring(snap, "BG26/P3A01")
                        && XlsSheetContainsSubstring(snap, "Khách P3a Báo Giá");

                    Check("In P3a (viii E2E QuoteListView nút Xuất excel): xuất đúng lưới đang lọc & tiêu đề cột, tên file mặc định rỗng",
                        ok, snap == null ? "no-file" : ("rows=" + snap.RowCount + ",cols=" + snap.ColCount));
                }
                finally { host.Close(); }
            }
        }

        private static void TestCatalogViewExcelButtons(
            string db,
            UserSession adminSession,
            string adminOutPath,
            string noBuyOutPath,
            string roomOutPath)
        {
            var catRepo = new CatalogRepository(db);
            string productCatId = catRepo.FindIdByTable("DMATHANG");
            string roomCatId = catRepo.FindIdByTable("DBAN");

            // 1. Tab mặt hàng (admin có quyền Xem giá nhập): xuất đúng kết quả đang tìm + có cột Giá nhập.
            AppSession.Current = adminSession;
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, productCatId, "Mặt hàng") { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.SetSearch("P2-CF");
                    Application.DoEvents();

                    var rec = new GridExportOutputRecorder { NextSavePath = adminOutPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();

                    XlsSheetSnapshot snap = File.Exists(adminOutPath) ? ReadFirstXlsSheet(adminOutPath) : null;
                    bool ok = rec.PromptSaveCount == 1 && rec.LastDefaultFileName == ""
                        && snap != null
                        && HeadersMatch(snap, VisibleGridHeaders(view.GridForTest))
                        && snap.RowCount == CountVisibleGridRows(view.GridForTest) + 1
                        && snap.RowCount >= 2
                        && XlsSheetContainsSubstring(snap, "Cà phê sữa")
                        && XlsSheetContainsSubstring(snap, "Giá nhập")
                        && XlsSheetContainsSubstring(snap, "777777");

                    Check("In P3a (viii E2E CatalogView tab Mặt hàng nút Xuất excel): xuất đúng lưới đang lọc & đủ cột theo quyền",
                        ok, snap == null ? "no-file" : ("rows=" + snap.RowCount + ",cols=" + snap.ColCount));
                }
                finally { host.Close(); }
            }

            // 2. Tab mặt hàng với người dùng KHÔNG có quyền "Xem giá nhập" (vii/T4): cột Giá nhập & số 777777 không vào file.
            var noBuyPerms = new FunctionPermissions(false);
            noBuyPerms.Add("fn_mh", ProductText.FunctionName, RoleModes.View);
            AppSession.Current = new UserSession
            {
                UserId = "u_nobuy_p3a",
                UserName = "u_nobuy_p3a",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = noBuyPerms
            };
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, productCatId, "Mặt hàng") { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.SetSearch("P2-CF");
                    Application.DoEvents();

                    var rec = new GridExportOutputRecorder { NextSavePath = noBuyOutPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();

                    XlsSheetSnapshot snap = File.Exists(noBuyOutPath) ? ReadFirstXlsSheet(noBuyOutPath) : null;
                    bool ok = snap != null
                        && HeadersMatch(snap, VisibleGridHeaders(view.GridForTest))
                        && XlsSheetContainsSubstring(snap, "Cà phê sữa")
                        && !XlsSheetContainsSubstring(snap, "Giá nhập")
                        && !XlsSheetContainsSubstring(snap, "777777");

                    Check("In P3a (vii/T4 CatalogView không có quyền Xem giá nhập): file Excel không chứa tiêu đề Giá nhập và không lộ giá 777777",
                        ok, snap == null ? "no-file" : ("rows=" + snap.RowCount + ",cols=" + snap.ColCount));
                }
                finally
                {
                    host.Close();
                    AppSession.Current = adminSession;
                }
            }

            // 3. Tab bàn/phòng (DBAN): bấm Xuất excel xuất đúng lưới bàn.
            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, roomCatId, "Bàn/Phòng") { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.SetSearch("Phòng VIP 1");
                    Application.DoEvents();

                    var rec = new GridExportOutputRecorder { NextSavePath = roomOutPath, ConfirmOpenResult = false };
                    view.GridExportOutputForTest = rec;
                    view.ExcelExportButtonForTest.PerformClick();
                    Application.DoEvents();

                    XlsSheetSnapshot snap = File.Exists(roomOutPath) ? ReadFirstXlsSheet(roomOutPath) : null;
                    bool ok = snap != null
                        && HeadersMatch(snap, VisibleGridHeaders(view.GridForTest))
                        && snap.RowCount == CountVisibleGridRows(view.GridForTest) + 1
                        && XlsSheetContainsSubstring(snap, "Phòng VIP 1");

                    Check("In P3a (viii E2E CatalogView tab Bàn/Phòng nút Xuất excel): xuất đúng lưới bàn đang hiện",
                        ok, snap == null ? "no-file" : ("rows=" + snap.RowCount + ",cols=" + snap.ColCount));
                }
                finally { host.Close(); }
            }
        }

        private static void TestBookingEditExcelButton(UserSession adminSession, string bookingId, string outPath)
        {
            using (var dlg = new BookingEditDialog(adminSession, bookingId, null))
            {
                dlg.SuppressPopup = true;
                dlg.WindowState = FormWindowState.Normal;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    var rec = new GridExportOutputRecorder { NextSavePath = outPath, ConfirmOpenResult = false };
                    dlg.GridExportOutputForTest = rec;
                    dlg.ExportExcelButtonForTest.PerformClick();
                    Application.DoEvents();

                    XlsSheetSnapshot snap = File.Exists(outPath) ? ReadFirstXlsSheet(outPath) : null;
                    bool ok = rec.PromptSaveCount == 1 && rec.LastDefaultFileName == ""
                        && snap != null
                        && HeadersMatch(snap, VisibleGridHeaders(dlg.DetailGridForTest))
                        && snap.RowCount == 3
                        && XlsSheetContainsSubstring(snap, "Cà phê sữa")
                        && XlsSheetContainsSubstring(snap, "Trà đào")
                        && XlsSheetContainsSubstring(snap, "50000")
                        && XlsSheetContainsSubstring(snap, "60000");

                    Check("In P3a (viii E2E BookingEditDialog nút Xuất excel): xuất đúng lưới chi tiết 2 dòng & tiêu đề cột",
                        ok, snap == null ? "no-file" : ("rows=" + snap.RowCount + ",cols=" + snap.ColCount));
                }
                finally { dlg.Close(); }
            }
        }

        private static void TestQuoteEditExcelButton(UserSession adminSession, string quoteId, string outPath)
        {
            using (var dlg = new QuoteEditDialog(adminSession, quoteId, null))
            {
                dlg.SuppressPopup = true;
                dlg.WindowState = FormWindowState.Normal;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    var rec = new GridExportOutputRecorder { NextSavePath = outPath, ConfirmOpenResult = false };
                    dlg.GridExportOutputForTest = rec;
                    dlg.ExportExcelButtonForTest.PerformClick();
                    Application.DoEvents();

                    XlsSheetSnapshot snap = File.Exists(outPath) ? ReadFirstXlsSheet(outPath) : null;
                    bool ok = rec.PromptSaveCount == 1 && rec.LastDefaultFileName == ""
                        && snap != null
                        && HeadersMatch(snap, VisibleGridHeaders(dlg.DetailGridForTest))
                        && snap.RowCount == 3
                        && XlsSheetContainsSubstring(snap, "Cà phê sữa")
                        && XlsSheetContainsSubstring(snap, "Trà đào")
                        && XlsSheetContainsSubstring(snap, "50000")
                        && XlsSheetContainsSubstring(snap, "30000");

                    Check("In P3a (viii E2E QuoteEditDialog nút Xuất excel): xuất đúng lưới chi tiết 2 dòng & tiêu đề cột",
                        ok, snap == null ? "no-file" : ("rows=" + snap.RowCount + ",cols=" + snap.ColCount));
                }
                finally { dlg.Close(); }
            }
        }
    }
}
