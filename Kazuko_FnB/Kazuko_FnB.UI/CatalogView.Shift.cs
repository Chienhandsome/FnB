using System;
using System.Collections.Generic;
using System.Data;
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
    /// Chế độ Danh mục ca làm việc (DCALAMVIEC, spec docs/specs/nhan-su-danh-muc.md §3) trong <see cref="CatalogView"/>:
    /// danh mục phẳng 5 cột (Tên ca, Từ giờ, Đến giờ, Tỉ lệ lương (%), Ghi chú), ô tìm không dấu (F3),
    /// phân quyền theo tên chức năng "Danh mục ca làm việc", chỉ Xem mở hộp thoại chỉ đọc,
    /// xóa cứng chặn khi còn nhân viên hoặc dòng chấm công tham chiếu, xuất Excel qua <c>GridExcelExporter</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _shiftMode;
        private bool _canViewShift;
        private ShiftService _shiftService;
        private List<ShiftRow> _shiftRows = new List<ShiftRow>();
        private readonly ThemedContextMenu _shiftGridMenu = new ThemedContextMenu();

        // Hook kiểm thử UI (selftest).
        internal bool IsShiftModeForTest { get { return _shiftMode; } }
        internal List<ShiftRow> ShiftRowsForTest { get { return _shiftRows; } }
        internal bool SuppressShiftPopupForTest { get; set; }
        internal string LastShiftMessageForTest { get; private set; }
        internal Func<ShiftEditDialog, DialogResult> ShiftDialogOpenerForTest { get; set; }

        private void BuildShiftUi()
        {
            _shiftGridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddShift(); });
            _shiftGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditShift(); });
            _shiftGridMenu.AddItem("Xem chi tiết", delegate { OpenShiftFromGrid(); });
            _shiftGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteShift(); });
            _shiftGridMenu.AddSeparator();
            _shiftGridMenu.AddItem("Refresh", delegate { Reload(); });
            _shiftGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _shiftGridMenu.AddSeparator();
            _shiftGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _shiftGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _shiftGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _shiftGridMenu.Opening += delegate { UpdateShiftGridMenuState(); };
        }

        private UserSession ResolveShiftSession()
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

        private void LoadShiftData(CatalogData data)
        {
            UserSession sess = ResolveShiftSession();
            _shiftService = new ShiftService(sess);
            _canViewShift = _shiftService.CanView;
            _canAdd = _shiftService.CanAdd;
            _canEdit = _shiftService.CanEdit;
            _canRemove = _shiftService.CanRemove;

            _shiftRows = _shiftService.LoadAll();
            _total = _shiftRows.Count;
            BuildTree(data);
            BuildShiftColumns();
            BuildShiftTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyShiftBarVisibility();
            UpdateShiftButtons();
            UpdateShiftAuditPanel();
        }

        private void ApplyShiftBarVisibility()
        {
            if (!_shiftMode)
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
            _bottom.Visible = true;
            _paneSplitter.Visible = true;
            LayoutBar();
        }

        /// <summary>5 cột lưới theo spec §3: Tên ca, Từ giờ, Đến giờ, Tỉ lệ lương (%), Ghi chú.</summary>
        private void BuildShiftColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddShiftColumn("sh_name", "Tên ca", 220, typeof(string), null, false);
            AddShiftColumn("sh_from", "Từ giờ", 100, typeof(string), null, true);
            AddShiftColumn("sh_to", "Đến giờ", 100, typeof(string), null, true);
            AddShiftColumn("sh_rate", "Tỉ lệ lương (%)", 140, typeof(decimal), "#,##0.##", false);
            AddShiftColumn("sh_note", "Ghi chú", 220, typeof(string), null, false);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddShiftColumn(string name, string header, int width96, Type type, string format, bool centerText)
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
            if (type == typeof(decimal))
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = new CultureInfo("vi-VN");
            }
            else if (centerText)
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            _grid.Columns.Add(gc);
        }

        private void BuildShiftTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("sh_name", typeof(string));
            dt.Columns.Add("sh_from", typeof(string));
            dt.Columns.Add("sh_to", typeof(string));
            dt.Columns.Add("sh_rate", typeof(decimal));
            dt.Columns.Add("sh_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (ShiftRow r in _shiftRows)
            {
                string fromText = ShiftRules.FormatShiftTime(r.FromTime);
                string toText = ShiftRules.FormatShiftTime(r.ToTime);
                sb.Length = 0;
                sb.Append(r.Name).Append(' ');
                sb.Append(fromText).Append(' ');
                sb.Append(toText).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Name ?? "",
                    fromText,
                    toText,
                    r.SalaryRate,
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

        private string CurrentShiftId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private ShiftRow CurrentShiftRow()
        {
            string id = CurrentShiftId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _shiftRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectShiftRowByIdForTest(string id)
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
                    UpdateShiftButtons();
                    UpdateShiftAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateShiftButtons()
        {
            if (!_shiftMode)
                return;
            bool hasRow = CurrentShiftId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void UpdateShiftAuditPanel()
        {
            if (!_shiftMode)
                return;
            ShiftRow row = CurrentShiftRow();
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

        private void UpdateShiftGridMenuState()
        {
            bool hasRow = CurrentShiftId() != null;
            foreach (ToolStripItem item in _shiftGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm mới":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewShift;
                        mi.Enabled = hasRow;
                        break;
                    case "Xóa":
                        mi.Visible = _canRemove;
                        mi.Enabled = _canRemove && hasRow;
                        break;
                    case "Sao chép ô":
                    case "Sao chép vùng chọn":
                        mi.Enabled = hasRow;
                        break;
                }
            }
        }

        // ------------------------------------------------------------------ CRUD Ca làm việc

        private void AddShift()
        {
            if (!_shiftMode || !_canAdd)
                return;
            using (var dlg = new ShiftEditDialog(ResolveShiftSession(), null, false))
            {
                OpenShiftDialog(dlg);
                if (dlg.AnySaved)
                    RefreshShiftAndSelect(dlg.SavedId);
            }
        }

        private void EditShift()
        {
            if (!_shiftMode || !_canEdit)
                return;
            string id = CurrentShiftId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new ShiftEditDialog(ResolveShiftSession(), id, false))
            {
                OpenShiftDialog(dlg);
                if (dlg.AnySaved)
                    RefreshShiftAndSelect(dlg.SavedId);
            }
        }

        /// <summary>Nhấp đôi dòng ca: có quyền Sửa thì mở sửa; chỉ có quyền Xem thì mở chỉ đọc.</summary>
        private void OpenShiftFromGrid()
        {
            if (!_shiftMode)
                return;
            if (_canEdit)
            {
                EditShift();
                return;
            }
            if (!_canViewShift)
                return;
            string id = CurrentShiftId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new ShiftEditDialog(ResolveShiftSession(), id, true))
                OpenShiftDialog(dlg);
        }

        private void DeleteShift()
        {
            if (!_shiftMode || !_canRemove)
                return;
            string id = CurrentShiftId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressShiftPopupForTest && !Msg.Confirm(this, ShiftText.DeleteConfirm))
                return;

            ShiftDeleteResult res;
            try
            {
                res = _shiftService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowShiftWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowShiftWarning(res.ErrorMessage);
                return;
            }
            LastShiftMessageForTest = null;
            Reload();
        }

        private DialogResult OpenShiftDialog(ShiftEditDialog dlg)
        {
            return ShiftDialogOpenerForTest != null
                ? ShiftDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void RefreshShiftAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectShiftRowByIdForTest(id);
        }

        private void ShowShiftWarning(string message)
        {
            LastShiftMessageForTest = message;
            if (!SuppressShiftPopupForTest)
                Msg.Warning(this, message);
        }
    }
}
