using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số định danh, quyền và thông báo chuẩn cho chức năng Kiểm kê kho (TDONHANG LOAI = 4, docs/specs/kiem-ke-kho.md).</summary>
    public static class StockTakeConstants
    {
        public const int OrderType = 4;
        public const int ActiveStatus = PurchaseConstants.ActiveStatus;
        public const int DeletedStatus = PurchaseConstants.DeletedStatus;
        public const int PaidFlag = PurchaseConstants.PaidFlag;

        public const string DefaultNumberTemplate = "KK(yy)/(*****)";
        public const string DefaultDescription = "Kiểm kê";
        public const string FunctionName = "Kiểm kê kho";
        public const int MaxTextLength = PurchaseConstants.MaxTextLength;

        public const string NotFound = "Phiếu kiểm kê kho không còn tồn tại.";
        public const string WarehouseRequired = "Mời bạn nhập: Kho kiểm kê";
        public const string LinesRequired = "Phiếu kiểm kê kho phải có ít nhất 1 mặt hàng.";
        public const string ActualQtyNegative = "Số lượng thực tế không được âm.";
        public const string WarehouseNotAllowed = "Bạn không có quyền thao tác với kho kiểm kê đã chọn.";
        public const string WarehouseLockedOnEdit = "Không thể đổi kho kiểm kê khi phiếu đã có dòng chi tiết.";
        public const string SystemStockChanged = "Tồn hệ thống đã thay đổi, hãy xem lại.";
        public const string DeleteConfirm = "Bạn có muốn xóa phiếu kiểm kê đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string DuplicateProductInLines(string itemName)
        {
            return string.Format("Mặt hàng \"{0}\" bị trùng dòng trong phiếu kiểm kê. Hãy gộp về một dòng.", itemName ?? "");
        }
    }

    /// <summary>Tông màu hiển thị chênh lệch kiểm kê (đỏ = thiếu / xuất, vàng = thừa / nhập, bình thường = khớp).</summary>
    public enum StockTakeDiffTone
    {
        Match = 0,
        Shortage = 1,
        Surplus = 2
    }

    /// <summary>Một mặt hàng đủ điều kiện kiểm kê (loại rỗng, '0' kiêm vật tư, '2' nguyên liệu; đang dùng).</summary>
    public sealed class StockTakeProductItem
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

    /// <summary>Một dòng chi tiết phiếu kiểm kê kho (TDONHANGCHITIET của phiếu LOAI = 4).</summary>
    public sealed class StockTakeLine
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

        /// <summary>Tồn hệ thống (SLHETHONG) theo ĐVT cơ sở tại thời điểm thêm/tính lại khi lưu.</summary>
        public decimal SystemQuantity { get; set; }
        /// <summary>Số lượng thực tế người dùng nhập theo ĐVT đang chọn trên dòng.</summary>
        public decimal RawActualQuantity { get; set; }
        /// <summary>Số lượng thực tế đã quy đổi về ĐVT cơ sở (lưu vào SLTHUCTE).</summary>
        public decimal ActualQuantityBase { get; set; }
        /// <summary>Chênh lệch = ActualQuantityBase - SystemQuantity (theo ĐVT cơ sở).</summary>
        public decimal Difference { get; set; }
        /// <summary>SLNHAP = SLNHAPCHUAQUYDOI = max(0, SLTHUCTE - SLHETHONG) theo ĐVT cơ sở.</summary>
        public decimal ImportQty { get; set; }
        /// <summary>SLXUAT = SLXUATCHUAQUYDOI = max(0, SLHETHONG - SLTHUCTE) theo ĐVT cơ sở.</summary>
        public decimal ExportQty { get; set; }
        public string Note { get; set; }
        public StockTakeDiffTone Tone { get; set; }

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

    /// <summary>Đầu phiếu kiểm kê kho (TDONHANG, LOAI = 4, không có tiền) kèm chi tiết và tên hiển thị.</summary>
    public sealed class StockTakeRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public int Loai { get; set; }
        public int Status { get; set; }
        public int PaidFlag { get; set; }

        public string WarehouseId { get; set; }
        public string StoreId { get; set; }
        public string Description { get; set; }
        public string Note { get; set; }

        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public string WarehouseName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public List<StockTakeLine> Lines { get; set; }

        public static StockTakeRow NewDefault()
        {
            return new StockTakeRow
            {
                Name = "",
                Date = DateTime.Today,
                Loai = StockTakeConstants.OrderType,
                Status = StockTakeConstants.ActiveStatus,
                PaidFlag = StockTakeConstants.PaidFlag,
                Description = StockTakeConstants.DefaultDescription,
                Note = "",
                Lines = new List<StockTakeLine>()
            };
        }
    }

    /// <summary>Bộ lọc lưới Quản lý kiểm kê.</summary>
    public sealed class StockTakeFilter
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string WarehouseId { get; set; }
        /// <summary>Giới hạn kho theo cửa hàng của người dùng không phải admin (null = không giới hạn).</summary>
        public List<string> AllowedWarehouseIds { get; set; }
        public string SearchText { get; set; }
    }

    /// <summary>Một mặt hàng có tồn khác 0 tại kho nhưng chưa có dòng trong phiếu kiểm kê (spec §5).</summary>
    public sealed class StockTakeUncountedItem
    {
        public string ProductId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string UnitName { get; set; }
        public decimal SystemQuantity { get; set; }
    }

    public sealed class StockTakeSaveResult
    {
        public bool Ok { get; set; }
        public string SavedId { get; set; }
        public string SavedNumber { get; set; }
        public bool SystemStockChanged { get; set; }
        public string ErrorMessage { get; set; }

        public static StockTakeSaveResult Fail(string message)
        {
            return new StockTakeSaveResult { Ok = false, ErrorMessage = message };
        }

        public static StockTakeSaveResult FailSystemStockChanged(string message)
        {
            return new StockTakeSaveResult { Ok = false, SystemStockChanged = true, ErrorMessage = message };
        }
    }

    public sealed class StockTakeDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static StockTakeDeleteResult Fail(string message)
        {
            return new StockTakeDeleteResult { Ok = false, ErrorMessage = message };
        }
    }
}
