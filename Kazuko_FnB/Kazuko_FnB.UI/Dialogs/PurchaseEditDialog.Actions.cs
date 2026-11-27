using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần nạp/tạo mới phiếu, tính tổng, lưu/xóa và phân quyền cho <see cref="PurchaseEditDialog"/>.</summary>
    internal sealed partial class PurchaseEditDialog
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
            _warehouse.SetItems(whItems);

            var suppliers = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            var employees = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            var banks = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            try
            {
                suppliers.AddRange(_formRepo.LoadAll("DNHACUNGCAP"));
                employees.AddRange(_formRepo.LoadAll("DNHANVIEN"));
                banks.AddRange(_formRepo.LoadAll("DTAIKHOANNGANHANG"));
            }
            catch (DataAccessException)
            {
                // Giữ danh sách trống nếu lỗi đọc lookup phụ.
            }
            _supplier.SetItems(suppliers);
            _employee.SetItems(employees);
            _bankAccount.SetItems(banks);

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

        private string ResolveDefaultWarehouseId()
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

        private void NewRecord()
        {
            _row = PurchaseRow.NewDefault();
            _row.WarehouseId = ResolveDefaultWarehouseId();
            try
            {
                _row.Name = _service.GenerateNumber(_row.Date);
            }
            catch (DataAccessException)
            {
                _row.Name = "";
            }
            _isNew = true;
            _lines = new List<PurchaseLine>();
            _discountByMoney = false;
            _totals = new PurchaseTotals();
            FillControls();
            SetTitle(_readOnly ? "PHIẾU NHẬP KHO - XEM CHI TIẾT" : "PHIẾU NHẬP KHO - THÊM MỚI");
        }

        private void LoadRecord(string purchaseId)
        {
            PurchaseRow loaded = null;
            try
            {
                loaded = _service.LoadById(purchaseId);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được phiếu nhập kho: " + ex.Message);
            }
            if (loaded == null)
            {
                _loadFailed = true;
                _row = PurchaseRow.NewDefault();
                _totals = new PurchaseTotals();
                return;
            }
            _row = loaded;
            _isNew = false;
            _lines = loaded.Lines ?? new List<PurchaseLine>();
            _discountByMoney = loaded.GiamTheoTien;
            _totals = new PurchaseTotals
            {
                TienHang = loaded.TienHang,
                TileGiamGia = loaded.TileGiamGia,
                TienGiamGia = loaded.TienGiamGia,
                TienThue = loaded.TienThue,
                TongCong = loaded.TongCong
            };
            FillControls();
            SetTitle(_readOnly ? "PHIẾU NHẬP KHO - XEM CHI TIẾT" : "PHIẾU NHẬP KHO - SỬA");
        }

        private void FillControls()
        {
            _loading = true;
            try
            {
                _dtNgay.Value = _row.Date == DateTime.MinValue ? DateTime.Today : _row.Date;
                _txtNumber.Text = _row.Name ?? "";
                _warehouse.SelectId(_row.WarehouseId);
                SelectOptionalLookup(_supplier, _row.SupplierId);
                SelectOptionalLookup(_employee, _row.EmployeeId);
                _txtDesc.Text = _row.Description ?? "";
                _txtNote.Text = _row.Note ?? "";

                ConfigureDiscountPercentBounds(_discountByMoney);
                SetNumericDisplay(_numTienHang, _totals.TienHang);
                SetNumericDisplay(_numTileGiamGia, _totals.TileGiamGia);
                SetNumericDisplay(_numTienGiamGia, _totals.TienGiamGia);
                SetNumericDisplay(_numTileThue, _row.TileThue);
                SetNumericDisplay(_numTienThue, _totals.TienThue);
                SetNumericDisplay(_numPhiVanChuyen, _row.PhiVanChuyen);
                SetNumericDisplay(_numTongCong, _totals.TongCong);
                SetNumericDisplay(_numPaid, _row.PaidAmount);

                bool isTransfer = _row.PaymentMethod == PayMethod.Transfer || _row.TransferAmount != 0m;
                _cboPayMethod.SelectedIndex = isTransfer ? 1 : 0;
                SelectOptionalLookup(_bankAccount, _row.BankAccountId);

                RebuildDetailGrid();
            }
            finally
            {
                _loading = false;
            }
            UpdatePaymentControlsState();
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
            _warehouse.Enabled = !_readOnly;
            _supplier.Enabled = !_readOnly;
            _employee.Enabled = !_readOnly;
            _txtDesc.ReadOnly = _readOnly;
            _txtNote.ReadOnly = _readOnly;

            _numTileGiamGia.Enabled = !_readOnly;
            _numTienGiamGia.Enabled = !_readOnly;
            _numTileThue.Enabled = !_readOnly;
            _numPhiVanChuyen.Enabled = !_readOnly;
            _numPaid.Enabled = !_readOnly;
            UpdatePaymentControlsState();

            _btnAddProduct.Enabled = !_readOnly;
            _numQty.Enabled = !_readOnly;
            _btnDeleteLine.Enabled = !_readOnly;
            _detailGrid.ReadOnly = _readOnly;

            // Ẩn toàn bộ cột/khối giá khi thiếu quyền Xem giá nhập.
            _moneyBox.Visible = _canViewBuyPrice;
            if (_productGrid.Columns["p_price"] != null)
                _productGrid.Columns["p_price"].Visible = _canViewBuyPrice;
            foreach (string col in new[] { "d_price", "d_disc", "d_amount" })
            {
                if (_detailGrid.Columns[col] != null)
                    _detailGrid.Columns[col].Visible = _canViewBuyPrice;
            }

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;
            _btnDelete.Visible = !_readOnly && !_isNew && _service.CanRemove;

            SubtitleText = _readOnly
                ? "Chế độ chỉ xem (không có quyền sửa)"
                : ShortcutsHint;
            UpdateAddUnitOptions();
        }

        private void UpdatePaymentControlsState()
        {
            bool hasPayment = _numPaid.Value > 0m;
            bool isTransfer = _cboPayMethod.SelectedIndex == 1;
            _cboPayMethod.Enabled = !_readOnly && hasPayment;
            _bankAccount.Enabled = !_readOnly && hasPayment && isTransfer;
        }

        private void RecalcTotals()
        {
            if (_loading)
                return;
            PurchaseTotals t = PurchaseRules.CalcTotals(
                _lines,
                _numTileGiamGia.Value,
                _numTienGiamGia.Value,
                _discountByMoney,
                _numTileThue.Value,
                _numPhiVanChuyen.Value);
            _totals = t;

            _recalc = true;
            try
            {
                ConfigureDiscountPercentBounds(_discountByMoney);
                SetNumericDisplay(_numTienHang, t.TienHang);
                SetNumericDisplay(_numTileGiamGia, t.TileGiamGia);
                SetNumericDisplay(_numTienGiamGia, t.TienGiamGia);
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
            _row.WarehouseId = _warehouse.SelectedId;
            string suppId = _supplier.SelectedId;
            _row.SupplierId = string.IsNullOrEmpty(suppId) ? null : suppId;
            string empId = _employee.SelectedId;
            _row.EmployeeId = string.IsNullOrEmpty(empId) ? null : empId;
            _row.Description = (_txtDesc.Text ?? "").Trim();
            _row.Note = (_txtNote.Text ?? "").Trim();

            _row.TienHang = _totals.TienHang;
            _row.TileGiamGia = _totals.TileGiamGia;
            _row.TienGiamGia = _totals.TienGiamGia;
            _row.GiamTheoTien = _discountByMoney;
            _row.TileThue = _numTileThue.Value;
            _row.TienThue = _totals.TienThue;
            _row.PhiVanChuyen = _numPhiVanChuyen.Value;
            _row.TongCong = _totals.TongCong;

            _row.PaidAmount = _numPaid.Value;
            _row.PaymentMethod = _cboPayMethod.SelectedIndex == 1 ? PayMethod.Transfer : PayMethod.Cash;
            string bankId = _bankAccount.SelectedId;
            _row.BankAccountId = string.IsNullOrEmpty(bankId) ? null : bankId;
            _row.Lines = new List<PurchaseLine>(_lines);
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

            PurchaseSaveResult res;
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
                else if (res.ErrorMessage == PurchaseConstants.WarehouseRequired)
                {
                    _warehouse.Focus();
                }
                else if (res.ErrorMessage == PurchaseConstants.SupplierRequired)
                {
                    _supplier.Focus();
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
                SetTitle(_readOnly ? "PHIẾU NHẬP KHO - XEM CHI TIẾT" : "PHIẾU NHẬP KHO - SỬA");
                ApplyPermissions();
            }
            _original = Snapshot();
            UpdateButtons();
            if (!string.IsNullOrEmpty(res.Warning))
                ShowWarning(res.Warning);
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
            if (!SuppressPopup && !Msg.Confirm(this, PurchaseConstants.DeleteConfirm))
                return;

            PurchaseDeleteResult res;
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
            if (!string.IsNullOrEmpty(res.Warning))
                ShowWarning(res.Warning);
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
            sb.Append(_warehouse.SelectedId ?? "").Append('|');
            sb.Append(_supplier.SelectedId ?? "").Append('|');
            sb.Append(_employee.SelectedId ?? "").Append('|');
            sb.Append(_txtDesc.Text).Append('|');
            sb.Append(_txtNote.Text).Append('|');
            sb.Append(_numTileGiamGia.Value).Append('|');
            sb.Append(_numTienGiamGia.Value).Append('|');
            sb.Append(_discountByMoney).Append('|');
            sb.Append(_numTileThue.Value).Append('|');
            sb.Append(_numPhiVanChuyen.Value).Append('|');
            sb.Append(_numPaid.Value).Append('|');
            sb.Append(_cboPayMethod.SelectedIndex).Append('|');
            sb.Append(_bankAccount.SelectedId ?? "").Append('|');
            foreach (PurchaseLine l in _lines)
            {
                sb.Append(l.ProductId).Append(':').Append(l.UnitId ?? "").Append(':');
                sb.Append(l.RawQuantity).Append(':').Append(l.UnitPrice).Append(':');
                sb.Append(l.DiscountPct).Append(':').Append(l.Amount).Append(':').Append(l.Note ?? "").Append(';');
            }
            return sb.ToString();
        }
    }
}
