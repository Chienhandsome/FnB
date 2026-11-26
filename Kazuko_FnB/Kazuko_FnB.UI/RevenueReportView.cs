using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn chỉ xem "Thống kê doanh thu" (docs/specs/thong-ke-doanh-thu.md):
    /// thanh lọc trên (Từ, Đến mặc định ngày DB, Cửa hàng theo quyền user/admin, Refresh (F5), In báo cáo),
    /// thân chính chia đôi dọc bằng SplitContainer (trái: lưới danh sách bill 14 cột có cuộn ngang;
    /// phải: lưới chi tiết 7 cột của bill đang chọn),
    /// dưới cùng: khối tổng cộng 3 nhóm (Doanh thu, Hình thức thanh toán, Thu chi) thuần decimal.
    /// Không chứa SQL; mọi màu và cỡ chữ từ ThemeTokens / ThemeManager.
    /// </summary>
    internal sealed class RevenueReportView : Panel
    {
        public const string MenuId = RevenueReportConstants.MenuId;

        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        private readonly UserSession _session;
        private readonly RevenueReportRepository _repository;

        // Thanh lọc trên
        private readonly FlowLayoutPanel _topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly DateBox _fromDate = new DateBox(false);
        private readonly DateBox _toDate = new DateBox(false);
        private readonly ComboBox _cboStore = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _storeFrame;
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Refresh (F5)", Tag = "primary" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In báo cáo" };

        // Thân chính: trái lưới bill, phải lưới chi tiết
        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

        private readonly Label _billsTitle = new Label { Text = "DANH SÁCH HÓA ĐƠN", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DataGridView _billsGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _billsEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có hóa đơn trong khoảng đã chọn", Visible = false };

        private readonly Label _detailTitle = new Label { Text = "CHI TIẾT HÓA ĐƠN", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DataGridView _detailGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _detailEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Chưa chọn hóa đơn", Visible = false };

        private readonly ThemedContextMenu _billsMenu;
        private readonly ThemedContextMenu _detailMenu;

        // Khối tổng cộng 3 nhóm ở đáy màn hình
        private readonly Panel _bottomPanel = new Panel { Dock = DockStyle.Bottom, AutoSize = true };
        private readonly TableLayoutPanel _bottomLayout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        private readonly TableLayoutPanel _groupsRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 1 };

        private readonly Panel _cardRevenue = new Panel { Dock = DockStyle.Fill, AutoSize = true };
        private readonly Panel _cardPayment = new Panel { Dock = DockStyle.Fill, AutoSize = true };
        private readonly Panel _cardCashFlow = new Panel { Dock = DockStyle.Fill, AutoSize = true };
        private readonly Label _hdrRevenue = new Label { Text = "DOANH THU", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _hdrPayment = new Label { Text = "HÌNH THỨC THANH TOÁN", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _hdrCashFlow = new Label { Text = "THU CHI", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly TableLayoutPanel _tblRevenue = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        private readonly TableLayoutPanel _tblPayment = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        private readonly TableLayoutPanel _tblCashFlow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };

        // Nhãn khối tổng cộng (nguyên văn in hoa theo spec §1)
        private readonly Label _lblGoodsAmountCap = MakeSummaryCaption("TIỀN HÀNG");
        private readonly Label _lblGoodsAmountVal = MakeSummaryValue();
        private readonly Label _lblTimeChargeCap = MakeSummaryCaption("TIỀN GIỜ");
        private readonly Label _lblTimeChargeVal = MakeSummaryValue();
        private readonly Label _lblGoodsDiscountCap = MakeSummaryCaption("GIẢM GIÁ TIỀN HÀNG");
        private readonly Label _lblGoodsDiscountVal = MakeSummaryValue();
        private readonly Label _lblTimeDiscountCap = MakeSummaryCaption("GIẢM GIÁ TIỀN GIỜ");
        private readonly Label _lblTimeDiscountVal = MakeSummaryValue();
        private readonly Label _lblTotalDiscountCap = MakeSummaryCaption("TỔNG GIẢM GIÁ");
        private readonly Label _lblTotalDiscountVal = MakeSummaryValue();
        private readonly Label _lblServiceFeeCap = MakeSummaryCaption("PHÍ DỊCH VỤ");
        private readonly Label _lblServiceFeeVal = MakeSummaryValue();
        private readonly Label _lblTaxCap = MakeSummaryCaption("THUẾ");
        private readonly Label _lblTaxVal = MakeSummaryValue();
        private readonly Label _lblTotalRevenueCap = MakeSummaryCaption("TỔNG DOANH THU");
        private readonly Label _lblTotalRevenueVal = MakeSummaryValue();

        private readonly Label _lblCashCap = MakeSummaryCaption("TIỀN MẶT");
        private readonly Label _lblCashVal = MakeSummaryValue();
        private readonly Label _lblCardCap = MakeSummaryCaption("TIỀN THẺ");
        private readonly Label _lblCardVal = MakeSummaryValue();
        private readonly Label _lblBankTransferCap = MakeSummaryCaption("CHUYỂN KHOẢN");
        private readonly Label _lblBankTransferVal = MakeSummaryValue();
        private readonly Label _lblVoucherCap = MakeSummaryCaption("VOUCHER");
        private readonly Label _lblVoucherVal = MakeSummaryValue();
        private readonly Label _lblLoyaltyCap = MakeSummaryCaption("TRỪ TÍCH LŨY");
        private readonly Label _lblLoyaltyVal = MakeSummaryValue();
        private readonly Label _lblPrepaidCap = MakeSummaryCaption("THẺ TRẢ TRƯỚC");
        private readonly Label _lblPrepaidVal = MakeSummaryValue();
        private readonly Label _lblDebtCap = MakeSummaryCaption("CÔNG NỢ");
        private readonly Label _lblDebtVal = MakeSummaryValue();

        private readonly Label _lblOtherIncomeCap = MakeSummaryCaption("THU KHÁC");
        private readonly Label _lblOtherIncomeVal = MakeSummaryValue();
        private readonly Label _lblOtherExpenseCap = MakeSummaryCaption("CHI KHÁC");
        private readonly Label _lblOtherExpenseVal = MakeSummaryValue();

        // Thanh tổng thực thu nổi bật phía dưới 3 nhóm (full-width để không cắt chữ ở 125% DPI)
        private readonly Panel _netReceiptPanel = new Panel { Dock = DockStyle.Top, AutoSize = true };
        private readonly TableLayoutPanel _netReceiptTable = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1 };
        private readonly Label _lblNetReceiptCap = MakeSummaryCaption(RevenueReportConstants.NetActualReceiptCaption);
        private readonly Label _lblNetReceiptVal = MakeSummaryValue();

        private readonly ToolTip _tip = new ToolTip();
        private readonly Dictionary<string, int> _baseBillWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _baseDetailWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _smallHeadingFont;
        private readonly Font _totalFont;

        private ThemeTokens _theme = ThemeManager.Current;
        private List<RevenueBillRow> _bills = new List<RevenueBillRow>();
        private List<RevenueBillLine> _lines = new List<RevenueBillLine>();
        private RevenueSummary _summary = new RevenueSummary();
        private readonly Printing.RevenueReportPrintCoordinator _printCoordinator = new Printing.RevenueReportPrintCoordinator();
        private DateTime _loadedFromDate;
        private DateTime _loadedToDate;
        private string _loadedDetailBillId;
        private bool _loading = true;
        private bool _suppressSelectionEvents;
        private bool _splitPlaced;

        /// <summary>Tắt hộp thoại báo lỗi/thông báo modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressErrorPopup { get; set; }

        /// <summary>Ghi nhận thông báo cuối cùng để selftest kiểm khi SuppressErrorPopup = true.</summary>
        internal string LastInfoMessageForTest { get; private set; }

        internal Printing.IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> PrintTemplatePickerForTest { get; set; }

        public RevenueReportView(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _session = session;
            _repository = new RevenueReportRepository(session.DatabasePath);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _smallHeadingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.SmallFontSize);
            _totalFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 2f, FontStyle.Bold);

            _storeFrame = new FieldFrame(_cboStore, 0);

            BuildTopBar(s);
            BuildBillsPane(s);
            BuildDetailPane(s);
            BuildSummaryPanel(s);

            _billsMenu = DetailGridMenu.AttachMenu(_billsGrid, this, ReloadAll);
            _detailMenu = DetailGridMenu.AttachMenu(_detailGrid, this, delegate { ReloadSelectedBillLines(true); });

            _split.SplitterWidth = Math.Max(5, (int)(6 * s));
            _split.SizeChanged += delegate { PlaceSplit(); };

            Controls.Add(_split);
            Controls.Add(_bottomPanel);
            Controls.Add(_topBar);
            _split.BringToFront();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            InitializeData();
        }

        // ------------------------------------------------------------------ Dựng giao diện

        private void BuildTopBar(float s)
        {
            _topBar.Padding = new Padding((int)(10 * s), (int)(6 * s), (int)(10 * s), (int)(6 * s));

            _fromDate.Inner.Font = _bodyFont;
            _fromDate.Size = new Size((int)(136 * s), (int)(32 * s));
            _fromDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));
            _fromDate.ValueChanged += delegate { if (!_loading) ReloadReportData(true); };

            _toDate.Inner.Font = _bodyFont;
            _toDate.Size = new Size((int)(136 * s), (int)(32 * s));
            _toDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _toDate.ValueChanged += delegate { if (!_loading) ReloadReportData(true); };

            _cboStore.Font = _bodyFont;
            _storeFrame.Size = new Size((int)(220 * s), (int)(32 * s));
            _storeFrame.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _cboStore.SelectedIndexChanged += delegate { if (!_loading) ReloadReportData(true); };

            _btnRefresh.Font = _bodyFont;
            int refreshW = Math.Max((int)(112 * s), TextRenderer.MeasureText(_btnRefresh.Text, _bodyFont).Width + (int)(26 * s));
            _btnRefresh.Size = new Size(refreshW, (int)(32 * s));
            _btnRefresh.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(8 * s), (int)(3 * s));
            _btnRefresh.Click += delegate { ReloadAll(); };

            _btnPrint.Font = _bodyFont;
            int printW = Math.Max((int)(108 * s), TextRenderer.MeasureText(_btnPrint.Text, _bodyFont).Width + (int)(26 * s));
            _btnPrint.Size = new Size(printW, (int)(32 * s));
            _btnPrint.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            _btnPrint.Click += delegate { PrintReport(); };

            _topBar.Controls.Add(MakeFlowLabel("Từ", s));
            _topBar.Controls.Add(_fromDate);
            _topBar.Controls.Add(MakeFlowLabel("Đến", s));
            _topBar.Controls.Add(_toDate);
            _topBar.Controls.Add(MakeFlowLabel("Cửa hàng", s));
            _topBar.Controls.Add(_storeFrame);
            _topBar.Controls.Add(_btnRefresh);
            _topBar.Controls.Add(_btnPrint);
        }

        private void BuildBillsPane(float s)
        {
            _billsTitle.Font = _headingFont;
            _billsTitle.Height = (int)(30 * s);
            _billsTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            ConfigureGrid(_billsGrid, s);
            _billsGrid.ScrollBars = ScrollBars.Both;

            // Đúng thứ tự 14 cột dữ liệu theo spec §1 & §2.1 (có cuộn ngang, không cắt chữ):
            // Số phiếu, Ngày, Bàn, Giờ vào, Giờ ra, Giờ thanh toán, Khách hàng,
            // Tiền giảm giá, Tổng cộng, Thanh toán bởi, Tiền hàng, Ghi chú, Địa chỉ, Mã khách.
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colSoPhieu", "Số phiếu", 112, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "HD261125-0001");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colNgay", "Ngày", 96, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "dd/MM/yyyy", CultureInfo.InvariantCulture, "25/11/2026");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colBan", "Bàn", 92, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Bàn VIP 01");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colGioVao", "Giờ vào", 76, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "HH:mm", CultureInfo.InvariantCulture, "23:59");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colGioRa", "Giờ ra", 76, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "HH:mm", CultureInfo.InvariantCulture, "23:59");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colGioThanhToan", "Giờ thanh toán", 112, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "HH:mm", CultureInfo.InvariantCulture, "23:59");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colKhachHang", "Khách hàng", 136, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Khách hàng lẻ");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colTienGiamGia", "Tiền giảm giá", 108, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colTongCong", "Tổng cộng", 112, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colThanhToanBoi", "Thanh toán bởi", 124, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Thu ngân 01");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colTienHang", "Tiền hàng", 108, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colGhiChu", "Ghi chú", 148, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Ghi chú hóa đơn");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colDiaChi", "Địa chỉ", 156, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "123 Nguyễn Huệ, Q1");
            AddGridColumn(_billsGrid, _baseBillWidths, s, "colMaKhach", "Mã khách", 96, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "KH00001");

            _billsGrid.SelectionChanged += delegate
            {
                if (!_loading && !_suppressSelectionEvents)
                    ReloadSelectedBillLines(false);
            };
            _billsGrid.CurrentCellChanged += delegate
            {
                if (!_loading && !_suppressSelectionEvents)
                    ReloadSelectedBillLines(false);
            };

            _billsEmpty.Font = _bodyFont;
            _billsEmpty.Height = (int)(32 * s);

            _split.Panel1.Controls.Add(_billsGrid);
            _split.Panel1.Controls.Add(_billsEmpty);
            _split.Panel1.Controls.Add(_billsTitle);
            _billsGrid.BringToFront();
        }

        private void BuildDetailPane(float s)
        {
            _detailTitle.Font = _headingFont;
            _detailTitle.Height = (int)(30 * s);
            _detailTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            ConfigureGrid(_detailGrid, s);
            _detailGrid.ScrollBars = ScrollBars.Both;

            // Đúng 7 cột theo spec §2.2 (bỏ Mã hàng, Bảo hành, Mã sẵn có theo Q10):
            // Mặt hàng, ĐVT, S.lượng, Đơn giá, CK%, Thành tiền, Ghi chú.
            AddGridColumn(_detailGrid, _baseDetailWidths, s, "colMatHang", "Mặt hàng", 164, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Trà đào cam sả");
            AddGridColumn(_detailGrid, _baseDetailWidths, s, "colDvt", "ĐVT", 66, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Ly");
            AddGridColumn(_detailGrid, _baseDetailWidths, s, "colSoLuong", "S.lượng", 78, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "#,##0.##", ViCulture, "100,5");
            AddGridColumn(_detailGrid, _baseDetailWidths, s, "colDonGia", "Đơn giá", 96, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddGridColumn(_detailGrid, _baseDetailWidths, s, "colCkPct", "CK%", 62, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "0.##", ViCulture, "100");
            AddGridColumn(_detailGrid, _baseDetailWidths, s, "colThanhTien", "Thành tiền", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddGridColumn(_detailGrid, _baseDetailWidths, s, "colChiTietGhiChu", "Ghi chú", 136, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Ít đường ít đá");

            _detailEmpty.Font = _bodyFont;
            _detailEmpty.Height = (int)(32 * s);

            _split.Panel2.Controls.Add(_detailGrid);
            _split.Panel2.Controls.Add(_detailEmpty);
            _split.Panel2.Controls.Add(_detailTitle);
            _detailGrid.BringToFront();
        }

        private void BuildSummaryPanel(float s)
        {
            int padX = (int)(10 * s);
            int padY = (int)(6 * s);
            _bottomPanel.Padding = new Padding(padX, padY, padX, padY);
            _bottomPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
            };

            int rowH = (int)(22 * s);
            int totalRowH = (int)(30 * s);
            int moneyW = Math.Max((int)(120 * s), MeasureLabelWidth("-999.999.999", _headingFont, s));
            int totalMoneyW = Math.Max((int)(150 * s), MeasureLabelWidth("-999.999.999", _totalFont, s));

            _bottomLayout.Margin = Padding.Empty;
            _bottomLayout.Padding = Padding.Empty;
            _bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _bottomLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _bottomLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _groupsRow.Margin = Padding.Empty;
            _groupsRow.Padding = Padding.Empty;
            _groupsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            _groupsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            _groupsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            _groupsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            SetupSummaryCard(_cardRevenue, _hdrRevenue, _tblRevenue, moneyW, s, new Padding(0, 0, (int)(8 * s), 0));
            SetupSummaryCard(_cardPayment, _hdrPayment, _tblPayment, moneyW, s, new Padding(0, 0, (int)(8 * s), 0));
            SetupSummaryCard(_cardCashFlow, _hdrCashFlow, _tblCashFlow, moneyW, s, Padding.Empty);

            // Nhóm 1: Doanh thu (Q7: ẩn TIỀN GIỜ và GIẢM GIÁ TIỀN GIỜ, vẫn cộng vào TỔNG GIẢM GIÁ)
            AddSummaryRow(_tblRevenue, _lblGoodsAmountCap, _lblGoodsAmountVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblRevenue, _lblTimeChargeCap, _lblTimeChargeVal, _bodyFont, _bodyFont, rowH, false);
            AddSummaryRow(_tblRevenue, _lblGoodsDiscountCap, _lblGoodsDiscountVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblRevenue, _lblTimeDiscountCap, _lblTimeDiscountVal, _bodyFont, _bodyFont, rowH, false);
            AddSummaryRow(_tblRevenue, _lblTotalDiscountCap, _lblTotalDiscountVal, _bodyFont, _headingFont, rowH, true);
            AddSummaryRow(_tblRevenue, _lblServiceFeeCap, _lblServiceFeeVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblRevenue, _lblTaxCap, _lblTaxVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblRevenue, _lblTotalRevenueCap, _lblTotalRevenueVal, _headingFont, _headingFont, rowH + (int)(2 * s), true);

            // Nhóm 2: Hình thức thanh toán
            AddSummaryRow(_tblPayment, _lblCashCap, _lblCashVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblPayment, _lblCardCap, _lblCardVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblPayment, _lblBankTransferCap, _lblBankTransferVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblPayment, _lblVoucherCap, _lblVoucherVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblPayment, _lblLoyaltyCap, _lblLoyaltyVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblPayment, _lblPrepaidCap, _lblPrepaidVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblPayment, _lblDebtCap, _lblDebtVal, _bodyFont, _bodyFont, rowH, true);

            // Nhóm 3: Thu chi
            AddSummaryRow(_tblCashFlow, _lblOtherIncomeCap, _lblOtherIncomeVal, _bodyFont, _bodyFont, rowH, true);
            AddSummaryRow(_tblCashFlow, _lblOtherExpenseCap, _lblOtherExpenseVal, _bodyFont, _bodyFont, rowH, true);

            _groupsRow.Controls.Add(_cardRevenue, 0, 0);
            _groupsRow.Controls.Add(_cardPayment, 1, 0);
            _groupsRow.Controls.Add(_cardCashFlow, 2, 0);

            // Thanh TỔNG THỰC THU full-width ở dưới cùng
            _netReceiptPanel.Margin = new Padding(0, (int)(6 * s), 0, 0);
            _netReceiptPanel.Padding = new Padding((int)(10 * s), (int)(4 * s), (int)(10 * s), (int)(4 * s));
            _netReceiptPanel.Paint += PaintCardBorder;

            _netReceiptTable.Margin = Padding.Empty;
            _netReceiptTable.Padding = Padding.Empty;
            _netReceiptTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _netReceiptTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, totalMoneyW));
            _netReceiptTable.RowStyles.Add(new RowStyle(SizeType.Absolute, totalRowH));

            _lblNetReceiptCap.Font = _totalFont;
            _lblNetReceiptCap.Height = totalRowH;
            _lblNetReceiptVal.Font = _totalFont;
            _lblNetReceiptVal.Height = totalRowH;
            _netReceiptTable.Controls.Add(_lblNetReceiptCap, 0, 0);
            _netReceiptTable.Controls.Add(_lblNetReceiptVal, 1, 0);
            _netReceiptPanel.Controls.Add(_netReceiptTable);

            // Tooltip P2 giải thích rõ khoản được cộng/trừ và khoản không cộng vào TỔNG THỰC THU
            _tip.SetToolTip(_netReceiptPanel, RevenueReportConstants.NetActualReceiptTooltip);
            _tip.SetToolTip(_netReceiptTable, RevenueReportConstants.NetActualReceiptTooltip);
            _tip.SetToolTip(_lblNetReceiptCap, RevenueReportConstants.NetActualReceiptTooltip);
            _tip.SetToolTip(_lblNetReceiptVal, RevenueReportConstants.NetActualReceiptTooltip);

            _bottomLayout.Controls.Add(_groupsRow, 0, 0);
            _bottomLayout.Controls.Add(_netReceiptPanel, 0, 1);
            _bottomPanel.Controls.Add(_bottomLayout);
        }

        private void SetupSummaryCard(Panel card, Label header, TableLayoutPanel table, int moneyW, float s, Padding margin)
        {
            card.Margin = margin;
            card.Padding = new Padding((int)(10 * s), (int)(5 * s), (int)(10 * s), (int)(5 * s));
            card.Paint += PaintCardBorder;

            header.Font = _smallHeadingFont;
            header.Height = (int)(22 * s);
            header.UseMnemonic = false;
            header.Margin = Padding.Empty;

            table.Margin = Padding.Empty;
            table.Padding = Padding.Empty;
            table.RowCount = 0;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, moneyW));

            card.Controls.Add(table);
            card.Controls.Add(header);
            table.BringToFront();
        }

        private static void AddSummaryRow(
            TableLayoutPanel table,
            Label caption,
            Label value,
            Font captionFont,
            Font valueFont,
            int height,
            bool visible)
        {
            int rowIndex = table.RowCount;
            table.RowCount = rowIndex + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            caption.Font = captionFont;
            caption.Height = height;
            caption.Visible = visible;

            value.Font = valueFont;
            value.Height = height;
            value.Visible = visible;

            table.Controls.Add(caption, 0, rowIndex);
            table.Controls.Add(value, 1, rowIndex);
        }

        private void PaintCardBorder(object sender, PaintEventArgs e)
        {
            var p = sender as Control;
            if (p == null)
                return;
            using (var pen = new Pen(Draw.StrongBorder(_theme)))
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        }

        private static Label MakeSummaryCaption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty,
                UseMnemonic = false
            };
        }

        private static Label MakeSummaryValue()
        {
            return new Label
            {
                Text = "0",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = Padding.Empty,
                UseMnemonic = false
            };
        }

        private static int MeasureLabelWidth(string text, Font font, float s)
        {
            return TextRenderer.MeasureText(text ?? "", font).Width + (int)Math.Ceiling(14 * s);
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

        private void ConfigureGrid(DataGridView grid, float s)
        {
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.Font = _bodyFont;
            grid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(36 * s);
            grid.RowTemplate.Height = (int)(36 * s); // đủ lớn cho thao tác chạm
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(grid, true, null);
        }

        private int MeasureColumnWidth(string header, int minWidthAt96, float s, string sample)
        {
            int pad = (int)Math.Ceiling(22 * s);
            int width = (int)Math.Ceiling(minWidthAt96 * s);
            int headerW = TextRenderer.MeasureText(header ?? "", _headingFont).Width + pad;
            if (headerW > width)
                width = headerW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, _bodyFont).Width + pad;
                if (sampleW > width)
                    width = sampleW;
            }
            return width;
        }

        private void AddGridColumn(
            DataGridView grid,
            Dictionary<string, int> baseWidths,
            float s,
            string name,
            string header,
            int minWidthAt96,
            DataGridViewContentAlignment alignment,
            Type valueType,
            string format,
            IFormatProvider formatProvider,
            string sample)
        {
            int width = MeasureColumnWidth(header, minWidthAt96, s, sample);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                MinimumWidth = width,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ValueType = valueType,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = alignment;
            col.DefaultCellStyle.NullValue = "";
            if (alignment == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            else if (alignment == DataGridViewContentAlignment.MiddleCenter)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (!string.IsNullOrEmpty(format))
                col.DefaultCellStyle.Format = format;
            if (formatProvider != null)
                col.DefaultCellStyle.FormatProvider = formatProvider;

            grid.Columns.Add(col);
            baseWidths[name] = width;
        }

        private void AdjustColumnWidths(DataGridView grid, Dictionary<string, int> baseWidths, int maxWidthAt96)
        {
            float s = DeviceDpi / 96f;
            int pad = (int)Math.Ceiling(22 * s);
            int maxW = (int)Math.Ceiling(maxWidthAt96 * s);

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (!col.Visible)
                    continue;
                int baseWidth;
                if (!baseWidths.TryGetValue(col.Name, out baseWidth))
                    continue;
                int target = baseWidth;
                for (int r = 0; r < grid.Rows.Count; r++)
                {
                    string text = Convert.ToString(grid.Rows[r].Cells[col.Index].FormattedValue);
                    if (string.IsNullOrEmpty(text))
                        continue;
                    int needed = TextRenderer.MeasureText(text, _bodyFont).Width + pad;
                    if (needed > target)
                        target = needed;
                }
                if (target > maxW)
                    target = maxW;
                if (col.Width != target)
                    col.Width = target;
            }
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int minLeft = (int)(400 * s);
            int minRight = (int)(300 * s);
            if (_split.Width > minLeft + minRight + _split.SplitterWidth)
            {
                int want = (int)(_split.Width * 0.62f);
                if (want < minLeft) want = minLeft;
                int max = _split.Width - minRight - _split.SplitterWidth;
                if (want > max) want = max;
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
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
            _loading = true;
            try
            {
                DateTime dbToday = _repository.GetServerDate();
                _fromDate.Value = dbToday;
                _toDate.Value = dbToday;
                LoadStores();
            }
            catch (DataAccessException ex)
            {
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không khởi tạo được dữ liệu thống kê doanh thu: " + ex.Message);
            }
            finally
            {
                _loading = false;
            }
            ReloadReportData(false);
        }

        /// <summary>Làm mới danh sách cửa hàng (giữ lựa chọn), lưới bill (giữ bill đang chọn theo ID nếu còn), chi tiết và khối tổng.</summary>
        public void ReloadAll()
        {
            bool was = _loading;
            _loading = true;
            try
            {
                LoadStores();
            }
            catch (DataAccessException ex)
            {
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không làm mới được danh mục cửa hàng: " + ex.Message);
            }
            finally
            {
                _loading = was;
            }
            ReloadReportData(true);
        }

        private void LoadStores()
        {
            var keep = _cboStore.SelectedItem as StoreRecord;
            string keepId = keep == null ? "" : (keep.Id ?? "");

            List<StoreRecord> stores = _repository.LoadAllowedStores(_session.UserId, _session.IsAdmin);
            _cboStore.Items.Clear();
            // Mục trống đầu danh sách (không chọn cửa hàng).
            _cboStore.Items.Add(new StoreRecord { Id = "", Name = "" });
            foreach (StoreRecord st in stores)
                _cboStore.Items.Add(st);

            int idx = 0;
            if (keepId.Length > 0)
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

        private BillReportScope BuildCurrentScope()
        {
            var selectedStore = _cboStore.SelectedItem as StoreRecord;
            return new BillReportScope
            {
                FromDate = _fromDate.Value.Date,
                ToDate = _toDate.Value.Date,
                StoreId = selectedStore == null ? "" : (selectedStore.Id ?? ""),
                UserId = _session.UserId,
                IsAdmin = _session.IsAdmin
            };
        }

        /// <summary>Tải lại lưới bill, khối tổng và lưới chi tiết bill đang chọn.</summary>
        public void ReloadReportData(bool preserveSelection)
        {
            string keepBillId = null;
            if (preserveSelection)
            {
                RevenueBillRow current = SelectedBill();
                if (current != null && !string.IsNullOrEmpty(current.Id))
                    keepBillId = current.Id;
            }

            _suppressSelectionEvents = true;
            try
            {
                BillReportScope scope = BuildCurrentScope();
                _loadedFromDate = scope.FromDate;
                _loadedToDate = scope.ToDate;
                _bills = _repository.LoadBills(scope);
                RevenueCashFlowTotals cashFlow = _repository.LoadOtherCashFlow(scope);
                _summary = RevenueSummaryCalculator.Calculate(_bills, cashFlow);

                _billsGrid.Rows.Clear();
                foreach (RevenueBillRow bill in _bills)
                {
                    int idx = _billsGrid.Rows.Add(
                        bill.Name ?? "",
                        (object)bill.Ngay,
                        bill.TableName ?? "",
                        (object)bill.StartTime,
                        (object)bill.EndTime,
                        (object)bill.PaidTime,
                        bill.CustomerName ?? "",
                        bill.TienGiamGia,
                        bill.TongCong,
                        bill.PaidBy ?? "",
                        bill.TienHangChuaGiam,
                        bill.Note ?? "",
                        bill.CustomerAddress ?? "",
                        bill.CustomerCode ?? "");
                    _billsGrid.Rows[idx].Tag = bill;
                }
                AdjustColumnWidths(_billsGrid, _baseBillWidths, 360);
                _billsEmpty.Visible = _bills.Count == 0;
                UpdateSummaryDisplay();

                _billsGrid.ClearSelection();
                if (_billsGrid.Rows.Count > 0)
                {
                    int targetRow = 0;
                    if (!string.IsNullOrEmpty(keepBillId))
                    {
                        int found = FindBillRowIndexById(keepBillId);
                        if (found >= 0)
                            targetRow = found;
                    }
                    int firstCol = FirstVisibleBillColumnIndex();
                    if (firstCol >= 0)
                        _billsGrid.CurrentCell = _billsGrid.Rows[targetRow].Cells[firstCol];
                    _billsGrid.Rows[targetRow].Selected = true;
                }
                else
                {
                    _billsGrid.CurrentCell = null;
                }
            }
            catch (DataAccessException ex)
            {
                _bills = new List<RevenueBillRow>();
                _summary = new RevenueSummary();
                _billsGrid.Rows.Clear();
                _billsGrid.CurrentCell = null;
                _billsEmpty.Visible = true;
                UpdateSummaryDisplay();
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được dữ liệu thống kê doanh thu: " + ex.Message);
            }
            finally
            {
                _suppressSelectionEvents = false;
            }

            ReloadSelectedBillLines(true);
        }

        private RevenueBillRow SelectedBill()
        {
            if (_billsGrid.CurrentRow != null && _billsGrid.CurrentRow.Selected)
                return _billsGrid.CurrentRow.Tag as RevenueBillRow;
            if (_billsGrid.SelectedRows.Count > 0)
                return _billsGrid.SelectedRows[0].Tag as RevenueBillRow;
            return null;
        }

        private void ReloadSelectedBillLines(bool force)
        {
            RevenueBillRow bill = SelectedBill();
            string billId = bill == null ? "" : (bill.Id ?? "");
            if (!force && string.Equals(billId, _loadedDetailBillId, StringComparison.Ordinal))
                return;
            _loadedDetailBillId = billId;

            if (billId.Length == 0)
            {
                _lines = new List<RevenueBillLine>();
                _detailGrid.Rows.Clear();
                _detailEmpty.Text = "Chưa chọn hóa đơn";
                _detailEmpty.Visible = true;
                return;
            }

            try
            {
                _lines = _repository.LoadBillLines(billId);
                _detailGrid.Rows.Clear();
                foreach (RevenueBillLine line in _lines)
                {
                    int idx = _detailGrid.Rows.Add(
                        line.ItemName ?? "",
                        line.UnitName ?? "",
                        (object)line.Quantity,
                        (object)line.UnitPrice,
                        (object)line.DiscountRate,
                        (object)line.Amount,
                        line.Note ?? "");
                    _detailGrid.Rows[idx].Tag = line;
                }
                AdjustColumnWidths(_detailGrid, _baseDetailWidths, 320);
                _detailGrid.ClearSelection();
                _detailEmpty.Text = "Hóa đơn không có dòng chi tiết";
                _detailEmpty.Visible = _detailGrid.Rows.Count == 0;
            }
            catch (DataAccessException ex)
            {
                _lines = new List<RevenueBillLine>();
                _detailGrid.Rows.Clear();
                _detailEmpty.Text = "Không đọc được chi tiết hóa đơn";
                _detailEmpty.Visible = true;
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được chi tiết hóa đơn: " + ex.Message);
            }
        }

        private void UpdateSummaryDisplay()
        {
            _lblGoodsAmountVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.GoodsAmount);
            _lblTimeChargeVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.TimeCharge);
            _lblGoodsDiscountVal.Text = RevenueSummaryCalculator.FormatDiscount(_summary.GoodsDiscount);
            _lblTimeDiscountVal.Text = RevenueSummaryCalculator.FormatDiscount(_summary.TimeDiscount);
            _lblTotalDiscountVal.Text = RevenueSummaryCalculator.FormatDiscount(_summary.TotalDiscount);
            _lblServiceFeeVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.ServiceFee);
            _lblTaxVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.Tax);
            _lblTotalRevenueVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.TotalRevenue);

            _lblCashVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.Cash);
            _lblCardVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.Card);
            _lblBankTransferVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.BankTransfer);
            _lblVoucherVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.Voucher);
            _lblLoyaltyVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.LoyaltyDeduction);
            _lblPrepaidVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.PrepaidCard);
            _lblDebtVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.Debt);

            _lblOtherIncomeVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.OtherIncome);
            _lblOtherExpenseVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.OtherExpense);
            _lblNetReceiptVal.Text = RevenueSummaryCalculator.FormatMoney(_summary.NetActualReceipt);
        }

        private int FirstVisibleBillColumnIndex()
        {
            for (int i = 0; i < _billsGrid.Columns.Count; i++)
                if (_billsGrid.Columns[i].Visible)
                    return i;
            return -1;
        }

        // ------------------------------------------------------------------ In báo cáo (logic ở Printing/RevenueReportPrintCoordinator)

        internal bool PrintReport()
        {
            return _printCoordinator.Print(
                this,
                _btnPrint,
                _session.DatabasePath,
                CollectVisibleBillsForPrint(),
                _loadedFromDate,
                _loadedToDate,
                PrintOutputHandlerForTest,
                PrintTemplatePickerForTest,
                delegate (string msg) { LastInfoMessageForTest = msg; });
        }

        internal PrintJobData BuildPrintJobDataForTest()
        {
            return _printCoordinator.BuildJobData(
                _session.DatabasePath,
                CollectVisibleBillsForPrint(),
                _loadedFromDate,
                _loadedToDate);
        }

        private List<RevenueBillRow> CollectVisibleBillsForPrint()
        {
            var rows = new List<RevenueBillRow>(_billsGrid.Rows.Count);
            for (int i = 0; i < _billsGrid.Rows.Count; i++)
            {
                var row = _billsGrid.Rows[i].Tag as RevenueBillRow;
                if (row != null)
                    rows.Add(row);
            }
            return rows;
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal DateTime FromDateValue { get { return _fromDate.Value; } }
        internal DateTime ToDateValue { get { return _toDate.Value; } }
        internal DateBox FromDateBoxForTest { get { return _fromDate; } }
        internal DateBox ToDateBoxForTest { get { return _toDate; } }
        internal ComboBox StoreComboForTest { get { return _cboStore; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }
        internal DataGridView BillsGridForTest { get { return _billsGrid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal int BillRowCount { get { return _billsGrid.Rows.Count; } }
        internal int DetailRowCount { get { return _detailGrid.Rows.Count; } }
        internal RevenueSummary CurrentSummaryForTest { get { return _summary; } }
        internal Font HeadingFontForTest { get { return _headingFont; } }
        internal Label TimeChargeCaptionLabelForTest { get { return _lblTimeChargeCap; } }
        internal Label TimeChargeValueLabelForTest { get { return _lblTimeChargeVal; } }
        internal Label TimeDiscountCaptionLabelForTest { get { return _lblTimeDiscountCap; } }
        internal Label TimeDiscountValueLabelForTest { get { return _lblTimeDiscountVal; } }
        internal Label NetReceiptCaptionLabelForTest { get { return _lblNetReceiptCap; } }
        internal Label NetReceiptValueLabelForTest { get { return _lblNetReceiptVal; } }
        internal Label TotalRevenueValueLabelForTest { get { return _lblTotalRevenueVal; } }
        internal string NetReceiptTooltipForTest { get { return _tip.GetToolTip(_lblNetReceiptCap) ?? ""; } }

        internal bool DispatchCmdKeyForTest(Keys keyData)
        {
            var m = new Message();
            return ProcessCmdKey(ref m, keyData);
        }

        internal List<string> BillColumnHeaders()
        {
            var headers = new List<string>(_billsGrid.Columns.Count);
            foreach (DataGridViewColumn col in _billsGrid.Columns)
                headers.Add(col.HeaderText);
            return headers;
        }

        internal List<string> DetailColumnHeaders()
        {
            var headers = new List<string>(_detailGrid.Columns.Count);
            foreach (DataGridViewColumn col in _detailGrid.Columns)
                headers.Add(col.HeaderText);
            return headers;
        }

        internal int FindBillRowIndexById(string billId)
        {
            for (int i = 0; i < _billsGrid.Rows.Count; i++)
            {
                var row = _billsGrid.Rows[i].Tag as RevenueBillRow;
                if (row != null && string.Equals(row.Id, billId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        internal string GetSelectedBillId()
        {
            RevenueBillRow b = SelectedBill();
            return b == null ? "" : (b.Id ?? "");
        }

        internal bool SelectBillRow(int index)
        {
            int firstCol = FirstVisibleBillColumnIndex();
            if (index < 0 || index >= _billsGrid.Rows.Count || firstCol < 0)
                return false;
            _billsGrid.CurrentCell = _billsGrid.Rows[index].Cells[firstCol];
            _billsGrid.Rows[index].Selected = true;
            return true;
        }

        internal string GetSummaryCaptionText(string kind)
        {
            switch (kind)
            {
                case "goods": return _lblGoodsAmountCap.Text;
                case "time": return _lblTimeChargeCap.Text;
                case "goodsDiscount": return _lblGoodsDiscountCap.Text;
                case "timeDiscount": return _lblTimeDiscountCap.Text;
                case "totalDiscount": return _lblTotalDiscountCap.Text;
                case "service": return _lblServiceFeeCap.Text;
                case "tax": return _lblTaxCap.Text;
                case "totalRevenue": return _lblTotalRevenueCap.Text;
                case "cash": return _lblCashCap.Text;
                case "card": return _lblCardCap.Text;
                case "bankTransfer": return _lblBankTransferCap.Text;
                case "voucher": return _lblVoucherCap.Text;
                case "loyalty": return _lblLoyaltyCap.Text;
                case "prepaid": return _lblPrepaidCap.Text;
                case "debt": return _lblDebtCap.Text;
                case "otherIncome": return _lblOtherIncomeCap.Text;
                case "otherExpense": return _lblOtherExpenseCap.Text;
                case "netReceipt": return _lblNetReceiptCap.Text;
                default: return "";
            }
        }

        internal string GetSummaryValueText(string kind)
        {
            switch (kind)
            {
                case "goods": return _lblGoodsAmountVal.Text;
                case "time": return _lblTimeChargeVal.Text;
                case "goodsDiscount": return _lblGoodsDiscountVal.Text;
                case "timeDiscount": return _lblTimeDiscountVal.Text;
                case "totalDiscount": return _lblTotalDiscountVal.Text;
                case "service": return _lblServiceFeeVal.Text;
                case "tax": return _lblTaxVal.Text;
                case "totalRevenue": return _lblTotalRevenueVal.Text;
                case "cash": return _lblCashVal.Text;
                case "card": return _lblCardVal.Text;
                case "bankTransfer": return _lblBankTransferVal.Text;
                case "voucher": return _lblVoucherVal.Text;
                case "loyalty": return _lblLoyaltyVal.Text;
                case "prepaid": return _lblPrepaidVal.Text;
                case "debt": return _lblDebtVal.Text;
                case "otherIncome": return _lblOtherIncomeVal.Text;
                case "otherExpense": return _lblOtherExpenseVal.Text;
                case "netReceipt": return _lblNetReceiptVal.Text;
                default: return "";
            }
        }

        internal void Preview(string option)
        {
            string[] parts = (option ?? "").Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return;

            if (parts[0] == "from")
            {
                _fromDate.Inner.Text = parts[1];
                _fromDate.Commit();
            }
            else if (parts[0] == "to")
            {
                _toDate.Inner.Text = parts[1];
                _toDate.Commit();
            }
            else if (parts[0] == "select")
            {
                int idx;
                if (int.TryParse(parts[1], out idx) && idx >= 0 && idx < _billsGrid.Rows.Count)
                    SelectBillRow(idx);
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
            _storeFrame.ApplyTheme(t, t.Background);

            ThemeApplier.ApplyButton(_btnRefresh, t, "primary");
            ThemeApplier.ApplyButton(_btnPrint, t, null);

            Color strong = Draw.StrongBorder(t);
            _split.BackColor = strong;
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _billsTitle.ForeColor = t.Primary;
            _detailTitle.ForeColor = t.Primary;

            ThemeApplier.ApplyGrid(_billsGrid, t);
            ThemeApplier.ApplyGrid(_detailGrid, t);
            _billsEmpty.BackColor = t.Background;
            _billsEmpty.ForeColor = t.TextMuted;
            _detailEmpty.BackColor = t.Background;
            _detailEmpty.ForeColor = t.TextMuted;

            Color summaryBg = Draw.Mix(t.Surface, t.Background, 0.35f);
            _bottomPanel.BackColor = summaryBg;
            _bottomLayout.BackColor = summaryBg;
            _groupsRow.BackColor = summaryBg;

            ApplySummaryCardTheme(_cardRevenue, _hdrRevenue, _tblRevenue, t);
            ApplySummaryCardTheme(_cardPayment, _hdrPayment, _tblPayment, t);
            ApplySummaryCardTheme(_cardCashFlow, _hdrCashFlow, _tblCashFlow, t);

            _lblTotalRevenueCap.ForeColor = t.Primary;
            _lblTotalRevenueVal.ForeColor = t.Primary;

            Color netBg = Draw.Mix(t.Surface, t.Danger, 0.06f);
            _netReceiptPanel.BackColor = netBg;
            _netReceiptTable.BackColor = netBg;
            _lblNetReceiptCap.BackColor = Color.Transparent;
            _lblNetReceiptVal.BackColor = Color.Transparent;
            _lblNetReceiptCap.ForeColor = t.Danger;
            _lblNetReceiptVal.ForeColor = t.Danger;

            _billsGrid.Invalidate();
            _detailGrid.Invalidate();
            _bottomPanel.Invalidate(true);
        }

        private static void ApplySummaryCardTheme(Panel card, Label header, TableLayoutPanel table, ThemeTokens t)
        {
            card.BackColor = t.Surface;
            header.BackColor = Color.Transparent;
            header.ForeColor = t.Primary;
            table.BackColor = t.Surface;

            for (int i = 0; i < table.Controls.Count; i++)
            {
                var lbl = table.Controls[i] as Label;
                if (lbl == null)
                    continue;
                lbl.BackColor = Color.Transparent;
                int col = table.GetColumn(lbl);
                lbl.ForeColor = col == 0 ? t.TextMuted : t.Text;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _tip.Dispose();
                _bodyFont.Dispose();
                _headingFont.Dispose();
                _smallHeadingFont.Dispose();
                _totalFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
