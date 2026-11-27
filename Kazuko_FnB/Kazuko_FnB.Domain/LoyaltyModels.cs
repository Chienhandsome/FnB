using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Hằng số, quyền chức năng và thông báo chuẩn cho màn Khách hàng thân thiết &amp; phiếu tặng/giảm điểm
    /// (docs/specs/khach-hang-than-thiet.md).
    /// </summary>
    public static class LoyaltyConstants
    {
        public const int ActiveStatus = 30;
        public const int DeletedStatus = -30;
        public const int PaidFlag = 30;
        public const int SaleOrderType = 0;

        public const int MaxTextLength = 255;
        public const decimal MaxPointValue = 999999999m;

        public const string FunctionName = "Khách hàng thân thiết";
        public const string FunctionDateChange = "Thay đổi ngày";
        public const string DefaultNumberTemplate = "TD(yy)/(*****)";
        public const string UnassignedGroupTag = "__UNASSIGNED__";

        public const string NoPermission = "Bạn không có quyền thực hiện chức năng này.";
        public const string EmptyCustomerMessage = "Không có khách hàng khớp điều kiện lọc.";
        public const string VoucherNotFound = "Phiếu tặng/giảm điểm không còn tồn tại.";
        public const string CustomerNotFound = "Khách hàng không còn tồn tại.";
        public const string NumberRequired = "Mời bạn nhập: Số phiếu";
        public const string CustomerRequired = "Mời bạn chọn khách hàng.";
        public const string ReasonRequired = "Mời bạn nhập: Lý do";
        public const string PointsNegative = "Điểm tặng và điểm giảm không được âm.";
        public const string PointsBothZero = "Điểm tặng và điểm giảm không được cùng bằng 0.";
        public const string PointsTooLarge = "Điểm tặng và điểm giảm tối đa 9 chữ số.";
        public const string DeleteConfirm = "Bạn có muốn xóa (1) phiếu tặng/giảm điểm đang chọn vĩnh viễn khỏi hệ thống không?";
        public const string DeleteWouldMakeNegative = "Xóa phiếu này làm điểm của khách âm";

        public static string NumberDuplicate(string number)
        {
            return string.Format("Số phiếu \"{0}\" đã tồn tại. Mời bạn nhập số phiếu khác.", number ?? "");
        }

        public static string FieldTooLong(string label, int maxLength)
        {
            return string.Format("Trường \"{0}\" không được vượt quá {1} ký tự!", label, maxLength);
        }

        public static string ReduceExceedsPoints(decimal availablePoints)
        {
            return string.Format("Điểm giảm vượt điểm hiện có (còn {0} điểm)", FormatPointsVi(availablePoints));
        }

        public static string UpgradedGroupMessage(string customerName, string groupName)
        {
            return string.Format("Khách {0} đã được nâng lên nhóm {1}", customerName ?? "", groupName ?? "");
        }

        public static string FormatMoneyVi(decimal value)
        {
            return VoucherConstants.FormatMoneyVi(value);
        }

        public static string FormatPointsVi(decimal value)
        {
            CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
            return value == Math.Truncate(value)
                ? value.ToString("#,##0", vi)
                : value.ToString("#,##0.##", vi);
        }

        public static string ClipText(string value, int maxLength = MaxTextLength)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }

    /// <summary>Một nút nhóm khách hàng trên cây lọc của màn Khách hàng thân thiết.</summary>
    public sealed class LoyaltyGroupNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public string SortOrder { get; set; }
        public decimal ThresholdPoints { get; set; }
    }

    /// <summary>Một dòng khách hàng trên lưới Khách hàng thân thiết kèm Doanh số, Số hóa đơn, Điểm tích lũy.</summary>
    public sealed class LoyaltyCustomerRow
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public decimal InitialPoints { get; set; }
        public decimal Revenue { get; set; }
        public int BillCount { get; set; }
        public decimal Points { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Một hóa đơn bán đã thanh toán (LOAI = 0, DATHANHTOAN = 30) ở tab "Hóa đơn" của khách.</summary>
    public sealed class LoyaltyBillRow
    {
        public string Id { get; set; }
        public decimal Points { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PointsUsed { get; set; }
    }

    /// <summary>Một phiếu tặng/giảm điểm (TTANGGIAMDIEM) ở tab "Tặng điểm - tặng quà" và hộp nhập liệu.</summary>
    public sealed class LoyaltyPointVoucherRow
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal GiftPoints { get; set; }
        public decimal ReducePoints { get; set; }
        public string Reason { get; set; }
        public string Note { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        public static LoyaltyPointVoucherRow NewDefault(DateTime date, string customerId)
        {
            return new LoyaltyPointVoucherRow
            {
                Number = "",
                Date = date.Date,
                CustomerId = customerId,
                GiftPoints = 0m,
                ReducePoints = 0m,
                Reason = "",
                Note = "",
                Status = LoyaltyConstants.ActiveStatus
            };
        }
    }

    /// <summary>Chi tiết hai tab dưới của một khách hàng đang chọn.</summary>
    public sealed class LoyaltyCustomerDetail
    {
        public LoyaltyCustomerDetail()
        {
            Bills = new List<LoyaltyBillRow>();
            Vouchers = new List<LoyaltyPointVoucherRow>();
        }

        public List<LoyaltyBillRow> Bills { get; set; }
        public List<LoyaltyPointVoucherRow> Vouchers { get; set; }
    }

    /// <summary>Bộ lọc lưới khách hàng thân thiết.</summary>
    public sealed class LoyaltyFilter
    {
        public string GroupId { get; set; }
        public HashSet<string> GroupIds { get; set; }
        public bool UnassignedGroupOnly { get; set; }
        public string SearchText { get; set; }
        public bool OnlyWithPoints { get; set; }
    }

    /// <summary>Tổng cộng hiển thị trên lưới khách hàng thân thiết.</summary>
    public sealed class LoyaltyTotals
    {
        public int CustomerCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalBillCount { get; set; }
        public decimal TotalPoints { get; set; }
    }

    /// <summary>Tổng cộng hiển thị trên tab Hóa đơn.</summary>
    public sealed class LoyaltyBillTotals
    {
        public int Count { get; set; }
        public decimal TotalPoints { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalPointsUsed { get; set; }
    }

    /// <summary>Tổng cộng hiển thị trên tab Tặng điểm - tặng quà.</summary>
    public sealed class LoyaltyPointVoucherTotals
    {
        public int Count { get; set; }
        public decimal TotalGiftPoints { get; set; }
        public decimal TotalReducePoints { get; set; }
    }

    /// <summary>Kết quả lưu phiếu tặng/giảm điểm.</summary>
    public sealed class LoyaltySaveResult
    {
        public bool Ok { get; set; }
        public string SavedId { get; set; }
        public string SavedNumber { get; set; }
        public string CustomerName { get; set; }
        public string UpgradedGroupName { get; set; }
        public string ErrorMessage { get; set; }
        public bool HasNumberError { get; set; }
        public bool HasCustomerError { get; set; }
        public bool HasPointsError { get; set; }
        public bool HasReasonError { get; set; }
        public bool HasNoteError { get; set; }

        public string UpgradeNotice
        {
            get
            {
                return string.IsNullOrEmpty(UpgradedGroupName)
                    ? null
                    : LoyaltyConstants.UpgradedGroupMessage(CustomerName, UpgradedGroupName);
            }
        }

        public static LoyaltySaveResult Fail(string message)
        {
            return new LoyaltySaveResult { Ok = false, ErrorMessage = message };
        }
    }

    /// <summary>Kết quả xóa phiếu tặng/giảm điểm.</summary>
    public sealed class LoyaltyDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static LoyaltyDeleteResult Fail(string message)
        {
            return new LoyaltyDeleteResult { Ok = false, ErrorMessage = message };
        }
    }
}
