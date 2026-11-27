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
    /// Màn hình Công nợ khách hàng &amp; Công nợ nhà cung cấp (docs/specs/cong-no.md §3–§5):
    /// - Trái: điều kiện lọc (Tìm không dấu, tích "Chỉ hiện khách/NCC còn nợ" mặc định bật) + cây nhóm đối tượng.
    /// - Phải trên: thanh nút (Lọc F3, Thu tiền nợ / Chi trả nợ, Xuất excel, Làm mới F5, tóm tắt) + lưới đối tượng kèm hàng tổng cộng.
    /// - Phải dưới: chi tiết hóa đơn/phiếu nhập-xuất trả và phiếu thu/chi theo ngày của đối tượng đang chọn.
    /// Phần nạp dữ liệu, lọc, thao tác thu/chi, xuất Excel, menu ngữ cảnh, phím tắt và theme đặt ở <c>DebtListView.Actions.cs</c>.
    /// </summary>
    internal sealed partial class DebtListView : Panel
    {
        private static readonly object SummaryRowTag = new object();

        private readonly UserSession _session;
        private readonly DebtPartyKind _kind;
        private readonly DebtService _service;

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _summaryFont;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly InputBox _txtSearch = new InputBox();
        private readonly CheckBox _chkOnlyDebt = new CheckBox { AutoSize = true, Checked = true };
        private readonly Label _treeTitle = new Label { Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowLines = true, ShowRootLines = true };

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnFilter = new FlatButton { Text = "Lọc (F3)" };
        private readonly FlatButton _btnAction = new FlatButton { Tag = "primary" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Làm mới (F5)" };
        private readonly Label _summary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

        private readonly SplitContainer _rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        private readonly TabControl _detailTabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private readonly DataGridView _billGrid = new DataGridView();
        private readonly DataGridView _voucherGrid = new DataGridView();

        private ThemedContextMenu _gridMenu;
        private ThemeTokens _theme = ThemeManager.Current;
        private List<DebtGroupNode> _allGroups = new List<DebtGroupNode>();
        private List<DebtPartnerRow> _allPartners = new List<DebtPartnerRow>();
        private List<DebtPartnerRow> _visiblePartners = new List<DebtPartnerRow>();
        private string _selectedGroupTag;
        private bool _loading;
        private bool _splitPlaced;
        private bool _rightSplitPlaced;
        private int _filterPaneWidth;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal string LastExportMessageForTest { get; private set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal Func<CustomerDebtCollectDialog, DialogResult> CollectDialogOpenerForTest { get; set; }
        internal Func<PhieuThuDialog, DialogResult> SupplierPayDialogOpenerForTest { get; set; }
        internal DebtPartyKind PartyKindForTest { get { return _kind; } }
        internal DataGridView GridForTest { get { return _grid; } }
        internal DataGridView BillDetailGridForTest { get { return _billGrid; } }
        internal DataGridView VoucherDetailGridForTest { get { return _voucherGrid; } }
        internal TabControl DetailTabsForTest { get { return _detailTabs; } }
        internal InputBox SearchBoxForTest { get { return _txtSearch; } }
        internal CheckBox OnlyDebtCheckForTest { get { return _chkOnlyDebt; } }
        internal TreeView GroupTreeForTest { get { return _tree; } }
        internal FlatButton FilterButtonForTest { get { return _btnFilter; } }
        internal FlatButton ActionButtonForTest { get { return _btnAction; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal Label SummaryLabelForTest { get { return _summary; } }

        public DebtListView(UserSession session, DebtPartyKind kind)
        {
            _session = session ?? AppSession.Current;
            _kind = kind;
            _service = new DebtService(_session);

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
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && CurrentPartner() != null && CanPerformDebtAction())
                    ExecuteDebtAction();
            };

            BuildGridMenu();
            _grid.MouseDown += GridMouseDown;
            _grid.KeyDown += OnKeyDown;
            _tree.KeyDown += OnKeyDown;
            KeyDown += OnKeyDown;

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            ReloadAll();
        }

        private bool IsCustomer { get { return _kind == DebtPartyKind.Customer; } }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int want = Math.Max((int)(290 * s), _filterPaneWidth);
            if (_split.Width > want + (int)(320 * s))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        private void PlaceRightSplit()
        {
            if (_rightSplitPlaced)
                return;
            int bottomH = (int)(220 * DeviceDpi / 96f);
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

            _chkOnlyDebt.Text = IsCustomer ? "Chỉ hiện khách còn nợ" : "Chỉ hiện NCC còn nợ";
            _chkOnlyDebt.Font = _bodyFont;
            _chkOnlyDebt.Checked = true;
            _chkOnlyDebt.Margin = new Padding((int)(2 * s), (int)(4 * s), 0, (int)(2 * s));
            _chkOnlyDebt.CheckedChanged += delegate { if (!_loading) ApplyLocalFilter(); };

            int lblColW = MeasureMaxWidth(new[] { "Tìm kiếm" }, _bodyFont, (int)Math.Ceiling(16 * s));
            int rowMinW = lblColW + (int)Math.Ceiling(190 * s);
            int chkMinW = TextRenderer.MeasureText(_chkOnlyDebt.Text, _bodyFont).Width + (int)Math.Ceiling(48 * s);
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

            _txtSearch.Placeholder = IsCustomer ? "Mã, tên, SĐT, địa chỉ khách..." : "Mã, tên, SĐT, địa chỉ NCC...";
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
            panel.Controls.Add(_chkOnlyDebt, 0, 1);
            panel.SetColumnSpan(_chkOnlyDebt, 2);

            _treeTitle.Text = IsCustomer ? "Nhóm khách hàng" : "Nhóm nhà cung cấp";
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

            _btnAction.Text = IsCustomer ? "Thu tiền nợ" : "Chi trả nợ";
            foreach (FlatButton b in new[] { _btnFilter, _btnAction, _btnExcel, _btnRefresh })
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = _bodyFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s);
                flow.Controls.Add(b);
            }

            _btnFilter.Click += delegate { ReloadDebts(); };
            _btnAction.Click += delegate { ExecuteDebtAction(); };
            _btnExcel.Click += delegate { ExportGridToExcel(); };
            _btnRefresh.Click += delegate { ReloadAll(); };

            _summary.Width = (int)(320 * s);
            _summary.Dock = DockStyle.Right;
            _summary.Padding = new Padding(0, 0, (int)(12 * s), 0);
            _summary.Font = _bodyFont;
            _bar.Controls.Add(flow);
            _bar.Controls.Add(_summary);
        }

        private void BuildMasterGrid(float s)
        {
            ConfigureReadOnlyGrid(_grid, s, true);
            AddGridColumn(_grid, "colCode", IsCustomer ? "Mã khách" : "Mã NCC", 110, "KH00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            DataGridViewColumn nameCol = AddGridColumn(_grid, "colName", IsCustomer ? "Tên khách hàng" : "Tên nhà cung cấp", 210, "Công ty TNHH Mẫu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            nameCol.MinimumWidth = nameCol.Width;
            nameCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            AddGridColumn(_grid, "colAddress", "Địa chỉ", 200, "123 Nguyễn Văn Cừ, Hà Nội", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colPhone", "Điện thoại", 120, "0988 123 456", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colDebt", "Còn nợ", 135, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);

            _empty.Font = _bodyFont;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private void BuildDetailTabs(float s)
        {
            var billsPage = new TabPage(IsCustomer ? "Hóa đơn" : "Phiếu nhập / xuất trả") { Font = _bodyFont };
            ConfigureReadOnlyGrid(_billGrid, s, false);
            AddGridColumn(_billGrid, "b_num", "Số phiếu", 125, "HD26/00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_billGrid, "b_date", "Ngày", 104, "00/00/0000", typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddGridColumn(_billGrid, "b_type", "Loại", 115, "Hóa đơn bán", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_billGrid, "b_total", "Tổng cộng", 130, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_billGrid, "b_paid", "Đã thanh toán", 135, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_billGrid, "b_rem", "Còn nợ", 130, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            DataGridViewColumn billNote = AddGridColumn(_billGrid, "b_desc", "Diễn giải", 180, "Ghi chú hóa đơn", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            billNote.MinimumWidth = billNote.Width;
            billNote.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            billsPage.Controls.Add(_billGrid);
            _detailTabs.TabPages.Add(billsPage);

            var voucherPage = new TabPage("Phiếu thu / chi") { Font = _bodyFont };
            ConfigureReadOnlyGrid(_voucherGrid, s, false);
            AddGridColumn(_voucherGrid, "v_num", "Số phiếu", 125, "PT26/00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_voucherGrid, "v_date", "Ngày", 104, "00/00/0000", typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddGridColumn(_voucherGrid, "v_thu", "Thu", 130, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_voucherGrid, "v_chi", "Chi", 130, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            DataGridViewColumn vDesc = AddGridColumn(_voucherGrid, "v_desc", "Diễn giải", 220, "Thu công nợ khách hàng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            vDesc.MinimumWidth = vDesc.Width;
            vDesc.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            voucherPage.Controls.Add(_voucherGrid);
            _detailTabs.TabPages.Add(voucherPage);
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
