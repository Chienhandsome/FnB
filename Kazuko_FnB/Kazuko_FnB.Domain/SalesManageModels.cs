using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Chế độ hoạt động của bộ máy hóa đơn nhà hàng (spec §1.1, §8).</summary>
    public enum OrderMode
    {
        Sale = 0,
        Manage = 1,
        Adjust = 2
    }

    /// <summary>Chế độ màn Quản lý bán hàng / Điều chỉnh hóa đơn (spec đầu trang).</summary>
    public enum BillManageMode
    {
        Manage = 0,
        Adjust = 1
    }

    /// <summary>Hằng số menu, quyền chức năng và thông báo chuẩn cho Quản lý bán hàng + Điều chỉnh hóa đơn.</summary>
    public static class SalesManageConstants
    {
        // SMENU.ID (spec đầu trang)
        public const string ManageMenuId = "65d26e6c-682f-40cf-84d3-ba02343c6223";
        public const string AdjustMenuId = "a6bfe97d-7e61-4c79-a6a3-e8437da57e5d";

        // SFUNCTION.ID (spec đầu trang, §1.1, §3.4)
        public const string FuncQuanLyBanHang = "7051d85d-341e-4b84-a4f6-eff601bbbdb8";
        public const string FuncDieuChinhHoaDon = "607326cf-2a24-476b-a069-404eeca616db";
        public const string FuncThayDoiKhachHangDieuChinh = "e818a646-dd01-4fd1-a500-eb86af1cf313";

        // SCONFIG khóa bổ sung (§1.1, §3.5)
        public const string ConfigAllowChangeDate = "ChoPhepThayDoiNgayTrenHoaDon";
        public const string ConfigEnableCheckItems = "KichHoatChucNangKiemDo";

        // Thông báo chuẩn (§2.2, §2.4, §5 P3/P4, §6.3, §6.4)
        public const string UpdatedSuccessMessage = "Đã cập nhật hóa đơn thành công";
        public const string UnsavedChangesConfirm = "Bạn có thay đổi chưa cập nhật, bỏ qua?";
        public const string GroupBillEditBlocked = "Không thể sửa hóa đơn thuộc nhóm thanh toán chung. Chỉ được xem và in lại.";
        public const string GroupBillReturnBlocked = "Không thể trả đồ trên hóa đơn thuộc nhóm thanh toán chung.";
        public const string GroupBillCancelBlocked = "Không thể hủy hóa đơn thuộc nhóm thanh toán chung.";
        public const string GroupBillBannerText = "Hóa đơn thuộc nhóm thanh toán chung: chỉ xem và in lại, không thể sửa, trả đồ hoặc hủy.";
        public const string SelectBillFirst = "Mời bạn chọn hóa đơn trong danh sách.";
        public const string NoReturnableItems = "Hóa đơn không có mặt hàng nào có thể trả đồ.";
        public const string BillNotPaidOrMissing = "Hóa đơn không tồn tại hoặc chưa thanh toán.";
        public const string AdjustModeOnlyCustomerAndReturn = "Chế độ Điều chỉnh hóa đơn chỉ cho phép đổi khách hàng và trả đồ.";

        /// <summary>Hỏi xác nhận hủy phiếu (P4 / §6.3: sửa lỗi chính tả "muỗn" -> "muốn").</summary>
        public static string CancelBillConfirm(string billNumber)
        {
            return string.Format("Bạn có muốn hủy hóa đơn số '{0}' không?", billNumber ?? "");
        }

        /// <summary>Chặn hủy hóa đơn mở từ phiếu đặt còn phiếu thu cọc còn hiệu lực (A1).</summary>
        public static string CancelBlockedHasBookingDeposit(string receiptNumbers)
        {
            return ServiceOrderConstants.CancelBlockedHasBookingDeposit(receiptNumbers);
        }

        /// <summary>Cảnh báo khi hủy hóa đơn đã thanh toán có phiếu thu/chi liên quan (§6.3).</summary>
        public static string RelatedVouchersCancelWarning(int count, decimal totalIncome, decimal totalExpense)
        {
            return string.Format(
                "Hóa đơn này có {0} phiếu thu/chi liên quan (tổng thu: {1}, tổng chi: {2}). " +
                "Các phiếu thu/chi này sẽ KHÔNG tự động bị xóa khi hủy hóa đơn — mời bạn kiểm tra và xử lý riêng. " +
                "Bạn có chắc chắn muốn tiếp tục hủy hóa đơn không?",
                count, FormatMoneyVi(totalIncome), FormatMoneyVi(totalExpense));
        }

        /// <summary>Cảnh báo khi sửa bill còn cọc dư (§6.4, §7: ghi rõ số đã chi trước đó và lưu ý phiếu app gốc).</summary>
        public static string AlreadyRefundedDepositWarning(decimal alreadyRefunded, decimal fullExcess)
        {
            if (alreadyRefunded > 0m)
            {
                return string.Format(
                    "Cảnh báo: hóa đơn này đã chi trước đó {0} (chỉ tính các phiếu chi có gắn mã hóa đơn; phiếu do app gốc tạo không gắn mã hóa đơn nên số đã chi có thể thiếu). " +
                    "Phiếu chi đang mở sẵn toàn bộ phần dư {1} — mời bạn kiểm tra và tự sửa số tiền cần chi thêm.",
                    FormatMoneyVi(alreadyRefunded), FormatMoneyVi(fullExcess));
            }
            return string.Format(
                "Lưu ý: phiếu chi đang mở sẵn toàn bộ phần cọc dư {0}. Phiếu chi trả cọc do app gốc tạo trước đây không gắn mã hóa đơn nên số đã chi có thể thiếu — mời bạn kiểm tra trước khi lưu.",
                FormatMoneyVi(fullExcess));
        }

        public static string FormatMoneyVi(decimal value)
        {
            CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
            return value == Math.Truncate(value)
                ? value.ToString("#,##0", vi)
                : value.ToString("#,##0.##", vi);
        }
    }

    /// <summary>Một dòng hóa đơn đã thanh toán trên lưới trái (TDONHANG LOAI = 0, DATHANHTOAN = 30 — spec §1).</summary>
    public sealed class PaidBillListItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Ngay { get; set; }
        public string CustomerId { get; set; }
        public string CustomerCode { get; set; }
        public string CustomerName { get; set; }
        public decimal TongCong { get; set; }
        public decimal TienGiamGia { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienThue { get; set; }
        public decimal TileThue { get; set; }
        public decimal TienHang { get; set; }
        public DateTime? GioThanhToan { get; set; }
        public decimal KhachDua { get; set; }
        public decimal TraLai { get; set; }
        public decimal Voucher { get; set; }
        public decimal TheTraTruoc { get; set; }
        public decimal TruTichLuy { get; set; }
        public decimal TienMat { get; set; }
        public decimal ChuyenKhoan { get; set; }
        public decimal The { get; set; }
        public string TableId { get; set; }
        public string TableName { get; set; }
        public DateTime? BatDau { get; set; }
        public DateTime? KetThuc { get; set; }
        public decimal TienGio { get; set; }
        public decimal TileGiamGiaGio { get; set; }
        public decimal TienGiamGiaGio { get; set; }
        public int GuestCount { get; set; }
        public decimal PhiDichVu { get; set; }
        public decimal TilePhiDichVu { get; set; }
        public string OrderNumbers { get; set; }
        public string GroupGuid { get; set; }
        public string Note { get; set; }
        public string BookingId { get; set; }
        public string UserThanhToanId { get; set; }
        public string UserThanhToanName { get; set; }

        public bool IsGroupBill
        {
            get { return !string.IsNullOrEmpty(GroupGuid); }
        }
    }

    /// <summary>Dữ liệu thanh toán đã lưu trên hóa đơn để nạp lại vào hộp Xác nhận thanh toán khi sửa (§2.2).</summary>
    public sealed class ExistingPaymentSnapshot
    {
        public decimal Cash { get; set; }
        public decimal KhachDua { get; set; }
        public decimal TraLai { get; set; }
        public decimal Transfer { get; set; }
        public decimal Card { get; set; }
        public string AccountId { get; set; }
        public decimal Voucher { get; set; }
        public string VoucherId { get; set; }
        public decimal PrepaidCard { get; set; }
        public string PrepaidCardId { get; set; }
        public decimal PointsMoney { get; set; }
        public decimal PointsUsed { get; set; }
        public decimal DatTruoc { get; set; }
        public decimal NoCu { get; set; }
        public decimal TienThanhToan { get; set; }
        public decimal CongNo { get; set; }
        public string EmployeeId { get; set; }
        public string OrderNumbers { get; set; }
        public string UserThanhToanId { get; set; }
        public DateTime? GioThanhToan { get; set; }
    }

    /// <summary>Đầy đủ dữ liệu một bill đã thanh toán khi nạp vào khung sửa bên phải.</summary>
    public sealed class PaidBillDetail
    {
        public ServiceOrderHeader Header { get; set; }
        public ExistingPaymentSnapshot Payment { get; set; }
        public DateTime? GioTraDo { get; set; }

        public bool IsGroupBill
        {
            get { return Header != null && !string.IsNullOrEmpty(Header.GroupGuid); }
        }
    }

    /// <summary>Một nhóm mặt hàng có thể trả đồ trong hộp Trả đồ (§2.3: SLTRALAI + SLSUDUNG = SLORDER).</summary>
    public sealed class ReturnableItemRow
    {
        public string GroupKey { get; set; }
        public string ProductId { get; set; }
        public string ProductTypeId { get; set; }
        public string DrinkTypeId { get; set; }
        public string ItemName { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string WarehouseId { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal DiscountRate { get; set; }
        /// <summary>Tổng số lượng đã gọi còn lại (&gt; 0).</summary>
        public decimal OrderQty { get; set; }
        /// <summary>Số lượng khách thực sử dụng (SLSUDUNG).</summary>
        public decimal UsedQty { get; set; }
        /// <summary>Số lượng trả lại (SLTRALAI).</summary>
        public decimal ReturnQty { get; set; }
    }

    /// <summary>Tóm tắt phiếu thu/chi liên quan đến một bill (dùng để cảnh báo khi hủy bill đã thanh toán, §6.3).</summary>
    public sealed class BillRelatedVouchersSummary
    {
        public int Count { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
    }

    /// <summary>Tập cờ bật/ẩn điều khiển của màn hóa đơn theo chế độ (OrderModePolicy, §1.1, §6.3).</summary>
    public sealed class OrderModeCapabilities
    {
        public OrderMode Mode { get; set; }
        public bool RightPaneEnabled { get; set; }
        public bool DateRangeEditable { get; set; }
        public bool ShowCancelBillButton { get; set; }
        public bool CancelBillEnabled { get; set; }
        public bool ShowMoveMergeStatsButtons { get; set; }
        public bool ShowMenuPicker { get; set; }
        public bool ShowLineEditButtons { get; set; }
        public bool ShowReturnButton { get; set; }
        public string ReturnButtonText { get; set; }
        public bool ReturnButtonEnabled { get; set; }
        public bool ShowReprintButton { get; set; }
        public bool ReprintButtonEnabled { get; set; }
        public bool ShowUpdateButton { get; set; }
        public bool UpdateButtonEnabled { get; set; }
        public bool HeaderCustomerEnabled { get; set; }
        public bool HeaderGuestsEnabled { get; set; }
        public bool HeaderNoteEnabled { get; set; }
        public bool HeaderDateEditable { get; set; }
        public bool TotalsDiscountEnabled { get; set; }
        public bool TotalsFeeEnabled { get; set; }
        public bool TotalsTaxEnabled { get; set; }
        public bool CanAddLine { get; set; }
        public bool CanModifySelectedLine { get; set; }
        public bool CanEditUnitPrice { get; set; }
        public bool CanEditLineDiscount { get; set; }
        public bool CanApplyGroupDiscount { get; set; }
        public bool ShowGroupBillWarning { get; set; }
    }

    /// <summary>Yêu cầu cập nhật hóa đơn đã thanh toán trong một transaction duy nhất (P1, §2.2).</summary>
    public sealed class BillUpdateRequest
    {
        public BillManageMode Mode { get; set; }
        public ServiceOrderHeader WorkingBill { get; set; }
        public DateTime OriginalDate { get; set; }
        public bool AllowDateChange { get; set; }
        public string CustomerId { get; set; }
        public string EmployeeId { get; set; }
        public string OrderNumbers { get; set; }
        public decimal CashTendered { get; set; }
        public decimal Transfer { get; set; }
        public decimal Card { get; set; }
        public string AccountId { get; set; }
        public decimal Voucher { get; set; }
        public string VoucherId { get; set; }
        public decimal PrepaidCard { get; set; }
        public string PrepaidCardId { get; set; }
        public decimal PointsMoney { get; set; }
        public bool PrintAfterClose { get; set; }
        public bool ConfirmDebt { get; set; }
        public List<ActivityLogRequest> PendingLogs { get; set; }
        /// <summary>Chỉ dùng cho selftest mô phỏng lỗi giữa chừng để kiểm rollback toàn bộ transaction.</summary>
        public Action TestHookAfterUpdate { get; set; }

        public BillUpdateRequest()
        {
            PendingLogs = new List<ActivityLogRequest>();
        }
    }

    /// <summary>Yêu cầu hủy phiếu đã thanh toán (chỉ Quản lý bán hàng, §2.4, §6.3).</summary>
    public sealed class BillCancelPaidRequest
    {
        public string BillId { get; set; }
        public string Reason { get; set; }
        public bool ConfirmRelatedVouchers { get; set; }
        /// <summary>Chỉ dùng cho selftest mô phỏng lỗi giữa chừng sau khi ghi TDONHANGHUY để kiểm rollback.</summary>
        public Action TestHookAfterSnapshot { get; set; }
    }

    /// <summary>Kết quả hủy phiếu đã thanh toán.</summary>
    public sealed class BillCancelPaidResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public bool NeedsReLogin { get; set; }
        public string RequiredFunctionId { get; set; }
        public bool NeedsVoucherWarningConfirmation { get; set; }
        public BillRelatedVouchersSummary RelatedVouchers { get; set; }
        public string CancelledBillHuyId { get; set; }

        public static BillCancelPaidResult Fail(string message)
        {
            return new BillCancelPaidResult { Ok = false, ErrorMessage = message };
        }

        public static BillCancelPaidResult ReLogin(string functionId)
        {
            return new BillCancelPaidResult
            {
                Ok = false,
                NeedsReLogin = true,
                RequiredFunctionId = functionId,
                ErrorMessage = ServiceOrderConstants.NoPermission
            };
        }
    }
}
