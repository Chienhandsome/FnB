using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Xuất lại định lượng", dùng cho <c>--render-screen preview.materialregen</c>.</summary>
    internal sealed class MaterialRegenPreviewForm : Form, IScreenPreview
    {
        private readonly MaterialRegenView _view;

        public MaterialRegenPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = MaterialRegenConstants.FunctionName;
            ClientSize = new Size(960, 620);
            _view = new MaterialRegenView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal MaterialRegenView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.RunPreview();
        }
    }
}
