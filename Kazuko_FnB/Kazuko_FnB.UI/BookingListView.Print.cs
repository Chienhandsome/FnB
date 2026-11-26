using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần in phiếu đang chọn từ màn danh sách <see cref="BookingListView"/> và hook kiểm thử.</summary>
    internal sealed partial class BookingListView
    {
        private readonly DocumentPrintCoordinator _printCoordinator = new DocumentPrintCoordinator();

        internal bool SuppressPopup { get; set; }
        internal string LastInfoMessageForTest { get; private set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }
        internal InputBox NumberBoxForTest { get { return _txtNumber; } }
        internal DateBox FromDateBoxForTest { get { return _dtFrom; } }
        internal DateBox ToDateBoxForTest { get { return _dtTo; } }
        internal IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> PrintTemplatePickerForTest { get; set; }
        internal Func<string, List<PrintTemplateRecord>> PrintTemplateLoaderForTest { get; set; }

        internal void ReloadForTest()
        {
            Reload();
        }

        internal bool ExportGridToExcel()
        {
            return GridExcelExporter.Export(
                this,
                _grid,
                "",
                GridExportOutputForTest,
                delegate (string msg) { LastInfoMessageForTest = msg; });
        }

        internal bool PrintSelectedBooking()
        {
            if (_printCoordinator.IsBusy)
                return false;

            string bookingId = SelectedBookingIdForPrint();
            if (string.IsNullOrEmpty(bookingId))
            {
                ShowPrintInfo(PrintConstants.SelectBookingToPrintMessage);
                return false;
            }

            return _printCoordinator.PrintBooking(
                this,
                _btnPrint,
                _databasePath,
                AppSession.Current,
                bookingId,
                false,
                PrintOutputHandlerForTest,
                PrintTemplatePickerForTest,
                PrintTemplateLoaderForTest,
                delegate (string msg) { LastInfoMessageForTest = msg; });
        }

        private string SelectedBookingIdForPrint()
        {
            if (_grid.Rows.Count == 0 || _grid.SelectedRows.Count == 0)
                return null;
            DataGridViewRow row = _grid.CurrentRow != null && _grid.CurrentRow.Selected
                ? _grid.CurrentRow
                : _grid.SelectedRows[0];
            var item = row == null ? null : row.Tag as BookingListRow;
            return item == null ? null : item.Id;
        }

        private void ShowPrintInfo(string message)
        {
            LastInfoMessageForTest = message;
            if (!SuppressPopup)
                Msg.Info(this, message);
        }
    }
}
