using System;
using System.Collections.Generic;
using System.IO;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP0Tests(string db)
        {
            Dictionary<string, string> snapBefore = SnapshotStemplate(db);

            var repo = new PrintTemplateRepository(db);
            List<PrintTemplateRecord> templates = repo.LoadTemplatesForForm(PrintConstants.ItemSalesFormId);
            Check("In P0 (STEMPLATE): đọc đủ 3 mẫu 54mm, 80mm, A4 đúng thứ tự tên và có nội dung XML",
                templates.Count == 3
                && templates[0].Name == "54mm" && templates[0].TemplateXml.Length > 100
                && templates[1].Name == "80mm" && templates[1].TemplateXml.Length > 100
                && templates[2].Name == "A4" && templates[2].TemplateXml.Length > 100,
                "count=" + templates.Count + ", names=" + string.Join(",", templates.ConvertAll(t => t.Name).ToArray()));

            List<PrintTemplateRecord> noTemplates = repo.LoadTemplatesForForm("00000000-0000-0000-0000-000000000000");
            Check("In P0 (T6 không có mẫu nào): form không có mẫu trả danh sách rỗng và có câu thông báo rõ ràng",
                noTemplates.Count == 0 && !string.IsNullOrWhiteSpace(PrintConstants.NoTemplateFoundMessage));

            CompanyPrintInfo company = repo.LoadCompanyInfo();
            Check("In P0 (SCONFIG): đọc thông tin công ty không ném lỗi và không null",
                company != null && company.CompanyName != null && company.CompanyAddress != null
                && company.CompanyPhone != null && company.CompanyEmail != null);

            if (templates.Count == 3)
            {
                RunPrintT1T2T7Tests(templates, company);
                RunPrintT6ErrorTests(templates[2]);
                PrintP0UiAndPermissionTests(db, templates);
            }

            Dictionary<string, string> snapAfter = SnapshotStemplate(db);
            Check("In P0 (T5): bảng STEMPLATE (số dòng và băm từng blob) không đổi sau toàn bộ ca kiểm thử",
                SnapshotsEqual(snapBefore, snapAfter),
                "before=" + snapBefore.Count + ", after=" + snapAfter.Count);
        }

        private static void RunPrintT1T2T7Tests(List<PrintTemplateRecord> templates, CompanyPrintInfo company)
        {
            // Dữ liệu có chiết khấu dòng sao cho SUM(THANHTIEN) = 178,000 KHÁC TongTienBan = 190,000,
            // SUM(LAI) = 109,000 KHÁC TongLai = 121,000, SUM(TIENGIAMGIA) = 12,000 KHÁC TongGiamGia = 17,000,
            // SUM(LAI)-SUM(TIENGIAMGIA) = 97,000 KHÁC ThucLai = 115,000.
            var rows = new List<ItemSalesRow>
            {
                new ItemSalesRow
                {
                    ProductId = "P1", GroupId = "G1", ProductCode = "SP01", Barcode = "BC01",
                    ItemName = "Trà sữa Ô Long nướng", Quantity = 3m, UnitName = "Ly",
                    UnitPrice = 40000m, DiscountRate = 10m, LineDiscountAmount = 12000m,
                    SalesAmount = 108000m, UnitCost = 15000m, CostAmount = 45000m,
                    Profit = 63000m, ProfitRate = 100m * 63000m / 108000m
                },
                new ItemSalesRow
                {
                    ProductId = "P2", GroupId = "G2", ProductCode = "SP02", Barcode = "BC02",
                    ItemName = "Bánh flan phô mai", Quantity = 2m, UnitName = "Phần",
                    UnitPrice = 35000m, DiscountRate = 0m, LineDiscountAmount = 0m,
                    SalesAmount = 70000m, UnitCost = 12000m, CostAmount = 24000m,
                    Profit = 46000m, ProfitRate = 100m * 46000m / 70000m
                }
            };
            var groupNames = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "G1", "Đồ uống đặc biệt" },
                { "G2", "Bánh ngọt" }
            };
            var summary = new ItemSalesSummary
            {
                TotalSalesBeforeDiscount = 190000m,
                TotalCost = 69000m,
                GrossSalesProfit = 121000m,
                TotalDiscount = 17000m,
                Tax = 8000m,
                ServiceFee = 3000m,
                TimeCharge = 0m,
                NetProfit = 115000m
            };
            var companyParam = new CompanyPrintInfo
            {
                CompanyName = string.IsNullOrEmpty(company.CompanyName) ? "QUÁN TRÀ KAZUKO" : company.CompanyName,
                CompanyAddress = company.CompanyAddress,
                CompanyPhone = company.CompanyPhone,
                CompanyEmail = company.CompanyEmail
            };
            DateTime from = new DateTime(2026, 11, 1);
            DateTime to = new DateTime(2026, 11, 26);

            string tempDir = Path.Combine(Path.GetTempPath(), "kazuko-print-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                using (PrintJobData sampleJob = ItemSalesPrintDataBuilder.Build(rows, groupNames, summary, from, to, companyParam, true))
                using (PrintJobData emptyJob = ItemSalesPrintDataBuilder.Build(new List<ItemSalesRow>(), groupNames, new ItemSalesSummary(), from, to, companyParam, true))
                {
                    bool kept54TotalOk = false;
                    bool kept80TotalOk = false;

                    foreach (PrintTemplateRecord tpl in templates)
                    {
                        Report rpt;
                        string err;
                        bool okNonEmpty = FastReportEngine.TryPrepare(tpl, sampleJob, out rpt, out err);
                        int pagesNonEmpty = okNonEmpty && rpt != null && rpt.PreparedPages != null ? rpt.PreparedPages.Count : 0;

                        Report rptEmpty;
                        string errEmpty;
                        bool okEmpty = FastReportEngine.TryPrepare(tpl, emptyJob, out rptEmpty, out errEmpty);
                        int pagesEmpty = okEmpty && rptEmpty != null && rptEmpty.PreparedPages != null ? rptEmpty.PreparedPages.Count : 0;
                        if (rptEmpty != null)
                            rptEmpty.Dispose();

                        Check("In P0 (T1 mẫu " + tpl.Name + "): dựng được với dữ liệu mẫu và với 0 dòng, số trang > 0",
                            okNonEmpty && pagesNonEmpty > 0 && okEmpty && pagesEmpty > 0,
                            "nonEmpty=" + pagesNonEmpty + " (" + err + "), empty=" + pagesEmpty + " (" + errEmpty + ")");

                        if (!okNonEmpty || rpt == null)
                            continue;

                        using (rpt)
                        {
                            List<KeyValuePair<string, string>> pageTexts = FastReportEngine.ExtractPreparedPageTexts(rpt);
                            bool hasViOnPage = PageTextsContain(pageTexts, "Trà sữa Ô Long nướng")
                                && PageTextsContain(pageTexts, "Bánh flan phô mai");
                            Check("In P0 (T7 mẫu " + tpl.Name + "): tiếng Việt có dấu hiện đúng trên trang đã dựng",
                                hasViOnPage, "objects=" + pageTexts.Count);

                            string xlsxPath = Path.Combine(tempDir, tpl.Name + ".xlsx");
                            string pdfPath = Path.Combine(tempDir, tpl.Name + ".pdf");
                            FastReportEngine.ExportExcel(rpt, xlsxPath);
                            FastReportEngine.ExportPdf(rpt, pdfPath);

                            bool pdfOk = IsValidPdfFile(pdfPath);
                            Check("In P0 (T7 PDF mẫu " + tpl.Name + "): xuất file .pdf hợp lệ, khác rỗng",
                                pdfOk, pdfPath);

                            if (tpl.Name == "54mm")
                            {
                                decimal t54Qty = 0m;
                                kept54TotalOk = rpt.Dictionary != null && rpt.Dictionary.Totals != null
                                    && rpt.Dictionary.Totals.FindByName("Total") != null
                                    && TryParseReportNumber(FindObjectText(pageTexts, "Cell60"), out t54Qty)
                                    && t54Qty == 5m;
                                Verify54mmExcel(xlsxPath);
                            }
                            else if (tpl.Name == "80mm")
                            {
                                decimal t80Qty = 0m, t80Amt = 0m;
                                kept80TotalOk = rpt.Dictionary != null && rpt.Dictionary.Totals != null
                                    && rpt.Dictionary.Totals.FindByName("Total") != null
                                    && rpt.Dictionary.Totals.FindByName("TotalTien") != null
                                    && TryParseReportNumber(FindObjectText(pageTexts, "Cell60"), out t80Qty) && t80Qty == 5m
                                    && TryParseReportNumber(FindObjectText(pageTexts, "Cell63"), out t80Amt) && t80Amt == 178000m;
                                Verify80mmExcel(xlsxPath);
                            }
                            else if (tpl.Name == "A4")
                            {
                                VerifyA4TotalsAndExcel(rpt, pageTexts, xlsxPath, kept54TotalOk, kept80TotalOk);
                            }
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                }
            }
        }

        private static void VerifyA4TotalsAndExcel(
            Report a4Report,
            List<KeyValuePair<string, string>> pageTexts,
            string xlsxPath,
            bool kept54TotalOk,
            bool kept80TotalOk)
        {
            string c63 = FindObjectText(pageTexts, "Cell63");
            string c65 = FindObjectText(pageTexts, "Cell65");
            string c66 = FindObjectText(pageTexts, "Cell66");
            string t13 = FindObjectText(pageTexts, "Text13");
            string t14 = FindObjectText(pageTexts, "Text14");

            decimal v63 = 0m, v65 = 0m, v66 = 0m, v13 = 0m, v14 = 0m;
            bool parsed = TryParseReportNumber(c63, out v63)
                && TryParseReportNumber(c65, out v65)
                && TryParseReportNumber(c66, out v66)
                && TryParseReportNumber(t13, out v13)
                && TryParseReportNumber(t14, out v14);

            bool a4ConflictingRemoved = a4Report != null && a4Report.Dictionary != null && a4Report.Dictionary.Totals != null
                && a4Report.Dictionary.Totals.FindByName("TongTienBan") == null
                && a4Report.Dictionary.Totals.FindByName("TongTienNhap") == null
                && a4Report.Dictionary.Totals.FindByName("TongLai") == null
                && a4Report.Dictionary.Totals.FindByName("TongGiamGia") == null
                && a4Report.Dictionary.Totals.FindByName("ThucLai") == null;

            // Khẳng định: gỡ Total trùng tên tham số trên A4 nên 5 ô in đúng tham số ItemSalesSummary
            // (190000, 69000, 121000, -17000, 115000; khác tổng cột 178000, 69000, 109000, -12000, 97000),
            // đồng thời 54mm và 80mm vẫn giữ nguyên Total riêng (Total = 5, TotalTien = 178000).
            bool matchesParams = parsed
                && a4ConflictingRemoved
                && v63 == 190000m && v65 == 69000m && v66 == 121000m
                && v13 == -17000m && v14 == 115000m
                && kept54TotalOk && kept80TotalOk;

            Check("In P0 (A4 Total trùng tên tham số): 5 ô tổng A4 in đúng tham số ItemSalesSummary và 54mm/80mm vẫn giữ Total riêng",
                matchesParams,
                "Cell63=" + c63 + ", Cell65=" + c65 + ", Cell66=" + c66 + ", Text13=" + t13 + ", Text14=" + t14
                + ", removed=" + a4ConflictingRemoved + ", 54mm=" + kept54TotalOk + ", 80mm=" + kept80TotalOk);

            List<List<ExcelCellVal>> rows = ReadExcelAllRows(xlsxPath);
            List<ExcelCellVal> rowTra = FindRowContaining(rows, "Trà sữa Ô Long nướng");
            List<ExcelCellVal> rowBanh = FindRowContaining(rows, "Bánh flan phô mai");
            List<ExcelCellVal> rowTotal = FindRowContaining(rows, "TỔNG CỘNG");

            bool traOk = rowTra != null
                && RowHasText(rowTra, "Ly")
                && RowHasNumber(rowTra, 3m) && RowHasNumber(rowTra, 40000m)
                && RowHasNumber(rowTra, 108000m) && RowHasNumber(rowTra, 15000m)
                && RowHasNumber(rowTra, 45000m) && RowHasNumber(rowTra, 63000m)
                && RowHasNumberApprox(rowTra, 58.3m, 0.15m);

            bool banhOk = rowBanh != null
                && RowHasText(rowBanh, "Phần")
                && RowHasNumber(rowBanh, 2m) && RowHasNumber(rowBanh, 35000m)
                && RowHasNumber(rowBanh, 70000m) && RowHasNumber(rowBanh, 12000m)
                && RowHasNumber(rowBanh, 24000m) && RowHasNumber(rowBanh, 46000m)
                && RowHasNumberApprox(rowBanh, 65.7m, 0.15m);

            bool totalOk = rowTotal != null
                && RowHasNumber(rowTotal, 190000m)
                && RowHasNumber(rowTotal, 69000m)
                && RowHasNumber(rowTotal, 121000m)
                && SheetHasNumber(rows, -17000m)
                && SheetHasNumber(rows, 115000m);

            Check("In P0 (T2/T7 mẫu A4 xuất Excel): đọc lại .xlsx khớp tên hàng tiếng Việt, số từng dòng và các ô tổng",
                traOk && banhOk && totalOk,
                "tra=" + traOk + ", banh=" + banhOk + ", total=" + totalOk);
        }

        private static void Verify80mmExcel(string xlsxPath)
        {
            List<List<ExcelCellVal>> rows = ReadExcelAllRows(xlsxPath);
            bool groupsOk = FindRowContaining(rows, "Đồ uống đặc biệt") != null
                && FindRowContaining(rows, "Bánh ngọt") != null;
            List<ExcelCellVal> rowTra = FindRowContaining(rows, "Trà sữa Ô Long nướng");
            List<ExcelCellVal> rowBanh = FindRowContaining(rows, "Bánh flan phô mai");
            List<ExcelCellVal> rowTotal = FindRowContaining(rows, "TỔNG CỘNG");

            bool ok = groupsOk
                && rowTra != null && RowHasNumber(rowTra, 3m) && RowHasNumber(rowTra, 108000m)
                && rowBanh != null && RowHasNumber(rowBanh, 2m) && RowHasNumber(rowBanh, 70000m)
                && rowTotal != null && RowHasNumber(rowTotal, 5m) && RowHasNumber(rowTotal, 178000m);

            Check("In P0 (T2/T7 mẫu 80mm xuất Excel): đọc lại .xlsx khớp nhóm hàng, tên tiếng Việt, SL, Thành tiền và dòng tổng",
                ok, "groups=" + groupsOk + ", totalRow=" + FormatExcelRow(rowTotal));
        }

        private static void Verify54mmExcel(string xlsxPath)
        {
            List<List<ExcelCellVal>> rows = ReadExcelAllRows(xlsxPath);
            bool groupsOk = FindRowContaining(rows, "Đồ uống đặc biệt") != null
                && FindRowContaining(rows, "Bánh ngọt") != null;
            List<ExcelCellVal> rowTra = FindRowContaining(rows, "Trà sữa Ô Long nướng");
            List<ExcelCellVal> rowBanh = FindRowContaining(rows, "Bánh flan phô mai");
            List<ExcelCellVal> rowTotal = FindRowContaining(rows, "TỔNG CỘNG");

            bool ok = groupsOk
                && rowTra != null && RowHasNumber(rowTra, 3m)
                && rowBanh != null && RowHasNumber(rowBanh, 2m)
                && rowTotal != null && RowHasNumber(rowTotal, 5m);

            Check("In P0 (T2/T7 mẫu 54mm xuất Excel): đọc lại .xlsx khớp nhóm hàng, tên tiếng Việt, SL và tổng SL",
                ok, "groups=" + groupsOk + ", totalRow=" + FormatExcelRow(rowTotal));
        }

        private static void RunPrintT6ErrorTests(PrintTemplateRecord validTemplate)
        {
            Report r1;
            string errMissing;
            bool okMissing = FastReportEngine.TryPrepare(
                new PrintTemplateRecord { Id = "m1", Name = "Mẫu rỗng" }, null, out r1, out errMissing);
            Check("In P0 (T6 mẫu thiếu/rỗng): trả câu báo tiếng Việt nêu tên mẫu, không văng app",
                !okMissing && r1 == null && !string.IsNullOrEmpty(errMissing) && errMissing.IndexOf("Mẫu rỗng", StringComparison.Ordinal) >= 0,
                errMissing);

            Report r2;
            string errXml;
            bool okXml = FastReportEngine.TryPrepare(
                new PrintTemplateRecord { Id = "m2", Name = "Mẫu XML lỗi", TemplateXml = "<Report><KhongDong" }, null, out r2, out errXml);
            Check("In P0 (T6 XML hỏng): trả câu báo tiếng Việt nêu tên mẫu, không văng app",
                !okXml && r2 == null && !string.IsNullOrEmpty(errXml) && errXml.IndexOf("Mẫu XML lỗi", StringComparison.Ordinal) >= 0,
                errXml);

            string badScriptXml =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Report ReportInfo.CreatorVersion=\"1.2.47.0\">" +
                "<ScriptText>using System; namespace FastReport { public class ReportScript { void Bad( { } } }</ScriptText>" +
                "<Dictionary/><ReportPage Name=\"Page1\"/></Report>";
            Report r3;
            string errScript;
            bool okScript = FastReportEngine.TryPrepare(
                new PrintTemplateRecord { Id = "m3", Name = "Mẫu script lỗi", TemplateXml = badScriptXml }, null, out r3, out errScript);
            Check("In P0 (T6 script lỗi): trả câu báo tiếng Việt nêu tên mẫu, không văng app",
                !okScript && r3 == null && !string.IsNullOrEmpty(errScript) && errScript.IndexOf("Mẫu script lỗi", StringComparison.Ordinal) >= 0,
                errScript);

            var recorder = new PrintOutputRecorder
            {
                NextSavePath = Path.Combine(Path.GetTempPath(), "dummy.xlsx"),
                ThrowOnExport = true
            };
            using (PrintJobData emptyJob = ItemSalesPrintDataBuilder.Build(
                new List<ItemSalesRow>(), null, new ItemSalesSummary(), DateTime.Today, DateTime.Today, new CompanyPrintInfo(), true))
            {
                bool exported = PrintWorkflowService.Execute(
                    null,
                    new PrintSelectionResult { Template = validTemplate, Action = PrintActionKind.ExportExcel },
                    emptyJob,
                    recorder);
                Check("In P0 (T6 lỗi xuất file): bắt lỗi, báo câu tiếng Việt nêu tên mẫu, không văng app",
                    !exported && recorder.ErrorMessages.Count == 1
                    && recorder.ErrorMessages[0].IndexOf(validTemplate.Name, StringComparison.Ordinal) >= 0,
                    recorder.ErrorMessages.Count > 0 ? recorder.ErrorMessages[0] : "");
            }
        }
    }
}
