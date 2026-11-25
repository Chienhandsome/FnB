using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void DailyActivityRepositoryTests(string db, DailyActivityFixture fx)
        {
            var repo = new DailyActivityRepository(db);

            // Admin không chọn cửa hàng / thu ngân: thấy tất cả (gồm phiếu nhập DKHONHAPID NULL, STATUS=-30, nguyên liệu, giờ lẻ ngày cuối).
            DailyActivityReportData adminAll = repo.LoadReport(new DailyActivityFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = "", CashierId = "", UserId = "selftest", IsAdmin = true
            });
            DailyActivitySummaryResult adminSum = DailyActivitySummary.Calculate(adminAll);

            Check("Chi tiết HĐ ngày Repo (§7 Khối 1 Admin): đúng 3 bill, sắp NGAY+NAME (HD00→HD01→HD02), loại chưa TT/STATUS<>30/ngoài khoảng",
                adminAll.SaleBills.Count == 3
                && adminAll.SaleBills[0].Id == fx.Bill3Id
                && adminAll.SaleBills[1].Id == fx.Bill1Id
                && adminAll.SaleBills[2].Id == fx.Bill2Id
                && adminAll.SaleBills[1].CustomerName == "CTHD Khách A"
                && adminAll.SaleBills[2].CustomerName == "",
                "bills=" + adminAll.SaleBills.Count);

            // Khối 2: kiểm tách riêng theo TỪNG khóa GROUP BY (TENHANG, CODE, DDONVITINHID cùng tên khác ID, DONGIA, TILEGIAMGIA),
            // gộp trùng khóa, trừ hàng trả, giữ nguyên liệu, loại combo con/XVT, GiamGiaTongBill riêng.
            DailySoldItemRow grpBase = FindSoldItem(adminAll, "CTHD-Cà phê", "CTHD-CF", fx.UnitId, 40000m, 10m);
            DailySoldItemRow grpDiffUnitId = FindSoldItem(adminAll, "CTHD-Cà phê", "CTHD-CF", fx.Unit2Id, 40000m, 10m);
            DailySoldItemRow grpDiffDisc = FindSoldItem(adminAll, "CTHD-Cà phê", "CTHD-CF", fx.UnitId, 40000m, 20m);
            DailySoldItemRow grpDiffCode = FindSoldItem(adminAll, "CTHD-Cà phê", "CTHD-CF2", fx.UnitId, 40000m, 10m);
            DailySoldItemRow grpDiffPrice = FindSoldItem(adminAll, "CTHD-Cà phê", "CTHD-CF", fx.UnitId, 50000m, 0m);
            DailySoldItemRow grpMaterial = FindSoldItem(adminAll, "CTHD-Mật ong NL", "CTHD-NL", fx.UnitId, 10000m, 0m);
            DailySoldItemRow grpTea = FindSoldItem(adminAll, "CTHD-Trà sữa", "CTHD-TR", fx.UnitId, 35000m, 0m);

            bool groupByKeysOk = adminAll.SoldItems.Count == 7
                && grpBase != null && grpBase.Quantity == 5m && grpBase.Amount == 180000m && grpBase.UnitName == "CTHD-Ly"
                && grpDiffUnitId != null && grpDiffUnitId.Quantity == 1m && grpDiffUnitId.Amount == 36000m && grpDiffUnitId.UnitName == "CTHD-Ly"
                && grpDiffDisc != null && grpDiffDisc.Quantity == 1m && grpDiffDisc.Amount == 32000m
                && grpDiffCode != null && grpDiffCode.Quantity == 1m && grpDiffCode.Amount == 36000m
                && grpDiffPrice != null && grpDiffPrice.Quantity == 1m && grpDiffPrice.Amount == 50000m
                && grpMaterial != null && grpMaterial.Quantity == 2m && grpMaterial.Amount == 20000m
                && grpTea != null && grpTea.Quantity == 2m && grpTea.Amount == 70000m;

            string[] expectedItemNames =
            {
                "CTHD-Cà phê", "CTHD-Cà phê", "CTHD-Cà phê", "CTHD-Cà phê", "CTHD-Cà phê",
                "CTHD-Mật ong NL",
                "CTHD-Trà sữa"
            };
            bool itemOrderOk = adminAll.SoldItems.Count == expectedItemNames.Length;
            for (int i = 0; itemOrderOk && i < expectedItemNames.Length; i++)
            {
                if (adminAll.SoldItems[i].ItemName != expectedItemNames[i])
                    itemOrderOk = false;
                if (i > 0 && string.CompareOrdinal(adminAll.SoldItems[i - 1].ItemName, adminAll.SoldItems[i].ItemName) > 0)
                    itemOrderOk = false;
            }

            Check("Chi tiết HĐ ngày Repo (§7 Khối 2 gộp đúng đủ 5 khóa tên/mã/ĐVT-ID/đơn giá/% giảm, sắp TENHANG không giảm, trừ hàng trả, giữ NL, loại combo/XVT, GiamGiaTongBill riêng)",
                groupByKeysOk
                && itemOrderOk
                && adminAll.BillTotalDiscount == 30000m
                && adminSum.ItemTotals.TotalQuantity == 13m
                && adminSum.ItemTotals.GrossAmount == 460000m
                && adminSum.ItemTotals.ItemDiscount == 36000m
                && adminSum.ItemTotals.NetTotal == 394000m,
                "items=" + adminAll.SoldItems.Count + ", ord=" + itemOrderOk + ", qty=" + adminSum.ItemTotals.TotalQuantity + ", net=" + adminSum.ItemTotals.NetTotal);

            Check("Chi tiết HĐ ngày Repo (§7 Khối 3 & Khối 4 & 8 dòng tổng hợp Admin): khớp số tính tay, có DKHONHAPID NULL & STATUS<>30 & tạm ứng",
                adminAll.Purchases.Count == 5
                && adminAll.Purchases.Exists(p => p.Id == fx.Pur5Id)
                && adminAll.Purchases.Exists(p => p.Id == fx.Pur2Id)
                && adminAll.CashFlows.Count == 4
                && adminSum.CashFlowTotals.CashIn == 85000m && adminSum.CashFlowTotals.CashOut == 22000m
                && adminSum.CashFlowTotals.TransferIn == 65000m && adminSum.CashFlowTotals.TransferOut == 19000m
                && adminSum.Overall.Cash == 355000m
                && adminSum.Overall.BankTransfer == 246000m
                && adminSum.Overall.Card == 113000m
                && adminSum.Overall.Voucher == 52000m
                && adminSum.Overall.PrepaidCard == 55000m
                && adminSum.Overall.LoyaltyDeduction == 30000m
                && adminSum.Overall.CustomerDebt == 37000m
                && adminSum.Overall.SupplierDebt == 172000m,
                "pur=" + adminAll.Purchases.Count + ", cf=" + adminAll.CashFlows.Count + ", cash=" + adminSum.Overall.Cash + ", ck=" + adminSum.Overall.BankTransfer);

            // Admin chọn StoreA: mất Pur5 (DKHONHAPID NULL) và dữ liệu StoreB.
            DailyActivityReportData storeAData = repo.LoadReport(new DailyActivityFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = fx.StoreAId, CashierId = "", UserId = "selftest", IsAdmin = true
            });
            Check("Chi tiết HĐ ngày Repo (§7/§3-5 lọc Store A): mất phiếu nhập DKHONHAPID NULL và dữ liệu Store B",
                storeAData.SaleBills.Count == 2
                && storeAData.Purchases.Count == 3
                && !storeAData.Purchases.Exists(p => p.Id == fx.Pur5Id)
                && !storeAData.Purchases.Exists(p => p.Id == fx.Pur3Id)
                && storeAData.CashFlows.Count == 3);

            // Lọc Thu ngân = Cashier1: bill lọc USERTHANHTOANID (Bill1, Bill3); nhập & thu-chi lọc USERCREATEDID (Pur2/3/4, Tc2/3).
            DailyActivityReportData tn1Data = repo.LoadReport(new DailyActivityFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = "", CashierId = fx.Cashier1Id, UserId = "selftest", IsAdmin = true
            });
            Check("Chi tiết HĐ ngày Repo (§7/P4 lọc Thu ngân): bill theo USERTHANHTOANID, thu-chi & nhập theo USERCREATEDID",
                tn1Data.SaleBills.Count == 2
                && tn1Data.SaleBills.Exists(b => b.Id == fx.Bill1Id) && tn1Data.SaleBills.Exists(b => b.Id == fx.Bill3Id)
                && !tn1Data.SaleBills.Exists(b => b.Id == fx.Bill2Id)
                && tn1Data.Purchases.Count == 3
                && !tn1Data.Purchases.Exists(p => p.Id == fx.Pur1Id)
                && tn1Data.CashFlows.Count == 2
                && tn1Data.BillTotalDiscount == 20000m);

            // User thường 1 cửa hàng (Cashier1, để trống cửa hàng): P2 chỉ thấy Store A, mất DKHONHAPID NULL và Store B.
            DailyActivityReportData user1Data = repo.LoadReport(new DailyActivityFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = "", CashierId = "", UserId = fx.Cashier1Id, IsAdmin = false
            });
            Check("Chi tiết HĐ ngày Repo (§7/P2 user thường 1 CH để trống cửa hàng): chỉ thấy Store A, mất Store B và DKHONHAPID NULL",
                user1Data.SaleBills.Count == 2 && user1Data.Purchases.Count == 3 && user1Data.CashFlows.Count == 3
                && !user1Data.Purchases.Exists(p => p.Id == fx.Pur5Id));

            // P2 khi user thường thuộc CH A truyền bộ lọc StoreId = CH B: cả 4 khối rỗng và BillTotalDiscount = 0;
            // đối chứng với Admin chọn CH B phải có đủ dữ liệu ở cả 4 khối.
            DailyActivityReportData adminStoreB = repo.LoadReport(new DailyActivityFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = fx.StoreBId, CashierId = "", UserId = "selftest", IsAdmin = true
            });
            DailyActivityReportData user1PickB = repo.LoadReport(new DailyActivityFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = fx.StoreBId, CashierId = "", UserId = fx.Cashier1Id, IsAdmin = false
            });
            Check("Chi tiết HĐ ngày Repo (§7/P2 user CH A chọn StoreId=CH B): cả 4 khối rỗng & BillTotalDiscount=0 (đối chứng Admin CH B đủ 4 khối)",
                adminStoreB.SaleBills.Count == 1 && adminStoreB.SoldItems.Count == 1
                && adminStoreB.Purchases.Count == 1 && adminStoreB.CashFlows.Count == 1
                && adminStoreB.BillTotalDiscount == 5000m
                && user1PickB.SaleBills.Count == 0 && user1PickB.SoldItems.Count == 0
                && user1PickB.Purchases.Count == 0 && user1PickB.CashFlows.Count == 0
                && user1PickB.BillTotalDiscount == 0m,
                "adminB=" + adminStoreB.SaleBills.Count + "/" + adminStoreB.SoldItems.Count + "/" + adminStoreB.Purchases.Count + "/" + adminStoreB.CashFlows.Count
                + ", userB=" + user1PickB.SaleBills.Count + "/" + user1PickB.SoldItems.Count + "/" + user1PickB.Purchases.Count + "/" + user1PickB.CashFlows.Count);

            // User thường 0 cửa hàng: toàn bộ rỗng.
            DailyActivityReportData noStoreData = repo.LoadReport(new DailyActivityFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = "", CashierId = "", UserId = fx.UserNoStoreId, IsAdmin = false
            });
            Check("Chi tiết HĐ ngày Repo (§7/P2 user thường không có cửa hàng): cả 4 khối đều rỗng",
                noStoreData.SaleBills.Count == 0 && noStoreData.SoldItems.Count == 0
                && noStoreData.Purchases.Count == 0 && noStoreData.CashFlows.Count == 0);
        }

        private static DailySoldItemRow FindSoldItem(
            DailyActivityReportData data,
            string itemName,
            string productCode,
            string unitId,
            decimal unitPrice,
            decimal discountRate)
        {
            return data.SoldItems.Find(x =>
                x.ItemName == itemName
                && x.ProductCode == productCode
                && x.UnitId == unitId
                && x.UnitPrice == unitPrice
                && x.DiscountRate == discountRate);
        }
    }
}
