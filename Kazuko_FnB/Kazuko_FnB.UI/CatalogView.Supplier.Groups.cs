using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần CRUD nhóm nhà cung cấp (DNHOMNHACUNGCAP) ngay trên cây nhóm của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void AddSupplierGroup(string parentId)
        {
            if (!_supplierMode || !_canAdd)
                return;
            if (parentId == UnsetTag)
                parentId = null;
            using (var dlg = new SupplierGroupEditDialog(ResolveSupplierSession(), null, parentId, false))
            {
                OpenSupplierGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(dlg.SavedId);
                }
            }
        }

        private void EditSupplierGroup()
        {
            if (!_supplierMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            SupplierGroupRow group = _supplierGroupService.LoadById(id);
            if (group == null)
            {
                ShowSupplierWarning(SupplierText.GroupNotFound);
                Reload();
                return;
            }
            using (var dlg = new SupplierGroupEditDialog(ResolveSupplierSession(), group, null, false))
            {
                OpenSupplierGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(id);
                }
            }
        }

        /// <summary>Nhấp đôi nút nhóm nhà cung cấp trên cây: có quyền Sửa thì mở sửa; không có quyền Sửa thì mở chỉ đọc.</summary>
        private void OpenSupplierGroupFromTree()
        {
            if (!_supplierMode)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_canEdit)
            {
                EditSupplierGroup();
                return;
            }
            if (!_canViewSupplier)
                return;
            SupplierGroupRow group = _supplierGroupService.LoadById(id);
            if (group == null)
                return;
            using (var dlg = new SupplierGroupEditDialog(ResolveSupplierSession(), group, null, true))
                OpenSupplierGroupDialog(dlg);
        }

        private DialogResult OpenSupplierGroupDialog(SupplierGroupEditDialog dlg)
        {
            return SupplierGroupDialogOpenerForTest != null
                ? SupplierGroupDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        internal void DeleteSupplierGroup()
        {
            if (!_supplierMode || !_canRemove)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            SupplierGroupRow group = _supplierGroupService.LoadById(id);
            if (group == null)
            {
                ShowSupplierWarning(SupplierText.GroupNotFound);
                Reload();
                return;
            }
            if (!SuppressSupplierPopupForTest && !Msg.Confirm(this, SupplierText.DeleteGroupConfirm(group.Name)))
                return;

            SupplierGroupDeleteResult res;
            try
            {
                res = _supplierGroupService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowSupplierError("Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!res.Ok)
            {
                ShowSupplierWarning(res.ErrorMessage);
                return;
            }
            LastSupplierMessageForTest = null;
            Reload();
        }

        private void RenameSelectedSupplierGroup()
        {
            if (!_supplierMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_tree.SelectedNode != null)
                _tree.SelectedNode.BeginEdit();
        }

        private void RenameSupplierGroupLabel(NodeLabelEditEventArgs e)
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
                ShowSupplierWarning(SupplierText.GroupNameEmpty);
                e.CancelEdit = true;
                return;
            }
            SupplierGroupRow group = _supplierGroupService.LoadById(id);
            if (group == null)
            {
                e.CancelEdit = true;
                return;
            }
            group.Name = e.Label;
            SupplierGroupSaveResult res;
            try
            {
                res = _supplierGroupService.Save(group);
            }
            catch (DataAccessException ex)
            {
                ShowSupplierError("Không ghi được dữ liệu: " + ex.Message);
                e.CancelEdit = true;
                return;
            }
            if (!res.Ok)
            {
                ShowSupplierWarning(res.ErrorMessage);
                e.CancelEdit = true;
                return;
            }
            Reload();
            SelectTreeNode(id);
        }

        private void ShowSupplierWarning(string message)
        {
            LastSupplierMessageForTest = message;
            if (!SuppressSupplierPopupForTest)
                Msg.Warning(this, message);
        }

        private void ShowSupplierError(string message)
        {
            LastSupplierMessageForTest = message;
            if (!SuppressSupplierPopupForTest)
                Msg.Error(this, message);
        }
    }
}
