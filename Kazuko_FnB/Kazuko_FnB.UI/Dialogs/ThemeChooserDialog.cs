using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
    /// Quản trị ▸ Giao diện chương trình. Lưới thẻ bảng màu; bấm thẻ = xem thử ngay trên toàn chương trình;
    /// "Áp dụng &amp; Lưu" lưu theo người dùng Windows (settings.txt, khóa Theme); đóng mà chưa lưu thì hoàn lại bảng màu đã lưu.
    /// Bàn phím: mũi tên chuyển thẻ, Enter/Space chọn, Esc đóng.
    /// </summary>
    internal sealed class ThemeChooserDialog : ThemedDialog, IScreenPreview
    {
        private const int CardWidthAt96 = 196;
        private const int CardHeightAt96 = 236;

        private readonly List<ThemeCard> _cards = new List<ThemeCard>();
        // Thông báo "đã lưu": dòng chữ nằm trong thanh nút cuối, bên trái (không chồng lên nội dung).
        private readonly Label _toast = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false, Visible = false, Dock = DockStyle.Left };
        private readonly Timer _toastTimer = new Timer { Interval = 2600 };
        private readonly Font _toastFont = new Font("Segoe UI Semibold", 10f);
        private ThemeTokens _saved;
        private ThemeTokens _selected;
        private int _columns = 4;

        public ThemeChooserDialog(UserSession session)
            : base("GIAO DIỆN CHƯƠNG TRÌNH",
                "Bấm một bảng màu để xem thử ngay trên toàn chương trình, rồi bấm Áp dụng & Lưu để giữ lại.",
                900, 420)
        {
            _saved = BuiltInThemes.Find(AppSettings.Load().ThemeName);
            _selected = ThemeManager.Current;

            // Nút thêm trước nằm ngoài cùng bên phải: Áp dụng & Lưu, rồi Đóng, rồi Khôi phục mặc định.
            AddButton("Áp dụng & Lưu", "primary", delegate { Save(); });
            FlatButton close = AddButton("Đóng", null, delegate { Close(); });
            AddButton("Khôi phục mặc định", null, delegate { Choose(BuiltInThemes.Default); });
            CancelButton = close;
            _toast.Font = _toastFont;

            Body.AutoScroll = true;
            foreach (ThemeTokens t in BuiltInThemes.All())
            {
                var card = new ThemeCard(t);
                card.Chosen += delegate(object sender, EventArgs e) { Choose(((ThemeCard)sender).Tokens); };
                _cards.Add(card);
                Body.Controls.Add(card);
            }

            _toast.Width = (int)(420 * Dpi);
            _toast.Padding = new Padding((int)(16 * Dpi), 0, 0, 0);
            FooterPanel.Controls.Add(_toast);
            _toastTimer.Tick += delegate { _toastTimer.Stop(); _toast.Visible = false; };

            Body.Resize += delegate { LayoutCards(); };
            Load += delegate
            {
                FitToScreen();
                LayoutCards();
                RefreshMarks();
                if (_cards.Count > 0)
                    CardOf(_selected).Focus();
            };
            FormClosing += delegate
            {
                // Chưa lưu thì hoàn lại bảng màu đã lưu (không thay đổi gì nếu vừa lưu).
                if (!ReferenceEquals(ThemeManager.Current, _saved))
                    ThemeManager.Apply(_saved);
            };
        }

        private ThemeCard CardOf(ThemeTokens t)
        {
            foreach (ThemeCard c in _cards)
                if (ReferenceEquals(c.Tokens, t))
                    return c;
            return _cards[0];
        }

        private void FitToScreen()
        {
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            if (Width > wa.Width || Height > wa.Height)
            {
                Size = new Size(Math.Min(Width, wa.Width), Math.Min(Height, wa.Height));
                Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
            }
        }

        private void LayoutCards()
        {
            float s = Dpi;
            int gap = (int)(12 * s);
            int minW = (int)(CardWidthAt96 * s);
            int avail = Body.ClientSize.Width - Body.Padding.Horizontal;
            _columns = Math.Max(2, Math.Min(_cards.Count, (avail + gap) / (minW + gap)));
            int cw = (avail - gap * (_columns - 1)) / _columns;
            int ch = (int)(CardHeightAt96 * s);
            Body.SuspendLayout();
            for (int i = 0; i < _cards.Count; i++)
            {
                int row = i / _columns;
                int col = i % _columns;
                _cards[i].SetBounds(Body.Padding.Left + col * (cw + gap), Body.Padding.Top + row * (ch + gap), cw, ch);
            }
            Body.ResumeLayout();
        }

        private void Choose(ThemeTokens t)
        {
            _selected = t;
            if (!ReferenceEquals(ThemeManager.Current, t))
                ThemeManager.Apply(t);
            RefreshMarks();
        }

        private void RefreshMarks()
        {
            foreach (ThemeCard c in _cards)
            {
                c.Selected = ReferenceEquals(c.Tokens, _selected);
                c.InUse = ReferenceEquals(c.Tokens, _saved);
            }
        }

        private void Save()
        {
            try
            {
                AppSettings settings = AppSettings.Load();
                settings.ThemeName = _selected.Name;
                settings.Save();
            }
            catch (IOException ex)
            {
                Msg.Error(this, "Không lưu được bảng màu: " + ex.Message);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                Msg.Error(this, "Không lưu được bảng màu: " + ex.Message);
                return;
            }
            _saved = _selected;
            RefreshMarks();
            ShowToast("Đã lưu bảng màu \"" + _selected.Name + "\"");
        }

        private void ShowToast(string text, bool autoHide = true)
        {
            _toast.Text = "✓  " + text;
            StyleToast();
            _toast.Visible = true;
            _toastTimer.Stop();
            if (autoHide)
                _toastTimer.Start();
        }

        private void StyleToast()
        {
            ThemeTokens t = ThemeManager.Current;
            _toast.BackColor = Color.Transparent;
            _toast.ForeColor = t.Success;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            StyleToast();
            foreach (ThemeCard c in _cards)
                c.Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            int step = 0;
            switch (keyData)
            {
                case Keys.Left: step = -1; break;
                case Keys.Right: step = 1; break;
                case Keys.Up: step = -_columns; break;
                case Keys.Down: step = _columns; break;
            }
            if (step != 0 && _cards.Count > 0)
            {
                int i = _cards.IndexOf(CardOf(_selected)) + step;
                if (i >= 0 && i < _cards.Count)
                {
                    Choose(_cards[i].Tokens);
                    _cards[i].Focus();
                    Body.ScrollControlIntoView(_cards[i]);
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>--render-screen: "theme=Tên" chọn thử một bảng màu; "toast" hiện thông báo đã lưu.</summary>
        public void Preview(string option)
        {
            if (option != null && option.StartsWith("theme=", StringComparison.Ordinal))
                Choose(BuiltInThemes.Find(option.Substring(6)));
            else if (option == "toast")
                ShowToast("Đã lưu bảng màu \"" + _selected.Name + "\"", false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toastTimer.Dispose();
                _toast.Dispose();
                _toastFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
