using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "Nhập số lượng" (§6.2 Q13): nhập số lượng lẻ + chẵn cho mặt hàng có 2 ĐVT,
    /// theo ThemSoLuongChan của app gốc (lượng chẵn thành một dòng riêng, giá GIABANCHAN).
    /// Ô chẵn chỉ bật khi mặt hàng có ĐVT chẵn.
    /// </summary>
    internal sealed class QuantityDialog : ThemedDialog
    {
        private readonly NumericUpDown _odd = new NumericUpDown();
        private readonly NumericUpDown _even = new NumericUpDown();

        /// <summary>Số lượng lẻ (ĐVT bán).</summary>
        public decimal OddQuantity { get { return _odd.Value; } }

        /// <summary>Số lượng chẵn (ĐVT chẵn); 0 nếu không nhập.</summary>
        public decimal EvenQuantity { get { return _even.Value; } }

        public QuantityDialog(BookingSaleItem item, decimal oddDefault)
            : base("NHẬP SỐ LƯỢNG", item == null ? "" : item.Name, 340, 210)
        {
            float s = Dpi;
            var lblOdd = new Label { Text = "Số lượng lẻ" + (item == null || string.IsNullOrEmpty(item.UnitName) ? "" : " (" + item.UnitName + ")"), AutoSize = true };
            lblOdd.SetBounds(0, (int)(10 * s), (int)(150 * s), (int)(20 * s));
            _odd.SetBounds((int)(160 * s), (int)(8 * s), (int)(130 * s), (int)(26 * s));
            _odd.DecimalPlaces = 2;
            _odd.Minimum = 0m;
            _odd.Maximum = 1000000m;
            _odd.Value = oddDefault < 0m ? 0m : (oddDefault > _odd.Maximum ? _odd.Maximum : oddDefault);
            _odd.Font = new Font("Segoe UI", 10f);
            _odd.TextAlign = HorizontalAlignment.Right;

            var lblEven = new Label { Text = "Số lượng chẵn" + (item == null || string.IsNullOrEmpty(item.BigUnitName) ? "" : " (" + item.BigUnitName + ")"), AutoSize = true };
            lblEven.SetBounds(0, (int)(48 * s), (int)(150 * s), (int)(20 * s));
            _even.SetBounds((int)(160 * s), (int)(46 * s), (int)(130 * s), (int)(26 * s));
            _even.DecimalPlaces = 2;
            _even.Minimum = 0m;
            _even.Maximum = 1000000m;
            _even.Value = 0m;
            _even.Font = new Font("Segoe UI", 10f);
            _even.TextAlign = HorizontalAlignment.Right;
            _even.Enabled = item != null && item.HasBigUnit;

            var hint = new Label
            {
                Text = "Lượng chẵn được thêm thành một dòng riêng với giá bán chẵn.",
                AutoSize = false,
                Width = (int)(300 * s),
                Height = (int)(34 * s)
            };
            hint.SetBounds(0, (int)(84 * s), (int)(300 * s), (int)(34 * s));

            Body.Controls.Add(lblOdd);
            Body.Controls.Add(_odd);
            Body.Controls.Add(lblEven);
            Body.Controls.Add(_even);
            Body.Controls.Add(hint);

            FlatButton cancel = AddButton("Hủy bỏ", null, delegate { Close(); });
            AddButton("OK", "primary", delegate { DialogResult = DialogResult.OK; Close(); });
            CancelButton = cancel;

            Load += delegate { _odd.Focus(); _odd.Select(0, _odd.Text.Length); };
        }
    }
}
