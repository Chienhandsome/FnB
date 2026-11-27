using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số định danh, quyền, cấu hình và thông báo chuẩn cho chức năng Nhập hàng vào kho (docs/specs/phieu-nhap-kho.md).</summary>
    public static class PurchaseConstants
    {
        public const int OrderType = 1;
        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;
        public const int PaidFlag = 30;

        public const string DefaultNumberTemplate = "PN(yy)/(*****)";
        public const string DefaultDescription = "Nhập mua hàng";

        public const string FunctionName = "Nhập kho";
        public const string FunctionDateChange = "Thay đổi ngày trong các phiếu";
        public const string FunctionViewBuyPrice = ItemSalesConstants.ViewCostPermissionName;

        public const string ConfigRequireSupplier = "BatBuocChonNhaCungCapTrongNhapKho";

        public const int MaxTextLength = 255;

        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string NotFound = "Phiếu nhập kho không còn tồn tại.";
        public const string NumberRequired = "Mời bạn nhập: Số phiếu";
        public const string WarehouseRequired = "Mời bạn nhập: Kho nhập";
        public const string SupplierRequired = "Mời bạn chọn nhà cung cấp.";
        public const string LinesRequired = "Phiếu nhập kho phải có ít nhất 1 mặt hàng.";
        public const string QtyMustBePositive = "Số lượng hàng nhập phải lớn hơn 0.";
        public const string WarehouseNotAllowed = "Bạn không có quyền thao tác với kho nhập đã chọn.";
        public const string DeleteConfirm = "Bạn có muốn xóa phiếu nhập kho đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string NumberDuplicate(string number)
        {
            return string.Format("Số phiếu \"{0}\" đã tồn tại. Mời bạn nhập số phiếu khác.", number ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string NegativeStockBlocked(string itemName, decimal currentStock, decimal afterStock)
        {
            return string.Format(
                "Không thể thực hiện vì mặt hàng \"{0}\" sẽ bị âm kho (tồn hiện có: {1}, tồn sau khi sửa: {2}).",
                itemName ?? "",
                FormatQtyVi(currentStock),
                FormatQtyVi(afterStock));
        }

        public static string FormatMoneyVi(decimal value)
        {
            CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
            return value == Math.Truncate(value)
                ? value.ToString("#,##0", vi)
                : value.ToString("#,##0.##", vi);
        }

        public static string FormatQtyVi(decimal value)
        {
            CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
            return value.ToString("#,##0.##", vi);
        }
    }

    /// <summary>Một mặt hàng đủ điều kiện nhập kho (loại rỗng, '0' kiêm vật tư, hoặc '2' nguyên liệu; đang dùng).</summary>
    public sealed class PurchaseProductItem
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
        public decimal BuyPrice { get; set; }

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

    /// <summary>Một dòng chi tiết phiếu nhập kho (TDONHANGCHITIET).</summary>
    public sealed class PurchaseLine
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
        /// <summary>SLNHAPCHUAQUYDOI — số lượng theo ĐVT người dùng chọn.</summary>
        public decimal RawQuantity { get; set; }
        /// <summary>SLNHAP — số lượng đã quy đổi về ĐVT cơ sở.</summary>
        public decimal ConvertedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPct { get; set; }
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

    /// <summary>Kết quả tính tổng tiền phiếu nhập kho (thuần decimal, không làm tròn thêm TONGCONG).</summary>
    public sealed class PurchaseTotals
    {
        public decimal TienHang { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienGiamGia { get; set; }
        public decimal TienThue { get; set; }
        public decimal TongCong { get; set; }
    }

    /// <summary>Đầu phiếu nhập kho (TDONHANG, LOAI = 1) kèm chi tiết và tên hiển thị.</summary>
    public sealed class PurchaseRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public int Loai { get; set; }
        public int Status { get; set; }
        public int PaidFlag { get; set; }

        public string WarehouseId { get; set; }
        public string StoreId { get; set; }
        public string SupplierId { get; set; }
        public string EmployeeId { get; set; }
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

        public decimal PaidAmount { get; set; }
        public PayMethod? PaymentMethod { get; set; }
        public decimal CashAmount { get; set; }
        public decimal TransferAmount { get; set; }
        public string BankAccountId { get; set; }

        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public string WarehouseName { get; set; }
        public string SupplierName { get; set; }
        public string EmployeeName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public List<PurchaseLine> Lines { get; set; }

        public static PurchaseRow NewDefault()
        {
            return new PurchaseRow
            {
                Name = "",
                Date = DateTime.Today,
                Loai = PurchaseConstants.OrderType,
                Status = PurchaseConstants.ActiveStatus,
                PaidFlag = PurchaseConstants.PaidFlag,
                Description = PurchaseConstants.DefaultDescription,
                Note = "",
                PaymentMethod = PayMethod.Cash,
                Lines = new List<PurchaseLine>()
            };
        }
    }

    /// <summary>Bộ lọc lưới Quản lý nhập kho.</summary>
    public sealed class PurchaseFilter
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string WarehouseId { get; set; }
        public string SupplierId { get; set; }
        /// <summary>Giới hạn kho theo cửa hàng của người dùng không phải admin (null = không giới hạn).</summary>
        public List<string> AllowedWarehouseIds { get; set; }
        public string SearchText { get; set; }
    }

    public sealed class PurchaseSaveResult
    {
        public bool Ok { get; set; }
        public string SavedId { get; set; }
        public string SavedNumber { get; set; }
        public string ErrorMessage { get; set; }
        /// <summary>Cảnh báo không chặn (đã lưu nhưng chưa tính lại được giá vốn tự động).</summary>
        public string Warning { get; set; }

        public static PurchaseSaveResult Fail(string message)
        {
            return new PurchaseSaveResult { Ok = false, ErrorMessage = message };
        }
    }

    public sealed class PurchaseDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        /// <summary>Cảnh báo không chặn (đã xóa nhưng chưa tính lại được giá vốn tự động).</summary>
        public string Warning { get; set; }

        public static PurchaseDeleteResult Fail(string message)
        {
            return new PurchaseDeleteResult { Ok = false, ErrorMessage = message };
        }
    }
}
