namespace Kazuko_FnB.Domain
{
    /// <summary>Hai quỹ tiền (docs/specs/quy-tien.md §4.1).</summary>
    public enum FundKind
    {
        Cash,
        Account
    }

    /// <summary>Hình thức trả của phiếu nhập / xuất trả hàng (§4.5).</summary>
    public enum PayMethod
    {
        Cash,
        Transfer
    }

    /// <summary>Một khoản làm đổi số dư quỹ.</summary>
    public sealed class FundEntry
    {
        public FundKind Fund { get; set; }
        /// <summary>DTAIKHOANNGANHANGID; rỗng = chưa xác định tài khoản. Quỹ tiền mặt luôn rỗng.</summary>
        public string AccountId { get; set; }
        /// <summary>Dương = tiền vào quỹ, âm = tiền ra.</summary>
        public decimal Amount { get; set; }
        /// <summary>true nếu là tiền quẹt thẻ (thuộc quỹ Tài khoản, xem riêng được).</summary>
        public bool IsCard { get; set; }
    }

    /// <summary>Các cột TDONHANG của hóa đơn bán liên quan tới quỹ.</summary>
    public sealed class FundBill
    {
        public int Type { get; set; }          // LOAI
        public int Status { get; set; }        // STATUS
        public int PaidFlag { get; set; }      // DATHANHTOAN
        public decimal Cash { get; set; }      // TIENMAT (có thể âm)
        public decimal Transfer { get; set; }  // CHUYENKHOAN
        public decimal Card { get; set; }      // THE
        public string AccountId { get; set; }  // DTAIKHOANNGANHANGID
    }

    /// <summary>Các cột TTHUCHI liên quan tới quỹ. Cột NULL đọc thành 0.</summary>
    public sealed class FundVoucher
    {
        public int Status { get; set; }        // STATUS
        public decimal Income { get; set; }    // THU
        public decimal Expense { get; set; }   // CHI
        public int TransferFlag { get; set; }  // CHUYENKHOAN (cờ)
        public string AccountId { get; set; }
    }

    /// <summary>Các cột TDONHANG của phiếu nhập (LOAI = 1) và xuất trả hàng (LOAI = 2).</summary>
    public sealed class FundStockDocument
    {
        public int Type { get; set; }           // LOAI
        public int Status { get; set; }         // STATUS
        public string SupplierId { get; set; }  // DNHACUNGCAPID
        public decimal PaidAmount { get; set; } // TIENTHANHTOAN
        public decimal Transfer { get; set; }   // CHUYENKHOAN (số tiền)
        public string AccountId { get; set; }
    }

    /// <summary>Số liệu ở hộp thanh toán bán hàng (§4.3).</summary>
    public sealed class FundPaymentInput
    {
        /// <summary>Số phải trả (đã trừ đặt trước, đã cộng nợ cũ thu kèm).</summary>
        public decimal Due { get; set; }
        public decimal CashTendered { get; set; }  // tiền mặt khách đưa
        public decimal Transfer { get; set; }
        public decimal Card { get; set; }
        /// <summary>Voucher + thẻ trả trước + trừ tích lũy.</summary>
        public decimal NonCash { get; set; }
        public string AccountId { get; set; }
    }

    public sealed class FundPaymentResult
    {
        /// <summary>null nếu hợp lệ; khi có lỗi thì các số tiền bằng 0.</summary>
        public string Error { get; set; }
        public decimal Change { get; set; }    // TRALAI
        /// <summary>Giá trị ghi TIENMAT = khách đưa − trả lại (có thể âm).</summary>
        public decimal CashKept { get; set; }
    }

    /// <summary>Giá trị các cột tiền cần ghi cho phiếu nhập / xuất trả hàng.</summary>
    public sealed class FundStockPaymentResult
    {
        public string Error { get; set; }
        public decimal Cash { get; set; }      // TIENMAT
        public decimal Transfer { get; set; }  // CHUYENKHOAN
        public string AccountId { get; set; }
    }

    public static class FundText
    {
        public const string NegativeAmount = "Số tiền không được âm";
        public const string AccountRequired = "Mời bạn chọn tài khoản";
        public const string CardOverDue = "Không được quẹt thẻ quá số tiền phải trả";
        public const string NothingDue = "Hóa đơn không còn số tiền phải trả, không nhận thêm tiền";
        public const string MethodRequired = "Mời bạn chọn hình thức thanh toán: tiền mặt hoặc chuyển khoản";
    }
}
