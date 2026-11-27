using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Tạo / Sửa / Xem Phiếu thu và Phiếu chi (docs/specs/phieu-thu-chi.md §1–§2),
    /// dựng từ layout SFORM gốc ("Phiếu thu" / "Phiếu chi") và ghi thật xuống TTHUCHI qua <see cref="VoucherService"/>.
    /// Chế độ đặt tiền trước (constructor có <see cref="PhieuThuDepositPreset"/>) giữ nguyên lưu cọc qua <see cref="PhieuThuRepository"/>.
    /// Phần thao tác lưu, nạp phiếu và kiểm thử đặt ở <c>PhieuThuDialog.Actions.cs</c>.
    /// </summary>
    internal sealed partial class PhieuThuDialog : ThemedDialog, IScreenPreview
    {
        public const string FormName = VoucherConstants.IncomeFormName;
        public const string DateFunction = VoucherConstants.FunctionDateChange;

        private readonly UserSession _session;
        private readonly FormRepository _repo;
        private readonly VoucherService _voucherService;
        private readonly PhieuThuRepository _phieuThuRepo;
        private readonly VoucherKind _kind;
        private readonly ScreenLayout _layout;
        private readonly DynamicForm _form;

        private readonly PhieuThuDepositPreset _depositPreset;
        private readonly bool _isDepositMode;
        private bool _isNew;
        private bool _readOnly;
        private bool _loading;
        private bool _loadFailed;
        private string _editingId;
        private string _linkedWarning;

        private readonly ComboBox _objectType;
        private readonly TextBox _partyName;
        private readonly TextBox _partyAddress;
        private readonly TextBox _number;
        private readonly LookupCombo _employee;
        private readonly LookupCombo _customer;
        private readonly LookupCombo _supplier;
        private readonly LookupCombo _bank;
        private readonly LookupCombo _store;
        private readonly LookupCombo _reason;
        private readonly CheckBox _transfer;
        private readonly DateTimePicker _date;
        private readonly NumericUpDown _amount;
        private readonly TextBox _description;
        private readonly TextBox _originalDoc;
        private readonly TextBox _note;
        private readonly CheckBox _noDebtChange;

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnClose;

        /// <summary>ID phiếu đã lưu gần nhất (null nếu chưa lưu).</summary>
        public string SavedId { get; private set; }

        /// <summary>Số phiếu đã lưu gần nhất (null nếu chưa lưu).</summary>
        public string SavedNumber { get; private set; }

        /// <summary>True nếu có ít nhất một lần lưu thành công (kể cả qua "Lưu &amp; Mới").</summary>
        public bool AnySaved { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal VoucherKind KindForTest { get { return _kind; } }
        internal TextBox NumberBoxForTest { get { return _number; } }
        internal DateTimePicker DatePickerForTest { get { return _date; } }
        internal ComboBox ObjectTypeComboForTest { get { return _objectType; } }
        internal TextBox PartyNameBoxForTest { get { return _partyName; } }
        internal TextBox PartyAddressBoxForTest { get { return _partyAddress; } }
        internal LookupCombo EmployeeComboForTest { get { return _employee; } }
        internal LookupCombo CustomerComboForTest { get { return _customer; } }
        internal LookupCombo SupplierComboForTest { get { return _supplier; } }
        internal LookupCombo StoreComboForTest { get { return _store; } }
        internal LookupCombo ReasonComboForTest { get { return _reason; } }
        internal NumericUpDown AmountBoxForTest { get { return _amount; } }
        internal CheckBox TransferCheckForTest { get { return _transfer; } }
        internal LookupCombo BankComboForTest { get { return _bank; } }
        internal TextBox DescriptionBoxForTest { get { return _description; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }

        public PhieuThuDialog(UserSession session)
            : this(session, VoucherKind.Income, null, false, false)
        {
        }

        public PhieuThuDialog(UserSession session, VoucherKind kind, string voucherId = null, bool readOnly = false)
            : this(session, kind, voucherId, readOnly, false)
        {
        }

        /// <summary>
        /// Chế độ đặt tiền trước (§3.5 spec Khách đặt hàng): mở phiếu thu với THU = TONGCONG của
        /// phiếu đặt, lý do = lý do của phiếu cọc gần nhất; có phiếu cọc rồi thì nạp lại để sửa.
        /// </summary>
        public PhieuThuDialog(UserSession session, PhieuThuDepositPreset preset)
            : this(session, VoucherKind.Income, null, false, true)
        {
            if (preset == null)
                throw new ArgumentNullException("preset");
            _depositPreset = preset;
            _btnSaveNew.Visible = false;
            SetTitle("PHIẾU THU — ĐẶT TIỀN TRƯỚC");
            SubtitleText = "Đặt tiền trước cho phiếu đặt " + preset.BookingNumber + ". Lưu sẽ ghi phiếu thu vào cơ sở dữ liệu.";
            InitDepositMode(preset);
            _form.ApplyTheme(ThemeManager.Current);
        }

        /// <summary>
        /// Mở hộp Phiếu chi dùng chung để chi trả nợ nhà cung cấp (docs/specs/cong-no.md §4):
        /// đặt sẵn đối tượng là NCC và số tiền mặc định bằng Còn nợ; ghi thật qua VoucherService (chặn chi TM vượt quỹ).
        /// </summary>
        public PhieuThuDialog(UserSession session, SupplierDebtPayPreset preset)
            : this(session, VoucherKind.Expense, null, false, false)
        {
            if (preset == null)
                throw new ArgumentNullException("preset");
            ApplySupplierDebtPreset(preset);
        }

        private void ApplySupplierDebtPreset(SupplierDebtPayPreset preset)
        {
            _loading = true;
            try
            {
                if (PhieuThuRules.Supplier < _objectType.Items.Count)
                    _objectType.SelectedIndex = PhieuThuRules.Supplier;
                _supplier.SelectId(preset.SupplierId);
                _partyName.Text = preset.SupplierName ?? "";
                _partyAddress.Text = preset.SupplierAddress ?? "";
                SetAmount(preset.Amount > 0m ? preset.Amount : 0m);
                SetOptionalText(_description, preset.Description ?? "");
            }
            finally
            {
                _loading = false;
            }
            ApplyObjectType();
            ApplyModeState();
        }

        private PhieuThuDialog(
            UserSession session,
            VoucherKind kind,
            string voucherId,
            bool readOnly,
            bool depositMode,
            bool salaryAdvanceMode = false,
            RewardPenaltyMonthLimit advanceMonthLimit = null,
            PayrollPayoutPreset salaryPayoutPreset = null)
            : base(
                salaryPayoutPreset != null ? BuildSalaryPayoutTitle(string.IsNullOrEmpty(voucherId), readOnly || salaryPayoutPreset.ReadOnly)
                    : (salaryAdvanceMode ? BuildAdvanceTitle(string.IsNullOrEmpty(voucherId), readOnly) : BuildTitle(kind, string.IsNullOrEmpty(voucherId), readOnly)),
                salaryPayoutPreset != null ? BuildSalaryPayoutSubtitle(salaryPayoutPreset, readOnly || salaryPayoutPreset.ReadOnly)
                    : (salaryAdvanceMode ? BuildAdvanceSubtitle(readOnly) : BuildSubtitle(kind, readOnly)),
                720, 640)
        {
            _session = session ?? AppSession.Current;
            _isSalaryPayoutMode = salaryPayoutPreset != null;
            _salaryPayoutPreset = salaryPayoutPreset;
            _kind = (_isSalaryPayoutMode || salaryAdvanceMode) ? VoucherKind.Expense : kind;
            _isDepositMode = depositMode;
            _isSalaryAdvanceMode = salaryAdvanceMode;
            _advanceMonthLimit = advanceMonthLimit;
            _editingId = string.IsNullOrEmpty(voucherId) ? null : voucherId;
            _isNew = _editingId == null;

            _repo = new FormRepository(_session.DatabasePath);
            _voucherService = new VoucherService(_session);
            _phieuThuRepo = new PhieuThuRepository(_session.DatabasePath);
            if (_isSalaryAdvanceMode)
                _salaryAdvanceService = new SalaryAdvanceService(_session);
            if (_isSalaryPayoutMode)
                _salaryPayoutService = new PayrollPayoutService(_session);

            if (_isSalaryPayoutMode)
                _readOnly = readOnly || salaryPayoutPreset.ReadOnly || !_salaryPayoutService.CanPayOut;
            else if (_isSalaryAdvanceMode)
                _readOnly = readOnly || (_isNew ? !_salaryAdvanceService.CanAdd : !_salaryAdvanceService.CanEdit);
            else if (!depositMode)
                _readOnly = readOnly || (_isNew ? !_voucherService.CanAdd : !_voucherService.CanEdit);

            _layout = LoadVoucherLayout(_kind);
            if (_layout == null || !string.Equals(_layout.Table, "TTHUCHI", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Không đọc được layout form \"" + (_kind == VoucherKind.Expense ? VoucherConstants.ExpenseFormName : FormName) + "\".");

            _form = new DynamicForm(_layout) { Dock = DockStyle.Fill, Padding = new Padding((int)(8 * Dpi)) };
            Body.Controls.Add(_form);

            _objectType = (ComboBox)Need("cboLOAIDOITUONG");
            _partyName = (TextBox)Need("txtTENDOITUONG");
            _partyAddress = (TextBox)Need("txtDIACHI");
            _number = (TextBox)Need("txtNAME");
            _employee = (LookupCombo)Need("lueDNHANVIENID");
            _customer = (LookupCombo)Need("lueDKHACHHANGID");
            _supplier = (LookupCombo)Need("lueDNHACUNGCAPID");
            _bank = (LookupCombo)Need("lueDTAIKHOANNGANHANGID");
            _store = (LookupCombo)Need("lueDCUAHANGID");
            _reason = (LookupCombo)Need("lueDLYDOTHUCHIID");
            _transfer = (CheckBox)Need("chkCHUYENKHOAN");
            _date = (DateTimePicker)Need("dtNGAY");

            _amount = FindAmountEditor();
            _description = FindOptionalEditor<TextBox>("txtDIENGIAI", "DIENGIAI");
            _originalDoc = FindOptionalEditor<TextBox>("txtCHUNGTUGOC", "CHUNGTUGOC");
            _note = FindOptionalEditor<TextBox>("txtNOTE", "NOTE");
            _noDebtChange = FindOptionalEditor<CheckBox>("chkKHONGTHAYDOICONGNO", "KHONGTHAYDOICONGNO");

            if (_kind == VoucherKind.Expense)
                AdjustExpenseLabels();
            if (_isSalaryAdvanceMode)
                ConfigureSalaryAdvanceLayout();
            else if (_isSalaryPayoutMode)
                ConfigureSalaryPayoutLayout();

            _btnClose = AddButton("Đóng", null, delegate { Close(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { Save(true); });
            _btnSave = AddButton("Lưu", "primary", delegate { Save(false); });
            CancelButton = _btnClose;

            LoadLookups();
            WireRules();

            if (_isNew)
            {
                if (_isSalaryPayoutMode)
                    InitNewSalaryPayoutDocument();
                else if (_isSalaryAdvanceMode)
                    InitNewSalaryAdvanceDocument();
                else
                    InitNewDocument();
            }
            else if (_isSalaryPayoutMode)
                LoadExistingSalaryPayout(_editingId);
            else if (_isSalaryAdvanceMode)
                LoadExistingSalaryAdvance(_editingId);
            else
                LoadExistingVoucher(_editingId);

            ApplyModeState();

            ClientSize = new Size(ClientSize.Width, (int)((60 + 56 + 24) * Dpi) + _form.ContentHeight + (int)(16 * Dpi));
            Shown += delegate
            {
                if (_loadFailed)
                {
                    ShowWarning(VoucherConstants.VoucherNotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                    _number.Select();
            };
        }

        private ScreenLayout LoadVoucherLayout(VoucherKind kind)
        {
            if (kind == VoucherKind.Expense)
            {
                ScreenLayout exp = _repo.LoadLayout(VoucherConstants.ExpenseFormName);
                if (exp != null && string.Equals(exp.Table, "TTHUCHI", StringComparison.OrdinalIgnoreCase))
                    return exp;
            }
            return _repo.LoadLayout(FormName);
        }

        private void AdjustExpenseLabels()
        {
            if (_amount != null)
            {
                Label amtLabel = _form.LabelOf(_amount.Name);
                if (amtLabel != null && amtLabel.Text.IndexOf("Thu", StringComparison.OrdinalIgnoreCase) >= 0)
                    amtLabel.Text = amtLabel.Text.Replace("Thu", "Chi").Replace("thu", "chi");
            }
            Label reasonLabel = _form.LabelOf("lueDLYDOTHUCHIID");
            if (reasonLabel != null && reasonLabel.Text.IndexOf("thu", StringComparison.OrdinalIgnoreCase) >= 0
                && reasonLabel.Text.IndexOf("chi", StringComparison.OrdinalIgnoreCase) < 0)
            {
                reasonLabel.Text = reasonLabel.Text.Replace("Thu", "Chi").Replace("thu", "chi");
            }
        }

        private static string BuildTitle(VoucherKind kind, bool isNew, bool readOnly)
        {
            string baseTitle = kind == VoucherKind.Expense ? "PHIẾU CHI" : "PHIẾU THU";
            if (readOnly)
                return baseTitle + " — XEM CHI TIẾT";
            return isNew ? baseTitle : baseTitle + " — SỬA";
        }

        private static string BuildSubtitle(VoucherKind kind, bool readOnly)
        {
            if (readOnly)
                return "Chế độ chỉ xem";
            string label = kind == VoucherKind.Expense ? "phiếu chi" : "phiếu thu";
            return "Nhập thông tin " + label + " (Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Đóng).";
        }

        private Control Need(string name)
        {
            Control c = _form.Editor(name);
            if (c == null)
                throw new InvalidOperationException("Layout thiếu ô \"" + name + "\".");
            return c;
        }

        private NumericUpDown FindAmountEditor()
        {
            string[] names = _kind == VoucherKind.Expense
                ? new[] { "numCHI", "numTHU" }
                : new[] { "numTHU", "numCHI" };
            foreach (string name in names)
            {
                var n = _form.Editor(name) as NumericUpDown;
                if (n != null)
                    return n;
            }
            foreach (string name in _form.EditorNames)
            {
                FieldMap map = _form.MapOf(name);
                if (map == null)
                    continue;
                if (string.Equals(map.DataField, "THU", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(map.DataField, "CHI", StringComparison.OrdinalIgnoreCase))
                {
                    var n = _form.Editor(name) as NumericUpDown;
                    if (n != null)
                        return n;
                }
            }
            return null;
        }

        private T FindOptionalEditor<T>(string editorName, string dataField) where T : Control
        {
            var direct = _form.Editor(editorName) as T;
            if (direct != null)
                return direct;
            foreach (string name in _form.EditorNames)
            {
                FieldMap map = _form.MapOf(name);
                if (map != null && string.Equals(map.DataField, dataField, StringComparison.OrdinalIgnoreCase))
                {
                    var ctl = _form.Editor(name) as T;
                    if (ctl != null)
                        return ctl;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ Danh sách & luật form

        private void LoadLookups()
        {
            Dictionary<string, string> tables = _repo.LookupTables(_layout.Table);
            foreach (string name in _form.EditorNames)
            {
                var lc = _form.Editor(name) as LookupCombo;
                FieldMap map = _form.MapOf(name);
                if (lc == null || map == null)
                    continue;

                IList<LookupItem> items;
                if (string.Equals(name, "lueDLYDOTHUCHIID", StringComparison.OrdinalIgnoreCase))
                {
                    items = _kind == VoucherKind.Expense
                        ? _repo.LoadExpenseReasons()
                        : _repo.LoadIncomeReasons();
                }
                else if (string.Equals(name, "lueDCUAHANGID", StringComparison.OrdinalIgnoreCase) && !_session.IsAdmin)
                {
                    items = _repo.LoadStoresOfUser(_session.UserId);
                }
                else
                {
                    string table;
                    if (!tables.TryGetValue(map.DataField, out table))
                        continue;
                    items = _repo.LoadAll(table);
                }
                lc.SetItems(items);
            }
        }

        private void WireRules()
        {
            _objectType.SelectedIndexChanged += delegate
            {
                ApplyObjectType();
            };
            _employee.ValueChanged += delegate
            {
                if (_loading)
                    return;
                string n, a;
                PhieuThuRules.FillFromEmployee(_employee.Selected == null ? "" : _employee.Selected.Name, out n, out a);
                _partyName.Text = n;
                _partyAddress.Text = a;
            };
            _customer.ValueChanged += delegate
            {
                if (!_loading)
                    FillParty(_customer.Selected);
            };
            _supplier.ValueChanged += delegate
            {
                if (!_loading)
                    FillParty(_supplier.Selected);
            };
            _transfer.CheckedChanged += delegate
            {
                _bank.Enabled = !_readOnly && PhieuThuRules.BankAccountEnabled(_transfer.Checked);
                _form.ApplyTheme(ThemeManager.Current);
                if (!_loading && !_readOnly && _transfer.Checked && _transfer.Focused && !SuppressPopup)
                    BeginInvoke((Action)delegate { _bank.DroppedDown = true; });
            };
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void FillParty(LookupItem item)
        {
            string n, a;
            PhieuThuRules.FillFromParty(item == null ? "" : item.Name, item == null ? "" : item.Address, out n, out a);
            _partyName.Text = n;
            _partyAddress.Text = a;
        }

        private void ApplyObjectType()
        {
            ObjectFieldState s = PhieuThuRules.ForObjectType(_objectType.SelectedIndex);
            _customer.Enabled = !_readOnly && s.Customer;
            _employee.Enabled = !_readOnly && s.Employee;
            _partyName.Enabled = !_readOnly && s.Name;
            _supplier.Enabled = !_readOnly && s.Supplier;
            _partyAddress.Enabled = !_readOnly && s.Address;
            _form.ApplyTheme(ThemeManager.Current);

            if (!_loading && !_readOnly && _objectType.Focused && IsHandleCreated && !SuppressPopup)
            {
                LookupCombo open = s.Customer ? _customer : s.Employee ? _employee : s.Supplier ? _supplier : null;
                if (open != null)
                    BeginInvoke((Action)delegate { open.DroppedDown = true; });
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            _form.ApplyTheme(ThemeManager.Current);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            if (_form != null)
                _form.ApplyTheme(ThemeManager.Current);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
