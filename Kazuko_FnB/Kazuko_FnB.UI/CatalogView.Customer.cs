using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
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
    /// Chế độ Danh mục khách hàng (DKHACHHANG, spec docs/specs/danh-muc-khach-hang.md) trong <see cref="CatalogView"/>:
    /// cây nhóm khách hàng (DNHOMKHACHHANG) bên trái, lưới 11 cột theo §4.1 bên phải (cột Ghi chú mặc định ẩn,
    /// bật/tắt được), tìm không dấu (F3), nút Thêm/Sửa/Xóa ẩn khi thiếu quyền (§9.0 Q10), nhấp đôi mở hộp thoại
    /// chỉ đọc khi chỉ có quyền Xem (§9.0.2 Q10). Phần CRUD nhóm nằm ở <c>CatalogView.Customer.Groups.cs</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private const string ColSearchNote = "_search_note";

        private bool _customerMode;
        private bool _canViewCustomer;
        private List<CustomerRow> _customerRows = new List<CustomerRow>();
        private CustomerRepository _customerRepo;
        private CustomerGroupRepository _customerGroupRepo;
        private readonly ThemedContextMenu _customerGridMenu = new ThemedContextMenu();
        private readonly ThemedContextMenu _customerTreeMenu = new ThemedContextMenu();
        private ToolStripMenuItem _customerColSubmenu;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressCustomerPopupForTest { get; set; }
        internal string LastCustomerMessageForTest { get; private set; }
        internal Func<CustomerEditDialog, DialogResult> CustomerDialogOpenerForTest { get; set; }
        internal Func<CustomerGroupEditDialog, DialogResult> CustomerGroupDialogOpenerForTest { get; set; }
        internal FlatButton AddButtonForTest { get { return _btnAdd; } }
        internal FlatButton EditButtonForTest { get { return _btnEdit; } }
        internal FlatButton RemoveButtonForTest { get { return _btnRemove; } }
        internal FlatButton ExcelAddButtonForTest { get { return _btnExcelAdd; } }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }
        internal FlatButton GroupAddButtonForTest { get { return _groupAdd; } }
        internal FlatButton GroupEditButtonForTest { get { return _groupEdit; } }
        internal Panel GroupBarForTest { get { return _groupBar; } }
        internal TreeView TreeForTest { get { return _tree; } }
        internal InputBox SearchBoxForTest { get { return _search; } }
        internal bool IsCustomerModeForTest { get { return _customerMode; } }
        internal List<CustomerRow> CustomerRowsForTest { get { return _customerRows; } }

        private void BuildCustomerUi()
        {
            BuildCustomerGridMenu();
            BuildCustomerTreeMenu();
        }

        private void LoadCustomerData(CatalogData data)
        {
            _customerRepo = _customerRepo ?? new CustomerRepository(_databasePath);
            _customerGroupRepo = _customerGroupRepo ?? new CustomerGroupRepository(_databasePath);
            _canViewCustomer = CustomerRules.CanCurrent(RoleModes.View);
            _canAdd = CustomerRules.CanCurrent(RoleModes.Add);
            _canEdit = CustomerRules.CanCurrent(RoleModes.Edit);
            _canRemove = CustomerRules.CanCurrent(RoleModes.Remove);

            _customerRows = _customerRepo.LoadCustomers();
            _total = _customerRows.Count;
            BuildTree(data);
            BuildCustomerColumns();
            BuildCustomerTable();

            // Gỡ các tab của chế độ Mặt hàng / Bàn nếu có; chỉ giữ tab "Thông tin" ở pane dưới.
            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyCustomerBarVisibility();
            UpdateCustomerButtons();
            UpdateCustomerAuditPanel();
        }

        /// <summary>Ẩn/hiện nút theo quyền (§9.0 Q10: thiếu quyền nào thì ẩn nút đó).</summary>
        private void ApplyCustomerBarVisibility()
        {
            if (!_customerMode)
                return;
            _btnAdd.Visible = _canAdd;
            _btnEdit.Visible = _canEdit;
            _btnRemove.Visible = _canRemove;
            _refresh.Visible = true;
            _btnExcelAdd.Visible = _canAdd;
            _btnExcelExport.Visible = true;
            _btnExcelImport.Visible = false;
            _btnPrint.Visible = true;
            _btnPrintBarcode.Visible = false;
            _btnQuickAdd.Visible = false;
            _btnTotal.Visible = false;

            float s = DeviceDpi / 96f;
            int gx = (int)(6 * s);
            _groupAdd.Visible = _canAdd;
            if (_groupAdd.Visible)
            {
                _groupAdd.SetBounds(gx, (int)(4 * s), (int)(28 * s), (int)(28 * s));
                gx += (int)(34 * s);
            }
            _groupEdit.Visible = _canEdit;
            if (_groupEdit.Visible)
                _groupEdit.SetBounds(gx, (int)(4 * s), (int)(28 * s), (int)(28 * s));
            _groupBar.Visible = _canAdd || _canEdit;

            _bottom.Visible = true;
            _paneSplitter.Visible = true;
            LayoutBar();
        }

        /// <summary>11 cột đúng thứ tự và ẩn/hiện mặc định theo §4.1 (SGRID DKHACHHANG); Ghi chú mặc định ẩn.</summary>
        private void BuildCustomerColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.ColumnHeadersHeight = (int)(48 * s);

            AddCustomerColumn("c_name", "Tên khách hàng", 97, typeof(string), null, true);
            AddCustomerColumn("c_group", "Nhóm khách hàng", 130, typeof(string), null, true);
            AddCustomerColumn("c_code", "Mã khách", 68, typeof(string), null, true);
            AddCustomerColumn("c_address", "Địa chỉ", 186, typeof(string), null, true);
            AddCustomerColumn("c_phone", "Điện thoại", 139, typeof(string), null, true);
            AddCustomerColumn("c_email", "Email", 139, typeof(string), null, true);
            AddCustomerColumn("c_tax", "Mã số thuế", 139, typeof(string), null, true);
            AddCustomerColumn("c_emp", "Nhân viên", 78, typeof(string), null, true);
            AddCustomerColumn("c_points", "Điểm tích lũy ban đầu", 105, typeof(int), "N0", true);
            AddCustomerColumn("c_birthday", "Ngày thành lập/sinh nhật", 80, typeof(DateTime), "dd/MM/yyyy", true);
            AddCustomerColumn("c_note", "Ghi chú", 150, typeof(string), null, false);
        }

        private void AddCustomerColumn(string name, string header, int width96, Type type, string format, bool visible)
        {
            float s = DeviceDpi / 96f;
            var gc = new DataGridViewTextBoxColumn
            {
                Name = name,
                DataPropertyName = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.Automatic,
                ReadOnly = true,
                Visible = visible
            };
            int headerNeed = TextRenderer.MeasureText(header, _grid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(30 * s);
            gc.Width = Math.Max(Math.Max((int)(60 * s), (int)(width96 * s)), headerNeed);
            gc.MinimumWidth = (int)(50 * s);
            if (type == typeof(int) || type == typeof(decimal))
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = new CultureInfo("vi-VN");
            }
            else if (type == typeof(DateTime))
            {
                gc.DefaultCellStyle.Format = format;
            }
            _grid.Columns.Add(gc);
        }

        private void BuildCustomerTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("c_name", typeof(string));
            dt.Columns.Add("c_group", typeof(string));
            dt.Columns.Add("c_code", typeof(string));
            dt.Columns.Add("c_address", typeof(string));
            dt.Columns.Add("c_phone", typeof(string));
            dt.Columns.Add("c_email", typeof(string));
            dt.Columns.Add("c_tax", typeof(string));
            dt.Columns.Add("c_emp", typeof(string));
            dt.Columns.Add("c_points", typeof(int));
            dt.Columns.Add("c_birthday", typeof(DateTime));
            dt.Columns.Add("c_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColSearchNote, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (CustomerRow r in _customerRows)
            {
                var values = new object[dt.Columns.Count];
                values[0] = r.Name ?? "";
                values[1] = r.GroupName ?? "";
                values[2] = r.Code ?? "";
                values[3] = r.Address ?? "";
                values[4] = r.Phone ?? "";
                values[5] = r.Email ?? "";
                values[6] = r.TaxCode ?? "";
                values[7] = r.EmployeeName ?? "";
                values[8] = r.InitialPoints;
                values[9] = r.Birthday.HasValue ? (object)r.Birthday.Value : DBNull.Value;
                values[10] = r.Note ?? "";
                values[11] = r.Id;

                sb.Length = 0;
                sb.Append(r.Name).Append(' ');
                sb.Append(r.GroupName).Append(' ');
                sb.Append(r.Code).Append(' ');
                sb.Append(r.Address).Append(' ');
                sb.Append(r.Phone).Append(' ');
                sb.Append(r.Email).Append(' ');
                sb.Append(r.TaxCode).Append(' ');
                sb.Append(r.EmployeeName).Append(' ');
                sb.Append(r.InitialPoints.ToString(CultureInfo.InvariantCulture)).Append(' ');
                if (r.Birthday.HasValue)
                    sb.Append(r.Birthday.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                values[12] = CatalogRepository.Fold(sb.ToString());
                values[13] = CatalogRepository.Fold(r.Note ?? "");
                values[14] = CatalogRepository.CategoryPath(_nodes, r.GroupId);
                dt.Rows.Add(values);
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private bool IsCustomerNoteVisible()
        {
            DataGridViewColumn col = _grid.Columns["c_note"];
            return col != null && col.Visible;
        }

        internal void ToggleCustomerColumnForTest(string columnName, bool visible)
        {
            DataGridViewColumn col = _grid.Columns[columnName];
            if (col == null)
                return;
            col.Visible = visible;
            ApplyFilter();
        }

        private string CurrentCustomerId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private CustomerRow CurrentCustomerRow()
        {
            string id = CurrentCustomerId();
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (CustomerRow r in _customerRows)
            {
                if (string.Equals(r.Id, id, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        private void SelectCustomerRow(string id)
        {
            if (_view == null || string.IsNullOrEmpty(id))
                return;
            for (int i = 0; i < _view.Count; i++)
            {
                if (string.Equals(Convert.ToString(_view[i][ColId]), id, StringComparison.Ordinal))
                {
                    _grid.ClearSelection();
                    _grid.CurrentCell = _grid.Rows[i].Cells[0];
                    _grid.Rows[i].Selected = true;
                    return;
                }
            }
        }

        private void RefreshCustomerAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectCustomerRow(id);
        }

        private void UpdateCustomerButtons()
        {
            if (!_customerMode)
                return;
            bool hasRow = CurrentCustomerId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;

            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            _groupAdd.Enabled = _canAdd;
            _groupEdit.Enabled = _canEdit && realGroup;
            _tree.LabelEdit = _canEdit;
        }

        private void UpdateCustomerAuditPanel()
        {
            if (!_customerMode)
                return;
            CustomerRow row = CurrentCustomerRow();
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

        // ------------------------------------------------------------------ CRUD khách hàng

        private void AddCustomer()
        {
            if (!_customerMode || !_canAdd)
                return;
            string groupId = SelectedGroupId();
            if (groupId == UnsetTag)
                groupId = null;
            using (var dlg = new CustomerEditDialog(AppSession.Current, null, groupId, false))
            {
                OpenCustomerDialog(dlg);
                if (dlg.SavedId != null)
                    RefreshCustomerAndSelect(dlg.SavedId);
            }
        }

        private void EditCustomer()
        {
            if (!_customerMode || !_canEdit)
                return;
            string id = CurrentCustomerId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new CustomerEditDialog(AppSession.Current, id, null, false))
            {
                OpenCustomerDialog(dlg);
                if (dlg.SavedId != null)
                    RefreshCustomerAndSelect(dlg.SavedId);
            }
        }

        /// <summary>Nhấp đôi dòng khách hàng: có quyền Sửa thì mở sửa; chỉ có quyền Xem thì mở chỉ đọc (§9.0.2 Q10).</summary>
        private void OpenCustomerFromGrid()
        {
            if (!_customerMode)
                return;
            if (_canEdit)
            {
                EditCustomer();
                return;
            }
            if (!_canViewCustomer)
                return;
            string id = CurrentCustomerId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new CustomerEditDialog(AppSession.Current, id, null, true))
                OpenCustomerDialog(dlg);
        }

        private DialogResult OpenCustomerDialog(CustomerEditDialog dlg)
        {
            if (CustomerDialogOpenerForTest != null)
                return CustomerDialogOpenerForTest(dlg);
            return dlg.ShowDialog(this);
        }

        private void DeleteCustomer()
        {
            if (!_customerMode || !_canRemove)
                return;
            CustomerRow row = CurrentCustomerRow();
            if (row == null)
                return;
            if (!SuppressCustomerPopupForTest && !Msg.Confirm(this, CustomerText.DeleteConfirm))
                return;
            CustomerDeleteResult res;
            try
            {
                res = _customerRepo.DeleteCustomer(row.Id);
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
            RefreshCustomerAndSelect(null);
        }
    }
}
