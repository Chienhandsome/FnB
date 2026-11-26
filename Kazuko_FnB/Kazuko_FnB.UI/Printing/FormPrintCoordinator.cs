using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Luồng điều phối in dùng chung cho các màn lấy mẫu từ STEMPLATE theo SFORMID:
    /// khóa nút + con trỏ chờ tránh bấm hai lần, đọc danh sách mẫu, mở hộp "CHỌN MẪU IN" (hoặc picker cho test),
    /// đọc thông tin công ty, dựng dữ liệu in và gọi <see cref="PrintWorkflowService.Execute"/>.
    /// </summary>
    internal sealed class FormPrintCoordinator
    {
        private bool _isBusy;

        public bool IsBusy
        {
            get { return _isBusy; }
        }

        public bool Print(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            string formId,
            Func<CompanyPrintInfo, PrintJobData> buildJob,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Action<string> recordMessage)
        {
            if (_isBusy)
                return false;
            if (buildJob == null)
                throw new ArgumentNullException("buildJob");

            IPrintOutputHandler output = outputOverride ?? new WinFormsPrintOutputHandler();
            IWin32Window owner = hostControl == null ? null : hostControl.FindForm();

            _isBusy = true;
            bool oldEnabled = triggerButton != null && triggerButton.Enabled;
            Cursor oldCursor = hostControl != null ? hostControl.Cursor : Cursors.Default;
            try
            {
                if (triggerButton != null)
                    triggerButton.Enabled = false;
                if (hostControl != null)
                    hostControl.Cursor = Cursors.WaitCursor;

                var repo = new PrintTemplateRepository(databasePath);
                List<PrintTemplateRecord> templates;
                try
                {
                    templates = repo.LoadTemplatesForForm(formId);
                }
                catch (DataAccessException ex)
                {
                    string msg = "Không đọc được danh sách mẫu in: " + ex.Message;
                    if (recordMessage != null)
                        recordMessage(msg);
                    output.ShowError(owner, msg);
                    return false;
                }

                if (templates == null || templates.Count == 0)
                {
                    if (recordMessage != null)
                        recordMessage(PrintConstants.NoTemplateFoundMessage);
                    output.ShowInfo(owner, PrintConstants.NoTemplateFoundMessage);
                    return false;
                }

                PrintSelectionResult selection;
                if (templatePickerOverride != null)
                {
                    selection = templatePickerOverride(owner, templates);
                }
                else
                {
                    if (hostControl != null)
                        hostControl.Cursor = oldCursor;
                    using (var dlg = new PrintTemplateDialog(templates))
                    {
                        selection = dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Selection : null;
                    }
                    if (hostControl != null)
                        hostControl.Cursor = Cursors.WaitCursor;
                }

                if (selection == null || selection.Template == null)
                    return false;

                Dictionary<string, bool> configFlags;
                try
                {
                    configFlags = repo.LoadConfigVisibilityForTemplate(selection.Template);
                }
                catch (DataAccessException ex)
                {
                    string msg = "Không đọc được cấu hình hiển thị cột của mẫu in: " + ex.Message;
                    if (recordMessage != null)
                        recordMessage(msg);
                    output.ShowError(owner, msg);
                    return false;
                }

                CompanyPrintInfo company = LoadCompanyInfoSafe(repo);
                using (PrintJobData job = buildJob(company))
                {
                    if (job != null)
                        job.ConfigVisibilityById = configFlags;
                    return PrintWorkflowService.Execute(owner, selection, job, output);
                }
            }
            finally
            {
                if (triggerButton != null)
                    triggerButton.Enabled = oldEnabled;
                if (hostControl != null)
                    hostControl.Cursor = oldCursor;
                _isBusy = false;
            }
        }

        public static CompanyPrintInfo LoadCompanyInfoSafe(string databasePath)
        {
            try
            {
                return LoadCompanyInfoSafe(new PrintTemplateRepository(databasePath));
            }
            catch (ArgumentException)
            {
                return new CompanyPrintInfo();
            }
        }

        private static CompanyPrintInfo LoadCompanyInfoSafe(PrintTemplateRepository repo)
        {
            try
            {
                return repo.LoadCompanyInfo();
            }
            catch (DataAccessException)
            {
                return new CompanyPrintInfo();
            }
        }
    }
}
