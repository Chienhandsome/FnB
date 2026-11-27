using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không đụng DB, không đụng UI) cho Công nợ ban đầu
    /// của khách hàng (LOAI = 99) và nhà cung cấp (LOAI = 98) (docs/specs/so-du-dau-ky.md §3, P2):
    /// ngày mặc định 31/12 năm trước, lọc không dấu + chỉ hiện đối tác có nợ đầu,
    /// hàng tổng cộng, kiểm tra nợ âm nêu rõ tên đối tác và kiểm quyền.
    /// </summary>
    public static class OpeningDebtRules
    {
        /// <summary>Ngày mặc định = 31/12 của năm trước.</summary>
        public static DateTime DefaultOpeningDate(DateTime? referenceDate = null)
        {
            return OpeningStockRules.DefaultOpeningDate(referenceDate);
        }

        public static int OrderType(DebtPartyKind kind)
        {
            return kind == DebtPartyKind.Customer
                ? OpeningDebtConstants.CustomerOrderType
                : OpeningDebtConstants.SupplierOrderType;
        }

        public static string FunctionName(DebtPartyKind kind)
        {
            return kind == DebtPartyKind.Customer
                ? OpeningDebtConstants.CustomerFunctionName
                : OpeningDebtConstants.SupplierFunctionName;
        }

        /// <summary>Tính tổng nợ đầu kỳ của danh sách đối tác đang hiển thị (thuần decimal).</summary>
        public static OpeningDebtTotals ComputeTotals(IEnumerable<OpeningDebtPartnerRow> items)
        {
            var totals = new OpeningDebtTotals();
            if (items == null)
                return totals;

            foreach (OpeningDebtPartnerRow row in items)
            {
                if (row == null)
                    continue;
                totals.PartnerCount++;
                if (row.OpeningDebt > 0m)
                    totals.WithDebtCount++;
                totals.TotalOpeningDebt += row.OpeningDebt;
            }
            return totals;
        }

        /// <summary>Kiểm tra một đối tác có khớp bộ lọc (tích Chỉ hiện đối tác có nợ đầu + ô tìm không dấu).</summary>
        public static bool MatchesFilter(OpeningDebtPartnerRow row, OpeningDebtFilter filter)
        {
            if (row == null)
                return false;
            if (filter == null)
                return true;

            if (filter.OnlyWithDebt && row.OpeningDebt <= 0m)
                return false;

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
                        row.Phone ?? ""
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

        /// <summary>
        /// Kiểm tra hợp lệ trước khi lưu (spec §3, T1):
        /// chặn nợ đầu âm và nêu rõ tên đối tác.
        /// </summary>
        public static string Validate(OpeningDebtSaveRequest request)
        {
            if (request == null || request.Items == null)
                return null;

            foreach (OpeningDebtPartnerRow item in request.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.PartnerId))
                    continue;
                if (item.OpeningDebt < 0m)
                {
                    string label = !string.IsNullOrWhiteSpace(item.Name)
                        ? item.Name
                        : (!string.IsNullOrWhiteSpace(item.Code) ? item.Code : item.PartnerId);
                    return OpeningDebtConstants.NegativeDebtBlocked(label);
                }
            }
            return null;
        }

        public static bool CanView(UserSession session, DebtPartyKind kind)
        {
            return Can(session, kind, RoleModes.View);
        }

        public static bool CanEdit(UserSession session, DebtPartyKind kind)
        {
            return Can(session, kind, RoleModes.Edit);
        }

        public static bool CanChangeDate(UserSession session)
        {
            return AppSession.Can(session, OpeningDebtConstants.FunctionDateChange, RoleModes.View);
        }

        private static bool Can(UserSession session, DebtPartyKind kind, int bit)
        {
            if (kind == DebtPartyKind.Customer)
            {
                return AppSession.Can(session, OpeningDebtConstants.CustomerFunctionName, bit)
                    || AppSession.Can(session, OpeningDebtConstants.CustomerFunctionNameAlt, bit);
            }
            return AppSession.Can(session, OpeningDebtConstants.SupplierFunctionName, bit);
        }
    }
}
