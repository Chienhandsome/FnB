using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP3aGridExcelTests(string db)
        {
            GridExcelCoreTypeAndDisplayIndexTests();
            GridExcelFilterSortEmptyTests();
            GridExcelDialogFlowAndErrorTests();
            GridExcelHiddenSecretColumnTest();
            PrintP3aGridExcelUiTests(db);
        }

        private static void GridExcelCoreTypeAndDisplayIndexTests()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "kazuko-p3a-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string path1 = Path.Combine(tempDir, "xuất-lưới-cà-phê-sữa.xls");
            string path2 = Path.Combine(tempDir, "đổi-thứ-tự-cột.xls");
            try
            {
                using (var host = MakeOffscreenHost())
                using (var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false })
                {
                    grid.DataError += delegate (object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
                    host.Controls.Add(grid);
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_text", HeaderText = "Tên món", ValueType = typeof(string) });
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_int", HeaderText = "Số lượng", ValueType = typeof(int) });
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_dec", HeaderText = "Đơn giá", ValueType = typeof(decimal), DefaultCellStyle = { Format = "#,##0.##" } });
                    grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "c_bool", HeaderText = "Đang bán", ValueType = typeof(bool) });
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_date", HeaderText = "Ngày bán", ValueType = typeof(DateTime), DefaultCellStyle = { Format = "dd/MM/yyyy" } });
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_hidden", HeaderText = "Cột ẩn", Visible = false, ValueType = typeof(string) });
                    grid.Columns.Add(new DataGridViewButtonColumn { Name = "c_btn", HeaderText = "Thao tác", Text = "Sửa", UseColumnTextForButtonValue = true });
                    grid.Columns.Add(new DataGridViewImageColumn { Name = "c_img", HeaderText = "Ảnh", DefaultCellStyle = { NullValue = null } });

                    DateTime d1 = new DateTime(2026, 11, 26);
                    DateTime d2 = new DateTime(2026, 11, 27);
                    DateTime d3 = new DateTime(2026, 11, 28);
                    grid.Rows.Add("Cà phê sữa đá", 5, 1234.5m, true, d1, "AN-1", null, null);
                    grid.Rows.Add("Trà đào cam sả", 12, 30000m, false, d2, "AN-2", null, null);
                    grid.Rows.Add(null, 0, null, true, d3, "AN-3", null, null);

                    host.Show();
                    Application.DoEvents();

                    var rec1 = new GridExportOutputRecorder { NextSavePath = path1, ConfirmOpenResult = false };
                    bool ok1 = GridExcelExporter.Export(host, grid, "", rec1, null);
                    bool fileExists = File.Exists(path1);
                    XlsSheetSnapshot snap = fileExists ? ReadFirstXlsSheet(path1) : null;

                    bool dimsOk = snap != null && snap.RowCount == 4 && snap.ColCount == 5;
                    bool headersOk = dimsOk
                        && snap.Rows[0][0].StringValue == "Tên món"
                        && snap.Rows[0][1].StringValue == "Số lượng"
                        && snap.Rows[0][2].StringValue == "Đơn giá"
                        && snap.Rows[0][3].StringValue == "Đang bán"
                        && snap.Rows[0][4].StringValue == "Ngày bán";
                    bool dateCellOk = dimsOk
                        && (snap.Rows[1][4].Type == Aspose.Cells.CellValueType.IsDateTime
                            || (snap.Rows[1][4].Type == Aspose.Cells.CellValueType.IsNumeric && snap.Rows[1][4].NumberFormat == "dd/MM/yyyy"))
                        && snap.Rows[1][4].DateTimeValue.HasValue
                        && snap.Rows[1][4].DateTimeValue.Value.Date == d1
                        && snap.Rows[1][4].NumberFormat == "dd/MM/yyyy";
                    bool row1Ok = dimsOk
                        && snap.Rows[1][0].StringValue == "Cà phê sữa đá"
                        && snap.Rows[1][1].Type == Aspose.Cells.CellValueType.IsNumeric
                        && snap.Rows[1][1].DoubleValue.HasValue && Math.Abs(snap.Rows[1][1].DoubleValue.Value - 5.0) < 0.0001
                        && snap.Rows[1][2].Type == Aspose.Cells.CellValueType.IsNumeric
                        && snap.Rows[1][2].DoubleValue.HasValue && Math.Abs(snap.Rows[1][2].DoubleValue.Value - 1234.5) < 0.0001
                        && snap.Rows[1][3].Type == Aspose.Cells.CellValueType.IsBool
                        && snap.Rows[1][3].BoolValue == true
                        && dateCellOk;
                    bool row2BoolOk = dimsOk
                        && snap.Rows[2][3].Type == Aspose.Cells.CellValueType.IsBool
                        && snap.Rows[2][3].BoolValue == false;
                    bool row3NullOk = dimsOk
                        && snap.Rows[3][0].RawValue == null
                        && snap.Rows[3][2].RawValue == null;
                    bool noHiddenOrBtnOrImg = dimsOk
                        && !XlsSheetContainsSubstring(snap, "Cột ẩn")
                        && !XlsSheetContainsSubstring(snap, "AN-1")
                        && !XlsSheetContainsSubstring(snap, "Thao tác")
                        && !XlsSheetContainsSubstring(snap, "Ảnh");

                    Check("In P3a (i/T2/T7 kiểu ô, tiếng Việt & bỏ cột ẩn/nút/ảnh): đúng 5 cột, số 1234.5 kiểu số, bool TRUE/FALSE, ngày định dạng, null rỗng, tên file có dấu",
                        ok1 && fileExists && rec1.LastDefaultFileName == "" && headersOk && row1Ok && row2BoolOk && row3NullOk && noHiddenOrBtnOrImg,
                        "ok=" + ok1 + ",dims=" + dimsOk + ",hdr=" + headersOk + ",r1=" + row1Ok + ",r2b=" + row2BoolOk + ",r3n=" + row3NullOk + ",noH=" + noHiddenOrBtnOrImg);

                    // Đổi DisplayIndex rồi xuất lại: Ngày bán -> 0, Đơn giá -> 1, Tên món -> 2.
                    grid.Columns["c_date"].DisplayIndex = 0;
                    grid.Columns["c_dec"].DisplayIndex = 1;
                    grid.Columns["c_text"].DisplayIndex = 2;

                    var rec2 = new GridExportOutputRecorder { NextSavePath = path2, ConfirmOpenResult = false };
                    bool ok2 = GridExcelExporter.Export(host, grid, "", rec2, null);
                    XlsSheetSnapshot snap2 = File.Exists(path2) ? ReadFirstXlsSheet(path2) : null;
                    bool reorderedOk = ok2 && snap2 != null && snap2.ColCount == 5
                        && snap2.Rows[0][0].StringValue == "Ngày bán"
                        && snap2.Rows[0][1].StringValue == "Đơn giá"
                        && snap2.Rows[0][2].StringValue == "Tên món"
                        && snap2.Rows[1][1].Type == Aspose.Cells.CellValueType.IsNumeric
                        && snap2.Rows[1][1].DoubleValue.HasValue && Math.Abs(snap2.Rows[1][1].DoubleValue.Value - 1234.5) < 0.0001
                        && snap2.Rows[1][2].StringValue == "Cà phê sữa đá";

                    Check("In P3a (i thứ tự DisplayIndex): đổi DisplayIndex rồi xuất lại ra đúng thứ tự cột mới",
                        reorderedOk,
                        snap2 == null ? "no-file" : (snap2.Rows[0][0].StringValue + "|" + snap2.Rows[0][1].StringValue + "|" + snap2.Rows[0][2].StringValue));
                }
            }
            finally
            {
                SafeDeleteFile(path1);
                SafeDeleteFile(path2);
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static void GridExcelFilterSortEmptyTests()
        {
            string pathUnbound = Path.Combine(Path.GetTempPath(), "kazuko-p3a-sort-" + Guid.NewGuid().ToString("N") + ".xls");
            string pathBound = Path.Combine(Path.GetTempPath(), "kazuko-p3a-dv-" + Guid.NewGuid().ToString("N") + ".xls");
            string pathEmpty = Path.Combine(Path.GetTempPath(), "kazuko-p3a-empty-" + Guid.NewGuid().ToString("N") + ".xls");
            try
            {
                using (var host = MakeOffscreenHost())
                using (var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false })
                {
                    host.Controls.Add(grid);
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_code", HeaderText = "Mã", ValueType = typeof(string) });
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_val", HeaderText = "Giá trị", ValueType = typeof(decimal) });
                    grid.Rows.Add("M1", 100m);
                    grid.Rows.Add("M2", 300m);
                    grid.Rows.Add("M3", 200m);
                    host.Show();
                    Application.DoEvents();

                    // Sắp giảm dần theo Giá trị (300, 200, 100) rồi ẩn dòng giữa (M3 = 200).
                    grid.Sort(grid.Columns["c_val"], ListSortDirection.Descending);
                    grid.CurrentCell = null;
                    grid.Rows[1].Visible = false;

                    var rec1 = new GridExportOutputRecorder { NextSavePath = pathUnbound, ConfirmOpenResult = false };
                    bool ok1 = GridExcelExporter.Export(host, grid, "", rec1, null);
                    XlsSheetSnapshot snap1 = File.Exists(pathUnbound) ? ReadFirstXlsSheet(pathUnbound) : null;
                    bool unboundOk = ok1 && snap1 != null && snap1.RowCount == 3
                        && snap1.Rows[1][0].StringValue == "M2"
                        && snap1.Rows[2][0].StringValue == "M1"
                        && !XlsSheetContainsSubstring(snap1, "M3");

                    // Lưới gắn DataView có RowFilter + Sort.
                    var dt = new DataTable();
                    dt.Columns.Add("code", typeof(string));
                    dt.Columns.Add("price", typeof(decimal));
                    dt.Rows.Add("A", 50000m);
                    dt.Rows.Add("B", 10000m);
                    dt.Rows.Add("C", 30000m);
                    var dv = new DataView(dt) { RowFilter = "price >= 20000", Sort = "price ASC" };
                    grid.Rows.Clear();
                    grid.Columns.Clear();
                    grid.AutoGenerateColumns = false;
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "g_code", DataPropertyName = "code", HeaderText = "Mã" });
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "g_price", DataPropertyName = "price", HeaderText = "Giá" });
                    grid.DataSource = dv;
                    Application.DoEvents();

                    var rec2 = new GridExportOutputRecorder { NextSavePath = pathBound, ConfirmOpenResult = false };
                    bool ok2 = GridExcelExporter.Export(host, grid, "", rec2, null);
                    XlsSheetSnapshot snap2 = File.Exists(pathBound) ? ReadFirstXlsSheet(pathBound) : null;
                    bool boundOk = ok2 && snap2 != null && snap2.RowCount == 3
                        && snap2.Rows[1][0].StringValue == "C"
                        && snap2.Rows[2][0].StringValue == "A"
                        && !XlsSheetContainsSubstring(snap2, "B");

                    Check("In P3a (ii lưới lọc và sắp xếp): ẩn dòng Visible=false + Sort và DataView RowFilter+Sort ra đúng số dòng & thứ tự",
                        unboundOk && boundOk, "unbound=" + unboundOk + ",bound=" + boundOk);

                    // (iii) Lưới không có dòng -> chỉ có dòng tiêu đề.
                    dv.RowFilter = "price < 0";
                    Application.DoEvents();
                    var rec3 = new GridExportOutputRecorder { NextSavePath = pathEmpty, ConfirmOpenResult = false };
                    bool ok3 = GridExcelExporter.Export(host, grid, "", rec3, null);
                    XlsSheetSnapshot snap3 = File.Exists(pathEmpty) ? ReadFirstXlsSheet(pathEmpty) : null;
                    bool emptyOk = ok3 && snap3 != null && snap3.RowCount == 1 && snap3.ColCount == 2
                        && snap3.Rows[0][0].StringValue == "Mã" && snap3.Rows[0][1].StringValue == "Giá";

                    Check("In P3a (iii lưới rỗng không có dòng): file xuất ra chỉ có đúng 1 dòng tiêu đề cột",
                        emptyOk, snap3 == null ? "no-file" : ("rows=" + snap3.RowCount + ",cols=" + snap3.ColCount));
                }
            }
            finally
            {
                SafeDeleteFile(pathUnbound);
                SafeDeleteFile(pathBound);
                SafeDeleteFile(pathEmpty);
            }
        }

        private static void GridExcelDialogFlowAndErrorTests()
        {
            string cancelPath = Path.Combine(Path.GetTempPath(), "kazuko-p3a-cancel-" + Guid.NewGuid().ToString("N") + ".xls");
            string okPath = Path.Combine(Path.GetTempPath(), "kazuko-p3a-open-" + Guid.NewGuid().ToString("N") + ".xls");
            string badDirPath = Path.Combine(Path.GetTempPath(), "kazuko-missing-dir-" + Guid.NewGuid().ToString("N"), "out.xls");
            string lockedPath = Path.Combine(Path.GetTempPath(), "kazuko-p3a-locked-" + Guid.NewGuid().ToString("N") + ".xls");
            try
            {
                using (var host = MakeOffscreenHost())
                using (var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false })
                {
                    host.Controls.Add(grid);
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c1", HeaderText = "Cột 1" });
                    grid.Rows.Add("Dòng 1");
                    host.Show();
                    Application.DoEvents();

                    // (iv) Hủy hộp Lưu -> không tạo file, không hỏi mở.
                    var recCancel = new GridExportOutputRecorder { NextSavePath = null, ConfirmOpenResult = true };
                    bool resCancel = GridExcelExporter.Export(host, grid, "", recCancel, null);
                    Check("In P3a (iv hủy hộp Lưu file): trả false, không tạo file, không hỏi mở file",
                        !resCancel && !File.Exists(cancelPath) && recCancel.PromptSaveCount == 1
                        && recCancel.ConfirmOpenCount == 0 && recCancel.OpenFileCount == 0);

                    // (v) Trả lời Không -> không mở; trả lời Có -> gọi OpenFile giả đúng 1 lần với đúng đường dẫn.
                    var recNo = new GridExportOutputRecorder { NextSavePath = okPath, ConfirmOpenResult = false };
                    bool resNo = GridExcelExporter.Export(host, grid, "", recNo, null);
                    var recYes = new GridExportOutputRecorder { NextSavePath = okPath, ConfirmOpenResult = true };
                    bool resYes = GridExcelExporter.Export(host, grid, "", recYes, null);
                    Check("In P3a (v hỏi mở file sau khi xuất): trả lời Không không mở; trả lời Có gọi OpenFile đúng 1 lần đúng đường dẫn",
                        resNo && recNo.ConfirmOpenCount == 1 && recNo.OpenFileCount == 0
                        && resYes && recYes.ConfirmOpenCount == 1 && recYes.OpenFileCount == 1 && recYes.LastOpenedPath == okPath,
                        "noOpen=" + recNo.OpenFileCount + ",yesOpen=" + recYes.OpenFileCount);

                    // (vi) Ghi lỗi: thư mục không tồn tại & file bị khóa -> báo MỘT câu rõ lý do, không văng, không hỏi mở.
                    string recordedBadDir = null;
                    var recBadDir = new GridExportOutputRecorder { NextSavePath = badDirPath, ConfirmOpenResult = true };
                    bool resBadDir = GridExcelExporter.Export(host, grid, "", recBadDir, msg => recordedBadDir = msg);
                    bool badDirOk = !resBadDir && recBadDir.ConfirmOpenCount == 0 && recBadDir.OpenFileCount == 0
                        && recBadDir.ErrorMessages.Count == 1
                        && !string.IsNullOrWhiteSpace(recordedBadDir)
                        && recordedBadDir.IndexOf("Không thể xuất file Excel", StringComparison.Ordinal) >= 0;

                    string recordedLocked = null;
                    var recLocked = new GridExportOutputRecorder { NextSavePath = lockedPath, ConfirmOpenResult = true };
                    bool resLocked;
                    using (new FileStream(lockedPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    {
                        resLocked = GridExcelExporter.Export(host, grid, "", recLocked, msg => recordedLocked = msg);
                    }
                    bool lockedOk = !resLocked && recLocked.ConfirmOpenCount == 0 && recLocked.OpenFileCount == 0
                        && recLocked.ErrorMessages.Count == 1
                        && !string.IsNullOrWhiteSpace(recordedLocked)
                        && recordedLocked.IndexOf("Không thể xuất file Excel", StringComparison.Ordinal) >= 0;

                    Check("In P3a (vi/T6 lỗi ghi file: thư mục không tồn tại & file đang bị giữ mở): báo một câu rõ lý do, không văng",
                        badDirOk && lockedOk,
                        "badDir=" + badDirOk + ",locked=" + lockedOk);
                }
            }
            finally
            {
                SafeDeleteFile(cancelPath);
                SafeDeleteFile(okPath);
                SafeDeleteFile(lockedPath);
            }
        }

        private static void GridExcelHiddenSecretColumnTest()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "kazuko-p3a-secret-" + Guid.NewGuid().ToString("N") + ".xls");
            try
            {
                using (var host = MakeOffscreenHost())
                using (var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false })
                {
                    host.Controls.Add(grid);
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_pub", HeaderText = "Tên công khai" });
                    grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c_sec", HeaderText = "Giá vốn bí mật", Visible = false });
                    grid.Rows.Add("Trà sữa trân châu", "BÍ-MẬT-123");
                    host.Show();
                    Application.DoEvents();

                    var rec = new GridExportOutputRecorder { NextSavePath = tempPath, ConfirmOpenResult = false };
                    bool ok = GridExcelExporter.Export(host, grid, "", rec, null);
                    XlsSheetSnapshot snap = File.Exists(tempPath) ? ReadFirstXlsSheet(tempPath) : null;
                    bool clean = ok && snap != null && snap.ColCount == 1
                        && XlsSheetContainsSubstring(snap, "Trà sữa trân châu")
                        && !XlsSheetContainsSubstring(snap, "BÍ-MẬT-123")
                        && !XlsSheetContainsSubstring(snap, "Giá vốn bí mật");

                    Check("In P3a (vii/T4 cột Visible=false): chuỗi 'BÍ-MẬT-123' và tiêu đề cột ẩn không có ở bất cứ ô nào trong file",
                        clean, snap == null ? "no-file" : ("cols=" + snap.ColCount));
                }
            }
            finally
            {
                SafeDeleteFile(tempPath);
            }
        }
    }
}
