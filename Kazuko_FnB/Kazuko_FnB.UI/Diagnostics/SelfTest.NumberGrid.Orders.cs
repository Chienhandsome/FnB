using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void NumberGridBookingAndQuoteTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            var configRepo = new ConfigRepository(db);
            bool oldTwoUnit = false;
            bool configChanged = false;

            string areaId = DbHelper.NewId();
            string roomId = DbHelper.NewId();
            string groupId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prodId = DbHelper.NewId();
            string bookingId = null;
            string quoteId = null;

            try
            {
                var adminSession = new UserSession
                {
                    UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
                };
                AppSession.Current = adminSession;
                SystemConfig.Reload(db);
                oldTwoUnit = SystemConfig.SuDung2DonViTinh;
                configChanged = true;
                configRepo.Save(new[] { new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = 0 } }, "selftest");
                SystemConfig.Reload(db);

                BookingExec(db, "INSERT INTO \"DKHUVUC\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", areaId, "@n", "KV THỬ NGP2");
                BookingExec(db, "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DKHUVUCID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @a, 30, 'selftest')",
                    "@id", roomId, "@n", "Bàn NGP2", "@a", areaId);
                BookingExec(db, "INSERT INTO \"DNHOMMATHANG\" (\"ID\", \"NAME\", \"CODE\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @c, 30, 'selftest')",
                    "@id", groupId, "@n", "NHÓM NGP2", "@c", "NGP2GRP");
                BookingExec(db, "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", unitId, "@n", "LyNG2");
                BookingExec(db,
                    "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"GIABAN\", \"STATUS\", \"USERCREATEDID\") " +
                    "VALUES (@id, @c, @n, @g, @u, @p, 30, 'selftest')",
                    "@id", prodId, "@c", "NGP2MH", "@n", "Mặt hàng NGP2", "@g", groupId, "@u", unitId, "@p", 15000m);

                DateTime now = DateTime.Now;
                var bRow = BookingRow.NewDefault();
                bRow.Name = "DH99/97701"; bRow.Ngay = DateTime.Today; bRow.GioDat = now;
                bRow.TuGio = now; bRow.DenGio = now.AddHours(2); bRow.TuNgay = now.Date; bRow.DenNgay = now.AddHours(2).Date;
                bRow.TenKhach = "Khách NGP2"; bRow.TienHang = 30000m; bRow.TongCong = 30000m;
                BookingSaveResult bSave = new BookingRepository(db).SaveBooking(
                    bRow,
                    new List<BookingLine> { new BookingLine { ProductId = prodId, UnitId = unitId, Quantity = 2m, Price = 15000m, DiscountPct = 0m, Amount = 30000m } },
                    new List<string> { roomId }, "selftest");
                if (bSave.Ok) bookingId = bSave.SavedId;

                var qRow = QuoteRow.NewDefault();
                qRow.Name = "BG99/97701"; qRow.Ngay = DateTime.Today; qRow.TenKhach = "Khách BG NGP2";
                qRow.TienHang = 30000m; qRow.TongCong = 30000m;
                QuoteSaveResult qSave = new QuoteService(adminSession).SaveQuote(
                    qRow,
                    new List<QuoteLine> { new QuoteLine { ProductId = prodId, UnitId = unitId, Quantity = 2m, Price = 15000m, DiscountPct = 0m, Amount = 30000m, Note = "Ghi chú ban đầu" } });
                if (qSave.Ok) quoteId = qSave.SavedId;

                foreach (string cultureName in new[] { "en-US", "vi-VN" })
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    TestBookingGridNumberInput(db, adminSession, bookingId, prodId, cultureName);
                    TestQuoteGridNumberInput(db, adminSession, quoteId, cultureName);
                }
            }
            finally
            {
                if (configChanged)
                    configRepo.Save(new[] { new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = oldTwoUnit ? 30 : 0 } }, "selftest");
                AppSession.Current = savedSession;
                SystemConfig.Reload(db);
                if (!string.IsNullOrEmpty(bookingId))
                {
                    BookingExec(db, "DELETE FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" = @id", "@id", bookingId);
                    BookingExec(db, "DELETE FROM \"TDATHANG\" WHERE \"ID\" = @id", "@id", bookingId);
                }
                if (!string.IsNullOrEmpty(quoteId))
                {
                    BookingExec(db, "DELETE FROM \"TBAOGIACHITIET\" WHERE \"TBAOGIAID\" = @id", "@id", quoteId);
                    BookingExec(db, "DELETE FROM \"TBAOGIA\" WHERE \"ID\" = @id", "@id", quoteId);
                }
                BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" = @id", "@id", prodId);
                BookingExec(db, "DELETE FROM \"DNHOMMATHANG\" WHERE \"ID\" = @id", "@id", groupId);
                BookingExec(db, "DELETE FROM \"DDONVITINH\" WHERE \"ID\" = @id", "@id", unitId);
                BookingExec(db, "DELETE FROM \"DBAN\" WHERE \"ID\" = @id", "@id", roomId);
                BookingExec(db, "DELETE FROM \"DKHUVUC\" WHERE \"ID\" = @id", "@id", areaId);
            }
        }

        private static void TestBookingGridNumberInput(string db, UserSession session, string bookingId, string prodId, string cultureTag)
        {
            if (string.IsNullOrEmpty(bookingId))
            {
                Check("Nhập số P2 [Đặt hàng lưới " + cultureTag + "]: tạo dữ liệu mẫu", false, "bookingId null");
                return;
            }
            BookingExec(db, "UPDATE \"TDATHANGCHITIET\" SET \"SOLUONG\" = 2, \"DONGIA\" = 15000, \"TILEGIAMGIA\" = 0, \"THANHTIEN\" = 30000 WHERE \"TDATHANGID\" = @id", "@id", bookingId);
            using (BookingEditDialog dlg = OpenOffscreenBookingDialog(session, bookingId))
            {
                try
                {
                    DataGridView g = dlg.DetailGridForTest;
                    bool colsOk = true;
                    string colDiag = "";

                    // Kiểm từng cột số sửa được (d_qty, d_price, d_disc): WM_CHAR 2.128 -> 2,12; dán 3.456 -> 3,45; WM_CHAR 1.500.000 & dán 1.500.000 giữ số cũ.
                    foreach (string col in new[] { "d_qty", "d_price", "d_disc" })
                    {
                        TextBox ed = BeginGridEditReal(g, 0, col);
                        if (ed == null) { colsOk = false; colDiag += col + ":noEdit "; break; }
                        TypeCharsIntoGridEditor(ed, "2.128", true);
                        string typed2128 = ed.Text;
                        LeaveGridCellReal(g, 0, "d_name");
                        decimal valTyped = Convert.ToDecimal(g.Rows[0].Cells[col].Value);

                        ed = BeginGridEditReal(g, 0, col);
                        PasteTextIntoGridEditor(ed, "3.456", true);
                        string pasted3456 = ed.Text;
                        LeaveGridCellReal(g, 0, "d_name");
                        decimal valPasted = Convert.ToDecimal(g.Rows[0].Cells[col].Value);

                        // Gõ từng phím 1.500.000 -> giữ đủ 2 dấu phẩy -> rời ô giữ số cũ 3.45m.
                        ed = BeginGridEditReal(g, 0, col);
                        TypeCharsIntoGridEditor(ed, "1.500.000", true);
                        string typedTwoDot = ed.Text;
                        LeaveGridCellReal(g, 0, "d_name");
                        decimal afterTypedBad = Convert.ToDecimal(g.Rows[0].Cells[col].Value);
                        string warnAfterBad = dlg.LastWarningForTest;

                        // Dán 1.500.000 -> giữ "1,500,000" -> rời ô giữ số cũ 3.45m.
                        ed = BeginGridEditReal(g, 0, col);
                        PasteTextIntoGridEditor(ed, "1.500.000", true);
                        string pastedTwoDot = ed.Text;
                        LeaveGridCellReal(g, 0, "d_name");
                        decimal afterPastedBad = Convert.ToDecimal(g.Rows[0].Cells[col].Value);

                        bool expectWarn = col == "d_qty";
                        bool warnOk = expectWarn ? warnAfterBad == BookingText.EnterQuantity : warnAfterBad == null;
                        bool oneOk = typed2128 == "2,12" && valTyped == 2.12m
                            && pasted3456 == "3,45" && valPasted == 3.45m
                            && CountChar(typedTwoDot, ',') >= 2 && afterTypedBad == 3.45m && warnOk
                            && pastedTwoDot == "1,500,000" && afterPastedBad == 3.45m;
                        if (!oneOk)
                        {
                            colsOk = false;
                            colDiag += col + "(t=" + typed2128 + "/" + valTyped + ",p=" + pasted3456 + "/" + valPasted
                                + ",b=" + typedTwoDot + "/" + afterTypedBad + ",pb=" + pastedTwoDot + ",w=" + warnAfterBad + ") ";
                        }
                    }

                    // Gõ dấu chấm vào giữa "12345" (sau chữ số đầu) -> cắt còn "1,23"; d_price 15000,5 hiện "15.000,5" và sửa hiện "15000,5".
                    TextBox edMid = BeginGridEditReal(g, 0, "d_price");
                    TypeCharsIntoGridEditor(edMid, "12345", true);
                    edMid.SelectionStart = 1;
                    edMid.SelectionLength = 0;
                    TypeCharsIntoGridEditor(edMid, ".", false);
                    string midText = edMid.Text;
                    LeaveGridCellReal(g, 0, "d_name");
                    decimal midVal = Convert.ToDecimal(g.Rows[0].Cells["d_price"].Value);

                    TextBox edPrice = BeginGridEditReal(g, 0, "d_price");
                    TypeCharsIntoGridEditor(edPrice, "15000.5", true);
                    LeaveGridCellReal(g, 0, "d_name");
                    string priceFmt = Convert.ToString(g.Rows[0].Cells["d_price"].FormattedValue);
                    TextBox edRe = BeginGridEditReal(g, 0, "d_price");
                    string priceReEdit = edRe != null ? edRe.Text : "";
                    LeaveGridCellReal(g, 0, "d_name");

                    // A9: đang sửa d_qty = 4.5 (CHƯA rời ô), bấm thẳng nút Lưu -> lưu DB đúng 4,5.
                    TextBox edSave = BeginGridEditReal(g, 0, "d_qty");
                    TypeCharsIntoGridEditor(edSave, "4.5", true);
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    BookingRow reloaded = new BookingRepository(db).LoadBooking(bookingId);
                    bool saveImmediateOk = reloaded != null && reloaded.Lines.Count == 1 && reloaded.Lines[0].Quantity == 4.5m;

                    // A9: đang sửa d_qty = 5.25 (CHƯA rời ô), bấm Thêm (ô SL ngoài = 1) -> commit 5,25 trước rồi gộp thành 6,25.
                    SelectBookingProductRow(dlg.ProductGridForTest, prodId);
                    dlg.QtyBoxForTest.Value = 1m;
                    TextBox edAdd = BeginGridEditReal(g, 0, "d_qty");
                    TypeCharsIntoGridEditor(edAdd, "5.25", true);
                    dlg.AddProductButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool addImmediateOk = Convert.ToDecimal(g.Rows[0].Cells["d_qty"].Value) == 6.25m;

                    Check("Nhập số P2 [BookingEditDialog lưới " + cultureTag + " A2b/A3/A6/A9/A10]: 3 cột chặn 2.128->2,12, 1.500.000 giữ cũ, giữa số 1,23, giá 15.000,5, A9 Lưu/Thêm, 0 DataError",
                        colsOk && midText == "1,23" && midVal == 1.23m && priceFmt == "15.000,5" && priceReEdit == "15000,5"
                        && saveImmediateOk && addImmediateOk && dlg.DetailDataErrorCountForTest == 0,
                        colDiag + "mid=" + midText + "/" + midVal + ", pf=" + priceFmt + "/" + priceReEdit
                        + ", save=" + saveImmediateOk + ", add=" + addImmediateOk + ", de=" + dlg.DetailDataErrorCountForTest
                        + FormatDataErrorSuffix(dlg.DetailDataErrorDetailForTest));
                }
                finally { dlg.Close(); }
            }
        }

        private static void TestQuoteGridNumberInput(string db, UserSession session, string quoteId, string cultureTag)
        {
            if (string.IsNullOrEmpty(quoteId))
            {
                Check("Nhập số P2 [Báo giá lưới " + cultureTag + "]: tạo dữ liệu mẫu", false, "quoteId null");
                return;
            }
            BookingExec(db, "UPDATE \"TBAOGIACHITIET\" SET \"SOLUONG\" = 2, \"DONGIA\" = 15000, \"TILEGIAMGIA\" = 0, \"THANHTIEN\" = 30000, \"NOTE\" = 'Ghi chú ban đầu' WHERE \"TBAOGIAID\" = @id", "@id", quoteId);
            using (var dlg = new QuoteEditDialog(session, quoteId, null))
            {
                dlg.SuppressPopup = true;
                dlg.WindowState = FormWindowState.Normal;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    DataGridView g = dlg.DetailGridForTest;

                    // A4: vào rồi ra cả 3 cột số KHÔNG đổi gì -> không dirty, nút Lưu vẫn tắt, không tính lại tổng.
                    bool startClean = !dlg.DirtyForTest && !dlg.SaveButtonForTest.Enabled && !dlg.TotalsDirtyForTest;
                    bool a4NoChangeOk = startClean;
                    foreach (string col in new[] { "d_qty", "d_price", "d_disc" })
                    {
                        TextBox ed = BeginGridEditReal(g, 0, col);
                        string initTxt = ed != null ? ed.Text : "";
                        LeaveGridCellReal(g, 0, "d_name");
                        a4NoChangeOk = a4NoChangeOk && ed != null && initTxt.IndexOf('.') < 0
                            && !dlg.DirtyForTest && !dlg.SaveButtonForTest.Enabled && !dlg.TotalsDirtyForTest
                            && dlg.LastWarningMessageForTest == null;
                    }

                    // Mỗi cột số sửa được: WM_CHAR 2.128 -> 2,12; dán 3.456 -> 3,45; WM_CHAR & dán 1.500.000 giữ số cũ.
                    bool colsOk = true;
                    string colDiag = "";
                    foreach (string col in new[] { "d_qty", "d_price", "d_disc" })
                    {
                        TextBox ed = BeginGridEditReal(g, 0, col);
                        if (ed == null) { colsOk = false; colDiag += col + ":noEdit "; break; }
                        TypeCharsIntoGridEditor(ed, "2.128", true);
                        string typed2128 = ed.Text;
                        LeaveGridCellReal(g, 0, "d_name");
                        decimal valTyped = Convert.ToDecimal(g.Rows[0].Cells[col].Value);

                        ed = BeginGridEditReal(g, 0, col);
                        PasteTextIntoGridEditor(ed, "3.456", true);
                        string pasted3456 = ed.Text;
                        LeaveGridCellReal(g, 0, "d_name");
                        decimal valPasted = Convert.ToDecimal(g.Rows[0].Cells[col].Value);

                        ed = BeginGridEditReal(g, 0, col);
                        TypeCharsIntoGridEditor(ed, "1.500.000", true);
                        string typedTwoDot = ed.Text;
                        LeaveGridCellReal(g, 0, "d_name");
                        decimal afterTypedBad = Convert.ToDecimal(g.Rows[0].Cells[col].Value);
                        string warnBad = dlg.LastWarningMessageForTest;

                        ed = BeginGridEditReal(g, 0, col);
                        PasteTextIntoGridEditor(ed, "1.500.000", true);
                        string pastedTwoDot = ed.Text;
                        LeaveGridCellReal(g, 0, "d_name");
                        decimal afterPastedBad = Convert.ToDecimal(g.Rows[0].Cells[col].Value);

                        bool expectWarn = col == "d_qty";
                        bool warnOk = expectWarn ? warnBad == QuoteText.EnterQuantity : warnBad == null;
                        bool oneOk = typed2128 == "2,12" && valTyped == 2.12m
                            && pasted3456 == "3,45" && valPasted == 3.45m
                            && CountChar(typedTwoDot, ',') >= 2 && afterTypedBad == 3.45m && warnOk
                            && pastedTwoDot == "1,500,000" && afterPastedBad == 3.45m;
                        if (!oneOk)
                        {
                            colsOk = false;
                            colDiag += col + "(t=" + typed2128 + "/" + valTyped + ",p=" + pasted3456 + "/" + valPasted
                                + ",b=" + typedTwoDot + "/" + afterTypedBad + ",w=" + warnBad + ") ";
                        }
                    }

                    // Cột chữ d_note SAU khi đã sửa các cột số: gõ "a.b 1.2345" giữ nguyên chuỗi (DetachEditingControl đã gỡ handler).
                    TextBox edNote = BeginGridEditReal(g, 0, "d_note");
                    TypeCharsIntoGridEditor(edNote, "a.b 1.2345", true);
                    string noteTyping = edNote != null ? edNote.Text : "";
                    LeaveGridCellReal(g, 0, "d_name");
                    string noteSaved = Convert.ToString(g.Rows[0].Cells["d_note"].Value);
                    bool noteTextOk = noteTyping == "a.b 1.2345" && noteSaved == "a.b 1.2345" && dlg.LinesForTest[0].Note == "a.b 1.2345";

                    // Hiển thị vi-VN có ngăn nghìn & số lẻ: d_price 15000.5 -> FormattedValue "15.000,5", vào sửa "15000,5".
                    TextBox edP = BeginGridEditReal(g, 0, "d_price");
                    TypeCharsIntoGridEditor(edP, "15000.5", true);
                    LeaveGridCellReal(g, 0, "d_name");
                    string pFmt = Convert.ToString(g.Rows[0].Cells["d_price"].FormattedValue);

                    // A9: đang sửa d_qty = 3.25 (CHƯA rời ô), bấm thẳng Lưu -> lưu đúng 3,25 xuống DB; sau khi lưu, sửa mỗi d_note vẫn đánh dấu dirty.
                    TextBox edSave = BeginGridEditReal(g, 0, "d_qty");
                    TypeCharsIntoGridEditor(edSave, "3.25", true);
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    QuoteRow dbQuote = new QuoteService(session).LoadQuote(quoteId);
                    int dbLineCount = dbQuote != null ? dbQuote.Lines.Count : -1;
                    decimal dbQty = dbLineCount >= 1 ? dbQuote.Lines[0].Quantity : -1m;
                    string dbNote = dbLineCount >= 1 ? dbQuote.Lines[0].Note : "<null>";
                    bool dirtyAfterSave = dlg.DirtyForTest;
                    bool saveImmediateOk = dbQuote != null && dbLineCount == 1
                        && dbQty == 3.25m && dbNote == "a.b 1.2345"
                        && !dirtyAfterSave;

                    TextBox edNote2 = BeginGridEditReal(g, 0, "d_note");
                    TypeCharsIntoGridEditor(edNote2, "1.2345", true);
                    LeaveGridCellReal(g, 0, "d_name");
                    bool noteDirtyOk = dlg.DirtyForTest && dlg.SaveButtonForTest.Enabled
                        && Convert.ToString(g.Rows[0].Cells["d_note"].Value) == "1.2345";

                    Check("Nhập số P2 [QuoteEditDialog lưới " + cultureTag + " A2b/A3/A4/A6/A9/A10]: A4 không đổi, 3 cột N4/1.500.000, d_note 'a.b 1.2345' & dirty, A9 Lưu DB, 0 DataError",
                        a4NoChangeOk && colsOk && noteTextOk && pFmt == "15.000,5" && saveImmediateOk && noteDirtyOk
                        && dlg.DetailDataErrorCountForTest == 0,
                        "a4=" + a4NoChangeOk + ", " + colDiag + "note=" + noteTyping + "/" + noteSaved
                        + ", pf=" + pFmt + ", save=" + saveImmediateOk
                        + "(cnt=" + dbLineCount + ",q=" + dbQty + ",n=" + dbNote + ",dirty=" + dirtyAfterSave + ")"
                        + ", nd=" + noteDirtyOk + ", de=" + dlg.DetailDataErrorCountForTest
                        + FormatDataErrorSuffix(dlg.DetailDataErrorDetailForTest));
                }
                finally { dlg.Close(); }
            }
        }
    }
}
