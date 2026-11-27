using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần nạp dữ liệu, cây nhóm, lọc cục bộ, chi tiết 2 tab, thêm/sửa/xóa phiếu tặng/giảm điểm,
    /// xuất Excel (lưới khách và từng tab), phím tắt và hook kiểm thử cho <see cref="LoyaltyView"/>.
    /// </summary>
    internal sealed partial class LoyaltyView
    {
        public void ReloadAll()
        {
            _loading = true;
            try
            {
                LoadGroupTree();
            }
            catch (DataAccessException ex)
            {
                _allGroups = new List<LoyaltyGroupNode>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được danh mục nhóm khách hàng: " + ex.Message;
            }
            finally
            {
                _loading = false;
            }
            ReloadCustomers();
        }

        public void ReloadCustomers(string keepCustomerId = null)
        {
            string keepId = !string.IsNullOrEmpty(keepCustomerId)
                ? keepCustomerId
                : (CurrentCustomer() == null ? null : CurrentCustomer().Id);

            _loading = true;
            try
            {
                if (!_service.CanView)
                {
                    _allCustomers = new List<LoyaltyCustomerRow>();
                    _visibleCustomers = new List<LoyaltyCustomerRow>();
                    _grid.Rows.Clear();
                    ClearDetails();
                    _empty.Text = LoyaltyConstants.NoPermission;
                    _empty.Visible = true;
                    _summary.Text = "0 khách hàng";
                    return;
                }

                _allCustomers = _service.LoadCustomers();
            }
            catch (DataAccessException ex)
            {
                _allCustomers = new List<LoyaltyCustomerRow>();
                _visibleCustomers = new List<LoyaltyCustomerRow>();
                _grid.Rows.Clear();
                ClearDetails();
                _empty.Text = "Không đọc được danh sách khách hàng thân thiết: " + ex.Message;
                _empty.Visible = true;
                _summary.Text = "0 khách hàng";
                return;
            }
            finally
            {
                _loading = false;
            }

            ApplyLocalFilter(keepId);
        }

        private void LoadGroupTree()
        {
            string keepTag = _selectedGroupTag;
            _allGroups = _service.LoadGroups();

            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            var root = new TreeNode("Tất cả") { Tag = null };
            _tree.Nodes.Add(root);

            var byId = new Dictionary<string, LoyaltyGroupNode>(StringComparer.Ordinal);
            foreach (LoyaltyGroupNode g in _allGroups)
                if (!string.IsNullOrEmpty(g.Id))
                    byId[g.Id] = g;

            var byParent = new Dictionary<string, List<LoyaltyGroupNode>>(StringComparer.Ordinal);
            var tops = new List<LoyaltyGroupNode>();
            foreach (LoyaltyGroupNode g in _allGroups)
            {
                if (string.IsNullOrEmpty(g.ParentId) || !byId.ContainsKey(g.ParentId)
                    || string.Equals(g.ParentId, g.Id, StringComparison.Ordinal))
                {
                    tops.Add(g);
                    continue;
                }
                List<LoyaltyGroupNode> kids;
                if (!byParent.TryGetValue(g.ParentId, out kids))
                    byParent[g.ParentId] = kids = new List<LoyaltyGroupNode>();
                kids.Add(g);
            }

            foreach (LoyaltyGroupNode top in tops)
                AddGroupTreeNode(root.Nodes, top, byParent, 0);

            root.Nodes.Add(new TreeNode("Chưa thiết lập") { Tag = LoyaltyConstants.UnassignedGroupTag });
            root.ExpandAll();
            TreeNode selected = string.IsNullOrEmpty(keepTag) ? root : (FindTreeNodeByTag(root, keepTag) ?? root);
            _selectedGroupTag = selected.Tag as string;
            _tree.SelectedNode = selected;
            _tree.EndUpdate();
        }

        private static void AddGroupTreeNode(
            TreeNodeCollection into,
            LoyaltyGroupNode node,
            Dictionary<string, List<LoyaltyGroupNode>> byParent,
            int depth)
        {
            var tn = new TreeNode(node.Name ?? "") { Tag = node.Id };
            into.Add(tn);
            List<LoyaltyGroupNode> kids;
            if (depth < 64 && byParent.TryGetValue(node.Id, out kids))
            {
                foreach (LoyaltyGroupNode child in kids)
                    AddGroupTreeNode(tn.Nodes, child, byParent, depth + 1);
            }
        }

        private static TreeNode FindTreeNodeByTag(TreeNode from, string tag)
        {
            foreach (TreeNode child in from.Nodes)
            {
                if (string.Equals(child.Tag as string, tag, StringComparison.Ordinal))
                    return child;
                TreeNode found = FindTreeNodeByTag(child, tag);
                if (found != null)
                    return found;
            }
            return null;
        }

        private LoyaltyFilter CurrentFilter()
        {
            bool unassigned = string.Equals(_selectedGroupTag, LoyaltyConstants.UnassignedGroupTag, StringComparison.Ordinal);
            HashSet<string> groupIds = (!unassigned && !string.IsNullOrEmpty(_selectedGroupTag))
                ? LoyaltyRules.CollectGroupSubtree(_allGroups, _selectedGroupTag)
                : null;
            return new LoyaltyFilter
            {
                GroupId = unassigned ? null : _selectedGroupTag,
                GroupIds = groupIds,
                UnassignedGroupOnly = unassigned,
                SearchText = _txtSearch.Text,
                OnlyWithPoints = _chkOnlyWithPoints.Checked
            };
        }

        private void ApplyLocalFilter(string selectCustomerId = null)
        {
            LoyaltyFilter filter = CurrentFilter();
            _visibleCustomers = _allCustomers.FindAll(r => LoyaltyRules.MatchesFilter(r, filter));
            FillMasterGrid(selectCustomerId);
            UpdateActionState();
            LoadSelectedDetail();
        }

        private void FillMasterGrid(string selectCustomerId)
        {
            _loading = true;
            try
            {
                _grid.Rows.Clear();
                ThemeTokens t = ThemeManager.Current;

                foreach (LoyaltyCustomerRow r in _visibleCustomers)
                {
                    int idx = _grid.Rows.Add(
                        r.Name, r.GroupName, r.Address, r.Phone, r.Email,
                        r.InitialPoints, r.Revenue, r.BillCount, r.Points, r.Note);
                    _grid.Rows[idx].Tag = r;
                }

                LoyaltyTotals totals = LoyaltyRules.ComputeCustomerTotals(_visibleCustomers);
                if (_visibleCustomers.Count > 0)
                {
                    int sumIdx = _grid.Rows.Add(
                        "Tổng cộng", "", "", "", "", null,
                        totals.TotalRevenue, totals.TotalBillCount, totals.TotalPoints, "");
                    _grid.Rows[sumIdx].Tag = SummaryRowTag;
                    StyleSummaryRow(_grid.Rows[sumIdx], t);
                }

                int selectIdx = -1;
                if (!string.IsNullOrEmpty(selectCustomerId))
                {
                    for (int i = 0; i < _grid.Rows.Count; i++)
                    {
                        var c = _grid.Rows[i].Tag as LoyaltyCustomerRow;
                        if (c != null && string.Equals(c.Id, selectCustomerId, StringComparison.Ordinal))
                        {
                            selectIdx = i;
                            break;
                        }
                    }
                }
                if (selectIdx < 0 && _visibleCustomers.Count > 0)
                    selectIdx = 0;

                if (selectIdx >= 0)
                {
                    int firstCol = FirstVisibleColumnIndex(_grid);
                    _grid.Rows[selectIdx].Selected = true;
                    if (firstCol >= 0)
                        _grid.CurrentCell = _grid.Rows[selectIdx].Cells[firstCol];
                }

                _empty.Visible = _visibleCustomers.Count == 0;
                _empty.Text = !_service.CanView
                    ? LoyaltyConstants.NoPermission
                    : LoyaltyConstants.EmptyCustomerMessage;

                _summary.Text = string.Format("{0} khách hàng — Tổng điểm: {1}",
                    totals.CustomerCount, LoyaltyConstants.FormatPointsVi(totals.TotalPoints));
            }
            finally
            {
                _loading = false;
            }
        }

        private void LoadSelectedDetail(string selectVoucherId = null)
        {
            ClearDetails();
            LoyaltyCustomerRow customer = CurrentCustomer();
            if (customer == null || string.IsNullOrEmpty(customer.Id))
                return;

            try
            {
                LoyaltyCustomerDetail detail = _service.LoadCustomerDetail(customer.Id);
                ThemeTokens t = ThemeManager.Current;

                foreach (LoyaltyBillRow b in detail.Bills)
                {
                    int idx = _billGrid.Rows.Add(b.Points, b.Number, b.Date, b.TotalAmount, b.PointsUsed);
                    _billGrid.Rows[idx].Tag = b;
                }
                if (detail.Bills.Count > 0)
                {
                    LoyaltyBillTotals bt = LoyaltyRules.ComputeBillTotals(detail.Bills);
                    int sumIdx = _billGrid.Rows.Add(bt.TotalPoints, "Tổng cộng", null, bt.TotalAmount, bt.TotalPointsUsed);
                    _billGrid.Rows[sumIdx].Tag = SummaryRowTag;
                    StyleSummaryRow(_billGrid.Rows[sumIdx], t);
                }

                int selVoucherIdx = -1;
                foreach (LoyaltyPointVoucherRow v in detail.Vouchers)
                {
                    int idx = _voucherGrid.Rows.Add(v.Number, v.Date, v.GiftPoints, v.ReducePoints, v.Reason, v.Note);
                    _voucherGrid.Rows[idx].Tag = v;
                    if (!string.IsNullOrEmpty(selectVoucherId) && string.Equals(v.Id, selectVoucherId, StringComparison.Ordinal))
                        selVoucherIdx = idx;
                }
                if (detail.Vouchers.Count > 0)
                {
                    LoyaltyPointVoucherTotals vt = LoyaltyRules.ComputeVoucherTotals(detail.Vouchers);
                    int sumIdx = _voucherGrid.Rows.Add("Tổng cộng", null, vt.TotalGiftPoints, vt.TotalReducePoints, "", "");
                    _voucherGrid.Rows[sumIdx].Tag = SummaryRowTag;
                    StyleSummaryRow(_voucherGrid.Rows[sumIdx], t);
                    if (selVoucherIdx < 0)
                        selVoucherIdx = 0;
                    int firstCol = FirstVisibleColumnIndex(_voucherGrid);
                    _voucherGrid.Rows[selVoucherIdx].Selected = true;
                    if (firstCol >= 0)
                        _voucherGrid.CurrentCell = _voucherGrid.Rows[selVoucherIdx].Cells[firstCol];
                }
            }
            catch (DataAccessException)
            {
                ClearDetails();
            }
            UpdateActionState();
        }

        private void ClearDetails()
        {
            _billGrid.Rows.Clear();
            _voucherGrid.Rows.Clear();
        }

        private LoyaltyCustomerRow CurrentCustomer()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as LoyaltyCustomerRow;
        }

        private LoyaltyPointVoucherRow CurrentVoucher()
        {
            return _voucherGrid.CurrentRow == null ? null : _voucherGrid.CurrentRow.Tag as LoyaltyPointVoucherRow;
        }

        private void UpdateActionState()
        {
            bool hasCustomer = CurrentCustomer() != null;
            bool hasVoucher = CurrentVoucher() != null;

            _btnAddPoint.Visible = _service.CanAdd;
            _btnAddPoint.Enabled = _service.CanAdd && hasCustomer;

            _btnVoucherAdd.Visible = _service.CanAdd;
            _btnVoucherAdd.Enabled = _service.CanAdd && hasCustomer;

            _btnVoucherEdit.Visible = _service.CanEdit;
            _btnVoucherEdit.Enabled = _service.CanEdit && hasVoucher;

            _btnVoucherDelete.Visible = _service.CanRemove;
            _btnVoucherDelete.Enabled = _service.CanRemove && hasVoucher;

            _btnExcel.Enabled = _service.CanView;
            _btnExportBills.Enabled = _service.CanView;
            _btnExportVouchers.Enabled = _service.CanView;
        }

        private void OpenAddVoucherDialog()
        {
            if (!_service.CanAdd)
            {
                ShowWarning(LoyaltyConstants.NoPermission);
                return;
            }
            LoyaltyCustomerRow cust = CurrentCustomer();
            if (cust == null || string.IsNullOrEmpty(cust.Id))
                return;

            using (var dlg = new LoyaltyPointDialog(_session, null, cust.Id, true, false))
            {
                if (SuppressPopup)
                    dlg.SuppressPopup = true;
                DialogResult res = PointDialogOpenerForTest != null
                    ? PointDialogOpenerForTest(dlg)
                    : dlg.ShowDialog(this);
                if (dlg.AnySaved || res == DialogResult.OK)
                {
                    if (!string.IsNullOrEmpty(dlg.LastUpgradeNotice))
                        LastInfoMessageForTest = dlg.LastUpgradeNotice;
                    ReloadCustomers(cust.Id);
                    if (!string.IsNullOrEmpty(dlg.SavedId))
                        SelectVoucherByIdForTest(dlg.SavedId);
                }
            }
        }

        private void EditSelectedVoucher()
        {
            LoyaltyPointVoucherRow voucher = CurrentVoucher();
            if (voucher == null || string.IsNullOrEmpty(voucher.Id))
                return;
            if (!_service.CanEdit)
            {
                ShowWarning(LoyaltyConstants.NoPermission);
                return;
            }
            OpenEditVoucherDialog(voucher, false);
        }

        private void OpenEditVoucherDialog(LoyaltyPointVoucherRow voucher, bool readOnly)
        {
            if (voucher == null || string.IsNullOrEmpty(voucher.Id))
                return;
            string customerId = CurrentCustomer() != null ? CurrentCustomer().Id : voucher.CustomerId;
            using (var dlg = new LoyaltyPointDialog(_session, voucher.Id, customerId, true, readOnly))
            {
                if (SuppressPopup)
                    dlg.SuppressPopup = true;
                DialogResult res = PointDialogOpenerForTest != null
                    ? PointDialogOpenerForTest(dlg)
                    : dlg.ShowDialog(this);
                if (!readOnly && (dlg.AnySaved || res == DialogResult.OK))
                {
                    if (!string.IsNullOrEmpty(dlg.LastUpgradeNotice))
                        LastInfoMessageForTest = dlg.LastUpgradeNotice;
                    ReloadCustomers(customerId);
                    if (!string.IsNullOrEmpty(dlg.SavedId))
                        SelectVoucherByIdForTest(dlg.SavedId);
                }
            }
        }

        private void DeleteSelectedVoucher()
        {
            if (!_service.CanRemove)
            {
                ShowWarning(LoyaltyConstants.NoPermission);
                return;
            }
            LoyaltyPointVoucherRow voucher = CurrentVoucher();
            if (voucher == null || string.IsNullOrEmpty(voucher.Id))
                return;

            bool confirmed = ConfirmDeleteOverrideForTest != null
                ? ConfirmDeleteOverrideForTest(LoyaltyConstants.DeleteConfirm)
                : (SuppressPopup || Msg.Confirm(this, LoyaltyConstants.DeleteConfirm));
            if (!confirmed)
                return;

            LoyaltyDeleteResult res;
            try
            {
                res = _service.DeleteVoucher(voucher.Id);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không xóa được phiếu tặng/giảm điểm: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }

            string custId = CurrentCustomer() != null ? CurrentCustomer().Id : voucher.CustomerId;
            ReloadCustomers(custId);
        }
    }
}
