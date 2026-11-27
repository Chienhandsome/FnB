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
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn Quản lý nhập kho (TDONHANG LOAI = 1, docs/specs/phieu-nhap-kho.md §5):
    /// lọc trái (Từ ngày, Đến ngày, Kho nhập theo cửa hàng user, Nhà cung cấp, Tìm kiếm),
    /// thanh nút trên, lưới 12 cột kèm hàng tổng cộng cột tiền, pane dưới Thông tin &amp; Chi tiết phiếu nhập.
    /// Phần thao tác dữ liệu, xuất Excel, menu ngữ cảnh, phím tắt và theme đặt ở <c>PurchaseListView.Actions.cs</c>.
    /// </summary>
    internal sealed partial class PurchaseListView : Panel
    {
        private static readonly object SummaryRowTag = new object();

        private static readonly HashSet<string> MoneyColumnNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "colTongCong",
            "colPhiVanChuyen",
            "colTienGiamGia",
            "colTienThue",
            "colTienHang"
        };

        private static readonly HashSet<string> DetailMoneyColumnNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "d_price",
            "d_disc",
            "d_amount"
        };

        private readonly UserSession _session;
        private readonly PurchaseService _service;
        private readonly FormRepository _formRepo;
        private readonly bool _canViewBuyPrice;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly List<FieldFrame> _frames = new List<FieldFrame>();
        private readonly DateBox _dtFrom = new DateBox();
        private readonly DateBox _dtTo = new DateBox();
        private readonly ComboBox _warehouse = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly LookupCombo _supplier = new LookupCombo();
        private readonly InputBox _txtSearch = new InputBox();

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnFilter = new FlatButton { Text = "Lọc (F3)" };
        private readonly FlatButton _btnAdd = new FlatButton { Text = "Thêm mới (Insert)", Tag = "primary" };
        private readonly FlatButton _btnEdit = new FlatButton { Text = "Chỉnh sửa (F4)" };
        private readonly FlatButton _btnRemove = new FlatButton { Text = "Xóa (Del)", Tag = "danger" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Làm mới" };
        private readonly Label _summary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

        private readonly SplitContainer _rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        private readonly TabControl _infoTabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private readonly Label _lblCreated = new Label { AutoSize = true };
        private readonly Label _lblCreatedBy = new Label { AutoSize = true };
        private readonly Label _lblModified = new Label { AutoSize = true };
        private readonly Label _lblModifiedBy = new Label { AutoSize = true };
        private readonly DataGridView _detailGrid = new DataGridView();

        private ThemedContextMenu _gridMenu;
        private List<PurchaseRow> _rows = new List<PurchaseRow>();
        private List<WarehouseRow> _allowedWarehouses = new List<WarehouseRow>();
        private bool _loading;
        private bool _splitPlaced;
        private bool _rightSplitPlaced;
        private int _filterPaneWidth;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal string LastExportMessageForTest { get; private set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal Func<PurchaseEditDialog, DialogResult> DialogOpenerForTest { get; set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal DateBox FromDateBoxForTest { get { return _dtFrom; } }
        internal DateBox ToDateBoxForTest { get { return _dtTo; } }
        internal ComboBox WarehouseComboForTest { get { return _warehouse; } }
        internal LookupCombo SupplierComboForTest { get { return _supplier; } }
        internal InputBox SearchBoxForTest { get { return _txtSearch; } }
        internal FlatButton FilterButtonForTest { get { return _btnFilter; } }
        internal FlatButton AddButtonForTest { get { return _btnAdd; } }
        internal FlatButton EditButtonForTest { get { return _btnEdit; } }
        internal FlatButton RemoveButtonForTest { get { return _btnRemove; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal bool CanViewBuyPriceForTest { get { return _canViewBuyPrice; } }

        public PurchaseListView(UserSession session)
        {
            _session = session ?? AppSession.Current;
            _service = new PurchaseService(_session);
            _formRepo = new FormRepository(_session.DatabasePath);
            _canViewBuyPrice = _service.CanViewBuyPrice;
            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _loading = true;

            BuildFilterPanel(s);
            BuildBar(s);
            BuildGrid(s);
            BuildBottomTabs(s);

            _rightSplit.Panel1.Controls.Add(_grid);
            _rightSplit.Panel2.Controls.Add(_infoTabs);
            _split.Panel2.Controls.Add(_rightSplit);
            _split.Panel2.Controls.Add(_bar);
            _rightSplit.BringToFront();

            _split.FixedPanel = FixedPanel.Panel1;
            _split.SizeChanged += delegate { PlaceSplit(); };
            _rightSplit.SizeChanged += delegate { PlaceRightSplit(); };
            Controls.Add(_split);
            _loading = false;

            _grid.RowPostPaint += DrawRowNumber;
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && CurrentPurchaseRow() != null)
                    OpenCurrentPurchase();
            };
            _grid.SelectionChanged += delegate
            {
                if (!_loading)
                {
                    UpdateButtons();
                    UpdateInfo();
                }
            };

            BuildGridMenu();
            _grid.MouseDown += GridMouseDown;
            _grid.KeyDown += OnKeyDown;
            KeyDown += OnKeyDown;

            ApplyBuyPriceVisibility();
            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            Reload();
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int want = Math.Max((int)(300 * s), _filterPaneWidth);
            if (_split.Width > want + (int)(240 * s))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        private void PlaceRightSplit()
        {
            if (_rightSplitPlaced)
                return;
            int bottomH = (int)(185 * DeviceDpi / 96f);
            if (_rightSplit.Height > bottomH + (int)(180 * DeviceDpi / 96f))
            {
                _rightSplit.SplitterDistance = _rightSplit.Height - bottomH;
                _rightSplitPlaced = true;
            }
        }

        private void BuildFilterPanel(float s)
        {
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            Font headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);

            var title = new Label
            {
                Text = "Điều kiện lọc",
                Dock = DockStyle.Top,
                Height = (int)(32 * s),
                Padding = new Padding((int)(10 * s), 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = headingFont
            };

            string[] captions = { "Từ ngày", "Đến ngày", "Kho nhập", "Nhà cung cấp", "Tìm kiếm" };
            int lblColW = MeasureMaxWidth(captions, bodyFont, (int)Math.Ceiling(16 * s));
            _filterPaneWidth = lblColW + (int)Math.Ceiling(190 * s);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding((int)(8 * s), (int)(4 * s), (int)(8 * s), (int)(8 * s)),
                Height = (int)(5 * 40 * s) + (int)(12 * s)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblColW));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 5; i++)
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));

            _dtFrom.Value = DateTime.Today.AddDays(-30);
            _dtTo.Value = DateTime.Today;
            _txtSearch.Placeholder = "Số phiếu, kho, NCC, ghi chú...";
            _dtFrom.Inner.KeyDown += OnKeyDown;
            _dtTo.Inner.KeyDown += OnKeyDown;
            _warehouse.KeyDown += OnKeyDown;
            _supplier.KeyDown += OnKeyDown;
            _txtSearch.Inner.KeyDown += OnKeyDown;
            _warehouse.SelectedIndexChanged += delegate { if (!_loading) Reload(); };
            _supplier.ValueChanged += delegate { if (!_loading) Reload(); };
            _txtSearch.TextChanged += delegate { if (!_loading) Reload(); };

            AddFilterRow(panel, 0, "Từ ngày", _dtFrom, bodyFont, s);
            AddFilterRow(panel, 1, "Đến ngày", _dtTo, bodyFont, s);
            AddFilterRow(panel, 2, "Kho nhập", _warehouse, bodyFont, s);
            AddFilterRow(panel, 3, "Nhà cung cấp", _supplier, bodyFont, s);
            AddFilterRow(panel, 4, "Tìm kiếm", _txtSearch, bodyFont, s);

            _split.Panel1.Controls.Add(panel);
            _split.Panel1.Controls.Add(title);
            LoadFilterLookups();
        }

        private void AddFilterRow(TableLayoutPanel panel, int row, string caption, Control field, Font font, float s)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = font
            };
            var input = field as InputBox;
            var date = field as DateBox;
            if (input != null)
                input.Inner.Font = font;
            else if (date != null)
                date.Inner.Font = font;
            else
                field.Font = font;

            Control host = field;
            if (date != null)
            {
                _frames.Add(date);
            }
            else if (field is ComboBox)
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

        private void LoadFilterLookups()
        {
            try
            {
                _allowedWarehouses = _service.LoadAllowedWarehouses();
            }
            catch (DataAccessException)
            {
                _allowedWarehouses = new List<WarehouseRow>();
            }

            _warehouse.Items.Clear();
            _warehouse.Items.Add(new LookupItem { Id = "", Name = "Tất cả kho" });
            foreach (WarehouseRow w in _allowedWarehouses)
                _warehouse.Items.Add(new LookupItem { Id = w.Id, Name = w.Name });
            _warehouse.DisplayMember = "Name";
            if (_warehouse.Items.Count > 0)
                _warehouse.SelectedIndex = 0;

            var suppliers = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            try
            {
                suppliers.AddRange(_formRepo.LoadAll("DNHACUNGCAP"));
            }
            catch (DataAccessException)
            {
                // Ô lọc NCC để trống nếu lỗi đọc.
            }
            _supplier.SetItems(suppliers);
            if (_supplier.Items.Count > 0)
                _supplier.SelectedIndex = 0;
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
            Font btnFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            foreach (FlatButton b in new[] { _btnFilter, _btnAdd, _btnEdit, _btnRemove, _btnExcel, _btnRefresh })
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = btnFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s);
                flow.Controls.Add(b);
            }

            _btnFilter.Click += delegate { Reload(); };
            _btnAdd.Click += delegate { AddPurchase(); };
            _btnEdit.Click += delegate { EditPurchase(); };
            _btnRemove.Click += delegate { DeletePurchase(); };
            _btnExcel.Click += delegate { ExportGridToExcel(); };
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd.Visible = _service.CanAdd;
            _btnEdit.Visible = _service.CanEdit;
            _btnRemove.Visible = _service.CanRemove;

            _summary.Width = (int)(260 * s);
            _summary.Dock = DockStyle.Right;
            _summary.Padding = new Padding(0, 0, (int)(12 * s), 0);
            _summary.Font = btnFont;
            _bar.Controls.Add(flow);
            _bar.Controls.Add(_summary);
        }

        private void BuildGrid(float s)
        {
            ConfigureReadOnlyGrid(_grid, s, true);
            // Đúng 12 cột theo thứ tự spec §5:
            // Ghi chú, Số phiếu, Ngày, Tổng cộng, Phí vận chuyển, Tiền giảm giá, Tiền thuế, Tiền hàng,
            // Nhà cung cấp, Kho nhập, Nhân viên nhập, Diễn giải.
            AddGridColumn(_grid, "colNote", "Ghi chú", 140, "Ghi chú phiếu nhập", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colNumber", "Số phiếu", 118, "PN26/00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colDate", "Ngày", 96, "00/00/0000", typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddGridColumn(_grid, "colTongCong", "Tổng cộng", 118, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colPhiVanChuyen", "Phí vận chuyển", 130, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colTienGiamGia", "Tiền giảm giá", 122, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colTienThue", "Tiền thuế", 108, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colTienHang", "Tiền hàng", 118, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colSupplier", "Nhà cung cấp", 160, "Công ty TNHH Mẫu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colWarehouse", "Kho nhập", 130, "Kho nguyên liệu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colEmployee", "Nhân viên nhập", 140, "Nguyễn Văn Mẫu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            DataGridViewColumn descCol = AddGridColumn(_grid, "colDescription", "Diễn giải", 160, "Nhập mua hàng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            descCol.MinimumWidth = descCol.Width;
            descCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _empty.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private void BuildBottomTabs(float s)
        {
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            var infoPage = new TabPage("Thông tin") { Font = bodyFont };
            AddInfoLabel(infoPage, "Khởi tạo:", _lblCreated, 12, 14, 145, s, bodyFont);
            AddInfoLabel(infoPage, "Khởi tạo bởi:", _lblCreatedBy, 340, 14, 120, s, bodyFont);
            AddInfoLabel(infoPage, "Sửa đổi gần nhất:", _lblModified, 12, 46, 145, s, bodyFont);
            AddInfoLabel(infoPage, "Sửa đổi bởi:", _lblModifiedBy, 340, 46, 120, s, bodyFont);
            _infoTabs.TabPages.Add(infoPage);

            var detailPage = new TabPage("Chi tiết phiếu nhập") { Font = bodyFont };
            ConfigureReadOnlyGrid(_detailGrid, s, false);
            AddGridColumn(_detailGrid, "d_code", "Mã hàng", 90, "MH0001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_detailGrid, "d_name", "Mặt hàng", 210, "Nguyên liệu trà sữa", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_detailGrid, "d_unit", "ĐVT", 74, "Thùng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_detailGrid, "d_qty", "Số lượng", 90, "999,99", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_detailGrid, "d_price", "Đơn giá", 110, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_detailGrid, "d_disc", "% giảm", 86, "100,00", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_detailGrid, "d_amount", "Thành tiền", 120, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            DataGridViewColumn dNote = AddGridColumn(_detailGrid, "d_note", "Ghi chú", 160, "Ghi chú dòng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            dNote.MinimumWidth = dNote.Width;
            dNote.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            detailPage.Controls.Add(_detailGrid);
            _infoTabs.TabPages.Add(detailPage);
        }

        private static void ConfigureReadOnlyGrid(DataGridView g, float s, bool showRowHeaders)
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
            g.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            g.ColumnHeadersDefaultCellStyle.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        }

        private DataGridViewTextBoxColumn AddGridColumn(DataGridView g, string name, string header, int minWidth96,
            string sample, Type type, string format, DataGridViewContentAlignment align)
        {
            float s = DeviceDpi / 96f;
            int w = MeasureColumnWidth(g, header, sample, minWidth96, s);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = Math.Min(w, (int)Math.Ceiling(60 * s)),
                ValueType = type,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = align;
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

        private static void AddInfoLabel(TabPage page, string caption, Label value, int x, int y, int capW, float s, Font font)
        {
            var cap = new Label { Text = caption, AutoSize = true, Font = font };
            cap.Location = new Point((int)(x * s), (int)(y * s));
            value.Font = font;
            value.Location = new Point((int)((x + capW) * s), (int)(y * s));
            page.Controls.Add(cap);
            page.Controls.Add(value);
        }
    }
}
