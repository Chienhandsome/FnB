using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP1RevenueTests(string db)
        {
            Dictionary<string, string> snapBefore = SnapshotStemplate(db);

            var repo = new PrintTemplateRepository(db);
            List<PrintTemplateRecord> templates = repo.LoadTemplatesForForm(PrintConstants.RevenueReportFormId);
            bool tplListOk = templates.Count == 4
                && templates[0].Name == "58mm" && templates[0].TemplateXml.Length > 100
                && templates[1].Name == "80mm" && templates[1].TemplateXml.Length > 100
                && templates[2].Name == "Báo cáo tổng hợp bán hàng A4" && templates[2].TemplateXml.Length > 100
                && templates[3].Name == "Báo cáo tổng hợp thanh toán A4" && templates[3].TemplateXml.Length > 100;
            Check("In P1 Doanh thu (STEMPLATE): đọc đủ 4 mẫu (58mm, 80mm, bán hàng A4, thanh toán A4) đúng thứ tự tên",
                tplListOk,
                "count=" + templates.Count + ", names=" + string.Join(",", templates.ConvertAll(t => t.Name).ToArray()));

            if (templates.Count == 4)
            {
                TestRevenuePrintDialogDefault(templates);
                TestRevenuePrintEndToEnd(db, templates);
            }

            TestRevenuePrintT6Errors(db);

            Dictionary<string, string> snapAfter = SnapshotStemplate(db);
            Check("In P1 Doanh thu (T5): bảng STEMPLATE (số dòng và băm từng blob) không đổi sau khi in và xuất",
                SnapshotsEqual(snapBefore, snapAfter),
                "before=" + snapBefore.Count + ", after=" + snapAfter.Count);
        }

        private static void TestRevenuePrintDialogDefault(List<PrintTemplateRecord> templates)
        {
            var printers = new List<string> { "Máy in nhiệt giả 80mm", "Máy in A4 giả" };
            using (var dlg = new PrintTemplateDialog(templates, printers, "Máy in A4 giả"))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.ShowInTaskbar = false;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                dlg.PerformLayout();
                Application.DoEvents();

                bool ok = dlg.TemplateListForTest.Items.Count == 4
                    && dlg.TemplateListForTest.SelectedIndex == 3
                    && Convert.ToString(dlg.TemplateListForTest.SelectedItem) == "Báo cáo tổng hợp thanh toán A4"
                    && dlg.RadioPreviewForTest.Checked;
                Check("In P1 Doanh thu (UI hộp CHỌN MẪU IN): hiện đủ 4 mẫu, chọn sẵn Báo cáo tổng hợp thanh toán A4 và Xem trên màn hình",
                    ok, "selected=" + Convert.ToString(dlg.TemplateListForTest.SelectedItem));
                dlg.Close();
            }
        }

        private static void TestRevenuePrintEndToEnd(string db, List<PrintTemplateRecord> templates)
        {
            string storeId = DbHelper.NewId();
            string userId = DbHelper.NewId();
            string tableId = DbHelper.NewId();
            string custId = DbHelper.NewId();
            string bill1Id = DbHelper.NewId();
            string bill2Id = DbHelper.NewId();
            DateTime fromDate = new DateTime(2003, 9, 15);
            DateTime toDate = new DateTime(2003, 9, 16);
            string tempDir = Path.Combine(Path.GetTempPath(), "kazuko-rev-print-" + Guid.NewGuid().ToString("N"));
            Dictionary<string, ConfigValue> savedConfigs = CaptureRevenuePrintConfigs(db);

            try
            {
                Directory.CreateDirectory(tempDir);
                SetRevenuePrintConfigs(db, false, false, false, false);
                templates = new PrintTemplateRepository(db).LoadTemplatesForForm(PrintConstants.RevenueReportFormId);
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", storeId, "@N", "REVPRN-CH1");
                BookingExec(db, "INSERT INTO SUSER (ID, NAME, USERNAME, ISADMIN, STATUS, USERCREATEDID) VALUES (@I, @N, @U, 0, 30, 'selftest')",
                    "@I", userId, "@N", "REVPRN-Thu ngân Hằng", "@U", "revprn_user");
                BookingExec(db, "INSERT INTO DBAN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", tableId, "@N", "REVPRN-Bàn 1");
                BookingExec(db, "INSERT INTO DKHACHHANG (ID, NAME, MAKHACH, DIACHI, STATUS, USERCREATEDID) VALUES (@I, @N, @M, @DC, 30, 'selftest')",
                    "@I", custId, "@N", "REVPRN-Nguyễn Văn Ánh", "@M", "REVPRN-KH1", "@DC", "12 Trần Hưng Đạo");

                // B1: TIENHANGCHUAGIAM 110000, GIAMGIAMATHANG 10000, TIENGIAMGIA 10000, TONGCONG 90000, TIENMAT 50000, CHUYENKHOAN 40000.
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, BATDAU, KETTHUC, GIOTHANHTOAN, " +
                    "DCUAHANGID, DBANID, DKHACHHANGID, USERTHANHTOANID, " +
                    "TIENHANGCHUAGIAM, TIENGIO, TIENGIAMGIA, GIAMGIAMATHANG, TIENGIAMGIAGIO, PHIDICHVU, TIENTHUE, TONGCONG, " +
                    "TIENMAT, VOUCHER, THE, CHUYENKHOAN, TRUTICHLUY, THETRATRUOC, DATTRUOC, CONGNO, USERCREATEDID) " +
                    "VALUES (@ID, 'REVPRN-B1', 'Hóa đơn thử 1', 0, 30, 30, @NGAY, @BD, @KT, @GTT, @CH, @BAN, @KH, @UTT, " +
                    "110000, 0, 10000, 10000, 0, 0, 0, 90000, 50000, 0, 0, 40000, 0, 0, 0, 0, 'selftest')",
                    "@ID", bill1Id,
                    "@NGAY", new DateTime(2003, 9, 15, 10, 0, 0),
                    "@BD", new DateTime(2003, 9, 15, 9, 45, 0),
                    "@KT", new DateTime(2003, 9, 15, 10, 25, 0),
                    "@GTT", new DateTime(2003, 9, 15, 10, 30, 0),
                    "@CH", storeId, "@BAN", tableId, "@KH", custId, "@UTT", userId);

                // B2: TIENHANGCHUAGIAM 80000, GIAMGIAMATHANG 0, TIENGIAMGIA 5000, TONGCONG 75000,
                // THE 20000, VOUCHER 10000, THETRATRUOC 7000, TRUTICHLUY 3000, DATTRUOC 10000, CONGNO 25000. BATDAU/KETTHUC = NULL.
                BookingExec(db,
                    "INSERT INTO TDONHANG (ID, NAME, NOTE, LOAI, DATHANHTOAN, STATUS, NGAY, BATDAU, KETTHUC, GIOTHANHTOAN, " +
                    "DCUAHANGID, DBANID, DKHACHHANGID, USERTHANHTOANID, " +
                    "TIENHANGCHUAGIAM, TIENGIO, TIENGIAMGIA, GIAMGIAMATHANG, TIENGIAMGIAGIO, PHIDICHVU, TIENTHUE, TONGCONG, " +
                    "TIENMAT, VOUCHER, THE, CHUYENKHOAN, TRUTICHLUY, THETRATRUOC, DATTRUOC, CONGNO, USERCREATEDID) " +
                    "VALUES (@ID, 'REVPRN-B2', 'Hóa đơn thử 2', 0, 30, 30, @NGAY, NULL, NULL, @GTT, @CH, @BAN, @KH, @UTT, " +
                    "80000, 0, 5000, 0, 0, 0, 0, 75000, 0, 10000, 20000, 0, 3000, 7000, 10000, 25000, 'selftest')",
                    "@ID", bill2Id,
                    "@NGAY", new DateTime(2003, 9, 16, 14, 0, 0),
                    "@GTT", new DateTime(2003, 9, 16, 14, 20, 0),
                    "@CH", storeId, "@BAN", tableId, "@KH", custId, "@UTT", userId);

                var adminSession = new UserSession
                {
                    UserId = "selftest_rev_print_admin",
                    UserName = "admin",
                    DisplayName = "Admin",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                using (var host = new Form { StartPosition = FormStartPosition.Manual, ShowInTaskbar = false, Location = new Point(-20000, -20000), ClientSize = new Size(1360, 760) })
                using (var view = new RevenueReportView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true })
                {
                    host.Controls.Add(view);
                    host.Show();
                    host.PerformLayout();
                    Application.DoEvents();

                    view.FromDateBoxForTest.Inner.Text = "15/09/2003";
                    view.FromDateBoxForTest.Commit();
                    view.ToDateBoxForTest.Inner.Text = "16/09/2003";
                    view.ToDateBoxForTest.Commit();
                    SelectStoreInCombo(view.StoreComboForTest, storeId);
                    Application.DoEvents();

                    using (PrintJobData job = view.BuildPrintJobDataForTest())
                    {
                        VerifyRevenueJobData(job, view, fromDate, toDate);
                        VerifyAllFourRevenueTemplates(templates, job, fromDate, toDate, tempDir);
                        TestRevenueVisibleByConfigCases(db, templates, job);
                    }

                    SetRevenuePrintConfigs(db, false, false, false, false);
                    VerifyRevenueViewPrintEvents(view);
                    host.Close();
                }
            }
            finally
            {
                try { RestoreRevenuePrintConfigs(db, savedConfigs); } catch { }
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                try { BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", bill1Id); } catch { }
                try { BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", bill2Id); } catch { }
                try { BookingExec(db, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", custId); } catch { }
                try { BookingExec(db, "DELETE FROM DBAN WHERE ID = @I", "@I", tableId); } catch { }
                try { BookingExec(db, "DELETE FROM SUSER WHERE ID = @I", "@I", userId); } catch { }
                try { BookingExec(db, "DELETE FROM DCUAHANG WHERE ID = @I", "@I", storeId); } catch { }
            }
        }

        private static void VerifyRevenueJobData(PrintJobData job, RevenueReportView view, DateTime fromDate, DateTime toDate)
        {
            DataTable t0 = job.DataSet != null ? job.DataSet.Tables["Table0"] : null;
            bool schemaOk = job.DataSet != null && job.DataSet.DataSetName == "Data" && t0 != null
                && t0.Columns.Count == 47
                && t0.Columns["NGAY"].DataType == typeof(DateTime)
                && t0.Columns["GIOTHANHTOAN"].DataType == typeof(DateTime)
                && t0.Columns["BATDAU"].DataType == typeof(DateTime)
                && t0.Columns["KETTHUC"].DataType == typeof(DateTime)
                && t0.Columns["SOKHACH"].DataType == typeof(int)
                && t0.Columns["DATTRUOC"].DataType == typeof(decimal);

            bool rowsOk = t0 != null && t0.Rows.Count == 2
                && Convert.ToString(t0.Rows[0]["NAME"]) == "REVPRN-B1"
                && Convert.ToString(t0.Rows[0]["DBAN_NAME"]) == "REVPRN-Bàn 1"
                && Convert.ToString(t0.Rows[0]["DKHACHHANG_NAME"]) == "REVPRN-Nguyễn Văn Ánh"
                && Convert.ToString(t0.Rows[0]["SUSER_NAME"]) == "REVPRN-Thu ngân Hằng"
                && t0.Rows[0]["BATDAU"] is DateTime && t0.Rows[0]["KETTHUC"] is DateTime && t0.Rows[0]["GIOTHANHTOAN"] is DateTime
                && Convert.ToDecimal(t0.Rows[0]["TIENHANGCHUAGIAM"]) == 110000m
                && Convert.ToDecimal(t0.Rows[0]["GIAMGIAMATHANG"]) == 10000m
                && Convert.ToDecimal(t0.Rows[0]["TIENGIAMGIA"]) == 10000m
                && Convert.ToDecimal(t0.Rows[0]["TONGCONG"]) == 90000m
                && Convert.ToDecimal(t0.Rows[0]["TIENMAT"]) == 50000m
                && Convert.ToDecimal(t0.Rows[0]["CHUYENKHOAN"]) == 40000m
                && Convert.ToDecimal(t0.Rows[0]["DATTRUOC"]) == 0m
                && Convert.ToString(t0.Rows[1]["NAME"]) == "REVPRN-B2"
                && t0.Rows[1]["BATDAU"] == DBNull.Value && t0.Rows[1]["KETTHUC"] == DBNull.Value
                && t0.Rows[1]["GIOTHANHTOAN"] is DateTime
                && Convert.ToDecimal(t0.Rows[1]["DATTRUOC"]) == 10000m
                && Convert.ToDecimal(t0.Rows[1]["THE"]) == 20000m
                && Convert.ToDecimal(t0.Rows[1]["VOUCHER"]) == 10000m
                && Convert.ToDecimal(t0.Rows[1]["THETRATRUOC"]) == 7000m
                && Convert.ToDecimal(t0.Rows[1]["TRUTICHLUY"]) == 3000m
                && Convert.ToDecimal(t0.Rows[1]["CONGNO"]) == 25000m;

            bool paramsOk = job.Parameters.Count == 6
                && (DateTime)job.Parameters["TuNgay"] == fromDate
                && (DateTime)job.Parameters["DenNgay"] == toDate
                && job.Parameters.ContainsKey(PrintConstants.ConfigCompanyName)
                && job.Parameters.ContainsKey(PrintConstants.ConfigCompanyAddress)
                && job.Parameters.ContainsKey(PrintConstants.ConfigCompanyPhone)
                && job.Parameters.ContainsKey(PrintConstants.ConfigCompanyEmail);

            // Gõ ngày mới vào ô lọc nhưng CHƯA tải lại -> TuNgay/DenNgay vẫn giữ ngày đã tải.
            view.FromDateBoxForTest.Inner.Text = "01/01/2000";
            bool datesLockedOk;
            using (PrintJobData jobAfterEdit = view.BuildPrintJobDataForTest())
            {
                datesLockedOk = (DateTime)jobAfterEdit.Parameters["TuNgay"] == fromDate
                    && (DateTime)jobAfterEdit.Parameters["DenNgay"] == toDate;
            }
            view.FromDateBoxForTest.Inner.Text = "15/09/2003";

            Check("In P1 Doanh thu (DataSet Table0 & tham số): đủ 47 cột đúng kiểu, DATTRUOC thật, giờ trống = DBNull, giữ ngày đã tải, không tham số tổng",
                schemaOk && rowsOk && paramsOk && datesLockedOk,
                "schema=" + schemaOk + ", rows=" + rowsOk + ", params=" + paramsOk + ", locked=" + datesLockedOk);
        }

        private static void VerifyAllFourRevenueTemplates(
            List<PrintTemplateRecord> templates,
            PrintJobData job,
            DateTime fromDate,
            DateTime toDate,
            string tempDir)
        {
            using (PrintJobData emptyJob = RevenueReportPrintDataBuilder.Build(new List<RevenueBillRow>(), fromDate, toDate, new CompanyPrintInfo()))
            {
                foreach (PrintTemplateRecord tpl in templates)
                {
                    Report rpt;
                    string err;
                    bool okNonEmpty = FastReportEngine.TryPrepare(tpl, job, out rpt, out err);
                    int pages = okNonEmpty && rpt != null && rpt.PreparedPages != null ? rpt.PreparedPages.Count : 0;

                    Report rptEmpty;
                    string errEmpty;
                    bool okEmpty = FastReportEngine.TryPrepare(tpl, emptyJob, out rptEmpty, out errEmpty);
                    int pagesEmpty = okEmpty && rptEmpty != null && rptEmpty.PreparedPages != null ? rptEmpty.PreparedPages.Count : 0;
                    if (rptEmpty != null)
                        rptEmpty.Dispose();

                    Check("In P1 Doanh thu (T1 mẫu " + tpl.Name + "): dựng được với 2 bill và 0 bill, số trang > 0",
                        okNonEmpty && pages > 0 && okEmpty && pagesEmpty > 0,
                        "pages=" + pages + " (" + err + "), empty=" + pagesEmpty + " (" + errEmpty + ")");

                    if (!okNonEmpty || rpt == null)
                        continue;

                    using (rpt)
                    {
                        List<TemplateObjInfo> objMap = ParseTemplateObjectMap(tpl.TemplateXml);
                        List<KeyValuePair<string, string>> pageTexts = FastReportEngine.ExtractPreparedPageTexts(rpt);
                        List<List<KeyValuePair<string, string>>> visualRows = ExtractPreparedRows(rpt);

                        bool headerDatesOk = VerifyHeaderDates(objMap, pageTexts, fromDate, toDate);
                        Check("In P1 Doanh thu (tiêu đề ngày mẫu " + tpl.Name + "): tiêu đề có đúng hai ngày đã tải",
                            headerDatesOk, DumpMatchingTexts(pageTexts, "2003"));

                        string pdfPath = Path.Combine(tempDir, MakeSafeFileName(tpl.Name) + ".pdf");
                        FastReportEngine.ExportPdf(rpt, pdfPath);
                        bool hasVi = PageTextsContain(pageTexts, "BÁO CÁO") || PageTextsContain(pageTexts, "Báo cáo")
                            || PageTextsContain(pageTexts, "TỔNG") || PageTextsContain(pageTexts, "Tổng")
                            || PageTextsContain(pageTexts, "REVPRN-Bàn 1") || PageTextsContain(pageTexts, "REVPRN-Nguyễn Văn Ánh");
                        Check("In P1 Doanh thu (T7 mẫu " + tpl.Name + "): tiếng Việt trên trang dựng và xuất PDF hợp lệ",
                            hasVi && IsValidPdfFile(pdfPath), "hasVi=" + hasVi + ", pdf=" + File.Exists(pdfPath));

                        if (tpl.Name == "Báo cáo tổng hợp thanh toán A4")
                        {
                            string xlsxPath = Path.Combine(tempDir, "ThanhToanA4.xlsx");
                            FastReportEngine.ExportExcel(rpt, xlsxPath);
                            VerifyPaymentA4CellsAndExcel(objMap, pageTexts, visualRows, xlsxPath);
                        }
                        else if (tpl.Name == "Báo cáo tổng hợp bán hàng A4")
                        {
                            VerifySalesA4Cells(objMap, pageTexts, visualRows);
                        }
                        else
                        {
                            VerifyThermalCells(tpl.Name, objMap, pageTexts);
                        }
                    }
                }
            }
        }

        private static void VerifyPaymentA4CellsAndExcel(
            List<TemplateObjInfo> objMap,
            List<KeyValuePair<string, string>> pageTexts,
            List<List<KeyValuePair<string, string>>> visualRows,
            string xlsxPath)
        {
            string datTruocDataObj = FindObjName(objMap, "Data", "DATTRUOC", null);
            List<string> datTruocDataVals = FindAllObjectTexts(pageTexts, datTruocDataObj);
            decimal b2DatTruoc = 0m;
            bool b2DatTruocOk = !string.IsNullOrEmpty(datTruocDataObj) && datTruocDataVals.Count == 2
                && TryParseCellNumber(datTruocDataVals[1], out b2DatTruoc) && b2DatTruoc == 10000m;

            string[] sumFields = { "TONGCONG", "TIENMAT", "THE", "CHUYENKHOAN", "VOUCHER", "DATTRUOC", "THETRATRUOC", "CONGNO" };
            decimal[] expected = { 165000m, 50000m, 20000m, 40000m, 10000m, 10000m, 7000m, 25000m };

            bool byNameOk = true;
            string firstSumObj = null;
            var detailParts = new List<string>();
            for (int i = 0; i < sumFields.Length; i++)
            {
                string objName = FindObjName(objMap, "Summary", sumFields[i], null);
                if (i == 0)
                    firstSumObj = objName;
                string raw = string.IsNullOrEmpty(objName) ? "" : FindLastObjectText(pageTexts, objName);
                decimal parsed = 0m;
                bool cellOk = !string.IsNullOrEmpty(objName) && TryParseCellNumber(raw, out parsed) && parsed == expected[i];
                if (!cellOk)
                    byNameOk = false;
                detailParts.Add(sumFields[i] + "(" + objName + ")=" + raw);
            }

            List<decimal> rowNums = ExtractRowNumbers(FindTotalVisualRow(visualRows, firstSumObj));
            bool byOrderOk = SequenceEquals(rowNums, expected);

            Check("In P1 Doanh thu (T3 Thanh toán A4 đúng ô & thứ tự): B2 đặt trước 10,000 và dòng tổng 165k/50k/20k/40k/10k/10k/7k/25k",
                b2DatTruocOk && byNameOk && byOrderOk,
                "b2DatTruoc(" + datTruocDataObj + ")=" + (datTruocDataVals.Count > 1 ? datTruocDataVals[1] : "none")
                + ", byName=" + byNameOk + " [" + string.Join("; ", detailParts.ToArray()) + "], rowOrder=" + FormatDecimals(rowNums));

            List<List<ExcelCellVal>> xlRows = ReadExcelAllRows(xlsxPath);
            List<ExcelCellVal> xlB1 = FindRowContaining(xlRows, "REVPRN-B1");
            List<ExcelCellVal> xlB2 = FindRowContaining(xlRows, "REVPRN-B2");
            List<ExcelCellVal> xlTotal = FindExcelNumericRowIgnoreCase(xlRows, "TỔNG");
            List<decimal> xlTotalNums = ExtractExcelRowNumbers(xlTotal);

            bool xlOk = xlB1 != null && RowHasNumber(xlB1, 90000m) && RowHasNumber(xlB1, 50000m) && RowHasNumber(xlB1, 40000m)
                && xlB2 != null && RowHasNumber(xlB2, 75000m) && RowHasNumber(xlB2, 20000m) && RowHasNumber(xlB2, 10000m) && RowHasNumber(xlB2, 25000m)
                && SequenceEquals(xlTotalNums, expected);
            Check("In P1 Doanh thu (T2 xuất Excel Thanh toán A4): đọc lại .xlsx khớp B1, B2 và đúng thứ tự 8 ô dòng tổng",
                xlOk, "total=" + FormatExcelRow(xlTotal));
        }

        private static void VerifySalesA4Cells(
            List<TemplateObjInfo> objMap,
            List<KeyValuePair<string, string>> pageTexts,
            List<List<KeyValuePair<string, string>>> visualRows)
        {
            string giamRowObj = FindObjName(objMap, "Data", "TIENGIAMGIA", "GIAMGIAMATHANG");
            List<string> giamRowVals = FindAllObjectTexts(pageTexts, giamRowObj);
            decimal g1 = 0m, g2 = 0m;
            bool rowsOk = !string.IsNullOrEmpty(giamRowObj) && giamRowVals.Count == 2
                && TryParseCellNumber(giamRowVals[0], out g1) && g1 == 10000m
                && TryParseCellNumber(giamRowVals[1], out g2) && g2 == 5000m;

            string thSumObj = FindObjName(objMap, "Summary", "TIENHANGCHUAGIAM", null);
            string ggSumObj = FindObjNameBoth(objMap, "Summary", "TIENGIAMGIA", "GIAMGIAMATHANG");
            string tcSumObj = FindObjName(objMap, "Summary", "TONGCONG", null);

            string rawTh = FindLastObjectText(pageTexts, thSumObj);
            string rawGg = FindLastObjectText(pageTexts, ggSumObj);
            string rawTc = FindLastObjectText(pageTexts, tcSumObj);
            decimal vTh = 0m, vGg = 0m, vTc = 0m;
            bool sumsByNameOk = !string.IsNullOrEmpty(thSumObj) && !string.IsNullOrEmpty(ggSumObj) && !string.IsNullOrEmpty(tcSumObj)
                && TryParseCellNumber(rawTh, out vTh) && vTh == 190000m
                && TryParseCellNumber(rawGg, out vGg) && vGg == 25000m
                && TryParseCellNumber(rawTc, out vTc) && vTc == 165000m;

            List<decimal> nonZeroOrder = ExtractNonZeroNumbers(ExtractRowNumbers(FindTotalVisualRow(visualRows, tcSumObj)));
            bool orderOk = SequenceEquals(nonZeroOrder, new[] { 190000m, 25000m, 165000m });

            Check("In P1 Doanh thu (T3 Bán hàng A4 đúng ô): giảm hàng 2 dòng 10,000 & 5,000; dòng tổng tiền hàng 190,000 / giảm hàng 25,000 / tổng 165,000",
                rowsOk && sumsByNameOk && orderOk,
                "giamRows(" + giamRowObj + ")=" + string.Join("|", giamRowVals.ToArray())
                + ", th(" + thSumObj + ")=" + rawTh + ", gg(" + ggSumObj + ")=" + rawGg + ", tc(" + tcSumObj + ")=" + rawTc
                + ", nonZeroOrder=" + FormatDecimals(nonZeroOrder));
        }

        private static void VerifyThermalCells(
            string tplName,
            List<TemplateObjInfo> objMap,
            List<KeyValuePair<string, string>> pageTexts)
        {
            string rowObj = FindObjName(objMap, "Data", "TONGCONG", null);
            List<string> rowVals = FindAllObjectTexts(pageTexts, rowObj);
            decimal r1 = 0m, r2 = 0m;
            bool rowsOk = !string.IsNullOrEmpty(rowObj) && rowVals.Count == 2
                && TryParseCellNumber(rowVals[0], out r1) && r1 == 90000m
                && TryParseCellNumber(rowVals[1], out r2) && r2 == 75000m;

            string sumObj = FindObjName(objMap, "Summary", "TONGCONG", null);
            string rawSum = FindLastObjectText(pageTexts, sumObj);
            decimal vSum = 0m;
            bool sumOk = !string.IsNullOrEmpty(sumObj) && TryParseCellNumber(rawSum, out vSum) && vSum == 165000m;

            Check("In P1 Doanh thu (T3 " + tplName + " đúng ô): 2 dòng 90,000 & 75,000 và tổng 165,000",
                rowsOk && sumOk,
                "rows(" + rowObj + ")=" + string.Join("|", rowVals.ToArray()) + ", sum(" + sumObj + ")=" + rawSum);
        }

        private static void VerifyRevenueViewPrintEvents(RevenueReportView view)
        {
            var recPreview = new PrintOutputRecorder();
            int pickerCount = 0;
            int pickerTemplates = 0;
            view.PrintOutputHandlerForTest = recPreview;
            view.PrintTemplatePickerForTest = delegate (IWin32Window owner, IList<PrintTemplateRecord> list)
            {
                pickerCount++;
                pickerTemplates = list == null ? 0 : list.Count;
                return new PrintSelectionResult { Template = list[list.Count - 1], Action = PrintActionKind.Preview };
            };
            view.PrintButtonForTest.PerformClick();
            bool previewOk = pickerCount == 1 && pickerTemplates == 4
                && recPreview.PreviewCount == 1 && recPreview.PrintCount == 0
                && recPreview.LastTemplate != null && recPreview.LastTemplate.Name == "Báo cáo tổng hợp thanh toán A4"
                && recPreview.LastPreparedPageCount > 0;

            var recCancel = new PrintOutputRecorder();
            view.PrintOutputHandlerForTest = recCancel;
            view.PrintTemplatePickerForTest = delegate { return null; };
            view.PrintButtonForTest.PerformClick();
            bool cancelOk = recCancel.PreviewCount == 0 && recCancel.PrintCount == 0
                && recCancel.ExcelCount == 0 && recCancel.PdfCount == 0;

            var recBusy = new PrintOutputRecorder();
            int busyCalls = 0;
            bool disabledWhileBusy = false;
            bool reentrantBlocked = false;
            view.PrintOutputHandlerForTest = recBusy;
            view.PrintTemplatePickerForTest = delegate (IWin32Window owner, IList<PrintTemplateRecord> list)
            {
                busyCalls++;
                disabledWhileBusy = !view.PrintButtonForTest.Enabled;
                reentrantBlocked = !view.PrintReport();
                return new PrintSelectionResult
                {
                    Template = list[1],
                    Action = PrintActionKind.Print,
                    PrinterName = "Máy in nhiệt giả 80mm"
                };
            };
            view.PrintButtonForTest.PerformClick();
            bool busyOk = busyCalls == 1 && disabledWhileBusy && reentrantBlocked && view.PrintButtonForTest.Enabled
                && recBusy.PrintCount == 1 && recBusy.LastTemplate != null && recBusy.LastTemplate.Name == "80mm"
                && recBusy.LastPrinterName == "Máy in nhiệt giả 80mm";

            Check("In P1 Doanh thu (UI sự kiện thật): PerformClick chọn mẫu, hủy trả null không in, và khóa nút chống bấm chồng",
                previewOk && cancelOk && busyOk,
                "preview=" + previewOk + ", cancel=" + cancelOk + ", busy=" + busyOk);
        }

        private static void TestRevenuePrintT6Errors(string db)
        {
            using (PrintJobData job = RevenueReportPrintDataBuilder.Build(
                new List<RevenueBillRow>(), DateTime.Today, DateTime.Today, new CompanyPrintInfo()))
            {
                var recCorrupt = new PrintOutputRecorder();
                bool okCorrupt = PrintWorkflowService.Execute(
                    null,
                    new PrintSelectionResult
                    {
                        Template = new PrintTemplateRecord { Id = "bad", Name = "Mẫu DT hỏng", TemplateXml = "<Report><Loi" },
                        Action = PrintActionKind.Preview
                    },
                    job,
                    recCorrupt);

                var recNoTpl = new PrintOutputRecorder();
                string recordedMsg = null;
                var coord = new FormPrintCoordinator();
                bool okNoTpl = coord.Print(
                    null,
                    null,
                    db,
                    "00000000-0000-0000-0000-000000000000",
                    delegate (CompanyPrintInfo c) { return RevenueReportPrintDataBuilder.Build(new List<RevenueBillRow>(), DateTime.Today, DateTime.Today, c); },
                    recNoTpl,
                    null,
                    delegate (string m) { recordedMsg = m; });

                Check("In P1 Doanh thu (T6 mẫu hỏng hoặc không có mẫu): báo lỗi rõ ràng, không văng app",
                    !okCorrupt && recCorrupt.ErrorMessages.Count == 1
                    && recCorrupt.ErrorMessages[0].IndexOf("Mẫu DT hỏng", StringComparison.Ordinal) >= 0
                    && !okNoTpl && recNoTpl.InfoMessages.Count == 1
                    && recordedMsg == PrintConstants.NoTemplateFoundMessage,
                    "corrupt=" + (recCorrupt.ErrorMessages.Count > 0 ? recCorrupt.ErrorMessages[0] : "") + ", noTpl=" + recordedMsg);
            }
        }
    }
}
