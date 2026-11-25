using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Lưu vết hoạt động", chỉ để chụp bằng <c>--render-screen preview.activitylog</c>.</summary>
    internal sealed class ActivityLogPreviewForm : Form, IScreenPreview
    {
        private readonly ActivityLogView _view;

        public ActivityLogPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Lưu vết hoạt động";
            ClientSize = new Size(1360, 740);
            _view = new ActivityLogView(session) { Dock = DockStyle.Fill, SuppressWarningPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        public void Preview(string option)
        {
            _view.Preview(option);
        }
    }
}
