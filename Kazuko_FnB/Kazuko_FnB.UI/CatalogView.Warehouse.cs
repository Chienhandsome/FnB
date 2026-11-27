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
    /// Chế độ Danh mục kho hàng (DKHOHANG, spec docs/specs/danh-muc-kho-hang/) trong <see cref="CatalogView"/>:
    /// lưới 4 cột (Tên kho hàng, Cửa hàng, Cho phép âm kho [chỉ đọc], Ghi chú), ô tìm không dấu (F3),
    /// nút Thêm/Sửa/Xóa ẩn khi thiếu quyền theo SFUNCTION, nhấp đôi mở hộp thoại chỉ đọc khi chỉ có quyền Xem (Q4),
    /// xóa cứng chặn khi còn tham chiếu ở 6 cột hoặc là kho cuối cùng (Q1), xuất Excel qua <c>GridExcelExporter</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _warehouseMode;
        private bool _canViewWarehouse;
        private WarehouseService _warehouseService;
        private List<WarehouseRow> _warehouseRows = new List<WarehouseRow>();
        private readonly ThemedContextMenu _warehouseGridMenu = new ThemedContextMenu();

        // Hook kiểm thử UI (selftest).
        internal bool IsWarehouseModeForTest { get { return _warehouseMode; } }
        internal List<WarehouseRow> WarehouseRowsForTest { get { return _warehouseRows; } }
        internal bool SuppressWarehousePopupForTest { get; set; }
        internal string LastWarehouseMessageForTest { get; private set; }
        internal Func<WarehouseEditDialog, DialogResult> WarehouseDialogOpenerForTest { get; set; }

        private void BuildWarehouseUi()
        {
            _warehouseGridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddWarehouse(); });
            _warehouseGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditWarehouse(); });
            _warehouseGridMenu.AddItem("Xem chi tiết", delegate { OpenWarehouseFromGrid(); });
            _warehouseGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteWarehouse(); });
            _warehouseGridMenu.AddSeparator();
            _warehouseGridMenu.AddItem("Refresh", delegate { Reload(); });
            _warehouseGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _warehouseGridMenu.AddSeparator();
            _warehouseGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _warehouseGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _warehouseGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _warehouseGridMenu.Opening += delegate { UpdateWarehouseGridMenuState(); };
        }

        private UserSession ResolveWarehouseSession()
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

        private void LoadWarehouseData(CatalogData data)
        {
            UserSession sess = ResolveWarehouseSession();
            _warehouseService = new WarehouseService(sess);
            _canViewWarehouse = _warehouseService.CanView;
            _canAdd = _warehouseService.CanAdd;
            _canEdit = _warehouseService.CanEdit;
            _canRemove = _warehouseService.CanRemove;

            _warehouseRows = _warehouseService.LoadAll();
            _total = _warehouseRows.Count;
            BuildTree(data);
            BuildWarehouseColumns();
            BuildWarehouseTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyWarehouseBarVisibility();
            UpdateWarehouseButtons();
            UpdateWarehouseAuditPanel();
        }

        private void ApplyWarehouseBarVisibility()
        {
            if (!_warehouseMode)
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

        private void BuildWarehouseColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddProductColumn("w_name", "Tên kho hàng", 240, typeof(string), null);
            AddProductColumn("w_store", "Cửa hàng", 220, typeof(string), null);
            AddProductColumn("w_allowneg", "Cho phép âm kho", 145, typeof(bool), null);
            AddProductColumn("w_note", "Ghi chú", 220, typeof(string), null);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void BuildWarehouseTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("w_name", typeof(string));
            dt.Columns.Add("w_store", typeof(string));
            dt.Columns.Add("w_allowneg", typeof(bool));
            dt.Columns.Add("w_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (WarehouseRow r in _warehouseRows)
            {
                sb.Length = 0;
                sb.Append(r.Name).Append(' ').Append(r.StoreName).Append(' ').Append(r.Note);
                dt.Rows.Add(
                    r.Name ?? "",
                    r.StoreName ?? "",
                    r.IsAllowNegativeStock,
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

        private string CurrentWarehouseId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private WarehouseRow CurrentWarehouseRow()
        {
            string id = CurrentWarehouseId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _warehouseRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectWarehouseRowByIdForTest(string id)
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
                    UpdateWarehouseButtons();
                    UpdateWarehouseAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateWarehouseButtons()
        {
            if (!_warehouseMode)
                return;
            bool hasRow = CurrentWarehouseId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void UpdateWarehouseAuditPanel()
        {
            if (!_warehouseMode)
                return;
            WarehouseRow row = CurrentWarehouseRow();
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

        private void UpdateWarehouseGridMenuState()
        {
            bool hasRow = CurrentWarehouseId() != null;
            foreach (ToolStripItem item in _warehouseGridMenu.Items)
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
                        mi.Visible = !_canEdit && _canViewWarehouse;
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

        // ------------------------------------------------------------------ CRUD Kho hàng

        private void AddWarehouse()
        {
            if (!_warehouseMode || !_canAdd)
                return;
            using (var dlg = new WarehouseEditDialog(ResolveWarehouseSession(), null, false))
            {
                OpenWarehouseDialog(dlg);
                if (dlg.AnySaved)
                    RefreshWarehouseAndSelect(dlg.SavedId);
            }
        }

        private void EditWarehouse()
        {
            if (!_warehouseMode || !_canEdit)
                return;
            string id = CurrentWarehouseId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new WarehouseEditDialog(ResolveWarehouseSession(), id, false))
            {
                OpenWarehouseDialog(dlg);
                if (dlg.AnySaved)
                    RefreshWarehouseAndSelect(dlg.SavedId);
            }
        }

        /// <summary>Nhấp đôi dòng kho hàng: có quyền Sửa thì mở sửa; chỉ có quyền Xem thì mở chỉ đọc (Q4).</summary>
        private void OpenWarehouseFromGrid()
        {
            if (!_warehouseMode)
                return;
            if (_canEdit)
            {
                EditWarehouse();
                return;
            }
            if (!_canViewWarehouse)
                return;
            string id = CurrentWarehouseId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new WarehouseEditDialog(ResolveWarehouseSession(), id, true))
                OpenWarehouseDialog(dlg);
        }

        private void DeleteWarehouse()
        {
            if (!_warehouseMode || !_canRemove)
                return;
            string id = CurrentWarehouseId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressWarehousePopupForTest && !Msg.Confirm(this, WarehouseText.DeleteConfirm))
                return;

            WarehouseDeleteResult res;
            try
            {
                res = _warehouseService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowWarehouseWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowWarehouseWarning(res.ErrorMessage);
                return;
            }
            LastWarehouseMessageForTest = null;
            Reload();
        }

        private DialogResult OpenWarehouseDialog(WarehouseEditDialog dlg)
        {
            return WarehouseDialogOpenerForTest != null
                ? WarehouseDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void RefreshWarehouseAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectWarehouseRowByIdForTest(id);
        }

        private void ShowWarehouseWarning(string message)
        {
            LastWarehouseMessageForTest = message;
            if (!SuppressWarehousePopupForTest)
                Msg.Warning(this, message);
        }
    }
}
