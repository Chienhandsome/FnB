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
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần lưới chọn mặt hàng, lưới chi tiết kiểm kê 2 ĐVT, tô màu chênh lệch từ ThemeTokens và xóa dòng cho <see cref="StockTakeEditDialog"/>.</summary>
    internal sealed partial class StockTakeEditDialog
    {
        private bool _cellParseFailed;
        private bool _updatingDetailCell;
        internal int DetailDataErrorCountForTest { get; private set; }
        internal string DetailDataErrorDetailForTest { get; private set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }

        private void BuildBottom()
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            Body.Controls.Add(split);
            bool placed = false;
            split.SizeChanged += delegate
            {
                if (!placed && split.Width > (int)(760 * s))
                {
                    split.SplitterDistance = (int)(470 * s);
                    placed = true;
                }
            };

            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(40 * s),
                ColumnCount = 6,
                RowCount = 1,
                Padding = new Padding(0, (int)(2 * s), 0, (int)(2 * s))
            };
            int lblSearchW = MeasureMaxLabelWidth(new[] { "Tìm (F3)" }, bodyFont, s);
            int lblQtyW = MeasureMaxLabelWidth(new[] { "Thực tế" }, bodyFont, s);
            int qtyBoxW = TextRenderer.MeasureText("9999.99", bodyFont).Width + SystemInformation.VerticalScrollBarWidth + (int)Math.Ceiling(16 * s);
            int unitW = TextRenderer.MeasureText("Thùng (24)", bodyFont).Width + SystemInformation.VerticalScrollBarWidth + (int)Math.Ceiling(20 * s);
            int btnAddW = TextRenderer.MeasureText(_btnAddProduct.Text, bodyFont).Width + (int)Math.Ceiling(24 * s);

            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblSearchW));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblQtyW));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, qtyBoxW));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, unitW));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, btnAddW + (int)(6 * s)));
            searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _txtSearch.Font = bodyFont;
            _txtSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _txtSearch.TextChanged += delegate { RebuildProductGrid(); };

            var lblQty = new Label { Text = "Thực tế", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = bodyFont };
            _numQty.DecimalPlaces = 2;
            _numQty.Minimum = 0m;
            _numQty.Maximum = 1000000m;
            _numQty.Value = 1m;
            _numQty.Font = bodyFont;
            _numQty.TextAlign = HorizontalAlignment.Right;
            _numQty.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _numQty.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddProduct();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            _cboAddUnit.Font = bodyFont;
            _cboAddUnit.DisplayMember = "Name";
            _cboAddUnit.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            _btnAddProduct.Font = bodyFont;
            _btnAddProduct.Size = new Size(btnAddW, (int)(34 * s));
            _btnAddProduct.Margin = new Padding((int)(6 * s), 0, 0, 0);
            _btnAddProduct.Anchor = AnchorStyles.Right;
            _btnAddProduct.Click += delegate { AddProduct(); };

            searchRow.Controls.Add(FieldLabel("Tìm (F3)", bodyFont), 0, 0);
            searchRow.Controls.Add(_txtSearch, 1, 0);
            searchRow.Controls.Add(lblQty, 2, 0);
            searchRow.Controls.Add(_numQty, 3, 0);
            searchRow.Controls.Add(_cboAddUnit, 4, 0);
            searchRow.Controls.Add(_btnAddProduct, 5, 0);

            SetupGrid(_productGrid, true, s);
            AddTextColumn(_productGrid, "p_code", "Mã hàng", 80, "MH0001", true, false, s);
            AddTextColumn(_productGrid, "p_name", "Tên hàng", 150, "Bột trà sữa", true, true, s);
            AddTextColumn(_productGrid, "p_unit", "ĐVT", 64, "Lon", true, false, s);
            AddTextColumn(_productGrid, "p_bigunit", "ĐVT chẵn", 78, "Thùng", true, false, s);
            AddNumberColumn(_productGrid, "p_quydoi", "Quy đổi", 74, "999,99", "#,##0.##", true, s);
            _productGrid.SelectionChanged += delegate { UpdateAddUnitOptions(); };
            _productGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    AddProduct();
            };
            split.Panel1.Controls.Add(_productGrid);
            split.Panel1.Controls.Add(searchRow);

            var lineRow = new Panel { Dock = DockStyle.Top, Height = (int)(40 * s) };
            var lineFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, (int)(3 * s), 0, 0)
            };
            foreach (FlatButton b in new[] { _btnDeleteLine, _btnUncounted, _btnExportExcel })
            {
                b.Font = bodyFont;
                b.Height = (int)(34 * s);
                b.Width = TextRenderer.MeasureText(b.Text, bodyFont).Width + (int)(22 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                lineFlow.Controls.Add(b);
            }
            _btnDeleteLine.Click += delegate { DeleteLine(); };
            _btnUncounted.Click += delegate { OpenUncountedDialog(); };
            _btnExportExcel.Click += delegate { ExportDetailGridToExcel(); };
            lineRow.Controls.Add(lineFlow);

            SetupGrid(_detailGrid, false, s);
            // Đúng 7 cột theo spec §2: Mã, Tên, ĐVT, Tồn hệ thống, Số lượng thực tế, Chênh lệch, Ghi chú.
            AddTextColumn(_detailGrid, "d_code", "Mã", 76, "MH0001", true, false, s);
            AddTextColumn(_detailGrid, "d_name", "Tên", 150, "Nguyên liệu mẫu", true, true, s);
            AddTextColumn(_detailGrid, "d_unit", "ĐVT", 82, "Thùng", false, false, s);
            AddNumberColumn(_detailGrid, "d_sys", "Tồn hệ thống", 112, "999.999,99", "#,##0.##", true, s);
            AddNumberColumn(_detailGrid, "d_actual", "Số lượng thực tế", 122, "999.999,99", "#,##0.##", false, s);
            AddNumberColumn(_detailGrid, "d_diff", "Chênh lệch", 104, "+999.999,99", "+#,##0.##;-#,##0.##;0", true, s);
            NumericEditGridCell.ConfigureColumn(_detailGrid.Columns["d_actual"] as DataGridViewTextBoxColumn, 2);
            AddTextColumn(_detailGrid, "d_note", "Ghi chú", 120, "Ghi chú dòng", false, false, s);

            _detailGrid.CellParsing += DetailCellParsing;
            _detailGrid.CellValueChanged += DetailCellValueChanged;
            _detailGrid.CellEndEdit += DetailCellEndEdit;
            _detailGrid.CurrentCellDirtyStateChanged += delegate
            {
                NumericEditGridCell.HandleDirtyStateChanged(_detailGrid, OnDirtyChanged);
            };
            _detailGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                DetailDataErrorCountForTest++;
                DetailDataErrorDetailForTest = NumericEditGridCell.AppendDataErrorDetail(DetailDataErrorDetailForTest, _detailGrid, e);
                e.ThrowException = false;
                if (e.RowIndex >= 0 && e.RowIndex < _lines.Count)
                    SyncDetailRow(e.RowIndex);
            };
            split.Panel2.Controls.Add(_detailGrid);
            split.Panel2.Controls.Add(lineRow);
        }

        private void RebuildProductGrid()
        {
            string f = (_txtSearch.Text ?? "").Trim();
            CompareInfo vi = CultureInfo.GetCultureInfo("vi-VN").CompareInfo;
            _productGrid.Rows.Clear();
            foreach (StockTakeProductItem item in _products)
            {
                if (f.Length > 0
                    && vi.IndexOf(item.Name ?? "", f, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0
                    && vi.IndexOf(item.Code ?? "", f, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0)
                    continue;
                int i = _productGrid.Rows.Add(
                    item.Code,
                    item.Name,
                    item.UnitName,
                    item.HasTwoUnits ? item.BigUnitName : "",
                    item.HasTwoUnits ? (object)item.QuyDoi : null);
                _productGrid.Rows[i].Tag = item;
            }
            UpdateAddUnitOptions();
        }

        private void UpdateAddUnitOptions()
        {
            _cboAddUnit.Items.Clear();
            var item = _productGrid.CurrentRow == null ? null : _productGrid.CurrentRow.Tag as StockTakeProductItem;
            if (item == null)
            {
                _cboAddUnit.Enabled = false;
                return;
            }

            // Đặt ĐVT cơ sở lên trước để khớp với Tồn hệ thống; mặt hàng 2 ĐVT cho phép chọn thêm ĐVT chẵn.
            _cboAddUnit.Items.Add(new LookupItem { Id = item.UnitId ?? "", Name = item.UnitName });
            if (item.HasTwoUnits)
            {
                _cboAddUnit.Items.Add(new LookupItem { Id = item.BigUnitId, Name = item.BigUnitName });
                _cboAddUnit.SelectedIndex = 0;
                _cboAddUnit.Enabled = !_readOnly;
            }
            else
            {
                _cboAddUnit.SelectedIndex = 0;
                _cboAddUnit.Enabled = false;
            }
        }

        private void RebuildDetailGrid()
        {
            _updatingDetailCell = true;
            try
            {
                _detailGrid.Rows.Clear();
                ThemeTokens t = ThemeManager.Current;
                for (int i = 0; i < _lines.Count; i++)
                {
                    StockTakeLine l = _lines[i];
                    StockTakeRules.RecalcLine(l);
                    int idx = _detailGrid.Rows.Add(
                        l.ProductCode,
                        l.ProductName,
                        l.UnitName,
                        l.SystemQuantity,
                        l.RawActualQuantity,
                        l.Difference,
                        l.Note);
                    _detailGrid.Rows[idx].Tag = l;
                    ConfigureUnitCell(_detailGrid.Rows[idx], l);
                    StyleDiffRow(_detailGrid.Rows[idx], l.Tone, t);
                }
            }
            finally
            {
                _updatingDetailCell = false;
            }
        }

        private void ConfigureUnitCell(DataGridViewRow row, StockTakeLine line)
        {
            if (row == null || line == null)
                return;
            int colIdx = _detailGrid.Columns["d_unit"].Index;
            if (line.HasTwoUnits && !_readOnly)
            {
                var comboCell = new DataGridViewComboBoxCell
                {
                    FlatStyle = FlatStyle.Flat,
                    DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
                };
                string baseName = string.IsNullOrEmpty(line.BaseUnitName) ? "Lẻ" : line.BaseUnitName;
                string bigName = string.IsNullOrEmpty(line.BigUnitName) ? "Chẵn" : line.BigUnitName;
                comboCell.Items.Add(baseName);
                if (!string.Equals(bigName, baseName, StringComparison.Ordinal))
                    comboCell.Items.Add(bigName);
                string currentName = line.IsUsingBigUnit ? bigName : baseName;
                comboCell.Value = currentName;
                row.Cells[colIdx] = comboCell;
                row.Cells[colIdx].ReadOnly = false;
            }
            else
            {
                row.Cells[colIdx].Value = line.UnitName ?? "";
                row.Cells[colIdx].ReadOnly = true;
            }
        }

        private void SyncDetailRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _lines.Count || rowIndex >= _detailGrid.Rows.Count)
                return;
            _updatingDetailCell = true;
            try
            {
                StockTakeLine l = _lines[rowIndex];
                StockTakeRules.RecalcLine(l);
                DataGridViewRow r = _detailGrid.Rows[rowIndex];
                r.Cells["d_sys"].Value = l.SystemQuantity;
                r.Cells["d_actual"].Value = l.RawActualQuantity;
                r.Cells["d_diff"].Value = l.Difference;
                r.Cells["d_note"].Value = l.Note ?? "";
                StyleDiffRow(r, l.Tone, ThemeManager.Current);
            }
            finally
            {
                _updatingDetailCell = false;
            }
        }

        private void AddProduct()
        {
            if (_readOnly || _productGrid.CurrentRow == null)
                return;
            _detailGrid.EndEdit();

            string warehouseId = _warehouse.SelectedId;
            if (string.IsNullOrEmpty(warehouseId))
            {
                ShowWarning(StockTakeConstants.WarehouseRequired);
                _warehouse.Focus();
                return;
            }

            var item = _productGrid.CurrentRow.Tag as StockTakeProductItem;
            if (item == null)
                return;

            decimal systemQty;
            try
            {
                string excludeOrderId = _isNew ? "" : (_row == null ? "" : (_row.Id ?? ""));
                systemQty = _service.GetStockBalance(item.Id, warehouseId, excludeOrderId);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được tồn hệ thống: " + ex.Message);
                return;
            }

            var selUnit = _cboAddUnit.SelectedItem as LookupItem;
            string unitId = selUnit != null && !string.IsNullOrEmpty(selUnit.Id) ? selUnit.Id : item.UnitId;
            string unitName = selUnit != null && !string.IsNullOrEmpty(selUnit.Name) ? selUnit.Name : item.UnitName;

            string err = StockTakeRules.AddOrMergeLine(
                _lines, item, unitId, unitName, _numQty.Value, systemQty);
            if (!string.IsNullOrEmpty(err))
            {
                ShowWarning(err);
                _numQty.Focus();
                return;
            }

            RebuildDetailGrid();
            UpdateWarehouseLock();
            OnDirtyChanged();
        }

        private void DetailCellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _lines.Count || e.ColumnIndex < 0)
                return;
            if (!NumericEditGridCell.IsNumericCell(_detailGrid, e.RowIndex, e.ColumnIndex))
                return;
            _cellParseFailed = !NumericEditGridCell.ParseCell(_detailGrid, e, 0m);
        }

        private void DetailCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_updatingDetailCell || _loading || _readOnly)
                return;
            if (e.RowIndex < 0 || e.RowIndex >= _lines.Count || e.ColumnIndex < 0)
                return;
            if (_detailGrid.Columns[e.ColumnIndex].Name != "d_unit")
                return;

            StockTakeLine line = _lines[e.RowIndex];
            if (!line.HasTwoUnits)
                return;
            string chosen = Convert.ToString(_detailGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value) ?? "";
            string bigName = string.IsNullOrEmpty(line.BigUnitName) ? "Chẵn" : line.BigUnitName;
            if (string.Equals(chosen, bigName, StringComparison.Ordinal))
            {
                line.UnitId = line.BigUnitId;
                line.UnitName = line.BigUnitName;
            }
            else
            {
                line.UnitId = line.BaseUnitId;
                line.UnitName = line.BaseUnitName;
            }
            SyncDetailRow(e.RowIndex);
            OnDirtyChanged();
        }

        private void DetailCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            LastWarningMessageForTest = null;
            bool parseFailed = _cellParseFailed;
            _cellParseFailed = false;
            if (e.RowIndex < 0 || e.RowIndex >= _lines.Count || e.ColumnIndex < 0)
            {
                OnDirtyChanged();
                return;
            }

            StockTakeLine line = _lines[e.RowIndex];
            DataGridViewRow r = _detailGrid.Rows[e.RowIndex];
            string col = _detailGrid.Columns[e.ColumnIndex].Name;

            if (col == "d_actual")
            {
                decimal v;
                if (parseFailed || !NumberInput.TryParse(r.Cells["d_actual"].Value, out v) || v < 0m)
                {
                    SyncDetailRow(e.RowIndex);
                    ShowWarning(StockTakeConstants.ActualQtyNegative);
                    OnDirtyChanged();
                    return;
                }
                line.RawActualQuantity = v;
                SyncDetailRow(e.RowIndex);
                OnDirtyChanged();
                return;
            }

            if (col == "d_note")
            {
                line.Note = PurchaseRules.Clip(Convert.ToString(r.Cells["d_note"].Value) ?? "");
                SyncDetailRow(e.RowIndex);
                OnDirtyChanged();
                return;
            }

            OnDirtyChanged();
        }

        internal void CommitDetailCellEditForTest(int rowIndex, string columnName, object newValue)
        {
            if (rowIndex < 0 || rowIndex >= _detailGrid.Rows.Count)
                return;
            DataGridViewColumn col = _detailGrid.Columns[columnName];
            if (col == null)
                return;
            _detailGrid.Rows[rowIndex].Cells[columnName].Value = newValue;
            if (columnName == "d_unit")
                DetailCellValueChanged(_detailGrid, new DataGridViewCellEventArgs(col.Index, rowIndex));
            else
                DetailCellEndEdit(_detailGrid, new DataGridViewCellEventArgs(col.Index, rowIndex));
        }

        private void DeleteLine()
        {
            if (_readOnly || _detailGrid.CurrentRow == null)
                return;
            _detailGrid.EndEdit();
            int idx = _detailGrid.CurrentRow.Index;
            if (idx < 0 || idx >= _lines.Count)
                return;
            _lines.RemoveAt(idx);
            RebuildDetailGrid();
            UpdateWarehouseLock();
            OnDirtyChanged();
        }

        private void ExportDetailGridToExcel()
        {
            GridExcelExporter.Export(this, _detailGrid, "", GridExportOutputForTest, null);
        }
    }
}
