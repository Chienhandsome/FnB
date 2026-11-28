using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Menu thả xuống của nút "Thêm mới ▾" và menu chuột phải trên lưới / cây thư mục Danh mục nhân viên
    /// (spec docs/specs/nhan-su-cay.md §2–§4).
    /// </summary>
    internal sealed partial class CatalogView
    {
        private readonly ThemedContextMenu _employeeAddMenu = new ThemedContextMenu();
        private readonly ThemedContextMenu _employeeTreeMenu = new ThemedContextMenu();
        private ToolStripMenuItem _empAddSubFolderItem;

        private void BuildEmployeeMenus()
        {
            // Thanh công cụ: "Thêm mới ▾" gồm 4 mục theo spec §2.
            _employeeAddMenu.AddItem("Thêm nhân viên", delegate { AddEmployee(); });
            _employeeAddMenu.AddItem("Thêm thư mục", delegate { AddEmployeeFolder(null); });
            _empAddSubFolderItem = _employeeAddMenu.AddItem("Thêm thư mục con", delegate { AddEmployeeFolder(SelectedEmployeeFolderId()); });
            _employeeAddMenu.AddItem("Thêm phân cách", delegate { AddEmployeeSeparator(); });
            _employeeAddMenu.Opening += delegate { UpdateEmployeeAddMenuState(); };

            // Menu chuột phải trên lưới nhân viên.
            _employeeGridMenu.AddItem("Thêm nhân viên", Keys.Insert, delegate { AddEmployee(); });
            _employeeGridMenu.AddItem("Thêm phân cách", delegate { AddEmployeeSeparator(); });
            _employeeGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditEmployee(); });
            _employeeGridMenu.AddItem("Xem chi tiết", delegate { OpenEmployeeFromGrid(); });
            _employeeGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteEmployee(); });
            _employeeGridMenu.AddItem("Xóa vĩnh viễn", Keys.Shift | Keys.Delete, delegate { DeleteEmployeePermanent(); });
            _employeeGridMenu.AddSeparator();
            _employeeGridMenu.AddItem("Refresh", delegate { Reload(); });
            _employeeGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _employeeGridMenu.AddSeparator();
            _employeeGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _employeeGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _employeeGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _employeeGridMenu.Opening += delegate { UpdateEmployeeGridMenuState(); };

            // Menu chuột phải trên cây thư mục nhân viên.
            _employeeTreeMenu.AddItem("Thêm thư mục", delegate { AddEmployeeFolder(null); });
            _employeeTreeMenu.AddItem("Thêm thư mục con", delegate { AddEmployeeFolder(SelectedEmployeeFolderId()); });
            _employeeTreeMenu.AddItem("Thêm nhân viên", delegate { AddEmployee(); });
            _employeeTreeMenu.AddItem("Thêm phân cách", delegate { AddEmployeeSeparator(); });
            _employeeTreeMenu.AddSeparator();
            _employeeTreeMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditEmployeeFolder(); });
            _employeeTreeMenu.AddItem("Đổi tên", Keys.F2, delegate { RenameSelectedEmployeeFolder(); });
            _employeeTreeMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteEmployeeFolder(false); });
            _employeeTreeMenu.AddItem("Xóa vĩnh viễn", Keys.Shift | Keys.Delete, delegate { DeleteEmployeeFolder(true); });
            _employeeTreeMenu.AddSeparator();
            _employeeTreeMenu.AddItem("Mở rộng", delegate { ExpandSelectedGroup(true); });
            _employeeTreeMenu.AddItem("Thu gọn", delegate { ExpandSelectedGroup(false); });
            _employeeTreeMenu.AddItem("Refresh", delegate { Reload(); });
            _employeeTreeMenu.Opening += delegate { UpdateEmployeeTreeMenuState(); };
        }

        private void ShowEmployeeAddMenu()
        {
            if (!_employeeMode || !_canAdd)
                return;
            UpdateEmployeeAddMenuState();
            _employeeAddMenu.Show(_btnAdd, new Point(0, _btnAdd.Height));
        }

        internal void UpdateEmployeeAddMenuState()
        {
            bool hasFolder = !string.IsNullOrEmpty(SelectedEmployeeFolderId());
            foreach (ToolStripItem item in _employeeAddMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                mi.Visible = _canAdd;
                mi.Enabled = _canAdd && (mi != _empAddSubFolderItem || hasFolder);
            }
        }

        private void UpdateEmployeeGridMenuState()
        {
            EmployeeRow row = CurrentEmployeeRow();
            bool hasRow = row != null;
            bool editableRow = hasRow && !row.IsSeparator;
            foreach (ToolStripItem item in _employeeGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm nhân viên":
                    case "Thêm phân cách":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && editableRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewEmployee;
                        mi.Enabled = editableRow;
                        break;
                    case "Xóa":
                    case "Xóa vĩnh viễn":
                        mi.Visible = _canRemove;
                        mi.Enabled = _canRemove && hasRow;
                        break;
                    case "Sao chép ô":
                    case "Sao chép vùng chọn":
                        mi.Enabled = hasRow;
                        break;
                }
            }
        }

        private void UpdateEmployeeTreeMenuState()
        {
            bool realFolder = !string.IsNullOrEmpty(SelectedEmployeeFolderId());
            bool hasKids = _tree.SelectedNode != null && _tree.SelectedNode.Nodes.Count > 0;
            foreach (ToolStripItem item in _employeeTreeMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm thư mục":
                    case "Thêm nhân viên":
                    case "Thêm phân cách":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Thêm thư mục con":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd && realFolder;
                        break;
                    case "Chỉnh sửa":
                    case "Đổi tên":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && realFolder;
                        break;
                    case "Xóa":
                    case "Xóa vĩnh viễn":
                        mi.Visible = _canRemove;
                        mi.Enabled = _canRemove && realFolder;
                        break;
                    case "Mở rộng":
                    case "Thu gọn":
                        mi.Enabled = hasKids;
                        break;
                }
            }
        }

        private void DisposeEmployeeUi()
        {
            _employeeAddMenu.Dispose();
            _employeeGridMenu.Dispose();
            _employeeTreeMenu.Dispose();
        }
    }
}
