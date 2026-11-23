using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Kiểu lưới chung của các pane dưới (cùng cỡ chữ/chiều cao tiêu đề/dòng với lưới chính, tiêu đề cột không bị
    /// cắt, dòng "Chưa có dữ liệu" khi rỗng).
    /// </summary>
    internal static class PaneGrid
    {
        public static void Configure(DataGridView g)
        {
            float s = g.DeviceDpi / 96f;
            g.Font = new Font("Segoe UI", 10f);
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = (int)(32 * s);
            g.RowTemplate.Height = (int)(28 * s);
            g.RowHeadersVisible = false;
            g.AllowUserToResizeRows = false;
        }

        /// <summary>Nới cột đủ rộng cho tiêu đề (chữ + lề + mũi tên sắp xếp) để không bị cắt.</summary>
        public static void FitHeaderWidths(DataGridView g)
        {
            float s = g.DeviceDpi / 96f;
            Font font = g.ColumnHeadersDefaultCellStyle.Font ?? g.Font;
            foreach (DataGridViewColumn col in g.Columns)
            {
                if (string.IsNullOrEmpty(col.HeaderText))
                    continue;
                int need = TextRenderer.MeasureText(col.HeaderText, font).Width + (int)(34 * s);
                if (col.Width < need)
                    col.Width = need;
            }
        }

        public static Label AttachEmpty(DataGridView g, string text)
        {
            var empty = new Label
            {
                Text = text,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10.5f),
                Visible = false
            };
            g.Controls.Add(empty);
            g.Resize += delegate { Place(g, empty); };
            return empty;
        }

        public static void Place(DataGridView g, Label empty)
        {
            int h = Math.Max(24, (int)(36 * g.DeviceDpi / 96f));
            int free = g.ClientSize.Height - g.ColumnHeadersHeight - h;
            empty.SetBounds(0, g.ColumnHeadersHeight + Math.Max(4, free / 2), g.ClientSize.Width, h);
        }

        public static void ApplyEmptyTheme(DataGridView g, Label empty, ThemeTokens t)
        {
            empty.BackColor = g.BackgroundColor;
            empty.ForeColor = t.TextMuted;
        }
    }

    /// <summary>
    /// Thêm các tab liên quan (xem DetailRepository) vào một TabControl có sẵn (sau tab "Thông tin"): mỗi tab một
    /// lưới chỉ-xem nạp theo dòng đang chọn, nạp lười khi bấm tab (như No1TreeGrid của app gốc). Lỗi đọc DB không
    /// làm hỏng màn hình: tab đó hiện lưới rỗng.
    /// </summary>
    internal sealed class DetailTabsHost
    {
        private sealed class Page
        {
            public DetailTab Tab;
            public TabPage Control;
            public DataGridView Grid;
            public Label Empty;
            public bool Stale = true;
        }

        private readonly TabControl _tabs;
        private readonly DetailRepository _repo;
        private readonly string _masterTable;
        private readonly IWin32Window _owner;
        private readonly List<Page> _pages = new List<Page>();
        private string _masterId;
        private bool _built;

        public DetailTabsHost(TabControl tabs, string databasePath, string masterTable, IWin32Window owner)
        {
            _tabs = tabs;
            _repo = new DetailRepository(databasePath);
            _masterTable = masterTable;
            _owner = owner;
            _tabs.SelectedIndexChanged += delegate { LoadSelected(); };
        }

        /// <summary>Dựng các tab (một lần) và gắn vào TabControl; gọi lại để gắn lại sau khi Detach.</summary>
        public void Attach()
        {
            if (!_built)
            {
                _built = true;
                List<DetailTab> defs;
                try
                {
                    defs = _repo.LoadTabs(_masterTable);
                }
                catch (DataAccessException)
                {
                    defs = new List<DetailTab>();
                }
                foreach (DetailTab def in defs)
                    _pages.Add(BuildPage(def));
            }
            foreach (Page p in _pages)
            {
                if (!_tabs.TabPages.Contains(p.Control))
                    _tabs.TabPages.Add(p.Control);
            }
        }

        /// <summary>Gỡ các tab khỏi TabControl (khi màn chuyển sang chế độ khác).</summary>
        public void Detach()
        {
            foreach (Page p in _pages)
            {
                if (_tabs.TabPages.Contains(p.Control))
                    _tabs.TabPages.Remove(p.Control);
            }
        }

        /// <summary>Đổi dòng chủ: đánh dấu mọi tab cần nạp lại, nạp ngay tab đang mở.</summary>
        public void SetMaster(string id)
        {
            if (string.Equals(id, _masterId, StringComparison.Ordinal))
                return;
            _masterId = id;
            foreach (Page p in _pages)
                p.Stale = true;
            LoadSelected();
        }

        /// <summary>Nạp lại tab đang mở (sau khi dữ liệu chính được làm mới).</summary>
        public void Refresh()
        {
            foreach (Page p in _pages)
                p.Stale = true;
            LoadSelected();
        }

        public void ApplyTheme(ThemeTokens t)
        {
            foreach (Page p in _pages)
            {
                p.Control.BackColor = t.Background;
                ThemeApplier.ApplyGrid(p.Grid, t);
                p.Grid.BorderStyle = BorderStyle.None; // viền đã có ở khung tab: tránh viền kép
                PaneGrid.ApplyEmptyTheme(p.Grid, p.Empty, t);
            }
        }

        private Page BuildPage(DetailTab def)
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            PaneGrid.Configure(grid);
            float s = grid.DeviceDpi / 96f;
            var vi = new CultureInfo("vi-VN");
            foreach (DetailColumn c in def.Columns)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = "d_" + c.Field,
                    DataPropertyName = c.Field,
                    HeaderText = c.Header,
                    Width = (int)(c.Width * s),
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.Automatic
                };
                col.DefaultCellStyle.FormatProvider = vi;
                switch (c.Kind)
                {
                    case DetailKind.Money:
                        col.DefaultCellStyle.Format = "#,##0.##";
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case DetailKind.Date:
                        col.DefaultCellStyle.Format = "dd/MM/yyyy";
                        break;
                    case DetailKind.DateTime:
                        col.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
                        break;
                    case DetailKind.Bool:
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                        break;
                }
                grid.Columns.Add(col);
            }
            PaneGrid.FitHeaderWidths(grid);
            if (grid.Columns.Count > 0)
                grid.Columns[grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            Label empty = PaneGrid.AttachEmpty(grid, "Chưa có dữ liệu");
            empty.Visible = true;
            grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.ColumnIndex < 0 || e.ColumnIndex >= def.Columns.Count || def.Columns[e.ColumnIndex].Kind != DetailKind.Bool)
                    return;
                bool on = e.Value != null && !(e.Value is DBNull) && Convert.ToInt32(e.Value, CultureInfo.InvariantCulture) != 0;
                e.Value = on ? "✓" : "";
                e.FormattingApplied = true;
            };

            var tab = new TabPage(def.Title);
            tab.Controls.Add(grid);
            var page = new Page { Tab = def, Control = tab, Grid = grid, Empty = empty };
            DetailGridMenu.Attach(grid, _owner, delegate { Reload(page); });
            return page;
        }

        private void LoadSelected()
        {
            TabPage selected = _tabs.SelectedTab;
            foreach (Page p in _pages)
            {
                if (p.Control == selected && p.Stale)
                {
                    Reload(p);
                    return;
                }
            }
        }

        private void Reload(Page p)
        {
            p.Stale = false;
            DataTable rows;
            try
            {
                rows = _repo.LoadRows(p.Tab, _masterId);
            }
            catch (DataAccessException)
            {
                rows = new DataTable();
            }
            p.Grid.DataSource = rows.Columns.Count == 0 ? null : rows;
            p.Empty.Visible = rows.Rows.Count == 0;
            PaneGrid.Place(p.Grid, p.Empty);
        }
    }
}
