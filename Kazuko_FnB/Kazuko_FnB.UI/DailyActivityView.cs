using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FastReport;
using FastReport.Preview;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn báo cáo chỉ xem "Chi tiết hoạt động ngày" (docs/specs/chi-tiet-hoat-dong-ngay.md &amp; in-xuat-excel Q9):
    /// thanh lọc trên (Từ ngày, Đến ngày bắt buộc; Thu ngân và Cửa hàng tùy chọn; Tải dữ liệu (F5))
    /// và trình xem FastReport nhúng trực tiếp phía dưới dùng mẫu SREPORT.LASTTEMPLATEID.
    /// Không chứa SQL; phần khung theo ThemeTokens / ThemeManager, vùng giấy FastReport giữ nguyên.
    /// </summary>
    internal sealed class DailyActivityView : Panel
    {
        public const string MenuId = DailyActivityConstants.MenuId;

        private readonly UserSession _session;
        private readonly DailyActivityRepository _repository;
        private readonly EmbeddedReportPreviewController _reportController = new EmbeddedReportPreviewController();

        private readonly FlowLayoutPanel _topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly DateBox _fromDate = new DateBox(false);
        private readonly DateBox _toDate = new DateBox(false);
        private readonly ComboBox _cboCashier = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _cashierFrame;
        private readonly ComboBox _cboStore = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _storeFrame;
        private readonly FlatButton _btnLoad = new FlatButton { Text = "Tải dữ liệu (F5)", Tag = "primary" };

        private readonly Font _bodyFont;
        private ThemeTokens _theme = ThemeManager.Current;
        private DailyActivityReportData _loadedData = new DailyActivityReportData();
        private DailyActivitySummaryResult _loadedSummary = new DailyActivitySummaryResult();
        private string _lastDataErrorMessage;

        /// <summary>Tắt hộp thoại modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressErrorPopup { get; set; }

        /// <summary>Số lượt tải dữ liệu báo cáo thành công.</summary>
        internal int LoadCountForTest { get; private set; }

        public DailyActivityView(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _session = session;
            _repository = new DailyActivityRepository(session.DatabasePath);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);

            _cashierFrame = new FieldFrame(_cboCashier, 0);
            _storeFrame = new FieldFrame(_cboStore, 0);

            BuildTopBar(s);

            Controls.Add(_reportController.PreviewControl);
            Controls.Add(_topBar);
            _reportController.PreviewControl.BringToFront();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            InitializeData();
        }

        private void BuildTopBar(float s)
        {
            _topBar.Padding = new Padding((int)(10 * s), (int)(6 * s), (int)(10 * s), (int)(6 * s));
            _topBar.Paint += delegate (object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
            };

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
            _btnLoad.Margin = new Padding((int)(2 * s), (int)(3 * s), 0, (int)(3 * s));
            _btnLoad.Click += delegate { ReloadAll(); };

            _topBar.Controls.Add(MakeFlowLabel("Từ ngày", s));
            _topBar.Controls.Add(_fromDate);
            _topBar.Controls.Add(MakeFlowLabel("Đến ngày", s));
            _topBar.Controls.Add(_toDate);
            _topBar.Controls.Add(MakeFlowLabel("Thu ngân", s));
            _topBar.Controls.Add(_cashierFrame);
            _topBar.Controls.Add(MakeFlowLabel("Cửa hàng", s));
            _topBar.Controls.Add(_storeFrame);
            _topBar.Controls.Add(_btnLoad);
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
                _lastDataErrorMessage = "Không khởi tạo được bộ lọc báo cáo hoạt động ngày: " + ex.Message;
                if (!SuppressErrorPopup)
                    Msg.Error(this, _lastDataErrorMessage);
            }
            LoadReportData();
        }

        /// <summary>Làm mới danh mục lọc (giữ lựa chọn hiện tại) và tải lại báo cáo.</summary>
        public void ReloadAll()
        {
            if (_reportController.IsBusy)
                return;

            try
            {
                LoadCashiers(true);
                LoadStores(false);
            }
            catch (DataAccessException ex)
            {
                _lastDataErrorMessage = "Không làm mới được danh mục bộ lọc: " + ex.Message;
                if (!SuppressErrorPopup)
                    Msg.Error(this, _lastDataErrorMessage);
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

        private DailyActivityFilter BuildCurrentFilter(out string cashierDisplay)
        {
            var cashier = _cboCashier.SelectedItem as CashierRecord;
            var store = _cboStore.SelectedItem as StoreRecord;
            string cashierId = cashier == null ? "" : (cashier.Id ?? "");
            string storeId = store == null ? "" : (store.Id ?? "");

            cashierDisplay = cashierId.Length == 0 ? DailyActivityConstants.AllLabel : cashier.ToString();

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
            DailyActivityFilter filter = BuildCurrentFilter(out cashierDisplay);
            _lastDataErrorMessage = null;

            _reportController.RunLoad(
                this,
                _btnLoad,
                _session.DatabasePath,
                PrintConstants.DailyActivityReportId,
                DailyActivityConstants.FunctionName,
                delegate
                {
                    try
                    {
                        _loadedData = _repository.LoadReport(filter);
                        _loadedSummary = DailyActivitySummary.Calculate(_loadedData);
                        LoadCountForTest++;
                        return DailyActivityReportDataBuilder.Build(_loadedData, filter.FromDate, filter.ToDate, cashierDisplay, null);
                    }
                    catch (DataAccessException ex)
                    {
                        _loadedData = new DailyActivityReportData();
                        _loadedSummary = new DailyActivitySummaryResult();
                        _lastDataErrorMessage = "Không đọc được dữ liệu hoạt động ngày: " + ex.Message;
                        if (!SuppressErrorPopup)
                            Msg.Error(this, _lastDataErrorMessage);
                        return null;
                    }
                },
                SuppressErrorPopup);
        }

        // ------------------------------------------------------------------ Giao diện / Theme

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            BackColor = t.Background;
            ForeColor = t.Text;

            // Chỉ tô màu phần khung thanh lọc; giữ nguyên vùng giấy và thanh công cụ của FastReport PreviewControl.
            ThemeApplier.Apply(_topBar, t);
            _topBar.BackColor = t.Background;
            _fromDate.ApplyTheme(t, t.Background);
            _toDate.ApplyTheme(t, t.Background);
            _cashierFrame.ApplyTheme(t, t.Background);
            _storeFrame.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyButton(_btnLoad, t, "primary");
            _topBar.Invalidate();
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal FlowLayoutPanel TopBarForTest { get { return _topBar; } }
        internal DateBox FromDateBoxForTest { get { return _fromDate; } }
        internal DateBox ToDateBoxForTest { get { return _toDate; } }
        internal ComboBox CashierComboForTest { get { return _cboCashier; } }
        internal ComboBox StoreComboForTest { get { return _cboStore; } }
        internal FlatButton LoadButtonForTest { get { return _btnLoad; } }
        internal DailyActivitySummaryResult LoadedSummaryForTest { get { return _loadedSummary; } }
        internal DailyActivityReportData LoadedDataForTest { get { return _loadedData; } }
        internal PreviewControl PreviewControlForTest { get { return _reportController.PreviewControl; } }
        internal Report CurrentReportForTest { get { return _reportController.CurrentReport; } }
        internal int DisposedReportCountForTest { get { return _reportController.DisposedReportCount; } }
        internal bool IsLoadingForTest { get { return _reportController.IsBusy; } }
        internal string LastErrorMessageForTest { get { return _reportController.LastErrorMessage ?? _lastDataErrorMessage; } }

        internal EmbeddedReportTemplateLoader TemplateLoaderForTest
        {
            get { return _reportController.TemplateLoaderForTest; }
            set { _reportController.TemplateLoaderForTest = value; }
        }

        internal Action OnBeforePrepareForTest
        {
            get { return _reportController.OnBeforePrepareForTest; }
            set { _reportController.OnBeforePrepareForTest = value; }
        }

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

            if (string.Equals(opt, "bottom", StringComparison.OrdinalIgnoreCase)
                || string.Equals(opt, "top", StringComparison.OrdinalIgnoreCase))
            {
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
                _reportController.Dispose();
                _bodyFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
