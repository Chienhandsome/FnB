using System;
using System.Windows.Forms;
using FastReport;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Điều phối luồng thực thi lựa chọn in: Prepare mẫu rồi đưa ra đúng đầu ra qua <see cref="IPrintOutputHandler"/>.
    /// </summary>
    internal static class PrintWorkflowService
    {
        public static bool Execute(
            IWin32Window owner,
            PrintSelectionResult selection,
            PrintJobData job,
            IPrintOutputHandler output)
        {
            if (selection == null || selection.Template == null || output == null)
                return false;

            Report preparedReport;
            string error;
            if (!FastReportEngine.TryPrepare(selection.Template, job, out preparedReport, out error))
            {
                output.ShowError(owner, error);
                return false;
            }

            string templateName = FastReportEngine.DisplayTemplateName(selection.Template);
            using (preparedReport)
            {
                switch (selection.Action)
                {
                    case PrintActionKind.Preview:
                        return RunGuarded(owner, output, templateName, "xem trước", delegate
                        {
                            string title = job != null && !string.IsNullOrWhiteSpace(job.PreviewTitle)
                                ? job.PreviewTitle
                                : PrintConstants.DefaultPreviewTitle;
                            output.ShowPreview(owner, preparedReport, selection.Template, title);
                        });

                    case PrintActionKind.Print:
                        return RunGuarded(owner, output, templateName, "in ra máy in", delegate
                        {
                            output.PrintToPrinter(preparedReport, selection.Template, selection.PrinterName);
                        });

                    case PrintActionKind.ExportExcel:
                        return ExecuteExport(owner, preparedReport, selection.Template, job, output, PrintActionKind.ExportExcel);

                    case PrintActionKind.ExportPdf:
                        return ExecuteExport(owner, preparedReport, selection.Template, job, output, PrintActionKind.ExportPdf);

                    default:
                        return false;
                }
            }
        }

        private static bool ExecuteExport(
            IWin32Window owner,
            Report preparedReport,
            PrintTemplateRecord template,
            PrintJobData job,
            IPrintOutputHandler output,
            PrintActionKind kind)
        {
            string templateName = FastReportEngine.DisplayTemplateName(template);
            string op = kind == PrintActionKind.ExportExcel ? "xuất file Excel" : "xuất file PDF";
            string defaultName = job == null ? "" : (job.DefaultFileName ?? "");

            string targetPath = output.PromptSaveFile(owner, kind, defaultName);
            if (string.IsNullOrWhiteSpace(targetPath))
                return false;

            bool exported = RunGuarded(owner, output, templateName, op, delegate
            {
                if (kind == PrintActionKind.ExportExcel)
                    output.ExportExcel(preparedReport, template, targetPath);
                else
                    output.ExportPdf(preparedReport, template, targetPath);
            });

            if (!exported)
                return false;

            if (output.ConfirmOpenFile(owner, targetPath))
            {
                RunGuarded(owner, output, templateName, "mở file đã xuất", delegate
                {
                    output.OpenFile(targetPath);
                });
            }
            return true;
        }

        private static bool RunGuarded(
            IWin32Window owner,
            IPrintOutputHandler output,
            string templateName,
            string operation,
            Action work)
        {
            try
            {
                work();
                return true;
            }
            catch (Exception ex)
            {
                output.ShowError(owner, FastReportEngine.FormatError(templateName, operation, ex));
                return false;
            }
        }
    }
}
