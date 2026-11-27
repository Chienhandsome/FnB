using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem chi tiết Danh mục nhân viên (DNHANVIEN, spec docs/specs/nhan-su-danh-muc.md §2).
    /// Đọc lại cờ TinhLuongSuDungMayChamCong mỗi lần mở để ẩn/hiện Mã nhân viên và Ca làm việc.
    /// Không có ô nhập LUONGTHEOCA (T3) và Ảnh (SIMAGEID); khi sửa giữ nguyên các trường ẩn/khóa.
    /// Gọn, không cuộn dọc ở 1366×768 (100–150% DPI), dùng ThemeManager và NumberBox dùng chung.
    /// </summary>
    internal sealed partial class EmployeeEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 580;

        private readonly UserSession _session;
        private readonly EmployeeService _service;
        private readonly bool _readOnly;
        private readonly bool _useTimekeeper;

        private EmployeeRow _row;
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _name = new TextBox { MaxLength = EmployeeText.MaxTextLength };
        private readonly TextBox _code = new TextBox { MaxLength = EmployeeText.MaxTextLength };
        private readonly TextBox _phone = new TextBox { MaxLength = EmployeeText.MaxTextLength };
        private readonly TextBox _address = new TextBox { MaxLength = EmployeeText.MaxTextLength };
        private readonly LookupCombo _shift = new LookupCombo();
        private readonly GroupBox _grpSalary = new GroupBox { Text = "Cách tính lương" };
        private readonly RadioButton _rdByShift = new RadioButton { Text = "Theo ca", AutoSize = true };
        private readonly RadioButton _rdByMonth = new RadioButton { Text = "Theo tháng", AutoSize = true };
        private readonly RadioButton _rdMonthByShift = new RadioButton { Text = "Tháng theo ca", AutoSize = true };
        private readonly CheckBox _chkOffSaturday = new CheckBox { Text = "Nghỉ thứ 7", AutoSize = true };
        private readonly CheckBox _chkOffSunday = new CheckBox { Text = "Nghỉ chủ nhật", AutoSize = true };
        private readonly NumberBox _shiftSalary = new NumberBox
        {
            Minimum = 0m,
            Maximum = EmployeeText.MaxSalary,
            DecimalPlaces = 2,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right
        };
        private readonly NumberBox _monthlySalary = new NumberBox
        {
            Minimum = 0m,
            Maximum = EmployeeText.MaxSalary,
            DecimalPlaces = 2,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right
        };
        private readonly TextBox _note = new TextBox
        {
            Multiline = true,
            MaxLength = EmployeeText.MaxTextLength,
            ScrollBars = ScrollBars.Vertical
        };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };
        private Label _lblCode;
        private Label _lblShift;

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnClose;

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }

        public EmployeeEditDialog(UserSession session, string employeeId, bool readOnly = false)
            : base("NHÂN VIÊN - THÊM MỚI", "Thông tin nhân viên", BodyWidth96, 460)
        {
            _session = session ?? AppSession.Current;
            _service = new EmployeeService(_session);
            _isNew = string.IsNullOrEmpty(employeeId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);
            _useTimekeeper = _service.ReadUseTimekeeper();

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
                LoadExistingRecord(employeeId);

            ApplyPermissions();
            ApplySalaryModeState();
            HookChangeEvents();
            _original = Snapshot();
            UpdateButtons();
            ApplyDialogTheme();

            FormClosing += OnFormClosing;
            Shown += delegate
            {
                if (_loadFailed)
                {
                    ShowValidationError(EmployeeText.NotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                {
                    _name.Focus();
                    _name.SelectAll();
                }
            };
        }

        private void LoadLookups()
        {
            var shifts = new List<LookupItem>();
            shifts.Add(new LookupItem { Id = null, Name = "(Không chọn)" });
            try
            {
                shifts.AddRange(_service.LoadActiveShifts());
            }
            catch (Exception)
            {
                // Bỏ qua nếu không tải được danh sách ca.
            }
            _shift.SetItems(shifts);
        }

        private void InitNewRecord()
        {
            _isNew = true;
            _row = EmployeeRow.NewDefault();
            SetTitle("NHÂN VIÊN - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string employeeId)
        {
            _isNew = false;
            _row = _service.LoadById(employeeId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = EmployeeRow.NewDefault();
                return;
            }
            SetTitle(_readOnly ? "NHÂN VIÊN - XEM CHI TIẾT" : "NHÂN VIÊN - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _name.Text = _row.Name ?? "";
                _code.Text = _row.Code ?? "";
                _phone.Text = _row.Phone ?? "";
                _address.Text = _row.Address ?? "";
                _shift.SelectId(string.IsNullOrEmpty(_row.ShiftId) ? null : _row.ShiftId);

                int mode = EmployeeRules.IsValidSalaryMode(_row.SalaryMode)
                    ? _row.SalaryMode
                    : EmployeeSalaryMode.ByShift;
                _rdByShift.Checked = mode == EmployeeSalaryMode.ByShift;
                _rdByMonth.Checked = mode == EmployeeSalaryMode.ByMonth;
                _rdMonthByShift.Checked = mode == EmployeeSalaryMode.MonthByShift;

                _chkOffSaturday.Checked = _row.IsOffSaturday;
                _chkOffSunday.Checked = _row.IsOffSunday;
                _shiftSalary.Value = ClampNumber(_row.ShiftSalary);
                _monthlySalary.Value = ClampNumber(_row.MonthlySalary);
                _note.Text = _row.Note ?? "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private static decimal ClampNumber(decimal v)
        {
            if (v < 0m) return 0m;
            if (v > EmployeeText.MaxSalary) return EmployeeText.MaxSalary;
            return v;
        }

        private int SelectedSalaryMode()
        {
            if (_rdByMonth.Checked) return EmployeeSalaryMode.ByMonth;
            if (_rdMonthByShift.Checked) return EmployeeSalaryMode.MonthByShift;
            return EmployeeSalaryMode.ByShift;
        }

        internal void ApplySalaryModeState()
        {
            int mode = SelectedSalaryMode();
            bool lockShift = _readOnly || EmployeeRules.IsShiftSalaryLocked(mode);
            bool lockMonth = _readOnly || EmployeeRules.IsMonthlySalaryLocked(mode);
            bool allowOff = !_readOnly && EmployeeRules.AreOffDayFlagsEnabled(mode);

            _shiftSalary.ReadOnly = lockShift;
            _shiftSalary.Enabled = !lockShift;
            _monthlySalary.ReadOnly = lockMonth;
            _monthlySalary.Enabled = !lockMonth;
            _chkOffSaturday.Enabled = allowOff;
            _chkOffSunday.Enabled = allowOff;
            ApplyDialogTheme();
        }

        private void ApplyPermissions()
        {
            _name.ReadOnly = _readOnly;
            _code.ReadOnly = _readOnly;
            _phone.ReadOnly = _readOnly;
            _address.ReadOnly = _readOnly;
            _shift.Enabled = !_readOnly;
            _rdByShift.Enabled = !_readOnly;
            _rdByMonth.Enabled = !_readOnly;
            _rdMonthByShift.Enabled = !_readOnly;
            _note.ReadOnly = _readOnly;

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly;
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
            EventHandler onModeChanged = delegate
            {
                if (_loading)
                    return;
                ApplySalaryModeState();
                ClearError();
                UpdateButtons();
            };

            _name.TextChanged += onChanged;
            _code.TextChanged += onChanged;
            _phone.TextChanged += onChanged;
            _address.TextChanged += onChanged;
            _shift.SelectedIndexChanged += onChanged;
            _shift.TextChanged += onChanged;
            _rdByShift.CheckedChanged += onModeChanged;
            _rdByMonth.CheckedChanged += onModeChanged;
            _rdMonthByShift.CheckedChanged += onModeChanged;
            _chkOffSaturday.CheckedChanged += onChanged;
            _chkOffSunday.CheckedChanged += onChanged;
            _shiftSalary.ValueChanged += onChanged;
            _monthlySalary.ValueChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_name.Text).Append('|');
            sb.Append(_code.Text).Append('|');
            sb.Append(_phone.Text).Append('|');
            sb.Append(_address.Text).Append('|');
            sb.Append(_shift.SelectedId).Append('|');
            sb.Append(SelectedSalaryMode()).Append('|');
            sb.Append(_chkOffSaturday.Checked).Append('|');
            sb.Append(_chkOffSunday.Checked).Append('|');
            sb.Append(_shiftSalary.Value).Append('|');
            sb.Append(_monthlySalary.Value).Append('|');
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
            _btnSaveNew.Enabled = canSave;
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
                    if (_btnSaveNew.Enabled) SaveAndNew();
                    return true;
                }
                if (keyData == Keys.Enter && !_note.Focused)
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
            ApplySalaryModeState();
            _original = Snapshot();
            UpdateButtons();
            _name.Focus();
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

            _row.Name = _name.Text ?? "";
            if (_useTimekeeper)
            {
                _row.Code = _code.Text ?? "";
                _row.ShiftId = string.IsNullOrEmpty(_shift.SelectedId) ? null : _shift.SelectedId;
            }
            else if (_isNew)
            {
                _row.Code = "";
                _row.ShiftId = null;
            }
            _row.Phone = _phone.Text ?? "";
            _row.Address = _address.Text ?? "";
            _row.SalaryMode = SelectedSalaryMode();
            if (!EmployeeRules.IsShiftSalaryLocked(_row.SalaryMode) || _isNew)
                _row.ShiftSalary = _shiftSalary.Value;
            if (!EmployeeRules.IsMonthlySalaryLocked(_row.SalaryMode) || _isNew)
                _row.MonthlySalary = _monthlySalary.Value;
            if (EmployeeRules.AreOffDayFlagsEnabled(_row.SalaryMode) || _isNew)
            {
                _row.IsOffSaturday = _chkOffSaturday.Checked;
                _row.IsOffSunday = _chkOffSunday.Checked;
            }
            _row.Note = _note.Text ?? "";

            EmployeeSaveResult res;
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
            _row.Id = res.SavedId;
            AnySaved = true;
            _isNew = false;
            SetTitle("NHÂN VIÊN - SỬA");
            _original = Snapshot();
            UpdateButtons();
            return true;
        }

        private void FocusErrorField(EmployeeSaveResult res)
        {
            if (res.HasNameError) { _name.Focus(); _name.SelectAll(); }
            else if (res.HasCodeError && _code.Visible) { _code.Focus(); _code.SelectAll(); }
            else if (res.HasPhoneError) { _phone.Focus(); _phone.SelectAll(); }
            else if (res.HasAddressError) { _address.Focus(); _address.SelectAll(); }
            else if (res.HasShiftError && _shift.Visible) { _shift.Focus(); }
            else if (res.HasShiftSalaryError && _shiftSalary.Enabled) { _shiftSalary.Focus(); }
            else if (res.HasMonthlySalaryError && _monthlySalary.Enabled) { _monthlySalary.Focus(); }
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
