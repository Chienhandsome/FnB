using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn Tính lương / Chấm công cho <c>--render-screen preview.payroll</c> và <c>preview.attendance</c>.</summary>
    internal sealed class PayrollPreviewForm : Form, IScreenPreview
    {
        private readonly PayrollView _view;

        public PayrollPreviewForm(UserSession session, PayrollMode mode)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = PayrollRules.FunctionNameOf(mode);
            ClientSize = new Size(1360, 760);
            _view = new PayrollView(session, mode) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
            BackColor = ThemeManager.Current.Background;
        }

        internal PayrollView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            _view.ReloadPeriods(null, true);
        }

        /// <summary>Chọn và hiển thị đúng kỳ bảng lương vừa tạo (dùng bởi nút "Lưu &amp; In" của hộp tạo mới).</summary>
        public void SelectPeriod(string periodId)
        {
            _view.ReloadPeriods(periodId, true);
        }
    }
}
