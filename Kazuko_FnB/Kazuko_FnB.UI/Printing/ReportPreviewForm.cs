using System;
using System.Drawing;
using System.Windows.Forms;
using FastReport;
using FastReport.Preview;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Cửa sổ xem trước báo cáo tiêu đề "IN PHIẾU", mở lớn (Maximized), chứa trình xem chuẩn của FastReport
    /// (giữ nguyên thanh công cụ của FastReport). Khung cửa sổ theo ThemeManager.
    /// </summary>
    internal sealed class ReportPreviewForm : BaseForm
    {
        private readonly Report _report;
        private readonly PreviewControl _previewControl = new PreviewControl { Dock = DockStyle.Fill };

        public ReportPreviewForm(Report preparedReport, string title)
        {
            if (preparedReport == null)
                throw new ArgumentNullException("preparedReport");

            _report = preparedReport;

            float s = DeviceDpi / 96f;
            Text = string.IsNullOrWhiteSpace(title) ? PrintConstants.DefaultPreviewTitle : title.Trim();
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size((int)(800 * s), (int)(540 * s));
            ClientSize = new Size((int)(1100 * s), (int)(740 * s));
            KeyPreview = true;

            Controls.Add(_previewControl);
            ApplyFormTheme();

            Load += OnFormLoad;
        }

        internal PreviewControl PreviewControlForTest
        {
            get { return _previewControl; }
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            _report.Preview = _previewControl;
            _report.ShowPrepared();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void ApplyFormTheme()
        {
            ThemeTokens t = ThemeManager.Current;
            BackColor = t.Background;
            ForeColor = t.Text;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_report != null && ReferenceEquals(_report.Preview, _previewControl))
                    _report.Preview = null;
            }
            base.Dispose(disposing);
        }
    }
}
