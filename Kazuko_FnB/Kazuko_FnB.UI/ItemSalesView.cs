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
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn chỉ xem "Thống kê mặt hàng bán" (docs/specs/thong-ke-mat-hang-ban.md):
    /// thanh lọc trên (Từ, Đến mặc định ngày DB, Cửa hàng, radio Theo giá vốn / Theo giá nhập, Lọc, Refresh (F5), In báo cáo),
    /// bên trái cây nhóm mặt hàng (bỏ nhóm nguyên liệu DLOAIDOID = '4', không có Chưa thiết lập),
    /// bên phải lưới 11 cột mặt hàng bán + khối tổng cộng (tính trên toàn phạm vi ngày &amp; cửa hàng, không đổi theo nhóm/ô Lọc).
    /// Quyền "Xem giá nhập" kiểm trên đúng phiên của view; không có quyền thì ẩn 5 cột, khối tổng và nút In.
    /// Không chứa SQL; mọi màu và cỡ chữ từ ThemeTokens / ThemeManager.
    /// </summary>
    internal sealed class ItemSalesView : Panel
    {
        public const string MenuId = ItemSalesConstants.MenuId;

        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        private static readonly HashSet<string> CostProtectedColumns = new HashSet<string>(StringComparer.Ordinal)
        {
            "colThanhTien",
            "colGiaVon",
            "colThanhTienNhap",
            "colLai",
            "colTiLeLai"
        };

        private readonly UserSession _session;
        private readonly ItemSalesRepository _repository;
        private readonly bool _canViewCost;

        // Thanh lọc trên
        private readonly FlowLayoutPanel _topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly DateBox _fromDate = new DateBox(false);
        private readonly DateBox _toDate = new DateBox(false);
        private readonly ComboBox _cboStore = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _storeFrame;
        private readonly FlowLayoutPanel _costModePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        private readonly RadioButton _rbCostPrice = new RadioButton { Text = "Theo giá vốn", Checked = true, AutoSize = true };
        private readonly RadioButton _rbBuyPrice = new RadioButton { Text = "Theo giá nhập", Checked = false, AutoSize = true };
        private readonly InputBox _txtFilter = new InputBox();
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Refresh (F5)", Tag = "primary" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In báo cáo" };

        // Thân chính: trái cây nhóm, phải lưới + khối tổng
        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        private readonly Label _treeTitle = new Label { Text = "NHÓM MẶT HÀNG", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowLines = true, ShowRootLines = true };

        private readonly Label _gridTitle = new Label { Text = "MẶT HÀNG BÁN", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _emptyLabel = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có mặt hàng bán trong khoảng đã chọn", Visible = false };
        private readonly ThemedContextMenu _gridMenu;

        // Khối tổng dưới (ẩn khi không có quyền Xem giá nhập)
        private readonly Panel _bottomPanel = new Panel { Dock = DockStyle.Bottom, AutoSize = true };
        private readonly TableLayoutPanel _summaryTable = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 6, RowCount = 7 };
        private readonly Label _lblTotalSalesCaption = new Label { Text = "Tổng tiền bán:", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _lblTotalSalesValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _lblTotalCostCaption = new Label { Text = "Tổng tiền nhập", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _lblTotalCostValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _lblGrossProfitValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _lblGrossProfitCaption = new Label { Text = "(Tổng tiền lãi bán hàng)", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _lblTimeChargeValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _lblTimeChargeCaption = new Label { Text = "(Tiền giờ)", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _lblDiscountValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _lblDiscountCaption = new Label { Text = "(Tổng tiền giảm giá)", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _lblServiceFeeValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _lblServiceFeeCaption = new Label { Text = "(Phí dịch vụ)", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _lblTaxValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _lblTaxCaption = new Label { Text = "(Thuế)", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Panel _netProfitDivider = new Panel { Dock = DockStyle.Fill };
        private readonly Label _lblNetProfitValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _lblNetProfitCaption = new Label { Text = "(Tổng tiền lãi)", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };

        private readonly ToolTip _tip = new ToolTip();
        private readonly Dictionary<string, int> _baseColWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _netProfitFont;

        private ThemeTokens _theme = ThemeManager.Current;
        private List<ItemSalesRow> _allRows = new List<ItemSalesRow>();
        private ItemSalesSummary _summary = new ItemSalesSummary();
        private readonly Dictionary<string, string> _groupNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Printing.ItemSalesPrintCoordinator _printCoordinator = new Printing.ItemSalesPrintCoordinator();
        private DateTime _loadedFromDate;
        private DateTime _loadedToDate;
        private string _selectedGroupId;
        private bool _loading = true;
        private bool _splitPlaced;

        /// <summary>Tắt hộp thoại báo lỗi/thông báo modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressErrorPopup { get; set; }

        /// <summary>Ghi nhận thông báo cuối cùng để selftest kiểm khi SuppressErrorPopup = true.</summary>
        internal string LastInfoMessageForTest { get; private set; }

        internal Printing.IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> PrintTemplatePickerForTest { get; set; }

        public ItemSalesView(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _session = session;
            _repository = new ItemSalesRepository(session.DatabasePath);
            _canViewCost = AppSession.Can(_session, ItemSalesConstants.ViewCostPermissionName, RoleModes.View);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _netProfitFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 3f, FontStyle.Bold);

            _storeFrame = new FieldFrame(_cboStore, 0);

            BuildTopBar(s);
            BuildTreePane(s);
            BuildGridPane(s);
            BuildSummaryPanel(s);

            _gridMenu = DetailGridMenu.AttachMenu(_grid, this, ReloadAll);
            _gridMenu.Opening += GridMenu_Opening;

            _split.FixedPanel = FixedPanel.Panel1;
            _split.SplitterWidth = Math.Max(5, (int)(6 * s));
            _split.SizeChanged += delegate { PlaceSplit(); };

            Controls.Add(_split);
            Controls.Add(_bottomPanel);
            Controls.Add(_topBar);
            _split.BringToFront();

            ApplyCostPermissionVisibility();

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
            _fromDate.ValueChanged += delegate { if (!_loading) ReloadReportData(); };

            _toDate.Inner.Font = _bodyFont;
            _toDate.Size = new Size((int)(136 * s), (int)(32 * s));
            _toDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _toDate.ValueChanged += delegate { if (!_loading) ReloadReportData(); };

            _cboStore.Font = _bodyFont;
            _storeFrame.Size = new Size((int)(200 * s), (int)(32 * s));
            _storeFrame.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _cboStore.SelectedIndexChanged += delegate { if (!_loading) ReloadReportData(); };

            _rbCostPrice.Font = _bodyFont;
            _rbCostPrice.Margin = new Padding((int)(2 * s), (int)(6 * s), (int)(10 * s), (int)(2 * s));
            _rbCostPrice.CheckedChanged += delegate
            {
                if (!_loading && _rbCostPrice.Checked)
                    ReloadReportData();
            };

            _rbBuyPrice.Font = _bodyFont;
            _rbBuyPrice.Margin = new Padding(0, (int)(6 * s), (int)(8 * s), (int)(2 * s));
            _rbBuyPrice.CheckedChanged += delegate
            {
                if (!_loading && _rbBuyPrice.Checked)
                    ReloadReportData();
            };

            _costModePanel.Margin = new Padding(0, 0, (int)(8 * s), 0);
            _costModePanel.Controls.Add(_rbCostPrice);
            _costModePanel.Controls.Add(_rbBuyPrice);

            _txtFilter.Inner.Font = _bodyFont;
            _txtFilter.Placeholder = "Lọc tên hàng...";
            _txtFilter.Size = new Size((int)(200 * s), (int)(32 * s));
            _txtFilter.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));
            _txtFilter.TextChanged += delegate { if (!_loading) ApplyGridFilter(); };

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
            _topBar.Controls.Add(_costModePanel);
            _topBar.Controls.Add(MakeFlowLabel("Lọc", s));
            _topBar.Controls.Add(_txtFilter);
            _topBar.Controls.Add(_btnRefresh);
            _topBar.Controls.Add(_btnPrint);
        }

        private void BuildTreePane(float s)
        {
            _treeTitle.Font = _headingFont;
            _treeTitle.Height = (int)(30 * s);
            _treeTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            _tree.Font = _bodyFont;
            _tree.ItemHeight = (int)(30 * s);
            _tree.ShowNodeToolTips = true;
            _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _tree.DrawNode += DrawTreeNode;
            _tree.AfterSelect += delegate(object sender, TreeViewEventArgs e)
            {
                if (_loading || e.Node == null)
                    return;
                _selectedGroupId = e.Node.Tag as string;
                ApplyGridFilter();
            };

            _split.Panel1.Controls.Add(_tree);
            _split.Panel1.Controls.Add(_treeTitle);
            _tree.BringToFront();
        }

        private void BuildGridPane(float s)
        {
            _gridTitle.Font = _headingFont;
            _gridTitle.Height = (int)(30 * s);
            _gridTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            ConfigureGrid(_grid, s);
            _grid.ScrollBars = ScrollBars.Both;

            // Đúng 11 cột theo thứ tự spec §1 và §2.0:
            // Mặt hàng, Số lượng, ĐVT, Đơn giá, Tiền giảm, Thành tiền bán, Giá vốn, Giảm giá %, Thành tiền nhập, Lãi, Tỉ lệ lãi.
            AddColumn(s, "colTenHang", "Mặt hàng", 200, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "Trà sữa trân châu đường đen");
            AddColumn(s, "colSoLuong", "Số lượng", 88, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "#,##0.##", "1.000,5");
            AddColumn(s, "colDvt", "ĐVT", 74, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "Ly");
            AddColumn(s, "colDonGia", "Đơn giá", 100, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", "1.000.000");
            AddColumn(s, "colTienGiam", "Tiền giảm", 98, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", "1.000.000");
            AddColumn(s, "colThanhTien", "Thành tiền bán", 122, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", "10.000.000");
            AddColumn(s, "colGiaVon", "Giá vốn", 98, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", "1.000.000");
            AddColumn(s, "colTiLeGiamGia", "Giảm giá %", 96, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "0.##", "100,5");
            AddColumn(s, "colThanhTienNhap", "Thành tiền nhập", 128, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", "10.000.000");
            AddColumn(s, "colLai", "Lãi", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", "10.000.000");
            AddColumn(s, "colTiLeLai", "Tỉ lệ lãi", 88, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "0.##", "100,55");

            _grid.CellDoubleClick += Grid_CellDoubleClick;

            _emptyLabel.Font = _bodyFont;
            _emptyLabel.Height = (int)(32 * s);

            _split.Panel2.Controls.Add(_grid);
            _split.Panel2.Controls.Add(_emptyLabel);
            _split.Panel2.Controls.Add(_gridTitle);
            _grid.BringToFront();
        }

        private void BuildSummaryPanel(float s)
        {
            _bottomPanel.Padding = new Padding((int)(12 * s), (int)(8 * s), (int)(16 * s), (int)(10 * s));
            _bottomPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawLine(pen, 0, 0, _bottomPanel.Width, 0);
            };

            int rowH = (int)(24 * s);
            int netRowH = (int)(30 * s);
            int divH = Math.Max(4, (int)(6 * s));

            int salesCapW = MeasureLabelWidth("Tổng tiền bán:", _bodyFont, s);
            int costCapW = MeasureLabelWidth("Tổng tiền nhập", _bodyFont, s);
            int moneyW = Math.Max((int)(130 * s), MeasureLabelWidth("-999.999.999", _netProfitFont, s));
            int rightCapW = Math.Max(
                MeasureLabelWidth("(Tổng tiền lãi bán hàng)", _bodyFont, s),
                MeasureLabelWidth("(Tổng tiền lãi)", _netProfitFont, s));

            _summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, salesCapW));
            _summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, moneyW));
            _summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, costCapW + (int)(24 * s)));
            _summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, moneyW));
            _summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, rightCapW));

            _summaryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));
            _summaryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));
            _summaryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));
            _summaryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));
            _summaryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));
            _summaryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, divH));
            _summaryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, netRowH));

            StyleSummaryLabel(_lblTotalSalesCaption, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblTotalSalesValue, _headingFont, DockStyle.Fill);
            StyleSummaryLabel(_lblTotalCostCaption, _bodyFont, DockStyle.Fill);
            _lblTotalCostCaption.Padding = new Padding((int)(20 * s), 0, 0, 0);
            StyleSummaryLabel(_lblTotalCostValue, _headingFont, DockStyle.Fill);

            StyleSummaryLabel(_lblGrossProfitValue, _headingFont, DockStyle.Fill);
            StyleSummaryLabel(_lblGrossProfitCaption, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblTimeChargeValue, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblTimeChargeCaption, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblDiscountValue, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblDiscountCaption, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblServiceFeeValue, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblServiceFeeCaption, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblTaxValue, _bodyFont, DockStyle.Fill);
            StyleSummaryLabel(_lblTaxCaption, _bodyFont, DockStyle.Fill);

            StyleSummaryLabel(_lblNetProfitValue, _netProfitFont, DockStyle.Fill);
            StyleSummaryLabel(_lblNetProfitCaption, _netProfitFont, DockStyle.Fill);

            Padding rightValPad = new Padding(0, 0, (int)(10 * s), 0);
            _lblGrossProfitValue.Padding = rightValPad;
            _lblTimeChargeValue.Padding = rightValPad;
            _lblDiscountValue.Padding = rightValPad;
            _lblServiceFeeValue.Padding = rightValPad;
            _lblTaxValue.Padding = rightValPad;
            _lblNetProfitValue.Padding = rightValPad;

            _netProfitDivider.Margin = new Padding((int)(12 * s), (int)(2 * s), 0, (int)(2 * s));
            _netProfitDivider.Paint += delegate(object sender, PaintEventArgs e)
            {
                int y = _netProfitDivider.Height / 2;
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawLine(pen, 0, y, _netProfitDivider.Width, y);
            };

            // Hàng 0: Tổng tiền bán: [val] · Tổng tiền nhập [val] · [val] (Tổng tiền lãi bán hàng)
            _summaryTable.Controls.Add(_lblTotalSalesCaption, 0, 0);
            _summaryTable.Controls.Add(_lblTotalSalesValue, 1, 0);
            _summaryTable.Controls.Add(_lblTotalCostCaption, 2, 0);
            _summaryTable.Controls.Add(_lblTotalCostValue, 3, 0);
            _summaryTable.Controls.Add(_lblGrossProfitValue, 4, 0);
            _summaryTable.Controls.Add(_lblGrossProfitCaption, 5, 0);

            // Hàng 1–4: 4 dòng xếp dọc
            _summaryTable.Controls.Add(_lblTimeChargeValue, 4, 1);
            _summaryTable.Controls.Add(_lblTimeChargeCaption, 5, 1);
            _summaryTable.Controls.Add(_lblDiscountValue, 4, 2);
            _summaryTable.Controls.Add(_lblDiscountCaption, 5, 2);
            _summaryTable.Controls.Add(_lblServiceFeeValue, 4, 3);
            _summaryTable.Controls.Add(_lblServiceFeeCaption, 5, 3);
            _summaryTable.Controls.Add(_lblTaxValue, 4, 4);
            _summaryTable.Controls.Add(_lblTaxCaption, 5, 4);

            // Hàng 5: đường kẻ phân tách
            _summaryTable.Controls.Add(_netProfitDivider, 4, 5);
            _summaryTable.SetColumnSpan(_netProfitDivider, 2);

            // Hàng 6: dòng cuối (Tổng tiền lãi) nổi bật
            _summaryTable.Controls.Add(_lblNetProfitValue, 4, 6);
            _summaryTable.Controls.Add(_lblNetProfitCaption, 5, 6);

            // Tooltip P3 giải thích công thức Tổng tiền lãi
            _tip.SetToolTip(_lblNetProfitValue, ItemSalesConstants.NetProfitFormulaTooltip);
            _tip.SetToolTip(_lblNetProfitCaption, ItemSalesConstants.NetProfitFormulaTooltip);
            _tip.SetToolTip(_bottomPanel, ItemSalesConstants.NetProfitFormulaTooltip);

            _bottomPanel.Controls.Add(_summaryTable);
        }

        private static void StyleSummaryLabel(Label lbl, Font font, DockStyle dock)
        {
            lbl.Font = font;
            lbl.Dock = dock;
            lbl.Margin = Padding.Empty;
            lbl.UseMnemonic = false;
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
            grid.RowTemplate.Height = (int)(36 * s); // đủ lớn cho màn cảm ứng
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

        private void AddColumn(
            float s, string name, string header, int minWidthAt96,
            DataGridViewContentAlignment alignment, Type valueType,
            string format, string sample)
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
            if (!string.IsNullOrEmpty(format))
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = ViCulture;
            }
            _grid.Columns.Add(col);
            _baseColWidths[name] = width;
        }

        private void AdjustColumnWidths()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)Math.Ceiling(22 * s);
            int maxW = (int)Math.Ceiling(360 * s);

            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (!col.Visible)
                    continue;
                int baseWidth;
                if (!_baseColWidths.TryGetValue(col.Name, out baseWidth))
                    continue;
                int target = baseWidth;
                for (int r = 0; r < _grid.Rows.Count; r++)
                {
                    string text = Convert.ToString(_grid.Rows[r].Cells[col.Index].FormattedValue);
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

        private void ApplyCostPermissionVisibility()
        {
            foreach (string colName in CostProtectedColumns)
            {
                DataGridViewColumn col = _grid.Columns[colName];
                if (col != null)
                    col.Visible = _canViewCost;
            }
            _bottomPanel.Visible = _canViewCost;
            _btnPrint.Visible = _canViewCost;
        }

        private void GridMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_canViewCost)
                return;
            // Không cho bật lại 5 cột bị ẩn bởi quyền Xem giá nhập qua menu chuột phải.
            foreach (ToolStripItem item in _gridMenu.Items)
            {
                var sub = item as ToolStripMenuItem;
                if (sub == null || !string.Equals(sub.Text, "Cột hiển thị", StringComparison.Ordinal))
                    continue;
                for (int i = sub.DropDownItems.Count - 1; i >= 0; i--)
                {
                    string caption = sub.DropDownItems[i].Text;
                    foreach (string colName in CostProtectedColumns)
                    {
                        DataGridViewColumn col = _grid.Columns[colName];
                        if (col != null && string.Equals(col.HeaderText, caption, StringComparison.Ordinal))
                        {
                            sub.DropDownItems.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int want = (int)(240 * s);
            if (_split.Width > want + (int)(320 * s))
            {
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
                LoadGroupTree();
            }
            catch (DataAccessException ex)
            {
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không khởi tạo được dữ liệu thống kê mặt hàng bán: " + ex.Message);
            }
            finally
            {
                _loading = false;
            }
            ReloadReportData();
        }

        /// <summary>Tải lại cửa hàng, cây nhóm (giữ nhóm đang chọn theo P4), dữ liệu lưới và khối tổng.</summary>
        public void ReloadAll()
        {
            bool was = _loading;
            _loading = true;
            try
            {
                LoadStores();
                LoadGroupTree();
            }
            catch (DataAccessException ex)
            {
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không làm mới được danh mục lọc: " + ex.Message);
            }
            finally
            {
                _loading = was;
            }
            ReloadReportData();
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

        private void LoadGroupTree()
        {
            string keepGroupId = _selectedGroupId;
            List<ItemSalesGroupNode> groups = _repository.LoadProductGroups();

            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            var root = new TreeNode("Tất cả") { Tag = null };
            _tree.Nodes.Add(root);

            var byId = new Dictionary<string, ItemSalesGroupNode>(StringComparer.Ordinal);
            _groupNames.Clear();
            foreach (ItemSalesGroupNode g in groups)
            {
                if (!string.IsNullOrEmpty(g.Id))
                {
                    byId[g.Id] = g;
                    _groupNames[g.Id] = g.Name ?? "";
                }
            }

            var byParent = new Dictionary<string, List<ItemSalesGroupNode>>(StringComparer.Ordinal);
            var tops = new List<ItemSalesGroupNode>();
            foreach (ItemSalesGroupNode g in groups)
            {
                if (string.IsNullOrEmpty(g.ParentId) || !byId.ContainsKey(g.ParentId) || string.Equals(g.ParentId, g.Id, StringComparison.Ordinal))
                {
                    tops.Add(g);
                    continue;
                }
                List<ItemSalesGroupNode> kids;
                if (!byParent.TryGetValue(g.ParentId, out kids))
                    byParent[g.ParentId] = kids = new List<ItemSalesGroupNode>();
                kids.Add(g);
            }

            foreach (ItemSalesGroupNode top in tops)
                AddGroupTreeNode(root.Nodes, top, byParent, 0);

            root.ExpandAll();

            // Giữ nhóm đang chọn sau Refresh (P4); nếu nhóm đã bị xóa thì quay về "Tất cả".
            TreeNode selected = string.IsNullOrEmpty(keepGroupId) ? root : (FindGroupNode(root, keepGroupId) ?? root);
            _selectedGroupId = selected.Tag as string;
            _tree.SelectedNode = selected;
            _tree.EndUpdate();
        }

        private static void AddGroupTreeNode(
            TreeNodeCollection into,
            ItemSalesGroupNode node,
            Dictionary<string, List<ItemSalesGroupNode>> byParent,
            int depth)
        {
            var tn = new TreeNode(node.Name ?? "") { Tag = node.Id };
            into.Add(tn);
            List<ItemSalesGroupNode> kids;
            if (depth < 64 && byParent.TryGetValue(node.Id, out kids))
            {
                foreach (ItemSalesGroupNode child in kids)
                    AddGroupTreeNode(tn.Nodes, child, byParent, depth + 1);
            }
        }

        private static TreeNode FindGroupNode(TreeNode from, string groupId)
        {
            foreach (TreeNode child in from.Nodes)
            {
                if (string.Equals(child.Tag as string, groupId, StringComparison.Ordinal))
                    return child;
                TreeNode found = FindGroupNode(child, groupId);
                if (found != null)
                    return found;
            }
            return null;
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

        private ItemSalesCostMode CurrentCostMode()
        {
            return _rbBuyPrice.Checked ? ItemSalesCostMode.ProductBuyPrice : ItemSalesCostMode.LineCost;
        }

        /// <summary>Tải lại dữ liệu lưới (toàn phạm vi ngày + cửa hàng) và khối tổng.</summary>
        public void ReloadReportData()
        {
            try
            {
                BillReportScope scope = BuildCurrentScope();
                _loadedFromDate = scope.FromDate;
                _loadedToDate = scope.ToDate;
                ItemSalesCostMode costMode = CurrentCostMode();
                bool sortByCode = _repository.ShouldSortByCode();

                _allRows = _repository.LoadItemSales(scope, costMode, sortByCode);
                if (_canViewCost)
                {
                    ItemSalesCalculator.ApplyRowMetrics(_allRows);
                    ItemSalesBillTotals billTotals = _repository.LoadBillSummaryTotals(scope);
                    _summary = ItemSalesCalculator.ComputeSummary(_allRows, billTotals);
                }
                else
                {
                    _summary = new ItemSalesSummary();
                }

                UpdateSummaryDisplay();
                ApplyGridFilter();
            }
            catch (DataAccessException ex)
            {
                _allRows = new List<ItemSalesRow>();
                _summary = new ItemSalesSummary();
                UpdateSummaryDisplay();
                _grid.Rows.Clear();
                _emptyLabel.Visible = true;
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được dữ liệu thống kê mặt hàng bán: " + ex.Message);
            }
        }

        private void UpdateSummaryDisplay()
        {
            _lblTotalSalesValue.Text = FormatMoney(_summary.TotalSalesBeforeDiscount);
            _lblTotalCostValue.Text = FormatMoney(_summary.TotalCost);
            _lblGrossProfitValue.Text = FormatMoney(_summary.GrossSalesProfit);
            _lblTimeChargeValue.Text = FormatMoney(_summary.TimeCharge);
            _lblDiscountValue.Text = FormatMoney(_summary.TotalDiscount);
            _lblServiceFeeValue.Text = FormatMoney(_summary.ServiceFee);
            _lblTaxValue.Text = FormatMoney(_summary.Tax);
            _lblNetProfitValue.Text = FormatMoney(_summary.NetProfit);
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("n0", ViCulture);
        }

        /// <summary>Áp bộ lọc cây nhóm và ô Lọc lên lưới; KHÔNG thay đổi khối tổng.</summary>
        private void ApplyGridFilter()
        {
            string query = _txtFilter.Text ?? "";
            _grid.Rows.Clear();
            foreach (ItemSalesRow row in _allRows)
            {
                if (!ItemSalesFilter.Matches(row, _selectedGroupId, query))
                    continue;
                int idx = _grid.Rows.Add(
                    row.ItemName ?? "",
                    row.Quantity,
                    row.UnitName ?? "",
                    row.UnitPrice,
                    row.LineDiscountAmount,
                    _canViewCost ? (object)row.SalesAmount : null,
                    _canViewCost ? (object)row.UnitCost : null,
                    row.DiscountRate,
                    _canViewCost ? (object)row.CostAmount : null,
                    _canViewCost ? (object)row.Profit : null,
                    _canViewCost ? (object)row.ProfitRate : null);
                // Khi không có quyền Xem giá nhập, chỉ gắn khóa mở dialog (không giữ giá vốn/tiền nhập/thành tiền/lãi trong Tag).
                _grid.Rows[idx].Tag = _canViewCost
                    ? (object)row
                    : new ItemSalesDetailKey
                    {
                        ProductId = row.ProductId,
                        ItemName = row.ItemName,
                        UnitPrice = row.UnitPrice,
                        DiscountRate = row.DiscountRate,
                        UnitCost = 0m
                    };
            }
            AdjustColumnWidths();
            _grid.ClearSelection();
            _emptyLabel.Text = _allRows.Count == 0
                ? "Không có mặt hàng bán trong khoảng đã chọn"
                : "Không có mặt hàng khớp điều kiện lọc";
            _emptyLabel.Visible = _grid.Rows.Count == 0;
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
                return;
            using (ItemSalesDetailDialog dlg = CreateDetailDialogForRow(e.RowIndex))
            {
                if (dlg == null)
                    return;
                dlg.SuppressErrorPopup = SuppressErrorPopup;
                dlg.ShowDialog(FindForm());
            }
        }

        /// <summary>Tạo dialog chi tiết cho dòng lưới <paramref name="rowIndex"/> (dùng chung cho nhấp đúp và selftest).</summary>
        internal ItemSalesDetailDialog CreateDetailDialogForRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return null;
            object tag = _grid.Rows[rowIndex].Tag;
            var key = tag as ItemSalesDetailKey;
            if (key == null)
            {
                var row = tag as ItemSalesRow;
                if (row == null)
                    return null;
                key = new ItemSalesDetailKey
                {
                    ProductId = row.ProductId,
                    ItemName = row.ItemName,
                    UnitPrice = row.UnitPrice,
                    DiscountRate = row.DiscountRate,
                    UnitCost = _canViewCost ? row.UnitCost : 0m
                };
            }
            return new ItemSalesDetailDialog(_session.DatabasePath, BuildCurrentScope(), key);
        }

        // ------------------------------------------------------------------ In báo cáo (logic ở Printing/ItemSalesPrintCoordinator)

        internal bool PrintReport()
        {
            return _printCoordinator.Print(
                this,
                _btnPrint,
                _session.DatabasePath,
                _canViewCost,
                CollectVisibleRowsForPrint(),
                _groupNames,
                _summary,
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
                _canViewCost,
                CollectVisibleRowsForPrint(),
                _groupNames,
                _summary,
                _loadedFromDate,
                _loadedToDate);
        }

        private List<ItemSalesRow> CollectVisibleRowsForPrint()
        {
            var rows = new List<ItemSalesRow>(_grid.Rows.Count);
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var row = _grid.Rows[i].Tag as ItemSalesRow;
                if (row != null)
                    rows.Add(row);
            }
            return rows;
        }

        // ------------------------------------------------------------------ Vẽ cây nhóm theo theme

        private void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || e.Bounds.IsEmpty)
                return;
            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            Color back = selected ? _theme.Primary : _tree.BackColor;
            Color fore = selected ? _theme.OnPrimary : _theme.Text;
            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.NodeFont ?? _tree.Font, e.Bounds, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal DateTime FromDateValue { get { return _fromDate.Value; } }
        internal DateTime ToDateValue { get { return _toDate.Value; } }
        internal DateBox FromDateBoxForTest { get { return _fromDate; } }
        internal DateBox ToDateBoxForTest { get { return _toDate; } }
        internal ComboBox StoreComboForTest { get { return _cboStore; } }
        internal RadioButton RadioCostPriceForTest { get { return _rbCostPrice; } }
        internal RadioButton RadioBuyPriceForTest { get { return _rbBuyPrice; } }
        internal InputBox FilterBoxForTest { get { return _txtFilter; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }
        internal TreeView GroupTreeForTest { get { return _tree; } }
        internal DataGridView GridForTest { get { return _grid; } }
        internal Panel BottomPanelForTest { get { return _bottomPanel; } }
        internal Label NetProfitValueLabelForTest { get { return _lblNetProfitValue; } }
        internal Label NetProfitCaptionLabelForTest { get { return _lblNetProfitCaption; } }
        internal string NetProfitTooltipForTest { get { return _tip.GetToolTip(_lblNetProfitCaption) ?? ""; } }
        internal ItemSalesSummary CurrentSummaryForTest { get { return _summary; } }
        internal int GridRowCount { get { return _grid.Rows.Count; } }
        internal Font HeadingFontForTest { get { return _headingFont; } }

        internal List<string> GridColumnHeaders()
        {
            var headers = new List<string>(_grid.Columns.Count);
            foreach (DataGridViewColumn col in _grid.Columns)
                headers.Add(col.HeaderText);
            return headers;
        }

        internal string GetSummaryText(string kind)
        {
            switch (kind)
            {
                case "sales": return _lblTotalSalesValue.Text;
                case "cost": return _lblTotalCostValue.Text;
                case "gross": return _lblGrossProfitValue.Text;
                case "time": return _lblTimeChargeValue.Text;
                case "discount": return _lblDiscountValue.Text;
                case "service": return _lblServiceFeeValue.Text;
                case "tax": return _lblTaxValue.Text;
                case "net": return _lblNetProfitValue.Text;
                default: return "";
            }
        }

        internal bool SelectRow(int index)
        {
            if (index < 0 || index >= _grid.Rows.Count)
                return false;
            int firstVisible = -1;
            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                if (_grid.Columns[i].Visible)
                {
                    firstVisible = i;
                    break;
                }
            }
            if (firstVisible < 0)
                return false;
            _grid.CurrentCell = _grid.Rows[index].Cells[firstVisible];
            _grid.Rows[index].Selected = true;
            return true;
        }

        internal TreeNode FindGroupNodeById(string groupId)
        {
            if (_tree.Nodes.Count == 0)
                return null;
            if (string.IsNullOrEmpty(groupId))
                return _tree.Nodes[0];
            return FindGroupNode(_tree.Nodes[0], groupId);
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
                if (int.TryParse(parts[1], out idx) && idx >= 0 && idx < _grid.Rows.Count)
                    SelectRow(idx);
            }
            else if (parts[0] == "mode")
            {
                if (string.Equals(parts[1], "buy", StringComparison.OrdinalIgnoreCase))
                    _rbBuyPrice.Checked = true;
                else
                    _rbCostPrice.Checked = true;
            }
            else if (parts[0] == "filter")
            {
                _txtFilter.Text = parts[1];
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
            _costModePanel.BackColor = t.Background;
            _rbCostPrice.BackColor = Color.Transparent;
            _rbCostPrice.ForeColor = t.Text;
            _rbBuyPrice.BackColor = Color.Transparent;
            _rbBuyPrice.ForeColor = t.Text;

            _fromDate.ApplyTheme(t, t.Background);
            _toDate.ApplyTheme(t, t.Background);
            _storeFrame.ApplyTheme(t, t.Background);

            Color strong = Draw.StrongBorder(t);
            ThemeApplier.ApplyInput(_txtFilter, t, t.Background, strong);
            ThemeApplier.ApplyButton(_btnRefresh, t, "primary");
            ThemeApplier.ApplyButton(_btnPrint, t, null);

            _split.BackColor = strong;
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _treeTitle.ForeColor = t.Primary;
            _gridTitle.ForeColor = t.Primary;

            _tree.BackColor = t.Surface;
            _tree.ForeColor = t.Text;
            _tree.LineColor = strong;
            _tree.BorderStyle = BorderStyle.FixedSingle;

            ThemeApplier.ApplyGrid(_grid, t);
            _emptyLabel.BackColor = t.Background;
            _emptyLabel.ForeColor = t.TextMuted;

            Color summaryBg = Draw.Mix(t.Surface, t.Background, 0.35f);
            _bottomPanel.BackColor = summaryBg;
            _summaryTable.BackColor = summaryBg;
            _netProfitDivider.BackColor = summaryBg;

            _lblTotalSalesCaption.ForeColor = t.TextMuted;
            _lblTotalSalesValue.ForeColor = t.Text;
            _lblTotalCostCaption.ForeColor = t.TextMuted;
            _lblTotalCostValue.ForeColor = t.Text;
            _lblGrossProfitValue.ForeColor = t.Text;
            _lblGrossProfitCaption.ForeColor = t.TextMuted;
            _lblTimeChargeValue.ForeColor = t.Text;
            _lblTimeChargeCaption.ForeColor = t.TextMuted;
            _lblDiscountValue.ForeColor = t.Text;
            _lblDiscountCaption.ForeColor = t.TextMuted;
            _lblServiceFeeValue.ForeColor = t.Text;
            _lblServiceFeeCaption.ForeColor = t.TextMuted;
            _lblTaxValue.ForeColor = t.Text;
            _lblTaxCaption.ForeColor = t.TextMuted;

            // Dòng (Tổng tiền lãi) nổi bật bằng màu cảnh báo từ token (không hard-code màu).
            _lblNetProfitValue.ForeColor = t.Danger;
            _lblNetProfitCaption.ForeColor = t.Danger;

            _grid.Invalidate();
            _tree.Invalidate();
            _bottomPanel.Invalidate(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _tip.Dispose();
                _bodyFont.Dispose();
                _headingFont.Dispose();
                _netProfitFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
