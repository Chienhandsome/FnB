using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem chi tiết Phiếu kiểm kê kho (TDONHANG LOAI = 4, không có ô tiền)
    /// theo docs/specs/kiem-ke-kho.md §2–§5.
    /// Phần lưới mặt hàng &amp; chi tiết đặt ở <c>StockTakeEditDialog.Lines.cs</c>;
    /// phần nạp/lưu/xóa/phân quyền/nút Mặt hàng chưa kiểm kê đặt ở <c>StockTakeEditDialog.Actions.cs</c>;
    /// phần phím tắt/theme/preview đặt ở <c>StockTakeEditDialog.Helpers.cs</c>.
    /// </summary>
    internal sealed partial class StockTakeEditDialog : ThemedDialog, IScreenPreview
    {
        private const string ShortcutsHint = "(F3: Tìm mặt hàng, F8: Xóa dòng, Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
        private const string TitlePrefix = "PHIẾU KIỂM KÊ KHO";

        private readonly UserSession _session;
        private readonly StockTakeService _service;

        private StockTakeRow _row;
        private bool _isNew;
        private bool _savedHadLines;
        private bool _readOnly;
        private bool _loadFailed;
        private bool _loading;
        private string _original = "";

        private List<StockTakeLine> _lines = new List<StockTakeLine>();
        private List<StockTakeProductItem> _products = new List<StockTakeProductItem>();
        private List<WarehouseRow> _warehouses = new List<WarehouseRow>();

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnDelete;
        private readonly FlatButton _btnClose;

        private readonly DateBox _dtNgay = new DateBox();
        private readonly TextBox _txtNumber = new TextBox { MaxLength = StockTakeConstants.MaxTextLength };
        private readonly LookupCombo _warehouse = new LookupCombo();
        private readonly TextBox _txtDesc = new TextBox { MaxLength = StockTakeConstants.MaxTextLength };
        private readonly TextBox _txtNote = new TextBox { MaxLength = StockTakeConstants.MaxTextLength };

        private readonly TextBox _txtSearch = new TextBox();
        private readonly NumberBox _numQty = new NumberBox();
        private readonly ComboBox _cboAddUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly FlatButton _btnAddProduct = new FlatButton { Text = "Thêm", Tag = "primary" };
        private readonly DataGridView _productGrid = new DataGridView();

        private readonly FlatButton _btnDeleteLine = new FlatButton { Text = "Xóa dòng (F8)", Tag = "danger" };
        private readonly FlatButton _btnUncounted = new FlatButton { Text = "Mặt hàng chưa kiểm kê" };
        private readonly FlatButton _btnExportExcel = new FlatButton { Text = "Xuất excel" };
        private readonly DataGridView _detailGrid = new DataGridView();

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }
        public bool Deleted { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal DateBox DateBoxForTest { get { return _dtNgay; } }
        internal TextBox NumberBoxForTest { get { return _txtNumber; } }
        internal LookupCombo WarehouseComboForTest { get { return _warehouse; } }
        internal TextBox DescriptionBoxForTest { get { return _txtDesc; } }
        internal TextBox NoteBoxForTest { get { return _txtNote; } }
        internal TextBox SearchBoxForTest { get { return _txtSearch; } }
        internal NumericUpDown QtyBoxForTest { get { return _numQty; } }
        internal ComboBox AddUnitComboForTest { get { return _cboAddUnit; } }
        internal FlatButton AddProductButtonForTest { get { return _btnAddProduct; } }
        internal FlatButton DeleteLineButtonForTest { get { return _btnDeleteLine; } }
        internal FlatButton UncountedButtonForTest { get { return _btnUncounted; } }
        internal FlatButton ExportExcelButtonForTest { get { return _btnExportExcel; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton DeleteButtonForTest { get { return _btnDelete; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal DataGridView ProductGridForTest { get { return _productGrid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal List<StockTakeLine> LinesForTest { get { return _lines; } }
        internal Func<UncountedStockDialog, DialogResult> UncountedDialogOpenerForTest { get; set; }

        public StockTakeEditDialog(UserSession session, string documentId, bool readOnly = false)
            : this(session, documentId, readOnly, false)
        {
        }

        private StockTakeEditDialog(UserSession session, string documentId, bool readOnly, bool previewMode)
            : base(TitlePrefix + " - THÊM MỚI", ShortcutsHint, 1220, 700)
        {
            _session = session ?? AppSession.Current;
            _service = new StockTakeService(_session);
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
                    ShowWarning(StockTakeConstants.NotFound);
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

        private void WireHeaderEvents()
        {
            _txtNumber.TextChanged += delegate { OnDirtyChanged(); };
            _dtNgay.ValueChanged += delegate { OnDirtyChanged(); };
            _warehouse.ValueChanged += delegate { OnDirtyChanged(); };
            _txtDesc.TextChanged += delegate { OnDirtyChanged(); };
            _txtNote.TextChanged += delegate { OnDirtyChanged(); };
        }

        private void BuildTopPanel()
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            int topH = (int)(130 * s);
            var top = new Panel { Dock = DockStyle.Top, Height = topH, Padding = new Padding(0, (int)(4 * s), 0, (int)(6 * s)) };
            Body.Controls.Add(top);

            string[] col0Labels = { "Ngày", "Kho kiểm kê *", "Ghi chú" };
            string[] col2Labels = { "Số phiếu *", "Diễn giải" };
            int col0W = MeasureMaxLabelWidth(col0Labels, bodyFont, s);
            int col2W = MeasureMaxLabelWidth(col2Labels, bodyFont, s);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col0W));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col2W));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < 3; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38 * s));

            _dtNgay.Inner.Font = bodyFont;
            _dtNgay.Height = (int)(30 * s);
            _dtNgay.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            foreach (Control c in new Control[] { _txtNumber, _warehouse, _txtDesc, _txtNote })
            {
                c.Font = bodyFont;
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }

            grid.Controls.Add(FieldLabel("Ngày", bodyFont), 0, 0);
            grid.Controls.Add(_dtNgay, 1, 0);
            grid.Controls.Add(FieldLabel("Số phiếu *", bodyFont), 2, 0);
            grid.Controls.Add(_txtNumber, 3, 0);

            grid.Controls.Add(FieldLabel("Kho kiểm kê *", bodyFont), 0, 1);
            grid.Controls.Add(_warehouse, 1, 1);
            grid.Controls.Add(FieldLabel("Diễn giải", bodyFont), 2, 1);
            grid.Controls.Add(_txtDesc, 3, 1);

            grid.Controls.Add(FieldLabel("Ghi chú", bodyFont), 0, 2);
            grid.Controls.Add(_txtNote, 1, 2);
            grid.SetColumnSpan(_txtNote, 3);

            top.Controls.Add(grid);
        }

        private static Label FieldLabel(string text, Font font)
        {
            return new Label { Text = text, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = font };
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

        private static void AddNumberColumn(DataGridView grid, string name, string header, int minWidth96,
            string sample, string format, bool readOnly, float s)
        {
            int w = MeasureGridColumnWidth(grid, header, sample, minWidth96, s);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = Math.Min(w, (int)Math.Ceiling(56 * s)),
                ValueType = typeof(decimal),
                ReadOnly = readOnly,
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
