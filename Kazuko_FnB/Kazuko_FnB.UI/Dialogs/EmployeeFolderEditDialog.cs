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
    /// Hộp thoại Thêm / Sửa thư mục nhân viên (DNHANVIEN ITEMTYPE = 1, spec docs/specs/nhan-su-cay.md §2).
    /// Chỉ có ô Tên (bắt buộc, &lt;= 255, không trùng thư mục anh em cùng cha, không phân biệt hoa thường và dấu).
    /// </summary>
    internal sealed class EmployeeFolderEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 480;
        private const int BodyHeight96 = 94;

        private readonly UserSession _session;
        private readonly EmployeeService _service;
        private readonly string _parentId;
        private readonly bool _readOnly;

        private EmployeeRow _row;
        private bool _isNew;
        private bool _loading;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _name = new TextBox { MaxLength = EmployeeText.MaxTextLength };
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
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        public EmployeeFolderEditDialog(UserSession session, EmployeeRow existing, string parentId, bool readOnly = false)
            : base("THƯ MỤC NHÂN VIÊN - THÊM MỚI", "Thông tin thư mục nhân viên", BodyWidth96, 210)
        {
            _session = session ?? AppSession.Current;
            _service = new EmployeeService(_session);
            _parentId = string.IsNullOrEmpty(parentId) ? "" : parentId;
            _isNew = existing == null || string.IsNullOrEmpty(existing.Id);
            _row = existing ?? EmployeeRow.NewFolder(_parentId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildFields();
            BindRowToControls();
            ApplyPermissions();
            HookChangeEvents();
            _original = _name.Text ?? "";
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
            const string caption = "Tên thư mục *";
            int labelW = Math.Max((int)(115 * s), TextRenderer.MeasureText(caption, Font).Width + (int)(12 * s));
            int leftPad = (int)(14 * s);
            int gap = (int)(8 * s);
            int editX = leftPad + labelW + gap;
            int editW = Math.Max((int)(200 * s), Body.ClientSize.Width - editX - leftPad);
            int rowH = (int)(26 * s);

            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = false
            };
            lbl.SetBounds(leftPad, (int)(18 * s), labelW, rowH);
            _name.SetBounds(editX, (int)(18 * s), editW, rowH);
            _name.TabIndex = 0;
            _fieldLabels.Add(lbl);
            Body.Controls.Add(lbl);
            Body.Controls.Add(_name);

            _lblError.SetBounds(leftPad, (int)(54 * s), Body.ClientSize.Width - 2 * leftPad, (int)(26 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                SetTitle(_readOnly ? "THƯ MỤC NHÂN VIÊN - XEM CHI TIẾT"
                    : (_isNew ? "THƯ MỤC NHÂN VIÊN - THÊM MỚI" : "THƯ MỤC NHÂN VIÊN - SỬA"));
                _name.Text = _row.Name ?? "";
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
            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;
            SubtitleText = _readOnly
                ? "Chế độ chỉ xem (không có quyền sửa)"
                : "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
        }

        private void HookChangeEvents()
        {
            _name.TextChanged += delegate
            {
                if (_loading)
                    return;
                ClearError();
                UpdateButtons();
            };
        }

        private bool Dirty
        {
            get { return !_readOnly && (_name.Text ?? "") != _original; }
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
                if (keyData == Keys.Enter)
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
            _row = EmployeeRow.NewFolder(_parentId);
            BindRowToControls();
            ApplyPermissions();
            ApplyDialogTheme();
            _original = _name.Text ?? "";
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
            if (_isNew)
                _row.ParentId = _parentId;

            EmployeeSaveResult res;
            try
            {
                res = _service.SaveFolder(_row);
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
            SetTitle("THƯ MỤC NHÂN VIÊN - SỬA");
            _original = _name.Text ?? "";
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
            if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
            if (answer == DialogResult.Yes && !DoSave()) e.Cancel = true;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);
            _name.BackColor = _name.ReadOnly ? disabledBack : t.Surface;
            _name.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
