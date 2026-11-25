using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Tồn quỹ", chỉ để chụp bằng <c>--render-screen preview.fund</c>.</summary>
    internal sealed class FundPreviewForm : Form, IScreenPreview
    {
        private readonly FundBalanceView _view;

        public FundPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Tồn quỹ";
            ClientSize = new Size(1300, 720);
            _view = new FundBalanceView(session) { Dock = DockStyle.Fill };
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
