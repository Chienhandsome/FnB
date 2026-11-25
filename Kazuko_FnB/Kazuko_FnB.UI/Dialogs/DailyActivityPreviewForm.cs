using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Chi tiết hoạt động ngày", chỉ để chụp bằng <c>--render-screen preview.dailyactivity</c>.</summary>
    internal sealed class DailyActivityPreviewForm : Form, IScreenPreview
    {
        private readonly DailyActivityView _view;

        public DailyActivityPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = DailyActivityConstants.FunctionName;
            ClientSize = new Size(1400, 780);
            _view = new DailyActivityView(session) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal DailyActivityView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.Preview(option);
        }
    }
}
