using System;
using System.Collections.Generic;

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

    // ------------------------------------------------------------------ Sổ quỹ (màn Tồn quỹ)

    /// <summary>Lý do thu chi dành riêng (ID cố định). Dòng DLYDOTHUCHI tương ứng được thêm ở bước 4.</summary>
    public static class FundReasons
    {
        public const string OpeningBalanceId = "4abb531f-99bd-435a-8c30-e8b9007d3fd7";
        public const string TransferId = "01aff53e-1c11-4b96-8430-6c74a190de1d";
    }

    public enum FundSourceKind
    {
        Sale,
        Voucher,
        Stock
    }

    /// <summary>Một giao dịch đọc từ CSDL; đúng một trong Bill / Voucher / Stock có giá trị, theo Kind.</summary>
    public sealed class FundSource
    {
        public FundSourceKind Kind { get; set; }
        public string Number { get; set; }
        public DateTime DocumentDate { get; set; }  // NGAY
        public DateTime? PaidTime { get; set; }     // GIOTHANHTOAN (hóa đơn bán)
        public DateTime? CreatedTime { get; set; }  // TIMECREATED
        public string StoreId { get; set; }
        public string ReasonId { get; set; }        // DLYDOTHUCHIID (phiếu thu/chi)
        public FundBill Bill { get; set; }
        public FundVoucher Voucher { get; set; }
        public FundStockDocument Stock { get; set; }
    }

    public sealed class FundAccount
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
    }

    /// <summary>Dữ liệu thô của màn Tồn quỹ.</summary>
    public sealed class FundData
    {
        public List<FundSource> Sources { get; set; }
        public List<FundAccount> Accounts { get; set; }
        public List<StoreRecord> Stores { get; set; }
    }

    /// <summary>Phạm vi cửa hàng đang xem.</summary>
    public sealed class FundStoreScope
    {
        /// <summary>Đang xem mọi cửa hàng của hệ thống: không lọc, và được hiện tồn tài khoản (Q13).</summary>
        public bool CoversAll { get; set; }
        /// <summary>Các cửa hàng đang xem; chỉ dùng để lọc khi CoversAll = false.</summary>
        public List<string> StoreIds { get; set; }
    }

    public enum FundNodeKind
    {
        All,
        Cash,
        AccountAll,
        Account,
        UnknownAccount
    }

    public sealed class FundQuery
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public FundStoreScope Scope { get; set; }
        public FundNodeKind Node { get; set; }
        public string AccountId { get; set; }  // khi Node = Account
        /// <summary>ID mọi tài khoản có trong DTAIKHOANNGANHANG; khoản ghi ID khác (hoặc rỗng) là "chưa xác định".</summary>
        public List<string> KnownAccountIds { get; set; }
    }

    public sealed class FundLedgerLine
    {
        public DateTime Time { get; set; }
        public string Number { get; set; }
        public string TypeText { get; set; }
        public FundKind Fund { get; set; }
        public string AccountId { get; set; }
        public bool IsCard { get; set; }
        public bool IsOpening { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        /// <summary>Tồn cộng dồn; null khi không được hiện tồn.</summary>
        public decimal? Balance { get; set; }
    }

    /// <summary>Kết quả sổ quỹ. Khi ShowBalance = false thì OpeningBalance, OpeningEntered, ClosingBalance bằng 0.</summary>
    public sealed class FundLedger
    {
        public List<FundLedgerLine> Lines { get; set; }
        public bool ShowBalance { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal OpeningEntered { get; set; }  // số dư đầu kỳ nhập trong khoảng đang xem
        public decimal Income { get; set; }          // không gồm dòng số dư đầu kỳ
        public decimal Expense { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal TransferNet { get; set; }     // thu - chi không phải quẹt thẻ, trong khoảng
        public decimal CardNet { get; set; }         // quẹt thẻ, trong khoảng
        /// <summary>Quỹ tiền mặt chưa nhập số dư đầu kỳ (ID cửa hàng).</summary>
        public List<string> CashStoresWithoutOpening { get; set; }
        /// <summary>Tài khoản chưa nhập số dư đầu kỳ (ID tài khoản).</summary>
        public List<string> AccountsWithoutOpening { get; set; }
        /// <summary>Phạm vi đang xem có khoản không ghi tài khoản (để hiện nút "Chưa xác định").</summary>
        public bool HasUnknownAccount { get; set; }
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
