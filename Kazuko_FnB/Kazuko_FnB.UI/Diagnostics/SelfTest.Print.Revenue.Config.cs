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
        private static readonly string[] RevenueVisibilityConfigKeys =
        {
            "CoTienGio",
            "CoPhiDichVu",
            "CoThueSuat",
            "SuDungDiemTichLuyDeThanhToan"
        };

        private static Dictionary<string, ConfigValue> CaptureRevenuePrintConfigs(string db)
        {
            Dictionary<string, ConfigValue> all = new ConfigRepository(db).LoadValues();
            var saved = new Dictionary<string, ConfigValue>(StringComparer.Ordinal);
            for (int i = 0; i < RevenueVisibilityConfigKeys.Length; i++)
            {
                string key = RevenueVisibilityConfigKeys[i];
                ConfigValue val;
                if (all.TryGetValue(key, out val) && val != null)
                {
                    saved[key] = new ConfigValue
                    {
                        DataType = val.DataType,
                        IntValue = val.IntValue,
                        DecimalValue = val.DecimalValue,
                        TextValue = val.TextValue
                    };
                }
            }
            return saved;
        }

        private static void SetRevenuePrintConfigs(
            string db,
            bool coTienGio,
            bool coPhiDichVu,
            bool coThueSuat,
            bool suDungDiem)
        {
            var cfgRepo = new ConfigRepository(db);
            cfgRepo.Save(new[]
            {
                new ConfigChange { Name = "CoTienGio", DataType = 3, IntValue = coTienGio ? PrintConstants.ConfigEnabledValue : 0 },
                new ConfigChange { Name = "CoPhiDichVu", DataType = 3, IntValue = coPhiDichVu ? PrintConstants.ConfigEnabledValue : 0 },
                new ConfigChange { Name = "CoThueSuat", DataType = 3, IntValue = coThueSuat ? PrintConstants.ConfigEnabledValue : 0 },
                new ConfigChange { Name = "SuDungDiemTichLuyDeThanhToan", DataType = 3, IntValue = suDungDiem ? PrintConstants.ConfigEnabledValue : 0 }
            }, "selftest");
            SystemConfig.Reload(db);
        }

        private static void RestoreRevenuePrintConfigs(string db, Dictionary<string, ConfigValue> saved)
        {
            if (saved == null || saved.Count == 0)
                return;
            var changes = new List<ConfigChange>(saved.Count);
            foreach (KeyValuePair<string, ConfigValue> kv in saved)
            {
                changes.Add(new ConfigChange
                {
                    Name = kv.Key,
                    DataType = kv.Value.DataType,
                    IntValue = kv.Value.IntValue,
                    DecimalValue = kv.Value.DecimalValue,
                    TextValue = kv.Value.TextValue
                });
            }
            new ConfigRepository(db).Save(changes, "selftest");
            SystemConfig.Reload(db);
        }

        private static void TestRevenueVisibleByConfigCases(
            string db,
            List<PrintTemplateRecord> templates,
            PrintJobData job)
        {
            PrintTemplateRecord salesTpl = FindTemplateByName(templates, "Báo cáo tổng hợp bán hàng A4");
            PrintTemplateRecord payTpl = FindTemplateByName(templates, "Báo cáo tổng hợp thanh toán A4");
            if (salesTpl == null || payTpl == null)
                return;

            var repo = new PrintTemplateRepository(db);
            Dictionary<string, bool> oldJobFlags = job.ConfigVisibilityById;
            try
            {
                // 1. Bốn cấu hình đều tắt: thanh toán A4 không có "Trừ tích lũy" và không có 3,000;
                // bán hàng A4 không có "Tiền giờ", "Giảm giờ", "Phí dv", "Thuế".
                SetRevenuePrintConfigs(db, false, false, false, false);
                VerifyAllFourConfigsOff(repo, payTpl, salesTpl, job);

                // 2. Bật SuDungDiemTichLuyDeThanhToan: thanh toán A4 hiện cột Trừ tích lũy, dòng tổng đủ 9 số.
                SetRevenuePrintConfigs(db, false, false, false, true);
                VerifyLoyaltyConfigOn(repo, payTpl, job);

                // 3. Bật CoPhiDichVu một mình: bán hàng A4 có Phí dv, vẫn ẩn Tiền giờ, Giảm giờ, Thuế.
                SetRevenuePrintConfigs(db, false, true, false, false);
                VerifyServiceFeeConfigOn(repo, salesTpl, job);

                // 4. ID không có trong SCONFIG giữ cột hiện; đọc cấu hình lỗi / thiếu cấu hình thì báo lỗi, không in cột đáng ẩn.
                VerifyUnknownAndFailedConfigVisibility(repo, job);
            }
            finally
            {
                job.ConfigVisibilityById = oldJobFlags;
            }
        }

        private static void VerifyAllFourConfigsOff(
            PrintTemplateRepository repo,
            PrintTemplateRecord payTpl,
            PrintTemplateRecord salesTpl,
            PrintJobData job)
        {
            bool payOk = false;
            string payDetail = "";
            job.ConfigVisibilityById = repo.LoadConfigVisibilityForTemplate(payTpl);
            Report payRpt;
            string payErr;
            if (FastReportEngine.TryPrepare(payTpl, job, out payRpt, out payErr) && payRpt != null)
            {
                using (payRpt)
                {
                    List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(payRpt);
                    bool noHeader = !PageTextsContainIgnoreCase(texts, "Trừ tích lũy");
                    bool no3000 = !PageTextsContainNumber(texts, 3000m)
                        && !PageTextsContain(texts, "3,000") && !PageTextsContain(texts, "3.000");
                    payOk = noHeader && no3000;
                    payDetail = "noHeader=" + noHeader + ", no3000=" + no3000;
                }
            }
            else
            {
                payDetail = "err=" + payErr;
            }

            bool salesOk = false;
            string salesDetail = "";
            job.ConfigVisibilityById = repo.LoadConfigVisibilityForTemplate(salesTpl);
            Report salesRpt;
            string salesErr;
            if (FastReportEngine.TryPrepare(salesTpl, job, out salesRpt, out salesErr) && salesRpt != null)
            {
                using (salesRpt)
                {
                    List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(salesRpt);
                    bool noTime = !PageTextsContainIgnoreCase(texts, "Tiền giờ");
                    bool noTimeDisc = !PageTextsContainIgnoreCase(texts, "Giảm giờ");
                    bool noFee = !PageTextsContainIgnoreCase(texts, "Phí dv") && !PageTextsContainIgnoreCase(texts, "Phí dịch vụ");
                    bool noTax = !PageTextsContainIgnoreCase(texts, "Thuế");
                    salesOk = noTime && noTimeDisc && noFee && noTax;
                    salesDetail = "noTime=" + noTime + ", noTimeDisc=" + noTimeDisc + ", noFee=" + noFee + ", noTax=" + noTax;
                }
            }
            else
            {
                salesDetail = "err=" + salesErr;
            }

            Check("In P1 Doanh thu (VisibleByConfigID 4 cấu hình tắt): thanh toán A4 ẩn Trừ tích lũy & 3,000; bán hàng A4 ẩn Tiền giờ, Giảm giờ, Phí dv, Thuế",
                payOk && salesOk, "pay[" + payDetail + "], sales[" + salesDetail + "]");
        }

        private static void VerifyLoyaltyConfigOn(PrintTemplateRepository repo, PrintTemplateRecord payTpl, PrintJobData job)
        {
            job.ConfigVisibilityById = repo.LoadConfigVisibilityForTemplate(payTpl);
            Report rpt;
            string err;
            bool ok = false;
            string detail = "";
            if (FastReportEngine.TryPrepare(payTpl, job, out rpt, out err) && rpt != null)
            {
                using (rpt)
                {
                    List<TemplateObjInfo> objMap = ParseTemplateObjectMap(payTpl.TemplateXml);
                    List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(rpt);
                    List<List<KeyValuePair<string, string>>> rows = ExtractPreparedRows(rpt);

                    bool hasHeader = PageTextsContainIgnoreCase(texts, "Trừ tích lũy");
                    string firstSumObj = FindObjName(objMap, "Summary", "TONGCONG", null);
                    List<decimal> rowNums = ExtractRowNumbers(FindTotalVisualRow(rows, firstSumObj));
                    decimal[] expected9 = { 165000m, 50000m, 20000m, 40000m, 10000m, 10000m, 7000m, 3000m, 25000m };
                    bool orderOk = SequenceEquals(rowNums, expected9);
                    ok = hasHeader && orderOk;
                    detail = "hasHeader=" + hasHeader + ", rowOrder=" + FormatDecimals(rowNums);
                }
            }
            else
            {
                detail = "err=" + err;
            }

            Check("In P1 Doanh thu (VisibleByConfigID bật SuDungDiemTichLuyDeThanhToan): thanh toán A4 hiện Trừ tích lũy và dòng tổng 9 số",
                ok, detail);
        }

        private static void VerifyServiceFeeConfigOn(PrintTemplateRepository repo, PrintTemplateRecord salesTpl, PrintJobData job)
        {
            job.ConfigVisibilityById = repo.LoadConfigVisibilityForTemplate(salesTpl);
            Report rpt;
            string err;
            bool ok = false;
            string detail = "";
            if (FastReportEngine.TryPrepare(salesTpl, job, out rpt, out err) && rpt != null)
            {
                using (rpt)
                {
                    List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(rpt);
                    bool hasFee = PageTextsContainIgnoreCase(texts, "Phí dv") || PageTextsContainIgnoreCase(texts, "Phí dịch vụ");
                    bool noTime = !PageTextsContainIgnoreCase(texts, "Tiền giờ");
                    bool noTimeDisc = !PageTextsContainIgnoreCase(texts, "Giảm giờ");
                    bool noTax = !PageTextsContainIgnoreCase(texts, "Thuế");
                    ok = hasFee && noTime && noTimeDisc && noTax;
                    detail = "hasFee=" + hasFee + ", noTime=" + noTime + ", noTimeDisc=" + noTimeDisc + ", noTax=" + noTax;
                }
            }
            else
            {
                detail = "err=" + err;
            }

            Check("In P1 Doanh thu (VisibleByConfigID bật CoPhiDichVu một mình): bán hàng A4 hiện Phí dv, vẫn ẩn Tiền giờ, Giảm giờ, Thuế",
                ok, detail);
        }

        private static void VerifyUnknownAndFailedConfigVisibility(PrintTemplateRepository repo, PrintJobData job)
        {
            const string unknownId = "99999999-9999-9999-9999-999999999999";
            string xml =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Report ReportInfo.CreatorVersion=\"1.2.47.0\">" +
                "<Dictionary/>" +
                "<ReportPage Name=\"Page1\">" +
                "<ReportTitleBand Name=\"ReportTitle1\" Width=\"718.2\" Height=\"40\">" +
                "<TableObject Name=\"Table1\" Width=\"200\" Height=\"30\">" +
                "<TableColumn Name=\"Column1\" Width=\"100\"/>" +
                "<TableColumn Name=\"Column2\" Width=\"100\" VisibleByConfigID=\"" + unknownId + "\"/>" +
                "<TableRow Name=\"Row1\" Height=\"30\">" +
                "<TableCell Name=\"Cell1\" Text=\"Cột chuẩn\"/>" +
                "<TableCell Name=\"Cell2\" Text=\"Cột ID lạ\"/>" +
                "</TableRow></TableObject></ReportTitleBand></ReportPage></Report>";

            var tpl = new PrintTemplateRecord { Id = "u1", Name = "Mẫu ID cấu hình", TemplateXml = xml };
            job.ConfigVisibilityById = repo.LoadConfigVisibilityForTemplate(tpl);
            Report rptUnknown;
            string errUnknown;
            bool keepUnknownOk = false;
            if (FastReportEngine.TryPrepare(tpl, job, out rptUnknown, out errUnknown) && rptUnknown != null)
            {
                using (rptUnknown)
                {
                    List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(rptUnknown);
                    keepUnknownOk = PageTextsContain(texts, "Cột chuẩn") && PageTextsContain(texts, "Cột ID lạ");
                }
            }

            // Thiếu bản đồ cấu hình khi mẫu có VisibleByConfigID -> phải báo lỗi, không lặng lẽ in cột đáng ẩn.
            tpl.ConfigVisibilityById = null;
            job.ConfigVisibilityById = null;
            Report rptMissing;
            string errMissing;
            bool rejectMissingOk = !FastReportEngine.TryPrepare(tpl, job, out rptMissing, out errMissing)
                && rptMissing == null && !string.IsNullOrEmpty(errMissing)
                && errMissing.IndexOf("Mẫu ID cấu hình", StringComparison.Ordinal) >= 0;

            // Lỗi đọc cấu hình ở tầng Data (DB hỏng) -> coordinator báo lỗi, không in.
            var recErr = new PrintOutputRecorder();
            string recordedErr = null;
            var coord = new FormPrintCoordinator();
            bool coordRejected = !coord.Print(
                null,
                null,
                "Z:\\khong-ton-tai-selftest.fdb",
                PrintConstants.RevenueReportFormId,
                delegate (CompanyPrintInfo c) { return RevenueReportPrintDataBuilder.Build(new List<RevenueBillRow>(), DateTime.Today, DateTime.Today, c); },
                recErr,
                null,
                delegate (string m) { recordedErr = m; });
            bool dbErrOk = coordRejected && recErr.ErrorMessages.Count == 1 && !string.IsNullOrEmpty(recordedErr)
                && recErr.PreviewCount == 0 && recErr.PrintCount == 0;

            Check("In P1 Doanh thu (VisibleByConfigID biên & lỗi): ID không có trong SCONFIG giữ cột hiện; thiếu/lỗi cấu hình báo lỗi và không in",
                keepUnknownOk && rejectMissingOk && dbErrOk,
                "keepUnknown=" + keepUnknownOk + ", rejectMissing=" + rejectMissingOk + ", dbErr=" + dbErrOk);
        }

        private static PrintTemplateRecord FindTemplateByName(List<PrintTemplateRecord> templates, string name)
        {
            for (int i = 0; i < templates.Count; i++)
                if (templates[i] != null && string.Equals(templates[i].Name, name, StringComparison.Ordinal))
                    return templates[i];
            return null;
        }

        private static bool PageTextsContainIgnoreCase(List<KeyValuePair<string, string>> items, string expected)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if ((items[i].Value ?? "").IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool PageTextsContainNumber(List<KeyValuePair<string, string>> items, decimal expected)
        {
            for (int i = 0; i < items.Count; i++)
            {
                decimal v;
                if (TryParseCellNumber(items[i].Value, out v) && v == expected)
                    return true;
            }
            return false;
        }
    }
}
