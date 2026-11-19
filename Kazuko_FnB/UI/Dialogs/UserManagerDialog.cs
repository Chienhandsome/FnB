using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Quản trị ▸ Người dùng và phân quyền. Trái: cây nhóm (Tất cả + các nhóm) và danh sách tài khoản của nhóm.
    /// Phải: vùng phân quyền của nhóm đang chọn (chưa chọn nhóm thì hiện lời mời chọn nhóm).
    /// Tài khoản hệ thống Admin không hiện trong danh sách.
    /// </summary>
    internal sealed class UserManagerDialog : ThemedDialog, IScreenPreview
    {
        private readonly UserSession _session;
        private readonly GroupRepository _groups;
        private readonly UserAdminRepository _users;
        private readonly ImageRepository _images;
        private readonly TreeView _tree = new TreeView { HideSelection = false, ShowLines = true, ShowRootLines = true };
        private readonly ImageList _icons = new ImageList { ColorDepth = ColorDepth.Depth32Bit };
        private readonly DataGridView _grid = new DataGridView();
        private readonly FlatButton _grpEdit, _grpDel, _usrEdit, _usrDel;
        private readonly Panel _rightHost = new Panel { Dock = DockStyle.Fill, Tag = "surface" };
        private readonly Label _hint = new Label
        {
            Text = "MỜI BẠN CHỌN NHÓM NGƯỜI DÙNG BÊN PHÍA TRÁI ĐỂ PHÂN QUYỀN",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 12f),
            TextAlign = ContentAlignment.TopLeft
        };
        private readonly RolesPanel _roles;
        private List<UserRow> _shown = new List<UserRow>();

        public UserManagerDialog(UserSession session)
            : base("QUẢN LÝ NGƯỜI SỬ DỤNG", "Quản lý tài khoản người sử dụng và phân quyền", 1260, 700)
        {
            _session = session;
            _groups = new GroupRepository(session.DatabasePath);
            _users = new UserAdminRepository(session.DatabasePath);
            _images = new ImageRepository(session.DatabasePath);
            float s = Dpi;
            _icons.ImageSize = new Size((int)(20 * s), (int)(20 * s));
            _tree.ImageList = _icons;
            _tree.ItemHeight = (int)(26 * s);

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            CancelButton = exit;

            // Chú ý ở chân thân.
            var note = new Label
            {
                Text = "Chú ý: Tài khoản hệ thống Admin không được hiển thị trong danh sách",
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = (int)(26 * s),
                Tag = "muted",
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Cột trái.
            var left = new Panel { Dock = DockStyle.Left, Width = (int)(330 * s), Padding = new Padding(0, 0, (int)(10 * s), 0) };
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersHeight = (int)(30 * s);
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.RowTemplate.Height = (int)(28 * s);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "User", HeaderText = "Tài khoản", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.DataError += delegate (object o, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            _grid.SelectionChanged += delegate { UpdateButtons(); };
            _grid.CellDoubleClick += delegate (object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) EditUser(); };

            var grpBar = ToolBar(s);
            AddTool(grpBar, "Thêm nhóm", s, delegate { AddGroup(); });
            _grpEdit = AddTool(grpBar, "Sửa", s, delegate { EditGroup(); });
            _grpDel = AddTool(grpBar, "Xóa", s, delegate { DeleteGroup(); });
            var usrBar = ToolBar(s);
            AddTool(usrBar, "Thêm tài khoản", s, delegate { AddUser(); });
            _usrEdit = AddTool(usrBar, "Sửa", s, delegate { EditUser(); });
            _usrDel = AddTool(usrBar, "Xóa", s, delegate { DeleteUser(); });

            _tree.Dock = DockStyle.Top;
            _tree.Height = (int)(230 * s);
            _tree.AfterSelect += delegate { OnGroupSelected(); };
            _tree.NodeMouseDoubleClick += delegate { EditGroup(); };

            left.Controls.Add(_grid);
            left.Controls.Add(usrBar);
            left.Controls.Add(_tree);
            left.Controls.Add(grpBar);

            _roles = new RolesPanel(session.DatabasePath) { Visible = false };
            _rightHost.Controls.Add(_roles);
            _rightHost.Controls.Add(_hint);
            Body.Controls.Add(_rightHost);
            Body.Controls.Add(left);
            Body.Controls.Add(note);

            ReloadGroups(null);
        }

        private static FlowLayoutPanel ToolBar(float s)
        {
            return new FlowLayoutPanel { Dock = DockStyle.Top, Height = (int)(40 * s), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        }

        private static FlatButton AddTool(FlowLayoutPanel bar, string text, float s, EventHandler click)
        {
            var b = new FlatButton
            {
                Text = text,
                Width = (int)(Math.Max(64, text.Length * 9 + 24) * s),
                Height = (int)(32 * s),
                Radius = (int)(6 * s),
                Margin = new Padding(0, 0, (int)(6 * s), 0)
            };
            b.Click += click;
            bar.Controls.Add(b);
            return b;
        }

        // ------------------------------------------------------------------ nhóm

        /// <summary>Tên nhóm đang chọn (null nếu chọn "Tất cả" hoặc chưa chọn).</summary>
        private GroupRow SelectedGroup
        {
            get { return _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as GroupRow; }
        }

        private void ReloadGroups(string selectId)
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            foreach (Image im in _icons.Images)
                im.Dispose();
            _icons.Images.Clear();
            _icons.Images.Add(new Bitmap(_icons.ImageSize.Width, _icons.ImageSize.Height)); // 0 = trống

            TreeNode all = _tree.Nodes.Add("Tất cả");
            TreeNode pick = null;
            foreach (GroupRow g in _groups.LoadAll())
            {
                int icon = 0;
                byte[] data = _images.Get(g.ImageId);
                if (data != null)
                {
                    _icons.Images.Add(ImageUtil.Thumb(data, _icons.ImageSize.Width));
                    icon = _icons.Images.Count - 1;
                }
                var n = new TreeNode(g.Name) { Tag = g, ImageIndex = icon, SelectedImageIndex = icon };
                all.Nodes.Add(n);
                if (g.Id == selectId)
                    pick = n;
            }
            all.Expand();
            _tree.EndUpdate();
            _tree.SelectedNode = pick ?? all;
            _tree.Select();
        }

        private void OnGroupSelected()
        {
            ReloadUsers(null);
            UpdateButtons();
            GroupRow g = SelectedGroup;
            _hint.Visible = g == null;
            _roles.Visible = g != null;
            if (g != null)
                _roles.SetGroup(g);
        }

        private void AddGroup()
        {
            using (var dlg = new GroupEditDialog(_session, null))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ReloadGroups(dlg.SavedId);
        }

        private void EditGroup()
        {
            GroupRow g = SelectedGroup;
            if (g == null)
                return;
            using (var dlg = new GroupEditDialog(_session, g))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ReloadGroups(dlg.SavedId ?? g.Id);
        }

        private void DeleteGroup()
        {
            GroupRow g = SelectedGroup;
            if (g == null)
                return;
            try
            {
                int n = _groups.CountReferences(g.Id);
                if (n > 0)
                {
                    Msg.Warning(this, ReferenceChecker.BlockedMessage("nhóm người dùng", g.Name, n));
                    return;
                }
                if (!Msg.Confirm(this, "Bạn có chắc chắn muốn xóa nhóm người dùng '" + g.Name + "' không?"))
                    return;
                _groups.Delete(g.Id);
            }
            catch (FbException ex)
            {
                Msg.Error(this, "Không xóa được: " + ex.Message);
                return;
            }
            ReloadGroups(null);
        }

        // ------------------------------------------------------------------ tài khoản

        private UserRow SelectedUser
        {
            get
            {
                if (_grid.CurrentRow == null || _grid.CurrentRow.Index < 0 || _grid.CurrentRow.Index >= _shown.Count)
                    return null;
                return _shown[_grid.CurrentRow.Index];
            }
        }

        private void ReloadUsers(string selectId)
        {
            GroupRow g = SelectedGroup;
            _shown = _users.LoadUsers(g == null ? null : g.Id);
            _grid.Rows.Clear();
            int pick = -1;
            for (int i = 0; i < _shown.Count; i++)
            {
                _grid.Rows.Add(_shown[i].UserName);
                if (_shown[i].Id == selectId)
                    pick = i;
            }
            if (pick >= 0)
                _grid.CurrentCell = _grid.Rows[pick].Cells[0];
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (_grpEdit == null || _usrEdit == null)
                return;
            _grpEdit.Enabled = SelectedGroup != null;
            _grpDel.Enabled = SelectedGroup != null;
            _usrEdit.Enabled = SelectedUser != null;
            _usrDel.Enabled = SelectedUser != null;
        }

        private void AddUser()
        {
            GroupRow g = SelectedGroup;
            using (var dlg = new UserEditDialog(_session, null, g == null ? null : g.Id, _shown))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ReloadUsers(dlg.LastSavedId);
        }

        private void EditUser()
        {
            UserRow u = SelectedUser;
            if (u == null)
                return;
            UserRow full = _users.Get(u.Id) ?? u;
            using (var dlg = new UserEditDialog(_session, full, null, _shown))
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ReloadUsers(dlg.LastSavedId ?? u.Id);
        }

        private void DeleteUser()
        {
            UserRow u = SelectedUser;
            if (u == null)
                return;
            try
            {
                int n = _users.CountReferences(u.Id);
                if (n > 0)
                {
                    Msg.Warning(this, ReferenceChecker.BlockedMessage("tài khoản", u.UserName, n));
                    return;
                }
                if (!Msg.Confirm(this, "Bạn có chắc chắn muốn xóa tài khoản '" + u.UserName + "' không?"))
                    return;
                _users.Delete(u.Id);
            }
            catch (FbException ex)
            {
                Msg.Error(this, "Không xóa được: " + ex.Message);
                return;
            }
            ReloadUsers(null);
        }

        // ------------------------------------------------------------------ theme / render

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            _hint.ForeColor = Theme.ThemeManager.Current.Text;
        }

        public void Preview(string option)
        {
            if (option.StartsWith("tab=", StringComparison.Ordinal))
            {
                _roles.ShowTabIndex(int.Parse(option.Substring(4)));
            }
            else if (option.StartsWith("group=", StringComparison.Ordinal))
            {
                string name = option.Substring(6);
                foreach (TreeNode n in _tree.Nodes[0].Nodes)
                    if (n.Text == name)
                        _tree.SelectedNode = n;
            }
        }
    }
}
