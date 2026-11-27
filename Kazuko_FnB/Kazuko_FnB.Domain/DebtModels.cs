using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Phía công nợ: khách hàng (phải thu) hoặc nhà cung cấp (phải trả).</summary>
    public enum DebtPartyKind
    {
        Customer = 0,
        Supplier = 1
    }

    /// <summary>
    /// Hằng số, quyền chức năng và thông báo chuẩn cho Công nợ khách hàng &amp; Công nợ nhà cung cấp
    /// (docs/specs/cong-no.md).
    /// </summary>
    public static class DebtConstants
    {
        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;
        public const int MaxTextLength = 255;

        public const string CustomerDebtFunctionName = "Công nợ khách hàng";
        public const string SupplierDebtFunctionName = "Công nợ nhà cung cấp";
        public const string UnassignedGroupTag = "__UNASSIGNED__";

        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string EmptyCustomerMessage = "Không có khách hàng khớp điều kiện lọc.";
        public const string EmptySupplierMessage = "Không có nhà cung cấp khớp điều kiện lọc.";
        public const string NoOpenBillsMessage = "Khách hàng hiện không có hóa đơn nào còn nợ.";
        public const string CollectTotalMustBePositive = "Tổng số tiền thu phải lớn hơn 0.";
        public const string CustomerNotFound = "Khách hàng không còn tồn tại.";
        public const string BillNotFound = "Hóa đơn không còn tồn tại hoặc không thuộc khách hàng này.";
        public const string AllocationNegative = "Số tiền thu cho từng đơn không được âm.";
        public const string AllocationBillDuplicated = "Danh sách phân bổ có hóa đơn bị trùng.";

        public static string AllocationExceedsDebt(string billNumber, decimal remaining, decimal requested)
        {
            return string.Format(
                "Hóa đơn \"{0}\" chỉ còn nợ {1}, không thể thu {2}.",
                billNumber ?? "",
                FormatMoneyVi(remaining),
                FormatMoneyVi(requested));
        }

        public static string FormatMoneyVi(decimal value)
        {
            return VoucherConstants.FormatMoneyVi(value);
        }

        public static string ClipText(string value, int maxLength = MaxTextLength)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }

    /// <summary>Một dòng đối tượng trên lưới Công nợ khách hàng hoặc Công nợ nhà cung cấp.</summary>
    public sealed class DebtPartnerRow
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        /// <summary>Còn nợ (dương = khách còn nợ quán, hoặc quán còn nợ NCC).</summary>
        public decimal RemainingDebt { get; set; }
    }

    /// <summary>Một nút trên cây nhóm khách hàng hoặc nhóm nhà cung cấp.</summary>
    public sealed class DebtGroupNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public string SortOrder { get; set; }
    }

    /// <summary>Một chứng từ TDONHANG trong phần chi tiết công nợ (hóa đơn bán, nợ đầu, phiếu nhập, phiếu xuất trả).</summary>
    public sealed class DebtDocumentRow
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public int Loai { get; set; }
        public string DocTypeLabel { get; set; }
        /// <summary>Tổng cộng (đã đảo dấu âm cho phiếu xuất trả NCC LOAI = 2).</summary>
        public decimal TotalAmount { get; set; }
        /// <summary>TIENTHANHTOAN ghi trực tiếp trên TDONHANG (giữ nguyên khi thu nợ theo đơn).</summary>
        public decimal PaidOnBill { get; set; }
        /// <summary>Tổng SOTIEN đã phân bổ qua TCHITIETTHANHTOAN (phiếu thu công nợ).</summary>
        public decimal AllocatedPaid { get; set; }
        /// <summary>Tổng đã thanh toán hiển thị (PaidOnBill + AllocatedPaid cho KH; SignedPaid cho NCC).</summary>
        public decimal PaidTotal { get; set; }
        /// <summary>Còn nợ của chứng từ này.</summary>
        public decimal RemainingDebt { get; set; }
        public string Description { get; set; }
        public string Note { get; set; }
        public DateTime? TimeCreated { get; set; }
    }

    /// <summary>Một phiếu thu / chi TTHUCHI trong phần chi tiết công nợ.</summary>
    public sealed class DebtVoucherRow
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public int Loai { get; set; }
        public bool IsDebtReceipt { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public string Description { get; set; }
        public string Note { get; set; }
        public DateTime? TimeCreated { get; set; }
    }

    /// <summary>Chi tiết phát sinh công nợ của một đối tượng đang chọn (chứng từ TDONHANG + phiếu thu/chi TTHUCHI).</summary>
    public sealed class DebtPartnerDetail
    {
        public DebtPartnerDetail()
        {
            Documents = new List<DebtDocumentRow>();
            Vouchers = new List<DebtVoucherRow>();
        }

        public List<DebtDocumentRow> Documents { get; set; }
        public List<DebtVoucherRow> Vouchers { get; set; }
    }

    /// <summary>Bộ lọc màn Công nợ khách hàng / Công nợ nhà cung cấp.</summary>
    public sealed class DebtFilter
    {
        public string GroupId { get; set; }
        public HashSet<string> GroupIds { get; set; }
        public bool UnassignedGroupOnly { get; set; }
        public string SearchText { get; set; }
        /// <summary>Mặc định bật (true): chỉ hiện đối tượng có Còn nợ != 0 (hoặc &gt; 0 — xem DebtRules).</summary>
        public bool OnlyWithDebt { get; set; }
    }

    /// <summary>Tổng hợp lưới danh sách công nợ đang hiển thị.</summary>
    public sealed class DebtTotals
    {
        public int Count { get; set; }
        public decimal TotalDebt { get; set; }
    }

    /// <summary>Một dòng phân bổ thu nợ theo đơn (TCHITIETTHANHTOAN).</summary>
    public sealed class DebtAllocationLine
    {
        public string BillId { get; set; }
        public string BillNumber { get; set; }
        public DateTime BillDate { get; set; }
        public decimal BillTotal { get; set; }
        public decimal BillRemaining { get; set; }
        public decimal PayAmount { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Đầu vào lưu phiếu thu nợ khách hàng theo từng đơn (spec §7 T1).</summary>
    public sealed class DebtCollectRequest
    {
        public DebtCollectRequest()
        {
            Allocations = new List<DebtAllocationLine>();
        }

        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerAddress { get; set; }
        public string VoucherNumber { get; set; }
        public DateTime Date { get; set; }
        public string StoreId { get; set; }
        public string ReasonId { get; set; }
        public bool Transfer { get; set; }
        public string BankAccountId { get; set; }
        public string Description { get; set; }
        public string Note { get; set; }
        public List<DebtAllocationLine> Allocations { get; set; }
    }

    /// <summary>Kết quả lưu phiếu thu nợ theo đơn.</summary>
    public sealed class DebtCollectResult
    {
        public bool Ok { get; set; }
        public string SavedVoucherId { get; set; }
        public string SavedVoucherNumber { get; set; }
        public decimal TotalCollected { get; set; }
        public int AllocationCount { get; set; }
        public string ErrorMessage { get; set; }

        public static DebtCollectResult Fail(string message)
        {
            return new DebtCollectResult { Ok = false, ErrorMessage = message };
        }
    }

    /// <summary>Dữ liệu điền sẵn khi mở PhieuThuDialog (loại chi) để chi trả nợ nhà cung cấp.</summary>
    public sealed class SupplierDebtPayPreset
    {
        public string SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string SupplierAddress { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }
}
