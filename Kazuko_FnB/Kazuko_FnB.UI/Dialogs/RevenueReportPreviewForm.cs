using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Thống kê doanh thu", chỉ để chụp bằng <c>--render-screen preview.revenue</c>.</summary>
    internal sealed class RevenueReportPreviewForm : Form, IScreenPreview
    {
        private readonly RevenueReportView _view;

        public RevenueReportPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = RevenueReportConstants.FunctionName;
            ClientSize = new Size(1400, 780);
            _view = new RevenueReportView(session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal RevenueReportView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Preview(option);
        }
    }
}
