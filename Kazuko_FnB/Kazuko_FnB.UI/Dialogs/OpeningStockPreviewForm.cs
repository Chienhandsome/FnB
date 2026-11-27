using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Tồn kho ban đầu", dùng cho <c>--render-screen preview.openingstock</c>.</summary>
    internal sealed class OpeningStockPreviewForm : Form, IScreenPreview
    {
        private readonly OpeningStockView _view;

        public OpeningStockPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = OpeningStockConstants.FunctionName;
            ClientSize = new Size(1360, 760);
            _view = new OpeningStockView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal OpeningStockView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.ReloadAll(true);
        }
    }
}
