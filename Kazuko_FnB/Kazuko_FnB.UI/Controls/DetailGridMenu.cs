using System;
using System.Collections.Generic;
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
                bool hasCell = grid.CurrentCell != null;
                foreach (ToolStripItem i in copy.DropDownItems)
                    i.Enabled = hasCell;
                columns.DropDownItems.Clear();
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    if (string.IsNullOrEmpty(col.HeaderText))
                        continue;
                    DataGridViewColumn target = col;
                    var item = new ToolStripMenuItem(col.HeaderText)
                    {
                        Font = menu.Font,
                        CheckOnClick = true,
                        Checked = col.Visible
                    };
                    item.Click += delegate { target.Visible = !target.Visible; };
                    columns.DropDownItems.Add(item);
                }
                columns.Enabled = columns.DropDownItems.Count > 0;
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
        }

        private static ToolStripMenuItem Sub(ThemedContextMenu menu, string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text) { Font = menu.Font };
            item.Click += onClick;
            return item;
        }

        private static void CopyCell(DataGridView grid)
        {
            if (grid.CurrentCell == null)
                return;
            object v = grid.CurrentCell.Value;
            Clipboard.SetText(v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private static void CopySelection(DataGridView grid)
        {
            if (grid.SelectedCells.Count == 0)
                return;
            var cells = new List<DataGridViewCell>();
            foreach (DataGridViewCell c in grid.SelectedCells)
                cells.Add(c);
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
            Clipboard.SetText(sb.ToString());
        }

        private static void AutoFit(DataGridView grid)
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
