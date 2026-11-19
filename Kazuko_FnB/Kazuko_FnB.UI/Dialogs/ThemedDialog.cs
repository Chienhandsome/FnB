using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Nền chung cho các hộp thoại: thanh tiêu đề màu theme, thân, chân chứa nút (căn phải).
    /// Tự tô lại khi đổi theme (xem thử/áp dụng). Kích thước truyền theo 96 DPI, tự nhân theo DeviceDpi.
    /// </summary>
    internal class ThemedDialog : BaseForm
    {
        private readonly Panel _header = new Panel { Dock = DockStyle.Top, Tag = "header" };
        private readonly Panel _body = new Panel { Dock = DockStyle.Fill };
        private readonly Panel _footer = new Panel { Dock = DockStyle.Bottom, Tag = "footer" };
        private readonly FlowLayoutPanel _buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Tag = "footer"
        };
        // UseMnemonic = false để dấu & trong tiêu đề/mô tả hiện ra đúng chữ (không bị nuốt làm phím tắt).
        private readonly Label _title = new Label { AutoSize = false, Tag = "header", UseMnemonic = false };
        private readonly Label _subtitle = new Label { AutoSize = false, Tag = "header", UseMnemonic = false };

        public ThemedDialog(string title, string subtitle, int widthAt96, int heightAt96)
        {
            float s = DeviceDpi / 96f;
            // Font, icon, AutoScaleMode.None và đăng ký ThemeChanged do BaseForm lo.
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            Text = title;
            ClientSize = new Size((int)(widthAt96 * s), (int)(heightAt96 * s));

            _title.Font = new Font("Segoe UI Semibold", 12f);
            _title.Text = title;
            _title.SetBounds((int)(16 * s), (int)(8 * s), 4000, (int)(26 * s));
            _subtitle.Font = new Font("Segoe UI", 9f);
            _subtitle.Text = subtitle ?? "";
            _subtitle.SetBounds((int)(16 * s), (int)(34 * s), 4000, (int)(20 * s));
            _header.Height = (int)(60 * s);
            _header.Controls.Add(_title);
            _header.Controls.Add(_subtitle);

            _footer.Height = (int)(56 * s);
            _buttons.Padding = new Padding((int)(12 * s), (int)(10 * s), (int)(12 * s), (int)(8 * s));
            _footer.Controls.Add(_buttons);

            _body.Padding = new Padding((int)(12 * s));

            // Thứ tự thêm quyết định thứ tự dock: thân → chân → tiêu đề.
            Controls.Add(_body);
            Controls.Add(_footer);
            Controls.Add(_header);

            Load += delegate { ApplyDialogTheme(); };
        }

        /// <summary>Thanh chân hộp thoại (chứa các nút); lớp con có thể thêm control dock bên trái (vd. dòng thông báo).</summary>
        protected Panel FooterPanel
        {
            get { return _footer; }
        }

        /// <summary>Vùng nội dung (Padding 12px); thêm control con vào đây.</summary>
        protected Panel Body
        {
            get { return _body; }
        }

        /// <summary>Đổi tiêu đề (thanh tiêu đề cửa sổ và dải màu).</summary>
        protected void SetTitle(string title)
        {
            Text = title;
            _title.Text = title;
        }

        protected string SubtitleText
        {
            get { return _subtitle.Text; }
            set { _subtitle.Text = value ?? ""; }
        }

        protected float Dpi
        {
            get { return DeviceDpi / 96f; }
        }

        /// <summary>
        /// Thêm nút vào chân hộp thoại. Nút đầu tiên được thêm nằm ngoài cùng bên phải.
        /// kind: "primary" (nút chính), "danger", hoặc null (nút thường).
        /// </summary>
        protected FlatButton AddButton(string text, string kind, EventHandler onClick)
        {
            float s = Dpi;
            var b = new FlatButton
            {
                Text = text,
                Tag = kind,
                Size = new Size((int)(Math.Max(84, TextRenderer.MeasureText(text, Font).Width / s + 28) * s), (int)(38 * s)),
                Margin = new Padding(0, 0, (int)(8 * s), 0),
                Radius = (int)(7 * s)
            };
            if (onClick != null)
                b.Click += onClick;
            _buttons.Controls.Add(b);
            return b;
        }

        // BaseForm gọi khi đổi theme.
        protected override void ApplyFormTheme()
        {
            ApplyDialogTheme();
        }

        /// <summary>Tô lại toàn bộ cây control. Lớp con có control tự vẽ riêng thì ghi đè và gọi base trước.</summary>
        protected virtual void ApplyDialogTheme()
        {
            ThemeTokens t = ThemeManager.Current;
            BackColor = t.Background;
            ForeColor = t.Text;
            ThemeApplier.Apply(this, t);
            // Nút nằm trên nền chân hộp (không phải nền thân).
            foreach (Control c in _buttons.Controls)
            {
                var fb = c as FlatButton;
                if (fb != null)
                {
                    fb.SurfaceColor = _footer.BackColor;
                    fb.Invalidate();
                }
            }
            Invalidate(true);
        }
    }
}
