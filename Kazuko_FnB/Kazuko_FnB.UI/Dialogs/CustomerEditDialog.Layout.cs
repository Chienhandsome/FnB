using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần dựng bố cục 2 cột và áp bảng màu cho <see cref="CustomerEditDialog"/>.</summary>
    internal sealed partial class CustomerEditDialog
    {
        private void BuildFields()
        {
            float s = Dpi;
            _sectionFont = new Font("Segoe UI Semibold", 9.5f);

            _points.DecimalPlaces = 0;
            _points.Minimum = 0m;
            _points.Maximum = int.MaxValue;
            _points.TextAlign = HorizontalAlignment.Right;

            foreach (string label in CustomerText.PriceTypeNames)
                _priceType.Items.Add(label);
            _priceType.SelectedIndex = 0;

            int tab = 0;
            // Cột trái: Định danh + Liên hệ
            AddSection("Định danh", 14, 6, 360);
            PlaceRow("Mã khách *", _code, 14, 26, 128, 232, 24, ref tab);
            PlaceRow("Tên khách hàng *", _name, 14, 56, 128, 232, 24, ref tab);
            PlaceRow("Nhóm khách hàng", _group, 14, 86, 128, 232, 24, ref tab);
            PlaceRow("Nhân viên", _employee, 14, 116, 128, 232, 24, ref tab);

            AddSection("Liên hệ", 14, 148, 360);
            PlaceRow("Điện thoại", _phone, 14, 168, 128, 232, 24, ref tab);
            PlaceRow("Email", _email, 14, 198, 128, 232, 24, ref tab);
            PlaceRow("Địa chỉ", _address, 14, 228, 128, 232, 24, ref tab);
            PlaceRow("Tỉnh thành", _province, 14, 258, 128, 232, 24, ref tab);

            // Cột phải: Pháp lý + Khác
            AddSection("Pháp lý & mạng xã hội", 394, 6, 372);
            PlaceRow("Mã số thuế", _taxCode, 394, 26, 160, 206, 24, ref tab);
            PlaceRow("Facebook", _facebook, 394, 56, 160, 206, 24, ref tab);

            AddSection("Khác", 394, 88, 372);
            PlaceRow("Ngày thành lập/sinh nhật", _birthday, 394, 108, 160, 206, 26, ref tab);
            PlaceRow("Điểm tích lũy ban đầu", _points, 394, 140, 160, 206, 24, ref tab);
            PlaceRow("Giá bán", _priceType, 394, 170, 160, 206, 24, ref tab);
            PlaceRow("Thẻ trả trước", _prepaidCard, 394, 200, 160, 206, 24, ref tab);
            PlaceRow("Ghi chú", _note, 394, 230, 160, 206, 52, ref tab);

            _lblError.SetBounds((int)(14 * s), (int)(288 * s), (int)(752 * s), (int)(24 * s));
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

        private void PlaceRow(string caption, Control editor, int x, int y, int labelW, int editW, int h, ref int tabIndex)
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
            lbl.SetBounds((int)(x * s), (int)(y * s), (int)(labelW * s), (int)(Math.Min(h, 24) * s));
            editor.SetBounds((int)((x + labelW + 4) * s), (int)(y * s), (int)(editW * s), (int)(h * s));
            editor.TabIndex = tabIndex++;
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);

            _birthday.ApplyTheme(t, t.Background);
            if (!_birthday.Enabled)
            {
                _birthday.FieldColor = disabledBack;
                _birthday.Inner.BackColor = disabledBack;
                _birthday.Invalidate();
            }

            foreach (ComboBox cb in new ComboBox[] { _group, _employee, _province, _priceType, _prepaidCard })
                cb.BackColor = cb.Enabled ? t.Surface : disabledBack;
            _points.BackColor = _points.Enabled && !_points.ReadOnly ? t.Surface : disabledBack;

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
