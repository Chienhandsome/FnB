using System;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    internal sealed partial class SalesManageView
    {
        // ------------------------------------------------------------------ Sửa trong bộ nhớ (CHỈ ghi DB khi bấm Thanh toán)

        private void DoAddItemInMemory(ServiceMenuItem item)
        {
            if (!EnsureManageEditable())
                return;
            if (item == null)
            {
                ShowWarning(ServiceOrderConstants.SelectProductFirst);
                return;
            }
            if (item.TempLocked)
            {
                ShowWarning(ServiceOrderConstants.ProductTempLocked);
                return;
            }
            if (item.ProductTypeId == ProductRules.TypeHourlyService)
            {
                ShowWarning(ServiceOrderConstants.HourlyServiceNotSupported);
                return;
            }

            decimal qty = _numAddQty.Value;
            if (qty <= 0m)
            {
                ShowWarning(ServiceOrderConstants.EnterQuantity);
                return;
            }

            decimal price = OrderCalculator.ResolveAreaPrice(item, _workingBill.PriceLevelId);
            string itemName = item.Name;
            string unitId = item.UnitId;
            string unitName = item.UnitName;

            if (!SuppressPopup)
            {
                if (item.ProductTypeId == ProductRules.TypeOpen)
                {
                    using (var dlg = new ServiceOpenItemDialog(item, _tableService.LoadUnits()))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK)
                            return;
                        itemName = dlg.ItemName;
                        unitId = dlg.UnitId;
                        price = dlg.UnitPrice;
                    }
                }
                else if (item.PriceByTime)
                {
                    decimal? entered = ServiceNumberPromptDialog.Ask(this, "NHẬP ĐƠN GIÁ", item.Name, item.SellPrice, 0m, 999999999m, 2);
                    if (!entered.HasValue)
                        return;
                    price = entered.Value;
                }
            }

            bool isOpen = item.ProductTypeId == ProductRules.TypeOpen;
            bool isCombo = item.ProductTypeId == ProductRules.TypeCombo;
            bool isComboOpen = item.ProductTypeId == ProductRules.TypeComboOpen;
            bool allowMerge = !SystemConfig.NhapMotMatHangNhieuLanTrongPhieu && !isOpen && !isComboOpen;
            string selectId = null;

            ServiceOrderLine merged = allowMerge ? _workingBill.Lines.Find(l =>
                !l.IsComboChild && !l.IsMaterialExport && l.Quantity > 0m
                && string.Equals(l.ProductId, item.Id, StringComparison.Ordinal)
                && l.DiscountRate == 0m && !l.DiscountByMoney
                && string.IsNullOrEmpty(l.Note)
                && (item.PriceByTime || l.UnitPrice == price)) : null;

            if (merged != null)
            {
                merged.Quantity += qty;
                if (item.PriceByTime)
                    merged.UnitPrice = price;
                OrderCalculator.RecalcLine(merged);
                UpdateComboChildrenInMemory(merged.ComboId, merged.Quantity);
                selectId = merged.Id;
            }
            else
            {
                string comboId = DbHelper.NewId();
                var newLine = new ServiceOrderLine
                {
                    Id = DbHelper.NewId(),
                    OrderId = _workingBill.Id,
                    ProductId = item.Id,
                    ProductTypeId = item.ProductTypeId,
                    DrinkTypeId = item.DrinkTypeId,
                    ItemName = itemName,
                    UnitId = unitId,
                    UnitName = unitName ?? "",
                    WarehouseId = _workingBill.WarehouseId,
                    Quantity = qty,
                    UnitPrice = price,
                    CostPrice = item.CostPrice,
                    ComboId = comboId,
                    ComboParentId = ""
                };
                OrderCalculator.RecalcLine(newLine);
                _workingBill.Lines.Add(newLine);
                selectId = newLine.Id;

                if (isCombo || isComboOpen)
                {
                    foreach (ServiceRecipeComponent c in _adjustService.LoadRecipe(item.Id))
                    {
                        if (string.IsNullOrEmpty(c.MaterialId) || c.Quantity <= 0m)
                            continue;
                        _workingBill.Lines.Add(new ServiceOrderLine
                        {
                            Id = DbHelper.NewId(),
                            OrderId = _workingBill.Id,
                            ProductId = c.MaterialId,
                            ProductTypeId = c.MaterialTypeId,
                            ItemName = c.MaterialName,
                            UnitId = c.UnitId,
                            UnitName = c.UnitName ?? "",
                            WarehouseId = _workingBill.WarehouseId,
                            Quantity = qty * c.Quantity,
                            ComboId = DbHelper.NewId(),
                            ComboParentId = comboId,
                            ComboQty = c.Quantity
                        });
                    }
                }
            }

            QueuePendingLog(ActivityKind.ThemMon, "Thêm " + itemName, itemName, qty, price, qty * price);
            MarkDirtyAndRecalc(selectId);
        }

        private void DoIncreaseSelectedInMemory()
        {
            if (!EnsureManageEditable())
                return;
            ServiceOrderLine line = SelectedParentLine();
            if (line == null)
                return;
            line.Quantity += 1m;
            OrderCalculator.RecalcLine(line);
            UpdateComboChildrenInMemory(line.ComboId, line.Quantity);
            QueuePendingLog(ActivityKind.TangMon, "Tăng SL " + line.ItemName + " lên " + line.Quantity, line.ItemName, 1m, line.UnitPrice, line.Amount);
            MarkDirtyAndRecalc(line.Id);
        }

        private void DoReduceSelectedInMemory(decimal delta, bool keepMinOne)
        {
            if (!EnsureManageEditable())
                return;
            ServiceOrderLine line = SelectedParentLine();
            if (line == null)
            {
                ShowWarning(ServiceOrderConstants.SelectLineToReduce);
                return;
            }
            if (!EnsurePermissionWithReLogin(ServiceOrderConstants.FuncXoaGiamMon, "Xóa giảm món"))
                return;
            if (SystemConfig.NhapMatKhauGiamDo)
            {
                ShowWarning(ServiceOrderConstants.ReducePasswordBlocked);
                return;
            }

            decimal targetQty;
            if (keepMinOne)
            {
                if (line.Quantity <= 1m)
                    return;
                targetQty = Math.Max(1m, line.Quantity - delta);
            }
            else
            {
                if (line.Quantity < delta + 1m)
                {
                    ShowWarning(ServiceOrderConstants.CannotReduceExceedsCurrent(delta, line.Quantity));
                    return;
                }
                targetQty = line.Quantity - delta;
            }

            decimal oldQty = line.Quantity;
            line.Quantity = targetQty;
            OrderCalculator.RecalcLine(line);
            UpdateComboChildrenInMemory(line.ComboId, line.Quantity);
            QueuePendingLog(ActivityKind.GiamMon, string.Format("Giảm {0}: {1} -> {2}", line.ItemName, oldQty, targetQty), line.ItemName, oldQty - targetQty, line.UnitPrice, line.Amount);
            MarkDirtyAndRecalc(line.Id);
        }

        private void DoSetQtySelectedInMemory()
        {
            if (!EnsureManageEditable())
                return;
            ServiceOrderLine line = SelectedParentLine();
            if (line == null)
                return;
            decimal? val = ServiceNumberPromptDialog.Ask(this, "ĐẶT SỐ LƯỢNG", line.ItemName, Math.Max(0.01m, line.Quantity), 0.01m, 99999m, 2);
            if (!val.HasValue || val.Value == line.Quantity)
                return;
            if (val.Value < line.Quantity)
            {
                if (!EnsurePermissionWithReLogin(ServiceOrderConstants.FuncXoaGiamMon, "Xóa giảm món"))
                    return;
                if (SystemConfig.NhapMatKhauGiamDo)
                {
                    ShowWarning(ServiceOrderConstants.ReducePasswordBlocked);
                    return;
                }
            }
            decimal oldQty = line.Quantity;
            line.Quantity = val.Value;
            OrderCalculator.RecalcLine(line);
            UpdateComboChildrenInMemory(line.ComboId, line.Quantity);
            QueuePendingLog(ActivityKind.DatSoLuong, string.Format("Đặt SL {0}: {1} -> {2}", line.ItemName, oldQty, val.Value), line.ItemName, val.Value, line.UnitPrice, line.Amount);
            MarkDirtyAndRecalc(line.Id);
        }

        private void DoChangePriceSelectedInMemory()
        {
            if (!EnsureManageEditable())
                return;
            ServiceOrderLine line = SelectedParentLine();
            if (line == null)
                return;
            if (!EnsurePermissionWithReLogin(ServiceOrderConstants.FuncSuaDonGia, "Sửa đơn giá"))
                return;
            decimal? val = ServiceNumberPromptDialog.Ask(this, "ĐỔI GIÁ BÁN", line.ItemName, line.UnitPrice, 0m, 999999999m, 2);
            if (!val.HasValue)
                return;
            decimal oldPrice = line.UnitPrice;
            line.UnitPrice = val.Value;
            OrderCalculator.RecalcLine(line);
            QueuePendingLog(ActivityKind.DoiGiaBan, string.Format("Đổi giá {0}: {1} -> {2}", line.ItemName, oldPrice, val.Value), line.ItemName, line.Quantity, val.Value, line.Amount);
            MarkDirtyAndRecalc(line.Id);
        }

        private void DoChangeNoteSelectedInMemory()
        {
            if (!EnsureManageEditable())
                return;
            ServiceOrderLine line = SelectedParentLine();
            if (line == null)
                return;
            string text = PromptDialog.Ask(this, "GHI CHÚ DÒNG", line.ItemName, line.Note ?? "");
            if (text == null)
                return;
            line.Note = text.Trim();
            MarkDirtyAndRecalc(line.Id);
        }

        private void DoChangeLineDiscountSelectedInMemory()
        {
            if (!EnsureManageEditable())
                return;
            ServiceOrderLine line = SelectedParentLine();
            if (line == null)
                return;
            if (!EnsurePermissionWithReLogin(ServiceOrderConstants.FuncGiamGiaMatHang, "Giảm giá mặt hàng"))
                return;
            using (var dlg = new ServiceLineDiscountDialog(line.ItemName, line.UnitPrice, line.DiscountRate))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                line.DiscountByMoney = false;
                line.DiscountRate = dlg.DiscountRate;
                OrderCalculator.RecalcLine(line);
                QueuePendingLog(ActivityKind.DatGiamGiaMatHang, string.Format("Giảm giá {0}: {1}%", line.ItemName, line.DiscountRate), line.ItemName, line.Quantity, line.UnitPrice, line.Amount);
                MarkDirtyAndRecalc(line.Id);
            }
        }

        private void DoDeleteSelectedInMemory()
        {
            if (!EnsureManageEditable())
                return;
            ServiceOrderLine line = SelectedParentLine();
            if (line == null)
            {
                ShowWarning(ServiceOrderConstants.SelectLineToReduce);
                return;
            }
            if (!EnsurePermissionWithReLogin(ServiceOrderConstants.FuncXoaGiamMon, "Xóa giảm món"))
                return;
            if (SystemConfig.NhapMatKhauGiamDo)
            {
                ShowWarning(ServiceOrderConstants.ReducePasswordBlocked);
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
            _workingBill.Lines.RemoveAll(x => x.Id == line.Id || (!string.IsNullOrEmpty(line.ComboId) && x.ComboParentId == line.ComboId));
            string note = string.IsNullOrWhiteSpace(reason) ? "Xóa món " + line.ItemName : "Xóa món " + line.ItemName + " (Lý do: " + reason.Trim() + ")";
            QueuePendingLog(ActivityKind.XoaMon, note, line.ItemName, line.Quantity, line.UnitPrice, line.Amount);
            MarkDirtyAndRecalc(null);
        }

        private void DoApplyGroupDiscountInMemory()
        {
            if (!EnsureManageEditable())
                return;
            if (!EnsurePermissionWithReLogin(ServiceOrderConstants.FuncGiamGiaMatHang, "Giảm giá mặt hàng"))
                return;
            using (var dlg = new ServiceGroupDiscountDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                GroupDiscountInput input = dlg.Result;
                foreach (ServiceOrderLine line in _workingBill.Lines)
                {
                    if (line.IsComboChild || line.IsMaterialExport)
                        continue;
                    decimal rate = line.DrinkTypeId == "0" ? input.FoodRate
                        : line.DrinkTypeId == "1" ? input.DrinkRate
                        : line.DrinkTypeId == "2" ? input.ServiceRate
                        : line.DrinkTypeId == "3" ? input.OtherRate : 0m;
                    line.DiscountByMoney = false;
                    line.DiscountRate = rate;
                    OrderCalculator.RecalcLine(line);
                }
                MarkDirtyAndRecalc(null);
            }
        }

        private void UpdateBillFinancialsInMemory(bool giamTheoTien, bool phiTheoTien)
        {
            if (_workingBill == null || _mode != BillManageMode.Manage || !string.IsNullOrEmpty(_workingBill.GroupGuid))
                return;
            _workingBill.TileGiamGia = Math.Max(0m, _numTileGiam.Value);
            _workingBill.TienGiamGia = Math.Max(0m, _numTienGiam.Value);
            _workingBill.GiamTheoTien = giamTheoTien;
            _workingBill.TilePhiDichVu = Math.Max(0m, _numTilePhi.Value);
            _workingBill.PhiDichVu = Math.Max(0m, _numPhiDichVu.Value);
            _workingBill.PhiDichVuTheoTien = phiTheoTien;
            _workingBill.TileThue = Math.Max(0m, _numTileThue.Value);
            MarkDirtyAndRecalc(null);
        }

        private void OnCustomerChangedInMemory()
        {
            if (_workingBill == null || !string.IsNullOrEmpty(_workingBill.GroupGuid))
                return;
            if (_mode == BillManageMode.Adjust && !BillAdjustService.CanChangeCustomerInAdjust(_session))
                return;
            string custId = _cboCustomer.SelectedId ?? "";
            CustomerLookupItem found = _customers.Find(c => string.Equals(c.Id, custId, StringComparison.Ordinal));
            _adjustService.ApplyCustomerChangeInMemory(_workingBill, custId, found != null ? found.Name : "");
            _isDirty = true;
            RenderWorkingBill();
        }

        private void OnHeaderMetaChangedInMemory()
        {
            if (_workingBill == null || _mode != BillManageMode.Manage || !string.IsNullOrEmpty(_workingBill.GroupGuid))
                return;
            _workingBill.GuestCount = Math.Max(1, (int)_numGuests.Value);
            _workingBill.Note = (_txtBillNote.Text ?? "").Trim();
            _isDirty = true;
        }

        private void OnBillDateChangedInMemory()
        {
            if (_workingBill == null || _mode != BillManageMode.Manage || !string.IsNullOrEmpty(_workingBill.GroupGuid))
                return;
            if (!SystemConfig.GetBool(SalesManageConstants.ConfigAllowChangeDate))
                return;
            _workingBill.Ngay = _dtBillDate.Value.Date;
            _isDirty = true;
        }
    }
}
