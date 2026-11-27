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
    /// Hộp thoại Thêm / Sửa / Xem chi tiết phiếu Thưởng phạt (TTHUONGPHAT, spec docs/specs/nhan-su-thuong-phat-tam-ung.md §2).
    /// Thứ tự trường: Số phiếu, Ngày (khóa nếu thiếu quyền đổi ngày), Nhân viên, Thưởng, Phạt, Lý do, Ghi chú.
    /// Hỗ trợ tham số giới hạn tháng/năm (<paramref name="limitMonth"/>, <paramref name="limitYear"/>) khi gọi từ bảng lương.
    /// Phần dựng bố cục, theme và hook kiểm thử UI đặt ở <c>RewardPenaltyEditDialog.Layout.cs</c>.
    /// </summary>
    internal sealed partial class RewardPenaltyEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 560;

        private readonly UserSession _session;
        private readonly RewardPenaltyService _service;
        private readonly bool _readOnly;
        private RewardPenaltyMonthLimit _monthLimit;

        private RewardPenaltyRow _row;
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _number = new TextBox { MaxLength = RewardPenaltyText.MaxTextLength };
        private readonly DateTimePicker _date = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy"
        };
        private readonly LookupCombo _employee = new LookupCombo();
        private readonly NumberBox _bonus = new NumberBox
        {
            Minimum = 0m,
            Maximum = RewardPenaltyText.MaxAmount,
            DecimalPlaces = 2,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right
        };
        private readonly NumberBox _penalty = new NumberBox
        {
            Minimum = 0m,
            Maximum = RewardPenaltyText.MaxAmount,
            DecimalPlaces = 2,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right
        };
        private readonly LookupCombo _reason = new LookupCombo();
        private readonly TextBox _note = new TextBox
        {
            Multiline = true,
            MaxLength = RewardPenaltyText.MaxTextLength,
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

        public RewardPenaltyEditDialog(
            UserSession session,
            string recordId = null,
            bool readOnly = false,
            int? limitMonth = null,
            int? limitYear = null)
            : base("THƯỞNG PHẠT - THÊM MỚI", "Thông tin phiếu thưởng phạt", BodyWidth96, 380)
        {
            _session = session ?? AppSession.Current;
            _service = new RewardPenaltyService(_session);
            _isNew = string.IsNullOrEmpty(recordId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);
            if (limitMonth.HasValue && limitYear.HasValue)
                _monthLimit = new RewardPenaltyMonthLimit(limitMonth.Value, limitYear.Value);

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
                    ShowValidationError(RewardPenaltyText.NotFound);
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

        public void SetMonthLimit(int month, int year)
        {
            _monthLimit = new RewardPenaltyMonthLimit(month, year);
        }

        private void LoadLookups()
        {
            try
            {
                _employee.SetItems(_service.LoadActiveEmployees());
            }
            catch (DataAccessException)
            {
                _employee.SetItems(new List<LookupItem>());
            }

            var reasons = new List<LookupItem> { new LookupItem { Id = null, Name = "(Không chọn)" } };
            try
            {
                reasons.AddRange(_service.LoadActiveReasons());
            }
            catch (DataAccessException)
            {
                // Bỏ qua nếu không tải được lý do.
            }
            _reason.SetItems(reasons);
        }

        private void InitNewRecord()
        {
            _isNew = true;
            _row = RewardPenaltyRow.NewDefault();
            try
            {
                _row.Name = _service.GenerateNumber(DateTime.Today);
            }
            catch (DataAccessException)
            {
                _row.Name = "";
            }
            SetTitle("THƯỞNG PHẠT - THÊM MỚI");
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
                _row = RewardPenaltyRow.NewDefault();
                return;
            }
            SetTitle(_readOnly ? "THƯỞNG PHẠT - XEM CHI TIẾT" : "THƯỞNG PHẠT - SỬA");
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
                _employee.SelectId(string.IsNullOrEmpty(_row.EmployeeId) ? null : _row.EmployeeId);
                _bonus.Value = ClampAmount(_row.Bonus);
                _penalty.Value = ClampAmount(_row.Penalty);
                _reason.SelectId(string.IsNullOrEmpty(_row.ReasonId) ? null : _row.ReasonId);
                _note.Text = _row.Note ?? "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private static decimal ClampAmount(decimal v)
        {
            if (v < 0m) return 0m;
            if (v > RewardPenaltyText.MaxAmount) return RewardPenaltyText.MaxAmount;
            return v;
        }

        private void ApplyPermissions()
        {
            _number.ReadOnly = _readOnly;
            _date.Enabled = !_readOnly && _service.CanChangeDate;
            _employee.Enabled = !_readOnly;
            _bonus.ReadOnly = _readOnly;
            _bonus.Enabled = !_readOnly;
            _penalty.ReadOnly = _readOnly;
            _penalty.Enabled = !_readOnly;
            _reason.Enabled = !_readOnly;
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
            _number.TextChanged += onChanged;
            _date.ValueChanged += onChanged;
            _employee.SelectedIndexChanged += onChanged;
            _employee.TextChanged += onChanged;
            _bonus.ValueChanged += onChanged;
            _penalty.ValueChanged += onChanged;
            _reason.SelectedIndexChanged += onChanged;
            _reason.TextChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_number.Text).Append('|');
            sb.Append(_date.Value.Date.ToString("yyyy-MM-dd")).Append('|');
            sb.Append(_employee.SelectedId).Append('|');
            sb.Append(_bonus.Value).Append('|');
            sb.Append(_penalty.Value).Append('|');
            sb.Append(_reason.SelectedId).Append('|');
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
            _row.EmployeeId = string.IsNullOrEmpty(_employee.SelectedId) ? null : _employee.SelectedId;
            _row.Bonus = _bonus.Value;
            _row.Penalty = _penalty.Value;
            _row.ReasonId = string.IsNullOrEmpty(_reason.SelectedId) ? null : _reason.SelectedId;
            _row.Note = _note.Text ?? "";

            RewardPenaltySaveResult res;
            try
            {
                res = _service.Save(_row, _monthLimit);
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
            SetTitle("THƯỞNG PHẠT - SỬA");
            _original = Snapshot();
            UpdateButtons();
            return true;
        }

        private void FocusErrorField(RewardPenaltySaveResult res)
        {
            if (res.HasNumberError) { _number.Focus(); _number.SelectAll(); }
            else if (res.HasDateError && _date.Enabled) { _date.Focus(); }
            else if (res.HasEmployeeError && _employee.Enabled) { _employee.Focus(); }
            else if (res.HasAmountError && _bonus.Enabled) { _bonus.Focus(); }
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
