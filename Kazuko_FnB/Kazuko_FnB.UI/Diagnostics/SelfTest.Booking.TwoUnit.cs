using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void BookingTwoUnitTests(string db)
        {
            BookingTwoUnitRuleTests();
            BookingTwoUnitUiTests(db);
        }

        private static BookingSaleItem MakeTwoUnitBookingItem()
        {
            return new BookingSaleItem
            {
                Id = "DH_P2",
                Code = "MH2DVT",
                Name = "Bia lon đặt hàng",
                UnitId = "U_LE",
                UnitName = "Lon",
                BigUnitId = "U_CHAN",
                BigUnitName = "Thùng",
                QuyDoi = 24m,
                SellPrice = 15000m,
                SellPrice2 = 14000m,
                SellPrice3 = 13000m,
                SellPrice4 = 12000m,
                BigSellPrice = 340000m
            };
        }

        private static void BookingTwoUnitRuleTests()
        {
            BookingSaleItem item = MakeTwoUnitBookingItem();
            var singleItem = new BookingSaleItem
            {
                Id = "DH_P1",
                Code = "MH1DVT",
                Name = "Nước suối đặt hàng",
                UnitId = "U_LE",
                UnitName = "Chai",
                SellPrice = 20000m,
                SellPrice2 = 18000m
            };

            // Ca 1: chưa có dòng lẻ, ô SL 2, lẻ hộp 5, chẵn 0 -> một dòng lẻ số lượng 2.
            var l1 = new List<BookingLine>();
            BookingAddItemOutcome o1 = BookingItemRules.AddTwoUnitItem(l1, item, 0, 2m, 5m, 0m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 1]: chưa có dòng lẻ, ô SL 2, lẻ hộp 5, chẵn 0 -> 1 dòng lẻ SL 2 giá 15000",
                o1.Changed && o1.WarningMessage == null && l1.Count == 1
                && l1[0].UnitId == "U_LE" && l1[0].UnitName == "Lon"
                && l1[0].Quantity == 2m && l1[0].Price == 15000m && l1[0].DiscountPct == 0m && l1[0].Amount == 30000m,
                "cnt=" + l1.Count + ", qty=" + (l1.Count > 0 ? l1[0].Quantity.ToString() : "-") + ", warn=" + o1.WarningMessage);

            // Ca 2: đã có dòng lẻ 2, ô SL 0, lẻ hộp 3 -> dòng lẻ vẫn 2, có cảnh báo.
            BookingAddItemOutcome o2 = BookingItemRules.AddTwoUnitItem(l1, item, 0, 0m, 3m, 0m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 2]: đã có dòng lẻ 2, ô SL 0, lẻ hộp 3 -> dòng lẻ vẫn 2, có cảnh báo",
                !o2.Changed && o2.WarningMessage == BookingText.EnterQuantity && l1.Count == 1
                && l1[0].UnitId == "U_LE" && l1[0].Quantity == 2m && l1[0].Price == 15000m && l1[0].Amount == 30000m,
                "chg=" + o2.Changed + ", qty=" + l1[0].Quantity + ", warn=" + o2.WarningMessage);

            // Ca 3: đã có dòng lẻ 2, ô SL 2, lẻ hộp 0 -> dòng lẻ 4, không cảnh báo.
            BookingAddItemOutcome o3 = BookingItemRules.AddTwoUnitItem(l1, item, 0, 2m, 0m, 0m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 3]: đã có dòng lẻ 2, ô SL 2, lẻ hộp 0 -> dòng lẻ 4, không cảnh báo",
                o3.Changed && o3.WarningMessage == null && l1.Count == 1
                && l1[0].UnitId == "U_LE" && l1[0].Quantity == 4m && l1[0].Price == 15000m && l1[0].Amount == 60000m,
                "qty=" + l1[0].Quantity + ", amt=" + l1[0].Amount + ", warn=" + o3.WarningMessage);

            // Ca 4: chưa có dòng lẻ, ô SL 0, lẻ hộp 3 -> không tạo dòng, có cảnh báo.
            var l4 = new List<BookingLine>();
            BookingAddItemOutcome o4 = BookingItemRules.AddTwoUnitItem(l4, item, 0, 0m, 3m, 0m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 4]: chưa có dòng lẻ, ô SL 0, lẻ hộp 3 -> không tạo dòng, có cảnh báo",
                !o4.Changed && o4.WarningMessage == BookingText.EnterQuantity && l4.Count == 0,
                "cnt=" + l4.Count + ", warn=" + o4.WarningMessage);

            // Ca 4b (luật 4 & 6): chưa có dòng lẻ, ô SL 0, lẻ hộp 3, chẵn 1 -> chặn lẻ + cảnh báo, vẫn tạo dòng chẵn 1 giá 340000.
            var l4b = new List<BookingLine>();
            BookingAddItemOutcome o4b = BookingItemRules.AddTwoUnitItem(l4b, item, 0, 0m, 3m, 1m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 4b]: chưa có lẻ, ô SL 0, lẻ 3, chẵn 1 -> vừa cảnh báo vừa tạo dòng chẵn 1 giá 340000",
                o4b.Changed && o4b.WarningMessage == BookingText.EnterQuantity && l4b.Count == 1
                && l4b[0].UnitId == "U_CHAN" && l4b[0].UnitName == "Thùng"
                && l4b[0].Quantity == 1m && l4b[0].Price == 340000m && l4b[0].DiscountPct == 0m && l4b[0].Amount == 340000m,
                "cnt=" + l4b.Count + ", chg=" + o4b.Changed + ", warn=" + o4b.WarningMessage);

            // Ca 5: đã có dòng lẻ 2, ô SL 2, lẻ 0 chẵn 0 -> dòng lẻ 4, không cảnh báo.
            var l5 = new List<BookingLine>
            {
                new BookingLine { ProductId = item.Id, ProductName = item.Name, UnitId = "U_LE", UnitName = "Lon", Quantity = 2m, Price = 15000m, DiscountPct = 0m, Amount = 30000m }
            };
            BookingAddItemOutcome o5 = BookingItemRules.AddTwoUnitItem(l5, item, 0, 2m, 0m, 0m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 5]: đã có dòng lẻ 2, ô SL 2, lẻ 0 chẵn 0 -> 4, không cảnh báo",
                o5.Changed && o5.WarningMessage == null && l5.Count == 1
                && l5[0].UnitId == "U_LE" && l5[0].Quantity == 4m && l5[0].Amount == 60000m,
                "qty=" + l5[0].Quantity + ", warn=" + o5.WarningMessage);

            // Ca 5b: đã có dòng lẻ 2, ô SL 0, lẻ 0 chẵn 0 -> không đổi, có cảnh báo.
            var l5b = new List<BookingLine>
            {
                new BookingLine { ProductId = item.Id, ProductName = item.Name, UnitId = "U_LE", UnitName = "Lon", Quantity = 2m, Price = 15000m, DiscountPct = 0m, Amount = 30000m }
            };
            BookingAddItemOutcome o5b = BookingItemRules.AddTwoUnitItem(l5b, item, 0, 0m, 0m, 0m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 5b]: đã có dòng lẻ 2, ô SL 0, lẻ 0 chẵn 0 -> không đổi, có cảnh báo",
                !o5b.Changed && o5b.WarningMessage == BookingText.EnterQuantity && l5b.Count == 1 && l5b[0].Quantity == 2m);

            // Ca 6: chưa có dòng lẻ, ô SL 2, lẻ 0 chẵn 0 -> không có dòng, có cảnh báo.
            var l6 = new List<BookingLine>();
            BookingAddItemOutcome o6 = BookingItemRules.AddTwoUnitItem(l6, item, 0, 2m, 0m, 0m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 6]: chưa có dòng lẻ, ô SL 2, lẻ 0 chẵn 0 -> 0 dòng, có cảnh báo",
                !o6.Changed && o6.WarningMessage == BookingText.EnterQuantity && l6.Count == 0,
                "cnt=" + l6.Count + ", warn=" + o6.WarningMessage);

            // Ca 6b: chỉ có dòng chẵn (chưa có dòng lẻ), ô SL 2, lẻ 0 chẵn 0 -> không đổi, có cảnh báo.
            var l6b = new List<BookingLine>
            {
                new BookingLine { ProductId = item.Id, ProductName = item.Name, UnitId = "U_CHAN", UnitName = "Thùng", Quantity = 1m, Price = 340000m, DiscountPct = 0m, Amount = 340000m }
            };
            BookingAddItemOutcome o6b = BookingItemRules.AddTwoUnitItem(l6b, item, 0, 2m, 0m, 0m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 6b]: chỉ có dòng chẵn 1, ô SL 2, lẻ 0 chẵn 0 -> không đổi, có cảnh báo",
                !o6b.Changed && o6b.WarningMessage == BookingText.EnterQuantity && l6b.Count == 1 && l6b[0].UnitId == "U_CHAN" && l6b[0].Quantity == 1m);

            // Ca 7: chưa có dòng lẻ, ô SL 2, lẻ 0, chẵn 1 -> chỉ một dòng chẵn 1 giá 340000.
            var l7 = new List<BookingLine>();
            BookingAddItemOutcome o7 = BookingItemRules.AddTwoUnitItem(l7, item, 0, 2m, 0m, 1m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 7]: chưa có dòng lẻ, ô SL 2, lẻ 0, chẵn 1 -> chỉ 1 dòng chẵn 1 giá 340000",
                o7.Changed && o7.WarningMessage == null && l7.Count == 1
                && l7[0].UnitId == "U_CHAN" && l7[0].UnitName == "Thùng"
                && l7[0].Quantity == 1m && l7[0].Price == 340000m && l7[0].DiscountPct == 0m && l7[0].Amount == 340000m,
                "cnt=" + l7.Count + ", u=" + (l7.Count > 0 ? l7[0].UnitId : "-"));

            // Ca 8: hộp hủy -> không đổi, không cảnh báo.
            BookingAddItemOutcome o8 = BookingItemRules.AddTwoUnitItem(l7, item, 0, 2m, 5m, 3m, false, true);
            Check("Đặt hàng 2 ĐVT [thuần 8]: hộp bấm Hủy bỏ -> không đổi, không cảnh báo",
                !o8.Changed && o8.WarningMessage == null && l7.Count == 1 && l7[0].UnitId == "U_CHAN" && l7[0].Quantity == 1m);

            // Ca 9: dòng chẵn gộp khi mergeAllowed = true; thêm dòng mới khi mergeAllowed = false; dòng lẻ vẫn gộp khi mergeAllowed = false.
            var l9 = new List<BookingLine>
            {
                new BookingLine { ProductId = item.Id, ProductName = item.Name, UnitId = "U_LE", UnitName = "Lon", Quantity = 2m, Price = 15000m, DiscountPct = 0m, Amount = 30000m },
                new BookingLine { ProductId = item.Id, ProductName = item.Name, UnitId = "U_CHAN", UnitName = "Thùng", Quantity = 1m, Price = 340000m, DiscountPct = 0m, Amount = 340000m }
            };
            BookingAddItemOutcome o9a = BookingItemRules.AddTwoUnitItem(l9, item, 0, 0m, 0m, 2m, true, true);
            BookingAddItemOutcome o9b = BookingItemRules.AddTwoUnitItem(l9, item, 0, 3m, 0m, 1m, true, false);
            Check("Đặt hàng 2 ĐVT [thuần 9]: dòng chẵn gộp khi được phép (1+2=3), thêm mới khi không được phép; dòng lẻ vẫn gộp (2+3=5)",
                o9a.Changed && o9a.WarningMessage == null && o9b.Changed && o9b.WarningMessage == null
                && l9.Count == 3
                && l9[0].UnitId == "U_LE" && l9[0].Quantity == 5m && l9[0].Amount == 75000m
                && l9[1].UnitId == "U_CHAN" && l9[1].Quantity == 3m && l9[1].Amount == 1020000m
                && l9[2].UnitId == "U_CHAN" && l9[2].Quantity == 1m && l9[2].Price == 340000m && l9[2].Amount == 340000m,
                "cnt=" + l9.Count + ", le=" + l9[0].Quantity + ", ch1=" + l9[1].Quantity);

            // Ca 10: dòng lẻ cũ có đơn giá sửa tay và % giảm khác 0: cộng số lượng giữ đơn giá và % giảm, thành tiền đúng.
            var l10 = new List<BookingLine>
            {
                new BookingLine { ProductId = item.Id, ProductName = item.Name, UnitId = "U_LE", UnitName = "Lon", Quantity = 2m, Price = 18000m, DiscountPct = 10m, Amount = 32400m }
            };
            BookingAddItemOutcome o10 = BookingItemRules.AddTwoUnitItem(l10, item, 0, 3m, 5m, 0m, true, false);
            Check("Đặt hàng 2 ĐVT [thuần 10]: dòng lẻ cũ sửa đơn giá (18000) & % giảm (10%) -> cộng SL giữ giá & % giảm (5*18000*0.9=81000)",
                o10.Changed && o10.WarningMessage == null && l10.Count == 1
                && l10[0].Quantity == 5m && l10[0].Price == 18000m && l10[0].DiscountPct == 10m && l10[0].Amount == 81000m,
                "qty=" + l10[0].Quantity + ", p=" + l10[0].Price + ", d=" + l10[0].DiscountPct + ", a=" + l10[0].Amount);

            // Ca 11: đơn giá dòng lẻ mới theo loại giá khách (PriceForCustomer), dòng chẵn giữ BigSellPrice.
            var l11 = new List<BookingLine>();
            BookingAddItemOutcome o11 = BookingItemRules.AddTwoUnitItem(l11, item, 1, 2m, 5m, 1m, true, true);
            Check("Đặt hàng 2 ĐVT [thuần 11]: dòng lẻ mới dùng PriceForCustomer(1)=14000, dòng chẵn dùng BigSellPrice=340000",
                o11.Changed && o11.WarningMessage == null && l11.Count == 2
                && l11[0].Price == 14000m && l11[0].Amount == 28000m
                && l11[1].Price == 340000m && l11[1].Amount == 340000m);

            // Ca 12: nhánh 1 đơn vị tính giữ nguyên hành vi cũ (SL <= 0 báo cảnh báo; gộp theo mergeAllowed).
            var l12 = new List<BookingLine>();
            BookingAddItemOutcome s0 = BookingItemRules.AddSingleUnitItem(l12, singleItem, 0, 0m, true);
            BookingAddItemOutcome s1 = BookingItemRules.AddSingleUnitItem(l12, singleItem, 0, 2m, true);
            BookingAddItemOutcome s2 = BookingItemRules.AddSingleUnitItem(l12, singleItem, 0, 3m, true);
            BookingAddItemOutcome s3 = BookingItemRules.AddSingleUnitItem(l12, singleItem, 0, 1m, false);
            Check("Đặt hàng 1 ĐVT [thuần 12]: giữ nguyên hành vi cũ (SL=0 cảnh báo; gộp khi mergeAllowed=true, thêm dòng mới khi false)",
                !s0.Changed && s0.WarningMessage == BookingText.EnterQuantity
                && s1.Changed && s2.Changed && s3.Changed && l12.Count == 2
                && l12[0].Quantity == 5m && l12[0].Amount == 100000m
                && l12[1].Quantity == 1m && l12[1].Amount == 20000m);
        }
    }
}
