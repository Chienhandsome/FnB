using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không đụng DB, không đụng UI) cho màn Tồn kho ban đầu
    /// (docs/specs/so-du-dau-ky.md §2, P1): ngày mặc định 31/12 năm trước, tính thành tiền,
    /// kiểm tra hợp lệ (chặn số âm nêu tên mặt hàng), lọc không dấu, tổng cộng và kiểm quyền.
    /// </summary>
    public static class OpeningStockRules
    {
        /// <summary>Ngày mặc định = 31/12 của năm trước.</summary>
        public static DateTime DefaultOpeningDate(DateTime? referenceDate = null)
        {
            DateTime d = (referenceDate ?? DateTime.Today).Date;
            int prevYear = d.Year > 1 ? d.Year - 1 : 1;
            return new DateTime(prevYear, 12, 31);
        }

        /// <summary>Thành tiền = Round(Tồn đầu × Giá vốn, 0) (spec §2).</summary>
        public static decimal CalcLineAmount(decimal quantity, decimal costPrice)
        {
            return Math.Round(quantity * costPrice, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Cập nhật Thành tiền cho một dòng.</summary>
        public static void RecalcItem(OpeningStockItemRow item)
        {
            if (item == null)
                return;
            item.Amount = CalcLineAmount(item.Quantity, item.CostPrice);
        }

        /// <summary>Tính tổng số lượng và tổng giá trị của danh sách dòng hiển thị (thuần decimal).</summary>
        public static OpeningStockTotals ComputeTotals(IEnumerable<OpeningStockItemRow> items)
        {
            var totals = new OpeningStockTotals();
            if (items == null)
                return totals;

            foreach (OpeningStockItemRow item in items)
            {
                if (item == null)
                    continue;
                totals.ItemCount++;
                if (item.Quantity > 0m)
                    totals.EnteredCount++;
                totals.TotalQuantity += item.Quantity;
                totals.TotalAmount += CalcLineAmount(item.Quantity, item.CostPrice);
            }
            return totals;
        }

        /// <summary>Kiểm tra một dòng có khớp bộ lọc (tích Chỉ hiện hàng đã nhập tồn + ô tìm không dấu).</summary>
        public static bool MatchesFilter(OpeningStockItemRow item, OpeningStockFilter filter)
        {
            if (item == null)
                return false;
            if (filter == null)
                return true;

            if (filter.OnlyEntered && item.Quantity <= 0m)
                return false;

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                string q = CatalogRepository.Fold(filter.SearchText.Trim());
                if (q.Length > 0)
                {
                    string codeFold = CatalogRepository.Fold(item.Code ?? "");
                    string nameFold = CatalogRepository.Fold(item.Name ?? "");
                    if (codeFold.IndexOf(q, StringComparison.Ordinal) < 0
                        && nameFold.IndexOf(q, StringComparison.Ordinal) < 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Kiểm tra đầu vào trước khi lưu (spec §2 T1):
        /// bắt buộc có kho; chặn Tồn đầu âm (báo rõ tên mặt hàng); chặn Giá vốn âm (báo rõ tên mặt hàng).
        /// </summary>
        public static string Validate(OpeningStockSaveRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.WarehouseId))
                return OpeningStockConstants.WarehouseRequired;

            if (request.Items == null)
                return null;

            foreach (OpeningStockItemRow item in request.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ProductId))
                    continue;
                string label = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : (item.Code ?? item.ProductId);
                if (item.Quantity < 0m)
                    return OpeningStockConstants.NegativeQtyBlocked(label);
                if (item.CostPrice < 0m)
                    return OpeningStockConstants.NegativeCostBlocked(label);
            }
            return null;
        }

        public static bool CanView(UserSession session)
        {
            return AppSession.Can(session, OpeningStockConstants.FunctionName, RoleModes.View);
        }

        public static bool CanEdit(UserSession session)
        {
            return AppSession.Can(session, OpeningStockConstants.FunctionName, RoleModes.Edit);
        }

        public static bool CanChangeDate(UserSession session)
        {
            return AppSession.Can(session, OpeningStockConstants.FunctionDateChange, RoleModes.View);
        }
    }
}
