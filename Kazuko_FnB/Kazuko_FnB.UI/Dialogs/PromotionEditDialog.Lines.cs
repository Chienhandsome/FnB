using System;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Thao tác thêm/xóa dòng, chọn nhóm/mặt hàng và sửa ô số trên lưới chi tiết của <see cref="PromotionEditDialog"/>.</summary>
    internal sealed partial class PromotionEditDialog
    {
        private void AddDetailLine()
        {
            if (_readOnly)
                return;
            _detailGrid.EndEdit();
            PromotionKind kind = CurrentKind();
            if (!PromotionRules.UsesDetailGrid(kind))
                return;

            PromotionDetailRow line = null;
            if (kind == PromotionKind.ProductGroupPercent)
            {
                ProductGroupRow grp = PickProductGroup();
                if (grp == null)
                    return;
                if (_details.Exists(x => string.Equals(x.ProductGroupId, grp.Id, StringComparison.Ordinal)))
                {
                    ShowValidationError("Nhóm mặt hàng '" + grp.Name + "' đã có trong danh sách.");
                    return;
                }
                line = new PromotionDetailRow
                {
                    ProductGroupId = grp.Id,
                    ProductGroupName = grp.Name,
                    DiscountPercent = 10m,
                    Note = ""
                };
            }
            else if (kind == PromotionKind.OrderValuePercent)
            {
                line = new PromotionDetailRow { OrderValue = 0m, DiscountPercent = 5m, Note = "" };
            }
            else if (kind == PromotionKind.QuantityPercent)
            {
                line = new PromotionDetailRow { ItemQuantity = 1m, DiscountPercent = 5m, Note = "" };
            }
            else if (kind == PromotionKind.GiftByOrderValue)
            {
                PromotionProductItem gift = PickProduct("CHỌN MẶT HÀNG TẶNG");
                if (gift == null)
                    return;
                line = new PromotionDetailRow
                {
                    OrderValue = 0m,
                    GiftQuantity = 1m,
                    GiftProductId = gift.Id,
                    GiftProductName = gift.Name,
                    GiftProductUnitName = gift.UnitName,
                    GiftProductSellPrice = gift.SellPrice,
                    Note = ""
                };
            }
            else
            {
                PromotionProductItem prod = PickProduct("CHỌN MẶT HÀNG");
                if (prod == null)
                    return;
                if (_details.Exists(x => string.Equals(x.ProductId, prod.Id, StringComparison.Ordinal)))
                {
                    ShowValidationError("Mặt hàng '" + prod.Name + "' đã có trong danh sách.");
                    return;
                }
                line = new PromotionDetailRow
                {
                    ProductId = prod.Id,
                    ProductCode = prod.Code,
                    ProductName = prod.Name,
                    ProductUnitName = prod.UnitName,
                    ProductSellPrice = prod.SellPrice,
                    DiscountPercent = kind == PromotionKind.ProductPercent ? 10m : 0m,
                    SalePrice = kind == PromotionKind.FixedPrice ? prod.SellPrice : 0m,
                    BuyQuantity = kind == PromotionKind.BuyXGetY ? 1m : 0m,
                    GiftQuantity = kind == PromotionKind.BuyXGetY ? 1m : 0m,
                    Note = ""
                };
            }

            _details.Add(line);
            RebuildDetailGridRows();
            int lastIdx = _detailGrid.Rows.Count - 1;
            if (lastIdx >= 0 && _detailGrid.Columns.Count > 0)
                _detailGrid.CurrentCell = _detailGrid.Rows[lastIdx].Cells[0];
            OnFieldChanged();
        }

        private void PickGiftProductForCurrentLine()
        {
            if (_readOnly || _detailGrid.CurrentRow == null)
                return;
            int idx = _detailGrid.CurrentRow.Index;
            if (idx < 0 || idx >= _details.Count)
                return;
            PromotionKind kind = CurrentKind();
            if (kind != PromotionKind.BuyXGetY && kind != PromotionKind.GiftByOrderValue)
                return;
            PromotionProductItem gift = PickProduct("CHỌN MẶT HÀNG TẶNG");
            if (gift == null)
                return;
            PromotionDetailRow d = _details[idx];
            d.GiftProductId = gift.Id;
            d.GiftProductName = gift.Name;
            d.GiftProductUnitName = gift.UnitName;
            d.GiftProductSellPrice = gift.SellPrice;
            SyncDetailRow(idx);
            OnFieldChanged();
        }

        private void ClearGiftProductForCurrentLine()
        {
            if (_readOnly || _detailGrid.CurrentRow == null || CurrentKind() != PromotionKind.BuyXGetY)
                return;
            int idx = _detailGrid.CurrentRow.Index;
            if (idx < 0 || idx >= _details.Count)
                return;
            PromotionDetailRow d = _details[idx];
            d.GiftProductId = null;
            d.GiftProductName = "";
            d.GiftProductUnitName = "";
            d.GiftProductSellPrice = 0m;
            SyncDetailRow(idx);
            OnFieldChanged();
        }

        private void DeleteSelectedDetailLine()
        {
            if (_readOnly || _detailGrid.CurrentRow == null)
                return;
            _detailGrid.EndEdit();
            int idx = _detailGrid.CurrentRow.Index;
            if (idx < 0 || idx >= _details.Count)
                return;
            _details.RemoveAt(idx);
            RebuildDetailGridRows();
            OnFieldChanged();
        }

        private void OnDetailCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_readOnly || e.RowIndex < 0 || e.RowIndex >= _details.Count || e.ColumnIndex < 0)
                return;
            string col = _detailGrid.Columns[e.ColumnIndex].Name;
            PromotionDetailRow d = _details[e.RowIndex];
            if (col == "col_group")
            {
                ProductGroupRow grp = PickProductGroup();
                if (grp == null)
                    return;
                for (int i = 0; i < _details.Count; i++)
                {
                    if (i != e.RowIndex && string.Equals(_details[i].ProductGroupId, grp.Id, StringComparison.Ordinal))
                    {
                        ShowValidationError("Nhóm mặt hàng '" + grp.Name + "' đã có trong danh sách.");
                        return;
                    }
                }
                d.ProductGroupId = grp.Id;
                d.ProductGroupName = grp.Name;
                SyncDetailRow(e.RowIndex);
                OnFieldChanged();
            }
            else if (col == "col_prod")
            {
                PromotionProductItem prod = PickProduct("CHỌN MẶT HÀNG");
                if (prod == null)
                    return;
                for (int i = 0; i < _details.Count; i++)
                {
                    if (i != e.RowIndex && string.Equals(_details[i].ProductId, prod.Id, StringComparison.Ordinal))
                    {
                        ShowValidationError("Mặt hàng '" + prod.Name + "' đã có trong danh sách.");
                        return;
                    }
                }
                d.ProductId = prod.Id;
                d.ProductCode = prod.Code;
                d.ProductName = prod.Name;
                d.ProductUnitName = prod.UnitName;
                d.ProductSellPrice = prod.SellPrice;
                SyncDetailRow(e.RowIndex);
                OnFieldChanged();
            }
            else if (col == "col_giftprod")
            {
                PickGiftProductForCurrentLine();
            }
        }

        private void OnDetailCellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _details.Count || e.ColumnIndex < 0)
                return;
            if (!NumericEditGridCell.IsNumericCell(_detailGrid, e.RowIndex, e.ColumnIndex))
                return;
            _cellParseFailed = !NumericEditGridCell.ParseCell(_detailGrid, e, 0m);
        }

        private void OnDetailCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            bool parseFailed = _cellParseFailed;
            _cellParseFailed = false;
            if (_updatingDetailCell || e.RowIndex < 0 || e.RowIndex >= _details.Count || e.ColumnIndex < 0)
                return;

            PromotionDetailRow d = _details[e.RowIndex];
            string col = _detailGrid.Columns[e.ColumnIndex].Name;
            object raw = _detailGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

            if (col == "col_note")
            {
                d.Note = PromotionRules.ClipText(Convert.ToString(raw) ?? "", PromotionText.MaxTextLength);
                SyncDetailRow(e.RowIndex);
                OnFieldChanged();
                return;
            }

            decimal val;
            if (parseFailed || !NumberInput.TryParse(raw, out val) || val < 0m)
            {
                SyncDetailRow(e.RowIndex);
                return;
            }

            switch (col)
            {
                case "col_pct": d.DiscountPercent = val; break;
                case "col_orderval": d.OrderValue = val; break;
                case "col_qty": d.ItemQuantity = val; break;
                case "col_buyqty": d.BuyQuantity = val; break;
                case "col_giftqty": d.GiftQuantity = val; break;
                case "col_saleprice": d.SalePrice = val; break;
            }
            SyncDetailRow(e.RowIndex);
            OnFieldChanged();
        }

        internal void CommitDetailCellEditForTest(int rowIndex, string columnName, object newValue)
        {
            if (rowIndex < 0 || rowIndex >= _detailGrid.Rows.Count)
                return;
            DataGridViewColumn col = _detailGrid.Columns[columnName];
            if (col == null)
                return;
            object valueToAssign = newValue;
            if (NumericEditGridCell.IsNumericCell(_detailGrid, rowIndex, col.Index) && newValue is string)
            {
                var parseArgs = new DataGridViewCellParsingEventArgs(rowIndex, col.Index, newValue, typeof(decimal), col.DefaultCellStyle);
                OnDetailCellParsing(_detailGrid, parseArgs);
                valueToAssign = parseArgs.Value;
            }
            _detailGrid.Rows[rowIndex].Cells[columnName].Value = valueToAssign;
            OnDetailCellEndEdit(_detailGrid, new DataGridViewCellEventArgs(col.Index, rowIndex));
        }

        private void UpdateDetailToolbarButtons()
        {
            bool hasRow = _detailGrid.CurrentRow != null && _detailGrid.CurrentRow.Index >= 0 && _detailGrid.CurrentRow.Index < _details.Count;
            _btnAddLine.Enabled = !_readOnly && PromotionRules.UsesDetailGrid(CurrentKind());
            _btnDeleteLine.Enabled = !_readOnly && hasRow;
            _btnPickGift.Enabled = !_readOnly && hasRow;
            _btnSameGift.Enabled = !_readOnly && hasRow;
        }

        private ProductGroupRow PickProductGroup()
        {
            if (_productGroups == null)
                _productGroups = _service.LoadProductGroups();
            if (GroupPickerOpenerForTest != null)
                return GroupPickerOpenerForTest(_productGroups);
            using (var dlg = new ProductGroupTreePickerDialog(_productGroups))
                return dlg.ShowDialog(this) == DialogResult.OK ? dlg.SelectedGroup : null;
        }

        private PromotionProductItem PickProduct(string title)
        {
            if (_products == null)
                _products = _service.LoadProductsForPicker();
            if (ProductPickerOpenerForTest != null)
                return ProductPickerOpenerForTest(_products, title);
            using (var dlg = new PromotionProductPickerDialog(_products, title))
                return dlg.ShowDialog(this) == DialogResult.OK ? dlg.SelectedProduct : null;
        }
    }
}
