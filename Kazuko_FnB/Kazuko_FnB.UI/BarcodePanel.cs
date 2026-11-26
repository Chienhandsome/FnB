using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Khung bên phải "IN MÃ VẠCH" của màn Danh mục mặt hàng (spec in-xuat-excel mục D, Q5, Q8):
    /// thanh nút Thêm, Xóa, Xuất excel, In (không Bartender), lưới 3 cột Mã / Tên / Số lượng (sửa được, số nguyên >= 0),
    /// dòng "Tổng sl" cộng số lượng. Toàn bộ màu/cỡ chữ theo ThemeManager, không SQL trong View.
    /// </summary>
    internal sealed class BarcodePanel : Panel
    {
        private const string ColCode = "bc_code";
        private const string ColName = "bc_name";
        private const string ColQty = "bc_qty";

        private readonly string _databasePath;
        private readonly Func<List<ProductRow>> _getSelectedProducts;
        private readonly BarcodePrintCoordinator _coordinator = new BarcodePrintCoordinator();

        private readonly Panel _header = new Panel { Dock = DockStyle.Top, Tag = "header" };
        private readonly Label _lblTitle = new Label { Text = PrintConstants.BarcodePreviewTitle, Tag = "header", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly FlatButton _btnClose = new FlatButton { Text = "×", Radius = 4, BorderWidth = 1f, AccessibleName = "Đóng khung in mã vạch" };

        private readonly Panel _toolbar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnAdd = new FlatButton { Text = "Thêm" };
        private readonly FlatButton _btnRemove = new FlatButton { Text = "Xóa", Tag = "danger" };
        private readonly FlatButton _btnExportExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In", Tag = "primary" };

        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill };

        private readonly Panel _footer = new Panel { Dock = DockStyle.Bottom, Tag = "footer" };
        private readonly Label _lblTotalCaption = new Label { Text = "Tổng sl:", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _lblTotalValue = new Label { Text = "0", AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

        private readonly Font _headerFont;
        private readonly Font _bodyFont;
        private readonly Font _boldFont;
        private readonly Font _closeFont;

        private ThemeTokens _theme = ThemeManager.Current;
        private bool _updatingGrid;
        private int _totalQuantity;

        public event EventHandler CloseRequested;

        public BarcodePanel(string databasePath, Func<List<ProductRow>> getSelectedProducts)
        {
            _databasePath = databasePath;
            _getSelectedProducts = getSelectedProducts;
            DoubleBuffered = true;
            AutoScroll = false;

            float s = DeviceDpi / 96f;
            _headerFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 0.5f, FontStyle.Bold);
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _boldFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);
            _closeFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 2f, FontStyle.Bold);

            Width = (int)(396 * s);
            MinimumSize = new Size((int)(320 * s), 0);

            BuildHeader(s);
            BuildToolbar(s);
            BuildGrid(s);
            BuildFooter(s);

            Controls.Add(_grid);
            Controls.Add(_footer);
            Controls.Add(_toolbar);
            Controls.Add(_header);

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
        }

        public int TotalQuantity { get { return _totalQuantity; } }
        public int ItemCount { get { return _grid.Rows.Count; } }

        // ---- Hook nội bộ cho selftest ----
        internal FlatButton AddButtonForTest { get { return _btnAdd; } }
        internal FlatButton RemoveButtonForTest { get { return _btnRemove; } }
        internal FlatButton ExportExcelButtonForTest { get { return _btnExportExcel; } }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal DataGridView GridForTest { get { return _grid; } }
        internal Label TitleLabelForTest { get { return _lblTitle; } }
        internal Label TotalCaptionLabelForTest { get { return _lblTotalCaption; } }
        internal Label TotalValueLabelForTest { get { return _lblTotalValue; } }
        internal BarcodePrintCoordinator CoordinatorForTest { get { return _coordinator; } }
        internal int DataErrorCountForTest { get; private set; }
        internal string LastMessageForTest { get; private set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal IPrintOutputHandler PrintOutputForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> TemplatePickerForTest { get; set; }
        internal Func<string, List<PrintTemplateRecord>> TemplateLoaderForTest { get; set; }
        internal bool? CanViewCostOverrideForTest { get; set; }

        private void BuildHeader(float s)
        {
            _header.Height = (int)(38 * s);
            _lblTitle.Font = _headerFont;
            _btnClose.Font = _closeFont;
            _btnClose.Click += delegate
            {
                EventHandler h = CloseRequested;
                if (h != null) h(this, EventArgs.Empty);
            };
            _header.Controls.Add(_lblTitle);
            _header.Controls.Add(_btnClose);
            _header.Resize += delegate { LayoutHeader(); };
            LayoutHeader();
        }

        private void LayoutHeader()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(10 * s);
            int btnSize = (int)(28 * s);
            int btnY = Math.Max(2, (_header.ClientSize.Height - btnSize) / 2);
            _btnClose.SetBounds(Math.Max(pad, _header.ClientSize.Width - btnSize - pad), btnY, btnSize, btnSize);
            _lblTitle.SetBounds(pad, 0, Math.Max(0, _btnClose.Left - pad - (int)(6 * s)), _header.ClientSize.Height);
        }

        private void BuildToolbar(float s)
        {
            _toolbar.Height = (int)(50 * s);
            _btnAdd.Font = _bodyFont;
            _btnRemove.Font = _boldFont;
            _btnExportExcel.Font = _bodyFont;
            _btnPrint.Font = _boldFont;

            _btnAdd.Click += delegate { AddSelectedFromCatalog(); };
            _btnRemove.Click += delegate { RemoveSelectedRows(); };
            _btnExportExcel.Click += delegate { ExportGridToExcel(); };
            _btnPrint.Click += delegate { PrintBarcodes(); };

            _toolbar.Controls.AddRange(new Control[] { _btnAdd, _btnRemove, _btnExportExcel, _btnPrint });
            _toolbar.Resize += delegate { LayoutToolbar(); };
            LayoutToolbar();
        }

        private void LayoutToolbar()
        {
            float s = DeviceDpi / 96f;
            int padX = (int)(8 * s);
            int padY = (int)(7 * s);
            int gap = (int)(6 * s);
            int btnH = Math.Max((int)(34 * s), _toolbar.ClientSize.Height - padY * 2);

            FlatButton[] buttons = { _btnAdd, _btnRemove, _btnExportExcel, _btnPrint };
            int[] minWidths = new int[buttons.Length];
            int totalMin = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                int textW = TextRenderer.MeasureText(buttons[i].Text, buttons[i].Font).Width;
                minWidths[i] = Math.Max((int)(54 * s), textW + (int)(20 * s));
                totalMin += minWidths[i];
            }

            int availW = Math.Max(0, _toolbar.ClientSize.Width - padX * 2 - gap * (buttons.Length - 1));
            int extra = availW > totalMin ? (availW - totalMin) / buttons.Length : 0;
            int x = padX;
            for (int i = 0; i < buttons.Length; i++)
            {
                int w = i == buttons.Length - 1 && availW > totalMin
                    ? Math.Max(minWidths[i], _toolbar.ClientSize.Width - padX - x)
                    : minWidths[i] + extra;
                buttons[i].SetBounds(x, padY, w, btnH);
                x = buttons[i].Right + gap;
            }
        }

        private void BuildGrid(float s)
        {
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.RowTemplate.Height = (int)(30 * s);
            _grid.Font = _bodyFont;
            _grid.ColumnHeadersDefaultCellStyle.Font = _boldFont;
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.AccessibleName = "Danh sách in mã vạch";

            PropertyInfo dbProp = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (dbProp != null) dbProp.SetValue(_grid, true, null);

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColCode, HeaderText = "Mã", ReadOnly = true, ValueType = typeof(string),
                Width = (int)(86 * s), MinimumWidth = (int)(60 * s), SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = ColName, HeaderText = "Tên", ReadOnly = true, ValueType = typeof(string),
                MinimumWidth = (int)(100 * s), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable
            });
            var colQty = new DataGridViewTextBoxColumn
            {
                Name = ColQty, HeaderText = "Số lượng", ReadOnly = false, ValueType = typeof(int),
                Width = (int)(78 * s), MinimumWidth = (int)(66 * s), SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colQty.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(colQty);

            _grid.CellParsing += OnGridCellParsing;
            _grid.CellValueChanged += OnGridCellValueChanged;
            _grid.DataError += OnGridDataError;
        }

        private void BuildFooter(float s)
        {
            _footer.Height = (int)(38 * s);
            _lblTotalCaption.Font = _boldFont;
            _lblTotalValue.Font = _boldFont;
            _footer.Controls.Add(_lblTotalCaption);
            _footer.Controls.Add(_lblTotalValue);
            _footer.Resize += delegate { LayoutFooter(); };
            LayoutFooter();
        }

        private void LayoutFooter()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(12 * s);
            int capW = (int)(100 * s);
            _lblTotalCaption.SetBounds(pad, 0, capW, _footer.ClientSize.Height);
            int valX = _lblTotalCaption.Right + (int)(6 * s);
            _lblTotalValue.SetBounds(valX, 0, Math.Max(0, _footer.ClientSize.Width - valX - pad), _footer.ClientSize.Height);
        }

        public void AddSelectedFromCatalog()
        {
            if (_getSelectedProducts == null) return;
            List<ProductRow> selected = _getSelectedProducts();
            if (selected != null && selected.Count > 0)
                AddProducts(selected);
        }

        public void AddProducts(IList<ProductRow> products)
        {
            if (products == null || products.Count == 0) return;
            CommitPendingEdit();
            _updatingGrid = true;
            try
            {
                int lastTouched = -1;
                for (int i = 0; i < products.Count; i++)
                {
                    ProductRow p = products[i];
                    if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                    int existingIdx = FindRowIndexByProductId(p.Id);
                    if (existingIdx >= 0)
                    {
                        var item = (BarcodePrintRequestItem)_grid.Rows[existingIdx].Tag;
                        item.Quantity = item.Quantity < int.MaxValue ? item.Quantity + 1 : int.MaxValue;
                        _grid.Rows[existingIdx].Cells[ColQty].Value = item.Quantity;
                        lastTouched = existingIdx;
                    }
                    else
                    {
                        var item = new BarcodePrintRequestItem { ProductId = p.Id, Code = p.Code ?? "", Name = p.Name ?? "", Quantity = 1 };
                        int idx = _grid.Rows.Add(item.Code, item.Name, item.Quantity);
                        _grid.Rows[idx].Tag = item;
                        lastTouched = idx;
                    }
                }
                if (lastTouched >= 0 && lastTouched < _grid.Rows.Count)
                {
                    _grid.ClearSelection();
                    _grid.Rows[lastTouched].Selected = true;
                    _grid.CurrentCell = _grid.Rows[lastTouched].Cells[ColQty];
                }
            }
            finally { _updatingGrid = false; }
            UpdateTotal();
        }

        public void RemoveSelectedRows()
        {
            CommitPendingEdit();
            var indices = new List<int>();
            foreach (DataGridViewRow r in _grid.SelectedRows)
                if (!r.IsNewRow && r.Index >= 0) indices.Add(r.Index);
            if (indices.Count == 0 && _grid.CurrentRow != null && !_grid.CurrentRow.IsNewRow && _grid.CurrentRow.Index >= 0)
                indices.Add(_grid.CurrentRow.Index);
            if (indices.Count == 0) return;

            indices.Sort();
            for (int i = indices.Count - 1; i >= 0; i--)
                _grid.Rows.RemoveAt(indices[i]);
            UpdateTotal();
        }

        public bool ExportGridToExcel()
        {
            CommitPendingEdit();
            return GridExcelExporter.Export(this, _grid, "", GridExportOutputForTest, msg => LastMessageForTest = msg);
        }

        public bool PrintBarcodes()
        {
            CommitPendingEdit();
            return _coordinator.Print(
                this, _btnPrint, _databasePath, GetItemsSnapshot(), CanViewCostOverrideForTest,
                PrintOutputForTest, TemplatePickerForTest, TemplateLoaderForTest, msg => LastMessageForTest = msg);
        }

        public List<BarcodePrintRequestItem> GetItemsSnapshot()
        {
            var list = new List<BarcodePrintRequestItem>(_grid.Rows.Count);
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var item = _grid.Rows[i].Tag as BarcodePrintRequestItem;
                if (item != null)
                    list.Add(new BarcodePrintRequestItem { ProductId = item.ProductId, Code = item.Code, Name = item.Name, Quantity = item.Quantity });
            }
            return list;
        }

        private void OnGridCellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count || !string.Equals(_grid.Columns[e.ColumnIndex].Name, ColQty, StringComparison.Ordinal))
                return;
            var item = _grid.Rows[e.RowIndex].Tag as BarcodePrintRequestItem;
            int oldQty = item == null ? 1 : item.Quantity;
            int parsed;
            e.Value = TryParseNonNegativeInt(e.Value, out parsed) ? parsed : oldQty;
            e.ParsingApplied = true;
        }

        private void OnGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_updatingGrid || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count || !string.Equals(_grid.Columns[e.ColumnIndex].Name, ColQty, StringComparison.Ordinal))
                return;
            DataGridViewRow row = _grid.Rows[e.RowIndex];
            var item = row.Tag as BarcodePrintRequestItem;
            if (item == null) return;

            object val = row.Cells[e.ColumnIndex].Value;
            int parsed;
            if (TryParseNonNegativeInt(val, out parsed))
            {
                item.Quantity = parsed;
                if (!(val is int))
                {
                    _updatingGrid = true;
                    try { row.Cells[e.ColumnIndex].Value = parsed; } finally { _updatingGrid = false; }
                }
            }
            else
            {
                _updatingGrid = true;
                try { row.Cells[e.ColumnIndex].Value = item.Quantity; } finally { _updatingGrid = false; }
            }
            UpdateTotal();
        }

        private void OnGridDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            DataErrorCountForTest++;
            e.ThrowException = false;
            e.Cancel = false;
        }

        private static bool TryParseNonNegativeInt(object raw, out int value)
        {
            value = 0;
            if (raw == null || raw is DBNull) return false;
            if (raw is int)
            {
                int v = (int)raw;
                if (v < 0) return false;
                value = v;
                return true;
            }
            string s = Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(s)) return false;
            string trimmed = s.Trim();
            for (int i = 0; i < trimmed.Length; i++)
                if (trimmed[i] < '0' || trimmed[i] > '9') return false;
            return int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        private void CommitPendingEdit()
        {
            if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
        }

        private int FindRowIndexByProductId(string productId)
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var item = _grid.Rows[i].Tag as BarcodePrintRequestItem;
                if (item != null && string.Equals(item.ProductId, productId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private void UpdateTotal()
        {
            long sum = 0;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var item = _grid.Rows[i].Tag as BarcodePrintRequestItem;
                if (item != null && item.Quantity > 0) sum += item.Quantity;
            }
            _totalQuantity = sum > int.MaxValue ? int.MaxValue : (int)sum;
            _lblTotalValue.Text = _totalQuantity.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"));
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed) ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            if (t == null || IsDisposed) return;
            _theme = t;
            ThemeApplier.Apply(this, t);
            _header.BackColor = t.Primary;
            _lblTitle.ForeColor = t.OnPrimary;
            _btnClose.SurfaceColor = t.Primary;
            _btnClose.FillColor = Draw.Darken(t.Primary, 0.14f);
            _btnClose.TextColor = t.OnPrimary;
            _btnClose.BorderColor = Color.Empty;
            _btnClose.Invalidate();

            _toolbar.BackColor = t.Background;
            ThemeApplier.ApplyButton(_btnAdd, t, null);
            ThemeApplier.ApplyButton(_btnRemove, t, "danger");
            ThemeApplier.ApplyButton(_btnExportExcel, t, null);
            ThemeApplier.ApplyButton(_btnPrint, t, "primary");

            _footer.BackColor = Draw.Mix(t.Background, t.Primary, 0.08f);
            _lblTotalCaption.ForeColor = t.Text;
            _lblTotalValue.ForeColor = t.Primary;
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Draw.StrongBorder(_theme)))
                e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _headerFont.Dispose();
                _bodyFont.Dispose();
                _boldFont.Dispose();
                _closeFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
