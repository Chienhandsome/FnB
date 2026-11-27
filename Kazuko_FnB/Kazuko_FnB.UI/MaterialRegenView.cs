using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Xuất lại định lượng" (KHO HÀNG &gt; Xuất lại định lượng, docs/specs/xuat-lai-dinh-luong.md):
    /// chọn khoảng ngày bắt buộc (mặc định tháng hiện tại) và cửa hàng (theo quyền user / admin tất cả),
    /// nút Xem trước (không ghi gì), nút Thực hiện (mở hộp xác nhận gõ XACNHAN, chạy nền BackgroundWorker
    /// với thanh tiến độ theo hóa đơn và đồng hồ thời gian). Màu và cỡ chữ qua ThemeManager / ThemeTokens.
    /// </summary>
    internal sealed class MaterialRegenView : Panel, IConfirmCloseView
    {
        private readonly UserSession _session;
        private readonly MaterialRegenService _service;
        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _labelFont;

        private readonly Label _title = new Label { Text = "XUẤT LẠI ĐỊNH LƯỢNG", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _desc = new Label
        {
            Text = "Xóa các dòng xuất vật tư cũ của hóa đơn bán đã thanh toán trong khoảng ngày đã chọn rồi sinh lại theo công thức định lượng hiện tại, sau đó tính lại giá vốn.",
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft
        };

        private readonly Label _lblFrom = new Label { Text = "Từ ngày (*)", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DateBox _dtFrom = new DateBox(true);
        private readonly Label _lblTo = new Label { Text = "Đến ngày (*)", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DateBox _dtTo = new DateBox(true);
        private readonly Label _lblStore = new Label { Text = "Cửa hàng", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ComboBox _cboStore = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _storeFrame;

        private readonly FlatButton _btnPreview = new FlatButton { Text = "Xem trước" };
        private readonly FlatButton _btnRun = new FlatButton { Text = "Thực hiện", Tag = "danger" };

        private readonly Panel _resultCard = new Panel { Tag = "surface" };
        private readonly Label _lblResult = new Label { AutoSize = false, TextAlign = ContentAlignment.TopLeft };

        private readonly ProgressBar _progress = new ProgressBar { Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };
        private readonly Label _status = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
        private readonly Label _timer = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Tag = "muted" };
        private readonly Timer _clock = new Timer { Interval = 500 };
        private readonly Stopwatch _watch = new Stopwatch();
        private BackgroundWorker _worker;
        private volatile bool _running;
        private bool _resultIsWarning;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal Func<ConfirmMaterialRegenDialog, DialogResult> ConfirmDialogOpenerForTest { get; set; }
        internal Func<UserSession, string> CostRecalcRunnerForTest { get; set; }
        internal DateBox FromDateBoxForTest { get { return _dtFrom; } }
        internal DateBox ToDateBoxForTest { get { return _dtTo; } }
        internal ComboBox StoreComboForTest { get { return _cboStore; } }
        internal FlatButton PreviewButtonForTest { get { return _btnPreview; } }
        internal FlatButton RunButtonForTest { get { return _btnRun; } }
        internal Label TitleLabelForTest { get { return _title; } }
        internal Label ResultLabelForTest { get { return _lblResult; } }
        internal Panel ResultCardForTest { get { return _resultCard; } }
        internal Label StatusLabelForTest { get { return _status; } }
        internal Label TimerLabelForTest { get { return _timer; } }
        internal ProgressBar ProgressForTest { get { return _progress; } }
        internal MaterialRegenPreview LastPreviewForTest { get; private set; }
        internal MaterialRegenResult LastResultForTest { get; private set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal bool IsRunning { get { return _running || (_worker != null && _worker.IsBusy); } }

        public MaterialRegenView(UserSession session)
        {
            _session = session ?? AppSession.Current;
            _service = new MaterialRegenService(_session);
            DoubleBuffered = true;

            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _labelFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 4, FontStyle.Bold);
            _storeFrame = new FieldFrame(_cboStore, 0);

            float s = DeviceDpi / 96f;
            int pad = (int)(24 * s);
            int width = (int)(780 * s);
            Padding = new Padding(pad);

            _title.Font = _headingFont;
            _lblFrom.Font = _lblTo.Font = _lblStore.Font = _labelFont;
            _desc.Font = _dtFrom.Inner.Font = _dtTo.Inner.Font = _cboStore.Font = _bodyFont;
            _btnPreview.Font = _btnRun.Font = _lblResult.Font = _status.Font = _timer.Font = _bodyFont;

            int y = pad;
            _title.SetBounds(pad, y, width, (int)(38 * s));
            y += (int)(44 * s);
            _desc.SetBounds(pad, y, width, (int)(48 * s));
            y += (int)(56 * s);

            int dateW = (int)(168 * s);
            int gap = (int)(16 * s);
            int storeX = pad + (dateW + gap) * 2;
            int storeW = width - (dateW + gap) * 2;

            _lblFrom.SetBounds(pad, y, dateW, (int)(22 * s));
            _lblTo.SetBounds(pad + dateW + gap, y, dateW, (int)(22 * s));
            _lblStore.SetBounds(storeX, y, storeW, (int)(22 * s));
            y += (int)(24 * s);

            int inputH = (int)(36 * s);
            _dtFrom.SetBounds(pad, y, dateW, inputH);
            _dtTo.SetBounds(pad + dateW + gap, y, dateW, inputH);
            _storeFrame.SetBounds(storeX, y, storeW, inputH);
            y += inputH + (int)(18 * s);

            int btnH = (int)(40 * s);
            _btnPreview.SetBounds(pad, y, (int)(140 * s), btnH);
            _btnRun.SetBounds(pad + (int)(152 * s), y, (int)(140 * s), btnH);
            y += btnH + (int)(18 * s);

            int cardH = (int)(136 * s);
            _resultCard.SetBounds(pad, y, width, cardH);
            _lblResult.SetBounds((int)(14 * s), (int)(12 * s), width - (int)(28 * s), cardH - (int)(24 * s));
            _resultCard.Controls.Add(_lblResult);
            y += cardH + (int)(18 * s);

            _progress.SetBounds(pad, y, width, (int)(22 * s));
            y += (int)(30 * s);

            _status.SetBounds(pad, y, width - (int)(140 * s), (int)(28 * s));
            _timer.SetBounds(pad + width - (int)(130 * s), y, (int)(130 * s), (int)(28 * s));
            _timer.Text = "00:00";

            Controls.AddRange(new Control[]
            {
                _title, _desc, _lblFrom, _dtFrom, _lblTo, _dtTo, _lblStore, _storeFrame,
                _btnPreview, _btnRun, _resultCard, _progress, _status, _timer
            });

            InitializeFilters();
            UpdateActionButtons();

            _btnPreview.Click += delegate { RunPreview(); };
            _btnRun.Click += delegate { StartExecute(); };
            _clock.Tick += delegate { UpdateClock(); };

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
        }

        private void InitializeFilters()
        {
            DateTime serverDate;
            try { serverDate = _service.GetServerDate(); }
            catch (Exception) { serverDate = DateTime.Today; }

            DateTime from, to;
            MaterialRegenRules.DefaultDateRange(serverDate, out from, out to);
            _dtFrom.NullableValue = from;
            _dtTo.NullableValue = to;

            _cboStore.Items.Clear();
            _cboStore.Items.Add(new StoreRecord { Id = "", Name = MaterialRegenConstants.AllStoresLabel });
            try
            {
                foreach (StoreRecord st in _service.LoadAllowedStores())
                    _cboStore.Items.Add(st);
            }
            catch (Exception) { } // Giữ mục "Tất cả cửa hàng" khi không đọc được danh sách cửa hàng.
            if (_cboStore.Items.Count > 0)
                _cboStore.SelectedIndex = 0;

            _resultIsWarning = false;
            if (!_service.CanView)
            {
                _lblResult.Text = MaterialRegenConstants.NoViewPermission;
                _status.Text = MaterialRegenConstants.NoViewPermission;
            }
            else if (!_service.CanRun)
            {
                _lblResult.Text = "Chọn khoảng ngày và bấm Xem trước để kiểm tra số hóa đơn bị ảnh hưởng.\r\nBạn chỉ có quyền xem, không có quyền thực hiện xuất lại định lượng.";
                _status.Text = "Chế độ chỉ xem.";
            }
            else
            {
                _lblResult.Text = "Chọn khoảng ngày và bấm Xem trước để kiểm tra số hóa đơn bị ảnh hưởng trước khi thực hiện.";
                _status.Text = "Sẵn sàng.";
            }
        }

        internal MaterialRegenRequest BuildCurrentRequest(string confirmText = null)
        {
            var store = _cboStore.SelectedItem as StoreRecord;
            string storeId = store != null ? (store.Id ?? "") : "";
            string storeName = store != null && !string.IsNullOrEmpty(store.Id) ? (store.Name ?? "") : "";
            return new MaterialRegenRequest
            {
                FromDate = _dtFrom.NullableValue,
                ToDate = _dtTo.NullableValue,
                StoreId = storeId,
                StoreName = storeName,
                ConfirmText = confirmText
            };
        }

        private string CurrentStoreLabel()
        {
            var store = _cboStore.SelectedItem as StoreRecord;
            return store != null && !string.IsNullOrWhiteSpace(store.Name)
                ? store.Name
                : MaterialRegenConstants.AllStoresLabel;
        }

        internal void ApplyDateTextForTest(string fromText, string toText)
        {
            _dtFrom.Inner.Text = fromText ?? "";
            _dtFrom.Commit();
            _dtTo.Inner.Text = toText ?? "";
            _dtTo.Commit();
        }

        /// <summary>Chạy Xem trước (chỉ đọc, không ghi gì vào CSDL).</summary>
        internal MaterialRegenPreview RunPreview()
        {
            if (IsRunning)
                return null;

            LastWarningMessageForTest = null;
            MaterialRegenRequest req = BuildCurrentRequest();
            MaterialRegenPreview preview;
            try
            {
                preview = _service.Preview(req);
            }
            catch (Exception ex)
            {
                preview = MaterialRegenPreview.Fail("Không xem trước được: " + ex.Message);
            }

            LastPreviewForTest = preview;
            if (!preview.Ok)
            {
                ShowResultText(preview.ErrorMessage, true);
                _status.Text = preview.ErrorMessage;
                ShowWarning(preview.ErrorMessage);
                return preview;
            }

            ShowResultText(MaterialRegenRules.BuildPreviewText(preview, CurrentStoreLabel()), false);
            _status.Text = "Đã xem trước.";
            return preview;
        }

        /// <summary>Mở hộp xác nhận gõ XACNHAN rồi chạy nền Xuất lại định lượng. Trả false nếu bị chặn hoặc người dùng hủy.</summary>
        internal bool StartExecute()
        {
            if (!_service.CanRun || IsRunning)
            {
                if (!_service.CanRun)
                {
                    LastWarningMessageForTest = MaterialRegenConstants.NoPermission;
                    ShowWarning(MaterialRegenConstants.NoPermission);
                }
                return false;
            }

            MaterialRegenPreview preview = RunPreview();
            if (preview == null || !preview.Ok)
                return false;

            string enteredWord;
            using (var dlg = new ConfirmMaterialRegenDialog(preview, CurrentStoreLabel()))
            {
                DialogResult dr = ConfirmDialogOpenerForTest != null
                    ? ConfirmDialogOpenerForTest(dlg)
                    : dlg.ShowDialog(FindForm());
                if (dr != DialogResult.OK)
                    return false;
                enteredWord = dlg.EnteredText;
            }

            if (!MaterialRegenRules.IsConfirmWordValid(enteredWord))
            {
                ShowWarning(MaterialRegenConstants.ConfirmWordInvalid);
                return false;
            }

            MaterialRegenRequest req = BuildCurrentRequest(enteredWord);
            LastResultForTest = null;
            LastWarningMessageForTest = null;
            _progress.Value = 0;
            _status.Text = "Đang xuất lại định lượng...";
            _watch.Reset();
            _watch.Start();
            _clock.Start();

            _running = true;
            _worker = new BackgroundWorker { WorkerReportsProgress = true };
            UpdateActionButtons();

            _worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                var w = (BackgroundWorker)sender;
                try
                {
                    e.Result = _service.Execute(
                        req,
                        (done, total) =>
                        {
                            int pct = total <= 0 ? 100 : (int)(100L * done / total);
                            w.ReportProgress(pct, string.Format("Đang xử lý hóa đơn {0}/{1}...", done, total));
                        },
                        null,
                        CostRecalcRunnerForTest);
                }
                catch (Exception ex)
                {
                    e.Result = MaterialRegenResult.Fail("Không xuất lại được định lượng: " + ex.Message);
                }
            };
            _worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                int pct = e.ProgressPercentage;
                var text = e.UserState as string;
                RunOnUi(() => HandleProgress(pct, text));
            };
            _worker.RunWorkerCompleted += OnCompleted;
            _worker.RunWorkerAsync();
            return true;
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                    BeginInvoke(action);
                return;
            }
            action();
        }

        private void HandleProgress(int pct, string text)
        {
            if (IsDisposed || !_running)
                return;
            _progress.Value = Math.Max(0, Math.Min(100, pct));
            if (!string.IsNullOrEmpty(text))
                _status.Text = text;
        }

        private void OnCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            RunOnUi(() => HandleCompleted(e));
        }

        private void HandleCompleted(RunWorkerCompletedEventArgs e)
        {
            if (IsDisposed)
                return;
            _clock.Stop();
            _watch.Stop();
            UpdateClock();

            MaterialRegenResult result = e.Error != null
                ? MaterialRegenResult.Fail("Không xuất lại được định lượng: " + e.Error.Message)
                : (MaterialRegenResult)e.Result;

            LastResultForTest = result;

            bool ok = result != null && result.Ok;
            bool hasWarn = ok && (result.NegativeStockProductCount > 0 || !string.IsNullOrEmpty(result.CostWarning));
            string displayMsg = ok
                ? (result.Message ?? "")
                : (result != null && !string.IsNullOrEmpty(result.Message) ? result.Message : "Không xuất lại được định lượng.");

            if (ok)
            {
                _progress.Value = 100;
                ShowResultText(displayMsg, hasWarn);
                _status.Text = hasWarn ? "Đã hoàn tất (có cảnh báo)." : "Đã hoàn tất.";
            }
            else
            {
                ShowResultText(displayMsg, true);
                _status.Text = displayMsg;
                LastWarningMessageForTest = displayMsg;
            }

            // Đặt kết quả và tiến độ xong trước khi hạ cờ _running để vòng chờ không thoát sớm.
            _running = false;
            _worker = null;
            UpdateActionButtons();

            if (!SuppressPopup)
            {
                if (ok && !hasWarn)
                    Msg.Info(this, displayMsg);
                else
                    Msg.Warning(this, displayMsg);
            }
        }

        public bool ConfirmClose()
        {
            if (IsRunning)
            {
                ShowWarning(MaterialRegenConstants.CloseWhileRunningWarning);
                return false;
            }
            return true;
        }

        private void UpdateActionButtons()
        {
            bool busy = IsRunning;
            _dtFrom.Enabled = !busy;
            _dtTo.Enabled = !busy;
            _cboStore.Enabled = !busy;
            _storeFrame.Enabled = !busy;
            _btnPreview.Enabled = !busy && _service.CanView;
            _btnRun.Enabled = !busy && _service.CanRun;
        }

        private void ShowResultText(string text, bool isWarning)
        {
            _resultIsWarning = isWarning;
            _lblResult.Text = text ?? "";
            _lblResult.ForeColor = isWarning ? ThemeManager.Current.Danger : ThemeManager.Current.Text;
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup && !string.IsNullOrEmpty(message))
                Msg.Warning(this, message);
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
            _dtFrom.ApplyTheme(t, t.Background);
            _dtTo.ApplyTheme(t, t.Background);
            _storeFrame.ApplyTheme(t, t.Background);
            _resultCard.BackColor = t.Surface;
            _lblResult.BackColor = t.Surface;
            _lblResult.ForeColor = _resultIsWarning ? t.Danger : t.Text;
            _btnPreview.SurfaceColor = t.Background;
            _btnRun.SurfaceColor = t.Background;
            _btnPreview.Invalidate();
            _btnRun.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _clock.Stop();
                _clock.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_labelFont != null) _labelFont.Dispose();
                if (_headingFont != null) _headingFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
