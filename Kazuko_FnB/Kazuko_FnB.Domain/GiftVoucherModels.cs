using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng voucher trong Danh mục voucher (DVOUCHER, spec docs/specs/danh-muc-voucher.md).</summary>
    public sealed class GiftVoucherRow
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public decimal Value { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string GroupId { get; set; }
        public string PartnerId { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Thông tin hiển thị / dẫn xuất từ bảng liên kết (không ghi trực tiếp vào DVOUCHER).
        public string GroupName { get; set; }
        public string PartnerName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        /// <summary>Số hóa đơn ĐÃ thanh toán (DATHANHTOAN = 30, STATUS &lt;&gt; -30) đã dùng voucher; rỗng nếu chưa dùng.</summary>
        public string PaidBillName { get; set; }

        /// <summary>Voucher đã được dùng ở một hóa đơn đã thanh toán (chỉ tính cho cột Đã dùng và lọc còn dùng được).</summary>
        public bool IsUsed { get { return !string.IsNullOrEmpty(PaidBillName); } }

        /// <summary>Số hóa đơn BẤT KỲ (mọi STATUS, mọi DATHANHTOAN) tham chiếu DVOUCHERID (dùng để chặn xóa và khóa sửa trường chính).</summary>
        public string AnyRefBillName { get; set; }

        /// <summary>Có bất kỳ hóa đơn nào trong TDONHANG tham chiếu tới voucher này.</summary>
        public bool HasAnyBillRef { get { return !string.IsNullOrEmpty(AnyRefBillName); } }

        public static GiftVoucherRow NewDefault()
        {
            return new GiftVoucherRow
            {
                Code = "",
                Value = 0m,
                IssueDate = null,
                ExpiryDate = null,
                GroupId = null,
                PartnerId = null,
                Note = "",
                Status = GiftVoucherText.ActiveStatus
            };
        }
    }

    /// <summary>Một dòng nhóm voucher (DNHOMVOUCHER — bảng cây chuẩn).</summary>
    public sealed class GiftVoucherGroupRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string ParentId { get; set; }
        public string ParentDir { get; set; }
        public string SortOrder { get; set; }
        public int ItemType { get; set; }
        public int AutoId { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public static GiftVoucherGroupRow NewDefault()
        {
            return new GiftVoucherGroupRow
            {
                Name = "",
                Note = "",
                ParentId = "",
                ParentDir = "",
                SortOrder = "ZZZZ",
                ItemType = 0,
                Status = GiftVoucherText.ActiveStatus
            };
        }
    }

    /// <summary>Kết quả lưu voucher (DVOUCHER).</summary>
    public sealed class GiftVoucherSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasCodeError { get; set; }
        public bool HasValueError { get; set; }
        public bool HasDateError { get; set; }
        public bool HasLockedFieldError { get; set; }

        public static GiftVoucherSaveResult Fail(
            string message,
            bool hasCodeError = false,
            bool hasValueError = false,
            bool hasDateError = false,
            bool hasLockedFieldError = false)
        {
            return new GiftVoucherSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasCodeError = hasCodeError,
                HasValueError = hasValueError,
                HasDateError = hasDateError,
                HasLockedFieldError = hasLockedFieldError
            };
        }
    }

    /// <summary>Kết quả xóa cứng voucher (DVOUCHER).</summary>
    public sealed class GiftVoucherDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string ReferenceBillName { get; set; }

        public static GiftVoucherDeleteResult Fail(string message, string referenceBillName = null)
        {
            return new GiftVoucherDeleteResult
            {
                Ok = false,
                ErrorMessage = message,
                ReferenceBillName = referenceBillName
            };
        }
    }

    /// <summary>Kết quả lưu nhóm voucher (DNHOMVOUCHER).</summary>
    public sealed class GiftVoucherGroupSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }

        public static GiftVoucherGroupSaveResult Fail(string message)
        {
            return new GiftVoucherGroupSaveResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Kết quả xóa cứng nhóm voucher (DNHOMVOUCHER).</summary>
    public sealed class GiftVoucherGroupDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int VoucherCount { get; set; }
        public int ChildGroupCount { get; set; }

        public static GiftVoucherGroupDeleteResult Fail(string message)
        {
            return new GiftVoucherGroupDeleteResult
            {
                Ok = false,
                ErrorMessage = message
            };
        }
    }

    /// <summary>Hằng số, thông báo và chuẩn hóa so trùng cho Danh mục voucher (DVOUCHER / DNHOMVOUCHER).</summary>
    public static class GiftVoucherText
    {
        public const string FunctionName = "Danh mục voucher";
        public const string TableName = "DVOUCHER";
        public const string GroupTableName = "DNHOMVOUCHER";
        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;
        public const int MaxTextLength = 255;

        public const string CodeEmpty = "Trường \"Mã voucher\" không được trống!";
        public const string ValueInvalid = "Giá trị voucher phải lớn hơn 0.";
        public const string ExpiryBeforeIssue = "Hạn sử dụng không được trước ngày phát hành.";
        public const string GroupNameEmpty = "Trường \"Tên nhóm voucher\" không được trống!";
        public const string NotFound = "Voucher không còn tồn tại.";
        public const string GroupNotFound = "Nhóm voucher không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string CodeDuplicate(string code)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Mã voucher\" đã tồn tại!", code ?? "");
        }

        public static string GroupNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên nhóm voucher\" đã tồn tại trong cùng nhóm cha!", name ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string DeleteBlockedByBill(string billName)
        {
            return string.Format("Voucher đã được dùng ở hóa đơn {0}, không xóa được", billName ?? "");
        }

        public static string UsedVoucherLocked(string billName)
        {
            return string.Format("Voucher đã được dùng ở hóa đơn {0}, chỉ cho phép sửa ghi chú.", billName ?? "");
        }

        public static string GroupHasVouchers(string groupName, int count)
        {
            return string.Format("Không thể xóa nhóm voucher '{0}' vì nhóm còn {1} voucher.", groupName ?? "", count);
        }

        public static string GroupHasChildren(string groupName, int count)
        {
            return string.Format("Không thể xóa nhóm voucher '{0}' vì nhóm còn {1} nhóm con.", groupName ?? "", count);
        }

        public static string DeleteGroupConfirm(string groupName)
        {
            return string.Format("Bạn có chắc chắn xóa nhóm voucher '{0}' không?", groupName ?? "");
        }

        public static string FormatUsedStatus(string paidBillName)
        {
            return string.IsNullOrEmpty(paidBillName) ? "Không" : "Có (" + paidBillName + ")";
        }

        /// <summary>So trùng theo collation UNICODE_CI_AI (không phân biệt hoa/thường và dấu, kể cả đ/Đ; không cắt khoảng trắng).</summary>
        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }
    }
}
