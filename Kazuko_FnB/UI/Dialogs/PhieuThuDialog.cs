using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Quỹ ▸ Tạo phiếu thu — BẢN THỬ dựng từ layout của app gốc (SFORM "Phiếu thu"), xem docs/specs/pilot-phieu-thu.md.
    /// Hiện đủ trường, chạy các luật bật/tắt ô và điền tên/địa chỉ; nút Lưu chỉ kiểm tra, KHÔNG ghi dữ liệu.
    /// </summary>
    internal sealed class PhieuThuDialog : ThemedDialog, IScreenPreview
    {
        public const string FormName = "Phiếu thu";
        public const string DateFunction = "Thay đổi ngày trong phiếu thu chi";

        private readonly UserSession _session;
        private readonly FormRepository _repo;
        private readonly ScreenLayout _layout;
        private readonly DynamicForm _form;

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

        // ------------------------------------------------------------------ Lưu (chỉ kiểm tra)

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
            Msg.Info(this, "Bản thử giao diện: dữ liệu hợp lệ nhưng chưa được ghi vào cơ sở dữ liệu.");
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
