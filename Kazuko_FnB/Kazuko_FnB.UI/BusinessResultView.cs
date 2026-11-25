using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn báo cáo chỉ xem "Tổng hợp kết quả kinh doanh" (docs/specs/tong-hop-ket-qua-kinh-doanh.md):
    /// thanh lọc trên (Từ ngày mặc định 01 tháng hiện tại, Đến ngày hôm nay theo DB, bắt buộc;
    /// Cửa hàng tùy chọn theo quyền user, chỉ chọn sẵn khi có đúng 1 cửa hàng; Tải dữ liệu (F5); In báo cáo),
    /// thanh tiêu đề kỳ so sánh, và lưới chỉ đọc 9 cột đúng thứ tự LOAI 0..9.
    /// Không chứa SQL; không xuất Excel; không thêm lệnh sao chép/xuất; màu và cỡ chữ từ ThemeTokens / ThemeManager.
    /// </summary>
    internal sealed partial class BusinessResultView : Panel
    {
        public const string MenuId = BusinessResultConstants.MenuId;

        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        private readonly UserSession _session;
        private readonly BusinessResultRepository _repository;

        private readonly FlowLayoutPanel _topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly DateBox _fromDate = new DateBox(true);
        private readonly DateBox _toDate = new DateBox(true);
        private readonly ComboBox _cboStore = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _storeFrame;
        private readonly FlatButton _btnLoad = new FlatButton { Text = "Tải dữ liệu (F5)", Tag = "primary" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In báo cáo" };

        private readonly Panel _headerBar = new Panel { Dock = DockStyle.Top };
        private readonly Label _lblTitle = new Label
        {
            Text = BusinessResultConstants.ReportTitle,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        private readonly Label _lblPeriodInfo = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        private readonly Panel _gridHost = new Panel { Dock = DockStyle.Fill };
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill };

        private readonly Dictionary<string, int> _baseColWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Font _bodyFont;
        private readonly Font _boldFont;
        private readonly Font _subHeadingFont;
        private readonly Font _titleFont;
        private readonly Font _smallFont;
        private readonly float _scale;

        private ThemeTokens _theme = ThemeManager.Current;
        private List<BusinessResultRow> _rows = new List<BusinessResultRow>();
        private BusinessResultPeriodRange _loadedCurrentPeriod;
        private BusinessResultPeriodRange _loadedPreviousPeriod;

        /// <summary>Tắt hộp thoại modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressErrorPopup { get; set; }

        /// <summary>Ghi nhận thông báo gần nhất (ví dụ bấm nút In báo cáo) để selftest kiểm tra.</summary>
        internal string LastInfoMessageForTest { get; private set; }

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
            _boldFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize, FontStyle.Bold);
            _subHeadingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _titleFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 2f, FontStyle.Bold);
            _smallFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);

            _storeFrame = new FieldFrame(_cboStore, 0);

            BuildTopBar();
            BuildHeaderBar();
            BuildGrid();

            Controls.Add(_gridHost);
            Controls.Add(_headerBar);
            Controls.Add(_topBar);
            _gridHost.BringToFront();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            InitializeData();
        }

        private void BuildTopBar()
        {
            float s = _scale;
            _topBar.Padding = new Padding((int)(10 * s), (int)(6 * s), (int)(10 * s), (int)(4 * s));

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
            _btnLoad.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(8 * s), (int)(3 * s));
            _btnLoad.Click += delegate { ReloadAll(); };

            _btnPrint.Font = _bodyFont;
            int printW = Math.Max((int)(104 * s), TextRenderer.MeasureText(_btnPrint.Text, _bodyFont).Width + (int)(26 * s));
            _btnPrint.Size = new Size(printW, (int)(32 * s));
            _btnPrint.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            _btnPrint.Click += delegate
            {
                LastInfoMessageForTest = BusinessResultConstants.PrintUnsupportedMessage;
                if (!SuppressErrorPopup)
                    Msg.Info(this, BusinessResultConstants.PrintUnsupportedMessage);
            };

            _topBar.Controls.Add(MakeFlowLabel("Từ ngày", s));
            _topBar.Controls.Add(_fromDate);
            _topBar.Controls.Add(MakeFlowLabel("Đến ngày", s));
            _topBar.Controls.Add(_toDate);
            _topBar.Controls.Add(MakeFlowLabel("Cửa hàng", s));
            _topBar.Controls.Add(_storeFrame);
            _topBar.Controls.Add(_btnLoad);
            _topBar.Controls.Add(_btnPrint);
        }

        private void BuildHeaderBar()
        {
            float s = _scale;
            int padX = (int)(12 * s);
            int titleH = (int)(26 * s);
            int infoH = (int)(20 * s);
            _headerBar.Height = titleH + infoH + (int)(10 * s);
            _headerBar.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawLine(pen, 0, _headerBar.Height - 1, _headerBar.Width, _headerBar.Height - 1);
            };

            _lblTitle.Font = _titleFont;
            _lblTitle.SetBounds(padX, (int)(4 * s), 600, titleH);
            _lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _lblPeriodInfo.Font = _smallFont;
            _lblPeriodInfo.SetBounds(padX, (int)(4 * s) + titleH, 600, infoH);
            _lblPeriodInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _headerBar.SizeChanged += delegate
            {
                int w = Math.Max(100, _headerBar.ClientSize.Width - padX * 2);
                _lblTitle.Width = w;
                _lblPeriodInfo.Width = w;
            };

            _headerBar.Controls.Add(_lblTitle);
            _headerBar.Controls.Add(_lblPeriodInfo);
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
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không khởi tạo được bộ lọc báo cáo kết quả kinh doanh: " + ex.Message);
            }
            LoadReportData();
        }

        /// <summary>Kiểm tra ngày bắt buộc, làm mới danh sách cửa hàng (giữ lựa chọn) và tải lại báo cáo.</summary>
        public void ReloadAll()
        {
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
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không làm mới được danh mục cửa hàng: " + ex.Message);
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
            string storeDisplay = storeId.Length == 0 ? BusinessResultConstants.AllLabel : (store.Name ?? "");

            BusinessResultPeriodRange prevPeriod = BusinessResultCalculator.CalculatePreviousPeriod(from, to);
            var filter = new BusinessResultFilter
            {
                FromDate = from,
                ToDate = to,
                StoreId = storeId,
                UserId = _session.UserId,
                IsAdmin = _session.IsAdmin
            };

            try
            {
                BusinessResultRawData raw = _repository.LoadRawData(filter, prevPeriod);
                _rows = BusinessResultCalculator.BuildRows(raw);
                _loadedCurrentPeriod = raw.CurrentPeriod;
                _loadedPreviousPeriod = raw.PreviousPeriod;
                LoadCountForTest++;
            }
            catch (DataAccessException ex)
            {
                _rows = new List<BusinessResultRow>();
                _loadedCurrentPeriod = new BusinessResultPeriodRange { FromDate = from, ToDate = to };
                _loadedPreviousPeriod = prevPeriod;
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được dữ liệu kết quả kinh doanh: " + ex.Message);
            }

            _lblPeriodInfo.Text =
                "Kỳ này: " + from.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +
                " – " + to.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +
                "   |   Kỳ trước: " + prevPeriod.FromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +
                " – " + prevPeriod.ToDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +
                "   |   Cửa hàng: " + storeDisplay;

            BindRowsToGrid();
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal FlowLayoutPanel TopBarForTest { get { return _topBar; } }
        internal DateBox FromDateBoxForTest { get { return _fromDate; } }
        internal DateBox ToDateBoxForTest { get { return _toDate; } }
        internal ComboBox StoreComboForTest { get { return _cboStore; } }
        internal FlatButton LoadButtonForTest { get { return _btnLoad; } }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }
        internal DataGridView GridForTest { get { return _grid; } }
        internal Label PeriodInfoLabelForTest { get { return _lblPeriodInfo; } }
        internal Font BoldFontForTest { get { return _boldFont; } }
        internal List<BusinessResultRow> LoadedRowsForTest { get { return _rows; } }
        internal BusinessResultPeriodRange LoadedCurrentPeriodForTest { get { return _loadedCurrentPeriod; } }
        internal BusinessResultPeriodRange LoadedPreviousPeriodForTest { get { return _loadedPreviousPeriod; } }

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
                ScrollToBottom();
                return;
            }
            if (string.Equals(opt, "top", StringComparison.OrdinalIgnoreCase))
            {
                if (_grid.Rows.Count > 0)
                    _grid.FirstDisplayedScrollingRowIndex = 0;
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
                _bodyFont.Dispose();
                _boldFont.Dispose();
                _subHeadingFont.Dispose();
                _titleFont.Dispose();
                _smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
