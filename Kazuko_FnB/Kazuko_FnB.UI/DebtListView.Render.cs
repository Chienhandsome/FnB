using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần tô màu từ ThemeTokens, hàng tổng cộng, menu ngữ cảnh, vẽ số dòng, vẽ cây nhóm,
    /// đổi theme trực tiếp và giải phóng tài nguyên cho <see cref="DebtListView"/>.
    /// </summary>
    internal sealed partial class DebtListView
    {
        internal static Color SummaryRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Primary, 0.10f);
        }

        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem actItem = _gridMenu.AddItem(IsCustomer ? "Thu tiền nợ" : "Chi trả nợ", delegate { ExecuteDebtAction(); });
            _gridMenu.AddSeparator();
            _gridMenu.AddItem("Làm mới", Keys.F5, delegate { ReloadAll(); });
            _gridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _gridMenu.AddSeparator();
            ToolStripMenuItem copy = _gridMenu.AddItem("Sao chép", null);
            var copyCell = new ToolStripMenuItem("Sao chép ô") { Font = _gridMenu.Font };
            copyCell.Click += delegate { DetailGridMenu.CopyCell(_grid); };
            var copySel = new ToolStripMenuItem("Sao chép vùng chọn") { Font = _gridMenu.Font };
            copySel.Click += delegate { DetailGridMenu.CopySelection(_grid); };
            copy.DropDownItems.Add(copyCell);
            copy.DropDownItems.Add(copySel);
            ToolStripMenuItem columns = _gridMenu.AddItem("Cột hiển thị", null);
            _gridMenu.AddItem("Tự động dãn cột", delegate { DetailGridMenu.AutoFit(_grid); });

            _gridMenu.Opening += delegate
            {
                bool canAct = CanPerformDebtAction();
                actItem.Visible = canAct;
                actItem.Enabled = canAct && CurrentPartner() != null;
                copy.Enabled = _grid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(columns, _grid, _gridMenu.Font);
            };
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                _grid.ClearSelection();
                int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : FirstVisibleColumnIndex(_grid);
                if (col >= 0)
                    _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[col];
                _grid.Rows[hit.RowIndex].Selected = true;
            }
            else
            {
                _grid.ClearSelection();
            }
            _gridMenu.Show(_grid, e.Location);
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

            foreach (DataGridViewRow r in _grid.Rows)
            {
                if (ReferenceEquals(r.Tag, SummaryRowTag))
                    StyleSummaryRow(r, t);
            }
            _tree.Invalidate();
            UpdateActionState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_gridMenu != null) _gridMenu.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_headingFont != null) _headingFont.Dispose();
                if (_summaryFont != null) _summaryFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
