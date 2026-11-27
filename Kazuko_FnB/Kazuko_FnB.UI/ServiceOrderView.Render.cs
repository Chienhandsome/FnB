using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    internal sealed partial class ServiceOrderView
    {
        private readonly Label _lblBillPromo = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Padding = new Padding(4, 0, 4, 0)
        };

        internal Label BillPromoLabelForTest { get { return _lblBillPromo; } }

        private void InitPromotionUi()
        {
            _lblBillPromo.Font = _bodyFont;
            _totalsGrid.Controls.Add(_lblBillPromo, 0, 2);
            _totalsGrid.SetColumnSpan(_lblBillPromo, 2);
        }

        private void ApplyPromotionTheme(ThemeTokens t)
        {
            _lblBillPromo.ForeColor = t.Success;
            _lblBillPromo.BackColor = t.Surface;
            for (int i = 0; i < _detailGrid.Rows.Count && i < _displayedLines.Count; i++)
                ApplyLineRowStyle(_detailGrid.Rows[i], _displayedLines[i]);
        }

        private void RenderCurrentBill(string selectLineId = null)
        {
            _updatingUi = true;
            try
            {
                _detailGrid.Rows.Clear();
                _displayedLines.Clear();

                if (_currentBill == null)
                {
                    _txtStartTime.Text = "";
                    _txtDate.Text = "";
                    _txtBillNo.Text = "";
                    _numGuests.Value = 1m;
                    _cboCustomer.SelectId("");
                    _txtBillNote.Text = "";
                    _lblBillPromo.Text = "";
                    SetMoneyValues(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
                }
                else
                {
                    _txtStartTime.Text = _currentBill.BatDau.ToString("dd/MM/yyyy HH:mm");
                    _txtDate.Text = _currentBill.Ngay.ToString("dd/MM/yyyy");
                    _txtBillNo.Text = _currentBill.Name ?? "";
                    _numGuests.Value = Math.Max(1, _currentBill.GuestCount);
                    _cboCustomer.SelectId(_currentBill.CustomerId ?? "");
                    _txtBillNote.Text = _currentBill.Note ?? "";
                    _lblBillPromo.Text = (_canViewPrice && _currentBill.BillDiscountFromPromo)
                        ? TableService.FormatPromotionTag(_currentBill.BillPromotionName)
                        : "";

                    int selectIdx = -1;
                    foreach (ServiceOrderLine l in _currentBill.Lines)
                    {
                        _displayedLines.Add(l);
                        string nameCell = l.IsComboChild
                            ? "   ↳ " + l.ItemName
                            : (l.IsPromoGift ? "[KM] " + l.ItemName : l.ItemName);
                        string qtyCell = NumberInput.FormatForDisplay(l.Quantity, 2, true);
                        // Không đổ dữ liệu giá/chiết khấu/thành tiền khi thiếu quyền XemDonGiaTrongHoaDonBanHang (§4).
                        string priceCell = _canViewPrice ? NumberInput.FormatForDisplay(l.UnitPrice, 2, true) : "";
                        string discCell = _canViewPrice ? NumberInput.FormatForDisplay(l.DiscountRate, 2, true) : "";
                        string amtCell = _canViewPrice ? NumberInput.FormatForDisplay(l.Amount, 2, true) : "";
                        string noteCell = FormatLineNoteForDisplay(l);
                        int idx = _detailGrid.Rows.Add(nameCell, l.UnitName ?? "", qtyCell, priceCell, discCell, amtCell, noteCell);
                        _detailGrid.Rows[idx].Tag = l;
                        ApplyLineRowStyle(_detailGrid.Rows[idx], l);
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
                            _currentBill.TienHang,
                            _currentBill.TileGiamGia,
                            _currentBill.TienGiamGia,
                            _currentBill.TilePhiDichVu,
                            _currentBill.PhiDichVu,
                            _currentBill.TileThue,
                            _currentBill.TienThue,
                            _currentBill.TongCong);
                    }
                    else
                    {
                        SetMoneyValues(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
                    }
                }
            }
            finally { _updatingUi = false; }

            UpdateControlStates();
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

        private void UpdateControlStates()
        {
            bool hasBill = _currentBill != null && _currentBill.PaidFlag == 0;
            bool hasLine = hasBill && SelectedParentLine() != null;

            _btnStart.Enabled = _currentTable != null && !hasBill;
            _numGuests.Enabled = hasBill;
            _cboCustomer.Enabled = hasBill;
            _txtBillNote.Enabled = hasBill;

            _btnAdd.Enabled = hasBill;
            _btnPlus.Enabled = hasLine;
            _btnMinus.Enabled = hasLine;
            _btnSetQty.Enabled = hasLine;
            _btnPrice.Enabled = hasLine && _canViewPrice && _service.CanEditUnitPrice();
            _btnNote.Enabled = hasLine;
            _btnLineDiscount.Enabled = hasLine && _canViewPrice && _service.CanEditLineDiscount();
            _btnReduce.Enabled = hasLine;
            _btnDelete.Enabled = hasLine;
            _btnGroupDiscount.Enabled = hasBill && _canViewPrice && _service.CanEditLineDiscount();
            _btnMoveTable.Enabled = hasBill;
            _btnMergeTable.Enabled = hasBill;
            _btnTransferItem.Enabled = hasLine;
            _btnCancelBill.Enabled = hasBill;
            _btnKitchenPrint.Enabled = hasBill;
            _btnTempPrint.Enabled = hasBill && _canViewPrice;
            _btnPay.Enabled = hasBill && _canViewPrice;
            _btnGroupPay.Enabled = hasBill && _canViewPrice;

            bool canBillDiscount = hasBill && _canViewPrice && _service.CanEditBillDiscount();
            _numTileGiam.Enabled = canBillDiscount;
            _numTienGiam.Enabled = canBillDiscount;

            bool canFee = hasBill && _canViewPrice && _service.CanEditServiceFee();
            _numTilePhi.Enabled = canFee;
            _numPhiDichVu.Enabled = canFee;

            _numTileThue.Enabled = hasBill && _canViewPrice && SystemConfig.CoThueSuat;
        }

        private ServiceOrderLine SelectedParentLine()
        {
            if (_detailGrid.CurrentRow == null || _detailGrid.CurrentRow.Index < 0
                || _detailGrid.CurrentRow.Index >= _displayedLines.Count)
                return null;
            ServiceOrderLine line = _displayedLines[_detailGrid.CurrentRow.Index];
            if (line.IsPromoGift)
                return null;
            if (line.IsComboChild)
                return _displayedLines.Find(l => string.Equals(l.ComboId, line.ComboParentId, StringComparison.Ordinal));
            return line;
        }

        private static string FormatLineNoteForDisplay(ServiceOrderLine l)
        {
            if (l == null)
                return "";
            if (l.IsPromoGift)
                return !string.IsNullOrEmpty(l.Note) ? l.Note : (!string.IsNullOrEmpty(l.PromotionLabel) ? l.PromotionLabel : "KM");
            if (l.IsPromoDiscounted)
            {
                string tag = !string.IsNullOrEmpty(l.PromotionLabel) ? l.PromotionLabel : "KM";
                string userNote = (l.Note ?? "").Trim();
                if (userNote.Length == 0)
                    return tag;
                if (userNote.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                    return userNote;
                return tag + " | " + userNote;
            }
            return l.Note ?? "";
        }

        private void ApplyLineRowStyle(DataGridViewRow row, ServiceOrderLine line)
        {
            if (row == null || line == null)
                return;
            if (line.IsComboChild)
            {
                row.DefaultCellStyle.ForeColor = _theme.TextMuted;
                row.DefaultCellStyle.Font = _bodyFont;
            }
            else if (line.IsPromoGift)
            {
                row.DefaultCellStyle.ForeColor = _theme.Success;
                row.DefaultCellStyle.Font = _headingFont;
            }
            else if (line.IsPromoDiscounted)
            {
                row.DefaultCellStyle.ForeColor = _theme.Primary;
                row.DefaultCellStyle.Font = _bodyFont;
            }
            else if (line.ProductTypeId == ProductRules.TypeCombo)
            {
                row.DefaultCellStyle.ForeColor = _theme.Danger;
                row.DefaultCellStyle.Font = _headingFont;
            }
            else if (line.ProductTypeId == ProductRules.TypeComboOpen)
            {
                row.DefaultCellStyle.ForeColor = _theme.Info;
                row.DefaultCellStyle.Font = _headingFont;
            }
            else
            {
                row.DefaultCellStyle.ForeColor = _theme.Text;
                row.DefaultCellStyle.Font = _bodyFont;
            }
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
            else if (line.IsPromoGift)
            {
                e.CellStyle.ForeColor = _theme.Success;
                e.CellStyle.Font = _headingFont;
            }
            else if (line.IsPromoDiscounted)
            {
                e.CellStyle.ForeColor = _theme.Primary;
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

        private void ShowNotSupported(string feature)
        {
            LastInfoForTest = ServiceOrderConstants.NotSupportedYet;
            if (!SuppressPopup)
                Msg.Info(this, feature + ": " + ServiceOrderConstants.NotSupportedYet);
        }

        private void ShowWarning(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }
    }
}
