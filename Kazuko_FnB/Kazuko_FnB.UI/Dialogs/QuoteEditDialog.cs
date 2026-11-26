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

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Dialog "BÁO GIÁ - THÊM MỚI / SỬA" (docs/specs/bao-gia-ban-hang.md §1.2):
    /// mở tối đa hóa, Enter không lưu, F3 nhảy ô Tìm, Enter ở ô Số lượng = Thêm.
    /// Phần lưới mặt hàng/chi tiết đặt ở QuoteEditDialog.Lines.cs; lưu/xóa/trạng thái ở QuoteEditDialog.Actions.cs.
    /// </summary>
    internal sealed partial class QuoteEditDialog : ThemedDialog, IScreenPreview
    {
        private const string ShortcutsHint = "(F3: Tìm mặt hàng, F8: Xóa dòng, F10/F11: Trước/Sau, Ctrl+L: Lưu, Ctrl+S: Lưu&Mới, Ctrl+N: Mới, ESC: Thoát)";

        private readonly UserSession _session;
        private readonly QuoteService _service;
        private readonly FormRepository _formRepo;
        private readonly List<string> _rowIds;
        private int _rowIndex;

        private QuoteRow _row;
        private bool _isNew;
        private bool _readOnly;
        private bool _loadFailed;
        private bool _loading;
        private string _original = "";

        private List<QuoteLine> _lines = new List<QuoteLine>();
        private List<QuoteSaleItem> _saleItems = new List<QuoteSaleItem>();
        private List<LookupItem> _units = new List<LookupItem>();

        private bool _discountByMoney;
        private bool _recalc;
        private bool _totalsDirty;
        private QuoteTotals _totals = new QuoteTotals();
        private bool _useTwoUnits;
        private bool _allowMultipleRowsPerItem;
        private bool _sortByCode;
        private decimal _roundStep = 1000m;

        private readonly FlatButton _btnShortcuts = new FlatButton { Text = "Phím tắt ▾" };
        private readonly FlatButton _btnPrev = new FlatButton { Text = "Trước (F10)" };
        private readonly FlatButton _btnNext = new FlatButton { Text = "Sau (F11)" };
        private readonly FlatButton _btnNew = new FlatButton { Text = "Tạo mới" };
        private readonly FlatButton _btnDelete = new FlatButton { Text = "Xóa", Tag = "danger" };
        private readonly ThemedContextMenu _shortcutsMenu = new ThemedContextMenu();
        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;

        private readonly DateBox _dtNgay = new DateBox();
        private readonly TextBox _txtNumber = new TextBox();
        private readonly LookupCombo _customer = new LookupCombo();
        private readonly TextBox _txtTenKhach = new TextBox();
        private readonly TextBox _txtDienThoai = new TextBox();
        private readonly TextBox _txtDiaChi = new TextBox();
        private readonly TextBox _txtEmail = new TextBox();
        private readonly TextBox _txtNote = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };

        private readonly NumberBox _numTienHang = new NumberBox();
        private readonly NumberBox _numTileGiamGia = new NumberBox();
        private readonly NumberBox _numTienGiamGia = new NumberBox();
        private readonly NumberBox _numTileThue = new NumberBox();
        private readonly NumberBox _numTienThue = new NumberBox();
        private readonly NumberBox _numPhiVanChuyen = new NumberBox();
        private readonly NumberBox _numTongCong = new NumberBox();

        private readonly TextBox _txtSearch = new TextBox();
        private readonly NumberBox _numQty = new NumberBox();
        private readonly FlatButton _btnAddProduct = new FlatButton { Text = "Thêm", Tag = "primary" };
        private readonly DataGridView _productGrid = new DataGridView();
        private readonly FlatButton _btnDeleteLine = new FlatButton { Text = "Xóa dòng (F8)", Tag = "danger" };
        private readonly FlatButton _btnImportExcel = new FlatButton { Text = "Nhập Excel" };
        private readonly FlatButton _btnExportExcel = new FlatButton { Text = "Xuất excel" };
        private readonly DataGridView _detailGrid = new DataGridView();

        public string SavedId { get; private set; }
        public bool Deleted { get; private set; }

        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal string LastInfoMessageForTest { get; private set; }
        internal Func<QuoteSaleItem, decimal, QuoteQuantityPromptResult> QuantityPromptOverrideForTest { get; set; }

        internal TextBox NumberBoxForTest { get { return _txtNumber; } }
        internal DateBox DateBoxForTest { get { return _dtNgay; } }
        internal LookupCombo CustomerComboForTest { get { return _customer; } }
        internal TextBox TenKhachBoxForTest { get { return _txtTenKhach; } }
        internal TextBox DiaChiBoxForTest { get { return _txtDiaChi; } }
        internal TextBox DienThoaiBoxForTest { get { return _txtDienThoai; } }
        internal TextBox EmailBoxForTest { get { return _txtEmail; } }
        internal TextBox NoteBoxForTest { get { return _txtNote; } }
        internal NumericUpDown TienHangBoxForTest { get { return _numTienHang; } }
        internal NumericUpDown TileGiamGiaBoxForTest { get { return _numTileGiamGia; } }
        internal NumericUpDown TienGiamGiaBoxForTest { get { return _numTienGiamGia; } }
        internal NumericUpDown TileThueBoxForTest { get { return _numTileThue; } }
        internal NumericUpDown TienThueBoxForTest { get { return _numTienThue; } }
        internal NumericUpDown PhiVanChuyenBoxForTest { get { return _numPhiVanChuyen; } }
        internal NumericUpDown TongCongBoxForTest { get { return _numTongCong; } }
        internal bool DiscountByMoneyForTest { get { return _discountByMoney; } }
        internal QuoteTotals TotalsForTest { get { return _totals; } }
        internal bool DirtyForTest { get { return Dirty; } }
        internal TextBox SearchBoxForTest { get { return _txtSearch; } }
        internal NumericUpDown QtyBoxForTest { get { return _numQty; } }
        internal FlatButton AddProductButtonForTest { get { return _btnAddProduct; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal DataGridView ProductGridForTest { get { return _productGrid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal List<QuoteLine> LinesForTest { get { return _lines; } }

        public QuoteEditDialog(UserSession session, string quoteId, List<string> rowIds)
            : this(session, quoteId, rowIds, false)
        {
        }

        private QuoteEditDialog(UserSession session, string quoteId, List<string> rowIds, bool previewMode)
            : base("BÁO GIÁ - THÊM MỚI", ShortcutsHint, 1180, 680)
        {
            _session = session ?? AppSession.Current;
            _service = new QuoteService(_session);
            _formRepo = new FormRepository(_session.DatabasePath);
            _rowIds = rowIds;
            _rowIndex = rowIds == null ? -1 : rowIds.IndexOf(quoteId ?? "");
            _isNew = string.IsNullOrEmpty(quoteId);
            _readOnly = _isNew ? !_service.CanAdd : !_service.CanEdit;

            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            if (!previewMode)
                WindowState = FormWindowState.Maximized;
            AcceptButton = null;

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = exit;

            BuildBottom();
            BuildTopPanel();
            BuildToolbar();
            WireDirty();

            LoadLookups();
            if (_isNew)
                NewRecord();
            else
                LoadRecord(quoteId);

            _original = Snapshot();
            ApplyConfigAndPermissions();
            UpdateSaveButtons();
            UpdateNavButtons();

            KeyDown += OnKeyDown;
            Shown += delegate
            {
                if (_loadFailed)
                {
                    ShowWarning(QuoteText.NotFound);
                    Close();
                    return;
                }
                if (_txtNumber.Enabled && !_txtNumber.ReadOnly)
                {
                    _txtNumber.Focus();
                    _txtNumber.SelectAll();
                }
            };
        }

        private void WireDirty()
        {
            _txtNumber.TextChanged += delegate { OnDirtyChanged(); };
            _dtNgay.ValueChanged += delegate { OnDirtyChanged(); };
            _txtTenKhach.TextChanged += delegate { OnDirtyChanged(); };
            _txtDienThoai.TextChanged += delegate { OnDirtyChanged(); };
            _txtDiaChi.TextChanged += delegate { OnDirtyChanged(); };
            _txtEmail.TextChanged += delegate { OnDirtyChanged(); };
            _txtNote.TextChanged += delegate { OnDirtyChanged(); };
        }

        private void BuildToolbar()
        {
            float s = Dpi;
            Font btnFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            var bar = new Panel { Dock = DockStyle.Top, Height = (int)(40 * s) };
            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            foreach (FlatButton b in new[] { _btnShortcuts, _btnPrev, _btnNext, _btnNew })
            {
                b.Font = btnFont;
                b.Height = (int)(34 * s);
                b.Width = TextRenderer.MeasureText(b.Text, btnFont).Width + (int)(22 * s);
                b.Margin = new Padding(0, (int)(2 * s), (int)(6 * s), 0);
                left.Controls.Add(b);
            }
            _btnShortcuts.Click += delegate { _shortcutsMenu.Show(_btnShortcuts, new Point(0, _btnShortcuts.Height)); };
            _btnPrev.Click += delegate { Navigate(-1); };
            _btnNext.Click += delegate { Navigate(1); };
            _btnNew.Click += delegate { NewBlank(); };

            _btnDelete.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _btnDelete.Height = (int)(34 * s);
            _btnDelete.Width = (int)(84 * s);
            _btnDelete.Dock = DockStyle.Right;
            _btnDelete.Click += delegate { DeleteCurrent(); };

            bar.Controls.Add(left);
            bar.Controls.Add(_btnDelete);
            Body.Controls.Add(bar);

            _shortcutsMenu.AddItem("Lưu (Ctrl+L)", delegate { SaveStay(); });
            _shortcutsMenu.AddItem("Lưu & Mới (Ctrl+S)", delegate { SaveAndNew(); });
            _shortcutsMenu.AddItem("Tạo mới (Ctrl+N)", delegate { NewBlank(); });
            _shortcutsMenu.AddSeparator();
            _shortcutsMenu.AddItem("Thoát (Esc)", delegate { Close(); });
        }

        private void BuildTopPanel()
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            var top = new Panel { Dock = DockStyle.Top, Height = (int)(226 * s), Padding = new Padding(0, (int)(4 * s), 0, (int)(6 * s)) };
            Body.Controls.Add(top);

            int col0W = MeasureMaxLabelWidth(new[] { "Ngày", "Khách hàng", "Tên khách", "Địa chỉ", "Ghi chú" }, bodyFont, s);
            int col2W = MeasureMaxLabelWidth(new[] { "Số phiếu", "Điện thoại", "Email" }, bodyFont, s);

            var leftGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 5,
                Padding = new Padding(0, 0, (int)(12 * s), 0)
            };
            leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col0W));
            leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col2W));
            leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < 4; i++)
                leftGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _dtNgay.Inner.Font = bodyFont;
            _dtNgay.Height = (int)(30 * s);
            _dtNgay.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            foreach (Control c in new Control[] { _txtNumber, _customer, _txtTenKhach, _txtDienThoai, _txtDiaChi, _txtEmail })
            {
                c.Font = bodyFont;
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }
            _customer.ValueChanged += delegate { OnCustomerChanged(); };
            _txtNote.Font = bodyFont;
            _txtNote.Dock = DockStyle.Fill;

            leftGrid.Controls.Add(FieldLabel("Ngày", bodyFont), 0, 0);
            leftGrid.Controls.Add(_dtNgay, 1, 0);
            leftGrid.Controls.Add(FieldLabel("Số phiếu", bodyFont), 2, 0);
            leftGrid.Controls.Add(_txtNumber, 3, 0);

            leftGrid.Controls.Add(FieldLabel("Khách hàng", bodyFont), 0, 1);
            leftGrid.Controls.Add(_customer, 1, 1);
            leftGrid.SetColumnSpan(_customer, 3);

            leftGrid.Controls.Add(FieldLabel("Tên khách", bodyFont), 0, 2);
            leftGrid.Controls.Add(_txtTenKhach, 1, 2);
            leftGrid.Controls.Add(FieldLabel("Điện thoại", bodyFont), 2, 2);
            leftGrid.Controls.Add(_txtDienThoai, 3, 2);

            leftGrid.Controls.Add(FieldLabel("Địa chỉ", bodyFont), 0, 3);
            leftGrid.Controls.Add(_txtDiaChi, 1, 3);
            leftGrid.Controls.Add(FieldLabel("Email", bodyFont), 2, 3);
            leftGrid.Controls.Add(_txtEmail, 3, 3);

            leftGrid.Controls.Add(FieldLabel("Ghi chú", bodyFont), 0, 4);
            leftGrid.Controls.Add(_txtNote, 1, 4);
            leftGrid.SetColumnSpan(_txtNote, 3);

            top.Controls.Add(leftGrid);
            BuildMoneyPanel(top, s, bodyFont);
        }

        private void BuildMoneyPanel(Panel top, float s, Font bodyFont)
        {
            Font headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            int lblW = Math.Max(
                MeasureMaxLabelWidth(new[] { "Tiền hàng", "Giảm giá", "Thuế", "Phí vận chuyển" }, bodyFont, s),
                MeasureMaxLabelWidth(new[] { "Tổng cộng" }, headingFont, s));
            int spinW = SystemInformation.VerticalScrollBarWidth;
            int pctBoxW = TextRenderer.MeasureText("-999.99", bodyFont).Width + spinW + (int)Math.Ceiling(22 * s);
            int pctSignW = TextRenderer.MeasureText("%", bodyFont).Width + (int)Math.Ceiling(10 * s);
            int valBoxW = TextRenderer.MeasureText("-999,999,999", headingFont).Width + spinW + (int)Math.Ceiling(22 * s);
            int boxW = lblW + pctBoxW + pctSignW + valBoxW + (int)Math.Ceiling(28 * s);

            var box = new GroupBox
            {
                Text = "Tổng tiền",
                Dock = DockStyle.Right,
                Width = boxW,
                Font = headingFont,
                Padding = new Padding((int)(8 * s), (int)(4 * s), (int)(8 * s), (int)(4 * s))
            };
            top.Controls.Add(box);

            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblW));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, pctBoxW));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, pctSignW));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 5; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));

            SetupMoneyBox(_numTienHang, true, true, bodyFont);
            SetupPercentBox(_numTileGiamGia, bodyFont);
            SetupMoneyBox(_numTienGiamGia, false, false, bodyFont);
            SetupPercentBox(_numTileThue, bodyFont);
            SetupMoneyBox(_numTienThue, true, true, bodyFont);
            SetupMoneyBox(_numPhiVanChuyen, false, false, bodyFont);
            SetupMoneyBox(_numTongCong, true, true, headingFont);

            grid.Controls.Add(FieldLabel("Tiền hàng", bodyFont), 0, 0);
            grid.Controls.Add(_numTienHang, 1, 0);
            grid.SetColumnSpan(_numTienHang, 3);

            grid.Controls.Add(FieldLabel("Giảm giá", bodyFont), 0, 1);
            grid.Controls.Add(_numTileGiamGia, 1, 1);
            grid.Controls.Add(new Label { Text = "%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = bodyFont }, 2, 1);
            grid.Controls.Add(_numTienGiamGia, 3, 1);

            grid.Controls.Add(FieldLabel("Thuế", bodyFont), 0, 2);
            grid.Controls.Add(_numTileThue, 1, 2);
            grid.Controls.Add(new Label { Text = "%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = bodyFont }, 2, 2);
            grid.Controls.Add(_numTienThue, 3, 2);

            grid.Controls.Add(FieldLabel("Phí vận chuyển", bodyFont), 0, 3);
            grid.Controls.Add(_numPhiVanChuyen, 1, 3);
            grid.SetColumnSpan(_numPhiVanChuyen, 3);

            grid.Controls.Add(FieldLabel("Tổng cộng", headingFont), 0, 4);
            grid.Controls.Add(_numTongCong, 1, 4);
            grid.SetColumnSpan(_numTongCong, 3);

            box.Controls.Add(grid);

            _numTileGiamGia.ValueChanged += delegate
            {
                if (!_loading && !_recalc)
                {
                    _discountByMoney = false;
                    decimal pct = _numTileGiamGia.Value;
                    if (pct < 0m) pct = 0m;
                    if (pct > 100m) pct = 100m;
                    _recalc = true;
                    try
                    {
                        ConfigureDiscountPercentBounds(false);
                        _numTileGiamGia.Value = pct;
                    }
                    finally { _recalc = false; }
                    RecalcTotals();
                    OnDirtyChanged();
                }
            };
            _numTienGiamGia.ValueChanged += delegate
            {
                if (!_loading && !_recalc)
                {
                    _discountByMoney = true;
                    RecalcTotals();
                    OnDirtyChanged();
                }
            };
            _numTileThue.ValueChanged += delegate { if (!_loading && !_recalc) { RecalcTotals(); OnDirtyChanged(); } };
            _numPhiVanChuyen.ValueChanged += delegate { if (!_loading && !_recalc) { RecalcTotals(); OnDirtyChanged(); } };
        }

        private static Label FieldLabel(string text, Font font)
        {
            return new Label { Text = text, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = font };
        }

        private static void SetupMoneyBox(NumericUpDown num, bool readOnly, bool allowNegative, Font font)
        {
            num.DecimalPlaces = 2;
            num.ThousandsSeparator = true;
            num.Minimum = allowNegative ? -999999999999m : 0m;
            num.Maximum = 999999999999m;
            num.TextAlign = HorizontalAlignment.Right;
            num.Font = font;
            num.ReadOnly = readOnly;
            num.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }

        private static void SetupPercentBox(NumericUpDown num, Font font)
        {
            num.DecimalPlaces = 2;
            num.Minimum = 0m;
            num.Maximum = 100m;
            num.TextAlign = HorizontalAlignment.Right;
            num.Font = font;
            num.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }

        private static void SetupGrid(DataGridView grid, bool readOnly, float s)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = readOnly;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.AutoGenerateColumns = false;
            grid.MultiSelect = false;
            grid.ScrollBars = ScrollBars.Both;
            grid.SelectionMode = readOnly ? DataGridViewSelectionMode.FullRowSelect : DataGridViewSelectionMode.CellSelect;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(36 * s);
            grid.RowTemplate.Height = (int)(30 * s);
            grid.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        }

        private static void AddTextColumn(DataGridView grid, string name, string header, int minWidth96,
            string sample, bool readOnly, bool fill, float s)
        {
            int w = MeasureGridColumnWidth(grid, header, sample, minWidth96, s);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = fill ? w : Math.Min(w, (int)Math.Ceiling(56 * s)),
                ReadOnly = readOnly,
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
            };
            grid.Columns.Add(col);
        }

        private static void AddMoneyColumn(DataGridView grid, string name, string header, int minWidth96,
            string sample, string format, float s)
        {
            int w = MeasureGridColumnWidth(grid, header, sample, minWidth96, s);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = Math.Min(w, (int)Math.Ceiling(56 * s)),
                ValueType = typeof(decimal),
                DefaultCellStyle =
                {
                    Format = format,
                    FormatProvider = NumberInput.ViFormat,
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            };
            grid.Columns.Add(col);
        }
    }

    internal sealed class QuoteQuantityPromptResult
    {
        public bool Confirmed { get; set; }
        public decimal OddQuantity { get; set; }
        public decimal EvenQuantity { get; set; }
    }
}
