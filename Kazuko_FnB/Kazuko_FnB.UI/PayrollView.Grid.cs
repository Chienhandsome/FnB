using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần dựng cột động theo số ngày trong tháng, đổ dữ liệu, tô màu trạng thái chấm công bằng ThemeManager,
    /// gộp 6 cột tiền theo nhân viên (chỉ chế độ Tính lương), hàng tổng cộng, đổi theme sống cho <see cref="PayrollView"/>.
    /// </summary>
    internal sealed partial class PayrollView
    {
        private const string DayColPrefix = "colDay_";

        internal static readonly string[] MoneyColumnNames =
        {
            "colBaseSalary",
            "colTotalSalary",
            "colBonus",
            "colPenalty",
            "colAdvance",
            "colNetSalary"
        };

        internal static Color AttendanceBackColor(ThemeTokens t, AttendanceStatus status, bool isWeekend)
        {
            switch (status)
            {
                case AttendanceStatus.Present:
                    return Draw.Mix(t.Surface, t.Success, 0.35f);
                case AttendanceStatus.PaidLeave:
                    return Draw.Mix(t.Surface, t.Info, 0.35f);
                case AttendanceStatus.UnpaidLeave:
                    return Draw.Mix(t.Surface, t.Danger, 0.30f);
                default:
                    return isWeekend ? Draw.Mix(t.Surface, t.Border, 0.22f) : t.Surface;
            }
        }

        internal static Color SummaryRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Primary, 0.12f);
        }

        internal static Color WarningBannerBackColor(ThemeTokens t)
        {
            Color warnBase = Draw.ShiftHsl(t.Danger, 44f, 0.16f);
            return Draw.Mix(t.Surface, warnBase, 0.26f);
        }

        private void RebuildGridColumns(int year, int month)
        {
            float s = DeviceDpi / 96f;
            _grid.Columns.Clear();

            var colEmp = new DataGridViewTextBoxColumn
            {
                Name = "colEmployee",
                HeaderText = "Nhân viên",
                Width = (int)(168 * s),
                MinimumWidth = (int)(120 * s),
                ReadOnly = true,
                Frozen = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colEmp.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            colEmp.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.Columns.Add(colEmp);

            var colShift = new DataGridViewTextBoxColumn
            {
                Name = "colShift",
                HeaderText = "Ca",
                Width = (int)(86 * s),
                MinimumWidth = (int)(64 * s),
                ReadOnly = true,
                Frozen = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colShift.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            colShift.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.Columns.Add(colShift);

            int totalDays = PayrollCalculator.DaysInMonth(year, month);
            int safeYear = year < 1 ? 1 : year;
            int dayColWidth = Math.Max(27, (int)Math.Round(29 * s));

            for (int d = 1; d <= totalDays; d++)
            {
                var date = new DateTime(safeYear, month, d);
                var colDay = new DataGridViewTextBoxColumn
                {
                    Name = DayColPrefix + d,
                    HeaderText = PayrollRules.DayHeaderText(date),
                    Width = dayColWidth,
                    MinimumWidth = 24,
                    ReadOnly = true,
                    Resizable = DataGridViewTriState.False,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Tag = date
                };
                colDay.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                colDay.DefaultCellStyle.Font = _cellSymbolFont;
                colDay.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _grid.Columns.Add(colDay);
            }

            bool showMoney = _mode == PayrollMode.Payroll;
            AddMoneyColumn("colBaseSalary", "Lương cơ bản", 112, showMoney, s);
            AddMoneyColumn("colTotalSalary", "Tổng lương", 112, showMoney, s);
            AddMoneyColumn("colBonus", "Thưởng", 96, showMoney, s);
            AddMoneyColumn("colPenalty", "Phạt", 96, showMoney, s);
            AddMoneyColumn("colAdvance", "Tạm ứng", 100, showMoney, s);
            AddMoneyColumn("colNetSalary", "Thực nhận", 118, showMoney, s);
        }

        private void AddMoneyColumn(string name, string header, int minWidth96, bool visible, float s)
        {
            int w = Math.Max((int)Math.Ceiling(minWidth96 * s),
                TextRenderer.MeasureText(header, _headingFont).Width + (int)Math.Ceiling(24 * s));
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = w,
                Visible = visible,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            col.DefaultCellStyle.NullValue = "";
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(col);
        }

        private void FillGridFromSheet()
        {
            _grid.SuspendLayout();
            try
            {
                _grid.Rows.Clear();
                if (_currentSheet == null || _currentSheet.Period == null)
                {
                    _empty.Text = _service.CanView ? "Chọn hoặc thêm một bảng lương." : PayrollText.NoPermission;
                    _empty.Visible = true;
                    _summary.Text = "";
                    _lblEmpStats.Text = "";
                    return;
                }

                PayrollPeriodRow period = _currentSheet.Period;
                RebuildGridColumns(period.Year, period.Month);
                ThemeApplier.ApplyGrid(_grid, _theme);
                _grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;

                int totalDays = PayrollCalculator.DaysInMonth(period.Year, period.Month);
                int safeYear = period.Year < 1 ? 1 : period.Year;
                bool showMoney = _mode == PayrollMode.Payroll;

                if (_currentSheet.Shifts.Count == 0)
                {
                    _empty.Text = PayrollText.NoShiftsWarning;
                    _empty.Visible = true;
                    _summary.Text = string.Format("{0} · 0 ca", period.Name);
                    _lblEmpStats.Text = "";
                    return;
                }

                for (int eIdx = 0; eIdx < _currentSheet.Employees.Count; eIdx++)
                {
                    PayrollEmployeeEntry emp = _currentSheet.Employees[eIdx];
                    for (int sIdx = 0; sIdx < _currentSheet.Shifts.Count; sIdx++)
                    {
                        PayrollShiftInfo sh = _currentSheet.Shifts[sIdx];
                        bool isFirst = sIdx == 0;
                        int rowIdx = _grid.Rows.Add();
                        DataGridViewRow row = _grid.Rows[rowIdx];
                        row.Tag = new GridRowMeta
                        {
                            Employee = emp,
                            Shift = sh,
                            IsFirstShiftOfEmployee = isFirst
                        };

                        row.Cells["colEmployee"].Value = isFirst ? emp.EmployeeName : "";
                        row.Cells["colShift"].Value = sh.Name;

                        for (int d = 1; d <= totalDays; d++)
                        {
                            var date = new DateTime(safeYear, period.Month, d);
                            AttendanceStatus st = emp.GetStatus(sh.Id, date);
                            DataGridViewCell cell = row.Cells[DayColPrefix + d];
                            ApplyDayCellPresentation(cell, st, IsWeekend(date), _theme);
                        }

                        if (showMoney && isFirst)
                            WriteEmployeeMoneyCells(row, emp, _theme);
                    }
                }

                if (showMoney && _currentSheet.Employees.Count > 0)
                {
                    int sumIdx = _grid.Rows.Add();
                    DataGridViewRow sumRow = _grid.Rows[sumIdx];
                    sumRow.Tag = SummaryRowTag;
                    sumRow.ReadOnly = true;
                    sumRow.Cells["colEmployee"].Value = "Tổng cộng";
                    UpdateSummaryRowValues(sumRow, _theme);
                    StyleSummaryRow(sumRow, _theme);
                }

                _empty.Visible = _currentSheet.Employees.Count == 0;
                if (_empty.Visible)
                    _empty.Text = "Chưa có nhân viên nào trong bảng lương này.";

                _summary.Text = string.Format("{0} · {1} nhân viên", period.Name, _currentSheet.Employees.Count);
                UpdateSelectedEmployeeStats();
            }
            finally
            {
                _grid.ResumeLayout();
            }
        }

        private void ApplyDayCellPresentation(DataGridViewCell cell, AttendanceStatus status, bool isWeekend, ThemeTokens t)
        {
            cell.Value = PayrollRules.StatusSymbol(status);
            Color back = AttendanceBackColor(t, status, isWeekend);
            cell.Style.BackColor = back;
            cell.Style.ForeColor = t.Text;
            cell.Style.SelectionBackColor = Draw.Mix(back, t.Primary, 0.45f);
            cell.Style.SelectionForeColor = t.OnPrimary;
        }

        private static void WriteEmployeeMoneyCells(DataGridViewRow row, PayrollEmployeeEntry emp, ThemeTokens t)
        {
            row.Cells["colBaseSalary"].Value = PayrollRules.FormatMoneyDisplay(emp.BaseSalary);
            row.Cells["colTotalSalary"].Value = PayrollRules.FormatMoneyDisplay(emp.TotalSalary);
            row.Cells["colBonus"].Value = PayrollRules.FormatMoneyDisplay(emp.Bonus);
            row.Cells["colPenalty"].Value = PayrollRules.FormatMoneyDisplay(emp.Penalty);
            row.Cells["colAdvance"].Value = PayrollRules.FormatMoneyDisplay(emp.Advance);

            DataGridViewCell netCell = row.Cells["colNetSalary"];
            netCell.Value = PayrollRules.FormatMoneyDisplay(emp.NetSalary);
            netCell.Style.ForeColor = emp.NetSalary < 0m ? t.Danger : t.Text;
        }

        private void RefreshEmployeeMoneyInGrid(string employeeId)
        {
            if (_mode != PayrollMode.Payroll || _currentSheet == null)
                return;

            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var meta = _grid.Rows[i].Tag as GridRowMeta;
                if (meta != null && meta.IsFirstShiftOfEmployee
                    && string.Equals(meta.Employee.EmployeeId, employeeId, StringComparison.Ordinal))
                {
                    WriteEmployeeMoneyCells(_grid.Rows[i], meta.Employee, _theme);
                    break;
                }
            }

            if (_grid.Rows.Count > 0 && ReferenceEquals(_grid.Rows[_grid.Rows.Count - 1].Tag, SummaryRowTag))
                UpdateSummaryRowValues(_grid.Rows[_grid.Rows.Count - 1], _theme);
        }

        private void UpdateSummaryRowValues(DataGridViewRow sumRow, ThemeTokens t)
        {
            if (sumRow == null || _currentSheet == null || _mode != PayrollMode.Payroll)
                return;

            decimal sumTotal = 0m, sumBonus = 0m, sumPenalty = 0m, sumAdvance = 0m, sumNet = 0m;
            foreach (PayrollEmployeeEntry emp in _currentSheet.Employees)
            {
                sumTotal += emp.TotalSalary;
                sumBonus += emp.Bonus;
                sumPenalty += emp.Penalty;
                sumAdvance += emp.Advance;
                sumNet += emp.NetSalary;
            }

            sumRow.Cells["colTotalSalary"].Value = PayrollRules.FormatMoneyDisplay(sumTotal);
            sumRow.Cells["colBonus"].Value = PayrollRules.FormatMoneyDisplay(sumBonus);
            sumRow.Cells["colPenalty"].Value = PayrollRules.FormatMoneyDisplay(sumPenalty);
            sumRow.Cells["colAdvance"].Value = PayrollRules.FormatMoneyDisplay(sumAdvance);
            sumRow.Cells["colNetSalary"].Value = PayrollRules.FormatMoneyDisplay(sumNet);
            sumRow.Cells["colNetSalary"].Style.ForeColor = sumNet < 0m ? t.Danger : t.Text;
        }

        private void StyleSummaryRow(DataGridViewRow row, ThemeTokens t)
        {
            if (row == null)
                return;
            row.DefaultCellStyle.Font = _summaryFont;
            row.DefaultCellStyle.BackColor = SummaryRowBackColor(t);
            row.DefaultCellStyle.ForeColor = t.Text;
            row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Surface, t.Primary, 0.22f);
            row.DefaultCellStyle.SelectionForeColor = t.Text;
        }

        internal void UpdateSelectedEmployeeStats()
        {
            PayrollEmployeeEntry emp = SelectedEmployee();
            if (emp == null)
            {
                _lblEmpStats.Text = "";
                return;
            }
            _lblEmpStats.Text = PayrollRules.FormatEmployeeStats(emp.EmployeeName, emp.TotalShifts, emp.TotalDays);
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

        private static bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        private static bool IsDayColumn(DataGridViewColumn col)
        {
            return col != null && col.Name != null && col.Name.StartsWith(DayColPrefix, StringComparison.Ordinal);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _leftTitle.ForeColor = t.Text;
            _bar.BackColor = t.Background;
            _barFlow.BackColor = t.Background;
            _infoBar.BackColor = Draw.Mix(t.Background, t.Surface, 0.5f);
            _summary.ForeColor = t.TextMuted;
            _lblEmpStats.ForeColor = t.Text;
            _empty.BackColor = t.Surface;
            _empty.ForeColor = t.TextMuted;
            _warnBanner.BackColor = WarningBannerBackColor(t);
            _lblWarn.ForeColor = t.Text;

            // Tô màu nút chấm công và ô chú giải từ ThemeTokens (không hard-code màu).
            StyleAttendanceButton(_btnPresent, AttendanceBackColor(t, AttendanceStatus.Present, false), t);
            StyleAttendanceButton(_btnPaidLeave, AttendanceBackColor(t, AttendanceStatus.PaidLeave, false), t);
            StyleAttendanceButton(_btnUnpaidLeave, AttendanceBackColor(t, AttendanceStatus.UnpaidLeave, false), t);

            _swatchPresent.BackColor = AttendanceBackColor(t, AttendanceStatus.Present, false);
            _swatchPaid.BackColor = AttendanceBackColor(t, AttendanceStatus.PaidLeave, false);
            _swatchUnpaid.BackColor = AttendanceBackColor(t, AttendanceStatus.UnpaidLeave, false);

            ThemeApplier.ApplyGrid(_periodGrid, t);
            ThemeApplier.ApplyGrid(_grid, t);
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            ReapplyGridColors(t);
        }

        private static void StyleAttendanceButton(FlatButton btn, Color fill, ThemeTokens t)
        {
            btn.SurfaceColor = btn.Parent != null ? btn.Parent.BackColor : t.Background;
            btn.FillColor = fill;
            btn.TextColor = t.Text;
            btn.BorderColor = Draw.StrongBorder(t);
            btn.Invalidate();
        }

        private void ReapplyGridColors(ThemeTokens t)
        {
            for (int r = 0; r < _grid.Rows.Count; r++)
            {
                DataGridViewRow row = _grid.Rows[r];
                if (ReferenceEquals(row.Tag, SummaryRowTag))
                {
                    StyleSummaryRow(row, t);
                    UpdateSummaryRowValues(row, t);
                    continue;
                }
                var meta = row.Tag as GridRowMeta;
                if (meta == null)
                    continue;
                for (int c = 0; c < _grid.Columns.Count; c++)
                {
                    DataGridViewColumn col = _grid.Columns[c];
                    if (!IsDayColumn(col) || !(col.Tag is DateTime))
                        continue;
                    var date = (DateTime)col.Tag;
                    AttendanceStatus st = meta.Employee.GetStatus(meta.Shift.Id, date);
                    ApplyDayCellPresentation(row.Cells[c], st, IsWeekend(date), t);
                }
                if (_mode == PayrollMode.Payroll && meta.IsFirstShiftOfEmployee)
                    row.Cells["colNetSalary"].Style.ForeColor = meta.Employee.NetSalary < 0m ? t.Danger : t.Text;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_gridMenu != null) _gridMenu.Dispose();
                if (_saveWorker != null) _saveWorker.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_headingFont != null) _headingFont.Dispose();
                if (_summaryFont != null) _summaryFont.Dispose();
                if (_cellSymbolFont != null) _cellSymbolFont.Dispose();
                if (_smallFont != null) _smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
