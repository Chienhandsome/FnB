using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Luồng điều phối in dùng chung cho các màn lấy mẫu từ STEMPLATE theo SFORMID hoặc STABLEDESCID:
    /// khóa nút + con trỏ chờ tránh bấm hai lần, đọc danh sách mẫu, mở hộp chọn mẫu (hoặc in thẳng mẫu mặc định),
    /// đọc cấu hình ẩn cột VisibleByConfigID và thông tin công ty, dựng dữ liệu in và gọi <see cref="PrintWorkflowService.Execute"/>.
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
            return ExecuteCore(
                hostControl,
                triggerButton,
                databasePath,
                repo => repo.LoadTemplatesForForm(formId),
                null,
                PrintDialogOptions.ForFormSelectDialog(),
                false,
                buildJob,
                outputOverride,
                templatePickerOverride,
                recordMessage);
        }

        public bool PrintForTable(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            string stableDescId,
            PrintDialogOptions dialogOptions,
            Func<CompanyPrintInfo, PrintJobData> buildJob,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            return ExecuteCore(
                hostControl,
                triggerButton,
                databasePath,
                repo => repo.LoadTemplatesForTable(stableDescId),
                templateLoaderOverride == null ? (Func<List<PrintTemplateRecord>>)null : delegate { return templateLoaderOverride(stableDescId); },
                dialogOptions,
                false,
                buildJob,
                outputOverride,
                templatePickerOverride,
                recordMessage);
        }

        public bool PrintDirectForTable(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            string stableDescId,
            PrintDialogOptions dialogOptions,
            Func<CompanyPrintInfo, PrintJobData> buildJob,
            IPrintOutputHandler outputOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            return ExecuteCore(
                hostControl,
                triggerButton,
                databasePath,
                repo => repo.LoadTemplatesForTable(stableDescId),
                templateLoaderOverride == null ? (Func<List<PrintTemplateRecord>>)null : delegate { return templateLoaderOverride(stableDescId); },
                dialogOptions,
                true,
                buildJob,
                outputOverride,
                null,
                recordMessage);
        }

        private bool ExecuteCore(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            Func<PrintTemplateRepository, List<PrintTemplateRecord>> loadFromRepo,
            Func<List<PrintTemplateRecord>> loadOverride,
            PrintDialogOptions dialogOptions,
            bool directPrintDefault,
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
            PrintDialogOptions effectiveOptions = dialogOptions ?? PrintDialogOptions.ForFormSelectDialog();

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
                    templates = loadOverride != null ? loadOverride() : loadFromRepo(repo);
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

                int defaultIdx;
                List<PrintTemplateRecord> ordered = PrintTemplateOrdering.OrderAndFindDefault(templates, effectiveOptions, out defaultIdx);
                PrintSelectionResult selection;
                if (directPrintDefault)
                {
                    PrintTemplateRecord chosen = defaultIdx >= 0 && defaultIdx < ordered.Count ? ordered[defaultIdx] : ordered[0];
                    selection = new PrintSelectionResult
                    {
                        Template = chosen,
                        Action = PrintActionKind.Print,
                        PrinterName = GetDefaultWindowsPrinter()
                    };
                }
                else if (templatePickerOverride != null)
                {
                    selection = templatePickerOverride(owner, ordered);
                }
                else
                {
                    if (hostControl != null)
                        hostControl.Cursor = oldCursor;
                    using (var dlg = new PrintTemplateDialog(ordered, effectiveOptions))
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
                    if (job == null)
                        return false;
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

        private static string GetDefaultWindowsPrinter()
        {
            try
            {
                var settings = new PrinterSettings();
                return settings.PrinterName ?? "";
            }
            catch
            {
                return "";
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
