using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại "Mặt hàng chưa kiểm kê" (docs/specs/kiem-ke-kho.md §5):
    /// liệt kê các mặt hàng theo dõi tồn của kho có tồn khác 0 nhưng chưa có dòng trong phiếu kiểm kê đã lưu
    /// (Mã, Tên, ĐVT, Tồn hệ thống) và có nút xuất Excel.
    /// </summary>
    internal sealed class UncountedStockDialog : ThemedDialog
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Tất cả mặt hàng có tồn khác 0 trong kho đã được kiểm kê.",
            Visible = false
        };
        private readonly FlatButton _btnExcel;
        private readonly FlatButton _btnClose;
        private readonly List<StockTakeUncountedItem> _items;

        internal bool SuppressPopup { get; set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal string LastExportMessageForTest { get; private set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<StockTakeUncountedItem> ItemsForTest { get { return _items; } }

        public UncountedStockDialog(string warehouseName, string documentNumber, List<StockTakeUncountedItem> items)
            : base("MẶT HÀNG CHƯA KIỂM KÊ", BuildSubtitle(warehouseName, documentNumber, items), 760, 520)
        {
            _items = items ?? new List<StockTakeUncountedItem>();

            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            float s = Dpi;
            MinimumSize = new Size((int)(560 * s), (int)(380 * s));

            _btnClose = AddButton("Thoát", null, delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            });
            _btnExcel = AddButton("Xuất excel", "primary", delegate { ExportToExcel(); });
            CancelButton = _btnClose;

            BuildGrid(s);
            FillRows();
            ApplyDialogTheme();
        }

        private static string BuildSubtitle(string warehouseName, string documentNumber, List<StockTakeUncountedItem> items)
        {
            int count = items == null ? 0 : items.Count;
            string wh = string.IsNullOrEmpty(warehouseName) ? "" : ("Kho: " + warehouseName + " · ");
            string num = string.IsNullOrEmpty(documentNumber) ? "" : ("Số phiếu: " + documentNumber + " · ");
            return string.Format("{0}{1}{2} mặt hàng có tồn khác 0 chưa kiểm kê", wh, num, count);
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
            _grid.RowTemplate.Height = (int)(30 * s);
            _grid.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            // Đúng 4 cột theo spec §5: Mã, Tên, ĐVT, Tồn hệ thống.
            AddColumn("colCode", "Mã", 110, "MH00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, false, s);
            AddColumn("colName", "Tên", 240, "Nguyên liệu trà sữa mẫu", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);
            AddColumn("colUnit", "ĐVT", 90, "Thùng", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, false, s);
            AddColumn("colSystemQty", "Tồn hệ thống", 126, "999.999,99", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight, false, s);

            _empty.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
            _grid.RowPostPaint += DrawRowNumber;

            Body.Controls.Add(_grid);
        }

        private void AddColumn(string name, string header, int minWidth96, string sample,
            Type valueType, string format, DataGridViewContentAlignment align, bool fill, float s)
        {
            int w = (int)Math.Ceiling(minWidth96 * s);
            Font hdrFont = _grid.ColumnHeadersDefaultCellStyle.Font ?? _grid.Font;
            int hdrW = TextRenderer.MeasureText(header ?? "", hdrFont).Width + (int)Math.Ceiling(28 * s);
            if (hdrW > w) w = hdrW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, _grid.Font).Width + (int)Math.Ceiling(24 * s);
                if (sampleW > w) w = sampleW;
            }
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = fill ? w : Math.Min(w, (int)Math.Ceiling(60 * s)),
                ValueType = valueType,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
            };
            col.DefaultCellStyle.Alignment = align;
            if (format != null)
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
            }
            _grid.Columns.Add(col);
        }

        private void FillRows()
        {
            _grid.Rows.Clear();
            ThemeTokens t = ThemeManager.Current;
            foreach (StockTakeUncountedItem item in _items)
            {
                int idx = _grid.Rows.Add(item.Code ?? "", item.Name ?? "", item.UnitName ?? "", item.SystemQuantity);
                _grid.Rows[idx].Tag = item;
                if (item.SystemQuantity < 0m)
                    StockTakeEditDialog.StyleDiffRow(_grid.Rows[idx], StockTakeDiffTone.Shortage, t);
            }
            _empty.Visible = _items.Count == 0;
            PositionEmpty();
        }

        internal bool ExportToExcel()
        {
            return GridExcelExporter.Export(
                this, _grid, "", GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        private void PositionEmpty()
        {
            _empty.SetBounds(0, _grid.Height / 2 - 15, _grid.Width, 30);
        }

        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_grid.RowHeadersVisible || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
                return;
            string text = (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, ThemeManager.Current.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            if (_grid != null)
            {
                ThemeApplier.ApplyGrid(_grid, t);
                _empty.BackColor = _grid.BackgroundColor;
                _empty.ForeColor = t.TextMuted;
                foreach (DataGridViewRow r in _grid.Rows)
                {
                    var item = r.Tag as StockTakeUncountedItem;
                    if (item != null && item.SystemQuantity < 0m)
                        StockTakeEditDialog.StyleDiffRow(r, StockTakeDiffTone.Shortage, t);
                }
            }
        }
    }
}
