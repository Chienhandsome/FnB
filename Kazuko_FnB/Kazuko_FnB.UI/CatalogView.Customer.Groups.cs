using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần CRUD nhóm khách hàng (DNHOMKHACHHANG) ngay trên cây nhóm của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void AddCustomerGroup(string parentId)
        {
            if (!_customerMode || !_canAdd)
                return;
            if (parentId == UnsetTag)
                parentId = null;
            using (var dlg = new CustomerGroupEditDialog(AppSession.Current, null, parentId, false))
            {
                OpenCustomerGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(dlg.SavedId);
                }
            }
        }

        private void EditCustomerGroup()
        {
            if (!_customerMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            CustomerGroupRow group = _customerGroupRepo.Load(id);
            if (group == null)
            {
                ShowCustomerWarning("Nhóm khách hàng không còn tồn tại.");
                Reload();
                return;
            }
            using (var dlg = new CustomerGroupEditDialog(AppSession.Current, group, null, false))
            {
                OpenCustomerGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(id);
                }
            }
        }

        /// <summary>Nhấp đôi nút nhóm trên cây: có quyền Sửa thì mở sửa; chỉ có quyền Xem thì mở chỉ đọc (§9.0.2 Q10).</summary>
        private void OpenCustomerGroupFromTree()
        {
            if (!_customerMode)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_canEdit)
            {
                EditCustomerGroup();
                return;
            }
            if (!_canViewCustomer)
                return;
            CustomerGroupRow group = _customerGroupRepo.Load(id);
            if (group == null)
                return;
            using (var dlg = new CustomerGroupEditDialog(AppSession.Current, group, null, true))
                OpenCustomerGroupDialog(dlg);
        }

        private DialogResult OpenCustomerGroupDialog(CustomerGroupEditDialog dlg)
        {
            if (CustomerGroupDialogOpenerForTest != null)
                return CustomerGroupDialogOpenerForTest(dlg);
            return dlg.ShowDialog(this);
        }

        private void DeleteCustomerGroup()
        {
            if (!_customerMode || !_canRemove)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            CustomerGroupRow group = _customerGroupRepo.Load(id);
            if (group == null)
            {
                ShowCustomerWarning("Nhóm khách hàng không còn tồn tại.");
                Reload();
                return;
            }
            if (!SuppressCustomerPopupForTest && !Msg.Confirm(this, CustomerText.DeleteGroupConfirm(group.Name)))
                return;
            CustomerGroupDeleteResult res;
            try
            {
                res = _customerGroupRepo.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowCustomerError("Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!res.Ok)
            {
                ShowCustomerWarning(res.ErrorMessage);
                return;
            }
            LastCustomerMessageForTest = null;
            Reload();
        }

        private void RenameSelectedCustomerGroup()
        {
            if (!_customerMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_tree.SelectedNode != null)
                _tree.SelectedNode.BeginEdit();
        }

        private void RenameCustomerGroupLabel(NodeLabelEditEventArgs e)
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
                ShowCustomerWarning(CustomerText.GroupNameEmpty);
                e.CancelEdit = true;
                return;
            }
            CustomerGroupRow group = _customerGroupRepo.Load(id);
            if (group == null)
            {
                e.CancelEdit = true;
                return;
            }
            group.Name = e.Label;
            CustomerGroupSaveResult res;
            try
            {
                res = _customerGroupRepo.Save(group, AppSession.UserId ?? "admin");
            }
            catch (DataAccessException ex)
            {
                ShowCustomerError("Không ghi được dữ liệu: " + ex.Message);
                e.CancelEdit = true;
                return;
            }
            if (!res.Ok)
            {
                ShowCustomerWarning(res.ErrorMessage);
                e.CancelEdit = true;
                return;
            }
            Reload();
            SelectTreeNode(id);
        }

        private void TreeNodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (!_customerMode || e.Node == null)
                return;
            string id = e.Node.Tag as string;
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            _tree.SelectedNode = e.Node;
            OpenCustomerGroupFromTree();
        }

        private void ShowCustomerWarning(string message)
        {
            LastCustomerMessageForTest = message;
            if (!SuppressCustomerPopupForTest)
                Msg.Warning(this, message);
        }

        private void ShowCustomerError(string message)
        {
            LastCustomerMessageForTest = message;
            if (!SuppressCustomerPopupForTest)
                Msg.Error(this, message);
        }
    }
}
