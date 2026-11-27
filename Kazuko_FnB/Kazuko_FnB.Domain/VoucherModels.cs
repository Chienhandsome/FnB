using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>Loại phiếu thu / chi trong bảng TTHUCHI (LOAI = 0 thu, 1 chi; docs/specs/phieu-thu-chi.md).</summary>
    public enum VoucherKind
    {
        Income = 0,
        Expense = 1
    }

    /// <summary>Hằng số nhận diện menu/form, quyền và thông báo chuẩn cho nhóm Phiếu thu/chi &amp; danh mục Quỹ.</summary>
    public static class VoucherConstants
    {
        public const string StableDescId = "97216923-7543-4178-b127-7587640f26ec";
        public const string IncomeListFormId = "b9a3ce4a-a264-4ff0-ae31-4c4e57d1fbde";
        public const string ExpenseListFormId = "5c3da6bd-50a8-4425-88e8-11109c8152c3";

        public const string IncomeFormName = "Phiếu thu";
        public const string ExpenseFormName = "Phiếu chi";
        public const string DefaultIncomeTemplate = "PT(yy)/(*****)";
        public const string DefaultExpenseTemplate = "PC(yy)/(*****)";

        public const string FunctionDateChange = "Thay đổi ngày trong phiếu thu chi";
        public const string FunctionListVoucher = "Danh mục phiếu thu chi";
        public const string FunctionThuChi = "Thu chi";
        public const string FunctionReasonCatalog = "Danh mục lý do thu chi";
        public const string FunctionBankAccountCatalog = "Danh mục tài khoản ngân hàng";

        public const int MaxTextLength = 255;

        public const string BankAccountMissing = "Mời bạn chọn tài khoản ngân hàng";
        public const string AmountMustBePositive = "Số tiền phải lớn hơn 0.";
        public const string NumberRequired = "Mời bạn nhập: Số phiếu";
        public const string StoreRequired = "Mời bạn nhập: Cửa hàng";
        public const string VoucherNotFound = "Phiếu không còn tồn tại.";
        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) dữ liệu đang chọn vĩnh viễn khỏi hệ thống không?";

        public const string ReasonNameEmpty = "Trường \"Tên lý do\" không được trống!";
        public const string ReasonNotFound = "Lý do thu chi không còn tồn tại.";
        public const string BankNameEmpty = "Trường \"Tên tài khoản\" không được trống!";
        public const string BankNotFound = "Tài khoản ngân hàng không còn tồn tại.";

        public static string NumberDuplicate(string number)
        {
            return string.Format("Số phiếu \"{0}\" đã tồn tại. Mời bạn nhập số phiếu khác.", number ?? "");
        }

        public static string LockedLinkedVoucher(string origin)
        {
            return string.Format(
                "Phiếu này gắn với {0}, không thể sửa hoặc xóa tại màn này. Vui lòng thao tác tại màn gốc.",
                string.IsNullOrEmpty(origin) ? "chứng từ liên kết" : origin);
        }

        public static string CashOverdraftBlocked(decimal currentBalance, decimal expenseAmount)
        {
            return string.Format(
                "Không thể chi tiền mặt {0} vì quỹ tiền mặt hiện có {1} (số dư sau chi sẽ âm).",
                FormatMoneyVi(expenseAmount), FormatMoneyVi(currentBalance));
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string ReasonNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên lý do\" đã tồn tại!", name ?? "");
        }

        public static string BankNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên tài khoản\" đã tồn tại!", name ?? "");
        }

        public static string SystemReasonDeleteBlocked(string name)
        {
            return string.Format("Không thể xóa lý do hệ thống '{0}'.", name ?? "");
        }

        public static string SystemReasonTypeEditBlocked(string name)
        {
            return string.Format("Không được thay đổi phân loại KQKD của lý do hệ thống '{0}'.", name ?? "");
        }

        public static string FormatMoneyVi(decimal value)
        {
            CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
            return value == Math.Truncate(value)
                ? value.ToString("#,##0", vi)
                : value.ToString("#,##0.##", vi);
        }

        /// <summary>Kiểm tra dòng DLYDOTHUCHI có phải lý do hệ thống (quy-tien.md bước 4 hoặc lý do nội bộ app gốc).</summary>
        public static bool IsSystemReason(string id, string name, int? reasonType)
        {
            if (string.Equals(id, FundReasons.OpeningBalanceId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, FundReasons.TransferId, StringComparison.OrdinalIgnoreCase))
                return true;
            if (reasonType.HasValue && reasonType.Value == 0)
                return true;
            string folded = CustomerText.FoldNoTrim((name ?? "").Trim());
            return folded == "so du dau ky"
                || folded == "chuyen quy"
                || folded == "thu tam ung"
                || folded == "nap the tra truoc"
                || folded == "chi tra the tra truoc";
        }

        public static string ReasonTypeLabel(int? reasonType)
        {
            if (!reasonType.HasValue)
                return "Không lên báo cáo KQKD";
            switch (reasonType.Value)
            {
                case 1: return "Chi phí cố định (định phí)";
                case 2: return "Chi phí khác";
                case 0: return "Tạm ứng (hệ thống)";
                case 99: return "Không tính KQKD";
                default: return "Không lên báo cáo KQKD";
            }
        }
    }

    /// <summary>Một dòng phiếu thu hoặc phiếu chi (TTHUCHI, LOAI = 0 hoặc 1).</summary>
    public sealed class VoucherRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public VoucherKind Kind { get; set; }
        public int RawLoai { get; set; }
        public int ObjectType { get; set; }
        public string ObjectName { get; set; }
        public string Address { get; set; }
        public string EmployeeId { get; set; }
        public string CustomerId { get; set; }
        public string SupplierId { get; set; }
        public string StoreId { get; set; }
        public string ReasonId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string OriginalDoc { get; set; }
        public string Note { get; set; }
        public bool Transfer { get; set; }
        public string BankAccountId { get; set; }
        public bool NoDebtChange { get; set; }

        // Các trường liên kết (dùng để chặn sửa/xóa ở màn phiếu thu chi, §2 & §5 T2).
        public string BookingId { get; set; }
        public string OrderId { get; set; }
        public string PayrollId { get; set; }
        public string PrepaidCardId { get; set; }
        public bool IsDebtReceipt { get; set; }
        public bool IsAdvance { get; set; }

        // Audit & tên hiển thị.
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public string StoreName { get; set; }
        public string ReasonName { get; set; }
        public string BankAccountName { get; set; }
        public string EmployeeName { get; set; }
        public string CustomerName { get; set; }
        public string SupplierName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public static VoucherRow NewDefault(VoucherKind kind)
        {
            return new VoucherRow
            {
                Name = "",
                Date = DateTime.Today,
                Kind = kind,
                RawLoai = (int)kind,
                ObjectType = 0,
                ObjectName = "",
                Address = "",
                Description = "",
                OriginalDoc = "",
                Note = "",
                Status = 30
            };
        }
    }

    /// <summary>Bộ lọc danh sách phiếu thu / chi.</summary>
    public sealed class VoucherFilter
    {
        public VoucherKind Kind { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        /// <summary>Rỗng = tất cả cửa hàng được phép.</summary>
        public string StoreId { get; set; }
        /// <summary>Ràng buộc cửa hàng của người dùng không phải admin (null = không giới hạn).</summary>
        public List<string> AllowedStoreIds { get; set; }
        public string SearchText { get; set; }
    }

    public sealed class VoucherSaveResult
    {
        public bool Ok { get; set; }
        public string SavedId { get; set; }
        public string SavedNumber { get; set; }
        public string ErrorMessage { get; set; }

        public static VoucherSaveResult Fail(string message)
        {
            return new VoucherSaveResult { Ok = false, ErrorMessage = message };
        }
    }

    public sealed class VoucherDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static VoucherDeleteResult Fail(string message)
        {
            return new VoucherDeleteResult { Ok = false, ErrorMessage = message };
        }
    }

    /// <summary>Một dòng Danh mục lý do thu chi (DLYDOTHUCHI).</summary>
    public sealed class CashReasonRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>LALYDOTHU = 30 là lý do thu, 0 là lý do chi.</summary>
        public bool IsIncome { get; set; }
        /// <summary>LOAILYDO: -1 không lên KQKD, 1 định phí, 2 chi khác, 0 tạm ứng, 99 không tính.</summary>
        public int? ReasonType { get; set; }
        public string Note { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        public bool IsSystem
        {
            get { return VoucherConstants.IsSystemReason(Id, Name, ReasonType); }
        }
    }

    /// <summary>Một dòng Danh mục tài khoản ngân hàng (DTAIKHOANNGANHANG).</summary>
    public sealed class BankAccountRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }
    }

    public sealed class SimpleCatalogSaveResult
    {
        public bool Ok { get; set; }
        public string SavedId { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class SimpleCatalogDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public int ReferenceCount { get; set; }
    }
}
