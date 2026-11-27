using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không đụng DB, không đụng UI) cho Xuất khác (LOAI = 2) và Chuyển kho (LOAI = 3)
    /// (docs/specs/phieu-xuat-chuyen-kho.md §2, §3, §6).
    /// Tái sử dụng toàn bộ công thức quy đổi ĐVT, tính dòng, tính tổng và cắt chuỗi của <see cref="PurchaseRules"/>.
    /// </summary>
    public static class StockDocumentRules
    {
        /// <summary>
        /// Đơn giá mặc định khi thêm dòng theo cấu hình GiaSuDungKhiChuyenXuatKho:
        /// 0 = GIABAN (mặc định), 1 = GIANHAP, 2 = GIAVON.
        /// </summary>
        public static decimal ResolveDefaultPrice(StockProductItem item, int priceMode)
        {
            if (item == null)
                return 0m;
            return item.GetPriceForMode(priceMode);
        }

        /// <summary>
        /// Cập nhật lại số lượng đã quy đổi (SLXUAT / SLNHAP) và THANHTIEN = Round(SL * DONGIA, 0)
        /// bằng đúng hàm của <see cref="PurchaseRules"/> (không có chiết khấu dòng).
        /// </summary>
        public static void RecalcLine(StockDocumentLine line)
        {
            if (line == null)
                return;
            line.ConvertedQuantity = PurchaseRules.CalcConvertedQuantity(
                line.RawQuantity, line.UnitId, line.BaseUnitId, line.BigUnitId, line.QuyDoi);
            line.Amount = PurchaseRules.CalcLineAmount(line.RawQuantity, line.UnitPrice, 0m);
        }

        /// <summary>Sửa Thành tiền thì suy ngược DONGIA (dùng lại PurchaseRules với chiết khấu dòng = 0).</summary>
        public static decimal CalcUnitPriceFromAmount(decimal rawQuantity, decimal amount)
        {
            return PurchaseRules.CalcUnitPriceFromAmount(rawQuantity, 0m, amount);
        }

        /// <summary>
        /// Tính tổng đầu phiếu bằng đúng công thức <see cref="PurchaseRules.CalcTotalsFromSubtotal"/>:
        /// - Xuất khác (LOAI = 2): ép TILETHUE = TIENTHUE = PHIVANCHUYEN = 0; TONGCONG = TIENHANG - TIENGIAMGIA.
        /// - Chuyển kho (LOAI = 3): giữ thuế và phí vận chuyển như phiếu nhập (Chiến chốt 27/11).
        /// </summary>
        public static PurchaseTotals CalcTotals(
            StockDocumentKind kind,
            IEnumerable<StockDocumentLine> lines,
            decimal tileGiamGia,
            decimal tienGiamGia,
            bool giamTheoTien,
            decimal tileThue,
            decimal phiVanChuyen)
        {
            decimal tienHang = 0m;
            if (lines != null)
            {
                foreach (StockDocumentLine line in lines)
                {
                    if (line == null)
                        continue;
                    tienHang += PurchaseRules.CalcLineAmount(line.RawQuantity, line.UnitPrice, 0m);
                }
            }
            decimal effTileThue = kind == StockDocumentKind.Export ? 0m : tileThue;
            decimal effPhiVc = kind == StockDocumentKind.Export ? 0m : phiVanChuyen;
            return PurchaseRules.CalcTotalsFromSubtotal(
                tienHang, tileGiamGia, tienGiamGia, giamTheoTien, effTileThue, effPhiVc);
        }

        /// <summary>
        /// Thêm mặt hàng vào danh sách dòng: cùng mặt hàng + cùng đơn giá + cùng ĐVT thì gộp dòng
        /// (cộng số lượng), trừ khi bật cho phép nhập một mặt hàng nhiều lần.
        /// </summary>
        public static string AddOrMergeLine(
            List<StockDocumentLine> lines,
            StockProductItem item,
            string unitId,
            string unitName,
            decimal rawQuantity,
            decimal unitPrice,
            bool allowMultipleRows,
            StockDocumentKind kind)
        {
            if (lines == null || item == null)
                return "Chưa chọn mặt hàng.";
            if (rawQuantity <= 0m)
                return StockDocumentConstants.QtyMustBePositiveOf(kind);

            string effectiveUnitId = string.IsNullOrEmpty(unitId) ? item.UnitId : unitId;
            string effectiveUnitName = !string.IsNullOrEmpty(unitName)
                ? unitName
                : (string.Equals(effectiveUnitId, item.BigUnitId, StringComparison.Ordinal) ? item.BigUnitName : item.UnitName);

            if (!allowMultipleRows)
            {
                StockDocumentLine existing = lines.Find(l =>
                    l != null
                    && string.Equals(l.ProductId, item.Id, StringComparison.Ordinal)
                    && string.Equals(l.UnitId ?? "", effectiveUnitId ?? "", StringComparison.Ordinal)
                    && l.UnitPrice == unitPrice);
                if (existing != null)
                {
                    existing.RawQuantity += rawQuantity;
                    RecalcLine(existing);
                    return null;
                }
            }

            var line = new StockDocumentLine
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
                RawQuantity = rawQuantity,
                UnitPrice = unitPrice,
                Note = ""
            };
            RecalcLine(line);
            lines.Add(line);
            return null;
        }

        /// <summary>Kiểm tra đầu vào phiếu Xuất khác / Chuyển kho trước khi lưu (spec §2, §3).</summary>
        public static string Validate(StockDocumentRow row, StockDocumentKind kind)
        {
            if (row == null)
                return StockDocumentConstants.NotFoundOf(kind);

            string number = (row.Name ?? "").Trim();
            if (number.Length == 0)
                return PurchaseConstants.NumberRequired;
            if (number.Length > StockDocumentConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Số phiếu", StockDocumentConstants.MaxTextLength);

            if (string.IsNullOrWhiteSpace(row.SourceWarehouseId))
                return StockDocumentConstants.SourceWarehouseRequired;

            if (kind == StockDocumentKind.Transfer)
            {
                if (string.IsNullOrWhiteSpace(row.DestWarehouseId))
                    return StockDocumentConstants.DestWarehouseRequired;
                if (string.Equals(row.SourceWarehouseId.Trim(), row.DestWarehouseId.Trim(), StringComparison.Ordinal))
                    return StockDocumentConstants.SameWarehouseBlocked;
            }

            if (row.Description != null && row.Description.Length > StockDocumentConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Diễn giải", StockDocumentConstants.MaxTextLength);
            if (row.Note != null && row.Note.Length > StockDocumentConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Ghi chú", StockDocumentConstants.MaxTextLength);

            if (row.Lines == null || row.Lines.Count == 0)
                return StockDocumentConstants.LinesRequiredOf(kind);

            foreach (StockDocumentLine l in row.Lines)
            {
                if (l == null || string.IsNullOrWhiteSpace(l.ProductId))
                    return "Có dòng chi tiết chưa chọn mặt hàng.";
                decimal converted = PurchaseRules.CalcConvertedQuantity(
                    l.RawQuantity, l.UnitId, l.BaseUnitId, l.BigUnitId, l.QuyDoi);
                if (l.RawQuantity <= 0m || converted <= 0m)
                    return StockDocumentConstants.QtyMustBePositiveOf(kind);
                if (l.UnitPrice < 0m)
                    return "Đơn giá không được âm.";
            }

            return null;
        }

        /// <summary>Lọc nhanh danh sách phiếu theo chuỗi tìm (không phân biệt hoa/thường/dấu tiếng Việt).</summary>
        public static bool MatchesSearch(StockDocumentRow row, string query)
        {
            if (row == null)
                return false;
            string q = CatalogRepository.Fold((query ?? "").Trim());
            if (q.Length == 0)
                return true;

            string[] fields =
            {
                row.Name ?? "",
                row.SourceWarehouseName ?? "",
                row.DestWarehouseName ?? "",
                row.SupplierName ?? "",
                row.ExportEmployeeName ?? "",
                row.ImportEmployeeName ?? "",
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

        public static bool Can(UserSession session, StockDocumentKind kind, int bit)
        {
            return AppSession.Can(session, StockDocumentConstants.FunctionNameOf(kind), bit);
        }

        public static bool CanChangeDate(UserSession session)
        {
            return PurchaseRules.CanChangeDate(session);
        }
    }
}

