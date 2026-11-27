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
    /// Chế độ Danh mục thẻ trả trước (DTHETRATRUOC / DNHOMTHETRATRUOC, spec docs/specs/danh-muc-the-tra-truoc.md §2)
    /// trong <see cref="CatalogView"/>: cây nhóm thẻ bên trái, lưới 9 cột bên phải, hàng tổng cộng 3 cột tiền,
    /// lọc "Chỉ thẻ còn tiền" &amp; "Chỉ thẻ chưa khóa", tìm không dấu (F3), nút CRUD theo quyền SFUNCTION,
    /// nhấp đôi mở hộp thẻ chỉ đọc khi thiếu quyền Sửa, xuất Excel qua <c>GridExcelExporter</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private const string ColPrepaidHasBalance = "_pc_hasbal";
        private const string ColPrepaidUnlocked = "_pc_unlocked";

        private bool _prepaidCardMode;
        private bool _canViewPrepaidCard;
        private PrepaidCardService _prepaidCardService;
        private PrepaidCardGroupService _prepaidCardGroupService;
        private List<PrepaidCardRow> _prepaidCardRows = new List<PrepaidCardRow>();
        private readonly ThemedContextMenu _prepaidGridMenu = new ThemedContextMenu();
        private readonly ThemedContextMenu _prepaidTreeMenu = new ThemedContextMenu();
        private ToolStripMenuItem _prepaidColSubmenu;

        private readonly Panel _prepaidFilterBar = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly CheckBox _chkPrepaidHasBalance = new CheckBox
        {
            Text = "Chỉ thẻ còn tiền",
            AutoSize = true,
            UseMnemonic = false
        };
        private readonly CheckBox _chkPrepaidUnlockedOnly = new CheckBox
        {
            Text = "Chỉ thẻ chưa khóa",
            AutoSize = true,
            UseMnemonic = false
        };

        private readonly Panel _prepaidTotalBar = new Panel { Dock = DockStyle.Bottom, Visible = false };
        private readonly Label _lblPrepaidTotalCaption = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        private readonly Label _lblPrepaidTotalValues = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            UseMnemonic = false
        };
        private Font _prepaidTotalFont;
        private decimal _prepaidTotalLoaded;
        private decimal _prepaidTotalUsed;
        private decimal _prepaidTotalRemaining;

        // Hook kiểm thử UI (selftest).
        internal bool IsPrepaidCardModeForTest { get { return _prepaidCardMode; } }
        internal List<PrepaidCardRow> PrepaidCardRowsForTest { get { return _prepaidCardRows; } }
        internal CheckBox PrepaidHasBalanceCheckForTest { get { return _chkPrepaidHasBalance; } }
        internal CheckBox PrepaidUnlockedOnlyCheckForTest { get { return _chkPrepaidUnlockedOnly; } }
        internal Panel PrepaidTotalBarForTest { get { return _prepaidTotalBar; } }
        internal Label PrepaidTotalValuesLabelForTest { get { return _lblPrepaidTotalValues; } }
        internal decimal PrepaidTotalLoadedForTest { get { return _prepaidTotalLoaded; } }
        internal decimal PrepaidTotalUsedForTest { get { return _prepaidTotalUsed; } }
        internal decimal PrepaidTotalRemainingForTest { get { return _prepaidTotalRemaining; } }
        internal bool SuppressPrepaidCardPopupForTest { get; set; }
        internal string LastPrepaidCardMessageForTest { get; private set; }
        internal Func<PrepaidCardEditDialog, DialogResult> PrepaidCardDialogOpenerForTest { get; set; }
        internal Func<PrepaidCardGroupEditDialog, DialogResult> PrepaidCardGroupDialogOpenerForTest { get; set; }
        internal ThemedContextMenu PrepaidCardTreeMenuForTest { get { return _prepaidTreeMenu; } }

        private void BuildPrepaidCardUi()
        {
            float s = DeviceDpi / 96f;
            _prepaidFilterBar.Height = (int)(36 * s);
            _chkPrepaidHasBalance.Font = _count.Font;
            _chkPrepaidUnlockedOnly.Font = _count.Font;
            _chkPrepaidHasBalance.CheckedChanged += delegate { ApplyFilter(); };
            _chkPrepaidUnlockedOnly.CheckedChanged += delegate { ApplyFilter(); };
            _prepaidFilterBar.Controls.Add(_chkPrepaidHasBalance);
            _prepaidFilterBar.Controls.Add(_chkPrepaidUnlockedOnly);
            _prepaidFilterBar.Resize += delegate { LayoutPrepaidFilterBar(); };
            _split.Panel2.Controls.Add(_prepaidFilterBar);

            _prepaidTotalFont = new Font("Segoe UI Semibold", 10f);
            _prepaidTotalBar.Height = (int)(32 * s);
            _lblPrepaidTotalCaption.Font = _prepaidTotalFont;
            _lblPrepaidTotalValues.Font = _prepaidTotalFont;
            _prepaidTotalBar.Controls.Add(_lblPrepaidTotalCaption);
            _prepaidTotalBar.Controls.Add(_lblPrepaidTotalValues);
            _prepaidTotalBar.Resize += delegate { LayoutPrepaidTotalBar(); };
            _split.Panel2.Controls.Add(_prepaidTotalBar);
            if (_split.Panel2.Controls.Count > 1)
                _split.Panel2.Controls.SetChildIndex(_prepaidTotalBar, 1);

            BuildPrepaidCardGridMenu();
            BuildPrepaidCardTreeMenu();
        }

        private UserSession ResolvePrepaidCardSession()
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

        private void LoadPrepaidCardData(CatalogData data)
        {
            UserSession sess = ResolvePrepaidCardSession();
            _prepaidCardService = new PrepaidCardService(sess);
            _prepaidCardGroupService = new PrepaidCardGroupService(sess);
            _canViewPrepaidCard = _prepaidCardService.CanView;
            _canAdd = _prepaidCardService.CanAdd;
            _canEdit = _prepaidCardService.CanEdit;
            _canRemove = _prepaidCardService.CanRemove;

            _prepaidCardRows = _prepaidCardService.LoadAll();
            _total = _prepaidCardRows.Count;

            if (_def != null)
            {
                _def.CategoryColumn = "DNHOMTHETRATRUOCID";
                _def.CategoryTable = PrepaidCardText.GroupTableName;
            }
            var treeData = new CatalogData();
            foreach (PrepaidCardGroupRow g in _prepaidCardGroupService.LoadAll())
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
            BuildPrepaidCardColumns();
            BuildPrepaidCardTable();

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyPrepaidCardBarVisibility();
            UpdatePrepaidCardButtons();
            UpdatePrepaidCardAuditPanel();
        }

        private void ApplyPrepaidCardBarVisibility()
        {
            if (!_prepaidCardMode)
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
            _prepaidFilterBar.Visible = true;
            _prepaidTotalBar.Visible = true;

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
            LayoutPrepaidFilterBar();
            LayoutPrepaidTotalBar();
        }

        /// <summary>9 cột lưới theo spec §2: Mã thẻ, Nhóm thẻ, Khách đang giữ thẻ, Ngày hết hạn, Khóa, Tổng nạp, Tổng sử dụng, Còn lại, Ghi chú.</summary>
        private void BuildPrepaidCardColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddPrepaidCardColumn("pc_code", "Mã thẻ", 130, typeof(string), null);
            AddPrepaidCardColumn("pc_group", "Nhóm thẻ", 130, typeof(string), null);
            AddPrepaidCardColumn("pc_holders", "Khách đang giữ thẻ", 160, typeof(string), null);
            AddPrepaidCardColumn("pc_expiry", "Ngày hết hạn", 112, typeof(DateTime), "dd/MM/yyyy");
            AddPrepaidCardColumn("pc_locked", "Khóa", 74, typeof(string), null);
            AddPrepaidCardColumn("pc_loaded", "Tổng nạp", 115, typeof(decimal), "#,##0.##");
            AddPrepaidCardColumn("pc_used", "Tổng sử dụng", 115, typeof(decimal), "#,##0.##");
            AddPrepaidCardColumn("pc_remaining", "Còn lại", 115, typeof(decimal), "#,##0.##");
            AddPrepaidCardColumn("pc_note", "Ghi chú", 130, typeof(string), null);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddPrepaidCardColumn(string name, string header, int width96, Type type, string format)
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
                gc.DefaultCellStyle.FormatProvider = NumberInput.ViFormat;
            }
            else if (type == typeof(DateTime))
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
            }
            else if (name == "pc_locked")
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            _grid.Columns.Add(gc);
        }

        private void BuildPrepaidCardTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("pc_code", typeof(string));
            dt.Columns.Add("pc_group", typeof(string));
            dt.Columns.Add("pc_holders", typeof(string));
            dt.Columns.Add("pc_expiry", typeof(DateTime));
            dt.Columns.Add("pc_locked", typeof(string));
            dt.Columns.Add("pc_loaded", typeof(decimal));
            dt.Columns.Add("pc_used", typeof(decimal));
            dt.Columns.Add("pc_remaining", typeof(decimal));
            dt.Columns.Add("pc_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));
            dt.Columns.Add(ColPrepaidHasBalance, typeof(bool));
            dt.Columns.Add(ColPrepaidUnlocked, typeof(bool));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (PrepaidCardRow r in _prepaidCardRows)
            {
                sb.Length = 0;
                sb.Append(r.Code).Append(' ');
                sb.Append(r.GroupName).Append(' ');
                sb.Append(r.HolderNames).Append(' ');
                sb.Append(r.Note);

                dt.Rows.Add(
                    r.Code ?? "",
                    r.GroupName ?? "",
                    r.HolderNames ?? "",
                    r.ExpiryDate.HasValue ? (object)r.ExpiryDate.Value.Date : DBNull.Value,
                    PrepaidCardText.FormatLocked(r.Locked),
                    r.TotalLoaded,
                    r.TotalUsed,
                    r.Remaining,
                    r.Note ?? "",
                    r.Id,
                    CatalogRepository.Fold(sb.ToString()),
                    CatalogRepository.CategoryPath(_nodes, r.GroupId),
                    r.Remaining > 0m,
                    !r.Locked);
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private string CurrentPrepaidCardId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        private PrepaidCardRow CurrentPrepaidCardRow()
        {
            string id = CurrentPrepaidCardId();
            if (string.IsNullOrEmpty(id))
                return null;
            return _prepaidCardRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        }

        internal bool SelectPrepaidCardRowByIdForTest(string id)
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
                    UpdatePrepaidCardButtons();
                    UpdatePrepaidCardAuditPanel();
                    return true;
                }
            }
            return false;
        }

        internal void UpdatePrepaidCardButtons()
        {
            if (!_prepaidCardMode)
                return;
            bool hasRow = CurrentPrepaidCardId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;

            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            _groupAdd.Enabled = _canAdd;
            _groupEdit.Enabled = _canEdit && realGroup;
            _tree.LabelEdit = _canEdit;
        }

        private void UpdatePrepaidCardAuditPanel()
        {
            if (!_prepaidCardMode)
                return;
            PrepaidCardRow row = CurrentPrepaidCardRow();
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

        // ------------------------------------------------------------------ CRUD Thẻ trả trước

        private void AddPrepaidCard()
        {
            if (!_prepaidCardMode || !_canAdd)
                return;
            string groupId = SelectedGroupId();
            if (groupId == UnsetTag)
                groupId = null;
            using (var dlg = new PrepaidCardEditDialog(ResolvePrepaidCardSession(), null, groupId, false))
            {
                OpenPrepaidCardDialog(dlg);
                if (dlg.AnyChanged)
                    RefreshPrepaidCardAndSelect(dlg.SavedId);
            }
        }

        private void EditPrepaidCard()
        {
            if (!_prepaidCardMode || !_canEdit)
                return;
            string id = CurrentPrepaidCardId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new PrepaidCardEditDialog(ResolvePrepaidCardSession(), id, null, false))
            {
                OpenPrepaidCardDialog(dlg);
                if (dlg.AnyChanged)
                    RefreshPrepaidCardAndSelect(dlg.SavedId ?? id);
            }
        }

        private void OpenPrepaidCardFromGrid()
        {
            if (!_prepaidCardMode)
                return;
            if (_canEdit)
            {
                EditPrepaidCard();
                return;
            }
            if (!_canViewPrepaidCard)
                return;
            string id = CurrentPrepaidCardId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new PrepaidCardEditDialog(ResolvePrepaidCardSession(), id, null, true))
            {
                OpenPrepaidCardDialog(dlg);
                if (dlg.AnyChanged)
                    RefreshPrepaidCardAndSelect(id);
            }
        }

        private DialogResult OpenPrepaidCardDialog(PrepaidCardEditDialog dlg)
        {
            return PrepaidCardDialogOpenerForTest != null
                ? PrepaidCardDialogOpenerForTest(dlg)
                : dlg.ShowDialog(this);
        }

        private void DeletePrepaidCard()
        {
            if (!_prepaidCardMode || !_canRemove)
                return;
            string id = CurrentPrepaidCardId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressPrepaidCardPopupForTest && !Msg.Confirm(this, PrepaidCardText.DeleteConfirm))
                return;

            PrepaidCardDeleteResult res;
            try
            {
                res = _prepaidCardService.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowPrepaidCardError("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowPrepaidCardWarning(res.ErrorMessage);
                return;
            }
            LastPrepaidCardMessageForTest = null;
            Reload();
        }

        private void RefreshPrepaidCardAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectPrepaidCardRowByIdForTest(id);
        }
    }
}
