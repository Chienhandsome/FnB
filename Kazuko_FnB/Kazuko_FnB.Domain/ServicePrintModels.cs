using System;
using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Chế độ in hóa đơn theo khu vực (SCONFIG.InHoaDonTheoKhuVuc, theo goc-PrintInvoice-nhahang).</summary>
    public enum InvoiceAreaPrintMode
    {
        AtCounter = 0,
        ByArea = 1,
        TempByAreaBillAtCounter = 2,
        OneAtCounterOneByArea = 3
    }

    /// <summary>Hằng số và thông báo nguyên văn cho luồng in hóa đơn và in bếp/chế biến (patch sddv-d).</summary>
    public static class ServicePrintConstants
    {
        public const string InvoiceTitleOfficial = "HÓA ĐƠN THANH TOÁN";
        public const string InvoiceTitleTemp = "PHIẾU TẠM TÍNH";
        public const string KitchenTitlePrefix = "PHIẾU CHẾ BIẾN";
        public const string NoPaidBillToReprint = "Chưa có hóa đơn đã thanh toán nào để in lại.";
        public const string BillNotFoundForPrint = "Không tìm thấy hóa đơn để in.";

        // Thông báo nguyên văn từ goc-InCheBien-code.cs.txt
        public const string KitchenPrinterNotConfigured = "Mời bạn thiết lập máy in xuống bar/bếp trước";
        public const string KitchenNoItemToPrint = "Không có mặt hàng nào để in";
        public const string KitchenNoRemainingItem = "Không còn mặt hàng nào để in";
        public const string KitchenBillAlreadyPaid = "Hóa đơn đã thanh toán, không thể in";
        public const string KitchenSelectPrinterPrompt = "Mời bạn chọn máy in";
        public const string KitchenFeatureDisabled = "Chức năng in xuống bếp chưa được kích hoạt.";

        // Khóa lưu máy in bếp (giống goc-InCheBien-code.cs.txt)
        public const string KeyPrinterFood = "MAY_IN_DO_AN";
        public const string KeyPrinterDrink = "MAY_IN_DO_UONG";
        public const string KeyPrinterOther = "MAY_IN_KHAC";
        public const string KeyPrinterService = "MAY_IN_DICH_VU";

        // Mã loại đồ DLOAIDOID
        public const string DrinkTypeFood = "0";
        public const string DrinkTypeDrink = "1";
        public const string DrinkTypeService = "2";
        public const string DrinkTypeOther = "3";

        public static string MaxTempPrintReached(int maxCount)
        {
            return "Chỉ cho phép in tạm tính tối đa " + maxCount + " lần";
        }

        public static string MaxInvoicePrintReached(int maxCount)
        {
            return "Chỉ cho phép in tối đa " + maxCount + " lần";
        }
    }

    /// <summary>Dữ liệu hóa đơn đầy đủ đọc lại từ CSDL để đổ vào các mẫu in STEMPLATE của màn Hóa đơn bán hàng.</summary>
    public sealed class ServiceInvoicePrintDocument
    {
        public string BillId { get; set; }
        public string BillName { get; set; }
        public string Note { get; set; }
        public DateTime Ngay { get; set; }
        public DateTime BatDau { get; set; }
        public DateTime? KetThuc { get; set; }
        public DateTime? GioThanhToan { get; set; }
        public int GuestCount { get; set; }
        public int SequenceInDay { get; set; }
        public int PaidFlag { get; set; }
        public int TempPrintCount { get; set; }
        public int InvoicePrintCount { get; set; }

        public string TableId { get; set; }
        public string TableName { get; set; }
        public string AreaId { get; set; }
        public string AreaName { get; set; }
        public string AreaTemplateId { get; set; }

        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string PassWifi { get; set; }

        // Các cột số tiền đọc trực tiếp từ TDONHANG đã lưu (KHÔNG tính lại)
        public decimal TienHang { get; set; }
        public decimal TienHangChuaGiam { get; set; }
        public decimal GiamGiaMatHang { get; set; }
        public decimal TienGio { get; set; }
        public decimal TileGiamGia { get; set; }
        public decimal TienGiamGia { get; set; }
        public decimal TileGiamGiaGio { get; set; }
        public decimal TienGiamGiaGio { get; set; }
        public decimal TileGiamGiaTong { get; set; }
        public decimal TienGiamGiaTong { get; set; }
        public decimal TilePhiDichVu { get; set; }
        public decimal PhiDichVu { get; set; }
        public decimal TileThue { get; set; }
        public decimal TienThue { get; set; }
        public decimal TongCong { get; set; }

        public decimal Voucher { get; set; }
        public decimal The { get; set; }
        public decimal ChuyenKhoan { get; set; }
        public decimal TheTraTruoc { get; set; }
        public decimal TruTichLuy { get; set; }
        public decimal TienMat { get; set; }
        public decimal KhachDua { get; set; }
        public decimal TraLai { get; set; }
        public decimal TienThanhToan { get; set; }
        public decimal ConLai { get; set; }
        public decimal CongNo { get; set; }
        public decimal Diem { get; set; }
        public decimal DiemGiam { get; set; }
        public decimal DoiTra { get; set; }
        public decimal NoCu { get; set; }
        public decimal DatTruoc { get; set; }
        public decimal CustomerTotalPoints { get; set; }

        public List<ServiceInvoicePrintLine> ParentLines { get; set; }
        public List<ServiceInvoicePrintLine> ComboChildLines { get; set; }
        public List<ServiceInvoiceConsolidatedLine> ConsolidatedLines { get; set; }

        public ServiceInvoicePrintDocument()
        {
            ParentLines = new List<ServiceInvoicePrintLine>();
            ComboChildLines = new List<ServiceInvoicePrintLine>();
            ConsolidatedLines = new List<ServiceInvoiceConsolidatedLine>();
        }
    }

    /// <summary>Một dòng chi tiết hóa đơn (Table0 hoặc ComboChild) đọc từ TDONHANGCHITIET (đã bỏ XUATVATTU = 30).</summary>
    public sealed class ServiceInvoicePrintLine
    {
        public string Id { get; set; }
        public string Note { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public string EnglishName { get; set; }
        public string ProductTypeId { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string Warranty { get; set; }
        public string ItemName { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Amount { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SlNhap { get; set; }
        public decimal SlXuat { get; set; }
        public decimal SlNhapChuaQuyDoi { get; set; }
        public decimal SlXuatChuaQuyDoi { get; set; }
        public string ComboId { get; set; }
        public string ComboParentId { get; set; }
        public DateTime? TuGio { get; set; }
        public DateTime? DenGio { get; set; }
    }

    /// <summary>Một dòng gộp theo (TENHANG, DDONVITINHID, DONGIA) cho bảng Data.InCongGop.</summary>
    public sealed class ServiceInvoiceConsolidatedLine
    {
        public string ItemName { get; set; }
        public string UnitName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>Tùy chọn ngữ cảnh khi dựng PrintJobData cho hóa đơn nhà hàng.</summary>
    public sealed class InvoicePrintBuildOptions
    {
        public bool IsTempPrint { get; set; }
        public bool GopChungChietKhau { get; set; }
        public bool ShowCustomerPoints { get; set; }
        public string LoiCamOn { get; set; }
        public string PrintedBy { get; set; }
    }

    /// <summary>Kết quả chuẩn bị/ghi nhận in tạm tính ở tầng Service.</summary>
    public sealed class TempPrintPrepareResult
    {
        public bool Ok { get; set; }
        public bool NeedsReLogin { get; set; }
        public string RequiredFunctionId { get; set; }
        public string ErrorMessage { get; set; }
        public int NewTempPrintCount { get; set; }
        public ServiceInvoicePrintDocument Document { get; set; }

        public static TempPrintPrepareResult Fail(string msg)
        {
            return new TempPrintPrepareResult { Ok = false, ErrorMessage = msg };
        }

        public static TempPrintPrepareResult ReLogin(string funcId)
        {
            return new TempPrintPrepareResult
            {
                Ok = false,
                NeedsReLogin = true,
                RequiredFunctionId = funcId,
                ErrorMessage = ServiceOrderConstants.NoPermission
            };
        }
    }

    /// <summary>Một dòng thô từ TDONHANGCHITIET hoặc TINCHEBIEN để tính chênh lệch cần in bếp.</summary>
    public sealed class KitchenRawLine
    {
        public string ItemName { get; set; }
        public string Note { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string DrinkTypeId { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>Một dòng đã cộng dồn chênh lệch cần in xuống bếp (dương = gọi thêm, âm = hủy/giảm).</summary>
    public sealed class KitchenPendingItem
    {
        public string ItemName { get; set; }
        public string Note { get; set; }
        public string UnitId { get; set; }
        public string UnitName { get; set; }
        public string DrinkTypeId { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>Một phiếu bếp nhóm theo máy in và loại đồ (tương ứng một tab InXuongBepControl trong app gốc).</summary>
    public sealed class KitchenPrintBatch
    {
        public string PrinterName { get; set; }
        public string Caption { get; set; }
        public List<string> DrinkTypeIds { get; set; }
        public List<KitchenPendingItem> Items { get; set; }

        public KitchenPrintBatch()
        {
            DrinkTypeIds = new List<string>();
            Items = new List<KitchenPendingItem>();
        }
    }

    /// <summary>Một dòng thiết lập máy in bếp theo loại đồ (hoặc theo khu vực + loại đồ).</summary>
    public sealed class KitchenPrinterSetupRow
    {
        public string AreaName { get; set; }
        public string DrinkTypeLabel { get; set; }
        public string ConfigKey { get; set; }
        public string PrinterName { get; set; }
    }

    /// <summary>Cấu hình bật/tắt in bếp truyền vào bộ lọc thuần KitchenPrintRules.</summary>
    public sealed class KitchenPrintSettings
    {
        public bool Enabled { get; set; }
        public bool PrintFood { get; set; }
        public bool PrintDrink { get; set; }
        public bool PrintService { get; set; }
        public bool PrintOther { get; set; }
        public bool SeparateFoodAndDrink { get; set; }
        public bool ByArea { get; set; }
        public bool IncludeUnitPrice { get; set; }
        public int Copies { get; set; }
        public bool OneItemPerSlip { get; set; }
        public bool ExtraCounterCopy { get; set; }
        public bool LogActivity { get; set; }
    }

    /// <summary>Kết quả lập kế hoạch in bếp cho một bill.</summary>
    public sealed class KitchenPrintPlan
    {
        public bool Ok { get; set; }
        public bool NeedsPrinterSetup { get; set; }
        public bool NoItemsToPrint { get; set; }
        public string ErrorMessage { get; set; }
        public string BillId { get; set; }
        public string BillName { get; set; }
        public string TableName { get; set; }
        public string AreaId { get; set; }
        public string AreaName { get; set; }
        public List<KitchenPrintBatch> Batches { get; set; }

        public KitchenPrintPlan()
        {
            Batches = new List<KitchenPrintBatch>();
        }
    }
}
