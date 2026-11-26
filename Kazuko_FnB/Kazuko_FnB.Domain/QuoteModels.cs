using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số định danh và câu chữ của chức năng Báo giá bán hàng (TBAOGIA).</summary>
    public static class QuoteConstants
    {
        public const string MenuId = "bd2b1cfd-0503-40d1-8f2f-39bb1c7817af";
        public const string StableDescId = "b2d3ffd4-9037-4dd1-b255-ec16dd122e66";
        public const string DetailStableDescId = "fc920f2a-158a-4acf-97d8-701bf00d9812";
        public const string TableName = "TBAOGIA";
        public const string DetailTableName = "TBAOGIACHITIET";
        public const string FunctionId = "7be6722c-aeed-486a-85ef-607b8b4c7407";
        public const string FunctionName = "Báo giá";
        public const string DefaultNumberTemplate = "BG(yy)/(*****)";
        public const int ActiveStatus = 30;
        public const int FlagOn = 30;
        public const int FlagOff = 0;
    }

    /// <summary>Thông báo nguyên văn của chức năng Báo giá bán hàng (docs/specs/bao-gia-ban-hang.md).</summary>
    public static class QuoteText
    {
        public const string EnterQuantity = "Mời bạn nhập số lượng";
        public const string NotFound = "Báo giá không còn tồn tại.";
        public const string DeleteConfirm = "Bạn có muốn thực hiện xóa báo giá đang chọn không?";
        public const string Unsupported = "Chức năng chưa hỗ trợ trong phiên bản này.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string SelectColumnCode = "Mời bạn chọn cột 'Mã hàng'";
        public const string SelectColumnName = "Mời bạn chọn cột 'Tên hàng'";
        public const string SelectColumnQuantity = "Mời bạn chọn cột 'Số lượng'";

        public static string Required(string fieldLabel)
        {
            return string.Format("Trường \"{0}\" không được trống!", fieldLabel);
        }

        public static string NumberDuplicate(string number)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Số phiếu\" đã tồn tại!", number);
        }

        public static string ItemNotFound(string key)
        {
            return string.Format("Mã hàng '{0}' không tồn tại trong hệ thống", key);
        }

        public static string UnitNotFound(string unitName, string itemKey)
        {
            return string.Format("Đơn vị tính '{0}' ở mặt hàng '{1}' không tồn tại trong hệ thống", unitName, itemKey);
        }

        public static string ItemNoUnit(string itemKey, string unitName)
        {
            return string.Format("Mặt hàng '{0}' không có đơn vị tính '{1}'", itemKey, unitName);
        }

        public static string SkippedNegativeNotice(int count)
        {
            return string.Format("Đã bỏ qua {0} dòng có số lượng âm.", count);
        }

        public static string ExcelInvalidNumber(string rawValue, string columnName, string itemKey)
        {
            return string.Format("Giá trị '{0}' ở cột '{1}' của mặt hàng '{2}' không đọc được thành số", rawValue, columnName, itemKey);
        }

        public static string ExcelInvalidValue(string rawValue, string columnName, string itemKey)
        {
            return string.Format("Giá trị '{0}' ở cột '{1}' của mặt hàng '{2}' không hợp lệ", rawValue, columnName, itemKey);
        }
    }

    /// <summary>Đầu phiếu báo giá (TBAOGIA). Tiền dùng decimal, khớp DECIMAL(18,2).</summary>
    public sealed class QuoteRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Ngay { get; set; }
        public string CustomerId { get; set; }
        public string TenKhach { get; set; }
        public string DiaChi { get; set; }
        public string DienThoai { get; set; }
        public string Email { get; set; }
        public string Note { get; set; }
        public decimal TienHang { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienGiamGia { get; set; }
        /// <summary>GIAMTHEOTIEN: true = 30 (theo tiền), false = 0 (theo %).</summary>
        public bool GiamTheoTien { get; set; }
        public decimal TileThue { get; set; }
        public decimal TienThue { get; set; }
        public decimal PhiVanChuyen { get; set; }
        public decimal TongCong { get; set; }
        /// <summary>LOAI: không dùng ở v1, luôn ghi 0.</summary>
        public int Loai { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public List<QuoteLine> Lines { get; set; }

        public static QuoteRow NewDefault()
        {
            return new QuoteRow
            {
                Ngay = DateTime.Today,
                Loai = 0,
                Status = QuoteConstants.ActiveStatus,
                Lines = new List<QuoteLine>()
            };
        }
    }

    /// <summary>Một dòng chi tiết báo giá (TBAOGIACHITIET). ProductCode/ProductName/UnitName chỉ để hiển thị.</summary>
    public sealed class QuoteLine
    {
        public string Id { get; set; }
        public string QuoteId { get; set; }
        public string ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Một hàng trên lưới danh sách Báo giá bán hàng.</summary>
    public sealed class QuoteListRow
    {
        public string Id { get; set; }
        public DateTime Ngay { get; set; }
        public string Name { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string TenKhach { get; set; }
        public string DiaChi { get; set; }
        public string DienThoai { get; set; }
        public string Email { get; set; }
        public decimal TienHang { get; set; }
        public decimal TienGiamGia { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienThue { get; set; }
        public decimal PhiVanChuyen { get; set; }
        public decimal TongCong { get; set; }
        public string Note { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }
    }

    /// <summary>Điều kiện lọc màn danh sách Báo giá (3 lọc: Ngày, Số phiếu, Khách hàng).</summary>
    public sealed class QuoteFilter
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string NumberText { get; set; }
        public string CustomerId { get; set; }
    }

    /// <summary>Mặt hàng chọn vào phiếu báo giá (STATUS = 30).</summary>
    public sealed class QuoteSaleItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string BigUnitId { get; set; }
        public string BigUnitName { get; set; }
        public decimal QuyDoi { get; set; }
        /// <summary>Đơn giá lẻ (DMATHANG.GIABAN).</summary>
        public decimal SellPrice { get; set; }
        /// <summary>Đơn giá chẵn (DMATHANG.GIABANCHAN).</summary>
        public decimal BigSellPrice { get; set; }
        public string Warranty { get; set; }

        public bool HasBigUnit
        {
            get { return !string.IsNullOrEmpty(BigUnitId); }
        }
    }

    /// <summary>Thông tin khách hàng điền tự động khi chọn lookup DKHACHHANG.</summary>
    public sealed class QuoteCustomerInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
    }

    /// <summary>Kết quả tính tổng phiếu báo giá (thuần decimal).</summary>
    public sealed class QuoteTotals
    {
        public decimal TienHang { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienGiamGia { get; set; }
        public decimal TienThue { get; set; }
        public decimal TongCong { get; set; }
    }

    public sealed class QuoteSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }

        public static QuoteSaveResult Fail(string error)
        {
            return new QuoteSaveResult { Ok = false, ErrorMessage = error };
        }
    }

    public sealed class QuoteDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static QuoteDeleteResult Fail(string error)
        {
            return new QuoteDeleteResult { Ok = false, ErrorMessage = error };
        }
    }

    /// <summary>Một ô đọc từ Excel, phân biệt rõ ô kiểu số (IsNumeric = true, chuỗi invariant) với ô chữ.</summary>
    public sealed class QuoteExcelCell
    {
        public string Text { get; set; }
        public bool IsNumeric { get; set; }

        public static QuoteExcelCell TextCell(string text)
        {
            return new QuoteExcelCell { Text = text ?? "", IsNumeric = false };
        }

        public static QuoteExcelCell NumberCell(decimal value)
        {
            return new QuoteExcelCell { Text = value.ToString(CultureInfo.InvariantCulture), IsNumeric = true };
        }
    }

    /// <summary>Ánh xạ cột khi nhập Excel chi tiết báo giá (-1 = không chọn).</summary>
    public sealed class QuoteExcelColumnMap
    {
        public int ItemCol { get; set; }
        public int QuantityCol { get; set; }
        public int UnitCol { get; set; }
        public int PriceCol { get; set; }
        public int DiscountPctCol { get; set; }

        public QuoteExcelColumnMap()
        {
            ItemCol = -1;
            QuantityCol = -1;
            UnitCol = -1;
            PriceCol = -1;
            DiscountPctCol = -1;
        }
    }

    /// <summary>Kết quả đọc và kiểm tra dữ liệu nhập từ Excel.</summary>
    public sealed class QuoteExcelImportResult
    {
        public bool Ok { get; set; }
        public List<string> Errors { get; set; }
        public List<QuoteLine> Lines { get; set; }
        public int SkippedEmptyOrZeroCount { get; set; }
        public int SkippedNegativeCount { get; set; }

        public QuoteExcelImportResult()
        {
            Errors = new List<string>();
            Lines = new List<QuoteLine>();
        }
    }
}
