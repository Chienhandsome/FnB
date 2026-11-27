using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// 9 loại hình khuyến mại theo spec docs/specs/danh-muc-khuyen-mai.md §2
    /// (nhận diện theo từ khóa trong DLOAIHINHKHUYENMAI.NAME không dấu, không phân biệt hoa thường).
    /// </summary>
    public enum PromotionKind
    {
        Unknown = 0,
        TotalBillPercent = 1,
        ProductGroupPercent = 2,
        OrderValuePercent = 3,
        QuantityPercent = 4,
        BuyXGetY = 5,
        FixedPrice = 6,
        ProductPercent = 7,
        ProductAmount = 8,
        GiftByOrderValue = 9
    }

    /// <summary>Trạng thái hiển thị của đợt khuyến mại tại ngày máy chủ (spec §3).</summary>
    public enum PromotionStatus
    {
        Active = 1,
        Upcoming = 2,
        Expired = 3,
        Stopped = 4
    }

    /// <summary>Một quy tắc nhận diện loại hình khuyến mại theo từ khóa (tên đã bỏ dấu, viết thường).</summary>
    public sealed class PromotionKindRule
    {
        public PromotionKind Kind { get; private set; }
        public string[] RequiredAll { get; private set; }
        public string[] RequiredAny { get; private set; }
        public string[] ExcludedAny { get; private set; }

        public PromotionKindRule(PromotionKind kind, string[] requiredAll, string[] requiredAny, string[] excludedAny)
        {
            Kind = kind;
            RequiredAll = requiredAll ?? new string[0];
            RequiredAny = requiredAny ?? new string[0];
            ExcludedAny = excludedAny ?? new string[0];
        }

        public bool Matches(string foldedName)
        {
            if (string.IsNullOrEmpty(foldedName))
                return false;
            for (int i = 0; i < RequiredAll.Length; i++)
            {
                if (foldedName.IndexOf(RequiredAll[i], StringComparison.Ordinal) < 0)
                    return false;
            }
            if (RequiredAny.Length > 0)
            {
                bool any = false;
                for (int i = 0; i < RequiredAny.Length; i++)
                {
                    if (foldedName.IndexOf(RequiredAny[i], StringComparison.Ordinal) >= 0)
                    {
                        any = true;
                        break;
                    }
                }
                if (!any)
                    return false;
            }
            for (int i = 0; i < ExcludedAny.Length; i++)
            {
                if (foldedName.IndexOf(ExcludedAny[i], StringComparison.Ordinal) >= 0)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Bảng từ khóa duy nhất nhận diện 9 loại khuyến mại theo tên trong DLOAIHINHKHUYENMAI (spec §2, điểm T2).
    /// Bỏ dấu tiếng Việt (kể cả đ/Đ → d) và chuyển chữ thường trước khi so khớp.
    /// </summary>
    public static class PromotionKindResolver
    {
        public static readonly PromotionKindRule[] Rules =
        {
            new PromotionKindRule(PromotionKind.TotalBillPercent, new[] { "tong bill" }, null, null),
            new PromotionKindRule(PromotionKind.ProductGroupPercent, new[] { "nhom hang" }, null, null),
            new PromotionKindRule(PromotionKind.GiftByOrderValue, new[] { "tang theo tri gia" }, null, null),
            new PromotionKindRule(PromotionKind.OrderValuePercent, new[] { "gia tri don hang" }, null, new[] { "tang" }),
            new PromotionKindRule(PromotionKind.BuyXGetY, new[] { "mua", "tang" }, null, null),
            new PromotionKindRule(PromotionKind.QuantityPercent, new[] { "so luong" }, null, new[] { "mua", "tang" }),
            new PromotionKindRule(PromotionKind.FixedPrice, new[] { "dong gia" }, null, null),
            new PromotionKindRule(PromotionKind.ProductAmount, new[] { "tien theo san pham" }, null, null),
            new PromotionKindRule(PromotionKind.ProductPercent, new[] { "san pham" }, new[] { "%", "phan tram" }, null)
        };

        public static PromotionKind Resolve(string typeName)
        {
            string folded = Fold(typeName);
            if (folded.Length == 0)
                return PromotionKind.Unknown;
            for (int i = 0; i < Rules.Length; i++)
            {
                if (Rules[i].Matches(folded))
                    return Rules[i].Kind;
            }
            return PromotionKind.Unknown;
        }

        public static string Fold(string text)
        {
            return CustomerText.FoldNoTrim((text ?? "").Trim());
        }
    }

    /// <summary>Một dòng loại hình khuyến mại trong DLOAIHINHKHUYENMAI.</summary>
    public sealed class PromotionTypeItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public PromotionKind Kind { get { return PromotionKindResolver.Resolve(Name); } }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    /// <summary>Một dòng đợt khuyến mại trong DDOTKHUYENMAI (spec §3, §8; không đụng các cột tiền giờ/giờ).</summary>
    public sealed class PromotionRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string TypeId { get; set; }
        public string TypeName { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        /// <summary>NGUNGAPDUNG: 30 = ngừng áp dụng, 0 = đang dùng.</summary>
        public int StoppedFlag { get; set; }
        /// <summary>TILEGIAMGIA ở đầu đợt (dùng cho loại Giảm giá tổng bill).</summary>
        public decimal DiscountPercent { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Dữ liệu hiển thị dẫn xuất từ bảng liên kết.
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }
        public int DetailCount { get; set; }

        public bool IsStopped
        {
            get { return StoppedFlag == PromotionText.FlagOn; }
            set { StoppedFlag = value ? PromotionText.FlagOn : PromotionText.FlagOff; }
        }

        public PromotionKind Kind
        {
            get { return PromotionKindResolver.Resolve(TypeName); }
        }

        public static PromotionRow NewDefault()
        {
            return new PromotionRow
            {
                Name = "",
                Note = "",
                TypeId = null,
                TypeName = "",
                FromDate = DateTime.Today,
                ToDate = null,
                StoppedFlag = PromotionText.FlagOff,
                DiscountPercent = 0m,
                Status = PromotionText.ActiveStatus
            };
        }
    }

    /// <summary>Một dòng chi tiết đợt khuyến mại trong DDOTKHUYENMAICHITIET (spec §2, §8).</summary>
    public sealed class PromotionDetailRow
    {
        public string Id { get; set; }
        public string PromotionId { get; set; }
        public string ProductGroupId { get; set; }
        public string ProductGroupName { get; set; }
        public decimal DiscountPercent { get; set; }
        public string ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string ProductUnitName { get; set; }
        public decimal ProductSellPrice { get; set; }
        /// <summary>GIABAN: giá khuyến mại (Đồng giá) hoặc số tiền giảm mỗi đơn vị (Giảm giá tiền theo sản phẩm, điểm T1).</summary>
        public decimal SalePrice { get; set; }
        public decimal OrderValue { get; set; }
        public decimal ItemQuantity { get; set; }
        public decimal BuyQuantity { get; set; }
        public decimal GiftQuantity { get; set; }
        public string GiftProductId { get; set; }
        public string GiftProductName { get; set; }
        public string GiftProductUnitName { get; set; }
        public decimal GiftProductSellPrice { get; set; }
        public string Note { get; set; }
        public int Status { get; set; }
    }

    /// <summary>Mặt hàng chọn được trong hộp thoại chọn mặt hàng cho chi tiết đợt khuyến mại.</summary>
    public sealed class PromotionProductItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string UnitName { get; set; }
        public decimal SellPrice { get; set; }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    /// <summary>Kết quả lưu đợt khuyến mại (DDOTKHUYENMAI + DDOTKHUYENMAICHITIET).</summary>
    public sealed class PromotionSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasNameError { get; set; }
        public bool HasTypeError { get; set; }
        public bool HasDateError { get; set; }
        public bool HasDiscountError { get; set; }
        public bool HasOverlapError { get; set; }
        public bool HasDetailError { get; set; }
        public int ErrorLineIndex { get; set; }

        public PromotionSaveResult()
        {
            ErrorLineIndex = -1;
        }

        public static PromotionSaveResult Fail(
            string message,
            bool hasNameError = false,
            bool hasTypeError = false,
            bool hasDateError = false,
            bool hasDiscountError = false,
            bool hasOverlapError = false,
            bool hasDetailError = false,
            int errorLineIndex = -1)
        {
            return new PromotionSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasNameError = hasNameError,
                HasTypeError = hasTypeError,
                HasDateError = hasDateError,
                HasDiscountError = hasDiscountError,
                HasOverlapError = hasOverlapError,
                HasDetailError = hasDetailError,
                ErrorLineIndex = errorLineIndex
            };
        }
    }

    /// <summary>Kết quả xóa cứng đợt khuyến mại và chi tiết.</summary>
    public sealed class PromotionDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static PromotionDeleteResult Fail(string message)
        {
            return new PromotionDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số và thông báo chuẩn cho Danh mục đợt khuyến mại (DDOTKHUYENMAI).</summary>
    public static class PromotionText
    {
        public const string FunctionName = "Danh mục đợt khuyến mại";
        public const string TableName = "DDOTKHUYENMAI";
        public const string DetailTableName = "DDOTKHUYENMAICHITIET";
        public const string TypeTableName = "DLOAIHINHKHUYENMAI";
        public const int ActiveStatus = 30;
        public const int FlagOn = 30;
        public const int FlagOff = 0;
        public const int MaxTextLength = 255;

        public const string StatusActive = "Đang áp dụng";
        public const string StatusUpcoming = "Chưa tới";
        public const string StatusExpired = "Hết hạn";
        public const string StatusStopped = "Ngừng áp dụng";

        public const string NameEmpty = "Trường \"Tên đợt\" không được trống!";
        public const string TypeRequired = "Vui lòng chọn loại hình khuyến mại.";
        public const string TypeUnknown = "Loại hình khuyến mại chưa được hỗ trợ.";
        public const string TypeLockedWhenHasDetails = "Đợt khuyến mại đã có chi tiết, không thể đổi loại hình.";
        public const string ToDateBeforeFromDate = "Đến ngày không được trước Từ ngày.";
        public const string DiscountOutOfRange = "Tỉ lệ giảm giá phải lớn hơn 0 và không quá 100%.";
        public const string DetailsRequired = "Vui lòng nhập ít nhất một dòng chi tiết cho đợt khuyến mại.";
        public const string NotFound = "Đợt khuyến mại không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";
        public const string SameProductGiftLabel = "(Cùng mặt hàng mua)";

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên đợt\" đã tồn tại!", name ?? "");
        }

        public static string OverlapBlocked(string existingName)
        {
            return string.Format("Đợt '{0}' cùng loại đã áp dụng trong khoảng ngày này", existingName ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
