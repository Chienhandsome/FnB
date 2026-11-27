using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Quản lý xuất kho" / "Quản lý chuyển kho", dùng cho <c>--render-screen</c>.</summary>
    internal sealed class StockDocumentPreviewForm : Form, IScreenPreview
    {
        private readonly StockDocumentListView _view;

        public StockDocumentPreviewForm(UserSession session, StockDocumentKind kind)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = kind == StockDocumentKind.Transfer ? "Quản lý chuyển kho" : "Quản lý xuất kho";
            ClientSize = new Size(1360, 760);
            _view = new StockDocumentListView(session, kind) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal StockDocumentListView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Reload();
        }
    }
}
