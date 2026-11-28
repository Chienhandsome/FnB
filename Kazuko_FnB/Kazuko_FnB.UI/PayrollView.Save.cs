using System;
using System.ComponentModel;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần Thêm/Xóa kỳ bảng lương, Tải lại từ danh mục (toàn bảng &amp; từng nhân viên),
    /// Cập nhật lưu một transaction (hỗ trợ BackgroundWorker + thanh tiến độ) và xác nhận đóng tab cho <see cref="PayrollView"/>.
    /// </summary>
    internal sealed partial class PayrollView
    {
        private const int AsyncEmployeeThreshold = 40;

        public bool ConfirmClose()
        {
            return ConfirmDiscardUnsavedChanges();
        }

        private bool ConfirmDiscardUnsavedChanges()
        {
            if (!_isDirty)
                return true;
            UnsavedPromptTriggeredForTest = true;
            if (ConfirmUnsavedOverrideForTest != null)
                return ConfirmUnsavedOverrideForTest();
            if (SuppressPopup)
                return true;
            return Msg.Confirm(this, PayrollText.UnsavedChangesConfirm);
        }

        // ------------------------------------------------------------------ Thêm / Xóa bảng lương

        internal void AddPeriod()
        {
            if (_busy || !_service.CanAdd)
                return;
            if (!ConfirmDiscardUnsavedChanges())
                return;

            using (var dlg = new PayrollCreateDialog(_service, _session))
            {
                DialogResult dr = CreateDialogOpenerForTest != null
                    ? CreateDialogOpenerForTest(dlg)
                    : dlg.ShowDialog(FindForm());
                if ((dr == DialogResult.OK || dlg.AnySaved) && dlg.CreatedPeriod != null)
                    ReloadPeriods(dlg.CreatedPeriod.Id, true);
            }
        }

        internal bool DeleteSelectedPeriod()
        {
            LastDeleteResultForTest = null;
            if (_busy || !_service.CanRemove)
                return false;
            PayrollPeriodRow period = SelectedPeriodRow();
            if (period == null)
                return false;

            string confirmText = PayrollText.DeleteConfirm(period.Name);
            bool confirmed = ConfirmDeleteOverrideForTest != null
                ? ConfirmDeleteOverrideForTest(confirmText)
                : (SuppressPopup || Msg.Confirm(this, confirmText));
            if (!confirmed)
                return false;

            PayrollDeleteResult res;
            try
            {
                res = _service.DeletePeriod(period.Id);
            }
            catch (DataAccessException ex)
            {
                res = PayrollDeleteResult.Fail("Không xóa được bảng lương: " + ex.Message);
            }

            LastDeleteResultForTest = res;
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage ?? "Không thể xóa bảng lương.");
                return false;
            }

            _isDirty = false;
            ReloadPeriods(null, true);
            return true;
        }

        // ------------------------------------------------------------------ Tải lại (toàn bảng & 1 nhân viên)

        internal bool ReloadAllFromCatalog()
        {
            if (_busy || _mode != PayrollMode.Payroll || !_service.CanEdit || _currentSheet == null)
                return false;

            bool confirmed = ConfirmReloadOverrideForTest != null
                ? ConfirmReloadOverrideForTest(PayrollText.ReloadConfirm)
                : (SuppressPopup || Msg.Confirm(this, PayrollText.ReloadConfirm));
            if (!confirmed)
                return false;

            string err;
            try
            {
                if (!_service.ReloadAllFromCatalog(_currentSheet, out err))
                {
                    ShowWarning(err ?? PayrollText.NoPermission);
                    return false;
                }
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không tải lại được dữ liệu: " + ex.Message);
                return false;
            }

            _warnBanner.Visible = !string.IsNullOrEmpty(_currentSheet.Warning);
            _lblWarn.Text = _currentSheet.Warning ?? "";
            FillGridFromSheet();
            _isDirty = true;
            ApplyPermissionsAndMode();
            return true;
        }

        internal bool ReloadSelectedEmployeeFromCatalog(string explicitEmployeeId = null)
        {
            if (_busy || _mode != PayrollMode.Payroll || !_service.CanEdit || _currentSheet == null)
                return false;

            string empId = explicitEmployeeId;
            if (string.IsNullOrEmpty(empId))
            {
                PayrollEmployeeEntry sel = SelectedEmployee();
                empId = sel == null ? null : sel.EmployeeId;
            }
            if (string.IsNullOrEmpty(empId))
                return false;

            string err;
            try
            {
                if (!_service.ReloadEmployeeFromCatalog(_currentSheet, empId, out err))
                {
                    ShowWarning(err ?? EmployeeText.NotFound);
                    return false;
                }
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không tải lại được nhân viên: " + ex.Message);
                return false;
            }

            RefreshEmployeeMoneyInGrid(empId);
            UpdateSelectedEmployeeStats();
            _isDirty = true;
            return true;
        }

        // ------------------------------------------------------------------ Lưu (Cập nhật)

        internal bool SaveChanges()
        {
            LastSaveResultForTest = null;
            if (_busy || !_service.CanEdit || _currentSheet == null)
            {
                if (!_service.CanEdit)
                    ShowWarning(PayrollText.NoPermission);
                return false;
            }

            bool runAsync = UseAsyncWorkerForTest || (_currentSheet.Employees.Count >= AsyncEmployeeThreshold && !SuppressPopup);
            if (runAsync && !_saveWorker.IsBusy)
            {
                SetBusy(true);
                _saveWorker.RunWorkerAsync(_currentSheet);
                return true;
            }

            PayrollSaveResult res;
            try
            {
                res = _service.SaveSheet(_currentSheet);
            }
            catch (DataAccessException ex)
            {
                res = PayrollSaveResult.Fail("Không cập nhật được bảng lương: " + ex.Message);
            }

            FinishSave(res);
            return res.Ok;
        }

        private void OnSaveWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            var sheet = e.Argument as PayrollSheetData;
            try
            {
                e.Result = _service.SaveSheet(sheet);
            }
            catch (DataAccessException ex)
            {
                e.Result = PayrollSaveResult.Fail("Không cập nhật được bảng lương: " + ex.Message);
            }
        }

        private void OnSaveWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            if (e.Error != null)
            {
                FinishSave(PayrollSaveResult.Fail("Không cập nhật được bảng lương: " + e.Error.Message));
                return;
            }
            FinishSave(e.Result as PayrollSaveResult ?? PayrollSaveResult.Fail("Không thể cập nhật bảng lương."));
        }

        private void FinishSave(PayrollSaveResult res)
        {
            LastSaveResultForTest = res;
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage ?? "Không thể cập nhật bảng lương.");
                return;
            }
            _isDirty = false;
            LoadSelectedSheet();
            ShowInfo(PayrollText.SavedSuccess);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _progressBar.Visible = busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            ApplyPermissionsAndMode();
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ShowInfo(string message)
        {
            LastInfoMessageForTest = message;
            if (!SuppressPopup)
                Msg.Info(this, message);
        }
    }
}
