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
    /// Màn "Tồn kho ban đầu" (QUẢN TRỊ &gt; Dữ liệu ban đầu &gt; Tồn kho ban đầu, docs/specs/so-du-dau-ky.md P1):
    /// chọn Kho, Ngày (mặc định 31/12 năm trước, khóa khi thiếu quyền Thay đổi ngày), Tìm không dấu,
    /// tích "Chỉ hiện hàng đã nhập tồn", lưới nhập trực tiếp Tồn đầu &amp; Giá vốn bằng <see cref="NumericEditGridCell"/>
    /// (Enter chuyển ô), hàng tổng cộng đậm, nút Lưu (cần quyền Sửa), Xuất Excel, cảnh báo khi đóng còn thay đổi chưa lưu.
    /// Phần nạp dữ liệu, sửa ô, lưu, lọc, đóng tab, xuất Excel đặt ở <c>OpeningStockView.Actions.cs</c>;
    /// phần đổ lưới, hàng tổng cộng, menu ngữ cảnh, đổi theme đặt ở <c>OpeningStockView.Render.cs</c>.
    /// </summary>
    internal sealed partial class OpeningStockView : Panel, IConfirmCloseView
    {
        private static readonly object SummaryRowTag = new object();

        private readonly UserSession _session;
        private readonly OpeningStockService _service;
        private readonly bool _readOnly;

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _summaryFont;

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnSave = new FlatButton { Text = "Lưu (Ctrl+L)", Tag = "primary" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Làm mới (F5)" };
        private readonly Label _summary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };

        private readonly Panel _bottomBar = new Panel { Dock = DockStyle.Bottom };
        private readonly Label _lblTotalValue = new Label { Text = "Tổng giá trị:", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false };
        private readonly NumberBox _numTotalValue = new NumberBox
        {
            Minimum = 0m,
            Maximum = 999999999999999m,
            DecimalPlaces = 0,
            ThousandsSeparator = true,
            ReadOnly = true,
            Increment = 0m,
            InterceptArrowKeys = false,
            TabStop = false,
            TextAlign = HorizontalAlignment.Right
        };

        private readonly Panel _filterPanel = new Panel { Dock = DockStyle.Top };
        private readonly List<FieldFrame> _frames = new List<FieldFrame>();
        private readonly ComboBox _warehouse = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly DateBox _dtDate = new DateBox(false);
        private readonly InputBox _txtSearch = new InputBox();
        private readonly CheckBox _chkOnlyEntered = new CheckBox { Text = "Chỉ hiện hàng đã nhập tồn", AutoSize = true };

        private readonly Panel _warnBanner = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _lblWarn = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        private readonly OpeningStockGrid _grid = new OpeningStockGrid();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        private ThemedContextMenu _gridMenu;
        private ThemeTokens _theme = ThemeManager.Current;
        private List<WarehouseRow> _allowedWarehouses = new List<WarehouseRow>();
        private List<OpeningStockItemRow> _allItems = new List<OpeningStockItemRow>();
        private List<OpeningStockItemRow> _visibleItems = new List<OpeningStockItemRow>();
        private string _activeWarehouseId = "";
        private DateTime _loadedDate;
        private bool _loading;
        private bool _isDirty;
        private bool _updatingCell;
        private bool _cellParseFailed;
        private bool _lastEditRejected;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal string LastInfoMessageForTest { get; private set; }
        internal string LastExportMessageForTest { get; private set; }
        internal OpeningStockSaveResult LastSaveResultForTest { get; private set; }
        internal bool UnsavedPromptTriggeredForTest { get; private set; }
        internal Func<bool> ConfirmUnsavedOverrideForTest { get; set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal int DataErrorCountForTest { get; private set; }
        internal string DataErrorDetailForTest { get; private set; }

        internal DataGridView GridForTest { get { return _grid; } }
        internal ComboBox WarehouseComboForTest { get { return _warehouse; } }
        internal DateBox DateBoxForTest { get { return _dtDate; } }
        internal InputBox SearchBoxForTest { get { return _txtSearch; } }
        internal CheckBox OnlyEnteredCheckForTest { get { return _chkOnlyEntered; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal Label SummaryLabelForTest { get { return _summary; } }
        internal Panel BottomBarForTest { get { return _bottomBar; } }
        internal Label TotalValueLabelForTest { get { return _lblTotalValue; } }
        internal NumberBox TotalValueBoxForTest { get { return _numTotalValue; } }
        internal Panel WarningBannerForTest { get { return _warnBanner; } }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal bool IsDirtyForTest { get { return _isDirty; } }

        public OpeningStockView(UserSession session)
        {
            _session = session ?? AppSession.Current;
            _service = new OpeningStockService(_session);
            _readOnly = !_service.CanEdit;

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _summaryFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            _loading = true;
            BuildBar(s);
            BuildFilterBar(s);
            BuildWarningBanner(s);
            BuildBottomBar(s);
            BuildGrid(s);

            Controls.Add(_grid);
            Controls.Add(_bottomBar);
            Controls.Add(_warnBanner);
            Controls.Add(_filterPanel);
            Controls.Add(_bar);
            _grid.BringToFront();
            _loading = false;

            ApplyPermissions();
            BuildGridMenu();
            WireEvents();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            ReloadAll(true);
        }

        private void BuildBar(float s)
        {
            _bar.Height = (int)(54 * s);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding((int)(10 * s), (int)(9 * s), (int)(10 * s), (int)(9 * s))
            };
            foreach (FlatButton b in new[] { _btnSave, _btnExcel, _btnRefresh })
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(8 * s), 0);
                b.Font = _bodyFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s);
                flow.Controls.Add(b);
            }

            _bar.Controls.Add(flow);
        }

        private void BuildBottomBar(float s)
        {
            _bottomBar.Height = (int)(40 * s);
            _summary.Font = _bodyFont;
            _lblTotalValue.Font = _summaryFont;
            _numTotalValue.Font = _summaryFont;
            _bottomBar.Controls.Add(_summary);
            _bottomBar.Controls.Add(_lblTotalValue);
            _bottomBar.Controls.Add(_numTotalValue);
            _bottomBar.Resize += delegate { LayoutBottomBar(); };
        }

        private void BuildFilterBar(float s)
        {
            _filterPanel.AutoSize = true;
            _filterPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding((int)(10 * s), (int)(8 * s), (int)(10 * s), (int)(6 * s))
            };

            AddInlineLabel(flow, "Kho:", s);
            _warehouse.Font = _bodyFont;
            var whFrame = new FieldFrame(_warehouse, 0)
            {
                Width = (int)(220 * s),
                Height = (int)(34 * s),
                Margin = new Padding(0, 0, (int)(14 * s), 0)
            };
            _frames.Add(whFrame);
            flow.Controls.Add(whFrame);

            AddInlineLabel(flow, "Ngày:", s);
            _dtDate.Inner.Font = _bodyFont;
            _dtDate.Width = (int)(138 * s);
            _dtDate.Height = (int)(34 * s);
            _dtDate.Margin = new Padding(0, 0, (int)(14 * s), 0);
            _dtDate.Value = OpeningStockRules.DefaultOpeningDate(DateTime.Today);
            flow.Controls.Add(_dtDate);

            AddInlineLabel(flow, "Tìm kiếm:", s);
            _txtSearch.Placeholder = "Mã hàng, mã sẵn có, tên mặt hàng...";
            _txtSearch.Inner.Font = _bodyFont;
            _txtSearch.Width = (int)(240 * s);
            _txtSearch.Height = (int)(34 * s);
            _txtSearch.Margin = new Padding(0, 0, (int)(14 * s), 0);
            flow.Controls.Add(_txtSearch);

            _chkOnlyEntered.Font = _bodyFont;
            _chkOnlyEntered.Height = (int)(34 * s);
            _chkOnlyEntered.Margin = new Padding(0, (int)(6 * s), 0, 0);
            flow.Controls.Add(_chkOnlyEntered);

            _filterPanel.Controls.Add(flow);
        }

        private void AddInlineLabel(FlowLayoutPanel flow, string text, float s)
        {
            int w = TextRenderer.MeasureText(text, _bodyFont).Width + (int)Math.Ceiling(8 * s);
            var lbl = new Label
            {
                Text = text,
                AutoSize = false,
                Width = w,
                Height = (int)(34 * s),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont,
                Margin = new Padding(0, 0, (int)(4 * s), 0)
            };
            flow.Controls.Add(lbl);
        }

        private void BuildWarningBanner(float s)
        {
            _warnBanner.Height = (int)(34 * s);
            _warnBanner.Padding = new Padding((int)(12 * s), 0, (int)(12 * s), 0);
            _lblWarn.Font = _bodyFont;
            _warnBanner.Controls.Add(_lblWarn);
        }

        private void BuildGrid(float s)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = _readOnly;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(44 * s);
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(36 * s);
            _grid.RowTemplate.Height = (int)(34 * s);
            _grid.Font = _bodyFont;
            _grid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.EnterAdvanceHandler = AdvanceCellOnEnter;

            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(_grid, true, null);

            // 7 cột: Mã hàng, Mã sẵn có (DMATHANG.MASANCO), Tên mặt hàng, ĐVT, Tồn đầu, Giá vốn, Thành tiền.
            AddGridColumn("colCode", "Mã hàng", 96, "MH00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);
            AddGridColumn("colBarcode", "Mã sẵn có", 104, "8930000001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);
            DataGridViewTextBoxColumn nameCol = AddGridColumn("colName", "Tên mặt hàng", 210, "Trà sữa trân châu đường đen", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);
            nameCol.MinimumWidth = nameCol.Width;
            nameCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            AddGridColumn("colUnit", "ĐVT", 76, "Thùng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);

            DataGridViewTextBoxColumn qtyCol = AddGridColumn("colQty", "Tồn đầu", 104, "99.999,99", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight, _readOnly, s);
            NumericEditGridCell.ConfigureColumn(qtyCol, 2);

            DataGridViewTextBoxColumn costCol = AddGridColumn("colCost", "Giá vốn", 122, "999.999.999,99", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight, _readOnly, s);
            NumericEditGridCell.ConfigureColumn(costCol, 2);

            DataGridViewTextBoxColumn amtCol = AddGridColumn("colAmount", "Thành tiền", 132, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight, true, s);
            amtCol.DefaultCellStyle.FormatProvider = NumberInput.ViFormat;

            _empty.Font = _bodyFont;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private DataGridViewTextBoxColumn AddGridColumn(
            string name,
            string header,
            int minWidth96,
            string sample,
            Type type,
            string format,
            DataGridViewContentAlignment align,
            bool readOnly,
            float s)
        {
            int w = MeasureColumnWidth(header, sample, minWidth96, s);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = w,
                ValueType = type,
                ReadOnly = readOnly,
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
            _grid.Columns.Add(col);
            return col;
        }

        private int MeasureColumnWidth(string header, string sample, int minWidth96, float s)
        {
            int w = (int)Math.Ceiling(minWidth96 * s);
            Font hdrFont = _grid.ColumnHeadersDefaultCellStyle.Font ?? _bodyFont;
            int hdrW = TextRenderer.MeasureText(header ?? "", hdrFont).Width + (int)Math.Ceiling(30 * s);
            if (hdrW > w) w = hdrW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, _bodyFont).Width + (int)Math.Ceiling(24 * s);
                if (sampleW > w) w = sampleW;
            }
            return w;
        }

        private void ApplyPermissions()
        {
            _btnSave.Visible = _service.CanEdit;
            _btnSave.Enabled = _service.CanEdit;
            _btnExcel.Enabled = _service.CanView;
            _grid.ReadOnly = _readOnly;
            bool allowDate = _service.CanEdit && _service.CanChangeDate;
            _dtDate.Enabled = allowDate;
            _dtDate.Inner.ReadOnly = !allowDate;
        }

        /// <summary>DataGridView tùy biến phím Enter để chuyển sang ô nhập số kế tiếp (spec §5).</summary>
        private sealed class OpeningStockGrid : DataGridView
        {
            public Func<bool> EnterAdvanceHandler { get; set; }

            private bool TryHandleEnter(Keys keyData)
            {
                if ((keyData & Keys.KeyCode) == Keys.Enter && (keyData & (Keys.Control | Keys.Alt | Keys.Shift)) == 0)
                {
                    if (EnterAdvanceHandler != null && EnterAdvanceHandler())
                        return true;
                }
                return false;
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (TryHandleEnter(keyData))
                    return true;
                return base.ProcessCmdKey(ref msg, keyData);
            }

            protected override bool ProcessDialogKey(Keys keyData)
            {
                if (TryHandleEnter(keyData))
                    return true;
                return base.ProcessDialogKey(keyData);
            }

            protected override bool ProcessDataGridViewKey(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && !e.Control && !e.Alt && !e.Shift)
                {
                    if (EnterAdvanceHandler != null && EnterAdvanceHandler())
                        return true;
                }
                return base.ProcessDataGridViewKey(e);
            }
        }
    }
}
