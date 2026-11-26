using System;
using System.Collections.Generic;
using System.Globalization;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP2BookingTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            var adminSession = new UserSession
            {
                UserId = "selftest_p2_dh",
                UserName = "nv_p2",
                DisplayName = "Nguyễn Văn In",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            AppSession.Current = adminSession;

            var tplRepo = new PrintTemplateRepository(db);
            var docRepo = new DocumentPrintRepository(db);
            var bookingRepo = new BookingRepository(db);
            var phieuThuRepo = new PhieuThuRepository(db);

            // 1. Đăng ký hàm ToVndWords lặp nhiều lần không văng; kiểm kết quả đọc số 204.940.
            bool repeatRegisterOk = true;
            string words204940 = "";
            try
            {
                FastReportFunctions.EnsureRegistered();
                FastReportFunctions.EnsureRegistered();
                FastReportFunctions.EnsureRegistered();
                words204940 = No1Lib.Sys.DocSo.ToVndWords(204940m) ?? "";
            }
            catch (Exception ex)
            {
                repeatRegisterOk = false;
                words204940 = SingleLineDetail(ex.Message);
            }
            Check("In P2 Đặt hàng (đăng ký ToVndWords lặp): gọi lặp 3 lần không văng & đọc đúng 204.940 thành chữ",
                repeatRegisterOk && FastReportFunctions.IsRegistered
                && words204940.IndexOf("Hai trăm linh bốn ngàn, chín trăm bốn mươi đồng chẵn", StringComparison.OrdinalIgnoreCase) >= 0,
                SingleLineDetail(words204940));

            // 2. Đọc mẫu theo STABLEDESCID TDATHANG: đúng 5 mẫu STATUS=30 có TEMPLATE & đúng thứ tự 54mm -> 54mm(2 dòng) -> 80mm -> A4 -> A5.
            List<PrintTemplateRecord> rawTemplates = tplRepo.LoadTemplatesForTable(PrintConstants.BookingTableDescId);
            int defaultIdx;
            List<PrintTemplateRecord> ordered = PrintTemplateOrdering.OrderAndFindDefault(
                rawTemplates, PrintDialogOptions.ForBookingReportDialog(), out defaultIdx);

            bool count5Ok = ordered.Count == 5;
            bool order5Ok = count5Ok
                && PrintTemplateOrdering.BookingTemplateRank(ordered[0].Name) == 0
                && PrintTemplateOrdering.BookingTemplateRank(ordered[1].Name) == 1
                && PrintTemplateOrdering.BookingTemplateRank(ordered[2].Name) == 2
                && PrintTemplateOrdering.BookingTemplateRank(ordered[3].Name) == 3
                && PrintTemplateOrdering.BookingTemplateRank(ordered[4].Name) == 4
                && defaultIdx == 3
                && string.Equals(ordered[defaultIdx].Name, PrintConstants.DefaultBookingTemplateName, StringComparison.OrdinalIgnoreCase);

            var withUnknown = new List<PrintTemplateRecord>(rawTemplates);
            withUnknown.Add(new PrintTemplateRecord { Id = "u1", Name = "Mẫu lạ 100 mm" });
            int uDef;
            List<PrintTemplateRecord> orderedWithUnknown = PrintTemplateOrdering.OrderAndFindDefault(
                withUnknown, PrintDialogOptions.ForBookingReportDialog(), out uDef);
            bool unknownLastOk = orderedWithUnknown.Count == 6 && orderedWithUnknown[5].Name == "Mẫu lạ 100 mm";

            Check("In P2 Đặt hàng (đọc STEMPLATE theo STABLEDESCID & sắp thứ tự): đủ 5 mẫu theo thứ tự 54mm/54mm(2 dòng)/80mm/A4/A5, mặc định A4, tên lạ xếp cuối",
                count5Ok && order5Ok && unknownLastOk,
                "cnt=" + ordered.Count + ", def=" + defaultIdx + ", names=" + string.Join("|", ordered.ConvertAll(t => t.Name).ToArray()));

            P2PrintFixture fx = null;
            try
            {
                fx = CreateP2PrintFixture(db);
                Dictionary<string, string> tplSnapBefore = SnapshotStemplate(db);

                // Tạo phiếu đặt hàng tính tay: Cà phê sữa 4 x 25.000 = 100.000; Trà đào 3 x 30.000 = 90.000;
                // TIENHANG=190.000, giảm 5% = 9.500, thuế 8% = 14.440, phí VC = 10.000 -> TONGCONG = 204.940; Đặt trước = 50.000; Còn lại = 154.940.
                DateTime arrivalTime = new DateTime(2026, 11, 26, 18, 0, 0);
                var row = new BookingRow
                {
                    Name = "DH26/P2001",
                    Ngay = new DateTime(2026, 11, 26),
                    GioDat = new DateTime(2026, 11, 26, 10, 30, 0),
                    TuGio = arrivalTime,
                    DenGio = new DateTime(2026, 11, 26, 21, 0, 0),
                    TuNgay = new DateTime(2026, 11, 26),
                    DenNgay = new DateTime(2026, 11, 26),
                    TenKhach = "Trần Minh Khách",
                    DiaChi = "88 Nguyễn Huệ",
                    DienThoai = "0901234567",
                    Note = "Ít đá, chuẩn bị sớm",
                    TienHang = 190000m,
                    TileGiamGia = 5m,
                    TienGiamGia = 9500m,
                    TileThue = 8m,
                    TienThue = 14440m,
                    PhiVanChuyen = 10000m,
                    TongCong = 204940m,
                    Status = 30
                };
                var lines = new List<BookingLine>
                {
                    new BookingLine { ProductId = fx.CoffeeId, UnitId = fx.UnitId, Quantity = 4m, Price = 25000m, DiscountPct = 0m, Amount = 100000m, Note = "" },
                    new BookingLine { ProductId = fx.PeachTeaId, UnitId = fx.UnitId, Quantity = 3m, Price = 30000m, DiscountPct = 0m, Amount = 90000m, Note = "" }
                };
                BookingSaveResult saved = bookingRepo.SaveBooking(row, lines, new List<string> { fx.Room1Id }, adminSession.UserId);
                if (saved.Ok && saved.GroupIds != null)
                    fx.CreatedBookingIds.AddRange(saved.GroupIds);

                string receiptId = phieuThuRepo.SaveDeposit(new BookingDeposit
                {
                    BookingId = saved.SavedId,
                    Number = "PT26/P2001",
                    Date = new DateTime(2026, 11, 26),
                    ObjectType = 2,
                    CustomerName = "Trần Minh Khách",
                    Address = "88 Nguyễn Huệ",
                    StoreId = fx.StoreId,
                    ReasonId = fx.ReasonId,
                    Note = "Đặt cọc P2",
                    Amount = 50000m
                }, adminSession.UserId);
                if (!string.IsNullOrEmpty(receiptId))
                    fx.CreatedReceiptIds.Add(receiptId);

                string bookingSnapBefore = SnapshotTableCountsAndTotals(db, "TDATHANG", "TDATHANGCHITIET");
                CompanyPrintInfo company = new CompanyPrintInfo
                {
                    CompanyName = "CÔNG TY KAZUKO TEST",
                    CompanyAddress = "12 Lê Lợi",
                    CompanyPhone = "02811112222",
                    CompanyEmail = "info@kazuko.test",
                    CompanyFax = "02833334444"
                };

                BookingPrintDocument loaded = docRepo.LoadBookingForPrint(saved.SavedId);
                using (PrintJobData job = BookingPrintDataBuilder.Build(loaded, company, adminSession.DisplayName))
                {
                    bool jobMetaOk = job.DefaultFileName == "Phieu-dat-hang-DH26-P2001"
                        && job.PreviewTitle == PrintConstants.DefaultPreviewTitle
                        && job.DataSet.DataSetName == "Data"
                        && job.DataSet.Tables.Count == 1
                        && job.DataSet.Tables["Table0"].Rows.Count == 2;

                    // 3. Dựng đủ 5 mẫu đặt hàng: TryPrepare OK, 1 trang, đủ 2 dòng Cà phê sữa & Trà đào.
                    bool all5Ok = jobMetaOk && ordered.Count == 5;
                    string all5Detail = "";
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        Report rpt;
                        string err;
                        bool ok = FastReportEngine.TryPrepare(ordered[i], job, out rpt, out err);
                        if (!ok)
                        {
                            all5Ok = false;
                            all5Detail += ordered[i].Name + ":" + SingleLineDetail(err) + "; ";
                            continue;
                        }
                        using (rpt)
                        {
                            int pages = rpt.PreparedPages.Count;
                            List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(rpt);
                            bool hasTwo = PageTextsContain(texts, "Cà phê sữa") && PageTextsContain(texts, "Trà đào");
                            if (pages != 1 || !hasTwo)
                            {
                                all5Ok = false;
                                all5Detail += ordered[i].Name + "(pages=" + pages + ",items=" + hasTwo + "); ";
                            }
                        }
                    }
                    Check("In P2 Đặt hàng (T1 dựng cả 5 mẫu): TryPrepare OK, 1 trang, đủ 2 dòng Cà phê sữa & Trà đào",
                        all5Ok, SingleLineDetail(all5Detail));

                    // 4. Kiểm ĐÚNG Ô trên Mẫu đặt hàng A4: TIỀN HÀNG, giảm, thuế, phí VC, TỔNG TIỀN, ĐẶT TRƯỚC, CÒN LẠI, Bằng chữ, thông tin khách/phòng/giờ/nhân viên/công ty.
                    PrintTemplateRecord tplA4 = FindTemplateByName(ordered, PrintConstants.DefaultBookingTemplateName);
                    Report rptA4 = null;
                    string errA4;
                    bool prepA4 = tplA4 != null && FastReportEngine.TryPrepare(tplA4, job, out rptA4, out errA4);
                    bool a4CellsOk = false;
                    string a4Detail = "";
                    if (prepA4)
                    {
                        using (rptA4)
                        {
                            List<TemplateObjInfo> map = ParseTemplateObjectMap(tplA4.TemplateXml);
                            List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(rptA4);
                            string vTh, vGg, vTax, vShip, vTot, vDep, vCust, vAddr, vPhone, vRoom, vTime, vUser, vComp;
                            bool okTh = CheckPreparedMoneyCell(map, texts, "Summary", "TIENHANG", null, 190000m, out vTh);
                            bool okGg = CheckPreparedMoneyCell(map, texts, "Summary", "TIENGIAMGIA", null, 9500m, out vGg);
                            bool okTax = CheckPreparedMoneyCell(map, texts, "Summary", "TIENTHUE", null, 14440m, out vTax);
                            bool okShip = CheckPreparedMoneyCell(map, texts, "Summary", "PHIVANCHUYEN", null, 10000m, out vShip);
                            bool okTot = CheckPreparedMoneyCell(map, texts, "Summary", "TONGCONG", "DatTruoc", 204940m, out vTot);
                            bool okDep = CheckPreparedMoneyCell(map, texts, "Summary", "DatTruoc", "TONGCONG", 50000m, out vDep);

                            string remObj = FindObjNameBoth(map, "Summary", "TONGCONG", "DatTruoc");
                            // Tìm ô CÒN LẠI (không phải ô ToVndWords).
                            string remOnlyObj = "";
                            for (int i = 0; i < map.Count; i++)
                            {
                                if (map[i].BandKind == "Summary"
                                    && ContainsFieldToken(map[i].RawText, "TONGCONG")
                                    && ContainsFieldToken(map[i].RawText, "DatTruoc")
                                    && !ContainsFieldToken(map[i].RawText, "ToVndWords"))
                                {
                                    remOnlyObj = map[i].Name;
                                    break;
                                }
                            }
                            string vRem = FindLastObjectText(texts, remOnlyObj.Length > 0 ? remOnlyObj : remObj);
                            decimal remVal;
                            bool okRem = TryParseCellNumber(vRem, out remVal) && remVal == 154940m;

                            string wordsObj = FindObjName(map, "Summary", "ToVndWords", null);
                            string vWords = FindLastObjectText(texts, wordsObj);
                            string expectRemWords = No1Lib.Sys.DocSo.ToVndWords(154940m) ?? "";
                            bool okWords = wordsObj.Length > 0
                                && vWords.IndexOf(expectRemWords, StringComparison.OrdinalIgnoreCase) >= 0;

                            bool okCust = CheckPreparedTextCell(map, texts, "Header", "TENKHACH", "Trần Minh Khách", out vCust);
                            bool okAddr = CheckPreparedTextCell(map, texts, "Header", "DIACHI", "88 Nguyễn Huệ", out vAddr);
                            bool okPhone = CheckPreparedTextCell(map, texts, "Header", "DIENTHOAI", "0901234567", out vPhone);
                            bool okRoom = CheckPreparedTextCell(map, texts, "Header", "DBAN_NAME", "Phòng VIP 1", out vRoom);
                            bool okTime = CheckPreparedTextCell(map, texts, "Header", "TUGIO", "26/11/26 18:00", out vTime);
                            bool okUser = CheckPreparedTextCell(map, texts, "Header", "In bởi", "Nhân viên: Nguyễn Văn In", out vUser);
                            bool okComp = CheckPreparedTextCell(map, texts, "Header", "CompanyName", "CÔNG TY KAZUKO TEST", out vComp);

                            a4CellsOk = okTh && okGg && okTax && okShip && okTot && okDep && okRem && okWords
                                && okCust && okAddr && okPhone && okRoom && okTime && okUser && okComp;
                            a4Detail = "th=" + vTh + ",gg=" + vGg + ",tax=" + vTax + ",ship=" + vShip
                                + ",tot=" + vTot + ",dep=" + vDep + ",rem=" + vRem + ",words=" + okWords
                                + ",room=" + vRoom + ",time=" + vTime + ",user=" + vUser;
                        }
                    }
                    Check("In P2 Đặt hàng (đúng ô trên Mẫu đặt hàng A4): tiền hàng/giảm/thuế/VC/tổng/cọc/còn lại/Bằng chữ & khách/phòng/giờ/NV/công ty",
                        a4CellsOk, SingleLineDetail(a4Detail));

                    // 5. Kiểm Bằng chữ trên Mẫu đặt hàng 80 mm (dùng [ToVndWords([TONGCONG])] = 204.940) và tên phòng từ Table0.DBAN_NAME.
                    PrintTemplateRecord tpl80 = FindTemplateByName(ordered, "Mẫu đặt hàng 80 mm");
                    Report rpt80 = null;
                    string err80;
                    bool ok80 = false;
                    string d80 = "";
                    if (tpl80 != null && FastReportEngine.TryPrepare(tpl80, job, out rpt80, out err80))
                    {
                        using (rpt80)
                        {
                            List<TemplateObjInfo> map80 = ParseTemplateObjectMap(tpl80.TemplateXml);
                            List<KeyValuePair<string, string>> texts80 = FastReportEngine.ExtractPreparedPageTexts(rpt80);
                            string wordsObj80 = FindObjName(map80, "Summary", "ToVndWords", null);
                            string vWords80 = FindLastObjectText(texts80, wordsObj80);
                            string roomObj80 = FindObjName(map80, "Header", "DBAN_NAME", null);
                            string vRoom80 = FindLastObjectText(texts80, roomObj80);
                            ok80 = vWords80.IndexOf("Hai trăm linh bốn ngàn, chín trăm bốn mươi đồng chẵn", StringComparison.OrdinalIgnoreCase) >= 0
                                && vRoom80.IndexOf("Phòng VIP 1", StringComparison.Ordinal) >= 0;
                            d80 = "words80=" + vWords80 + ", room80=" + vRoom80;
                        }
                    }
                    Check("In P2 Đặt hàng (đúng ô trên Mẫu 80 mm): Bằng chữ 204.940 khớp & Table0.DBAN_NAME hiện đúng phòng",
                        ok80, SingleLineDetail(d80));

                    // 6. Mẫu hỏng XML / thiếu nội dung báo lỗi nêu tên mẫu, không văng.
                    var brokenTpl = new PrintTemplateRecord { Id = "bad", Name = "Mẫu ĐH hỏng XML", TemplateXml = "<Report><Broken" };
                    var emptyTpl = new PrintTemplateRecord { Id = "emp", Name = "Mẫu ĐH rỗng", TemplateXml = "" };
                    Report dummyRpt;
                    string badErr, emptyErr;
                    bool badHandled = !FastReportEngine.TryPrepare(brokenTpl, job, out dummyRpt, out badErr)
                        && !string.IsNullOrEmpty(badErr) && badErr.IndexOf("Mẫu ĐH hỏng XML", StringComparison.Ordinal) >= 0;
                    bool emptyHandled = !FastReportEngine.TryPrepare(emptyTpl, job, out dummyRpt, out emptyErr)
                        && !string.IsNullOrEmpty(emptyErr) && emptyErr.IndexOf("Mẫu ĐH rỗng", StringComparison.Ordinal) >= 0;
                    Check("In P2 Đặt hàng (T6 mẫu thiếu/hỏng): báo lỗi một câu nêu tên mẫu, không văng",
                        badHandled && emptyHandled, SingleLineDetail(badErr + " | " + emptyErr));
                }

                // 7. Phiếu đặt nhiều phòng (đoàn 2 phòng chung GUID): DBAN_NAME nối bằng ", ".
                var multiRow = new BookingRow
                {
                    Name = "DH26/P2002",
                    Ngay = new DateTime(2026, 11, 26),
                    GioDat = new DateTime(2026, 11, 26, 11, 0, 0),
                    TuGio = arrivalTime,
                    DenGio = new DateTime(2026, 11, 26, 21, 0, 0),
                    TuNgay = new DateTime(2026, 11, 26),
                    DenNgay = new DateTime(2026, 11, 26),
                    TenKhach = "Đoàn 2 phòng",
                    TienHang = 100000m,
                    TongCong = 100000m,
                    Status = 30
                };
                BookingSaveResult multiSaved = bookingRepo.SaveBooking(
                    multiRow,
                    new List<BookingLine> { new BookingLine { ProductId = fx.CoffeeId, UnitId = fx.UnitId, Quantity = 4m, Price = 25000m, Amount = 100000m } },
                    new List<string> { fx.Room1Id, fx.Room2Id },
                    adminSession.UserId);
                if (multiSaved.Ok && multiSaved.GroupIds != null)
                    fx.CreatedBookingIds.AddRange(multiSaved.GroupIds);
                BookingPrintDocument multiDoc = docRepo.LoadBookingForPrint(multiSaved.SavedId);
                Check("In P2 Đặt hàng (đoàn nhiều phòng): DBAN_NAME nối các phòng cùng GUID bằng dấu phẩy",
                    multiDoc != null && multiDoc.RoomNames == "Phòng VIP 1, Phòng VIP 2",
                    multiDoc == null ? "null" : multiDoc.RoomNames);

                // 8. Kiểm STEMPLATE và bảng phiếu không đổi sau khi in.
                string bookingSnapAfter = SnapshotTableCountsAndTotals(db, "TDATHANG", "TDATHANGCHITIET");
                // Lưu ý: bước 7 vừa thêm đoàn 2 phòng -> chỉ kiểm bất biến của STEMPLATE + kiểm bất biến của bảng phiếu quanh bước dựng in.
                Dictionary<string, string> tplSnapAfter = SnapshotStemplate(db);
                bool stplUnchanged = SnapshotsEqual(tplSnapBefore, tplSnapAfter);
                string snapBeforePrintOnly = SnapshotTableCountsAndTotals(db, "TDATHANG", "TDATHANGCHITIET");
                using (PrintJobData job2 = BookingPrintDataBuilder.Build(multiDoc, new CompanyPrintInfo(), adminSession.DisplayName))
                {
                    Report rpt2;
                    string err2;
                    if (FastReportEngine.TryPrepare(ordered[3], job2, out rpt2, out err2))
                        rpt2.Dispose();
                }
                string snapAfterPrintOnly = SnapshotTableCountsAndTotals(db, "TDATHANG", "TDATHANGCHITIET");
                Check("In P2 Đặt hàng (T5 bất biến CSDL): STEMPLATE và TDATHANG/TDATHANGCHITIET không đổi sau khi in",
                    stplUnchanged && snapBeforePrintOnly == snapAfterPrintOnly && bookingSnapBefore != bookingSnapAfter,
                    "stpl=" + stplUnchanged);

                // 9. Chạy nhóm test UI hộp IN BÁO CÁO, BookingEditDialog và BookingListView.
                PrintP2BookingUiTests(db, adminSession, fx, ordered, saved.SavedId);
            }
            finally
            {
                CleanupP2PrintFixture(db, fx);
                AppSession.Current = savedSession;
            }
        }
    }
}
