using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Thống kê mặt hàng bán", chỉ để chụp bằng <c>--render-screen preview.itemsales</c>.</summary>
    internal sealed class ItemSalesPreviewForm : Form, IScreenPreview
    {
        private readonly ItemSalesView _view;

        public ItemSalesPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = ItemSalesConstants.FunctionName;
            ClientSize = new Size(1400, 780);
            _view = new ItemSalesView(session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal ItemSalesView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Preview(option);
        }
    }
}
