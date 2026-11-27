using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Quản lý nhập kho", dùng cho <c>--render-screen preview.purchase</c>.</summary>
    internal sealed class PurchasePreviewForm : Form, IScreenPreview
    {
        private readonly PurchaseListView _view;

        public PurchasePreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Quản lý nhập kho";
            ClientSize = new Size(1360, 760);
            _view = new PurchaseListView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal PurchaseListView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Reload();
        }
    }
}
