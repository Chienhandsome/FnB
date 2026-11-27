using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần tô màu từ ThemeTokens, hàng tổng cộng, menu ngữ cảnh cho 3 lưới, vẽ số dòng, vẽ cây nhóm,
    /// đổi theme trực tiếp và giải phóng tài nguyên cho <see cref="LoyaltyView"/>.
    /// </summary>
    internal sealed partial class LoyaltyView
    {
        internal static Color SummaryRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Primary, 0.10f);
        }

        private void BuildContextMenus()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem addPtItem = _gridMenu.AddItem("Tặng/giảm điểm", delegate { OpenAddVoucherDialog(); });
            _gridMenu.AddSeparator();
            _gridMenu.AddItem("Làm mới", Keys.F5, delegate { ReloadAll(); });
            _gridMenu.AddItem("Xuất excel", delegate { ExportCustomersToExcel(); });
            _gridMenu.AddSeparator();
            ToolStripMenuItem gCopy = AddCopySubmenu(_gridMenu, _grid);
            ToolStripMenuItem gCols = _gridMenu.AddItem("Cột hiển thị", null);
            _gridMenu.AddItem("Tự động dãn cột", delegate { DetailGridMenu.AutoFit(_grid); });
            _gridMenu.Opening += delegate
            {
                addPtItem.Visible = _service.CanAdd;
                addPtItem.Enabled = _service.CanAdd && CurrentCustomer() != null;
                gCopy.Enabled = _grid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(gCols, _grid, _gridMenu.Font);
            };

            _billMenu = new ThemedContextMenu();
            _billMenu.AddItem("Xuất excel", delegate { ExportBillsToExcel(); });
            _billMenu.AddSeparator();
            ToolStripMenuItem bCopy = AddCopySubmenu(_billMenu, _billGrid);
            ToolStripMenuItem bCols = _billMenu.AddItem("Cột hiển thị", null);
            _billMenu.AddItem("Tự động dãn cột", delegate { DetailGridMenu.AutoFit(_billGrid); });
            _billMenu.Opening += delegate
            {
                bCopy.Enabled = _billGrid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(bCols, _billGrid, _billMenu.Font);
            };

            _voucherMenu = new ThemedContextMenu();
            ToolStripMenuItem vAdd = _voucherMenu.AddItem("Thêm", delegate { OpenAddVoucherDialog(); });
            ToolStripMenuItem vEdit = _voucherMenu.AddItem("Sửa", delegate { EditSelectedVoucher(); });
            ToolStripMenuItem vDel = _voucherMenu.AddItem("Xóa", delegate { DeleteSelectedVoucher(); });
            _voucherMenu.AddSeparator();
            _voucherMenu.AddItem("Xuất excel", delegate { ExportVouchersToExcel(); });
            _voucherMenu.AddSeparator();
            ToolStripMenuItem vCopy = AddCopySubmenu(_voucherMenu, _voucherGrid);
            ToolStripMenuItem vCols = _voucherMenu.AddItem("Cột hiển thị", null);
            _voucherMenu.AddItem("Tự động dãn cột", delegate { DetailGridMenu.AutoFit(_voucherGrid); });
            _voucherMenu.Opening += delegate
            {
                vAdd.Visible = _service.CanAdd;
                vAdd.Enabled = _service.CanAdd && CurrentCustomer() != null;
                vEdit.Visible = _service.CanEdit;
                vEdit.Enabled = _service.CanEdit && CurrentVoucher() != null;
                vDel.Visible = _service.CanRemove;
                vDel.Enabled = _service.CanRemove && CurrentVoucher() != null;
                vCopy.Enabled = _voucherGrid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(vCols, _voucherGrid, _voucherMenu.Font);
            };
        }

        private static ToolStripMenuItem AddCopySubmenu(ThemedContextMenu menu, DataGridView grid)
        {
            ToolStripMenuItem copy = menu.AddItem("Sao chép", null);
            var copyCell = new ToolStripMenuItem("Sao chép ô") { Font = menu.Font };
            copyCell.Click += delegate { DetailGridMenu.CopyCell(grid); };
            var copySel = new ToolStripMenuItem("Sao chép vùng chọn") { Font = menu.Font };
            copySel.Click += delegate { DetailGridMenu.CopySelection(grid); };
            copy.DropDownItems.Add(copyCell);
            copy.DropDownItems.Add(copySel);
            return copy;
        }

        private static void ShowGridMenu(DataGridView grid, ThemedContextMenu menu, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                grid.ClearSelection();
                int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : FirstVisibleColumnIndex(grid);
                if (col >= 0)
                    grid.CurrentCell = grid.Rows[hit.RowIndex].Cells[col];
                grid.Rows[hit.RowIndex].Selected = true;
            }
            else
            {
                grid.ClearSelection();
            }
            menu.Show(grid, e.Location);
        }

        private void PositionEmpty()
        {
            _empty.SetBounds(0, _grid.Height / 2 - 15, _grid.Width, 30);
        }

        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_grid.RowHeadersVisible || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
                return;
            bool isSummary = ReferenceEquals(_grid.Rows[e.RowIndex].Tag, SummaryRowTag);
            string text = isSummary ? "Σ" : (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, _theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void StyleSummaryRow(DataGridViewRow row, ThemeTokens t)
        {
            if (row == null)
                return;
            row.DefaultCellStyle.Font = _summaryFont;
            row.DefaultCellStyle.BackColor = SummaryRowBackColor(t);
            row.DefaultCellStyle.ForeColor = t.Text;
            row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Surface, t.Primary, 0.22f);
            row.DefaultCellStyle.SelectionForeColor = t.Text;
        }

        private void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
        {
            bool selected = (e.State & TreeNodeStates.Selected) == TreeNodeStates.Selected;
            Color bg = selected ? _theme.Primary : _theme.Surface;
            Color fg = selected ? _theme.OnPrimary : _theme.Text;
            Rectangle fill = new Rectangle(e.Bounds.X, e.Bounds.Y,
                Math.Max(e.Bounds.Width, _tree.ClientSize.Width - e.Bounds.X), e.Bounds.Height);
            using (var br = new SolidBrush(bg))
                e.Graphics.FillRectangle(br, fill);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, _bodyFont, e.Bounds, fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        private static int FirstVisibleColumnIndex(DataGridView g)
        {
            foreach (DataGridViewColumn c in g.Columns)
                if (c.Visible)
                    return c.Index;
            return -1;
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
            _bar.BackColor = t.Background;
            _billBar.BackColor = t.Background;
            _voucherBar.BackColor = t.Background;
            _summary.BackColor = Color.Transparent;
            _summary.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = t.TextMuted;
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = _split.Panel2.BackColor = t.Background;
            _rightSplit.BackColor = Draw.StrongBorder(t);
            _rightSplit.Panel1.BackColor = _rightSplit.Panel2.BackColor = t.Background;
            ThemeApplier.ApplyInput(_txtSearch, t, t.Background, Draw.StrongBorder(t));
            _detailTabs.BackColor = t.Background;
            _detailTabs.ForeColor = t.Text;
            foreach (TabPage p in _detailTabs.TabPages)
                p.BackColor = t.Background;
            ThemeApplier.ApplyGrid(_grid, t);
            ThemeApplier.ApplyGrid(_billGrid, t);
            ThemeApplier.ApplyGrid(_voucherGrid, t);

            RestyleSummaryRows(_grid, t);
            RestyleSummaryRows(_billGrid, t);
            RestyleSummaryRows(_voucherGrid, t);
            _tree.Invalidate();
            UpdateActionState();
        }

        private void RestyleSummaryRows(DataGridView grid, ThemeTokens t)
        {
            foreach (DataGridViewRow r in grid.Rows)
            {
                if (ReferenceEquals(r.Tag, SummaryRowTag))
                    StyleSummaryRow(r, t);
            }
        }

        internal bool ExportCustomersToExcel()
        {
            return GridExcelExporter.Export(
                this, _grid, "KhachHangThanThiet", GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        internal bool ExportBillsToExcel()
        {
            return GridExcelExporter.Export(
                this, _billGrid, "HoaDonKhachHangThanThiet", GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        internal bool ExportVouchersToExcel()
        {
            return GridExcelExporter.Export(
                this, _voucherGrid, "TangDiemTangQua", GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        internal DataGridViewRow FindRowByCustomerIdForTest(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var c = r.Tag as LoyaltyCustomerRow;
                if (c != null && string.Equals(c.Id, customerId, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        internal bool SelectCustomerByIdForTest(string customerId)
        {
            DataGridViewRow r = FindRowByCustomerIdForTest(customerId);
            if (r == null)
                return false;
            int col = FirstVisibleColumnIndex(_grid);
            _grid.ClearSelection();
            r.Selected = true;
            if (col >= 0)
                _grid.CurrentCell = r.Cells[col];
            UpdateActionState();
            LoadSelectedDetail();
            return true;
        }

        internal bool SelectVoucherByIdForTest(string voucherId)
        {
            if (string.IsNullOrEmpty(voucherId))
                return false;
            foreach (DataGridViewRow r in _voucherGrid.Rows)
            {
                var v = r.Tag as LoyaltyPointVoucherRow;
                if (v != null && string.Equals(v.Id, voucherId, StringComparison.Ordinal))
                {
                    int col = FirstVisibleColumnIndex(_voucherGrid);
                    _voucherGrid.ClearSelection();
                    r.Selected = true;
                    if (col >= 0)
                        _voucherGrid.CurrentCell = r.Cells[col];
                    UpdateActionState();
                    return true;
                }
            }
            return false;
        }

        internal bool SelectGroupTagForTest(string groupTag)
        {
            if (_tree.Nodes.Count == 0)
                return false;
            TreeNode root = _tree.Nodes[0];
            TreeNode target = string.IsNullOrEmpty(groupTag) ? root : FindTreeNodeByTag(root, groupTag);
            if (target == null)
                return false;
            _tree.SelectedNode = target;
            return true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ReloadAll();
                return true;
            }
            if (keyData == Keys.F3)
            {
                ReloadCustomers();
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                ReloadCustomers();
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ReloadAll();
                e.Handled = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_gridMenu != null) _gridMenu.Dispose();
                if (_billMenu != null) _billMenu.Dispose();
                if (_voucherMenu != null) _voucherMenu.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_headingFont != null) _headingFont.Dispose();
                if (_summaryFont != null) _summaryFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
