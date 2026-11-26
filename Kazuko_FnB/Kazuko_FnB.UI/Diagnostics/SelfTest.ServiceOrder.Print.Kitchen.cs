using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void ServicePrintKitchenTests(string db, ConfigRepository cfgRepo, UserSession admin, ServicePrintFixture fx)
        {
            var svc = new TableService(admin);
            var coord = new KitchenPrintCoordinator();

            // Mở bill trên Table2 với 2 Phở (Đồ ăn), 3 Trà Đào (Đồ uống), 1 Khăn lạnh (Dịch vụ).
            ServiceOpResult open = svc.OpenTable(fx.Table2Id, true, null);
            fx.CreatedBillIds.Add(open.Bill.Id);
            svc.AddItem(open.Bill.Id, fx.Table2Id, fx.ProdFoodId, 2m, 120000m);
            svc.AddItem(open.Bill.Id, fx.Table2Id, fx.ProdDrinkId, 3m, 40000m);
            svc.AddItem(open.Bill.Id, fx.Table2Id, fx.ProdServiceId, 1m, 10000m);

            var store = new MemoryKitchenPrinterStore("MAY-IN-BEP", "MAY-IN-BAR");

            // 1. Tắt SuDungChucNangInXuongBep -> chặn rõ ràng.
            SetPrintConfigInt(db, cfgRepo, fx, "SuDungChucNangInXuongBep", 0);
            KitchenPrintPlan disabledPlan = svc.BuildKitchenPrintPlan(open.Bill.Id, store);
            Check("SDDV In Bếp §1 Tắt SuDungChucNangInXuongBep chặn in với thông báo chuẩn",
                !disabledPlan.Ok && disabledPlan.ErrorMessage == ServicePrintConstants.KitchenFeatureDisabled,
                SingleLineDetail(disabledPlan.ErrorMessage));

            // 2. Bật In bếp (Đồ ăn + Đồ uống, không in Dịch vụ), chưa thiết lập máy in -> NeedsPrinterSetup = true.
            SetPrintConfigInt(db, cfgRepo, fx, "SuDungChucNangInXuongBep", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "InDoAn", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "InDoUong", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "InDichVu", 0);
            SetPrintConfigInt(db, cfgRepo, fx, "InDoKhac", 0);
            SetPrintConfigInt(db, cfgRepo, fx, "InRiengDoAnUong", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "InPhaCheTheoKhuVuc", 0);
            SetPrintConfigInt(db, cfgRepo, fx, "InDonGiaTrenMauInBep", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "SoLienInCheBien", 1);
            SetPrintConfigInt(db, cfgRepo, fx, "InMoiDoRa1To", 0);
            SetPrintConfigInt(db, cfgRepo, fx, "InThem1LienTaiQuay", 0);

            KitchenPrintPlan noPrnPlan = svc.BuildKitchenPrintPlan(open.Bill.Id, store);
            Check("SDDV In Bếp §2 Chưa thiết lập máy in chế biến trả NeedsPrinterSetup = true",
                !noPrnPlan.Ok && noPrnPlan.NeedsPrinterSetup,
                "setup=" + noPrnPlan.NeedsPrinterSetup + ", err=" + SingleLineDetail(noPrnPlan.ErrorMessage));

            // 3. Thiết lập máy in Bếp + Bar, kiểm in lỗi KHÔNG ghi TINCHEBIEN.
            store.SetConfiguredPrinter(ServicePrintConstants.KeyPrinterFood, "MAY-IN-BEP");
            store.SetConfiguredPrinter(ServicePrintConstants.KeyPrinterDrink, "MAY-IN-BAR");
            KitchenPrintPlan readyPlan = svc.BuildKitchenPrintPlan(open.Bill.Id, store);

            var failRec = new ServicePrintOutputRecorder { ThrowOnPrint = true, ThrowMessage = "Hết giấy bếp" };
            bool failOk = coord.ExecuteBatches(null, null, svc, db, admin, readyPlan, false, failRec, null, null);
            int printedAfterFail = CountKitchenRows(db, open.Bill.Id);
            Check("SDDV In Bếp §3 Lỗi máy in bếp không ghi TINCHEBIEN để tránh mất món chưa in",
                readyPlan.Ok && readyPlan.Batches.Count == 2 && !failOk && failRec.ErrorMessages.Count >= 1 && printedAfterFail == 0,
                "batches=" + readyPlan.Batches.Count + ", rows=" + printedAfterFail);

            // 4. In bếp thành công: 2 phiếu (Đồ ăn -> MAY-IN-BEP, Đồ uống -> MAY-IN-BAR), kiểm nội dung trang in & bản ghi TINCHEBIEN.
            var okRec = new ServicePrintOutputRecorder();
            bool execOk = coord.ExecuteBatches(null, null, svc, db, admin, readyPlan, false, okRec, null, null);
            int printedAfterOk = CountKitchenRows(db, open.Bill.Id);
            bool slipContentOk = okRec.PrintCalls.Count == 2
                && okRec.PrintCalls[0].PrinterName == "MAY-IN-BEP"
                && okRec.PrintCalls[1].PrinterName == "MAY-IN-BAR"
                && PageTextsContain(okRec.PrintCalls[0].PageTexts, "Pho Bo PRT " + fx.Tag)
                && PageTextsContain(okRec.PrintCalls[0].PageTexts, fx.AreaName)
                && PageTextsContain(okRec.PrintCalls[0].PageTexts, admin.DisplayName)
                && PageTextsContain(okRec.PrintCalls[1].PageTexts, "Tra Dao PRT " + fx.Tag);

            // Lần gọi thứ 2 khi không thêm món -> NoItemsToPrint = true.
            KitchenPrintPlan secondPlan = svc.BuildKitchenPrintPlan(open.Bill.Id, store);
            bool noMoreMsgOk = secondPlan.ErrorMessage == ServicePrintConstants.KitchenNoRemainingItem
                || secondPlan.ErrorMessage == ServicePrintConstants.KitchenNoItemToPrint;
            Check("SDDV In Bếp §4 In bếp tách Đồ ăn/Đồ uống thành công, đúng nội dung phiếu, ghi TINCHEBIEN và lần sau báo không còn món",
                execOk && slipContentOk && printedAfterOk == 2
                && !secondPlan.Ok && secondPlan.NoItemsToPrint && noMoreMsgOk,
                "calls=" + okRec.PrintCalls.Count + ", rows=" + printedAfterOk + ", msg2=" + SingleLineDetail(secondPlan.ErrorMessage) + ", err=" + SingleLineDetail(string.Join(" | ", okRec.ErrorMessages.ToArray())) + ", ok=" + execOk);

            // 5. Thêm 2 Phở mới & giảm 1 Trà (khi KhongDuocGiamDoSauKhiInPhaChe=30 bị chặn; =0 cho giảm và in phiếu bổ sung chênh lệch +2 / -1).
            svc.AddItem(open.Bill.Id, fx.Table2Id, fx.ProdFoodId, 2m, 120000m);
            ServiceOrderHeader loadedBill = svc.LoadBill(open.Bill.Id);
            ServiceOrderLine drinkLine = loadedBill.Lines.Find(l => l.ProductId == fx.ProdDrinkId);

            SetPrintConfigInt(db, cfgRepo, fx, "KhongDuocGiamDoSauKhiInPhaChe", 30);
            ServiceOpResult blockedReduce = svc.ReduceLineQty(open.Bill.Id, fx.Table2Id, drinkLine.Id, 1m, false, null);

            SetPrintConfigInt(db, cfgRepo, fx, "KhongDuocGiamDoSauKhiInPhaChe", 0);
            ServiceOpResult allowedReduce = svc.ReduceLineQty(open.Bill.Id, fx.Table2Id, drinkLine.Id, 1m, false, null);

            // Bật InMoiDoRa1To + InThem1LienTaiQuay để kiểm số lệnh in & chênh lệch dương/âm.
            SetPrintConfigInt(db, cfgRepo, fx, "InMoiDoRa1To", 30);
            SetPrintConfigInt(db, cfgRepo, fx, "InThem1LienTaiQuay", 30);
            KitchenPrintPlan deltaPlan = svc.BuildKitchenPrintPlan(open.Bill.Id, store);
            var deltaRec = new ServicePrintOutputRecorder();
            bool deltaOk = coord.ExecuteBatches(null, null, svc, db, admin, deltaPlan, false, deltaRec, null, null);
            int maxSeq = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COALESCE(MAX(LANSO), 0) FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", open.Bill.Id)));
            KitchenPrintPlan afterDeltaPlan = svc.BuildKitchenPrintPlan(open.Bill.Id, store);

            Check("SDDV In Bếp §5 KhongDuocGiamDoSauKhiInPhaChe chặn giảm món đã in; in bổ sung đúng delta (+2 Phở, -1 Trà), InMoiDoRa1To + InThem1LienTaiQuay và tăng LANSO",
                !blockedReduce.Ok && allowedReduce.Ok
                && deltaPlan.Ok && deltaPlan.Batches.Count == 2
                && deltaOk && deltaRec.PrintCalls.Count == 4 && maxSeq >= 2
                && afterDeltaPlan.NoItemsToPrint,
                "blk=" + !blockedReduce.Ok + ", alw=" + allowedReduce.Ok + ", calls=" + deltaRec.PrintCalls.Count + ", seq=" + maxSeq);

            // 6. Đóng bill -> xóa TINCHEBIEN và chặn in chế biến hóa đơn đã thanh toán.
            PaymentExecutionResult paid = svc.ExecutePayment(new PaymentRequest
            {
                PrimaryBillId = open.Bill.Id,
                PrimaryTableId = fx.Table2Id,
                CashTendered = 1000000m,
                PrintAfterClose = false
            });
            int rowsAfterPay = CountKitchenRows(db, open.Bill.Id);
            KitchenPrintPlan paidKitchenPlan = svc.BuildKitchenPrintPlan(open.Bill.Id, store);
            Check("SDDV In Bếp §6 Sau thanh toán xóa sạch TINCHEBIEN và chặn in bếp với thông báo hóa đơn đã thanh toán",
                paid.Ok && rowsAfterPay == 0 && !paidKitchenPlan.Ok
                && paidKitchenPlan.ErrorMessage == ServicePrintConstants.KitchenBillAlreadyPaid,
                "rows=" + rowsAfterPay + ", msg=" + SingleLineDetail(paidKitchenPlan.ErrorMessage));
        }

        private static int CountKitchenRows(string db, string billId)
        {
            return DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TINCHEBIEN WHERE TDONHANGID = @I AND COALESCE(STATUS, 30) <> -30",
                "@I", billId)));
        }
    }
}
