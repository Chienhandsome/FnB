using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>
    /// Ô ngày bo góc cùng kiểu InputBox (thay DateTimePicker của Windows vốn luôn có viền và nút riêng): gõ ngày
    /// dạng dd/MM/yyyy (hoặc d/M/yyyy), Enter hoặc rời ô thì kiểm tra, ngày sai quay về ngày cũ; bấm biểu tượng
    /// lịch bên phải để chọn bằng lịch tháng.
    /// </summary>
    internal sealed class DateBox : FieldFrame
    {
        private const string Format = "dd/MM/yyyy";
        private static readonly string[] Formats = { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy" };

        private readonly PlaceholderTextBox _tb;
        private DateTime _value = DateTime.Today;
        private ToolStripDropDown _drop;

        public event EventHandler ValueChanged;

        public DateBox() : this(new PlaceholderTextBox())
        {
        }

        private DateBox(PlaceholderTextBox tb) : base(tb, 0)
        {
            _tb = tb;
            RightReserve = 30;
            _tb.BorderStyle = BorderStyle.None;
            _tb.Text = _value.ToString(Format, CultureInfo.InvariantCulture);
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

        public DateTime Value
        {
            get
            {
                Commit();
                return _value;
            }
            set
            {
                DateTime d = value.Date;
                bool changed = d != _value;
                _value = d;
                _tb.Text = d.ToString(Format, CultureInfo.InvariantCulture);
                if (changed && ValueChanged != null)
                    ValueChanged(this, EventArgs.Empty);
            }
        }

        private void Commit()
        {
            DateTime parsed;
            if (DateTime.TryParseExact(_tb.Text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                if (parsed.Date != _value)
                    Value = parsed;
                else
                    _tb.Text = _value.ToString(Format, CultureInfo.InvariantCulture);
            }
            else
            {
                _tb.Text = _value.ToString(Format, CultureInfo.InvariantCulture); // ngày sai → giữ ngày cũ
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
                    Value = cal.SelectionStart;
                    _drop.Close();
                };
                _drop = new ToolStripDropDown { Padding = Padding.Empty };
                _drop.Items.Add(new ToolStripControlHost(cal) { Margin = Padding.Empty, Padding = Padding.Empty });
            }
            var host = (ToolStripControlHost)_drop.Items[0];
            ((MonthCalendar)host.Control).SetDate(_value);
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
