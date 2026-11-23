using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng TDATHANG (đầu phiếu đặt hàng). Tiền decimal, khớp NUMERIC(18,2).</summary>
    public sealed class BookingRow
    {
        /// <summary>Rỗng = thêm mới (repository gán Id mới sau khi kiểm trùng, như ProductRepository).</summary>
        public string Id { get; set; }
        /// <summary>GUID chung của đoàn (mỗi bàn một dòng TDATHANG). Rỗng = đoàn mới.</summary>
        public string Guid { get; set; }
        public string Name { get; set; }
        public DateTime Ngay { get; set; }
        public DateTime GioDat { get; set; }
        public DateTime? TuGio { get; set; }
        public DateTime? DenGio { get; set; }
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public string CustomerId { get; set; }
        public string TenKhach { get; set; }
        public string DiaChi { get; set; }
        public string DienThoai { get; set; }
        /// <summary>Không có ô trên dialog: giữ nguyên giá trị cũ khi sửa (§3.2).</summary>
        public string Email { get; set; }
        public string MethodId { get; set; }
        public string PurposeId { get; set; }
        public string Note { get; set; }
        /// <summary>MAUSAC: ARGB int, mặc định cam.</summary>
        public int ColorArgb { get; set; }
        public decimal TienHang { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienGiamGia { get; set; }
        /// <summary>GIAMTHEOTIEN: true = giảm theo tiền, false = giảm theo %.</summary>
        public bool GiamTheoTien { get; set; }
        public decimal TileThue { get; set; }
        public decimal TienThue { get; set; }
        public decimal PhiVanChuyen { get; set; }
        public decimal TongCong { get; set; }
        /// <summary>LOAI/LOAIGIA: NotShow, không dùng (§6.2 Q9); ghi 0 khi mới, giữ nguyên khi sửa.</summary>
        public int Loai { get; set; }
        public int LoaiGia { get; set; }
        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        /// <summary>Các dòng TDATHANG cùng đoàn (nạp kèm khi đọc một phiếu); null nếu chưa nạp.</summary>
        public List<GroupRowRef> GroupRows { get; set; }
        /// <summary>Chi tiết của dòng đang mở (nạp kèm khi đọc một phiếu); null nếu chưa nạp.</summary>
        public List<BookingLine> Lines { get; set; }

        public static BookingRow NewDefault()
        {
            return new BookingRow
            {
                Ngay = DateTime.Today,
                GioDat = DateTime.Now,
                // Cam mặc định của app gốc (Color.Orange.ToArgb() = -23296); Domain không tham chiếu System.Drawing.
                ColorArgb = -23296,
                // STATUS = 30 (đang dùng); Domain không tham chiếu Data nên dùng số trực tiếp.
                Status = 30
            };
        }
    }

    /// <summary>Một dòng TDATHANGCHITIET (mặt hàng trong phiếu đặt).</summary>
    public sealed class BookingLine
    {
        public string Id { get; set; }
        public string BookingId { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Một hàng trên lưới màn "Khách đặt hàng" (§1.1 + Q5/Q6: mỗi dòng TDATHANG một hàng).</summary>
    public sealed class BookingListRow
    {
        public string Id { get; set; }
        public string Guid { get; set; }
        public DateTime Ngay { get; set; }
        public string Name { get; set; }
        public string TenKhach { get; set; }
        public string DiaChi { get; set; }
        public string DienThoai { get; set; }
        public string Email { get; set; }
        public decimal TongCong { get; set; }
        public decimal TienHang { get; set; }
        public decimal TienGiamGia { get; set; }
        public decimal TileGiamGia { get; set; }
        public string MethodName { get; set; }
        public string PurposeName { get; set; }
        public DateTime? TuGio { get; set; }
        public DateTime? DenGio { get; set; }
        public DateTime GioDat { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public string Note { get; set; }
        // Panel "Thông tin" dưới lưới (khởi tạo / sửa đổi).
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }
    }

    /// <summary>
    /// Thư mục "Phương thức đặt" (DPHUONGTHUCDAT): cây thư mục thường (PARENTID/PARENTDIR/SORTORDER), cho thêm,
    /// sửa, xóa như danh mục khác; Công văn/Điện thoại/Email/Tin nhắn/Trực tiếp chỉ là thư mục tạo sẵn.
    /// </summary>
    public sealed class BookingMethodRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string ParentId { get; set; }
        public string ParentDir { get; set; }
        public string SortOrder { get; set; }
        public int AutoId { get; set; }
        public int Status { get; set; }
        public string ImageId { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }

    /// <summary>Điều kiện lọc màn danh sách (§1.1). MethodId: null = Tất cả, "" = Chưa thiết lập.</summary>
    public sealed class BookingFilter
    {
        public string NumberText { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string CustomerId { get; set; }
        public string MethodId { get; set; }
        /// <summary>Lọc theo cả thư mục và các thư mục con (ưu tiên hơn MethodId khi có phần tử).</summary>
        public List<string> MethodIds { get; set; }
    }

    /// <summary>Mặt hàng để chọn vào phiếu đặt (lưới tìm mặt hàng, §1.2).</summary>
    public sealed class BookingSaleItem
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string BigUnitId { get; set; }
        public string BigUnitName { get; set; }
        public decimal QuyDoi { get; set; }
        public decimal SellPrice { get; set; }
        public decimal SellPrice2 { get; set; }
        public decimal SellPrice3 { get; set; }
        public decimal SellPrice4 { get; set; }
        public decimal BigSellPrice { get; set; }
        public string TypeId { get; set; }

        public bool HasBigUnit
        {
            get { return !string.IsNullOrEmpty(BigUnitId); }
        }

        /// <summary>Giá bán theo loại giá của khách (0→GIABAN, 1→GIABAN2, 2→GIABAN3, khác→GIABAN4).</summary>
        public decimal PriceForCustomer(int priceType)
        {
            if (priceType == 1)
                return SellPrice2;
            if (priceType == 2)
                return SellPrice3;
            if (priceType == 3)
                return SellPrice4;
            return SellPrice;
        }
    }

    /// <summary>Một bàn trong hộp "CHỌN PHÒNG" (§1.2, ảnh 05).</summary>
    public sealed class BookingRoomItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string AreaName { get; set; }
        public string TypeName { get; set; }
    }

    /// <summary>Khách hàng để điền tên/địa chỉ/điện thoại và loại giá khi chọn khách.</summary>
    public sealed class BookingCustomerInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        /// <summary>DKHACHHANG.GIABAN: 0→GIABAN, 1→GIABAN2, 2→GIABAN3, khác→GIABAN4.</summary>
        public int PriceType { get; set; }
    }

    /// <summary>Phiếu thu cọc của một phiếu đặt (TTHUCHI theo TDATHANGID, §3.5).</summary>
    public sealed class BookingDepositInfo
    {
        public string Id { get; set; }
        public string Number { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class BookingSaveResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        /// <summary>ID dòng TDATHANG đang sửa (dòng đầu đoàn khi thêm mới).</summary>
        public string SavedId { get; set; }
        /// <summary>ID mọi dòng TDATHANG của đoàn sau khi lưu.</summary>
        public List<string> GroupIds { get; set; }

        public static BookingSaveResult Fail(string error)
        {
            return new BookingSaveResult { Ok = false, ErrorMessage = error };
        }
    }

    public sealed class BookingDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }

        public static BookingDeleteResult Fail(string error)
        {
            return new BookingDeleteResult { Ok = false, ErrorMessage = error };
        }
    }

    /// <summary>Một dòng TDATHANG trong đoàn (chỉ ID + bàn + số phiếu), dùng lập kế hoạch đoàn (§3.1).</summary>
    public sealed class GroupRowRef
    {
        public string Id { get; set; }
        public string RoomId { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Kế hoạch thay đổi đoàn do BookingRules.PlanGroupChanges tính (thuần, kiểm thử được).
    /// Repository chỉ thực thi kế hoạch này trong một transaction.
    /// </summary>
    public sealed class GroupChangePlan
    {
        /// <summary>DBANID cuối cùng của dòng đang sửa (sau luật BeforeSave #2).</summary>
        public string CurrentRoomId { get; set; }
        /// <summary>Dòng giữ nguyên bàn (kể cả dòng đang sửa): Id → DbanId cuối cùng.</summary>
        public List<GroupRowRef> Keep { get; set; }
        /// <summary>Dòng của bàn bị bỏ được tái dùng cho bàn mới: Id + bàn mới (giữ số phiếu cũ).</summary>
        public List<GroupRowRef> Reuse { get; set; }
        /// <summary>Dòng thừa không tái dùng được → xóa.</summary>
        public List<string> DeleteIds { get; set; }
        /// <summary>Bàn mới chưa có dòng sau khi tái dùng → tạo dòng mới (sinh số phiếu mới).</summary>
        public List<string> CreateRoomIds { get; set; }
    }

    /// <summary>Thông tin mở dialog Phiếu thu ở chế độ đặt tiền trước (§3.5).</summary>
    public sealed class PhieuThuDepositPreset
    {
        public string BookingId { get; set; }
        public string BookingNumber { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Address { get; set; }
        public decimal Amount { get; set; }
        /// <summary>ID phiếu thu cọc đã có (mở lại để sửa); null = lập phiếu mới.</summary>
        public string ExistingReceiptId { get; set; }
    }

    /// <summary>Dữ liệu ghi phiếu thu cọc xuống TTHUCHI (xem PhieuThuRepository.SaveDeposit).</summary>
    public sealed class BookingDeposit
    {
        /// <summary>Rỗng = lập mới; có giá trị = cập nhật phiếu đã có.</summary>
        public string Id { get; set; }
        /// <summary>Loại đối tượng (0 Khác, 1 Nhân viên, 2 Khách hàng, 3 Nhà cung cấp — theo PhieuThuRules).</summary>
        public int ObjectType { get; set; }
        public string BookingId { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Address { get; set; }
        public string EmployeeId { get; set; }
        public string StoreId { get; set; }
        public string ReasonId { get; set; }
        public string Note { get; set; }
        public decimal Amount { get; set; }
        public bool Transfer { get; set; }
        public string BankAccountId { get; set; }
    }

    /// <summary>Câu chữ của chức năng Khách đặt hàng (nguyên văn app gốc / framework, §2.1/§3.4).</summary>
    public static class BookingText
    {
        public const string FunctionName = "Đặt hàng";
        public const string FunctionId = "97487cfa-66f6-415e-9046-9a245c61f2ae";

        public const string SelectRoomFirst = "Mời bạn chọn phòng trước";
        /// <summary>Quyết định Chiến 21/11 (§5.4): sửa câu gốc nghĩa đảo, so cả ngày+giờ.</summary>
        public const string DateOrder = "Giờ ra phải sau giờ vào";
        public const string EnterQuantity = "Mời bạn nhập số lượng";
        public const string NotFound = "Đặt hàng không còn tồn tại.";
        public const string DeleteConfirm = "Bạn có muốn thực hiện xóa đặt hàng đang chọn không?";
        public const string DeleteBlocked = "Bạn không thể xóa đặt hàng này vì đã đặt tiền hoặc đã tạo hóa đơn";
        public const string Unsupported = "Chức năng chưa hỗ trợ trong phiên bản này.";

        public static string Required(string fieldLabel)
        {
            return string.Format("Trường \"{0}\" không được trống!", fieldLabel);
        }

        public static string NumberDuplicate(string number)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Số phiếu\" đã tồn tại!", number);
        }

        public static string DepositNote(string bookingNumber)
        {
            return "Đặt tiền trước số phiếu đặt hàng: " + bookingNumber;
        }
    }
}
