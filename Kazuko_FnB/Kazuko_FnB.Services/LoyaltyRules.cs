using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không đụng DB, không đụng UI) cho màn Khách hàng thân thiết
    /// và phiếu tặng/giảm điểm (docs/specs/khach-hang-than-thiet.md §2–§3).
    /// </summary>
    public static class LoyaltyRules
    {
        /// <summary>
        /// Tính điểm tích lũy theo công thức riêng của màn Khách hàng thân thiết (spec §2):
        /// - CachTinhDiem = 1: DIEMTICHLUYBANDAU + Σ(DIEM − DIEMGIAM) + Σ(DIEMTANG − DIEMGIAM).
        /// - CachTinhDiem != 1: DIEMTICHLUYBANDAU + (DoanhSo1Diem == 0 ? 0 : Σ TONGCONG / DoanhSo1Diem) − Σ DIEMGIAM + Σ(DIEMTANG − DIEMGIAM).
        /// </summary>
        public static decimal ComputePoints(
            decimal initialPoints,
            decimal billRevenue,
            decimal billEarnedPoints,
            decimal billUsedPoints,
            decimal manualNetPoints,
            int cachTinhDiem,
            int doanhSo1Diem)
        {
            return LoyaltyRepository.ComputeScreenPoints(
                initialPoints, billRevenue, billEarnedPoints, billUsedPoints, manualNetPoints, cachTinhDiem, doanhSo1Diem);
        }

        /// <summary>Làm tròn xuống số thập phân về số nguyên (chỉ áp với số không âm; số âm giữ nguyên để chặn).</summary>
        public static decimal NormalizePointInput(decimal value)
        {
            return value >= 0m ? Math.Floor(value) : value;
        }

        /// <summary>
        /// Kiểm tra hợp lệ đầu vào của phiếu tặng/giảm điểm (spec §3):
        /// số phiếu, khách hàng, lý do bắt buộc (<= 255), ghi chú <= 255;
        /// điểm tặng &amp; giảm >= 0, làm tròn xuống số nguyên, tối đa 9 chữ số, không cùng bằng 0.
        /// </summary>
        public static LoyaltySaveResult ValidateVoucher(LoyaltyPointVoucherRow row)
        {
            if (row == null)
                return LoyaltySaveResult.Fail(LoyaltyConstants.VoucherNotFound);

            string number = (row.Number ?? "").Trim();
            if (number.Length == 0)
                return new LoyaltySaveResult { Ok = false, HasNumberError = true, ErrorMessage = LoyaltyConstants.NumberRequired };
            if (number.Length > LoyaltyConstants.MaxTextLength)
                return new LoyaltySaveResult { Ok = false, HasNumberError = true, ErrorMessage = LoyaltyConstants.FieldTooLong("Số phiếu", LoyaltyConstants.MaxTextLength) };

            if (string.IsNullOrWhiteSpace(row.CustomerId))
                return new LoyaltySaveResult { Ok = false, HasCustomerError = true, ErrorMessage = LoyaltyConstants.CustomerRequired };

            decimal gift = NormalizePointInput(row.GiftPoints);
            decimal reduce = NormalizePointInput(row.ReducePoints);
            if (gift < 0m || reduce < 0m)
                return new LoyaltySaveResult { Ok = false, HasPointsError = true, ErrorMessage = LoyaltyConstants.PointsNegative };
            if (gift > LoyaltyConstants.MaxPointValue || reduce > LoyaltyConstants.MaxPointValue)
                return new LoyaltySaveResult { Ok = false, HasPointsError = true, ErrorMessage = LoyaltyConstants.PointsTooLarge };
            if (gift == 0m && reduce == 0m)
                return new LoyaltySaveResult { Ok = false, HasPointsError = true, ErrorMessage = LoyaltyConstants.PointsBothZero };

            string reason = (row.Reason ?? "").Trim();
            if (reason.Length == 0)
                return new LoyaltySaveResult { Ok = false, HasReasonError = true, ErrorMessage = LoyaltyConstants.ReasonRequired };
            if (reason.Length > LoyaltyConstants.MaxTextLength)
                return new LoyaltySaveResult { Ok = false, HasReasonError = true, ErrorMessage = LoyaltyConstants.FieldTooLong("Lý do", LoyaltyConstants.MaxTextLength) };

            string note = (row.Note ?? "").Trim();
            if (note.Length > LoyaltyConstants.MaxTextLength)
                return new LoyaltySaveResult { Ok = false, HasNoteError = true, ErrorMessage = LoyaltyConstants.FieldTooLong("Ghi chú", LoyaltyConstants.MaxTextLength) };

            return new LoyaltySaveResult { Ok = true };
        }

        /// <summary>
        /// Chặn giảm điểm vượt điểm hiện có của khách (đã bỏ chính phiếu đang sửa) cộng điểm tặng của chính phiếu (spec §3).
        /// </summary>
        public static string ValidateReduceWithinBalance(decimal availableWithoutThisVoucher, decimal giftPoints, decimal reducePoints)
        {
            if (reducePoints <= 0m)
                return null;
            decimal maxAvailable = availableWithoutThisVoucher + giftPoints;
            if (reducePoints > maxAvailable)
                return LoyaltyConstants.ReduceExceedsPoints(maxAvailable);
            return null;
        }

        /// <summary>Kiểm tra một khách hàng có khớp bộ lọc (cây nhóm, tìm không dấu tên/mã/SĐT/địa chỉ, chỉ khách có điểm).</summary>
        public static bool MatchesFilter(LoyaltyCustomerRow row, LoyaltyFilter filter)
        {
            if (row == null)
                return false;
            if (filter == null)
                return true;

            if (filter.OnlyWithPoints && row.Points <= 0m)
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
                        row.Name ?? "",
                        row.Code ?? "",
                        row.Phone ?? "",
                        row.Address ?? ""
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

        /// <summary>Mở rộng một nhóm trên cây thành tập gồm chính nó và mọi nhóm con cháu.</summary>
        public static HashSet<string> CollectGroupSubtree(IList<LoyaltyGroupNode> allGroups, string rootGroupId)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(rootGroupId))
                return ids;

            ids.Add(rootGroupId);
            if (allGroups == null || allGroups.Count == 0)
                return ids;

            var byParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (LoyaltyGroupNode g in allGroups)
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

        /// <summary>Tổng hợp hàng tổng cộng cho lưới khách hàng thân thiết.</summary>
        public static LoyaltyTotals ComputeCustomerTotals(IEnumerable<LoyaltyCustomerRow> rows)
        {
            var totals = new LoyaltyTotals();
            if (rows == null)
                return totals;
            foreach (LoyaltyCustomerRow r in rows)
            {
                if (r == null)
                    continue;
                totals.CustomerCount++;
                totals.TotalRevenue += r.Revenue;
                totals.TotalBillCount += r.BillCount;
                totals.TotalPoints += r.Points;
            }
            return totals;
        }

        /// <summary>Tổng hợp hàng tổng cộng cho tab Hóa đơn.</summary>
        public static LoyaltyBillTotals ComputeBillTotals(IEnumerable<LoyaltyBillRow> rows)
        {
            var totals = new LoyaltyBillTotals();
            if (rows == null)
                return totals;
            foreach (LoyaltyBillRow r in rows)
            {
                if (r == null)
                    continue;
                totals.Count++;
                totals.TotalPoints += r.Points;
                totals.TotalAmount += r.TotalAmount;
                totals.TotalPointsUsed += r.PointsUsed;
            }
            return totals;
        }

        /// <summary>Tổng hợp hàng tổng cộng cho tab Tặng điểm - tặng quà.</summary>
        public static LoyaltyPointVoucherTotals ComputeVoucherTotals(IEnumerable<LoyaltyPointVoucherRow> rows)
        {
            var totals = new LoyaltyPointVoucherTotals();
            if (rows == null)
                return totals;
            foreach (LoyaltyPointVoucherRow r in rows)
            {
                if (r == null)
                    continue;
                totals.Count++;
                totals.TotalGiftPoints += r.GiftPoints;
                totals.TotalReducePoints += r.ReducePoints;
            }
            return totals;
        }

        // ------------------------------------------------------------------ Quyền chức năng (§2, §3)

        public static bool CanView(UserSession session)
        {
            return AppSession.Can(session, LoyaltyConstants.FunctionName, RoleModes.View);
        }

        public static bool CanAdd(UserSession session)
        {
            return AppSession.Can(session, LoyaltyConstants.FunctionName, RoleModes.Add);
        }

        public static bool CanEdit(UserSession session)
        {
            return AppSession.Can(session, LoyaltyConstants.FunctionName, RoleModes.Edit);
        }

        public static bool CanRemove(UserSession session)
        {
            return AppSession.Can(session, LoyaltyConstants.FunctionName, RoleModes.Remove);
        }

        public static bool CanChangeDate(UserSession session)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return AppSession.Can(session, LoyaltyConstants.FunctionDateChange, RoleModes.View)
                || AppSession.Can(session, PurchaseConstants.FunctionDateChange, RoleModes.View)
                || AppSession.Can(session, VoucherConstants.FunctionDateChange, RoleModes.View);
        }
    }
}
