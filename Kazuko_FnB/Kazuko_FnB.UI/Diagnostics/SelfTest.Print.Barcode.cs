using System;
using System.Collections.Generic;
using System.Data;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP4BarcodeTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            var adminSession = new UserSession
            {
                UserId = "selftest_p4",
                UserName = "selftest_p4",
                DisplayName = "Selftest P4",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };
            AppSession.Current = adminSession;

            BarcodePrintFixture fx = null;
            try
            {
                fx = CreateBarcodePrintFixture(db);
                Dictionary<string, string> tplSnapBefore = SnapshotStemplate(db);
                string mhSnapBefore = SnapshotDmathang(db, fx.DeletedProductId);

                TestBarcodeAllSixTemplatesAndLayout(db, fx);
                TestBarcodeEmptyCodeAndDeletedProduct(db, fx);
                TestBarcodeCostPermissionN3(db, fx);
                TestBarcodeCorruptOrMissingTemplates(db, fx);
                PrintP4BarcodeUiTests(db, adminSession, fx);

                Dictionary<string, string> tplSnapAfter = SnapshotStemplate(db);
                string mhSnapAfter = SnapshotDmathang(db, fx.DeletedProductId);
                Check("In P4 (ix/T5 STEMPLATE và DMATHANG không đổi): toàn bộ mẫu tem và dữ liệu mặt hàng giữ nguyên sau khi in/xuất",
                    SnapshotsEqual(tplSnapBefore, tplSnapAfter) && mhSnapBefore == mhSnapAfter,
                    "tpl=" + SnapshotsEqual(tplSnapBefore, tplSnapAfter) + ",mh=" + (mhSnapBefore == mhSnapAfter));
            }
            finally
            {
                CleanupBarcodePrintFixture(db, fx);
                AppSession.Current = savedSession;
            }
        }

        private static void TestBarcodeAllSixTemplatesAndLayout(string db, BarcodePrintFixture fx)
        {
            var tplRepo = new PrintTemplateRepository(db);
            List<PrintTemplateRecord> templates = tplRepo.LoadBarcodeTemplatesForTable(PrintConstants.ProductTableDescId);

            bool sortedOk = true;
            for (int i = 1; i < templates.Count; i++)
            {
                if (StringComparer.OrdinalIgnoreCase.Compare(templates[i - 1].Name ?? "", templates[i].Name ?? "") > 0)
                {
                    sortedOk = false;
                    break;
                }
            }
            Check("In P4 (i/vi đọc 6 mẫu tem STEMPLATE): đúng 6 mẫu BARCODE=30, STATUS=30 của DMATHANG sắp theo NAME",
                templates.Count == 6 && sortedOk,
                "count=" + templates.Count + ",sorted=" + sortedOk);

            var dataRepo = new BarcodePrintRepository(db);
            Dictionary<string, BarcodeProductRecord> products = dataRepo.LoadProductsByIds(
                new[] { fx.Product1Id, fx.Product2Id, fx.Product3Id }, true);

            BarcodeProductRecord p1 = products.ContainsKey(fx.Product1Id) ? products[fx.Product1Id] : null;
            BarcodeProductRecord p2 = products.ContainsKey(fx.Product2Id) ? products[fx.Product2Id] : null;
            BarcodeProductRecord p3 = products.ContainsKey(fx.Product3Id) ? products[fx.Product3Id] : null;

            var entries = new List<BarcodePrintEntry>
            {
                new BarcodePrintEntry { Product = p1, Quantity = 2 },
                new BarcodePrintEntry { Product = p2, Quantity = 1 },
                new BarcodePrintEntry { Product = p3, Quantity = 3 }
            };
            var expectedOrder = new[] { p1, p1, p2, p3, p3, p3 };
            CompanyPrintInfo company = tplRepo.LoadCompanyInfo();

            bool sawTwoPerRow = false;
            bool sawFivePerRow = false;
            bool allPreparedOk = templates.Count == 6 && p1 != null && p2 != null && p3 != null;
            bool allEmptySlotsOk = true;
            string failDetail = "";

            for (int t = 0; t < templates.Count; t++)
            {
                PrintTemplateRecord tpl = templates[t];
                int n;
                List<BarcodeTemplateColumnSpec> cols;
                string layoutErr;
                if (!BarcodePrintDataBuilder.TryInferLayout(tpl, out n, out cols, out layoutErr))
                {
                    allPreparedOk = false;
                    failDetail = "layout:" + tpl.Name + ":" + SingleLineDetail(layoutErr);
                    break;
                }

                if (n == 2) sawTwoPerRow = true;
                if (n == 5) sawFivePerRow = true;

                int expectedRows = (expectedOrder.Length + n - 1) / n;
                PrintJobData job;
                string buildErr;
                if (!BarcodePrintDataBuilder.TryBuild(tpl, entries, company, true, out job, out buildErr))
                {
                    allPreparedOk = false;
                    failDetail = "build:" + tpl.Name + ":" + SingleLineDetail(buildErr);
                    break;
                }

                using (job)
                {
                    DataTable dt = job.DataSet.Tables["Table0"];
                    if (dt == null || dt.Rows.Count != expectedRows
                        || job.PreviewTitle != PrintConstants.BarcodePreviewTitle
                        || job.DefaultFileName != PrintConstants.BarcodeDefaultFileName)
                    {
                        allPreparedOk = false;
                        failDetail = "rows:" + tpl.Name + ":" + (dt == null ? -1 : dt.Rows.Count) + "!=" + expectedRows;
                        break;
                    }

                    Report prepared;
                    string prepErr;
                    if (!FastReportEngine.TryPrepare(tpl, job, out prepared, out prepErr))
                    {
                        allPreparedOk = false;
                        failDetail = "prepare:" + tpl.Name + ":" + SingleLineDetail(prepErr);
                        break;
                    }

                    using (prepared)
                    {
                        if (prepared.PreparedPages == null || prepared.PreparedPages.Count == 0)
                        {
                            allPreparedOk = false;
                            failDetail = "pages=0:" + tpl.Name;
                            break;
                        }

                        string verifyErr;
                        bool emptyOk;
                        if (!VerifyPreparedBarcodeTemplate(tpl, n, prepared, expectedOrder, out emptyOk, out verifyErr))
                        {
                            allPreparedOk = false;
                            failDetail = tpl.Name + ":" + verifyErr;
                            break;
                        }
                        if (!emptyOk)
                        {
                            allEmptySlotsOk = false;
                            failDetail = "emptySlot:" + tpl.Name + ":" + verifyErr;
                        }
                    }
                }
            }

            Check("In P4 (i/T1/T7 cả 6 mẫu tem mã vạch dựng OK): 3 MH sl 2,1,3=6 tem, mẫu 2 tem ra 3 hàng, mẫu 5 tem ra 2 hàng, đúng thứ tự/tên tiếng Việt/giá/mã vạch",
                allPreparedOk && sawTwoPerRow && sawFivePerRow,
                allPreparedOk ? ("2tem=" + sawTwoPerRow + ",5tem=" + sawFivePerRow) : failDetail);

            Check("In P4 (ii tem trống ở hàng cuối thiếu tem): các ô trống trên mẫu 5 tem không mang tên mặt hàng và BarcodeObject không có mã",
                allPreparedOk && sawFivePerRow && allEmptySlotsOk,
                allEmptySlotsOk ? "ok" : failDetail);
        }

        private static bool VerifyPreparedBarcodeTemplate(
            PrintTemplateRecord tpl,
            int n,
            Report prepared,
            BarcodeProductRecord[] expectedOrder,
            out bool emptySlotsOk,
            out string error)
        {
            emptySlotsOk = true;
            error = "";

            BarcodeSlotMap slots = ParseBarcodeTemplateSlots(tpl, n);
            List<KeyValuePair<string, string>> pageTexts = FastReportEngine.ExtractPreparedPageTexts(prepared);
            List<KeyValuePair<string, string>> pageBarcodes = ExtractPreparedBarcodes(prepared);

            int expectedRows = (expectedOrder.Length + n - 1) / n;
            for (int k = 0; k < n; k++)
            {
                if (string.IsNullOrEmpty(slots.NameObjNames[k]))
                {
                    error = "noNameObj k=" + k;
                    return false;
                }
                List<string> nameCells = FindAllObjectTexts(pageTexts, slots.NameObjNames[k]);
                if (nameCells.Count != expectedRows)
                {
                    error = "nameRows k=" + k + " got=" + nameCells.Count + " exp=" + expectedRows;
                    return false;
                }

                List<string> priceCells = string.IsNullOrEmpty(slots.PriceObjNames[k])
                    ? null
                    : FindAllObjectTexts(pageTexts, slots.PriceObjNames[k]);
                List<string> codeCells = !string.IsNullOrEmpty(slots.BarcodeObjNames[k])
                    ? FindAllObjectTexts(pageBarcodes, slots.BarcodeObjNames[k])
                    : (!string.IsNullOrEmpty(slots.CodeTextObjNames[k]) ? FindAllObjectTexts(pageTexts, slots.CodeTextObjNames[k]) : null);

                // Tem trống (mã rỗng) thì FastReport không phát BarcodeObject: chỉ đếm các hàng có tem thật ở vị trí k.
                int filledRows = 0;
                for (int rr = 0; rr < expectedRows; rr++)
                {
                    if (rr * n + k < expectedOrder.Length)
                        filledRows++;
                }
                if (codeCells == null || codeCells.Count != filledRows)
                {
                    error = "codeRows k=" + k + " got=" + (codeCells == null ? -1 : codeCells.Count) + " exp=" + filledRows;
                    return false;
                }

                for (int r = 0; r < expectedRows; r++)
                {
                    int idx = r * n + k;
                    if (idx < expectedOrder.Length)
                    {
                        BarcodeProductRecord exp = expectedOrder[idx];
                        if ((nameCells[r] ?? "").IndexOf(exp.Name, StringComparison.Ordinal) < 0)
                        {
                            error = "nameMismatch r=" + r + " k=" + k + " got=" + SingleLineDetail(nameCells[r]);
                            return false;
                        }
                        if (priceCells != null && r < priceCells.Count)
                        {
                            decimal parsedPrice;
                            if (!TryExtractBarcodePrice(priceCells[r], out parsedPrice) || parsedPrice != exp.SellPrice)
                            {
                                error = "priceMismatch r=" + r + " k=" + k + " got=" + SingleLineDetail(priceCells[r]) + " exp=" + exp.SellPrice;
                                return false;
                            }
                        }
                        if (!MatchesBarcodeText(codeCells[r], exp.Code))
                        {
                            error = "codeMismatch r=" + r + " k=" + k + " got=" + SingleLineDetail(codeCells[r]) + " exp=" + exp.Code;
                            return false;
                        }
                    }
                    else
                    {
                        string emptyName = nameCells[r] ?? "";
                        for (int p = 0; p < expectedOrder.Length; p++)
                        {
                            if (emptyName.IndexOf(expectedOrder[p].Name, StringComparison.Ordinal) >= 0)
                            {
                                emptySlotsOk = false;
                                error = "emptySlotHasName r=" + r + " k=" + k;
                                return true;
                            }
                        }
                        if (r < codeCells.Count && !string.IsNullOrWhiteSpace(codeCells[r]))
                        {
                            emptySlotsOk = false;
                            error = "emptySlotHasCode r=" + r + " k=" + k + " val=" + SingleLineDetail(codeCells[r]);
                            return true;
                        }
                    }
                }
            }
            return true;
        }

        private static void TestBarcodeEmptyCodeAndDeletedProduct(string db, BarcodePrintFixture fx)
        {
            var tplRepo = new PrintTemplateRepository(db);
            List<PrintTemplateRecord> templates = tplRepo.LoadBarcodeTemplatesForTable(PrintConstants.ProductTableDescId);
            PrintTemplateRecord sampleTpl = templates.Count > 0 ? templates[0] : null;

            // 1. Mã hàng trống: vẫn dựng được tem, không văng, BarcodeObject rỗng.
            var dataRepo = new BarcodePrintRepository(db);
            Dictionary<string, BarcodeProductRecord> loaded = dataRepo.LoadProductsByIds(new[] { fx.EmptyCodeProductId }, true);
            BarcodeProductRecord emptyCodeProd = loaded.ContainsKey(fx.EmptyCodeProductId) ? loaded[fx.EmptyCodeProductId] : null;

            bool emptyCodeOk = false;
            string emptyCodeDetail = "no-template";
            if (sampleTpl != null && emptyCodeProd != null)
            {
                int n;
                List<BarcodeTemplateColumnSpec> cols;
                string err;
                PrintJobData job;
                if (BarcodePrintDataBuilder.TryInferLayout(sampleTpl, out n, out cols, out err)
                    && BarcodePrintDataBuilder.TryBuild(
                        sampleTpl,
                        new[] { new BarcodePrintEntry { Product = emptyCodeProd, Quantity = 1 } },
                        tplRepo.LoadCompanyInfo(),
                        true,
                        out job,
                        out err))
                {
                    using (job)
                    {
                        Report prepared;
                        if (FastReportEngine.TryPrepare(sampleTpl, job, out prepared, out err))
                        {
                            using (prepared)
                            {
                                BarcodeSlotMap slots = ParseBarcodeTemplateSlots(sampleTpl, n);
                                List<KeyValuePair<string, string>> texts = FastReportEngine.ExtractPreparedPageTexts(prepared);
                                List<KeyValuePair<string, string>> bcs = ExtractPreparedBarcodes(prepared);
                                List<string> slot0Names = FindAllObjectTexts(texts, slots.NameObjNames[0]);
                                List<string> slot0Codes = !string.IsNullOrEmpty(slots.BarcodeObjNames[0])
                                    ? FindAllObjectTexts(bcs, slots.BarcodeObjNames[0])
                                    : FindAllObjectTexts(texts, slots.CodeTextObjNames[0]);
                                emptyCodeOk = slot0Names.Count == 1
                                    && slot0Names[0].IndexOf("Nước suối không mã P4", StringComparison.Ordinal) >= 0
                                    && (slot0Codes.Count == 0 || string.IsNullOrWhiteSpace(slot0Codes[0]));
                                emptyCodeDetail = "names=" + slot0Names.Count + ",code=" + SingleLineDetail(slot0Codes.Count > 0 ? slot0Codes[0] : "");
                            }
                        }
                        else
                        {
                            emptyCodeDetail = SingleLineDetail(err);
                        }
                    }
                }
            }
            Check("In P4 (mã hàng trống): vẫn in tem bình thường, có tên mặt hàng, ô mã vạch trống, không văng",
                emptyCodeOk, emptyCodeDetail);

            // 2. Mặt hàng đã xóa khỏi CSDL trước khi bấm Chấp nhận: bỏ qua dòng đã xóa, vẫn xem trước mặt hàng còn lại và báo 1 câu cuối.
            BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" = @id", "@id", fx.DeletedProductId);
            var coordinator = new BarcodePrintCoordinator();
            var recorder = new PrintOutputRecorder();
            string recordedMsg = null;
            bool ok = coordinator.Print(
                null,
                null,
                db,
                new[]
                {
                    new BarcodePrintRequestItem { ProductId = fx.DeletedProductId, Code = "8931099", Name = "Mặt hàng sắp xóa P4", Quantity = 2 },
                    new BarcodePrintRequestItem { ProductId = fx.Product1Id, Code = "8931001", Name = "Cà phê sữa đá P4", Quantity = 1 }
                },
                true,
                recorder,
                delegate (System.Windows.Forms.IWin32Window owner, IList<PrintTemplateRecord> list)
                {
                    return new PrintSelectionResult { Template = list[0], Action = PrintActionKind.Preview };
                },
                null,
                msg => recordedMsg = msg);

            bool deletedSkipOk = ok
                && recorder.PreviewCount == 1
                && recorder.PrintCount == 0
                && recorder.InfoMessages.Count == 1
                && recorder.InfoMessages[0].IndexOf("Mặt hàng sắp xóa P4", StringComparison.Ordinal) >= 0
                && !string.IsNullOrEmpty(recordedMsg);

            Check("In P4 (mặt hàng đã xóa trước khi bấm Chấp nhận): bỏ qua dòng đã xóa, vẫn mở xem trước dòng còn lại và báo một câu cuối quá trình",
                deletedSkipOk, "preview=" + recorder.PreviewCount + ",info=" + recorder.InfoMessages.Count);
        }

        private static void TestBarcodeCostPermissionN3(string db, BarcodePrintFixture fx)
        {
            var dataRepo = new BarcodePrintRepository(db);
            Dictionary<string, BarcodeProductRecord> noCostMap = dataRepo.LoadProductsByIds(
                new[] { fx.Product1Id, fx.Product2Id }, false);
            string sqlNoCost = dataRepo.LastExecutedSql ?? "";

            bool sqlExcludesCost = sqlNoCost.IndexOf("GIANHAP", StringComparison.OrdinalIgnoreCase) < 0
                && sqlNoCost.IndexOf("GIAVON", StringComparison.OrdinalIgnoreCase) < 0
                && sqlNoCost.IndexOf("HOAHONG", StringComparison.OrdinalIgnoreCase) < 0;

            BarcodeProductRecord p1NoCost = noCostMap.ContainsKey(fx.Product1Id) ? noCostMap[fx.Product1Id] : null;
            bool memoryZero = p1NoCost != null
                && p1NoCost.BuyPrice == 0m
                && p1NoCost.CostPrice == 0m
                && p1NoCost.Commission == 0m
                && p1NoCost.SellPrice == 25000m;

            var tplRepo = new PrintTemplateRepository(db);
            List<PrintTemplateRecord> templates = tplRepo.LoadBarcodeTemplatesForTable(PrintConstants.ProductTableDescId);
            PrintTemplateRecord tpl = templates.Count > 0 ? templates[0] : null;

            bool dataSetZero = false;
            if (tpl != null && p1NoCost != null)
            {
                PrintJobData jobNoCost;
                string err;
                if (BarcodePrintDataBuilder.TryBuild(
                    tpl,
                    new[] { new BarcodePrintEntry { Product = p1NoCost, Quantity = 2 } },
                    new CompanyPrintInfo(),
                    false,
                    out jobNoCost,
                    out err))
                {
                    using (jobNoCost)
                    {
                        DataTable dt = jobNoCost.DataSet.Tables["Table0"];
                        dataSetZero = dt != null && dt.Rows.Count > 0;
                        if (dataSetZero)
                        {
                            foreach (DataColumn col in dt.Columns)
                            {
                                string cn = col.ColumnName;
                                if (cn.StartsWith("GIANHAP", StringComparison.OrdinalIgnoreCase)
                                    || cn.StartsWith("GIAVON", StringComparison.OrdinalIgnoreCase)
                                    || cn.StartsWith("HOAHONG", StringComparison.OrdinalIgnoreCase))
                                {
                                    foreach (DataRow row in dt.Rows)
                                    {
                                        if (Convert.ToDecimal(row[col]) != 0m)
                                            dataSetZero = false;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Đối chứng: khi CÓ quyền Xem giá nhập thì đọc đủ giá thật.
            Dictionary<string, BarcodeProductRecord> withCostMap = dataRepo.LoadProductsByIds(new[] { fx.Product1Id }, true);
            string sqlWithCost = dataRepo.LastExecutedSql ?? "";
            BarcodeProductRecord p1WithCost = withCostMap.ContainsKey(fx.Product1Id) ? withCostMap[fx.Product1Id] : null;
            bool withCostOk = sqlWithCost.IndexOf("GIANHAP", StringComparison.OrdinalIgnoreCase) >= 0
                && p1WithCost != null
                && p1WithCost.BuyPrice == 111111m
                && p1WithCost.CostPrice == 122222m
                && p1WithCost.Commission == 1333m;

            Check("In P4 (iii/T4 quyền Xem giá nhập N3): không có quyền thì SQL không đọc GIANHAP/GIAVON/HOAHONG và DataSet toàn 0; có quyền thì đọc đúng giá thật",
                sqlExcludesCost && memoryZero && dataSetZero && withCostOk,
                "sqlNoCost=" + sqlExcludesCost + ",memZero=" + memoryZero + ",dsZero=" + dataSetZero + ",withCost=" + withCostOk);
        }

        private static void TestBarcodeCorruptOrMissingTemplates(string db, BarcodePrintFixture fx)
        {
            var badXmlTpl = new PrintTemplateRecord { Id = "bad-1", Name = "Mẫu XML Hỏng P4", TemplateXml = "<Report><Broken" };
            var noNameColsTpl = new PrintTemplateRecord
            {
                Id = "bad-2",
                Name = "Mẫu Thiếu Cột NAME P4",
                TemplateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Report><Dictionary><TableDataSource Name=\"Table0\"><Column Name=\"CODE0\" DataType=\"System.String\"/></TableDataSource></Dictionary></Report>"
            };
            var emptyTpl = new PrintTemplateRecord { Id = "bad-3", Name = "Mẫu Rỗng P4", TemplateXml = "" };

            PrintJobData job;
            string err1, err2, err3;
            bool b1 = BarcodePrintDataBuilder.TryBuild(badXmlTpl, null, null, true, out job, out err1);
            bool b2 = BarcodePrintDataBuilder.TryBuild(noNameColsTpl, null, null, true, out job, out err2);
            bool b3 = BarcodePrintDataBuilder.TryBuild(emptyTpl, null, null, true, out job, out err3);

            var coordinator = new BarcodePrintCoordinator();
            var recorder = new PrintOutputRecorder();
            bool coordOk = coordinator.Print(
                null,
                null,
                db,
                new[] { new BarcodePrintRequestItem { ProductId = fx.Product1Id, Code = "8931001", Name = "Cà phê sữa đá P4", Quantity = 1 } },
                true,
                recorder,
                delegate (System.Windows.Forms.IWin32Window owner, IList<PrintTemplateRecord> list)
                {
                    return new PrintSelectionResult { Template = list[0], Action = PrintActionKind.Preview };
                },
                delegate { return new List<PrintTemplateRecord> { badXmlTpl }; },
                null);

            bool ok = !b1 && !b2 && !b3 && !coordOk
                && (err1 ?? "").IndexOf("Mẫu XML Hỏng P4", StringComparison.Ordinal) >= 0
                && (err2 ?? "").IndexOf("Mẫu Thiếu Cột NAME P4", StringComparison.Ordinal) >= 0
                && (err3 ?? "").IndexOf("Mẫu Rỗng P4", StringComparison.Ordinal) >= 0
                && recorder.PreviewCount == 0
                && recorder.ErrorMessages.Count == 1
                && recorder.ErrorMessages[0].IndexOf("Mẫu XML Hỏng P4", StringComparison.Ordinal) >= 0;

            Check("In P4 (viii/T6 mẫu hỏng hoặc thiếu): báo lỗi nêu đúng tên mẫu, không đoán N, không văng",
                ok, "errs=" + recorder.ErrorMessages.Count);
        }
    }
}
