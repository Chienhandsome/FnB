using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần (không đụng DB, không đụng UI) cho chức năng Nhập hàng vào kho
    /// (docs/specs/phieu-nhap-kho.md §2, §3, §5).
    /// </summary>
    public static class PurchaseRules
    {
        /// <summary>
        /// Quy đổi số lượng về ĐVT cơ sở (SLNHAP):
        /// khi mặt hàng có 2 ĐVT (DDONVITINHCHANID khác DDONVITINHID, QUYDOI &gt; 0) và dòng chọn ĐVT chẵn
        /// thì SLNHAP = QUYDOI * SLNHAPCHUAQUYDOI; ngược lại SLNHAP = SLNHAPCHUAQUYDOI.
        /// </summary>
        public static decimal CalcConvertedQuantity(
            decimal rawQuantity,
            string selectedUnitId,
            string baseUnitId,
            string bigUnitId,
            decimal quyDoi)
        {
            bool hasTwoUnits = !string.IsNullOrEmpty(bigUnitId)
                && !string.Equals(bigUnitId, baseUnitId, StringComparison.Ordinal)
                && quyDoi > 0m;
            bool isBigUnit = hasTwoUnits
                && !string.IsNullOrEmpty(selectedUnitId)
                && string.Equals(selectedUnitId, bigUnitId, StringComparison.Ordinal);
            return isBigUnit ? rawQuantity * quyDoi : rawQuantity;
        }

        /// <summary>Thành tiền một dòng = Round(SL * DONGIA * (1 - TILEGIAMGIA / 100), 0).</summary>
        public static decimal CalcLineAmount(decimal rawQuantity, decimal unitPrice, decimal discountPct)
        {
            return Math.Round(rawQuantity * unitPrice * (1m - discountPct / 100m), 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Sửa Thành tiền thì suy ngược DONGIA (0 khi SL = 0 hoặc giảm 100%).</summary>
        public static decimal CalcUnitPriceFromAmount(decimal rawQuantity, decimal discountPct, decimal amount)
        {
            if (rawQuantity == 0m || discountPct >= 100m)
                return 0m;
            decimal factor = rawQuantity * (1m - discountPct / 100m);
            if (factor == 0m)
                return 0m;
            return Math.Round(amount / factor, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Cập nhật lại SLNHAP và THANHTIEN của một dòng chi tiết.</summary>
        public static void RecalcLine(PurchaseLine line)
        {
            if (line == null)
                return;
            line.ConvertedQuantity = CalcConvertedQuantity(
                line.RawQuantity, line.UnitId, line.BaseUnitId, line.BigUnitId, line.QuyDoi);
            line.Amount = CalcLineAmount(line.RawQuantity, line.UnitPrice, line.DiscountPct);
        }

        /// <summary>
        /// Tính tổng phiếu nhập kho (spec §2):
        /// - TIENHANG = Σ THANHTIEN
        /// - Giảm theo %: TIENGIAMGIA = 100 * Round(TIENHANG * TL / 10000)
        /// - Giảm theo tiền: giữ nguyên TIENGIAMGIA, suy ra TILEGIAMGIA = TIENHANG == 0 ? 0 : 100 * TIENGIAMGIA / TIENHANG
        /// - TIENTHUE = TILETHUE * (TIENHANG - TIENGIAMGIA) / 100
        /// - TONGCONG = TIENHANG - TIENGIAMGIA + TIENTHUE + PHIVANCHUYEN (không làm tròn thêm).
        /// </summary>
        public static PurchaseTotals CalcTotals(
            IEnumerable<PurchaseLine> lines,
            decimal tileGiamGia,
            decimal tienGiamGia,
            bool giamTheoTien,
            decimal tileThue,
            decimal phiVanChuyen)
        {
            decimal tienHang = 0m;
            if (lines != null)
            {
                foreach (PurchaseLine line in lines)
                {
                    if (line == null)
                        continue;
                    tienHang += CalcLineAmount(line.RawQuantity, line.UnitPrice, line.DiscountPct);
                }
            }
            return CalcTotalsFromSubtotal(tienHang, tileGiamGia, tienGiamGia, giamTheoTien, tileThue, phiVanChuyen);
        }

        public static PurchaseTotals CalcTotalsFromSubtotal(
            decimal tienHang,
            decimal tileGiamGia,
            decimal tienGiamGia,
            bool giamTheoTien,
            decimal tileThue,
            decimal phiVanChuyen)
        {
            decimal outTile;
            decimal outTienGiam;
            if (giamTheoTien)
            {
                outTienGiam = tienGiamGia;
                outTile = tienHang == 0m ? 0m : Math.Round(100m * outTienGiam / tienHang, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                outTile = tileGiamGia;
                outTienGiam = 100m * Math.Round(tienHang * tileGiamGia / 10000m, 0, MidpointRounding.AwayFromZero);
            }

            decimal tienThue = tileThue * (tienHang - outTienGiam) / 100m;
            decimal tongCong = tienHang - outTienGiam + tienThue + phiVanChuyen;

            return new PurchaseTotals
            {
                TienHang = tienHang,
                TileGiamGia = outTile,
                TienGiamGia = outTienGiam,
                TienThue = tienThue,
                TongCong = tongCong
            };
        }

        /// <summary>
        /// Thêm mặt hàng vào danh sách dòng: cùng mặt hàng + cùng đơn giá + cùng ĐVT thì gộp dòng
        /// (cộng số lượng), trừ khi bật cho phép nhập một mặt hàng nhiều lần.
        /// Trả về thông báo lỗi nếu SL &lt;= 0; null nếu thêm/gộp thành công.
        /// </summary>
        public static string AddOrMergeLine(
            List<PurchaseLine> lines,
            PurchaseProductItem item,
            string unitId,
            string unitName,
            decimal rawQuantity,
            decimal unitPrice,
            bool allowMultipleRows)
        {
            if (lines == null || item == null)
                return "Chưa chọn mặt hàng.";
            if (rawQuantity <= 0m)
                return PurchaseConstants.QtyMustBePositive;

            string effectiveUnitId = string.IsNullOrEmpty(unitId) ? item.UnitId : unitId;
            string effectiveUnitName = !string.IsNullOrEmpty(unitName)
                ? unitName
                : (string.Equals(effectiveUnitId, item.BigUnitId, StringComparison.Ordinal) ? item.BigUnitName : item.UnitName);

            if (!allowMultipleRows)
            {
                PurchaseLine existing = lines.Find(l =>
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

            var line = new PurchaseLine
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
                DiscountPct = 0m,
                Note = ""
            };
            RecalcLine(line);
            lines.Add(line);
            return null;
        }

        /// <summary>Kiểm tra đầu vào phiếu nhập kho trước khi lưu (spec §3).</summary>
        public static string Validate(PurchaseRow row, bool requireSupplier)
        {
            if (row == null)
                return PurchaseConstants.NotFound;

            string number = (row.Name ?? "").Trim();
            if (number.Length == 0)
                return PurchaseConstants.NumberRequired;
            if (number.Length > PurchaseConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Số phiếu", PurchaseConstants.MaxTextLength);

            if (string.IsNullOrWhiteSpace(row.WarehouseId))
                return PurchaseConstants.WarehouseRequired;

            if (requireSupplier && string.IsNullOrWhiteSpace(row.SupplierId))
                return PurchaseConstants.SupplierRequired;

            if (row.Description != null && row.Description.Length > PurchaseConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Diễn giải", PurchaseConstants.MaxTextLength);
            if (row.Note != null && row.Note.Length > PurchaseConstants.MaxTextLength)
                return PurchaseConstants.FieldTooLong("Ghi chú", PurchaseConstants.MaxTextLength);

            if (row.Lines == null || row.Lines.Count == 0)
                return PurchaseConstants.LinesRequired;

            foreach (PurchaseLine l in row.Lines)
            {
                if (l == null || string.IsNullOrWhiteSpace(l.ProductId))
                    return "Có dòng chi tiết chưa chọn mặt hàng.";
                decimal converted = CalcConvertedQuantity(l.RawQuantity, l.UnitId, l.BaseUnitId, l.BigUnitId, l.QuyDoi);
                if (l.RawQuantity <= 0m || converted <= 0m)
                    return PurchaseConstants.QtyMustBePositive;
                if (l.UnitPrice < 0m)
                    return "Đơn giá không được âm.";
                if (l.DiscountPct < 0m || l.DiscountPct > 100m)
                    return "Tỉ lệ giảm giá dòng phải nằm trong khoảng 0 đến 100%.";
            }

            if (row.PaidAmount < 0m)
                return FundText.NegativeAmount;

            if (row.PaidAmount > 0m)
            {
                PayMethod? method = row.PaymentMethod ?? PayMethod.Cash;
                FundStockPaymentResult payRes = FundRules.StockPaymentFields(method, row.PaidAmount, row.BankAccountId);
                if (!string.IsNullOrEmpty(payRes.Error))
                    return payRes.Error;
            }

            return null;
        }

        /// <summary>Lọc nhanh danh sách phiếu nhập theo chuỗi tìm (không phân biệt hoa/thường/dấu tiếng Việt).</summary>
        public static bool MatchesSearch(PurchaseRow row, string query)
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
                row.SupplierName ?? "",
                row.EmployeeName ?? "",
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

        /// <summary>Cắt chuỗi cho vừa cột VARCHAR có giới hạn (mặc định 255 ký tự, kể cả lưu vết TLUUVET).</summary>
        public static string Clip(string text, int maxLength = PurchaseConstants.MaxTextLength)
        {
            string s = text ?? "";
            if (maxLength > 0 && s.Length > maxLength)
                return s.Substring(0, maxLength);
            return s;
        }

        public static bool Can(UserSession session, int bit)
        {
            return AppSession.Can(session, PurchaseConstants.FunctionName, bit);
        }

        public static bool CanChangeDate(UserSession session)
        {
            return AppSession.Can(session, PurchaseConstants.FunctionDateChange, RoleModes.View);
        }

        public static bool CanViewBuyPrice(UserSession session)
        {
            return AppSession.Can(session, PurchaseConstants.FunctionViewBuyPrice, RoleModes.View);
        }
    }
}
