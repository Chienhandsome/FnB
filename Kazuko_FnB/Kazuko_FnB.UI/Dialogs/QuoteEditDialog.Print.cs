using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Phần nút "Lưu &amp; In" / "Lưu &amp; Xem in" và hook kiểm thử của <see cref="QuoteEditDialog"/>.
    /// Cả hai nút gọi đúng đường lưu <see cref="DoSave"/>; lưu hỏng hoặc chế độ chỉ đọc thì không in;
    /// lưu xong đọc lại báo giá từ CSDL để in và giữ hộp sửa vẫn mở.
    /// </summary>
    internal sealed partial class QuoteEditDialog
    {
        private readonly DocumentPrintCoordinator _printCoordinator = new DocumentPrintCoordinator();

        internal FlatButton SavePrintButtonForTest { get { return _btnSavePrint; } }
        internal FlatButton SaveViewPrintButtonForTest { get { return _btnSaveViewPrint; } }
        internal FlatButton ExportExcelButtonForTest { get { return _btnExportExcel; } }
        internal string LastPrintMessageForTest { get; private set; }
        internal IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> PrintTemplatePickerForTest { get; set; }
        internal Func<string, List<PrintTemplateRecord>> PrintTemplateLoaderForTest { get; set; }

        internal bool ExportDetailGridToExcel()
        {
            return GridExcelExporter.Export(
                this,
                _detailGrid,
                "",
                GridExportOutputForTest,
                delegate (string msg) { LastPrintMessageForTest = msg; });
        }

        internal bool SaveAndPrint()
        {
            return SaveThenPrint(true, _btnSavePrint);
        }

        internal bool SaveAndViewPrint()
        {
            return SaveThenPrint(false, _btnSaveViewPrint);
        }

        private bool SaveThenPrint(bool directDefault, FlatButton triggerButton)
        {
            if (_printCoordinator.IsBusy || _readOnly)
                return false;

            string targetId;
            bool hasSavedId = !_isNew && _row != null && !string.IsNullOrEmpty(_row.Id);
            if (Dirty || !hasSavedId)
            {
                if (!DoSave())
                    return false;
                targetId = SavedId;
            }
            else
            {
                targetId = _row.Id;
            }

            if (string.IsNullOrEmpty(targetId))
                return false;

            return _printCoordinator.PrintQuote(
                this,
                triggerButton,
                _session.DatabasePath,
                _session,
                targetId,
                directDefault,
                PrintOutputHandlerForTest,
                PrintTemplatePickerForTest,
                PrintTemplateLoaderForTest,
                delegate (string msg) { LastPrintMessageForTest = msg; });
        }
    }
}
