using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    internal sealed partial class ServiceOrderView
    {
        private void WireEvents()
        {
            _tableMap.BindLoaders(() => _service.LoadAreas(), () => _service.LoadTables());
            _tableMap.TableSelected += delegate(object s, ServiceTableInfo t) { OnTableSelected(t); };
            _tableMap.TableDoubleClicked += delegate(object s, ServiceTableInfo t) { OnTableDoubleClicked(t); };
            _tableMap.OpenTableRequested += delegate(object s, ServiceTableInfo t)
            {
                OnTableSelected(t);
                DoOpenTable();
            };
            _tableMap.PayRequested += delegate(object s, ServiceTableInfo t)
            {
                OnTableSelected(t);
                DoPay();
            };
            _tableMap.GroupPayRequested += delegate(object s, ServiceTableInfo t)
            {
                OnTableSelected(t);
                DoGroupPay();
            };
            _tableMap.MoveTableRequested += delegate(object s, ServiceTableInfo t)
            {
                OnTableSelected(t);
                DoMoveTable();
            };
            _tableMap.MergeTableRequested += delegate(object s, ServiceTableInfo t)
            {
                OnTableSelected(t);
                DoMergeTable();
            };
            _tableMap.CancelBillRequested += delegate(object s, ServiceTableInfo t)
            {
                OnTableSelected(t);
                DoCancelBill();
            };
            _tableMap.UnsupportedActionRequested += delegate(object s, string name) { ShowNotSupported(name); };

            _menuPicker.ItemActivated += delegate(object s, ServiceMenuItem item) { DoAddItem(item); };

            _btnStart.Click += delegate { DoOpenTable(); };
            _btnAdd.Click += delegate { DoAddItem(_menuPicker.SelectedItem); };
            _btnPlus.Click += delegate { DoIncreaseSelected(); };
            _btnMinus.Click += delegate { DoReduceSelected(1m, true); };
            _btnSetQty.Click += delegate { DoSetQtySelected(); };
            _btnPrice.Click += delegate { DoChangePriceSelected(); };
            _btnNote.Click += delegate { DoChangeNoteSelected(); };
            _btnLineDiscount.Click += delegate { DoChangeLineDiscountSelected(); };
            _btnReduce.Click += delegate { DoReduceSelected(_numAddQty.Value, false); };
            _btnDelete.Click += delegate { DoDeleteSelected(); };
            _btnGroupDiscount.Click += delegate { DoApplyGroupDiscount(); };

            // Các chức năng patch (c): Chuyển bàn, Gộp bàn, Chuyển mặt hàng, Hủy bill (§3.8).
            _btnMoveTable.Click += delegate { DoMoveTable(); };
            _btnMergeTable.Click += delegate { DoMergeTable(); };
            _btnTransferItem.Click += delegate { DoTransferItem(); };
            _btnCancelBill.Click += delegate { DoCancelBill(); };
            // Các chức năng thuộc patch (d) / thống kê -> hiện nút nhưng báo "chưa hỗ trợ" (§7).
            _btnKitchenPrint.Click += delegate { ShowNotSupported("In chế biến"); };
            _btnPay.Click += delegate { DoPay(); };
            _btnGroupPay.Click += delegate { DoGroupPay(); };
            _btnStats.Click += delegate { ShowNotSupported("Thống kê"); };
            _btnReprint.Click += delegate { ShowNotSupported("In lại bill"); };

            _detailGrid.SelectionChanged += delegate { if (!_updatingUi) UpdateControlStates(); };
            _detailGrid.CellFormatting += DetailGrid_CellFormatting;

            _numTileGiam.ValueChanged += delegate { if (!_updatingUi) CommitBillFinancials(false, _currentBill != null && _currentBill.PhiDichVuTheoTien); };
            _numTienGiam.ValueChanged += delegate { if (!_updatingUi) CommitBillFinancials(true, _currentBill != null && _currentBill.PhiDichVuTheoTien); };
            _numTilePhi.ValueChanged += delegate { if (!_updatingUi) CommitBillFinancials(_currentBill != null && _currentBill.GiamTheoTien, false); };
            _numPhiDichVu.ValueChanged += delegate { if (!_updatingUi) CommitBillFinancials(_currentBill != null && _currentBill.GiamTheoTien, true); };
            _numTileThue.ValueChanged += delegate { if (!_updatingUi) CommitBillFinancials(_currentBill != null && _currentBill.GiamTheoTien, _currentBill != null && _currentBill.PhiDichVuTheoTien); };

            _cboCustomer.ValueChanged += delegate { if (!_updatingUi) CommitBillMeta(); };
            _numGuests.ValueChanged += delegate { if (!_updatingUi) CommitBillMeta(); };
            _txtBillNote.Leave += delegate { if (!_updatingUi) CommitBillMeta(); };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F2:
                    DoChangeNoteSelected();
                    return true;
                case Keys.F3:
                    _menuPicker.FocusSearch();
                    return true;
                case Keys.F8:
                    ShowNotSupported("In lại bill");
                    return true;
                case Keys.F9:
                    ShowNotSupported("Thống kê");
                    return true;
                case Keys.F10:
                    ShowNotSupported("In chế biến");
                    return true;
                case Keys.F11:
                    if (_canViewPrice)
                        DoPay();
                    return true;
                case Keys.F12:
                    _cboCustomer.Focus();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void ReloadAll()
        {
            _tableMap.ReloadAll();
            _menuPicker.SetData(_service.LoadMenuGroups(), _service.LoadMenuItems());

            var customers = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            foreach (CustomerLookupItem c in _service.LoadCustomers())
                customers.Add(new LookupItem { Id = c.Id, Name = c.Name });
            _updatingUi = true;
            try
            {
                string currentCust = _currentBill != null ? _currentBill.CustomerId : "";
                _cboCustomer.SetItems(customers);
                _cboCustomer.SelectId(currentCust ?? "");
            }
            finally { _updatingUi = false; }

            if (_currentTable != null)
            {
                ServiceTableInfo fresh = _tableMap.FindTable(_currentTable.Id);
                if (fresh != null)
                    _currentTable = fresh;
            }
        }

        private void OnTableSelected(ServiceTableInfo table)
        {
            string prevId = _currentTable != null ? _currentTable.Id : null;
            ServiceOpResult res = _service.SelectTable(table != null ? table.Id : null, prevId);
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                _tableMap.SelectTableVisual(prevId);
                return;
            }

            _currentTable = table;
            _currentBill = res.Bill;
            _menuPicker.SetPriceLevel(table != null ? table.AreaPriceLevelId : "1");

            if (_currentBill != null && SystemConfig.HeThongChayNhieuMayTram)
                _lockRenewTimer.Start();
            else
                _lockRenewTimer.Stop();

            RenderCurrentBill();
        }

        private void OnTableDoubleClicked(ServiceTableInfo table)
        {
            OnTableSelected(table);
            if (_currentTable != null && _currentBill == null)
                DoOpenTable();
        }

        private void DoOpenTable()
        {
            if (_currentTable == null)
            {
                ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }

            ServiceOpResult res = _service.OpenTable(_currentTable.Id, false);
            if (res.NeedsConfirmation)
            {
                if (!SuppressPopup && !Msg.Confirm(this, res.ErrorMessage))
                    return;
                res = _service.OpenTable(_currentTable.Id, true);
            }
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }

            _currentBill = res.Bill;
            if (SystemConfig.HeThongChayNhieuMayTram)
                _lockRenewTimer.Start();
            _tableMap.ReloadAll();
            _tableMap.SelectTableVisual(_currentTable.Id);
            RenderCurrentBill();
        }

        private void DoAddItem(ServiceMenuItem item)
        {
            if (_currentBill == null || _currentTable == null)
            {
                ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }
            if (item == null)
            {
                ShowWarning(ServiceOrderConstants.SelectProductFirst);
                return;
            }

            decimal qty = _numAddQty.Value;
            decimal? customPrice = null;
            string customName = null;
            string customUnitId = null;

            if (!SuppressPopup)
            {
                if (item.ProductTypeId == ProductRules.TypeOpen)
                {
                    using (var dlg = new ServiceOpenItemDialog(item, _service.LoadUnits()))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK)
                            return;
                        customName = dlg.ItemName;
                        customUnitId = dlg.UnitId;
                        customPrice = dlg.UnitPrice;
                    }
                }
                else if (item.PriceByTime)
                {
                    decimal? entered = ServiceNumberPromptDialog.Ask(this, "NHẬP ĐƠN GIÁ", item.Name, item.SellPrice, 0m, 999999999m, 2);
                    if (!entered.HasValue)
                        return;
                    customPrice = entered.Value;
                }
                else if (string.Equals(_currentTable.AreaPriceLevelId, "0", StringComparison.Ordinal)
                    && (SystemConfig.SuDungGia2 || SystemConfig.SuDungGia3 || SystemConfig.SuDungGia4))
                {
                    using (var dlg = new ServicePriceChoiceDialog(item))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK)
                            return;
                        customPrice = dlg.SelectedPrice;
                    }
                }
            }

            ServiceOpResult res = _service.AddItem(_currentBill.Id, _currentTable.Id, item.Id, qty, customPrice, customName, customUnitId);
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }

            _currentBill = res.Bill;
            RenderCurrentBill();
            _menuPicker.FocusSearch();
        }

        private void DoIncreaseSelected()
        {
            ServiceOrderLine line = SelectedParentLine();
            if (_currentBill == null || line == null)
                return;
            ServiceOpResult res = _service.IncreaseLineQty(_currentBill.Id, _currentTable.Id, line.Id, 1m);
            ApplyMutationResult(res, line.Id);
        }

        private void DoReduceSelected(decimal delta, bool keepMinOne)
        {
            ServiceOrderLine line = SelectedParentLine();
            if (_currentBill == null || line == null)
            {
                ShowWarning(ServiceOrderConstants.SelectLineToReduce);
                return;
            }
            ServiceOpResult res = ExecuteWithReLogin(
                overrideUser => _service.ReduceLineQty(_currentBill.Id, _currentTable.Id, line.Id, delta, keepMinOne, overrideUser),
                "Xóa giảm món");
            ApplyMutationResult(res, line.Id);
        }

        private void DoSetQtySelected()
        {
            ServiceOrderLine line = SelectedParentLine();
            if (_currentBill == null || line == null)
            {
                ShowWarning(ServiceOrderConstants.SelectLineToReduce);
                return;
            }
            decimal? val = ServiceNumberPromptDialog.Ask(this, "ĐẶT SỐ LƯỢNG", line.ItemName, line.Quantity, 0.01m, 99999m, 2);
            if (!val.HasValue)
                return;
            ServiceOpResult res = ExecuteWithReLogin(
                overrideUser => _service.SetLineQty(_currentBill.Id, _currentTable.Id, line.Id, val.Value, overrideUser),
                "Xóa giảm món");
            ApplyMutationResult(res, line.Id);
        }

        private void DoChangePriceSelected()
        {
            ServiceOrderLine line = SelectedParentLine();
            if (_currentBill == null || line == null)
                return;
            decimal? val = ServiceNumberPromptDialog.Ask(this, "ĐỔI GIÁ BÁN", line.ItemName, line.UnitPrice, 0m, 999999999m, 2);
            if (!val.HasValue)
                return;
            ServiceOpResult res = ExecuteWithReLogin(
                overrideUser => _service.ChangeLinePrice(_currentBill.Id, _currentTable.Id, line.Id, val.Value, overrideUser),
                "Sửa đơn giá");
            ApplyMutationResult(res, line.Id);
        }

        private void DoChangeNoteSelected()
        {
            ServiceOrderLine line = SelectedParentLine();
            if (_currentBill == null || line == null)
                return;
            string text = PromptDialog.Ask(this, "GHI CHÚ DÒNG", line.ItemName, line.Note ?? "");
            if (text == null)
                return;
            ServiceOpResult res = _service.ChangeLineNote(_currentBill.Id, _currentTable.Id, line.Id, text);
            ApplyMutationResult(res, line.Id);
        }

        private void DoChangeLineDiscountSelected()
        {
            ServiceOrderLine line = SelectedParentLine();
            if (_currentBill == null || line == null)
                return;
            using (var dlg = new ServiceLineDiscountDialog(line.ItemName, line.UnitPrice, line.DiscountRate))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                ServiceOpResult res = ExecuteWithReLogin(
                    overrideUser => _service.ChangeLineDiscount(_currentBill.Id, _currentTable.Id, line.Id, dlg.DiscountRate, 0m, false, overrideUser),
                    "Giảm giá mặt hàng");
                ApplyMutationResult(res, line.Id);
            }
        }

        private void DoDeleteSelected()
        {
            ServiceOrderLine line = SelectedParentLine();
            if (_currentBill == null || line == null)
            {
                ShowWarning(ServiceOrderConstants.SelectLineToReduce);
                return;
            }
            string reason = "";
            if (!SuppressPopup)
            {
                if (SystemConfig.KichHoatLuuVetHoatDong && SystemConfig.NhapLyDoKhiXoaMon)
                {
                    reason = PromptDialog.Ask(this, "LÝ DO XÓA MÓN", "Nhập lý do xóa mặt hàng '" + line.ItemName + "':", "");
                    if (string.IsNullOrWhiteSpace(reason))
                        return;
                }
                else if (!Msg.Confirm(this, ServiceOrderConstants.DeleteLineConfirm))
                {
                    return;
                }
            }
            ServiceOpResult res = ExecuteWithReLogin(
                overrideUser => _service.DeleteLine(_currentBill.Id, _currentTable.Id, line.Id, reason, overrideUser),
                "Xóa giảm món");
            ApplyMutationResult(res, null);
        }

        private void DoApplyGroupDiscount()
        {
            if (_currentBill == null)
                return;
            using (var dlg = new ServiceGroupDiscountDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                ServiceOpResult res = ExecuteWithReLogin(
                    overrideUser => _service.ApplyGroupDiscount(_currentBill.Id, _currentTable.Id, dlg.Result, overrideUser),
                    "Giảm giá mặt hàng");
                ApplyMutationResult(res, null);
            }
        }

        private void CommitBillFinancials(bool giamTheoTien, bool phiTheoTien)
        {
            if (_currentBill == null || _currentTable == null)
                return;
            ServiceOpResult res = _service.UpdateBillFinancials(
                _currentBill.Id,
                _currentTable.Id,
                _numTileGiam.Value,
                _numTienGiam.Value,
                giamTheoTien,
                _numTilePhi.Value,
                _numPhiDichVu.Value,
                phiTheoTien,
                _numTileThue.Value);
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                RenderCurrentBill();
                return;
            }
            _currentBill = res.Bill;
            RenderCurrentBill();
        }

        private void CommitBillMeta()
        {
            if (_currentBill == null || _currentTable == null)
                return;
            ServiceOpResult res = _service.UpdateBillMeta(
                _currentBill.Id,
                _currentTable.Id,
                _cboCustomer.SelectedId,
                (int)_numGuests.Value,
                _txtBillNote.Text);
            if (res.Ok)
                _currentBill = res.Bill;
        }

        private ServiceOpResult ExecuteWithReLogin(Func<UserSession, ServiceOpResult> action, string title)
        {
            ServiceOpResult res = action(null);
            if (res.NeedsReLogin && !SuppressPopup)
            {
                using (var dlg = new ServiceReLoginDialog(_session.DatabasePath, res.RequiredFunctionId, title))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK && dlg.VerifiedSession != null)
                        res = action(dlg.VerifiedSession);
                }
            }
            return res;
        }

        private void ApplyMutationResult(ServiceOpResult res, string keepSelectedLineId)
        {
            if (res == null)
                return;
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }
            _currentBill = res.Bill;
            RenderCurrentBill(keepSelectedLineId);
        }
    }
}
