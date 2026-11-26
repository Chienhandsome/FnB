using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Chế độ hộp thoại Chuyển / Gộp bàn / Chuyển mặt hàng (spec §3.8, ban-khu-vuc.md §2).</summary>
    public enum TableTransferMode
    {
        MoveTable = 0,
        MergeTable = 1,
        TransferItem = 2
    }

    /// <summary>Cách cập nhật giá khi chuyển/gộp bàn theo SCONFIG.GiaMatHangKhiChuyenBan (ban-khu-vuc.md §2).</summary>
    public enum MoveTablePriceMode
    {
        /// <summary>0 = chỉ nâng giá nếu đơn giá hiện tại &lt; giá khu vực mới.</summary>
        RaiseOnly = 0,
        /// <summary>1 = luôn cập nhật theo giá khu vực mới.</summary>
        AlwaysUpdate = 1,
        /// <summary>2 = giữ nguyên giá hiện tại.</summary>
        KeepCurrent = 2
    }

    /// <summary>Thông báo nguyên văn cho Chuyển bàn, Gộp bàn, Chuyển mặt hàng và Hủy bill (spec §3.8, §6.4 P5, Q19, Q20).</summary>
    public static class ServiceTransferConstants
    {
        public const string TableTempPrintedBlocked = "Bàn này đã in tạm tính, không thể thay đổi";
        public const string TableAlreadyPaidBlocked = "Bàn này đã thanh toán, không thể thay đổi";
        public const string ChooseDifferentTable = "Mời bạn chọn bàn khác";
        public const string DestTableInUseForMove = "Bàn này đang sử dụng, mời bạn chọn bàn khác(bàn trống) để chuyển!";
        public const string DestTableEmptyForMerge = "Bàn này không sử dụng, mời bạn chọn bàn khác (bàn đang có khách) để gộp!";
        public const string DestTableEmptyForItemTransfer = "Bàn này không sử dụng, mời bạn chọn bàn khác (bàn đang có khách) để chuyển mặt hàng!";
        public const string MergeBlockedHasBooking = "Không thể gộp bàn vì hóa đơn nguồn được tạo từ phiếu đặt hàng.";
        public const string MergeBlockedDifferentDiscount = "Không thể gộp bàn vì tỉ lệ giảm giá của hai hóa đơn khác nhau.";
        public const string CancelBlockedInGroup = "Không thể hủy hóa đơn thuộc nhóm thanh toán chung.";
        public const string CancelReasonRequired = "Mời bạn nhập lý do hủy hóa đơn.";
        public const string CancelDefaultReason = "Hủy hóa đơn";
        public const string CancelBillAlreadyDeleted = "Đơn hàng này đã hủy";
        public const string TransferItemSelectLine = "Mời bạn chọn mặt hàng trong hóa đơn để chuyển.";
        public const string TransferItemInvalidQty = "Số lượng chuyển phải lớn hơn 0 và không vượt quá số lượng hiện có.";
        public const string TransferItemChildNotAllowed = "Không thể chuyển riêng dòng con của combo. Mời bạn chọn dòng combo chính.";

        public static string TableLockedBy(string tableName, string account)
        {
            return string.Format("Bàn {0} đang bị giữ bởi {1}", tableName ?? "", account ?? "");
        }

        public static string MoveTableConfirm(string fromTable, string toTable)
        {
            return string.Format("Bạn có muốn chuyển hóa đơn từ bàn '{0}' sang bàn '{1}' không?",
                fromTable ?? "", toTable ?? "");
        }

        public static string MergeTableConfirm(string fromTable, string toTable)
        {
            return string.Format(
                "Bạn có chắc chắn muốn gộp bàn '{0}' và bàn '{1}' không? Sau khi gộp, chỉ còn hóa đơn trên bàn '{1}', các mặt hàng sẽ được chuyển sang bàn này.",
                fromTable ?? "", toTable ?? "");
        }

        /// <summary>Sửa lỗi chính tả "muỗn" -> "muốn" theo P5 (§6.4).</summary>
        public static string CancelTableConfirm(string tableName)
        {
            return string.Format("Bạn có muốn hủy hóa đơn trên bàn '{0}' không?", tableName ?? "");
        }

        public static string MergeBlockedHasAdvance(string tableName)
        {
            return string.Format(
                "Bàn '{0}' đã có tiền tạm ứng, không thể gộp. Mời bạn dùng Thanh toán chung hoặc thanh toán riêng.",
                tableName ?? "");
        }

        public static string CancelBlockedHasAdvance(string tableName)
        {
            return string.Format(
                "Hóa đơn trên bàn '{0}' đã có tiền tạm ứng, không thể hủy. Mời bạn lập phiếu chi trả lại tạm ứng trước.",
                tableName ?? "");
        }

        public static string CancelBlockedExpired(int maxMinutes)
        {
            return string.Format("Hóa đơn đã mở quá thời gian cho phép hủy ({0} phút), không thể hủy.", maxMinutes);
        }

        public static string MergeCancelledReason(string destBillName)
        {
            return "Gộp vào hóa đơn " + (destBillName ?? "");
        }
    }

    public sealed class MoveTableRequest
    {
        public string SourceBillId { get; set; }
        public string SourceTableId { get; set; }
        public string TargetTableId { get; set; }
        /// <summary>Chỉ dùng cho selftest mô phỏng lỗi giữa chừng để kiểm tra rollback sạch transaction.</summary>
        public Action TestHookBeforeCommit { get; set; }
    }

    public sealed class MergeTableRequest
    {
        public string SourceBillId { get; set; }
        public string SourceTableId { get; set; }
        public string TargetTableId { get; set; }
        /// <summary>Chỉ dùng cho selftest mô phỏng lỗi giữa chừng sau khi ghi TDONHANGHUY để kiểm tra rollback.</summary>
        public Action TestHookAfterSnapshot { get; set; }
    }

    public sealed class TransferItemRequest
    {
        public string SourceBillId { get; set; }
        public string SourceTableId { get; set; }
        public string TargetTableId { get; set; }
        public string LineId { get; set; }
        /// <summary>Null hoặc &lt; 0 = chuyển nguyên dòng (kèm con combo); &gt; 0 = chuyển đúng số lượng này.</summary>
        public decimal? TransferQty { get; set; }
        /// <summary>Chỉ dùng cho selftest mô phỏng lỗi giữa chừng để kiểm tra rollback sạch transaction.</summary>
        public Action TestHookBeforeCommit { get; set; }
    }

    public sealed class CancelBillRequest
    {
        public string BillId { get; set; }
        public string TableId { get; set; }
        public string Reason { get; set; }
        /// <summary>Chỉ dùng cho selftest mô phỏng lỗi giữa chừng sau khi ghi TDONHANGHUY để kiểm tra rollback.</summary>
        public Action TestHookAfterSnapshot { get; set; }
    }

    /// <summary>Kết quả chuẩn cho các thao tác Chuyển bàn, Gộp bàn, Chuyển mặt hàng và Hủy bill.</summary>
    public sealed class TableTransferResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public bool NeedsReLogin { get; set; }
        public string RequiredFunctionId { get; set; }
        public string SourceBillId { get; set; }
        public string TargetBillId { get; set; }
        public string TargetTableId { get; set; }
        public string CancelledBillHuyId { get; set; }
        public ServiceOrderHeader SourceBill { get; set; }
        public ServiceOrderHeader TargetBill { get; set; }

        public static TableTransferResult Fail(string message)
        {
            return new TableTransferResult { Ok = false, ErrorMessage = message };
        }

        public static TableTransferResult ReLogin(string functionId)
        {
            return new TableTransferResult
            {
                Ok = false,
                NeedsReLogin = true,
                RequiredFunctionId = functionId,
                ErrorMessage = ServiceOrderConstants.NoPermission
            };
        }
    }
}
