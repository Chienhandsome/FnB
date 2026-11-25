using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Danh mục hóa đơn hủy", chỉ để chụp bằng <c>--render-screen preview.cancelledbill</c>.</summary>
    internal sealed class CancelledBillPreviewForm : Form, IScreenPreview
    {
        private readonly CancelledBillView _view;

        public CancelledBillPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = CancelledBillConstants.FunctionName;
            ClientSize = new Size(1360, 740);
            _view = new CancelledBillView(session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal CancelledBillView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Preview(option);
        }
    }
}
