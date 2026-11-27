using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Chi lương cho bảng lương đang chọn (patch P3b, spec docs/specs/nhan-su-tinh-luong.md §5):
    /// - Lưới các nhân viên có dòng tổng hợp TBANGLUONGTONGHOP: Nhân viên, Thực nhận, Đã chi, Còn lại, Số phiếu; hàng tổng.
    /// - Tô màu cảnh báo (ThemeTokens) khi tổng Đã chi vượt Thực nhận.
    /// - Thanh nút Tạo phiếu chi lương, Xem/Sửa phiếu, Xóa phiếu, Làm mới.
    /// Phần nạp dữ liệu, xác nhận cảnh báo, mở PhieuThuDialog chế độ Chi lương và xóa đặt ở <c>PayrollPayoutDialog.Actions.cs</c>.
    /// </summary>
    internal sealed partial class PayrollPayoutDialog : ThemedDialog
    {
        private static readonly object SummaryRowTag = new object();

        private readonly UserSession _session;
        private readonly PayrollPeriodRow _period;
        private readonly PayrollPayoutService _service;

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _summaryFont;

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlowLayoutPanel _barFlow = new FlowLayoutPanel();
        private readonly FlatButton _btnCreate = new FlatButton { Text = "Tạo phiếu chi lương", Tag = "primary" };
        private readonly FlatButton _btnViewEdit = new FlatButton { Text = "Xem/Sửa phiếu" };
        private readonly FlatButton _btnDelete = new FlatButton { Text = "Xóa phiếu", Tag = "danger" };
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Làm mới" };
        private readonly Label _lblSummary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false };

        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        private readonly FlatButton _btnClose;

        private ThemedContextMenu _gridMenu;
        private PayrollPayoutSheet _sheet;
        private ThemeTokens _theme = ThemeManager.Current;
        private bool _loading;

        public bool AnyChanged { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal string LastConfirmMessageForTest { get; private set; }
        internal VoucherDeleteResult LastDeleteResultForTest { get; private set; }
        internal Func<string, bool> ConfirmZeroSalaryOverrideForTest { get; set; }
        internal Func<string, bool> ConfirmAdditionalPayoutOverrideForTest { get; set; }
        internal Func<string, bool> ConfirmDeleteOverrideForTest { get; set; }
        internal Func<PhieuThuDialog, DialogResult> VoucherDialogOpenerForTest { get; set; }
        internal Func<List<PayrollPayoutVoucherInfo>, string, PayrollPayoutVoucherInfo> VoucherPickerOverrideForTest { get; set; }

        internal PayrollPayoutSheet SheetForTest { get { return _sheet; } }
        internal DataGridView GridForTest { get { return _grid; } }
        internal Panel ToolbarPanelForTest { get { return _bar; } }
        internal FlowLayoutPanel ToolbarFlowForTest { get { return _barFlow; } }
        internal FlatButton CreateButtonForTest { get { return _btnCreate; } }
        internal FlatButton ViewEditButtonForTest { get { return _btnViewEdit; } }
        internal FlatButton DeleteButtonForTest { get { return _btnDelete; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal Label SummaryLabelForTest { get { return _lblSummary; } }

        internal static Color OverpaidRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Danger, 0.22f);
        }

        internal static Color OverpaidForeColor(ThemeTokens t)
        {
            return t.Danger;
        }

        internal static Color SummaryRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Primary, 0.12f);
        }

        public PayrollPayoutDialog(UserSession session, PayrollPeriodRow period)
            : base(
                "CHI LƯƠNG",
                period == null ? "Chi lương nhân viên" : ("Bảng lương " + (period.Name ?? PayrollText.FormatPeriodName(period.Month, period.Year))),
                940, 560)
        {
            if (period == null || string.IsNullOrEmpty(period.Id))
                throw new ArgumentNullException("period");
            _session = session ?? AppSession.Current;
            _period = period;
            _service = new PayrollPayoutService(_session);

            float s = Dpi;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _summaryFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            SetTitle("CHI LƯƠNG — " + (period.Name ?? PayrollText.FormatPeriodName(period.Month, period.Year)).ToUpperInvariant());
            Body.Padding = new Padding((int)(10 * s));

            BuildToolbar(s);
            BuildGrid(s);

            Body.Controls.Add(_grid);
            Body.Controls.Add(_bar);
            _grid.BringToFront();

            _btnClose = AddButton("Đóng", null, delegate { Close(); });
            CancelButton = _btnClose;

            BuildGridMenu();
            WireEvents();
            ApplyDialogTheme();
            ReloadData(null);
        }

        private void BuildToolbar(float s)
        {
            _bar.Height = (int)(52 * s);
            _barFlow.Dock = DockStyle.Fill;
            _barFlow.FlowDirection = FlowDirection.LeftToRight;
            _barFlow.WrapContents = false;
            _barFlow.AutoScroll = false;
            _barFlow.Padding = new Padding(0, (int)(6 * s), (int)(6 * s), (int)(6 * s));

            foreach (FlatButton b in new[] { _btnCreate, _btnViewEdit, _btnDelete, _btnRefresh })
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(8 * s), 0);
                b.Font = _bodyFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s);
                _barFlow.Controls.Add(b);
            }

            _lblSummary.Width = (int)(250 * s);
            _lblSummary.Dock = DockStyle.Right;
            _lblSummary.Font = _bodyFont;
            _lblSummary.Padding = new Padding(0, 0, (int)(4 * s), 0);

            _bar.Controls.Add(_barFlow);
            _bar.Controls.Add(_lblSummary);
        }

        private void BuildGrid(float s)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(42 * s);
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(36 * s);
            _grid.RowTemplate.Height = (int)(32 * s);
            _grid.Font = _bodyFont;
            _grid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            EnableGridDoubleBuffer(_grid);

            AddColumn("colEmployee", "Nhân viên", 210, DataGridViewContentAlignment.MiddleLeft, false, s);
            AddColumn("colNetSalary", "Thực nhận", 130, DataGridViewContentAlignment.MiddleRight, false, s);
            AddColumn("colPaidAmount", "Đã chi", 130, DataGridViewContentAlignment.MiddleRight, false, s);
            AddColumn("colRemaining", "Còn lại", 130, DataGridViewContentAlignment.MiddleRight, false, s);
            AddColumn("colVouchers", "Số phiếu", 220, DataGridViewContentAlignment.MiddleLeft, true, s);

            _empty.Font = _bodyFont;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private void AddColumn(string name, string header, int minWidth96, DataGridViewContentAlignment align, bool fill, float s)
        {
            int w = Math.Max((int)Math.Ceiling(minWidth96 * s),
                TextRenderer.MeasureText(header, _headingFont).Width + (int)Math.Ceiling(26 * s));
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = Math.Min(w, (int)Math.Ceiling(90 * s)),
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None
            };
            col.DefaultCellStyle.Alignment = align;
            col.HeaderCell.Style.Alignment = align;
            _grid.Columns.Add(col);
        }

        private void FillGrid(string selectEmployeeId)
        {
            _grid.SuspendLayout();
            try
            {
                _grid.Rows.Clear();
                if (_sheet == null || _sheet.Rows.Count == 0)
                {
                    _empty.Text = _service.CanView ? PayrollPayoutText.NoSavedSummary : PayrollPayoutText.NoPermission;
                    _empty.Visible = true;
                    _lblSummary.Text = "0 nhân viên";
                    return;
                }

                _empty.Visible = false;
                int targetRowIdx = -1;
                for (int i = 0; i < _sheet.Rows.Count; i++)
                {
                    PayrollPayoutRow item = _sheet.Rows[i];
                    int idx = _grid.Rows.Add(
                        item.EmployeeName,
                        PayrollPayoutRules.FormatMoney(item.NetSalary),
                        PayrollPayoutRules.FormatMoney(item.PaidAmount),
                        PayrollPayoutRules.FormatMoney(item.RemainingAmount),
                        item.VoucherNumbersDisplay ?? "");
                    DataGridViewRow gRow = _grid.Rows[idx];
                    gRow.Tag = item;
                    StyleDataRow(gRow, item, _theme);

                    if (!string.IsNullOrEmpty(selectEmployeeId)
                        && string.Equals(item.EmployeeId, selectEmployeeId, StringComparison.Ordinal))
                    {
                        targetRowIdx = idx;
                    }
                }

                // Hàng tổng cộng ở cuối lưới.
                int sumIdx = _grid.Rows.Add(
                    "Tổng cộng",
                    PayrollPayoutRules.FormatMoney(_sheet.TotalNetSalary),
                    PayrollPayoutRules.FormatMoney(_sheet.TotalPaidAmount),
                    PayrollPayoutRules.FormatMoney(_sheet.TotalRemainingAmount),
                    _sheet.TotalVoucherCount > 0 ? (_sheet.TotalVoucherCount + " phiếu") : "");
                DataGridViewRow sumRow = _grid.Rows[sumIdx];
                sumRow.Tag = SummaryRowTag;
                sumRow.ReadOnly = true;
                StyleSummaryRow(sumRow, _theme);

                if (targetRowIdx < 0 && _sheet.Rows.Count > 0)
                    targetRowIdx = 0;
                if (targetRowIdx >= 0)
                {
                    _grid.ClearSelection();
                    _grid.Rows[targetRowIdx].Selected = true;
                    _grid.CurrentCell = _grid.Rows[targetRowIdx].Cells[0];
                }

                _lblSummary.Text = string.Format("{0} nhân viên · {1} phiếu chi",
                    _sheet.Rows.Count, _sheet.TotalVoucherCount);
            }
            finally
            {
                _grid.ResumeLayout();
            }
        }

        private void StyleDataRow(DataGridViewRow gRow, PayrollPayoutRow item, ThemeTokens t)
        {
            if (gRow == null || item == null)
                return;
            if (item.IsOverpaid)
            {
                Color warnBack = OverpaidRowBackColor(t);
                Color warnFore = OverpaidForeColor(t);
                Color selBack = Draw.Mix(warnBack, t.Primary, 0.35f);
                foreach (DataGridViewCell cell in gRow.Cells)
                {
                    cell.Style.BackColor = warnBack;
                    cell.Style.ForeColor = t.Text;
                    cell.Style.SelectionBackColor = selBack;
                    cell.Style.SelectionForeColor = t.OnPrimary;
                }
                gRow.Cells["colPaidAmount"].Style.ForeColor = warnFore;
                gRow.Cells["colRemaining"].Style.ForeColor = warnFore;
            }
            else
            {
                foreach (DataGridViewCell cell in gRow.Cells)
                {
                    cell.Style.BackColor = t.Surface;
                    cell.Style.ForeColor = t.Text;
                    cell.Style.SelectionBackColor = t.Primary;
                    cell.Style.SelectionForeColor = t.OnPrimary;
                }
                if (item.NetSalary < 0m)
                    gRow.Cells["colNetSalary"].Style.ForeColor = t.Danger;
                if (item.RemainingAmount < 0m)
                    gRow.Cells["colRemaining"].Style.ForeColor = t.Danger;
            }
        }

        private void StyleSummaryRow(DataGridViewRow sumRow, ThemeTokens t)
        {
            if (sumRow == null)
                return;
            Color back = SummaryRowBackColor(t);
            Color selBack = Draw.Mix(t.Surface, t.Primary, 0.22f);
            sumRow.DefaultCellStyle.Font = _summaryFont;
            sumRow.DefaultCellStyle.BackColor = back;
            sumRow.DefaultCellStyle.ForeColor = t.Text;
            sumRow.DefaultCellStyle.SelectionBackColor = selBack;
            sumRow.DefaultCellStyle.SelectionForeColor = t.Text;
            foreach (DataGridViewCell cell in sumRow.Cells)
            {
                cell.Style.Font = _summaryFont;
                cell.Style.BackColor = back;
                cell.Style.ForeColor = t.Text;
                cell.Style.SelectionBackColor = selBack;
                cell.Style.SelectionForeColor = t.Text;
            }
            if (_sheet != null && _sheet.TotalPaidAmount > 0m && _sheet.TotalPaidAmount > _sheet.TotalNetSalary)
                sumRow.Cells["colRemaining"].Style.ForeColor = OverpaidForeColor(t);
        }

        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_grid.RowHeadersVisible || e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
                return;
            bool isSummary = ReferenceEquals(_grid.Rows[e.RowIndex].Tag, SummaryRowTag);
            string text = isSummary ? "Σ" : (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth - 4, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, _theme.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void PositionEmpty()
        {
            _empty.SetBounds(0, _grid.Height / 2 - 16, _grid.Width, 32);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            _theme = t;
            _bar.BackColor = t.Background;
            _barFlow.BackColor = t.Background;
            _lblSummary.BackColor = Color.Transparent;
            _lblSummary.ForeColor = t.TextMuted;
            _empty.BackColor = t.Surface;
            _empty.ForeColor = t.TextMuted;
            if (_grid != null)
            {
                ThemeApplier.ApplyGrid(_grid, t);
                _grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
                ReapplyRowStyles(t);
            }
        }

        private void ReapplyRowStyles(ThemeTokens t)
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                DataGridViewRow gRow = _grid.Rows[i];
                if (ReferenceEquals(gRow.Tag, SummaryRowTag))
                    StyleSummaryRow(gRow, t);
                else if (gRow.Tag is PayrollPayoutRow)
                    StyleDataRow(gRow, (PayrollPayoutRow)gRow.Tag, t);
            }
        }

        private static void EnableGridDoubleBuffer(DataGridView g)
        {
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(g, true, null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_gridMenu != null) _gridMenu.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_headingFont != null) _headingFont.Dispose();
                if (_summaryFont != null) _summaryFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
