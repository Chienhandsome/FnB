using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần dựng bố cục 2 cột, ẩn/hiện tỉ lệ giảm và áp bảng màu cho <see cref="CustomerGroupEditDialog"/>.</summary>
    internal sealed partial class CustomerGroupEditDialog
    {
        private void BuildFields()
        {
            float s = Dpi;
            _sectionFont = new Font("Segoe UI Semibold", 9.5f);

            _points.DecimalPlaces = 2;
            _points.Minimum = 0m;
            _points.Maximum = 999999999999m;
            _points.TextAlign = HorizontalAlignment.Right;

            foreach (NumberBox nb in AllDiscountBoxes())
            {
                nb.DecimalPlaces = 2;
                nb.Minimum = 0m;
                nb.Maximum = 100m;
                nb.TextAlign = HorizontalAlignment.Right;
            }

            AddSection("Thông tin nhóm", 14, 6, 346);
            int tab = 0;
            PlaceLeft("Tên nhóm khách hàng *", _name, 28, 24, ref tab);
            PlaceLeft("Ảnh", _image, 60, 26, ref tab);
            PlaceLeft("Điểm tích lũy", _points, 94, 24, ref tab);
            PlaceLeft("Ghi chú", _note, 126, 142, ref tab);

            AddSection("Tỉ lệ giảm giá (%)", 382, 6, 344);
            _rowTotal = CreateRateRow("Tỉ lệ giảm giá", _discountTotal, ref tab);
            _rowGoods = CreateRateRow("Tỉ lệ giảm giá tiền hàng", _discountGoods, ref tab);
            _rowHour = CreateRateRow("Tỉ lệ giảm giá tiền giờ", _discountHour, ref tab);
            _rowFood = CreateRateRow("Tỉ lệ giảm đồ ăn", _discountFood, ref tab);
            _rowDrink = CreateRateRow("Tỉ lệ giảm đồ uống", _discountDrink, ref tab);
            _rowService = CreateRateRow("Tỉ lệ giảm dịch vụ", _discountService, ref tab);
            _rowOther = CreateRateRow("Tỉ lệ giảm đồ khác", _discountOther, ref tab);

            _lblError.SetBounds((int)(14 * s), (int)(280 * s), (int)(712 * s), (int)(24 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private void AddSection(string title, int x, int y, int width)
        {
            float s = Dpi;
            var lbl = new Label
            {
                Text = title,
                AutoSize = false,
                Font = _sectionFont,
                Tag = "muted",
                UseMnemonic = false,
                TabStop = false
            };
            lbl.SetBounds((int)(x * s), (int)(y * s), (int)(width * s), (int)(18 * s));
            Body.Controls.Add(lbl);
        }

        private void PlaceLeft(string caption, Control editor, int y, int h, ref int tabIndex)
        {
            float s = Dpi;
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = false
            };
            lbl.SetBounds((int)(14 * s), (int)(y * s), (int)(146 * s), (int)(Math.Min(h, 24) * s));
            editor.SetBounds((int)(164 * s), (int)(y * s), (int)(196 * s), (int)(h * s));
            editor.TabIndex = tabIndex++;
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
        }

        private RateRow CreateRateRow(string caption, NumberBox box, ref int tabIndex)
        {
            var r = new RateRow
            {
                Caption = new Label
                {
                    Text = caption,
                    AutoSize = false,
                    AutoEllipsis = true,
                    UseMnemonic = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    TabStop = false
                },
                Box = box,
                Percent = new Label
                {
                    Text = "%",
                    AutoSize = false,
                    UseMnemonic = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    TabStop = false
                }
            };
            box.TabIndex = tabIndex++;
            Body.Controls.Add(r.Caption);
            Body.Controls.Add(r.Box);
            Body.Controls.Add(r.Percent);
            return r;
        }

        internal void ApplyDiscountVisibility(bool coTienGio, bool gopChung)
        {
            CustomerGroupDiscountVisibility vis = CustomerRules.GetDiscountVisibility(coTienGio, gopChung);
            SetRowVisible(_rowTotal, vis.ShowTotalDiscount);
            SetRowVisible(_rowGoods, vis.ShowGoodsDiscount);
            SetRowVisible(_rowHour, vis.ShowHourDiscount);
            SetRowVisible(_rowFood, true);
            SetRowVisible(_rowDrink, true);
            SetRowVisible(_rowService, true);
            SetRowVisible(_rowOther, true);

            float s = Dpi;
            int y = 28;
            foreach (RateRow r in new[] { _rowTotal, _rowGoods, _rowHour, _rowFood, _rowDrink, _rowService, _rowOther })
            {
                if (!r.Box.Visible)
                    continue;
                r.Caption.SetBounds((int)(382 * s), (int)(y * s), (int)(178 * s), (int)(24 * s));
                r.Box.SetBounds((int)(564 * s), (int)(y * s), (int)(134 * s), (int)(24 * s));
                r.Percent.SetBounds((int)(702 * s), (int)(y * s), (int)(24 * s), (int)(24 * s));
                y += 34;
            }
        }

        private static void SetRowVisible(RateRow row, bool visible)
        {
            row.Caption.Visible = visible;
            row.Box.Visible = visible;
            row.Percent.Visible = visible;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);
            _image.BackColor = _image.Enabled ? t.Surface : disabledBack;
            _image.ForeColor = t.Text;
            _points.BackColor = _points.Enabled && !_points.ReadOnly ? t.Surface : disabledBack;
            foreach (NumberBox nb in AllDiscountBoxes())
                nb.BackColor = nb.Enabled && !nb.ReadOnly ? t.Surface : disabledBack;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _sectionFont != null)
                _sectionFont.Dispose();
            base.Dispose(disposing);
        }
    }
}
