using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Lưới chỉ đọc không cuộn dọc nội bộ (chỉ cuộn ngang khi cột rộng hơn khung)
    /// và chuyển tiếp sự kiện lăn chuột lên vùng cuộn dọc của trang báo cáo.
    /// </summary>
    internal sealed class DailyActivityGrid : DataGridView
    {
        public event Action<int> MouseWheelForwarded;

        public DailyActivityGrid()
        {
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(this, true, null);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Action<int> handler = MouseWheelForwarded;
            if (handler != null)
            {
                handler(e.Delta);
                var handled = e as HandledMouseEventArgs;
                if (handled != null)
                    handled.Handled = true;
                return;
            }
            base.OnMouseWheel(e);
        }

        internal void SimulateMouseWheelForTest(int delta)
        {
            OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 10, 10, delta));
        }
    }

    /// <summary>
    /// Vùng cuộn dọc không tự nhảy vị trí cuộn về control đang giữ focus khi PerformLayout hoặc Focus.
    /// </summary>
    internal sealed class DailyActivityScrollHost : Panel
    {
        public DailyActivityScrollHost()
        {
            AutoScroll = true;
        }

        protected override Point ScrollToControl(Control activeControl)
        {
            return DisplayRectangle.Location;
        }
    }

    /// <summary>
    /// Phần nội dung cuộn dọc của báo cáo "Chi tiết hoạt động ngày":
    /// đầu báo cáo → 1. BÁN HÀNG → 2. MẶT HÀNG BÁN → 3. NHẬP HÀNG (ẩn khi rỗng) → 4. THU-CHI → TỔNG HỢP (8 dòng).
    /// </summary>
    internal sealed partial class DailyActivitySections : IDisposable
    {
        private const string SummaryRowTag = "summary";
        private const string GrandTotalRowTag = "grandtotal";
        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _titleFont;
        private readonly Font _smallFont;
        private readonly float _scale;

        private readonly DailyActivityScrollHost _scrollHost = new DailyActivityScrollHost { Dock = DockStyle.Fill };
        private readonly Panel _page = new Panel { Location = Point.Empty };

        // Đầu báo cáo
        private readonly Panel _headerCard = new Panel();
        private readonly Label _lblCompanyName = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly Label _lblCompanyDetails = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly Label _lblReportTitle = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, UseMnemonic = false, Text = DailyActivityConstants.ReportTitle };
        private readonly Label _lblFilterSummary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, UseMnemonic = false };

        // 4 khối dữ liệu
        private readonly Panel _salesSection = new Panel();
        private readonly Label _salesTitle = new Label { Text = DailyActivityConstants.SectionSalesTitle, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly DailyActivityGrid _salesGrid = new DailyActivityGrid();
        private readonly Dictionary<string, int> _salesBaseWidths = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Panel _itemsSection = new Panel();
        private readonly Label _itemsTitle = new Label { Text = DailyActivityConstants.SectionItemsTitle, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly DailyActivityGrid _itemsGrid = new DailyActivityGrid();
        private readonly Dictionary<string, int> _itemsBaseWidths = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Panel _purchasesSection = new Panel();
        private readonly Label _purchasesTitle = new Label { Text = DailyActivityConstants.SectionPurchasesTitle, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly DailyActivityGrid _purchasesGrid = new DailyActivityGrid();
        private readonly Dictionary<string, int> _purchasesBaseWidths = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Panel _cashFlowSection = new Panel();
        private readonly Label _cashFlowTitle = new Label { Text = DailyActivityConstants.SectionCashFlowTitle, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly DailyActivityGrid _cashFlowGrid = new DailyActivityGrid();
        private readonly Dictionary<string, int> _cashFlowBaseWidths = new Dictionary<string, int>(StringComparer.Ordinal);

        // Khối tổng hợp 8 dòng ở cuối
        private readonly Panel _overallSection = new Panel();
        private readonly Label _overallTitle = new Label { Text = DailyActivityConstants.SectionOverallTitle, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly Panel _overallCard = new Panel();
        private readonly TableLayoutPanel _overallTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
        private readonly Label[] _overallCapLabels = new Label[8];
        private readonly Label[] _overallValLabels = new Label[8];

        private readonly List<ThemedContextMenu> _menus = new List<ThemedContextMenu>();
        private ThemeTokens _theme = ThemeManager.Current;
        private bool _hasPurchases;
        private bool _inLayout;

        public DailyActivitySections(
            Control owner,
            Action reloadAction,
            Font bodyFont,
            Font headingFont,
            Font titleFont,
            Font smallFont,
            float scale)
        {
            _bodyFont = bodyFont;
            _headingFont = headingFont;
            _titleFont = titleFont;
            _smallFont = smallFont;
            _scale = scale;

            BuildHeader();
            BuildSalesSection(owner, reloadAction);
            BuildItemsSection(owner, reloadAction);
            BuildPurchasesSection(owner, reloadAction);
            BuildCashFlowSection(owner, reloadAction);
            BuildOverallSection();

            _page.Controls.Add(_headerCard);
            _page.Controls.Add(_salesSection);
            _page.Controls.Add(_itemsSection);
            _page.Controls.Add(_purchasesSection);
            _page.Controls.Add(_cashFlowSection);
            _page.Controls.Add(_overallSection);
            _scrollHost.Controls.Add(_page);

            _scrollHost.SizeChanged += delegate { LayoutContent(); };
        }

        public Panel ScrollHost { get { return _scrollHost; } }
        public Panel PagePanel { get { return _page; } }
        public Label CompanyNameLabel { get { return _lblCompanyName; } }
        public Label CompanyDetailsLabel { get { return _lblCompanyDetails; } }
        public Label ReportTitleLabel { get { return _lblReportTitle; } }
        public Label FilterSummaryLabel { get { return _lblFilterSummary; } }
        public Panel SalesSection { get { return _salesSection; } }
        public Panel ItemsSection { get { return _itemsSection; } }
        public Panel PurchasesSection { get { return _purchasesSection; } }
        public Panel CashFlowSection { get { return _cashFlowSection; } }
        public Panel OverallSection { get { return _overallSection; } }
        public DailyActivityGrid SalesGrid { get { return _salesGrid; } }
        public DailyActivityGrid ItemsGrid { get { return _itemsGrid; } }
        public DailyActivityGrid PurchasesGrid { get { return _purchasesGrid; } }
        public DailyActivityGrid CashFlowGrid { get { return _cashFlowGrid; } }
        public Label[] OverallCaptionLabels { get { return _overallCapLabels; } }
        public Label[] OverallValueLabels { get { return _overallValLabels; } }

        private void BuildHeader()
        {
            _headerCard.Paint += PaintCardBorder;
            _lblCompanyName.Font = _headingFont;
            _lblCompanyDetails.Font = _smallFont;
            _lblReportTitle.Font = _titleFont;
            _lblFilterSummary.Font = _bodyFont;
            _headerCard.Controls.Add(_lblCompanyName);
            _headerCard.Controls.Add(_lblCompanyDetails);
            _headerCard.Controls.Add(_lblReportTitle);
            _headerCard.Controls.Add(_lblFilterSummary);
        }

        private void BuildSalesSection(Control owner, Action reloadAction)
        {
            SetupSection(_salesSection, _salesTitle, _salesGrid, owner, reloadAction);
            // Đúng 12 cột Mẫu đầy đủ theo spec §1 (Trừ tích lũy TRƯỚC Thẻ trả trước, không thêm cột nào khác).
            AddCol(_salesGrid, _salesBaseWidths, "colStt", "STT", 46, DataGridViewContentAlignment.MiddleCenter, typeof(string), null, null, "999");
            AddCol(_salesGrid, _salesBaseWidths, "colSoHd", "Số HĐ", 112, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "TỔNG CỘNG");
            AddCol(_salesGrid, _salesBaseWidths, "colGioTt", "Giờ tt", 68, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "HH:mm", CultureInfo.InvariantCulture, "23:59");
            AddCol(_salesGrid, _salesBaseWidths, "colKhachHang", "Khách hàng", 136, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Khách hàng lẻ");
            AddCol(_salesGrid, _salesBaseWidths, "colTongCong", "Tổng cộng", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_salesGrid, _salesBaseWidths, "colTienMat", "Tiền mặt", 98, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_salesGrid, _salesBaseWidths, "colChuyenKhoan", "Chuyển khoản", 112, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_salesGrid, _salesBaseWidths, "colTheAtm", "Thẻ ATM", 94, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_salesGrid, _salesBaseWidths, "colVoucher", "Voucher", 90, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddCol(_salesGrid, _salesBaseWidths, "colTruTichLuy", "Trừ tích lũy", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddCol(_salesGrid, _salesBaseWidths, "colTheTraTruoc", "Thẻ trả trước", 108, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddCol(_salesGrid, _salesBaseWidths, "colConNo", "Còn nợ", 96, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
        }

        private void BuildItemsSection(Control owner, Action reloadAction)
        {
            SetupSection(_itemsSection, _itemsTitle, _itemsGrid, owner, reloadAction);
            // Đúng 8 cột theo spec §1.
            AddCol(_itemsGrid, _itemsBaseWidths, "colItemStt", "STT", 46, DataGridViewContentAlignment.MiddleCenter, typeof(string), null, null, "999");
            AddCol(_itemsGrid, _itemsBaseWidths, "colMaHang", "Mã hàng", 96, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "MH00001");
            AddCol(_itemsGrid, _itemsBaseWidths, "colTenHang", "Tên hàng", 240, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, DailyActivityConstants.ItemGrossCaption);
            AddCol(_itemsGrid, _itemsBaseWidths, "colDvt", "ĐVT", 72, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Ly");
            AddCol(_itemsGrid, _itemsBaseWidths, "colSoLuong", "Số lượng", 88, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "#,##0.##", ViCulture, "1.000,5");
            AddCol(_itemsGrid, _itemsBaseWidths, "colDonGia", "Đơn giá", 98, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddCol(_itemsGrid, _itemsBaseWidths, "colPctGiamGia", "% G.giá", 76, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "0.##", ViCulture, "100");
            AddCol(_itemsGrid, _itemsBaseWidths, "colThanhTien", "Thành tiền", 116, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
        }

        private void BuildPurchasesSection(Control owner, Action reloadAction)
        {
            SetupSection(_purchasesSection, _purchasesTitle, _purchasesGrid, owner, reloadAction);
            // Đúng 6 cột theo spec §1.
            AddCol(_purchasesGrid, _purchasesBaseWidths, "colPurStt", "STT", 46, DataGridViewContentAlignment.MiddleCenter, typeof(string), null, null, "999");
            AddCol(_purchasesGrid, _purchasesBaseWidths, "colPurSoPhieu", "Số phiếu", 124, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "TỔNG CỘNG");
            AddCol(_purchasesGrid, _purchasesBaseWidths, "colNhaCungCap", "Nhà cung cấp", 220, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Nhà cung cấp mẫu");
            AddCol(_purchasesGrid, _purchasesBaseWidths, "colPurTongCong", "Tổng cộng", 116, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_purchasesGrid, _purchasesBaseWidths, "colPurThanhToan", "Tiền thanh toán", 128, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_purchasesGrid, _purchasesBaseWidths, "colPurConNo", "Còn nợ", 110, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
        }

        private void BuildCashFlowSection(Control owner, Action reloadAction)
        {
            SetupSection(_cashFlowSection, _cashFlowTitle, _cashFlowGrid, owner, reloadAction);
            // Đúng 8 cột theo spec §1 & §7: STT, Số phiếu, Diễn giải, Tên đối tượng, Thu TM, Chi TM, Thu CK, Chi CK.
            AddCol(_cashFlowGrid, _cashFlowBaseWidths, "colTcStt", "STT", 46, DataGridViewContentAlignment.MiddleCenter, typeof(string), null, null, "999");
            AddCol(_cashFlowGrid, _cashFlowBaseWidths, "colTcSoPhieu", "Số phiếu", 116, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "TỔNG CỘNG");
            AddCol(_cashFlowGrid, _cashFlowBaseWidths, "colDienGiai", "Diễn giải", 200, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Diễn giải thu chi");
            AddCol(_cashFlowGrid, _cashFlowBaseWidths, "colTenDoiTuong", "Tên đối tượng", 160, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Đối tượng giao dịch");
            AddCol(_cashFlowGrid, _cashFlowBaseWidths, "colThuTm", "Thu TM", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_cashFlowGrid, _cashFlowBaseWidths, "colChiTm", "Chi TM", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_cashFlowGrid, _cashFlowBaseWidths, "colThuCk", "Thu CK", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddCol(_cashFlowGrid, _cashFlowBaseWidths, "colChiCk", "Chi CK", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
        }

        private void BuildOverallSection()
        {
            _overallTitle.Font = _headingFont;
            _overallCard.Paint += PaintCardBorder;
            _overallCard.Padding = new Padding((int)(12 * _scale), (int)(8 * _scale), (int)(12 * _scale), (int)(8 * _scale));

            _overallTable.Margin = Padding.Empty;
            _overallTable.Padding = Padding.Empty;
            _overallTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            _overallTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));

            int rowH = (int)(30 * _scale);
            for (int i = 0; i < 8; i++)
            {
                _overallTable.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));
                var cap = new Label
                {
                    Text = DailyActivitySummary.OverallCaptions[i],
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = i < 2 ? _headingFont : _bodyFont,
                    UseMnemonic = false
                };
                var val = new Label
                {
                    Text = DailyActivitySummary.FormatOverallValue(i, 0m),
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = i < 2 ? _headingFont : _bodyFont,
                    UseMnemonic = false
                };
                _overallCapLabels[i] = cap;
                _overallValLabels[i] = val;
                _overallTable.Controls.Add(cap, 0, i);
                _overallTable.Controls.Add(val, 1, i);
            }

            _overallCard.Controls.Add(_overallTable);
            _overallSection.Controls.Add(_overallTitle);
            _overallSection.Controls.Add(_overallCard);
        }

        private void SetupSection(Panel section, Label title, DailyActivityGrid grid, Control owner, Action reloadAction)
        {
            title.Font = _headingFont;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.ScrollBars = ScrollBars.Horizontal;
            grid.Font = _bodyFont;
            grid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(36 * _scale);
            grid.RowTemplate.Height = (int)(36 * _scale);
            grid.MouseWheelForwarded += ForwardWheelToScrollHost;

            _menus.Add(DetailGridMenu.AttachMenu(grid, owner, reloadAction));
            section.Controls.Add(title);
            section.Controls.Add(grid);
        }

        private void AddCol(
            DataGridView grid,
            Dictionary<string, int> baseWidths,
            string name,
            string header,
            int minWidthAt96,
            DataGridViewContentAlignment align,
            Type valueType,
            string format,
            IFormatProvider formatProvider,
            string sample)
        {
            int pad = (int)Math.Ceiling(22 * _scale);
            int width = (int)Math.Ceiling(minWidthAt96 * _scale);
            int headerW = TextRenderer.MeasureText(header ?? "", _headingFont).Width + pad;
            if (headerW > width) width = headerW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, _headingFont).Width + pad;
                if (sampleW > width) width = sampleW;
            }

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
            col.DefaultCellStyle.Alignment = align;
            col.DefaultCellStyle.NullValue = "";
            if (align == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            else if (align == DataGridViewContentAlignment.MiddleCenter)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (!string.IsNullOrEmpty(format))
                col.DefaultCellStyle.Format = format;
            if (formatProvider != null)
                col.DefaultCellStyle.FormatProvider = formatProvider;

            grid.Columns.Add(col);
            baseWidths[name] = width;
        }
    }
}
