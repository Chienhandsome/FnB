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
    /// Hộp thoại Thêm / Sửa / Xem chi tiết nhóm nhà cung cấp (DNHOMNHACUNGCAP,
    /// spec docs/specs/danh-muc-nha-cung-cap/).
    /// Tên nhóm bắt buộc, không trùng (UNICODE_CI_AI); hỗ trợ chế độ chỉ đọc khi không có quyền sửa;
    /// gọn, đo nhãn bằng chữ thật, không cuộn dọc ở 1366×768 (100–150% DPI).
    /// </summary>
    internal sealed class SupplierGroupEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 520;
        private const int BodyHeight96 = 212;

        private readonly UserSession _session;
        private readonly SupplierGroupService _service;
        private readonly string _parentId;
        private readonly bool _readOnly;

        private SupplierGroupRow _row;
        private bool _isNew;
        private bool _loading;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _name = new TextBox { MaxLength = SupplierText.MaxTextLength };
        private readonly ImageCombo _image = new ImageCombo();
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
        internal ImageCombo ImageComboForTest { get { return _image; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        public SupplierGroupEditDialog(UserSession session, SupplierGroupRow existing, string parentId, bool readOnly = false)
            : base("NHÓM NHÀ CUNG CẤP - THÊM MỚI", "Thông tin nhóm nhà cung cấp", BodyWidth96, 328)
        {
            _session = session ?? AppSession.Current;
            _service = new SupplierGroupService(_session);
            _parentId = parentId;
            _isNew = existing == null || string.IsNullOrEmpty(existing.Id);
            _row = existing ?? SupplierGroupRow.NewDefault();
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildFields();
            LoadImages();
            BindRowToControls();
            ApplyPermissions();
            HookChangeEvents();
            _original = Snapshot();
            UpdateButtons();
            ApplyDialogTheme();

            FormClosing += OnFormClosing;
            Shown += delegate
            {
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
            string[] captions = { "Tên nhóm nhà cung cấp *", "Ảnh", "Ghi chú" };
            int maxCap = (int)(150 * s);
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

            int tab = 0;
            PlaceRow(captions[0], _name, leftPad, (int)(14 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[1], _image, leftPad, (int)(48 * s), maxCap, editX, editW, (int)(28 * s), ref tab);
            PlaceRow(captions[2], _note, leftPad, (int)(84 * s), maxCap, editX, editW, (int)(84 * s), ref tab);

            _lblError.SetBounds(leftPad, (int)(178 * s), Body.ClientSize.Width - 2 * leftPad, (int)(24 * s));
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

        private void LoadImages()
        {
            try
            {
                var imgRepo = new ImageRepository(_session.DatabasePath);
                _image.Load(imgRepo.LoadAll(), (int)(20 * Dpi));
            }
            catch (Exception)
            {
                _image.Load(new List<ImageRecord>(), (int)(20 * Dpi));
            }
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                SetTitle(_readOnly ? "NHÓM NHÀ CUNG CẤP - XEM CHI TIẾT"
                    : (_isNew ? "NHÓM NHÀ CUNG CẤP - THÊM MỚI" : "NHÓM NHÀ CUNG CẤP - SỬA"));
                _name.Text = _row.Name ?? "";
                _image.SelectedId = _row.ImageId ?? "";
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
            _image.Enabled = !_readOnly;
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
            _image.SelectedIndexChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_name.Text).Append('|');
            sb.Append(_image.SelectedId).Append('|');
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
            _isNew = true;
            _row = SupplierGroupRow.NewDefault();
            BindRowToControls();
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
            _row.ImageId = string.IsNullOrEmpty(_image.SelectedId) ? null : _image.SelectedId;
            _row.Note = _note.Text ?? "";
            if (_isNew)
                _row.ParentId = _parentId ?? "";

            SupplierGroupSaveResult res;
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
                _name.Focus();
                _name.SelectAll();
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;
            _isNew = false;
            SetTitle("NHÓM NHÀ CUNG CẤP - SỬA");
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

            _image.BackColor = _image.Enabled ? t.Surface : disabledBack;
            _image.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
