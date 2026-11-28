using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thùng rác của Danh mục nhân viên (DNHANVIEN STATUS = 0, spec docs/specs/nhan-su-cay.md §3–§4):
    /// liệt kê nhân viên, thư mục, phân cách đã xóa mềm; hỗ trợ Khôi phục (quyền Sửa) và Xóa vĩnh viễn (quyền Xóa).
    /// </summary>
    internal sealed class EmployeeTrashDialog : ThemedDialog
    {
        private const string ColId = "_id";
        private const int BodyWidth96 = 780;
        private const int BodyHeight96 = 380;

        private readonly EmployeeService _service;
        private List<EmployeeRow> _rows = new List<EmployeeRow>();

        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _lblEmpty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        private readonly Label _lblStatus = new Label { AutoSize = false, Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleLeft };

        private readonly FlatButton _btnClose;
        private readonly FlatButton _btnDeletePermanent;
        private readonly FlatButton _btnRestore;
        private readonly FlatButton _btnRefresh;

        public bool AnyChanged { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastMessageForTest { get; private set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal FlatButton RestoreButtonForTest { get { return _btnRestore; } }
        internal FlatButton DeletePermanentButtonForTest { get { return _btnDeletePermanent; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal List<EmployeeRow> RowsForTest { get { return _rows; } }

        public EmployeeTrashDialog(UserSession session)
            : base("THÙNG RÁC - DANH MỤC NHÂN VIÊN", "Chọn mục để khôi phục hoặc xóa vĩnh viễn khỏi hệ thống", BodyWidth96, 500)
        {
            _service = new EmployeeService(session ?? AppSession.Current);
            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Đóng", null, delegate { Close(); });
            _btnDeletePermanent = AddButton("Xóa vĩnh viễn", "danger", delegate { DeleteSelectedPermanent(); });
            _btnRestore = AddButton("Khôi phục", "primary", delegate { RestoreSelected(); });
            _btnRefresh = AddButton("Làm mới", null, delegate { ReloadTrash(); });
            CancelButton = _btnClose;

            _btnRestore.Visible = _service.CanEdit;
            _btnDeletePermanent.Visible = _service.CanRemove;

            BuildGrid();
            ReloadTrash();
            ApplyDialogTheme();
        }

        private void BuildGrid()
        {
            float s = Dpi;
            _lblStatus.Height = (int)(26 * s);
            _lblStatus.Font = new Font("Segoe UI", 9.5f);
            _lblStatus.Padding = new Padding((int)(6 * s), 0, (int)(6 * s), 0);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.RowTemplate.Height = (int)(28 * s);
            _grid.Font = new Font("Segoe UI", 10f);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_grid, true, null);

            AddColumn("tr_kind", "Loại", 95);
            AddColumn("tr_code", "Mã", 95);
            AddColumn("tr_name", "Tên", 200);
            AddColumn("tr_parent", "Thư mục cũ", 150);
            AddColumn("tr_phone", "Điện thoại", 110);
            AddColumn("tr_note", "Ghi chú", 120);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _lblEmpty.Font = new Font("Segoe UI", 10.5f);
            _lblEmpty.Text = "Thùng rác trống";
            _grid.Controls.Add(_lblEmpty);
            _grid.Resize += delegate { PositionEmpty(); };
            _grid.SelectionChanged += delegate { UpdateActionButtons(); };

            Body.Padding = new Padding((int)(10 * s), (int)(10 * s), (int)(10 * s), (int)(4 * s));
            Body.Controls.Add(_grid);
            Body.Controls.Add(_lblStatus);
        }

        private void AddColumn(string name, string header, int width96)
        {
            float s = Dpi;
            int headerNeed = TextRenderer.MeasureText(header, _grid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(26 * s);
            int minW = Math.Max((int)(60 * s), headerNeed);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                DataPropertyName = name,
                HeaderText = header,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Automatic,
                MinimumWidth = minW,
                Width = Math.Max((int)(width96 * s), minW)
            });
        }

        public void ReloadTrash()
        {
            _rows = _service.LoadTrash();

            var dt = new DataTable();
            dt.Columns.Add("tr_kind", typeof(string));
            dt.Columns.Add("tr_code", typeof(string));
            dt.Columns.Add("tr_name", typeof(string));
            dt.Columns.Add("tr_parent", typeof(string));
            dt.Columns.Add("tr_phone", typeof(string));
            dt.Columns.Add("tr_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));

            dt.BeginLoadData();
            foreach (EmployeeRow r in _rows)
            {
                string parentLabel = string.IsNullOrEmpty(r.ParentId)
                    ? "(Gốc)"
                    : (string.IsNullOrEmpty(r.ParentName) ? "(Thư mục đã xóa)" : r.ParentName);
                dt.Rows.Add(
                    EmployeeText.ItemTypeDisplayText(r.ItemType),
                    r.Code ?? "",
                    r.Name ?? "",
                    parentLabel,
                    r.Phone ?? "",
                    r.Note ?? "",
                    r.Id);
            }
            dt.EndLoadData();

            _grid.DataSource = dt;
            if (_grid.Rows.Count > 0)
            {
                _grid.ClearSelection();
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
                _grid.Rows[0].Selected = true;
            }
            _lblEmpty.Visible = _rows.Count == 0;
            PositionEmpty();
            _lblStatus.Text = _rows.Count + " mục trong thùng rác";
            UpdateActionButtons();
        }

        internal bool SelectRowByIdForTest(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var drv = row.DataBoundItem as DataRowView;
                if (drv != null && string.Equals(Convert.ToString(drv[ColId]), id, StringComparison.Ordinal))
                {
                    _grid.ClearSelection();
                    _grid.CurrentCell = row.Cells[0];
                    row.Selected = true;
                    UpdateActionButtons();
                    return true;
                }
            }
            return false;
        }

        private EmployeeRow SelectedRow()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null)
                return null;
            string id = drv[ColId] as string;
            if (string.IsNullOrEmpty(id))
                return null;
            return _rows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        private void UpdateActionButtons()
        {
            bool hasRow = SelectedRow() != null;
            _btnRestore.Enabled = _service.CanEdit && hasRow;
            _btnDeletePermanent.Enabled = _service.CanRemove && hasRow;
        }

        private void RestoreSelected()
        {
            if (!_service.CanEdit)
                return;
            EmployeeRow row = SelectedRow();
            if (row == null)
                return;

            EmployeeRestoreResult res;
            try
            {
                res = _service.Restore(row.Id);
            }
            catch (DataAccessException ex)
            {
                ShowMessage("Không khôi phục được dữ liệu: " + ex.Message, true);
                return;
            }

            if (!res.Ok)
            {
                ShowMessage(res.ErrorMessage, true);
                return;
            }

            AnyChanged = true;
            LastMessageForTest = res.RestoredToRoot
                ? "Đã khôi phục lên thư mục gốc do thư mục cũ không còn hoạt động."
                : null;
            ReloadTrash();
            if (res.RestoredToRoot)
                _lblStatus.Text = LastMessageForTest;
        }

        private void DeleteSelectedPermanent()
        {
            if (!_service.CanRemove)
                return;
            EmployeeRow row = SelectedRow();
            if (row == null)
                return;

            string label = row.IsSeparator ? "dòng phân cách" : (row.Name ?? "");
            if (!SuppressPopup && !Msg.Confirm(this, EmployeeText.HardDeleteConfirm(label)))
                return;

            EmployeeDeleteResult res;
            try
            {
                res = _service.Delete(row.Id);
            }
            catch (DataAccessException ex)
            {
                ShowMessage("Không xóa được dữ liệu: " + ex.Message, true);
                return;
            }

            if (!res.Ok)
            {
                ShowMessage(res.ErrorMessage, true);
                return;
            }

            AnyChanged = true;
            LastMessageForTest = null;
            ReloadTrash();
        }

        private void ShowMessage(string message, bool isWarning)
        {
            LastMessageForTest = message;
            if (!SuppressPopup)
            {
                if (isWarning) Msg.Warning(this, message);
                else Msg.Info(this, message);
            }
        }

        private void PositionEmpty()
        {
            float s = Dpi;
            int h = (int)(48 * s);
            int free = _grid.ClientSize.Height - _grid.ColumnHeadersHeight - h;
            int top = _grid.ColumnHeadersHeight + Math.Max((int)(16 * s), free / 2);
            _lblEmpty.SetBounds(0, top, _grid.ClientSize.Width, h);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            ThemeApplier.ApplyGrid(_grid, t);
            _lblEmpty.BackColor = _grid.BackgroundColor;
            _lblEmpty.ForeColor = t.TextMuted;
            _lblStatus.BackColor = Color.Transparent;
            _lblStatus.ForeColor = t.TextMuted;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _grid.Font.Dispose();
                _grid.ColumnHeadersDefaultCellStyle.Font.Dispose();
                _lblEmpty.Font.Dispose();
                _lblStatus.Font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
