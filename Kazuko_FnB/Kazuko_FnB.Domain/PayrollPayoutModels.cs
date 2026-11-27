using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Thông tin một phiếu chi lương (TTHUCHI LOAI = 1, TBANGLUONGID = bảng lương, STATUS = 30).</summary>
    public sealed class PayrollPayoutVoucherInfo
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public bool Transfer { get; set; }
        public string BankAccountId { get; set; }
        public string BankAccountName { get; set; }
        public string ReasonId { get; set; }
        public string ReasonName { get; set; }
        public string StoreId { get; set; }
        public string StoreName { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    /// <summary>Một dòng nhân viên trong hộp Chi lương (có dòng tổng hợp TBANGLUONGTONGHOP của bảng lương đang chọn).</summary>
    public sealed class PayrollPayoutRow
    {
        public PayrollPayoutRow()
        {
            Vouchers = new List<PayrollPayoutVoucherInfo>();
        }

        public string SummaryId { get; set; }
        public string EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeName { get; set; }
        public decimal NetSalary { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public List<PayrollPayoutVoucherInfo> Vouchers { get; set; }
        public string VoucherNumbersDisplay { get; set; }

        /// <summary>True khi tổng Đã chi vượt Thực nhận (PaidAmount &gt; 0 và PaidAmount &gt; NetSalary).</summary>
        public bool IsOverpaid
        {
            get { return PaidAmount > 0m && PaidAmount > NetSalary; }
        }
    }

    /// <summary>Toàn bộ dữ liệu hộp Chi lương của một bảng lương đã lưu.</summary>
    public sealed class PayrollPayoutSheet
    {
        public PayrollPayoutSheet()
        {
            Rows = new List<PayrollPayoutRow>();
        }

        public PayrollPeriodRow Period { get; set; }
        public List<PayrollPayoutRow> Rows { get; set; }
        public decimal TotalNetSalary { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalRemainingAmount { get; set; }
        public int TotalVoucherCount { get; set; }
    }

    /// <summary>Tham số mở hộp PhieuThuDialog ở chế độ Chi lương (spec docs/specs/nhan-su-tinh-luong.md §5).</summary>
    public sealed class PayrollPayoutPreset
    {
        public string PayrollId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string PeriodName { get; set; }
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public decimal NetSalary { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        /// <summary>ID phiếu chi lương khi Xem/Sửa phiếu đã có; null/rỗng khi Tạo phiếu mới.</summary>
        public string VoucherId { get; set; }
        public bool ReadOnly { get; set; }
    }

    /// <summary>Hằng số và thông báo chuẩn cho phân hệ Chi lương (patch P3b, spec §5).</summary>
    public static class PayrollPayoutText
    {
        public const string FunctionName = PayrollText.PayrollFunctionName;
        public const string ZeroSalaryWarning = "Nhân viên này không còn tiền lương cần chi";
        public const string NotPayoutVoucher = "Chỉ được sửa hoặc xóa phiếu chi lương của bảng lương này tại màn Chi lương.";
        public const string NotFound = "Phiếu chi lương không còn tồn tại.";
        public const string NoPermission = PayrollText.NoPermission;
        public const string PayrollRequired = PayrollText.PeriodNotFound;
        public const string EmployeeRequired = SalaryAdvanceText.EmployeeRequired;
        public const string EmployeeNotInPayroll = "Nhân viên chưa có dòng tổng hợp trong bảng lương này.";
        public const string IncomeReasonNotAllowed = "Lý do chi lương phải là lý do chi.";
        public const string NoSavedSummary = "Bảng lương này chưa có dữ liệu tổng hợp lương đã lưu.";
        public const decimal MaxAmount = 999999999999.99m;

        public static string DefaultDescription(int month, int year)
        {
            return string.Format("Chi tiền lương tháng {0} năm {1}", month, year);
        }

        public static string MonthLimitBlocked(int month, int year)
        {
            return string.Format("Ngày phải nằm trong tháng {0} năm {1}", month, year);
        }

        public static string AlreadyPaidConfirm(decimal paidAmount)
        {
            return string.Format("Nhân viên đã có phiếu chi lương {0} đ, tạo thêm phiếu?",
                VoucherConstants.FormatMoneyVi(paidAmount));
        }

        public static string DeleteConfirm(string voucherNumber)
        {
            return string.Format("Bạn có muốn xóa phiếu chi lương \"{0}\" vĩnh viễn khỏi hệ thống không?",
                voucherNumber ?? "");
        }
    }
}
