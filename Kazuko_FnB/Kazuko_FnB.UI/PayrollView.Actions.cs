using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần nạp danh sách kỳ bảng lương, chọn kỳ, chấm công nhiều ô (nút + phím tắt 1/2/3/0),
    /// tính lại tại chỗ trong bộ nhớ và menu ngữ cảnh cho <see cref="PayrollView"/>.
    /// Phần Thêm/Xóa kỳ, Tải lại và Lưu (Cập nhật) đặt ở <c>PayrollView.Save.cs</c>.
    /// </summary>
    internal sealed partial class PayrollView
    {
        private void WireEvents()
        {
            _btnAdd.Click += delegate { AddPeriod(); };
            _btnDelete.Click += delegate { DeleteSelectedPeriod(); };

            _btnPresent.Click += delegate { ApplyStatusToSelection(AttendanceStatus.Present); };
            _btnPaidLeave.Click += delegate { ApplyStatusToSelection(AttendanceStatus.PaidLeave); };
            _btnUnpaidLeave.Click += delegate { ApplyStatusToSelection(AttendanceStatus.UnpaidLeave); };
            _btnClearCell.Click += delegate { ApplyStatusToSelection(AttendanceStatus.Empty); };
            _btnReload.Click += delegate { ReloadAllFromCatalog(); };
            _btnSave.Click += delegate { SaveChanges(); };
            _btnPayOut.Click += delegate { OpenPayOutDialog(); };

            _periodGrid.SelectionChanged += OnPeriodSelectionChanged;
            _grid.SelectionChanged += delegate { if (!_loading) UpdateSelectedEmployeeStats(); };
            _grid.CurrentCellChanged += delegate { if (!_loading) UpdateSelectedEmployeeStats(); };
            _grid.RowPostPaint += DrawRowNumber;
            _grid.MouseDown += OnGridMouseDown;
            _grid.KeyDown += OnGridKeyDown;

            _saveWorker.DoWork += OnSaveWorkerDoWork;
            _saveWorker.RunWorkerCompleted += OnSaveWorkerCompleted;
        }

        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem mPresent = _gridMenu.AddItem("Đi làm (1)", delegate { ApplyStatusToSelection(AttendanceStatus.Present); });
            ToolStripMenuItem mPaid = _gridMenu.AddItem("Nghỉ có phép (2)", delegate { ApplyStatusToSelection(AttendanceStatus.PaidLeave); });
            ToolStripMenuItem mUnpaid = _gridMenu.AddItem("Nghỉ không phép (3)", delegate { ApplyStatusToSelection(AttendanceStatus.UnpaidLeave); });
            ToolStripMenuItem mClear = _gridMenu.AddItem("Xóa chấm công (0)", delegate { ApplyStatusToSelection(AttendanceStatus.Empty); });

            ToolStripSeparator sepReload = null;
            ToolStripMenuItem mReloadEmp = null;
            if (_mode == PayrollMode.Payroll)
            {
                sepReload = new ToolStripSeparator();
                _gridMenu.Items.Add(sepReload);
                mReloadEmp = _gridMenu.AddItem("Tải lại cho nhân viên này", delegate { ReloadSelectedEmployeeFromCatalog(); });
            }

            _gridMenu.AddSeparator();
            ToolStripMenuItem mCopyCell = _gridMenu.AddItem("Sao chép ô", delegate { DetailGridMenu.CopyCell(_grid); });
            ToolStripMenuItem mCopySel = _gridMenu.AddItem("Sao chép vùng chọn", delegate { DetailGridMenu.CopySelection(_grid); });

            _gridMenu.Opening += delegate
            {
                bool canEditCells = !_busy && _service.CanEdit && _currentSheet != null && _currentSheet.Shifts.Count > 0;
                bool hasDayCell = HasSelectedDayCell();
                mPresent.Enabled = canEditCells && hasDayCell;
                mPaid.Enabled = canEditCells && hasDayCell;
                mUnpaid.Enabled = canEditCells && hasDayCell;
                mClear.Enabled = canEditCells && hasDayCell;

                if (mReloadEmp != null)
                {
                    mReloadEmp.Visible = _mode == PayrollMode.Payroll;
                    mReloadEmp.Enabled = !_busy && _service.CanEdit && SelectedEmployee() != null;
                    if (sepReload != null)
                        sepReload.Visible = mReloadEmp.Visible;
                }

                mCopyCell.Enabled = _grid.CurrentCell != null;
                mCopySel.Enabled = _grid.SelectedCells.Count > 0;
            };
        }

        /// <summary>Nạp lại danh sách bảng lương (trái) và mở kỳ đang chọn (hoặc kỳ chỉ định).</summary>
        public void ReloadPeriods(string selectPeriodId = null, bool skipConfirm = false)
        {
            if (!skipConfirm && !ConfirmDiscardUnsavedChanges())
                return;

            string wantId = !string.IsNullOrEmpty(selectPeriodId) ? selectPeriodId : _activePeriodId;
            _loading = true;
            try
            {
                _periods = _service.LoadPeriods();
                _periodGrid.Rows.Clear();
                foreach (PayrollPeriodRow p in _periods)
                {
                    int idx = _periodGrid.Rows.Add(p.Name);
                    _periodGrid.Rows[idx].Tag = p;
                }

                int targetIdx = -1;
                if (!string.IsNullOrEmpty(wantId))
                {
                    for (int i = 0; i < _periods.Count; i++)
                    {
                        if (string.Equals(_periods[i].Id, wantId, StringComparison.Ordinal))
                        {
                            targetIdx = i;
                            break;
                        }
                    }
                }
                if (targetIdx < 0 && _periods.Count > 0)
                    targetIdx = 0;

                if (targetIdx >= 0)
                {
                    _periodGrid.ClearSelection();
                    _periodGrid.Rows[targetIdx].Selected = true;
                    _periodGrid.CurrentCell = _periodGrid.Rows[targetIdx].Cells[0];
                    _activePeriodId = _periods[targetIdx].Id;
                }
                else
                {
                    _activePeriodId = "";
                }
            }
            catch (DataAccessException ex)
            {
                _periods = new List<PayrollPeriodRow>();
                _periodGrid.Rows.Clear();
                _activePeriodId = "";
                ShowWarning("Không đọc được danh sách bảng lương: " + ex.Message);
            }
            finally
            {
                _loading = false;
            }

            LoadSelectedSheet();
        }

        internal bool SelectPeriodByIdForTest(string periodId)
        {
            for (int i = 0; i < _periodGrid.Rows.Count; i++)
            {
                var p = _periodGrid.Rows[i].Tag as PayrollPeriodRow;
                if (p != null && string.Equals(p.Id, periodId, StringComparison.Ordinal))
                {
                    _periodGrid.ClearSelection();
                    _periodGrid.Rows[i].Selected = true;
                    _periodGrid.CurrentCell = _periodGrid.Rows[i].Cells[0];
                    return true;
                }
            }
            return false;
        }

        private void OnPeriodSelectionChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            PayrollPeriodRow picked = SelectedPeriodRow();
            string nextId = picked == null ? "" : (picked.Id ?? "");
            if (string.Equals(nextId, _activePeriodId, StringComparison.Ordinal))
                return;

            if (!ConfirmDiscardUnsavedChanges())
            {
                _loading = true;
                try { SelectPeriodByIdForTest(_activePeriodId); }
                finally { _loading = false; }
                return;
            }

            _activePeriodId = nextId;
            LoadSelectedSheet();
        }

        private void LoadSelectedSheet()
        {
            _loading = true;
            try
            {
                _warnBanner.Visible = false;
                _lblWarn.Text = "";
                _isDirty = false;

                if (string.IsNullOrEmpty(_activePeriodId))
                {
                    _currentSheet = null;
                    FillGridFromSheet();
                    return;
                }

                _currentSheet = _service.LoadSheet(_activePeriodId);
                if (_currentSheet != null && !string.IsNullOrEmpty(_currentSheet.Warning))
                {
                    _lblWarn.Text = _currentSheet.Warning;
                    _warnBanner.Visible = true;
                    ShowWarning(_currentSheet.Warning);
                }
                FillGridFromSheet();
            }
            catch (DataAccessException ex)
            {
                _currentSheet = null;
                FillGridFromSheet();
                ShowWarning("Không đọc được bảng lương: " + ex.Message);
            }
            finally
            {
                _loading = false;
                ApplyPermissionsAndMode();
            }
        }

        // ------------------------------------------------------------------ Chấm công (áp trạng thái cho tập ô đang chọn)

        internal int ApplyStatusToSelection(AttendanceStatus status)
        {
            if (_busy || !_service.CanEdit || _currentSheet == null || _currentSheet.Period == null)
                return 0;

            var cells = new List<DataGridViewCell>();
            foreach (DataGridViewCell c in _grid.SelectedCells)
                cells.Add(c);
            return ApplyStatusToCells(cells, status);
        }

        /// <summary>Áp trạng thái trực tiếp cho tập ô ngày cụ thể (dùng cho selftest UI và nút/phím tắt).</summary>
        internal int ApplyStatusToCellsForTest(IEnumerable<DataGridViewCell> cells, AttendanceStatus status)
        {
            return ApplyStatusToCells(cells, status);
        }

        private int ApplyStatusToCells(IEnumerable<DataGridViewCell> cells, AttendanceStatus status)
        {
            if (_busy || !_service.CanEdit || _currentSheet == null || _currentSheet.Period == null || cells == null)
                return 0;

            var affectedEmployees = new HashSet<PayrollEmployeeEntry>();
            int changed = 0;

            foreach (DataGridViewCell cell in cells)
            {
                if (cell == null || cell.RowIndex < 0 || cell.ColumnIndex < 0)
                    continue;
                DataGridViewRow row = _grid.Rows[cell.RowIndex];
                if (ReferenceEquals(row.Tag, SummaryRowTag))
                    continue;
                var meta = row.Tag as GridRowMeta;
                DataGridViewColumn col = _grid.Columns[cell.ColumnIndex];
                if (meta == null || !IsDayColumn(col) || !(col.Tag is DateTime))
                    continue;

                var date = (DateTime)col.Tag;
                meta.Employee.SetStatus(meta.Shift.Id, date, status);
                ApplyDayCellPresentation(cell, status, IsWeekend(date), _theme);
                affectedEmployees.Add(meta.Employee);
                changed++;
            }

            if (changed == 0)
                return 0;

            foreach (PayrollEmployeeEntry emp in affectedEmployees)
            {
                if (_mode == PayrollMode.Payroll)
                {
                    PayrollCalculator.RecalculateEmployee(_currentSheet.Period.Year, _currentSheet.Period.Month, emp, _currentSheet.Shifts);
                    RefreshEmployeeMoneyInGrid(emp.EmployeeId);
                }
                else
                {
                    PayrollCalculator.RecalculateAttendanceCountsOnly(_currentSheet.Period.Year, _currentSheet.Period.Month, emp, _currentSheet.Shifts);
                }
            }

            _isDirty = true;
            UpdateSelectedEmployeeStats();
            return changed;
        }

        // ------------------------------------------------------------------ Chuột phải, phím tắt, hỗ trợ tra cứu

        private void OnGridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
            {
                DataGridViewCell cell = _grid.Rows[hit.RowIndex].Cells[hit.ColumnIndex];
                if (!cell.Selected)
                {
                    _grid.ClearSelection();
                    _grid.CurrentCell = cell;
                    cell.Selected = true;
                }
            }
            _gridMenu.Show(_grid, e.Location);
        }

        private void OnGridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control || e.Alt || e.Shift)
                return;
            if (TryHandleStatusKey(e.KeyCode))
                e.Handled = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S) || keyData == (Keys.Control | Keys.L))
            {
                if (_btnSave.Enabled)
                    SaveChanges();
                return true;
            }
            if ((keyData & (Keys.Control | Keys.Alt | Keys.Shift)) == 0 && _grid.ContainsFocus)
            {
                if (TryHandleStatusKey(keyData & Keys.KeyCode))
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool TryHandleStatusKey(Keys keyCode)
        {
            switch (keyCode)
            {
                case Keys.D1:
                case Keys.NumPad1:
                    return ApplyStatusToSelection(AttendanceStatus.Present) > 0;
                case Keys.D2:
                case Keys.NumPad2:
                    return ApplyStatusToSelection(AttendanceStatus.PaidLeave) > 0;
                case Keys.D3:
                case Keys.NumPad3:
                    return ApplyStatusToSelection(AttendanceStatus.UnpaidLeave) > 0;
                case Keys.D0:
                case Keys.NumPad0:
                    return ApplyStatusToSelection(AttendanceStatus.Empty) > 0;
                default:
                    return false;
            }
        }

        private PayrollPeriodRow SelectedPeriodRow()
        {
            if (_periodGrid.CurrentRow == null)
                return null;
            return _periodGrid.CurrentRow.Tag as PayrollPeriodRow;
        }

        private PayrollEmployeeEntry SelectedEmployee()
        {
            if (_grid.CurrentCell != null && _grid.CurrentCell.RowIndex >= 0 && _grid.CurrentCell.RowIndex < _grid.Rows.Count)
            {
                var meta = _grid.Rows[_grid.CurrentCell.RowIndex].Tag as GridRowMeta;
                if (meta != null)
                    return meta.Employee;
            }
            if (_grid.SelectedCells.Count > 0)
            {
                int r = _grid.SelectedCells[0].RowIndex;
                if (r >= 0 && r < _grid.Rows.Count)
                {
                    var meta = _grid.Rows[r].Tag as GridRowMeta;
                    if (meta != null)
                        return meta.Employee;
                }
            }
            return null;
        }

        private bool HasSelectedDayCell()
        {
            foreach (DataGridViewCell c in _grid.SelectedCells)
            {
                if (c.RowIndex >= 0 && c.ColumnIndex >= 0
                    && !ReferenceEquals(_grid.Rows[c.RowIndex].Tag, SummaryRowTag)
                    && IsDayColumn(_grid.Columns[c.ColumnIndex]))
                {
                    return true;
                }
            }
            return false;
        }

        internal DataGridViewRow FindRowForEmployeeShiftForTest(string employeeId, string shiftId)
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var meta = _grid.Rows[i].Tag as GridRowMeta;
                if (meta != null
                    && string.Equals(meta.Employee.EmployeeId, employeeId, StringComparison.Ordinal)
                    && string.Equals(meta.Shift.Id, shiftId, StringComparison.Ordinal))
                {
                    return _grid.Rows[i];
                }
            }
            return null;
        }

        internal DataGridViewRow FindFirstRowForEmployeeForTest(string employeeId)
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                var meta = _grid.Rows[i].Tag as GridRowMeta;
                if (meta != null
                    && meta.IsFirstShiftOfEmployee
                    && string.Equals(meta.Employee.EmployeeId, employeeId, StringComparison.Ordinal))
                {
                    return _grid.Rows[i];
                }
            }
            return null;
        }
    }
}
