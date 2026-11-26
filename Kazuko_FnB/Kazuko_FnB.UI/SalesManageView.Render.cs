using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    internal sealed partial class SalesManageView
    {
        private void RenderBillListGrid(string selectBillId)
        {
            _loadingList = true;
            try
            {
                string q = _txtFilter.Text ?? "";
                _filteredBills.Clear();
                _billGrid.Rows.Clear();

                int targetIdx = -1;
                foreach (PaidBillListItem item in _allBills)
                {
                    if (!OrderModePolicy.MatchesBillFilter(item, q))
                        continue;
                    _filteredBills.Add(item);

                    int idx = _billGrid.Rows.Add(
                        item.Name ?? "",
                        item.Ngay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                        item.CustomerCode ?? "",
                        item.CustomerName ?? "",
                        MoneyCell(item.TongCong),
                        MoneyCell(item.TienGiamGia),
                        MoneyCell(item.TileGiamGia),
                        MoneyCell(item.TienThue),
                        MoneyCell(item.TileThue),
                        MoneyCell(item.TienHang),
                        item.GioThanhToan.HasValue ? item.GioThanhToan.Value.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture) : "",
                        MoneyCell(item.KhachDua),
                        MoneyCell(item.TraLai),
                        MoneyCell(item.Voucher),
                        MoneyCell(item.TheTraTruoc),
                        MoneyCell(item.TruTichLuy),
                        MoneyCell(item.TienMat),
                        MoneyCell(item.ChuyenKhoan),
                        MoneyCell(item.The),
                        item.TableName ?? "",
                        item.BatDau.HasValue ? item.BatDau.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : "",
                        item.KetThuc.HasValue ? item.KetThuc.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : "",
                        MoneyCell(item.TienGio),
                        MoneyCell(item.TileGiamGiaGio),
                        MoneyCell(item.TienGiamGiaGio),
                        item.GuestCount.ToString(CultureInfo.InvariantCulture),
                        MoneyCell(item.PhiDichVu),
                        MoneyCell(item.TilePhiDichVu),
                        item.OrderNumbers ?? "");
                    _billGrid.Rows[idx].Tag = item;
                    if (!string.IsNullOrEmpty(selectBillId) && string.Equals(item.Id, selectBillId, StringComparison.Ordinal))
                        targetIdx = idx;
                }

                _leftEmpty.Visible = _filteredBills.Count == 0;
                _billGrid.ClearSelection();
                if (targetIdx >= 0)
                {
                    _billGrid.CurrentCell = _billGrid.Rows[targetIdx].Cells["colBillNo"];
                    _billGrid.Rows[targetIdx].Selected = true;
                }
                else
                {
                    _billGrid.CurrentCell = null;
                }
            }
            finally { _loadingList = false; }

            if (SelectedListBill() != null)
            {
                LoadBillIntoRightPane(SelectedListBill().Id);
            }
            else
            {
                ClearRightPane();
            }
        }

        private string MoneyCell(decimal value)
        {
            // Không đổ dữ liệu tiền vào ô khi thiếu quyền XemDonGiaTrongHoaDonBanHang (kinh-nghiem.md).
            return _canViewPrice ? NumberInput.FormatForDisplay(value, 2, true) : "";
        }

        private void ClearRightPane()
        {
            _loadedDetail = null;
            _workingBill = null;
            _activeBillId = null;
            _pendingLogs.Clear();
            _isDirty = false;
            RenderWorkingBill();
        }

        private void RenderWorkingBill(string selectLineId = null)
        {
            _updatingUi = true;
            try
            {
                _detailGrid.Rows.Clear();
                _displayedLines.Clear();

                if (_workingBill == null)
                {
                    _txtStartTime.Text = "";
                    _txtBillNo.Text = "";
                    _numGuests.Value = 1m;
                    _cboCustomer.SelectId("");
                    _txtBillNote.Text = "";
                    SetMoneyValues(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
                }
                else
                {
                    _txtStartTime.Text = _workingBill.BatDau.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                    _dtBillDate.Value = _workingBill.Ngay.Date;
                    _txtBillNo.Text = _workingBill.Name ?? "";
                    _numGuests.Value = Math.Max(1, _workingBill.GuestCount);
                    _cboCustomer.SelectId(_workingBill.CustomerId ?? "");
                    _txtBillNote.Text = _workingBill.Note ?? "";
                    _menuPicker.SetPriceLevel(_workingBill.PriceLevelId ?? "1");

                    int selectIdx = -1;
                    foreach (ServiceOrderLine l in _workingBill.Lines)
                    {
                        if (l == null || l.IsMaterialExport)
                            continue;
                        _displayedLines.Add(l);
                        string nameCell = l.IsComboChild ? "   ↳ " + l.ItemName : l.ItemName;
                        string qtyCell = NumberInput.FormatForDisplay(l.Quantity, 2, true);
                        string priceCell = _canViewPrice ? NumberInput.FormatForDisplay(l.UnitPrice, 2, true) : "";
                        string discCell = _canViewPrice ? NumberInput.FormatForDisplay(l.DiscountRate, 2, true) : "";
                        string amtCell = _canViewPrice ? NumberInput.FormatForDisplay(l.Amount, 2, true) : "";
                        int idx = _detailGrid.Rows.Add(nameCell, l.UnitName ?? "", qtyCell, priceCell, discCell, amtCell, l.Note ?? "");
                        _detailGrid.Rows[idx].Tag = l;
                        if (!string.IsNullOrEmpty(selectLineId) && string.Equals(l.Id, selectLineId, StringComparison.Ordinal))
                            selectIdx = idx;
                    }
                    if (selectIdx >= 0)
                        _detailGrid.CurrentCell = _detailGrid.Rows[selectIdx].Cells["colItemName"];
                    else if (_detailGrid.Rows.Count > 0)
                        _detailGrid.CurrentCell = _detailGrid.Rows[0].Cells["colItemName"];

                    if (_canViewPrice)
                    {
                        SetMoneyValues(
                            _workingBill.TienHang,
                            _workingBill.TileGiamGia,
                            _workingBill.TienGiamGia,
                            _workingBill.TilePhiDichVu,
                            _workingBill.PhiDichVu,
                            _workingBill.TileThue,
                            _workingBill.TienThue,
                            _workingBill.TongCong);
                    }
                    else
                    {
                        SetMoneyValues(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
                    }
                }
            }
            finally { _updatingUi = false; }

            ApplyModeAndControlStates();
        }

        private void ApplyModeAndControlStates()
        {
            bool hasBill = _workingBill != null;
            bool hasLine = hasBill && SelectedParentLine() != null;
            bool isGroupBill = hasBill && !string.IsNullOrEmpty(_workingBill.GroupGuid);

            OrderModeCapabilities caps = OrderModePolicy.Evaluate(
                OrderModePolicy.FromManageMode(_mode),
                hasBill,
                hasLine,
                isGroupBill,
                _canViewPrice,
                BillAdjustService.CanEditManage(_session),
                _tableService.CanEditUnitPrice(),
                _tableService.CanEditLineDiscount(),
                _tableService.CanEditBillDiscount(),
                _tableService.CanEditServiceFee(),
                SystemConfig.CoThueSuat,
                BillAdjustService.CanChangeCustomerInAdjust(_session),
                SystemConfig.GetBool(SalesManageConstants.ConfigAllowChangeDate),
                SystemConfig.GetBool(SalesManageConstants.ConfigEnableCheckItems));

            _dtFrom.Enabled = caps.DateRangeEditable;
            _dtTo.Enabled = caps.DateRangeEditable;
            _btnCancelBill.Visible = caps.ShowCancelBillButton;
            _btnCancelBill.Enabled = caps.CancelBillEnabled;

            _rightPane.Enabled = caps.RightPaneEnabled;
            _groupBanner.Visible = caps.ShowGroupBillWarning;

            _dtBillDate.Enabled = caps.HeaderDateEditable;
            _numGuests.Enabled = caps.HeaderGuestsEnabled;
            _cboCustomer.Enabled = caps.HeaderCustomerEnabled;
            _txtBillNote.Enabled = caps.HeaderNoteEnabled;

            _lineBar.Visible = caps.ShowLineEditButtons;
            _menuPicker.Visible = caps.ShowMenuPicker;
            _lblAddQty.Visible = caps.ShowLineEditButtons;
            _numAddQty.Visible = caps.ShowLineEditButtons;
            _btnAdd.Visible = caps.ShowLineEditButtons;
            _btnReduce.Visible = caps.ShowLineEditButtons;
            _btnDelete.Visible = caps.ShowLineEditButtons;
            _btnGroupDiscount.Visible = caps.ShowLineEditButtons;

            _btnReturn.Visible = caps.ShowReturnButton;
            _btnReturn.Text = caps.ReturnButtonText;
            _btnReturn.Enabled = caps.ReturnButtonEnabled;

            _btnAdd.Enabled = caps.CanAddLine;
            _btnPlus.Enabled = caps.CanModifySelectedLine;
            _btnMinus.Enabled = caps.CanModifySelectedLine;
            _btnSetQty.Enabled = caps.CanModifySelectedLine;
            _btnPrice.Enabled = caps.CanEditUnitPrice;
            _btnNote.Enabled = caps.CanModifySelectedLine;
            _btnLineDiscount.Enabled = caps.CanEditLineDiscount;
            _btnReduce.Enabled = caps.CanModifySelectedLine;
            _btnDelete.Enabled = caps.CanModifySelectedLine;
            _btnGroupDiscount.Enabled = caps.CanApplyGroupDiscount;

            _numTileGiam.Enabled = caps.TotalsDiscountEnabled;
            _numTienGiam.Enabled = caps.TotalsDiscountEnabled;
            _numTilePhi.Enabled = caps.TotalsFeeEnabled;
            _numPhiDichVu.Enabled = caps.TotalsFeeEnabled;
            _numTileThue.Enabled = caps.TotalsTaxEnabled;

            _btnUpdate.Visible = caps.ShowUpdateButton;
            _btnUpdate.Enabled = caps.UpdateButtonEnabled;
            _btnReprint.Visible = caps.ShowReprintButton;
            _btnReprint.Enabled = caps.ReprintButtonEnabled;
        }

        private void SetMoneyValues(decimal tienHang, decimal tileGiam, decimal tienGiam, decimal tilePhi, decimal phi, decimal tileThue, decimal thue, decimal tong)
        {
            _numTienHang.Value = ClampBox(_numTienHang, tienHang);
            _numTileGiam.Value = ClampBox(_numTileGiam, tileGiam);
            _numTienGiam.Value = ClampBox(_numTienGiam, tienGiam);
            _numTilePhi.Value = ClampBox(_numTilePhi, tilePhi);
            _numPhiDichVu.Value = ClampBox(_numPhiDichVu, phi);
            _numTileThue.Value = ClampBox(_numTileThue, tileThue);
            _numTienThue.Value = ClampBox(_numTienThue, thue);
            _numTongCong.Value = ClampBox(_numTongCong, tong);
        }

        private static decimal ClampBox(NumberBox box, decimal val)
        {
            if (val < box.Minimum) return box.Minimum;
            if (val > box.Maximum) return box.Maximum;
            return val;
        }

        private void DetailGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _displayedLines.Count)
                return;
            ServiceOrderLine line = _displayedLines[e.RowIndex];
            if (line.IsComboChild)
            {
                e.CellStyle.ForeColor = _theme.TextMuted;
            }
            else if (line.Quantity < 0m)
            {
                e.CellStyle.ForeColor = _theme.Danger;
            }
            else if (line.ProductTypeId == ProductRules.TypeCombo)
            {
                e.CellStyle.ForeColor = _theme.Danger;
                e.CellStyle.Font = _headingFont;
            }
            else if (line.ProductTypeId == ProductRules.TypeComboOpen)
            {
                e.CellStyle.ForeColor = _theme.Info;
                e.CellStyle.Font = _headingFont;
            }
        }

        public static Color ResolveAlternatingRowColor(ThemeTokens t)
        {
            ThemeTokens tok = t ?? ThemeManager.Current;
            return Draw.Mix(tok.Surface, tok.Info, 0.10f);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t ?? ThemeManager.Current;
            ThemeApplier.Apply(this, _theme);
            BackColor = _theme.Background;
            _mainSplit.BackColor = _theme.Border;
            _leftPane.BackColor = _theme.Background;
            _leftTopBar.BackColor = _theme.Background;
            _rightPane.BackColor = _theme.Background;
            _headerPanel.BackColor = _theme.Surface;
            _bottomPanel.BackColor = _theme.Surface;

            _groupBanner.BackColor = Draw.Mix(_theme.Surface, _theme.Danger, 0.12f);
            _lblGroupBanner.BackColor = Color.Transparent;
            _lblGroupBanner.ForeColor = _theme.Danger;

            _dtFrom.ApplyTheme(_theme, _theme.Background);
            _dtTo.ApplyTheme(_theme, _theme.Background);
            _dtBillDate.ApplyTheme(_theme, _theme.Surface);
            ThemeApplier.ApplyInput(_txtFilter, _theme, _theme.Background, Draw.StrongBorder(_theme));

            ThemeApplier.ApplyGrid(_billGrid, _theme);
            // §1: hàng chẵn nền xanh nhạt lấy từ token (không hard-code màu).
            _billGrid.AlternatingRowsDefaultCellStyle.BackColor = ResolveAlternatingRowColor(_theme);
            _billGrid.AlternatingRowsDefaultCellStyle.ForeColor = _theme.Text;

            ThemeApplier.ApplyGrid(_detailGrid, _theme);
            _menuPicker.ApplyTheme(_theme);
            _numTongCong.ForeColor = _theme.Primary;
            _leftEmpty.ForeColor = _theme.TextMuted;
            Invalidate(true);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed)
                ApplyTheme(ThemeManager.Current);
        }
    }
}
