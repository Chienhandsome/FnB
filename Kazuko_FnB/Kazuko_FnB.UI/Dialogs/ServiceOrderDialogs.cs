using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Hộp nhập một số (Đặt số lượng, Đổi giá bán, Nhập giá thời giá) dùng NumberBox dùng chung.</summary>
    internal sealed class ServiceNumberPromptDialog : ThemedDialog
    {
        private readonly NumberBox _num = new NumberBox();

        private ServiceNumberPromptDialog(string title, string subtitle, decimal initial, decimal min, decimal max, int decimals)
            : base(title, subtitle, 380, 200)
        {
            float s = Dpi;
            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = AddButton("Đồng ý", "primary", delegate { DialogResult = DialogResult.OK; Close(); });

            _num.DecimalPlaces = decimals;
            _num.Minimum = min;
            _num.Maximum = max;
            _num.Value = Math.Max(min, Math.Min(max, initial));
            _num.Dock = DockStyle.Top;
            _num.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize + 1f);
            Body.Padding = new Padding((int)(16 * s), (int)(18 * s), (int)(16 * s), 0);
            Body.Controls.Add(_num);
            Shown += delegate { _num.Focus(); };
        }

        public static decimal? Ask(IWin32Window owner, string title, string subtitle, decimal initial, decimal min, decimal max, int decimals)
        {
            using (var dlg = new ServiceNumberPromptDialog(title, subtitle, initial, min, max, decimals))
                return dlg.ShowDialog(owner) == DialogResult.OK ? (decimal?)dlg._num.Value : null;
        }
    }

    /// <summary>Hộp Đặt giảm giá 1 dòng món (DatGiamGia, spec §3.5): nhập % hoặc tiền, tự quy đổi qua lại.</summary>
    internal sealed class ServiceLineDiscountDialog : ThemedDialog
    {
        private readonly decimal _unitPrice;
        private readonly NumberBox _numPrice = new NumberBox { ReadOnly = true, Maximum = 999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numRate = new NumberBox { Minimum = 0m, Maximum = 100m, DecimalPlaces = 2 };
        private readonly NumberBox _numAmount = new NumberBox { Minimum = 0m, Maximum = 999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numAfter = new NumberBox { ReadOnly = true, Maximum = 999999999m, DecimalPlaces = 2 };
        private bool _syncing;

        public decimal DiscountRate { get { return _numRate.Value; } }
        public decimal DiscountAmount { get { return _numAmount.Value; } }

        public ServiceLineDiscountDialog(string itemName, decimal unitPrice, decimal currentRate)
            : base("ĐẶT GIẢM GIÁ", itemName ?? "", 420, 290)
        {
            _unitPrice = Math.Max(0m, unitPrice);
            float s = Dpi;
            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = AddButton("Đồng ý", "primary", delegate { DialogResult = DialogResult.OK; Close(); });

            _numPrice.Value = _unitPrice;
            _numAmount.Maximum = Math.Max(0m, _unitPrice);
            _numRate.Value = Math.Max(0m, Math.Min(100m, currentRate));
            SyncFromRate();

            _numRate.ValueChanged += delegate { if (!_syncing) SyncFromRate(); };
            _numAmount.ValueChanged += delegate { if (!_syncing) SyncFromAmount(); };

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 4, Height = (int)(156 * s) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130 * s));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 4; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38 * s));

            AddRow(grid, 0, "Đơn giá gốc:", _numPrice);
            AddRow(grid, 1, "Giảm giá (%):", _numRate);
            AddRow(grid, 2, "Tiền giảm:", _numAmount);
            AddRow(grid, 3, "Giá sau giảm:", _numAfter);

            Body.Controls.Add(grid);
            Shown += delegate { _numRate.Focus(); };
        }

        private void SyncFromRate()
        {
            _syncing = true;
            try
            {
                decimal amt = Math.Round(_unitPrice * _numRate.Value / 100m, 0, MidpointRounding.AwayFromZero);
                _numAmount.Value = Math.Max(0m, Math.Min(_unitPrice, amt));
                _numAfter.Value = Math.Max(0m, _unitPrice - _numAmount.Value);
            }
            finally { _syncing = false; }
        }

        private void SyncFromAmount()
        {
            _syncing = true;
            try
            {
                decimal rate = _unitPrice == 0m ? 0m : Math.Round(_numAmount.Value * 100m / _unitPrice, 2, MidpointRounding.AwayFromZero);
                _numRate.Value = Math.Max(0m, Math.Min(100m, rate));
                _numAfter.Value = Math.Max(0m, _unitPrice - _numAmount.Value);
            }
            finally { _syncing = false; }
        }

        private static void AddRow(TableLayoutPanel panel, int row, string text, Control box)
        {
            var lbl = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            box.Dock = DockStyle.Fill;
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(box, 1, row);
        }
    }

    /// <summary>Hộp Giảm giá theo nhóm (4 tỉ lệ: Đồ ăn, Đồ uống, Dịch vụ, Đồ khác — spec §3.5).</summary>
    internal sealed class ServiceGroupDiscountDialog : ThemedDialog
    {
        private readonly NumberBox _numFood = new NumberBox { Minimum = 0m, Maximum = 100m, DecimalPlaces = 2 };
        private readonly NumberBox _numDrink = new NumberBox { Minimum = 0m, Maximum = 100m, DecimalPlaces = 2 };
        private readonly NumberBox _numService = new NumberBox { Minimum = 0m, Maximum = 100m, DecimalPlaces = 2 };
        private readonly NumberBox _numOther = new NumberBox { Minimum = 0m, Maximum = 100m, DecimalPlaces = 2 };

        public GroupDiscountInput Result
        {
            get
            {
                return new GroupDiscountInput
                {
                    FoodRate = _numFood.Value,
                    DrinkRate = _numDrink.Value,
                    ServiceRate = _numService.Value,
                    OtherRate = _numOther.Value
                };
            }
        }

        public ServiceGroupDiscountDialog()
            : base("GIẢM GIÁ THEO NHÓM", "Nhập tỉ lệ % giảm giá áp dụng cho từng loại đồ", 400, 290)
        {
            float s = Dpi;
            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = AddButton("Áp dụng", "primary", delegate { DialogResult = DialogResult.OK; Close(); });

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 4, Height = (int)(156 * s) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120 * s));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 4; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38 * s));

            AddRow(grid, 0, "Đồ ăn (%):", _numFood);
            AddRow(grid, 1, "Đồ uống (%):", _numDrink);
            AddRow(grid, 2, "Dịch vụ (%):", _numService);
            AddRow(grid, 3, "Đồ khác (%):", _numOther);
            Body.Controls.Add(grid);
        }

        private static void AddRow(TableLayoutPanel panel, int row, string text, Control box)
        {
            var lbl = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            box.Dock = DockStyle.Fill;
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(box, 1, row);
        }
    }

    /// <summary>Hộp Thêm mặt hàng mở (nhập Tên hàng, ĐVT, Đơn giá — spec §3.4).</summary>
    internal sealed class ServiceOpenItemDialog : ThemedDialog
    {
        private readonly TextBox _txtName = new TextBox();
        private readonly LookupCombo _cboUnit = new LookupCombo();
        private readonly NumberBox _numPrice = new NumberBox { Minimum = 0m, Maximum = 999999999m, DecimalPlaces = 2 };

        public string ItemName { get { return _txtName.Text.Trim(); } }
        public string UnitId { get { return _cboUnit.SelectedId; } }
        public decimal UnitPrice { get { return _numPrice.Value; } }

        public ServiceOpenItemDialog(ServiceMenuItem item, List<LookupItem> units)
            : base("MẶT HÀNG MỞ", "Nhập tên hàng, đơn vị tính và đơn giá bán", 430, 260)
        {
            float s = Dpi;
            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = AddButton("Đồng ý", "primary", delegate
            {
                if (string.IsNullOrWhiteSpace(_txtName.Text))
                {
                    Msg.Warning(this, "Mời bạn nhập tên mặt hàng.");
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            });

            _txtName.Text = item != null ? (item.Name ?? "") : "";
            _cboUnit.SetItems(units ?? new List<LookupItem>());
            if (item != null && !string.IsNullOrEmpty(item.UnitId))
                _cboUnit.SelectId(item.UnitId);
            _numPrice.Value = item != null ? Math.Max(0m, item.SellPrice) : 0m;

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 3, Height = (int)(120 * s) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110 * s));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 3; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38 * s));

            AddRow(grid, 0, "Tên hàng:", _txtName);
            AddRow(grid, 1, "Đơn vị tính:", _cboUnit);
            AddRow(grid, 2, "Đơn giá:", _numPrice);
            Body.Controls.Add(grid);
        }

        private static void AddRow(TableLayoutPanel panel, int row, string text, Control box)
        {
            var lbl = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            box.Dock = DockStyle.Fill;
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(box, 1, row);
        }
    }

    /// <summary>Hộp đăng nhập xác thực quyền CanLogin (spec §4, §3.5: XoaGiamMon, SuaDonGia, GiamGiaMatHang).</summary>
    internal sealed class ServiceReLoginDialog : ThemedDialog
    {
        private readonly string _databasePath;
        private readonly string _requiredFunctionId;
        private readonly TextBox _txtUser = new TextBox();
        private readonly TextBox _txtPass = new TextBox { UseSystemPasswordChar = true };

        public UserSession VerifiedSession { get; private set; }

        public ServiceReLoginDialog(string databasePath, string requiredFunctionId, string actionTitle)
            : base("XÁC NHẬN QUYỀN", "Đăng nhập tài khoản có quyền: " + (actionTitle ?? ""), 400, 240)
        {
            _databasePath = databasePath;
            _requiredFunctionId = requiredFunctionId;
            float s = Dpi;
            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = AddButton("Xác nhận", "primary", delegate { TryVerify(); });

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 2, Height = (int)(84 * s) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110 * s));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));

            grid.Controls.Add(new Label { Text = "Tài khoản:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtUser.Dock = DockStyle.Fill;
            grid.Controls.Add(_txtUser, 1, 0);
            grid.Controls.Add(new Label { Text = "Mật khẩu:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _txtPass.Dock = DockStyle.Fill;
            grid.Controls.Add(_txtPass, 1, 1);

            Body.Controls.Add(grid);
            Shown += delegate { _txtUser.Focus(); };
        }

        private void TryVerify()
        {
            LoginResult login = new LoginService().Login(_databasePath, _txtUser.Text.Trim(), _txtPass.Text);
            if (!login.Success)
            {
                Msg.Warning(this, login.Message);
                return;
            }
            if (!TableService.HasFunction(login.Session, _requiredFunctionId))
            {
                Msg.Warning(this, ServiceOrderConstants.NoPermission);
                return;
            }
            VerifiedSession = login.Session;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Hộp chọn mức giá khi khu vực đặt DGIAMATHANGID = '0' (HIENTHILUACHON) và bật giá 2/3/4 (§3.4, §6.5).</summary>
    internal sealed class ServicePriceChoiceDialog : ThemedDialog
    {
        public decimal SelectedPrice { get; private set; }

        public ServicePriceChoiceDialog(ServiceMenuItem item)
            : base("CHỌN LOẠI GIÁ", item != null ? item.Name : "", 380, 290)
        {
            float s = Dpi;
            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            AddPriceButton(flow, "Giá chung", item.SellPrice, s);
            if (SystemConfig.SuDungGia2)
                AddPriceButton(flow, SystemConfig.DienGiaiGia2, item.SellPrice2, s);
            if (SystemConfig.SuDungGia3)
                AddPriceButton(flow, SystemConfig.DienGiaiGia3, item.SellPrice3, s);
            if (SystemConfig.SuDungGia4)
                AddPriceButton(flow, SystemConfig.DienGiaiGia4, item.SellPrice4, s);
            Body.Controls.Add(flow);
        }

        private void AddPriceButton(FlowLayoutPanel flow, string label, decimal price, float s)
        {
            var btn = new FlatButton
            {
                Text = label + " — " + NumberInput.FormatForDisplay(price, 2, true),
                Size = new Size((int)(320 * s), (int)(44 * s)),
                Margin = new Padding(0, 0, 0, (int)(6 * s))
            };
            btn.Click += delegate
            {
                SelectedPrice = price;
                DialogResult = DialogResult.OK;
                Close();
            };
            flow.Controls.Add(btn);
        }
    }

    /// <summary>Khung chứa màn "Sử dụng dịch vụ" để chụp bằng <c>--render-screen preview.service</c>.</summary>
    internal sealed class ServicePreviewForm : Form, IScreenPreview
    {
        private readonly ServiceOrderView _view;

        public ServicePreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Sử dụng dịch vụ";
            ClientSize = new Size(1360, 760);
            _view = new ServiceOrderView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            _view.TableMapForTest.StopAutoRefresh();
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        public void Preview(string option)
        {
            _view.ReloadAll();
        }
    }
}
