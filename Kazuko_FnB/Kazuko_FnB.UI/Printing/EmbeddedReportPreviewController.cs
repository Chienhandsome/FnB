using System;
using System.Windows.Forms;
using FastReport;
using FastReport.Preview;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    internal delegate PrintTemplateRecord EmbeddedReportTemplateLoader(
        string reportId,
        string reportDisplayName,
        out string errorMessage);

    /// <summary>
    /// Điều phối trình xem FastReport nhúng trong tab (màn Chi tiết hoạt động ngày &amp; Tổng hợp KQKD):
    /// khóa tải chồng, đọc mẫu SREPORTTEMPLATE qua tầng Data, dựng trang bằng FastReportEngine.TryPrepare,
    /// gắn vào PreviewControl và Dispose báo cáo cũ cùng DataSet mỗi lần tải lại hoặc đóng màn.
    /// </summary>
    internal sealed class EmbeddedReportPreviewController : IDisposable
    {
        private readonly PreviewControl _previewControl = new PreviewControl { Dock = DockStyle.Fill };
        private Report _currentReport;
        private PrintJobData _currentJob;
        private bool _isBusy;
        private bool _disposed;

        public PreviewControl PreviewControl { get { return _previewControl; } }
        public Report CurrentReport { get { return _currentReport; } }
        public bool IsBusy { get { return _isBusy; } }
        public string LastErrorMessage { get; private set; }
        public int DisposedReportCount { get; private set; }

        /// <summary>Hook cho selftest thay thế việc đọc mẫu từ CSDL mà không sửa SREPORT/SREPORTTEMPLATE.</summary>
        internal EmbeddedReportTemplateLoader TemplateLoaderForTest { get; set; }

        /// <summary>Hook cho selftest kiểm khóa tải chồng khi đang dựng báo cáo.</summary>
        internal Action OnBeforePrepareForTest { get; set; }

        /// <summary>
        /// Thực hiện một lượt tải dữ liệu + dựng trang báo cáo nhúng dưới khóa chống bấm chồng.
        /// <paramref name="loadAndBuildJob"/> thực thi truy vấn dữ liệu màn hình và trả về <see cref="PrintJobData"/>;
        /// trả về null nếu việc tải dữ liệu thất bại và đã tự xử lý thông báo.
        /// </summary>
        public bool RunLoad(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            string reportId,
            string reportDisplayName,
            Func<PrintJobData> loadAndBuildJob,
            bool suppressErrorPopup)
        {
            if (_disposed || _isBusy)
                return false;
            if (loadAndBuildJob == null)
                throw new ArgumentNullException("loadAndBuildJob");

            _isBusy = true;
            bool oldEnabled = triggerButton != null && triggerButton.Enabled;
            Cursor oldCursor = hostControl != null ? hostControl.Cursor : Cursors.Default;
            try
            {
                if (triggerButton != null)
                    triggerButton.Enabled = false;
                if (hostControl != null)
                    hostControl.Cursor = Cursors.WaitCursor;

                Action hook = OnBeforePrepareForTest;
                if (hook != null)
                    hook();

                PrintJobData job = loadAndBuildJob();
                if (job == null)
                    return false;

                PrintTemplateRecord template;
                string tplError;
                try
                {
                    EmbeddedReportTemplateLoader loader = TemplateLoaderForTest;
                    if (loader != null)
                        template = loader(reportId, reportDisplayName, out tplError);
                    else
                        template = new PrintTemplateRepository(databasePath).LoadReportTemplate(reportId, reportDisplayName, out tplError);
                }
                catch (DataAccessException ex)
                {
                    job.Dispose();
                    ReportError(hostControl, "Không đọc được mẫu báo cáo '" + reportDisplayName + "': " + ex.Message, suppressErrorPopup);
                    return false;
                }
                catch (Exception ex)
                {
                    job.Dispose();
                    ReportError(hostControl, "Không đọc được mẫu báo cáo '" + reportDisplayName + "': " + ex.Message, suppressErrorPopup);
                    return false;
                }

                if (template == null)
                {
                    job.Dispose();
                    string msg = string.IsNullOrWhiteSpace(tplError)
                        ? PrintTemplateRepository.FormatMissingReportError(reportDisplayName)
                        : tplError;
                    ReportError(hostControl, msg, suppressErrorPopup);
                    return false;
                }

                Report prepared;
                string prepareError;
                if (!FastReportEngine.TryPrepare(template, job, out prepared, out prepareError))
                {
                    job.Dispose();
                    ReportError(hostControl, prepareError, suppressErrorPopup);
                    return false;
                }

                ReplaceCurrentReport(prepared, job);
                LastErrorMessage = null;
                return true;
            }
            finally
            {
                if (triggerButton != null && !triggerButton.IsDisposed)
                    triggerButton.Enabled = oldEnabled;
                if (hostControl != null && !hostControl.IsDisposed)
                    hostControl.Cursor = oldCursor;
                _isBusy = false;
            }
        }

        private void ReplaceCurrentReport(Report newReport, PrintJobData newJob)
        {
            Report oldReport = _currentReport;
            PrintJobData oldJob = _currentJob;

            _currentReport = null;
            _currentJob = null;

            if (oldReport != null)
            {
                if (ReferenceEquals(oldReport.Preview, _previewControl))
                    oldReport.Preview = null;
                try { oldReport.Dispose(); } catch { }
                DisposedReportCount++;
            }
            if (oldJob != null)
            {
                try { oldJob.Dispose(); } catch { }
            }

            _currentReport = newReport;
            _currentJob = newJob;

            if (newReport != null && !_previewControl.IsDisposed)
            {
                newReport.Preview = _previewControl;
                newReport.ShowPrepared();
            }
        }

        private void ReportError(Control hostControl, string message, bool suppressErrorPopup)
        {
            LastErrorMessage = message;
            if (!suppressErrorPopup)
                Msg.Error(hostControl, message);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            ReplaceCurrentReport(null, null);
        }
    }
}
