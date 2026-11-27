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
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Chế độ Danh mục voucher (DVOUCHER / DNHOMVOUCHER, spec docs/specs/danh-muc-voucher.md §2) trong <see cref="CatalogView"/>:
    /// cây nhóm voucher bên trái, lưới 8 cột bên phải (Mã voucher, Giá trị, Ngày phát hành, Hạn sử dụng,
    /// Nhóm voucher, Đối tác phân phối, Đã dùng, Ghi chú), hàng tổng cộng cột Giá trị, lọc nhanh
    /// "Chỉ voucher còn dùng được", tìm không dấu (F3), nút CRUD theo quyền SFUNCTION,
    /// nhấp đôi mở hộp thoại chỉ đọc khi thiếu quyền Sửa, xuất Excel qua <c>GridExcelExporter</c>.
    /// Phần CRUD nhóm ở <c>CatalogView.GiftVoucher.Groups.cs</c>, menu ngữ cảnh ở <c>CatalogView.GiftVoucher.Menus.cs</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private const string ColUsable = "_usable";

        private bool _giftVoucherMode;
        private bool _canViewGiftVoucher;
        private GiftVoucherService _giftVoucherService;
        private GiftVoucherGroupService _giftVoucherGroupService;
        private List<GiftVoucherRow> _giftVoucherRows = new List<GiftVoucherRow>();
        private DateTime _giftVoucherServerDate = DateTime.Today;
        private readonly ThemedContextMenu _giftVoucherGridMenu = new ThemedContextMenu();
        private readonly ThemedContextMenu _giftVoucherTreeMenu = new ThemedContextMenu();
        private ToolStripMenuItem _giftVoucherColSubmenu;

        private readonly CheckBox _chkVoucherUsableOnly = new CheckBox
        {
            Text = "Chỉ voucher còn dùng được",
            AutoSize = true,
            Visible = false,
            UseMnemonic = false
        };
        private readonly Panel _voucherTotalBar = new Panel { Dock = DockStyle.Bottom, Visible = false };
        private readonly Label _lblVoucherTotalCaption = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        private readonly Label _lblVoucherTotalValue = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            UseMnemonic = false
        };
        private Font _voucherTotalFont;
        private decimal _voucherTotalValue;

        // Hook kiểm thử UI (selftest).
        internal bool IsGiftVoucherModeForTest { get { return _giftVoucherMode; } }
        internal List<GiftVoucherRow> GiftVoucherRowsForTest { get { return _giftVoucherRows; } }
        internal CheckBox VoucherUsableOnlyCheckForTest { get { return _chkVoucherUsableOnly; } }
        internal Panel VoucherTotalBarForTest { get { return _voucherTotalBar; } }
        internal Label VoucherTotalValueLabelForTest { get { return _lblVoucherTotalValue; } }
        internal decimal VoucherTotalValueForTest { get { return _voucherTotalValue; } }
        internal bool SuppressGiftVoucherPopupForTest { get; set; }
        internal string LastGiftVoucherMessageForTest { get; private set; }
        internal Func<GiftVoucherEditDialog, DialogResult> GiftVoucherDialogOpenerForTest { get; set; }
        internal Func<GiftVoucherGroupEditDialog, DialogResult> GiftVoucherGroupDialogOpenerForTest { get; set; }
        internal ThemedContextMenu GiftVoucherTreeMenuForTest { get { return _giftVoucherTreeMenu; } }

        private void BuildGiftVoucherUi()
        {
            float s = DeviceDpi / 96f;
            _chkVoucherUsableOnly.Font = _count.Font;
            _chkVoucherUsableOnly.CheckedChanged += delegate { ApplyFilter(); };
            _bar.Controls.Add(_chkVoucherUsableOnly);

            _voucherTotalFont = new Font("Segoe UI Semibold", 10f);
            _voucherTotalBar.Height = (int)(32 * s);
            _lblVoucherTotalCaption.Font = _voucherTotalFont;
            _lblVoucherTotalValue.Font = _voucherTotalFont;
            _voucherTotalBar.Controls.Add(_lblVoucherTotalCaption);
            _voucherTotalBar.Controls.Add(_lblVoucherTotalValue);
            _voucherTotalBar.Resize += delegate { LayoutVoucherTotalBar(); };

            // Đặt thanh tổng cộng ngay dưới lưới và phía trên _paneSplitter + _bottom.
            _split.Panel2.Controls.Add(_voucherTotalBar);
            if (_split.Panel2.Controls.Count > 1)
                _split.Panel2.Controls.SetChildIndex(_voucherTotalBar, 1);

            BuildGiftVoucherGridMenu();
            BuildGiftVoucherTreeMenu();
        }

        private UserSession ResolveGiftVoucherSession()
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

        private void LoadGiftVoucherData(CatalogData data)
        {
            UserSession sess = ResolveGiftVoucherSession();
            _giftVoucherService = new GiftVoucherService(sess);
            _giftVoucherGroupService = new GiftVoucherGroupService(sess);
            _canViewGiftVoucher = _giftVoucherService.CanView;
            _canAdd = _giftVoucherService.CanAdd;
            _canEdit = _giftVoucherService.CanEdit;
            _canRemove = _giftVoucherService.CanRemove;

            _giftVoucherServerDate = _giftVoucherService.GetServerDate();
            _giftVoucherRows = _giftVoucherService.LoadAll();
            _total = _giftVoucherRows.Count;

            if (_def != null)
            {
                _def.CategoryColumn = "DNHOMVOUCHERID";
                _def.CategoryTable = GiftVoucherText.GroupTableName;
            }
            var treeData = new CatalogData();
            foreach (GiftVoucherGroupRow g in _giftVoucherGroupService.LoadAll())
            {
                treeData.Nodes.Add(new CatalogNode
                {
                    Id = g.Id,
                    Name = g.Name ?? "",
                    ParentId = string.IsNullOrEmpty(g.ParentId) ? null : g.ParentId,
                    SortOrder = g.SortOrder ?? ""
                });
            }
            BuildTree(treeData);
            BuildGiftVoucherColumns();
            BuildGiftVoucherTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyGiftVoucherBarVisibility();
            UpdateGiftVoucherButtons();
            UpdateGiftVoucherAuditPanel();
        }

        private void ApplyGiftVoucherBarVisibility()
        {
            if (!_giftVoucherMode)
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
            _chkVoucherUsableOnly.Visible = true;
            _voucherTotalBar.Visible = true;

            float s = DeviceDpi / 96f;
            int gx = (int)(6 * s);
            _groupBar.Visible = _canAdd || _canEdit;
            _groupAdd.Visible = _canAdd;
            if (_canAdd)
            {
                _groupAdd.SetBounds(gx, (int)(4 * s), (int)(28 * s), (int)(28 * s));
                gx += (int)(34 * s);
            }
            _groupEdit.Visible = _canEdit;
            if (_canEdit)
                _groupEdit.SetBounds(gx, (int)(4 * s), (int)(28 * s), (int)(28 * s));

            _bottom.Visible = true;
            _paneSplitter.Visible = true;
            LayoutBar();
            LayoutVoucherTotalBar();
        }

        /// <summary>8 cột lưới theo spec §2: Mã voucher, Giá trị, Ngày phát hành, Hạn sử dụng, Nhóm voucher, Đối tác phân phối, Đã dùng, Ghi chú.</summary>
        private void BuildGiftVoucherColumns()
        {
            float s = DeviceDpi / 96f;
            var vi = new CultureInfo("vi-VN");
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddGiftVoucherColumn("gv_code", "Mã voucher", 140, typeof(string), null, vi);
            AddGiftVoucherColumn("gv_value", "Giá trị", 115, typeof(decimal), "#,##0.##", vi);
            AddGiftVoucherColumn("gv_issue", "Ngày phát hành", 125, typeof(DateTime), "dd/MM/yyyy", vi);
            AddGiftVoucherColumn("gv_expiry", "Hạn sử dụng", 115, typeof(DateTime), "dd/MM/yyyy", vi);
            AddGiftVoucherColumn("gv_group", "Nhóm voucher", 150, typeof(string), null, vi);
            AddGiftVoucherColumn("gv_partner", "Đối tác phân phối", 165, typeof(string), null, vi);
            AddGiftVoucherColumn("gv_used", "Đã dùng", 150, typeof(string), null, vi);
            AddGiftVoucherColumn("gv_note", "Ghi chú", 140, typeof(string), null, vi);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddGiftVoucherColumn(string name, string header, int width96, Type type, string format, CultureInfo vi)
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
                gc.DefaultCellStyle.FormatProvider = vi;
            }
            else if (type == typeof(DateTime))
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
            }
            _grid.Columns.Add(gc);
        }

        private void BuildGiftVoucherTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("gv_code", typeof(string));
            dt.Columns.Add("gv_value", typeof(decimal));
            dt.Columns.Add("gv_issue", typeof(DateTime));
            dt.Columns.Add("gv_expiry", typeof(DateTime));
            dt.Columns.Add("gv_group", typeof(string));
            dt.Columns.Add("gv_partner", typeof(string));
            dt.Columns.Add("gv_used", typeof(string));
            dt.Columns.Add("gv_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));
            dt.Columns.Add(ColUsable, typeof(bool));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (GiftVoucherRow r in _giftVoucherRows)
            {
                string usedText = GiftVoucherText.FormatUsedStatus(r.PaidBillName);
                sb.Length = 0;
                sb.Append(r.Code).Append(' ');
                sb.Append(r.GroupName).Append(' ');
                sb.Append(r.PartnerName).Append(' ');
                sb.Append(usedText).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Code ?? "",
                    r.Value,
                    r.IssueDate.HasValue ? (object)r.IssueDate.Value.Date : DBNull.Value,
                    r.ExpiryDate.HasValue ? (object)r.ExpiryDate.Value.Date : DBNull.Value,
                    r.GroupName ?? "",
                    r.PartnerName ?? "",
                    usedText,
                    r.Note ?? "",
                    r.Id,
                    CatalogRepository.Fold(sb.ToString()),
                    CatalogRepository.CategoryPath(_nodes, r.GroupId),
                    GiftVoucherRules.IsUsableOn(r, _giftVoucherServerDate));
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private string CurrentGiftVoucherId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private GiftVoucherRow CurrentGiftVoucherRow()
        {
            string id = CurrentGiftVoucherId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _giftVoucherRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectGiftVoucherRowByIdForTest(string id)
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
                    UpdateGiftVoucherButtons();
                    UpdateGiftVoucherAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateGiftVoucherButtons()
        {
            if (!_giftVoucherMode)
                return;
            bool hasRow = CurrentGiftVoucherId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;

            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            _groupAdd.Enabled = _canAdd;
            _groupEdit.Enabled = _canEdit && realGroup;
            _tree.LabelEdit = _canEdit;
        }

        private void UpdateGiftVoucherAuditPanel()
        {
            if (!_giftVoucherMode)
                return;
            GiftVoucherRow row = CurrentGiftVoucherRow();
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

        // ------------------------------------------------------------------ CRUD Voucher

        private void AddGiftVoucher()
        {
            if (!_giftVoucherMode || !_canAdd)
                return;
            string groupId = SelectedGroupId();
            if (groupId == UnsetTag)
                groupId = null;
            using (var dlg = new GiftVoucherEditDialog(ResolveGiftVoucherSession(), null, groupId, false))
            {
                OpenGiftVoucherDialog(dlg);
                if (dlg.AnySaved)
                    RefreshGiftVoucherAndSelect(dlg.SavedId);
            }
        }

        private void EditGiftVoucher()
        {
            if (!_giftVoucherMode || !_canEdit)
                return;
            string id = CurrentGiftVoucherId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new GiftVoucherEditDialog(ResolveGiftVoucherSession(), id, null, false))
            {
                OpenGiftVoucherDialog(dlg);
                if (dlg.AnySaved)
                    RefreshGiftVoucherAndSelect(dlg.SavedId);
            }
        }

        /// <summary>Nhấp đôi dòng voucher: có quyền Sửa thì mở sửa; thiếu quyền Sửa (chỉ Xem) thì mở chỉ đọc.</summary>
        private void OpenGiftVoucherFromGrid()
        {
            if (!_giftVoucherMode)
                return;
            if (_canEdit)
            {
                EditGiftVoucher();
                return;
            }
            if (!_canViewGiftVoucher)
                return;
            string id = CurrentGiftVoucherId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new GiftVoucherEditDialog(ResolveGiftVoucherSession(), id, null, true))
                OpenGiftVoucherDialog(dlg);
        }

        private DialogResult OpenGiftVoucherDialog(GiftVoucherEditDialog dlg)
        {
            return GiftVoucherDialogOpenerForTest != null
                ? GiftVoucherDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void DeleteGiftVoucher()
        {
            if (!_giftVoucherMode || !_canRemove)
                return;
            string id = CurrentGiftVoucherId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressGiftVoucherPopupForTest && !Msg.Confirm(this, GiftVoucherText.DeleteConfirm))
                return;

            GiftVoucherDeleteResult res;
            try
            {
                res = _giftVoucherService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowGiftVoucherError("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowGiftVoucherWarning(res.ErrorMessage);
                return;
            }
            LastGiftVoucherMessageForTest = null;
            Reload();
        }

        private void RefreshGiftVoucherAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectGiftVoucherRowByIdForTest(id);
        }
    }
}
