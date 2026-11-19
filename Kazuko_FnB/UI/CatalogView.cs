using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn danh mục dùng chung (chỉ xem): cây nhóm bên trái (nếu danh mục có nhóm), lưới bên phải, ô tìm (F3), Làm mới.
    /// Cột, nhãn, thứ tự lấy từ metadata của DB (SCOLUMN/SGRID); chỉ hiện dòng đang dùng (STATUS = 30).
    /// Tìm không phân biệt hoa thường và dấu tiếng Việt. Chọn một nhóm thì lọc theo nhóm đó và mọi nhóm con.
    /// Thêm/Sửa/Xóa chưa có ở bản này (xem docs/specs/goi-demo-ui.md, B2).
    /// </summary>
    internal sealed class CatalogView : Panel
    {
        private const string ColSearch = "_search";
        private const string ColCategory = "_cat";
        private const string ColId = "_id";

        private readonly string _databasePath;
        private readonly string _stableDescId;
        private readonly string _title;

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly TextBox _search = new TextBox();
        private readonly FlatButton _refresh = new FlatButton { Text = "Làm mới" };
        private readonly Label _count = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        private readonly DataGridView _grid = new DataGridView();
        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill };

        private CatalogDefinition _def;
        private DataTable _table;
        private DataView _view;
        private int _total;
        private string _error;
        private ThemeTokens _theme = ThemeManager.Current;
        private Color _page = ThemeManager.Current.Background;
        private bool _loading;
        private bool _splitPlaced;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public CatalogView(string databasePath, string stableDescId, string title)
        {
            _databasePath = databasePath;
            _stableDescId = stableDescId;
            _title = title;
            DoubleBuffered = true;
            float s = DeviceDpi / 96f;

            _bar.Height = (int)(52 * s);
            _bar.Controls.Add(_search);
            _bar.Controls.Add(_refresh);
            _bar.Controls.Add(_count);

            _search.Font = new Font("Segoe UI", 10f);
            _search.TextChanged += delegate { ApplyFilter(); };
            _search.HandleCreated += delegate { SendMessage(_search.Handle, 0x1501, (IntPtr)1, "Tìm... (F3)"); };
            _refresh.Click += delegate { Reload(); };
            _count.Font = new Font("Segoe UI", 9.5f);
            _empty.Font = new Font("Segoe UI", 11f);

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
            _grid.RowTemplate.Height = (int)(28 * s);
            _grid.Font = new Font("Segoe UI", 10f);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.AccessibleName = _title;
            typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_grid, true, null);
            _grid.DataBindingComplete += delegate { _grid.ClearSelection(); };
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };

            _tree.HideSelection = false;
            _tree.ShowLines = true;
            _tree.ShowRootLines = true;
            _tree.Font = new Font("Segoe UI", 10f);
            _tree.ItemHeight = (int)(26 * s);
            _tree.ShowNodeToolTips = true;
            _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _tree.DrawNode += DrawTreeNode;
            _tree.AfterSelect += delegate { ApplyFilter(); };

            _split.FixedPanel = FixedPanel.Panel1;
            _split.Panel1.Controls.Add(_tree);
            _split.Panel2.Controls.Add(_grid);
            _split.Panel1Collapsed = true;
            _split.SizeChanged += delegate { PlaceSplit(); };

            Controls.Add(_split);
            Controls.Add(_bar);
            _bar.Resize += delegate { LayoutBar(); };

            Reload();
            ApplyTheme(_theme, _page);
        }

        /// <summary>Số dòng đang hiện trên lưới (sau tìm/lọc nhóm).</summary>
        public int VisibleRows
        {
            get { return _view == null ? 0 : _view.Count; }
        }

        public int TotalRows
        {
            get { return _total; }
        }

        public string ErrorText
        {
            get { return _error; }
        }

        public CatalogDefinition Definition
        {
            get { return _def; }
        }

        // ------------------------------------------------------------------ Nạp dữ liệu

        public void Reload()
        {
            _loading = true;
            try
            {
                _error = null;
                var repo = new CatalogRepository(_databasePath);
                _def = repo.LoadDefinition(_stableDescId);
                if (_def == null)
                    throw new InvalidOperationException("Không tìm thấy định nghĩa danh mục.");
                CatalogData data = repo.Load(_def);
                _total = data.Rows.Count;
                BuildColumns();
                BuildTree(data);
                BuildTable(data);
            }
            catch (Exception ex)
            {
                _error = "Không đọc được dữ liệu: " + ex.Message;
                _total = 0;
                _grid.DataSource = null;
                _grid.Columns.Clear();
                _view = null;
            }
            finally
            {
                _loading = false;
            }
            ApplyFilter();
        }

        private void BuildColumns()
        {
            float s = DeviceDpi / 96f;
            var vi = new CultureInfo("vi-VN");
            _grid.DataSource = null;
            _grid.Columns.Clear();
            for (int i = 0; i < _def.Columns.Count; i++)
            {
                CatalogColumn c = _def.Columns[i];
                DataGridViewColumn gc;
                if (c.ClrType == typeof(bool))
                    gc = new DataGridViewCheckBoxColumn();
                else
                    gc = new DataGridViewTextBoxColumn();
                gc.Name = "c" + i;
                gc.DataPropertyName = "c" + i;
                gc.HeaderText = c.Caption;
                gc.SortMode = DataGridViewColumnSortMode.Automatic;
                gc.ReadOnly = true;
                int w = c.Width > 0 ? c.Width : DefaultWidth(c);
                // Đủ chỗ cho tiêu đề (cộng chỗ cho mũi tên sắp xếp), không để tiêu đề bị cắt.
                int headerNeed = TextRenderer.MeasureText(c.Caption, _grid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(30 * s);
                gc.Width = Math.Max(Math.Max((int)(60 * s), (int)(w * s)), headerNeed);
                gc.MinimumWidth = (int)(50 * s);
                if (c.ClrType == typeof(decimal))
                {
                    gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    gc.DefaultCellStyle.Format = string.Equals(c.Format, "n0", StringComparison.OrdinalIgnoreCase) ? "N0" : "#,##0.##";
                    gc.DefaultCellStyle.FormatProvider = vi;
                }
                else if (c.ClrType == typeof(DateTime))
                {
                    gc.DefaultCellStyle.Format = string.Equals(c.Format, "TIME", StringComparison.OrdinalIgnoreCase) ? "HH:mm" : "dd/MM/yyyy";
                }
                else if (c.ClrType == typeof(bool))
                {
                    gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                _grid.Columns.Add(gc);
            }
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private static int DefaultWidth(CatalogColumn c)
        {
            if (c.Name == "NAME")
                return 220;
            if (c.ClrType == typeof(bool))
                return 80;
            if (c.ClrType == typeof(decimal))
                return 110;
            if (c.ClrType == typeof(DateTime))
                return 100;
            return 150;
        }

        private readonly Dictionary<string, CatalogNode> _nodes = new Dictionary<string, CatalogNode>(StringComparer.Ordinal);

        private void BuildTree(CatalogData data)
        {
            string selectedId = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as string;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            _nodes.Clear();
            if (_def.CategoryTable == null)
            {
                _split.Panel1Collapsed = true;
                _tree.EndUpdate();
                return;
            }
            _split.Panel1Collapsed = false;
            _splitPlaced = false;
            PlaceSplit();

            foreach (CatalogNode n in data.Nodes)
                _nodes[n.Id] = n;

            var root = new TreeNode("Tất cả") { Tag = null };
            _tree.Nodes.Add(root);

            var byParent = new Dictionary<string, List<CatalogNode>>(StringComparer.Ordinal);
            var tops = new List<CatalogNode>();
            foreach (CatalogNode n in data.Nodes)
            {
                if (string.IsNullOrEmpty(n.ParentId) || !_nodes.ContainsKey(n.ParentId) || n.ParentId == n.Id)
                {
                    tops.Add(n);
                    continue;
                }
                List<CatalogNode> list;
                if (!byParent.TryGetValue(n.ParentId, out list))
                    byParent[n.ParentId] = list = new List<CatalogNode>();
                list.Add(n);
            }
            var cmp = new NodeComparer();
            tops.Sort(cmp);
            foreach (CatalogNode n in tops)
                AddNode(root.Nodes, n, byParent, cmp, 0);
            root.Expand();

            TreeNode pick = selectedId == null ? root : FindNode(root, selectedId) ?? root;
            _tree.SelectedNode = pick;
            _tree.EndUpdate();
        }

        /// <summary>Đặt độ rộng cây nhóm khi vùng đã đủ rộng (SplitterDistance chỉ hợp lệ sau khi có kích thước).</summary>
        private void PlaceSplit()
        {
            if (_splitPlaced || _split.Panel1Collapsed)
                return;
            int want = (int)(300 * DeviceDpi / 96f);
            if (_split.Width > want + (int)(200 * DeviceDpi / 96f))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        private static void AddNode(TreeNodeCollection into, CatalogNode n, Dictionary<string, List<CatalogNode>> byParent, NodeComparer cmp, int depth)
        {
            var tn = new TreeNode(n.Name) { Tag = n.Id };
            into.Add(tn);
            List<CatalogNode> kids;
            if (depth < 64 && byParent.TryGetValue(n.Id, out kids))
            {
                kids.Sort(cmp);
                foreach (CatalogNode k in kids)
                    AddNode(tn.Nodes, k, byParent, cmp, depth + 1);
            }
        }

        /// <summary>Tô dòng đang chọn bằng màu của theme (mặc định của Windows là xanh dương, lạc tông).</summary>
        private void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || e.Bounds.IsEmpty)
                return;
            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            Color back = selected ? _theme.Primary : _tree.BackColor;
            Color fore = selected ? _theme.OnPrimary : _theme.Text;
            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.NodeFont ?? _tree.Font, e.Bounds, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        private static TreeNode FindNode(TreeNode from, string id)
        {
            foreach (TreeNode c in from.Nodes)
            {
                if ((c.Tag as string) == id)
                    return c;
                TreeNode f = FindNode(c, id);
                if (f != null)
                    return f;
            }
            return null;
        }

        private sealed class NodeComparer : IComparer<CatalogNode>
        {
            private static readonly CompareInfo Vi = new CultureInfo("vi-VN").CompareInfo;

            public int Compare(CatalogNode a, CatalogNode b)
            {
                int c = string.CompareOrdinal(a.SortOrder ?? "", b.SortOrder ?? "");
                return c != 0 ? c : Vi.Compare(a.Name ?? "", b.Name ?? "", CompareOptions.IgnoreCase);
            }
        }

        private void BuildTable(CatalogData data)
        {
            var dt = new DataTable();
            for (int i = 0; i < _def.Columns.Count; i++)
                dt.Columns.Add("c" + i, _def.Columns[i].ClrType);
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (CatalogRow r in data.Rows)
            {
                var values = new object[dt.Columns.Count];
                sb.Length = 0;
                for (int i = 0; i < _def.Columns.Count; i++)
                {
                    object v = r.Values[i];
                    values[i] = v ?? DBNull.Value;
                    if (v == null)
                        continue;
                    if (v is string)
                        sb.Append((string)v);
                    else if (v is DateTime)
                        sb.Append(((DateTime)v).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                    else if (v is bool)
                        continue;
                    else
                        sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                    sb.Append(' ');
                }
                values[_def.Columns.Count] = r.Id;
                values[_def.Columns.Count + 1] = CatalogRepository.Fold(sb.ToString());
                values[_def.Columns.Count + 2] = CatalogRepository.CategoryPath(_nodes, r.CategoryId);
                dt.Rows.Add(values);
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        // ------------------------------------------------------------------ Tìm và lọc

        /// <summary>Đặt ô tìm (dùng cho kiểm thử và --render-screen).</summary>
        public void SetSearch(string text)
        {
            _search.Text = text ?? "";
        }

        /// <summary>Chọn nhóm theo tên (so khớp không phân biệt dấu); dùng cho kiểm thử và --render-screen.</summary>
        public bool SelectCategory(string name)
        {
            string target = CatalogRepository.Fold(name);
            TreeNode f = FindByName(_tree.Nodes, target);
            if (f == null)
                return false;
            _tree.SelectedNode = f;
            return true;
        }

        private static TreeNode FindByName(TreeNodeCollection nodes, string foldedName)
        {
            foreach (TreeNode n in nodes)
            {
                if (CatalogRepository.Fold(n.Text) == foldedName)
                    return n;
                TreeNode f = FindByName(n.Nodes, foldedName);
                if (f != null)
                    return f;
            }
            return null;
        }

        private void ApplyFilter()
        {
            if (_loading)
                return;
            if (_view != null)
            {
                var parts = new List<string>();
                string term = CatalogRepository.Fold(_search.Text.Trim());
                if (term.Length > 0)
                    parts.Add("[" + ColSearch + "] LIKE '%" + CatalogRepository.EscapeLike(term) + "%'");
                string catId = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as string;
                if (!string.IsNullOrEmpty(catId))
                    parts.Add("[" + ColCategory + "] LIKE '%|" + CatalogRepository.EscapeLike(catId) + "|%'");
                _view.RowFilter = string.Join(" AND ", parts.ToArray());
                _grid.ClearSelection();
            }
            UpdateCount();
        }

        private void UpdateCount()
        {
            int shown = VisibleRows;
            if (_error != null)
            {
                _count.Text = "";
                _empty.Text = _error;
            }
            else
            {
                _count.Text = shown == _total ? _total + " dòng" : shown + " / " + _total + " dòng";
                _empty.Text = _total == 0 ? "Chưa có dữ liệu" : "Không tìm thấy dòng nào";
            }
            _empty.Visible = _error != null || shown == 0;
            _search.Enabled = _error == null;
            PositionEmpty();
        }

        private void PositionEmpty()
        {
            float s = DeviceDpi / 96f;
            int top = _grid.ColumnHeadersHeight + (int)(24 * s);
            _empty.SetBounds(0, top, _grid.ClientSize.Width, (int)(60 * s));
        }

        // ------------------------------------------------------------------ Bố cục và giao diện

        private void LayoutBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(10 * s);
            int h = _bar.ClientSize.Height - 2 * pad;
            int btnW = (int)(100 * s);
            _search.SetBounds(pad, pad + (h - _search.Height) / 2, (int)(340 * s), _search.Height);
            _refresh.SetBounds(_search.Right + pad, pad, btnW, h);
            _count.SetBounds(_refresh.Right + pad, pad, Math.Max(0, _bar.ClientSize.Width - _refresh.Right - 2 * pad), h);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutBar();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F3 || keyData == (Keys.Control | Keys.F))
            {
                _search.Focus();
                _search.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void ApplyTheme(ThemeTokens t, Color page)
        {
            _theme = t;
            _page = page;
            ThemeApplier.Apply(this, t);
            BackColor = page;
            _bar.BackColor = page;
            _count.BackColor = Color.Transparent;
            _count.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = _error != null ? t.Danger : t.TextMuted;
            _search.BackColor = t.Surface;
            _search.ForeColor = t.Text;
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = page;
            _split.Panel2.BackColor = page;
            _refresh.SurfaceColor = page;
            _refresh.FillColor = t.Surface;
            _refresh.TextColor = t.Text;
            _refresh.BorderColor = Draw.StrongBorder(t);
            _refresh.Invalidate();
            Invalidate(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _search.Font.Dispose();
                _count.Font.Dispose();
                _empty.Font.Dispose();
                _tree.Font.Dispose();
                _grid.Font.Dispose();
                _grid.ColumnHeadersDefaultCellStyle.Font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
