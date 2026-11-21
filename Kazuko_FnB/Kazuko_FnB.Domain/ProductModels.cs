using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng DMATHANG: đủ các cột dialog sửa + các cột không có ô (giữ nguyên khi ghi, xem spec 1.2/3.1b).</summary>
    public sealed class ProductRow
    {
        public string Id { get; set; }
        /// <summary>Mã hàng. Chuỗi rỗng '' (không phải NULL) khi không dùng mã (3.1b).</summary>
        public string Code { get; set; }
        public string Name { get; set; }
        public string EnglishName { get; set; }
        public decimal SellPrice { get; set; }
        public decimal SellPrice2 { get; set; }
        public decimal SellPrice3 { get; set; }
        public decimal SellPrice4 { get; set; }
        /// <summary>ĐVT lẻ (bán, định lượng).</summary>
        public string UnitId { get; set; }
        /// <summary>ĐVT chẵn (nhập mua). NULL khi không dùng ĐVT chẵn.</summary>
        public string BigUnitId { get; set; }
        /// <summary>Số ĐVT lẻ trong 1 ĐVT chẵn. NULL khi không dùng ĐVT chẵn.</summary>
        public decimal? QuyDoi { get; set; }
        public decimal BuyPrice { get; set; }
        /// <summary>Giá vốn (chỉ đọc trên dialog).</summary>
        public decimal CostPrice { get; set; }
        /// <summary>Giá bán theo ĐVT chẵn (không có ô, giữ nguyên).</summary>
        public decimal? BigSellPrice { get; set; }
        public string GroupId { get; set; }
        /// <summary>Loại mặt hàng '0'..'7' ('' = kiêm vật tư, xem như '0').</summary>
        public string TypeId { get; set; }
        public bool PriceByTime { get; set; }
        public bool TempLocked { get; set; }
        public bool ByWeight { get; set; }
        public decimal MinStock { get; set; }
        /// <summary>Ảnh mặt hàng (ANH, BLOB). NULL = không có ảnh.</summary>
        public byte[] Image { get; set; }

        // Các cột không có ô trên dialog: khi sửa phải giữ nguyên giá trị cũ (spec 1.2, Q1).
        public string Note { get; set; }
        public decimal? MaxStock { get; set; }
        public string Warranty { get; set; }
        public string Barcode { get; set; }
        public string ManufacturerId { get; set; }
        public string ConsignmentId { get; set; }
        public decimal? Commission { get; set; }
        public decimal? DefaultDiscountRate { get; set; }
        public decimal? DefaultDiscountAmount { get; set; }
        public int? HasExpiry { get; set; }
        public string Size { get; set; }
        public int? Color { get; set; }
        /// <summary>Giờ hát combo (không dựng, giữ nguyên).</summary>
        public decimal? SingHours { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị (chỉ để hiện trên lưới/thông tin, không ghi xuống DB).
        public string GroupName { get; set; }
        public string TypeName { get; set; }
        public string UnitName { get; set; }
        public string BigUnitName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        /// <summary>Loại hiệu dụng: '' và NULL đều xem như '0' (kiêm vật tư), như COALESCE(DLOAIMATHANGID,'0') của app gốc.</summary>
        public string EffectiveTypeId
        {
            get { return string.IsNullOrEmpty(TypeId) ? "0" : TypeId; }
        }

        /// <summary>
        /// Dòng mới với đúng giá trị app gốc ghi khi thêm (3.1b): CODE '', loại '0', các giá = 0,
        /// GIABANCHAN/TONTOIDA/HOAHONG/giảm giá/NOTE/hãng/ký gửi/màu = NULL, QUYDOI NULL, COHANSUDUNG = 0.
        /// Người gọi gán thêm UnitId (ĐVT đầu danh sách), GroupId (nhóm đang chọn) và Code (sinh theo nhóm).
        /// </summary>
        public static ProductRow NewDefault()
        {
            return new ProductRow
            {
                Code = "",
                Name = "",
                EnglishName = "",
                SellPrice = 0m,
                SellPrice2 = 0m,
                SellPrice3 = 0m,
                SellPrice4 = 0m,
                UnitId = null,
                BigUnitId = null,
                QuyDoi = null,
                BuyPrice = 0m,
                CostPrice = 0m,
                BigSellPrice = null,
                GroupId = null,
                TypeId = "0",
                PriceByTime = false,
                TempLocked = false,
                ByWeight = false,
                MinStock = 0m,
                Image = null,
                Note = null,
                MaxStock = null,
                Warranty = null,
                Barcode = null,
                ManufacturerId = null,
                ConsignmentId = null,
                Commission = null,
                DefaultDiscountRate = null,
                DefaultDiscountAmount = null,
                HasExpiry = 0,
                Size = null,
                Color = null,
                SingHours = null,
                Status = 30
            };
        }
    }

    /// <summary>Một nhóm mặt hàng (DNHOMMATHANG).</summary>
    public sealed class ProductGroupRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>Tiền tố sinh mã mặt hàng (có thể rỗng).</summary>
        public string Code { get; set; }
        /// <summary>Loại đồ (DLOAIDO): '0' Đồ ăn, '1' Đồ uống, '2' Dịch vụ, '3' Đồ khác, '4' Nguyên liệu. Mặc định '1'.</summary>
        public string DrinkTypeId { get; set; }
        public string ImageId { get; set; }
        public string ParentId { get; set; }
        public string ParentDir { get; set; }
        public string SortOrder { get; set; }
        public int AutoId { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    /// <summary>Một dòng định lượng (DDINHLUONG) kèm thông tin vật tư đã join (để tính tiền).</summary>
    public sealed class RecipeLine
    {
        public string Id { get; set; }
        /// <summary>Vật tư (DVATTUID → DMATHANG.ID).</summary>
        public string MaterialId { get; set; }
        public string MaterialName { get; set; }
        public string MaterialTypeId { get; set; }
        public string MaterialUnitId { get; set; }
        public string MaterialUnitName { get; set; }
        /// <summary>Số lượng (n4, phải &gt; 0).</summary>
        public decimal Quantity { get; set; }
        /// <summary>Giá nhập của vật tư tính theo ĐVT lẻ (= GIANHAP / QUYDOI nếu có ĐVT chẵn).</summary>
        public decimal BuyPrice { get; set; }
        /// <summary>Giá vốn của vật tư (theo ĐVT lẻ).</summary>
        public decimal CostPrice { get; set; }
        /// <summary>Thành tiền nhập = Số lượng × Giá nhập (tính).</summary>
        public decimal AmountBuy { get; set; }
        /// <summary>Thành tiền vốn = Số lượng × Giá vốn (tính).</summary>
        public decimal AmountCost { get; set; }
        /// <summary>QUYDOI của vật tư (để chia giá nhập). NULL = không có ĐVT chẵn.</summary>
        public decimal? MaterialQuyDoi { get; set; }
        /// <summary>Vật tư có ĐVT chẵn không (DDONVITINHCHANID khác rỗng).</summary>
        public bool MaterialHasBigUnit { get; set; }
        /// <summary>Vật tư không còn trong DB (dòng mồ côi, xem spec 3.6): lưới phải chịu được, không lỗi.</summary>
        public bool MaterialMissing { get; set; }
    }

    /// <summary>Một loại mặt hàng (DLOAIMATHANG) với ba cờ quyết định hành vi.</summary>
    public sealed class ProductTypeInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool CanSell { get; set; }
        public bool HasStock { get; set; }
        /// <summary>Có định lượng (CODINHLUONG = 30): pha chế (1), Combo (4), Combo mở (5).</summary>
        public bool HasRecipe { get; set; }
    }

    /// <summary>Một bảng tham chiếu đang chặn xóa (số dòng còn liên kết tới mặt hàng).</summary>
    public sealed class ReferenceBlock
    {
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public int Count { get; set; }
    }

    /// <summary>Kết quả lưu mặt hàng (kiểm trùng làm trong cùng transaction với ghi).</summary>
    public sealed class ProductSaveResult
    {
        public bool Ok { get; set; }
        /// <summary>Thông báo lỗi nguyên văn để hiện cho người dùng (null khi lưu được).</summary>
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
    }

    /// <summary>Kết quả xóa mặt hàng (kèm danh sách tham chiếu đang chặn, nếu có).</summary>
    public sealed class ProductDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public List<ReferenceBlock> Blocks { get; set; }
    }

    /// <summary>Tùy chọn lưu mặt hàng (đọc từ SCONFIG ở tầng gọi — Data không đọc Services).</summary>
    public sealed class ProductSaveOptions
    {
        /// <summary>Cho trùng tên (ChoPhepTrungTenMatHang = 30).</summary>
        public bool AllowDuplicateName { get; set; }
        /// <summary>Mã hàng bắt buộc + không trùng (SuDungMaHang = 30). Tắt thì CODE '' được lưu và không kiểm trùng.</summary>
        public bool CodeRequired { get; set; }
        /// <summary>Tự cập nhật GIANHAP theo tổng định lượng (TuDongCapNhatGiaNhapCuaMatHangDinhLuong = 30).</summary>
        public bool AutoUpdateBuyPrice { get; set; }
        /// <summary>Tự cập nhật GIAVON theo tổng định lượng (TuDongCapNhatGiaVonCuaMatHangDinhLuong = 30).</summary>
        public bool AutoUpdateCostPrice { get; set; }
    }

    /// <summary>Một dòng lịch sử mua/bán (form phụ 1.4).</summary>
    public sealed class ProductHistoryRow
    {
        public DateTime? Date { get; set; }
        public string Time { get; set; }
        public string PartnerName { get; set; }
        public decimal Price { get; set; }
    }

    /// <summary>
    /// Công thức tiền của định lượng (spec 3.4), đặt ở Domain để cả Data và Services dùng chung một công thức:
    /// giá nhập của vật tư tính theo ĐVT lẻ (chia QUYDOI khi có ĐVT chẵn), giá vốn tính theo ĐVT lẻ (không chia).
    /// </summary>
    public static class ProductMath
    {
        /// <summary>Giá nhập của một vật tư tính theo ĐVT lẻ.</summary>
        public static decimal UnitBuyPrice(decimal buyPrice, decimal? quyDoi, bool hasBigUnit)
        {
            if (!hasBigUnit || quyDoi == null || quyDoi.Value == 0m)
                return buyPrice;
            return buyPrice / quyDoi.Value;
        }

        /// <summary>Thành tiền nhập = Số lượng × Giá nhập (theo ĐVT lẻ).</summary>
        public static decimal AmountBuy(decimal quantity, decimal unitBuyPrice)
        {
            return quantity * unitBuyPrice;
        }

        /// <summary>Thành tiền vốn = Số lượng × Giá vốn.</summary>
        public static decimal AmountCost(decimal quantity, decimal costPrice)
        {
            return quantity * costPrice;
        }
    }

    /// <summary>
    /// Chuỗi thông báo nguyên văn (spec 2.1/2.2) và chuẩn hóa tên, đặt ở Domain để cả Data (kiểm trùng trong
    /// transaction) và Services (kiểm tra khi lưu) dùng chung một nguồn — tránh hai bản chữ khác nhau.
    /// Hai câu "Mã hàng" trống/trùng chưa được quan sát ở app gốc: dựng theo đúng mẫu câu đã quan sát của "Tên hàng".
    /// </summary>
    public static class ProductText
    {
        /// <summary>Chuẩn hóa tên để kiểm trùng (Q21: không phân biệt hoa/thường, cắt khoảng trắng).</summary>
        public static string NormalizeName(string name)
        {
            return (name ?? "").Trim();
        }

        public const string NameEmpty = "Trường \"Tên hàng\" không được trống!";
        public const string CodeEmpty = "Trường \"Mã hàng\" không được trống!";

        public static string NameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên hàng\" đã tồn tại!", name);
        }

        public static string CodeDuplicate(string code)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Mã hàng\" đã tồn tại!", code);
        }

        /// <summary>Hộp xác nhận xóa vĩnh viễn của framework (2.2, Q22): (1) = số dòng đang chọn.</summary>
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        // Kiểm tra khi lưu 2.1 (#1–#8), nguyên văn.
        public const string EnterQuyDoi = "Mời bạn nhập quy đổi";
        public const string UnitSame = "Đơn vị lẻ và đơn vị chẵn không được trùng nhau";
        public const string ComboOpen = "Phiên bản hiện tại mới hỗ trợ combo mở ở giao diện quản trị";
        public const string NoRecipeAllowed = "Mặt hàng này không có vật tư / định lượng đi kèm";
        public const string RecipeRequired = "Vui lòng nhập vật tư/định lượng cho mặt hàng";
        public const string EnterQuantity = "Mời bạn nhập số lượng cho định lượng";
        public const string EnterMaterial = "Mời bạn nhập vật tư cho định lượng";
        public const string ComboNoMaterial = "Combo không được phép chứa nguyên vật liệu";
    }

    /// <summary>Một vật tư được chọn cho định lượng (kèm giá để tính tiền trên dialog).</summary>
    public sealed class MaterialInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeId { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal? QuyDoi { get; set; }
        public bool HasBigUnit { get; set; }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    /// <summary>Đầu vào cho kiểm tra khi lưu (spec 2.1). Dialog điền từ các ô + loại hiện tại.</summary>
    public sealed class ProductValidationInput
    {
        public string TypeId { get; set; }
        /// <summary>Loại hiện tại có định lượng không (DLOAIMATHANG.CODINHLUONG = 30)? Dùng cho luật tự chuyển sang pha chế.</summary>
        public bool TypeHasRecipe { get; set; }
        /// <summary>SuDung2DonViTinh = 30.</summary>
        public bool UseTwoUnits { get; set; }
        public decimal QuyDoi { get; set; }
        public string UnitId { get; set; }
        public string BigUnitId { get; set; }
        public List<RecipeValidationLine> Lines { get; set; }
    }

    /// <summary>Một dòng định lượng để kiểm tra (2.1 #6–#8).</summary>
    public sealed class RecipeValidationLine
    {
        public string MaterialId { get; set; }
        /// <summary>Loại của vật tư ('0'/'1'/'2'/...). Null khi vật tư mồ côi (không còn trong DB).</summary>
        public string MaterialTypeId { get; set; }
        public decimal Quantity { get; set; }
        public bool MaterialMissing { get; set; }
    }

    /// <summary>Kết quả kiểm tra khi lưu (2.1).</summary>
    public sealed class ProductValidationResult
    {
        /// <summary>Lỗi chặn lưu (null = hợp lệ). Dừng ở lỗi đầu tiên (quyết định 5.6: gộp #1/#2).</summary>
        public string Error { get; set; }
        /// <summary>Cảnh báo không chặn lưu (#3 Combo mở): hiện rồi vẫn lưu.</summary>
        public string Warning { get; set; }
        /// <summary>Chỉ số dòng định lượng gây lỗi (-1 = không phải lỗi dòng).</summary>
        public int ErrorLineIndex { get; set; }
        /// <summary>Lỗi #1: đưa con trỏ vào ô Quy đổi.</summary>
        public bool FocusQuyDoi { get; set; }

        public bool IsValid
        {
            get { return Error == null; }
        }
    }
}
