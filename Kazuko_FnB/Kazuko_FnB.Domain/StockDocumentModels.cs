using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Loại chứng từ kho: Xuất khác (LOAI = 2) hoặc Chuyển kho (LOAI = 3).</summary>
    public enum StockDocumentKind
    {
        Export = 2,
        Transfer = 3
    }

    /// <summary>Hằng số định danh, quyền, cấu hình và thông báo chuẩn cho Xuất khác &amp; Chuyển kho (docs/specs/phieu-xuat-chuyen-kho.md).</summary>
    public static class StockDocumentConstants
    {
        public const int ExportOrderType = 2;
        public const int TransferOrderType = 3;
        public const int ActiveStatus = PurchaseConstants.ActiveStatus;
        public const int DeletedStatus = PurchaseConstants.DeletedStatus;
        public const int PaidFlag = PurchaseConstants.PaidFlag;

        public const string ExportNumberTemplate = "PX(yy)/(*****)";
        public const string TransferNumberTemplate = "CK(yy)/(*****)";
        public const string ExportDefaultDescription = "Xuất khác";
        public const string TransferDefaultDescription = "Chuyển kho nội bộ";

        public const string ExportFunctionName = "Xuất kho";
        public const string TransferFunctionName = "Chuyển kho";
        public const string ConfigPriceMode = "GiaSuDungKhiChuyenXuatKho";

        public const int MaxTextLength = PurchaseConstants.MaxTextLength;

        public const string ExportNotFound = "Phiếu xuất kho không còn tồn tại.";
        public const string TransferNotFound = "Phiếu chuyển kho không còn tồn tại.";
        public const string SourceWarehouseRequired = "Mời bạn nhập: Kho xuất";
        public const string DestWarehouseRequired = "Mời bạn nhập: Kho nhập";
        public const string SameWarehouseBlocked = "Kho nhập và kho xuất không được phép trùng.";
        public const string ExportLinesRequired = "Phiếu xuất kho phải có ít nhất 1 mặt hàng.";
        public const string TransferLinesRequired = "Phiếu chuyển kho phải có ít nhất 1 mặt hàng.";
        public const string ExportQtyMustBePositive = "Số lượng hàng xuất phải lớn hơn 0.";
        public const string TransferQtyMustBePositive = "Số lượng hàng chuyển phải lớn hơn 0.";
        public const string WarehouseNotAllowed = "Bạn không có quyền thao tác với kho đã chọn.";
        public const string ExportDeleteConfirm = "Bạn có muốn xóa phiếu xuất kho đang chọn vĩnh viễn khỏi hệ thống không?";
        public const string TransferDeleteConfirm = "Bạn có muốn xóa phiếu chuyển kho đang chọn vĩnh viễn khỏi hệ thống không?";

        public static int OrderTypeOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? TransferOrderType : ExportOrderType;
        }

        public static string FunctionNameOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? TransferFunctionName : ExportFunctionName;
        }

        public static string NumberTemplateOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? TransferNumberTemplate : ExportNumberTemplate;
        }

        public static string DefaultDescriptionOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? TransferDefaultDescription : ExportDefaultDescription;
        }

        public static string NotFoundOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? TransferNotFound : ExportNotFound;
        }

        public static string LinesRequiredOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? TransferLinesRequired : ExportLinesRequired;
        }

        public static string QtyMustBePositiveOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? TransferQtyMustBePositive : ExportQtyMustBePositive;
        }

        public static string DeleteConfirmOf(StockDocumentKind kind)
        {
            return kind == StockDocumentKind.Transfer ? TransferDeleteConfirm : ExportDeleteConfirm;
        }

        public static string InsufficientStock(string itemName, decimal available, decimal requested)
        {
            return string.Format(
                "Mặt hàng \"{0}\" không đủ tồn kho để xuất (tồn hiện có: {1}, số lượng xuất: {2}).",
                itemName ?? "",
                PurchaseConstants.FormatQtyVi(available),
                PurchaseConstants.FormatQtyVi(requested));
        }
    }

    /// <summary>Một mặt hàng đủ điều kiện xuất / chuyển kho (loại rỗng, '0' kiêm vật tư, hoặc '2' nguyên liệu; đang dùng).</summary>
    public sealed class StockProductItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string TypeId { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string BigUnitId { get; set; }
        public string BigUnitName { get; set; }
        public decimal QuyDoi { get; set; }
        public decimal SellPrice { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal CostPrice { get; set; }

        public bool HasTwoUnits
        {
            get
            {
                return !string.IsNullOrEmpty(BigUnitId)
                    && !string.Equals(BigUnitId, UnitId, StringComparison.Ordinal)
                    && QuyDoi > 0m;
            }
        }

        public decimal GetPriceForMode(int priceMode)
        {
            if (priceMode == 1)
                return BuyPrice;
            if (priceMode == 2)
                return CostPrice;
            return SellPrice;
        }
    }

    /// <summary>Một dòng chi tiết phiếu xuất khác (SLXUAT) hoặc phiếu chuyển kho (dòng nhập SLNHAP &gt; 0).</summary>
    public sealed class StockDocumentLine
    {
        public string Id { get; set; }
        public string OrderId { get; set; }
        public string ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string BaseUnitId { get; set; }
        public string BaseUnitName { get; set; }
        public string BigUnitId { get; set; }
        public string BigUnitName { get; set; }
        public decimal QuyDoi { get; set; }
        public string WarehouseId { get; set; }
        /// <summary>SLXUATCHUAQUYDOI (xuất khác) hoặc SLNHAPCHUAQUYDOI (chuyển kho).</summary>
        public decimal RawQuantity { get; set; }
        /// <summary>SLXUAT (xuất khác) hoặc SLNHAP (chuyển kho) — số lượng đã quy đổi về ĐVT cơ sở.</summary>
        public decimal ConvertedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; }

        public bool HasTwoUnits
        {
            get
            {
                return !string.IsNullOrEmpty(BigUnitId)
                    && !string.Equals(BigUnitId, BaseUnitId, StringComparison.Ordinal)
                    && QuyDoi > 0m;
            }
        }

        public bool IsUsingBigUnit
        {
            get
            {
                return HasTwoUnits
                    && !string.IsNullOrEmpty(UnitId)
                    && string.Equals(UnitId, BigUnitId, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>Đầu phiếu xuất khác (LOAI = 2) hoặc chuyển kho (LOAI = 3) kèm chi tiết và tên hiển thị.</summary>
    public sealed class StockDocumentRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public int Loai { get; set; }
        public int Status { get; set; }
        public int PaidFlag { get; set; }

        public string SourceWarehouseId { get; set; }
        public string DestWarehouseId { get; set; }
        public string StoreId { get; set; }
        public string SupplierId { get; set; }
        public string ExportEmployeeId { get; set; }
        public string ImportEmployeeId { get; set; }
        public string Description { get; set; }
        public string Note { get; set; }

        public decimal TienHang { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienGiamGia { get; set; }
        public bool GiamTheoTien { get; set; }
        public decimal TileThue { get; set; }
        public decimal TienThue { get; set; }
        public decimal PhiVanChuyen { get; set; }
        public decimal TongCong { get; set; }

        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public string SourceWarehouseName { get; set; }
        public string DestWarehouseName { get; set; }
        public string SupplierName { get; set; }
        public string ExportEmployeeName { get; set; }
        public string ImportEmployeeName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public List<StockDocumentLine> Lines { get; set; }

        public static StockDocumentRow NewDefault(StockDocumentKind kind)
        {
            return new StockDocumentRow
            {
                Name = "",
                Date = DateTime.Today,
                Loai = StockDocumentConstants.OrderTypeOf(kind),
                Status = StockDocumentConstants.ActiveStatus,
                PaidFlag = StockDocumentConstants.PaidFlag,
                Description = StockDocumentConstants.DefaultDescriptionOf(kind),
                Note = "",
                Lines = new List<StockDocumentLine>()
            };
        }
    }

    /// <summary>Bộ lọc lưới Quản lý xuất kho / Quản lý chuyển kho.</summary>
    public sealed class StockDocumentFilter
    {
        public StockDocumentKind Kind { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string SourceWarehouseId { get; set; }
        public string DestWarehouseId { get; set; }
        public string SupplierId { get; set; }
        /// <summary>Giới hạn kho theo cửa hàng của người dùng không phải admin (null = không giới hạn).</summary>
        public List<string> AllowedWarehouseIds { get; set; }
        public string SearchText { get; set; }
    }

    public sealed class StockDocumentSaveResult
    {
        public bool Ok { get; set; }
        public string SavedId { get; set; }
        public string SavedNumber { get; set; }
        public string ErrorMessage { get; set; }

        public static StockDocumentSaveResult Fail(string message)
        {
            return new StockDocumentSaveResult { Ok = false, ErrorMessage = message };
        }
    }

    public sealed class StockDocumentDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static StockDocumentDeleteResult Fail(string message)
        {
            return new StockDocumentDeleteResult { Ok = false, ErrorMessage = message };
        }
    }
}
