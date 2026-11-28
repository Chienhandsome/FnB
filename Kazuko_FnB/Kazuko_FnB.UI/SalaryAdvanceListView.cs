using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
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
    /// Màn NHÂN SỰ > Tạm ứng lương (TTHUCHI LOAI = 1, LATAMUNG = 30, TBANGLUONGID IS NULL;
    /// spec docs/specs/nhan-su-thuong-phat-tam-ung.md §3).
    /// Bố cục theo khung lưới Danh mục phiếu chi: bộ lọc trái (Từ ngày, Đến ngày mặc định tháng hiện tại, Nhân viên, Tìm kiếm),
    /// thanh nút trên, lưới 8 cột (cột Cửa hàng chỉ hiện khi có nhiều cửa hàng), hàng tổng cộng Số tiền và tab Thông tin.
    /// Phần thao tác CRUD, xuất Excel, menu ngữ cảnh, phím tắt và theme đặt ở <c>SalaryAdvanceListView.Actions.cs</c>.
    /// </summary>
    internal sealed partial class SalaryAdvanceListView : Panel
    {
        private readonly UserSession _session;
        private readonly SalaryAdvanceService _service;
        private readonly FormRepository _formRepo;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly List<FieldFrame> _frames = new List<FieldFrame>();
        private readonly CheckBox _chkFrom = new CheckBox { AutoSize = false, CheckAlign = ContentAlignment.MiddleLeft, Checked = false };
        private readonly CheckBox _chkTo = new CheckBox { AutoSize = false, CheckAlign = ContentAlignment.MiddleLeft, Checked = false };
        private readonly DateBox _dtFrom = new DateBox(true);
        private readonly DateBox _dtTo = new DateBox(true);
        private DateTime _lastFromDate = DateTime.Today;
        private DateTime _lastToDate = DateTime.Today;
        private readonly ComboBox _employee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
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
        private readonly Panel _totalBar = new Panel { Dock = DockStyle.Bottom };
        private readonly Label _lblTotalCaption = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly Label _lblTotalAmount = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false };
        private Font _totalFont;

        private readonly TabControl _infoTabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private readonly Label _lblCreated = new Label { AutoSize = true };
        private readonly Label _lblCreatedBy = new Label { AutoSize = true };
        private readonly Label _lblModified = new Label { AutoSize = true };
        private readonly Label _lblModifiedBy = new Label { AutoSize = true };

        private ThemedContextMenu _gridMenu;
        private List<VoucherRow> _rows = new List<VoucherRow>();
        private List<string> _allowedStoreIds = new List<string>();
        private bool _multiStore;
        private decimal _totalAmount;
        private bool _loading;
        private bool _splitPlaced;
        private bool _rightSplitPlaced;
        private int _filterPaneWidth;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal string LastExportMessageForTest { get; private set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal Func<PhieuThuDialog, DialogResult> DialogOpenerForTest { get; set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal CheckBox FromDateCheckForTest { get { return _chkFrom; } }
        internal CheckBox ToDateCheckForTest { get { return _chkTo; } }
        internal DateBox FromDateBoxForTest { get { return _dtFrom; } }
        internal DateBox ToDateBoxForTest { get { return _dtTo; } }
        internal ComboBox EmployeeComboForTest { get { return _employee; } }
        internal InputBox SearchBoxForTest { get { return _txtSearch; } }
        internal FlatButton FilterButtonForTest { get { return _btnFilter; } }
        internal FlatButton AddButtonForTest { get { return _btnAdd; } }
        internal FlatButton EditButtonForTest { get { return _btnEdit; } }
        internal FlatButton RemoveButtonForTest { get { return _btnRemove; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal decimal TotalAmountForTest { get { return _totalAmount; } }
        internal Label TotalAmountLabelForTest { get { return _lblTotalAmount; } }

        public SalaryAdvanceListView(UserSession session)
        {
            _session = session ?? AppSession.Current;
            _service = new SalaryAdvanceService(_session);
            _formRepo = new FormRepository(_session.DatabasePath);
            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _loading = true;

            BuildFilterPanel(s);
            BuildBar(s);
            BuildGrid(s);
            BuildTotalBar(s);
            BuildBottomTabs(s);

            _rightSplit.Panel1.Controls.Add(_grid);
            _rightSplit.Panel1.Controls.Add(_totalBar);
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
            _grid.CellDoubleClick += delegate (object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    OpenCurrentAdvance();
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
            int bottomH = (int)(120 * DeviceDpi / 96f);
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

            string[] captions = { "Từ ngày", "Đến ngày", "Nhân viên", "Tìm kiếm" };
            int lblColW = MeasureMaxWidth(captions, bodyFont, (int)Math.Ceiling(16 * s));
            _filterPaneWidth = lblColW + (int)Math.Ceiling(218 * s);

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

            _chkFrom.Checked = false;
            _chkTo.Checked = false;
            _dtFrom.NullableValue = null;
            _dtTo.NullableValue = null;
            _txtSearch.Placeholder = "Số phiếu, nhân viên, lý do...";
            _dtFrom.Inner.KeyDown += OnKeyDown;
            _dtTo.Inner.KeyDown += OnKeyDown;
            _employee.KeyDown += OnKeyDown;
            _txtSearch.Inner.KeyDown += OnKeyDown;
            _chkFrom.CheckedChanged += delegate { OnDateCheckChanged(_chkFrom, _dtFrom, ref _lastFromDate); };
            _chkTo.CheckedChanged += delegate { OnDateCheckChanged(_chkTo, _dtTo, ref _lastToDate); };
            _dtFrom.ValueChanged += delegate { OnDateValueChanged(_chkFrom, _dtFrom, ref _lastFromDate); };
            _dtTo.ValueChanged += delegate { OnDateValueChanged(_chkTo, _dtTo, ref _lastToDate); };
            _employee.SelectedIndexChanged += delegate { if (!_loading) Reload(); };
            _txtSearch.TextChanged += delegate { if (!_loading) Reload(); };

            AddDateFilterRow(panel, 0, "Từ ngày", _chkFrom, _dtFrom, bodyFont, s);
            AddDateFilterRow(panel, 1, "Đến ngày", _chkTo, _dtTo, bodyFont, s);
            AddFilterRow(panel, 2, "Nhân viên", _employee, bodyFont, s);
            AddFilterRow(panel, 3, "Tìm kiếm", _txtSearch, bodyFont, s);

            _split.Panel1.Controls.Add(panel);
            _split.Panel1.Controls.Add(title);
            LoadFilterLookups();
        }

        private void AddDateFilterRow(TableLayoutPanel panel, int row, string caption, CheckBox check, DateBox date, Font font, float s)
        {
            var lbl = new Label { Text = caption, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = font };
            date.Inner.Font = font;
            date.Dock = DockStyle.Fill;
            _frames.Add(date);
            check.Dock = DockStyle.Left;
            check.Width = (int)(22 * s);
            check.Margin = Padding.Empty;
            var wrap = new Panel { Height = (int)(34 * s), Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s)) };
            wrap.Controls.Add(date);
            wrap.Controls.Add(check);
            date.BringToFront();
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(wrap, 1, row);
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
            List<LookupItem> allStores;
            try { allStores = _formRepo.LoadAll("DCUAHANG"); }
            catch (DataAccessException) { allStores = new List<LookupItem>(); }

            _multiStore = allStores.Count > 1;
            List<string> userIds = _session.Stores == null
                ? new List<string>()
                : _session.Stores.Select(x => x.Id).ToList();
            if (!_session.IsAdmin && userIds.Count == 0 && !string.IsNullOrEmpty(_session.UserId))
            {
                try { userIds = _formRepo.LoadStoresOfUser(_session.UserId).Select(x => x.Id).ToList(); }
                catch (DataAccessException) { userIds = new List<string>(); }
            }
            _allowedStoreIds = FundLedgerRules.AllowedStores(
                _session.IsAdmin,
                allStores.Select(x => x.Id).ToList(),
                userIds);

            List<LookupItem> emps;
            try { emps = _service.LoadActiveEmployees(); }
            catch (DataAccessException) { emps = new List<LookupItem>(); }

            _employee.Items.Clear();
            _employee.Items.Add(new LookupItem { Id = "", Name = "Tất cả nhân viên" });
            foreach (LookupItem e in emps)
                _employee.Items.Add(e);
            _employee.DisplayMember = "Name";
            if (_employee.Items.Count > 0)
                _employee.SelectedIndex = 0;
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
            _btnAdd.Click += delegate { AddAdvance(); };
            _btnEdit.Click += delegate { EditAdvance(); };
            _btnRemove.Click += delegate { DeleteAdvance(); };
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
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(44 * s);
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(36 * s);
            _grid.RowTemplate.Height = (int)(32 * s);
            _grid.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            AddGridColumn("colNumber", "Số phiếu", 115, "PC26/00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn("colDate", "Ngày", 95, "00/00/0000", typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddGridColumn("colEmployee", "Nhân viên", 170, "Nguyễn Văn Mẫu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn("colAmount", "Số tiền", 120, "999.999.999", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn("colReason", "Lý do", 150, "Tạm ứng lương", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn("colDesc", "Diễn giải", 180, "Tạm ứng lương tháng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            DataGridViewColumn storeCol = AddGridColumn("colStore", "Cửa hàng", 140, "Cửa hàng chính", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            storeCol.Visible = _multiStore;
            DataGridViewColumn noteCol = AddGridColumn("colNote", "Ghi chú", 160, "Ghi chú phiếu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            noteCol.MinimumWidth = noteCol.Width;
            noteCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _empty.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private void BuildTotalBar(float s)
        {
            _totalFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _totalBar.Height = (int)(32 * s);
            _lblTotalCaption.Font = _totalFont;
            _lblTotalAmount.Font = _totalFont;
            _totalBar.Controls.Add(_lblTotalCaption);
            _totalBar.Controls.Add(_lblTotalAmount);
            _totalBar.Resize += delegate { LayoutTotalBar(); };
        }

        private void BuildBottomTabs(float s)
        {
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            var infoPage = new TabPage("Thông tin") { Font = bodyFont };
            AddInfoLabel(infoPage, "Khởi tạo:", _lblCreated, 12, 12, 145, s, bodyFont);
            AddInfoLabel(infoPage, "Khởi tạo bởi:", _lblCreatedBy, 340, 12, 120, s, bodyFont);
            AddInfoLabel(infoPage, "Sửa đổi gần nhất:", _lblModified, 12, 42, 145, s, bodyFont);
            AddInfoLabel(infoPage, "Sửa đổi bởi:", _lblModifiedBy, 340, 42, 120, s, bodyFont);
            _infoTabs.TabPages.Add(infoPage);
        }

        private DataGridViewTextBoxColumn AddGridColumn(string name, string header, int minWidth96,
            string sample, Type type, string format, DataGridViewContentAlignment align)
        {
            float s = DeviceDpi / 96f;
            int w = MeasureColumnWidth(header, sample, minWidth96, s);
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
            _grid.Columns.Add(col);
            return col;
        }

        private int MeasureColumnWidth(string header, string sample, int minWidth96, float s)
        {
            int w = (int)Math.Ceiling(minWidth96 * s);
            Font hdrFont = _grid.ColumnHeadersDefaultCellStyle.Font ?? _grid.Font;
            int hdrW = TextRenderer.MeasureText(header ?? "", hdrFont).Width + (int)Math.Ceiling(26 * s);
            if (hdrW > w) w = hdrW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, _grid.Font).Width + (int)Math.Ceiling(24 * s);
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
