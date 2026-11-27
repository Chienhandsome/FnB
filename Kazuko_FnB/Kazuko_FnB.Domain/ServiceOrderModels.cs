using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Kiểu quay vòng số hóa đơn (SCONFIG.SoHoaDonQuayVongTheo, spec §6.5).</summary>
    public enum InvoiceCycleMode
    {
        ByDay = 0,
        ByMonth = 1,
        ByYear = 2,
        NoCycle = 3
    }

    /// <summary>Chế độ chọn ngày giao dịch (SCONFIG.CachChonNgayGiaoDich, spec §6.5).</summary>
    public enum BusinessDateMode
    {
        ByOpenDate = 0,
        ByPaymentDate = 1
    }

    /// <summary>
    /// Trạng thái bàn trên sơ đồ khu vực (spec §3.1), xếp theo thứ tự ưu tiên từ cao xuống thấp:
    /// TempPrinted &gt; InUse &gt; NeedCleaning &gt; Cleaning &gt; UnderRepair &gt; Reserved &gt; Empty.
    /// </summary>
    public enum TableStatus
    {
        Empty = 0,
        Reserved = 1,
        UnderRepair = 2,
        Cleaning = 3,
        NeedCleaning = 4,
        InUse = 5,
        TempPrinted = 6
    }

    /// <summary>Hằng số menu, quyền chức năng (spec §4) và thông báo nguyên văn (spec §3.2–§3.5, §6.5, §6.6).</summary>
    public static class ServiceOrderConstants
    {
        public const string MenuId = "8e360da4-b18e-43ba-a0fe-aac67914aabf";
        public const string FunctionId = "db591d0e-d671-42d0-b3b1-f705345cc641";
        public const string FunctionName = "Sử dụng dịch vụ";

        // SFUNCTION.ID theo bảng §4 của docs/specs/su-dung-dich-vu.md.
        public const string FuncChuyenBan = "f3401c18-c9ce-4d97-92e3-ba1542671edb";
        public const string FuncGopBan = "7a46b358-71e7-4400-b480-e9f47f580f93";
        public const string FuncHuyHoaDon = "912818cc-88a5-4b26-8aa3-8a9bb4a7c65b";
        public const string FuncThanhToan = "4ceaa330-c51a-41e9-80ae-a28ad69433b8";
        public const string FuncInTamTinh = "d057c064-cedc-4429-bc41-e4e8e2472994";
        public const string FuncInLaiBill = "679ea71e-9921-4c39-9df3-74d292594de5";
        public const string FuncXoaGiamMon = "83a0fd04-067c-46fd-857e-01cc70af7a23";
        public const string FuncXoaGiamMonSauInBep = "d6579615-3967-4a7f-888e-2678f1abac4d";
        public const string FuncDuocGiamSauInTamTinh = "af0cd926-4f9e-4e96-9049-a07716a1e038";
        public const string FuncSuaDonGia = "0f06705f-f59a-4252-893b-680a2baa3e27";
        public const string FuncGiamGiaMatHang = "267fdc55-2f47-42d1-8ca7-4a6d92e73b15";
        public const string FuncThayDoiGiamGiaTong = "e5f93aa1-50ff-4ea3-ab8b-117254c3c70e";
        public const string FuncThayDoiPhiDichVu = "326162e4-b204-4bb5-a8c5-c782c8cdc462";
        public const string FuncXemDonGia = "8f35f8c3-f141-4099-a2d2-a3328d3ec949";
        public const string FuncChuyenMatHang = "e35fe6b9-64b9-4461-87be-17a0565e914a";
        public const string FuncThongKe = "43402cbf-c1b8-4a96-b10f-31b233925801";

        public const int TableLockRenewSeconds = 20;
        public const int TableLockExpireSeconds = 120;
        public const int TableMapRefreshIntervalMs = 20000;

        // Thông báo nguyên văn (§3.2–§3.5, §6.4–§6.6).
        public const string SelectTableFirst = "Mời bạn chọn bàn trước khi mở bàn";
        public const string TableOccupied = "Bàn này đang có khách";
        public const string BillAlreadyPaid = "Hóa đơn này đã thanh toán, không thể thay đổi";
        public const string SelectProductFirst = "Mời bạn chọn mặt hàng trước!";
        public const string EnterQuantity = "Mời bạn nhập số lượng!";
        public const string ProductDeleted = "Mặt hàng này đã bị xóa ra khỏi danh mục";
        public const string ProductTempLocked = "Mặt hàng này tạm khóa, không thể sử dụng";
        public const string HourlyServiceNotSupported = "Dịch vụ theo giờ không được hỗ trợ trong phiên bản hiện tại.";
        public const string CannotReduceTempPrinted = "Không thể giảm vì phiếu đã in tạm tính";
        public const string CannotDeleteTempPrinted = "Không thể xóa vì phiếu đã in tạm tính";
        public const string CannotSetQtyTempPrinted = "Phiếu đã in tạm tính, mời bạn sử dụng chức năng thêm hoặc click nút dấu '+' phía bên trái";
        public const string CannotDeleteAfterKitchenPrint = "Không được xóa sau khi in xuống bếp!";
        public const string SelectLineToReduce = "Mời bạn chọn mặt hàng trong hóa đơn để giảm số lượng";
        public const string DeleteLineConfirm = "Bạn có muốn xóa mặt hàng đang chọn ra khỏi hóa đơn không?";
        public const string DeleteReasonRequired = "Mời bạn nhập lý do xóa món.";
        public const string ReducePasswordBlocked = "Hệ thống đang bật yêu cầu mật khẩu giảm đồ (chưa hỗ trợ nhập mật khẩu giảm đồ nên không thể giảm/xóa món).";
        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string NotSupportedYet = "Chức năng này chưa hỗ trợ trong phiên bản hiện tại.";
        public const string InvoiceDataErrorBase = "Tổng tiền hóa đơn bị sai, vui lòng kiểm tra lại";
        public const string InvoiceDataErrorComponent = "Tổng tiền hóa đơn bị sai, vui lòng kiểm tra lại (1)";
        public const string InvoiceDataErrorTotal = "Tổng tiền hóa đơn bị sai, vui lòng kiểm tra lại (2)";

        public static string TableLockedByOther(string tableName, string account)
        {
            return string.Format("{0} NÀY ĐANG ĐƯỢC THAO TÁC BỞI: {1}", tableName ?? "BÀN", account ?? "");
        }

        public static string UnderRepairWarning(string tableName)
        {
            return string.Format("Bàn '{0}' đang có thông báo sửa chữa chưa hoàn tất. Bạn có vẫn muốn mở bàn không?", tableName ?? "");
        }

        public static string CannotReduceBelowKitchenPrint(decimal printedQty)
        {
            return string.Format("Bạn đã in xuống bếp số lượng {0}. Không thể giảm.", FormatPlainQty(printedQty));
        }

        public static string CannotReduceExceedsCurrent(decimal reduceQty, decimal currentQty)
        {
            return string.Format("Không thể giảm '{0}' vì số lượng hiện tại chỉ có '{1}'",
                FormatPlainQty(reduceQty), FormatPlainQty(currentQty));
        }

        public static string InsufficientStock(string itemName, decimal available, decimal required)
        {
            return string.Format("Mặt hàng '{0}' không đủ tồn kho (tồn hiện có: {1}, cần xuất: {2}).",
                itemName ?? "", FormatPlainQty(available), FormatPlainQty(required));
        }

        public static string CancelBlockedHasBookingDeposit(string receiptNumbers)
        {
            return string.Format(
                "Không thể hủy hóa đơn vì phiếu đặt hàng đang có phiếu thu tiền cọc còn hiệu lực ({0}), vui lòng xử lý phiếu thu cọc trước khi hủy.",
                string.IsNullOrEmpty(receiptNumbers) ? "" : receiptNumbers);
        }

        public static string TableStatusLabel(TableStatus status)
        {
            switch (status)
            {
                case TableStatus.TempPrinted: return "Đã in tạm tính";
                case TableStatus.InUse: return "Đang sử dụng";
                case TableStatus.NeedCleaning: return "Báo dọn";
                case TableStatus.Cleaning: return "Dọn dẹp";
                case TableStatus.UnderRepair: return "Sửa chữa";
                case TableStatus.Reserved: return "Đặt trước";
                default: return "Trống";
            }
        }

        private static string FormatPlainQty(decimal value)
        {
            return value == Math.Truncate(value)
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Định dạng số hóa đơn theo 4 kiểu quay vòng (spec §6.5). Đặt ở Domain để Data và Services dùng chung.</summary>
    public static class InvoiceNumberFormat
    {
        public static InvoiceCycleMode NormalizeMode(int rawMode)
        {
            if (rawMode >= (int)InvoiceCycleMode.ByDay && rawMode <= (int)InvoiceCycleMode.NoCycle)
                return (InvoiceCycleMode)rawMode;
            return InvoiceCycleMode.ByMonth;
        }

        public static DateTime PeriodKey(InvoiceCycleMode mode, DateTime date)
        {
            DateTime d = date.Date;
            switch (mode)
            {
                case InvoiceCycleMode.ByDay:
                    return d;
                case InvoiceCycleMode.ByMonth:
                    return new DateTime(d.Year, d.Month, 1);
                case InvoiceCycleMode.ByYear:
                    return new DateTime(d.Year, 1, 1);
                default:
                    return new DateTime(2000, 1, 1);
            }
        }

        public static DateTime? PeriodEndExclusive(InvoiceCycleMode mode, DateTime periodKey)
        {
            switch (mode)
            {
                case InvoiceCycleMode.ByDay:
                    return periodKey.AddDays(1);
                case InvoiceCycleMode.ByMonth:
                    return periodKey.AddMonths(1);
                case InvoiceCycleMode.ByYear:
                    return periodKey.AddYears(1);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 0 ngày: ddMM + 4 số; 1 tháng: MMyy + 5 số; 2 năm: yy + 6 số; 3 không quay vòng: 7 số.
        /// </summary>
        public static string Format(InvoiceCycleMode mode, DateTime date, int sequence)
        {
            int seq = sequence < 0 ? 0 : sequence;
            switch (mode)
            {
                case InvoiceCycleMode.ByDay:
                    return date.ToString("ddMM", CultureInfo.InvariantCulture) + seq.ToString("0000", CultureInfo.InvariantCulture);
                case InvoiceCycleMode.ByMonth:
                    return date.ToString("MMyy", CultureInfo.InvariantCulture) + seq.ToString("00000", CultureInfo.InvariantCulture);
                case InvoiceCycleMode.ByYear:
                    return date.ToString("yy", CultureInfo.InvariantCulture) + seq.ToString("000000", CultureInfo.InvariantCulture);
                default:
                    return seq.ToString("0000000", CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>Khu vực hiển thị trên sơ đồ bàn (DKHUVUC STATUS = 30).</summary>
    public sealed class ServiceAreaInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string WarehouseId { get; set; }
        public string PriceLevelId { get; set; }
        public string IconId { get; set; }
        /// <summary>0 = tab trên, 1 = tab dưới (§3.1).</summary>
        public int ShowTab { get; set; }
        public string SortOrder { get; set; }
    }

    /// <summary>Một bàn trên sơ đồ khu vực kèm thông tin trạng thái thời gian thực (§3.1, §3.2).</summary>
    public sealed class ServiceTableInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AreaId { get; set; }
        public string AreaName { get; set; }
        public string AreaWarehouseId { get; set; }
        public string AreaPriceLevelId { get; set; }
        public string DisplayGroupId { get; set; }
        public string DisplayGroupName { get; set; }
        /// <summary>Cột DBAN.TRANGTHAI (1 = Báo dọn, 2 = Dọn dẹp).</summary>
        public int TableFlag { get; set; }
        public string ActiveBillId { get; set; }
        public string BillNote { get; set; }
        public DateTime? BillStartTime { get; set; }
        public int TempPrintCount { get; set; }
        public string LockedByAccount { get; set; }
        public DateTime? LockedAt { get; set; }
        public bool IsLockedByOther { get; set; }
        public int UnderRepairCount { get; set; }
        public string ReservationBookingId { get; set; }
        public string ReservationText { get; set; }
        public TableStatus Status { get; set; }
    }

    /// <summary>Đầu phiếu hóa đơn nhà hàng (TDONHANG LOAI = 0).</summary>
    public sealed class ServiceOrderHeader
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int SoHd { get; set; }
        public DateTime Ngay { get; set; }
        public DateTime BatDau { get; set; }
        public string TableId { get; set; }
        public string TableName { get; set; }
        public string AreaId { get; set; }
        public string AreaName { get; set; }
        public string WarehouseId { get; set; }
        public string PriceLevelId { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string StoreId { get; set; }
        public int GuestCount { get; set; }
        public int PaidFlag { get; set; }
        public int TempPrintCount { get; set; }
        public string GroupGuid { get; set; }
        public string BookingId { get; set; }
        public string Note { get; set; }

        public decimal TienHang { get; set; }
        public decimal TienHangChuaGiam { get; set; }
        public decimal GiamGiaMatHang { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienGiamGia { get; set; }
        public bool GiamTheoTien { get; set; }
        /// <summary>Cờ GIAMTONGTHEOTIEN = 30 trên TDONHANG: giảm giá tổng đang do khuyến mại tự áp (0 = giảm giá tay/mặc định).</summary>
        public bool BillDiscountFromPromo { get; set; }
        /// <summary>Tên đợt khuyến mại giảm tổng hóa đơn đang áp (dữ liệu hiển thị, không lưu DB).</summary>
        public string BillPromotionName { get; set; }
        public decimal TilePhiDichVu { get; set; }
        public decimal PhiDichVu { get; set; }
        public bool PhiDichVuTheoTien { get; set; }
        public decimal TileThue { get; set; }
        public decimal TienThue { get; set; }
        public decimal TongCong { get; set; }
        /// <summary>Tổng TONGCONG các bill khác cùng NHOMGUID (biến tính, không phải cột DB).</summary>
        public decimal BillKhac { get; set; }
        /// <summary>TONGCONG + BILLKHAC (biến tính, không phải cột DB).</summary>
        public decimal TienPhaiTra { get; set; }
        public List<ServiceOrderLine> Lines { get; set; }

        public ServiceOrderHeader()
        {
            Lines = new List<ServiceOrderLine>();
            GuestCount = 1;
        }
    }

    /// <summary>Một dòng chi tiết trên hóa đơn nhà hàng (TDONHANGCHITIET).</summary>
    public sealed class ServiceOrderLine
    {
        public string Id { get; set; }
        public string OrderId { get; set; }
        public string ProductId { get; set; }
        public string ProductGroupId { get; set; }
        public string ProductTypeId { get; set; }
        public string DrinkTypeId { get; set; }
        public string ItemName { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string WarehouseId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool DiscountByMoney { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; }
        public string ComboId { get; set; }
        public string ComboParentId { get; set; }
        public decimal ComboQty { get; set; }
        public int XuatVatTu { get; set; }
        /// <summary>Cột TDONHANGCHITIET.KHUYENMAI: 30 = dòng tặng 0 đồng hoặc dòng đang nhận giảm giá từ khuyến mại; 0 = bình thường/giảm tay.</summary>
        public int KhuyenMai { get; set; }
        /// <summary>Nhãn hiển thị nguồn khuyến mại trên lưới (vd "KM: Tên đợt", không ghi đè NOTE của dòng bán thường).</summary>
        public string PromotionLabel { get; set; }
        public DateTime? TimeCreated { get; set; }

        public bool IsComboChild
        {
            get { return !string.IsNullOrEmpty(ComboParentId); }
        }

        public bool IsMaterialExport
        {
            get { return XuatVatTu == 30; }
        }

        public bool IsPromoGift
        {
            get { return KhuyenMai == 30 && UnitPrice == 0m && !IsComboChild && !IsMaterialExport; }
        }

        public bool IsPromoDiscounted
        {
            get { return KhuyenMai == 30 && UnitPrice > 0m && !IsComboChild && !IsMaterialExport; }
        }
    }

    /// <summary>Mặt hàng hiển thị trong cột chọn món bên phải màn Sử dụng dịch vụ.</summary>
    public sealed class ServiceMenuItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string GroupId { get; set; }
        public string DrinkTypeId { get; set; }
        public string ProductTypeId { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal SellPrice { get; set; }
        public decimal SellPrice2 { get; set; }
        public decimal SellPrice3 { get; set; }
        public decimal SellPrice4 { get; set; }
        public decimal CostPrice { get; set; }
        public bool PriceByTime { get; set; }
        public bool TempLocked { get; set; }
        public bool HasStock { get; set; }
        public int Status { get; set; }
    }

    /// <summary>Thành phần của combo hoặc công thức định lượng (DDINHLUONG).</summary>
    public sealed class ServiceRecipeComponent
    {
        public string MaterialId { get; set; }
        public string MaterialName { get; set; }
        public string MaterialTypeId { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public bool HasStock { get; set; }
    }

    /// <summary>Kết quả tính tổng bill thuần từ OrderCalculator (§3.6).</summary>
    public sealed class ServiceOrderTotals
    {
        public decimal TienHang { get; set; }
        public decimal TienHangChuaGiam { get; set; }
        public decimal GiamGiaMatHang { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienGiamGia { get; set; }
        public decimal TilePhiDichVu { get; set; }
        public decimal PhiDichVu { get; set; }
        public decimal TienThue { get; set; }
        public decimal TongCong { get; set; }
        public decimal TienPhaiTra { get; set; }
    }

    /// <summary>Kết quả kiểm tra tính nhất quán hóa đơn (DuLieuHoaDonOk, §6.5).</summary>
    public sealed class ServiceOrderValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }

        public static ServiceOrderValidationResult Valid()
        {
            return new ServiceOrderValidationResult { IsValid = true };
        }

        public static ServiceOrderValidationResult Invalid(string message)
        {
            return new ServiceOrderValidationResult { IsValid = false, ErrorMessage = message };
        }
    }

    /// <summary>Đầu vào giảm giá theo 4 nhóm loại đồ (% theo nhóm).</summary>
    public sealed class GroupDiscountInput
    {
        public decimal FoodRate { get; set; }
        public decimal DrinkRate { get; set; }
        public decimal ServiceRate { get; set; }
        public decimal OtherRate { get; set; }
    }

    /// <summary>Kết quả chuẩn cho các thao tác nghiệp vụ của TableService.</summary>
    public sealed class ServiceOpResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string PromotionWarning { get; set; }
        public bool NeedsConfirmation { get; set; }
        public bool NeedsReLogin { get; set; }
        public string RequiredFunctionId { get; set; }
        public ServiceOrderHeader Bill { get; set; }

        public static ServiceOpResult Success(ServiceOrderHeader bill = null, string promotionWarning = null)
        {
            return new ServiceOpResult { Ok = true, Bill = bill, PromotionWarning = promotionWarning };
        }

        public static ServiceOpResult Fail(string message)
        {
            return new ServiceOpResult { Ok = false, ErrorMessage = message };
        }

        public static ServiceOpResult Confirm(string message)
        {
            return new ServiceOpResult { Ok = false, NeedsConfirmation = true, ErrorMessage = message };
        }

        public static ServiceOpResult ReLogin(string functionId, string message = null)
        {
            return new ServiceOpResult
            {
                Ok = false,
                NeedsReLogin = true,
                RequiredFunctionId = functionId,
                ErrorMessage = message ?? ServiceOrderConstants.NoPermission
            };
        }
    }
}
