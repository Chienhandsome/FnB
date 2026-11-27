using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần nạp/tạo mới phiếu, tính tổng, lưu/xóa và phân quyền cho <see cref="StockDocumentEditDialog"/>.</summary>
    internal sealed partial class StockDocumentEditDialog
    {
        private void LoadLookups()
        {
            _allowMultipleRowsPerItem = SystemConfig.NhapMotMatHangNhieuLanTrongPhieu;

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
            _sourceWarehouse.SetItems(whItems);
            _destWarehouse.SetItems(new List<LookupItem>(whItems));

            var suppliers = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            var employees = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            try
            {
                if (_kind == StockDocumentKind.Export)
                    suppliers.AddRange(_formRepo.LoadAll("DNHACUNGCAP"));
                employees.AddRange(_formRepo.LoadAll("DNHANVIEN"));
            }
            catch (DataAccessException)
            {
                // Giữ danh sách trống nếu lỗi đọc lookup phụ.
            }
            _supplier.SetItems(suppliers);
            _exportEmployee.SetItems(employees);
            _importEmployee.SetItems(new List<LookupItem>(employees));

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

        private string ResolveDefaultSourceWarehouseId()
        {
            if (_session != null && _session.CurrentStore != null && !string.IsNullOrEmpty(_session.CurrentStore.Id))
            {
                WarehouseRow match = _warehouses.Find(w =>
                    string.Equals(w.StoreId, _session.CurrentStore.Id, StringComparison.Ordinal));
                if (match != null)
                    return match.Id;
            }
            return _warehouses.Count > 0 ? _warehouses[0].Id : null;
        }

        private string ResolveDefaultDestWarehouseId(string sourceId)
        {
            if (_warehouses.Count < 2)
                return null;
            WarehouseRow other = _warehouses.Find(w => !string.Equals(w.Id, sourceId, StringComparison.Ordinal));
            return other == null ? null : other.Id;
        }

        private void NewRecord()
        {
            _row = StockDocumentRow.NewDefault(_kind);
            _row.SourceWarehouseId = ResolveDefaultSourceWarehouseId();
            if (_kind == StockDocumentKind.Transfer)
                _row.DestWarehouseId = ResolveDefaultDestWarehouseId(_row.SourceWarehouseId);
            try
            {
                _row.Name = _service.GenerateNumber(_row.Date);
            }
            catch (DataAccessException)
            {
                _row.Name = "";
            }
            _isNew = true;
            _lines = new List<StockDocumentLine>();
            _discountByMoney = false;
            _totals = new PurchaseTotals();
            FillControls();
            SetTitle(_readOnly ? TitlePrefixOf(_kind) + " - XEM CHI TIẾT" : TitlePrefixOf(_kind) + " - THÊM MỚI");
        }

        private void LoadRecord(string documentId)
        {
            StockDocumentRow loaded = null;
            try
            {
                loaded = _service.LoadById(documentId);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được phiếu: " + ex.Message);
            }
            if (loaded == null)
            {
                _loadFailed = true;
                _row = StockDocumentRow.NewDefault(_kind);
                _totals = new PurchaseTotals();
                return;
            }
            _row = loaded;
            _isNew = false;
            _lines = loaded.Lines ?? new List<StockDocumentLine>();
            _discountByMoney = loaded.GiamTheoTien;
            _totals = new PurchaseTotals
            {
                TienHang = loaded.TienHang,
                TileGiamGia = loaded.TileGiamGia,
                TienGiamGia = loaded.TienGiamGia,
                TienThue = _kind == StockDocumentKind.Export ? 0m : loaded.TienThue,
                TongCong = loaded.TongCong
            };
            FillControls();
            SetTitle(_readOnly ? TitlePrefixOf(_kind) + " - XEM CHI TIẾT" : TitlePrefixOf(_kind) + " - SỬA");
        }

        private void FillControls()
        {
            _loading = true;
            try
            {
                _dtNgay.Value = _row.Date == DateTime.MinValue ? DateTime.Today : _row.Date;
                _txtNumber.Text = _row.Name ?? "";
                _sourceWarehouse.SelectId(_row.SourceWarehouseId);
                if (_kind == StockDocumentKind.Transfer)
                {
                    if (!string.IsNullOrEmpty(_row.DestWarehouseId))
                        _destWarehouse.SelectId(_row.DestWarehouseId);
                    else
                        _destWarehouse.SelectedIndex = -1;
                    SelectOptionalLookup(_importEmployee, _row.ImportEmployeeId);
                }
                else
                {
                    SelectOptionalLookup(_supplier, _row.SupplierId);
                }
                SelectOptionalLookup(_exportEmployee, _row.ExportEmployeeId);
                _txtDesc.Text = _row.Description ?? "";
                _txtNote.Text = _row.Note ?? "";

                ConfigureDiscountPercentBounds(_discountByMoney);
                SetNumericDisplay(_numTienHang, _totals.TienHang);
                SetNumericDisplay(_numTileGiamGia, _totals.TileGiamGia);
                SetNumericDisplay(_numTienGiamGia, _totals.TienGiamGia);
                SetNumericDisplay(_numTileThue, _kind == StockDocumentKind.Export ? 0m : _row.TileThue);
                SetNumericDisplay(_numTienThue, _kind == StockDocumentKind.Export ? 0m : _totals.TienThue);
                SetNumericDisplay(_numPhiVanChuyen, _kind == StockDocumentKind.Export ? 0m : _row.PhiVanChuyen);
                SetNumericDisplay(_numTongCong, _totals.TongCong);

                RebuildDetailGrid();
            }
            finally
            {
                _loading = false;
            }
        }

        private static void SelectOptionalLookup(LookupCombo combo, string id)
        {
            if (string.IsNullOrEmpty(id))
                combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
            else
                combo.SelectId(id);
        }

        private void ApplyPermissions()
        {
            _dtNgay.Enabled = !_readOnly && _service.CanChangeDate;
            _txtNumber.ReadOnly = _readOnly;
            _sourceWarehouse.Enabled = !_readOnly;
            _destWarehouse.Enabled = !_readOnly;
            _supplier.Enabled = !_readOnly;
            _exportEmployee.Enabled = !_readOnly;
            _importEmployee.Enabled = !_readOnly;
            _txtDesc.ReadOnly = _readOnly;
            _txtNote.ReadOnly = _readOnly;

            _numTileGiamGia.Enabled = !_readOnly;
            _numTienGiamGia.Enabled = !_readOnly;
            _numTileThue.Enabled = !_readOnly && _kind == StockDocumentKind.Transfer;
            _numPhiVanChuyen.Enabled = !_readOnly && _kind == StockDocumentKind.Transfer;

            _btnAddProduct.Enabled = !_readOnly;
            _numQty.Enabled = !_readOnly;
            _btnDeleteLine.Enabled = !_readOnly;
            _detailGrid.ReadOnly = _readOnly;

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;
            _btnDelete.Visible = !_readOnly && !_isNew && _service.CanRemove;

            SubtitleText = _readOnly
                ? "Chế độ chỉ xem (không có quyền sửa)"
                : ShortcutsHint;
            UpdateAddUnitOptions();
        }

        private void RecalcTotals()
        {
            if (_loading)
                return;
            PurchaseTotals t = StockDocumentRules.CalcTotals(
                _kind,
                _lines,
                _numTileGiamGia.Value,
                _numTienGiamGia.Value,
                _discountByMoney,
                _kind == StockDocumentKind.Transfer ? _numTileThue.Value : 0m,
                _kind == StockDocumentKind.Transfer ? _numPhiVanChuyen.Value : 0m);
            _totals = t;

            _recalc = true;
            try
            {
                ConfigureDiscountPercentBounds(_discountByMoney);
                SetNumericDisplay(_numTienHang, t.TienHang);
                SetNumericDisplay(_numTileGiamGia, t.TileGiamGia);
                SetNumericDisplay(_numTienGiamGia, t.TienGiamGia);
                if (_kind == StockDocumentKind.Transfer)
                    SetNumericDisplay(_numTienThue, t.TienThue);
                SetNumericDisplay(_numTongCong, t.TongCong);
            }
            finally
            {
                _recalc = false;
            }
        }

        private void ConfigureDiscountPercentBounds(bool byMoney)
        {
            if (byMoney)
            {
                _numTileGiamGia.Minimum = -999999999999m;
                _numTileGiamGia.Maximum = 999999999999m;
            }
            else
            {
                _numTileGiamGia.Minimum = 0m;
                _numTileGiamGia.Maximum = 100m;
            }
        }

        private static void SetNumericDisplay(NumericUpDown num, decimal value)
        {
            if (value < num.Minimum) num.Value = num.Minimum;
            else if (value > num.Maximum) num.Value = num.Maximum;
            else num.Value = value;
        }

        private void CollectRow()
        {
            _row.Date = _dtNgay.Value.Date;
            _row.Name = (_txtNumber.Text ?? "").Trim();
            _row.SourceWarehouseId = _sourceWarehouse.SelectedId;
            _row.DestWarehouseId = _kind == StockDocumentKind.Transfer ? _destWarehouse.SelectedId : null;
            string suppId = _supplier.SelectedId;
            _row.SupplierId = _kind == StockDocumentKind.Export && !string.IsNullOrEmpty(suppId) ? suppId : null;
            string expEmp = _exportEmployee.SelectedId;
            _row.ExportEmployeeId = string.IsNullOrEmpty(expEmp) ? null : expEmp;
            string impEmp = _importEmployee.SelectedId;
            _row.ImportEmployeeId = _kind == StockDocumentKind.Transfer && !string.IsNullOrEmpty(impEmp) ? impEmp : null;
            _row.Description = (_txtDesc.Text ?? "").Trim();
            _row.Note = (_txtNote.Text ?? "").Trim();

            _row.TienHang = _totals.TienHang;
            _row.TileGiamGia = _totals.TileGiamGia;
            _row.TienGiamGia = _totals.TienGiamGia;
            _row.GiamTheoTien = _discountByMoney;
            _row.TileThue = _kind == StockDocumentKind.Transfer ? _numTileThue.Value : 0m;
            _row.TienThue = _kind == StockDocumentKind.Transfer ? _totals.TienThue : 0m;
            _row.PhiVanChuyen = _kind == StockDocumentKind.Transfer ? _numPhiVanChuyen.Value : 0m;
            _row.TongCong = _totals.TongCong;
            _row.Lines = new List<StockDocumentLine>(_lines);
        }

        internal bool DoSave()
        {
            if (_readOnly)
            {
                ShowWarning(PurchaseConstants.NoPermission);
                return false;
            }
            _detailGrid.EndEdit();
            RecalcTotals();
            CollectRow();

            StockDocumentSaveResult res;
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
                ShowWarning(res.ErrorMessage);
                if (res.ErrorMessage == PurchaseConstants.NumberRequired
                    || (res.ErrorMessage != null && res.ErrorMessage.IndexOf("đã tồn tại", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    _txtNumber.Focus();
                    _txtNumber.SelectAll();
                }
                else if (res.ErrorMessage == StockDocumentConstants.SourceWarehouseRequired)
                {
                    _sourceWarehouse.Focus();
                }
                else if (res.ErrorMessage == StockDocumentConstants.DestWarehouseRequired
                    || res.ErrorMessage == StockDocumentConstants.SameWarehouseBlocked)
                {
                    _destWarehouse.Focus();
                }
                return false;
            }

            LastWarningMessageForTest = null;
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;
            bool wasNew = _isNew;
            _isNew = false;
            if (wasNew)
            {
                _readOnly = !_service.CanEdit;
                SetTitle(_readOnly ? TitlePrefixOf(_kind) + " - XEM CHI TIẾT" : TitlePrefixOf(_kind) + " - SỬA");
                ApplyPermissions();
            }
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
            if (!SuppressPopup && !Msg.Confirm(this, StockDocumentConstants.DeleteConfirmOf(_kind)))
                return;

            StockDocumentDeleteResult res;
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
            sb.Append(_sourceWarehouse.SelectedId ?? "").Append('|');
            sb.Append(_destWarehouse.SelectedId ?? "").Append('|');
            sb.Append(_supplier.SelectedId ?? "").Append('|');
            sb.Append(_exportEmployee.SelectedId ?? "").Append('|');
            sb.Append(_importEmployee.SelectedId ?? "").Append('|');
            sb.Append(_txtDesc.Text).Append('|');
            sb.Append(_txtNote.Text).Append('|');
            sb.Append(_numTileGiamGia.Value).Append('|');
            sb.Append(_numTienGiamGia.Value).Append('|');
            sb.Append(_discountByMoney).Append('|');
            sb.Append(_numTileThue.Value).Append('|');
            sb.Append(_numPhiVanChuyen.Value).Append('|');
            foreach (StockDocumentLine l in _lines)
            {
                sb.Append(l.ProductId).Append(':').Append(l.UnitId ?? "").Append(':');
                sb.Append(l.RawQuantity).Append(':').Append(l.UnitPrice).Append(':');
                sb.Append(l.Amount).Append(':').Append(l.Note ?? "").Append(';');
            }
            return sb.ToString();
        }
    }
}
