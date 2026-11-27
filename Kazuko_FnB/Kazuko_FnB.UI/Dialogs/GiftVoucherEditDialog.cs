using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
    /// Hộp thoại Thêm / Sửa / Xem chi tiết voucher (DVOUCHER, spec docs/specs/danh-muc-voucher.md §2).
    /// Bố cục gọn, đo nhãn bằng chữ thật, không cuộn dọc ở 1366×768 (100–150% DPI).
    /// Hỗ trợ chế độ chỉ đọc khi thiếu quyền và khóa mọi trường trừ Ghi chú khi voucher đã có hóa đơn tham chiếu.
    /// </summary>
    internal sealed class GiftVoucherEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 560;
        private const int BodyHeight96 = 344;

        private readonly UserSession _session;
        private readonly GiftVoucherService _service;
        private readonly FormRepository _formRepo;
        private readonly string _presetGroupId;
        private readonly bool _readOnly;

        private GiftVoucherRow _row;
        private bool _isNew;
        private bool _isLockedByBill;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _code = new TextBox { MaxLength = GiftVoucherText.MaxTextLength };
        private readonly NumberBox _value = new NumberBox();
        private readonly DateBox _issueDate = new DateBox(true);
        private readonly DateBox _expiryDate = new DateBox(true);
        private readonly LookupCombo _group = new LookupCombo();
        private readonly LookupCombo _partner = new LookupCombo();
        private readonly TextBox _note = new TextBox
        {
            Multiline = true,
            MaxLength = GiftVoucherText.MaxTextLength,
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
        internal bool IsLockedByBillForTest { get { return _isLockedByBill; } }
        internal TextBox CodeBoxForTest { get { return _code; } }
        internal NumberBox ValueBoxForTest { get { return _value; } }
        internal DateBox IssueDateBoxForTest { get { return _issueDate; } }
        internal DateBox ExpiryDateBoxForTest { get { return _expiryDate; } }
        internal LookupCombo GroupComboForTest { get { return _group; } }
        internal LookupCombo PartnerComboForTest { get { return _partner; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        public GiftVoucherEditDialog(UserSession session, string voucherId, string presetGroupId, bool readOnly = false)
            : base("VOUCHER - THÊM MỚI", "Thông tin voucher", BodyWidth96, 460)
        {
            _session = session ?? AppSession.Current;
            _service = new GiftVoucherService(_session);
            _formRepo = new FormRepository(_session.DatabasePath);
            _presetGroupId = presetGroupId;
            _isNew = string.IsNullOrEmpty(voucherId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildFields();
            LoadLookups();

            if (_isNew)
                InitNewRecord(_presetGroupId);
            else
                LoadExistingRecord(voucherId);

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
                    if (!SuppressPopup)
                        Msg.Error(this, GiftVoucherText.NotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                {
                    if (_isLockedByBill)
                    {
                        _note.Focus();
                        _note.SelectionStart = _note.TextLength;
                    }
                    else
                    {
                        _code.Focus();
                        _code.SelectAll();
                    }
                }
            };
        }

        private void BuildFields()
        {
            float s = Dpi;
            _value.DecimalPlaces = 0;
            _value.Minimum = 0m;
            _value.Maximum = 999999999999m;
            _value.TextAlign = HorizontalAlignment.Right;

            string[] captions =
            {
                "Mã voucher *",
                "Giá trị *",
                "Ngày phát hành",
                "Hạn sử dụng",
                "Nhóm voucher",
                "Đối tác phân phối",
                "Ghi chú"
            };
            int maxCap = (int)(145 * s);
            foreach (string cap in captions)
            {
                int need = TextRenderer.MeasureText(cap, Font).Width + (int)(12 * s);
                if (need > maxCap)
                    maxCap = need;
            }
            int leftPad = (int)(14 * s);
            int gap = (int)(8 * s);
            int editX = leftPad + maxCap + gap;
            int editW = Math.Max((int)(220 * s), Body.ClientSize.Width - editX - leftPad);

            int tab = 0;
            PlaceRow(captions[0], _code, leftPad, (int)(12 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[1], _value, leftPad, (int)(46 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[2], _issueDate, leftPad, (int)(80 * s), maxCap, editX, editW, (int)(28 * s), ref tab);
            PlaceRow(captions[3], _expiryDate, leftPad, (int)(116 * s), maxCap, editX, editW, (int)(28 * s), ref tab);
            PlaceRow(captions[4], _group, leftPad, (int)(152 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[5], _partner, leftPad, (int)(186 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[6], _note, leftPad, (int)(220 * s), maxCap, editX, editW, (int)(80 * s), ref tab);

            _lblError.SetBounds(leftPad, (int)(308 * s), Body.ClientSize.Width - 2 * leftPad, (int)(28 * s));
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

        private void LoadLookups()
        {
            _group.SetItems(SafeLoadLookup(GiftVoucherText.GroupTableName));
            _partner.SetItems(SafeLoadLookup("DKHACHHANG"));
        }

        private List<LookupItem> SafeLoadLookup(string table)
        {
            try
            {
                return _formRepo.LoadAll(table);
            }
            catch (Exception)
            {
                return new List<LookupItem>();
            }
        }

        private void InitNewRecord(string presetGroupId)
        {
            _isNew = true;
            _isLockedByBill = false;
            _row = GiftVoucherRow.NewDefault();
            _row.GroupId = string.IsNullOrEmpty(presetGroupId) ? null : presetGroupId;
            SetTitle("VOUCHER - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string voucherId)
        {
            _isNew = false;
            _row = _service.LoadById(voucherId);
            if (_row == null)
            {
                _loadFailed = true;
                _isLockedByBill = false;
                _row = GiftVoucherRow.NewDefault();
                return;
            }
            _isLockedByBill = _row.HasAnyBillRef;
            SetTitle(_readOnly ? "VOUCHER - XEM CHI TIẾT" : "VOUCHER - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _code.Text = _row.Code ?? "";
                _value.Value = _row.Value < 0m ? 0m : _row.Value;
                _issueDate.NullableValue = _row.IssueDate;
                _expiryDate.NullableValue = _row.ExpiryDate;
                _group.SelectId(_row.GroupId);
                _partner.SelectId(_row.PartnerId);
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
            bool lockPrimary = _readOnly || _isLockedByBill;
            _code.ReadOnly = lockPrimary;
            _value.ReadOnly = lockPrimary;
            _value.Enabled = !lockPrimary;
            _issueDate.Enabled = !lockPrimary;
            _issueDate.Inner.ReadOnly = lockPrimary;
            _expiryDate.Enabled = !lockPrimary;
            _expiryDate.Inner.ReadOnly = lockPrimary;
            _group.Enabled = !lockPrimary;
            _partner.Enabled = !lockPrimary;
            _note.ReadOnly = _readOnly;

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;
            if (_readOnly)
                SubtitleText = "Chế độ chỉ xem (không có quyền sửa)";
            else if (_isLockedByBill)
                SubtitleText = "Voucher đã gắn hóa đơn " + (_row.AnyRefBillName ?? "") + " — chỉ sửa được ghi chú";
            else
                SubtitleText = "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
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
            _code.TextChanged += onChanged;
            _value.ValueChanged += onChanged;
            _issueDate.ValueChanged += onChanged;
            _expiryDate.ValueChanged += onChanged;
            _group.SelectedIndexChanged += onChanged;
            _partner.SelectedIndexChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_code.Text).Append('|');
            sb.Append(_value.Value.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(_issueDate.NullableValue.HasValue ? _issueDate.NullableValue.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "").Append('|');
            sb.Append(_expiryDate.NullableValue.HasValue ? _expiryDate.NullableValue.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "").Append('|');
            sb.Append(_group.SelectedId).Append('|');
            sb.Append(_partner.SelectedId).Append('|');
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
            InitNewRecord(_presetGroupId);
            ApplyPermissions();
            ApplyDialogTheme();
            _original = Snapshot();
            UpdateButtons();
            _code.Focus();
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

            _issueDate.Commit();
            _expiryDate.Commit();

            if (!_isLockedByBill)
            {
                _row.Code = _code.Text ?? "";
                _row.Value = _value.Value;
                _row.IssueDate = _issueDate.NullableValue;
                _row.ExpiryDate = _expiryDate.NullableValue;
                _row.GroupId = _group.SelectedId;
                _row.PartnerId = _partner.SelectedId;
            }
            _row.Note = _note.Text ?? "";

            GiftVoucherSaveResult res;
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
                if (res.HasCodeError && !_code.ReadOnly) { _code.Focus(); _code.SelectAll(); }
                else if (res.HasValueError && _value.Enabled) { _value.Focus(); }
                else if (res.HasDateError && _expiryDate.Enabled) { _expiryDate.Focus(); }
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;
            _isNew = false;
            SetTitle("VOUCHER - SỬA");
            _original = Snapshot();
            UpdateButtons();
            return true;
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
            if (answer == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (answer == DialogResult.Yes && !DoSave())
                e.Cancel = true;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);

            ApplyDateTheme(_issueDate, t, disabledBack);
            ApplyDateTheme(_expiryDate, t, disabledBack);

            _code.BackColor = _code.ReadOnly ? disabledBack : t.Surface;
            _value.BackColor = _value.Enabled && !_value.ReadOnly ? t.Surface : disabledBack;
            _group.BackColor = _group.Enabled ? t.Surface : disabledBack;
            _partner.BackColor = _partner.Enabled ? t.Surface : disabledBack;
            _note.BackColor = _note.ReadOnly ? disabledBack : t.Surface;

            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }

        private static void ApplyDateTheme(DateBox box, ThemeTokens t, Color disabledBack)
        {
            box.ApplyTheme(t, t.Background);
            if (!box.Enabled)
            {
                box.FieldColor = disabledBack;
                box.Inner.BackColor = disabledBack;
                box.Invalidate();
            }
        }
    }
}
