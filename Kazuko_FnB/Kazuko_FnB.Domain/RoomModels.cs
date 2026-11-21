using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Cách tính giờ của bàn/khu vực (cột CACHTINHGIO, số nguyên).
    /// Q8 CHƯA XÁC MINH: mã số enum CachTinhGiaGio không có trong mã đã xuất (nằm trong No1Lib).
    /// Giá trị dưới đây là suy luận duy nhất khớp cả hai giá trị đã quan sát trong DATA.fdb thật:
    /// bàn "bàn 1" có CACHTINHGIO = 0 (mặc định khi thêm bàn là "theo khu vực") và khu vực "Lầu 1"
    /// có CACHTINHGIO = 1 (dialog khu vực chỉ có hai lựa chọn, mặc định plausibly là "theo giờ").
    /// KHÔNG dùng các hằng này làm bằng chứng — khi có enum thật hoặc test được trên app gốc thì sửa lại.
    /// </summary>
    public static class RoomCalc
    {
        /// <summary>Theo khu vực (mặc định của bàn mới, spec 2.1).</summary>
        public const int ByArea = 0;
        /// <summary>Theo giờ.</summary>
        public const int ByHour = 1;
        /// <summary>Theo bảng giá.</summary>
        public const int ByPriceList = 2;
    }

    /// <summary>Một bàn (DBAN): đủ các cột dialog sửa + các cột không có ô (giữ nguyên khi ghi, xem spec 3.1).</summary>
    public sealed class RoomRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>Khu vực của bàn (DKHUVUCID). Bắt buộc (MUSTSELECTCATEGORY = 30).</summary>
        public string AreaId { get; set; }
        /// <summary>Bảng giá (DBANGGIAID). NULL khi không chọn theo bảng giá.</summary>
        public string PriceListId { get; set; }
        /// <summary>Đơn giá (DONGIA, NUMERIC(18,2)). Mặc định 0.</summary>
        public decimal UnitPrice { get; set; }
        /// <summary>Cách tính giờ (CACHTINHGIO). Mặc định theo khu vực (RoomCalc.ByArea).</summary>
        public int CalcHour { get; set; }
        /// <summary>Nhóm hiển thị (DNHOMHIENTHIID). NULL khi không chọn.</summary>
        public string DisplayGroupId { get; set; }
        /// <summary>Tiền mở bàn (TIENMOBAN, NUMERIC(18,2)). Mặc định 0.</summary>
        public decimal OpenAmount { get; set; }
        /// <summary>Loại phòng (DLOAIPHONGID). NULL khi không chọn.</summary>
        public string RoomTypeId { get; set; }
        public string Note { get; set; }
        /// <summary>IP công tắc điện (IPCONGTAC). NULL khi không có.</summary>
        public string SwitchIp { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị (chỉ để hiện trên lưới, không ghi xuống DB).
        public string AreaName { get; set; }
        public string DisplayGroupName { get; set; }
        public string RoomTypeName { get; set; }
        public string PriceListName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        /// <summary>
        /// Bàn mới với đúng giá trị app gốc ghi khi thêm (spec 2.1): CACHTINHGIO = theo khu vực,
        /// DONGIA/TIENMOBAN = 0. Người gọi gán thêm AreaId (khu vực đang chọn).
        /// Các cột thiết kế (POSLEFT, WIDTH…) và runtime (TRANGTHAI, TDONHANGID…) không nằm trong
        /// RoomRow: repo không đụng tới chúng nên khi sửa chúng giữ nguyên (spec 3.1).
        /// </summary>
        public static RoomRow NewDefault()
        {
            return new RoomRow
            {
                Name = "",
                AreaId = null,
                PriceListId = null,
                UnitPrice = 0m,
                CalcHour = RoomCalc.ByArea,
                DisplayGroupId = null,
                OpenAmount = 0m,
                RoomTypeId = null,
                Note = null,
                SwitchIp = null,
                Status = 30
            };
        }
    }

    /// <summary>Một khu vực (DKHUVUC): đủ các cột dialog sửa (spec 1.3/3.3).</summary>
    public sealed class AreaRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>Mẫu hóa đơn (STEMPLATEID → STEMPLATE). NULL = dùng SystemConfig.MauHoaDon khi lên bill.</summary>
        public string TemplateId { get; set; }
        /// <summary>Kho hàng (DKHOHANGID). NULL khi không chọn.</summary>
        public string WarehouseId { get; set; }
        /// <summary>
        /// Giá mặt hàng (DGIAMATHANGID → DGIAMATHANG): '0' Hiển thị để lựa chọn, '1' Giá chung,
        /// '2' Giá bán t2, '3' Giá bán t3, '4' Giá bán cafe. Mặc định '0' (giá trị seed trong DB thật).
        /// </summary>
        public string PriceLevelId { get; set; }
        /// <summary>Giá theo giờ (DGIATHEOGIOID → DGIAMATHANG). NULL khi không chọn.</summary>
        public string HourlyPriceId { get; set; }
        /// <summary>Từ giờ (TUGIO). NULL khi không đặt.</summary>
        public DateTime? FromHour { get; set; }
        /// <summary>Đến giờ (DENGIO). NULL khi không đặt.</summary>
        public DateTime? ToHour { get; set; }
        /// <summary>Tiền mở bàn (TIENMOBAN, NUMERIC(18,2)). Mặc định 0.</summary>
        public decimal OpenAmount { get; set; }
        /// <summary>Đơn giá (DONGIA, NUMERIC(18,2)). Mặc định 0.</summary>
        public decimal UnitPrice { get; set; }
        /// <summary>Bảng giá (DBANGGIAID). NULL khi không chọn theo bảng giá.</summary>
        public string PriceListId { get; set; }
        /// <summary>Cách tính giờ (CACHTINHGIO, chỉ hai giá trị trên dialog khu vực). Mặc định theo giờ.</summary>
        public int CalcHour { get; set; }
        /// <summary>Biểu tượng (DBIEUTUONGID → DBIEUTUONG). NULL khi không chọn.</summary>
        public string IconId { get; set; }
        /// <summary>Ảnh (SIMAGEID → SIMAGE). NULL khi không chọn.</summary>
        public string ImageId { get; set; }
        /// <summary>
        /// Tab hiển thị (TABHIENTHI, kiểu CheckBox trong SCOLUMN): 30 = tích, 0 = không (quy ước checkbox
        /// chung của app — cờ bật là 30; chưa xác minh riêng cho cột này). Ánh xạ "trên/dưới" chưa xác minh
        /// (ảnh app gốc 05 ghi "Tab hiển thị (trên/dưới)" nhưng không rõ chiều nào là tích).
        /// </summary>
        public int ShowTab { get; set; }
        public string Note { get; set; }

        public int Status { get; set; }
        public string CreatedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string ModifiedById { get; set; }
        public DateTime? ModifiedAt { get; set; }

        // Tên hiển thị (chỉ để hiện trên lưới/thông tin, không ghi xuống DB).
        public string TemplateName { get; set; }
        public string WarehouseName { get; set; }
        public string PriceLevelName { get; set; }
        public string CreatedByName { get; set; }
        public string ModifiedByName { get; set; }

        /// <summary>
        /// Khu vực mới với đúng giá trị seed trong DB thật: DGIAMATHANGID = '0', CACHTINHGIO = 1,
        /// DONGIA/TIENMOBAN = 0. Các cột cây/khung (PARENTID, SORTORDER…) và thiết kế (GRIDSIZE, WIDTH…)
        /// không nằm trong AreaRow: repo ghi giá trị gốc khi thêm (PARENTID '', SORTORDER 'ZZZZ',
        /// AUTOID = MAX + 1) và không đụng tới chúng khi sửa (spec 3.3).
        /// </summary>
        public static AreaRow NewDefault()
        {
            return new AreaRow
            {
                Name = "",
                TemplateId = null,
                WarehouseId = null,
                PriceLevelId = "0",
                HourlyPriceId = null,
                FromHour = null,
                ToHour = null,
                OpenAmount = 0m,
                UnitPrice = 0m,
                PriceListId = null,
                CalcHour = RoomCalc.ByHour,
                IconId = null,
                ImageId = null,
                ShowTab = 0,
                Note = null,
                Status = 30
            };
        }
    }

    /// <summary>Một dòng "Bảng giá áp dụng" của bàn (DBANGGIATHEOBAN) hoặc khu vực (DBANGGIATHEOKHUVUC).</summary>
    public sealed class PriceAssignment
    {
        /// <summary>ID bảng giá (DBANGGIAID → DBANGGIA).</summary>
        public string PriceListId { get; set; }
        public string PriceListName { get; set; }
    }

    /// <summary>Một người dùng trên trang "Truy cập" của khu vực (DTKTHEOKHUVUC).</summary>
    public sealed class AreaAccessRow
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        /// <summary>Có quyền truy cập khu vực này không (có dòng DTKTHEOKHUVUC).</summary>
        public bool Allowed { get; set; }
    }

    /// <summary>Một dòng "Mặt hàng mặc định" của khu vực (DMATHANGMACDINH).</summary>
    public sealed class AreaDefaultItem
    {
        /// <summary>Mặt hàng (DMATHANGID → DMATHANG).</summary>
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        /// <summary>Số lượng (SOLUONG, NUMERIC(18,2)).</summary>
        public decimal Quantity { get; set; }
    }

    /// <summary>Một biểu tượng khu vực (DBIEUTUONG) kèm ảnh xem trước.</summary>
    public sealed class AreaIconRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>Ảnh của biểu tượng (SIMAGEID → SIMAGE).</summary>
        public string ImageId { get; set; }
    }

    /// <summary>Một yêu cầu sinh bàn trong "Thêm nhanh" (một khu vực + danh sách tên đã sinh).</summary>
    public sealed class QuickAddRequest
    {
        public string AreaId { get; set; }
        public List<string> Names { get; set; }
    }

    /// <summary>Kết quả "Thêm nhanh bàn" (ghi tất cả các khu vực trong một transaction).</summary>
    public sealed class QuickAddResult
    {
        public bool Ok { get; set; }
        /// <summary>Thông báo lỗi nguyên văn để hiện cho người dùng (null khi ghi được).</summary>
        public string ErrorMessage { get; set; }
        /// <summary>Số bàn đã tạo.</summary>
        public int CreatedCount { get; set; }
    }

    /// <summary>Kết quả lưu bàn (kiểm trùng làm trong cùng transaction với ghi).</summary>
    public sealed class RoomSaveResult
    {
        public bool Ok { get; set; }
        /// <summary>Thông báo lỗi nguyên văn để hiện cho người dùng (null khi lưu được).</summary>
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
    }

    /// <summary>Kết quả xóa bàn (kèm danh sách tham chiếu đang chặn, nếu có).</summary>
    public sealed class RoomDeleteResult
    {
        public bool Ok { get; set; }
        public string ErrorMessage { get; set; }
        public List<ReferenceBlock> Blocks { get; set; }
    }

    /// <summary>Kết quả lưu khu vực (kiểm trùng làm trong cùng transaction với ghi).</summary>
    public sealed class AreaSaveResult
    {
        public bool Ok { get; set; }
        /// <summary>Thông báo lỗi nguyên văn để hiện cho người dùng (null khi lưu được).</summary>
        public string ErrorMessage { get; set; }
        public string SavedId { get; set; }
    }

    /// <summary>
    /// Chuỗi thông báo nguyên văn (spec 2.2/3.6/3.11), đặt ở Domain để cả Data (kiểm trùng trong
    /// transaction) và Services (kiểm tra khi lưu) dùng chung một nguồn.
    /// Hai câu Thêm nhanh giữ đúng mẫu câu của app gốc ("Bàn" trong dấu ngoặc kép, tên trong dấu nháy đơn);
    /// spec viết "<Bàn>" theo lối ghi chỗ giữ (placeholder), chữ thật là Bàn.
    /// </summary>
    public static class RoomText
    {
        public const string RoomNameEmpty = "Trường \"Tên bàn\" không được trống!";
        public const string RoomAreaEmpty = "Trường \"Khu vực\" không được trống!";
        public const string AreaNameEmpty = "Trường \"Tên khu vực\" không được trống!";
        public const string RoomGone = "Bàn không còn tồn tại.";
        public const string AreaGone = "Khu vực không còn tồn tại.";
        /// <summary>Thêm nhanh: TUSO &gt; DENSO (Q6, quyết định của Chiến 21/11).</summary>
        public const string InvalidRange = "Khoảng số không hợp lệ";

        public static string RoomNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên bàn\" đã tồn tại!", name);
        }

        public static string AreaNameDuplicate(string name)
        {
            return string.Format("Giá trị \"{0}\" ở trường \"Tên khu vực\" đã tồn tại!", name);
        }

        /// <summary>Thêm nhanh: tên đã có trong DBAN (2.2).</summary>
        public static string QuickAddExists(string name)
        {
            return string.Format("\"Bàn\" '{0}' đã tồn tại trong dữ liệu", name);
        }

        /// <summary>Thêm nhanh: tên trùng trong chính lần sinh (2.2).</summary>
        public static string QuickAddDuplicate(string name)
        {
            return string.Format("\"Bàn\" '{0}' bị trùng lặp", name);
        }
    }
}
