using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn Công nợ khách hàng / Công nợ nhà cung cấp, dùng cho <c>--render-screen</c>.</summary>
    internal sealed class DebtPreviewForm : Form, IScreenPreview
    {
        private readonly DebtListView _view;

        public DebtPreviewForm(UserSession session, DebtPartyKind kind)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = kind == DebtPartyKind.Customer
                ? DebtConstants.CustomerDebtFunctionName
                : DebtConstants.SupplierDebtFunctionName;
            ClientSize = new Size(1360, 760);
            _view = new DebtListView(session, kind) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal DebtListView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.ReloadAll();
        }
    }
}
