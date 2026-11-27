using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Hằng số định danh, quyền và thông báo chuẩn cho màn Tồn kho (docs/specs/ton-kho.md).
    /// Màn chỉ đọc, không có Thêm/Sửa/Xóa; dùng chung công thức tồn chuẩn với bán hàng, xuất, chuyển.
    /// </summary>
    public static class InventoryConstants
    {
        public const int ActiveStatus = 30;

        public const string FunctionName = "Tồn kho";
        public const string ViewBuyPriceFunctionName = ItemSalesConstants.ViewCostPermissionName;

        public const string TypeAll = "";
        public const string TypeProductWithStock = "0";
        public const string TypeMaterial = "2";

        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string EmptyNoWarehouse = "Không có kho hàng nào thuộc phạm vi cửa hàng của bạn.";
        public const string EmptyNoItems = "Không có mặt hàng tồn kho khớp điều kiện lọc.";
    }

    /// <summary>Trạng thái tô màu dòng tồn kho trên lưới (spec §1).</summary>
    public enum InventoryRowTone
    {
        /// <summary>Bình thường (tồn không âm và không dưới/bằng tồn tối thiểu).</summary>
        Normal = 0,
        /// <summary>Tô vàng: tồn không âm và tồn &lt;= tồn tối thiểu khi TONTOITHIEU &gt; 0.</summary>
        LowStock = 1,
        /// <summary>Tô đỏ: tồn âm (tồn &lt; 0, ưu tiên hơn LowStock).</summary>
        NegativeStock = 2
    }

    /// <summary>Một dòng tồn kho của mặt hàng tại một kho (đúng các cột spec §1).</summary>
    public sealed class InventoryItemRow
    {
        public string ProductId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string TypeId { get; set; }
        public string TypeName { get; set; }
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string BigUnitId { get; set; }
        public string BigUnitName { get; set; }
        public decimal QuyDoi { get; set; }

        /// <summary>Tồn kho theo ĐVT cơ sở = Σ(SLNHAP - SLXUAT) tại kho đang chọn.</summary>
        public decimal StockQuantity { get; set; }
        /// <summary>Hiển thị 2 ĐVT ("N thùng M chai") khi mặt hàng có đơn vị chẵn.</summary>
        public string TwoUnitDisplay { get; set; }
        /// <summary>Mức tồn tối thiểu (DMATHANG.TONTOITHIEU).</summary>
        public decimal MinStock { get; set; }
        /// <summary>Giá nhập (DMATHANG.GIANHAP).</summary>
        public decimal BuyPrice { get; set; }
        /// <summary>Giá vốn (DMATHANG.GIAVON).</summary>
        public decimal CostPrice { get; set; }
        /// <summary>Giá bán (DMATHANG.GIABAN).</summary>
        public decimal SellPrice { get; set; }
        /// <summary>Giá trị vốn = tồn × giá vốn.</summary>
        public decimal CostValue { get; set; }
        /// <summary>Giá trị bán = tồn × giá bán.</summary>
        public decimal SellValue { get; set; }

        public InventoryRowTone Tone { get; set; }

        /// <summary>Loại hiệu dụng: rỗng hoặc null đều xem như '0' (mặt hàng kiêm vật tư).</summary>
        public string EffectiveTypeId
        {
            get { return string.IsNullOrEmpty(TypeId) ? InventoryConstants.TypeProductWithStock : TypeId; }
        }

        public bool HasTwoUnits
        {
            get
            {
                return !string.IsNullOrEmpty(BigUnitId)
                    && !string.Equals(BigUnitId, UnitId, StringComparison.Ordinal)
                    && QuyDoi > 0m;
            }
        }
    }

    /// <summary>Bộ lọc màn Tồn kho (spec §1).</summary>
    public sealed class InventoryFilter
    {
        public string WarehouseId { get; set; }
        /// <summary>Lọc loại mặt hàng: rỗng = tất cả, '0' = kiêm vật tư, '2' = nguyên liệu.</summary>
        public string ProductTypeId { get; set; }
        /// <summary>Lọc một nhóm mặt hàng (khớp trực tiếp khi GroupIds trống).</summary>
        public string GroupId { get; set; }
        /// <summary>Tập ID nhóm mở rộng (gồm nhóm cha đang chọn và các nhóm con).</summary>
        public HashSet<string> GroupIds { get; set; }
        /// <summary>Ô tìm (mã, tên, không phân biệt hoa thường và dấu).</summary>
        public string SearchText { get; set; }
        /// <summary>Tích "Chỉ hiện hàng dưới mức tồn tối thiểu" (TONTOITHIEU &gt; 0 và tồn &lt;= TONTOITHIEU).</summary>
        public bool OnlyBelowMinStock { get; set; }
        /// <summary>Tích "Ẩn hàng tồn 0" (ẩn dòng có tồn == 0, vẫn giữ tồn âm và tồn dương).</summary>
        public bool HideZeroStock { get; set; }
    }

    /// <summary>Tổng cộng giá trị của lưới tồn kho đang hiển thị (thuần decimal).</summary>
    public sealed class InventoryTotals
    {
        public int ItemCount { get; set; }
        public decimal TotalStockQuantity { get; set; }
        public decimal TotalCostValue { get; set; }
        public decimal TotalSellValue { get; set; }
    }
}
