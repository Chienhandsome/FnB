using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một đợt khuyến mại kèm danh sách chi tiết để áp dụng khi bán (spec docs/specs/danh-muc-khuyen-mai.md §4).</summary>
    public sealed class ActivePromotionBundle
    {
        public PromotionRow Header { get; set; }
        public List<PromotionDetailRow> Details { get; set; }

        public ActivePromotionBundle()
        {
            Details = new List<PromotionDetailRow>();
        }
    }

    /// <summary>Đầu vào một dòng bán thường trên hóa đơn cho bộ tính khuyến mại thuần PromotionApplier.</summary>
    public sealed class PromotionBillLineInput
    {
        public string LineId { get; set; }
        public string ProductId { get; set; }
        public string ProductGroupId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal ManualDiscountRate { get; set; }
        public decimal ManualDiscountAmount { get; set; }
        public bool ManualDiscountByMoney { get; set; }
    }

    /// <summary>Đầu vào đầy đủ cho bộ tính khuyến mại thuần PromotionApplier (không chạm DB).</summary>
    public sealed class PromotionApplyInput
    {
        public DateTime SaleDate { get; set; }
        public decimal RoundStep { get; set; }
        public List<PromotionBillLineInput> Lines { get; set; }
        /// <summary>Bản đồ DNHOMMATHANG: ID nhóm -> PARENTID nhóm cha (để dò cả nhóm cha của nhóm con).</summary>
        public IDictionary<string, string> GroupParents { get; set; }
        public decimal ManualBillDiscountRate { get; set; }
        public decimal ManualBillDiscountAmount { get; set; }
        public bool ManualBillDiscountByMoney { get; set; }
        public List<ActivePromotionBundle> Promotions { get; set; }

        public PromotionApplyInput()
        {
            RoundStep = 1000m;
            Lines = new List<PromotionBillLineInput>();
            Promotions = new List<ActivePromotionBundle>();
        }
    }

    /// <summary>Kết quả giảm giá hiệu dụng của một dòng bán thường sau khi xét khuyến mại và giảm giá tay.</summary>
    public sealed class PromotionLineApplyResult
    {
        public string LineId { get; set; }
        public string ProductId { get; set; }
        public decimal EffectiveDiscountRate { get; set; }
        public decimal EffectiveDiscountAmount { get; set; }
        public bool EffectiveDiscountByMoney { get; set; }
        public decimal EffectiveAmount { get; set; }
        public bool IsPromotionDiscount { get; set; }
        public string PromotionId { get; set; }
        public string PromotionName { get; set; }
        public PromotionKind PromotionKind { get; set; }
    }

    /// <summary>Yêu cầu dòng tặng 0 đồng do Mua x tặng y hoặc Tặng theo trị giá sinh ra.</summary>
    public sealed class PromotionGiftRequest
    {
        public string ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string PromotionId { get; set; }
        public string PromotionName { get; set; }
        public PromotionKind PromotionKind { get; set; }
    }

    /// <summary>Kết quả tính khuyến mại thuần cho toàn bộ hóa đơn.</summary>
    public sealed class PromotionApplyResult
    {
        public List<PromotionLineApplyResult> Lines { get; set; }
        public List<PromotionGiftRequest> Gifts { get; set; }
        public decimal TienHang { get; set; }
        public decimal TienHangChuaGiam { get; set; }
        public decimal TotalQuantity { get; set; }
        public bool UsePromotionBillDiscount { get; set; }
        public decimal EffectiveBillDiscountRate { get; set; }
        public decimal EffectiveBillDiscountAmount { get; set; }
        public bool EffectiveBillDiscountByMoney { get; set; }
        public string BillPromotionId { get; set; }
        public string BillPromotionName { get; set; }
        public PromotionKind BillPromotionKind { get; set; }

        public PromotionApplyResult()
        {
            Lines = new List<PromotionLineApplyResult>();
            Gifts = new List<PromotionGiftRequest>();
        }
    }
}
