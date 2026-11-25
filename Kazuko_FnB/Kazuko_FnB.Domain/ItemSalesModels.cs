using System;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số định danh, quyền và thông báo của màn "Thống kê mặt hàng bán" (docs/specs/thong-ke-mat-hang-ban.md).</summary>
    public static class ItemSalesConstants
    {
        public const string MenuId = "a5810c3b-e57a-4cca-99c7-ed89d5dff667";
        public const string FunctionId = "ff5edbec-dee4-4fe8-9315-30c96dec1c4e";
        public const string FunctionName = "Thống kê mặt hàng bán";

        public const string ViewCostFunctionId = "1edce531-ffba-4b06-be16-3d7d6bbedb9e";
        public const string ViewCostPermissionName = "Xem giá nhập";

        /// <summary>Mã loại mặt hàng nguyên liệu (DMATHANG.DLOAIMATHANGID = '2').</summary>
        public const string MaterialProductTypeId = "2";
        /// <summary>Mã loại đồ nguyên liệu trên nhóm mặt hàng (DNHOMMATHANG.DLOAIDOID = '4').</summary>
        public const string MaterialDrinkTypeId = "4";

        public const string ConfigSortOrderKey = "SapXepThuTuTheo";
        public const string ConfigUseCodeKey = "SuDungMaHang";

        public const string PrintUnsupportedMessage = "Chức năng in báo cáo chưa hỗ trợ trong phiên bản này.";
        public const string NetProfitFormulaTooltip =
            "Tổng tiền lãi = Tổng tiền lãi bán hàng − Tổng tiền giảm giá + Thuế + Phí dịch vụ + Tiền giờ\n" +
            "(Tổng tiền lãi bán hàng = Tổng tiền bán − Tổng tiền nhập; tính trên toàn bộ phạm vi ngày và cửa hàng, không đổi theo cây nhóm hay ô Lọc)";
    }

    /// <summary>Cách tính vốn (rdCachTinh): 0 = Theo giá vốn dòng (mặc định), 1 = Theo giá nhập hiện tại.</summary>
    public enum ItemSalesCostMode
    {
        /// <summary>TDONHANGCHITIET.GIAVON (mặc định).</summary>
        LineCost = 0,
        /// <summary>DMATHANG.GIANHAP.</summary>
        ProductBuyPrice = 1
    }

    /// <summary>Một nút nhóm mặt hàng cho cây bên trái (DNHOMMATHANG, bỏ DLOAIDOID = '4').</summary>
    public sealed class ItemSalesGroupNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public string SortOrder { get; set; }
    }

    /// <summary>Một dòng đã gộp trên lưới "Mặt hàng bán" (đúng trường gộp và 11 cột spec §2.0).</summary>
    public sealed class ItemSalesRow
    {
        public string ProductId { get; set; }
        public string GroupId { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; }
        public string UnitId { get; set; }

        // 11 cột hiển thị theo thứ tự §1 / §2.0:
        public string ItemName { get; set; }
        /// <summary>Σ (COALESCE(SLXUATCHUAQUYDOI,0) − COALESCE(SLNHAPCHUAQUYDOI,0)).</summary>
        public decimal Quantity { get; set; }
        public string UnitName { get; set; }
        public decimal UnitPrice { get; set; }
        /// <summary>TIENGIAMGIA (trường nhóm, không cộng).</summary>
        public decimal LineDiscountAmount { get; set; }
        /// <summary>Σ THANHTIEN.</summary>
        public decimal SalesAmount { get; set; }
        /// <summary>Giá vốn dòng (TDONHANGCHITIET.GIAVON) hoặc Giá nhập (DMATHANG.GIANHAP) theo radio.</summary>
        public decimal UnitCost { get; set; }
        public decimal DiscountRate { get; set; }
        /// <summary>Σ giá × (COALESCE(SLXUAT,0) − COALESCE(SLNHAP,0)).</summary>
        public decimal CostAmount { get; set; }
        /// <summary>LAI = THANHTIEN − THANHTIENNHAP (tính bằng ItemSalesCalculator).</summary>
        public decimal Profit { get; set; }
        /// <summary>TILELAI = THANHTIEN == 0 ? 0 : 100 × LAI / THANHTIEN (tính bằng ItemSalesCalculator).</summary>
        public decimal ProfitRate { get; set; }
    }

    /// <summary>Tổng các khoản cấp bill đọc riêng trên TDONHANG trong phạm vi bill (§2.0, §2.3).</summary>
    public sealed class ItemSalesBillTotals
    {
        /// <summary>Σ (COALESCE(TIENGIAMGIATONG,0) + COALESCE(GIAMGIAMATHANG,0)).</summary>
        public decimal TotalDiscount { get; set; }
        /// <summary>Σ COALESCE(TIENGIO,0).</summary>
        public decimal TimeCharge { get; set; }
        /// <summary>Σ COALESCE(PHIDICHVU,0).</summary>
        public decimal ServiceFee { get; set; }
        /// <summary>Σ COALESCE(TIENTHUE,0).</summary>
        public decimal Tax { get; set; }
    }

    /// <summary>Khối tổng cộng dưới màn hình (thuần decimal, §2.3 & §2.0).</summary>
    public sealed class ItemSalesSummary
    {
        /// <summary>Tổng tiền bán = Σ (SLXUATCHUAQUYDOI_net × DONGIA) trước chiết khấu dòng.</summary>
        public decimal TotalSalesBeforeDiscount { get; set; }
        /// <summary>Tổng tiền nhập = Σ THANHTIENNHAP.</summary>
        public decimal TotalCost { get; set; }
        /// <summary>(Tổng tiền lãi bán hàng) = Tổng tiền bán − Tổng tiền nhập.</summary>
        public decimal GrossSalesProfit { get; set; }
        /// <summary>(Tiền giờ).</summary>
        public decimal TimeCharge { get; set; }
        /// <summary>(Tổng tiền giảm giá) = Σ (TIENGIAMGIATONG + GIAMGIAMATHANG).</summary>
        public decimal TotalDiscount { get; set; }
        /// <summary>(Phí dịch vụ).</summary>
        public decimal ServiceFee { get; set; }
        /// <summary>(Thuế).</summary>
        public decimal Tax { get; set; }
        /// <summary>(Tổng tiền lãi) = Tổng tiền lãi bán hàng − Tổng tiền giảm giá + Thuế + Phí dịch vụ + Tiền giờ.</summary>
        public decimal NetProfit { get; set; }
    }

    /// <summary>Khóa mở dialog "Chi tiết bán hàng theo mặt hàng" khi nhấp đúp một dòng lưới (§2.0, §2.4).</summary>
    public sealed class ItemSalesDetailKey
    {
        public string ProductId { get; set; }
        public string ItemName { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal UnitCost { get; set; }
    }

    /// <summary>Một hóa đơn ở lưới trên dialog chi tiết (đúng 22 cột theo ViewConfig ở spec §2.0).</summary>
    public sealed class ItemSalesBillRow
    {
        public string Id { get; set; }
        public string Note { get; set; }
        public string Name { get; set; }
        public DateTime? Ngay { get; set; }
        public string CustomerName { get; set; }
        public string CustomerCode { get; set; }
        public string CustomerAddress { get; set; }
        public string CustomerPhone { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? ShippingFee { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? DiscountRate { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? TaxRate { get; set; }
        public decimal? GoodsAmount { get; set; }
        public string Description { get; set; }
        public DateTime? PaidTime { get; set; }
        public decimal? CashTendered { get; set; }
        public decimal? ChangeAmount { get; set; }
        public string PaidBy { get; set; }
        public string BookingNumber { get; set; }
        public string Delivery { get; set; }
        public decimal? ReturnAmount { get; set; }
    }

    /// <summary>Một dòng chi tiết hóa đơn ở lưới dưới dialog chi tiết (đúng 11 cột theo spec §2.0).</summary>
    public sealed class ItemSalesBillLine
    {
        public string Id { get; set; }
        public string ProductId { get; set; }
        public string Note { get; set; }
        public string ProductName { get; set; }
        public string ProductUnitName { get; set; }
        public string ProductCode { get; set; }
        public string ProductWarranty { get; set; }
        public string Barcode { get; set; }
        public string LineWarranty { get; set; }
        public decimal? DiscountRate { get; set; }
        public decimal? Amount { get; set; }
        public decimal? UnitPrice { get; set; }
        /// <summary>SLXUAT nguyên giá trị (không trừ SLNHAP, theo §2.0).</summary>
        public decimal? QuantityOut { get; set; }
    }

    /// <summary>Luật lọc phía lưới (cây nhóm khớp chính xác + ô Lọc chứa chuỗi trên TENHANG, không phân biệt hoa thường chữ Việt).</summary>
    public static class ItemSalesFilter
    {
        private static readonly CompareInfo ViCompare = CultureInfo.GetCultureInfo("vi-VN").CompareInfo;

        public static bool Matches(ItemSalesRow row, string selectedGroupId, string nameQuery)
        {
            if (row == null)
                return false;
            if (!string.IsNullOrEmpty(selectedGroupId))
            {
                if (!string.Equals(row.GroupId ?? "", selectedGroupId, StringComparison.Ordinal))
                    return false;
            }
            string q = (nameQuery ?? "").Trim();
            if (q.Length == 0)
                return true;
            if (string.IsNullOrEmpty(row.ItemName))
                return false;
            return ViCompare.IndexOf(row.ItemName, q, CompareOptions.IgnoreCase) >= 0;
        }
    }
}
