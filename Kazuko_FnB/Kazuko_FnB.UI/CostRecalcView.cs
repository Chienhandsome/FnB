using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Tính lại giá vốn" (KHO HÀNG &gt; Tính lại giá vốn, docs/specs/tinh-lai-gia-von.md §3):
    /// mô tả ngắn, nút Tính lại, thanh tiến độ theo tháng, đồng hồ thời gian; chạy nền, không treo giao diện.
    /// Nút Tính lại cần quyền Sửa; màn xem được khi có quyền Xem.
    /// </summary>
    internal sealed class CostRecalcView : Panel
    {
        private readonly UserSession _session;
        private readonly CostRecalcService _service;
        private readonly Font _bodyFont;
        private readonly Font _headingFont;

        private readonly Label _title = new Label { Text = "TÍNH LẠI GIÁ VỐN", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _desc = new Label
        {
            Text = "Tính lại giá vốn theo bình quân cuối kỳ (từng tháng) từ phiếu nhập đầu tiên đến nay.\r\n" +
                   "Giá vốn của các dòng chứng từ và của mặt hàng sẽ được cập nhật. Việc này có thể mất vài phút khi dữ liệu lớn.",
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft
        };
        private readonly FlatButton _btnRun = new FlatButton { Text = "Tính lại", Tag = "primary" };
        private readonly ProgressBar _progress = new ProgressBar { Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };
        private readonly Label _status = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
        private readonly Label _timer = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Tag = "muted" };
        private readonly Timer _clock = new Timer { Interval = 500 };
        private readonly Stopwatch _watch = new Stopwatch();
        private BackgroundWorker _worker;

        // Hook kiểm thử UI.
        internal bool SuppressPopup { get; set; }
        internal FlatButton RunButtonForTest { get { return _btnRun; } }
        internal Label StatusLabelForTest { get { return _status; } }
        internal ProgressBar ProgressForTest { get { return _progress; } }
        internal CostRecalcResult LastResultForTest { get; private set; }
        internal bool IsRunning { get { return _worker != null && _worker.IsBusy; } }

        public CostRecalcView(UserSession session)
        {
            _session = session ?? AppSession.Current;
            _service = new CostRecalcService(_session);
            DoubleBuffered = true;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 4, FontStyle.Bold);

            float s = DeviceDpi / 96f;
            Padding = new Padding((int)(24 * s));
            _title.Font = _headingFont;
            _desc.Font = _bodyFont;
            _status.Font = _bodyFont;
            _timer.Font = _bodyFont;
            _btnRun.Font = _bodyFont;

            _title.SetBounds(0, 0, 600, (int)(40 * s));
            _desc.SetBounds(0, (int)(48 * s), 760, (int)(64 * s));
            _btnRun.SetBounds(0, (int)(124 * s), (int)(140 * s), (int)(40 * s));
            _progress.SetBounds(0, (int)(180 * s), 760, (int)(22 * s));
            _status.SetBounds(0, (int)(210 * s), 560, (int)(28 * s));
            _timer.SetBounds((int)(580 * s), (int)(210 * s), (int)(180 * s), (int)(28 * s));
            Controls.AddRange(new Control[] { _title, _desc, _btnRun, _progress, _status, _timer });

            _btnRun.Enabled = _service.CanRun;
            _status.Text = _service.CanView
                ? (_service.CanRun ? "Sẵn sàng." : "Bạn chỉ có quyền xem, không có quyền tính lại giá vốn.")
                : CostRecalcConstants.NoPermission;

            _btnRun.Click += delegate { StartRun(); };
            _clock.Tick += delegate { UpdateClock(); };
            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
        }

        /// <summary>Bắt đầu chạy nền. Trả false nếu không có quyền hoặc đang chạy.</summary>
        internal bool StartRun()
        {
            if (!_service.CanRun || IsRunning)
                return false;

            _btnRun.Enabled = false;
            _progress.Value = 0;
            _status.Text = "Đang tính...";
            LastResultForTest = null;
            _watch.Reset();
            _watch.Start();
            _clock.Start();

            _worker = new BackgroundWorker { WorkerReportsProgress = true };
            _worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                var w = (BackgroundWorker)sender;
                try
                {
                    e.Result = _service.Run((done, total) => w.ReportProgress(total <= 0 ? 0 : (int)(100L * done / total)));
                }
                catch (Exception ex)
                {
                    e.Result = CostRecalcResult.Fail("Không tính lại được giá vốn: " + ex.Message);
                }
            };
            _worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                _progress.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
            };
            _worker.RunWorkerCompleted += OnCompleted;
            _worker.RunWorkerAsync();
            return true;
        }

        private void OnCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _clock.Stop();
            _watch.Stop();
            UpdateClock();
            var result = e.Error != null
                ? CostRecalcResult.Fail("Không tính lại được giá vốn: " + e.Error.Message)
                : (CostRecalcResult)e.Result;
            LastResultForTest = result;
            _btnRun.Enabled = _service.CanRun;
            if (result.Ok)
                _progress.Value = 100;
            _status.Text = result.Message ?? "";
            if (!SuppressPopup)
            {
                if (result.Ok)
                    Msg.Info(this, result.Message);
                else
                    Msg.Warning(this, result.Message);
            }
        }

        private void UpdateClock()
        {
            TimeSpan t = _watch.Elapsed;
            _timer.Text = string.Format("{0:00}:{1:00}", (int)t.TotalMinutes, t.Seconds);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _clock.Stop();
                _clock.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_headingFont != null) _headingFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
