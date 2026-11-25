using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Ô ngày bo góc cùng kiểu InputBox (thay DateTimePicker của Windows vốn luôn có viền và nút riêng): gõ ngày
    /// dạng dd/MM/yyyy (hoặc d/M/yyyy), Enter hoặc rời ô thì kiểm tra, ngày sai quay về ngày cũ; bấm biểu tượng
    /// lịch bên phải để chọn bằng lịch tháng. Tùy chọn AllowEmpty (mặc định tắt) cho phép để trống (NullableValue = null).
    /// </summary>
    internal sealed class DateBox : FieldFrame
    {
        private const string Format = "dd/MM/yyyy";
        private static readonly string[] Formats = { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" };

        private readonly PlaceholderTextBox _tb;
        private bool _allowEmpty;
        private DateTime? _value = DateTime.Today;
        private ToolStripDropDown _drop;

        public event EventHandler ValueChanged;

        public DateBox() : this(false)
        {
        }

        public DateBox(bool allowEmpty) : this(new PlaceholderTextBox(), allowEmpty)
        {
        }

        private DateBox(PlaceholderTextBox tb, bool allowEmpty) : base(tb, 0)
        {
            _tb = tb;
            _allowEmpty = allowEmpty;
            RightReserve = 30;
            _tb.BorderStyle = BorderStyle.None;
            if (allowEmpty)
            {
                _value = null;
                _tb.Text = "";
                _tb.Placeholder = "__/__/____";
            }
            else
            {
                _value = DateTime.Today;
                _tb.Text = _value.Value.ToString(Format, CultureInfo.InvariantCulture);
            }
            _tb.Leave += delegate { Commit(); };
            _tb.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                    Commit();
            };
            Cursor = Cursors.IBeam;
        }

        /// <summary>TextBox bên trong (để gắn KeyDown, đặt font, focus).</summary>
        public TextBox Inner
        {
            get { return _tb; }
        }

        /// <summary>Cho phép bỏ trống ô ngày (mặc định false để giữ nguyên hành vi các màn cũ).</summary>
        public bool AllowEmpty
        {
            get { return _allowEmpty; }
            set
            {
                _allowEmpty = value;
                if (!_allowEmpty && !_value.HasValue)
                {
                    _value = DateTime.Today;
                    _tb.Text = _value.Value.ToString(Format, CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>Chữ gợi ý hiển thị khi ô ngày đang trống.</summary>
        public string Placeholder
        {
            get { return _tb.Placeholder; }
            set { _tb.Placeholder = value; }
        }

        public new void ApplyTheme(ThemeTokens t, Color surface)
        {
            base.ApplyTheme(t, surface);
            _tb.PlaceholderColor = Draw.Mix(FieldColor, t.TextMuted, 0.75f);
            _tb.Invalidate();
        }

        public DateTime Value
        {
            get
            {
                Commit();
                return _value ?? DateTime.Today;
            }
            set
            {
                NullableValue = value.Date;
            }
        }

        /// <summary>Giá trị ngày có thể rỗng (khi AllowEmpty = true); khi AllowEmpty = false luôn có ngày.</summary>
        public DateTime? NullableValue
        {
            get
            {
                Commit();
                return _value;
            }
            set
            {
                DateTime? next = value.HasValue
                    ? (DateTime?)value.Value.Date
                    : (_allowEmpty ? (DateTime?)null : (_value ?? DateTime.Today));
                bool changed = next != _value;
                _value = next;
                _tb.Text = _value.HasValue ? _value.Value.ToString(Format, CultureInfo.InvariantCulture) : "";
                if (changed && ValueChanged != null)
                    ValueChanged(this, EventArgs.Empty);
            }
        }

        public void Commit()
        {
            string text = (_tb.Text ?? "").Trim();
            if (_allowEmpty && text.Length == 0)
            {
                if (_value.HasValue)
                {
                    _value = null;
                    _tb.Text = "";
                    if (ValueChanged != null)
                        ValueChanged(this, EventArgs.Empty);
                }
                else
                {
                    _tb.Text = "";
                }
                return;
            }

            DateTime parsed;
            if (DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                DateTime d = parsed.Date;
                if (!_value.HasValue || _value.Value != d)
                {
                    _value = d;
                    _tb.Text = d.ToString(Format, CultureInfo.InvariantCulture);
                    if (ValueChanged != null)
                        ValueChanged(this, EventArgs.Empty);
                }
                else
                {
                    _tb.Text = d.ToString(Format, CultureInfo.InvariantCulture);
                }
            }
            else
            {
                // Ngày sai định dạng → giữ nguyên giá trị cũ (nếu đang trống thì vẫn trống, không âm thầm thành ngày lạ).
                _tb.Text = _value.HasValue ? _value.Value.ToString(Format, CultureInfo.InvariantCulture) : "";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = DeviceDpi / 96f;
            var area = new Rectangle(Width - (int)(RightReserve * s) - (int)(4 * s), 0, (int)(RightReserve * s), Height);
            using (var font = new Font("Segoe MDL2 Assets", 11f))
                TextRenderer.DrawText(e.Graphics, "", font, area, _tb.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            float s = DeviceDpi / 96f;
            if (e.X >= Width - (int)((RightReserve + 4) * s))
                ShowCalendar();
            else
                _tb.Focus();
        }

        private void ShowCalendar()
        {
            Commit();
            if (_drop == null)
            {
                var cal = new MonthCalendar { MaxSelectionCount = 1 };
                cal.DateSelected += delegate
                {
                    NullableValue = cal.SelectionStart.Date;
                    _drop.Close();
                };
                _drop = new ToolStripDropDown { Padding = Padding.Empty };
                _drop.Items.Add(new ToolStripControlHost(cal) { Margin = Padding.Empty, Padding = Padding.Empty });
            }
            var host = (ToolStripControlHost)_drop.Items[0];
            ((MonthCalendar)host.Control).SetDate(_value ?? DateTime.Today);
            _drop.Show(this, new Point(0, Height));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _drop != null)
                _drop.Dispose();
            base.Dispose(disposing);
        }
    }
}
