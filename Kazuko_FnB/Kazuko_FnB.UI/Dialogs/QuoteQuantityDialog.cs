using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "Nhập số lượng" cho màn Báo giá khi mặt hàng có 2 đơn vị tính (docs/specs/bao-gia-ban-hang.md §2.1, §5.0.2):
    /// gồm số lượng lẻ, số lượng chẵn và ô chiết khấu bị KHÓA theo đúng app gốc.
    /// </summary>
    internal sealed class QuoteQuantityDialog : ThemedDialog
    {
        private readonly NumericUpDown _odd = new NumericUpDown();
        private readonly NumericUpDown _even = new NumericUpDown();
        private readonly NumericUpDown _discount = new NumericUpDown();

        public decimal OddQuantity { get { return _odd.Value; } }
        public decimal EvenQuantity { get { return _even.Value; } }

        internal NumericUpDown OddBoxForTest { get { return _odd; } }
        internal NumericUpDown EvenBoxForTest { get { return _even; } }
        internal NumericUpDown DiscountBoxForTest { get { return _discount; } }

        public QuoteQuantityDialog(QuoteSaleItem item, decimal oddDefault)
            : base("NHẬP SỐ LƯỢNG", item == null ? "" : item.Name, 360, 240)
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165 * s));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            string oddSuffix = item == null || string.IsNullOrEmpty(item.UnitName) ? "" : " (" + item.UnitName + ")";
            string evenSuffix = item == null || string.IsNullOrEmpty(item.BigUnitName) ? "" : " (" + item.BigUnitName + ")";

            ConfigureQtyBox(_odd, oddDefault, bodyFont);
            ConfigureQtyBox(_even, 0m, bodyFont);
            _even.Enabled = item != null && item.HasBigUnit;

            // Ô chiết khấu khóa theo spec §2.1 ("ô chiết khấu bị khóa").
            ConfigureQtyBox(_discount, 0m, bodyFont);
            _discount.Maximum = 100m;
            _discount.ReadOnly = true;
            _discount.Enabled = false;

            grid.Controls.Add(MakeLabel("Số lượng lẻ" + oddSuffix, bodyFont), 0, 0);
            grid.Controls.Add(_odd, 1, 0);
            grid.Controls.Add(MakeLabel("Số lượng chẵn" + evenSuffix, bodyFont), 0, 1);
            grid.Controls.Add(_even, 1, 1);
            grid.Controls.Add(MakeLabel("Chiết khấu (%)", bodyFont), 0, 2);
            grid.Controls.Add(_discount, 1, 2);

            var hint = new Label
            {
                Text = "Lượng chẵn được thêm với đơn vị chẵn và giá bán chẵn.",
                Dock = DockStyle.Fill,
                Tag = "muted",
                Font = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize)
            };
            grid.Controls.Add(hint, 0, 3);
            grid.SetColumnSpan(hint, 2);

            Body.Controls.Add(grid);

            FlatButton cancel = AddButton("Thoát", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            FlatButton ok = AddButton("OK", "primary", delegate { DialogResult = DialogResult.OK; Close(); });
            CancelButton = cancel;
            AcceptButton = ok;

            Load += delegate
            {
                _odd.Focus();
                _odd.Select(0, _odd.Text.Length);
            };
        }

        private static Label MakeLabel(string text, Font font)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = font
            };
        }

        private static void ConfigureQtyBox(NumericUpDown num, decimal initial, Font font)
        {
            num.DecimalPlaces = 2;
            num.Minimum = 0m;
            num.Maximum = 1000000m;
            num.Value = initial < 0m ? 0m : (initial > num.Maximum ? num.Maximum : initial);
            num.Font = font;
            num.TextAlign = HorizontalAlignment.Right;
            num.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
    }
}
