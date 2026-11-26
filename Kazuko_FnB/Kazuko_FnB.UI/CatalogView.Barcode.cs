using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần khung "IN MÃ VẠCH" bên phải của <see cref="CatalogView"/> (chỉ dùng cho Danh mục mặt hàng DMATHANG)
    /// và các hook nội bộ phục vụ selftest.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private BarcodePanel _barcodePanel;
        private Splitter _barcodeSplitter;

        internal FlatButton PrintBarcodeButtonForTest { get { return _btnPrintBarcode; } }
        internal BarcodePanel BarcodePanelForTest { get { return _barcodePanel; } }

        private void BuildBarcodeUi()
        {
            float s = DeviceDpi / 96f;
            _barcodePanel = new BarcodePanel(_databasePath, GetSelectedProductsForBarcode)
            {
                Dock = DockStyle.Right,
                Visible = false
            };
            _barcodePanel.CloseRequested += delegate { HideBarcodePanel(); };

            _barcodeSplitter = new Splitter
            {
                Dock = DockStyle.Right,
                Width = Math.Max(5, (int)(5 * s)),
                MinExtra = (int)(320 * s),
                MinSize = (int)(320 * s),
                Cursor = Cursors.VSplit,
                Visible = false
            };

            Controls.Add(_barcodeSplitter);
            Controls.Add(_barcodePanel);

            // Thứ tự dock (từ ngoài vào trong: chỉ số lớn nhất ra ngoài cùng):
            // _bar (Top) -> _barcodePanel (Right) -> _barcodeSplitter (Right) -> _split (Fill).
            Controls.SetChildIndex(_split, 0);
            Controls.SetChildIndex(_barcodeSplitter, 1);
            Controls.SetChildIndex(_barcodePanel, 2);
            Controls.SetChildIndex(_bar, 3);
        }

        internal void OpenBarcodePanel()
        {
            if (!_productMode || _barcodePanel == null)
                return;
            if (!_barcodePanel.Visible)
            {
                _barcodePanel.Visible = true;
                if (_barcodeSplitter != null)
                    _barcodeSplitter.Visible = true;
                PerformLayout();
            }
            _barcodePanel.ApplyTheme(_theme);
        }

        internal void HideBarcodePanel()
        {
            if (_barcodePanel == null)
                return;
            _barcodePanel.Visible = false;
            if (_barcodeSplitter != null)
                _barcodeSplitter.Visible = false;
        }

        private List<ProductRow> GetSelectedProductsForBarcode()
        {
            var result = new List<ProductRow>();
            if (!_productMode || _grid.Rows.Count == 0)
                return result;

            var rowIndices = new List<int>();
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (!row.IsNewRow && row.Index >= 0)
                    rowIndices.Add(row.Index);
            }
            rowIndices.Sort();

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rowIndices.Count; i++)
            {
                DataGridViewRow row = _grid.Rows[rowIndices[i]];
                var drv = row.DataBoundItem as DataRowView;
                if (drv == null)
                    continue;
                string id = Convert.ToString(drv[ColId]);
                if (string.IsNullOrEmpty(id) || !seenIds.Add(id))
                    continue;
                ProductRow found = FindProductById(id);
                if (found != null)
                    result.Add(found);
            }
            return result;
        }

        private ProductRow FindProductById(string id)
        {
            for (int i = 0; i < _productRows.Count; i++)
            {
                if (string.Equals(_productRows[i].Id, id, StringComparison.Ordinal))
                    return _productRows[i];
            }
            return null;
        }

        private void ApplyBarcodeTheme(ThemeTokens t, Color page)
        {
            if (_barcodeSplitter != null)
                _barcodeSplitter.BackColor = Draw.Mix(page, t.Border, 0.55f);
            if (_barcodePanel != null)
                _barcodePanel.ApplyTheme(t);
        }

        private void DisposeBarcodeUi()
        {
            if (_barcodePanel != null && !_barcodePanel.IsDisposed)
                _barcodePanel.Dispose();
            if (_barcodeSplitter != null && !_barcodeSplitter.IsDisposed)
                _barcodeSplitter.Dispose();
        }

        internal void SelectProductRowsForTest(params string[] productIds)
        {
            _grid.ClearSelection();
            if (productIds == null || productIds.Length == 0 || _view == null)
                return;
            var targets = new HashSet<string>(productIds, StringComparer.Ordinal);
            DataGridViewRow firstRow = null;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var drv = _grid.Rows[i].DataBoundItem as DataRowView;
                if (drv == null)
                    continue;
                string id = Convert.ToString(drv[ColId]);
                if (targets.Contains(id))
                {
                    if (firstRow == null)
                        firstRow = _grid.Rows[i];
                    _grid.Rows[i].Selected = true;
                }
            }
            if (firstRow != null)
            {
                _grid.CurrentCell = firstRow.Cells[0];
                // Đặt lại Selected cho mọi dòng đích vì gán CurrentCell có thể xóa chọn nhiều dòng.
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    var drv = _grid.Rows[i].DataBoundItem as DataRowView;
                    if (drv != null && targets.Contains(Convert.ToString(drv[ColId])))
                        _grid.Rows[i].Selected = true;
                }
            }
        }
    }
}
