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
    /// Hộp thoại Thêm / Sửa / Xem chi tiết Danh mục nhà cung cấp (DNHACUNGCAP,
    /// spec docs/specs/danh-muc-nha-cung-cap/).
    /// Bố cục 2 cột gọn đủ 8 trường theo layout.xml (Tên, Nhóm, Mã, Địa chỉ | Điện thoại, Email, Website, Ghi chú),
    /// đo nhãn bằng chữ thật, không cuộn dọc ở 1366×768 (100–150% DPI), chế độ chỉ đọc khi không có quyền sửa.
    /// </summary>
    internal sealed class SupplierEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 760;
        private const int BodyHeight96 = 256;

        private readonly UserSession _session;
        private readonly SupplierService _service;
        private readonly FormRepository _formRepo;
        private readonly bool _readOnly;
        private readonly string _presetGroupId;

        private SupplierRow _row;
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();
        private Font _sectionFont;

        private readonly TextBox _name = new TextBox { MaxLength = SupplierText.MaxTextLength };
        private readonly LookupCombo _group = new LookupCombo();
        private readonly TextBox _code = new TextBox { MaxLength = SupplierText.MaxTextLength };
        private readonly TextBox _address = new TextBox { MaxLength = SupplierText.MaxTextLength };
        private readonly TextBox _phone = new TextBox { MaxLength = SupplierText.MaxTextLength };
        private readonly TextBox _email = new TextBox { MaxLength = SupplierText.MaxTextLength };
        private readonly TextBox _website = new TextBox { MaxLength = SupplierText.MaxTextLength };
        private readonly TextBox _note = new TextBox
        {
            Multiline = true,
            MaxLength = SupplierText.MaxTextLength,
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
        internal TextBox NameBoxForTest { get { return _name; } }
        internal LookupCombo GroupComboForTest { get { return _group; } }
        internal TextBox CodeBoxForTest { get { return _code; } }
        internal TextBox AddressBoxForTest { get { return _address; } }
        internal TextBox PhoneBoxForTest { get { return _phone; } }
        internal TextBox EmailBoxForTest { get { return _email; } }
        internal TextBox WebsiteBoxForTest { get { return _website; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        public SupplierEditDialog(UserSession session, string supplierId, string presetGroupId, bool readOnly = false)
            : base("NHÀ CUNG CẤP - THÊM MỚI", "Thông tin nhà cung cấp", BodyWidth96, 372)
        {
            _session = session ?? AppSession.Current;
            _service = new SupplierService(_session);
            _formRepo = new FormRepository(_session.DatabasePath);
            _presetGroupId = presetGroupId;
            _isNew = string.IsNullOrEmpty(supplierId);
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
                LoadExistingRecord(supplierId);

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
                    ShowValidationError(SupplierText.NotFound);
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
            _sectionFont = new Font(ThemeManager.HeadingFontName, ThemeManager.SmallFontSize);

            string[] leftCaps = { "Tên nhà cung cấp *", "Nhóm nhà cung cấp", "Mã nhà cung cấp *", "Địa chỉ" };
            string[] rightCaps = { "Điện thoại", "Email", "Website", "Ghi chú" };

            int pad = (int)(14 * s);
            int colGap = (int)(18 * s);
            int fieldGap = (int)(6 * s);
            int halfW = (Body.ClientSize.Width - 2 * pad - colGap) / 2;
            int leftX = pad;
            int rightX = pad + halfW + colGap;

            int leftLabelW = MeasureLabelWidth(leftCaps, (int)(136 * s));
            int rightLabelW = MeasureLabelWidth(rightCaps, (int)(92 * s));
            int leftEditW = Math.Max((int)(140 * s), halfW - leftLabelW - fieldGap);
            int rightEditW = Math.Max((int)(140 * s), Body.ClientSize.Width - pad - (rightX + rightLabelW + fieldGap));

            int tab = 0;
            AddSection("Định danh", leftX, (int)(8 * s), halfW);
            PlaceRow(leftCaps[0], _name, leftX, (int)(32 * s), leftLabelW, leftX + leftLabelW + fieldGap, leftEditW, (int)(26 * s), ref tab);
            PlaceRow(leftCaps[1], _group, leftX, (int)(66 * s), leftLabelW, leftX + leftLabelW + fieldGap, leftEditW, (int)(26 * s), ref tab);
            PlaceRow(leftCaps[2], _code, leftX, (int)(100 * s), leftLabelW, leftX + leftLabelW + fieldGap, leftEditW, (int)(26 * s), ref tab);
            PlaceRow(leftCaps[3], _address, leftX, (int)(134 * s), leftLabelW, leftX + leftLabelW + fieldGap, leftEditW, (int)(26 * s), ref tab);

            AddSection("Liên hệ & ghi chú", rightX, (int)(8 * s), halfW);
            PlaceRow(rightCaps[0], _phone, rightX, (int)(32 * s), rightLabelW, rightX + rightLabelW + fieldGap, rightEditW, (int)(26 * s), ref tab);
            PlaceRow(rightCaps[1], _email, rightX, (int)(66 * s), rightLabelW, rightX + rightLabelW + fieldGap, rightEditW, (int)(26 * s), ref tab);
            PlaceRow(rightCaps[2], _website, rightX, (int)(100 * s), rightLabelW, rightX + rightLabelW + fieldGap, rightEditW, (int)(26 * s), ref tab);
            PlaceRow(rightCaps[3], _note, rightX, (int)(134 * s), rightLabelW, rightX + rightLabelW + fieldGap, rightEditW, (int)(74 * s), ref tab);

            _lblError.SetBounds(pad, (int)(220 * s), Body.ClientSize.Width - 2 * pad, (int)(26 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private int MeasureLabelWidth(string[] captions, int minWidth)
        {
            int maxW = minWidth;
            int pad = (int)(12 * Dpi);
            foreach (string cap in captions)
            {
                int need = TextRenderer.MeasureText(cap, Font).Width + pad;
                if (need > maxW)
                    maxW = need;
            }
            return maxW;
        }

        private void AddSection(string title, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = title,
                AutoSize = false,
                Font = _sectionFont,
                Tag = "muted",
                UseMnemonic = false,
                TabStop = false
            };
            lbl.SetBounds(x, y, width, (int)(18 * Dpi));
            Body.Controls.Add(lbl);
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
            try
            {
                _group.SetItems(_formRepo.LoadAll(SupplierText.GroupTableName));
            }
            catch (Exception)
            {
                _group.SetItems(new List<LookupItem>());
            }
        }

        private void InitNewRecord(string presetGroupId)
        {
            _isNew = true;
            _row = SupplierRow.NewDefault();
            _row.Code = _service.NextCode();
            _row.GroupId = string.IsNullOrEmpty(presetGroupId) ? null : presetGroupId;
            SetTitle("NHÀ CUNG CẤP - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string supplierId)
        {
            _isNew = false;
            _row = _service.LoadById(supplierId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = SupplierRow.NewDefault();
                return;
            }
            SetTitle(_readOnly ? "NHÀ CUNG CẤP - XEM CHI TIẾT" : "NHÀ CUNG CẤP - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _name.Text = _row.Name ?? "";
                _group.SelectId(_row.GroupId);
                _code.Text = _row.Code ?? "";
                _address.Text = _row.Address ?? "";
                _phone.Text = _row.Phone ?? "";
                _email.Text = _row.Email ?? "";
                _website.Text = _row.Website ?? "";
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
            _name.ReadOnly = _readOnly;
            _group.Enabled = !_readOnly;
            _code.ReadOnly = _readOnly;
            _address.ReadOnly = _readOnly;
            _phone.ReadOnly = _readOnly;
            _email.ReadOnly = _readOnly;
            _website.ReadOnly = _readOnly;
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
            _group.SelectedIndexChanged += onChanged;
            _group.TextChanged += onChanged;
            _code.TextChanged += onChanged;
            _address.TextChanged += onChanged;
            _phone.TextChanged += onChanged;
            _email.TextChanged += onChanged;
            _website.TextChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_name.Text).Append('|');
            sb.Append(_group.SelectedId).Append('|');
            sb.Append(_code.Text).Append('|');
            sb.Append(_address.Text).Append('|');
            sb.Append(_phone.Text).Append('|');
            sb.Append(_email.Text).Append('|');
            sb.Append(_website.Text).Append('|');
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
            InitNewRecord(_presetGroupId);
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
            _row.GroupId = _group.SelectedId;
            _row.Code = _code.Text ?? "";
            _row.Address = _address.Text ?? "";
            _row.Phone = _phone.Text ?? "";
            _row.Email = _email.Text ?? "";
            _row.Website = _website.Text ?? "";
            _row.Note = _note.Text ?? "";

            SupplierSaveResult res;
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
                if (res.HasNameError)
                {
                    _name.Focus();
                    _name.SelectAll();
                }
                else if (res.HasCodeError)
                {
                    _code.Focus();
                    _code.SelectAll();
                }
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;
            _isNew = false;
            SetTitle("NHÀ CUNG CẤP - SỬA");
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

            _group.BackColor = _group.Enabled ? t.Surface : disabledBack;
            _group.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _sectionFont != null)
                _sectionFont.Dispose();
            base.Dispose(disposing);
        }
    }
}
