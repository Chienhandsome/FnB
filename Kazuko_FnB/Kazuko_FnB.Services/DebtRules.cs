using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không đụng DB, không đụng UI) cho Công nợ khách hàng &amp; Công nợ nhà cung cấp
    /// (docs/specs/cong-no.md §2–§5, §7 T1): lọc nhóm/tìm/chỉ còn nợ, tính tổng cộng,
    /// phân bổ thu nợ theo đơn từ đơn cũ nhất, kiểm tra hợp lệ và kiểm tra quyền.
    /// </summary>
    public static class DebtRules
    {
        /// <summary>Kiểm tra một đối tượng công nợ có khớp bộ lọc hiện tại hay không.</summary>
        public static bool MatchesFilter(DebtPartnerRow row, DebtFilter filter)
        {
            if (row == null)
                return false;
            if (filter == null)
                return true;

            if (filter.OnlyWithDebt && row.RemainingDebt <= 0m)
                return false;

            if (filter.UnassignedGroupOnly)
            {
                if (!string.IsNullOrEmpty(row.GroupId))
                    return false;
            }
            else if (filter.GroupIds != null && filter.GroupIds.Count > 0)
            {
                if (!filter.GroupIds.Contains(row.GroupId ?? ""))
                    return false;
            }
            else if (!string.IsNullOrEmpty(filter.GroupId))
            {
                if (!string.Equals(row.GroupId ?? "", filter.GroupId, StringComparison.Ordinal))
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                string q = CatalogRepository.Fold(filter.SearchText.Trim());
                if (q.Length > 0)
                {
                    string[] fields =
                    {
                        row.Code ?? "",
                        row.Name ?? "",
                        row.Address ?? "",
                        row.Phone ?? "",
                        row.GroupName ?? ""
                    };
                    bool matched = false;
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (CatalogRepository.Fold(fields[i]).IndexOf(q, StringComparison.Ordinal) >= 0)
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (!matched)
                        return false;
                }
            }

            return true;
        }

        /// <summary>Mở rộng một nhóm được chọn trên cây thành tập gồm chính nó và mọi nhóm con cháu.</summary>
        public static HashSet<string> CollectGroupSubtree(IList<DebtGroupNode> allGroups, string rootGroupId)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(rootGroupId))
                return ids;

            ids.Add(rootGroupId);
            if (allGroups == null || allGroups.Count == 0)
                return ids;

            var byParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (DebtGroupNode g in allGroups)
            {
                if (g == null || string.IsNullOrEmpty(g.Id) || string.IsNullOrEmpty(g.ParentId))
                    continue;
                if (string.Equals(g.ParentId, g.Id, StringComparison.Ordinal))
                    continue;
                List<string> kids;
                if (!byParent.TryGetValue(g.ParentId, out kids))
                    byParent[g.ParentId] = kids = new List<string>();
                kids.Add(g.Id);
            }

            var queue = new Queue<string>();
            queue.Enqueue(rootGroupId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                List<string> kids;
                if (!byParent.TryGetValue(current, out kids))
                    continue;
                foreach (string childId in kids)
                {
                    if (ids.Add(childId))
                        queue.Enqueue(childId);
                }
            }
            return ids;
        }

        /// <summary>Tính tổng cộng cho hàng tổng trên lưới công nợ (thuần decimal).</summary>
        public static DebtTotals ComputeTotals(IEnumerable<DebtPartnerRow> rows)
        {
            var totals = new DebtTotals();
            if (rows == null)
                return totals;
            foreach (DebtPartnerRow r in rows)
            {
                if (r == null)
                    continue;
                totals.Count++;
                totals.TotalDebt += r.RemainingDebt;
            }
            return totals;
        }

        /// <summary>
        /// Phân bổ mặc định số tiền thu từ đơn cũ nhất tới hết số tiền (spec §7 T1).
        /// Mỗi đơn chỉ nhận tối đa đúng BillRemaining của đơn đó. Trả về tổng số tiền đã phân bổ được.
        /// </summary>
        public static decimal AllocateOldestFirst(IList<DebtAllocationLine> lines, decimal totalAmount)
        {
            if (lines == null || lines.Count == 0)
                return 0m;

            decimal left = totalAmount > 0m ? totalAmount : 0m;
            decimal allocated = 0m;
            for (int i = 0; i < lines.Count; i++)
            {
                DebtAllocationLine line = lines[i];
                if (line == null)
                    continue;
                decimal cap = line.BillRemaining > 0m ? line.BillRemaining : 0m;
                decimal take = left >= cap ? cap : left;
                line.PayAmount = take;
                left -= take;
                allocated += take;
            }
            return allocated;
        }

        /// <summary>Tính tổng số tiền thu lần này của danh sách dòng phân bổ.</summary>
        public static decimal SumAllocations(IEnumerable<DebtAllocationLine> lines)
        {
            decimal sum = 0m;
            if (lines == null)
                return 0m;
            foreach (DebtAllocationLine l in lines)
            {
                if (l != null && l.PayAmount > 0m)
                    sum += l.PayAmount;
            }
            return sum;
        }

        /// <summary>
        /// Kiểm tra đầu vào hộp "Thu nợ theo đơn" trước khi ghi DB (spec §3, §6 #5, §7 T1):
        /// bắt buộc khách hàng, số phiếu, cửa hàng, tài khoản ngân hàng khi chuyển khoản,
        /// mỗi dòng không âm và không vượt quá Còn nợ của đơn đó, tổng thu &gt; 0.
        /// </summary>
        public static string ValidateCollectRequest(DebtCollectRequest req, out decimal totalAmount)
        {
            totalAmount = 0m;
            if (req == null)
                return DebtConstants.CustomerNotFound;
            if (string.IsNullOrWhiteSpace(req.CustomerId))
                return DebtConstants.CustomerNotFound;

            var missing = new List<string>();
            string number = (req.VoucherNumber ?? "").Trim();
            if (number.Length == 0)
                missing.Add("Số phiếu");
            if (string.IsNullOrWhiteSpace(req.StoreId))
                missing.Add("Cửa hàng");
            if (missing.Count > 0)
                return "Mời bạn nhập: " + string.Join(", ", missing.ToArray());

            if (req.Transfer && string.IsNullOrWhiteSpace(req.BankAccountId))
                return VoucherConstants.BankAccountMissing;

            string lenErr = CheckLength(number, "Số phiếu")
                ?? CheckLength(req.Description, "Diễn giải")
                ?? CheckLength(req.Note, "Ghi chú");
            if (lenErr != null)
                return lenErr;

            if (req.Allocations == null || req.Allocations.Count == 0)
                return DebtConstants.CollectTotalMustBePositive;

            var seenBills = new HashSet<string>(StringComparer.Ordinal);
            decimal sum = 0m;
            foreach (DebtAllocationLine line in req.Allocations)
            {
                if (line == null)
                    continue;
                if (line.PayAmount < 0m)
                    return DebtConstants.AllocationNegative;
                if (line.PayAmount == 0m)
                    continue;
                if (string.IsNullOrWhiteSpace(line.BillId))
                    return DebtConstants.BillNotFound;
                if (!seenBills.Add(line.BillId))
                    return DebtConstants.AllocationBillDuplicated;
                if (line.BillRemaining >= 0m && line.PayAmount > line.BillRemaining)
                    return DebtConstants.AllocationExceedsDebt(line.BillNumber, line.BillRemaining, line.PayAmount);
                sum += line.PayAmount;
            }

            if (sum <= 0m)
                return DebtConstants.CollectTotalMustBePositive;

            totalAmount = sum;
            return null;
        }

        /// <summary>Tạo bộ giá trị điền sẵn khi mở PhieuThuDialog (loại chi) để chi trả nợ NCC (spec §4).</summary>
        public static SupplierDebtPayPreset BuildSupplierPayPreset(DebtPartnerRow supplier)
        {
            if (supplier == null)
                return null;
            string name = (supplier.Name ?? "").Trim();
            return new SupplierDebtPayPreset
            {
                SupplierId = supplier.Id,
                SupplierName = name,
                SupplierAddress = (supplier.Address ?? "").Trim(),
                Amount = supplier.RemainingDebt > 0m ? supplier.RemainingDebt : 0m,
                Description = DebtConstants.ClipText("Chi trả nợ nhà cung cấp " + name)
            };
        }

        // ------------------------------------------------------------------ Quyền chức năng (§5)

        public static bool CanViewCustomerDebt(UserSession session)
        {
            return AppSession.Can(session, DebtConstants.CustomerDebtFunctionName, RoleModes.View);
        }

        public static bool CanViewSupplierDebt(UserSession session)
        {
            return AppSession.Can(session, DebtConstants.SupplierDebtFunctionName, RoleModes.View);
        }

        public static bool CanCollectCustomerDebt(UserSession session)
        {
            return VoucherRules.CanVoucher(session, RoleModes.Add);
        }

        public static bool CanPaySupplierDebt(UserSession session)
        {
            return VoucherRules.CanVoucher(session, RoleModes.Add);
        }

        public static bool CanChangeVoucherDate(UserSession session)
        {
            return VoucherRules.CanChangeDate(session);
        }

        private static string CheckLength(string value, string label)
        {
            if (value != null && value.Length > DebtConstants.MaxTextLength)
                return VoucherConstants.FieldTooLong(label, DebtConstants.MaxTextLength);
            return null;
        }
    }
}
