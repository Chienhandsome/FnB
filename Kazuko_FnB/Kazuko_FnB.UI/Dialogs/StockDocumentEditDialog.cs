using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem chi tiết Phiếu xuất khác (LOAI = 2) và Phiếu chuyển kho (LOAI = 3)
    /// (docs/specs/phieu-xuat-chuyen-kho.md §2, §3, §5).
    /// Phần lưới mặt hàng &amp; chi tiết đặt ở <c>StockDocumentEditDialog.Lines.cs</c>;
    /// phần nạp/lưu/xóa/tính tổng/phân quyền đặt ở <c>StockDocumentEditDialog.Actions.cs</c>;
    /// phần phím tắt/theme/preview đặt ở <c>StockDocumentEditDialog.Helpers.cs</c>.
    /// </summary>
    internal sealed partial class StockDocumentEditDialog : ThemedDialog, IScreenPreview
    {
        private const string ShortcutsHint = "(F3: Tìm mặt hàng, F8: Xóa dòng, Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";

        private readonly UserSession _session;
        private readonly StockDocumentKind _kind;
        private readonly StockDocumentService _service;
        private readonly FormRepository _formRepo;

        private StockDocumentRow _row;
        private bool _isNew;
        private bool _readOnly;
        private bool _loadFailed;
        private bool _loading;
        private string _original = "";

        private List<StockDocumentLine> _lines = new List<StockDocumentLine>();
        private List<StockProductItem> _products = new List<StockProductItem>();
        private List<WarehouseRow> _warehouses = new List<WarehouseRow>();

        private bool _discountByMoney;
        private bool _recalc;
        private PurchaseTotals _totals = new PurchaseTotals();
        private bool _allowMultipleRowsPerItem;

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnDelete;
        private readonly FlatButton _btnClose;

        private readonly DateBox _dtNgay = new DateBox();
        private readonly TextBox _txtNumber = new TextBox { MaxLength = StockDocumentConstants.MaxTextLength };
        private readonly LookupCombo _sourceWarehouse = new LookupCombo();
        private readonly LookupCombo _destWarehouse = new LookupCombo();
        private readonly LookupCombo _supplier = new LookupCombo();
        private readonly LookupCombo _exportEmployee = new LookupCombo();
        private readonly LookupCombo _importEmployee = new LookupCombo();
        private readonly TextBox _txtDesc = new TextBox { MaxLength = StockDocumentConstants.MaxTextLength };
        private readonly TextBox _txtNote = new TextBox { MaxLength = StockDocumentConstants.MaxTextLength };

        private GroupBox _moneyBox;
        private readonly NumberBox _numTienHang = new NumberBox();
        private readonly NumberBox _numTileGiamGia = new NumberBox();
        private readonly NumberBox _numTienGiamGia = new NumberBox();
        private readonly NumberBox _numTileThue = new NumberBox();
        private readonly NumberBox _numTienThue = new NumberBox();
        private readonly NumberBox _numPhiVanChuyen = new NumberBox();
        private readonly NumberBox _numTongCong = new NumberBox();

        private readonly TextBox _txtSearch = new TextBox();
        private readonly NumberBox _numQty = new NumberBox();
        private readonly ComboBox _cboAddUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly FlatButton _btnAddProduct = new FlatButton { Text = "Thêm", Tag = "primary" };
        private readonly DataGridView _productGrid = new DataGridView();
        private readonly FlatButton _btnDeleteLine = new FlatButton { Text = "Xóa dòng (F8)", Tag = "danger" };
        private readonly FlatButton _btnExportExcel = new FlatButton { Text = "Xuất excel" };
        private readonly DataGridView _detailGrid = new DataGridView();

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }
        public bool Deleted { get; private set; }
        public StockDocumentKind Kind { get { return _kind; } }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal DateBox DateBoxForTest { get { return _dtNgay; } }
        internal TextBox NumberBoxForTest { get { return _txtNumber; } }
        internal LookupCombo SourceWarehouseComboForTest { get { return _sourceWarehouse; } }
        internal LookupCombo DestWarehouseComboForTest { get { return _destWarehouse; } }
        internal LookupCombo SupplierComboForTest { get { return _supplier; } }
        internal LookupCombo ExportEmployeeComboForTest { get { return _exportEmployee; } }
        internal LookupCombo ImportEmployeeComboForTest { get { return _importEmployee; } }
        internal TextBox DescriptionBoxForTest { get { return _txtDesc; } }
        internal TextBox NoteBoxForTest { get { return _txtNote; } }
        internal GroupBox MoneyBoxForTest { get { return _moneyBox; } }
        internal NumericUpDown TienHangBoxForTest { get { return _numTienHang; } }
        internal NumericUpDown TileGiamGiaBoxForTest { get { return _numTileGiamGia; } }
        internal NumericUpDown TienGiamGiaBoxForTest { get { return _numTienGiamGia; } }
        internal NumericUpDown TileThueBoxForTest { get { return _numTileThue; } }
        internal NumericUpDown TienThueBoxForTest { get { return _numTienThue; } }
        internal NumericUpDown PhiVanChuyenBoxForTest { get { return _numPhiVanChuyen; } }
        internal NumericUpDown TongCongBoxForTest { get { return _numTongCong; } }
        internal TextBox SearchBoxForTest { get { return _txtSearch; } }
        internal NumericUpDown QtyBoxForTest { get { return _numQty; } }
        internal ComboBox AddUnitComboForTest { get { return _cboAddUnit; } }
        internal FlatButton AddProductButtonForTest { get { return _btnAddProduct; } }
        internal FlatButton DeleteLineButtonForTest { get { return _btnDeleteLine; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton DeleteButtonForTest { get { return _btnDelete; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal DataGridView ProductGridForTest { get { return _productGrid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal List<StockDocumentLine> LinesForTest { get { return _lines; } }

        public StockDocumentEditDialog(UserSession session, StockDocumentKind kind, string documentId, bool readOnly = false)
            : this(session, kind, documentId, readOnly, false)
        {
        }

        private StockDocumentEditDialog(UserSession session, StockDocumentKind kind, string documentId, bool readOnly, bool previewMode)
            : base(TitlePrefixOf(kind) + " - THÊM MỚI", ShortcutsHint, 1220, 700)
        {
            _session = session ?? AppSession.Current;
            _kind = kind;
            _service = new StockDocumentService(_session, _kind);
            _formRepo = new FormRepository(_session.DatabasePath);
            _isNew = string.IsNullOrEmpty(documentId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);

            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            if (!previewMode)
                WindowState = FormWindowState.Maximized;
            AcceptButton = null;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            _btnDelete = AddButton("Xóa", "danger", delegate { DeleteCurrent(); });
            CancelButton = _btnClose;

            BuildBottom();
            BuildTopPanel();
            WireHeaderEvents();

            LoadLookups();
            if (_isNew)
                NewRecord();
            else
                LoadRecord(documentId);

            _original = Snapshot();
            ApplyPermissions();
            UpdateButtons();

            KeyDown += OnKeyDown;
            Shown += delegate
            {
                if (_loadFailed)
                {
                    ShowWarning(StockDocumentConstants.NotFoundOf(_kind));
                    Close();
                    return;
                }
                if (!_readOnly && _txtNumber.Enabled && !_txtNumber.ReadOnly)
                {
                    _txtNumber.Focus();
                    _txtNumber.SelectAll();
                }
            };
        }

        private static string TitlePrefixOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? "PHIẾU CHUYỂN KHO" : "PHIẾU XUẤT KHO";
        }

        private void WireHeaderEvents()
        {
            _txtNumber.TextChanged += delegate { OnDirtyChanged(); };
            _dtNgay.ValueChanged += delegate { OnDirtyChanged(); };
            _sourceWarehouse.ValueChanged += delegate { OnDirtyChanged(); };
            _destWarehouse.ValueChanged += delegate { OnDirtyChanged(); };
            _supplier.ValueChanged += delegate { OnDirtyChanged(); };
            _exportEmployee.ValueChanged += delegate { OnDirtyChanged(); };
            _importEmployee.ValueChanged += delegate { OnDirtyChanged(); };
            _txtDesc.TextChanged += delegate { OnDirtyChanged(); };
            _txtNote.TextChanged += delegate { OnDirtyChanged(); };
        }

        private void BuildTopPanel()
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            int topH = _kind == StockDocumentKind.Transfer ? (int)(198 * s) : (int)(172 * s);
            var top = new Panel { Dock = DockStyle.Top, Height = topH, Padding = new Padding(0, (int)(4 * s), 0, (int)(6 * s)) };
            Body.Controls.Add(top);

            string[] col0Labels = _kind == StockDocumentKind.Transfer
                ? new[] { "Ngày", "Kho xuất *", "Nhân viên xuất", "Diễn giải" }
                : new[] { "Ngày", "Kho xuất *", "Nhân viên xuất", "Ghi chú" };
            string[] col2Labels = _kind == StockDocumentKind.Transfer
                ? new[] { "Số phiếu *", "Kho nhập *", "Nhân viên nhập", "Ghi chú" }
                : new[] { "Số phiếu *", "Nhà cung cấp", "Diễn giải" };

            int col0W = MeasureMaxLabelWidth(col0Labels, bodyFont, s);
            int col2W = MeasureMaxLabelWidth(col2Labels, bodyFont, s);

            var leftGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4,
                Padding = new Padding(0, 0, (int)(12 * s), 0)
            };
            leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col0W));
            leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col2W));
            leftGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < 4; i++)
                leftGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38 * s));

            _dtNgay.Inner.Font = bodyFont;
            _dtNgay.Height = (int)(30 * s);
            _dtNgay.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            foreach (Control c in new Control[] { _txtNumber, _sourceWarehouse, _destWarehouse, _supplier, _exportEmployee, _importEmployee, _txtDesc, _txtNote })
            {
                c.Font = bodyFont;
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }

            leftGrid.Controls.Add(FieldLabel("Ngày", bodyFont), 0, 0);
            leftGrid.Controls.Add(_dtNgay, 1, 0);
            leftGrid.Controls.Add(FieldLabel("Số phiếu *", bodyFont), 2, 0);
            leftGrid.Controls.Add(_txtNumber, 3, 0);

            if (_kind == StockDocumentKind.Transfer)
            {
                leftGrid.Controls.Add(FieldLabel("Kho xuất *", bodyFont), 0, 1);
                leftGrid.Controls.Add(_sourceWarehouse, 1, 1);
                leftGrid.Controls.Add(FieldLabel("Kho nhập *", bodyFont), 2, 1);
                leftGrid.Controls.Add(_destWarehouse, 3, 1);

                leftGrid.Controls.Add(FieldLabel("Nhân viên xuất", bodyFont), 0, 2);
                leftGrid.Controls.Add(_exportEmployee, 1, 2);
                leftGrid.Controls.Add(FieldLabel("Nhân viên nhập", bodyFont), 2, 2);
                leftGrid.Controls.Add(_importEmployee, 3, 2);

                leftGrid.Controls.Add(FieldLabel("Diễn giải", bodyFont), 0, 3);
                leftGrid.Controls.Add(_txtDesc, 1, 3);
                leftGrid.Controls.Add(FieldLabel("Ghi chú", bodyFont), 2, 3);
                leftGrid.Controls.Add(_txtNote, 3, 3);
            }
            else
            {
                leftGrid.Controls.Add(FieldLabel("Kho xuất *", bodyFont), 0, 1);
                leftGrid.Controls.Add(_sourceWarehouse, 1, 1);
                leftGrid.Controls.Add(FieldLabel("Nhà cung cấp", bodyFont), 2, 1);
                leftGrid.Controls.Add(_supplier, 3, 1);

                leftGrid.Controls.Add(FieldLabel("Nhân viên xuất", bodyFont), 0, 2);
                leftGrid.Controls.Add(_exportEmployee, 1, 2);
                leftGrid.Controls.Add(FieldLabel("Diễn giải", bodyFont), 2, 2);
                leftGrid.Controls.Add(_txtDesc, 3, 2);

                leftGrid.Controls.Add(FieldLabel("Ghi chú", bodyFont), 0, 3);
                leftGrid.Controls.Add(_txtNote, 1, 3);
                leftGrid.SetColumnSpan(_txtNote, 3);
            }

            top.Controls.Add(leftGrid);
            BuildMoneyPanel(top, s, bodyFont);
        }

        private void BuildMoneyPanel(Panel top, float s, Font bodyFont)
        {
            Font headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            bool hasTaxShip = _kind == StockDocumentKind.Transfer;
            string[] moneyLabels = hasTaxShip
                ? new[] { "Tiền hàng", "Giảm giá", "Thuế", "Phí vận chuyển" }
                : new[] { "Tiền hàng", "Giảm giá" };
            int lblW = Math.Max(
                MeasureMaxLabelWidth(moneyLabels, bodyFont, s),
                MeasureMaxLabelWidth(new[] { "Tổng cộng" }, headingFont, s));
            int spinW = SystemInformation.VerticalScrollBarWidth;
            int pctBoxW = TextRenderer.MeasureText("-999.99", bodyFont).Width + spinW + (int)Math.Ceiling(20 * s);
            int pctSignW = TextRenderer.MeasureText("%", bodyFont).Width + (int)Math.Ceiling(10 * s);
            int valBoxW = TextRenderer.MeasureText("-999,999,999", headingFont).Width + spinW + (int)Math.Ceiling(24 * s);
            int boxW = lblW + pctBoxW + pctSignW + valBoxW + (int)Math.Ceiling(28 * s);

            _moneyBox = new GroupBox
            {
                Text = "Tổng tiền",
                Dock = DockStyle.Right,
                Width = boxW,
                Font = headingFont,
                Padding = new Padding((int)(8 * s), (int)(2 * s), (int)(8 * s), (int)(4 * s))
            };
            top.Controls.Add(_moneyBox);

            int rows = hasTaxShip ? 5 : 3;
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = rows };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblW));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, pctBoxW));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, pctSignW));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

            SetupMoneyBox(_numTienHang, true, true, bodyFont);
            SetupPercentBox(_numTileGiamGia, bodyFont);
            SetupMoneyBox(_numTienGiamGia, false, false, bodyFont);
            SetupPercentBox(_numTileThue, bodyFont);
            SetupMoneyBox(_numTienThue, true, true, bodyFont);
            SetupMoneyBox(_numPhiVanChuyen, false, false, bodyFont);
            SetupMoneyBox(_numTongCong, true, true, headingFont);

            int r = 0;
            grid.Controls.Add(FieldLabel("Tiền hàng", bodyFont), 0, r);
            grid.Controls.Add(_numTienHang, 1, r);
            grid.SetColumnSpan(_numTienHang, 3);
            r++;

            grid.Controls.Add(FieldLabel("Giảm giá", bodyFont), 0, r);
            grid.Controls.Add(_numTileGiamGia, 1, r);
            grid.Controls.Add(new Label { Text = "%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = bodyFont }, 2, r);
            grid.Controls.Add(_numTienGiamGia, 3, r);
            r++;

            if (hasTaxShip)
            {
                grid.Controls.Add(FieldLabel("Thuế", bodyFont), 0, r);
                grid.Controls.Add(_numTileThue, 1, r);
                grid.Controls.Add(new Label { Text = "%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = bodyFont }, 2, r);
                grid.Controls.Add(_numTienThue, 3, r);
                r++;

                grid.Controls.Add(FieldLabel("Phí vận chuyển", bodyFont), 0, r);
                grid.Controls.Add(_numPhiVanChuyen, 1, r);
                grid.SetColumnSpan(_numPhiVanChuyen, 3);
                r++;
            }

            grid.Controls.Add(FieldLabel("Tổng cộng", headingFont), 0, r);
            grid.Controls.Add(_numTongCong, 1, r);
            grid.SetColumnSpan(_numTongCong, 3);

            _moneyBox.Controls.Add(grid);

            _numTileGiamGia.ValueChanged += delegate
            {
                if (!_loading && !_recalc)
                {
                    _discountByMoney = false;
                    decimal pct = Math.Max(0m, Math.Min(100m, _numTileGiamGia.Value));
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
            num.DecimalPlaces = 0;
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
                SortMode = DataGridViewColumnSortMode.NotSortable,
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
                SortMode = DataGridViewColumnSortMode.NotSortable,
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
}
