using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem Danh mục lý do thu chi (DLYDOTHUCHI, docs/specs/phieu-thu-chi.md §3).
    /// Kiểm trùng tên (UNICODE_CI_AI); khóa sửa LOAILYDO với lý do thu và lý do hệ thống.
    /// </summary>
    internal sealed class CashReasonEditDialog : ThemedDialog
    {
        private sealed class ReasonTypeItem
        {
            public int? Value;
            public string Text;
            public override string ToString() { return Text; }
        }

        private readonly UserSession _session;
        private readonly CashReasonRepository _repo;
        private readonly bool _readOnly;

        private CashReasonRow _row;
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;

        private readonly TextBox _name = new TextBox { MaxLength = VoucherConstants.MaxTextLength };
        private readonly ComboBox _kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _reasonType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _note = new TextBox { Multiline = true, MaxLength = VoucherConstants.MaxTextLength, ScrollBars = ScrollBars.Vertical };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnClose;

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal TextBox NameBoxForTest { get { return _name; } }
        internal ComboBox KindComboForTest { get { return _kind; } }
        internal ComboBox ReasonTypeComboForTest { get { return _reasonType; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }

        public CashReasonEditDialog(UserSession session, string reasonId, bool readOnly = false)
            : base("LÝ DO THU CHI - THÊM MỚI", "Thông tin lý do thu chi", 500, 320)
        {
            _session = session ?? AppSession.Current;
            _repo = new CashReasonRepository(_session.DatabasePath);
            _isNew = string.IsNullOrEmpty(reasonId);
            _readOnly = readOnly || (_isNew
                ? !VoucherRules.CanReasonCatalog(_session, RoleModes.Add)
                : !VoucherRules.CanReasonCatalog(_session, RoleModes.Edit));

            FitBody(500, 212);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { Save(true); });
            _btnSave = AddButton("Lưu", "primary", delegate { Save(false); });
            CancelButton = _btnClose;

            BuildFields();

            if (_isNew)
                InitNewRecord();
            else
                LoadExistingRecord(reasonId);

            ApplyState();
            ApplyDialogTheme();

            Shown += delegate
            {
                if (_loadFailed)
                {
                    ShowError(VoucherConstants.ReasonNotFound);
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
            _kind.Items.Add("Lý do chi");
            _kind.Items.Add("Lý do thu");
            _kind.SelectedIndexChanged += delegate
            {
                if (!_loading)
                    UpdateReasonTypeEnabled();
            };

            FillReasonTypes(null);

            int tab = 0;
            PlaceRow("Tên lý do *", _name, 14, 26, ref tab, s);
            PlaceRow("Loại lý do", _kind, 50, 26, ref tab, s);
            PlaceRow("Phân loại KQKD", _reasonType, 86, 26, ref tab, s);
            PlaceRow("Ghi chú", _note, 122, 56, ref tab, s);

            _lblError.SetBounds((int)(14 * s), (int)(184 * s), (int)(472 * s), (int)(22 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private void PlaceRow(string caption, Control editor, int y, int h, ref int tabIndex, float s)
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
            lbl.SetBounds((int)(14 * s), (int)(y * s), (int)(130 * s), (int)(Math.Min(h, 26) * s));
            editor.SetBounds((int)(148 * s), (int)(y * s), (int)(338 * s), (int)(h * s));
            editor.TabIndex = tabIndex++;
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
        }

        private void FillReasonTypes(int? existingValue)
        {
            _reasonType.Items.Clear();
            var items = new List<ReasonTypeItem>
            {
                new ReasonTypeItem { Value = -1, Text = VoucherConstants.ReasonTypeLabel(-1) },
                new ReasonTypeItem { Value = 1, Text = VoucherConstants.ReasonTypeLabel(1) },
                new ReasonTypeItem { Value = 2, Text = VoucherConstants.ReasonTypeLabel(2) }
            };
            if (existingValue.HasValue && existingValue.Value != -1 && existingValue.Value != 1 && existingValue.Value != 2)
                items.Add(new ReasonTypeItem { Value = existingValue.Value, Text = VoucherConstants.ReasonTypeLabel(existingValue.Value) });

            foreach (ReasonTypeItem it in items)
                _reasonType.Items.Add(it);
        }

        private void SelectReasonType(int? value)
        {
            int target = value ?? -1;
            for (int i = 0; i < _reasonType.Items.Count; i++)
            {
                var it = (ReasonTypeItem)_reasonType.Items[i];
                if ((it.Value ?? -1) == target)
                {
                    _reasonType.SelectedIndex = i;
                    return;
                }
            }
            if (_reasonType.Items.Count > 0)
                _reasonType.SelectedIndex = 0;
        }

        private int? SelectedReasonType()
        {
            var it = _reasonType.SelectedItem as ReasonTypeItem;
            return it == null ? (int?)(-1) : it.Value;
        }

        private void InitNewRecord()
        {
            _loading = true;
            try
            {
                _isNew = true;
                _row = new CashReasonRow { IsIncome = false, ReasonType = -1, Status = 30 };
                SetTitle("LÝ DO THU CHI - THÊM MỚI");
                FillReasonTypes(null);
                _name.Text = "";
                _kind.SelectedIndex = 0;
                SelectReasonType(-1);
                _note.Text = "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private void LoadExistingRecord(string reasonId)
        {
            _loading = true;
            try
            {
                _isNew = false;
                _row = _repo.LoadById(reasonId);
                if (_row == null)
                {
                    _loadFailed = true;
                    _row = new CashReasonRow();
                    return;
                }
                SetTitle(_readOnly ? "LÝ DO THU CHI - XEM CHI TIẾT" : "LÝ DO THU CHI - SỬA");
                FillReasonTypes(_row.ReasonType);
                _name.Text = _row.Name ?? "";
                _kind.SelectedIndex = _row.IsIncome ? 1 : 0;
                SelectReasonType(_row.ReasonType);
                _note.Text = _row.Note ?? "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private void ApplyState()
        {
            bool isSys = !_isNew && _row != null && _row.IsSystem;
            _name.ReadOnly = _readOnly;
            _kind.Enabled = !_readOnly && !isSys;
            _note.ReadOnly = _readOnly;
            UpdateReasonTypeEnabled();

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly;
            SubtitleText = _readOnly
                ? "Chế độ chỉ xem"
                : (isSys ? "Lý do hệ thống: không được đổi loại và phân loại KQKD."
                    : "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)");
        }

        private void UpdateReasonTypeEnabled()
        {
            bool isSys = !_isNew && _row != null && _row.IsSystem;
            bool isIncome = _kind.SelectedIndex == 1;
            if (isIncome && !isSys)
                SelectReasonType(-1);
            _reasonType.Enabled = !_readOnly && !isSys && !isIncome;
            ApplyDialogTheme();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Control | Keys.L))
                {
                    Save(false);
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S))
                {
                    Save(true);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Save(bool saveAndNew)
        {
            if (_readOnly)
                return;

            _row.Name = (_name.Text ?? "").Trim();
            if (!_row.IsSystem)
            {
                _row.IsIncome = _kind.SelectedIndex == 1;
                _row.ReasonType = _row.IsIncome ? -1 : SelectedReasonType();
            }
            _row.Note = (_note.Text ?? "").Trim();

            SimpleCatalogSaveResult res;
            try
            {
                res = _repo.Save(_row, _session.UserId ?? "admin");
            }
            catch (DataAccessException ex)
            {
                ShowError("Không ghi được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowError(res.ErrorMessage);
                _name.Focus();
                _name.SelectAll();
                return;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;

            if (saveAndNew)
            {
                InitNewRecord();
                ApplyState();
                _name.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowError(string message)
        {
            LastErrorForTest = message;
            _lblError.Text = message ?? "";
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

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);
            _kind.BackColor = _kind.Enabled ? t.Surface : disabledBack;
            _kind.ForeColor = t.Text;
            _reasonType.BackColor = _reasonType.Enabled ? t.Surface : disabledBack;
            _reasonType.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
