using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng trong khung "IN MÃ VẠCH" (chọn mặt hàng và số lượng tem cần in).</summary>
    public sealed class BarcodePrintRequestItem
    {
        public string ProductId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Dữ liệu một mặt hàng đọc lại từ CSDL (DMATHANG + bảng danh mục liên kết) để đổ vào mẫu tem mã vạch.
    /// Quy tắc N3: khi không có quyền Xem giá nhập, GIANHAP/GIAVON/HOAHONG không được đọc từ CSDL và luôn bằng 0.
    /// </summary>
    public sealed class BarcodeProductRecord
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string GroupName { get; set; }
        public string UnitName { get; set; }
        public decimal SellPrice { get; set; }
        public decimal SellPrice2 { get; set; }
        public decimal SellPrice3 { get; set; }
        public decimal SellPrice4 { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal Commission { get; set; }
        public decimal MinStock { get; set; }
        public decimal MaxStock { get; set; }
        public string Warranty { get; set; }
        public string Barcode { get; set; }
        public string ManufacturerName { get; set; }
        public string BigUnitName { get; set; }
        public decimal QuyDoi { get; set; }
        public decimal BigSellPrice { get; set; }
        public string ConsignmentName { get; set; }
        public decimal DefaultDiscountRate { get; set; }
        public decimal DefaultDiscountAmount { get; set; }
    }

    /// <summary>Một mặt hàng đã đọc lại từ CSDL kèm số lượng tem cần in (theo thứ tự dòng trong khung).</summary>
    public sealed class BarcodePrintEntry
    {
        public BarcodeProductRecord Product { get; set; }
        public int Quantity { get; set; }
    }
}
