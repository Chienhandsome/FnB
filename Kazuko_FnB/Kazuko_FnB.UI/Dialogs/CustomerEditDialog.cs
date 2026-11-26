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
    /// Dialog Thêm/Sửa/Xem khách hàng (spec docs/specs/danh-muc-khach-hang.md §4.1, §5, §9.0, §9.0.2).
    /// Bố cục 2 cột đủ 15 trường POS TAP (không CMND), không cuộn dọc ở 1366×768 (100/125/150% DPI).
    /// Ô "Giá bán" khóa khi SuDungGiaTheoKhach = 0; "Thẻ trả trước" khóa khi SuDungTheTraTruoc = 0.
    /// Chế độ chỉ đọc (Q10): mọi ô khóa, ẩn các nút Lưu. Phần bố cục &amp; theme nằm ở <c>CustomerEditDialog.Layout.cs</c>.
    /// </summary>
    internal sealed partial class CustomerEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 780;
        private const int BodyHeight96 = 316;

        private readonly string _databasePath;
        private readonly CustomerRepository _repo;
        private readonly FormRepository _formRepo;
        private readonly bool _readOnly;

        private CustomerRow _row;
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private string _presetGroupId;

        private readonly TextBox _code = new TextBox { MaxLength = CustomerText.MaxTextLength };
        private readonly TextBox _name = new TextBox { MaxLength = CustomerText.MaxTextLength };
        private readonly LookupCombo _group = new LookupCombo();
        private readonly LookupCombo _employee = new LookupCombo();
        private readonly TextBox _phone = new TextBox { MaxLength = CustomerText.MaxTextLength };
        private readonly TextBox _email = new TextBox { MaxLength = CustomerText.MaxTextLength };
        private readonly TextBox _address = new TextBox { MaxLength = CustomerText.MaxTextLength };
        private readonly LookupCombo _province = new LookupCombo();
        private readonly TextBox _taxCode = new TextBox { MaxLength = CustomerText.MaxTextLength };
        private readonly TextBox _facebook = new TextBox { MaxLength = CustomerText.MaxTextLength };
        private readonly DateBox _birthday = new DateBox(true);
        private readonly NumberBox _points = new NumberBox();
        private readonly ComboBox _priceType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly LookupCombo _prepaidCard = new LookupCombo();
        private readonly TextBox _note = new TextBox { Multiline = true, MaxLength = CustomerText.MaxTextLength, ScrollBars = ScrollBars.Vertical };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };
        private Font _sectionFont;

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnClose;

        /// <summary>ID khách hàng đã lưu (null nếu chưa lưu).</summary>
        public string SavedId { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal TextBox CodeBoxForTest { get { return _code; } }
        internal TextBox NameBoxForTest { get { return _name; } }
        internal LookupCombo GroupComboForTest { get { return _group; } }
        internal LookupCombo EmployeeComboForTest { get { return _employee; } }
        internal TextBox PhoneBoxForTest { get { return _phone; } }
        internal TextBox EmailBoxForTest { get { return _email; } }
        internal TextBox AddressBoxForTest { get { return _address; } }
        internal LookupCombo ProvinceComboForTest { get { return _province; } }
        internal TextBox TaxCodeBoxForTest { get { return _taxCode; } }
        internal TextBox FacebookBoxForTest { get { return _facebook; } }
        internal DateBox BirthdayBoxForTest { get { return _birthday; } }
        internal NumberBox PointsBoxForTest { get { return _points; } }
        internal ComboBox PriceTypeComboForTest { get { return _priceType; } }
        internal LookupCombo PrepaidCardComboForTest { get { return _prepaidCard; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }

        public CustomerEditDialog(UserSession session, string customerId, string presetGroupId, bool readOnly = false)
            : base("KHÁCH HÀNG - THÊM MỚI", "Thông tin khách hàng", BodyWidth96, 440)
        {
            _databasePath = session.DatabasePath;
            _repo = new CustomerRepository(_databasePath);
            _formRepo = new FormRepository(_databasePath);
            _presetGroupId = presetGroupId;
            _isNew = string.IsNullOrEmpty(customerId);
            _readOnly = readOnly || (_isNew ? !CustomerRules.Can(session, RoleModes.Add) : !CustomerRules.Can(session, RoleModes.Edit));

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
                LoadExistingRecord(customerId);

            ApplyPermissionsAndConfig();
            HookChangeEvents();
            _original = Snapshot();
            UpdateButtons();

            FormClosing += OnFormClosing;
            Shown += delegate
            {
                if (_loadFailed)
                {
                    if (!SuppressPopup)
                        Msg.Error(this, "Khách hàng không còn tồn tại.");
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
            _group.SetItems(SafeLoadLookup("DNHOMKHACHHANG"));
            _employee.SetItems(SafeLoadLookup("DNHANVIEN"));
            _province.SetItems(SafeLoadLookup("DTINHTHANH"));
            _prepaidCard.SetItems(SafeLoadLookup("DTHETRATRUOC"));
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
            _row = CustomerRow.NewDefault();
            _row.Code = _repo.NextCode();
            _row.GroupId = string.IsNullOrEmpty(presetGroupId) ? null : presetGroupId;
            SetTitle("KHÁCH HÀNG - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string customerId)
        {
            _isNew = false;
            _row = _repo.LoadCustomer(customerId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = CustomerRow.NewDefault();
                return;
            }
            SetTitle(_readOnly ? "KHÁCH HÀNG - XEM CHI TIẾT" : "KHÁCH HÀNG - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _code.Text = _row.Code ?? "";
                _name.Text = _row.Name ?? "";
                _group.SelectId(_row.GroupId);
                _employee.SelectId(_row.EmployeeId);
                _phone.Text = _row.Phone ?? "";
                _email.Text = _row.Email ?? "";
                _address.Text = _row.Address ?? "";
                _province.SelectId(_row.ProvinceId);
                _taxCode.Text = _row.TaxCode ?? "";
                _facebook.Text = _row.Facebook ?? "";
                _birthday.NullableValue = _row.Birthday;
                int pts = _row.InitialPoints < 0 ? 0 : _row.InitialPoints;
                _points.Value = pts;
                int priceIdx = _row.PriceType >= 0 && _row.PriceType < _priceType.Items.Count ? _row.PriceType : 0;
                _priceType.SelectedIndex = priceIdx;
                _prepaidCard.SelectId(_row.PrepaidCardId);
                _note.Text = _row.Note ?? "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private void ApplyPermissionsAndConfig()
        {
            bool allowPrice = !_readOnly && SystemConfig.SuDungGiaTheoKhach;
            bool allowCard = !_readOnly && SystemConfig.SuDungTheTraTruoc;

            _code.ReadOnly = _readOnly;
            _name.ReadOnly = _readOnly;
            _group.Enabled = !_readOnly;
            _employee.Enabled = !_readOnly;
            _phone.ReadOnly = _readOnly;
            _email.ReadOnly = _readOnly;
            _address.ReadOnly = _readOnly;
            _province.Enabled = !_readOnly;
            _taxCode.ReadOnly = _readOnly;
            _facebook.ReadOnly = _readOnly;
            _birthday.Enabled = !_readOnly;
            _birthday.Inner.ReadOnly = _readOnly;
            _points.ReadOnly = _readOnly;
            _points.Enabled = !_readOnly;
            _priceType.Enabled = allowPrice;
            _prepaidCard.Enabled = allowCard;
            _note.ReadOnly = _readOnly;

            // §9.0.2 Q10: chế độ chỉ đọc không có nút Lưu.
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
            _group.SelectedIndexChanged += onChanged;
            _employee.SelectedIndexChanged += onChanged;
            _phone.TextChanged += onChanged;
            _email.TextChanged += onChanged;
            _address.TextChanged += onChanged;
            _province.SelectedIndexChanged += onChanged;
            _taxCode.TextChanged += onChanged;
            _facebook.TextChanged += onChanged;
            _birthday.ValueChanged += onChanged;
            _points.ValueChanged += onChanged;
            _priceType.SelectedIndexChanged += onChanged;
            _prepaidCard.SelectedIndexChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_code.Text).Append('|');
            sb.Append(_name.Text).Append('|');
            sb.Append(_group.SelectedId).Append('|');
            sb.Append(_employee.SelectedId).Append('|');
            sb.Append(_phone.Text).Append('|');
            sb.Append(_email.Text).Append('|');
            sb.Append(_address.Text).Append('|');
            sb.Append(_province.SelectedId).Append('|');
            sb.Append(_taxCode.Text).Append('|');
            sb.Append(_facebook.Text).Append('|');
            sb.Append(_birthday.NullableValue.HasValue ? _birthday.NullableValue.Value.ToString("yyyy-MM-dd") : "").Append('|');
            sb.Append(_points.Value).Append('|');
            sb.Append(_priceType.SelectedIndex).Append('|');
            sb.Append(_prepaidCard.SelectedId).Append('|');
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
            ApplyPermissionsAndConfig();
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

            _birthday.Commit();
            decimal rawPoints = _points.Value;
            if (!CustomerRules.IsValidInitialPoints(rawPoints))
            {
                ShowValidationError(CustomerText.PointsInvalid);
                _points.Focus();
                return false;
            }

            _row.Code = _code.Text ?? "";
            _row.Name = _name.Text ?? "";
            _row.GroupId = _group.SelectedId;
            _row.EmployeeId = _employee.SelectedId;
            _row.Phone = _phone.Text ?? "";
            _row.Email = _email.Text ?? "";
            _row.Address = _address.Text ?? "";
            _row.ProvinceId = _province.SelectedId;
            _row.TaxCode = _taxCode.Text ?? "";
            _row.Facebook = _facebook.Text ?? "";
            _row.Birthday = _birthday.NullableValue;
            _row.InitialPoints = (int)rawPoints;
            if (_priceType.Enabled)
                _row.PriceType = _priceType.SelectedIndex >= 0 ? _priceType.SelectedIndex : 0;
            else if (_isNew)
                _row.PriceType = 0;
            if (_prepaidCard.Enabled)
                _row.PrepaidCardId = _prepaidCard.SelectedId;
            else if (_isNew)
                _row.PrepaidCardId = null;
            _row.Note = _note.Text ?? "";

            CustomerSaveResult res;
            try
            {
                var opts = new CustomerSaveOptions { AllowDuplicateName = SystemConfig.ChoPhepTrungTenKhachHang };
                res = _repo.SaveCustomer(_row, AppSession.UserId ?? "admin", opts);
            }
            catch (DataAccessException ex)
            {
                ShowValidationError("Không ghi được dữ liệu: " + ex.Message);
                return false;
            }

            if (!res.Ok)
            {
                ShowValidationError(res.ErrorMessage);
                if (res.HasNameError) { _name.Focus(); _name.SelectAll(); }
                else if (res.HasCodeError) { _code.Focus(); _code.SelectAll(); }
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            _isNew = false;
            SetTitle("KHÁCH HÀNG - SỬA");
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
    }
}
