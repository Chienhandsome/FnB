using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Kiểm soát order", dùng cho <c>--render-screen preview.ordercontrol</c>.</summary>
    internal sealed class OrderControlPreviewForm : Form, IScreenPreview
    {
        private readonly OrderControlView _view;

        public OrderControlPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = OrderControlConstants.FunctionName;
            ClientSize = new Size(1366, 768);
            _view = new OrderControlView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal OrderControlView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Preview(option);
        }
    }
}
