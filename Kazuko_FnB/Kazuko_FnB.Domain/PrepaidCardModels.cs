using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Tổng hợp số dư thẻ trả trước tính từ một công thức duy nhất (ServicePaymentRepository).</summary>
    public sealed class PrepaidCardBalanceSummary
    {
        public decimal Loaded { get; set; }
        public decimal Used { get; set; }
        public decimal Remaining { get { return Loaded - Used; } }
    }

    /// <summary>Khách hàng đang giữ thẻ trả trước (DKHACHHANG.DTHETRATRUOCID).</summary>
    public sealed class PrepaidCardHolderInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
    }

    /// <summary>Một dòng thẻ trả trước (DTHETRATRUOC, spec docs/specs/danh-muc-the-tra-truoc.md §2).</summary>
    public sealed class PrepaidCardRow
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string GroupId { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool Locked { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Dẫn xuất từ bảng liên kết (không ghi trực tiếp vào DTHETRATRUOC).
        public string GroupName { get; set; }
        public string HolderNames { get; set; }
        public List<PrepaidCardHolderInfo> Holders { get; set; }
        public decimal TotalLoaded { get; set; }
        public decimal TotalUsed { get; set; }
        public decimal Remaining { get { return TotalLoaded - TotalUsed; } }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public PrepaidCardRow()
        {
            Holders = new List<PrepaidCardHolderInfo>();
        }

        public static PrepaidCardRow NewDefault()
        {
            return new PrepaidCardRow
            {
                Code = "",
                GroupId = null,
                ExpiryDate = null,
                Locked = false,
                Note = "",
                Status = PrepaidCardText.ActiveStatus
            };
        }
    }

    /// <summary>Một dòng nhóm thẻ trả trước (DNHOMTHETRATRUOC — bảng cây chuẩn).</summary>
    public sealed class PrepaidCardGroupRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string ParentId { get; set; }
        public string ParentDir { get; set; }
        public string SortOrder { get; set; }
        public int ItemType { get; set; }
        public int AutoId { get; set; }
        public string ImageId { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public static PrepaidCardGroupRow NewDefault()
        {
            return new PrepaidCardGroupRow
            {
                Name = "",
                Note = "",
                ParentId = "",
                ParentDir = "",
                SortOrder = "ZZZZ",
                ItemType = 0,
                ImageId = null,
                Status = PrepaidCardText.ActiveStatus
            };
        }
    }

    /// <summary>Một dòng trong tab Lịch sử nạp tiền của hộp thẻ (TTHUCHI gắn DTHETRATRUOCID).</summary>
    public sealed class PrepaidCardVoucherItem
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public VoucherKind Kind { get; set; }
        public decimal IncomeAmount { get; set; }
        public decimal ExpenseAmount { get; set; }
        public bool Transfer { get; set; }
        public string BankAccountName { get; set; }
        public string Description { get; set; }
    }

    /// <summary>Một dòng trong tab Lịch sử sử dụng của hộp thẻ (TDONHANG gắn DTHETRATRUOCID).</summary>
    public sealed class PrepaidCardUsageItem
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public decimal UsedAmount { get; set; }
    }

    /// <summary>Tham số mở hộp PhieuThuDialog ở chế độ Nạp tiền / Chi trả tiền thẻ trả trước (spec §3).</summary>
    public sealed class PrepaidCardVoucherPreset
    {
        public string CardId { get; set; }
        public string CardCode { get; set; }
        public VoucherKind Kind { get; set; }
        public string VoucherId { get; set; }
        public bool ReadOnly { get; set; }
        public PrepaidCardHolderInfo SingleHolder { get; set; }
        public decimal CurrentRemaining { get; set; }
    }

    /// <summary>Số lượng bản ghi tham chiếu đến một thẻ trả trước (để chặn xóa theo spec §2).</summary>
    public sealed class PrepaidCardReferenceCounts
    {
        public int VoucherCount { get; set; }
        public int OrderCount { get; set; }
        public int CustomerCount { get; set; }

        public bool HasAny
        {
            get { return VoucherCount > 0 || OrderCount > 0 || CustomerCount > 0; }
        }
    }

    public sealed class PrepaidCardSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
        public bool HasCodeError { get; set; }

        public static PrepaidCardSaveResult Fail(string message, bool hasCodeError = false)
        {
            return new PrepaidCardSaveResult
            {
                Ok = false,
                ErrorMessage = message,
                HasCodeError = hasCodeError
            };
        }
    }

    public sealed class PrepaidCardDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int VoucherCount { get; set; }
        public int OrderCount { get; set; }
        public int CustomerCount { get; set; }

        public static PrepaidCardDeleteResult Fail(string message)
        {
            return new PrepaidCardDeleteResult { Ok = false, ErrorMessage = message };
        }
    }

    public sealed class PrepaidCardGroupSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }

        public static PrepaidCardGroupSaveResult Fail(string message)
        {
            return new PrepaidCardGroupSaveResult { Ok = false, ErrorMessage = message };
        }
    }

    public sealed class PrepaidCardGroupDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int CardCount { get; set; }
        public int ChildGroupCount { get; set; }

        public static PrepaidCardGroupDeleteResult Fail(string message)
        {
            return new PrepaidCardGroupDeleteResult { Ok = false, ErrorMessage = message };
        }
    }

    /// <summary>Hằng số, thông báo và chuẩn hóa cho Danh mục thẻ trả trước (DTHETRATRUOC / DNHOMTHETRATRUOC).</summary>
    public static class PrepaidCardText
    {
        public const string FunctionId = "09f35712-d2af-489c-ba39-e21f3bb3bf20";
        public const string FunctionName = "Danh mục thẻ trả trước";
        public const string TableName = "DTHETRATRUOC";
        public const string GroupTableName = "DNHOMTHETRATRUOC";
        public const string ConfigKey = "SuDungTheTraTruoc";
        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;
        public const int MaxTextLength = 255;
        public const decimal MaxAmount = 999999999.99m;

        public const string CodeEmpty = "Trường \"Mã thẻ\" không được trống!";
        public const string GroupNameEmpty = "Trường \"Tên nhóm thẻ\" không được trống!";
        public const string NotFound = "Thẻ trả trước không còn tồn tại.";
        public const string GroupNotFound = "Nhóm thẻ trả trước không còn tồn tại.";
        public const string VoucherNotFound = "Phiếu thu/chi thẻ trả trước không còn tồn tại.";
        public const string NotCardVoucher = "Phiếu này không thuộc thẻ trả trước đang chọn.";
        public const string NoPermission = "Bạn không có quyền thực hiện thao tác này.";
        public const string SaveCardBeforeTransaction = "Vui lòng lưu thẻ trả trước trước khi nạp hoặc chi trả tiền.";
        public const string AmountTooLarge = "Số tiền vượt quá giới hạn tối đa (9 chữ số nguyên).";
        public const string ReasonKindMismatch = "Lý do thu/chi không khớp với loại phiếu.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public static string CodeDuplicate(string code)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Mã thẻ\" đã tồn tại!", code ?? "");
        }

        public static string GroupNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên nhóm thẻ\" đã tồn tại trong cùng nhóm cha!", name ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string DefaultTopUpDescription(string cardCode)
        {
            return "Nạp tiền thẻ trả trước " + (cardCode ?? "");
        }

        public static string DefaultWithdrawDescription(string cardCode)
        {
            return "Chi trả tiền thẻ trả trước " + (cardCode ?? "");
        }

        public static string GroupHasCards(string groupName, int count)
        {
            return string.Format("Không thể xóa nhóm thẻ '{0}' vì nhóm còn {1} thẻ trả trước.", groupName ?? "", count);
        }

        public static string GroupHasChildren(string groupName, int count)
        {
            return string.Format("Không thể xóa nhóm thẻ '{0}' vì nhóm còn {1} nhóm con.", groupName ?? "", count);
        }

        public static string DeleteGroupConfirm(string groupName)
        {
            return string.Format("Bạn có chắc chắn xóa nhóm thẻ '{0}' không?", groupName ?? "");
        }

        public static string DeleteVoucherConfirm(string voucherNumber)
        {
            return string.Format("Bạn có muốn xóa phiếu \"{0}\" vĩnh viễn khỏi hệ thống không?", voucherNumber ?? "");
        }

        public static string FormatLocked(bool locked)
        {
            return locked ? "Có" : "Không";
        }

        public static string FoldNoTrim(string value)
        {
            return CustomerText.FoldNoTrim(value);
        }

        public static string FormatMoneyInvariantVi(decimal value)
        {
            var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberDecimalSeparator = ",";
            nfi.NumberGroupSeparator = ".";
            nfi.NumberGroupSizes = new[] { 3 };
            nfi.NegativeSign = "-";
            decimal rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            if (rounded == 0m)
                return "0";
            return rounded == Math.Truncate(rounded)
                ? rounded.ToString("#,##0", nfi)
                : rounded.ToString("#,##0.##", nfi);
        }
    }
}
