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
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn chỉ đọc "Tồn kho" (KHO HÀNG &gt; Tồn kho, docs/specs/ton-kho.md):
    /// khung trái bộ lọc (Kho theo cửa hàng người dùng, Loại hàng, Tìm không dấu, tích dưới tồn tối thiểu,
    /// tích ẩn hàng tồn 0) + cây Nhóm mặt hàng; khung phải thanh nút + lưới 10 cột tồn kho (2 ĐVT, tô màu dòng,
    /// hàng tổng cộng giá trị, ẩn 4 cột giá &amp; tắt xuất khi không có quyền Xem giá nhập).
    /// Phần nạp dữ liệu, lọc, tô màu, xuất Excel, menu ngữ cảnh, phím tắt và theme đặt ở <c>InventoryView.Actions.cs</c>.
    /// </summary>
    internal sealed partial class InventoryView : Panel
    {
        private static readonly object SummaryRowTag = new object();

        private static readonly HashSet<string> PriceProtectedColumns = new HashSet<string>(StringComparer.Ordinal)
        {
            "colBuyPrice",
            "colCostPrice",
            "colCostValue",
            "colSellValue"
        };

        private readonly UserSession _session;
        private readonly InventoryService _service;
        private readonly bool _canViewBuyPrice;
        private readonly bool _allWarehousesMode;

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _summaryFont;
        private readonly Font _smallFont;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly List<FieldFrame> _frames = new List<FieldFrame>();
        private readonly ComboBox _warehouse = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly ComboBox _productType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly InputBox _txtSearch = new InputBox();
        private readonly CheckBox _chkBelowMin = new CheckBox { Text = "Chỉ hiện hàng dưới mức tồn tối thiểu", AutoSize = true };
        private readonly CheckBox _chkHideZero = new CheckBox { Text = "Ẩn hàng tồn 0", AutoSize = true };
        private readonly Label _lblCutoff = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
        private readonly Label _treeTitle = new Label { Text = "Nhóm mặt hàng", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowLines = true, ShowRootLines = true };

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnFilter = new FlatButton { Text = "Lọc (F3)" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Làm mới (F5)", Tag = "primary" };
        private readonly Label _summary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        private ThemedContextMenu _gridMenu;
        private ThemeTokens _theme = ThemeManager.Current;
        private List<WarehouseRow> _allowedWarehouses = new List<WarehouseRow>();
        private List<ProductGroupRow> _allGroups = new List<ProductGroupRow>();
        private List<InventoryItemRow> _warehouseItems = new List<InventoryItemRow>();
        private List<InventoryItemRow> _visibleItems = new List<InventoryItemRow>();
        private string _selectedGroupId;
        private bool _loading;
        private bool _splitPlaced;
        private int _filterPaneWidth;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastExportMessageForTest { get; private set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal ComboBox WarehouseComboForTest { get { return _warehouse; } }
        internal ComboBox ProductTypeComboForTest { get { return _productType; } }
        internal InputBox SearchBoxForTest { get { return _txtSearch; } }
        internal CheckBox BelowMinCheckForTest { get { return _chkBelowMin; } }
        internal CheckBox HideZeroCheckForTest { get { return _chkHideZero; } }
        internal TreeView GroupTreeForTest { get { return _tree; } }
        internal FlatButton FilterButtonForTest { get { return _btnFilter; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal ThemedContextMenu GridMenuForTest { get { return _gridMenu; } }
        internal bool CanViewBuyPriceForTest { get { return _canViewBuyPrice; } }
        internal bool IsAllWarehousesModeForTest { get { return _allWarehousesMode; } }

        public InventoryView(UserSession session) : this(session, false)
        {
        }

        public InventoryView(UserSession session, bool allWarehousesMode)
        {
            _session = session ?? AppSession.Current;
            _service = new InventoryService(_session);
            _canViewBuyPrice = _service.CanViewBuyPrice;
            _allWarehousesMode = allWarehousesMode;

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _summaryFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);
            _smallFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);

            _loading = true;
            BuildFilterPanel(s);
            BuildBar(s);
            BuildGrid(s);

            if (_allWarehousesMode)
            {
                BuildAllWarehousesDetailPanel(s);
                _split.Panel2.Controls.Add(_rightSplit);
                _split.Panel2.Controls.Add(_bar);
                _rightSplit.BringToFront();
            }
            else
            {
                _split.Panel2.Controls.Add(_grid);
                _split.Panel2.Controls.Add(_bar);
                _grid.BringToFront();
            }

            _split.FixedPanel = FixedPanel.Panel1;
            _split.SizeChanged += delegate { PlaceSplit(); };
            Controls.Add(_split);
            _loading = false;

            _grid.RowPostPaint += DrawRowNumber;
            _grid.SelectionChanged += OnMainGridSelectionChanged;
            _grid.CurrentCellChanged += OnMainGridSelectionChanged;
            BuildGridMenu();
            _grid.MouseDown += GridMouseDown;
            _grid.KeyDown += OnKeyDown;
            _tree.KeyDown += OnKeyDown;
            KeyDown += OnKeyDown;

            ApplyPricePermissionVisibility();
            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            ReloadAll();
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int want = Math.Max((int)(300 * s), _filterPaneWidth);
            if (_split.Width > want + (int)(320 * s))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
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

            string[] captions = { "Kho", "Loại hàng", "Tìm kiếm" };
            int lblColW = MeasureMaxWidth(captions, _bodyFont, (int)Math.Ceiling(16 * s));
            int rowMinW = lblColW + (int)Math.Ceiling(190 * s);
            int chkMinW = Math.Max(
                TextRenderer.MeasureText(_chkBelowMin.Text, _bodyFont).Width,
                TextRenderer.MeasureText(_chkHideZero.Text, _bodyFont).Width) + (int)Math.Ceiling(48 * s);
            _filterPaneWidth = Math.Max(rowMinW, chkMinW);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding((int)(8 * s), (int)(4 * s), (int)(8 * s), (int)(6 * s)),
                Height = (int)(3 * 40 * s + 2 * 30 * s + 26 * s + 12 * s)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblColW));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30 * s));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30 * s));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26 * s));

            _txtSearch.Placeholder = "Mã hàng, tên mặt hàng...";
            _warehouse.KeyDown += OnKeyDown;
            _productType.KeyDown += OnKeyDown;
            _txtSearch.Inner.KeyDown += OnKeyDown;
            _warehouse.SelectedIndexChanged += delegate { if (!_loading) ReloadStock(); };
            _productType.SelectedIndexChanged += delegate { if (!_loading) ApplyLocalFilter(); };
            _txtSearch.TextChanged += delegate { if (!_loading) ApplyLocalFilter(); };

            _chkBelowMin.Font = _bodyFont;
            _chkBelowMin.Margin = new Padding((int)(2 * s), (int)(4 * s), 0, (int)(2 * s));
            _chkBelowMin.CheckedChanged += delegate { if (!_loading) ApplyLocalFilter(); };

            _chkHideZero.Font = _bodyFont;
            _chkHideZero.Margin = new Padding((int)(2 * s), (int)(4 * s), 0, (int)(2 * s));
            _chkHideZero.CheckedChanged += delegate { if (!_loading) ApplyLocalFilter(); };

            _lblCutoff.Font = _smallFont;
            _lblCutoff.Text = "Ngày chốt: hiện tại";
            _lblCutoff.Dock = DockStyle.Fill;
            _lblCutoff.Margin = new Padding((int)(2 * s), (int)(2 * s), 0, 0);

            AddFilterRow(panel, 0, "Kho", _warehouse, s);
            AddFilterRow(panel, 1, "Loại hàng", _productType, s);
            AddFilterRow(panel, 2, "Tìm kiếm", _txtSearch, s);

            panel.Controls.Add(_chkBelowMin, 0, 3);
            panel.SetColumnSpan(_chkBelowMin, 2);
            panel.Controls.Add(_chkHideZero, 0, 4);
            panel.SetColumnSpan(_chkHideZero, 2);
            panel.Controls.Add(_lblCutoff, 0, 5);
            panel.SetColumnSpan(_lblCutoff, 2);

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
                _selectedGroupId = e.Node.Tag as string;
                ApplyLocalFilter();
            };

            _split.Panel1.Controls.Add(_tree);
            _split.Panel1.Controls.Add(_treeTitle);
            _split.Panel1.Controls.Add(panel);
            _split.Panel1.Controls.Add(title);
            _tree.BringToFront();
        }

        private void AddFilterRow(TableLayoutPanel panel, int row, string caption, Control field, float s)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont
            };
            var input = field as InputBox;
            if (input != null)
                input.Inner.Font = _bodyFont;
            else
                field.Font = _bodyFont;

            Control host = field;
            if (field is ComboBox)
            {
                ((ComboBox)field).FlatStyle = FlatStyle.Flat;
                var frame = new FieldFrame(field, 0);
                _frames.Add(frame);
                host = frame;
            }
            host.Height = (int)(34 * s);
            host.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            host.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(host, 1, row);
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
            foreach (FlatButton b in new[] { _btnFilter, _btnExcel, _btnRefresh })
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = _bodyFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s);
                flow.Controls.Add(b);
            }

            _btnFilter.Click += delegate { ReloadStock(); };
            _btnExcel.Click += delegate { ExportGridToExcel(); };
            _btnRefresh.Click += delegate { ReloadAll(); };

            _summary.Width = (int)(320 * s);
            _summary.Dock = DockStyle.Right;
            _summary.Padding = new Padding(0, 0, (int)(12 * s), 0);
            _summary.Font = _bodyFont;
            _bar.Controls.Add(flow);
            _bar.Controls.Add(_summary);
        }

        private void BuildGrid(float s)
        {
            ConfigureReadOnlyGrid(_grid, s);
            // Đúng 10 cột theo thứ tự spec §1:
            // Mã, Tên, ĐVT, Tồn kho, Tồn 2 ĐVT, Tồn tối thiểu, Giá nhập, Giá vốn, Giá trị vốn, Giá trị bán.
            AddGridColumn(_grid, "colCode", "Mã hàng", 96, "MH00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            DataGridViewColumn nameCol = AddGridColumn(_grid, "colName", "Tên mặt hàng", 200, "Trà sữa trân châu đường đen", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            nameCol.MinimumWidth = nameCol.Width;
            nameCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            AddGridColumn(_grid, "colUnit", "ĐVT", 74, "Thùng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colStock", "Tồn kho", 96, "-99.999,5", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colTwoUnit", "Tồn 2 ĐVT", 148, "99 thùng 23 chai", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colMinStock", "Tồn tối thiểu", 118, "9.999,5", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colBuyPrice", "Giá nhập", 110, "99.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colCostPrice", "Giá vốn", 110, "99.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colCostValue", "Giá trị vốn", 126, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colSellValue", "Giá trị bán", 126, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);

            _empty.Font = _bodyFont;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private void ConfigureReadOnlyGrid(DataGridView g, float s)
        {
            g.Dock = DockStyle.Fill;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AutoGenerateColumns = false;
            g.MultiSelect = false;
            g.ScrollBars = ScrollBars.Both;
            g.RowHeadersVisible = true;
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
