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
    /// Hộp thoại "Trả đồ" (docs/specs/quan-ly-ban-hang-dieu-chinh-hoa-don.md §2.3, §7):
    /// liệt kê các nhóm mặt hàng đã gọi (SLORDER &gt; 0), cho nhập SL sử dụng (SLSUDUNG) hoặc SL trả lại (SLTRALAI),
    /// luôn cân bằng SLTRALAI + SLSUDUNG = SLORDER; nhập vượt đưa SLTRALAI về 0.
    /// </summary>
    internal sealed class ReturnItemsDialog : ThemedDialog
    {
        private readonly List<ReturnableItemRow> _rows;
        private readonly bool _canViewPrice;
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _lblHint = new Label { Dock = DockStyle.Top, AutoSize = false };
        private readonly FlatButton _btnOk;
        private readonly FlatButton _btnCancel;
        private bool _updatingCell;

        public List<ReturnableItemRow> ConfirmedReturns { get; private set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal FlatButton OkButtonForTest { get { return _btnOk; } }
        internal int DataErrorCountForTest { get; private set; }
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }

        public ReturnItemsDialog(string billName, List<ReturnableItemRow> items, bool canViewPrice)
            : base("TRẢ ĐỒ", "Hóa đơn " + (billName ?? "") + " — nhập SL trả lại hoặc SL sử dụng (luôn cân bằng tổng SL gọi).", 780, 480)
        {
            _rows = items ?? new List<ReturnableItemRow>();
            _canViewPrice = canViewPrice;
            ConfirmedReturns = new List<ReturnableItemRow>();

            float s = Dpi;
            int btnH = Math.Max((int)(44 * s), 44);
            _btnCancel = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            _btnOk = AddButton("Xác nhận trả đồ", "primary", delegate { AcceptReturns(); });
            _btnCancel.Height = btnH;
            _btnOk.Height = btnH;
            CancelButton = _btnCancel;
            AcceptButton = _btnOk;

            _lblHint.Height = (int)(26 * s);
            _lblHint.Text = "Sửa cột 'SL sử dụng' hoặc 'SL trả lại' — nếu nhập vượt SL gọi, SL trả lại sẽ tự đặt về 0.";
            _lblHint.TextAlign = ContentAlignment.MiddleLeft;
            _lblHint.Font = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);

            BuildGrid(s);
            PopulateRows();

            Body.Controls.Add(_grid);
            Body.Controls.Add(_lblHint);
            ApplyDialogTheme();
        }

        private void BuildGrid(float s)
        {
            var bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _grid.ReadOnly = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.Font = bodyFont;
            _grid.RowTemplate.Height = Math.Max((int)(34 * s), 32);
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                DataErrorCountForTest++;
                e.ThrowException = false;
            };

            AddCol("colItemName", "Tên hàng", (int)(200 * s), DataGridViewContentAlignment.MiddleLeft, true, true);
            AddCol("colUnit", "ĐVT", (int)(66 * s), DataGridViewContentAlignment.MiddleCenter, false, true);
            AddCol("colPrice", "Đơn giá", (int)(92 * s), DataGridViewContentAlignment.MiddleRight, false, true);
            AddCol("colDisc", "CK%", (int)(60 * s), DataGridViewContentAlignment.MiddleRight, false, true);
            AddCol("colOrder", "SL gọi", (int)(78 * s), DataGridViewContentAlignment.MiddleRight, false, true);
            AddCol("colUsed", "SL sử dụng", (int)(92 * s), DataGridViewContentAlignment.MiddleRight, false, false);
            AddCol("colReturn", "SL trả lại", (int)(92 * s), DataGridViewContentAlignment.MiddleRight, false, false);

            if (!_canViewPrice)
            {
                _grid.Columns["colPrice"].Visible = false;
                _grid.Columns["colDisc"].Visible = false;
            }

            _grid.CellEndEdit += Grid_CellEndEdit;
        }

        private void AddCol(string name, string header, int width, DataGridViewContentAlignment align, bool fill, bool readOnly)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = align;
            if (fill)
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(col);
        }

        private void PopulateRows()
        {
            _updatingCell = true;
            try
            {
                _grid.Rows.Clear();
                foreach (ReturnableItemRow row in _rows)
                {
                    string priceCell = _canViewPrice ? NumberInput.FormatForDisplay(row.UnitPrice, 2, true) : "";
                    string discCell = _canViewPrice ? NumberInput.FormatForDisplay(row.DiscountRate, 2, true) : "";
                    int idx = _grid.Rows.Add(
                        row.ItemName ?? "",
                        row.UnitName ?? "",
                        priceCell,
                        discCell,
                        NumberInput.FormatForDisplay(row.OrderQty, 2, true),
                        NumberInput.FormatForDisplay(row.UsedQty, 2, true),
                        NumberInput.FormatForDisplay(row.ReturnQty, 2, true));
                    _grid.Rows[idx].Tag = row;
                }
            }
            finally { _updatingCell = false; }
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_updatingCell || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
                return;
            var item = _grid.Rows[e.RowIndex].Tag as ReturnableItemRow;
            if (item == null)
                return;

            string colName = _grid.Columns[e.ColumnIndex].Name;
            object raw = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            decimal parsed;
            bool ok = NumberInput.TryParse(raw, out parsed);

            if (colName == "colReturn")
            {
                ReturnItemRules.ApplyReturnQty(item, ok ? parsed : 0m);
            }
            else if (colName == "colUsed")
            {
                ReturnItemRules.ApplyUsedQty(item, ok ? parsed : item.OrderQty);
            }
            else
            {
                return;
            }

            _updatingCell = true;
            try
            {
                _grid.Rows[e.RowIndex].Cells["colUsed"].Value = NumberInput.FormatForDisplay(item.UsedQty, 2, true);
                _grid.Rows[e.RowIndex].Cells["colReturn"].Value = NumberInput.FormatForDisplay(item.ReturnQty, 2, true);
            }
            finally { _updatingCell = false; }
        }

        /// <summary>Hỗ trợ selftest gán giá trị vào ô và kích hoạt đúng sự kiện CellEndEdit thật của lưới.</summary>
        internal void CommitCellEditForTest(int rowIndex, string columnName, string rawText)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return;
            DataGridViewColumn col = _grid.Columns[columnName];
            if (col == null)
                return;
            _grid.CurrentCell = _grid.Rows[rowIndex].Cells[col.Index];
            _grid.Rows[rowIndex].Cells[col.Index].Value = rawText;
            Grid_CellEndEdit(_grid, new DataGridViewCellEventArgs(col.Index, rowIndex));
        }

        private void AcceptReturns()
        {
            _grid.EndEdit();
            ConfirmedReturns.Clear();
            foreach (ReturnableItemRow r in _rows)
            {
                if (r.ReturnQty > 0m)
                    ConfirmedReturns.Add(r);
            }
            if (ConfirmedReturns.Count == 0)
            {
                LastWarningForTest = "Mời bạn nhập số lượng trả lại lớn hơn 0 cho ít nhất một mặt hàng.";
                if (!SuppressPopup)
                    Msg.Warning(this, LastWarningForTest);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            if (_grid != null)
            {
                ThemeApplier.ApplyGrid(_grid, t);
                Color editBg = Draw.Mix(t.Surface, t.Info, 0.12f);
                if (_grid.Columns.Contains("colUsed"))
                    _grid.Columns["colUsed"].DefaultCellStyle.BackColor = editBg;
                if (_grid.Columns.Contains("colReturn"))
                    _grid.Columns["colReturn"].DefaultCellStyle.BackColor = editBg;
            }
            if (_lblHint != null)
                _lblHint.ForeColor = t.TextMuted;
        }
    }
}
