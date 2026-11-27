using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần CRUD nhóm thẻ trả trước (DNHOMTHETRATRUOC) ngay trên cây nhóm của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void AddPrepaidCardGroup(string parentId)
        {
            if (!_prepaidCardMode || !_canAdd)
                return;
            if (parentId == UnsetTag)
                parentId = null;
            using (var dlg = new PrepaidCardGroupEditDialog(ResolvePrepaidCardSession(), null, parentId, false))
            {
                OpenPrepaidCardGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(dlg.SavedId);
                }
            }
        }

        private void EditPrepaidCardGroup()
        {
            if (!_prepaidCardMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            PrepaidCardGroupRow group = _prepaidCardGroupService.LoadById(id);
            if (group == null)
            {
                ShowPrepaidCardWarning(PrepaidCardText.GroupNotFound);
                Reload();
                return;
            }
            using (var dlg = new PrepaidCardGroupEditDialog(ResolvePrepaidCardSession(), group, null, false))
            {
                OpenPrepaidCardGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(id);
                }
            }
        }

        private void OpenPrepaidCardGroupFromTree()
        {
            if (!_prepaidCardMode)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_canEdit)
            {
                EditPrepaidCardGroup();
                return;
            }
            if (!_canViewPrepaidCard)
                return;
            PrepaidCardGroupRow group = _prepaidCardGroupService.LoadById(id);
            if (group == null)
                return;
            using (var dlg = new PrepaidCardGroupEditDialog(ResolvePrepaidCardSession(), group, null, true))
                OpenPrepaidCardGroupDialog(dlg);
        }

        private DialogResult OpenPrepaidCardGroupDialog(PrepaidCardGroupEditDialog dlg)
        {
            return PrepaidCardGroupDialogOpenerForTest != null
                ? PrepaidCardGroupDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        internal void DeletePrepaidCardGroup()
        {
            if (!_prepaidCardMode || !_canRemove)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            PrepaidCardGroupRow group = _prepaidCardGroupService.LoadById(id);
            if (group == null)
            {
                ShowPrepaidCardWarning(PrepaidCardText.GroupNotFound);
                Reload();
                return;
            }
            if (!SuppressPrepaidCardPopupForTest && !Msg.Confirm(this, PrepaidCardText.DeleteGroupConfirm(group.Name)))
                return;

            PrepaidCardGroupDeleteResult res;
            try
            {
                res = _prepaidCardGroupService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowPrepaidCardError("Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!res.Ok)
            {
                ShowPrepaidCardWarning(res.ErrorMessage);
                return;
            }
            LastPrepaidCardMessageForTest = null;
            Reload();
        }

        private void RenameSelectedPrepaidCardGroup()
        {
            if (!_prepaidCardMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_tree.SelectedNode != null)
                _tree.SelectedNode.BeginEdit();
        }

        private void RenamePrepaidCardGroupLabel(NodeLabelEditEventArgs e)
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
                ShowPrepaidCardWarning(PrepaidCardText.GroupNameEmpty);
                e.CancelEdit = true;
                return;
            }
            PrepaidCardGroupRow group = _prepaidCardGroupService.LoadById(id);
            if (group == null)
            {
                e.CancelEdit = true;
                return;
            }
            group.Name = e.Label;
            PrepaidCardGroupSaveResult res;
            try
            {
                res = _prepaidCardGroupService.Save(group);
            }
            catch (DataAccessException ex)
            {
                ShowPrepaidCardError("Không ghi được dữ liệu: " + ex.Message);
                e.CancelEdit = true;
                return;
            }
            if (!res.Ok)
            {
                ShowPrepaidCardWarning(res.ErrorMessage);
                e.CancelEdit = true;
                return;
            }
            Reload();
            SelectTreeNode(id);
        }

        private void ShowPrepaidCardWarning(string message)
        {
            LastPrepaidCardMessageForTest = message;
            if (!SuppressPrepaidCardPopupForTest)
                Msg.Warning(this, message);
        }

        private void ShowPrepaidCardError(string message)
        {
            LastPrepaidCardMessageForTest = message;
            if (!SuppressPrepaidCardPopupForTest)
                Msg.Error(this, message);
        }
    }
}
