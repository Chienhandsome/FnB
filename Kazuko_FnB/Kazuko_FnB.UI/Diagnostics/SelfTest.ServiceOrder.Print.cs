using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FastReport;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class ServicePrintFixture
        {
            public string Tag;
            public string StoreId;
            public string UnitId;
            public string WarehouseId;
            public string AreaId;
            public string AreaName;
            public string Table1Id;
            public string Table1Name;
            public string Table2Id;
            public string Table3Id;
            public string GroupFoodId;
            public string GroupDrinkId;
            public string GroupServiceId;
            public string ProdFoodId;
            public string ProdDrinkId;
            public string ProdComboId;
            public string ProdComboChildId;
            public string ProdServiceId;
            public string StockOrderId;
            public string CustomerId;
            public string CustomerName;
            public string EmployeeId;
            public string EmployeeName;
            public string BookingId;
            public string DepositReceiptId;
            public readonly List<string> CreatedBillIds = new List<string>();
            public readonly List<string> CreatedConfigIds = new List<string>();
            public readonly List<string> CreatedReceiptIds = new List<string>();
        }

        private sealed class CapturedPrintCall
        {
            public string Kind;
            public string TemplateName;
            public string PrinterName;
            public int PageCount;
            public List<KeyValuePair<string, string>> PageTexts;
        }

        private sealed class ServicePrintOutputRecorder : IPrintOutputHandler
        {
            public readonly List<CapturedPrintCall> PrintCalls = new List<CapturedPrintCall>();
            public readonly List<CapturedPrintCall> PreviewCalls = new List<CapturedPrintCall>();
            public readonly List<string> ErrorMessages = new List<string>();
            public readonly List<string> InfoMessages = new List<string>();
            public bool ThrowOnPrint { get; set; }
            public string ThrowMessage { get; set; }

            public void ShowPreview(IWin32Window owner, Report preparedReport, PrintTemplateRecord template, string title)
            {
                PreviewCalls.Add(new CapturedPrintCall
                {
                    Kind = "Preview",
                    TemplateName = template != null ? template.Name : "",
                    PrinterName = "",
                    PageCount = preparedReport != null && preparedReport.PreparedPages != null ? preparedReport.PreparedPages.Count : 0,
                    PageTexts = preparedReport != null ? FastReportEngine.ExtractPreparedPageTexts(preparedReport) : new List<KeyValuePair<string, string>>()
                });
            }

            public void PrintToPrinter(Report preparedReport, PrintTemplateRecord template, string printerName)
            {
                if (ThrowOnPrint)
                    throw new InvalidOperationException(ThrowMessage ?? "Giả lập lỗi máy in không kết nối.");
                PrintCalls.Add(new CapturedPrintCall
                {
                    Kind = "Print",
                    TemplateName = template != null ? template.Name : "",
                    PrinterName = printerName ?? "",
                    PageCount = preparedReport != null && preparedReport.PreparedPages != null ? preparedReport.PreparedPages.Count : 0,
                    PageTexts = preparedReport != null ? FastReportEngine.ExtractPreparedPageTexts(preparedReport) : new List<KeyValuePair<string, string>>()
                });
            }

            public string PromptSaveFile(IWin32Window owner, PrintActionKind exportKind, string defaultFileName) { return null; }
            public void ExportExcel(Report preparedReport, PrintTemplateRecord template, string filePath) { }
            public void ExportPdf(Report preparedReport, PrintTemplateRecord template, string filePath) { }
            public bool ConfirmOpenFile(IWin32Window owner, string filePath) { return false; }
            public void OpenFile(string filePath) { }
            public void ShowError(IWin32Window owner, string message) { ErrorMessages.Add(SingleLineDetail(message)); }
            public void ShowInfo(IWin32Window owner, string message) { InfoMessages.Add(SingleLineDetail(message)); }
        }

        private sealed class MemoryKitchenPrinterStore : IKitchenPrinterStore
        {
            private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> _installed = new List<string>();

            public MemoryKitchenPrinterStore(params string[] installedPrinters)
            {
                if (installedPrinters != null)
                    _installed.AddRange(installedPrinters);
            }

            public string GetConfiguredPrinter(string key)
            {
                string val;
                return _map.TryGetValue(key ?? "", out val) ? val : "";
            }

            public void SetConfiguredPrinter(string key, string printerName)
            {
                if (!string.IsNullOrEmpty(key))
                    _map[key] = printerName ?? "";
            }

            public List<string> GetInstalledPrinters()
            {
                return new List<string>(_installed);
            }
        }

        private static void ServiceOrderPrintTests(string db)
        {
            ServicePrintPureRuleTests();

            var cfgRepo = new ConfigRepository(db);
            int oldMaxTemp = SystemConfig.SoLanInTamTinhToiDa;
            bool oldPreviewBeforePrint = SystemConfig.HienThiTruocKhiIn;
            bool oldChooseTpl = SystemConfig.LuaChonMauKhiIn;
            int oldPrintCopies = SystemConfig.SoLanIn;
            string oldMauHoaDon = SystemConfig.MauHoaDon;
            int oldAreaInvoiceMode = SystemConfig.InHoaDonTheoKhuVuc;
            bool oldConsolidated = SystemConfig.InHoaDonCongGopMatHang;
            bool oldShowPoints = SystemConfig.HienThiDiemCuaKhachHangTrenHoaDon;
            bool oldUseKitchen = SystemConfig.SuDungChucNangInXuongBep;
            bool oldInDoAn = SystemConfig.InDoAn;
            bool oldInDoUong = SystemConfig.InDoUong;
            bool oldInDichVu = SystemConfig.InDichVu;
            bool oldInDoKhac = SystemConfig.InDoKhac;
            bool oldSplitKitchen = SystemConfig.InRiengDoAnUong;
            bool oldKitchenByArea = SystemConfig.InPhaCheTheoKhuVuc;
            bool oldKitchenPrice = SystemConfig.InDonGiaTrenMauInBep;
            int oldKitchenCopies = SystemConfig.SoLienInCheBien;
            bool oldOneItemSlip = SystemConfig.InMoiDoRa1To;
            bool oldExtraCounter = SystemConfig.InThem1LienTaiQuay;
            bool oldBlockReduceKitchen = SystemConfig.KhongDuocGiamDoSauKhiInPhaChe;
            bool oldAllowAfterTemp = SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh;
            bool oldAllowDebt = SystemConfig.ChoPhepKhachNo;
            bool oldFee = SystemConfig.CoPhiDichVu;
            bool oldTax = SystemConfig.CoThueSuat;

            var admin = new UserSession
            {
                UserId = "selftest-print-1",
                UserName = "print_admin",
                DisplayName = "Thu Ngan In",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };

            ServicePrintFixture fx = SeedServicePrintFixture(db);
            try
            {
                ServicePrintInvoiceTests(db, cfgRepo, admin, fx);
                ServicePrintKitchenTests(db, cfgRepo, admin, fx);
                ServicePrintUiTests(db, cfgRepo, admin, fx);
            }
            finally
            {
                CleanupServicePrintFixture(db, fx);
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = "SoLanInTamTinhToiDa", DataType = 3, IntValue = oldMaxTemp },
                    new ConfigChange { Name = "HienThiTruocKhiIn", DataType = 3, IntValue = oldPreviewBeforePrint ? 30 : 0 },
                    new ConfigChange { Name = "LuaChonMauKhiIn", DataType = 3, IntValue = oldChooseTpl ? 30 : 0 },
                    new ConfigChange { Name = "SoLanIn", DataType = 3, IntValue = oldPrintCopies },
                    new ConfigChange { Name = "MauHoaDon", DataType = 1, TextValue = oldMauHoaDon ?? "" },
                    new ConfigChange { Name = "InHoaDonTheoKhuVuc", DataType = 3, IntValue = oldAreaInvoiceMode },
                    new ConfigChange { Name = "InHoaDonCongGopMatHang", DataType = 3, IntValue = oldConsolidated ? 30 : 0 },
                    new ConfigChange { Name = "HienThiDiemCuaKhachHangTrenHoaDon", DataType = 3, IntValue = oldShowPoints ? 30 : 0 },
                    new ConfigChange { Name = "SuDungChucNangInXuongBep", DataType = 3, IntValue = oldUseKitchen ? 30 : 0 },
                    new ConfigChange { Name = "InDoAn", DataType = 3, IntValue = oldInDoAn ? 30 : 0 },
                    new ConfigChange { Name = "InDoUong", DataType = 3, IntValue = oldInDoUong ? 30 : 0 },
                    new ConfigChange { Name = "InDichVu", DataType = 3, IntValue = oldInDichVu ? 30 : 0 },
                    new ConfigChange { Name = "InDoKhac", DataType = 3, IntValue = oldInDoKhac ? 30 : 0 },
                    new ConfigChange { Name = "InRiengDoAnUong", DataType = 3, IntValue = oldSplitKitchen ? 30 : 0 },
                    new ConfigChange { Name = "InPhaCheTheoKhuVuc", DataType = 3, IntValue = oldKitchenByArea ? 30 : 0 },
                    new ConfigChange { Name = "InDonGiaTrenMauInBep", DataType = 3, IntValue = oldKitchenPrice ? 30 : 0 },
                    new ConfigChange { Name = "SoLienInCheBien", DataType = 3, IntValue = oldKitchenCopies },
                    new ConfigChange { Name = "InMoiDoRa1To", DataType = 3, IntValue = oldOneItemSlip ? 30 : 0 },
                    new ConfigChange { Name = "InThem1LienTaiQuay", DataType = 3, IntValue = oldExtraCounter ? 30 : 0 },
                    new ConfigChange { Name = "KhongDuocGiamDoSauKhiInPhaChe", DataType = 3, IntValue = oldBlockReduceKitchen ? 30 : 0 },
                    new ConfigChange { Name = "ChoPhepThayDoiHoaDonSauKhiInTamTinh", DataType = 3, IntValue = oldAllowAfterTemp ? 30 : 0 },
                    new ConfigChange { Name = "ChoPhepKhachNo", DataType = 3, IntValue = oldAllowDebt ? 30 : 0 },
                    new ConfigChange { Name = "CoPhiDichVu", DataType = 3, IntValue = oldFee ? 30 : 0 },
                    new ConfigChange { Name = "CoThueSuat", DataType = 3, IntValue = oldTax ? 30 : 0 }
                }, "selftest");
                SystemConfig.Reload(db);
            }
        }

        private static void ServicePrintPureRuleTests()
        {
            // 1. KitchenPrintRules.ComputePendingItems: chênh lệch dương/âm, ngưỡng |100*SUM| > 1.
            var billLines = new List<KitchenRawLine>
            {
                new KitchenRawLine { ItemName = "Phở", UnitId = "U1", UnitName = "Tô", DrinkTypeId = "0", Quantity = 3m, UnitPrice = 50000m, Note = "Ít hành" },
                new KitchenRawLine { ItemName = "Trà", UnitId = "U2", UnitName = "Ly", DrinkTypeId = "1", Quantity = 2m, UnitPrice = 20000m, Note = "" },
                new KitchenRawLine { ItemName = "Khăn", UnitId = "U2", UnitName = "Cái", DrinkTypeId = "1", Quantity = 1.005m, UnitPrice = 5000m, Note = "" }
            };
            var printed = new List<KitchenRawLine>
            {
                new KitchenRawLine { ItemName = "Phở", UnitId = "U1", UnitName = "Tô", DrinkTypeId = "0", Quantity = 1m, UnitPrice = 50000m, Note = "Ít hành" },
                new KitchenRawLine { ItemName = "Trà", UnitId = "U2", UnitName = "Ly", DrinkTypeId = "1", Quantity = 3m, UnitPrice = 20000m, Note = "" },
                new KitchenRawLine { ItemName = "Khăn", UnitId = "U2", UnitName = "Cái", DrinkTypeId = "1", Quantity = 1.00m, UnitPrice = 5000m, Note = "" }
            };
            List<KitchenPendingItem> diff = KitchenPrintRules.ComputePendingItems(billLines, printed, true);
            bool diffOk = diff.Count == 2
                && diff[0].ItemName == "Phở" && diff[0].Quantity == 2m
                && diff[1].ItemName == "Trà" && diff[1].Quantity == -1m;
            Check("SDDV In §5 KitchenPrintRules.ComputePendingItems: tính chênh lệch dương/âm và bỏ sai số <= 0.01",
                diffOk, "count=" + diff.Count);

            // 2. BuildBatches: SeparateFoodAndDrink=true tách 2 phiếu; =false cùng máy in gộp 1 phiếu.
            var settingsSplit = new KitchenPrintSettings { Enabled = true, PrintFood = true, PrintDrink = true, SeparateFoodAndDrink = true };
            var store = new MemoryKitchenPrinterStore("BEP-PRN", "BAR-PRN");
            store.SetConfiguredPrinter(ServicePrintConstants.KeyPrinterFood, "BEP-PRN");
            store.SetConfiguredPrinter(ServicePrintConstants.KeyPrinterDrink, "BAR-PRN");
            bool missingSplit;
            List<KitchenPrintBatch> splitBatches = KitchenPrintRules.BuildBatches(settingsSplit, "", diff, store, out missingSplit);

            var settingsMerge = new KitchenPrintSettings { Enabled = true, PrintFood = true, PrintDrink = true, SeparateFoodAndDrink = false };
            store.SetConfiguredPrinter(ServicePrintConstants.KeyPrinterDrink, "BEP-PRN");
            bool missingMerge;
            List<KitchenPrintBatch> mergeBatches = KitchenPrintRules.BuildBatches(settingsMerge, "", diff, store, out missingMerge);
            Check("SDDV In §5 KitchenPrintRules.BuildBatches: tách riêng Đồ ăn/Đồ uống và gộp chung khi cùng máy in",
                !missingSplit && splitBatches.Count == 2 && !missingMerge && mergeBatches.Count == 1 && mergeBatches[0].Items.Count == 2,
                "split=" + splitBatches.Count + ", merge=" + mergeBatches.Count);

            // 3. ResolveInvoicePrinter cho đủ 4 kiểu InHoaDonTheoKhuVuc.
            string second0, second1, second2, second3, secondReprint;
            string p0 = KitchenPrintRules.ResolveInvoicePrinter(InvoiceAreaPrintMode.AtCounter, false, false, "QUAY", "KV-A", out second0);
            string p1 = KitchenPrintRules.ResolveInvoicePrinter(InvoiceAreaPrintMode.ByArea, false, false, "QUAY", "KV-A", out second1);
            string p2 = KitchenPrintRules.ResolveInvoicePrinter(InvoiceAreaPrintMode.TempByAreaBillAtCounter, true, false, "QUAY", "KV-A", out second2);
            string p3 = KitchenPrintRules.ResolveInvoicePrinter(InvoiceAreaPrintMode.OneAtCounterOneByArea, false, false, "QUAY", "KV-A", out second3);
            string pRep = KitchenPrintRules.ResolveInvoicePrinter(InvoiceAreaPrintMode.OneAtCounterOneByArea, false, true, "QUAY", "KV-A", out secondReprint);
            Check("SDDV In §5 KitchenPrintRules.ResolveInvoicePrinter: định tuyến máy in hóa đơn theo khu vực đúng cả 4 chế độ",
                p0 == "QUAY" && second0 == null
                && p1 == "KV-A" && second1 == null
                && p2 == "KV-A" && second2 == null
                && p3 == "QUAY" && second3 == "KV-A"
                && pRep == "QUAY" && secondReprint == null,
                "p1=" + p1 + ", p3=" + p3 + "+" + second3);
        }

        private static void SetPrintConfigInt(string db, ConfigRepository cfgRepo, ServicePrintFixture fx, string key, int intValue)
        {
            int updated = cfgRepo.Save(new[] { new ConfigChange { Name = key, DataType = 3, IntValue = intValue } }, "selftest");
            if (updated == 0)
            {
                string id = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO SCONFIG (ID, NAME, DATATYPE, INTVALUE, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 3, @V, 30, 'selftest', @T)",
                    "@I", id, "@N", key, "@V", intValue, "@T", DbHelper.Now());
                fx.CreatedConfigIds.Add(id);
            }
            SystemConfig.Reload(db);
        }

        private static void SetPrintConfigStr(string db, ConfigRepository cfgRepo, ServicePrintFixture fx, string key, string strValue)
        {
            int updated = cfgRepo.Save(new[] { new ConfigChange { Name = key, DataType = 1, TextValue = strValue ?? "" } }, "selftest");
            if (updated == 0)
            {
                string id = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO SCONFIG (ID, NAME, DATATYPE, TEXTVALUE, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 1, @V, 30, 'selftest', @T)",
                    "@I", id, "@N", key, "@V", strValue ?? "", "@T", DbHelper.Now());
                fx.CreatedConfigIds.Add(id);
            }
            SystemConfig.Reload(db);
        }

        private static ServicePrintFixture SeedServicePrintFixture(string db)
        {
            var fx = new ServicePrintFixture();
            fx.Tag = DbHelper.NewId().Substring(0, 6);
            DateTime now = new InvoiceNumberRepository(db).GetServerTimestamp();

            DbHelper.InTransaction(db, (conn, tx) =>
            {
                fx.StoreId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DCUAHANG WHERE STATUS = 30 ORDER BY NAME"));
                fx.UnitId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DDONVITINH WHERE STATUS = 30 ORDER BY NAME"));

                fx.WarehouseId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHOHANG (ID, NAME, STATUS, DCUAHANGID, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, @S, 'selftest', @T)",
                    "@I", fx.WarehouseId, "@N", "KHO PRT " + fx.Tag, "@S", fx.StoreId, "@T", now);

                fx.AreaId = DbHelper.NewId();
                fx.AreaName = "Khu In " + fx.Tag;
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZZ9', 'selftest', @T)",
                    "@I", fx.AreaId, "@N", fx.AreaName, "@W", fx.WarehouseId, "@T", now);

                fx.Table1Name = "Ban In 01 " + fx.Tag;
                fx.Table1Id = InsertTestTable(conn, tx, fx.Table1Name, fx.AreaId, 0, now);
                fx.Table2Id = InsertTestTable(conn, tx, "Ban In 02 " + fx.Tag, fx.AreaId, 0, now);
                fx.Table3Id = InsertTestTable(conn, tx, "Ban In 03 " + fx.Tag, fx.AreaId, 0, now);

                fx.GroupFoodId = DbHelper.NewId();
                fx.GroupDrinkId = DbHelper.NewId();
                fx.GroupServiceId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, '', '0', '', '', 'ZZZ1', 30, 'selftest', @T)",
                    "@I", fx.GroupFoodId, "@N", "Nhom An PRT " + fx.Tag, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, '', '1', '', '', 'ZZZ2', 30, 'selftest', @T)",
                    "@I", fx.GroupDrinkId, "@N", "Nhom Uong PRT " + fx.Tag, "@T", now);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHOMMATHANG (ID, NAME, CODE, DLOAIDOID, PARENTID, PARENTDIR, SORTORDER, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, '', '2', '', '', 'ZZZ3', 30, 'selftest', @T)",
                    "@I", fx.GroupServiceId, "@N", "Nhom DV PRT " + fx.Tag, "@T", now);

                fx.ProdFoodId = InsertTestProduct(conn, tx, "Pho Bo PRT " + fx.Tag, fx.GroupFoodId, fx.UnitId, "0", 120000m, 120000m, 0, now);
                fx.ProdDrinkId = InsertTestProduct(conn, tx, "Tra Dao PRT " + fx.Tag, fx.GroupDrinkId, fx.UnitId, "0", 40000m, 40000m, 0, now);
                fx.ProdServiceId = InsertTestProduct(conn, tx, "Khan Lanh PRT " + fx.Tag, fx.GroupServiceId, fx.UnitId, "0", 10000m, 10000m, 0, now);
                fx.ProdComboChildId = InsertTestProduct(conn, tx, "Banh Cuon Con " + fx.Tag, fx.GroupFoodId, fx.UnitId, "0", 25000m, 25000m, 0, now);
                fx.ProdComboId = InsertTestProduct(conn, tx, "Combo Trua " + fx.Tag, fx.GroupFoodId, fx.UnitId, "4", 150000m, 150000m, 0, now);

                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDINHLUONG (ID, STATUS, USERCREATEDID, TIMECREATED, DMATHANGID, SOLUONG, DVATTUID) VALUES (@I, 30, 'selftest', @T, @P, 2, @M)",
                    "@I", DbHelper.NewId(), "@T", now, "@P", fx.ProdComboId, "@M", fx.ProdComboChildId);

                fx.StockOrderId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DCUAHANGID, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 97, 30, 0, @D, @W, @S, 'selftest', @T)",
                    "@I", fx.StockOrderId, "@N", "TONDAU-PRT-" + fx.Tag, "@D", now.Date, "@W", fx.WarehouseId, "@S", fx.StoreId, "@T", now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdFoodId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdDrinkId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdServiceId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdComboChildId, fx.UnitId, fx.WarehouseId, 200m, now);

                fx.CustomerId = DbHelper.NewId();
                fx.CustomerName = "Khach Hang In " + fx.Tag;
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIEMTICHLUYBANDAU, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @M, @N, '99 Nguyen Hue', 120, 0, 30, 'selftest', @T)",
                    "@I", fx.CustomerId, "@M", "KHPRT-" + fx.Tag, "@N", fx.CustomerName, "@T", now);

                fx.EmployeeId = DbHelper.NewId();
                fx.EmployeeName = "NV Phuc Vu " + fx.Tag;
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DNHANVIEN (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest', @T)",
                    "@I", fx.EmployeeId, "@N", fx.EmployeeName, "@T", now);

                fx.BookingId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, TUGIO, DENGIO, GIODAT, DBANID, DKHACHHANGID, TENKHACH, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @G, @N, @D, @D, @D, @TG, @DG, @B, @T, @K, @KN, 30, 'selftest', @B)",
                    "@I", fx.BookingId, "@G", DbHelper.NewId(), "@N", "DH-PRT-" + fx.Tag, "@D", now.Date,
                    "@TG", now.AddMinutes(-10), "@DG", now.AddMinutes(90), "@B", now, "@T", fx.Table1Id, "@K", fx.CustomerId, "@KN", fx.CustomerName);

                fx.DepositReceiptId = DbHelper.NewId();
                fx.CreatedReceiptIds.Add(fx.DepositReceiptId);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, THU, CHI, CHUYENKHOAN, KHONGTHAYDOICONGNO, TDATHANGID, DKHACHHANGID, DCUAHANGID, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @D, 0, 100000, 0, 0, 30, @B, @K, @S, 30, 'selftest', @T)",
                    "@I", fx.DepositReceiptId, "@N", "PT-COC-PRT-" + fx.Tag, "@D", now.Date, "@B", fx.BookingId, "@K", fx.CustomerId, "@S", fx.StoreId, "@T", now);
            });

            return fx;
        }

        private static void CleanupServicePrintFixture(string db, ServicePrintFixture fx)
        {
            if (fx == null)
                return;
            for (int i = 0; i < fx.CreatedBillIds.Count; i++)
            {
                string id = fx.CreatedBillIds[i];
                try { BookingExec(db, "DELETE FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", id); } catch { }
                try { BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", id); } catch { }
                try { BookingExec(db, "DELETE FROM TTHUCHI WHERE TDONHANGID = @I", "@I", id); } catch { }
                try { BookingExec(db, "UPDATE DBAN SET TDONHANGID = NULL, USEREDITID = NULL WHERE TDONHANGID = @I", "@I", id); } catch { }
                try { BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", id); } catch { }
            }
            for (int i = 0; i < fx.CreatedReceiptIds.Count; i++)
            {
                try { BookingExec(db, "DELETE FROM TTHUCHI WHERE ID = @I", "@I", fx.CreatedReceiptIds[i]); } catch { }
            }
            for (int i = 0; i < fx.CreatedConfigIds.Count; i++)
            {
                try { BookingExec(db, "DELETE FROM SCONFIG WHERE ID = @I", "@I", fx.CreatedConfigIds[i]); } catch { }
            }
            try { BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", fx.StockOrderId); } catch { }
            try { BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", fx.StockOrderId); } catch { }
            try { BookingExec(db, "DELETE FROM TDATHANG WHERE ID = @I", "@I", fx.BookingId); } catch { }
            try { BookingExec(db, "DELETE FROM DDINHLUONG WHERE DMATHANGID = @I", "@I", fx.ProdComboId); } catch { }
            try { BookingExec(db, "DELETE FROM DBAN WHERE ID IN (@A, @B, @C)", "@A", fx.Table1Id, "@B", fx.Table2Id, "@C", fx.Table3Id); } catch { }
            try { BookingExec(db, "DELETE FROM DKHUVUC WHERE ID = @I", "@I", fx.AreaId); } catch { }
            try { BookingExec(db, "DELETE FROM DKHOHANG WHERE ID = @I", "@I", fx.WarehouseId); } catch { }
            try { BookingExec(db, "DELETE FROM DMATHANG WHERE ID IN (@A, @B, @C, @D, @E)", "@A", fx.ProdFoodId, "@B", fx.ProdDrinkId, "@C", fx.ProdServiceId, "@D", fx.ProdComboId, "@E", fx.ProdComboChildId); } catch { }
            try { BookingExec(db, "DELETE FROM DNHOMMATHANG WHERE ID IN (@A, @B, @C)", "@A", fx.GroupFoodId, "@B", fx.GroupDrinkId, "@C", fx.GroupServiceId); } catch { }
            try { BookingExec(db, "DELETE FROM DKHACHHANG WHERE ID = @I", "@I", fx.CustomerId); } catch { }
            try { BookingExec(db, "DELETE FROM DNHANVIEN WHERE ID = @I", "@I", fx.EmployeeId); } catch { }
        }
    }
}
