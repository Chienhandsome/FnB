using System;
using System.Collections.Generic;
using System.Data;
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
    /// Chế độ Danh mục lý do thu chi (DLYDOTHUCHI) và Danh mục tài khoản ngân hàng (DTAIKHOANNGANHANG)
    /// trong <see cref="CatalogView"/> (docs/specs/phieu-thu-chi.md §3): Thêm / Sửa / Xóa theo quyền,
    /// chặn xóa lý do hệ thống hoặc dòng đang được tham chiếu, xuất Excel qua <c>GridExcelExporter</c>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private bool _reasonMode;
        private bool _bankAccountMode;
        private bool _canViewFundCatalog;
        private CashReasonRepository _reasonRepo;
        private BankAccountRepository _bankRepo;
        private List<CashReasonRow> _reasonRows = new List<CashReasonRow>();
        private List<BankAccountRow> _bankRows = new List<BankAccountRow>();
        private readonly ThemedContextMenu _fundGridMenu = new ThemedContextMenu();

        private bool IsFundCatalogMode { get { return _reasonMode || _bankAccountMode; } }

        // Hook kiểm thử UI (selftest).
        internal bool IsReasonModeForTest { get { return _reasonMode; } }
        internal bool IsBankAccountModeForTest { get { return _bankAccountMode; } }
        internal bool SuppressFundCatalogPopupForTest { get; set; }
        internal string LastFundCatalogMessageForTest { get; private set; }
        internal Func<CashReasonEditDialog, DialogResult> CashReasonDialogOpenerForTest { get; set; }
        internal Func<BankAccountEditDialog, DialogResult> BankAccountDialogOpenerForTest { get; set; }

        private void BuildFundCatalogUi()
        {
            _fundGridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddFundCatalogItem(); });
            _fundGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditFundCatalogItem(); });
            _fundGridMenu.AddItem("Xem chi tiết", delegate { OpenFundCatalogFromGrid(); });
            _fundGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteFundCatalogItem(); });
            _fundGridMenu.AddSeparator();
            _fundGridMenu.AddItem("Refresh", delegate { Reload(); });
            _fundGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _fundGridMenu.AddSeparator();
            _fundGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _fundGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _fundGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _fundGridMenu.Opening += delegate { UpdateFundGridMenuState(); };
        }

        private void LoadFundCatalogData(CatalogData data)
        {
            UserSession sess = AppSession.Current;
            if (_reasonMode)
            {
                _reasonRepo = _reasonRepo ?? new CashReasonRepository(_databasePath);
                _canViewFundCatalog = VoucherRules.CanReasonCatalog(sess, RoleModes.View);
                _canAdd = VoucherRules.CanReasonCatalog(sess, RoleModes.Add);
                _canEdit = VoucherRules.CanReasonCatalog(sess, RoleModes.Edit);
                _canRemove = VoucherRules.CanReasonCatalog(sess, RoleModes.Remove);

                _reasonRows = _reasonRepo.LoadAll();
                _total = _reasonRows.Count;
                BuildTree(data);
                BuildReasonColumns();
                BuildReasonTable();
            }
            else if (_bankAccountMode)
            {
                _bankRepo = _bankRepo ?? new BankAccountRepository(_databasePath);
                _canViewFundCatalog = VoucherRules.CanBankAccountCatalog(sess, RoleModes.View);
                _canAdd = VoucherRules.CanBankAccountCatalog(sess, RoleModes.Add);
                _canEdit = VoucherRules.CanBankAccountCatalog(sess, RoleModes.Edit);
                _canRemove = VoucherRules.CanBankAccountCatalog(sess, RoleModes.Remove);

                _bankRows = _bankRepo.LoadAll();
                _total = _bankRows.Count;
                BuildTree(data);
                BuildBankAccountColumns();
                BuildBankAccountTable();
            }

            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);

            ApplyFundCatalogBarVisibility();
            UpdateFundCatalogButtons();
            UpdateFundCatalogAuditPanel();
        }

        private void ApplyFundCatalogBarVisibility()
        {
            if (!IsFundCatalogMode)
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

        private void BuildReasonColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddProductColumn("f_name", "Tên lý do", 220, typeof(string), null);
            AddProductColumn("f_kind", "Loại", 110, typeof(string), null);
            AddProductColumn("f_type", "Phân loại KQKD", 200, typeof(string), null);
            AddProductColumn("f_sys", "Hệ thống", 95, typeof(string), null);
            AddProductColumn("f_note", "Ghi chú", 200, typeof(string), null);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void BuildReasonTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("f_name", typeof(string));
            dt.Columns.Add("f_kind", typeof(string));
            dt.Columns.Add("f_type", typeof(string));
            dt.Columns.Add("f_sys", typeof(string));
            dt.Columns.Add("f_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (CashReasonRow r in _reasonRows)
            {
                string kindText = r.IsIncome ? "Lý do thu" : "Lý do chi";
                string typeText = VoucherConstants.ReasonTypeLabel(r.ReasonType);
                string sysText = r.IsSystem ? "Hệ thống" : "";
                sb.Length = 0;
                sb.Append(r.Name).Append(' ').Append(kindText).Append(' ').Append(typeText).Append(' ').Append(r.Note);
                dt.Rows.Add(r.Name ?? "", kindText, typeText, sysText, r.Note ?? "", r.Id, CatalogRepository.Fold(sb.ToString()), "");
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private void BuildBankAccountColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = (int)(36 * s);

            AddProductColumn("f_name", "Tên tài khoản", 260, typeof(string), null);
            AddProductColumn("f_note", "Ghi chú", 260, typeof(string), null);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void BuildBankAccountTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("f_name", typeof(string));
            dt.Columns.Add("f_note", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            dt.BeginLoadData();
            foreach (BankAccountRow r in _bankRows)
            {
                string folded = CatalogRepository.Fold((r.Name ?? "") + " " + (r.Note ?? ""));
                dt.Rows.Add(r.Name ?? "", r.Note ?? "", r.Id, folded, "");
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private string CurrentFundCatalogId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            return drv == null ? null : drv[ColId] as string;
        }

        internal bool SelectFundRowByIdForTest(string id)
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
                    UpdateFundCatalogButtons();
                    UpdateFundCatalogAuditPanel();
                    return true;
                }
            }
            return false;
        }

        private void UpdateFundCatalogButtons()
        {
            if (!IsFundCatalogMode)
                return;
            bool hasRow = CurrentFundCatalogId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void UpdateFundCatalogAuditPanel()
        {
            if (!IsFundCatalogMode)
                return;
            string id = CurrentFundCatalogId();
            if (string.IsNullOrEmpty(id))
            {
                _lblCreated.Text = "";
                _lblCreatedBy.Text = "";
                _lblModified.Text = "";
                _lblModifiedBy.Text = "";
                return;
            }
            if (_reasonMode)
            {
                CashReasonRow row = _reasonRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
                if (row != null)
                {
                    _lblCreated.Text = FormatAuditTime(row.CreatedAt);
                    _lblCreatedBy.Text = row.CreatedByName ?? "";
                    _lblModified.Text = FormatAuditTime(row.ModifiedAt);
                    _lblModifiedBy.Text = row.ModifiedByName ?? "";
                }
            }
            else if (_bankAccountMode)
            {
                BankAccountRow row = _bankRows.Find(r => string.Equals(r.Id, id, StringComparison.Ordinal));
                if (row != null)
                {
                    _lblCreated.Text = FormatAuditTime(row.CreatedAt);
                    _lblCreatedBy.Text = row.CreatedByName ?? "";
                    _lblModified.Text = FormatAuditTime(row.ModifiedAt);
                    _lblModifiedBy.Text = row.ModifiedByName ?? "";
                }
            }
        }

        private void UpdateFundGridMenuState()
        {
            bool hasRow = CurrentFundCatalogId() != null;
            foreach (ToolStripItem item in _fundGridMenu.Items)
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
                        mi.Visible = !_canEdit && _canViewFundCatalog;
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

        // ------------------------------------------------------------------ CRUD Lý do / Tài khoản

        private void AddFundCatalogItem()
        {
            if (!IsFundCatalogMode || !_canAdd)
                return;
            if (_reasonMode)
            {
                using (var dlg = new CashReasonEditDialog(AppSession.Current, null, false))
                {
                    OpenCashReasonDialog(dlg);
                    if (dlg.AnySaved)
                        RefreshFundAndSelect(dlg.SavedId);
                }
            }
            else if (_bankAccountMode)
            {
                using (var dlg = new BankAccountEditDialog(AppSession.Current, null, false))
                {
                    OpenBankAccountDialog(dlg);
                    if (dlg.AnySaved)
                        RefreshFundAndSelect(dlg.SavedId);
                }
            }
        }

        private void EditFundCatalogItem()
        {
            if (!IsFundCatalogMode || !_canEdit)
                return;
            string id = CurrentFundCatalogId();
            if (string.IsNullOrEmpty(id))
                return;
            if (_reasonMode)
            {
                using (var dlg = new CashReasonEditDialog(AppSession.Current, id, false))
                {
                    OpenCashReasonDialog(dlg);
                    if (dlg.AnySaved)
                        RefreshFundAndSelect(dlg.SavedId);
                }
            }
            else if (_bankAccountMode)
            {
                using (var dlg = new BankAccountEditDialog(AppSession.Current, id, false))
                {
                    OpenBankAccountDialog(dlg);
                    if (dlg.AnySaved)
                        RefreshFundAndSelect(dlg.SavedId);
                }
            }
        }

        private void OpenFundCatalogFromGrid()
        {
            if (!IsFundCatalogMode)
                return;
            if (_canEdit)
            {
                EditFundCatalogItem();
                return;
            }
            if (!_canViewFundCatalog)
                return;
            string id = CurrentFundCatalogId();
            if (string.IsNullOrEmpty(id))
                return;
            if (_reasonMode)
            {
                using (var dlg = new CashReasonEditDialog(AppSession.Current, id, true))
                    OpenCashReasonDialog(dlg);
            }
            else if (_bankAccountMode)
            {
                using (var dlg = new BankAccountEditDialog(AppSession.Current, id, true))
                    OpenBankAccountDialog(dlg);
            }
        }

        private void DeleteFundCatalogItem()
        {
            if (!IsFundCatalogMode || !_canRemove)
                return;
            string id = CurrentFundCatalogId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!SuppressFundCatalogPopupForTest && !Msg.Confirm(this, VoucherConstants.DeleteConfirm))
                return;

            SimpleCatalogDeleteResult res;
            try
            {
                res = _reasonMode ? _reasonRepo.Delete(id) : _bankRepo.Delete(id);
            }
            catch (DataAccessException ex)
            {
                ShowFundCatalogWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowFundCatalogWarning(res.ErrorMessage);
                return;
            }
            LastFundCatalogMessageForTest = null;
            Reload();
        }

        private DialogResult OpenCashReasonDialog(CashReasonEditDialog dlg)
        {
            return CashReasonDialogOpenerForTest != null ? CashReasonDialogOpenerForTest(dlg) : dlg.ShowDialog(this);
        }

        private DialogResult OpenBankAccountDialog(BankAccountEditDialog dlg)
        {
            return BankAccountDialogOpenerForTest != null ? BankAccountDialogOpenerForTest(dlg) : dlg.ShowDialog(this);
        }

        private void RefreshFundAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectFundRowByIdForTest(id);
        }

        private void ShowFundCatalogWarning(string message)
        {
            LastFundCatalogMessageForTest = message;
            if (!SuppressFundCatalogPopupForTest)
                Msg.Warning(this, message);
        }
    }
}
