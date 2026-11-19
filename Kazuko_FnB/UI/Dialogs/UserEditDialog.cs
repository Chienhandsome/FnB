using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Tài khoản người dùng — thêm mới / sửa. Nhân viên → Họ và tên lấy theo nhân viên (khóa ô); không chọn nhân viên thì nhập tay.
    /// Nút Lưu chỉ bật khi đủ Họ và tên + Tài khoản + Nhóm và có thay đổi. Lưới cửa hàng tích sẵn khi thêm mới.
    /// Trước (F10) / Sau (F11) duyệt các tài khoản trong danh sách; Tạo mới = form trắng.
    /// </summary>
    internal sealed class UserEditDialog : ThemedDialog
    {
        private readonly UserAdminRepository _repo;
        private readonly List<UserRow> _nav;
        private int _navIndex;
        private UserRow _current;
        private string _snapshot = "";
        private bool _loading;

        private readonly ComboBox _emp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _user = new TextBox();
        private readonly TextBox _pass = new TextBox();
        private readonly TextBox _mail = new TextBox();
        private readonly ComboBox _group = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DataGridView _stores = new DataGridView();
        private readonly FlatButton _btnSave, _btnSaveNew, _btnSaveExit, _btnPrev, _btnNext, _btnNewRow;
        private readonly string _defaultGroupId;

        /// <summary>true nếu đã ghi ít nhất một tài khoản (để màn danh sách tải lại).</summary>
        public bool Changed { get; private set; }

        /// <summary>ID tài khoản được ghi gần nhất.</summary>
        public string LastSavedId { get; private set; }

        public UserEditDialog(UserSession session, UserRow existing, string defaultGroupId, IList<UserRow> navigation)
            : base("TÀI KHOẢN NGƯỜI DÙNG", "Tài khoản người dùng", 580, 640)
        {
            _repo = new UserAdminRepository(session.DatabasePath);
            _defaultGroupId = defaultGroupId;
            _nav = navigation == null ? new List<UserRow>() : new List<UserRow>(navigation);
            float s = Dpi;

            // Chân: Lưu | Lưu & Mới | Lưu & thoát | Thoát (nút đầu tiên thêm nằm ngoài cùng bên phải).
            FlatButton exit = AddButton("Thoát", null, delegate { CloseDialog(); });
            _btnSaveExit = AddButton("Lưu & thoát", null, delegate { DoSave(true, false); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { DoSave(false, true); });
            _btnSave = AddButton("Lưu", "primary", delegate { DoSave(false, false); });
            CancelButton = exit;

            // Thanh duyệt phía trên.
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = (int)(40 * s), FlowDirection = FlowDirection.LeftToRight };
            _btnPrev = Tool("‹ Trước (F10)", s, delegate { Navigate(-1); });
            _btnNext = Tool("Sau (F11) ›", s, delegate { Navigate(+1); });
            _btnNewRow = Tool("+ Tạo mới", s, delegate { if (ConfirmDiscard()) LoadUser(null); });
            bar.Controls.Add(_btnPrev);
            bar.Controls.Add(_btnNext);
            bar.Controls.Add(_btnNewRow);

            int labelW = (int)(150 * s), x = labelW + (int)(8 * s), w = ClientSize.Width - x - (int)(30 * s);
            int y = (int)(52 * s), rowH = (int)(38 * s);
            Row("Nhân viên", _emp, x, ref y, rowH, w, labelW, s);
            Row("Họ và tên", _name, x, ref y, rowH, w, labelW, s);
            Row("Tài khoản", _user, x, ref y, rowH, w, labelW, s);
            Row("Mật khẩu", _pass, x, ref y, rowH, w, labelW, s);
            Row("Email", _mail, x, ref y, rowH, w, labelW, s);
            Row("Nhóm người dùng", _group, x, ref y, rowH, w, labelW, s);

            Body.Controls.Add(new Label
            {
                Text = "Cửa hàng truy cập",
                AutoSize = false,
                Left = 0,
                Top = y,
                Width = labelW,
                Height = rowH,
                TextAlign = ContentAlignment.MiddleLeft
            });
            _stores.SetBounds(x, y + (int)(4 * s), w, (int)(150 * s));
            _stores.AllowUserToAddRows = false;
            _stores.AllowUserToDeleteRows = false;
            _stores.RowHeadersVisible = false;
            _stores.AllowUserToResizeRows = false;
            _stores.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _stores.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _stores.ColumnHeadersHeight = (int)(30 * s);
            _stores.RowTemplate.Height = (int)(28 * s);
            _stores.Columns.Add(new DataGridViewTextBoxColumn { Name = "Store", HeaderText = "Cửa hàng", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _stores.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Access", HeaderText = "Truy cập", Width = (int)(100 * s) });
            _stores.CurrentCellDirtyStateChanged += delegate { if (_stores.IsCurrentCellDirty) _stores.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _stores.DataError += delegate (object o, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            _stores.CellValueChanged += delegate { if (!_loading) UpdateButtons(); };
            Body.Controls.Add(_stores);
            Body.Controls.Add(bar);

            // Danh sách nhân viên / nhóm.
            _emp.Items.Add(new Choice("", ""));
            foreach (KeyValuePair<string, string> p in _repo.LoadEmployees())
                _emp.Items.Add(new Choice(p.Key, p.Value));
            foreach (GroupRow g in new GroupRepository(session.DatabasePath).LoadAll())
                _group.Items.Add(new Choice(g.Id, g.Name));

            _emp.SelectedIndexChanged += delegate { OnEmployeeChanged(); };
            _name.TextChanged += delegate { UpdateButtons(); };
            _user.TextChanged += delegate { UpdateButtons(); };
            _pass.TextChanged += delegate { UpdateButtons(); };
            _mail.TextChanged += delegate { UpdateButtons(); };
            _group.SelectedIndexChanged += delegate { UpdateButtons(); };
            FormClosing += OnFormClosing;

            if (existing != null)
            {
                int i = _nav.FindIndex(u => u.Id == existing.Id);
                if (i < 0)
                {
                    _nav.Add(existing);
                    i = _nav.Count - 1;
                }
                _navIndex = i;
            }
            LoadUser(existing);
        }

        private static FlatButton Tool(string text, float s, EventHandler click)
        {
            var b = new FlatButton { Text = text, Width = (int)(120 * s), Height = (int)(32 * s), Radius = (int)(6 * s), Margin = new Padding(0, 0, (int)(6 * s), 0) };
            b.Click += click;
            return b;
        }

        private void Row(string label, Control input, int x, ref int y, int rowH, int w, int labelW, float s)
        {
            Body.Controls.Add(new Label { Text = label, AutoSize = false, Left = 0, Top = y, Width = labelW, Height = rowH, TextAlign = ContentAlignment.MiddleLeft });
            input.SetBounds(x, y + (int)(4 * s), w, (int)(28 * s));
            Body.Controls.Add(input);
            y += rowH;
        }

        // ------------------------------------------------------------------ nạp / duyệt

        private void LoadUser(UserRow u)
        {
            _loading = true;
            _current = u;
            SetTitle(u == null ? "TÀI KHOẢN NGƯỜI DÙNG - THÊM MỚI" : "TÀI KHOẢN NGƯỜI DÙNG - SỬA");
            SelectKey(_emp, u == null ? "" : u.EmployeeId);
            _name.Text = u == null ? "" : u.Name;
            _user.Text = u == null ? "" : u.UserName;
            _pass.Text = u == null ? "" : u.Password;
            _mail.Text = u == null ? "" : u.Email;
            SelectKey(_group, u == null ? _defaultGroupId : u.GroupId);
            _stores.Rows.Clear();
            foreach (StoreAccess st in _repo.LoadStores(u == null ? null : u.Id))
            {
                int r = _stores.Rows.Add(st.StoreName, st.Access);
                _stores.Rows[r].Tag = st.StoreId;
            }
            _name.Enabled = SelectedKey(_emp).Length == 0;
            _loading = false;
            _snapshot = Snapshot();
            UpdateButtons();
            _user.Focus();
        }

        private static string SelectedKey(ComboBox c)
        {
            var ch = c.SelectedItem as Choice;
            return ch == null ? "" : ch.Key;
        }

        private static void SelectKey(ComboBox c, string key)
        {
            for (int i = 0; i < c.Items.Count; i++)
                if (string.Equals(((Choice)c.Items[i]).Key, key ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    c.SelectedIndex = i;
                    return;
                }
            // Ô nhân viên có dòng trống ở đầu (chọn dòng đó); ô nhóm để trống cho tới khi người dùng chọn.
            c.SelectedIndex = c.Items.Count > 0 && ((Choice)c.Items[0]).Key.Length == 0 ? 0 : -1;
        }

        private void OnEmployeeChanged()
        {
            if (_loading)
                return;
            var ch = _emp.SelectedItem as Choice;
            if (ch != null && ch.Key.Length > 0)
            {
                _name.Text = ch.Text;
                _name.Enabled = false;
            }
            else
            {
                _name.Enabled = true;
            }
            UpdateButtons();
        }

        private void Navigate(int delta)
        {
            int i = _navIndex + delta;
            if (i < 0 || i >= _nav.Count || !ConfirmDiscard())
                return;
            _navIndex = i;
            LoadUser(_repo.Get(_nav[i].Id) ?? _nav[i]);
        }

        // ------------------------------------------------------------------ trạng thái nút

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(SelectedKey(_emp)).Append('|').Append(_name.Text).Append('|').Append(_user.Text).Append('|')
              .Append(_pass.Text).Append('|').Append(_mail.Text).Append('|').Append(SelectedKey(_group)).Append('|');
            foreach (DataGridViewRow r in _stores.Rows)
                sb.Append(Convert.ToBoolean(r.Cells[1].Value ?? false) ? '1' : '0');
            return sb.ToString();
        }

        private bool Dirty
        {
            get { return Snapshot() != _snapshot; }
        }

        private bool Valid
        {
            get { return _name.Text.Trim().Length > 0 && _user.Text.Trim().Length > 0 && SelectedKey(_group).Length > 0; }
        }

        private void UpdateButtons()
        {
            if (_loading)
                return;
            bool ok = Valid && Dirty;
            _btnSave.Enabled = ok;
            _btnSaveNew.Enabled = ok;
            _btnSaveExit.Enabled = ok;
            _btnPrev.Enabled = _navIndex > 0 && _nav.Count > 0 && _current != null;
            _btnNext.Enabled = _current != null && _navIndex < _nav.Count - 1;
        }

        private bool ConfirmDiscard()
        {
            return !Dirty || Msg.Confirm(this, "Dữ liệu của bạn đã thay đổi, bạn có muốn hủy bỏ các thay đổi để thực hiện tiếp không?");
        }

        // ------------------------------------------------------------------ lưu

        private void DoSave(bool close, bool newAfter)
        {
            if (!Valid)
                return;
            string excludeId = _current == null ? null : _current.Id;
            try
            {
                if (_repo.IsTaken("USERNAME", _user.Text, excludeId))
                {
                    Msg.Warning(this, "Tài khoản '" + _user.Text.Trim() + "' đã tồn tại.");
                    return;
                }
                if (_repo.IsTaken("NAME", _name.Text, excludeId))
                {
                    Msg.Warning(this, "Họ và tên '" + _name.Text.Trim() + "' đã tồn tại.");
                    return;
                }

                var storeIds = new List<string>();
                foreach (DataGridViewRow r in _stores.Rows)
                    if (Convert.ToBoolean(r.Cells[1].Value ?? false))
                        storeIds.Add((string)r.Tag);
                if (storeIds.Count == 0 &&
                    !Msg.Confirm(this, "Bạn chưa phân quyền cho người dùng vào cửa hàng nào, bạn có muốn tiếp tục không?"))
                    return;

                var row = new UserRow
                {
                    Id = excludeId,
                    Name = _name.Text,
                    UserName = _user.Text,
                    Password = _pass.Text,
                    Email = _mail.Text,
                    GroupId = SelectedKey(_group),
                    EmployeeId = SelectedKey(_emp)
                };
                string id = _repo.Save(row, storeIds, AppSession.UserId);
                LastSavedId = id;
                Changed = true;
                UserRow saved = _repo.Get(id);
                int idx = _nav.FindIndex(u => u.Id == id);
                if (idx >= 0)
                    _nav[idx] = saved;
                else
                    _nav.Add(saved);
                _navIndex = _nav.FindIndex(u => u.Id == id);

                if (close)
                {
                    _snapshot = Snapshot();
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else if (newAfter)
                {
                    LoadUser(null);
                }
                else
                {
                    LoadUser(saved);
                }
            }
            catch (FbException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
            }
        }

        private void CloseDialog()
        {
            DialogResult = Changed ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && DialogResult != DialogResult.OK && Dirty &&
                !Msg.Confirm(this, "Dữ liệu của bạn đã thay đổi, bạn có muốn hủy bỏ các thay đổi để thực hiện tiếp không?"))
                e.Cancel = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F10) { Navigate(-1); return true; }
            if (keyData == Keys.F11) { Navigate(+1); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
