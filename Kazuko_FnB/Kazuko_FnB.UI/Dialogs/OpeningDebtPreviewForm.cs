using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn Công nợ khách hàng / nhà cung cấp ban đầu, dùng cho <c>--render-screen</c>.</summary>
    internal sealed class OpeningDebtPreviewForm : Form, IScreenPreview
    {
        private readonly OpeningDebtView _view;

        public OpeningDebtPreviewForm(UserSession session, DebtPartyKind kind)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = kind == DebtPartyKind.Customer
                ? OpeningDebtConstants.CustomerFunctionName
                : OpeningDebtConstants.SupplierFunctionName;
            ClientSize = new Size(1360, 760);
            _view = new OpeningDebtView(session, kind) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal OpeningDebtView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.ReloadAll(true);
        }
    }
}
