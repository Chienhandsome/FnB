using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Khung chứa màn "Quản lý bán hàng" và "Điều chỉnh hóa đơn" để chụp bằng
    /// <c>--render-screen preview.salesmanage</c> hoặc <c>preview.billadjust</c>.
    /// </summary>
    internal sealed class SalesManagePreviewForm : Form, IScreenPreview
    {
        private readonly SalesManageView _view;

        public SalesManagePreviewForm(UserSession session, BillManageMode mode)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = mode == BillManageMode.Adjust ? "Điều chỉnh hóa đơn" : "Quản lý bán hàng";
            ClientSize = new Size(1360, 760);
            _view = new SalesManageView(session, mode) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        public void Preview(string option)
        {
            _view.ReloadBillList(null);
        }
    }
}
