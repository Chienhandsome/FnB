using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Aspose.Cells;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP4BarcodeUiTests(string db, UserSession adminSession, BarcodePrintFixture fx)
        {
            string excelOutPath = Path.Combine(Path.GetTempPath(), "kazuko-p4-barcode-" + Guid.NewGuid().ToString("N") + ".xls");
            ThemeTokens savedTheme = ThemeManager.Current;
            try
            {
                TestBarcodeTemplateDialogUi(db);
                TestBarcodeCatalogPanelInteractionsAndFlow(db, adminSession, fx, excelOutPath);
                TestBarcodeRepeatClickAndThemeSwitch(db, adminSession, fx);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
                SafeDeleteFile(excelOutPath);
            }
        }

        private static void TestBarcodeTemplateDialogUi(string db)
        {
            List<PrintTemplateRecord> templates = new PrintTemplateRepository(db)
                .LoadBarcodeTemplatesForTable(PrintConstants.ProductTableDescId);

            using (var dlg = new PrintTemplateDialog(templates, PrintDialogOptions.ForBarcodeDialog()))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    bool titleOk = dlg.Text == PrintConstants.BarcodeTemplateDialogTitle;
                    bool countOk = dlg.TemplateListForTest.Items.Count == 6;
                    bool buttonsOk = dlg.OkButtonForTest.Text == "Chấp nhận"
                        && dlg.CancelButtonForTest.Text == "Thoát"
                        && !dlg.OptionsPanelForTest.Visible;
                    bool noForbidden = !ControlTreeHasAnyText(
                        dlg, "Thêm mới", "Chỉnh sửa", "Xóa mẫu", "Thiết kế", "Bartender");

                    // Không chọn mẫu nào -> bấm Chấp nhận không làm gì (hộp vẫn mở, Selection == null).
                    dlg.TemplateListForTest.SelectedIndex = -1;
                    dlg.OkButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool noSelectIgnored = dlg.Selection == null && dlg.Visible && dlg.DialogResult == DialogResult.None;

                    // Bấm Thoát -> hủy, Selection == null.
                    dlg.CancelButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool cancelOk = dlg.Selection == null && dlg.DialogResult == DialogResult.Cancel;

                    Check("In P4 (vi hộp MẪU IN MÃ VẠCH): đúng tiêu đề, 6 mẫu, nút Chấp nhận/Thoát, ẩn Thêm/Sửa/Xóa/Thiết kế, không chọn mẫu thì Chấp nhận không làm gì, Thoát hủy",
                        titleOk && countOk && buttonsOk && noForbidden && noSelectIgnored && cancelOk,
                        "title=" + titleOk + ",cnt=" + dlg.TemplateListForTest.Items.Count + ",btn=" + buttonsOk + ",noForb=" + noForbidden + ",noSel=" + noSelectIgnored + ",cancel=" + cancelOk);
                }
                finally
                {
                    if (!dlg.IsDisposed)
                        dlg.Close();
                }
            }
        }

        private static void TestBarcodeCatalogPanelInteractionsAndFlow(
            string db,
            UserSession adminSession,
            BarcodePrintFixture fx,
            string excelOutPath)
        {
            AppSession.Current = adminSession;
            string productCatId = new CatalogRepository(db).FindIdByTable("DMATHANG");

            using (var host = MakeOffscreenHost())
            {
                var view = new CatalogView(db, productCatId, "Mặt hàng") { Dock = DockStyle.Fill };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    BarcodePanel panel = view.BarcodePanelForTest;
                    bool initiallyHidden = panel != null && !panel.Visible;

                    // (iv) Bấm "In mã vạch" khi chưa chọn dòng -> mở khung trống, không văng, không Bartender, không AutoScroll dọc.
                    view.GridForTest.ClearSelection();
                    view.PrintBarcodeButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool openEmptyOk = initiallyHidden
                        && panel.Visible
                        && panel.ItemCount == 0
                        && panel.TotalQuantity == 0
                        && panel.TitleLabelForTest.Text == "IN MÃ VẠCH"
                        && !ControlTreeHasAnyText(panel, "Bartender")
                        && !view.AutoScroll
                        && panel.AddButtonForTest.Height >= 32
                        && panel.PrintButtonForTest.Height >= 32;

                    // (v) Bấm "In" khi khung rỗng -> báo câu chuẩn, không mở hộp chọn mẫu.
                    var emptyRec = new PrintOutputRecorder();
                    int pickerCallsWhenEmpty = 0;
                    panel.PrintOutputForTest = emptyRec;
                    panel.TemplatePickerForTest = delegate
                    {
                        pickerCallsWhenEmpty++;
                        return null;
                    };
                    panel.PrintButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool emptyPrintOk = pickerCallsWhenEmpty == 0
                        && emptyRec.PreviewCount == 0
                        && emptyRec.PrintCount == 0
                        && emptyRec.InfoMessages.Count == 1
                        && emptyRec.InfoMessages[0] == PrintConstants.NoBarcodeItemsMessage;

                    Check("In P4 (v nút In khi khung rỗng): báo đúng câu chuẩn 'Chưa có mặt hàng nào để in mã vạch', không mở hộp chọn mẫu",
                        emptyPrintOk, "picker=" + pickerCallsWhenEmpty + ",info=" + (emptyRec.InfoMessages.Count > 0 ? emptyRec.InfoMessages[0] : ""));

                    // (iv) Thêm P1 hai lần -> thành 1 dòng số lượng 2.
                    view.SelectProductRowsForTest(fx.Product1Id);
                    Application.DoEvents();
                    panel.AddButtonForTest.PerformClick();
                    panel.AddButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool addTwiceOk = panel.ItemCount == 1
                        && Convert.ToInt32(panel.GridForTest.Rows[0].Cells["bc_qty"].Value) == 2
                        && panel.TotalQuantity == 2
                        && panel.TotalValueLabelForTest.Text == "2";

                    // Chọn 2 dòng P2 + P3 -> Thêm cả hai.
                    view.SelectProductRowsForTest(fx.Product2Id, fx.Product3Id);
                    Application.DoEvents();
                    panel.AddButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool addMultiOk = panel.ItemCount == 3 && panel.TotalQuantity == 4;

                    // Sửa số lượng dòng 3 thành 3 -> Tổng sl = 6.
                    panel.GridForTest.CurrentCell = panel.GridForTest.Rows[2].Cells["bc_qty"];
                    panel.GridForTest.BeginEdit(true);
                    if (panel.GridForTest.EditingControl != null)
                        panel.GridForTest.EditingControl.Text = "3";
                    panel.GridForTest.EndEdit();
                    Application.DoEvents();
                    bool editValidQtyOk = Convert.ToInt32(panel.GridForTest.Rows[2].Cells["bc_qty"].Value) == 3
                        && panel.TotalQuantity == 6;

                    // Nhập số âm "-5" và chữ "abc" -> bị từ chối/trả về giá trị cũ 3, không DataError.
                    panel.GridForTest.CurrentCell = panel.GridForTest.Rows[2].Cells["bc_qty"];
                    panel.GridForTest.BeginEdit(true);
                    if (panel.GridForTest.EditingControl != null)
                        panel.GridForTest.EditingControl.Text = "-5";
                    panel.GridForTest.EndEdit();
                    Application.DoEvents();
                    bool negRejectedOk = Convert.ToInt32(panel.GridForTest.Rows[2].Cells["bc_qty"].Value) == 3
                        && panel.TotalQuantity == 6;

                    panel.GridForTest.CurrentCell = panel.GridForTest.Rows[2].Cells["bc_qty"];
                    panel.GridForTest.BeginEdit(true);
                    if (panel.GridForTest.EditingControl != null)
                        panel.GridForTest.EditingControl.Text = "abc";
                    panel.GridForTest.EndEdit();
                    Application.DoEvents();
                    bool textRejectedOk = Convert.ToInt32(panel.GridForTest.Rows[2].Cells["bc_qty"].Value) == 3
                        && panel.TotalQuantity == 6
                        && panel.DataErrorCountForTest == 0;

                    // Xóa dòng thứ 2 (P2) -> còn 2 dòng (P1 sl 2, P3 sl 3), Tổng sl = 5.
                    panel.GridForTest.ClearSelection();
                    panel.GridForTest.Rows[1].Selected = true;
                    panel.GridForTest.CurrentCell = panel.GridForTest.Rows[1].Cells["bc_name"];
                    panel.RemoveButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool removeOk = panel.ItemCount == 2 && panel.TotalQuantity == 5;

                    Check("In P4 (iv thao tác khung IN MÃ VẠCH): mở khung trống, thêm trùng cộng dồn, thêm nhiều dòng, sửa số lượng đổi Tổng sl, từ chối âm/chữ không DataError, Xóa cập nhật tổng",
                        openEmptyOk && addTwiceOk && addMultiOk && editValidQtyOk && negRejectedOk && textRejectedOk && removeOk,
                        "open=" + openEmptyOk + ",add2=" + addTwiceOk + ",multi=" + addMultiOk + ",edit=" + editValidQtyOk + ",neg=" + negRejectedOk + ",txt=" + textRejectedOk + ",rm=" + removeOk + ",de=" + panel.DataErrorCountForTest);

                    // (vii) Xuất excel của khung: đúng 3 cột Mã, Tên, Số lượng và Số lượng là kiểu số.
                    var xlsRec = new GridExportOutputRecorder { NextSavePath = excelOutPath, ConfirmOpenResult = false };
                    panel.GridExportOutputForTest = xlsRec;
                    panel.ExportExcelButtonForTest.PerformClick();
                    Application.DoEvents();

                    XlsSheetSnapshot snap = File.Exists(excelOutPath) ? ReadFirstXlsSheet(excelOutPath) : null;
                    bool excelOk = xlsRec.PromptSaveCount == 1
                        && snap != null
                        && snap.ColCount == 3
                        && snap.RowCount == 3
                        && snap.Rows[0][0].StringValue == "Mã"
                        && snap.Rows[0][1].StringValue == "Tên"
                        && snap.Rows[0][2].StringValue == "Số lượng"
                        && snap.Rows[1][0].StringValue == "8931001"
                        && snap.Rows[1][1].StringValue == "Cà phê sữa đá P4"
                        && snap.Rows[1][2].Type == CellValueType.IsNumeric
                        && snap.Rows[1][2].DoubleValue.HasValue && Math.Abs(snap.Rows[1][2].DoubleValue.Value - 2.0) < 0.0001
                        && snap.Rows[2][2].Type == CellValueType.IsNumeric
                        && snap.Rows[2][2].DoubleValue.HasValue && Math.Abs(snap.Rows[2][2].DoubleValue.Value - 3.0) < 0.0001;

                    Check("In P4 (vii/T2/T7 Xuất excel của khung IN MÃ VẠCH): file .xls đúng 3 cột Mã/Tên/Số lượng, tên tiếng Việt đúng, cột Số lượng kiểu số",
                        excelOk, snap == null ? "no-file" : ("rows=" + snap.RowCount + ",cols=" + snap.ColCount));

                    // (vi) Bấm In -> Chấp nhận gọi ShowPreview đúng 1 lần (KHÔNG gọi PrintToPrinter); Thoát không làm gì.
                    var previewRec = new PrintOutputRecorder();
                    panel.PrintOutputForTest = previewRec;
                    panel.TemplatePickerForTest = delegate (IWin32Window owner, IList<PrintTemplateRecord> list)
                    {
                        return new PrintSelectionResult { Template = list[0], Action = PrintActionKind.Preview };
                    };
                    panel.PrintButtonForTest.PerformClick();
                    Application.DoEvents();

                    bool previewOk = previewRec.PreviewCount == 1
                        && previewRec.PrintCount == 0
                        && previewRec.LastPreviewTitle == PrintConstants.BarcodePreviewTitle
                        && previewRec.LastPreparedPageCount > 0;

                    var cancelRec = new PrintOutputRecorder();
                    panel.PrintOutputForTest = cancelRec;
                    panel.TemplatePickerForTest = delegate { return null; };
                    panel.PrintButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool exitNothingOk = cancelRec.PreviewCount == 0 && cancelRec.PrintCount == 0 && cancelRec.ErrorMessages.Count == 0;

                    Check("In P4 (vi luồng Chấp nhận / Thoát): Chấp nhận mở ShowPreview đúng 1 lần và không gọi PrintToPrinter; Thoát không làm gì",
                        previewOk && exitNothingOk,
                        "preview=" + previewRec.PreviewCount + ",print=" + previewRec.PrintCount + ",exitPrev=" + cancelRec.PreviewCount);
                }
                finally
                {
                    host.Close();
                }
            }
        }

        private static void TestBarcodeRepeatClickAndThemeSwitch(string db, UserSession adminSession, BarcodePrintFixture fx)
        {
            AppSession.Current = adminSession;
            List<PrintTemplateRecord> templates = new PrintTemplateRepository(db)
                .LoadBarcodeTemplatesForTable(PrintConstants.ProductTableDescId);

            // (x.1) Bấm lặp nút Chấp nhận trên PrintTemplateDialog -> không dựng hai lần, nút bị khóa sau lần đầu.
            int okEvents = 0;
            using (var dlg = new PrintTemplateDialog(templates, PrintDialogOptions.ForBarcodeDialog()))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                dlg.OkButtonForTest.Click += delegate { okEvents++; };
                dlg.OkButtonForTest.PerformClick();
                if (!dlg.IsDisposed)
                    dlg.OkButtonForTest.PerformClick();
                Application.DoEvents();
                bool dlgRepeatOk = dlg.Selection != null
                    && dlg.Selection.Action == PrintActionKind.Preview
                    && !dlg.OkButtonForTest.Enabled
                    && okEvents <= 1;

                // (x.2) Gọi lặp lại BarcodePrintCoordinator khi đang bận -> không dựng hai lần (ShowPreview vẫn = 1).
                var coord = new BarcodePrintCoordinator();
                bool secondResult = true;
                var reentrantOut = new ReentrantBarcodeOutputHandler(delegate
                {
                    secondResult = coord.Print(
                        null,
                        null,
                        db,
                        new[] { new BarcodePrintRequestItem { ProductId = fx.Product1Id, Code = "8931001", Name = "Cà phê sữa đá P4", Quantity = 1 } },
                        true,
                        new PrintOutputRecorder(),
                        delegate (IWin32Window owner, IList<PrintTemplateRecord> list)
                        {
                            return new PrintSelectionResult { Template = list[0], Action = PrintActionKind.Preview };
                        },
                        null,
                        null);
                });

                bool firstResult = coord.Print(
                    null,
                    null,
                    db,
                    new[] { new BarcodePrintRequestItem { ProductId = fx.Product1Id, Code = "8931001", Name = "Cà phê sữa đá P4", Quantity = 1 } },
                    true,
                    reentrantOut,
                    delegate (IWin32Window owner, IList<PrintTemplateRecord> list)
                    {
                        return new PrintSelectionResult { Template = list[0], Action = PrintActionKind.Preview };
                    },
                    null,
                    null);

                bool busyGuardOk = firstResult && !secondResult && reentrantOut.PreviewCount == 1;

                // (x.3) Đổi theme lúc chạy trên CatalogView + BarcodePanel -> không văng, màu tiêu đề đổi theo token.
                string productCatId = new CatalogRepository(db).FindIdByTable("DMATHANG");
                bool themeOk = true;
                using (var host = MakeOffscreenHost())
                {
                    var view = new CatalogView(db, productCatId, "Mặt hàng") { Dock = DockStyle.Fill };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    view.PrintBarcodeButtonForTest.PerformClick();
                    Application.DoEvents();
                    try
                    {
                        foreach (ThemeTokens t in BuiltInThemes.All())
                        {
                            ThemeManager.Apply(t);
                            view.ApplyTheme(t, t.Background);
                            Application.DoEvents();
                            if (view.BarcodePanelForTest.TitleLabelForTest.ForeColor != t.OnPrimary
                                || view.BarcodePanelForTest.TitleLabelForTest.Parent.BackColor != t.Primary)
                            {
                                themeOk = false;
                            }
                        }
                    }
                    finally
                    {
                        host.Close();
                    }
                }

                Check("In P4 (x bấm lặp Chấp nhận & đổi theme lúc chạy): bấm lặp không dựng hai lần và đổi mọi theme không văng, màu cập nhật theo token",
                    dlgRepeatOk && busyGuardOk && themeOk,
                    "dlgRepeat=" + dlgRepeatOk + ",busyGuard=" + busyGuardOk + ",theme=" + themeOk);
            }
        }

        private sealed class ReentrantBarcodeOutputHandler : IPrintOutputHandler
        {
            private readonly Action _onPreview;
            public int PreviewCount { get; private set; }

            public ReentrantBarcodeOutputHandler(Action onPreview)
            {
                _onPreview = onPreview;
            }

            public void ShowPreview(IWin32Window owner, FastReport.Report preparedReport, PrintTemplateRecord template, string title)
            {
                PreviewCount++;
                if (_onPreview != null)
                    _onPreview();
            }

            public void PrintToPrinter(FastReport.Report preparedReport, PrintTemplateRecord template, string printerName) { }
            public string PromptSaveFile(IWin32Window owner, PrintActionKind exportKind, string defaultFileName) { return null; }
            public void ExportExcel(FastReport.Report preparedReport, PrintTemplateRecord template, string filePath) { }
            public void ExportPdf(FastReport.Report preparedReport, PrintTemplateRecord template, string filePath) { }
            public bool ConfirmOpenFile(IWin32Window owner, string filePath) { return false; }
            public void OpenFile(string filePath) { }
            public void ShowError(IWin32Window owner, string message) { }
            public void ShowInfo(IWin32Window owner, string message) { }
        }
    }
}
