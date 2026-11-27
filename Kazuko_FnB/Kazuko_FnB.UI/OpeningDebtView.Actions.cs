using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần nạp danh sách đối tác, lọc cục bộ, theo dõi thay đổi chưa lưu,
    /// lưu một transaction, cảnh báo khi đóng tab và xuất Excel cho <see cref="OpeningDebtView"/>.
    /// </summary>
    internal sealed partial class OpeningDebtView
    {
        private void WireEvents()
        {
            _btnSave.Click += delegate { SaveChanges(); };
            _btnExcel.Click += delegate { ExportGridToExcel(); };
            _btnRefresh.Click += delegate { ReloadAll(false); };

            _dtDate.ValueChanged += delegate { if (!_loading) UpdateDirtyState(); };
            _txtSearch.TextChanged += delegate { if (!_loading) ApplyLocalFilter(); };
            _chkOnlyWithDebt.CheckedChanged += delegate { if (!_loading) ApplyLocalFilter(); };

            _grid.RowPostPaint += DrawRowNumber;
            _grid.MouseDown += GridMouseDown;
            _grid.CellBeginEdit += GridCellBeginEdit;
            _grid.CellParsing += GridCellParsing;
            _grid.CellEndEdit += GridCellEndEdit;
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                NumericEditGridCell.HandleDirtyStateChanged(_grid, MarkNumericCellDirty);
            };
            _grid.DataError += GridDataError;

            _dtDate.Inner.KeyDown += OnKeyDown;
            _txtSearch.Inner.KeyDown += OnKeyDown;
            _grid.KeyDown += OnKeyDown;
            KeyDown += OnKeyDown;
        }

        public bool ConfirmClose()
        {
            return ConfirmDiscardUnsavedChanges();
        }

        public void ReloadAll(bool skipConfirm = false)
        {
            if (!skipConfirm && !ConfirmDiscardUnsavedChanges())
                return;

            LoadCurrentDocument();
        }

        private void LoadCurrentDocument()
        {
            _loading = true;
            try
            {
                _warnBanner.Visible = false;
                _lblWarn.Text = "";

                if (!_service.CanView(_kind))
                {
                    _allItems = new List<OpeningDebtPartnerRow>();
                    _visibleItems = new List<OpeningDebtPartnerRow>();
                    _grid.Rows.Clear();
                    _empty.Text = OpeningDebtConstants.NoPermission;
                    _empty.Visible = true;
                    _summary.Text = _kind == DebtPartyKind.Customer ? "0 khách hàng" : "0 nhà cung cấp";
                    _isDirty = false;
                    return;
                }

                OpeningDebtDocument doc = _service.LoadDocument(_kind);
                _allItems = doc.Items ?? new List<OpeningDebtPartnerRow>();
                _loadedDate = doc.Date.Date;
                _dtDate.Value = _loadedDate;
                _isDirty = false;

                if (!string.IsNullOrEmpty(doc.Warning))
                {
                    _lblWarn.Text = doc.Warning;
                    _warnBanner.Visible = true;
                }
            }
            catch (DataAccessException ex)
            {
                _allItems = new List<OpeningDebtPartnerRow>();
                _visibleItems = new List<OpeningDebtPartnerRow>();
                _grid.Rows.Clear();
                _empty.Text = "Không đọc được công nợ ban đầu: " + ex.Message;
                _empty.Visible = true;
                _summary.Text = _kind == DebtPartyKind.Customer ? "0 khách hàng" : "0 nhà cung cấp";
                _isDirty = false;
                return;
            }
            finally
            {
                _loading = false;
            }

            ApplyLocalFilter();
        }

        private void ApplyLocalFilter()
        {
            if (_grid.IsCurrentCellInEditMode)
                _grid.EndEdit();
            var filter = new OpeningDebtFilter
            {
                SearchText = _txtSearch.Text,
                OnlyWithDebt = _chkOnlyWithDebt.Checked
            };
            _visibleItems = _allItems.FindAll(r => OpeningDebtRules.MatchesFilter(r, filter));
            FillGrid();
        }

        // ------------------------------------------------------------------ Trạng thái thay đổi (dirty)

        private void MarkNumericCellDirty()
        {
            if (!_readOnly && !_loading)
                _isDirty = true;
        }

        private void UpdateDirtyState()
        {
            if (_readOnly)
            {
                _isDirty = false;
                return;
            }
            if (_dtDate.Value.Date != _loadedDate.Date)
            {
                _isDirty = true;
                return;
            }
            for (int i = 0; i < _allItems.Count; i++)
            {
                if (_allItems[i].IsDirty)
                {
                    _isDirty = true;
                    return;
                }
            }
            _isDirty = false;
        }

        // ------------------------------------------------------------------ Lưu & Xuất Excel

        internal bool SaveChanges()
        {
            LastSaveResultForTest = null;
            if (_readOnly)
            {
                ShowWarning(OpeningDebtConstants.NoPermission);
                return false;
            }

            if (_grid.IsCurrentCellInEditMode)
                _grid.EndEdit();
            _dtDate.Commit();

            var req = new OpeningDebtSaveRequest
            {
                PartyKind = _kind,
                Date = _dtDate.Value.Date,
                DateChanged = _dtDate.Value.Date != _loadedDate.Date,
                Items = _allItems
            };

            OpeningDebtSaveResult res;
            try
            {
                res = _service.Save(req);
            }
            catch (DataAccessException ex)
            {
                res = OpeningDebtSaveResult.Fail("Không lưu được công nợ ban đầu: " + ex.Message);
            }

            LastSaveResultForTest = res;
            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage ?? "Không thể lưu công nợ ban đầu.");
                return false;
            }

            _isDirty = false;
            LoadCurrentDocument();

            if (!string.IsNullOrEmpty(res.Warning))
                ShowWarning(res.Warning);
            else if (res.ChangedCount == 0)
                ShowInfo(OpeningDebtConstants.NoChangesToSave);
            else
                ShowInfo(OpeningDebtConstants.SavedSuccess);

            return true;
        }

        internal bool ExportGridToExcel()
        {
            if (!_service.CanView(_kind))
                return false;
            if (_grid.IsCurrentCellInEditMode)
                _grid.EndEdit();
            string prefix = _kind == DebtPartyKind.Customer
                ? "CongNoKhachHangBanDau"
                : "CongNoNhaCungCapBanDau";
            return GridExcelExporter.Export(
                this, _grid, prefix, GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        // ------------------------------------------------------------------ Hỗ trợ & kiểm thử

        private bool ConfirmDiscardUnsavedChanges()
        {
            if (!_isDirty)
                return true;
            UnsavedPromptTriggeredForTest = true;
            if (ConfirmUnsavedOverrideForTest != null)
                return ConfirmUnsavedOverrideForTest();
            if (SuppressPopup)
                return true;
            return Msg.Confirm(this, OpeningDebtConstants.UnsavedChangesConfirm);
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ShowInfo(string message)
        {
            LastInfoMessageForTest = message;
            if (!SuppressPopup)
                Msg.Info(this, message);
        }

        internal DataGridViewRow FindRowByPartnerIdForTest(string partnerId)
        {
            if (string.IsNullOrEmpty(partnerId))
                return null;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var item = r.Tag as OpeningDebtPartnerRow;
                if (item != null && string.Equals(item.PartnerId, partnerId, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.L) || keyData == (Keys.Control | Keys.S))
            {
                if (_btnSave.Visible && _btnSave.Enabled)
                    SaveChanges();
                return true;
            }
            if (keyData == Keys.F5)
            {
                ReloadAll(false);
                return true;
            }
            if (keyData == Keys.F3)
            {
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ReloadAll(false);
                e.Handled = true;
            }
        }
    }
}
