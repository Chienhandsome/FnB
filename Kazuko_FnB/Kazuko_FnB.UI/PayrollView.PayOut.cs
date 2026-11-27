using System;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần Chi lương (patch P3b, spec docs/specs/nhan-su-tinh-luong.md §5) của <see cref="PayrollView"/>:
    /// bật/tắt nút "Chi lương" (chỉ chế độ Tính lương, chỉ khi đã chọn bảng lương đã lưu, cần quyền "Tính lương" Sửa)
    /// và mở hộp thoại <see cref="PayrollPayoutDialog"/> cho bảng lương đang chọn.
    /// </summary>
    internal sealed partial class PayrollView
    {
        internal void ApplyPayOutButtonState()
        {
            bool isPayroll = _mode == PayrollMode.Payroll;
            bool hasSavedPeriod = _currentSheet != null
                && _currentSheet.Period != null
                && !string.IsNullOrEmpty(_currentSheet.Period.Id);
            bool canPayOut = PayrollPayoutRules.CanPayOut(_session);

            _btnPayOut.Visible = isPayroll;
            _btnPayOut.Enabled = !_busy && isPayroll && canPayOut && hasSavedPeriod;
        }

        internal bool OpenPayOutDialog()
        {
            if (_busy || _mode != PayrollMode.Payroll)
                return false;
            if (!PayrollPayoutRules.CanPayOut(_session))
            {
                ShowWarning(PayrollPayoutText.NoPermission);
                return false;
            }
            if (_currentSheet == null || _currentSheet.Period == null || string.IsNullOrEmpty(_currentSheet.Period.Id))
            {
                ShowWarning(PayrollText.PeriodNotFound);
                return false;
            }

            using (var dlg = new PayrollPayoutDialog(_session, _currentSheet.Period))
            {
                if (PayoutDialogOpenerForTest != null)
                    PayoutDialogOpenerForTest(dlg);
                else
                    dlg.ShowDialog(FindForm());
                return true;
            }
        }
    }
}
