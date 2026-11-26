using System;
using System.Collections.Generic;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP2QuoteTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            var adminSession = new UserSession
            {
                UserId = "selftest_p2_bg",
                UserName = "nv_bg",
                DisplayName = "Lê Thị Báo Giá",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            AppSession.Current = adminSession;

            var tplRepo = new PrintTemplateRepository(db);
            var docRepo = new DocumentPrintRepository(db);
            var quoteRepo = new QuoteRepository(db);

            // 1. Đọc mẫu theo STABLEDESCID TBAOGIA: đúng 2 mẫu STATUS=30 có TEMPLATE (bỏ qua 54mm/80mm STATUS=0).
            List<PrintTemplateRecord> rawTemplates = tplRepo.LoadTemplatesForTable(PrintConstants.QuoteTableDescId);
            int defIdx;
            List<PrintTemplateRecord> ordered = PrintTemplateOrdering.OrderAndFindDefault(
                rawTemplates, PrintDialogOptions.ForQuoteReportDialog(), out defIdx);

            bool count2Ok = ordered.Count == 2
                && defIdx >= 0 && defIdx < ordered.Count
                && string.Equals(ordered[defIdx].Name, PrintConstants.DefaultQuoteTemplateName, StringComparison.OrdinalIgnoreCase);
            Check("In P2 Báo giá (đọc STEMPLATE theo STABLEDESCID): đúng 2 mẫu STATUS=30 có nội dung & chọn sẵn Báo Giá A4",
                count2Ok, "cnt=" + ordered.Count + ", names=" + string.Join("|", ordered.ConvertAll(t => t.Name).ToArray()));

            P2PrintFixture fx = null;
            try
            {
                fx = CreateP2PrintFixture(db);
                Dictionary<string, string> tplSnapBefore = SnapshotStemplate(db);

                // Phiếu báo giá tính tay: Cà phê sữa 4 x 25.000, Trà đào 3 x 30.000 -> TIENHANG 190.000,
                // giảm 5% = 9.500, thuế 8% = 14.440, phí VC = 10.000 -> TONGCONG = 204.940.
                var qRow = new QuoteRow
                {
                    Name = "BG26/P2001",
                    Ngay = new DateTime(2026, 11, 26),
                    TenKhach = "Công ty Khách Báo Giá",
                    DiaChi = "99 Pasteur",
                    DienThoai = "0908887776",
                    Note = "Báo giá ưu đãi",
                    TienHang = 190000m,
                    TileGiamGia = 5m,
                    TienGiamGia = 9500m,
                    TileThue = 8m,
                    TienThue = 14440m,
                    PhiVanChuyen = 10000m,
                    TongCong = 204940m,
                    Status = QuoteConstants.ActiveStatus
                };
                var qLines = new List<QuoteLine>
                {
                    new QuoteLine { ProductId = fx.CoffeeId, UnitId = fx.UnitId, Quantity = 4m, Price = 25000m, DiscountPct = 0m, Amount = 100000m, Note = "" },
                    new QuoteLine { ProductId = fx.PeachTeaId, UnitId = fx.UnitId, Quantity = 3m, Price = 30000m, DiscountPct = 0m, Amount = 90000m, Note = "" }
                };
                QuoteSaveResult saved = quoteRepo.SaveQuote(qRow, qLines, adminSession.UserId);
                if (saved.Ok && !string.IsNullOrEmpty(saved.SavedId))
                    fx.CreatedQuoteIds.Add(saved.SavedId);

                string quoteSnapBefore = SnapshotTableCountsAndTotals(db, "TBAOGIA", "TBAOGIACHITIET");
                CompanyPrintInfo company = new CompanyPrintInfo
                {
                    CompanyName = "CÔNG TY KAZUKO BG",
                    CompanyAddress = "12 Lê Lợi",
                    CompanyPhone = "02811112222",
                    CompanyEmail = "sales@kazuko.test",
                    CompanyFax = "02899998888"
                };

                QuotePrintDocument loaded = docRepo.LoadQuoteForPrint(saved.SavedId);
                using (PrintJobData job = QuotePrintDataBuilder.Build(loaded, company, adminSession.DisplayName))
                {
                    bool jobMetaOk = job.DefaultFileName == "Bao-gia-BG26-P2001"
                        && job.PreviewTitle == PrintConstants.DefaultPreviewTitle
                        && job.DataSet.DataSetName == "Data"
                        && job.DataSet.Tables.Count == 1
                        && job.DataSet.Tables["Table0"].Rows.Count == 2;

                    // 2. Dựng cả 2 mẫu báo giá: TryPrepare OK, 1 trang, đủ 2 dòng hàng.
                    bool bothOk = jobMetaOk && ordered.Count == 2;
                    string bothDetail = "";
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        Report rpt;
                        string err;
                        if (!FastReportEngine.TryPrepare(ordered[i], job, out rpt, out err))
                        {
                            bothOk = false;
                            bothDetail += ordered[i].Name + ":" + SingleLineDetail(err) + "; ";
                            continue;
                        }
                        using (rpt)
                        {
                            List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(rpt);
                            bool hasTwo = PageTextsContain(texts, "Cà phê sữa") && PageTextsContain(texts, "Trà đào");
                            if (rpt.PreparedPages.Count != 1 || !hasTwo)
                            {
                                bothOk = false;
                                bothDetail += ordered[i].Name + "(pages=" + rpt.PreparedPages.Count + ",items=" + hasTwo + "); ";
                            }
                        }
                    }
                    Check("In P2 Báo giá (T1 dựng cả 2 mẫu): TryPrepare OK, 1 trang, đủ 2 dòng Cà phê sữa & Trà đào",
                        bothOk, SingleLineDetail(bothDetail));

                    // 3. Kiểm ĐÚNG Ô trên Báo Giá A4: tiền hàng/giảm/thuế/VC/tổng/Bằng chữ/khách/NV/công ty.
                    PrintTemplateRecord tplA4 = FindTemplateByName(ordered, PrintConstants.DefaultQuoteTemplateName);
                    bool a4Ok = false;
                    string a4Detail = "";
                    Report rptA4 = null;
                    string errA4;
                    if (tplA4 != null && FastReportEngine.TryPrepare(tplA4, job, out rptA4, out errA4))
                    {
                        using (rptA4)
                        {
                            List<TemplateObjInfo> map = ParseTemplateObjectMap(tplA4.TemplateXml);
                            List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(rptA4);
                            string vTh, vGg, vTax, vShip, vTot, vCust, vAddr, vPhone, vUser, vComp;
                            bool okTh = CheckPreparedMoneyCell(map, texts, "Summary", "TIENHANG", null, 190000m, out vTh);
                            bool okGg = CheckPreparedMoneyCell(map, texts, "Summary", "TIENGIAMGIA", null, 9500m, out vGg);
                            bool okTax = CheckPreparedMoneyCell(map, texts, "Summary", "TIENTHUE", null, 14440m, out vTax);
                            bool okShip = CheckPreparedMoneyCell(map, texts, "Summary", "PHIVANCHUYEN", null, 10000m, out vShip);
                            bool okTot = CheckPreparedMoneyCell(map, texts, "Summary", "TONGCONG", "ToVndWords", 204940m, out vTot);

                            string wordsObj = FindObjName(map, "Summary", "ToVndWords", null);
                            string vWords = FindLastObjectText(texts, wordsObj);
                            bool okWords = wordsObj.Length > 0
                                && vWords.IndexOf("Hai trăm linh bốn ngàn, chín trăm bốn mươi đồng chẵn", StringComparison.OrdinalIgnoreCase) >= 0;

                            bool okCust = CheckPreparedTextCell(map, texts, "Header", "TENKHACH", "Công ty Khách Báo Giá", out vCust);
                            bool okAddr = CheckPreparedTextCell(map, texts, "Header", "DIACHI", "99 Pasteur", out vAddr);
                            bool okPhone = CheckPreparedTextCell(map, texts, "Header", "DIENTHOAI", "0908887776", out vPhone);
                            bool okUser = CheckPreparedTextCell(map, texts, "Header", "In bởi", "Nhân viên: Lê Thị Báo Giá", out vUser);
                            bool okComp = CheckPreparedTextCell(map, texts, "Header", "CompanyName", "CÔNG TY KAZUKO BG", out vComp);

                            a4Ok = okTh && okGg && okTax && okShip && okTot && okWords
                                && okCust && okAddr && okPhone && okUser && okComp;
                            a4Detail = "th=" + vTh + ",gg=" + vGg + ",tax=" + vTax + ",ship=" + vShip + ",tot=" + vTot + ",words=" + okWords + ",user=" + vUser;
                        }
                    }
                    Check("In P2 Báo giá (đúng ô trên Báo Giá A4): tiền hàng/giảm/thuế/VC/tổng/Bằng chữ & khách/NV/công ty",
                        a4Ok, SingleLineDetail(a4Detail));

                    // 4. Kiểm ĐÚNG Ô trên Báo giá A4 (mẫu 2), gồm CompanyFax & Bằng chữ 204.940.
                    PrintTemplateRecord tpl2 = null;
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        if (!string.Equals((ordered[i].Name ?? "").Trim(), PrintConstants.DefaultQuoteTemplateName, StringComparison.OrdinalIgnoreCase))
                        {
                            tpl2 = ordered[i];
                            break;
                        }
                    }
                    bool m2Ok = false;
                    string m2Detail = "";
                    Report rpt2 = null;
                    string err2;
                    if (tpl2 != null && FastReportEngine.TryPrepare(tpl2, job, out rpt2, out err2))
                    {
                        using (rpt2)
                        {
                            List<TemplateObjInfo> map2 = ParseTemplateObjectMap(tpl2.TemplateXml);
                            List<KeyValuePair<string, string>> texts2 = FastReportEngine.ExtractPreparedPageTexts(rpt2);
                            string vTot2, vFax2;
                            bool okTot2 = CheckPreparedMoneyCell(map2, texts2, "Summary", "TONGCONG", "ToVndWords", 204940m, out vTot2);
                            bool okFax2 = CheckPreparedTextCell(map2, texts2, "Header", "CompanyFax", "02899998888", out vFax2);
                            string wordsObj2 = FindObjName(map2, "Summary", "ToVndWords", null);
                            string vWords2 = FindLastObjectText(texts2, wordsObj2);
                            bool okWords2 = vWords2.IndexOf("Hai trăm linh bốn ngàn, chín trăm bốn mươi đồng chẵn", StringComparison.OrdinalIgnoreCase) >= 0;
                            m2Ok = okTot2 && okFax2 && okWords2;
                            m2Detail = "tot=" + vTot2 + ",fax=" + vFax2 + ",words=" + okWords2;
                        }
                    }
                    Check("In P2 Báo giá (đúng ô trên mẫu 2): tổng tiền 204.940, CompanyFax & Bằng chữ khớp",
                        m2Ok, SingleLineDetail(m2Detail));
                }

                // 5. STEMPLATE và TBAOGIA/TBAOGIACHITIET không đổi sau khi in.
                string quoteSnapAfter = SnapshotTableCountsAndTotals(db, "TBAOGIA", "TBAOGIACHITIET");
                Dictionary<string, string> tplSnapAfter = SnapshotStemplate(db);
                Check("In P2 Báo giá (T5 bất biến CSDL): STEMPLATE và TBAOGIA/TBAOGIACHITIET không đổi sau khi in",
                    SnapshotsEqual(tplSnapBefore, tplSnapAfter) && quoteSnapBefore == quoteSnapAfter);

                // 6. Chạy nhóm test UI hộp IN BÁO CÁO, QuoteEditDialog và QuoteListView.
                PrintP2QuoteUiTests(db, adminSession, fx, ordered, saved.SavedId);
            }
            finally
            {
                CleanupP2PrintFixture(db, fx);
                AppSession.Current = savedSession;
            }
        }
    }
}
