using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần CRUD nhóm wifi (DNHOMWIFI) ngay trên cây nhóm của <see cref="CatalogView"/>.</summary>
    internal sealed partial class CatalogView
    {
        private void AddWifiGroup(string parentId)
        {
            if (!_wifiPasswordMode || !_canAdd)
                return;
            if (parentId == UnsetTag)
                parentId = null;
            using (var dlg = new WifiGroupEditDialog(ResolveWifiSession(), null, parentId, false))
            {
                OpenWifiGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(dlg.SavedId);
                }
            }
        }

        private void EditWifiGroup()
        {
            if (!_wifiPasswordMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            WifiGroupRow group = _wifiGroupService.LoadById(id);
            if (group == null)
            {
                ShowWifiWarning(WifiPasswordText.GroupNotFound);
                Reload();
                return;
            }
            using (var dlg = new WifiGroupEditDialog(ResolveWifiSession(), group, null, false))
            {
                OpenWifiGroupDialog(dlg);
                if (!string.IsNullOrEmpty(dlg.SavedId))
                {
                    Reload();
                    SelectTreeNode(id);
                }
            }
        }

        private void OpenWifiGroupFromTree()
        {
            if (!_wifiPasswordMode)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_canEdit)
            {
                EditWifiGroup();
                return;
            }
            if (!_canViewWifiPassword)
                return;
            WifiGroupRow group = _wifiGroupService.LoadById(id);
            if (group == null)
                return;
            using (var dlg = new WifiGroupEditDialog(ResolveWifiSession(), group, null, true))
                OpenWifiGroupDialog(dlg);
        }

        private DialogResult OpenWifiGroupDialog(WifiGroupEditDialog dlg)
        {
            return WifiGroupDialogOpenerForTest != null
                ? WifiGroupDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        internal void DeleteWifiGroup()
        {
            if (!_wifiPasswordMode || !_canRemove)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            WifiGroupRow group = _wifiGroupService.LoadById(id);
            if (group == null)
            {
                ShowWifiWarning(WifiPasswordText.GroupNotFound);
                Reload();
                return;
            }
            if (!SuppressWifiPopupForTest && !Msg.Confirm(this, WifiPasswordText.DeleteGroupConfirm(group.Name)))
                return;

            WifiGroupDeleteResult res;
            try
            {
                res = _wifiGroupService.Delete(id);
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

        private void RenameSelectedWifiGroup()
        {
            if (!_wifiPasswordMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_tree.SelectedNode != null)
                _tree.SelectedNode.BeginEdit();
        }

        private void RenameWifiGroupLabel(NodeLabelEditEventArgs e)
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
                ShowWifiWarning(WifiPasswordText.GroupNameEmpty);
                e.CancelEdit = true;
                return;
            }
            WifiGroupRow group = _wifiGroupService.LoadById(id);
            if (group == null)
            {
                e.CancelEdit = true;
                return;
            }
            group.Name = e.Label;
            WifiGroupSaveResult res;
            try
            {
                res = _wifiGroupService.Save(group);
            }
            catch (DataAccessException ex)
            {
                ShowWifiError("Không ghi được dữ liệu: " + ex.Message);
                e.CancelEdit = true;
                return;
            }
            if (!res.Ok)
            {
                ShowWifiWarning(res.ErrorMessage);
                e.CancelEdit = true;
                return;
            }
            Reload();
            SelectTreeNode(id);
        }

        private void ShowWifiWarning(string message)
        {
            LastWifiMessageForTest = message;
            if (!SuppressWifiPopupForTest)
                Msg.Warning(this, message);
        }

        private void ShowWifiError(string message)
        {
            LastWifiMessageForTest = message;
            if (!SuppressWifiPopupForTest)
                Msg.Error(this, message);
        }
    }
}
