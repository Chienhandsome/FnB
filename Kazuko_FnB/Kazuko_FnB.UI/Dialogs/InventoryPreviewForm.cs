using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Tồn kho", dùng cho <c>--render-screen preview.inventory</c>.</summary>
    internal sealed class InventoryPreviewForm : Form, IScreenPreview
    {
        private readonly InventoryView _view;

        public InventoryPreviewForm(UserSession session)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = InventoryConstants.FunctionName;
            ClientSize = new Size(1360, 760);
            _view = new InventoryView(session) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal InventoryView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.ReloadAll();
        }
    }
}
