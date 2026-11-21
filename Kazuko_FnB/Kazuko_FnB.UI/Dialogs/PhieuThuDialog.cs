using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Quỹ ▸ Tạo phiếu thu — BẢN THỬ dựng từ layout của app gốc (SFORM "Phiếu thu"), xem docs/specs/pilot-phieu-thu.md.
    /// Hiện đủ trường, chạy các luật bật/tắt ô và điền tên/địa chỉ; nút Lưu chỉ kiểm tra, KHÔNG ghi dữ liệu.
    /// Ngoại lệ: chế độ đặt tiền trước (constructor có preset, §3.5 spec Khách đặt hàng) thì nút Lưu
    /// GHI THẬT phiếu thu cọc xuống TTHUCHI qua PhieuThuRepository.
    /// </summary>
    internal sealed class PhieuThuDialog : ThemedDialog, IScreenPreview
    {
        public const string FormName = "Phiếu thu";
        public const string DateFunction = "Thay đổi ngày trong phiếu thu chi";

        private readonly UserSession _session;
        private readonly FormRepository _repo;
        private readonly ScreenLayout _layout;
        private readonly DynamicForm _form;

        private readonly PhieuThuRepository _phieuThuRepo;
        private readonly PhieuThuDepositPreset _depositPreset;
        private readonly bool _isDepositMode;

        /// <summary>ID phiếu thu đã lưu ở chế độ cọc (null nếu chưa lưu).</summary>
        public string SavedId { get; private set; }

        /// <summary>Số phiếu thu đã lưu ở chế độ cọc (null nếu chưa lưu).</summary>
        public string SavedNumber { get; private set; }

        private readonly ComboBox _objectType;
        private readonly TextBox _partyName, _partyAddress, _number;
        private readonly LookupCombo _employee, _customer, _supplier, _bank, _store, _reason;
        private readonly CheckBox _transfer;
        private readonly DateTimePicker _date;

        public PhieuThuDialog(UserSession session)
            : base("PHIẾU THU", "Bản thử giao diện dựng từ form gốc: chưa ghi dữ liệu vào cơ sở dữ liệu.", 720, 640)
        {
            _session = session;
            _repo = new FormRepository(session.DatabasePath);
            _phieuThuRepo = new PhieuThuRepository(session.DatabasePath);
            _layout = _repo.LoadLayout(FormName);
            if (_layout == null || !string.Equals(_layout.Table, "TTHUCHI", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Không đọc được layout form \"" + FormName + "\".");

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

            AddButton("Lưu", "primary", delegate { Save(); });
            FlatButton close = AddButton("Đóng", null, delegate { Close(); });
            CancelButton = close;

            LoadLookups();
            WireRules();
            InitNewDocument();

            // Cao vừa đủ nội dung (không cuộn dọc nếu màn đủ cao).
            ClientSize = new Size(ClientSize.Width, (int)((60 + 56 + 24) * Dpi) + _form.ContentHeight + (int)(16 * Dpi));
            _number.Select();
        }

        /// <summary>
        /// Chế độ đặt tiền trước (§3.5 spec Khách đặt hàng): mở phiếu thu với THU = TONGCONG của
        /// phiếu đặt, lý do = lý do của phiếu cọc gần nhất; có phiếu cọc rồi thì nạp lại để sửa.
        /// Nút Lưu ở chế độ này GHI THẬT xuống TTHUCHI.
        /// </summary>
        public PhieuThuDialog(UserSession session, PhieuThuDepositPreset preset)
            : this(session)
        {
            if (preset == null)
                throw new ArgumentNullException("preset");
            _depositPreset = preset;
            _isDepositMode = true;
            SetTitle("PHIẾU THU — ĐẶT TIỀN TRƯỚC");
            SubtitleText = "Đặt tiền trước cho phiếu đặt " + preset.BookingNumber + ". Lưu sẽ ghi phiếu thu vào cơ sở dữ liệu.";

            BookingDeposit existing = null;
            if (!string.IsNullOrEmpty(preset.ExistingReceiptId))
            {
                try
                {
                    existing = _phieuThuRepo.LoadReceipt(preset.ExistingReceiptId);
                }
                catch (DataAccessException)
                {
                    existing = null;
                }
            }

            if (existing != null)
            {
                // Nạp lại phiếu cọc đã có để sửa (như btnDatTruoc_Click của app gốc).
                _number.Text = existing.Number ?? "";
                _number.ReadOnly = true;
                _objectType.SelectedIndex = string.IsNullOrEmpty(existing.CustomerId)
                    ? PhieuThuRules.OtherObject : PhieuThuRules.Customer;
                _customer.SelectId(existing.CustomerId);
                _partyName.Text = existing.CustomerName ?? "";
                _partyAddress.Text = existing.Address ?? "";
                _employee.SelectId(existing.EmployeeId);
                _store.SelectId(existing.StoreId);
                _reason.SelectId(existing.ReasonId);
                SetAmount(existing.Amount);
                _date.Value = existing.Date;
                _transfer.Checked = existing.Transfer;
                _bank.SelectId(existing.BankAccountId);
                SetNote(existing.Note);
            }
            else
            {
                // Lập phiếu mới: điền sẵn theo phiếu đặt (§3.5).
                string number;
                try
                {
                    number = _phieuThuRepo.GenerateNumber();
                }
                catch (DataAccessException)
                {
                    number = "";
                }
                _number.Text = number;
                _number.ReadOnly = true;
                _objectType.SelectedIndex = string.IsNullOrEmpty(preset.CustomerId)
                    ? PhieuThuRules.OtherObject : PhieuThuRules.Customer;
                _customer.SelectId(preset.CustomerId);
                _partyName.Text = preset.CustomerName ?? "";
                _partyAddress.Text = preset.Address ?? "";
                string reasonId = null;
                try
                {
                    reasonId = _phieuThuRepo.LoadLatestDepositReasonId();
                }
                catch (DataAccessException)
                {
                    reasonId = null;
                }
                _reason.SelectId(reasonId);
                SetAmount(preset.Amount);
                SetNote(BookingText.DepositNote(preset.BookingNumber));
            }
            _form.ApplyTheme(ThemeManager.Current);
        }

        private void SetAmount(decimal amount)
        {
            var numThu = _form.Editor("numTHU") as NumericUpDown;
            if (numThu == null)
                return;
            if (amount < numThu.Minimum)
                amount = numThu.Minimum;
            if (amount > numThu.Maximum)
                amount = numThu.Maximum;
            numThu.Value = amount;
        }

        private void SetNote(string note)
        {
            var tb = _form.Editor("txtDIENGIAI") as TextBox;
            if (tb != null)
                tb.Text = note ?? "";
        }

        private string GetNote()
        {
            var tb = _form.Editor("txtDIENGIAI") as TextBox;
            return tb == null ? null : tb.Text;
        }

        private decimal GetAmount()
        {
            var numThu = _form.Editor("numTHU") as NumericUpDown;
            return numThu == null ? 0m : numThu.Value;
        }

        private Control Need(string name)
        {
            Control c = _form.Editor(name);
            if (c == null)
                throw new InvalidOperationException("Layout thiếu ô \"" + name + "\".");
            return c;
        }

        // ------------------------------------------------------------------ Danh sách

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
                if (name == "lueDLYDOTHUCHIID")
                    items = _repo.LoadIncomeReasons();
                else if (name == "lueDCUAHANGID" && !_session.IsAdmin)
                    items = _repo.LoadStoresOfUser(_session.UserId);
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

        // ------------------------------------------------------------------ Luật của form (mục 3 của spec)

        private void WireRules()
        {
            _objectType.SelectedIndexChanged += delegate { ApplyObjectType(); };
            _employee.ValueChanged += delegate
            {
                string n, a;
                PhieuThuRules.FillFromEmployee(_employee.Selected == null ? "" : _employee.Selected.Name, out n, out a);
                _partyName.Text = n;
                _partyAddress.Text = a;
            };
            _customer.ValueChanged += delegate { FillParty(_customer.Selected); };
            _supplier.ValueChanged += delegate { FillParty(_supplier.Selected); };
            _transfer.CheckedChanged += delegate
            {
                _bank.Enabled = PhieuThuRules.BankAccountEnabled(_transfer.Checked);
                _form.ApplyTheme(ThemeManager.Current);
                if (_transfer.Checked && _transfer.Focused)
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
            _customer.Enabled = s.Customer;
            _employee.Enabled = s.Employee;
            _partyName.Enabled = s.Name;
            _supplier.Enabled = s.Supplier;
            _partyAddress.Enabled = s.Address;
            _form.ApplyTheme(ThemeManager.Current);

            // Như form gốc: đang đứng ở ô loại đối tượng thì mở sẵn danh sách của ô vừa được bật.
            if (_objectType.Focused && IsHandleCreated)
            {
                LookupCombo open = s.Customer ? _customer : s.Employee ? _employee : s.Supplier ? _supplier : null;
                if (open != null)
                    BeginInvoke((Action)delegate { open.DroppedDown = true; });
            }
        }

        /// <summary>Phiếu mới: loại đối tượng = 0, cửa hàng = cửa hàng hiện tại, ngày = hôm nay (mặc định của bản thử).</summary>
        private void InitNewDocument()
        {
            _objectType.SelectedIndex = PhieuThuRules.OtherObject;
            if (_session.CurrentStore != null)
                _store.SelectId(_session.CurrentStore.Id);
            _date.Value = DateTime.Today;
            _bank.Enabled = PhieuThuRules.BankAccountEnabled(_transfer.Checked);

            // Ngày chỉ đổi được khi có quyền xem chức năng "Thay đổi ngày trong phiếu thu chi" (admin luôn có).
            bool canChangeDate = _session.IsAdmin
                || (_session.Permissions != null && _session.Permissions.Can(DateFunction, RoleModes.View));
            _date.Enabled = canChangeDate;
            _form.ApplyTheme(ThemeManager.Current);
        }

        // ------------------------------------------------------------------ Lưu

        private void Save()
        {
            var missing = new List<string>();
            foreach (string name in _form.EditorNames)
            {
                FieldMap map = _form.MapOf(name);
                Control c = _form.Editor(name);
                if (map == null || !map.Required || !c.Enabled)
                    continue;
                if (IsEmpty(c))
                {
                    LayoutControl lbl = map.LabelControl == null ? null : _layout.Find(map.LabelControl);
                    missing.Add(lbl != null ? lbl.Text.Trim() : map.DataField);
                }
            }

            string error = PhieuThuRules.Validate(missing, _transfer.Checked, _bank.SelectedId != null);
            if (error != null)
            {
                Msg.Warning(this, error);
                if (error == PhieuThuRules.BankAccountMissing)
                    _bank.DroppedDown = true;
                return;
            }

            if (_isDepositMode)
            {
                SaveDeposit();
                return;
            }
            Msg.Info(this, "Bản thử giao diện: dữ liệu hợp lệ nhưng chưa được ghi vào cơ sở dữ liệu.");
        }

        /// <summary>
        /// Chế độ cọc (§3.5): ghi thật phiếu thu xuống TTHUCHI. Số phiếu đã sinh sẵn ở ctor;
        /// trùng số thì báo và giữ dialog để người dùng sửa.
        /// </summary>
        private void SaveDeposit()
        {
            BookingDeposit existing = null;
            if (!string.IsNullOrEmpty(_depositPreset.ExistingReceiptId))
            {
                try
                {
                    existing = _phieuThuRepo.LoadReceipt(_depositPreset.ExistingReceiptId);
                }
                catch (DataAccessException)
                {
                    existing = null;
                }
            }
            var dep = new BookingDeposit
            {
                Id = existing == null ? null : existing.Id,
                BookingId = _depositPreset.BookingId,
                Number = (_number.Text ?? "").Trim(),
                Date = _date.Value.Date,
                ObjectType = _objectType.SelectedIndex < 0 ? 0 : _objectType.SelectedIndex,
                CustomerId = _objectType.SelectedIndex == PhieuThuRules.Customer ? _customer.SelectedId : null,
                CustomerName = (_partyName.Text ?? "").Trim(),
                Address = (_partyAddress.Text ?? "").Trim(),
                EmployeeId = _employee.SelectedId,
                StoreId = _store.SelectedId,
                ReasonId = _reason.SelectedId,
                Note = GetNote(),
                Amount = GetAmount(),
                Transfer = _transfer.Checked,
                BankAccountId = _bank.SelectedId
            };
            string savedId;
            try
            {
                savedId = _phieuThuRepo.SaveDeposit(dep, _session.UserId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return;
            }
            if (savedId == null)
            {
                Msg.Warning(this, "Số phiếu \"" + dep.Number + "\" đã tồn tại. Mời bạn nhập số phiếu khác.");
                _number.Focus();
                _number.SelectAll();
                return;
            }
            SavedId = savedId;
            SavedNumber = dep.Number;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool IsEmpty(Control c)
        {
            var lc = c as LookupCombo;
            if (lc != null)
                return lc.SelectedId == null;
            var cb = c as ComboBox;
            if (cb != null)
                return cb.SelectedIndex < 0;
            var tb = c as TextBox;
            if (tb != null)
                return tb.Text.Trim().Length == 0;
            return false;
        }

        // ------------------------------------------------------------------ Giao diện

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

        /// <summary>--render-screen: "type=N" chọn loại đối tượng; "transfer" tích chuyển khoản; "customer" chọn khách đầu tiên.</summary>
        public void Preview(string option)
        {
            if (option == null)
                return;
            if (option.StartsWith("type=", StringComparison.Ordinal))
            {
                int n;
                if (int.TryParse(option.Substring(5), out n))
                    _objectType.SelectedIndex = n;
            }
            else if (option == "transfer")
                _transfer.Checked = true;
            else if (option == "customer" && _customer.Items.Count > 0)
                _customer.SelectedIndex = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
