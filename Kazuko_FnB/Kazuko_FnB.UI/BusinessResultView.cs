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
    /// Màn báo cáo chỉ xem "Tổng hợp kết quả kinh doanh" (docs/specs/tong-hop-ket-qua-kinh-doanh.md &amp; in-xuat-excel Q9):
    /// thanh lọc trên (Từ ngày mặc định 01 tháng hiện tại, Đến ngày hôm nay theo DB, bắt buộc;
    /// Cửa hàng tùy chọn theo quyền user, chỉ chọn sẵn khi có đúng 1 cửa hàng; Tải dữ liệu (F5))
    /// và trình xem FastReport nhúng trực tiếp phía dưới dùng mẫu SREPORT.LASTTEMPLATEID.
    /// Không chứa SQL; phần khung theo ThemeTokens / ThemeManager, vùng giấy FastReport giữ nguyên.
    /// </summary>
    internal sealed class BusinessResultView : Panel
    {
        public const string MenuId = BusinessResultConstants.MenuId;

        private readonly UserSession _session;
        private readonly BusinessResultRepository _repository;
        private readonly EmbeddedReportPreviewController _reportController = new EmbeddedReportPreviewController();

        private readonly FlowLayoutPanel _topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly DateBox _fromDate = new DateBox(true);
        private readonly DateBox _toDate = new DateBox(true);
        private readonly ComboBox _cboStore = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _storeFrame;
        private readonly FlatButton _btnLoad = new FlatButton { Text = "Tải dữ liệu (F5)", Tag = "primary" };

        private readonly Font _bodyFont;
        private readonly float _scale;

        private ThemeTokens _theme = ThemeManager.Current;
        private List<BusinessResultRow> _rows = new List<BusinessResultRow>();
        private BusinessResultPeriodRange _loadedCurrentPeriod;
        private BusinessResultPeriodRange _loadedPreviousPeriod;
        private string _lastDataErrorMessage;

        /// <summary>Tắt hộp thoại modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressErrorPopup { get; set; }

        /// <summary>Ghi nhận cảnh báo gần nhất (ví dụ thiếu Từ ngày / Đến ngày) để selftest kiểm tra.</summary>
        internal string LastWarningMessageForTest { get; private set; }

        /// <summary>Số lượt tải báo cáo thành công (để selftest kiểm tra thiếu ngày không chạy truy vấn).</summary>
        internal int LoadCountForTest { get; private set; }

        public BusinessResultView(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _session = session;
            _repository = new BusinessResultRepository(session.DatabasePath);

            DoubleBuffered = true;
            _scale = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);

            _storeFrame = new FieldFrame(_cboStore, 0);

            BuildTopBar();

            Controls.Add(_reportController.PreviewControl);
            Controls.Add(_topBar);
            _reportController.PreviewControl.BringToFront();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            InitializeData();
        }

        private void BuildTopBar()
        {
            float s = _scale;
            _topBar.Padding = new Padding((int)(10 * s), (int)(6 * s), (int)(10 * s), (int)(6 * s));
            _topBar.Paint += delegate (object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
            };

            _fromDate.Inner.Font = _bodyFont;
            _fromDate.Size = new Size((int)(136 * s), (int)(32 * s));
            _fromDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));

            _toDate.Inner.Font = _bodyFont;
            _toDate.Size = new Size((int)(136 * s), (int)(32 * s));
            _toDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));

            _cboStore.Font = _bodyFont;
            _storeFrame.Size = new Size((int)(220 * s), (int)(32 * s));
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
                _fromDate.NullableValue = new DateTime(dbToday.Year, dbToday.Month, 1);
                _toDate.NullableValue = dbToday;
                LoadStores(true);
            }
            catch (DataAccessException ex)
            {
                _lastDataErrorMessage = "Không khởi tạo được bộ lọc báo cáo kết quả kinh doanh: " + ex.Message;
                if (!SuppressErrorPopup)
                    Msg.Error(this, _lastDataErrorMessage);
            }
            LoadReportData();
        }

        /// <summary>Kiểm tra ngày bắt buộc, làm mới danh sách cửa hàng (giữ lựa chọn) và tải lại báo cáo.</summary>
        public void ReloadAll()
        {
            if (_reportController.IsBusy)
                return;

            DateTime from;
            DateTime to;
            if (!TryValidateDates(out from, out to))
                return;

            try
            {
                LoadStores(false);
            }
            catch (DataAccessException ex)
            {
                _lastDataErrorMessage = "Không làm mới được danh mục cửa hàng: " + ex.Message;
                if (!SuppressErrorPopup)
                    Msg.Error(this, _lastDataErrorMessage);
            }
            LoadReportDataCore(from, to);
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
                // C9: chỉ chọn sẵn khi user có đúng 1 cửa hàng được phép; còn lại để trống.
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

        private bool TryValidateDates(out DateTime from, out DateTime to)
        {
            DateTime? nullableFrom = _fromDate.NullableValue;
            DateTime? nullableTo = _toDate.NullableValue;
            if (!nullableFrom.HasValue || !nullableTo.HasValue)
            {
                from = DateTime.MinValue;
                to = DateTime.MinValue;
                LastWarningMessageForTest = BusinessResultConstants.MissingDateMessage;
                if (!SuppressErrorPopup)
                    Msg.Warning(this, BusinessResultConstants.MissingDateMessage);
                return false;
            }

            LastWarningMessageForTest = null;
            from = nullableFrom.Value.Date;
            to = nullableTo.Value.Date;
            return true;
        }

        private void LoadReportData()
        {
            DateTime from;
            DateTime to;
            if (!TryValidateDates(out from, out to))
                return;
            LoadReportDataCore(from, to);
        }

        private void LoadReportDataCore(DateTime from, DateTime to)
        {
            var store = _cboStore.SelectedItem as StoreRecord;
            string storeId = store == null ? "" : (store.Id ?? "");

            BusinessResultPeriodRange prevPeriod = BusinessResultCalculator.CalculatePreviousPeriod(from, to);
            var filter = new BusinessResultFilter
            {
                FromDate = from,
                ToDate = to,
                StoreId = storeId,
                UserId = _session.UserId,
                IsAdmin = _session.IsAdmin
            };

            _lastDataErrorMessage = null;

            _reportController.RunLoad(
                this,
                _btnLoad,
                _session.DatabasePath,
                PrintConstants.BusinessResultReportId,
                BusinessResultConstants.FunctionName,
                delegate
                {
                    try
                    {
                        BusinessResultRawData raw = _repository.LoadRawData(filter, prevPeriod);
                        _rows = BusinessResultCalculator.BuildRows(raw);
                        _loadedCurrentPeriod = raw.CurrentPeriod;
                        _loadedPreviousPeriod = raw.PreviousPeriod;
                        LoadCountForTest++;
                        CompanyPrintInfo company = FormPrintCoordinator.LoadCompanyInfoSafe(_session.DatabasePath);
                        return BusinessResultReportDataBuilder.Build(_rows, from, to, company);
                    }
                    catch (DataAccessException ex)
                    {
                        _rows = new List<BusinessResultRow>();
                        _loadedCurrentPeriod = new BusinessResultPeriodRange { FromDate = from, ToDate = to };
                        _loadedPreviousPeriod = prevPeriod;
                        _lastDataErrorMessage = "Không đọc được dữ liệu kết quả kinh doanh: " + ex.Message;
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
            _storeFrame.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyButton(_btnLoad, t, "primary");
            _topBar.Invalidate();
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal FlowLayoutPanel TopBarForTest { get { return _topBar; } }
        internal DateBox FromDateBoxForTest { get { return _fromDate; } }
        internal DateBox ToDateBoxForTest { get { return _toDate; } }
        internal ComboBox StoreComboForTest { get { return _cboStore; } }
        internal FlatButton LoadButtonForTest { get { return _btnLoad; } }
        internal List<BusinessResultRow> LoadedRowsForTest { get { return _rows; } }
        internal BusinessResultPeriodRange LoadedCurrentPeriodForTest { get { return _loadedCurrentPeriod; } }
        internal BusinessResultPeriodRange LoadedPreviousPeriodForTest { get { return _loadedPreviousPeriod; } }
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
            else if (parts[0] == "store")
            {
                for (int i = 0; i < _cboStore.Items.Count; i++)
                {
                    var st = _cboStore.Items[i] as StoreRecord;
                    if (st != null && (string.Equals(st.Id, parts[1], StringComparison.OrdinalIgnoreCase)
                        || string.Equals(st.Name, parts[1], StringComparison.OrdinalIgnoreCase)))
                    {
                        _cboStore.SelectedIndex = i;
                        break;
                    }
                }
                _btnLoad.PerformClick();
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
