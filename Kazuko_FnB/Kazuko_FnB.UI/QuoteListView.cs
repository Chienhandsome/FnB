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
    /// Màn danh sách "Báo giá bán hàng" (docs/specs/bao-gia-ban-hang.md §1.1):
    /// panel lọc trái (Ngày mặc định 30 ngày gần nhất, Số phiếu, Khách hàng);
    /// thanh nút trên (Lọc F3, Thêm mới, Chỉnh sửa, Xóa, Xuất excel, In, Làm mới);
    /// lưới chứng từ phải + pane dưới gồm tab Thông tin và tab Báo giá chi tiết.
    /// Phần thao tác, tải dữ liệu và giao diện đặt ở QuoteListView.Actions.cs.
    /// </summary>
    internal sealed partial class QuoteListView : Panel
    {
        private readonly UserSession _session;
        private readonly QuoteService _service;
        private readonly FormRepository _formRepo;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly List<FieldFrame> _frames = new List<FieldFrame>();
        private readonly DateBox _dtFrom = new DateBox();
        private readonly DateBox _dtTo = new DateBox();
        private readonly InputBox _txtNumber = new InputBox();
        private readonly LookupCombo _customer = new LookupCombo();

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnFilter = new FlatButton { Text = "Lọc (F3)" };
        private readonly FlatButton _btnAdd = new FlatButton { Text = "Thêm mới (Insert)", Tag = "primary" };
        private readonly FlatButton _btnEdit = new FlatButton { Text = "Chỉnh sửa (F4)" };
        private readonly FlatButton _btnRemove = new FlatButton { Text = "Xóa (Del)", Tag = "danger" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In" };
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Làm mới" };
        private readonly Label _count = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

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
        private List<QuoteListRow> _rows = new List<QuoteListRow>();
        private bool _loading;
        private bool _splitPlaced;
        private bool _rightSplitPlaced;

        internal bool SuppressPopup { get; set; }
        internal string LastInfoMessageForTest { get; private set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal DateBox FromDateBoxForTest { get { return _dtFrom; } }
        internal DateBox ToDateBoxForTest { get { return _dtTo; } }
        internal InputBox NumberBoxForTest { get { return _txtNumber; } }
        internal LookupCombo CustomerComboForTest { get { return _customer; } }
        internal FlatButton FilterButtonForTest { get { return _btnFilter; } }
        internal FlatButton AddButtonForTest { get { return _btnAdd; } }
        internal FlatButton EditButtonForTest { get { return _btnEdit; } }
        internal FlatButton RemoveButtonForTest { get { return _btnRemove; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }

        public QuoteListView(UserSession session)
        {
            _session = session ?? AppSession.Current;
            _service = new QuoteService(_session);
            _formRepo = new FormRepository(_session.DatabasePath);
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
                if (e.RowIndex >= 0)
                    EditQuote();
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

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            Reload();
        }

        private int _filterPaneWidth;

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int want = Math.Max((int)(310 * s), _filterPaneWidth);
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
            int bottomH = (int)(195 * DeviceDpi / 96f);
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

            string[] filterCaptions = { "Từ ngày", "Đến ngày", "Số phiếu", "Khách hàng" };
            int lblColW = MeasureMaxWidth(filterCaptions, bodyFont, (int)Math.Ceiling(16 * s));
            _filterPaneWidth = lblColW + (int)Math.Ceiling(190 * s);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding((int)(8 * s), (int)(4 * s), (int)(8 * s), (int)(8 * s)),
                Height = (int)(4 * 40 * s) + (int)(12 * s)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblColW));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 4; i++)
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));

            _dtFrom.Value = DateTime.Today.AddDays(-30);
            _dtTo.Value = DateTime.Today;
            _dtFrom.Inner.KeyDown += OnKeyDown;
            _dtTo.Inner.KeyDown += OnKeyDown;
            _txtNumber.Inner.KeyDown += OnKeyDown;
            _customer.KeyDown += OnKeyDown;

            AddFilterRow(panel, 0, "Từ ngày", _dtFrom, bodyFont, s);
            AddFilterRow(panel, 1, "Đến ngày", _dtTo, bodyFont, s);
            AddFilterRow(panel, 2, "Số phiếu", _txtNumber, bodyFont, s);
            AddFilterRow(panel, 3, "Khách hàng", _customer, bodyFont, s);

            _split.Panel1.Controls.Add(panel);
            _split.Panel1.Controls.Add(title);
            LoadCustomerLookup();
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

        private void LoadCustomerLookup()
        {
            var items = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            try
            {
                items.AddRange(_formRepo.LoadAll("DKHACHHANG"));
            }
            catch (DataAccessException)
            {
                // Ô lọc khách hàng để trống, lưới vẫn hoạt động bình thường.
            }
            _customer.SetItems(items);
            if (_customer.Items.Count > 0)
                _customer.SelectedIndex = 0;
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
            foreach (FlatButton b in new[] { _btnFilter, _btnAdd, _btnEdit, _btnRemove, _btnExcel, _btnPrint, _btnRefresh })
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = btnFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s);
                flow.Controls.Add(b);
            }

            _btnFilter.Click += delegate { Reload(); };
            _btnAdd.Click += delegate { AddQuote(); };
            _btnEdit.Click += delegate { EditQuote(); };
            _btnRemove.Click += delegate { DeleteQuote(); };
            _btnExcel.Click += delegate { ShowInfo(QuoteText.Unsupported); };
            _btnPrint.Click += delegate { PrintSelectedQuote(); };
            _btnRefresh.Click += delegate { Reload(); };

            _count.Width = (int)(110 * s);
            _count.Dock = DockStyle.Right;
            _count.Padding = new Padding(0, 0, (int)(12 * s), 0);
            _count.Font = btnFont;
            _bar.Controls.Add(flow);
            _bar.Controls.Add(_count);
        }

        private void BuildGrid(float s)
        {
            ConfigureReadOnlyGrid(_grid, s, true);
            // Thứ tự cột mặc định: Ngày, Số phiếu, Tên khách, Địa chỉ, Điện thoại, Tiền hàng, Tiền giảm giá, Tổng cộng, Email, Thuế, Phí vận chuyển, Ghi chú.
            AddGridColumn(_grid, "colDate", "Ngày", 95, "00/00/0000", typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddGridColumn(_grid, "colNumber", "Số phiếu", 120, "BG26/00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colCustomer", "Tên khách", 170, "Công ty TNHH mẫu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colAddress", "Địa chỉ", 180, "123 Nguyễn Thị Minh", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colPhone", "Điện thoại", 115, "0909 123 456", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colGoods", "Tiền hàng", 115, "-999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colDiscount", "Tiền giảm giá", 125, "-999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colTotal", "Tổng cộng", 120, "-999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colEmail", "Email", 160, "ketoan@domain.vn", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_grid, "colTax", "Thuế", 105, "-999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_grid, "colShipping", "Phí vận chuyển", 130, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            DataGridViewColumn noteCol = AddGridColumn(_grid, "colNote", "Ghi chú", 180, "Ghi chú báo giá", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            noteCol.MinimumWidth = noteCol.Width;
            noteCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

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

            var detailPage = new TabPage("Báo giá chi tiết") { Font = bodyFont };
            ConfigureReadOnlyGrid(_detailGrid, s, false);
            AddGridColumn(_detailGrid, "d_code", "Mã hàng", 90, "MH0001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_detailGrid, "d_name", "Mặt hàng", 220, "Mặt hàng mẫu dài", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_detailGrid, "d_unit", "ĐVT", 70, "Thùng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(_detailGrid, "d_qty", "Số lượng", 90, "999,99", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_detailGrid, "d_price", "Đơn giá", 110, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_detailGrid, "d_disc", "Giảm giá %", 95, "100,00", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(_detailGrid, "d_amount", "Thành tiền", 120, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);
            DataGridViewColumn dNote = AddGridColumn(_detailGrid, "d_note", "Ghi chú", 160, "Ghi chú chi tiết", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
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
                ReadOnly = true
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
            int hdrW = TextRenderer.MeasureText(header ?? "", hdrFont).Width + (int)Math.Ceiling(26 * s);
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
