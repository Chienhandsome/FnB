using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần in phiếu đang chọn từ màn danh sách <see cref="QuoteListView"/> và hook kiểm thử.</summary>
    internal sealed partial class QuoteListView
    {
        private readonly DocumentPrintCoordinator _printCoordinator = new DocumentPrintCoordinator();

        internal IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> PrintTemplatePickerForTest { get; set; }
        internal Func<string, List<PrintTemplateRecord>> PrintTemplateLoaderForTest { get; set; }

        internal bool ExportGridToExcel()
        {
            return GridExcelExporter.Export(
                this,
                _grid,
                "",
                GridExportOutputForTest,
                delegate (string msg) { LastInfoMessageForTest = msg; });
        }

        internal bool PrintSelectedQuote()
        {
            if (_printCoordinator.IsBusy)
                return false;

            string quoteId = SelectedQuoteIdForPrint();
            if (string.IsNullOrEmpty(quoteId))
            {
                ShowInfo(PrintConstants.SelectQuoteToPrintMessage);
                return false;
            }

            return _printCoordinator.PrintQuote(
                this,
                _btnPrint,
                _session.DatabasePath,
                _session,
                quoteId,
                false,
                PrintOutputHandlerForTest,
                PrintTemplatePickerForTest,
                PrintTemplateLoaderForTest,
                delegate (string msg) { LastInfoMessageForTest = msg; });
        }

        private string SelectedQuoteIdForPrint()
        {
            if (_grid.Rows.Count == 0 || _grid.SelectedRows.Count == 0)
                return null;
            DataGridViewRow row = _grid.CurrentRow != null && _grid.CurrentRow.Selected
                ? _grid.CurrentRow
                : _grid.SelectedRows[0];
            var item = row == null ? null : row.Tag as QuoteListRow;
            return item == null ? null : item.Id;
        }
    }
}
