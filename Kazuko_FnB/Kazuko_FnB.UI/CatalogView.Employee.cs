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
    /// Chế độ Danh mục nhân viên (DNHANVIEN, spec docs/specs/nhan-su-danh-muc.md §2) trong <see cref="CatalogView"/>:
    /// danh mục phẳng 9 cột (Mã, Tên nhân viên, Điện thoại, Địa chỉ, Ca làm việc, Cách tính lương, Lương ca, Lương tháng, Ghi chú),
    /// ô tìm không dấu (F3), phân quyền theo tên chức năng "Danh mục nhân viên", chỉ Xem mở hộp thoại chỉ đọc,
    /// xóa cứng chặn khi còn tham chiếu ở 8 bảng, xuất Excel qua <c>GridExcelExporter</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _employeeMode;
        private bool _canViewEmployee;
        private EmployeeService _employeeService;
        private List<EmployeeRow> _employeeRows = new List<EmployeeRow>();
        private readonly ThemedContextMenu _employeeGridMenu = new ThemedContextMenu();

        // Hook kiểm thử UI (selftest).
        internal bool IsEmployeeModeForTest { get { return _employeeMode; } }
        internal List<EmployeeRow> EmployeeRowsForTest { get { return _employeeRows; } }
        internal bool SuppressEmployeePopupForTest { get; set; }
        internal string LastEmployeeMessageForTest { get; private set; }
        internal Func<EmployeeEditDialog, DialogResult> EmployeeDialogOpenerForTest { get; set; }

        private void BuildEmployeeUi()
        {
            _employeeGridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddEmployee(); });
            _employeeGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditEmployee(); });
            _employeeGridMenu.AddItem("Xem chi tiết", delegate { OpenEmployeeFromGrid(); });
            _employeeGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteEmployee(); });
            _employeeGridMenu.AddSeparator();
            _employeeGridMenu.AddItem("Refresh", delegate { Reload(); });
            _employeeGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _employeeGridMenu.AddSeparator();
            _employeeGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _employeeGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _employeeGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _employeeGridMenu.Opening += delegate { UpdateEmployeeGridMenuState(); };
        }

        private UserSession ResolveEmployeeSession()
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

        private void LoadEmployeeData(CatalogData data)
        {
            UserSession sess = ResolveEmployeeSession();
            _employeeService = new EmployeeService(sess);
            _canViewEmployee = _employeeService.CanView;
            _canAdd = _employeeService.CanAdd;
            _canEdit = _employeeService.CanEdit;
            _canRemove = _employeeService.CanRemove;

            _employeeRows = _employeeService.LoadAll();
            _total = _employeeRows.Count;
            BuildTree(data);
            BuildEmployeeColumns();
            BuildEmployeeTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyEmployeeBarVisibility();
            UpdateEmployeeButtons();
            UpdateEmployeeAuditPanel();
        }

        private void ApplyEmployeeBarVisibility()
        {
            if (!_employeeMode)
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

            _groupBar.Visible = false;
            _bottom.Visible = true;
            _paneSplitter.Visible = true;
            LayoutBar();
        }

        /// <summary>9 cột lưới theo spec §2: Mã, Tên nhân viên, Điện thoại, Địa chỉ, Ca làm việc, Cách tính lương, Lương ca, Lương tháng, Ghi chú.</summary>
        private void BuildEmployeeColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddEmployeeColumn("emp_code", "Mã", 95, typeof(string), null);
            AddEmployeeColumn("emp_name", "Tên nhân viên", 185, typeof(string), null);
            AddEmployeeColumn("emp_phone", "Điện thoại", 115, typeof(string), null);
            AddEmployeeColumn("emp_address", "Địa chỉ", 175, typeof(string), null);
            AddEmployeeColumn("emp_shift", "Ca làm việc", 125, typeof(string), null);
            AddEmployeeColumn("emp_mode", "Cách tính lương", 130, typeof(string), null);
            AddEmployeeColumn("emp_shift_sal", "Lương ca", 110, typeof(decimal), "#,##0.##");
            AddEmployeeColumn("emp_month_sal", "Lương tháng", 120, typeof(decimal), "#,##0.##");
            AddEmployeeColumn("emp_note", "Ghi chú", 145, typeof(string), null);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddEmployeeColumn(string name, string header, int width96, Type type, string format)
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
            if (type == typeof(decimal))
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = new CultureInfo("vi-VN");
            }
            _grid.Columns.Add(gc);
        }

        private void BuildEmployeeTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("emp_code", typeof(string));
            dt.Columns.Add("emp_name", typeof(string));
            dt.Columns.Add("emp_phone", typeof(string));
            dt.Columns.Add("emp_address", typeof(string));
            dt.Columns.Add("emp_shift", typeof(string));
            dt.Columns.Add("emp_mode", typeof(string));
            dt.Columns.Add("emp_shift_sal", typeof(decimal));
            dt.Columns.Add("emp_month_sal", typeof(decimal));
            dt.Columns.Add("emp_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (EmployeeRow r in _employeeRows)
            {
                sb.Length = 0;
                sb.Append(r.Code).Append(' ');
                sb.Append(r.Name).Append(' ');
                sb.Append(r.Phone).Append(' ');
                sb.Append(r.Address).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Code ?? "",
                    r.Name ?? "",
                    r.Phone ?? "",
                    r.Address ?? "",
                    r.ShiftName ?? "",
                    EmployeeRules.SalaryModeDisplayText(r.SalaryMode),
                    r.ShiftSalary,
                    r.MonthlySalary,
                    r.Note ?? "",
                    r.Id,
                    CatalogRepository.Fold(sb.ToString()),
                    "");
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private string CurrentEmployeeId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private EmployeeRow CurrentEmployeeRow()
        {
            string id = CurrentEmployeeId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _employeeRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectEmployeeRowByIdForTest(string id)
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
                    UpdateEmployeeButtons();
                    UpdateEmployeeAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateEmployeeButtons()
        {
            if (!_employeeMode)
                return;
            bool hasRow = CurrentEmployeeId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void UpdateEmployeeAuditPanel()
        {
            if (!_employeeMode)
                return;
            EmployeeRow row = CurrentEmployeeRow();
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

        private void UpdateEmployeeGridMenuState()
        {
            bool hasRow = CurrentEmployeeId() != null;
            foreach (ToolStripItem item in _employeeGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm mới":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewEmployee;
                        mi.Enabled = hasRow;
                        break;
                    case "Xóa":
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

        // ------------------------------------------------------------------ CRUD Nhân viên

        private void AddEmployee()
        {
            if (!_employeeMode || !_canAdd)
                return;
            using (var dlg = new EmployeeEditDialog(ResolveEmployeeSession(), null, false))
            {
                OpenEmployeeDialog(dlg);
                if (dlg.AnySaved)
                    RefreshEmployeeAndSelect(dlg.SavedId);
            }
        }

        private void EditEmployee()
        {
            if (!_employeeMode || !_canEdit)
                return;
            string id = CurrentEmployeeId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new EmployeeEditDialog(ResolveEmployeeSession(), id, false))
            {
                OpenEmployeeDialog(dlg);
                if (dlg.AnySaved)
                    RefreshEmployeeAndSelect(dlg.SavedId);
            }
        }

        /// <summary>Nhấp đôi dòng nhân viên: có quyền Sửa thì mở sửa; chỉ có quyền Xem thì mở chỉ đọc.</summary>
        private void OpenEmployeeFromGrid()
        {
            if (!_employeeMode)
                return;
            if (_canEdit)
            {
                EditEmployee();
                return;
            }
            if (!_canViewEmployee)
                return;
            string id = CurrentEmployeeId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new EmployeeEditDialog(ResolveEmployeeSession(), id, true))
                OpenEmployeeDialog(dlg);
        }

        private void DeleteEmployee()
        {
            if (!_employeeMode || !_canRemove)
                return;
            string id = CurrentEmployeeId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressEmployeePopupForTest && !Msg.Confirm(this, EmployeeText.DeleteConfirm))
                return;

            EmployeeDeleteResult res;
            try
            {
                res = _employeeService.Delete(id);
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

        private DialogResult OpenEmployeeDialog(EmployeeEditDialog dlg)
        {
            return EmployeeDialogOpenerForTest != null
                ? EmployeeDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void RefreshEmployeeAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectEmployeeRowByIdForTest(id);
        }

        private void ShowEmployeeWarning(string message)
        {
            LastEmployeeMessageForTest = message;
            if (!SuppressEmployeePopupForTest)
                Msg.Warning(this, message);
        }
    }
}
