using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Chế độ Danh mục mật khẩu wifi (DMATKHAUWIFI / DNHOMWIFI, spec docs/specs/quan-tri-nho.md §3) trong <see cref="CatalogView"/>:
    /// cây nhóm wifi bên trái (Tất cả + Chưa thiết lập + cây cha/con), lưới 3 cột bên phải (Mật khẩu wifi, Nhóm wifi, Ghi chú),
    /// hiển thị mật khẩu rõ, tìm không dấu (F3), nút CRUD theo quyền SFUNCTION, nhấp đôi mở hộp thoại chỉ đọc khi thiếu quyền Sửa,
    /// xuất Excel qua <c>GridExcelExporter</c>.
    /// Phần CRUD nhóm ở <c>CatalogView.WifiPassword.Groups.cs</c>, menu ngữ cảnh ở <c>CatalogView.WifiPassword.Menus.cs</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _wifiPasswordMode;
        private bool _canViewWifiPassword;
        private WifiPasswordService _wifiPasswordService;
        private WifiGroupService _wifiGroupService;
        private List<WifiPasswordRow> _wifiPasswordRows = new List<WifiPasswordRow>();
        private readonly ThemedContextMenu _wifiGridMenu = new ThemedContextMenu();
        private readonly ThemedContextMenu _wifiTreeMenu = new ThemedContextMenu();

        // Hook kiểm thử UI (selftest).
        internal bool IsWifiPasswordModeForTest { get { return _wifiPasswordMode; } }
        internal List<WifiPasswordRow> WifiPasswordRowsForTest { get { return _wifiPasswordRows; } }
        internal bool SuppressWifiPopupForTest { get; set; }
        internal string LastWifiMessageForTest { get; private set; }
        internal Func<WifiPasswordEditDialog, DialogResult> WifiDialogOpenerForTest { get; set; }
        internal Func<WifiGroupEditDialog, DialogResult> WifiGroupDialogOpenerForTest { get; set; }
        internal ThemedContextMenu WifiTreeMenuForTest { get { return _wifiTreeMenu; } }

        private void BuildWifiPasswordUi()
        {
            BuildWifiGridMenu();
            BuildWifiTreeMenu();
        }

        private UserSession ResolveWifiSession()
        {
            UserSession current = AppSession.Current;
            if (current != null && !string.IsNullOrEmpty(current.DatabasePath))
                return current;
            return new UserSession
            {
                UserId = current == null ? "" : current.UserId,
                UserName = current == null ? "" : current.UserName,
                DisplayName = current == null ? "" : current.DisplayName,
                IsAdmin = current != null && current.IsAdmin,
                DatabasePath = _databasePath,
                Permissions = current == null ? new FunctionPermissions(false) : current.Permissions
            };
        }

        private void LoadWifiPasswordData(CatalogData data)
        {
            UserSession sess = ResolveWifiSession();
            _wifiPasswordService = new WifiPasswordService(sess);
            _wifiGroupService = new WifiGroupService(sess);
            _canViewWifiPassword = _wifiPasswordService.CanView;
            _canAdd = _wifiPasswordService.CanAdd;
            _canEdit = _wifiPasswordService.CanEdit;
            _canRemove = _wifiPasswordService.CanRemove;

            _wifiPasswordRows = _wifiPasswordService.LoadAll();
            _total = _wifiPasswordRows.Count;

            if (_def != null)
            {
                _def.CategoryColumn = "DNHOMWIFIID";
                _def.CategoryTable = WifiPasswordText.GroupTableName;
            }
            var treeData = new CatalogData();
            foreach (WifiGroupRow g in _wifiGroupService.LoadAll())
            {
                treeData.Nodes.Add(new CatalogNode
                {
                    Id = g.Id,
                    Name = g.Name ?? "",
                    ParentId = string.IsNullOrEmpty(g.ParentId) ? null : g.ParentId,
                    SortOrder = g.SortOrder ?? ""
                });
            }
            BuildTree(treeData);
            BuildWifiPasswordColumns();
            BuildWifiPasswordTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyWifiPasswordBarVisibility();
            UpdateWifiPasswordButtons();
            UpdateWifiPasswordAuditPanel();
        }

        private void ApplyWifiPasswordBarVisibility()
        {
            if (!_wifiPasswordMode)
                return;
            _btnAdd.Visible = _canAdd;
            _btnEdit.Visible = _canEdit;
            _btnRemove.Visible = _canRemove;
            _refresh.Visible = true;
            _btnExcelExport.Visible = true;
            _btnExcelAdd.Visible = false;
            _btnExcelImport.Visible = false;
            _btnPrint.Visible = false;
            _btnPrintBarcode.Visible = false;
            _btnQuickAdd.Visible = false;
            _btnTotal.Visible = false;

            float s = DeviceDpi / 96f;
            int gx = (int)(6 * s);
            _groupBar.Visible = _canAdd || _canEdit;
            _groupAdd.Visible = _canAdd;
            if (_canAdd)
            {
                _groupAdd.SetBounds(gx, (int)(4 * s), (int)(28 * s), (int)(28 * s));
                gx += (int)(34 * s);
            }
            _groupEdit.Visible = _canEdit;
            if (_canEdit)
                _groupEdit.SetBounds(gx, (int)(4 * s), (int)(28 * s), (int)(28 * s));

            _bottom.Visible = true;
            _paneSplitter.Visible = true;
            LayoutBar();
        }

        /// <summary>3 cột lưới theo spec §3: Mật khẩu wifi, Nhóm wifi, Ghi chú.</summary>
        private void BuildWifiPasswordColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddWifiColumn("wp_name", "Mật khẩu wifi", 240);
            AddWifiColumn("wp_group", "Nhóm wifi", 220);
            AddWifiColumn("wp_note", "Ghi chú", 220);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddWifiColumn(string name, string header, int width96)
        {
            float s = DeviceDpi / 96f;
            int headerNeed = TextRenderer.MeasureText(header, _grid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(30 * s);
            int minW = Math.Max((int)(60 * s), headerNeed);
            var gc = new DataGridViewTextBoxColumn
            {
                Name = name,
                DataPropertyName = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.Automatic,
                ReadOnly = true,
                MinimumWidth = minW,
                Width = Math.Max((int)(width96 * s), minW)
            };
            _grid.Columns.Add(gc);
        }

        private void BuildWifiPasswordTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("wp_name", typeof(string));
            dt.Columns.Add("wp_group", typeof(string));
            dt.Columns.Add("wp_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (WifiPasswordRow r in _wifiPasswordRows)
            {
                sb.Length = 0;
                sb.Append(r.Name).Append(' ');
                sb.Append(r.GroupName).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Name ?? "",
                    r.GroupName ?? "",
                    r.Note ?? "",
                    r.Id,
                    CatalogRepository.Fold(sb.ToString()),
                    CatalogRepository.CategoryPath(_nodes, r.GroupId));
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private string CurrentWifiPasswordId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private WifiPasswordRow CurrentWifiPasswordRow()
        {
            string id = CurrentWifiPasswordId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _wifiPasswordRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectWifiPasswordRowByIdForTest(string id)
        {
            if (_view == null || string.IsNullOrEmpty(id))
                return false;
            for (int i = 0; i < _view.Count; i++)
            {
                if (string.Equals(Convert.ToString(_view[i][ColId]), id, StringComparison.Ordinal))
                {
                    _grid.ClearSelection();
                    _grid.CurrentCell = _grid.Rows[i].Cells[0];
                    _grid.Rows[i].Selected = true;
                    UpdateWifiPasswordButtons();
                    UpdateWifiPasswordAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateWifiPasswordButtons()
        {
            if (!_wifiPasswordMode)
                return;
            bool hasRow = CurrentWifiPasswordId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;

            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            _groupAdd.Enabled = _canAdd;
            _groupEdit.Enabled = _canEdit && realGroup;
            _tree.LabelEdit = _canEdit;
        }

        private void UpdateWifiPasswordAuditPanel()
        {
            if (!_wifiPasswordMode)
                return;
            WifiPasswordRow row = CurrentWifiPasswordRow();
            if (row == null)
            {
                _lblCreated.Text = "";
                _lblCreatedBy.Text = "";
                _lblModified.Text = "";
                _lblModifiedBy.Text = "";
                return;
            }
            _lblCreated.Text = FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row.CreatedByName ?? "";
            _lblModified.Text = FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row.ModifiedByName ?? "";
        }

        // ------------------------------------------------------------------ CRUD Mật khẩu wifi

        private void AddWifiPassword()
        {
            if (!_wifiPasswordMode || !_canAdd)
                return;
            string groupId = SelectedGroupId();
            if (groupId == UnsetTag)
                groupId = null;
            using (var dlg = new WifiPasswordEditDialog(ResolveWifiSession(), null, groupId, false))
            {
                OpenWifiPasswordDialog(dlg);
                if (dlg.AnySaved)
                    RefreshWifiPasswordAndSelect(dlg.SavedId);
            }
        }

        private void EditWifiPassword()
        {
            if (!_wifiPasswordMode || !_canEdit)
                return;
            string id = CurrentWifiPasswordId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new WifiPasswordEditDialog(ResolveWifiSession(), id, null, false))
            {
                OpenWifiPasswordDialog(dlg);
                if (dlg.AnySaved)
                    RefreshWifiPasswordAndSelect(dlg.SavedId);
            }
        }

        private void OpenWifiPasswordFromGrid()
        {
            if (!_wifiPasswordMode)
                return;
            if (_canEdit)
            {
                EditWifiPassword();
                return;
            }
            if (!_canViewWifiPassword)
                return;
            string id = CurrentWifiPasswordId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new WifiPasswordEditDialog(ResolveWifiSession(), id, null, true))
                OpenWifiPasswordDialog(dlg);
        }

        private DialogResult OpenWifiPasswordDialog(WifiPasswordEditDialog dlg)
        {
            return WifiDialogOpenerForTest != null
                ? WifiDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void DeleteWifiPassword()
        {
            if (!_wifiPasswordMode || !_canRemove)
                return;
            string id = CurrentWifiPasswordId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressWifiPopupForTest && !Msg.Confirm(this, WifiPasswordText.DeleteConfirm))
                return;

            WifiPasswordDeleteResult res;
            try
            {
                res = _wifiPasswordService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowWifiError("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowWifiWarning(res.ErrorMessage);
                return;
            }
            LastWifiMessageForTest = null;
            Reload();
        }

        private void RefreshWifiPasswordAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectWifiPasswordRowByIdForTest(id);
        }
    }
}
