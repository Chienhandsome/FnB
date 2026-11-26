using System;
using System.Collections.Generic;
using System.Data;
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
        private static void TestBusinessResultEmbeddedEndToEnd(string db)
        {
            string storeAId = DbHelper.NewId();
            string storeBId = DbHelper.NewId();
            string grpCoffeeId = DbHelper.NewId();
            string grpTeaId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prodCoffeeId = DbHelper.NewId();
            string prodTeaId = DbHelper.NewId();
            var orderIds = new List<string>();
            var lineIds = new List<string>();
            var voucherIds = new List<string>();
            string tempDir = Path.Combine(Path.GetTempPath(), "kazuko-emb-kqkd-" + Guid.NewGuid().ToString("N"));

            string fix1Id = null, fix1Name = null;
            string fix2Id = null, fix2Name = null;
            string fix3Id = null, fix3Name = null;
            string fixZeroName = null;
            string oth1Id = null, oth1Name = null;
            string othZeroName = null;

            try
            {
                Directory.CreateDirectory(tempDir);
                using (var conn = KazukoConnection.Open(db))
                {
                    DataTable dtFix = DbHelper.Table(conn, null, "SELECT ID, NAME FROM DLYDOTHUCHI WHERE LOAILYDO = 1");
                    if (dtFix.Rows.Count > 0) { fix1Id = DbHelper.Str(dtFix.Rows[0]["ID"]); fix1Name = DbHelper.Str(dtFix.Rows[0]["NAME"]); }
                    if (dtFix.Rows.Count > 1) { fix2Id = DbHelper.Str(dtFix.Rows[1]["ID"]); fix2Name = DbHelper.Str(dtFix.Rows[1]["NAME"]); }
                    if (dtFix.Rows.Count > 2) { fix3Id = DbHelper.Str(dtFix.Rows[2]["ID"]); fix3Name = DbHelper.Str(dtFix.Rows[2]["NAME"]); }
                    if (dtFix.Rows.Count > 3) { fixZeroName = DbHelper.Str(dtFix.Rows[3]["NAME"]); }

                    DataTable dtOth = DbHelper.Table(conn, null, "SELECT ID, NAME FROM DLYDOTHUCHI WHERE LOAILYDO = 2");
                    if (dtOth.Rows.Count > 0) { oth1Id = DbHelper.Str(dtOth.Rows[0]["ID"]); oth1Name = DbHelper.Str(dtOth.Rows[0]["NAME"]); }
                    if (dtOth.Rows.Count > 1) { othZeroName = DbHelper.Str(dtOth.Rows[1]["NAME"]); }
                }

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", storeAId, "@N", "EMBKQ-CH-A");
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest')", "@I", storeBId, "@N", "EMBKQ-CH-B");
                BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '1', '', '0001', 30, 'selftest')", "@I", grpCoffeeId, "@N", "EMBKQ-01-Cà phê");
                BookingExec(db, "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, SORTORDER, STATUS, USERCREATEDID) VALUES (@I, @N, '', '1', '', '0002', 30, 'selftest')", "@I", grpTeaId, "@N", "EMBKQ-02-Trà");
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, 'EMBKQ-Ly', 30, 'selftest')", "@I", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'EMBKQ-CF', 'Cà phê', @G, @U, '0', 10000, 90000, 30, 'selftest')", "@I", prodCoffeeId, "@G", grpCoffeeId, "@U", unitId);
                BookingExec(db, "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIAVON, GIABAN, STATUS, USERCREATEDID) VALUES (@I, 'EMBKQ-TR', 'Trà', @G, @U, '0', 0, 50000, 30, 'selftest')", "@I", prodTeaId, "@G", grpTeaId, "@U", unitId);

                Func<string, DateTime, string, decimal, decimal, string> addBill =
                    delegate (string name, DateTime ngay, string storeId, decimal tc, decimal gg)
                    {
                        string id = DbHelper.NewId();
                        orderIds.Add(id);
                        BookingExec(db,
                            "INSERT INTO TDONHANG (ID, NAME, LOAI, DATHANHTOAN, STATUS, NGAY, DCUAHANGID, TONGCONG, TIENGIAMGIATONG, PHIDICHVU, TIENTHUE, USERCREATEDID) " +
                            "VALUES (@ID, @N, 0, 30, 30, @NG, @CH, @TC, @GG, 0, 0, 'selftest')",
                            "@ID", id, "@N", name, "@NG", ngay, "@CH", storeId, "@TC", tc, "@GG", gg);
                        return id;
                    };

                Action<string, string, decimal, decimal, decimal> addLine =
                    delegate (string bid, string pid, decimal tt, decimal slx, decimal gv)
                    {
                        string lid = DbHelper.NewId();
                        lineIds.Add(lid);
                        BookingExec(db,
                            "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, DDONVITINHID, THANHTIEN, SLXUAT, SLNHAP, GIAVON, XUATVATTU, COMBOPARENTID, STATUS, USERCREATEDID) " +
                            "VALUES (@ID, @B, @P, 'EMBKQ-Line', @U, @TT, @SX, 0, @GV, 0, '', 30, 'selftest')",
                            "@ID", lid, "@B", bid, "@P", pid, "@U", unitId, "@TT", tt, "@SX", slx, "@GV", gv);
                    };

                Action<string, DateTime, string, string, decimal> addChi =
                    delegate (string name, DateTime ngay, string storeId, string reasonId, decimal chi)
                    {
                        if (string.IsNullOrEmpty(reasonId))
                            return;
                        string vid = DbHelper.NewId();
                        voucherIds.Add(vid);
                        BookingExec(db,
                            "INSERT INTO TTHUCHI (ID, NAME, NGAY, TIMECREATED, CHI, THU, DCUAHANGID, DLYDOTHUCHIID, STATUS, LATAMUNG, USERCREATEDID) " +
                            "VALUES (@ID, @N, @NG, @NG, @CHI, 0, @CH, @LD, 30, 0, 'selftest')",
                            "@ID", vid, "@N", name, "@NG", ngay, "@CHI", chi, "@CH", storeId, "@LD", reasonId);
                    };

                // Kỳ này (01/05–31/05/2006) Store A:
                // Cà phê 900k / Trà 100k / Giảm giá 100k (N_tong = 1.100k -> 82% / 9% / 9%); DT = 1.000k (tăng +500k -> "B").
                // Định phí: Fix1 = 15k (1.5% DT -> 2% ToEven), Fix2 = 25k (2.5% DT -> 2% ToEven), Fix3 = 260k (26%) => DP = 300k.
                // Biến phí: Cà phê = 100k; Chi khác: Oth1 = 100k => CP = 500k (tăng +300k -> "æ"); LÃI/LỖ = 500k (50% DT, 100% CP).
                string curA = addBill("EMBKQ-C-A1", new DateTime(2006, 5, 10, 10, 0, 0), storeAId, 1000000m, 100000m);
                addLine(curA, prodCoffeeId, 900000m, 10m, 10000m);
                addLine(curA, prodTeaId, 100000m, 0m, 0m);
                addChi("EMBKQ-F1-C", new DateTime(2006, 5, 11, 9, 0, 0), storeAId, fix1Id, 15000m);
                addChi("EMBKQ-F2-C", new DateTime(2006, 5, 11, 9, 5, 0), storeAId, fix2Id, 25000m);
                addChi("EMBKQ-F3-C", new DateTime(2006, 5, 11, 9, 10, 0), storeAId, fix3Id, 260000m);
                addChi("EMBKQ-O1-C", new DateTime(2006, 5, 12, 9, 0, 0), storeAId, oth1Id, 100000m);

                // Kỳ trước (01/04–30/04/2006) Store A: DT = 500k, DP = 130k, BP = 50k, CK = 20k => CP = 200k, LÃI/LỖ = 300k.
                string prevA = addBill("EMBKQ-P-A1", new DateTime(2006, 4, 15, 10, 0, 0), storeAId, 500000m, 0m);
                addLine(prevA, prodCoffeeId, 400000m, 5m, 10000m);
                addLine(prevA, prodTeaId, 100000m, 0m, 0m);
                addChi("EMBKQ-F1-P", new DateTime(2006, 4, 16, 9, 0, 0), storeAId, fix1Id, 10000m);
                addChi("EMBKQ-F2-P", new DateTime(2006, 4, 16, 9, 5, 0), storeAId, fix2Id, 20000m);
                addChi("EMBKQ-F3-P", new DateTime(2006, 4, 16, 9, 10, 0), storeAId, fix3Id, 100000m);
                addChi("EMBKQ-O1-P", new DateTime(2006, 4, 17, 9, 0, 0), storeAId, oth1Id, 20000m);

                // Kỳ này Store B (kiểm lọc CH + phím F5): DT = 200k, BP = 30k => LÃI/LỖ = 170k.
                string curB = addBill("EMBKQ-C-B1", new DateTime(2006, 5, 12, 11, 0, 0), storeBId, 200000m, 0m);
                addLine(curB, prodTeaId, 200000m, 2m, 15000m);

                var adminSession = new UserSession
                {
                    UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                    IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
                };

                using (var host = MakeOffscreenHost())
                {
                    var view = new BusinessResultView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        bool noPrintBtn = true;
                        foreach (Control c in view.TopBarForTest.Controls)
                            if (c.Text != null && c.Text.IndexOf("In báo cáo", StringComparison.OrdinalIgnoreCase) >= 0)
                                noPrintBtn = false;

                        // Gõ 01/05/2006..31/05/2006, chọn Store A, bấm Tải dữ liệu thật (PerformClick).
                        view.FromDateBoxForTest.Inner.Text = "01/05/2006";
                        view.FromDateBoxForTest.Commit();
                        view.ToDateBoxForTest.Inner.Text = "31/05/2006";
                        view.ToDateBoxForTest.Commit();
                        SelectStoreInCombo(view.StoreComboForTest, storeAId);
                        view.LoadButtonForTest.PerformClick();
                        Application.DoEvents();

                        List<KqkdPreparedRow> prepRows = ExtractKqkdPreparedRows(view.CurrentReportForTest);
                        List<BusinessResultRow> calcRows = view.LoadedRowsForTest;

                        // 1. Kiểm từng dòng trên trang khớp 1-1 với BusinessResultCalculator (giá trị + 3 cột % + không có Tiền giờ).
                        bool allMatchCalculator = prepRows.Count == calcRows.Count && prepRows.Count > 0;
                        string mismatchDetail = "";
                        for (int i = 0; allMatchCalculator && i < calcRows.Count; i++)
                        {
                            BusinessResultRow c = calcRows[i];
                            KqkdPreparedRow p = prepRows[i];
                            bool rowOk = p.Content == c.Content
                                && p.PctDt == c.PercentOfRevenue
                                && p.PctMain == c.PercentMain
                                && p.PctThird == c.PercentThird
                                && CheckPreparedDecimalEquals(p.CurrentText, c.CurrentValue)
                                && CheckPreparedDecimalEquals(p.ChangeText, c.ChangeAmount)
                                && CheckPreparedDecimalEquals(p.PrevText, c.PreviousValue);
                            if (!rowOk)
                            {
                                allMatchCalculator = false;
                                mismatchDetail = "row[" + i + "] " + c.Content + " calc(" + c.PercentOfRevenue + "|" + c.PercentMain + "|" + c.PercentThird + ") vs page(" + p.PctDt + "|" + p.PctMain + "|" + p.PctThird + ")";
                            }
                        }

                        // 2. Kiểm các mốc cụ thể: 82%/9%/9%, 100% đúng ô, ToEven (1.5%→2%, 2.5%→2%), "-" khi 0, đủ 3 cột % định phí, LÃI/LỖ, Wingdings B & æ.
                        KqkdPreparedRow rDt = FindKqkdRowObj(prepRows, "DOANH THU", 0);
                        KqkdPreparedRow rCf = FindKqkdRowObj(prepRows, "EMBKQ-01-Cà phê", 0);
                        KqkdPreparedRow rTr = FindKqkdRowObj(prepRows, "EMBKQ-02-Trà", 0);
                        KqkdPreparedRow rGg = FindKqkdRowObj(prepRows, "Giảm giá", 0);
                        KqkdPreparedRow rCp = FindKqkdRowObj(prepRows, "CHI PHÍ", 0);
                        KqkdPreparedRow rDp = FindKqkdRowObj(prepRows, "Định phí", 0);
                        KqkdPreparedRow rFix1 = FindKqkdRowObj(prepRows, fix1Name, 0);
                        KqkdPreparedRow rFix2 = FindKqkdRowObj(prepRows, fix2Name, 0);
                        KqkdPreparedRow rFix0 = string.IsNullOrEmpty(fixZeroName) ? null : FindKqkdRowObj(prepRows, fixZeroName, 0);
                        KqkdPreparedRow rBp = FindKqkdRowObj(prepRows, "Biến phí", 0);
                        KqkdPreparedRow rCk = FindKqkdRowObj(prepRows, "Chi khác", 0);
                        KqkdPreparedRow rOth0 = string.IsNullOrEmpty(othZeroName) ? null : FindKqkdRowObj(prepRows, othZeroName, 0);
                        KqkdPreparedRow rPl = FindKqkdRowObj(prepRows, "LÃI/LỖ", 0);

                        bool specificPctOk = noPrintBtn
                            && rDt != null && rDt.PctMain == "100%"
                            && rCf != null && rCf.PctMain == "82%"
                            && rTr != null && rTr.PctMain == "9%"
                            && rGg != null && rGg.PctMain == "9%"
                            && rCp != null && rCp.PctDt == "50%" && rCp.PctThird == "100%"
                            && rDp != null && rDp.PctDt == "30%" && rDp.PctMain == "60%" && rDp.PctThird == "100%"
                            && rFix1 != null && rFix1.PctDt == "2%" && rFix1.PctMain == "3%" && rFix1.PctThird == "5%"
                            && rFix2 != null && rFix2.PctDt == "2%" && rFix2.PctMain == "5%" && rFix2.PctThird == "8%"
                            && (rFix0 == null || (rFix0.PctDt == "-" && rFix0.PctMain == "-" && rFix0.PctThird == "-"))
                            && rBp != null && rBp.PctThird == "100%"
                            && rCk != null && rCk.PctThird == "100%"
                            && (rOth0 == null || (rOth0.PctDt == "-" && rOth0.PctMain == "-" && rOth0.PctThird == "-"))
                            && rPl != null && rPl.PctMain == "50%" && rPl.PctThird == "100%"
                            && FindKqkdRow(prepRows, "Tiền giờ", 0) < 0;

                        Check("In P1b KQKD (T1/T3 cột % & khớp 1-1 calculator): 82%/9%/9%, ToEven 1.5%/2.5%→2%, 100%, '-', LÃI/LỖ và khớp từng dòng",
                            allMatchCalculator && specificPctOk,
                            mismatchDetail.Length > 0 ? mismatchDetail : ("rows=" + prepRows.Count + ", specific=" + specificPctOk));

                        // 3. Biểu tượng Wingdings: DOANH THU tăng có "B" (Wingdings), CHI PHÍ tăng có "æ" (Wingdings 3), bằng 0 trống.
                        bool wingdingsOk = rDt != null && rDt.SymbolText == "B"
                            && rDt.SymbolFontName.IndexOf("Wingdings", StringComparison.OrdinalIgnoreCase) >= 0
                            && rCp != null && rCp.SymbolText == "\u00E6"
                            && rCp.SymbolFontName.IndexOf("Wingdings", StringComparison.OrdinalIgnoreCase) >= 0
                            && rTr != null && rTr.SymbolText == "";
                        Check("In P1b KQKD (biểu tượng Wingdings trên trang): DOANH THU tăng='B' (Wingdings), CHI PHÍ tăng='æ' (Wingdings 3), bằng 0 trống",
                            wingdingsOk,
                            "dt=" + (rDt == null ? "null" : rDt.SymbolText + "/" + rDt.SymbolFontName)
                            + ", cp=" + (rCp == null ? "null" : rCp.SymbolText + "/" + rCp.SymbolFontName));

                        // 4. Chứng minh cờ RaiseStartReportWithData có tác dụng: tắt cờ thì các cột % do script tính bị trống!
                        VerifyKqkdFlagOffLeavesPercentEmpty(db, calcRows);

                        // 5. Xuất Excel KQKD đọc lại khớp DOANH THU, CHI PHÍ, LÃI/LỖ và %.
                        string xlsxPath = Path.Combine(tempDir, "KQKD.xlsx");
                        FastReportEngine.ExportExcel(view.CurrentReportForTest, xlsxPath);
                        List<List<ExcelCellVal>> xlRows = ReadExcelAllRows(xlsxPath);
                        List<ExcelCellVal> xlDt = FindRowContaining(xlRows, "DOANH THU");
                        List<ExcelCellVal> xlCp = FindRowContaining(xlRows, "CHI PHÍ");
                        List<ExcelCellVal> xlPl = FindRowContaining(xlRows, "LÃI/LỖ");
                        bool xlOk = xlDt != null && RowHasNumber(xlDt, 1000000m) && RowHasText(xlDt, "100%")
                            && xlCp != null && RowHasNumber(xlCp, 500000m) && RowHasText(xlCp, "50%")
                            && xlPl != null && RowHasNumber(xlPl, 500000m) && RowHasText(xlPl, "50%");
                        Check("In P1b KQKD (T2/T7 xuất Excel): đọc lại .xlsx khớp DOANH THU (1tr/100%), CHI PHÍ (500k/50%), LÃI/LỖ (500k/50%)",
                            xlOk, "dt=" + FormatExcelRow(xlDt) + ", cp=" + FormatExcelRow(xlCp) + ", pl=" + FormatExcelRow(xlPl));

                        // 6. Chuyển sang Store B và nhấn phím F5 thật → trang cập nhật sang Store B (LÃI/LỖ = 170.000).
                        SelectStoreInCombo(view.StoreComboForTest, storeBId);
                        int loadBeforeF5 = view.LoadCountForTest;
                        bool f5Handled = view.DispatchCmdKeyForTest(Keys.F5);
                        Application.DoEvents();
                        List<KqkdPreparedRow> storeBRows = ExtractKqkdPreparedRows(view.CurrentReportForTest);
                        KqkdPreparedRow plB = FindKqkdRowObj(storeBRows, "LÃI/LỖ", 0);
                        bool f5Ok = f5Handled && view.LoadCountForTest == loadBeforeF5 + 1
                            && plB != null && CheckPreparedDecimalEquals(plB.CurrentText, 170000m);
                        Check("In P1b KQKD (phím F5 lọc cửa hàng): chuyển Store B + F5 cập nhật trang LÃI/LỖ = 170,000",
                            f5Ok, plB == null ? "plB=null" : plB.CurrentText);
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                foreach (string lid in lineIds)
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE ID = @I", "@I", lid);
                foreach (string oid in orderIds)
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", oid);
                foreach (string vid in voucherIds)
                    BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", vid);
                BookingExec(db, "DELETE FROM DMATHANG WHERE ID IN (@A, @B)", "@A", prodCoffeeId, "@B", prodTeaId);
                BookingExec(db, "DELETE FROM DDONVITINH WHERE ID = @I", "@I", unitId);
                BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID IN (@A, @B)", "@A", grpCoffeeId, "@B", grpTeaId);
                BookingExec(db, "DELETE FROM DCUAHANG WHERE ID IN (@A, @B)", "@A", storeAId, "@B", storeBId);
            }
        }

        private static void VerifyKqkdFlagOffLeavesPercentEmpty(string db, List<BusinessResultRow> rows)
        {
            string tplErr;
            PrintTemplateRecord tpl = new PrintTemplateRepository(db).LoadReportTemplate(
                PrintConstants.BusinessResultReportId, BusinessResultConstants.FunctionName, out tplErr);
            bool ok = false;
            string detail = tplErr ?? "";
            if (tpl != null)
            {
                using (PrintJobData jobOff = BusinessResultReportDataBuilder.Build(
                    rows, new DateTime(2006, 5, 1), new DateTime(2006, 5, 31), new CompanyPrintInfo()))
                {
                    jobOff.RaiseStartReportWithData = false;
                    Report rptOff;
                    string errOff;
                    if (FastReportEngine.TryPrepare(tpl, jobOff, out rptOff, out errOff) && rptOff != null)
                    {
                        using (rptOff)
                        {
                            List<KqkdPreparedRow> offRows = ExtractKqkdPreparedRows(rptOff);
                            KqkdPreparedRow offCf = FindKqkdRowObj(offRows, "EMBKQ-01-Cà phê", 0);
                            KqkdPreparedRow offCp = FindKqkdRowObj(offRows, "CHI PHÍ", 0);
                            KqkdPreparedRow offPl = FindKqkdRowObj(offRows, "LÃI/LỖ", 0);
                            ok = offCf != null && offCf.PctMain == ""
                                && offCp != null && offCp.PctDt == ""
                                && offPl != null && offPl.PctMain == "" && offPl.PctThird == "";
                            detail = "cf=" + (offCf == null ? "null" : "'" + offCf.PctMain + "'")
                                + ", cpDt=" + (offCp == null ? "null" : "'" + offCp.PctDt + "'")
                                + ", pl=" + (offPl == null ? "null" : "'" + offPl.PctMain + "'/'" + offPl.PctThird + "'");
                        }
                    }
                    else
                    {
                        detail = errOff;
                    }
                }
            }
            Check("In P1b KQKD (chứng minh cờ RaiseStartReportWithData): tắt cờ thì các cột % do script tính trống",
                ok, detail);
        }

        private static KqkdPreparedRow FindKqkdRowObj(List<KqkdPreparedRow> rows, string content, int startIndex)
        {
            int idx = FindKqkdRow(rows, content, startIndex);
            return idx >= 0 ? rows[idx] : null;
        }
    }
}
