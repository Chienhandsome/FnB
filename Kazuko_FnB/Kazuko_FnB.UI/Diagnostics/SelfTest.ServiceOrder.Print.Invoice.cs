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
        private static void ServicePrintInvoiceTests(string db, ConfigRepository cfgRepo, UserSession admin, ServicePrintFixture fx)
        {
            var svc = new TableService(admin);
            var tplRepo = new PrintTemplateRepository(db);
            CompanyPrintInfo company = FormPrintCoordinator.LoadCompanyInfoSafe(db);
            Dictionary<string, string> tplSnapBefore = SnapshotStemplate(db);

            // 1. Nạp đủ các mẫu hóa đơn bán hàng đang hoạt động (10 mẫu chuẩn DB) và kiểm mặc định "Mẫu in bill 80mm".
            List<PrintTemplateRecord> invoiceTemplates = tplRepo.LoadTemplatesForForm(PrintConstants.SalesInvoiceFormId);
            int defIdx;
            List<PrintTemplateRecord> ordered = PrintTemplateOrdering.OrderAndFindDefault(
                invoiceTemplates, PrintDialogOptions.ForSalesInvoiceReportDialog(), out defIdx);
            PrintTemplateRecord tpl80 = FindTemplateByName(invoiceTemplates, "Mẫu in bill 80mm");
            PrintTemplateRecord tplA4 = FindTemplateByName(invoiceTemplates, "Mẫu in bill A4");

            Check("SDDV In §1 Nạp đủ mẫu STEMPLATE hóa đơn bán hàng (10 mẫu) và mặc định chọn Mẫu in bill 80mm",
                invoiceTemplates.Count == 10 && tpl80 != null && tplA4 != null
                && defIdx >= 0 && string.Equals(ordered[defIdx].Name, "Mẫu in bill 80mm", StringComparison.OrdinalIgnoreCase),
                "count=" + invoiceTemplates.Count + ", def=" + (defIdx >= 0 ? ordered[defIdx].Name : "none"));

            // 2. Tạo bill đủ tình huống: món thường + đồ uống + combo cha/con + giảm giá + phí DV + thuế + khách (nợ cũ 50k, điểm 120) + NV + cọc 100k.
            SetPrintConfigInt(db, cfgRepo, fx, "CoPhiDichVu", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "CoThueSuat", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "ChoPhepKhachNo", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "ChoPhepThayDoiHoaDonSauKhiInTamTinh", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "SoLanInTamTinhToiDa", 2);
            SetPrintConfigInt(db, cfgRepo, fx, "HienThiTruocKhiIn", 0);
            SetPrintConfigInt(db, cfgRepo, fx, "LuaChonMauKhiIn", 0);
            SetPrintConfigInt(db, cfgRepo, fx, "SoLanIn", 1);
            SetPrintConfigInt(db, cfgRepo, fx, "InHoaDonTheoKhuVuc", 0);
            SetPrintConfigInt(db, cfgRepo, fx, "HienThiDiemCuaKhachHangTrenHoaDon", 30);
            SetPrintConfigStr(db, cfgRepo, fx, "MauHoaDon", "");

            ServiceOpResult open = svc.OpenTable(fx.Table1Id, true, fx.BookingId);
            fx.CreatedBillIds.Add(open.Bill.Id);
            svc.AddItem(open.Bill.Id, fx.Table1Id, fx.ProdFoodId, 2m, 120000m);
            svc.AddItem(open.Bill.Id, fx.Table1Id, fx.ProdDrinkId, 3m, 40000m);
            svc.AddItem(open.Bill.Id, fx.Table1Id, fx.ProdComboId, 1m, 150000m);
            svc.UpdateBillFinancials(open.Bill.Id, fx.Table1Id, 10m, 0m, false, 5m, 0m, false, 8m);

            // Gắn khách hàng, nhân viên, pass wifi vào đầu phiếu.
            BookingExec(db,
                "UPDATE TDONHANG SET DKHACHHANGID = @K, DNHANVIENXUATID = @E, PASSWIFI = 'WIFI-TEST' WHERE ID = @I",
                "@K", fx.CustomerId, "@E", fx.EmployeeId, "@I", open.Bill.Id);

            // 3. Kiểm TryPrepare toàn bộ 10 mẫu STEMPLATE với dữ liệu hóa đơn thật.
            ServiceInvoicePrintDocument docBeforePay = svc.LoadInvoiceForPrint(open.Bill.Id);
            PrintJobData jobPaid = SalesInvoicePrintDataBuilder.Build(docBeforePay, company,
                new InvoicePrintBuildOptions { IsTempPrint = false, ShowCustomerPoints = true, PrintedBy = admin.DisplayName });
            PrintJobData jobTemp = SalesInvoicePrintDataBuilder.Build(docBeforePay, company,
                new InvoicePrintBuildOptions { IsTempPrint = true, ShowCustomerPoints = true, PrintedBy = admin.DisplayName });

            var failedTemplates = new List<string>();
            for (int i = 0; i < invoiceTemplates.Count; i++)
            {
                PrintTemplateRecord tpl = invoiceTemplates[i];
                Report rpt;
                string err;
                if (!FastReportEngine.TryPrepare(tpl, jobPaid, out rpt, out err))
                    failedTemplates.Add(tpl.Name + ":" + SingleLineDetail(err));
                else if (rpt != null)
                    rpt.Dispose();
            }
            Check("SDDV In §2 Toàn bộ 10 mẫu STEMPLATE hóa đơn bán hàng đều TryPrepare thành công với dữ liệu thật",
                failedTemplates.Count == 0,
                failedTemplates.Count == 0 ? "ok=10/10" : SingleLineDetail(string.Join("; ", failedTemplates.ToArray())));

            // 4. Kiểm chính xác từng ô đã render trên Mẫu in bill 80mm và Mẫu in bill A4 (tên món, TONGCONG, Đặt trước, Nợ mới, bàn, khách, NV, công ty).
            CheckInvoiceTemplateRenderedCells(tpl80, jobPaid, jobTemp, docBeforePay, company, fx, "Mẫu in bill 80mm");
            CheckInvoiceTemplateRenderedCells(tplA4, jobPaid, jobTemp, docBeforePay, company, fx, "Mẫu in bill A4");

            // 5. In tạm tính: tăng SOLANINTAMTINH, ghi INTAMTINHLUC, đổi trạng thái bàn -> TempPrinted, chặn vượt SoLanInTamTinhToiDa=2 và kiểm quyền FuncInTamTinh.
            var waiter = new UserSession { UserId = "u-no-temp", UserName = "waiter", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false) };
            var waiterSvc = new TableService(waiter);
            TempPrintPrepareResult deniedTemp = waiterSvc.PrepareAndRecordTempPrint(open.Bill.Id, fx.Table1Id, null);

            var recorder = new ServicePrintOutputRecorder();
            var store = new MemoryKitchenPrinterStore("MAY-IN-QUAY", "MAY-IN-KV");
            var coord = new ServiceInvoicePrintCoordinator();

            TempPrintPrepareResult tp1 = svc.PrepareAndRecordTempPrint(open.Bill.Id, fx.Table1Id, null);
            bool p1Ok = tp1.Ok && coord.PrintTempBill(null, null, db, admin, tp1.Document, store, recorder, null, null, null);
            DataRow rowAfterTp1 = DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                "SELECT SOLANINTAMTINH, INTAMTINHLUC FROM TDONHANG WHERE ID = @I", "@I", open.Bill.Id).Rows[0]);
            ServiceTableInfo cardAfterTp1 = svc.LoadTables().Find(t => t.Id == fx.Table1Id);

            TempPrintPrepareResult tp2 = svc.PrepareAndRecordTempPrint(open.Bill.Id, fx.Table1Id, null);
            TempPrintPrepareResult tp3 = svc.PrepareAndRecordTempPrint(open.Bill.Id, fx.Table1Id, null);
            bool tempTitleRendered = recorder.PrintCalls.Count == 1 && PageTextsContain(recorder.PrintCalls[0].PageTexts, ServicePrintConstants.InvoiceTitleTemp);
            string expectedMaxMsg = ServicePrintConstants.MaxTempPrintReached(2);

            Check("SDDV In §3 In tạm tính: kiểm quyền FuncInTamTinh, tăng SOLANINTAMTINH + INTAMTINHLUC, bàn chuyển TempPrinted, in tiêu đề PHIẾU TẠM TÍNH, chặn vượt giới hạn",
                deniedTemp.NeedsReLogin
                && p1Ok && tempTitleRendered
                && DbHelper.Int(rowAfterTp1["SOLANINTAMTINH"]) == 1 && !(rowAfterTp1["INTAMTINHLUC"] is DBNull)
                && cardAfterTp1 != null && cardAfterTp1.Status == TableStatus.TempPrinted
                && tp2.Ok && !tp3.Ok && tp3.ErrorMessage == expectedMaxMsg,
                "tp1=" + p1Ok + ", cnt=" + DbHelper.Int(rowAfterTp1["SOLANINTAMTINH"]) + ", st=" + (cardAfterTp1 != null ? cardAfterTp1.Status.ToString() : "null") + ", tp3=" + SingleLineDetail(tp3.ErrorMessage));

            // 6. Thanh toán & In + InHoaDonTheoKhuVuc = OneAtCounterOneByArea (2 bản: quầy + khu vực), và In lại bill (F8) không đổi dữ liệu + kiểm quyền FuncInLaiBill.
            SetPrintConfigInt(db, cfgRepo, fx, "InHoaDonTheoKhuVuc", (int)InvoiceAreaPrintMode.OneAtCounterOneByArea);
            store.SetConfiguredPrinter(KitchenPrintRules.AreaInvoicePrinterKey(fx.AreaId), "MAY-IN-KV");

            PaymentExecutionResult payRes = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = open.Bill.Id,
                PrimaryTableId = fx.Table1Id,
                CustomerId = fx.CustomerId,
                CashTendered = 200000m,
                ConfirmDebt = true,
                PrintAfterClose = true
            });
            var payRec = new ServicePrintOutputRecorder();
            ServiceInvoicePrintDocument paidDoc = svc.LoadInvoiceForPrint(open.Bill.Id);
            bool payPrintOk = payRes.Ok && payRes.PrintAfterClose && coord.PrintAfterPayment(null, null, db, admin, paidDoc, store, payRec, null, null);
            bool payAreaRoutingOk = payRec.PrintCalls.Count == 2
                && payRec.PrintCalls[1].PrinterName == "MAY-IN-KV"
                && PageTextsContain(payRec.PrintCalls[0].PageTexts, ServicePrintConstants.InvoiceTitleOfficial);

            Check("SDDV In §4 Thanh toán & In: đóng bill và in đúng 2 liên (1 tại quầy + 1 máy in khu vực) với tiêu đề HÓA ĐƠN THANH TOÁN",
                payPrintOk && payAreaRoutingOk,
                "calls=" + payRec.PrintCalls.Count + ", p0=" + (payRec.PrintCalls.Count > 0 ? payRec.PrintCalls[0].PrinterName : "") + ", p1=" + (payRec.PrintCalls.Count > 1 ? payRec.PrintCalls[1].PrinterName : ""));

            // In lại bill: quyền FuncInLaiBill, tiêu đề HÓA ĐƠN THANH TOÁN, không đổi TDONHANG/TDONHANGCHITIET/STEMPLATE.
            TempPrintPrepareResult deniedReprint = waiterSvc.PrepareReprintPaidBill(open.Bill.Id, null);
            string billSnapBefore = SnapshotSingleBill(db, open.Bill.Id);
            var reprintRec = new ServicePrintOutputRecorder();
            TempPrintPrepareResult prepReprint = svc.PrepareReprintPaidBill(open.Bill.Id, null);
            bool reprintOk = prepReprint.Ok && coord.ReprintPaidBill(null, null, db, admin, prepReprint.Document, store, reprintRec, null, null, null);
            string billSnapAfter = SnapshotSingleBill(db, open.Bill.Id);
            Dictionary<string, string> tplSnapAfter = SnapshotStemplate(db);

            Check("SDDV In §5 In lại bill (F8): kiểm quyền FuncInLaiBill, in HÓA ĐƠN THANH TOÁN (1 bản tại quầy), không đổi TDONHANG/TDONHANGCHITIET và STEMPLATE",
                deniedReprint.NeedsReLogin
                && reprintOk && reprintRec.PrintCalls.Count == 1
                && PageTextsContain(reprintRec.PrintCalls[0].PageTexts, ServicePrintConstants.InvoiceTitleOfficial)
                && billSnapBefore == billSnapAfter
                && SnapshotsEqual(tplSnapBefore, tplSnapAfter),
                "reprint=" + reprintOk + ", calls=" + reprintRec.PrintCalls.Count + ", billSame=" + (billSnapBefore == billSnapAfter));

            // 7. Khi lỗi máy in hoặc mẫu hỏng lúc Thanh toán & In: báo 1 lỗi rõ tên mẫu, bill vẫn đã đóng (DATHANHTOAN = 30).
            var failRec = new ServicePrintOutputRecorder { ThrowOnPrint = true, ThrowMessage = "Máy in mất kết nối" };
            bool failPrinterRes = coord.PrintAfterPayment(null, null, db, admin, paidDoc, store, failRec, null, null);
            var brokenTplRec = new ServicePrintOutputRecorder();
            bool brokenTplRes = coord.PrintAfterPayment(null, null, db, admin, paidDoc, store, brokenTplRec,
                delegate (string formId)
                {
                    return new List<PrintTemplateRecord> { new PrintTemplateRecord { Id = "broken", Name = "Mẫu hỏng test", TemplateXml = "<broken" } };
                }, null);
            int paidFlagNow = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT DATHANHTOAN FROM TDONHANG WHERE ID = @I", "@I", open.Bill.Id)));

            Check("SDDV In §6 Lỗi máy in / mẫu XML hỏng sau thanh toán: báo đúng 1 lỗi kèm tên mẫu và bill vẫn giữ trạng thái đã đóng",
                !failPrinterRes && failRec.ErrorMessages.Count == 1 && failRec.ErrorMessages[0].IndexOf("Mẫu in bill 80mm", StringComparison.OrdinalIgnoreCase) >= 0
                && !brokenTplRes && brokenTplRec.ErrorMessages.Count == 1 && brokenTplRec.ErrorMessages[0].IndexOf("Mẫu hỏng test", StringComparison.OrdinalIgnoreCase) >= 0
                && paidFlagNow == 30,
                "errPrn=" + (failRec.ErrorMessages.Count > 0 ? failRec.ErrorMessages[0] : "") + ", errTpl=" + (brokenTplRec.ErrorMessages.Count > 0 ? brokenTplRec.ErrorMessages[0] : ""));
        }

        private static void CheckInvoiceTemplateRenderedCells(
            PrintTemplateRecord tpl,
            PrintJobData jobPaid,
            PrintJobData jobTemp,
            ServiceInvoicePrintDocument doc,
            CompanyPrintInfo company,
            ServicePrintFixture fx,
            string label)
        {
            Report rptPaid;
            string errPaid;
            bool okPaid = FastReportEngine.TryPrepare(tpl, jobPaid, out rptPaid, out errPaid);
            List<KeyValuePair<string, string>> textsPaid = okPaid ? FastReportEngine.ExtractPreparedPageTexts(rptPaid) : new List<KeyValuePair<string, string>>();
            if (rptPaid != null) rptPaid.Dispose();

            Report rptTemp;
            string errTemp;
            bool okTemp = FastReportEngine.TryPrepare(tpl, jobTemp, out rptTemp, out errTemp);
            List<KeyValuePair<string, string>> textsTemp = okTemp ? FastReportEngine.ExtractPreparedPageTexts(rptTemp) : new List<KeyValuePair<string, string>>();
            if (rptTemp != null) rptTemp.Dispose();

            List<TemplateObjInfo> map = ParseTemplateObjectMap(tpl.TemplateXml);
            string actualTotal;
            actualTotal = doc.TongCong.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            bool totalOk = PageTextsContain(textsPaid, actualTotal);
            string actualDeposit;
            actualDeposit = doc.DatTruoc.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            bool depOk = PageTextsContain(textsPaid, actualDeposit);
            decimal expectedNewDebt = doc.NoCu + doc.CongNo;
            string actualDebt;
            actualDebt = expectedNewDebt.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            bool debtOk = PageTextsContain(textsPaid, actualDebt);

            bool itemsOk = PageTextsContain(textsPaid, "Pho Bo PRT " + fx.Tag)
                && PageTextsContain(textsPaid, "Tra Dao PRT " + fx.Tag)
                && PageTextsContain(textsPaid, "Combo Trua " + fx.Tag)
                && PageTextsContain(textsPaid, "Banh Cuon Con " + fx.Tag);
            string companyName = company != null ? (company.CompanyName ?? "").Trim() : "";
            bool headerOk = PageTextsContain(textsPaid, ServicePrintConstants.InvoiceTitleOfficial)
                && PageTextsContain(textsTemp, ServicePrintConstants.InvoiceTitleTemp)
                && PageTextsContain(textsPaid, fx.Table1Name)
                && PageTextsContain(textsPaid, fx.CustomerName)
                && (companyName.Length == 0 || PageTextsContain(textsPaid, companyName));

            Check("SDDV In §2 Chi tiết ô đã render trên " + label + " (Table0, combo con, TONGCONG, Đặt trước, Nợ mới, bàn/khách/tiêu đề)",
                okPaid && okTemp && totalOk && depOk && debtOk && itemsOk && headerOk,
                "tot=" + actualTotal + "/" + doc.TongCong + ", dep=" + actualDeposit + ", debt=" + actualDebt + ", items=" + itemsOk + ", hdr=" + headerOk);
        }

        private static string SnapshotSingleBill(string db, string billId)
        {
            return DbHelper.Read(db, conn =>
            {
                DataRow h = DbHelper.Table(conn, null,
                    "SELECT DATHANHTOAN, SOLANINTAMTINH, LANINHOADON, TONGCONG, TIENMAT, CONGNO, TIENTHANHTOAN FROM TDONHANG WHERE ID = @I",
                    "@I", billId).Rows[0];
                DataRow d = DbHelper.Table(conn, null,
                    "SELECT COUNT(*) AS CNT, COALESCE(SUM(SLXUAT), 0) AS QTY, COALESCE(SUM(THANHTIEN), 0) AS AMT FROM TDONHANGCHITIET WHERE TDONHANGID = @I",
                    "@I", billId).Rows[0];
                return DbHelper.Int(h["DATHANHTOAN"]) + "|" + DbHelper.Int(h["SOLANINTAMTINH"]) + "|" + DbHelper.Int(h["LANINHOADON"])
                    + "|" + DbHelper.Dec(h["TONGCONG"]) + "|" + DbHelper.Dec(h["TIENMAT"]) + "|" + DbHelper.Dec(h["CONGNO"])
                    + "|" + DbHelper.Int(d["CNT"]) + "|" + DbHelper.Dec(d["QTY"]) + "|" + DbHelper.Dec(d["AMT"]);
            });
        }
    }
}
