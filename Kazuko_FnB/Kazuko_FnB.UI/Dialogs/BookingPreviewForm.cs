using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Khách đặt hàng", chỉ để chụp bằng <c>--render-screen preview.booking</c>.</summary>
    internal sealed class BookingPreviewForm : Form
    {
        private readonly BookingListView _view;

        public BookingPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Khách đặt hàng";
            ClientSize = new Size(1300, 720);
            _view = new BookingListView(session.DatabasePath) { Dock = DockStyle.Fill };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }
    }
}
