using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần CRUD nhóm voucher (DNHOMVOUCHER) ngay trên cây nhóm của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void AddGiftVoucherGroup(string parentId)
        {
            if (!_giftVoucherMode || !_canAdd)
                return;
            if (parentId == UnsetTag)
                parentId = null;
            using (var dlg = new GiftVoucherGroupEditDialog(ResolveGiftVoucherSession(), null, parentId, false))
            {
                OpenGiftVoucherGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(dlg.SavedId);
                }
            }
        }

        private void EditGiftVoucherGroup()
        {
            if (!_giftVoucherMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            GiftVoucherGroupRow group = _giftVoucherGroupService.LoadById(id);
            if (group == null)
            {
                ShowGiftVoucherWarning(GiftVoucherText.GroupNotFound);
                Reload();
                return;
            }
            using (var dlg = new GiftVoucherGroupEditDialog(ResolveGiftVoucherSession(), group, null, false))
            {
                OpenGiftVoucherGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(id);
                }
            }
        }

        /// <summary>Nhấp đôi nút nhóm voucher trên cây: có quyền Sửa thì mở sửa; không có quyền Sửa thì mở chỉ đọc.</summary>
        private void OpenGiftVoucherGroupFromTree()
        {
            if (!_giftVoucherMode)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_canEdit)
            {
                EditGiftVoucherGroup();
                return;
            }
            if (!_canViewGiftVoucher)
                return;
            GiftVoucherGroupRow group = _giftVoucherGroupService.LoadById(id);
            if (group == null)
                return;
            using (var dlg = new GiftVoucherGroupEditDialog(ResolveGiftVoucherSession(), group, null, true))
                OpenGiftVoucherGroupDialog(dlg);
        }

        private DialogResult OpenGiftVoucherGroupDialog(GiftVoucherGroupEditDialog dlg)
        {
            return GiftVoucherGroupDialogOpenerForTest != null
                ? GiftVoucherGroupDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        internal void DeleteGiftVoucherGroup()
        {
            if (!_giftVoucherMode || !_canRemove)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            GiftVoucherGroupRow group = _giftVoucherGroupService.LoadById(id);
            if (group == null)
            {
                ShowGiftVoucherWarning(GiftVoucherText.GroupNotFound);
                Reload();
                return;
            }
            if (!SuppressGiftVoucherPopupForTest && !Msg.Confirm(this, GiftVoucherText.DeleteGroupConfirm(group.Name)))
                return;

            GiftVoucherGroupDeleteResult res;
            try
            {
                res = _giftVoucherGroupService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowGiftVoucherError("Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!res.Ok)
            {
                ShowGiftVoucherWarning(res.ErrorMessage);
                return;
            }
            LastGiftVoucherMessageForTest = null;
            Reload();
        }

        private void RenameSelectedGiftVoucherGroup()
        {
            if (!_giftVoucherMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_tree.SelectedNode != null)
                _tree.SelectedNode.BeginEdit();
        }

        private void RenameGiftVoucherGroupLabel(NodeLabelEditEventArgs e)
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
                ShowGiftVoucherWarning(GiftVoucherText.GroupNameEmpty);
                e.CancelEdit = true;
                return;
            }
            GiftVoucherGroupRow group = _giftVoucherGroupService.LoadById(id);
            if (group == null)
            {
                e.CancelEdit = true;
                return;
            }
            group.Name = e.Label;
            GiftVoucherGroupSaveResult res;
            try
            {
                res = _giftVoucherGroupService.Save(group);
            }
            catch (DataAccessException ex)
            {
                ShowGiftVoucherError("Không ghi được dữ liệu: " + ex.Message);
                e.CancelEdit = true;
                return;
            }
            if (!res.Ok)
            {
                ShowGiftVoucherWarning(res.ErrorMessage);
                e.CancelEdit = true;
                return;
            }
            Reload();
            SelectTreeNode(id);
        }

        private void ShowGiftVoucherWarning(string message)
        {
            LastGiftVoucherMessageForTest = message;
            if (!SuppressGiftVoucherPopupForTest)
                Msg.Warning(this, message);
        }

        private void ShowGiftVoucherError(string message)
        {
            LastGiftVoucherMessageForTest = message;
            if (!SuppressGiftVoucherPopupForTest)
                Msg.Error(this, message);
        }
    }
}
