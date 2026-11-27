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
    /// Phần nạp dữ liệu công nợ, cây nhóm, lọc cục bộ, chi tiết chứng từ/phiếu thu chi,
    /// thao tác Thu tiền nợ / Chi trả nợ, xuất Excel, phím tắt và hỗ trợ kiểm thử cho <see cref="DebtListView"/>.
    /// Phần tô màu, menu ngữ cảnh, vẽ cây/số dòng và đổi theme đặt ở <c>DebtListView.Render.cs</c>.
    /// </summary>
    internal sealed partial class DebtListView
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
                _allGroups = new List<DebtGroupNode>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được danh mục nhóm: " + ex.Message;
            }
            finally
            {
                _loading = false;
            }
            ReloadDebts();
        }

        public void ReloadDebts()
        {
            string keepId = CurrentPartner() == null ? null : CurrentPartner().Id;
            _loading = true;
            try
            {
                if (!_service.CanView(_kind))
                {
                    _allPartners = new List<DebtPartnerRow>();
                    _visiblePartners = new List<DebtPartnerRow>();
                    _grid.Rows.Clear();
                    ClearDetails();
                    _empty.Text = DebtConstants.NoPermission;
                    _empty.Visible = true;
                    _summary.Text = IsCustomer ? "0 khách hàng" : "0 nhà cung cấp";
                    return;
                }

                _allPartners = _service.LoadPartnerDebts(_kind);
            }
            catch (DataAccessException ex)
            {
                _allPartners = new List<DebtPartnerRow>();
                _visiblePartners = new List<DebtPartnerRow>();
                _grid.Rows.Clear();
                ClearDetails();
                _empty.Text = "Không đọc được dữ liệu công nợ: " + ex.Message;
                _empty.Visible = true;
                _summary.Text = IsCustomer ? "0 khách hàng" : "0 nhà cung cấp";
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
            _allGroups = _service.LoadGroups(_kind);

            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            var root = new TreeNode("Tất cả") { Tag = null };
            _tree.Nodes.Add(root);

            var byId = new Dictionary<string, DebtGroupNode>(StringComparer.Ordinal);
            foreach (DebtGroupNode g in _allGroups)
                if (!string.IsNullOrEmpty(g.Id))
                    byId[g.Id] = g;

            var byParent = new Dictionary<string, List<DebtGroupNode>>(StringComparer.Ordinal);
            var tops = new List<DebtGroupNode>();
            foreach (DebtGroupNode g in _allGroups)
            {
                if (string.IsNullOrEmpty(g.ParentId) || !byId.ContainsKey(g.ParentId)
                    || string.Equals(g.ParentId, g.Id, StringComparison.Ordinal))
                {
                    tops.Add(g);
                    continue;
                }
                List<DebtGroupNode> kids;
                if (!byParent.TryGetValue(g.ParentId, out kids))
                    byParent[g.ParentId] = kids = new List<DebtGroupNode>();
                kids.Add(g);
            }

            foreach (DebtGroupNode top in tops)
                AddGroupTreeNode(root.Nodes, top, byParent, 0);

            root.Nodes.Add(new TreeNode("Chưa thiết lập") { Tag = DebtConstants.UnassignedGroupTag });
            root.ExpandAll();
            TreeNode selected = string.IsNullOrEmpty(keepTag) ? root : (FindTreeNodeByTag(root, keepTag) ?? root);
            _selectedGroupTag = selected.Tag as string;
            _tree.SelectedNode = selected;
            _tree.EndUpdate();
        }

        private static void AddGroupTreeNode(
            TreeNodeCollection into,
            DebtGroupNode node,
            Dictionary<string, List<DebtGroupNode>> byParent,
            int depth)
        {
            var tn = new TreeNode(node.Name ?? "") { Tag = node.Id };
            into.Add(tn);
            List<DebtGroupNode> kids;
            if (depth < 64 && byParent.TryGetValue(node.Id, out kids))
            {
                foreach (DebtGroupNode child in kids)
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

        private DebtFilter CurrentFilter()
        {
            bool unassigned = string.Equals(_selectedGroupTag, DebtConstants.UnassignedGroupTag, StringComparison.Ordinal);
            HashSet<string> groupIds = (!unassigned && !string.IsNullOrEmpty(_selectedGroupTag))
                ? DebtRules.CollectGroupSubtree(_allGroups, _selectedGroupTag)
                : null;
            return new DebtFilter
            {
                GroupId = unassigned ? null : _selectedGroupTag,
                GroupIds = groupIds,
                UnassignedGroupOnly = unassigned,
                SearchText = _txtSearch.Text,
                OnlyWithDebt = _chkOnlyDebt.Checked
            };
        }

        private void ApplyLocalFilter(string selectPartnerId = null)
        {
            DebtFilter filter = CurrentFilter();
            _visiblePartners = _allPartners.FindAll(r => DebtRules.MatchesFilter(r, filter));
            FillMasterGrid(selectPartnerId);
            UpdateActionState();
            LoadSelectedDetail();
        }

        private void FillMasterGrid(string selectPartnerId)
        {
            _loading = true;
            try
            {
                _grid.Rows.Clear();
                ThemeTokens t = ThemeManager.Current;

                foreach (DebtPartnerRow r in _visiblePartners)
                {
                    int idx = _grid.Rows.Add(r.Code, r.Name, r.Address, r.Phone, r.RemainingDebt);
                    _grid.Rows[idx].Tag = r;
                }

                DebtTotals totals = DebtRules.ComputeTotals(_visiblePartners);
                if (_visiblePartners.Count > 0)
                {
                    int sumIdx = _grid.Rows.Add("", "Tổng cộng", "", "", totals.TotalDebt);
                    _grid.Rows[sumIdx].Tag = SummaryRowTag;
                    StyleSummaryRow(_grid.Rows[sumIdx], t);
                }

                int selectIdx = -1;
                if (!string.IsNullOrEmpty(selectPartnerId))
                {
                    for (int i = 0; i < _grid.Rows.Count; i++)
                    {
                        var p = _grid.Rows[i].Tag as DebtPartnerRow;
                        if (p != null && string.Equals(p.Id, selectPartnerId, StringComparison.Ordinal))
                        {
                            selectIdx = i;
                            break;
                        }
                    }
                }
                if (selectIdx < 0 && _visiblePartners.Count > 0)
                    selectIdx = 0;

                if (selectIdx >= 0)
                {
                    int firstCol = FirstVisibleColumnIndex(_grid);
                    _grid.Rows[selectIdx].Selected = true;
                    if (firstCol >= 0)
                        _grid.CurrentCell = _grid.Rows[selectIdx].Cells[firstCol];
                }

                _empty.Visible = _visiblePartners.Count == 0;
                _empty.Text = !_service.CanView(_kind)
                    ? DebtConstants.NoPermission
                    : (IsCustomer ? DebtConstants.EmptyCustomerMessage : DebtConstants.EmptySupplierMessage);

                string noun = IsCustomer ? "khách hàng" : "nhà cung cấp";
                _summary.Text = string.Format("{0} {1} — Tổng nợ: {2}",
                    totals.Count, noun, DebtConstants.FormatMoneyVi(totals.TotalDebt));
            }
            finally
            {
                _loading = false;
            }
        }

        private void LoadSelectedDetail()
        {
            ClearDetails();
            DebtPartnerRow partner = CurrentPartner();
            if (partner == null || string.IsNullOrEmpty(partner.Id))
                return;

            try
            {
                DebtPartnerDetail detail = _service.LoadPartnerDetail(_kind, partner.Id);
                foreach (DebtDocumentRow d in detail.Documents)
                {
                    string desc = !string.IsNullOrEmpty(d.Description) ? d.Description : d.Note;
                    int idx = _billGrid.Rows.Add(
                        d.Number, d.Date, d.DocTypeLabel, d.TotalAmount, d.PaidTotal, d.RemainingDebt, desc);
                    _billGrid.Rows[idx].Tag = d;
                }
                foreach (DebtVoucherRow v in detail.Vouchers)
                {
                    string desc = !string.IsNullOrEmpty(v.Description) ? v.Description : v.Note;
                    int idx = _voucherGrid.Rows.Add(v.Number, v.Date, v.Income, v.Expense, desc);
                    _voucherGrid.Rows[idx].Tag = v;
                }
            }
            catch (DataAccessException)
            {
                ClearDetails();
            }
        }

        private void ClearDetails()
        {
            _billGrid.Rows.Clear();
            _voucherGrid.Rows.Clear();
        }

        private DebtPartnerRow CurrentPartner()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as DebtPartnerRow;
        }

        private bool CanPerformDebtAction()
        {
            return IsCustomer ? _service.CanCollectCustomer : _service.CanPaySupplier;
        }

        private void UpdateActionState()
        {
            bool canAct = CanPerformDebtAction();
            bool hasPartner = CurrentPartner() != null;
            _btnAction.Visible = canAct;
            _btnAction.Enabled = canAct && hasPartner;
        }

        private void ExecuteDebtAction()
        {
            if (!CanPerformDebtAction())
            {
                ShowWarning(DebtConstants.NoPermission);
                return;
            }
            DebtPartnerRow partner = CurrentPartner();
            if (partner == null || string.IsNullOrEmpty(partner.Id))
                return;

            if (IsCustomer)
                OpenCollectCustomerDialog(partner);
            else
                OpenPaySupplierDialog(partner);
        }

        private void OpenCollectCustomerDialog(DebtPartnerRow customer)
        {
            using (var dlg = new CustomerDebtCollectDialog(_session, customer))
            {
                if (SuppressPopup)
                    dlg.SuppressPopup = true;
                DialogResult res = CollectDialogOpenerForTest != null
                    ? CollectDialogOpenerForTest(dlg)
                    : dlg.ShowDialog(this);
                if (dlg.SavedOk || res == DialogResult.OK)
                    ReloadDebtsAndSelect(customer.Id);
            }
        }

        private void OpenPaySupplierDialog(DebtPartnerRow supplier)
        {
            SupplierDebtPayPreset preset = DebtRules.BuildSupplierPayPreset(supplier);
            using (var dlg = new PhieuThuDialog(_session, preset))
            {
                if (SuppressPopup)
                    dlg.SuppressPopup = true;
                DialogResult res = SupplierPayDialogOpenerForTest != null
                    ? SupplierPayDialogOpenerForTest(dlg)
                    : dlg.ShowDialog(this);
                if (dlg.AnySaved || res == DialogResult.OK)
                    ReloadDebtsAndSelect(supplier.Id);
            }
        }

        private void ReloadDebtsAndSelect(string partnerId)
        {
            ReloadDebts();
            if (!string.IsNullOrEmpty(partnerId))
                SelectPartnerByIdForTest(partnerId);
        }

        internal bool ExportGridToExcel()
        {
            string name = IsCustomer ? "CongNoKhachHang" : "CongNoNhaCungCap";
            return GridExcelExporter.Export(
                this, _grid, name, GridExportOutputForTest,
                delegate(string msg) { LastExportMessageForTest = msg; });
        }

        internal DataGridViewRow FindRowByPartnerIdForTest(string partnerId)
        {
            if (string.IsNullOrEmpty(partnerId))
                return null;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var p = r.Tag as DebtPartnerRow;
                if (p != null && string.Equals(p.Id, partnerId, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        internal bool SelectPartnerByIdForTest(string partnerId)
        {
            DataGridViewRow r = FindRowByPartnerIdForTest(partnerId);
            if (r == null)
                return false;
            int col = FirstVisibleColumnIndex(_grid);
            _grid.ClearSelection();
            r.Selected = true;
            if (col >= 0)
                _grid.CurrentCell = r.Cells[col];
            UpdateActionState();
            LoadSelectedDetail();
            return true;
        }

        internal bool SelectGroupTagForTest(string groupTag)
        {
            if (_tree.Nodes.Count == 0)
                return false;
            TreeNode root = _tree.Nodes[0];
            TreeNode target = string.IsNullOrEmpty(groupTag) ? root : FindTreeNodeByTag(root, groupTag);
            if (target == null)
                return false;
            _tree.SelectedNode = target;
            return true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ReloadAll();
                return true;
            }
            if (keyData == Keys.F3)
            {
                ReloadDebts();
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
                ReloadDebts();
                _txtSearch.Inner.Focus();
                _txtSearch.Inner.SelectAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ReloadAll();
                e.Handled = true;
            }
        }
    }
}
