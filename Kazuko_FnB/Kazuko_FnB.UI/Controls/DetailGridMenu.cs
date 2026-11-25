using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Menu chuột phải của lưới chỉ-xem ở các pane dưới (menu "mnuReadOnly" của No1DataGrid app gốc, đã decompile):
    /// Sao chép ▸ (Sao chép ô, Sao chép vùng chọn) · Cột hiển thị ▸ · — · Refresh · Tìm kiếm… · In danh sách · — ·
    /// Tự động dãn cột · Thuộc tính. Tìm kiếm/In/Thuộc tính chưa hỗ trợ (báo như các nút Excel/In khác).
    /// </summary>
    internal static class DetailGridMenu
    {
        private const string UnsupportedText = "Chức năng chưa hỗ trợ trong phiên bản này.";

        /// <summary>Gắn menu vào lưới; <paramref name="refresh"/> null thì mục Refresh bị mờ.</summary>
        public static void Attach(DataGridView grid, IWin32Window owner, Action refresh)
        {
            AttachMenu(grid, owner, refresh);
        }

        /// <summary>Gắn menu vào lưới và trả về thể hiện menu (để kiểm thử kiểm tra mục menu thật).</summary>
        internal static ThemedContextMenu AttachMenu(DataGridView grid, IWin32Window owner, Action refresh)
        {
            var menu = new ThemedContextMenu();
            ToolStripMenuItem copy = menu.AddItem("Sao chép", null);
            copy.DropDownItems.Add(Sub(menu, "Sao chép ô", delegate { CopyCell(grid); }));
            copy.DropDownItems.Add(Sub(menu, "Sao chép vùng chọn", delegate { CopySelection(grid); }));
            ToolStripMenuItem columns = menu.AddItem("Cột hiển thị", null);
            menu.AddSeparator();
            ToolStripMenuItem refreshItem = menu.AddItem("Refresh", delegate { if (refresh != null) refresh(); });
            refreshItem.Enabled = refresh != null;
            menu.AddItem("Tìm kiếm...", delegate { Msg.Info(owner, UnsupportedText); });
            menu.AddItem("In danh sách", delegate { Msg.Info(owner, UnsupportedText); });
            menu.AddSeparator();
            menu.AddItem("Tự động dãn cột", delegate { AutoFit(grid); });
            menu.AddItem("Thuộc tính", delegate { Msg.Info(owner, UnsupportedText); });

            menu.Opening += delegate
            {
                bool hasVisibleCell = BuildCellCopyText(grid) != null;
                bool hasVisibleSelection = BuildSelectionCopyText(grid) != null;
                if (copy.DropDownItems.Count >= 2)
                {
                    copy.DropDownItems[0].Enabled = hasVisibleCell;
                    copy.DropDownItems[1].Enabled = hasVisibleSelection;
                }
                FillColumnsMenu(columns, grid, menu.Font);
            };

            grid.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right)
                    return;
                DataGridView.HitTestInfo hit = grid.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
                {
                    grid.ClearSelection();
                    grid.CurrentCell = grid.Rows[hit.RowIndex].Cells[hit.ColumnIndex];
                    grid.Rows[hit.RowIndex].Selected = true;
                }
                menu.Show(grid, e.Location);
            };
            grid.Disposed += delegate { menu.Dispose(); };
            return menu;
        }

        private static ToolStripMenuItem Sub(ThemedContextMenu menu, string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text) { Font = menu.Font };
            item.Click += onClick;
            return item;
        }

        /// <summary>Đổ danh sách cột (đánh dấu = đang hiện) vào menu con "Cột hiển thị".</summary>
        internal static void FillColumnsMenu(ToolStripMenuItem columns, DataGridView grid, Font font)
        {
            columns.DropDownItems.Clear();
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (string.IsNullOrEmpty(col.HeaderText))
                    continue;
                DataGridViewColumn target = col;
                var item = new ToolStripMenuItem(col.HeaderText) { Font = font, CheckOnClick = true, Checked = col.Visible };
                item.Click += delegate { target.Visible = !target.Visible; };
                columns.DropDownItems.Add(item);
            }
            columns.Enabled = columns.DropDownItems.Count > 0;
        }

        /// <summary>Dựng chuỗi sao chép cho ô hiện tại; trả null nếu không có ô hoặc cột đang ẩn.</summary>
        internal static string BuildCellCopyText(DataGridView grid)
        {
            if (grid == null || grid.CurrentCell == null)
                return null;
            DataGridViewColumn col = grid.CurrentCell.OwningColumn;
            if (col == null || !col.Visible)
                return null;
            object v = grid.CurrentCell.Value;
            return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Dựng chuỗi sao chép vùng chọn (phân tách tab giữa các cột, CRLF giữa các dòng),
        /// chỉ gồm các ô thuộc cột đang hiện (Visible = true). Dòng chỉ còn ô ẩn không sinh dòng trống.
        /// Trả null nếu không có ô nào thuộc cột đang hiện.
        /// </summary>
        internal static string BuildSelectionCopyText(DataGridView grid)
        {
            if (grid == null || grid.SelectedCells.Count == 0)
                return null;
            var cells = new List<DataGridViewCell>();
            foreach (DataGridViewCell c in grid.SelectedCells)
            {
                if (c != null && c.OwningColumn != null && c.OwningColumn.Visible)
                    cells.Add(c);
            }
            if (cells.Count == 0)
                return null;
            cells.Sort(delegate(DataGridViewCell a, DataGridViewCell b)
            {
                int r = a.RowIndex.CompareTo(b.RowIndex);
                return r != 0 ? r : a.ColumnIndex.CompareTo(b.ColumnIndex);
            });
            var sb = new StringBuilder();
            int lastRow = -1;
            foreach (DataGridViewCell c in cells)
            {
                if (lastRow >= 0)
                    sb.Append(c.RowIndex == lastRow ? "\t" : "\r\n");
                object v = c.Value;
                sb.Append(v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture));
                lastRow = c.RowIndex;
            }
            return sb.ToString();
        }

        internal static void CopyCell(DataGridView grid)
        {
            string text = BuildCellCopyText(grid);
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        internal static void CopySelection(DataGridView grid)
        {
            string text = BuildSelectionCopyText(grid);
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        internal static void AutoFit(DataGridView grid)
        {
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Visible)
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            if (grid.Columns.Count > 0)
                grid.Columns[grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }
    }
}
