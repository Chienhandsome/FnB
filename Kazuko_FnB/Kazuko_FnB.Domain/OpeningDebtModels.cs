using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Hằng số định danh, quyền và thông báo chuẩn cho chức năng Công nợ ban đầu
    /// của khách hàng (TDONHANG LOAI = 99) và nhà cung cấp (TDONHANG LOAI = 98)
    /// (docs/specs/so-du-dau-ky.md §3, P2).
    /// </summary>
    public static class OpeningDebtConstants
    {
        public const int CustomerOrderType = 99;
        public const int SupplierOrderType = 98;
        public const string OrderName = "CONGNODAU";
        public const int ActiveStatus = DebtConstants.ActiveStatus;
        public const int DeletedStatus = DebtConstants.DeletedStatus;
        public const int PaidFlag = PurchaseConstants.PaidFlag;
        public const int MaxTextLength = PurchaseConstants.MaxTextLength;

        public const string CustomerFunctionName = "Công nợ khách hàng ban đầu";
        /// <summary>Tên viết thiếu dấu "đâu" trong SMENU/SFUNCTION của dữ liệu gốc POS TAP.</summary>
        public const string CustomerFunctionNameAlt = "Công nợ khách hàng ban đâu";
        public const string SupplierFunctionName = "Công nợ nhà cung cấp ban đầu";
        public const string FunctionDateChange = PurchaseConstants.FunctionDateChange;

        public const string CustomerDefaultDescription = "Công nợ khách hàng ban đầu";
        public const string SupplierDefaultDescription = "Công nợ nhà cung cấp ban đầu";

        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string PartnerNotFound = "Đối tác không còn tồn tại.";
        public const string EmptyCustomers = "Không có khách hàng khớp điều kiện lọc.";
        public const string EmptySuppliers = "Không có nhà cung cấp khớp điều kiện lọc.";
        public const string UnsavedChangesConfirm =
            "Dữ liệu công nợ ban đầu có thay đổi chưa lưu. Bạn có muốn bỏ qua các thay đổi này không?";
        public const string SavedSuccess = "Đã lưu công nợ ban đầu thành công.";
        public const string NoChangesToSave = "Không có thay đổi công nợ ban đầu nào để lưu.";

        public static string NegativeDebtBlocked(string partnerName)
        {
            return string.Format("Nợ đầu kỳ của \"{0}\" không được âm.", partnerName ?? "");
        }

        /// <summary>Thông báo chặn giảm/xóa nợ đầu của khách thấp hơn số đã thu theo đơn (spec §3, T4).</summary>
        public static string BelowCollectedBlocked(string partnerName, decimal collectedAmount)
        {
            return string.Format(
                "Nợ đầu của {0} không thể thấp hơn số đã thu ({1})",
                partnerName ?? "",
                DebtConstants.FormatMoneyVi(collectedAmount));
        }

        public static string DuplicateOrdersWarning(int partnerCount)
        {
            return string.Format(
                "Cảnh báo: có {0} đối tác đang có nhiều hơn 1 phiếu công nợ ban đầu trong CSDL, hệ thống đang dùng phiếu đầu tiên theo ngày.",
                partnerCount);
        }
    }

    /// <summary>Đầu phiếu công nợ ban đầu (TDONHANG, LOAI = 99 hoặc 98, NAME = 'CONGNODAU').</summary>
    public sealed class OpeningDebtOrderHeader
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public string PartnerId { get; set; }
        public string StoreId { get; set; }
        public decimal Amount { get; set; }
        /// <summary>Tổng SOTIEN đã phân bổ qua TCHITIETTHANHTOAN (chỉ áp dụng với khách hàng LOAI = 99).</summary>
        public decimal AllocatedPaid { get; set; }
    }

    /// <summary>Một dòng đối tác trên lưới Công nợ ban đầu (khách hàng hoặc nhà cung cấp đang dùng).</summary>
    public sealed class OpeningDebtPartnerRow
    {
        public string PartnerId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }

        /// <summary>ID phiếu TDONHANG (LOAI 99/98) nếu đối tác đã có nợ đầu; rỗng/null nếu chưa có.</summary>
        public string OrderId { get; set; }
        public DateTime? OrderDate { get; set; }

        /// <summary>Nợ đầu kỳ (nhập, &gt;= 0; dương = khách nợ mình hoặc mình nợ NCC).</summary>
        public decimal OpeningDebt { get; set; }
        public decimal OriginalOpeningDebt { get; set; }
        /// <summary>Tổng SOTIEN đã thu theo đơn phân bổ vào phiếu nợ đầu này (khách hàng).</summary>
        public decimal AllocatedPaid { get; set; }

        public bool HasExistingOrder
        {
            get { return !string.IsNullOrEmpty(OrderId); }
        }

        public bool IsDirty
        {
            get { return OpeningDebt != OriginalOpeningDebt; }
        }
    }

    /// <summary>Dữ liệu màn Công nợ ban đầu theo chế độ (Khách hàng hoặc Nhà cung cấp).</summary>
    public sealed class OpeningDebtDocument
    {
        public OpeningDebtDocument()
        {
            Items = new List<OpeningDebtPartnerRow>();
        }

        public DebtPartyKind PartyKind { get; set; }
        public DateTime Date { get; set; }
        public int DuplicatePartnerCount { get; set; }
        public string Warning { get; set; }
        public List<OpeningDebtPartnerRow> Items { get; set; }
    }

    /// <summary>Bộ lọc cục bộ trên lưới Công nợ ban đầu.</summary>
    public sealed class OpeningDebtFilter
    {
        public string SearchText { get; set; }
        public bool OnlyWithDebt { get; set; }
    }

    /// <summary>Tổng cộng lưới Công nợ ban đầu (thuần decimal).</summary>
    public sealed class OpeningDebtTotals
    {
        public int PartnerCount { get; set; }
        public int WithDebtCount { get; set; }
        public decimal TotalOpeningDebt { get; set; }
    }

    /// <summary>Dữ liệu yêu cầu lưu công nợ ban đầu cho nhiều đối tác trong một transaction.</summary>
    public sealed class OpeningDebtSaveRequest
    {
        public OpeningDebtSaveRequest()
        {
            Items = new List<OpeningDebtPartnerRow>();
        }

        public DebtPartyKind PartyKind { get; set; }
        public DateTime Date { get; set; }
        /// <summary>True khi người dùng chủ động đổi ngày trên màn hình và muốn cập nhật ngày của phiếu đã có.</summary>
        public bool DateChanged { get; set; }
        public List<OpeningDebtPartnerRow> Items { get; set; }
    }

    /// <summary>Kết quả lưu công nợ ban đầu.</summary>
    public sealed class OpeningDebtSaveResult
    {
        public OpeningDebtSaveResult()
        {
            SavedOrderIds = new List<string>();
        }

        public bool Ok { get; set; }
        public int ChangedCount { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int DeletedCount { get; set; }
        public decimal TotalSavedDebt { get; set; }
        public List<string> SavedOrderIds { get; set; }
        public string ErrorMessage { get; set; }
        public string Warning { get; set; }

        public static OpeningDebtSaveResult Fail(string message)
        {
            return new OpeningDebtSaveResult { Ok = false, ErrorMessage = message };
        }
    }
}
