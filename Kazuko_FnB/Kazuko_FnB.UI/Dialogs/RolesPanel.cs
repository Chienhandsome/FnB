using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Phân quyền của một nhóm người dùng: tab "Quyền sử dụng" (ma trận Khóa/Xem/Thêm/Sửa/Xóa/Tất cả theo chức năng)
    /// và tab "Quyền xem báo cáo". Mỗi lần tích là ghi ngay vào DB (không có nút Lưu), giống app gốc.
    /// </summary>
    internal sealed class RolesPanel : Panel
    {
        private const int ColName = 0, ColLock = 1, ColView = 2, ColAdd = 3, ColEdit = 4, ColRemove = 5, ColAll = 6;

        private sealed class FuncState
        {
            public FunctionRow F;
            public int Mode;
        }

        private readonly RoleRepository _repo;
        private GroupRow _group;
        private bool _loading;

        // --- tab
        private readonly FlatButton _tabUse, _tabReport;
        private readonly Panel _pageUse = new Panel { Dock = DockStyle.Fill };
        private readonly Panel _pageReport = new Panel { Dock = DockStyle.Fill };

        // --- quyền sử dụng
        private readonly TreeView _funcTree = new TreeView { HideSelection = false, ShowLines = true };
        private readonly DataGridView _funcGrid = new DataGridView();
        private readonly TextBox _funcFilter = new TextBox();
        private List<FuncState> _funcs = new List<FuncState>();

        // --- quyền xem báo cáo
        private readonly TreeView _repTree = new TreeView { HideSelection = false, ShowLines = true };
        private readonly DataGridView _repGrid = new DataGridView();
        private readonly TextBox _repFilter = new TextBox();
        private List<ReportRow> _reports = new List<ReportRow>();
        private HashSet<string> _allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public RolesPanel(string databasePath)
        {
            _repo = new RoleRepository(databasePath);
            float s = DeviceDpi / 96f;
            Dock = DockStyle.Fill;
            Tag = "surface";
            Padding = new Padding((int)(10 * s), 0, 0, 0);

            var tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = (int)(42 * s), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            _tabUse = Tab("Quyền sử dụng", s, delegate { ShowTab(0); });
            _tabReport = Tab("Quyền xem báo cáo", s, delegate { ShowTab(1); });
            tabs.Controls.Add(_tabUse);
            tabs.Controls.Add(_tabReport);

            BuildPage(_pageUse, _funcTree, _funcGrid, _funcFilter, s, true);
            BuildPage(_pageReport, _repTree, _repGrid, _repFilter, s, false);
            _pageReport.Visible = false;

            Controls.Add(_pageUse);
            Controls.Add(_pageReport);
            Controls.Add(tabs);

            _funcTree.AfterSelect += delegate { FillFunctions(); };
            _funcFilter.TextChanged += delegate { FillFunctions(); };
            _funcGrid.CellContentClick += OnFuncClick;
            _funcGrid.DataError += IgnoreGridError;
            _repGrid.DataError += IgnoreGridError;
            _repTree.AfterSelect += delegate { FillReports(); };
            _repFilter.TextChanged += delegate { FillReports(); };
            _repGrid.CellContentClick += OnReportClick;
        }

        private static void IgnoreGridError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false; // không bật hộp lỗi mặc định của DataGridView
        }

        private static FlatButton Tab(string text, float s, EventHandler click)
        {
            var b = new FlatButton { Text = text, Width = (int)(170 * s), Height = (int)(34 * s), Radius = (int)(6 * s), Margin = new Padding(0, 4, (int)(6 * s), 0) };
            b.Click += click;
            return b;
        }

        private void BuildPage(Panel page, TreeView tree, DataGridView grid, TextBox filter, float s, bool functions)
        {
            tree.Dock = DockStyle.Left;
            tree.Width = (int)(220 * s);
            tree.ItemHeight = (int)(24 * s);

            var rightHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding((int)(8 * s), 0, 0, 0) };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = (int)(42 * s), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Lọc:", AutoSize = false, Width = (int)(40 * s), Height = (int)(34 * s), TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 0) });
            filter.Width = (int)(220 * s);
            filter.Margin = new Padding(0, 8, (int)(12 * s), 0);
            bar.Controls.Add(filter);
            var lockAll = new FlatButton { Text = functions ? "Khóa tất cả" : "Không cho xem tất cả", Width = (int)((functions ? 130 : 190) * s), Height = (int)(34 * s), Radius = (int)(6 * s), Margin = new Padding(0, 4, (int)(6 * s), 0) };
            var allowAll = new FlatButton { Text = functions ? "Cho phép tất cả" : "Cho xem tất cả", Width = (int)((functions ? 150 : 150) * s), Height = (int)(34 * s), Radius = (int)(6 * s), Margin = new Padding(0, 4, 0, 0) };
            lockAll.Click += delegate { if (functions) BulkFunctions(false); else BulkReports(false); };
            allowAll.Click += delegate { if (functions) BulkFunctions(true); else BulkReports(true); };
            bar.Controls.Add(lockAll);
            bar.Controls.Add(allowAll);

            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.ColumnHeadersHeight = (int)(32 * s);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = (int)(30 * s);
            int cw = (int)(64 * s);
            if (functions)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Chức năng", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
                foreach (string h in new[] { "Khóa", "Xem", "Thêm", "Sửa", "Xóa", "Tất cả" })
                    grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = h, HeaderText = h, Width = cw });
            }
            else
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Báo cáo", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
                grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "No", HeaderText = "Không được xem", Width = (int)(150 * s) });
                grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Yes", HeaderText = "Được phép xem", Width = (int)(150 * s) });
            }
            rightHost.Controls.Add(grid);
            rightHost.Controls.Add(bar);
            page.Controls.Add(rightHost);
            page.Controls.Add(tree);
        }

        // ------------------------------------------------------------------ nạp

        /// <summary>Hiển thị quyền của một nhóm.</summary>
        public void SetGroup(GroupRow group)
        {
            _group = group;
            if (group == null)
                return;
            _loading = true;
            // Chức năng.
            Dictionary<string, int> modes = _repo.LoadModes(group.Id);
            _funcs = _repo.LoadFunctions().Select(f => new FuncState { F = f, Mode = modes.ContainsKey(f.Id) ? modes[f.Id] : 0 }).ToList();
            _funcTree.BeginUpdate();
            string prevFunc = _funcTree.SelectedNode == null ? null : _funcTree.SelectedNode.Text;
            _funcTree.Nodes.Clear();
            TreeNode root = _funcTree.Nodes.Add("Tất cả");
            var cmp = StringComparer.Create(new CultureInfo("vi-VN"), true);
            foreach (string g in _funcs.Select(f => f.F.GroupName).Distinct().OrderBy(x => x, cmp))
                root.Nodes.Add(g).Tag = g;
            root.Expand();
            _funcTree.EndUpdate();
            _funcTree.SelectedNode = FindNode(root, prevFunc) ?? root;

            // Báo cáo.
            _reports = _repo.LoadReports();
            _allowed = _repo.LoadAllowedReports(group.Id);
            _repTree.BeginUpdate();
            string prevRep = _repTree.SelectedNode == null ? null : _repTree.SelectedNode.Text;
            _repTree.Nodes.Clear();
            TreeNode rroot = _repTree.Nodes.Add("Tất cả");
            AddReportFolders(rroot, "");
            rroot.Expand();
            _repTree.EndUpdate();
            _repTree.SelectedNode = FindNode(rroot, prevRep) ?? rroot;
            _loading = false;
            FillFunctions();
            FillReports();
            ShowTab(_pageReport.Visible ? 1 : 0);
        }

        private static TreeNode FindNode(TreeNode root, string text)
        {
            if (text == null)
                return null;
            if (root.Text == text)
                return root;
            foreach (TreeNode c in root.Nodes)
            {
                TreeNode f = FindNode(c, text);
                if (f != null)
                    return f;
            }
            return null;
        }

        private void AddReportFolders(TreeNode parent, string parentId)
        {
            foreach (ReportRow r in _reports.Where(x => x.IsFolder && string.Equals(x.ParentId ?? "", parentId, StringComparison.OrdinalIgnoreCase)))
            {
                TreeNode n = parent.Nodes.Add(r.Name);
                n.Tag = r;
                AddReportFolders(n, r.Id);
            }
        }

        private void ShowTab(int i)
        {
            _pageUse.Visible = i == 0;
            _pageReport.Visible = i == 1;
            StyleTab(_tabUse, i == 0);
            StyleTab(_tabReport, i == 1);
        }

        private static void StyleTab(FlatButton b, bool selected)
        {
            b.Tag = selected ? "primary" : null;
            Theme.ThemeTokens t = Theme.ThemeManager.Current;
            ThemeApplier.ApplyButton(b, t, selected ? "primary" : null);
        }

        // ------------------------------------------------------------------ quyền sử dụng

        private void FillFunctions()
        {
            if (_loading || _group == null)
                return;
            _loading = true;
            string group = _funcTree.SelectedNode == null ? null : _funcTree.SelectedNode.Tag as string;
            string q = (_funcFilter.Text ?? "").Trim().ToLowerInvariant();
            _funcGrid.Rows.Clear();
            foreach (FuncState f in _funcs.Where(x => (group == null || x.F.GroupName == group) &&
                                                      (q.Length == 0 || x.F.Name.ToLowerInvariant().Contains(q))))
            {
                int i = _funcGrid.Rows.Add();
                DataGridViewRow row = _funcGrid.Rows[i];
                row.Tag = f;
                row.Cells[ColName].Value = f.F.Name;
                if (f.F.ViewOnly)
                {
                    // Chỉ có Khóa và Xem: các ô Thêm/Sửa/Xóa/Tất cả để trống.
                    foreach (int c in new[] { ColAdd, ColEdit, ColRemove, ColAll })
                    {
                        // Ô trống nằm trong cột checkbox: phải là ô chữ kiểu chuỗi, nếu không vẽ sẽ báo lỗi định dạng.
                        var blank = new DataGridViewTextBoxCell { ValueType = typeof(string) };
                        row.Cells[c] = blank;
                        blank.Value = "";
                        blank.ReadOnly = true;
                    }
                }
                RefreshRow(row);
            }
            _loading = false;
        }

        private static void RefreshRow(DataGridViewRow row)
        {
            var f = (FuncState)row.Tag;
            row.Cells[ColLock].Value = RoleLogic.IsLocked(f.Mode);
            row.Cells[ColView].Value = RoleLogic.Has(f.Mode, RoleModes.View);
            if (!f.F.ViewOnly)
            {
                row.Cells[ColAdd].Value = RoleLogic.Has(f.Mode, RoleModes.Add);
                row.Cells[ColEdit].Value = RoleLogic.Has(f.Mode, RoleModes.Edit);
                row.Cells[ColRemove].Value = RoleLogic.Has(f.Mode, RoleModes.Remove);
                row.Cells[ColAll].Value = RoleLogic.IsAll(f.Mode, false);
            }
        }

        private void OnFuncClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_loading || _group == null || e.RowIndex < 0 || e.ColumnIndex < ColLock)
                return;
            DataGridViewRow row = _funcGrid.Rows[e.RowIndex];
            var f = (FuncState)row.Tag;
            if (row.Cells[e.ColumnIndex] is DataGridViewTextBoxCell)
                return;
            bool now = !Convert.ToBoolean(row.Cells[e.ColumnIndex].Value ?? false);
            int mode = f.Mode;
            switch (e.ColumnIndex)
            {
                case ColLock: mode = RoleLogic.SetLocked(mode, now); break;
                case ColView: mode = RoleLogic.SetBit(mode, RoleModes.View, now); break;
                case ColAdd: mode = RoleLogic.SetBit(mode, RoleModes.Add, now); break;
                case ColEdit: mode = RoleLogic.SetBit(mode, RoleModes.Edit, now); break;
                case ColRemove: mode = RoleLogic.SetBit(mode, RoleModes.Remove, now); break;
                case ColAll: mode = RoleLogic.SetAll(mode, now, f.F.ViewOnly); break;
            }
            ApplyModes(new Dictionary<FuncState, int> { { f, mode } });
        }

        private void BulkFunctions(bool allow)
        {
            if (_group == null)
                return;
            var changes = new Dictionary<FuncState, int>();
            foreach (DataGridViewRow row in _funcGrid.Rows)
            {
                var f = (FuncState)row.Tag;
                changes[f] = allow ? RoleLogic.SetAll(f.Mode, true, f.F.ViewOnly) : RoleModes.Locked;
            }
            ApplyModes(changes);
        }

        private void ApplyModes(Dictionary<FuncState, int> changes)
        {
            var toSave = new Dictionary<string, int>();
            foreach (KeyValuePair<FuncState, int> kv in changes)
                if (kv.Key.Mode != kv.Value)
                    toSave[kv.Key.F.Id] = kv.Value;
            if (toSave.Count == 0)
                return;
            try
            {
                _repo.SaveModes(_group.Id, toSave, AppSession.UserId);
            }
            catch (FbException ex)
            {
                Msg.Error(this, "Không ghi được phân quyền: " + ex.Message);
                SetGroup(_group);
                return;
            }
            foreach (KeyValuePair<FuncState, int> kv in changes)
                kv.Key.Mode = kv.Value;
            _loading = true;
            foreach (DataGridViewRow row in _funcGrid.Rows)
                RefreshRow(row);
            _loading = false;
            _funcGrid.Invalidate();
        }

        // ------------------------------------------------------------------ quyền xem báo cáo

        private IEnumerable<ReportRow> ReportsUnder(ReportRow folder)
        {
            if (folder == null)
                return _reports.Where(r => !r.IsFolder);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { folder.Id };
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (ReportRow r in _reports.Where(x => x.IsFolder && !ids.Contains(x.Id) && ids.Contains(x.ParentId ?? "")))
                {
                    ids.Add(r.Id);
                    grew = true;
                }
            }
            return _reports.Where(r => !r.IsFolder && ids.Contains(r.ParentId ?? ""));
        }

        private List<ReportRow> VisibleReports()
        {
            var folder = _repTree.SelectedNode == null ? null : _repTree.SelectedNode.Tag as ReportRow;
            string q = (_repFilter.Text ?? "").Trim().ToLowerInvariant();
            return ReportsUnder(folder).Where(r => q.Length == 0 || r.Name.ToLowerInvariant().Contains(q)).ToList();
        }

        private void FillReports()
        {
            if (_loading || _group == null)
                return;
            _loading = true;
            _repGrid.Rows.Clear();
            foreach (ReportRow r in VisibleReports())
            {
                int i = _repGrid.Rows.Add();
                DataGridViewRow row = _repGrid.Rows[i];
                row.Tag = r;
                row.Cells[0].Value = r.Name;
                bool ok = _allowed.Contains(r.Id);
                row.Cells[1].Value = !ok;
                row.Cells[2].Value = ok;
            }
            _loading = false;
        }

        private void OnReportClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_loading || _group == null || e.RowIndex < 0 || e.ColumnIndex < 1)
                return;
            var r = (ReportRow)_repGrid.Rows[e.RowIndex].Tag;
            bool allow = e.ColumnIndex == 2; // bấm "Được phép xem" = cho xem; bấm "Không được xem" = chặn
            ApplyReports(new[] { r }, allow);
        }

        private void BulkReports(bool allow)
        {
            if (_group == null)
                return;
            ApplyReports(VisibleReports(), allow);
        }

        private void ApplyReports(IEnumerable<ReportRow> reports, bool allow)
        {
            List<ReportRow> list = reports.ToList();
            try
            {
                _repo.SetReportsAllowed(_group.Id, list.Select(x => x.Id), allow, AppSession.UserId);
            }
            catch (FbException ex)
            {
                Msg.Error(this, "Không ghi được phân quyền: " + ex.Message);
                SetGroup(_group);
                return;
            }
            foreach (ReportRow r in list)
            {
                if (allow) _allowed.Add(r.Id); else _allowed.Remove(r.Id);
            }
            _loading = true;
            foreach (DataGridViewRow row in _repGrid.Rows)
            {
                var rr = (ReportRow)row.Tag;
                bool ok = _allowed.Contains(rr.Id);
                row.Cells[1].Value = !ok;
                row.Cells[2].Value = ok;
            }
            _loading = false;
            _repGrid.Invalidate();
        }

        /// <summary>Dùng cho --render-screen.</summary>
        public void ShowTabIndex(int i)
        {
            ShowTab(i);
        }
    }
}
