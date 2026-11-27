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
    /// Chế độ Danh mục nhà cung cấp (DNHACUNGCAP, spec docs/specs/danh-muc-nha-cung-cap/) trong <see cref="CatalogView"/>:
    /// cây nhóm nhà cung cấp (DNHOMNHACUNGCAP) bên trái, lưới 8 cột bên phải (Mã, Tên, Nhóm, Địa chỉ, Điện thoại,
    /// Email, Website, Ghi chú), ô tìm không dấu (F3), nút Thêm/Sửa/Xóa ẩn khi thiếu quyền theo SFUNCTION,
    /// nhấp đôi mở hộp thoại chỉ đọc khi không có quyền Sửa, xuất Excel qua <c>GridExcelExporter</c>.
    /// Phần CRUD nhóm ở <c>CatalogView.Supplier.Groups.cs</c>, menu ngữ cảnh ở <c>CatalogView.Supplier.Menus.cs</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _supplierMode;
        private bool _canViewSupplier;
        private SupplierService _supplierService;
        private SupplierGroupService _supplierGroupService;
        private List<SupplierRow> _supplierRows = new List<SupplierRow>();
        private readonly ThemedContextMenu _supplierGridMenu = new ThemedContextMenu();
        private readonly ThemedContextMenu _supplierTreeMenu = new ThemedContextMenu();
        private ToolStripMenuItem _supplierColSubmenu;

        // Hook kiểm thử UI (selftest).
        internal bool IsSupplierModeForTest { get { return _supplierMode; } }
        internal List<SupplierRow> SupplierRowsForTest { get { return _supplierRows; } }
        internal bool SuppressSupplierPopupForTest { get; set; }
        internal string LastSupplierMessageForTest { get; private set; }
        internal Func<SupplierEditDialog, DialogResult> SupplierDialogOpenerForTest { get; set; }
        internal Func<SupplierGroupEditDialog, DialogResult> SupplierGroupDialogOpenerForTest { get; set; }
        internal ThemedContextMenu SupplierTreeMenuForTest { get { return _supplierTreeMenu; } }

        private void BuildSupplierUi()
        {
            BuildSupplierGridMenu();
            BuildSupplierTreeMenu();
        }

        private UserSession ResolveSupplierSession()
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
                Permissions = current == null ? new FunctionPermissions(false) : current.Permissions,
                CurrentStore = current == null ? null : current.CurrentStore
            };
        }

        private void LoadSupplierData(CatalogData data)
        {
            UserSession sess = ResolveSupplierSession();
            _supplierService = new SupplierService(sess);
            _supplierGroupService = new SupplierGroupService(sess);
            _canViewSupplier = _supplierService.CanView;
            _canAdd = _supplierService.CanAdd;
            _canEdit = _supplierService.CanEdit;
            _canRemove = _supplierService.CanRemove;

            _supplierRows = _supplierService.LoadAll();
            _total = _supplierRows.Count;

            // DNHACUNGCAP là danh mục cây nhóm (TABLETYPE = 2): luôn nạp cây từ DNHOMNHACUNGCAP
            // thay vì phụ thuộc STABLEDESC.CATEGORYCOL / SCOLUMN.REFTABLEID trong DB gốc.
            if (_def != null)
            {
                _def.CategoryColumn = "DNHOMNHACUNGCAPID";
                _def.CategoryTable = SupplierText.GroupTableName;
            }
            var treeData = new CatalogData();
            foreach (SupplierGroupRow g in _supplierGroupService.LoadAll())
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
            BuildSupplierColumns();
            BuildSupplierTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplySupplierBarVisibility();
            UpdateSupplierButtons();
            UpdateSupplierAuditPanel();
        }

        private void ApplySupplierBarVisibility()
        {
            if (!_supplierMode)
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

        /// <summary>8 cột lưới theo giao-dien.md: Mã, Tên nhà cung cấp, Nhóm, Địa chỉ, Điện thoại, Email, Website, Ghi chú.</summary>
        private void BuildSupplierColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddSupplierColumn("s_code", "Mã nhà cung cấp", 135);
            AddSupplierColumn("s_name", "Tên nhà cung cấp", 190);
            AddSupplierColumn("s_group", "Nhóm nhà cung cấp", 155);
            AddSupplierColumn("s_address", "Địa chỉ", 170);
            AddSupplierColumn("s_phone", "Điện thoại", 110);
            AddSupplierColumn("s_email", "Email", 140);
            AddSupplierColumn("s_website", "Website", 130);
            AddSupplierColumn("s_note", "Ghi chú", 140);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddSupplierColumn(string name, string header, int width96)
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

        private void BuildSupplierTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("s_code", typeof(string));
            dt.Columns.Add("s_name", typeof(string));
            dt.Columns.Add("s_group", typeof(string));
            dt.Columns.Add("s_address", typeof(string));
            dt.Columns.Add("s_phone", typeof(string));
            dt.Columns.Add("s_email", typeof(string));
            dt.Columns.Add("s_website", typeof(string));
            dt.Columns.Add("s_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (SupplierRow r in _supplierRows)
            {
                sb.Length = 0;
                sb.Append(r.Code).Append(' ');
                sb.Append(r.Name).Append(' ');
                sb.Append(r.GroupName).Append(' ');
                sb.Append(r.Address).Append(' ');
                sb.Append(r.Phone).Append(' ');
                sb.Append(r.Email).Append(' ');
                sb.Append(r.Website).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Code ?? "",
                    r.Name ?? "",
                    r.GroupName ?? "",
                    r.Address ?? "",
                    r.Phone ?? "",
                    r.Email ?? "",
                    r.Website ?? "",
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

        private string CurrentSupplierId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private SupplierRow CurrentSupplierRow()
        {
            string id = CurrentSupplierId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _supplierRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectSupplierRowByIdForTest(string id)
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
                    UpdateSupplierButtons();
                    UpdateSupplierAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateSupplierButtons()
        {
            if (!_supplierMode)
                return;
            bool hasRow = CurrentSupplierId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;

            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            _groupAdd.Enabled = _canAdd;
            _groupEdit.Enabled = _canEdit && realGroup;
            _tree.LabelEdit = _canEdit;
        }

        private void UpdateSupplierAuditPanel()
        {
            if (!_supplierMode)
                return;
            SupplierRow row = CurrentSupplierRow();
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

        // ------------------------------------------------------------------ CRUD Nhà cung cấp

        private void AddSupplier()
        {
            if (!_supplierMode || !_canAdd)
                return;
            string groupId = SelectedGroupId();
            if (groupId == UnsetTag)
                groupId = null;
            using (var dlg = new SupplierEditDialog(ResolveSupplierSession(), null, groupId, false))
            {
                OpenSupplierDialog(dlg);
                if (dlg.AnySaved)
                    RefreshSupplierAndSelect(dlg.SavedId);
            }
        }

        private void EditSupplier()
        {
            if (!_supplierMode || !_canEdit)
                return;
            string id = CurrentSupplierId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new SupplierEditDialog(ResolveSupplierSession(), id, null, false))
            {
                OpenSupplierDialog(dlg);
                if (dlg.AnySaved)
                    RefreshSupplierAndSelect(dlg.SavedId);
            }
        }

        /// <summary>Nhấp đôi dòng nhà cung cấp: có quyền Sửa thì mở sửa; không có quyền Sửa (chỉ Xem / Thủ kho 80) thì mở chỉ đọc.</summary>
        private void OpenSupplierFromGrid()
        {
            if (!_supplierMode)
                return;
            if (_canEdit)
            {
                EditSupplier();
                return;
            }
            if (!_canViewSupplier)
                return;
            string id = CurrentSupplierId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new SupplierEditDialog(ResolveSupplierSession(), id, null, true))
                OpenSupplierDialog(dlg);
        }

        private DialogResult OpenSupplierDialog(SupplierEditDialog dlg)
        {
            return SupplierDialogOpenerForTest != null
                ? SupplierDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void DeleteSupplier()
        {
            if (!_supplierMode || !_canRemove)
                return;
            string id = CurrentSupplierId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressSupplierPopupForTest && !Msg.Confirm(this, SupplierText.DeleteConfirm))
                return;

            SupplierDeleteResult res;
            try
            {
                res = _supplierService.Delete(id);
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

        private void RefreshSupplierAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectSupplierRowByIdForTest(id);
        }
    }
}
