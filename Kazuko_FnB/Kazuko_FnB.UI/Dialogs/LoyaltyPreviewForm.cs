using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn Khách hàng thân thiết, dùng cho <c>--render-screen preview.loyalty</c>.</summary>
    internal sealed class LoyaltyPreviewForm : Form, IScreenPreview
    {
        private readonly LoyaltyView _view;

        public LoyaltyPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = LoyaltyConstants.FunctionName;
            ClientSize = new Size(1360, 760);
            _view = new LoyaltyView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal LoyaltyView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.ReloadAll();
        }
    }
}
