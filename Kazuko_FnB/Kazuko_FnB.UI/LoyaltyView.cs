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
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn hình Khách hàng thân thiết (docs/specs/khach-hang-than-thiet.md §2, §5):
    /// - Trái: điều kiện lọc (tìm không dấu tên/mã/SĐT/địa chỉ, tích "Chỉ khách có điểm") + cây nhóm khách hàng.
    /// - Phải trên: thanh nút (Lọc F3, Tặng/giảm điểm, Xuất excel, Làm mới F5, tóm tắt) + lưới khách 10 cột hiển thị kèm hàng tổng cộng.
    /// - Phải dưới: 2 tab của khách đang chọn ("Hóa đơn" và "Tặng điểm - tặng quà" với nút Thêm/Sửa/Xóa/Xuất excel theo quyền).
    /// Phần nạp dữ liệu, lọc, thao tác phiếu, xuất Excel, phím tắt đặt ở <c>LoyaltyView.Actions.cs</c>;
    /// phần tô màu theme, hàng tổng, vẽ cây/số dòng, menu ngữ cảnh đặt ở <c>LoyaltyView.Render.cs</c>.
    /// </summary>
    internal sealed partial class LoyaltyView : Panel
    {
        private static readonly object SummaryRowTag = new object();

        private readonly UserSession _session;
        private readonly LoyaltyService _service;

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _summaryFont;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly InputBox _txtSearch = new InputBox();
        private readonly CheckBox _chkOnlyWithPoints = new CheckBox { AutoSize = true, Checked = false };
        private readonly Label _treeTitle = new Label { Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowLines = true, ShowRootLines = true };

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnFilter = new FlatButton { Text = "Lọc (F3)" };
        private readonly FlatButton _btnAddPoint = new FlatButton { Text = "Tặng/giảm điểm", Tag = "primary" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Làm mới (F5)" };
        private readonly Label _summary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

        private readonly SplitContainer _rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        private readonly TabControl _detailTabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private readonly Panel _billBar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnExportBills = new FlatButton { Text = "Xuất excel" };
        private readonly DataGridView _billGrid = new DataGridView();

        private readonly Panel _voucherBar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnVoucherAdd = new FlatButton { Text = "Thêm", Tag = "primary" };
        private readonly FlatButton _btnVoucherEdit = new FlatButton { Text = "Sửa" };
        private readonly FlatButton _btnVoucherDelete = new FlatButton { Text = "Xóa", Tag = "danger" };
        private readonly FlatButton _btnExportVouchers = new FlatButton { Text = "Xuất excel" };
        private readonly DataGridView _voucherGrid = new DataGridView();

        private ThemedContextMenu _gridMenu;
        private ThemedContextMenu _billMenu;
        private ThemedContextMenu _voucherMenu;
        private ThemeTokens _theme = ThemeManager.Current;
        private List<LoyaltyGroupNode> _allGroups = new List<LoyaltyGroupNode>();
        private List<LoyaltyCustomerRow> _allCustomers = new List<LoyaltyCustomerRow>();
        private List<LoyaltyCustomerRow> _visibleCustomers = new List<LoyaltyCustomerRow>();
        private string _selectedGroupTag;
        private bool _loading;
        private bool _splitPlaced;
        private bool _rightSplitPlaced;
        private int _filterPaneWidth;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal Func<string, bool> ConfirmDeleteOverrideForTest { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal string LastInfoMessageForTest { get; private set; }
        internal string LastExportMessageForTest { get; private set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal Func<LoyaltyPointDialog, DialogResult> PointDialogOpenerForTest { get; set; }

        internal DataGridView GridForTest { get { return _grid; } }
        internal DataGridView BillDetailGridForTest { get { return _billGrid; } }
        internal DataGridView VoucherDetailGridForTest { get { return _voucherGrid; } }
        internal TabControl DetailTabsForTest { get { return _detailTabs; } }
        internal InputBox SearchBoxForTest { get { return _txtSearch; } }
        internal CheckBox OnlyWithPointsCheckForTest { get { return _chkOnlyWithPoints; } }
        internal TreeView GroupTreeForTest { get { return _tree; } }
        internal FlatButton FilterButtonForTest { get { return _btnFilter; } }
        internal FlatButton AddPointButtonForTest { get { return _btnAddPoint; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal FlatButton ExportBillsButtonForTest { get { return _btnExportBills; } }
        internal FlatButton VoucherAddButtonForTest { get { return _btnVoucherAdd; } }
        internal FlatButton VoucherEditButtonForTest { get { return _btnVoucherEdit; } }
        internal FlatButton VoucherDeleteButtonForTest { get { return _btnVoucherDelete; } }
        internal FlatButton ExportVouchersButtonForTest { get { return _btnExportVouchers; } }
        internal Label SummaryLabelForTest { get { return _summary; } }

        public LoyaltyView(UserSession session)
        {
            _session = session ?? AppSession.Current;
            _service = new LoyaltyService(_session);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _summaryFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            _loading = true;
            BuildFilterPanel(s);
            BuildBar(s);
            BuildMasterGrid(s);
            BuildDetailTabs(s);

            _rightSplit.Panel1.Controls.Add(_grid);
            _rightSplit.Panel2.Controls.Add(_detailTabs);
            _split.Panel2.Controls.Add(_rightSplit);
            _split.Panel2.Controls.Add(_bar);
            _rightSplit.BringToFront();

            _split.FixedPanel = FixedPanel.Panel1;
            _split.SizeChanged += delegate { PlaceSplit(); };
            _rightSplit.SizeChanged += delegate { PlaceRightSplit(); };
            Controls.Add(_split);
            _loading = false;

            _grid.RowPostPaint += DrawRowNumber;
            _grid.SelectionChanged += delegate
            {
                if (!_loading)
                {
                    UpdateActionState();
                    LoadSelectedDetail();
                }
            };
            _voucherGrid.SelectionChanged += delegate
            {
                if (!_loading)
                    UpdateActionState();
            };
            _voucherGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && CurrentVoucher() != null)
                    OpenEditVoucherDialog(CurrentVoucher(), !_service.CanEdit);
            };

            BuildContextMenus();
            _grid.MouseDown += delegate(object sender, MouseEventArgs e) { ShowGridMenu(_grid, _gridMenu, e); };
            _billGrid.MouseDown += delegate(object sender, MouseEventArgs e) { ShowGridMenu(_billGrid, _billMenu, e); };
            _voucherGrid.MouseDown += delegate(object sender, MouseEventArgs e) { ShowGridMenu(_voucherGrid, _voucherMenu, e); };

            _grid.KeyDown += OnKeyDown;
            _billGrid.KeyDown += OnKeyDown;
            _voucherGrid.KeyDown += OnKeyDown;
            _tree.KeyDown += OnKeyDown;
            KeyDown += OnKeyDown;

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            ReloadAll();
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int want = Math.Max((int)(280 * s), _filterPaneWidth);
            if (_split.Width > want + (int)(360 * s))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        private void PlaceRightSplit()
        {
            if (_rightSplitPlaced)
                return;
            int bottomH = (int)(240 * DeviceDpi / 96f);
            if (_rightSplit.Height > bottomH + (int)(200 * DeviceDpi / 96f))
            {
                _rightSplit.SplitterDistance = _rightSplit.Height - bottomH;
                _rightSplitPlaced = true;
            }
        }

        private void BuildFilterPanel(float s)
        {
            var title = new Label
            {
                Text = "Điều kiện lọc",
                Dock = DockStyle.Top,
                Height = (int)(32 * s),
                Padding = new Padding((int)(10 * s), 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _headingFont
            };

            _chkOnlyWithPoints.Text = "Chỉ khách có điểm";
            _chkOnlyWithPoints.Font = _bodyFont;
            _chkOnlyWithPoints.Checked = false;
            _chkOnlyWithPoints.Margin = new Padding((int)(2 * s), (int)(4 * s), 0, (int)(2 * s));
            _chkOnlyWithPoints.CheckedChanged += delegate { if (!_loading) ApplyLocalFilter(); };

            int lblColW = MeasureMaxWidth(new[] { "Tìm kiếm" }, _bodyFont, (int)Math.Ceiling(16 * s));
            int rowMinW = lblColW + (int)Math.Ceiling(190 * s);
            int chkMinW = TextRenderer.MeasureText(_chkOnlyWithPoints.Text, _bodyFont).Width + (int)Math.Ceiling(48 * s);
            _filterPaneWidth = Math.Max(rowMinW, chkMinW);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding((int)(8 * s), (int)(4 * s), (int)(8 * s), (int)(6 * s)),
                Height = (int)(40 * s + 32 * s + 12 * s)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblColW));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32 * s));

            _txtSearch.Placeholder = "Tên, mã, SĐT, địa chỉ...";
            _txtSearch.Inner.Font = _bodyFont;
            _txtSearch.Height = (int)(34 * s);
            _txtSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _txtSearch.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            _txtSearch.Inner.KeyDown += OnKeyDown;
            _txtSearch.TextChanged += delegate { if (!_loading) ApplyLocalFilter(); };

            var lblSearch = new Label
            {
                Text = "Tìm kiếm",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont
            };
            panel.Controls.Add(lblSearch, 0, 0);
            panel.Controls.Add(_txtSearch, 1, 0);
            panel.Controls.Add(_chkOnlyWithPoints, 0, 1);
            panel.SetColumnSpan(_chkOnlyWithPoints, 2);

            _treeTitle.Text = "Nhóm khách hàng";
            _treeTitle.Font = _headingFont;
            _treeTitle.Height = (int)(30 * s);
            _treeTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            _tree.Font = _bodyFont;
            _tree.ItemHeight = (int)(28 * s);
            _tree.ShowNodeToolTips = true;
            _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _tree.DrawNode += DrawTreeNode;
            _tree.AfterSelect += delegate(object sender, TreeViewEventArgs e)
            {
                if (_loading || e.Node == null)
                    return;
                _selectedGroupTag = e.Node.Tag as string;
                ApplyLocalFilter();
            };

            _split.Panel1.Controls.Add(_tree);
            _split.Panel1.Controls.Add(_treeTitle);
            _split.Panel1.Controls.Add(panel);
            _split.Panel1.Controls.Add(title);
            _tree.BringToFront();
        }

        private void BuildBar(float s)
        {
            _bar.Height = (int)(56 * s);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding((int)(10 * s), (int)(10 * s), (int)(10 * s), (int)(10 * s))
            };

            foreach (FlatButton b in new[] { _btnFilter, _btnAddPoint, _btnExcel, _btnRefresh })
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = _bodyFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s);
                flow.Controls.Add(b);
            }

            _btnFilter.Click += delegate { ReloadCustomers(); };
            _btnAddPoint.Click += delegate { OpenAddVoucherDialog(); };
            _btnExcel.Click += delegate { ExportCustomersToExcel(); };
            _btnRefresh.Click += delegate { ReloadAll(); };

            _summary.Width = (int)(340 * s);
            _summary.Dock = DockStyle.Right;
            _summary.Padding = new Padding(0, 0, (int)(12 * s), 0);
            _summary.Font = _bodyFont;
            _bar.Controls.Add(flow);
            _bar.Controls.Add(_summary);
        }

        private void BuildMasterGrid(float s)
        {
            ConfigureReadOnlyGrid(_grid, s, true);
            DataGridViewColumn nameCol = AddGridColumn(_grid, "colName", "Tên khách", 180, "Nguyễn Văn Mẫu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            nameCol.MinimumWidth = nameCol.Width;
            AddGridColumn(_grid, "colGroup", "Nhóm khách", 130, "Khách VIP", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colAddress", "Địa chỉ", 170, "123 Nguyễn Văn Cừ", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colPhone", "Điện thoại", 115, "0988 123 456", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colEmail", "Email", 150, "khach@example.com", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colInitialPoints", "Điểm tích lũy ban đầu", 145, "999.999", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colRevenue", "Doanh số", 130, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colBillCount", "Số hóa đơn", 105, "99.999", typeof(int), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colPoints", "Điểm tích lũy", 125, "999.999", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            DataGridViewColumn noteCol = AddGridColumn(_grid, "colNote", "Ghi chú", 140, "Ghi chú khách hàng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            noteCol.MinimumWidth = noteCol.Width;

            _empty.Font = _bodyFont;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private void BuildDetailTabs(float s)
        {
            var billsPage = new TabPage("Hóa đơn") { Font = _bodyFont };
            ConfigureDetailBar(_billBar, s, new[] { _btnExportBills });
            _btnExportBills.Click += delegate { ExportBillsToExcel(); };

            ConfigureReadOnlyGrid(_billGrid, s, false);
            AddGridColumn(_billGrid, "b_points", "Điểm", 100, "999.999", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_billGrid, "b_num", "Số phiếu", 140, "HD26/00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_billGrid, "b_date", "Ngày", 110, "00/00/0000", typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddGridColumn(_billGrid, "b_total", "Tổng cộng", 140, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_billGrid, "b_used", "Điểm sử dụng", 130, "999.999", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            billsPage.Controls.Add(_billGrid);
            billsPage.Controls.Add(_billBar);
            _billGrid.BringToFront();
            _detailTabs.TabPages.Add(billsPage);

            var voucherPage = new TabPage("Tặng điểm - tặng quà") { Font = _bodyFont };
            ConfigureDetailBar(_voucherBar, s, new[] { _btnVoucherAdd, _btnVoucherEdit, _btnVoucherDelete, _btnExportVouchers });
            _btnVoucherAdd.Click += delegate { OpenAddVoucherDialog(); };
            _btnVoucherEdit.Click += delegate { EditSelectedVoucher(); };
            _btnVoucherDelete.Click += delegate { DeleteSelectedVoucher(); };
            _btnExportVouchers.Click += delegate { ExportVouchersToExcel(); };

            ConfigureReadOnlyGrid(_voucherGrid, s, false);
            AddGridColumn(_voucherGrid, "v_num", "Số phiếu", 130, "TD26/00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_voucherGrid, "v_date", "Ngày", 110, "00/00/0000", typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddGridColumn(_voucherGrid, "v_gift", "Điểm tặng", 115, "999.999", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_voucherGrid, "v_reduce", "Điểm giảm", 115, "999.999", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            DataGridViewColumn reasonCol = AddGridColumn(_voucherGrid, "v_reason", "Lý do", 200, "Tặng điểm sinh nhật", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            reasonCol.MinimumWidth = reasonCol.Width;
            reasonCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            AddGridColumn(_voucherGrid, "v_note", "Ghi chú", 170, "Ghi chú phiếu điểm", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            voucherPage.Controls.Add(_voucherGrid);
            voucherPage.Controls.Add(_voucherBar);
            _voucherGrid.BringToFront();
            _detailTabs.TabPages.Add(voucherPage);
        }

        private void ConfigureDetailBar(Panel bar, float s, FlatButton[] buttons)
        {
            bar.Height = (int)(44 * s);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding((int)(8 * s), (int)(5 * s), (int)(8 * s), (int)(5 * s))
            };
            foreach (FlatButton b in buttons)
            {
                b.Height = (int)(32 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = _bodyFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(22 * s);
                flow.Controls.Add(b);
            }
            bar.Controls.Add(flow);
        }

        private void ConfigureReadOnlyGrid(DataGridView g, float s, bool showRowHeaders)
        {
            g.Dock = DockStyle.Fill;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AutoGenerateColumns = false;
            g.MultiSelect = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowHeadersVisible = showRowHeaders;
            if (showRowHeaders)
                g.RowHeadersWidth = (int)(44 * s);
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = (int)(36 * s);
            g.RowTemplate.Height = (int)(32 * s);
            g.Font = _bodyFont;
            g.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(g, true, null);
        }

        private DataGridViewTextBoxColumn AddGridColumn(
            DataGridView g, string name, string header, int minWidth96,
            string sample, Type type, string format, DataGridViewContentAlignment align)
        {
            float s = DeviceDpi / 96f;
            int w = MeasureColumnWidth(g, header, sample, minWidth96, s);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = w,
                ValueType = type,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = align;
            col.DefaultCellStyle.NullValue = "";
            if (align == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (format != null)
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
            }
            g.Columns.Add(col);
            return col;
        }

        private static int MeasureColumnWidth(DataGridView g, string header, string sample, int minWidth96, float s)
        {
            int w = (int)Math.Ceiling(minWidth96 * s);
            Font hdrFont = g.ColumnHeadersDefaultCellStyle.Font ?? g.Font;
            int hdrW = TextRenderer.MeasureText(header ?? "", hdrFont).Width + (int)Math.Ceiling(28 * s);
            if (hdrW > w) w = hdrW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, g.Font).Width + (int)Math.Ceiling(24 * s);
                if (sampleW > w) w = sampleW;
            }
            return w;
        }

        private static int MeasureMaxWidth(string[] texts, Font font, int pad)
        {
            int max = 0;
            foreach (string t in texts)
            {
                int w = TextRenderer.MeasureText(t, font).Width + pad;
                if (w > max) max = w;
            }
            return max;
        }
    }
}
