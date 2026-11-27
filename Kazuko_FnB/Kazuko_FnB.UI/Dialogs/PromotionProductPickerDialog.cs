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
    /// Hộp chọn mặt hàng (DMATHANG) có ô tìm không dấu theo mã/tên cho chi tiết đợt khuyến mại.
    /// </summary>
    internal sealed class PromotionProductPickerDialog : ThemedDialog
    {
        private readonly IList<PromotionProductItem> _products;
        private readonly InputBox _search = new InputBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly FlatButton _btnOk;

        public PromotionProductItem SelectedProduct { get; private set; }

        internal InputBox SearchBoxForTest { get { return _search; } }
        internal DataGridView GridForTest { get { return _grid; } }
        internal FlatButton OkButtonForTest { get { return _btnOk; } }

        public PromotionProductPickerDialog(IList<PromotionProductItem> products, string title = "CHỌN MẶT HÀNG")
            : base(title, "Tìm theo mã hoặc tên mặt hàng rồi chọn (nhấp đôi hoặc Enter)", 640, 460)
        {
            _products = products ?? new List<PromotionProductItem>();
            FlatButton cancel = AddButton("Hủy bỏ", null, delegate { Close(); });
            _btnOk = AddButton("Chấp nhận", "primary", delegate { AcceptSelection(); });
            CancelButton = cancel;
            _btnOk.Enabled = false;

            float s = Dpi;
            var top = new Panel { Dock = DockStyle.Top, Height = (int)(44 * s), Padding = new Padding(0, 0, 0, (int)(8 * s)) };
            _search.Dock = DockStyle.Fill;
            _search.Inner.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _search.Placeholder = "Tìm mã hoặc tên mặt hàng...";
            _search.TextChanged += delegate { RebuildGrid(); };
            _search.Inner.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && _grid.CurrentRow != null)
                {
                    AcceptSelection();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            top.Controls.Add(_search);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.RowTemplate.Height = (int)(30 * s);
            _grid.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            AddColumn("code", "Mã hàng", 100, false, false, s);
            AddColumn("name", "Tên mặt hàng", 220, true, false, s);
            AddColumn("unit", "ĐVT", 85, false, false, s);
            AddColumn("price", "Giá bán", 110, false, true, s);

            _grid.SelectionChanged += delegate { _btnOk.Enabled = _grid.CurrentRow != null && _grid.CurrentRow.Tag is PromotionProductItem; };
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    AcceptSelection();
            };

            Body.Controls.Add(_grid);
            Body.Controls.Add(top);

            RebuildGrid();
            ApplyDialogTheme();
            Shown += delegate { _search.Inner.Focus(); };
        }

        private void AddColumn(string name, string header, int width96, bool fill, bool isMoney, float s)
        {
            int headerNeed = TextRenderer.MeasureText(header, _grid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(26 * s);
            int w = Math.Max((int)(width96 * s), headerNeed);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = headerNeed,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
            };
            if (isMoney)
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.DefaultCellStyle.Format = "N0";
                col.DefaultCellStyle.FormatProvider = NumberInput.ViFormat;
            }
            _grid.Columns.Add(col);
        }

        private void RebuildGrid()
        {
            string term = CatalogRepository.Fold((_search.Text ?? "").Trim());
            _grid.Rows.Clear();
            foreach (PromotionProductItem item in _products)
            {
                if (term.Length > 0)
                {
                    string hay = CatalogRepository.Fold((item.Code ?? "") + " " + (item.Name ?? ""));
                    if (hay.IndexOf(term, StringComparison.Ordinal) < 0)
                        continue;
                }
                int idx = _grid.Rows.Add(item.Code ?? "", item.Name ?? "", item.UnitName ?? "", item.SellPrice);
                _grid.Rows[idx].Tag = item;
            }
            if (_grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
                _grid.Rows[0].Selected = true;
            }
            _btnOk.Enabled = _grid.CurrentRow != null && _grid.CurrentRow.Tag is PromotionProductItem;
        }

        private void AcceptSelection()
        {
            var item = _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as PromotionProductItem;
            if (item == null)
                return;
            SelectedProduct = item;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
