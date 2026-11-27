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
    /// Hộp thoại Thêm / Sửa / Xem chi tiết Danh mục cửa hàng (DCUAHANG, spec docs/specs/danh-muc-cua-hang/).
    /// Thứ tự trường theo giao-dien.md: Mã cửa hàng, Tên cửa hàng, Địa chỉ, Điện thoại, Kho mặc định,
    /// Mẫu hóa đơn (chỉ SFORMID = 5f029a5b-8ec2-4229-bd1d-03fed9569db2), Ghi chú.
    /// Không làm ô nhập Ảnh (SIMAGEID giữ nguyên giá trị cũ khi sửa). Chế độ chỉ đọc khi chỉ có quyền Xem (Q4).
    /// Gọn, không cuộn dọc ở 1366×768 (100–150% DPI), đo nhãn bằng chữ thật để không bị cắt.
    /// </summary>
    internal sealed class StoreEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 540;
        private const int BodyHeight96 = 324;

        private readonly UserSession _session;
        private readonly StoreService _service;
        private readonly bool _readOnly;

        private StoreRow _row;
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _code = new TextBox { MaxLength = StoreText.MaxCodeLength };
        private readonly TextBox _name = new TextBox { MaxLength = StoreText.MaxTextLength };
        private readonly TextBox _address = new TextBox { MaxLength = StoreText.MaxTextLength };
        private readonly TextBox _phone = new TextBox { MaxLength = StoreText.MaxTextLength };
        private readonly LookupCombo _warehouse = new LookupCombo();
        private readonly LookupCombo _template = new LookupCombo();
        private readonly TextBox _note = new TextBox
        {
            Multiline = true,
            MaxLength = StoreText.MaxTextLength,
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
        internal TextBox CodeBoxForTest { get { return _code; } }
        internal TextBox NameBoxForTest { get { return _name; } }
        internal TextBox AddressBoxForTest { get { return _address; } }
        internal TextBox PhoneBoxForTest { get { return _phone; } }
        internal LookupCombo WarehouseComboForTest { get { return _warehouse; } }
        internal LookupCombo TemplateComboForTest { get { return _template; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        public StoreEditDialog(UserSession session, string storeId, bool readOnly = false)
            : base("CỬA HÀNG - THÊM MỚI", "Thông tin cửa hàng", BodyWidth96, 440)
        {
            _session = session ?? AppSession.Current;
            _service = new StoreService(_session);
            _isNew = string.IsNullOrEmpty(storeId);
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
                InitNewRecord();
            else
                LoadExistingRecord(storeId);

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
                    ShowValidationError(StoreText.NotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                {
                    _code.Focus();
                    _code.SelectAll();
                }
            };
        }

        private void BuildFields()
        {
            float s = Dpi;
            string[] captions =
            {
                "Mã cửa hàng *",
                "Tên cửa hàng *",
                "Địa chỉ",
                "Điện thoại",
                "Kho mặc định",
                "Mẫu hóa đơn",
                "Ghi chú"
            };
            int maxCap = (int)(120 * s);
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
            PlaceRow(captions[1], _name, leftPad, (int)(46 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[2], _address, leftPad, (int)(80 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[3], _phone, leftPad, (int)(114 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[4], _warehouse, leftPad, (int)(148 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[5], _template, leftPad, (int)(182 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[6], _note, leftPad, (int)(216 * s), maxCap, editX, editW, (int)(68 * s), ref tab);

            _lblError.SetBounds(leftPad, (int)(292 * s), Body.ClientSize.Width - 2 * leftPad, (int)(24 * s));
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
            var warehouses = new List<LookupItem>();
            warehouses.Add(new LookupItem { Id = null, Name = "(Không chọn)" });
            try
            {
                warehouses.AddRange(_service.LoadActiveWarehouses());
            }
            catch (Exception)
            {
                // Bỏ qua nếu không tải được danh sách kho.
            }
            _warehouse.SetItems(warehouses);

            var templates = new List<LookupItem>();
            templates.Add(new LookupItem { Id = null, Name = "(Không chọn)" });
            try
            {
                templates.AddRange(_service.LoadInvoiceTemplates());
            }
            catch (Exception)
            {
                // Bỏ qua nếu không tải được danh sách mẫu hóa đơn.
            }
            _template.SetItems(templates);
        }

        private void InitNewRecord()
        {
            _isNew = true;
            _row = StoreRow.NewDefault();
            SetTitle("CỬA HÀNG - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string storeId)
        {
            _isNew = false;
            _row = _service.LoadById(storeId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = StoreRow.NewDefault();
                return;
            }
            SetTitle(_readOnly ? "CỬA HÀNG - XEM CHI TIẾT" : "CỬA HÀNG - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _code.Text = _row.Code ?? "";
                _name.Text = _row.Name ?? "";
                _address.Text = _row.Address ?? "";
                _phone.Text = _row.Phone ?? "";
                _warehouse.SelectId(string.IsNullOrEmpty(_row.WarehouseId) ? null : _row.WarehouseId);
                _template.SelectId(string.IsNullOrEmpty(_row.TemplateId) ? null : _row.TemplateId);
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
            _code.ReadOnly = _readOnly;
            _name.ReadOnly = _readOnly;
            _address.ReadOnly = _readOnly;
            _phone.ReadOnly = _readOnly;
            _warehouse.Enabled = !_readOnly;
            _template.Enabled = !_readOnly;
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
            _code.TextChanged += onChanged;
            _name.TextChanged += onChanged;
            _address.TextChanged += onChanged;
            _phone.TextChanged += onChanged;
            _warehouse.SelectedIndexChanged += onChanged;
            _warehouse.TextChanged += onChanged;
            _template.SelectedIndexChanged += onChanged;
            _template.TextChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_code.Text).Append('|');
            sb.Append(_name.Text).Append('|');
            sb.Append(_address.Text).Append('|');
            sb.Append(_phone.Text).Append('|');
            sb.Append(_warehouse.SelectedId).Append('|');
            sb.Append(_template.SelectedId).Append('|');
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

            _row.Code = _code.Text ?? "";
            _row.Name = _name.Text ?? "";
            _row.Address = _address.Text ?? "";
            _row.Phone = _phone.Text ?? "";
            _row.WarehouseId = string.IsNullOrEmpty(_warehouse.SelectedId) ? null : _warehouse.SelectedId;
            _row.TemplateId = string.IsNullOrEmpty(_template.SelectedId) ? null : _template.SelectedId;
            _row.Note = _note.Text ?? "";

            StoreSaveResult res;
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
            SetTitle("CỬA HÀNG - SỬA");
            _original = Snapshot();
            UpdateButtons();
            return true;
        }

        private void FocusErrorField(StoreSaveResult res)
        {
            if (res.HasCodeError)
            {
                _code.Focus();
                _code.SelectAll();
            }
            else if (res.HasNameError)
            {
                _name.Focus();
                _name.SelectAll();
            }
            else if (res.HasAddressError)
            {
                _address.Focus();
                _address.SelectAll();
            }
            else if (res.HasPhoneError)
            {
                _phone.Focus();
                _phone.SelectAll();
            }
            else if (res.HasNoteError)
            {
                _note.Focus();
                _note.SelectAll();
            }
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

            _warehouse.BackColor = _warehouse.Enabled ? t.Surface : disabledBack;
            _warehouse.ForeColor = t.Text;
            _template.BackColor = _template.Enabled ? t.Surface : disabledBack;
            _template.ForeColor = t.Text;

            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
