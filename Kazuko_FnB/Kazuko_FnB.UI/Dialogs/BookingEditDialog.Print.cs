using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Phần nút "Lưu &amp; In" / "Lưu &amp; Xem in" và hook kiểm thử của <see cref="BookingEditDialog"/>.
    /// Cả hai nút gọi đúng đường lưu <see cref="DoSave"/>; lưu hỏng hoặc chế độ chỉ đọc thì không in;
    /// lưu xong đọc lại phiếu từ CSDL để in và giữ hộp sửa vẫn mở.
    /// </summary>
    internal sealed partial class BookingEditDialog
    {
        private readonly DocumentPrintCoordinator _printCoordinator = new DocumentPrintCoordinator();

        internal FlatButton SavePrintButtonForTest { get { return _btnSavePrint; } }
        internal FlatButton SaveViewPrintButtonForTest { get { return _btnSaveViewPrint; } }
        internal TextBox NumberBoxForTest { get { return _txtNumber; } }
        internal TextBox TenKhachBoxForTest { get { return _txtTenKhach; } }
        internal TextBox DiaChiBoxForTest { get { return _txtDiaChi; } }
        internal TextBox DienThoaiBoxForTest { get { return _txtDienThoai; } }
        internal NumberBox PhiVanChuyenBoxForTest { get { return _numPhiVanChuyen; } }
        internal NumberBox TileThueBoxForTest { get { return _numTileThue; } }
        internal string LastPrintMessageForTest { get; private set; }
        internal IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> PrintTemplatePickerForTest { get; set; }
        internal Func<string, List<PrintTemplateRecord>> PrintTemplateLoaderForTest { get; set; }

        internal void SetRoomsForTest(List<string> roomIds, List<string> roomNames)
        {
            _roomIds = roomIds == null ? new List<string>() : new List<string>(roomIds);
            _roomNames = roomNames == null ? new List<string>() : new List<string>(roomNames);
            _txtRooms.Text = string.Join(", ", _roomNames.ToArray());
            OnDirtyChanged();
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
            if (_printCoordinator.IsBusy)
                return false;
            if (!DoSave())
                return false;
            if (string.IsNullOrEmpty(SavedId))
                return false;

            return _printCoordinator.PrintBooking(
                this,
                triggerButton,
                _databasePath,
                _session,
                SavedId,
                directDefault,
                PrintOutputHandlerForTest,
                PrintTemplatePickerForTest,
                PrintTemplateLoaderForTest,
                delegate (string msg) { LastPrintMessageForTest = msg; });
        }

        private void ShowError(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Error(this, message);
        }
    }
}
