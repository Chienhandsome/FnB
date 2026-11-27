using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Hằng số và thông báo nguyên văn cho luồng Thanh toán (§3.7, §6.5, §6.6).</summary>
    public static class ServicePaymentConstants
    {
        public const string EmptyBillError = "Mời bạn thêm mặt hàng vào phiếu";
        public const string EmployeeRequired = "Mời bạn chọn nhân viên trước";
        public const string OrderNumberRequired = "Mời bạn nhập số order";
        public const string OrderNumberInvalid = "Mời bạn nhập đúng định dạng số order, ví dụ: 12, 16, 17,19";
        public const string DebtDisabled = "Chức năng này hiện tại chưa được kích hoạt. Bạn có thể kích hoạt trong menu 'Quản trị'|'Cấu hình toàn hệ thống'";
        public const string CustomerRequiredForDebt = "Bạn chưa chọn khách hàng, mời bạn chọn khách hàng trước!";
        public const string CustomerNotFound = "Khách hàng đã chọn không có hồ sơ trong hệ thống, không thể ghi nợ.";
        public const string VoucherCodeEmpty = "Mời bạn nhập mã voucher trước";
        public const string VoucherExpired = "Mã voucher đã quá hạn sử dụng";
        public const string VoucherAlreadyUsed = "Mã voucher đã được sử dụng";
        public const string VoucherRequiredFromList = "Mời bạn chọn voucher từ danh sách.";
        public const string PrepaidCodeEmpty = "Mời bạn nhập mã thẻ trả trước";
        public const string PrepaidExpired = "Mã thẻ này đã quá hạn sử dụng";
        public const string PrepaidLocked = "Thẻ này đã bị khóa, không thể sử dụng";
        public const string PrepaidEmptyBalance = "Thẻ này không còn tiền để sử dụng";
        public const string PrepaidOverBalance = "Số tiền thanh toán bằng thẻ trả trước vượt quá số dư còn lại của thẻ.";
        public const string PointRateNotConfigured = "VUI LÒNG THIẾT LẬP QUY ĐỔI ĐIỂM SANG TIỀN TRƯỚC";
        public const string PointsOverAvailable = "Số tiền trừ tích lũy vượt quá điểm hiện có của khách hàng.";
        public const string PointsOverDue = "Số tiền trừ tích lũy không được vượt quá số tiền phải thanh toán.";
        public const string PointsRequireCustomer = "Mời bạn chọn khách hàng trước khi trừ tích lũy.";
        public const string GroupMinTables = "Mời bạn chọn ít nhất 2 bàn để thanh toán chung.";
        public const string RefundDismissWarning =
            "Phiếu chi trả lại tiền đặt trước chưa được lưu. Nếu bỏ qua, quỹ tiền sẽ chưa trừ khoản trả lại khách này. " +
            "Bạn có chắc chắn muốn đóng mà không lưu không?";

        public static string DebtConfirmPrompt(string customerName, string amountText)
        {
            return string.Format("Bạn có muốn cho khách hàng '{0}' nợ số tiền '{1}' không?",
                customerName ?? "", amountText ?? "0");
        }

        public static string DateTransferWarning(DateTime paymentDate)
        {
            return string.Format(
                "Hóa đơn được mở từ ngày cũ. Hệ thống sẽ chuyển hóa đơn này sang ngày hiện tại ({0:dd/MM/yyyy}). Bạn có muốn tiếp tục thanh toán không?",
                paymentDate);
        }

        public static string VoucherNotFound(string code)
        {
            return string.Format("Mã voucher '{0}' không tồn tại trong hệ thống", code ?? "");
        }

        public static string VoucherNotYetIssued(string code, DateTime issueDate)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "Voucher '{0}' chưa đến ngày phát hành ({1:dd/MM/yyyy})",
                code ?? "", issueDate.Date);
        }

        public static string PrepaidNotFound(string code)
        {
            return string.Format("Mã thẻ trả trước '{0}' không tồn tại trong hệ thống", code ?? "");
        }

        public static string RefundDepositNote(string billNumbers)
        {
            return "Trả lại tiền đặt trước hóa đơn số " + (billNumbers ?? "");
        }

        public static string PointsRangeWarning(decimal maxPoints)
        {
            string s = maxPoints == Math.Truncate(maxPoints)
                ? maxPoints.ToString("0", CultureInfo.InvariantCulture)
                : maxPoints.ToString("0.##", CultureInfo.InvariantCulture);
            return "VUI LÒNG NHẬP ĐIỂM SỬ DỤNG TRONG KHOẢNG TỪ 0 TỚI " + s;
        }
    }

    /// <summary>Một hóa đơn đầu vào cho bộ phân bổ thanh toán thuần (bill đơn = 1 phần tử, nhóm = >= 2 phần tử).</summary>
    public sealed class PaymentBillSlice
    {
        public string BillId { get; set; }
        public string BillName { get; set; }
        public string TableId { get; set; }
        public string TableName { get; set; }
        public DateTime BatDau { get; set; }
        public DateTime OpenDate { get; set; }
        public string OriginalCustomerId { get; set; }
        public decimal TongCong { get; set; }
        /// <summary>Tiền đặt trước / tạm ứng riêng gắn với bill này (từ TTHUCHI, không gồm STATUS = -30).</summary>
        public decimal Deposit { get; set; }
    }

    /// <summary>Đầu vào thuần cho PaymentAllocationRules.Allocate (§3.7.1–§3.7.3, quy-tien.md §4.3 &amp; §4.8).</summary>
    public sealed class PaymentAllocationInput
    {
        public List<PaymentBillSlice> Bills { get; set; }
        /// <summary>Khách hàng được chọn trên hộp thanh toán (bắt buộc khi trả thiếu; dùng cho nợ cũ Q17).</summary>
        public string CustomerId { get; set; }
        /// <summary>Nợ cũ của khách chọn trên hộp thanh toán (GetNoCu; âm khi khách đang dư tiền).</summary>
        public decimal OldDebt { get; set; }
        public decimal CashTendered { get; set; }
        public decimal Transfer { get; set; }
        public decimal Card { get; set; }
        public string AccountId { get; set; }
        public decimal Voucher { get; set; }
        public string VoucherId { get; set; }
        public decimal PrepaidCard { get; set; }
        public string PrepaidCardId { get; set; }
        public decimal PointsMoney { get; set; }
        /// <summary>QuyDoi1DiemSangTien (đồng/điểm) để tính DIEMGIAM.</summary>
        public decimal PointRate { get; set; }
        /// <summary>DoanhSoTuongUngVoi1Diem để tính DIEM tích lũy trên mỗi bill.</summary>
        public int RevenuePerPoint { get; set; }
        public bool AllowCustomerDebt { get; set; }

        public PaymentAllocationInput()
        {
            Bills = new List<PaymentBillSlice>();
        }
    }

    /// <summary>Kết quả phân bổ chi tiết cho từng bill để ghi vào TDONHANG (§3.7.2, §3.7.3, quy-tien.md §4.8).</summary>
    public sealed class PaymentBillAllocation
    {
        public string BillId { get; set; }
        public string BillName { get; set; }
        public string TableId { get; set; }
        public string TableName { get; set; }
        public decimal TongCong { get; set; }
        /// <summary>Phần tiền đặt trước thực dùng cho bill này (tính chung cả nhóm, cọc dư bill trước bù bill sau — Q15).</summary>
        public decimal DepositUsed { get; set; }
        /// <summary>Giá trị ghi cột TDONHANG.DATTRUOC.</summary>
        public decimal DatTruoc { get; set; }
        /// <summary>Giá trị ghi cột TDONHANG.NOCU (chỉ bill cuối mang nợ cũ khi thu kèm).</summary>
        public decimal NoCu { get; set; }
        public decimal Voucher { get; set; }
        public string VoucherId { get; set; }
        public decimal PrepaidCard { get; set; }
        public string PrepaidCardId { get; set; }
        public decimal PointsMoney { get; set; }
        public decimal PointsUsed { get; set; }
        public decimal Card { get; set; }
        public decimal Transfer { get; set; }
        /// <summary>Giá trị ghi cột TDONHANG.TIENMAT (có thể âm ở bill cuối khi chuyển khoản dư trả lại tiền mặt).</summary>
        public decimal Cash { get; set; }
        public string AccountId { get; set; }
        public decimal KhachDua { get; set; }
        /// <summary>Giá trị ghi cột TDONHANG.TRALAI (chỉ bill cuối mang tiền trả lại, bill khác = 0).</summary>
        public decimal Change { get; set; }
        /// <summary>Giá trị ghi cột TDONHANG.TIENTHANHTOAN = DepositUsed + phần phân bổ vào bill.</summary>
        public decimal TienThanhToan { get; set; }
        /// <summary>Giá trị ghi cột TDONHANG.CONLAI = max(0, TONGCONG - TIENTHANHTOAN).</summary>
        public decimal ConLai { get; set; }
        /// <summary>Giá trị ghi cột TDONHANG.CONGNO = TONGCONG - TIENTHANHTOAN (âm khi thu kèm nợ cũ).</summary>
        public decimal CongNo { get; set; }
        /// <summary>Cờ TDONHANG.CONNO = 30 khi bill mang nợ (CongNo > 0), ngược lại 0.</summary>
        public int ConNoFlag { get; set; }
        /// <summary>Khách hàng cuối cùng gán vào TDONHANG.DKHACHHANGID (bill mang nợ hoặc bill cuối có nợ cũ dùng khách chọn).</summary>
        public string FinalCustomerId { get; set; }
        /// <summary>Điểm tích lũy cộng cho bill này (TDONHANG.DIEM).</summary>
        public int EarnedPoints { get; set; }
    }

    /// <summary>Kết quả phân bổ thanh toán toàn bộ bill đơn hoặc nhóm bill.</summary>
    public sealed class PaymentAllocationResult
    {
        public string Error { get; set; }
        public bool IsValid { get { return string.IsNullOrEmpty(Error); } }
        public decimal TotalBills { get; set; }
        public decimal TotalDeposit { get; set; }
        public decimal TotalDepositUsed { get; set; }
        /// <summary>TONGTIEN = TotalBills + OldDebt - TotalDeposit (số phải trả truyền vào FundRules.SettlePayment).</summary>
        public decimal NetDue { get; set; }
        public decimal TotalPaidInput { get; set; }
        public decimal Change { get; set; }
        public decimal CashKept { get; set; }
        /// <summary>Số tiền thiếu (ghi nợ) = max(0, NetDue - TotalPaidInput).</summary>
        public decimal Shortfall { get; set; }
        /// <summary>Số tiền đặt trước dư cần lập phiếu chi trả lại khách = min(TotalDeposit, max(0, -NetDue)).</summary>
        public decimal ExcessDeposit { get; set; }
        public List<PaymentBillAllocation> Bills { get; set; }

        public PaymentAllocationResult()
        {
            Bills = new List<PaymentBillAllocation>();
        }
    }

    /// <summary>Thông tin thẻ trả trước (DTHETRATRUOC) kèm số dư tính theo công thức desktop chuẩn.</summary>
    public sealed class PrepaidCardInfo
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool Locked { get; set; }
        public decimal Balance { get; set; }
        public string Error { get; set; }
        public bool IsValid { get { return string.IsNullOrEmpty(Error); } }
    }

    /// <summary>Thông tin voucher / coupon (DVOUCHER) kèm kết quả kiểm tra hợp lệ.</summary>
    public sealed class VoucherInfo
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public decimal Value { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Error { get; set; }
        public bool IsValid { get { return string.IsNullOrEmpty(Error); } }
    }

    /// <summary>Trạng thái công nợ, điểm tích lũy và thẻ trả trước gắn theo một khách hàng.</summary>
    public sealed class CustomerPaymentState
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal OldDebt { get; set; }
        public decimal AvailablePoints { get; set; }
        public decimal PointRate { get; set; }
        public PrepaidCardInfo LinkedPrepaidCard { get; set; }
    }

    /// <summary>Dữ liệu chuẩn bị trước khi mở hộp Xác nhận thanh toán.</summary>
    public sealed class PaymentDialogContext
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string PrimaryBillId { get; set; }
        public string PrimaryTableId { get; set; }
        public string GroupGuid { get; set; }
        public List<PaymentBillSlice> Bills { get; set; }
        public decimal TotalBills { get; set; }
        public decimal TotalDeposit { get; set; }
        public string InitialCustomerId { get; set; }
        public CustomerPaymentState InitialCustomerState { get; set; }
        public List<CustomerLookupItem> Customers { get; set; }
        public List<LookupItem> BankAccounts { get; set; }
        public List<LookupItem> Employees { get; set; }
        public bool IsEditPaidBill { get; set; }
        public ExistingPaymentSnapshot ExistingPayment { get; set; }
        public bool IsGroupPayment { get { return Bills != null && Bills.Count > 1; } }

        public PaymentDialogContext()
        {
            Bills = new List<PaymentBillSlice>();
            Customers = new List<CustomerLookupItem>();
            BankAccounts = new List<LookupItem>();
            Employees = new List<LookupItem>();
        }
    }

    /// <summary>Yêu cầu thanh toán từ hộp Xác nhận thanh toán gửi xuống TableService.ExecutePayment.</summary>
    public sealed class PaymentRequest
    {
        public string PrimaryBillId { get; set; }
        public string PrimaryTableId { get; set; }
        public string GroupGuid { get; set; }
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
        public bool ConfirmDateTransfer { get; set; }
        public bool ConfirmDebt { get; set; }
        /// <summary>Chỉ dùng cho selftest mô phỏng lỗi giữa chừng để kiểm tra rollback toàn bộ transaction.</summary>
        public Action<string> TestHookAfterBillUpdate { get; set; }
    }

    /// <summary>Kết quả thực thi thanh toán một bill hoặc một nhóm bill.</summary>
    public sealed class PaymentExecutionResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public bool NeedsReLogin { get; set; }
        public string RequiredFunctionId { get; set; }
        public bool NeedsDateTransferConfirmation { get; set; }
        public bool NeedsDebtConfirmation { get; set; }
        public decimal DebtAmount { get; set; }
        public string DebtCustomerName { get; set; }

        public string PrimaryBillId { get; set; }
        public List<string> PaidBillIds { get; set; }
        public string PaidBillNames { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal ExcessDeposit { get; set; }
        public decimal AlreadyRefundedDeposit { get; set; }
        public string RefundDepositWarning { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerAddress { get; set; }
        public string StoreId { get; set; }
        public string UpgradedGroupName { get; set; }
        public bool PrintAfterClose { get; set; }
        public PaymentAllocationResult Allocation { get; set; }

        public PaymentExecutionResult()
        {
            PaidBillIds = new List<string>();
        }

        public static PaymentExecutionResult Fail(string message)
        {
            return new PaymentExecutionResult { Ok = false, ErrorMessage = message };
        }

        public static PaymentExecutionResult ReLogin(string functionId)
        {
            return new PaymentExecutionResult
            {
                Ok = false,
                NeedsReLogin = true,
                RequiredFunctionId = functionId,
                ErrorMessage = ServiceOrderConstants.NoPermission
            };
        }
    }

    /// <summary>Đầu vào lưu phiếu chi trả lại tiền đặt trước (TTHUCHI LOAI = 1, KHONGTHAYDOICONGNO = 30).</summary>
    public sealed class RefundVoucherInput
    {
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public string PrimaryBillId { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Address { get; set; }
        public string StoreId { get; set; }
        public string ReasonId { get; set; }
        public string Note { get; set; }
        public decimal Amount { get; set; }
        public bool Transfer { get; set; }
        public string BankAccountId { get; set; }
    }

    /// <summary>Kết quả lưu phiếu chi trả lại tiền đặt trước.</summary>
    public sealed class RefundVoucherResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public string SavedNumber { get; set; }
    }

    /// <summary>Một dòng vật tư cần xuất (TDONHANGCHITIET XUATVATTU = 30) khi thanh toán (§3.7, nha-hang.md §8).</summary>
    public sealed class MaterialExportItem
    {
        public string MaterialId { get; set; }
        public string MaterialName { get; set; }
        public string UnitId { get; set; }
        public string WarehouseId { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public bool HasStock { get; set; }
    }

    /// <summary>Một bàn đang có khách hiển thị trong hộp chọn bàn cho "Thanh toán chung" (§3.7.3 Q14).</summary>
    public sealed class BusyTableForGroupItem
    {
        public string TableId { get; set; }
        public string TableName { get; set; }
        public string AreaName { get; set; }
        public string BillId { get; set; }
        public string BillName { get; set; }
        public DateTime BatDau { get; set; }
        public decimal TongCong { get; set; }
        public string CustomerName { get; set; }
        public string ExistingGroupGuid { get; set; }
    }

    /// <summary>Kết quả gán nhóm NHOMGUID tạm thời cho "Thanh toán chung" (lưu map cũ để gỡ khi hủy).</summary>
    public sealed class GroupAssignResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string GroupGuid { get; set; }
        public Dictionary<string, string> PreviousGroupGuids { get; set; }

        public GroupAssignResult()
        {
            PreviousGroupGuids = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
