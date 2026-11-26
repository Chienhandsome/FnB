using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    internal sealed partial class SalesManageView
    {
        private void WireEvents()
        {
            _dtFrom.ValueChanged += delegate { if (!_loadingList) OnDateRangeChanged(); };
            _dtTo.ValueChanged += delegate { if (!_loadingList) OnDateRangeChanged(); };
            _txtFilter.TextChanged += delegate { if (!_loadingList) RenderBillListGrid(_activeBillId); };
            _btnReload.Click += delegate { OnReloadClicked(); };
            _btnCancelBill.Click += delegate { DoCancelBill(); };

            _billGrid.SelectionChanged += delegate
            {
                if (_loadingList)
                    return;
                PaidBillListItem sel = SelectedListBill();
                string nextId = sel != null ? sel.Id : null;
                if (string.Equals(nextId, _activeBillId, StringComparison.Ordinal))
                    return;
                if (!ConfirmDiscardUnsavedChanges())
                {
                    RestoreLeftSelection(_activeBillId);
                    return;
                }
                _pendingLoadBillId = nextId;
                _loadTimer.Stop();
                if (string.IsNullOrEmpty(nextId))
                    ClearRightPane();
                else
                    _loadTimer.Start();
            };

            _loadTimer.Tick += delegate
            {
                _loadTimer.Stop();
                if (!string.IsNullOrEmpty(_pendingLoadBillId))
                    LoadBillIntoRightPane(_pendingLoadBillId);
            };

            _menuPicker.ItemActivated += delegate(object s, ServiceMenuItem item) { DoAddItemInMemory(item); };
            _btnAdd.Click += delegate { DoAddItemInMemory(_menuPicker.SelectedItem); };
            _btnPlus.Click += delegate { DoIncreaseSelectedInMemory(); };
            _btnMinus.Click += delegate { DoReduceSelectedInMemory(1m, true); };
            _btnSetQty.Click += delegate { DoSetQtySelectedInMemory(); };
            _btnPrice.Click += delegate { DoChangePriceSelectedInMemory(); };
            _btnNote.Click += delegate { DoChangeNoteSelectedInMemory(); };
            _btnLineDiscount.Click += delegate { DoChangeLineDiscountSelectedInMemory(); };
            _btnReduce.Click += delegate { DoReduceSelectedInMemory(_numAddQty.Value, false); };
            _btnDelete.Click += delegate { DoDeleteSelectedInMemory(); };
            _btnGroupDiscount.Click += delegate { DoApplyGroupDiscountInMemory(); };
            _btnReturn.Click += delegate { DoReturnItems(); };

            _btnUpdate.Click += delegate { DoUpdateBill(); };
            _btnReprint.Click += delegate { DoReprintBill(); };

            _detailGrid.SelectionChanged += delegate { if (!_updatingUi) ApplyModeAndControlStates(); };
            _detailGrid.CellFormatting += DetailGrid_CellFormatting;

            _numTileGiam.ValueChanged += delegate { if (!_updatingUi) UpdateBillFinancialsInMemory(false, _workingBill != null && _workingBill.PhiDichVuTheoTien); };
            _numTienGiam.ValueChanged += delegate { if (!_updatingUi) UpdateBillFinancialsInMemory(true, _workingBill != null && _workingBill.PhiDichVuTheoTien); };
            _numTilePhi.ValueChanged += delegate { if (!_updatingUi) UpdateBillFinancialsInMemory(_workingBill != null && _workingBill.GiamTheoTien, false); };
            _numPhiDichVu.ValueChanged += delegate { if (!_updatingUi) UpdateBillFinancialsInMemory(_workingBill != null && _workingBill.GiamTheoTien, true); };
            _numTileThue.ValueChanged += delegate { if (!_updatingUi) UpdateBillFinancialsInMemory(_workingBill != null && _workingBill.GiamTheoTien, _workingBill != null && _workingBill.PhiDichVuTheoTien); };

            _cboCustomer.ValueChanged += delegate { if (!_updatingUi) OnCustomerChangedInMemory(); };
            _numGuests.ValueChanged += delegate { if (!_updatingUi) OnHeaderMetaChangedInMemory(); };
            _txtBillNote.Leave += delegate { if (!_updatingUi) OnHeaderMetaChangedInMemory(); };
            _dtBillDate.ValueChanged += delegate { if (!_updatingUi) OnBillDateChangedInMemory(); };
        }

        public bool ConfirmClose()
        {
            return ConfirmDiscardUnsavedChanges();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F2:
                    if (_btnNote.Visible && _btnNote.Enabled)
                        DoChangeNoteSelectedInMemory();
                    return true;
                case Keys.F3:
                    if (_menuPicker.Visible)
                        _menuPicker.FocusSearch();
                    return true;
                case Keys.F8:
                    // §1.1: ở Điều chỉnh hóa đơn nút In lại bill ẩn nên F8 không chạy.
                    if (_btnReprint.Visible && _btnReprint.Enabled)
                        DoReprintBill();
                    return true;
                case Keys.F11:
                    if (_btnUpdate.Visible && _btnUpdate.Enabled)
                        DoUpdateBill();
                    return true;
                case Keys.F12:
                    if (_cboCustomer.Enabled)
                        _cboCustomer.Focus();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        internal bool DispatchKeyForTest(Keys keyData)
        {
            Message m = new Message();
            return ProcessCmdKey(ref m, keyData);
        }

        private void LoadLookups()
        {
            _menuPicker.SetData(_tableService.LoadMenuGroups(), _tableService.LoadMenuItems());
            _customers = _tableService.LoadCustomers();
            var items = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            foreach (CustomerLookupItem c in _customers)
                items.Add(new LookupItem { Id = c.Id, Name = c.Name });
            _updatingUi = true;
            try { _cboCustomer.SetItems(items); }
            finally { _updatingUi = false; }
        }

        public void ReloadBillList(string keepBillId = null)
        {
            _loadTimer.Stop();
            _allBills = _adjustService.LoadPaidBills(_dtFrom.Value.Date, _dtTo.Value.Date);
            RenderBillListGrid(keepBillId);
        }

        private void OnDateRangeChanged()
        {
            if (!ConfirmDiscardUnsavedChanges())
                return;
            ReloadBillList(null);
        }

        private void OnReloadClicked()
        {
            if (!ConfirmDiscardUnsavedChanges())
                return;
            ReloadBillList(_activeBillId);
        }

        private void LoadBillIntoRightPane(string billId)
        {
            PaidBillDetail detail = _adjustService.LoadPaidBillDetail(billId);
            if (detail == null || detail.Header == null)
            {
                ClearRightPane();
                return;
            }
            _loadedDetail = detail;
            _workingBill = BillAdjustService.CloneWorkingBill(detail.Header);
            _activeBillId = detail.Header.Id;
            _pendingLogs.Clear();
            _isDirty = false;
            RenderWorkingBill();
        }

        private PaidBillListItem SelectedListBill()
        {
            if (_billGrid.SelectedRows.Count > 0)
                return _billGrid.SelectedRows[0].Tag as PaidBillListItem;
            if (_billGrid.CurrentRow != null && _billGrid.CurrentRow.Selected)
                return _billGrid.CurrentRow.Tag as PaidBillListItem;
            return null;
        }

        private void RestoreLeftSelection(string billId)
        {
            _loadingList = true;
            try
            {
                _billGrid.ClearSelection();
                for (int i = 0; i < _billGrid.Rows.Count; i++)
                {
                    var item = _billGrid.Rows[i].Tag as PaidBillListItem;
                    if (item != null && string.Equals(item.Id, billId, StringComparison.Ordinal))
                    {
                        _billGrid.CurrentCell = _billGrid.Rows[i].Cells["colBillNo"];
                        _billGrid.Rows[i].Selected = true;
                        return;
                    }
                }
            }
            finally { _loadingList = false; }
        }

        private bool ConfirmDiscardUnsavedChanges()
        {
            if (!_isDirty)
                return true;
            UnsavedPromptTriggeredForTest = true;
            if (ConfirmUnsavedOverrideForTest != null)
                return ConfirmUnsavedOverrideForTest();
            if (SuppressPopup)
                return true;
            return Msg.Confirm(this, SalesManageConstants.UnsavedChangesConfirm);
        }

        private ServiceOrderLine SelectedParentLine()
        {
            if (_detailGrid.CurrentRow == null || _detailGrid.CurrentRow.Index < 0
                || _detailGrid.CurrentRow.Index >= _displayedLines.Count)
                return null;
            ServiceOrderLine line = _displayedLines[_detailGrid.CurrentRow.Index];
            if (line.IsComboChild)
                return _displayedLines.Find(l => string.Equals(l.ComboId, line.ComboParentId, StringComparison.Ordinal));
            return line;
        }

        private bool EnsureManageEditable()
        {
            if (_workingBill == null)
            {
                ShowWarning(SalesManageConstants.SelectBillFirst);
                return false;
            }
            if (_mode != BillManageMode.Manage)
            {
                ShowWarning(SalesManageConstants.AdjustModeOnlyCustomerAndReturn);
                return false;
            }
            if (!string.IsNullOrEmpty(_workingBill.GroupGuid))
            {
                ShowWarning(SalesManageConstants.GroupBillEditBlocked);
                return false;
            }
            return true;
        }

        private void UpdateComboChildrenInMemory(string comboId, decimal parentQty)
        {
            if (string.IsNullOrEmpty(comboId) || _workingBill == null)
                return;
            foreach (ServiceOrderLine c in _workingBill.Lines)
            {
                if (string.Equals(c.ComboParentId, comboId, StringComparison.Ordinal))
                    c.Quantity = parentQty * c.ComboQty;
            }
        }

        private void QueuePendingLog(ActivityKind kind, string note, string itemName, decimal qty, decimal unitPrice, decimal amount)
        {
            _pendingLogs.Add(new ActivityLogRequest
            {
                FunctionName = OrderModePolicy.FunctionNameOf(_mode),
                Kind = kind,
                BillId = _workingBill != null ? _workingBill.Id : "",
                TableName = _workingBill != null ? _workingBill.TableName : "",
                Note = note,
                ItemName = itemName ?? "",
                Quantity = qty,
                UnitPrice = unitPrice,
                Amount = amount
            });
        }

        private void MarkDirtyAndRecalc(string keepLineId)
        {
            if (_workingBill == null)
                return;
            OrderModePolicy.RecalcBillTotalsInPlace(_workingBill, SystemConfig.LamTronTien);
            _isDirty = true;
            RenderWorkingBill(keepLineId);
        }

        private bool EnsurePermissionWithReLogin(string functionId, string title)
        {
            if (TableService.HasFunction(_session, functionId))
                return true;
            if (SuppressPopup)
            {
                ShowWarning(ServiceOrderConstants.NoPermission);
                return false;
            }
            using (var dlg = new ServiceReLoginDialog(_session.DatabasePath, functionId, title))
                return dlg.ShowDialog(this) == DialogResult.OK && dlg.VerifiedSession != null;
        }

        private void ShowWarning(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }
    }
}
