using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Báo giá bán hàng", dùng cho <c>--render-screen preview.quote</c>.</summary>
    internal sealed class QuotePreviewForm : Form, IScreenPreview
    {
        private readonly QuoteListView _view;

        public QuotePreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Báo giá bán hàng";
            ClientSize = new Size(1360, 760);
            _view = new QuoteListView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal QuoteListView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Preview(option);
        }
    }
}
