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
    /// Chế độ Danh mục cửa hàng (DCUAHANG, spec docs/specs/danh-muc-cua-hang/) trong <see cref="CatalogView"/>:
    /// lưới 6 cột (Mã cửa hàng, Tên cửa hàng, Địa chỉ, Điện thoại, Kho mặc định, Ghi chú), ô tìm không dấu (F3),
    /// nút Thêm/Sửa/Xóa ẩn khi thiếu quyền theo SFUNCTION, nhấp đôi mở hộp thoại chỉ đọc khi chỉ có quyền Xem (Q4),
    /// xóa cứng chặn khi còn tham chiếu ở 4 bảng hoặc là cửa hàng cuối cùng (Q1), xuất Excel qua <c>GridExcelExporter</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _storeMode;
        private bool _canViewStore;
        private StoreService _storeService;
        private List<StoreRow> _storeRows = new List<StoreRow>();
        private readonly ThemedContextMenu _storeGridMenu = new ThemedContextMenu();

        // Hook kiểm thử UI (selftest).
        internal bool IsStoreModeForTest { get { return _storeMode; } }
        internal List<StoreRow> StoreRowsForTest { get { return _storeRows; } }
        internal bool SuppressStorePopupForTest { get; set; }
        internal string LastStoreMessageForTest { get; private set; }
        internal Func<StoreEditDialog, DialogResult> StoreDialogOpenerForTest { get; set; }

        private void BuildStoreUi()
        {
            _storeGridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddStore(); });
            _storeGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditStore(); });
            _storeGridMenu.AddItem("Xem chi tiết", delegate { OpenStoreFromGrid(); });
            _storeGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteStore(); });
            _storeGridMenu.AddSeparator();
            _storeGridMenu.AddItem("Refresh", delegate { Reload(); });
            _storeGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _storeGridMenu.AddSeparator();
            _storeGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _storeGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _storeGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _storeGridMenu.Opening += delegate { UpdateStoreGridMenuState(); };
        }

        private UserSession ResolveStoreSession()
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
                Stores = current == null ? null : current.Stores,
                CurrentStore = current == null ? null : current.CurrentStore,
                CurrentWarehouseId = current == null ? null : current.CurrentWarehouseId
            };
        }

        private void LoadStoreData(CatalogData data)
        {
            UserSession sess = ResolveStoreSession();
            _storeService = new StoreService(sess);
            _canViewStore = _storeService.CanView;
            _canAdd = _storeService.CanAdd;
            _canEdit = _storeService.CanEdit;
            _canRemove = _storeService.CanRemove;

            _storeRows = _storeService.LoadAll();
            _total = _storeRows.Count;
            BuildTree(data);
            BuildStoreColumns();
            BuildStoreTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyStoreBarVisibility();
            UpdateStoreButtons();
            UpdateStoreAuditPanel();
        }

        private void ApplyStoreBarVisibility()
        {
            if (!_storeMode)
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

        /// <summary>6 cột lưới theo giao-dien.md: Mã cửa hàng, Tên cửa hàng, Địa chỉ, Điện thoại, Kho mặc định, Ghi chú.</summary>
        private void BuildStoreColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddStoreColumn("st_code", "Mã cửa hàng", 135);
            AddStoreColumn("st_name", "Tên cửa hàng", 210);
            AddStoreColumn("st_address", "Địa chỉ", 210);
            AddStoreColumn("st_phone", "Điện thoại", 125);
            AddStoreColumn("st_warehouse", "Kho mặc định", 180);
            AddStoreColumn("st_note", "Ghi chú", 170);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddStoreColumn(string name, string header, int width96)
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

        private void BuildStoreTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("st_code", typeof(string));
            dt.Columns.Add("st_name", typeof(string));
            dt.Columns.Add("st_address", typeof(string));
            dt.Columns.Add("st_phone", typeof(string));
            dt.Columns.Add("st_warehouse", typeof(string));
            dt.Columns.Add("st_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (StoreRow r in _storeRows)
            {
                sb.Length = 0;
                sb.Append(r.Code).Append(' ');
                sb.Append(r.Name).Append(' ');
                sb.Append(r.Address).Append(' ');
                sb.Append(r.Phone).Append(' ');
                sb.Append(r.WarehouseName).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Code ?? "",
                    r.Name ?? "",
                    r.Address ?? "",
                    r.Phone ?? "",
                    r.WarehouseName ?? "",
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

        private string CurrentStoreId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private StoreRow CurrentStoreRow()
        {
            string id = CurrentStoreId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _storeRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectStoreRowByIdForTest(string id)
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
                    UpdateStoreButtons();
                    UpdateStoreAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateStoreButtons()
        {
            if (!_storeMode)
                return;
            bool hasRow = CurrentStoreId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void UpdateStoreAuditPanel()
        {
            if (!_storeMode)
                return;
            StoreRow row = CurrentStoreRow();
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

        private void UpdateStoreGridMenuState()
        {
            bool hasRow = CurrentStoreId() != null;
            foreach (ToolStripItem item in _storeGridMenu.Items)
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
                        mi.Visible = !_canEdit && _canViewStore;
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

        // ------------------------------------------------------------------ CRUD Cửa hàng

        private void AddStore()
        {
            if (!_storeMode || !_canAdd)
                return;
            using (var dlg = new StoreEditDialog(ResolveStoreSession(), null, false))
            {
                OpenStoreDialog(dlg);
                if (dlg.AnySaved)
                    RefreshStoreAndSelect(dlg.SavedId);
            }
        }

        private void EditStore()
        {
            if (!_storeMode || !_canEdit)
                return;
            string id = CurrentStoreId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new StoreEditDialog(ResolveStoreSession(), id, false))
            {
                OpenStoreDialog(dlg);
                if (dlg.AnySaved)
                    RefreshStoreAndSelect(dlg.SavedId);
            }
        }

        /// <summary>Nhấp đôi dòng cửa hàng: có quyền Sửa thì mở sửa; chỉ có quyền Xem thì mở chỉ đọc (Q4).</summary>
        private void OpenStoreFromGrid()
        {
            if (!_storeMode)
                return;
            if (_canEdit)
            {
                EditStore();
                return;
            }
            if (!_canViewStore)
                return;
            string id = CurrentStoreId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new StoreEditDialog(ResolveStoreSession(), id, true))
                OpenStoreDialog(dlg);
        }

        private void DeleteStore()
        {
            if (!_storeMode || !_canRemove)
                return;
            string id = CurrentStoreId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressStorePopupForTest && !Msg.Confirm(this, StoreText.DeleteConfirm))
                return;

            StoreDeleteResult res;
            try
            {
                res = _storeService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowStoreWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowStoreWarning(res.ErrorMessage);
                return;
            }
            LastStoreMessageForTest = null;
            Reload();
        }

        private DialogResult OpenStoreDialog(StoreEditDialog dlg)
        {
            return StoreDialogOpenerForTest != null
                ? StoreDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void RefreshStoreAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectStoreRowByIdForTest(id);
        }

        private void ShowStoreWarning(string message)
        {
            LastStoreMessageForTest = message;
            if (!SuppressStorePopupForTest)
                Msg.Warning(this, message);
        }
    }
}
