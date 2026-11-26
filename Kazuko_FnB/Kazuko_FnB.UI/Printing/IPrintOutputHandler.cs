using System.Diagnostics;
using System.Windows.Forms;
using FastReport;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Interface trừu tượng hóa mọi thao tác đưa ra ngoài khi in (xem trước, gửi máy in, hộp Lưu file, hỏi/mở file, báo lỗi).
    /// Selftest thay bằng bản giả để kiểm đủ luồng mà KHÔNG BAO GIỜ gửi lệnh ra máy in thật hay bật hộp thoại modal.
    /// </summary>
    internal interface IPrintOutputHandler
    {
        void ShowPreview(IWin32Window owner, Report preparedReport, PrintTemplateRecord template, string title);
        void PrintToPrinter(Report preparedReport, PrintTemplateRecord template, string printerName);
        string PromptSaveFile(IWin32Window owner, PrintActionKind exportKind, string defaultFileName);
        void ExportExcel(Report preparedReport, PrintTemplateRecord template, string filePath);
        void ExportPdf(Report preparedReport, PrintTemplateRecord template, string filePath);
        bool ConfirmOpenFile(IWin32Window owner, string filePath);
        void OpenFile(string filePath);
        void ShowError(IWin32Window owner, string message);
        void ShowInfo(IWin32Window owner, string message);
    }

    /// <summary>Triển khai mặc định trên WinForms dùng cho app thật.</summary>
    internal sealed class WinFormsPrintOutputHandler : IPrintOutputHandler
    {
        public void ShowPreview(IWin32Window owner, Report preparedReport, PrintTemplateRecord template, string title)
        {
            using (var form = new ReportPreviewForm(preparedReport, title))
                form.ShowDialog(owner);
        }

        public void PrintToPrinter(Report preparedReport, PrintTemplateRecord template, string printerName)
        {
            FastReportEngine.PrintPrepared(preparedReport, printerName);
        }

        public string PromptSaveFile(IWin32Window owner, PrintActionKind exportKind, string defaultFileName)
        {
            bool isExcel = exportKind == PrintActionKind.ExportExcel;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = isExcel ? "Lưu file Excel" : "Lưu file PDF";
                dlg.Filter = isExcel
                    ? "Excel Workbook (*.xlsx)|*.xlsx"
                    : "PDF Document (*.pdf)|*.pdf";
                dlg.DefaultExt = isExcel ? "xlsx" : "pdf";
                dlg.AddExtension = true;
                dlg.OverwritePrompt = true;
                dlg.FileName = defaultFileName ?? "";
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.FileName : null;
            }
        }

        public void ExportExcel(Report preparedReport, PrintTemplateRecord template, string filePath)
        {
            FastReportEngine.ExportExcel(preparedReport, filePath);
        }

        public void ExportPdf(Report preparedReport, PrintTemplateRecord template, string filePath)
        {
            FastReportEngine.ExportPdf(preparedReport, filePath);
        }

        public bool ConfirmOpenFile(IWin32Window owner, string filePath)
        {
            return Msg.Confirm(owner, PrintConstants.ConfirmOpenExportedFileMessage);
        }

        public void OpenFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        public void ShowError(IWin32Window owner, string message)
        {
            Msg.Error(owner, message);
        }

        public void ShowInfo(IWin32Window owner, string message)
        {
            Msg.Info(owner, message);
        }
    }
}
