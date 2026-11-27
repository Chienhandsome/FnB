using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần nạp/tạo mới phiếu, lưu/xóa, khóa kho khi có dòng, nút Mặt hàng chưa kiểm kê và phân quyền cho <see cref="StockTakeEditDialog"/>.</summary>
    internal sealed partial class StockTakeEditDialog
    {
        private void LoadLookups()
        {
            try
            {
                _warehouses = _service.LoadAllowedWarehouses();
            }
            catch (DataAccessException)
            {
                _warehouses = new List<WarehouseRow>();
            }
            var whItems = new List<LookupItem>();
            foreach (WarehouseRow w in _warehouses)
                whItems.Add(new LookupItem { Id = w.Id, Name = w.Name });
            _warehouse.SetItems(whItems);

            try
            {
                _products = _service.LoadProducts();
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được danh sách mặt hàng: " + ex.Message);
            }
            RebuildProductGrid();
        }

        private void NewRecord()
        {
            _row = StockTakeRow.NewDefault();
            try
            {
                _row.WarehouseId = _service.ResolveDefaultWarehouseId(_warehouses);
            }
            catch (DataAccessException)
            {
                _row.WarehouseId = _warehouses.Count > 0 ? _warehouses[0].Id : null;
            }
            try
            {
                _row.Name = _service.GenerateNumber(_row.Date);
            }
            catch (DataAccessException)
            {
                _row.Name = "";
            }
            _isNew = true;
            _savedHadLines = false;
            _lines = new List<StockTakeLine>();
            FillControls();
            SetTitle(_readOnly ? TitlePrefix + " - XEM CHI TIẾT" : TitlePrefix + " - THÊM MỚI");
        }

        private void LoadRecord(string documentId)
        {
            StockTakeRow loaded = null;
            try
            {
                loaded = _service.LoadById(documentId);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được phiếu kiểm kê: " + ex.Message);
            }
            if (loaded == null)
            {
                _loadFailed = true;
                _row = StockTakeRow.NewDefault();
                _savedHadLines = false;
                return;
            }
            _row = loaded;
            _isNew = false;
            _lines = loaded.Lines ?? new List<StockTakeLine>();
            _savedHadLines = _lines.Count > 0;
            FillControls();
            SetTitle(_readOnly ? TitlePrefix + " - XEM CHI TIẾT" : TitlePrefix + " - SỬA");
        }

        private void FillControls()
        {
            _loading = true;
            try
            {
                _dtNgay.Value = _row.Date == DateTime.MinValue ? DateTime.Today : _row.Date;
                _txtNumber.Text = _row.Name ?? "";
                if (!string.IsNullOrEmpty(_row.WarehouseId))
                    _warehouse.SelectId(_row.WarehouseId);
                else
                    _warehouse.SelectedIndex = -1;
                _txtDesc.Text = _row.Description ?? "";
                _txtNote.Text = _row.Note ?? "";
                RebuildDetailGrid();
            }
            finally
            {
                _loading = false;
            }
        }

        private void UpdateWarehouseLock()
        {
            // Spec §2 & §4: khóa không đổi kho kiểm kê khi đã có dòng hoặc phiếu đã lưu có dòng.
            _warehouse.Enabled = !_readOnly && _lines.Count == 0 && (_isNew || !_savedHadLines);
        }

        private void ApplyPermissions()
        {
            _dtNgay.Enabled = !_readOnly && _service.CanChangeDate;
            _txtNumber.ReadOnly = _readOnly;
            UpdateWarehouseLock();
            _txtDesc.ReadOnly = _readOnly;
            _txtNote.ReadOnly = _readOnly;

            _btnAddProduct.Enabled = !_readOnly;
            _numQty.Enabled = !_readOnly;
            _btnDeleteLine.Enabled = !_readOnly;
            _detailGrid.ReadOnly = _readOnly;

            // Spec §5: nút "Mặt hàng chưa kiểm kê" chỉ hiện khi phiếu đã lưu.
            _btnUncounted.Visible = !_isNew && _row != null && !string.IsNullOrEmpty(_row.Id);
            _btnUncounted.Enabled = _btnUncounted.Visible;

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;
            _btnDelete.Visible = !_readOnly && !_isNew && _service.CanRemove;

            SubtitleText = _readOnly
                ? "Chế độ chỉ xem (không có quyền sửa)"
                : ShortcutsHint;
            UpdateAddUnitOptions();
        }

        private void CollectRow()
        {
            _row.Date = _dtNgay.Value.Date;
            _row.Name = (_txtNumber.Text ?? "").Trim();
            _row.WarehouseId = _warehouse.SelectedId;
            _row.Description = (_txtDesc.Text ?? "").Trim();
            _row.Note = (_txtNote.Text ?? "").Trim();
            _row.Lines = new List<StockTakeLine>(_lines);
        }

        internal bool DoSave()
        {
            if (_readOnly)
            {
                ShowWarning(PurchaseConstants.NoPermission);
                return false;
            }
            _detailGrid.EndEdit();
            CollectRow();

            StockTakeSaveResult res;
            try
            {
                res = _service.Save(_row);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không ghi được dữ liệu: " + ex.Message);
                return false;
            }

            if (!res.Ok)
            {
                if (res.SystemStockChanged)
                {
                    // Tồn hệ thống đã đổi trong transaction -> nạp lại số mới lên lưới để người dùng xem và xác nhận.
                    RebuildDetailGrid();
                    UpdateButtons();
                }
                ShowWarning(res.ErrorMessage);
                if (res.ErrorMessage == PurchaseConstants.NumberRequired
                    || (res.ErrorMessage != null && res.ErrorMessage.IndexOf("đã tồn tại", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _txtNumber.Focus();
                    _txtNumber.SelectAll();
                }
                else if (res.ErrorMessage == StockTakeConstants.WarehouseRequired)
                {
                    _warehouse.Focus();
                }
                return false;
            }

            LastWarningMessageForTest = null;
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            _row.WarehouseName = _warehouse.Selected == null ? (_row.WarehouseName ?? "") : _warehouse.Selected.Name;
            AnySaved = true;
            _savedHadLines = _lines.Count > 0;

            bool wasNew = _isNew;
            _isNew = false;
            if (wasNew)
            {
                _readOnly = !_service.CanEdit;
                SetTitle(_readOnly ? TitlePrefix + " - XEM CHI TIẾT" : TitlePrefix + " - SỬA");
            }
            ApplyPermissions();
            _original = Snapshot();
            UpdateButtons();
            return true;
        }

        private void SaveStay() { DoSave(); }

        private void SaveAndNew()
        {
            if (!DoSave()) return;
            NewRecord();
            _original = Snapshot();
            ApplyPermissions();
            UpdateButtons();
        }

        private void SaveAndClose()
        {
            if (!DoSave()) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DeleteCurrent()
        {
            if (_isNew || _readOnly || !_service.CanRemove)
                return;
            if (!SuppressPopup && !Msg.Confirm(this, StockTakeConstants.DeleteConfirm))
                return;

            StockTakeDeleteResult res;
            try
            {
                res = _service.Delete(_row.Id);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }
            Deleted = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OpenUncountedDialog()
        {
            if (_isNew || _row == null || string.IsNullOrEmpty(_row.Id))
                return;
            List<StockTakeUncountedItem> items;
            try
            {
                items = _service.LoadUncountedItems(_row.Id);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được mặt hàng chưa kiểm kê: " + ex.Message);
                return;
            }

            string whName = _warehouse.Selected != null ? _warehouse.Selected.Name : (_row.WarehouseName ?? "");
            using (var dlg = new UncountedStockDialog(whName, _row.Name ?? "", items))
            {
                if (UncountedDialogOpenerForTest != null)
                    UncountedDialogOpenerForTest(dlg);
                else
                    dlg.ShowDialog(this);
            }
        }

        private void OnDirtyChanged()
        {
            if (!_loading)
                UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (_readOnly)
                return;
            bool dirty = Dirty;
            _btnSave.Enabled = dirty;
            _btnSaveNew.Enabled = dirty && _service.CanAdd;
            _btnSaveClose.Enabled = dirty;
            _btnDelete.Visible = !_isNew && _service.CanRemove;
            _btnDelete.Enabled = !_isNew && _service.CanRemove;
        }

        private bool Dirty
        {
            get { return !_readOnly && (_original == null || Snapshot() != _original || _detailGrid.IsCurrentCellDirty); }
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_txtNumber.Text).Append('|');
            sb.Append(_dtNgay.Value.ToString("yyyyMMdd")).Append('|');
            sb.Append(_warehouse.SelectedId ?? "").Append('|');
            sb.Append(_txtDesc.Text).Append('|');
            sb.Append(_txtNote.Text).Append('|');
            foreach (StockTakeLine l in _lines)
            {
                sb.Append(l.ProductId).Append(':').Append(l.UnitId ?? "").Append(':');
                sb.Append(l.SystemQuantity).Append(':').Append(l.RawActualQuantity).Append(':');
                sb.Append(l.Note ?? "").Append(';');
            }
            return sb.ToString();
        }
    }
}
