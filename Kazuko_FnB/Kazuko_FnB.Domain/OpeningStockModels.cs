using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Hằng số định danh, quyền và thông báo chuẩn cho chức năng Tồn kho ban đầu
    /// (TDONHANG LOAI = 97, NAME = 'TONDAU', docs/specs/so-du-dau-ky.md P1).
    /// </summary>
    public static class OpeningStockConstants
    {
        public const int OrderType = 97;
        public const string OrderName = "TONDAU";
        public const int ActiveStatus = PurchaseConstants.ActiveStatus;
        public const int PaidFlag = PurchaseConstants.PaidFlag;
        public const int MaxTextLength = PurchaseConstants.MaxTextLength;

        public const string FunctionName = "Tồn kho ban đầu";
        public const string FunctionDateChange = PurchaseConstants.FunctionDateChange;
        public const string DefaultDescription = "Tồn kho ban đầu";

        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string WarehouseRequired = "Mời bạn chọn kho hàng.";
        public const string WarehouseNotAllowed = "Bạn không có quyền thao tác với kho hàng đã chọn.";
        public const string EmptyNoWarehouse = "Không có kho hàng nào thuộc phạm vi cửa hàng của bạn.";
        public const string EmptyNoItems = "Không có mặt hàng khớp điều kiện lọc.";
        public const string UnsavedChangesConfirm =
            "Dữ liệu tồn kho ban đầu có thay đổi chưa lưu. Bạn có muốn bỏ qua các thay đổi này không?";
        public const string SavedSuccess = "Đã lưu tồn kho ban đầu thành công.";
        public const string SavedDeletedSuccess = "Đã xóa phiếu tồn kho ban đầu do không còn mặt hàng nào có tồn đầu.";
        public const string NoChangesToSave = "Không có mặt hàng nào có tồn đầu để lưu.";

        public static string NegativeQtyBlocked(string itemName)
        {
            return string.Format("Tồn đầu của mặt hàng \"{0}\" không được âm.", itemName ?? "");
        }

        public static string NegativeCostBlocked(string itemName)
        {
            return string.Format("Giá vốn của mặt hàng \"{0}\" không được âm.", itemName ?? "");
        }

        public static string DuplicateOrdersWarning(int count)
        {
            return string.Format(
                "Cảnh báo: kho đang có {0} phiếu tồn kho ban đầu trong CSDL, hệ thống đang dùng phiếu đầu tiên theo ngày.",
                count);
        }
    }

    /// <summary>Đầu phiếu tồn kho ban đầu (TDONHANG, LOAI = 97, NAME = 'TONDAU').</summary>
    public sealed class OpeningStockHeader
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string WarehouseId { get; set; }
        public string StoreId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    /// <summary>Một dòng chi tiết tồn kho ban đầu đã lưu trong TDONHANGCHITIET.</summary>
    public sealed class OpeningStockLineRecord
    {
        public string Id { get; set; }
        public string OrderId { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string UnitId { get; set; }
        public string WarehouseId { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>Một mặt hàng trên lưới Tồn kho ban đầu (mọi hàng theo dõi tồn đang dùng của kho).</summary>
    public sealed class OpeningStockItemRow
    {
        public string ProductId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }

        /// <summary>ID dòng TDONHANGCHITIET nếu mặt hàng đã có tồn đầu trong phiếu 97 của kho; rỗng nếu chưa có.</summary>
        public string LineId { get; set; }
        /// <summary>Giá vốn mặc định từ DMATHANG.GIAVON hiện có khi chưa có dòng tồn đầu.</summary>
        public decimal DefaultCostPrice { get; set; }

        /// <summary>Tồn đầu (nhập, &gt;= 0, theo ĐVT cơ sở).</summary>
        public decimal Quantity { get; set; }
        /// <summary>Giá vốn đơn vị (nhập, &gt;= 0).</summary>
        public decimal CostPrice { get; set; }
        /// <summary>Thành tiền = Round(Tồn đầu × Giá vốn, 0).</summary>
        public decimal Amount { get; set; }

        public decimal OriginalQuantity { get; set; }
        public decimal OriginalCostPrice { get; set; }

        public bool HasExistingLine
        {
            get { return !string.IsNullOrEmpty(LineId); }
        }

        public bool IsDirty
        {
            get
            {
                if (Quantity != OriginalQuantity)
                    return true;
                if ((Quantity > 0m || HasExistingLine) && CostPrice != OriginalCostPrice)
                    return true;
                return false;
            }
        }
    }

    /// <summary>Dữ liệu màn Tồn kho ban đầu của một kho.</summary>
    public sealed class OpeningStockDocument
    {
        public OpeningStockDocument()
        {
            Items = new List<OpeningStockItemRow>();
        }

        public string OrderId { get; set; }
        public string WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public DateTime Date { get; set; }
        public int DuplicateOrderCount { get; set; }
        public string Warning { get; set; }
        public List<OpeningStockItemRow> Items { get; set; }
    }

    /// <summary>Bộ lọc cục bộ trên lưới Tồn kho ban đầu.</summary>
    public sealed class OpeningStockFilter
    {
        public string SearchText { get; set; }
        public bool OnlyEntered { get; set; }
    }

    /// <summary>Tổng cộng lưới Tồn kho ban đầu (thuần decimal).</summary>
    public sealed class OpeningStockTotals
    {
        public int ItemCount { get; set; }
        public int EnteredCount { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
    }

    /// <summary>Dữ liệu yêu cầu lưu tồn kho ban đầu cho một kho.</summary>
    public sealed class OpeningStockSaveRequest
    {
        public OpeningStockSaveRequest()
        {
            Items = new List<OpeningStockItemRow>();
        }

        public string WarehouseId { get; set; }
        public DateTime Date { get; set; }
        public List<OpeningStockItemRow> Items { get; set; }
    }

    /// <summary>Kết quả lưu tồn kho ban đầu.</summary>
    public sealed class OpeningStockSaveResult
    {
        public bool Ok { get; set; }
        public string SavedOrderId { get; set; }
        public bool OrderDeleted { get; set; }
        public int LineCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string ErrorMessage { get; set; }
        /// <summary>Cảnh báo không chặn (trùng phiếu 97 hoặc tự động tính lại giá vốn lỗi).</summary>
        public string Warning { get; set; }

        public static OpeningStockSaveResult Fail(string message)
        {
            return new OpeningStockSaveResult { Ok = false, ErrorMessage = message };
        }
    }
}
