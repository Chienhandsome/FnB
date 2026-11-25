using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn báo cáo chỉ xem "Chi tiết hoạt động ngày" (docs/specs/chi-tiet-hoat-dong-ngay.md):
    /// thanh lọc trên (Từ ngày, Đến ngày bắt buộc; Thu ngân và Cửa hàng tùy chọn; Tải dữ liệu (F5); In báo cáo),
    /// dòng ghi chú quy tắc lọc thu ngân (P4), và một vùng cuộn dọc chứa đầu báo cáo, 4 khối dữ liệu, khối tổng hợp 8 dòng ở cuối.
    /// Không chứa SQL; mọi màu và cỡ chữ từ ThemeTokens / ThemeManager.
    /// </summary>
    internal sealed class DailyActivityView : Panel
    {
        public const string MenuId = DailyActivityConstants.MenuId;

        private readonly UserSession _session;
        private readonly DailyActivityRepository _repository;

        private readonly FlowLayoutPanel _topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly DateBox _fromDate = new DateBox(false);
        private readonly DateBox _toDate = new DateBox(false);
        private readonly ComboBox _cboCashier = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _cashierFrame;
        private readonly ComboBox _cboStore = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _storeFrame;
        private readonly FlatButton _btnLoad = new FlatButton { Text = "Tải dữ liệu (F5)", Tag = "primary" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In báo cáo" };

        private readonly Panel _noteBar = new Panel { Dock = DockStyle.Top };
        private readonly Label _lblCashierNote = new Label
        {
            Text = DailyActivityConstants.CashierNote,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        private readonly DailyActivitySections _body;
        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _titleFont;
        private readonly Font _smallFont;

        private ThemeTokens _theme = ThemeManager.Current;
        private DailyActivityReportData _loadedData = new DailyActivityReportData();
        private DailyActivitySummaryResult _loadedSummary = new DailyActivitySummaryResult();

        /// <summary>Tắt hộp thoại modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressErrorPopup { get; set; }

        /// <summary>Ghi nhận thông báo gần nhất (ví dụ bấm nút In báo cáo) để selftest kiểm tra.</summary>
        internal string LastInfoMessageForTest { get; private set; }

        public DailyActivityView(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _session = session;
            _repository = new DailyActivityRepository(session.DatabasePath);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _titleFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 3f, FontStyle.Bold);
            _smallFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);

            _cashierFrame = new FieldFrame(_cboCashier, 0);
            _storeFrame = new FieldFrame(_cboStore, 0);
            _body = new DailyActivitySections(this, ReloadAll, _bodyFont, _headingFont, _titleFont, _smallFont, s);

            BuildTopBar(s);
            BuildNoteBar(s);

            Controls.Add(_body.ScrollHost);
            Controls.Add(_noteBar);
            Controls.Add(_topBar);
            _body.ScrollHost.BringToFront();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            InitializeData();
        }

        private void BuildTopBar(float s)
        {
            _topBar.Padding = new Padding((int)(10 * s), (int)(6 * s), (int)(10 * s), (int)(4 * s));

            _fromDate.Inner.Font = _bodyFont;
            _fromDate.Size = new Size((int)(134 * s), (int)(32 * s));
            _fromDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));

            _toDate.Inner.Font = _bodyFont;
            _toDate.Size = new Size((int)(134 * s), (int)(32 * s));
            _toDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));

            _cboCashier.Font = _bodyFont;
            _cashierFrame.Size = new Size((int)(180 * s), (int)(32 * s));
            _cashierFrame.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));

            _cboStore.Font = _bodyFont;
            _storeFrame.Size = new Size((int)(200 * s), (int)(32 * s));
            _storeFrame.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));

            _btnLoad.Font = _bodyFont;
            int loadW = Math.Max((int)(124 * s), TextRenderer.MeasureText(_btnLoad.Text, _bodyFont).Width + (int)(26 * s));
            _btnLoad.Size = new Size(loadW, (int)(32 * s));
            _btnLoad.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(8 * s), (int)(3 * s));
            _btnLoad.Click += delegate { ReloadAll(); };

            _btnPrint.Font = _bodyFont;
            int printW = Math.Max((int)(104 * s), TextRenderer.MeasureText(_btnPrint.Text, _bodyFont).Width + (int)(26 * s));
            _btnPrint.Size = new Size(printW, (int)(32 * s));
            _btnPrint.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            _btnPrint.Click += delegate
            {
                LastInfoMessageForTest = DailyActivityConstants.PrintUnsupportedMessage;
                if (!SuppressErrorPopup)
                    Msg.Info(this, DailyActivityConstants.PrintUnsupportedMessage);
            };

            _topBar.Controls.Add(MakeFlowLabel("Từ ngày", s));
            _topBar.Controls.Add(_fromDate);
            _topBar.Controls.Add(MakeFlowLabel("Đến ngày", s));
            _topBar.Controls.Add(_toDate);
            _topBar.Controls.Add(MakeFlowLabel("Thu ngân", s));
            _topBar.Controls.Add(_cashierFrame);
            _topBar.Controls.Add(MakeFlowLabel("Cửa hàng", s));
            _topBar.Controls.Add(_storeFrame);
            _topBar.Controls.Add(_btnLoad);
            _topBar.Controls.Add(_btnPrint);
        }

        private void BuildNoteBar(float s)
        {
            _noteBar.Height = (int)(24 * s);
            _noteBar.Padding = new Padding((int)(14 * s), 0, (int)(14 * s), (int)(2 * s));
            _noteBar.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawLine(pen, 0, _noteBar.Height - 1, _noteBar.Width, _noteBar.Height - 1);
            };
            _lblCashierNote.Font = _smallFont;
            _noteBar.Controls.Add(_lblCashierNote);
        }

        private Label MakeFlowLabel(string text, float s)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(TextRenderer.MeasureText(text, _bodyFont).Width + (int)(8 * s), (int)(34 * s)),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont,
                Margin = new Padding((int)(4 * s), (int)(2 * s), 0, (int)(2 * s))
            };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ReloadAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ Dữ liệu

        private void InitializeData()
        {
            try
            {
                DateTime dbToday = _repository.GetServerDate();
                _fromDate.Value = new DateTime(dbToday.Year, dbToday.Month, 1);
                _toDate.Value = dbToday;
                LoadCashiers(false);
                LoadStores(true);
            }
            catch (DataAccessException ex)
            {
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không khởi tạo được bộ lọc báo cáo hoạt động ngày: " + ex.Message);
            }
            LoadReportData();
        }

        /// <summary>Làm mới danh mục lọc (giữ lựa chọn hiện tại) và tải lại báo cáo.</summary>
        public void ReloadAll()
        {
            try
            {
                LoadCashiers(true);
                LoadStores(false);
            }
            catch (DataAccessException ex)
            {
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không làm mới được danh mục bộ lọc: " + ex.Message);
            }
            LoadReportData();
        }

        private void LoadCashiers(bool preserveSelection)
        {
            var keep = preserveSelection ? (_cboCashier.SelectedItem as CashierRecord) : null;
            string keepId = keep == null ? "" : (keep.Id ?? "");

            List<CashierRecord> cashiers = _repository.LoadCashiers();
            _cboCashier.Items.Clear();
            _cboCashier.Items.Add(new CashierRecord { Id = "", Name = "", UserName = "" });
            foreach (CashierRecord c in cashiers)
                _cboCashier.Items.Add(c);

            int idx = 0;
            if (keepId.Length > 0)
            {
                for (int i = 1; i < _cboCashier.Items.Count; i++)
                {
                    var item = _cboCashier.Items[i] as CashierRecord;
                    if (item != null && string.Equals(item.Id, keepId, StringComparison.Ordinal))
                    {
                        idx = i;
                        break;
                    }
                }
            }
            _cboCashier.SelectedIndex = idx;
        }

        private void LoadStores(bool applySingleStoreDefault)
        {
            var keep = applySingleStoreDefault ? null : (_cboStore.SelectedItem as StoreRecord);
            string keepId = keep == null ? "" : (keep.Id ?? "");

            List<StoreRecord> stores = _repository.LoadAllowedStores(_session.UserId, _session.IsAdmin);
            _cboStore.Items.Clear();
            _cboStore.Items.Add(new StoreRecord { Id = "", Name = "" });
            foreach (StoreRecord st in stores)
                _cboStore.Items.Add(st);

            int idx = 0;
            if (applySingleStoreDefault)
            {
                // Q15: user chỉ có 1 cửa hàng được phép thì chọn sẵn, ngược lại để trống.
                if (stores.Count == 1)
                    idx = 1;
            }
            else if (keepId.Length > 0)
            {
                for (int i = 1; i < _cboStore.Items.Count; i++)
                {
                    var item = _cboStore.Items[i] as StoreRecord;
                    if (item != null && string.Equals(item.Id, keepId, StringComparison.Ordinal))
                    {
                        idx = i;
                        break;
                    }
                }
            }
            _cboStore.SelectedIndex = idx;
        }

        private DailyActivityFilter BuildCurrentFilter(out string cashierDisplay, out string storeDisplay)
        {
            var cashier = _cboCashier.SelectedItem as CashierRecord;
            var store = _cboStore.SelectedItem as StoreRecord;
            string cashierId = cashier == null ? "" : (cashier.Id ?? "");
            string storeId = store == null ? "" : (store.Id ?? "");

            cashierDisplay = cashierId.Length == 0 ? DailyActivityConstants.AllLabel : cashier.ToString();
            storeDisplay = storeId.Length == 0 ? DailyActivityConstants.AllLabel : (store.Name ?? "");

            return new DailyActivityFilter
            {
                FromDate = _fromDate.Value.Date,
                ToDate = _toDate.Value.Date,
                CashierId = cashierId,
                StoreId = storeId,
                UserId = _session.UserId,
                IsAdmin = _session.IsAdmin
            };
        }

        private void LoadReportData()
        {
            string cashierDisplay;
            string storeDisplay;
            DailyActivityFilter filter = BuildCurrentFilter(out cashierDisplay, out storeDisplay);
            try
            {
                _loadedData = _repository.LoadReport(filter);
                _loadedSummary = DailyActivitySummary.Calculate(_loadedData);
                _body.BindData(_loadedData, _loadedSummary, filter.FromDate, filter.ToDate, cashierDisplay, storeDisplay);
            }
            catch (DataAccessException ex)
            {
                _loadedData = new DailyActivityReportData();
                _loadedSummary = new DailyActivitySummaryResult();
                _body.BindData(_loadedData, _loadedSummary, filter.FromDate, filter.ToDate, cashierDisplay, storeDisplay);
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được dữ liệu hoạt động ngày: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ Giao diện / Theme

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;

            _topBar.BackColor = t.Background;
            _fromDate.ApplyTheme(t, t.Background);
            _toDate.ApplyTheme(t, t.Background);
            _cashierFrame.ApplyTheme(t, t.Background);
            _storeFrame.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyButton(_btnLoad, t, "primary");
            ThemeApplier.ApplyButton(_btnPrint, t, null);

            _noteBar.BackColor = t.Background;
            _lblCashierNote.BackColor = Color.Transparent;
            _lblCashierNote.ForeColor = t.TextMuted;
            _noteBar.Invalidate();

            _body.ApplyTheme(t);
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal DateBox FromDateBoxForTest { get { return _fromDate; } }
        internal DateBox ToDateBoxForTest { get { return _toDate; } }
        internal ComboBox CashierComboForTest { get { return _cboCashier; } }
        internal ComboBox StoreComboForTest { get { return _cboStore; } }
        internal FlatButton LoadButtonForTest { get { return _btnLoad; } }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }
        internal Label CashierNoteLabelForTest { get { return _lblCashierNote; } }
        internal DailyActivitySections BodyForTest { get { return _body; } }
        internal Font HeadingFontForTest { get { return _headingFont; } }
        internal DailyActivitySummaryResult LoadedSummaryForTest { get { return _loadedSummary; } }

        internal bool DispatchCmdKeyForTest(Keys keyData)
        {
            var m = new Message();
            return ProcessCmdKey(ref m, keyData);
        }

        internal void Preview(string option)
        {
            string opt = (option ?? "").Trim();
            if (opt.Length == 0)
                return;

            if (string.Equals(opt, "bottom", StringComparison.OrdinalIgnoreCase))
            {
                _body.ScrollToBottom();
                return;
            }
            if (string.Equals(opt, "top", StringComparison.OrdinalIgnoreCase))
            {
                _body.ScrollToTop();
                return;
            }

            string[] parts = opt.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return;

            if (parts[0] == "from")
            {
                _fromDate.Inner.Text = parts[1];
                _fromDate.Commit();
                _btnLoad.PerformClick();
            }
            else if (parts[0] == "to")
            {
                _toDate.Inner.Text = parts[1];
                _toDate.Commit();
                _btnLoad.PerformClick();
            }
            else if (parts[0] == "cashier")
            {
                SelectComboByIdOrText(_cboCashier, parts[1]);
                _btnLoad.PerformClick();
            }
            else if (parts[0] == "store")
            {
                SelectComboByIdOrText(_cboStore, parts[1]);
                _btnLoad.PerformClick();
            }
        }

        private static void SelectComboByIdOrText(ComboBox combo, string key)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var c = combo.Items[i] as CashierRecord;
                if (c != null && (string.Equals(c.Id, key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.UserName, key, StringComparison.OrdinalIgnoreCase)))
                {
                    combo.SelectedIndex = i;
                    return;
                }
                var s = combo.Items[i] as StoreRecord;
                if (s != null && (string.Equals(s.Id, key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase)))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _body.Dispose();
                _bodyFont.Dispose();
                _headingFont.Dispose();
                _titleFont.Dispose();
                _smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
