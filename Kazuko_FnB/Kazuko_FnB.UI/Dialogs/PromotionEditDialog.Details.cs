using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Dựng vùng chi tiết theo loại khuyến mại và cấu hình cột lưới cho <see cref="PromotionEditDialog"/>
    /// (spec docs/specs/danh-muc-khuyen-mai.md §2, §3). Thao tác thêm/xóa/sửa ô dòng ở <c>PromotionEditDialog.Lines.cs</c>.
    /// </summary>
    internal sealed partial class PromotionEditDialog
    {
        private bool _cellParseFailed;
        private bool _updatingDetailCell;

        private void BuildDetailHost()
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _detailHost.Padding = new Padding(0, (int)(4 * s), 0, (int)(4 * s));

            // 1. Panel cho Giảm giá tổng bill (TILEGIAMGIA ở đầu đợt).
            int capW = TextRenderer.MeasureText(_lblTotalBillCaption.Text, bodyFont).Width + (int)(16 * s);
            _lblTotalBillCaption.Font = bodyFont;
            _lblTotalBillCaption.SetBounds(0, (int)(16 * s), capW, (int)(28 * s));
            _numHeaderPercent.Font = bodyFont;
            _numHeaderPercent.DecimalPlaces = 2;
            _numHeaderPercent.Minimum = 0m;
            _numHeaderPercent.Maximum = 100m;
            _numHeaderPercent.TextAlign = HorizontalAlignment.Right;
            _numHeaderPercent.SetBounds(capW + (int)(8 * s), (int)(16 * s), (int)(130 * s), (int)(28 * s));
            _lblTotalBillHint.Font = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);
            _lblTotalBillHint.SetBounds(_numHeaderPercent.Right + (int)(12 * s), (int)(16 * s), (int)(460 * s), (int)(28 * s));
            _pnlTotalBill.Controls.Add(_lblTotalBillCaption);
            _pnlTotalBill.Controls.Add(_numHeaderPercent);
            _pnlTotalBill.Controls.Add(_lblTotalBillHint);

            // 2. Panel cho 8 loại có lưới chi tiết.
            _detailToolPanel.Height = (int)(42 * s);
            _detailToolFlow.Padding = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            foreach (FlatButton b in new[] { _btnAddLine, _btnPickGift, _btnSameGift, _btnDeleteLine })
            {
                b.Font = bodyFont;
                b.Height = (int)(34 * s);
                b.Radius = (int)(6 * s);
                b.Margin = new Padding(0, 0, (int)(8 * s), 0);
                _detailToolFlow.Controls.Add(b);
            }
            _btnAddLine.Click += delegate { AddDetailLine(); };
            _btnPickGift.Click += delegate { PickGiftProductForCurrentLine(); };
            _btnSameGift.Click += delegate { ClearGiftProductForCurrentLine(); };
            _btnDeleteLine.Click += delegate { DeleteSelectedDetailLine(); };
            _detailToolPanel.Controls.Add(_detailToolFlow);

            SetupDetailGrid(s);
            _pnlDetailGrid.Controls.Add(_detailGrid);
            _pnlDetailGrid.Controls.Add(_detailToolPanel);

            _detailHost.Controls.Add(_pnlTotalBill);
            _detailHost.Controls.Add(_pnlDetailGrid);
            _detailHost.Controls.Add(_lblNoTypeHint);
            Body.Controls.Add(_detailHost);
        }

        private void SetupDetailGrid(float s)
        {
            _detailGrid.Dock = DockStyle.Fill;
            _detailGrid.AllowUserToAddRows = false;
            _detailGrid.AllowUserToDeleteRows = false;
            _detailGrid.AllowUserToResizeRows = false;
            _detailGrid.AutoGenerateColumns = false;
            _detailGrid.MultiSelect = false;
            _detailGrid.RowHeadersVisible = false;
            _detailGrid.ScrollBars = ScrollBars.Both;
            _detailGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _detailGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _detailGrid.ColumnHeadersHeight = (int)(36 * s);
            _detailGrid.RowTemplate.Height = (int)(30 * s);
            _detailGrid.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _detailGrid.ColumnHeadersDefaultCellStyle.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _detailGrid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            _detailGrid.CellParsing += OnDetailCellParsing;
            _detailGrid.CellEndEdit += OnDetailCellEndEdit;
            _detailGrid.CellDoubleClick += OnDetailCellDoubleClick;
            _detailGrid.SelectionChanged += delegate { UpdateDetailToolbarButtons(); };
            _detailGrid.CurrentCellDirtyStateChanged += delegate
            {
                NumericEditGridCell.HandleDirtyStateChanged(_detailGrid, OnFieldChanged);
            };
            _detailGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                DetailDataErrorCountForTest++;
                DetailDataErrorDetailForTest = NumericEditGridCell.AppendDataErrorDetail(DetailDataErrorDetailForTest, _detailGrid, e);
                e.ThrowException = false;
                if (e.RowIndex >= 0 && e.RowIndex < _details.Count)
                    SyncDetailRow(e.RowIndex);
            };
        }

        private void SwitchDetailSection(PromotionKind kind)
        {
            if (kind == PromotionKind.TotalBillPercent)
            {
                _pnlTotalBill.Visible = true;
                _pnlDetailGrid.Visible = false;
                _lblNoTypeHint.Visible = false;
            }
            else if (PromotionRules.UsesDetailGrid(kind))
            {
                _pnlTotalBill.Visible = false;
                _pnlDetailGrid.Visible = true;
                _lblNoTypeHint.Visible = false;
                ConfigureDetailToolbarForKind(kind);
                BuildDetailGridColumns(kind);
                RebuildDetailGridRows();
            }
            else
            {
                _pnlTotalBill.Visible = false;
                _pnlDetailGrid.Visible = false;
                _lblNoTypeHint.Visible = true;
            }
            UpdateDetailToolbarButtons();
            ApplyDialogTheme();
        }

        private void ConfigureDetailToolbarForKind(PromotionKind kind)
        {
            float s = Dpi;
            switch (kind)
            {
                case PromotionKind.ProductGroupPercent:
                    _btnAddLine.Text = "Thêm nhóm...";
                    break;
                case PromotionKind.OrderValuePercent:
                case PromotionKind.QuantityPercent:
                    _btnAddLine.Text = "Thêm mức";
                    break;
                case PromotionKind.GiftByOrderValue:
                    _btnAddLine.Text = "Thêm mức tặng...";
                    break;
                default:
                    _btnAddLine.Text = "Thêm mặt hàng...";
                    break;
            }
            _btnPickGift.Visible = !_readOnly && (kind == PromotionKind.BuyXGetY || kind == PromotionKind.GiftByOrderValue);
            _btnSameGift.Visible = !_readOnly && kind == PromotionKind.BuyXGetY;
            _btnAddLine.Visible = !_readOnly;
            _btnDeleteLine.Visible = !_readOnly;

            foreach (FlatButton b in new[] { _btnAddLine, _btnPickGift, _btnSameGift, _btnDeleteLine })
            {
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(24 * s);
            }
        }

        private void BuildDetailGridColumns(PromotionKind kind)
        {
            float s = Dpi;
            _detailGrid.Columns.Clear();
            switch (kind)
            {
                case PromotionKind.ProductGroupPercent:
                    AddDetailTextCol("col_group", "Nhóm mặt hàng", 240, true, true, s);
                    AddDetailNumericCol("col_pct", "Tỉ lệ giảm (%)", 135, 2, false, s);
                    AddDetailTextCol("col_note", "Ghi chú", 180, false, false, s);
                    break;

                case PromotionKind.OrderValuePercent:
                    AddDetailNumericCol("col_orderval", "Giá trị đơn hàng từ", 185, 0, false, s);
                    AddDetailNumericCol("col_pct", "Tỉ lệ giảm (%)", 135, 2, false, s);
                    AddDetailTextCol("col_note", "Ghi chú", 220, false, true, s);
                    break;

                case PromotionKind.QuantityPercent:
                    AddDetailNumericCol("col_qty", "Tổng số lượng từ", 170, 2, false, s);
                    AddDetailNumericCol("col_pct", "Tỉ lệ giảm (%)", 135, 2, false, s);
                    AddDetailTextCol("col_note", "Ghi chú", 220, false, true, s);
                    break;

                case PromotionKind.BuyXGetY:
                    AddDetailTextCol("col_prod", "Mặt hàng mua", 185, true, true, s);
                    AddDetailTextCol("col_unit", "ĐVT", 65, true, false, s);
                    AddDetailNumericCol("col_buyqty", "SL mua", 85, 2, false, s);
                    AddDetailNumericCol("col_giftqty", "SL tặng", 85, 2, false, s);
                    AddDetailTextCol("col_giftprod", "Mặt hàng tặng", 175, true, false, s);
                    AddDetailTextCol("col_note", "Ghi chú", 120, false, false, s);
                    break;

                case PromotionKind.FixedPrice:
                    AddDetailTextCol("col_prod", "Mặt hàng", 210, true, true, s);
                    AddDetailTextCol("col_unit", "ĐVT", 70, true, false, s);
                    AddDetailNumericCol("col_baseprice", "Giá bán hiện tại", 135, 0, true, s);
                    AddDetailNumericCol("col_saleprice", "Giá khuyến mại", 135, 0, false, s);
                    AddDetailTextCol("col_note", "Ghi chú", 140, false, false, s);
                    break;

                case PromotionKind.ProductPercent:
                    AddDetailTextCol("col_prod", "Mặt hàng", 240, true, true, s);
                    AddDetailTextCol("col_unit", "ĐVT", 75, true, false, s);
                    AddDetailNumericCol("col_pct", "Tỉ lệ giảm (%)", 135, 2, false, s);
                    AddDetailTextCol("col_note", "Ghi chú", 180, false, false, s);
                    break;

                case PromotionKind.ProductAmount:
                    AddDetailTextCol("col_prod", "Mặt hàng", 210, true, true, s);
                    AddDetailTextCol("col_unit", "ĐVT", 70, true, false, s);
                    AddDetailNumericCol("col_baseprice", "Giá bán hiện tại", 135, 0, true, s);
                    AddDetailNumericCol("col_saleprice", "Tiền giảm / ĐV", 135, 0, false, s);
                    AddDetailTextCol("col_note", "Ghi chú", 140, false, false, s);
                    break;

                case PromotionKind.GiftByOrderValue:
                    AddDetailNumericCol("col_orderval", "Giá trị đơn hàng từ", 170, 0, false, s);
                    AddDetailNumericCol("col_giftqty", "SL tặng", 85, 2, false, s);
                    AddDetailTextCol("col_giftprod", "Mặt hàng tặng", 200, true, true, s);
                    AddDetailNumericCol("col_giftprice", "Giá bán", 110, 0, true, s);
                    AddDetailTextCol("col_note", "Ghi chú", 130, false, false, s);
                    break;
            }
            _detailGrid.ReadOnly = _readOnly;
        }

        private void AddDetailTextCol(string name, string header, int width96, bool readOnly, bool fill, float s)
        {
            int headerNeed = TextRenderer.MeasureText(header, _detailGrid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(26 * s);
            int w = Math.Max((int)(width96 * s), headerNeed);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = headerNeed,
                ReadOnly = readOnly || _readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
            };
            _detailGrid.Columns.Add(col);
        }

        private void AddDetailNumericCol(string name, string header, int width96, int decimalPlaces, bool readOnly, float s)
        {
            int headerNeed = TextRenderer.MeasureText(header, _detailGrid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(26 * s);
            int w = Math.Max((int)(width96 * s), headerNeed);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = headerNeed,
                ReadOnly = readOnly || _readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (!readOnly)
            {
                NumericEditGridCell.ConfigureColumn(col, decimalPlaces);
            }
            else
            {
                col.ValueType = typeof(decimal);
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.DefaultCellStyle.Format = decimalPlaces > 0 ? "#,##0." + new string('#', decimalPlaces) : "N0";
                col.DefaultCellStyle.FormatProvider = NumberInput.ViFormat;
            }
            _detailGrid.Columns.Add(col);
        }

        private void RebuildDetailGridRows()
        {
            PromotionKind kind = CurrentKind();
            _updatingDetailCell = true;
            try
            {
                _detailGrid.Rows.Clear();
                for (int i = 0; i < _details.Count; i++)
                {
                    int idx = _detailGrid.Rows.Add();
                    _detailGrid.Rows[idx].Tag = _details[i];
                    FillGridRow(kind, _detailGrid.Rows[idx], _details[i]);
                }
            }
            finally
            {
                _updatingDetailCell = false;
            }
            UpdateDetailToolbarButtons();
        }

        private void SyncDetailRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _details.Count || rowIndex >= _detailGrid.Rows.Count)
                return;
            _updatingDetailCell = true;
            try
            {
                FillGridRow(CurrentKind(), _detailGrid.Rows[rowIndex], _details[rowIndex]);
            }
            finally
            {
                _updatingDetailCell = false;
            }
        }

        private static void FillGridRow(PromotionKind kind, DataGridViewRow r, PromotionDetailRow d)
        {
            switch (kind)
            {
                case PromotionKind.ProductGroupPercent:
                    r.Cells["col_group"].Value = d.ProductGroupName ?? "";
                    r.Cells["col_pct"].Value = d.DiscountPercent;
                    r.Cells["col_note"].Value = d.Note ?? "";
                    break;

                case PromotionKind.OrderValuePercent:
                    r.Cells["col_orderval"].Value = d.OrderValue;
                    r.Cells["col_pct"].Value = d.DiscountPercent;
                    r.Cells["col_note"].Value = d.Note ?? "";
                    break;

                case PromotionKind.QuantityPercent:
                    r.Cells["col_qty"].Value = d.ItemQuantity;
                    r.Cells["col_pct"].Value = d.DiscountPercent;
                    r.Cells["col_note"].Value = d.Note ?? "";
                    break;

                case PromotionKind.BuyXGetY:
                    r.Cells["col_prod"].Value = d.ProductName ?? "";
                    r.Cells["col_unit"].Value = d.ProductUnitName ?? "";
                    r.Cells["col_buyqty"].Value = d.BuyQuantity;
                    r.Cells["col_giftqty"].Value = d.GiftQuantity;
                    r.Cells["col_giftprod"].Value = string.IsNullOrEmpty(d.GiftProductId)
                        ? PromotionText.SameProductGiftLabel
                        : (d.GiftProductName ?? "");
                    r.Cells["col_note"].Value = d.Note ?? "";
                    break;

                case PromotionKind.FixedPrice:
                case PromotionKind.ProductAmount:
                    r.Cells["col_prod"].Value = d.ProductName ?? "";
                    r.Cells["col_unit"].Value = d.ProductUnitName ?? "";
                    r.Cells["col_baseprice"].Value = d.ProductSellPrice;
                    r.Cells["col_saleprice"].Value = d.SalePrice;
                    r.Cells["col_note"].Value = d.Note ?? "";
                    break;

                case PromotionKind.ProductPercent:
                    r.Cells["col_prod"].Value = d.ProductName ?? "";
                    r.Cells["col_unit"].Value = d.ProductUnitName ?? "";
                    r.Cells["col_pct"].Value = d.DiscountPercent;
                    r.Cells["col_note"].Value = d.Note ?? "";
                    break;

                case PromotionKind.GiftByOrderValue:
                    r.Cells["col_orderval"].Value = d.OrderValue;
                    r.Cells["col_giftqty"].Value = d.GiftQuantity;
                    r.Cells["col_giftprod"].Value = d.GiftProductName ?? "";
                    r.Cells["col_giftprice"].Value = d.GiftProductSellPrice;
                    r.Cells["col_note"].Value = d.Note ?? "";
                    break;
            }
        }
    }
}
