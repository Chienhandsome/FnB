using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn hình Tính lương và Chấm công (một view, hai chế độ; patch P3a, spec docs/specs/nhan-su-tinh-luong.md §2, §7):
    /// - Trái: danh sách kỳ bảng lương (mới nhất trước), nút Thêm, Xóa.
    /// - Phải: thanh nút chấm công (Đi làm / Nghỉ có phép / Nghỉ không phép / Xóa, phím tắt 1/2/3/0),
    ///   Tải lại (chỉ chế độ Tính lương), Cập nhật (KHÔNG có nút Chi lương ở P3a); dải chú giải màu + số ca/số ngày NV;
    ///   lưới chấm công tự dựng cột ngày 1..n, cố định cột Nhân viên &amp; Ca, 6 cột tiền chỉ ở chế độ Tính lương.
    /// Phần dựng lưới, tô màu ô theo ThemeManager và đổi theme đặt ở <c>PayrollView.Grid.cs</c>;
    /// phần nạp/lưu/chấm công/tải lại/thêm/xóa/cảnh báo đóng đặt ở <c>PayrollView.Actions.cs</c>.
    /// </summary>
    internal sealed partial class PayrollView : Panel, IConfirmCloseView
    {
        private static readonly object SummaryRowTag = new object();

        private sealed class GridRowMeta
        {
            public PayrollEmployeeEntry Employee { get; set; }
            public PayrollShiftInfo Shift { get; set; }
            public bool IsFirstShiftOfEmployee { get; set; }
        }

        private readonly UserSession _session;
        private readonly PayrollMode _mode;
        private readonly PayrollService _service;

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _summaryFont;
        private readonly Font _cellSymbolFont;
        private readonly Font _smallFont;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };

        // Khung trái: danh sách bảng lương.
        private readonly Label _leftTitle = new Label { Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Panel _leftBar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnAdd = new FlatButton { Text = "Thêm", Tag = "primary" };
        private readonly FlatButton _btnDelete = new FlatButton { Text = "Xóa", Tag = "danger" };
        private readonly DataGridView _periodGrid = new DataGridView();

        // Khung phải: thanh công cụ, chú giải/số ca-ngày, cảnh báo, tiến độ, lưới chấm công.
        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlowLayoutPanel _barFlow = new FlowLayoutPanel();
        private readonly FlatButton _btnPresent = new FlatButton { Text = "1. Đi làm" };
        private readonly FlatButton _btnPaidLeave = new FlatButton { Text = "2. Nghỉ có phép" };
        private readonly FlatButton _btnUnpaidLeave = new FlatButton { Text = "3. Nghỉ không phép" };
        private readonly FlatButton _btnClearCell = new FlatButton { Text = "0. Xóa" };
        private readonly FlatButton _btnReload = new FlatButton { Text = "Tải lại" };
        private readonly FlatButton _btnSave = new FlatButton { Text = "Cập nhật", Tag = "primary" };
        private readonly FlatButton _btnPayOut = new FlatButton { Text = "Chi lương" };
        private readonly Label _summary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

        private readonly Panel _infoBar = new Panel { Dock = DockStyle.Top };
        private readonly Panel _swatchPresent = new Panel { Size = new Size(14, 14) };
        private readonly Panel _swatchPaid = new Panel { Size = new Size(14, 14) };
        private readonly Panel _swatchUnpaid = new Panel { Size = new Size(14, 14) };
        private readonly Label _lblLegendPresent = new Label { Text = "Đi làm (1)", AutoSize = true };
        private readonly Label _lblLegendPaid = new Label { Text = "Nghỉ có phép (2)", AutoSize = true };
        private readonly Label _lblLegendUnpaid = new Label { Text = "Nghỉ không phép (3)", AutoSize = true };
        private readonly Label _lblEmpStats = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

        private readonly Panel _warnBanner = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _lblWarn = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ProgressBar _progressBar = new ProgressBar { Dock = DockStyle.Top, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 28, Visible = false };

        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        private readonly BackgroundWorker _saveWorker = new BackgroundWorker();
        private ThemedContextMenu _gridMenu;
        private ThemeTokens _theme = ThemeManager.Current;

        private List<PayrollPeriodRow> _periods = new List<PayrollPeriodRow>();
        private PayrollSheetData _currentSheet;
        private string _activePeriodId = "";
        private bool _loading;
        private bool _isDirty;
        private bool _splitPlaced;
        private bool _busy;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal bool UseAsyncWorkerForTest { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal string LastInfoMessageForTest { get; private set; }
        internal PayrollSaveResult LastSaveResultForTest { get; private set; }
        internal PayrollDeleteResult LastDeleteResultForTest { get; private set; }
        internal bool UnsavedPromptTriggeredForTest { get; private set; }
        internal Func<bool> ConfirmUnsavedOverrideForTest { get; set; }
        internal Func<string, bool> ConfirmReloadOverrideForTest { get; set; }
        internal Func<string, bool> ConfirmDeleteOverrideForTest { get; set; }
        internal Func<PayrollCreateDialog, DialogResult> CreateDialogOpenerForTest { get; set; }
        internal Func<PayrollPayoutDialog, DialogResult> PayoutDialogOpenerForTest { get; set; }

        internal PayrollMode ModeForTest { get { return _mode; } }
        internal PayrollSheetData CurrentSheetForTest { get { return _currentSheet; } }
        internal DataGridView PeriodGridForTest { get { return _periodGrid; } }
        internal DataGridView GridForTest { get { return _grid; } }
        internal Panel ToolbarPanelForTest { get { return _bar; } }
        internal FlowLayoutPanel ToolbarFlowForTest { get { return _barFlow; } }
        internal FlatButton AddButtonForTest { get { return _btnAdd; } }
        internal FlatButton DeleteButtonForTest { get { return _btnDelete; } }
        internal FlatButton PresentButtonForTest { get { return _btnPresent; } }
        internal FlatButton PaidLeaveButtonForTest { get { return _btnPaidLeave; } }
        internal FlatButton UnpaidLeaveButtonForTest { get { return _btnUnpaidLeave; } }
        internal FlatButton ClearCellButtonForTest { get { return _btnClearCell; } }
        internal FlatButton ReloadButtonForTest { get { return _btnReload; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton PayOutButtonForTest { get { return _btnPayOut; } }
        internal Label EmployeeStatsLabelForTest { get { return _lblEmpStats; } }
        internal Label SummaryLabelForTest { get { return _summary; } }
        internal Panel WarningBannerForTest { get { return _warnBanner; } }
        internal bool IsDirtyForTest { get { return _isDirty; } }

        public PayrollView(UserSession session, PayrollMode mode)
        {
            _session = session ?? AppSession.Current;
            _mode = mode;
            _service = new PayrollService(_session, mode);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _summaryFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);
            _cellSymbolFont = new Font(ThemeManager.HeadingFontName, ThemeManager.SmallFontSize, FontStyle.Bold);
            _smallFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);

            _loading = true;
            BuildLeftPane(s);
            BuildRightToolbar(s);
            BuildInfoBar(s);
            BuildWarningAndProgress(s);
            BuildMainGrid(s);

            _split.Panel2.Controls.Add(_grid);
            _split.Panel2.Controls.Add(_progressBar);
            _split.Panel2.Controls.Add(_warnBanner);
            _split.Panel2.Controls.Add(_infoBar);
            _split.Panel2.Controls.Add(_bar);
            _grid.BringToFront();

            _split.FixedPanel = FixedPanel.Panel1;
            _split.SizeChanged += delegate { PlaceSplit(); };
            Controls.Add(_split);
            _loading = false;

            BuildGridMenu();
            WireEvents();
            ApplyPermissionsAndMode();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            ReloadPeriods(null, true);
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int want = (int)(228 * s);
            if (_split.Width > want + (int)(420 * s))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        private void BuildLeftPane(float s)
        {
            _leftTitle.Text = "Danh sách bảng lương";
            _leftTitle.Font = _headingFont;
            _leftTitle.Height = (int)(32 * s);
            _leftTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            _leftBar.Height = (int)(48 * s);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding((int)(8 * s), (int)(6 * s), (int)(8 * s), (int)(6 * s))
            };
            foreach (FlatButton b in new[] { _btnAdd, _btnDelete })
            {
                b.Height = (int)(34 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = _bodyFont;
                b.Width = Math.Max((int)(78 * s), TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s));
                flow.Controls.Add(b);
            }
            _leftBar.Controls.Add(flow);

            _periodGrid.Dock = DockStyle.Fill;
            _periodGrid.ReadOnly = true;
            _periodGrid.AllowUserToAddRows = false;
            _periodGrid.AllowUserToDeleteRows = false;
            _periodGrid.AllowUserToResizeRows = false;
            _periodGrid.AutoGenerateColumns = false;
            _periodGrid.MultiSelect = false;
            _periodGrid.RowHeadersVisible = false;
            _periodGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _periodGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _periodGrid.ColumnHeadersHeight = (int)(34 * s);
            _periodGrid.RowTemplate.Height = (int)(32 * s);
            _periodGrid.Font = _bodyFont;
            _periodGrid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            EnableGridDoubleBuffer(_periodGrid);

            var colName = new DataGridViewTextBoxColumn
            {
                Name = "colPeriodName",
                HeaderText = "Bảng lương",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
            _periodGrid.Columns.Add(colName);

            _split.Panel1.Controls.Add(_periodGrid);
            _split.Panel1.Controls.Add(_leftBar);
            _split.Panel1.Controls.Add(_leftTitle);
            _periodGrid.BringToFront();
        }

        private void BuildRightToolbar(float s)
        {
            _bar.Height = (int)(54 * s);
            _barFlow.Dock = DockStyle.Fill;
            _barFlow.FlowDirection = FlowDirection.LeftToRight;
            _barFlow.WrapContents = false;
            _barFlow.AutoScroll = false;
            _barFlow.Padding = new Padding((int)(10 * s), (int)(8 * s), (int)(10 * s), (int)(8 * s));

            FlatButton[] buttons = { _btnPresent, _btnPaidLeave, _btnUnpaidLeave, _btnClearCell, _btnReload, _btnSave, _btnPayOut };
            foreach (FlatButton b in buttons)
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = _bodyFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(22 * s);
                _barFlow.Controls.Add(b);
            }

            _summary.Width = (int)(240 * s);
            _summary.Dock = DockStyle.Right;
            _summary.Padding = new Padding(0, 0, (int)(12 * s), 0);
            _summary.Font = _bodyFont;

            _bar.Controls.Add(_barFlow);
            _bar.Controls.Add(_summary);
        }

        private void BuildInfoBar(float s)
        {
            _infoBar.Height = (int)(32 * s);
            _infoBar.Padding = new Padding((int)(10 * s), (int)(4 * s), (int)(12 * s), (int)(4 * s));

            var legendFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            AddLegendItem(legendFlow, _swatchPresent, _lblLegendPresent, s);
            AddLegendItem(legendFlow, _swatchPaid, _lblLegendPaid, s);
            AddLegendItem(legendFlow, _swatchUnpaid, _lblLegendUnpaid, s);

            _lblEmpStats.Dock = DockStyle.Fill;
            _lblEmpStats.Font = _headingFont;
            _lblEmpStats.Text = "";

            _infoBar.Controls.Add(_lblEmpStats);
            _infoBar.Controls.Add(legendFlow);
        }

        private void AddLegendItem(FlowLayoutPanel flow, Panel swatch, Label label, float s)
        {
            int sq = (int)(14 * s);
            swatch.Size = new Size(sq, sq);
            swatch.Margin = new Padding(0, (int)(5 * s), (int)(4 * s), 0);
            swatch.BorderStyle = BorderStyle.FixedSingle;

            label.Font = _smallFont;
            label.Margin = new Padding(0, (int)(3 * s), (int)(14 * s), 0);

            flow.Controls.Add(swatch);
            flow.Controls.Add(label);
        }

        private void BuildWarningAndProgress(float s)
        {
            _warnBanner.Height = (int)(32 * s);
            _warnBanner.Padding = new Padding((int)(12 * s), 0, (int)(12 * s), 0);
            _lblWarn.Font = _bodyFont;
            _warnBanner.Controls.Add(_lblWarn);

            _progressBar.Height = Math.Max(4, (int)(4 * s));
        }

        private void BuildMainGrid(float s)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = true;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(42 * s);
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(44 * s);
            _grid.RowTemplate.Height = (int)(30 * s);
            _grid.Font = _bodyFont;
            _grid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            EnableGridDoubleBuffer(_grid);

            _empty.Font = _bodyFont;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private void ApplyPermissionsAndMode()
        {
            bool isPayroll = _mode == PayrollMode.Payroll;
            bool hasSheet = _currentSheet != null && _currentSheet.Period != null;
            bool canEditCells = !_busy && _service.CanEdit && hasSheet && _currentSheet.Shifts.Count > 0;

            _btnAdd.Enabled = !_busy && _service.CanAdd;
            _btnDelete.Enabled = !_busy && _service.CanRemove && hasSheet;

            _btnPresent.Enabled = canEditCells;
            _btnPaidLeave.Enabled = canEditCells;
            _btnUnpaidLeave.Enabled = canEditCells;
            _btnClearCell.Enabled = canEditCells;

            _btnReload.Visible = isPayroll;
            _btnReload.Enabled = !_busy && isPayroll && _service.CanEdit && hasSheet;

            _btnSave.Enabled = !_busy && _service.CanEdit && hasSheet;
            ApplyPayOutButtonState();
        }

        private static void EnableGridDoubleBuffer(DataGridView g)
        {
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(g, true, null);
        }
    }
}
