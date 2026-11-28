using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Bộ lọc danh sách Tạm ứng lương (TTHUCHI LOAI = 1, LATAMUNG = 30, TBANGLUONGID IS NULL).</summary>
    public sealed class SalaryAdvanceFilter
    {
        /// <summary>null = không lọc cận dưới ngày.</summary>
        public DateTime? FromDate { get; set; }
        /// <summary>null = không lọc cận trên ngày.</summary>
        public DateTime? ToDate { get; set; }
        /// <summary>Rỗng = tất cả nhân viên.</summary>
        public string EmployeeId { get; set; }
        /// <summary>Giới hạn cửa hàng cho user không phải admin (null = không giới hạn).</summary>
        public List<string> AllowedStoreIds { get; set; }
        public string SearchText { get; set; }
    }

    /// <summary>Hằng số định danh, quyền và thông báo cho màn Tạm ứng lương (docs/specs/nhan-su-thuong-phat-tam-ung.md §3).</summary>
    public static class SalaryAdvanceText
    {
        public const string FunctionName = "Tạm ứng lương";
        public const string FunctionId = "20027954-9b80-411b-a9f5-9e45cff99407";
        public const string DefaultDescription = "Tạm ứng lương";
        public const decimal MaxAmount = 999999999.99m;

        public const string EmployeeRequired = "Mời bạn chọn nhân viên.";
        public const string EmployeeInvalid = "Nhân viên không tồn tại hoặc đã ngừng sử dụng.";
        public const string AmountTooLarge = "Số tiền tạm ứng tối đa 9 chữ số phần nguyên.";
        public const string TransferNotAllowed = "Tạm ứng lương chỉ hỗ trợ chi tiền mặt.";
        public const string IncomeReasonNotAllowed = "Lý do tạm ứng lương phải là lý do chi.";
        public const string NotAdvanceVoucher = "Chỉ được sửa hoặc xóa phiếu tạm ứng lương (không gắn bảng lương) tại màn này.";
        public const string NotFound = "Phiếu tạm ứng lương không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";
    }
}
