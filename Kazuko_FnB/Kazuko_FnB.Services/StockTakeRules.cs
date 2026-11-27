using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không đụng DB, không đụng UI) cho chức năng Kiểm kê kho (TDONHANG LOAI = 4)
    /// theo docs/specs/kiem-ke-kho.md §2, §3, §5.
    /// Tái sử dụng hàm quy đổi ĐVT <see cref="PurchaseRules.CalcConvertedQuantity"/> và cắt chuỗi <see cref="PurchaseRules.Clip"/>.
    /// </summary>
    public static class StockTakeRules
    {
        public static StockTakeDiffTone ResolveTone(decimal difference)
        {
            if (difference < 0m)
                return StockTakeDiffTone.Shortage;
            if (difference > 0m)
                return StockTakeDiffTone.Surplus;
            return StockTakeDiffTone.Match;
        }

        /// <summary>
        /// Quy đổi số lượng thực tế về ĐVT cơ sở bằng đúng hàm dùng chung <see cref="PurchaseRules.CalcConvertedQuantity"/>,
        /// rồi tính chênh lệch và SLNHAP/SLXUAT:
        /// - SLXUAT = SLXUATCHUAQUYDOI = max(0, SLHETHONG - SLTHUCTE)
        /// - SLNHAP = SLNHAPCHUAQUYDOI = max(0, SLTHUCTE - SLHETHONG)
        /// </summary>
        public static void RecalcLine(StockTakeLine line)
        {
            if (line == null)
                return;
            line.ActualQuantityBase = PurchaseRules.CalcConvertedQuantity(
                line.RawActualQuantity, line.UnitId, line.BaseUnitId, line.BigUnitId, line.QuyDoi);
            line.Difference = line.ActualQuantityBase - line.SystemQuantity;
            line.ImportQty = line.Difference > 0m ? line.Difference : 0m;
            line.ExportQty = line.Difference < 0m ? -line.Difference : 0m;
            line.Tone = ResolveTone(line.Difference);
        }

        /// <summary>
        /// Thêm mặt hàng vào danh sách dòng kiểm kê (spec §2, §6 #3):
        /// - Cùng mặt hàng + cùng ĐVT: cộng dồn số lượng thực tế (gộp dòng).
        /// - Cùng mặt hàng + khác ĐVT (vd. thùng rồi chai): quy đổi cả hai về ĐVT cơ sở rồi gộp về 1 dòng để tránh cộng/trừ tồn hai lần.
        /// - Cho phép số lượng thực tế bằng 0; chặn số âm.
        /// </summary>
        public static string AddOrMergeLine(
            List<StockTakeLine> lines,
            StockTakeProductItem item,
            string unitId,
            string unitName,
            decimal rawActualQuantity,
            decimal systemQuantity)
        {
            if (lines == null || item == null)
                return "Chưa chọn mặt hàng.";
            if (rawActualQuantity < 0m)
                return StockTakeConstants.ActualQtyNegative;

            string effectiveUnitId = string.IsNullOrEmpty(unitId) ? item.UnitId : unitId;
            string effectiveUnitName = !string.IsNullOrEmpty(unitName)
                ? unitName
                : (string.Equals(effectiveUnitId, item.BigUnitId, StringComparison.Ordinal) ? item.BigUnitName : item.UnitName);

            StockTakeLine existing = lines.Find(l =>
                l != null && string.Equals(l.ProductId, item.Id, StringComparison.Ordinal));
            if (existing != null)
            {
                if (string.Equals(existing.UnitId ?? "", effectiveUnitId ?? "", StringComparison.Ordinal))
                {
                    existing.RawActualQuantity += rawActualQuantity;
                }
                else
                {
                    RecalcLine(existing);
                    decimal addedBase = PurchaseRules.CalcConvertedQuantity(
                        rawActualQuantity, effectiveUnitId, item.UnitId, item.BigUnitId, item.QuyDoi);
                    existing.UnitId = item.UnitId;
                    existing.UnitName = item.UnitName ?? "";
                    existing.RawActualQuantity = existing.ActualQuantityBase + addedBase;
                }
                RecalcLine(existing);
                return null;
            }

            var line = new StockTakeLine
            {
                ProductId = item.Id,
                ProductCode = item.Code ?? "",
                ProductName = item.Name ?? "",
                UnitId = effectiveUnitId,
                UnitName = effectiveUnitName ?? "",
                BaseUnitId = item.UnitId,
                BaseUnitName = item.UnitName ?? "",
                BigUnitId = item.BigUnitId,
                BigUnitName = item.BigUnitName ?? "",
                QuyDoi = item.QuyDoi,
                SystemQuantity = systemQuantity,
                RawActualQuantity = rawActualQuantity,
                Note = ""
            };
            RecalcLine(line);
            lines.Add(line);
            return null;
        }

        /// <summary>Kiểm tra đầu vào phiếu kiểm kê kho trước khi lưu (spec §2, §3, §6 #3).</summary>
        public static string Validate(StockTakeRow row)
        {
            if (row == null)
                return StockTakeConstants.NotFound;

            string number = (row.Name ?? "").Trim();
            if (number.Length == 0)
                return PurchaseConstants.NumberRequired;
            if (number.Length > StockTakeConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Số phiếu", StockTakeConstants.MaxTextLength);

            if (string.IsNullOrWhiteSpace(row.WarehouseId))
                return StockTakeConstants.WarehouseRequired;

            if (row.Description != null && row.Description.Length > StockTakeConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Diễn giải", StockTakeConstants.MaxTextLength);
            if (row.Note != null && row.Note.Length > StockTakeConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Ghi chú", StockTakeConstants.MaxTextLength);

            if (row.Lines == null || row.Lines.Count == 0)
                return StockTakeConstants.LinesRequired;

            var seenProducts = new HashSet<string>(StringComparer.Ordinal);
            foreach (StockTakeLine l in row.Lines)
            {
                if (l == null || string.IsNullOrWhiteSpace(l.ProductId))
                    return "Có dòng chi tiết chưa chọn mặt hàng.";
                if (!seenProducts.Add(l.ProductId))
                    return StockTakeConstants.DuplicateProductInLines(l.ProductName);
                decimal converted = PurchaseRules.CalcConvertedQuantity(
                    l.RawActualQuantity, l.UnitId, l.BaseUnitId, l.BigUnitId, l.QuyDoi);
                if (l.RawActualQuantity < 0m || converted < 0m)
                    return StockTakeConstants.ActualQtyNegative;
            }

            return null;
        }

        /// <summary>Lọc nhanh danh sách phiếu kiểm kê theo chuỗi tìm (không phân biệt hoa/thường/dấu tiếng Việt).</summary>
        public static bool MatchesSearch(StockTakeRow row, string query)
        {
            if (row == null)
                return false;
            string q = CatalogRepository.Fold((query ?? "").Trim());
            if (q.Length == 0)
                return true;

            string[] fields =
            {
                row.Name ?? "",
                row.WarehouseName ?? "",
                row.Description ?? "",
                row.Note ?? ""
            };
            for (int i = 0; i < fields.Length; i++)
            {
                if (CatalogRepository.Fold(fields[i]).IndexOf(q, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        public static bool Can(UserSession session, int bit)
        {
            return AppSession.Can(session, StockTakeConstants.FunctionName, bit);
        }

        public static bool CanChangeDate(UserSession session)
        {
            return PurchaseRules.CanChangeDate(session);
        }
    }
}
