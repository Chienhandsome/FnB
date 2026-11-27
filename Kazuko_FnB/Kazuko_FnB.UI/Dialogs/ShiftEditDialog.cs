using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem chi tiết Danh mục ca làm việc (DCALAMVIEC, spec docs/specs/nhan-su-danh-muc.md §3).
    /// Đọc lại cờ TinhLuongSuDungMayChamCong mỗi lần mở: khi bật mở nhập Từ giờ/Đến giờ (HH:mm, bắt buộc, ca qua đêm hợp lệ);
    /// khi tắt khóa Từ giờ/Đến giờ và giữ nguyên giá trị cũ khi sửa.
    /// Tỉ lệ lương (%) 0..1000 mặc định 100 kèm ghi chú "0 được hiểu là 100%".
    /// </summary>
    internal sealed class ShiftEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 520;
        private const int BodyHeight96 = 278;

        private readonly UserSession _session;
        private readonly ShiftService _service;
        private readonly bool _readOnly;
        private readonly bool _useTimekeeper;

        private ShiftRow _row;
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _name = new TextBox { MaxLength = ShiftText.MaxTextLength };
        private readonly DateTimePicker _fromTime = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            ShowCheckBox = true
        };
        private readonly DateTimePicker _toTime = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            ShowCheckBox = true
        };
        private readonly NumberBox _salaryRate = new NumberBox
        {
            Minimum = ShiftText.MinSalaryRate,
            Maximum = ShiftText.MaxSalaryRate,
            DecimalPlaces = 2,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right
        };
        private readonly Label _lblRateHint = new Label
        {
            Text = ShiftText.ZeroRateHint,
            AutoSize = false,
            UseMnemonic = false,
            Tag = "muted",
            TextAlign = ContentAlignment.MiddleLeft
        };
        private readonly TextBox _note = new TextBox
        {
            Multiline = true,
            MaxLength = ShiftText.MaxTextLength,
            ScrollBars = ScrollBars.Vertical
        };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnClose;

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal bool UseTimekeeperForTest { get { return _useTimekeeper; } }
        internal TextBox NameBoxForTest { get { return _name; } }
        internal DateTimePicker FromTimePickerForTest { get { return _fromTime; } }
        internal DateTimePicker ToTimePickerForTest { get { return _toTime; } }
        internal NumberBox SalaryRateBoxForTest { get { return _salaryRate; } }
        internal Label RateHintLabelForTest { get { return _lblRateHint; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        public ShiftEditDialog(UserSession session, string shiftId, bool readOnly = false)
            : base("CA LÀM VIỆC - THÊM MỚI", "Thông tin ca làm việc", BodyWidth96, 394)
        {
            _session = session ?? AppSession.Current;
            _service = new ShiftService(_session);
            _isNew = string.IsNullOrEmpty(shiftId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);
            _useTimekeeper = _service.ReadUseTimekeeper();

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildFields();

            if (_isNew)
                InitNewRecord();
            else
                LoadExistingRecord(shiftId);

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
                    ShowValidationError(ShiftText.NotFound);
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

        private void BuildFields()
        {
            float s = Dpi;
            string fromCap = _useTimekeeper ? "Từ giờ *" : "Từ giờ";
            string toCap = _useTimekeeper ? "Đến giờ *" : "Đến giờ";
            string[] captions =
            {
                "Tên ca làm việc *",
                fromCap,
                toCap,
                "Tỉ lệ lương (%)",
                "Ghi chú"
            };
            int maxCap = (int)(124 * s);
            foreach (string cap in captions)
            {
                int need = TextRenderer.MeasureText(cap, Font).Width + (int)(12 * s);
                if (need > maxCap)
                    maxCap = need;
            }
            int leftPad = (int)(14 * s);
            int gap = (int)(8 * s);
            int editX = leftPad + maxCap + gap;
            int editW = Math.Max((int)(200 * s), Body.ClientSize.Width - editX - leftPad);
            int rowH = (int)(26 * s);
            int timeW = Math.Max((int)(130 * s), TextRenderer.MeasureText("23:59", Font).Width + (int)(66 * s));

            int tab = 0;
            PlaceRow(captions[0], _name, leftPad, (int)(14 * s), maxCap, editX, editW, rowH, ref tab);
            PlaceRow(captions[1], _fromTime, leftPad, (int)(48 * s), maxCap, editX, Math.Min(timeW, editW), rowH, ref tab);
            PlaceRow(captions[2], _toTime, leftPad, (int)(82 * s), maxCap, editX, Math.Min(timeW, editW), rowH, ref tab);

            int rateW = (int)(120 * s);
            PlaceRow(captions[3], _salaryRate, leftPad, (int)(116 * s), maxCap, editX, rateW, rowH, ref tab);
            int hintX = editX + rateW + (int)(10 * s);
            _lblRateHint.SetBounds(hintX, (int)(116 * s), Math.Max((int)(120 * s), Body.ClientSize.Width - hintX - leftPad), rowH);
            Body.Controls.Add(_lblRateHint);

            PlaceRow(captions[4], _note, leftPad, (int)(152 * s), maxCap, editX, editW, (int)(84 * s), ref tab);

            _lblError.SetBounds(leftPad, (int)(244 * s), Body.ClientSize.Width - 2 * leftPad, (int)(24 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private void PlaceRow(string caption, Control editor, int x, int y, int labelW, int editX, int editW, int h, ref int tabIndex)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = false
            };
            lbl.SetBounds(x, y, labelW, Math.Min(h, (int)(26 * Dpi)));
            editor.SetBounds(editX, y, editW, h);
            editor.TabIndex = tabIndex++;
            _fieldLabels.Add(lbl);
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
        }

        private void InitNewRecord()
        {
            _isNew = true;
            _row = ShiftRow.NewDefault();
            SetTitle("CA LÀM VIỆC - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string shiftId)
        {
            _isNew = false;
            _row = _service.LoadById(shiftId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = ShiftRow.NewDefault();
                return;
            }
            SetTitle(_readOnly ? "CA LÀM VIỆC - XEM CHI TIẾT" : "CA LÀM VIỆC - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _name.Text = _row.Name ?? "";
                SetTimePicker(_fromTime, _row.FromTime, 8, 0);
                SetTimePicker(_toTime, _row.ToTime, 17, 0);
                _salaryRate.Value = ClampRate(_row.SalaryRate);
                _note.Text = _row.Note ?? "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private static void SetTimePicker(DateTimePicker picker, DateTime? value, int fallbackHour, int fallbackMinute)
        {
            if (value.HasValue)
            {
                picker.Value = ShiftRules.NormalizeShiftTime(value).Value;
                picker.Checked = true;
            }
            else
            {
                picker.Value = new DateTime(2000, 1, 1, fallbackHour, fallbackMinute, 0);
                picker.Checked = false;
            }
        }

        private static decimal ClampRate(decimal v)
        {
            if (v < ShiftText.MinSalaryRate) return ShiftText.MinSalaryRate;
            if (v > ShiftText.MaxSalaryRate) return ShiftText.MaxSalaryRate;
            return v;
        }

        private void ApplyPermissions()
        {
            _name.ReadOnly = _readOnly;
            bool timeEditable = !_readOnly && _useTimekeeper;
            _fromTime.Enabled = timeEditable;
            _toTime.Enabled = timeEditable;
            _salaryRate.ReadOnly = _readOnly;
            _salaryRate.Enabled = !_readOnly;
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
            _name.TextChanged += onChanged;
            _fromTime.ValueChanged += onChanged;
            _toTime.ValueChanged += onChanged;
            _salaryRate.ValueChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_name.Text).Append('|');
            sb.Append(_fromTime.Checked ? ShiftRules.FormatShiftTime(_fromTime.Value) : "").Append('|');
            sb.Append(_toTime.Checked ? ShiftRules.FormatShiftTime(_toTime.Value) : "").Append('|');
            sb.Append(_salaryRate.Value).Append('|');
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
            ApplyDialogTheme();
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
                _row.FromTime = _fromTime.Checked ? ShiftRules.NormalizeShiftTime(_fromTime.Value) : (DateTime?)null;
                _row.ToTime = _toTime.Checked ? ShiftRules.NormalizeShiftTime(_toTime.Value) : (DateTime?)null;
            }
            else if (_isNew)
            {
                _row.FromTime = null;
                _row.ToTime = null;
            }
            _row.SalaryRate = _salaryRate.Value;
            _row.Note = _note.Text ?? "";

            ShiftSaveResult res;
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
            SetTitle("CA LÀM VIỆC - SỬA");
            _original = Snapshot();
            UpdateButtons();
            return true;
        }

        private void FocusErrorField(ShiftSaveResult res)
        {
            if (res.HasNameError) { _name.Focus(); _name.SelectAll(); }
            else if (res.HasFromTimeError && _fromTime.Enabled) { _fromTime.Focus(); }
            else if (res.HasToTimeError && _toTime.Enabled) { _toTime.Focus(); }
            else if (res.HasSalaryRateError) { _salaryRate.Focus(); }
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

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);

            _salaryRate.BackColor = _salaryRate.Enabled ? t.Surface : disabledBack;
            _salaryRate.ForeColor = t.Text;
            _lblRateHint.BackColor = Color.Transparent;
            _lblRateHint.ForeColor = t.TextMuted;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
