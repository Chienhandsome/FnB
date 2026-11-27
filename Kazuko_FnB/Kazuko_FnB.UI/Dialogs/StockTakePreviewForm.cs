using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Quản lý kiểm kê kho", dùng cho <c>--render-screen preview.stocktake</c>.</summary>
    internal sealed class StockTakePreviewForm : Form, IScreenPreview
    {
        private readonly StockTakeListView _view;

        public StockTakePreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Quản lý kiểm kê kho";
            ClientSize = new Size(1360, 760);
            _view = new StockTakeListView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal StockTakeListView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Reload();
        }
    }
}
