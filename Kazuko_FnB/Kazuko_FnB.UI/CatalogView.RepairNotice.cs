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
    /// Chế độ Thông báo sửa chữa (TSUACHUA, spec docs/specs/quan-tri-nho.md §2) trong <see cref="CatalogView"/>:
    /// lưới 9 cột (Số phiếu, Ngày, Phòng/Bàn, Loại phòng, Nhân viên, Nội dung, Đã sửa xong, Còn sử dụng được, Ghi chú),
    /// sắp chưa sửa xong trước rồi ngày giảm dần, bộ lọc Từ/Đến ngày (để trống = tất cả) + tích "Chỉ chưa sửa xong" (mặc định tắt),
    /// ô tìm không dấu (F3), phân quyền theo tên chức năng, chỉ Xem mở hộp thoại chỉ đọc, xuất Excel qua <c>GridExcelExporter</c>.
    /// Phần bộ lọc, menu ngữ cảnh và theme đặt ở <c>CatalogView.RepairNotice.Filters.cs</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _repairNoticeMode;
        private bool _canViewRepairNotice;
        private RepairNoticeService _repairNoticeService;
        private List<RepairNoticeRow> _repairNoticeRows = new List<RepairNoticeRow>();

        private readonly Panel _rnFilterBar = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _lblRnFrom = new Label { Text = "Từ ngày:", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly DateBox _dtRnFrom = new DateBox(true);
        private readonly Label _lblRnTo = new Label { Text = "Đến ngày:", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        private readonly DateBox _dtRnTo = new DateBox(true);
        private readonly CheckBox _chkRnUnfixedOnly = new CheckBox
        {
            Text = "Chỉ chưa sửa xong",
            AutoSize = true,
            Checked = false,
            UseMnemonic = false
        };
        private readonly FlatButton _btnRnFilter = new FlatButton { Text = "Lọc" };
        private readonly ThemedContextMenu _rnGridMenu = new ThemedContextMenu();

        // Hook kiểm thử UI (selftest).
        internal bool IsRepairNoticeModeForTest { get { return _repairNoticeMode; } }
        internal List<RepairNoticeRow> RepairNoticeRowsForTest { get { return _repairNoticeRows; } }
        internal DateBox RnFromDateBoxForTest { get { return _dtRnFrom; } }
        internal DateBox RnToDateBoxForTest { get { return _dtRnTo; } }
        internal CheckBox RnUnfixedOnlyCheckForTest { get { return _chkRnUnfixedOnly; } }
        internal FlatButton RnFilterButtonForTest { get { return _btnRnFilter; } }
        internal bool SuppressRnPopupForTest { get; set; }
        internal string LastRnMessageForTest { get; private set; }
        internal Func<RepairNoticeEditDialog, DialogResult> RnDialogOpenerForTest { get; set; }

        private UserSession ResolveRepairNoticeSession()
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

        private void LoadRepairNoticeData(CatalogData data)
        {
            UserSession sess = ResolveRepairNoticeSession();
            _repairNoticeService = new RepairNoticeService(sess);
            _canViewRepairNotice = _repairNoticeService.CanView;
            _canAdd = _repairNoticeService.CanAdd;
            _canEdit = _repairNoticeService.CanEdit;
            _canRemove = _repairNoticeService.CanRemove;

            if (_def != null)
            {
                _def.CategoryColumn = null;
                _def.CategoryTable = null;
            }
            BuildTree(new CatalogData());

            ReloadRepairNoticeRowsOnly();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyRepairNoticeBarVisibility();
            UpdateRepairNoticeButtons();
            UpdateRepairNoticeAuditPanel();
        }

        private void ReloadRepairNoticeRowsOnly()
        {
            if (!_repairNoticeMode || _repairNoticeService == null)
                return;
            _dtRnFrom.Commit();
            _dtRnTo.Commit();
            var filter = new RepairNoticeFilter
            {
                FromDate = _dtRnFrom.NullableValue,
                ToDate = _dtRnTo.NullableValue,
                UnfixedOnly = _chkRnUnfixedOnly.Checked
            };
            _repairNoticeRows = _repairNoticeService.LoadList(filter);
            _total = _repairNoticeRows.Count;
            BuildRepairNoticeColumns();
            BuildRepairNoticeTable();
            if (!_loading)
                ApplyFilter();
            UpdateRepairNoticeButtons();
            UpdateRepairNoticeAuditPanel();
        }

        private void ApplyRepairNoticeBarVisibility()
        {
            if (!_repairNoticeMode)
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
            _rnFilterBar.Visible = true;
            _bottom.Visible = true;
            _paneSplitter.Visible = true;
            LayoutBar();
            LayoutRepairNoticeFilterBar();
        }

        /// <summary>9 cột theo spec §2: Số phiếu, Ngày, Phòng/Bàn, Loại phòng, Nhân viên, Nội dung, Đã sửa xong, Còn sử dụng được, Ghi chú.</summary>
        private void BuildRepairNoticeColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddRnColumn("rn_number", "Số phiếu", 125, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddRnColumn("rn_date", "Ngày", 105, typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddRnColumn("rn_room", "Phòng/Bàn", 130, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddRnColumn("rn_roomtype", "Loại phòng", 120, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddRnColumn("rn_emp", "Nhân viên", 150, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddRnColumn("rn_content", "Nội dung", 210, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddRnColumn("rn_fixed", "Đã sửa xong", 110, typeof(string), null, DataGridViewContentAlignment.MiddleCenter);
            AddRnColumn("rn_usable", "Còn sử dụng được", 145, typeof(string), null, DataGridViewContentAlignment.MiddleCenter);
            AddRnColumn("rn_note", "Ghi chú", 160, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddRnColumn(string name, string header, int width96, Type type, string format,
            DataGridViewContentAlignment align)
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
            if (type == typeof(DateTime))
            {
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
            }
            _grid.Columns.Add(gc);
        }

        private void BuildRepairNoticeTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("rn_number", typeof(string));
            dt.Columns.Add("rn_date", typeof(DateTime));
            dt.Columns.Add("rn_room", typeof(string));
            dt.Columns.Add("rn_roomtype", typeof(string));
            dt.Columns.Add("rn_emp", typeof(string));
            dt.Columns.Add("rn_content", typeof(string));
            dt.Columns.Add("rn_fixed", typeof(string));
            dt.Columns.Add("rn_usable", typeof(string));
            dt.Columns.Add("rn_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (RepairNoticeRow r in _repairNoticeRows)
            {
                sb.Length = 0;
                sb.Append(r.Name).Append(' ');
                sb.Append(r.RoomName).Append(' ');
                sb.Append(r.EmployeeName).Append(' ');
                sb.Append(r.Content).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Name ?? "",
                    r.Date.Date,
                    r.RoomName ?? "",
                    r.RoomTypeName ?? "",
                    r.EmployeeName ?? "",
                    r.Content ?? "",
                    RepairNoticeText.BoolText(r.IsFixed),
                    RepairNoticeText.BoolText(r.IsUsable),
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

        private string CurrentRepairNoticeId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private RepairNoticeRow CurrentRepairNoticeRow()
        {
            string id = CurrentRepairNoticeId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _repairNoticeRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectRepairNoticeRowByIdForTest(string id)
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
                    UpdateRepairNoticeButtons();
                    UpdateRepairNoticeAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateRepairNoticeButtons()
        {
            if (!_repairNoticeMode)
                return;
            bool hasRow = CurrentRepairNoticeId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void UpdateRepairNoticeAuditPanel()
        {
            if (!_repairNoticeMode)
                return;
            RepairNoticeRow row = CurrentRepairNoticeRow();
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

        // ------------------------------------------------------------------ CRUD Thông báo sửa chữa

        private void AddRepairNotice()
        {
            if (!_repairNoticeMode || !_canAdd)
                return;
            using (var dlg = new RepairNoticeEditDialog(ResolveRepairNoticeSession(), null, false))
            {
                OpenRepairNoticeDialog(dlg);
                if (dlg.AnySaved)
                    RefreshRepairNoticeAndSelect(dlg.SavedId);
            }
        }

        private void EditRepairNotice()
        {
            if (!_repairNoticeMode || !_canEdit)
                return;
            string id = CurrentRepairNoticeId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new RepairNoticeEditDialog(ResolveRepairNoticeSession(), id, false))
            {
                OpenRepairNoticeDialog(dlg);
                if (dlg.AnySaved)
                    RefreshRepairNoticeAndSelect(dlg.SavedId);
            }
        }

        private void OpenRepairNoticeFromGrid()
        {
            if (!_repairNoticeMode)
                return;
            if (_canEdit)
            {
                EditRepairNotice();
                return;
            }
            if (!_canViewRepairNotice)
                return;
            string id = CurrentRepairNoticeId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new RepairNoticeEditDialog(ResolveRepairNoticeSession(), id, true))
                OpenRepairNoticeDialog(dlg);
        }

        private void DeleteRepairNotice()
        {
            if (!_repairNoticeMode || !_canRemove)
                return;
            string id = CurrentRepairNoticeId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressRnPopupForTest && !Msg.Confirm(this, RepairNoticeText.DeleteConfirm))
                return;

            RepairNoticeDeleteResult res;
            try
            {
                res = _repairNoticeService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowRnWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowRnWarning(res.ErrorMessage);
                return;
            }
            LastRnMessageForTest = null;
            Reload();
        }

        private DialogResult OpenRepairNoticeDialog(RepairNoticeEditDialog dlg)
        {
            return RnDialogOpenerForTest != null
                ? RnDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void RefreshRepairNoticeAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectRepairNoticeRowByIdForTest(id);
        }

        private void ShowRnWarning(string message)
        {
            LastRnMessageForTest = message;
            if (!SuppressRnPopupForTest)
                Msg.Warning(this, message);
        }
    }
}
