using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Tổng hợp kết quả kinh doanh", chỉ để chụp bằng <c>--render-screen preview.businessresult</c>.</summary>
    internal sealed class BusinessResultPreviewForm : Form, IScreenPreview
    {
        private readonly BusinessResultView _view;

        public BusinessResultPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = BusinessResultConstants.FunctionName;
            ClientSize = new Size(1400, 780);
            _view = new BusinessResultView(session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal BusinessResultView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Preview(option);
        }
    }
}
