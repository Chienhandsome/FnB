using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem chi tiết Thông báo sửa chữa (TSUACHUA, spec docs/specs/quan-tri-nho.md §2).
    /// Thứ tự trường: Số phiếu, Ngày, Phòng/Bàn (tự điền Loại phòng), Loại phòng, Nhân viên, Nội dung,
    /// Đã sửa xong + Còn sử dụng được (độc lập), Ghi chú.
    /// Phần dựng bố cục, theme và hook kiểm thử UI đặt ở <c>RepairNoticeEditDialog.Layout.cs</c>.
    /// </summary>
    internal sealed partial class RepairNoticeEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 620;

        private readonly UserSession _session;
        private readonly RepairNoticeService _service;
        private readonly bool _readOnly;

        private RepairNoticeRow _row;
        private List<RepairRoomLookup> _rooms = new List<RepairRoomLookup>();
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _number = new TextBox { MaxLength = RepairNoticeText.MaxTextLength };
        private readonly DateTimePicker _date = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy"
        };
        private readonly LookupCombo _room = new LookupCombo();
        private readonly LookupCombo _roomType = new LookupCombo();
        private readonly LookupCombo _employee = new LookupCombo();
        private readonly TextBox _content = new TextBox
        {
            Multiline = true,
            MaxLength = RepairNoticeText.MaxTextLength,
            ScrollBars = ScrollBars.Vertical
        };
        private readonly CheckBox _chkFixed = new CheckBox
        {
            Text = "Đã sửa xong",
            AutoSize = true,
            UseMnemonic = false
        };
        private readonly CheckBox _chkUsable = new CheckBox
        {
            Text = "Còn sử dụng được",
            AutoSize = true,
            UseMnemonic = false
        };
        private readonly TextBox _note = new TextBox
        {
            Multiline = true,
            MaxLength = RepairNoticeText.MaxTextLength,
            ScrollBars = ScrollBars.Vertical
        };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnClose;

        public string SavedId { get; private set; }
        public string SavedNumber { get; private set; }
        public bool AnySaved { get; private set; }

        public RepairNoticeEditDialog(UserSession session, string recordId = null, bool readOnly = false)
            : base("THÔNG BÁO SỬA CHỮA - THÊM MỚI", "Thông tin phiếu thông báo sửa chữa", BodyWidth96, 382)
        {
            _session = session ?? AppSession.Current;
            _service = new RepairNoticeService(_session);
            _isNew = string.IsNullOrEmpty(recordId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);

            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildFields();
            LoadLookups();

            if (_isNew)
                InitNewRecord();
            else
                LoadExistingRecord(recordId);

            ApplyPermissions();
            HookChangeEvents();
            _original = Snapshot();
            UpdateButtons();
            ApplyDialogTheme();

            FormClosing += OnFormClosing;
            Shown += delegate
            {
                if (_loadFailed)
                {
                    ShowValidationError(RepairNoticeText.NotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                {
                    _number.Focus();
                    _number.SelectAll();
                }
            };
        }

        private void LoadLookups()
        {
            try
            {
                _rooms = _service.LoadActiveRooms();
            }
            catch (DataAccessException)
            {
                _rooms = new List<RepairRoomLookup>();
            }
            var roomItems = new List<LookupItem>(_rooms.Count);
            foreach (RepairRoomLookup r in _rooms)
                roomItems.Add(new LookupItem { Id = r.Id, Name = r.Name });
            _room.SetItems(roomItems);

            var roomTypes = new List<LookupItem> { new LookupItem { Id = null, Name = "(Không chọn)" } };
            try
            {
                roomTypes.AddRange(_service.LoadActiveRoomTypes());
            }
            catch (DataAccessException)
            {
                // Bỏ qua nếu không tải được loại phòng.
            }
            _roomType.SetItems(roomTypes);

            var employees = new List<LookupItem> { new LookupItem { Id = null, Name = "(Không chọn)" } };
            try
            {
                employees.AddRange(_service.LoadActiveEmployees());
            }
            catch (DataAccessException)
            {
                // Bỏ qua nếu không tải được nhân viên.
            }
            _employee.SetItems(employees);
        }

        private void InitNewRecord()
        {
            _isNew = true;
            _row = RepairNoticeRow.NewDefault();
            try
            {
                _row.Name = _service.GenerateNumber(DateTime.Today);
            }
            catch (DataAccessException)
            {
                _row.Name = "";
            }
            SetTitle("THÔNG BÁO SỬA CHỮA - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string recordId)
        {
            _isNew = false;
            try
            {
                _row = _service.LoadById(recordId);
            }
            catch (DataAccessException)
            {
                _row = null;
            }
            if (_row == null)
            {
                _loadFailed = true;
                _row = RepairNoticeRow.NewDefault();
                return;
            }
            SetTitle(_readOnly ? "THÔNG BÁO SỬA CHỮA - XEM CHI TIẾT" : "THÔNG BÁO SỬA CHỮA - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _number.Text = _row.Name ?? "";
                DateTime d = _row.Date == DateTime.MinValue ? DateTime.Today : _row.Date.Date;
                if (d < _date.MinDate) d = DateTime.Today;
                if (d > _date.MaxDate) d = DateTime.Today;
                _date.Value = d;
                _room.SelectId(string.IsNullOrEmpty(_row.RoomId) ? null : _row.RoomId);
                _roomType.SelectId(string.IsNullOrEmpty(_row.RoomTypeId) ? null : _row.RoomTypeId);
                _employee.SelectId(string.IsNullOrEmpty(_row.EmployeeId) ? null : _row.EmployeeId);
                _content.Text = _row.Content ?? "";
                _chkFixed.Checked = _row.IsFixed;
                _chkUsable.Checked = _row.IsUsable;
                _note.Text = _row.Note ?? "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private void ApplyPermissions()
        {
            _number.ReadOnly = _readOnly;
            _date.Enabled = !_readOnly && _service.CanChangeDate;
            _room.Enabled = !_readOnly;
            _roomType.Enabled = !_readOnly;
            _employee.Enabled = !_readOnly;
            _content.ReadOnly = _readOnly;
            _chkFixed.Enabled = !_readOnly;
            _chkUsable.Enabled = !_readOnly;
            _note.ReadOnly = _readOnly;

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;
            SubtitleText = _readOnly
                ? "Chế độ chỉ xem (không có quyền sửa)"
                : "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
        }

        private void HookChangeEvents()
        {
            EventHandler onChanged = delegate
            {
                if (_loading)
                    return;
                ClearError();
                UpdateButtons();
            };
            _number.TextChanged += onChanged;
            _date.ValueChanged += onChanged;
            _room.SelectedIndexChanged += delegate
            {
                if (_loading)
                    return;
                AutoFillRoomTypeFromSelectedRoom();
                ClearError();
                UpdateButtons();
            };
            _room.TextChanged += onChanged;
            _roomType.SelectedIndexChanged += onChanged;
            _roomType.TextChanged += onChanged;
            _employee.SelectedIndexChanged += onChanged;
            _employee.TextChanged += onChanged;
            _content.TextChanged += onChanged;
            _chkFixed.CheckedChanged += onChanged;
            _chkUsable.CheckedChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        /// <summary>Khi chọn Phòng/Bàn, tự điền Loại phòng theo DBAN.DLOAIPHONGID (người dùng vẫn sửa được sau đó).</summary>
        private void AutoFillRoomTypeFromSelectedRoom()
        {
            string roomId = _room.SelectedId;
            if (string.IsNullOrEmpty(roomId))
                return;
            RepairRoomLookup found = _rooms.Find(r => string.Equals(r.Id, roomId, StringComparison.Ordinal));
            if (found == null)
                return;
            _loading = true;
            try
            {
                _roomType.SelectId(string.IsNullOrEmpty(found.RoomTypeId) ? null : found.RoomTypeId);
            }
            finally
            {
                _loading = false;
            }
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_number.Text).Append('|');
            sb.Append(_date.Value.Date.ToString("yyyy-MM-dd")).Append('|');
            sb.Append(_room.SelectedId).Append('|');
            sb.Append(_roomType.SelectedId).Append('|');
            sb.Append(_employee.SelectedId).Append('|');
            sb.Append(_content.Text).Append('|');
            sb.Append(_chkFixed.Checked ? '1' : '0').Append('|');
            sb.Append(_chkUsable.Checked ? '1' : '0').Append('|');
            sb.Append(_note.Text);
            return sb.ToString();
        }

        private bool Dirty
        {
            get { return !_readOnly && Snapshot() != _original; }
        }

        private void UpdateButtons()
        {
            if (_readOnly)
                return;
            bool canSave = Dirty || _isNew;
            _btnSave.Enabled = canSave;
            _btnSaveNew.Enabled = canSave && _service.CanAdd;
            _btnSaveClose.Enabled = canSave;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Control | Keys.L))
                {
                    if (_btnSave.Enabled) SaveStay();
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S))
                {
                    if (_btnSaveNew.Visible && _btnSaveNew.Enabled) SaveAndNew();
                    return true;
                }
                if (keyData == Keys.Enter && !_note.Focused && !_content.Focused)
                {
                    SaveAndClose();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SaveStay() { DoSave(); }

        private void SaveAndNew()
        {
            if (!DoSave())
                return;
            InitNewRecord();
            ApplyPermissions();
            ApplyDialogTheme();
            _original = Snapshot();
            UpdateButtons();
            _number.Focus();
            _number.SelectAll();
        }

        private void SaveAndClose()
        {
            if (!DoSave())
                return;
            DialogResult = DialogResult.OK;
            Close();
        }

        internal bool DoSave()
        {
            if (_readOnly)
                return false;

            _row.Name = (_number.Text ?? "").Trim();
            _row.Date = _date.Value.Date;
            _row.RoomId = string.IsNullOrEmpty(_room.SelectedId) ? null : _room.SelectedId;
            _row.RoomTypeId = string.IsNullOrEmpty(_roomType.SelectedId) ? null : _roomType.SelectedId;
            _row.EmployeeId = string.IsNullOrEmpty(_employee.SelectedId) ? null : _employee.SelectedId;
            _row.Content = _content.Text ?? "";
            _row.IsFixed = _chkFixed.Checked;
            _row.IsUsable = _chkUsable.Checked;
            _row.Note = _note.Text ?? "";

            RepairNoticeSaveResult res;
            try
            {
                res = _service.Save(_row);
            }
            catch (DataAccessException ex)
            {
                ShowValidationError("Không ghi được dữ liệu: " + ex.Message);
                return false;
            }

            if (!res.Ok)
            {
                ShowValidationError(res.ErrorMessage);
                FocusErrorField(res);
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            SavedNumber = res.SavedNumber;
            _row.Id = res.SavedId;
            AnySaved = true;
            _isNew = false;
            SetTitle("THÔNG BÁO SỬA CHỮA - SỬA");
            _original = Snapshot();
            UpdateButtons();
            return true;
        }

        private void FocusErrorField(RepairNoticeSaveResult res)
        {
            if (res.HasNumberError) { _number.Focus(); _number.SelectAll(); }
            else if (res.HasRoomError && _room.Enabled) { _room.Focus(); }
            else if (res.HasContentError) { _content.Focus(); _content.SelectAll(); }
            else if (res.HasNoteError) { _note.Focus(); _note.SelectAll(); }
        }

        private void ShowValidationError(string message)
        {
            LastErrorForTest = message;
            _lblError.Text = (message ?? "").Replace(Environment.NewLine, "  |  ");
            _lblError.Visible = !string.IsNullOrEmpty(message);
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ClearError()
        {
            LastErrorForTest = null;
            _lblError.Text = "";
            _lblError.Visible = false;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_readOnly || DialogResult == DialogResult.OK || !Dirty || SuppressPopup)
                return;
            DialogResult answer = MessageBox.Show(this, "Dữ liệu đã thay đổi. Bạn có muốn lưu lại không?",
                "Xác nhận", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
            if (answer == DialogResult.Yes && !DoSave()) e.Cancel = true;
        }
    }
}
