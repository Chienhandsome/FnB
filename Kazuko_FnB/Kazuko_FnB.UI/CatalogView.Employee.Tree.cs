using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần cây thư mục, phân cách, xóa mềm/xóa vĩnh viễn (Shift+Del) và Thùng rác của Danh mục nhân viên
    /// (DNHANVIEN, spec docs/specs/nhan-su-cay.md §1–§4) trong <see cref="CatalogView"/>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private const string ColEmpItemType = "_emp_type";

        private List<EmployeeRow> _employeeFolders = new List<EmployeeRow>();
        private readonly Panel _empTrashBar = new Panel { Dock = DockStyle.Bottom, Visible = false };
        private readonly FlatButton _btnEmpTrash = new FlatButton { Text = "Thùng rác", BorderWidth = 1.2f };

        // Hook kiểm thử UI (selftest) cho cây nhân viên và Thùng rác.
        internal List<EmployeeRow> EmployeeFoldersForTest { get { return _employeeFolders; } }
        internal FlatButton EmployeeTrashButtonForTest { get { return _btnEmpTrash; } }
        internal Panel EmployeeTrashBarForTest { get { return _empTrashBar; } }
        internal ThemedContextMenu EmployeeAddMenuForTest { get { return _employeeAddMenu; } }
        internal Func<EmployeeFolderEditDialog, DialogResult> EmployeeFolderDialogOpenerForTest { get; set; }
        internal Func<EmployeeTrashDialog, DialogResult> EmployeeTrashDialogOpenerForTest { get; set; }

        private void BuildEmployeeTreePanelUi()
        {
            float s = DeviceDpi / 96f;
            _empTrashBar.Height = (int)(44 * s);
            _btnEmpTrash.Click += delegate { OpenEmployeeTrash(); };
            _empTrashBar.Controls.Add(_btnEmpTrash);
            _empTrashBar.Resize += delegate { LayoutEmployeeTrashBar(); };
            _split.Panel1.Controls.Add(_empTrashBar);
            LayoutEmployeeTrashBar();
        }

        private void LayoutEmployeeTrashBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(6 * s);
            int w = Math.Max((int)(80 * s), _empTrashBar.ClientSize.Width - 2 * pad);
            int h = Math.Max((int)(28 * s), _empTrashBar.ClientSize.Height - 2 * pad);
            _btnEmpTrash.SetBounds(pad, pad, w, h);
        }

        private void HideEmployeeTreeBars()
        {
            _empTrashBar.Visible = false;
        }

        private void ApplyEmployeeTheme(ThemeTokens t, Color page)
        {
            _empTrashBar.BackColor = page;
            _btnEmpTrash.SurfaceColor = page;
            _btnEmpTrash.FillColor = t.Surface;
            _btnEmpTrash.TextColor = t.Text;
            _btnEmpTrash.BorderColor = Draw.StrongBorder(t);
            _btnEmpTrash.Invalidate();
            _grid.Invalidate();
        }

        private void BuildEmployeeTree()
        {
            string selectedId = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as string;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            _nodes.Clear();

            _split.Panel1Collapsed = false;
            _splitPlaced = false;
            PlaceSplit();

            var catalogNodes = new List<CatalogNode>(_employeeFolders.Count);
            foreach (EmployeeRow f in _employeeFolders)
            {
                var cn = new CatalogNode
                {
                    Id = f.Id,
                    Name = f.Name ?? "",
                    ParentId = string.IsNullOrEmpty(f.ParentId) ? null : f.ParentId,
                    SortOrder = f.SortOrder ?? ""
                };
                catalogNodes.Add(cn);
                _nodes[cn.Id] = cn;
            }

            var root = new TreeNode("Tất cả") { Tag = null };
            _tree.Nodes.Add(root);

            var byParent = new Dictionary<string, List<CatalogNode>>(StringComparer.Ordinal);
            var tops = new List<CatalogNode>();
            foreach (CatalogNode n in catalogNodes)
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
            root.ExpandAll();

            TreeNode pick = string.IsNullOrEmpty(selectedId) ? root : FindNode(root, selectedId) ?? root;
            _tree.SelectedNode = pick;
            _tree.EndUpdate();
        }

        internal string SelectedEmployeeFolderId()
        {
            string id = SelectedGroupId();
            return string.IsNullOrEmpty(id) || id == UnsetTag ? null : id;
        }

        internal bool SelectEmployeeFolderByIdForTest(string folderId)
        {
            if (_tree.Nodes.Count == 0)
                return false;
            if (string.IsNullOrEmpty(folderId))
            {
                _tree.SelectedNode = _tree.Nodes[0];
                return true;
            }
            TreeNode f = FindNode(_tree.Nodes[0], folderId);
            if (f == null)
                return false;
            f.EnsureVisible();
            _tree.SelectedNode = f;
            return true;
        }

        // ------------------------------------------------------------------ CRUD Thư mục & Phân cách

        internal void AddEmployeeFolder(string parentId)
        {
            if (!_employeeMode || !_canAdd)
                return;
            if (parentId == UnsetTag)
                parentId = null;
            using (var dlg = new EmployeeFolderEditDialog(ResolveEmployeeSession(), null, parentId, false))
            {
                OpenEmployeeFolderDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(dlg.SavedId);
                }
            }
        }

        internal void EditEmployeeFolder()
        {
            if (!_employeeMode || !_canEdit)
                return;
            string id = SelectedEmployeeFolderId();
            if (string.IsNullOrEmpty(id))
                return;
            EmployeeRow folder = _employeeService.LoadById(id);
            if (folder == null || !folder.IsFolder)
            {
                ShowEmployeeWarning(EmployeeText.FolderNotFound);
                Reload();
                return;
            }
            using (var dlg = new EmployeeFolderEditDialog(ResolveEmployeeSession(), folder, folder.ParentId, false))
            {
                OpenEmployeeFolderDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(id);
                }
            }
        }

        private void OpenEmployeeFolderFromTree()
        {
            if (!_employeeMode)
                return;
            string id = SelectedEmployeeFolderId();
            if (string.IsNullOrEmpty(id))
                return;
            if (_canEdit)
            {
                EditEmployeeFolder();
                return;
            }
            if (!_canViewEmployee)
                return;
            EmployeeRow folder = _employeeService.LoadById(id);
            if (folder == null || !folder.IsFolder)
                return;
            using (var dlg = new EmployeeFolderEditDialog(ResolveEmployeeSession(), folder, folder.ParentId, true))
                OpenEmployeeFolderDialog(dlg);
        }

        private void RenameSelectedEmployeeFolder()
        {
            if (!_employeeMode || !_canEdit)
                return;
            if (string.IsNullOrEmpty(SelectedEmployeeFolderId()))
                return;
            if (_tree.SelectedNode != null)
                _tree.SelectedNode.BeginEdit();
        }

        private void RenameEmployeeFolderLabel(NodeLabelEditEventArgs e)
        {
            if (e.Label == null || e.Node == null)
                return;
            string id = e.Node.Tag as string;
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
            {
                e.CancelEdit = true;
                return;
            }
            if (string.IsNullOrWhiteSpace(e.Label))
            {
                ShowEmployeeWarning(EmployeeText.FolderNameEmpty);
                e.CancelEdit = true;
                return;
            }
            EmployeeRow folder = _employeeService.LoadById(id);
            if (folder == null || !folder.IsFolder)
            {
                e.CancelEdit = true;
                return;
            }
            folder.Name = e.Label;
            EmployeeSaveResult res;
            try
            {
                res = _employeeService.SaveFolder(folder);
            }
            catch (DataAccessException ex)
            {
                ShowEmployeeWarning("Không ghi được dữ liệu: " + ex.Message);
                e.CancelEdit = true;
                return;
            }
            if (!res.Ok)
            {
                ShowEmployeeWarning(res.ErrorMessage);
                e.CancelEdit = true;
                return;
            }
            Reload();
            SelectTreeNode(id);
        }

        internal void AddEmployeeSeparator()
        {
            if (!_employeeMode || !_canAdd)
                return;
            string parentId = SelectedEmployeeFolderId();
            EmployeeSaveResult res;
            try
            {
                res = _employeeService.AddSeparator(parentId);
            }
            catch (DataAccessException ex)
            {
                ShowEmployeeWarning("Không thêm được phân cách: " + ex.Message);
                return;
            }
            if (!res.Ok)
            {
                ShowEmployeeWarning(res.ErrorMessage);
                return;
            }
            LastEmployeeMessageForTest = null;
            RefreshEmployeeAndSelect(res.SavedId);
        }

        internal void DeleteEmployeeFolder(bool permanent)
        {
            if (!_employeeMode || !_canRemove)
                return;
            string id = SelectedEmployeeFolderId();
            if (string.IsNullOrEmpty(id))
                return;
            EmployeeRow folder = _employeeService.LoadById(id);
            if (folder == null || !folder.IsFolder)
            {
                ShowEmployeeWarning(EmployeeText.FolderNotFound);
                Reload();
                return;
            }

            string prompt = permanent
                ? EmployeeText.HardDeleteConfirm(folder.Name)
                : EmployeeText.SoftDeleteConfirm(1);
            if (!SuppressEmployeePopupForTest && !Msg.Confirm(this, prompt))
                return;

            EmployeeDeleteResult res;
            try
            {
                res = permanent ? _employeeService.Delete(id) : _employeeService.SoftDelete(id);
            }
            catch (DataAccessException ex)
            {
                ShowEmployeeWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowEmployeeWarning(res.ErrorMessage);
                return;
            }
            LastEmployeeMessageForTest = null;
            Reload();
        }

        internal void DeleteEmployeePermanent()
        {
            if (!_employeeMode || !_canRemove)
                return;
            EmployeeRow row = CurrentEmployeeRow();
            if (row == null)
                return;
            string label = row.IsSeparator ? "dòng phân cách" : (row.Name ?? "");
            if (!SuppressEmployeePopupForTest && !Msg.Confirm(this, EmployeeText.HardDeleteConfirm(label)))
                return;

            EmployeeDeleteResult res;
            try
            {
                res = _employeeService.Delete(row.Id);
            }
            catch (DataAccessException ex)
            {
                ShowEmployeeWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowEmployeeWarning(res.ErrorMessage);
                return;
            }
            LastEmployeeMessageForTest = null;
            Reload();
        }

        private void DeleteEmployeeOrFolderPermanent()
        {
            if (!_employeeMode || !_canRemove)
                return;
            if (_tree.Focused && !string.IsNullOrEmpty(SelectedEmployeeFolderId()))
            {
                DeleteEmployeeFolder(true);
                return;
            }
            if (CurrentEmployeeId() != null)
            {
                DeleteEmployeePermanent();
                return;
            }
            if (!string.IsNullOrEmpty(SelectedEmployeeFolderId()))
                DeleteEmployeeFolder(true);
        }

        internal void OpenEmployeeTrash()
        {
            if (!_employeeMode || !_canRemove)
                return;
            using (var dlg = new EmployeeTrashDialog(ResolveEmployeeSession()))
            {
                OpenEmployeeTrashDialog(dlg);
                if (dlg.AnyChanged)
                    Reload();
            }
        }

        private DialogResult OpenEmployeeFolderDialog(EmployeeFolderEditDialog dlg)
        {
            return EmployeeFolderDialogOpenerForTest != null
                ? EmployeeFolderDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private DialogResult OpenEmployeeTrashDialog(EmployeeTrashDialog dlg)
        {
            return EmployeeTrashDialogOpenerForTest != null
                ? EmployeeTrashDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private bool IsEmployeeSeparatorRowIndex(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return false;
            var drv = _grid.Rows[rowIndex].DataBoundItem as System.Data.DataRowView;
            if (drv == null || drv.Row.Table == null || !drv.Row.Table.Columns.Contains(ColEmpItemType))
                return false;
            return Convert.ToInt32(drv[ColEmpItemType]) == EmployeeItemType.Separator;
        }
    }
}
