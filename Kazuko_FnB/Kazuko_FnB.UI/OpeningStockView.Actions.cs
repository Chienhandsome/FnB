using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần nạp kho/dữ liệu tồn đầu, lọc cục bộ, sửa ô số trên lưới (Enter chuyển ô, 0 DataError),
    /// lưu một transaction, cảnh báo khi đóng còn thay đổi chưa lưu và xuất Excel cho <see cref="OpeningStockView"/>.
    /// </summary>
    internal sealed partial class OpeningStockView
    {
        private void WireEvents()
        {
            _btnSave.Click += delegate { SaveChanges(); };
            _btnExcel.Click += delegate { ExportGridToExcel(); };
            _btnRefresh.Click += delegate { ReloadAll(false); };

            _warehouse.SelectedIndexChanged += OnWarehouseChanged;
            _dtDate.ValueChanged += delegate { if (!_loading) UpdateDirtyState(); };
            _txtSearch.TextChanged += delegate { if (!_loading) ApplyLocalFilter(); };
            _chkOnlyEntered.CheckedChanged += delegate { if (!_loading) ApplyLocalFilter(); };

            _grid.RowPostPaint += DrawRowNumber;
            _grid.MouseDown += GridMouseDown;
            _grid.CellBeginEdit += GridCellBeginEdit;
            _grid.CellParsing += GridCellParsing;
            _grid.CellEndEdit += GridCellEndEdit;
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                NumericEditGridCell.HandleDirtyStateChanged(_grid, MarkNumericCellDirty);
            };
            _grid.DataError += GridDataError;

            _warehouse.KeyDown += OnKeyDown;
            _dtDate.Inner.KeyDown += OnKeyDown;
            _txtSearch.Inner.KeyDown += OnKeyDown;
            _grid.KeyDown += OnKeyDown;
            KeyDown += OnKeyDown;
        }

        public bool ConfirmClose()
        {
            return ConfirmDiscardUnsavedChanges();
        }

        public void ReloadAll(bool skipConfirm = false)
        {
            if (!skipConfirm && !ConfirmDiscardUnsavedChanges())
                return;

            _loading = true;
            try
            {
                LoadWarehouses();
            }
            catch (DataAccessException ex)
            {
                _allowedWarehouses = new List<WarehouseRow>();
                _allItems = new List<OpeningStockItemRow>();
                _visibleItems = new List<OpeningStockItemRow>();
                _grid.Rows.Clear();
                _empty.Visible = true;
                _empty.Text = "Không đọc được danh sách kho: " + ex.Message;
                _isDirty = false;
                return;
            }
            finally
            {
                _loading = false;
            }

            LoadCurrentWarehouseDocument();
        }

        private void LoadWarehouses()
        {
            string keepId = SelectedWarehouseId();
            _allowedWarehouses = _service.LoadAllowedWarehouses();

            _warehouse.Items.Clear();
            foreach (WarehouseRow w in _allowedWarehouses)
                _warehouse.Items.Add(new LookupItem { Id = w.Id, Name = w.Name });
            _warehouse.DisplayMember = "Name";

            if (_warehouse.Items.Count == 0)
            {
                _activeWarehouseId = "";
                return;
            }

            string targetId = keepId;
            if (string.IsNullOrEmpty(targetId)
                || !_allowedWarehouses.Exists(w => string.Equals(w.Id, targetId, StringComparison.Ordinal)))
            {
                targetId = _service.ResolveDefaultWarehouseId(_allowedWarehouses);
            }

            SelectWarehouseComboItem(targetId);
            _activeWarehouseId = SelectedWarehouseId();
        }

        private void OnWarehouseChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            string nextId = SelectedWarehouseId();
            if (string.Equals(nextId, _activeWarehouseId, StringComparison.Ordinal))
                return;
            if (!ConfirmDiscardUnsavedChanges())
            {
                _loading = true;
                try { SelectWarehouseComboItem(_activeWarehouseId); }
                finally { _loading = false; }
                return;
            }
            _activeWarehouseId = nextId;
            LoadCurrentWarehouseDocument();
        }

        private void LoadCurrentWarehouseDocument()
        {
            _loading = true;
            try
            {
                _warnBanner.Visible = false;
                _lblWarn.Text = "";

                if (!_service.CanView)
                {
                    _allItems = new List<OpeningStockItemRow>();
                    _visibleItems = new List<OpeningStockItemRow>();
                    _grid.Rows.Clear();
                    _empty.Text = OpeningStockConstants.NoPermission;
                    _empty.Visible = true;
                    _summary.Text = "0 mặt hàng";
                    _isDirty = false;
                    return;
                }

                string whId = SelectedWarehouseId();
                _activeWarehouseId = whId;
                if (string.IsNullOrEmpty(whId))
                {
                    _allItems = new List<OpeningStockItemRow>();
                    _visibleItems = new List<OpeningStockItemRow>();
                    _grid.Rows.Clear();
                    _empty.Text = OpeningStockConstants.EmptyNoWarehouse;
                    _empty.Visible = true;
                    _summary.Text = "0 mặt hàng";
                    _isDirty = false;
                    return;
                }

                OpeningStockDocument doc = _service.LoadDocument(whId);
                _allItems = doc.Items ?? new List<OpeningStockItemRow>();
                _loadedDate = doc.Date.Date;
                _dtDate.Value = _loadedDate;
                _isDirty = false;

                if (!string.IsNullOrEmpty(doc.Warning))
                {
                    _lblWarn.Text = doc.Warning;
                    _warnBanner.Visible = true;
                }
            }
            catch (DataAccessException ex)
            {
                _allItems = new List<OpeningStockItemRow>();
                _visibleItems = new List<OpeningStockItemRow>();
                _grid.Rows.Clear();
                _empty.Text = "Không đọc được tồn kho ban đầu: " + ex.Message;
                _empty.Visible = true;
                _summary.Text = "0 mặt hàng";
                _isDirty = false;
                return;
            }
            finally
            {
                _loading = false;
            }

            ApplyLocalFilter();
        }

        private void ApplyLocalFilter()
        {
            if (_grid.IsCurrentCellInEditMode)
                _grid.EndEdit();
            var filter = new OpeningStockFilter
            {
                SearchText = _txtSearch.Text,
                OnlyEntered = _chkOnlyEntered.Checked
            };
            _visibleItems = _allItems.FindAll(r => OpeningStockRules.MatchesFilter(r, filter));
            FillGrid();
        }

        // ------------------------------------------------------------------ Trạng thái thay đổi (dirty)

        private void MarkNumericCellDirty()
        {
            if (!_readOnly && !_loading)
                _isDirty = true;
        }

        private void UpdateDirtyState()
        {
            if (_readOnly)
            {
                _isDirty = false;
                return;
            }
            if (_dtDate.Value.Date != _loadedDate.Date)
            {
                _isDirty = true;
                return;
            }
            for (int i = 0; i < _allItems.Count; i++)
            {
                if (_allItems[i].IsDirty)
                {
                    _isDirty = true;
                    return;
                }
            }
            _isDirty = false;
        }

        // ------------------------------------------------------------------ Lưu & Xuất Excel

        internal bool SaveChanges()
        {
            LastSaveResultForTest = null;
            if (_readOnly)
            {
                ShowWarning(OpeningStockConstants.NoPermission);
                return false;
            }

            if (_grid.IsCurrentCellInEditMode)
                _grid.EndEdit();
            _dtDate.Commit();

            string whId = SelectedWarehouseId();
            if (string.IsNullOrEmpty(whId))
            {
                ShowWarning(OpeningStockConstants.WarehouseRequired);
                return false;
            }

            var req = new OpeningStockSaveRequest
            {
                WarehouseId = whId,
                Date = _dtDate.Value.Date,
                Items = _allItems
            };

            OpeningStockSaveResult res;
            try
            {
                res = _service.Save(req);
            }
            catch (DataAccessException ex)
            {
                res = OpeningStockSaveResult.Fail("Không lưu được tồn kho ban đầu: " + ex.Message);
            }

            LastSaveResultForTest = res;
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage ?? "Không thể lưu tồn kho ban đầu.");
                return false;
            }

            _isDirty = false;
            LoadCurrentWarehouseDocument();

            if (!string.IsNullOrEmpty(res.Warning))
                ShowWarning(res.Warning);
            else if (res.OrderDeleted)
                ShowInfo(OpeningStockConstants.SavedDeletedSuccess);
            else if (res.LineCount == 0 && string.IsNullOrEmpty(res.SavedOrderId))
                ShowInfo(OpeningStockConstants.NoChangesToSave);
            else
                ShowInfo(OpeningStockConstants.SavedSuccess);

            return true;
        }

        internal bool ExportGridToExcel()
        {
            if (!_service.CanView)
                return false;
            if (_grid.IsCurrentCellInEditMode)
                _grid.EndEdit();
            return GridExcelExporter.Export(
                this, _grid, "TonKhoBanDau", GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        // ------------------------------------------------------------------ Hỗ trợ & kiểm thử

        private bool ConfirmDiscardUnsavedChanges()
        {
            if (!_isDirty)
                return true;
            UnsavedPromptTriggeredForTest = true;
            if (ConfirmUnsavedOverrideForTest != null)
                return ConfirmUnsavedOverrideForTest();
            if (SuppressPopup)
                return true;
            return Msg.Confirm(this, OpeningStockConstants.UnsavedChangesConfirm);
        }

        private string SelectedWarehouseId()
        {
            var li = _warehouse.SelectedItem as LookupItem;
            return li == null ? "" : (li.Id ?? "");
        }

        private void SelectWarehouseComboItem(string warehouseId)
        {
            if (_warehouse.Items.Count == 0)
                return;
            int selIdx = 0;
            for (int i = 0; i < _warehouse.Items.Count; i++)
            {
                var li = _warehouse.Items[i] as LookupItem;
                if (li != null && string.Equals(li.Id, warehouseId, StringComparison.Ordinal))
                {
                    selIdx = i;
                    break;
                }
            }
            _warehouse.SelectedIndex = selIdx;
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ShowInfo(string message)
        {
            LastInfoMessageForTest = message;
            if (!SuppressPopup)
                Msg.Info(this, message);
        }

        internal DataGridViewRow FindRowByProductIdForTest(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return null;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var item = r.Tag as OpeningStockItemRow;
                if (item != null && string.Equals(item.ProductId, productId, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.L) || keyData == (Keys.Control | Keys.S))
            {
                if (_btnSave.Visible && _btnSave.Enabled)
                    SaveChanges();
                return true;
            }
            if (keyData == Keys.F5)
            {
                ReloadAll(false);
                return true;
            }
            if (keyData == Keys.F3)
            {
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ReloadAll(false);
                e.Handled = true;
            }
        }
    }
}
