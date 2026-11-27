using System.Drawing;
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
    /// Chế độ Danh mục đợt khuyến mại (DDOTKHUYENMAI, spec docs/specs/danh-muc-khuyen-mai.md P1 §3)
    /// trong <see cref="CatalogView"/>:
    /// lưới 6 cột (Tên đợt, Loại hình, Từ ngày, Đến ngày, Trạng thái, Ghi chú), lọc theo loại hình và trạng thái,
    /// tìm không dấu (F3), xuất Excel qua GridExcelExporter, quyền Xem/Thêm/Sửa/Xóa theo SFUNCTION,
    /// nhấp đôi mở chỉ đọc khi thiếu quyền Sửa. Phần menu, tô màu và bố cục thanh lọc ở <c>CatalogView.Promotion.Menus.cs</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private const string ColPromoTypeId = "_pm_type_id";
        private const string ColPromoStatus = "_pm_status_code";

        private bool _promotionMode;
        private bool _canViewPromotion;
        private PromotionService _promotionService;
        private List<PromotionRow> _promotionRows = new List<PromotionRow>();
        private List<PromotionTypeItem> _promotionTypes = new List<PromotionTypeItem>();
        private DateTime _promotionServerDate = DateTime.Today;
        private bool _updatingPromoFilters;

        private readonly Panel _promoFilterBar = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _lblPromoTypeFilter = new Label
        {
            Text = "Loại hình:",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        private readonly ComboBox _cboPromoTypeFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        private readonly Label _lblPromoStatusFilter = new Label
        {
            Text = "Trạng thái:",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        private readonly ComboBox _cboPromoStatusFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        private readonly ThemedContextMenu _promotionGridMenu = new ThemedContextMenu();

        // Hook kiểm thử UI (selftest).
        internal bool IsPromotionModeForTest { get { return _promotionMode; } }
        internal List<PromotionRow> PromotionRowsForTest { get { return _promotionRows; } }
        internal ComboBox PromoTypeFilterForTest { get { return _cboPromoTypeFilter; } }
        internal ComboBox PromoStatusFilterForTest { get { return _cboPromoStatusFilter; } }
        internal Panel PromoFilterBarForTest { get { return _promoFilterBar; } }
        internal bool SuppressPromotionPopupForTest { get; set; }
        internal string LastPromotionMessageForTest { get; private set; }
        internal Func<PromotionEditDialog, DialogResult> PromotionDialogOpenerForTest { get; set; }

        private void BuildPromotionUi()
        {
            float s = DeviceDpi / 96f;
            _promoFilterBar.Height = (int)(40 * s);
            _lblPromoTypeFilter.Font = _count.Font;
            _lblPromoStatusFilter.Font = _count.Font;
            _cboPromoTypeFilter.Font = _grid.Font;
            _cboPromoStatusFilter.Font = _grid.Font;

            _cboPromoStatusFilter.Items.Add(new Choice("", "Tất cả trạng thái"));
            _cboPromoStatusFilter.Items.Add(new Choice(((int)PromotionStatus.Active).ToString(CultureInfo.InvariantCulture), PromotionText.StatusActive));
            _cboPromoStatusFilter.Items.Add(new Choice(((int)PromotionStatus.Upcoming).ToString(CultureInfo.InvariantCulture), PromotionText.StatusUpcoming));
            _cboPromoStatusFilter.Items.Add(new Choice(((int)PromotionStatus.Expired).ToString(CultureInfo.InvariantCulture), PromotionText.StatusExpired));
            _cboPromoStatusFilter.Items.Add(new Choice(((int)PromotionStatus.Stopped).ToString(CultureInfo.InvariantCulture), PromotionText.StatusStopped));
            _cboPromoStatusFilter.SelectedIndex = 0;

            _cboPromoTypeFilter.SelectedIndexChanged += delegate
            {
                if (!_updatingPromoFilters)
                    ApplyFilter();
            };
            _cboPromoStatusFilter.SelectedIndexChanged += delegate
            {
                if (!_updatingPromoFilters)
                    ApplyFilter();
            };

            _promoFilterBar.Controls.Add(_lblPromoTypeFilter);
            _promoFilterBar.Controls.Add(_cboPromoTypeFilter);
            _promoFilterBar.Controls.Add(_lblPromoStatusFilter);
            _promoFilterBar.Controls.Add(_cboPromoStatusFilter);
            _promoFilterBar.Resize += delegate { LayoutPromotionFilterBar(); };
            _split.Panel2.Controls.Add(_promoFilterBar);

            _grid.DataBindingComplete += delegate
            {
                if (_promotionMode)
                    ApplyPromotionRowColors();
            };
            _grid.Sorted += delegate
            {
                if (_promotionMode)
                    ApplyPromotionRowColors();
            };

            BuildPromotionGridMenu();
        }

        private UserSession ResolvePromotionSession()
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
                Permissions = current == null ? new FunctionPermissions(false) : current.Permissions,
                CurrentStore = current == null ? null : current.CurrentStore
            };
        }

        private void LoadPromotionData(CatalogData data)
        {
            UserSession sess = ResolvePromotionSession();
            _promotionService = new PromotionService(sess);
            _canViewPromotion = _promotionService.CanView;
            _canAdd = _promotionService.CanAdd;
            _canEdit = _promotionService.CanEdit;
            _canRemove = _promotionService.CanRemove;

            _promotionServerDate = _promotionService.GetServerDate();
            _promotionTypes = _promotionService.LoadTypes();
            _promotionRows = _promotionService.LoadAll();
            _total = _promotionRows.Count;

            if (_def != null)
            {
                _def.CategoryColumn = null;
                _def.CategoryTable = null;
            }
            BuildTree(new CatalogData());
            PopulatePromotionTypeFilter();
            BuildPromotionColumns();
            BuildPromotionTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyPromotionBarVisibility();
            UpdatePromotionButtons();
            UpdatePromotionAuditPanel();
        }

        private void ApplyPromotionBarVisibility()
        {
            if (!_promotionMode)
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
            _promoFilterBar.Visible = true;
            _bottom.Visible = true;
            _paneSplitter.Visible = true;
            LayoutBar();
            LayoutPromotionFilterBar();
        }

        /// <summary>6 cột lưới theo spec §3: Tên đợt, Loại hình, Từ ngày, Đến ngày, Trạng thái, Ghi chú.</summary>
        private void BuildPromotionColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddPromotionColumn("pm_name", "Tên đợt", 230, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddPromotionColumn("pm_type", "Loại hình", 220, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            AddPromotionColumn("pm_from", "Từ ngày", 110, typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddPromotionColumn("pm_to", "Đến ngày", 110, typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter);
            AddPromotionColumn("pm_status", "Trạng thái", 135, typeof(string), null, DataGridViewContentAlignment.MiddleCenter);
            AddPromotionColumn("pm_note", "Ghi chú", 200, typeof(string), null, DataGridViewContentAlignment.MiddleLeft);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddPromotionColumn(string name, string header, int width96, Type type, string format, DataGridViewContentAlignment align)
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

        private void BuildPromotionTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("pm_name", typeof(string));
            dt.Columns.Add("pm_type", typeof(string));
            dt.Columns.Add("pm_from", typeof(DateTime));
            dt.Columns.Add("pm_to", typeof(DateTime));
            dt.Columns.Add("pm_status", typeof(string));
            dt.Columns.Add("pm_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));
            dt.Columns.Add(ColPromoTypeId, typeof(string));
            dt.Columns.Add(ColPromoStatus, typeof(int));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (PromotionRow r in _promotionRows)
            {
                PromotionStatus st = PromotionRules.ComputeStatus(r, _promotionServerDate);
                string statusText = PromotionRules.StatusText(st);
                sb.Length = 0;
                sb.Append(r.Name).Append(' ');
                sb.Append(r.TypeName).Append(' ');
                sb.Append(statusText).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Name ?? "",
                    r.TypeName ?? "",
                    r.FromDate.HasValue ? (object)r.FromDate.Value.Date : DBNull.Value,
                    r.ToDate.HasValue ? (object)r.ToDate.Value.Date : DBNull.Value,
                    statusText,
                    r.Note ?? "",
                    r.Id,
                    CatalogRepository.Fold(sb.ToString()),
                    "",
                    r.TypeId ?? "",
                    (int)st);
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
            ApplyPromotionRowColors();
        }

        private string CurrentPromotionId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private PromotionRow CurrentPromotionRow()
        {
            string id = CurrentPromotionId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _promotionRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectPromotionRowByIdForTest(string id)
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
                    UpdatePromotionButtons();
                    UpdatePromotionAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdatePromotionButtons()
        {
            if (!_promotionMode)
                return;
            bool hasRow = CurrentPromotionId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void UpdatePromotionAuditPanel()
        {
            if (!_promotionMode)
                return;
            PromotionRow row = CurrentPromotionRow();
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

        // ------------------------------------------------------------------ CRUD Đợt khuyến mại

        private void AddPromotion()
        {
            if (!_promotionMode || !_canAdd)
                return;
            using (var dlg = new PromotionEditDialog(ResolvePromotionSession(), null, false))
            {
                OpenPromotionDialog(dlg);
                if (dlg.AnySaved)
                    RefreshPromotionAndSelect(dlg.SavedId);
            }
        }

        private void EditPromotion()
        {
            if (!_promotionMode || !_canEdit)
                return;
            string id = CurrentPromotionId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new PromotionEditDialog(ResolvePromotionSession(), id, false))
            {
                OpenPromotionDialog(dlg);
                if (dlg.AnySaved)
                    RefreshPromotionAndSelect(dlg.SavedId);
            }
        }

        /// <summary>Nhấp đôi dòng đợt khuyến mại: có quyền Sửa thì mở sửa; thiếu quyền Sửa (chỉ Xem) thì mở chỉ đọc.</summary>
        private void OpenPromotionFromGrid()
        {
            if (!_promotionMode)
                return;
            if (_canEdit)
            {
                EditPromotion();
                return;
            }
            if (!_canViewPromotion)
                return;
            string id = CurrentPromotionId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new PromotionEditDialog(ResolvePromotionSession(), id, true))
                OpenPromotionDialog(dlg);
        }

        private DialogResult OpenPromotionDialog(PromotionEditDialog dlg)
        {
            return PromotionDialogOpenerForTest != null
                ? PromotionDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void DeletePromotion()
        {
            if (!_promotionMode || !_canRemove)
                return;
            string id = CurrentPromotionId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressPromotionPopupForTest && !Msg.Confirm(this, PromotionText.DeleteConfirm))
                return;

            PromotionDeleteResult res;
            try
            {
                res = _promotionService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowPromotionError("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowPromotionWarning(res.ErrorMessage);
                return;
            }
            LastPromotionMessageForTest = null;
            Reload();
        }

        private void RefreshPromotionAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectPromotionRowByIdForTest(id);
        }
    }
}
