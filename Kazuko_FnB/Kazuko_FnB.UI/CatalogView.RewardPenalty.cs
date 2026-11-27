using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Chế độ Thưởng phạt (TTHUONGPHAT, spec docs/specs/nhan-su-thuong-phat-tam-ung.md §2) trong <see cref="CatalogView"/>:
    /// lưới 7 cột (Số phiếu, Ngày, Nhân viên, Thưởng, Phạt, Lý do, Ghi chú),
    /// bộ lọc Từ ngày / Đến ngày (mặc định tháng hiện tại) + Nhân viên, hàng tổng cộng Thưởng và Phạt,
    /// ô tìm không dấu (F3), phân quyền theo tên chức năng, chỉ Xem mở hộp thoại chỉ đọc, xuất Excel qua <c>GridExcelExporter</c>.
    /// Phần bộ lọc, thanh tổng cộng, menu ngữ cảnh và theme đặt ở <c>CatalogView.RewardPenalty.Filters.cs</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _rewardPenaltyMode;
        private bool _canViewRewardPenalty;
        private RewardPenaltyService _rewardPenaltyService;
        private List<RewardPenaltyRow> _rewardPenaltyRows = new List<RewardPenaltyRow>();
        private List<LookupItem> _rpEmployees = new List<LookupItem>();
        private bool _updatingRpFilters;
        private bool _rpDatesInitialized;

        private readonly Panel _rpFilterBar = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _lblRpFrom = new Label { Text = "Từ ngày:", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly DateBox _dtRpFrom = new DateBox();
        private readonly Label _lblRpTo = new Label { Text = "Đến ngày:", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly DateBox _dtRpTo = new DateBox();
        private readonly Label _lblRpEmp = new Label { Text = "Nhân viên:", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly ComboBox _cboRpEmp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly FlatButton _btnRpFilter = new FlatButton { Text = "Lọc" };

        private readonly Panel _rpTotalBar = new Panel { Dock = DockStyle.Bottom, Visible = false };
        private readonly Label _lblRpTotalCaption = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly Label _lblRpTotalValues = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false };
        private Font _rpTotalFont;
        private decimal _rpTotalBonus;
        private decimal _rpTotalPenalty;

        private readonly ThemedContextMenu _rpGridMenu = new ThemedContextMenu();

        // Hook kiểm thử UI (selftest).
        internal bool IsRewardPenaltyModeForTest { get { return _rewardPenaltyMode; } }
        internal List<RewardPenaltyRow> RewardPenaltyRowsForTest { get { return _rewardPenaltyRows; } }
        internal DateBox RpFromDateBoxForTest { get { return _dtRpFrom; } }
        internal DateBox RpToDateBoxForTest { get { return _dtRpTo; } }
        internal ComboBox RpEmployeeFilterForTest { get { return _cboRpEmp; } }
        internal FlatButton RpFilterButtonForTest { get { return _btnRpFilter; } }
        internal Panel RpTotalBarForTest { get { return _rpTotalBar; } }
        internal decimal RpTotalBonusForTest { get { return _rpTotalBonus; } }
        internal decimal RpTotalPenaltyForTest { get { return _rpTotalPenalty; } }
        internal Label RpTotalValuesLabelForTest { get { return _lblRpTotalValues; } }
        internal bool SuppressRpPopupForTest { get; set; }
        internal string LastRpMessageForTest { get; private set; }
        internal Func<RewardPenaltyEditDialog, DialogResult> RpDialogOpenerForTest { get; set; }

        private UserSession ResolveRewardPenaltySession()
        {
            UserSession current = AppSession.Current;
            if (current != null && !string.IsNullOrEmpty(current.DatabasePath))
                return current;
            return new UserSession
            {
                UserId = current == null ? "" : current.UserId,
                UserName = current == null ? "" : current.UserName,
                DisplayName = current == null ? "" : current.DisplayName,
                IsAdmin = current != null && current.IsAdmin,
                DatabasePath = _databasePath,
                Permissions = current == null ? new FunctionPermissions(false) : current.Permissions
            };
        }

        private void LoadRewardPenaltyData(CatalogData data)
        {
            UserSession sess = ResolveRewardPenaltySession();
            _rewardPenaltyService = new RewardPenaltyService(sess);
            _canViewRewardPenalty = _rewardPenaltyService.CanView;
            _canAdd = _rewardPenaltyService.CanAdd;
            _canEdit = _rewardPenaltyService.CanEdit;
            _canRemove = _rewardPenaltyService.CanRemove;

            if (!_rpDatesInitialized)
            {
                _updatingRpFilters = true;
                try
                {
                    DateTime today = DateTime.Today;
                    _dtRpFrom.Value = new DateTime(today.Year, today.Month, 1);
                    _dtRpTo.Value = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    _rpDatesInitialized = true;
                }
                finally
                {
                    _updatingRpFilters = false;
                }
            }

            try
            {
                _rpEmployees = _rewardPenaltyService.LoadActiveEmployees();
            }
            catch (DataAccessException)
            {
                _rpEmployees = new List<LookupItem>();
            }
            PopulateRewardPenaltyEmployeeFilter();

            if (_def != null)
            {
                _def.CategoryColumn = null;
                _def.CategoryTable = null;
            }
            BuildTree(new CatalogData());

            ReloadRewardPenaltyRowsOnly();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyRewardPenaltyBarVisibility();
            UpdateRewardPenaltyButtons();
            UpdateRewardPenaltyAuditPanel();
        }

        private void ReloadRewardPenaltyRowsOnly()
        {
            if (!_rewardPenaltyMode || _rewardPenaltyService == null)
                return;
            var filter = new RewardPenaltyFilter
            {
                FromDate = _dtRpFrom.Value.Date,
                ToDate = _dtRpTo.Value.Date,
                EmployeeId = SelectedRpEmployeeId()
            };
            _rewardPenaltyRows = _rewardPenaltyService.LoadList(filter);
            _total = _rewardPenaltyRows.Count;
            BuildRewardPenaltyColumns();
            BuildRewardPenaltyTable();
            if (!_loading)
                ApplyFilter();
            UpdateRewardPenaltyButtons();
            UpdateRewardPenaltyAuditPanel();
        }

        private void ApplyRewardPenaltyBarVisibility()
        {
            if (!_rewardPenaltyMode)
                return;
            _btnAdd.Visible = _canAdd;
            _btnEdit.Visible = _canEdit;
            _btnRemove.Visible = _canRemove;
            _refresh.Visible = true;
            _btnExcelExport.Visible = true;
            _btnExcelAdd.Visible = false;
            _btnExcelImport.Visible = false;
            _btnPrint.Visible = false;
            _btnPrintBarcode.Visible = false;
            _btnQuickAdd.Visible = false;
            _btnTotal.Visible = false;

            _groupBar.Visible = false;
            _rpFilterBar.Visible = true;
            _rpTotalBar.Visible = true;
            _bottom.Visible = true;
            _paneSplitter.Visible = true;
            LayoutBar();
            LayoutRewardPenaltyFilterBar();
            LayoutRewardPenaltyTotalBar();
        }

        /// <summary>7 cột theo spec §2: Số phiếu, Ngày, Nhân viên, Thưởng, Phạt, Lý do, Ghi chú.</summary>
        private void BuildRewardPenaltyColumns()
        {
            float s = DeviceDpi / 96f;
            var vi = new CultureInfo("vi-VN");
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddRpColumn("rp_number", "Số phiếu", 125, typeof(string), null, DataGridViewContentAlignment.MiddleLeft, vi);
            AddRpColumn("rp_date", "Ngày", 105, typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter, vi);
            AddRpColumn("rp_emp", "Nhân viên", 190, typeof(string), null, DataGridViewContentAlignment.MiddleLeft, vi);
            AddRpColumn("rp_bonus", "Thưởng", 120, typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight, vi);
            AddRpColumn("rp_penalty", "Phạt", 120, typeof(decimal), "#,##0.##", DataGridViewContentAlignment.MiddleRight, vi);
            AddRpColumn("rp_reason", "Lý do", 170, typeof(string), null, DataGridViewContentAlignment.MiddleLeft, vi);
            AddRpColumn("rp_note", "Ghi chú", 180, typeof(string), null, DataGridViewContentAlignment.MiddleLeft, vi);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddRpColumn(string name, string header, int width96, Type type, string format,
            DataGridViewContentAlignment align, CultureInfo vi)
        {
            float s = DeviceDpi / 96f;
            int headerNeed = TextRenderer.MeasureText(header, _grid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(30 * s);
            int minW = Math.Max((int)(60 * s), headerNeed);
            var gc = new DataGridViewTextBoxColumn
            {
                Name = name,
                DataPropertyName = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.Automatic,
                ReadOnly = true,
                MinimumWidth = minW,
                Width = Math.Max((int)(width96 * s), minW)
            };
            gc.DefaultCellStyle.Alignment = align;
            if (type == typeof(decimal))
            {
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = vi;
            }
            else if (type == typeof(DateTime))
            {
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
            }
            _grid.Columns.Add(gc);
        }

        private void BuildRewardPenaltyTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("rp_number", typeof(string));
            dt.Columns.Add("rp_date", typeof(DateTime));
            dt.Columns.Add("rp_emp", typeof(string));
            dt.Columns.Add("rp_bonus", typeof(decimal));
            dt.Columns.Add("rp_penalty", typeof(decimal));
            dt.Columns.Add("rp_reason", typeof(string));
            dt.Columns.Add("rp_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (RewardPenaltyRow r in _rewardPenaltyRows)
            {
                sb.Length = 0;
                sb.Append(r.Name).Append(' ');
                sb.Append(r.EmployeeName).Append(' ');
                sb.Append(r.ReasonName).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Name ?? "",
                    r.Date.Date,
                    r.EmployeeName ?? "",
                    r.Bonus,
                    r.Penalty,
                    r.ReasonName ?? "",
                    r.Note ?? "",
                    r.Id,
                    CatalogRepository.Fold(sb.ToString()),
                    "");
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private string CurrentRewardPenaltyId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private RewardPenaltyRow CurrentRewardPenaltyRow()
        {
            string id = CurrentRewardPenaltyId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _rewardPenaltyRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectRewardPenaltyRowByIdForTest(string id)
        {
            if (_view == null || string.IsNullOrEmpty(id))
                return false;
            for (int i = 0; i < _view.Count; i++)
            {
                if (string.Equals(Convert.ToString(_view[i][ColId]), id, StringComparison.Ordinal))
                {
                    _grid.ClearSelection();
                    _grid.CurrentCell = _grid.Rows[i].Cells[0];
                    _grid.Rows[i].Selected = true;
                    UpdateRewardPenaltyButtons();
                    UpdateRewardPenaltyAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateRewardPenaltyButtons()
        {
            if (!_rewardPenaltyMode)
                return;
            bool hasRow = CurrentRewardPenaltyId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void UpdateRewardPenaltyAuditPanel()
        {
            if (!_rewardPenaltyMode)
                return;
            RewardPenaltyRow row = CurrentRewardPenaltyRow();
            if (row == null)
            {
                _lblCreated.Text = "";
                _lblCreatedBy.Text = "";
                _lblModified.Text = "";
                _lblModifiedBy.Text = "";
                return;
            }
            _lblCreated.Text = FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row.CreatedByName ?? "";
            _lblModified.Text = FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row.ModifiedByName ?? "";
        }

        // ------------------------------------------------------------------ CRUD Thưởng phạt

        private void AddRewardPenalty()
        {
            if (!_rewardPenaltyMode || !_canAdd)
                return;
            using (var dlg = new RewardPenaltyEditDialog(ResolveRewardPenaltySession(), null, false))
            {
                OpenRewardPenaltyDialog(dlg);
                if (dlg.AnySaved)
                    RefreshRewardPenaltyAndSelect(dlg.SavedId);
            }
        }

        private void EditRewardPenalty()
        {
            if (!_rewardPenaltyMode || !_canEdit)
                return;
            string id = CurrentRewardPenaltyId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new RewardPenaltyEditDialog(ResolveRewardPenaltySession(), id, false))
            {
                OpenRewardPenaltyDialog(dlg);
                if (dlg.AnySaved)
                    RefreshRewardPenaltyAndSelect(dlg.SavedId);
            }
        }

        private void OpenRewardPenaltyFromGrid()
        {
            if (!_rewardPenaltyMode)
                return;
            if (_canEdit)
            {
                EditRewardPenalty();
                return;
            }
            if (!_canViewRewardPenalty)
                return;
            string id = CurrentRewardPenaltyId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new RewardPenaltyEditDialog(ResolveRewardPenaltySession(), id, true))
                OpenRewardPenaltyDialog(dlg);
        }

        private void DeleteRewardPenalty()
        {
            if (!_rewardPenaltyMode || !_canRemove)
                return;
            string id = CurrentRewardPenaltyId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressRpPopupForTest && !Msg.Confirm(this, RewardPenaltyText.DeleteConfirm))
                return;

            RewardPenaltyDeleteResult res;
            try
            {
                res = _rewardPenaltyService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowRpWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowRpWarning(res.ErrorMessage);
                return;
            }
            LastRpMessageForTest = null;
            Reload();
        }

        private DialogResult OpenRewardPenaltyDialog(RewardPenaltyEditDialog dlg)
        {
            return RpDialogOpenerForTest != null
                ? RpDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void RefreshRewardPenaltyAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectRewardPenaltyRowByIdForTest(id);
        }

        private void ShowRpWarning(string message)
        {
            LastRpMessageForTest = message;
            if (!SuppressRpPopupForTest)
                Msg.Warning(this, message);
        }
    }
}
