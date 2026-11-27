using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần mở rộng chế độ "Tồn nhiều kho" cho <see cref="InventoryView"/> (docs/specs/quan-tri-nho.md §4):
    /// bảng chi tiết tồn theo từng kho ở dưới lưới chính cho mặt hàng đang chọn (Kho, Tồn kho, Tồn 2 ĐVT, Giá trị vốn),
    /// không có tab tồn theo kích thước/hạn dùng; ẩn cột &amp; giá trị vốn khi thiếu quyền Xem giá nhập.
    /// </summary>
    internal sealed partial class InventoryView
    {
        private const string DetailCostValueColumnName = "colDetailCostValue";

        private SplitContainer _rightSplit;
        private Panel _detailHost;
        private Label _detailTitle;
        private DataGridView _detailGrid;
        private Label _detailEmpty;
        private ThemedContextMenu _detailMenu;
        private bool _rightSplitPlaced;
        private bool _suppressDetailRefresh;
        private Dictionary<string, Dictionary<string, decimal>> _stockByWarehouse =
            new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal);

        // Hook kiểm thử UI chế độ Tồn nhiều kho.
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal Label DetailTitleForTest { get { return _detailTitle; } }
        internal ThemedContextMenu DetailMenuForTest { get { return _detailMenu; } }
        internal SplitContainer RightSplitForTest { get { return _rightSplit; } }

        internal bool SelectProductRowForTest(string productId)
        {
            DataGridViewRow row = FindRowByProductIdForTest(productId);
            if (row == null)
                return false;
            int col = FirstVisibleColumnIndex(_grid);
            _grid.ClearSelection();
            if (col >= 0)
                _grid.CurrentCell = row.Cells[col];
            row.Selected = true;
            if (_allWarehousesMode)
                RefreshWarehouseDetail();
            return true;
        }

        internal DataGridViewRow FindDetailRowByWarehouseIdForTest(string warehouseId)
        {
            if (_detailGrid == null || string.IsNullOrEmpty(warehouseId))
                return null;
            foreach (DataGridViewRow r in _detailGrid.Rows)
            {
                var detail = r.Tag as InventoryWarehouseDetailRow;
                if (detail != null && string.Equals(detail.WarehouseId, warehouseId, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        internal void RefreshWarehouseDetailForTest()
        {
            if (_allWarehousesMode)
                RefreshWarehouseDetail();
        }

        private void BuildAllWarehousesDetailPanel(float s)
        {
            _rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2
            };

            _rightSplit.Panel1.Controls.Add(_grid);

            _detailHost = new Panel { Dock = DockStyle.Fill };
            _detailTitle = new Label
            {
                Text = "Tồn chi tiết theo kho",
                Dock = DockStyle.Top,
                Height = (int)(30 * s),
                Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _headingFont
            };

            _detailGrid = new DataGridView();
            ConfigureReadOnlyGrid(_detailGrid, s);
            DataGridViewColumn whCol = AddGridColumn(
                _detailGrid, "colDetailWarehouse", "Kho", 200,
                "Kho tổng cửa hàng trung tâm", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            whCol.MinimumWidth = whCol.Width;
            whCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            AddGridColumn(
                _detailGrid, "colDetailStock", "Tồn kho", 110,
                "-99.999,5", typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight);
            AddGridColumn(
                _detailGrid, "colDetailTwoUnit", "Tồn 2 ĐVT", 160,
                "99 thùng 23 chai", typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddGridColumn(
                _detailGrid, DetailCostValueColumnName, "Giá trị vốn", 140,
                "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight);

            _detailEmpty = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
                Font = _bodyFont,
                Text = InventoryConstants.EmptyWarehouseDetail
            };
            _detailGrid.Controls.Add(_detailEmpty);
            _detailGrid.Resize += delegate { PositionDetailEmpty(); };
            _detailGrid.RowPostPaint += DrawDetailRowNumber;
            _detailGrid.KeyDown += OnKeyDown;

            BuildDetailGridMenu();
            _detailGrid.MouseDown += DetailGridMouseDown;

            _detailHost.Controls.Add(_detailGrid);
            _detailHost.Controls.Add(_detailTitle);
            _detailGrid.BringToFront();

            _rightSplit.Panel2.Controls.Add(_detailHost);
            _rightSplit.SizeChanged += delegate { PlaceRightSplit(); };
        }

        private void PlaceRightSplit()
        {
            if (_rightSplit == null || _rightSplitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int minTop = (int)(120 * s);
            int minBottom = (int)(110 * s);
            int minTotal = minTop + minBottom + _rightSplit.SplitterWidth + (int)(24 * s);
            if (_rightSplit.Height < minTotal)
                return;
            int wantDetail = Math.Max((int)(160 * s), Math.Min((int)(230 * s), (int)(_rightSplit.Height * 0.34f)));
            int dist = _rightSplit.Height - wantDetail - _rightSplit.SplitterWidth;
            if (dist >= minTop && _rightSplit.Height - dist - _rightSplit.SplitterWidth >= minBottom)
            {
                _rightSplit.SplitterDistance = dist;
                _rightSplitPlaced = true;
            }
        }

        private void ApplyDetailPricePermissionVisibility()
        {
            if (_detailGrid == null)
                return;
            DataGridViewColumn costCol = _detailGrid.Columns[DetailCostValueColumnName];
            if (costCol != null)
                costCol.Visible = _canViewBuyPrice;
        }

        private void BuildDetailGridMenu()
        {
            _detailMenu = new ThemedContextMenu();
            ToolStripMenuItem copy = _detailMenu.AddItem("Sao chép", null);
            var copyCell = new ToolStripMenuItem("Sao chép ô") { Font = _detailMenu.Font };
            copyCell.Click += delegate { DetailGridMenu.CopyCell(_detailGrid); };
            var copySel = new ToolStripMenuItem("Sao chép vùng chọn") { Font = _detailMenu.Font };
            copySel.Click += delegate { DetailGridMenu.CopySelection(_detailGrid); };
            copy.DropDownItems.Add(copyCell);
            copy.DropDownItems.Add(copySel);
            ToolStripMenuItem columns = _detailMenu.AddItem("Cột hiển thị", null);
            _detailMenu.AddItem("Tự động dãn cột", delegate { DetailGridMenu.AutoFit(_detailGrid); });

            _detailMenu.Opening += delegate
            {
                copy.Enabled = _detailGrid != null && _detailGrid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(columns, _detailGrid, _detailMenu.Font);
                if (!_canViewBuyPrice)
                    StripProtectedDetailColumnsFromMenu(columns);
            };
        }

        private void StripProtectedDetailColumnsFromMenu(ToolStripMenuItem columnsMenu)
        {
            if (_detailGrid == null)
                return;
            DataGridViewColumn costCol = _detailGrid.Columns[DetailCostValueColumnName];
            if (costCol == null)
                return;
            for (int i = columnsMenu.DropDownItems.Count - 1; i >= 0; i--)
            {
                if (string.Equals(columnsMenu.DropDownItems[i].Text, costCol.HeaderText, StringComparison.Ordinal))
                    columnsMenu.DropDownItems.RemoveAt(i);
            }
        }

        private void DetailGridMouseDown(object sender, MouseEventArgs e)
        {
            if (_detailGrid == null || _detailMenu == null || e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _detailGrid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                _detailGrid.ClearSelection();
                int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : FirstVisibleColumnIndex(_detailGrid);
                if (col >= 0)
                    _detailGrid.CurrentCell = _detailGrid.Rows[hit.RowIndex].Cells[col];
                _detailGrid.Rows[hit.RowIndex].Selected = true;
            }
            else
            {
                _detailGrid.ClearSelection();
            }
            _detailMenu.Show(_detailGrid, e.Location);
        }

        private void PositionDetailEmpty()
        {
            if (_detailEmpty != null && _detailGrid != null)
                _detailEmpty.SetBounds(0, _detailGrid.Height / 2 - 15, _detailGrid.Width, 30);
        }

        private void DrawDetailRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (_detailGrid == null || !_detailGrid.RowHeadersVisible || e.RowIndex < 0 || e.RowIndex >= _detailGrid.Rows.Count)
                return;
            string text = (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _detailGrid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _detailGrid.Font, bounds, _theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void OnMainGridSelectionChanged(object sender, EventArgs e)
        {
            if (_loading || _suppressDetailRefresh || !_allWarehousesMode)
                return;
            RefreshWarehouseDetail();
        }

        private InventoryItemRow SelectedMainInventoryItem()
        {
            DataGridViewRow row = _grid.CurrentRow;
            if (row == null && _grid.SelectedRows.Count > 0)
                row = _grid.SelectedRows[0];
            if (row == null || ReferenceEquals(row.Tag, SummaryRowTag))
                return null;
            return row.Tag as InventoryItemRow;
        }

        private void ClearWarehouseDetail(string emptyMessage)
        {
            if (_detailGrid == null)
                return;
            _detailGrid.Rows.Clear();
            if (_detailTitle != null)
                _detailTitle.Text = "Tồn chi tiết theo kho";
            if (_detailEmpty != null)
            {
                _detailEmpty.Text = emptyMessage ?? InventoryConstants.EmptyWarehouseDetail;
                _detailEmpty.Visible = true;
            }
        }

        private void RefreshWarehouseDetail()
        {
            if (!_allWarehousesMode || _detailGrid == null)
                return;

            InventoryItemRow item = SelectedMainInventoryItem();
            if (item == null)
            {
                ClearWarehouseDetail(InventoryConstants.EmptyWarehouseDetail);
                return;
            }

            string caption = string.IsNullOrEmpty(item.Code)
                ? (item.Name ?? "")
                : (item.Code + " - " + (item.Name ?? ""));
            _detailTitle.Text = "Tồn chi tiết theo kho — " + caption;

            List<InventoryWarehouseDetailRow> details = _service.BuildProductWarehouseDetails(
                item, _allowedWarehouses, _stockByWarehouse, _chkHideZero.Checked);

            _detailGrid.Rows.Clear();
            ThemeTokens t = ThemeManager.Current;
            foreach (InventoryWarehouseDetailRow d in details)
            {
                // Thiếu quyền Xem giá nhập -> không đưa giá trị vốn vào ô (chống lộ khi sao chép).
                int idx = _detailGrid.Rows.Add(
                    d.WarehouseName ?? "",
                    d.StockQuantity,
                    d.TwoUnitDisplay ?? "",
                    _canViewBuyPrice ? (object)d.CostValue : null);
                _detailGrid.Rows[idx].Tag = d;
                StyleDetailRow(_detailGrid.Rows[idx], d, t);
            }

            if (_detailGrid.Rows.Count > 0)
            {
                int firstCol = FirstVisibleColumnIndex(_detailGrid);
                _detailGrid.Rows[0].Selected = true;
                if (firstCol >= 0)
                    _detailGrid.CurrentCell = _detailGrid.Rows[0].Cells[firstCol];
            }

            _detailEmpty.Visible = details.Count == 0;
            _detailEmpty.Text = InventoryConstants.EmptyWarehouseDetailNoRows;
        }

        private void StyleDetailRow(DataGridViewRow row, InventoryWarehouseDetailRow detail, ThemeTokens t)
        {
            if (row == null || detail == null)
                return;
            if (detail.StockQuantity < 0m)
            {
                row.DefaultCellStyle.BackColor = NegativeRowBackColor(t);
                row.DefaultCellStyle.ForeColor = NegativeRowForeColor(t);
                row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Primary, t.Danger, 0.50f);
                row.DefaultCellStyle.SelectionForeColor = t.OnPrimary;
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.Empty;
                row.DefaultCellStyle.ForeColor = Color.Empty;
                row.DefaultCellStyle.SelectionBackColor = Color.Empty;
                row.DefaultCellStyle.SelectionForeColor = Color.Empty;
            }
        }

        private void ApplyAllWarehousesTheme(ThemeTokens t)
        {
            if (_rightSplit != null)
            {
                _rightSplit.BackColor = Draw.StrongBorder(t);
                _rightSplit.Panel1.BackColor = t.Background;
                _rightSplit.Panel2.BackColor = t.Background;
            }
            if (_detailHost != null)
                _detailHost.BackColor = t.Background;
            if (_detailTitle != null)
            {
                _detailTitle.BackColor = t.Background;
                _detailTitle.ForeColor = t.Text;
            }
            if (_detailGrid != null)
            {
                ThemeApplier.ApplyGrid(_detailGrid, t);
                foreach (DataGridViewRow r in _detailGrid.Rows)
                {
                    var d = r.Tag as InventoryWarehouseDetailRow;
                    if (d != null)
                        StyleDetailRow(r, d, t);
                }
            }
            if (_detailEmpty != null && _detailGrid != null)
            {
                _detailEmpty.BackColor = _detailGrid.BackgroundColor;
                _detailEmpty.ForeColor = t.TextMuted;
            }
        }

        private void DisposeAllWarehousesControls()
        {
            if (_detailMenu != null)
            {
                _detailMenu.Dispose();
                _detailMenu = null;
            }
        }
    }
}
