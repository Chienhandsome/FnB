using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PromotionTests(string db)
        {
            PromotionKeywordTableTests();
            PromotionPureRuleTests();
            PromotionCrudOverlapEditDeleteDbTests(db);
            PromotionUiTests(db);
            PromotionApplyTests(db);
        }

        private static void PromotionKeywordTableTests()
        {
            // Kiểm từng loại trong bảng từ khóa duy nhất PromotionKindResolver (§2).
            bool k1 = PromotionKindResolver.Resolve("Giảm giá tổng bill") == PromotionKind.TotalBillPercent
                && PromotionKindResolver.Resolve("GIAM GIA TONG BILL") == PromotionKind.TotalBillPercent;
            bool k2 = PromotionKindResolver.Resolve("Giảm giá theo nhóm hàng") == PromotionKind.ProductGroupPercent
                && PromotionKindResolver.Resolve("giam gia theo nhom hang") == PromotionKind.ProductGroupPercent;
            bool k3 = PromotionKindResolver.Resolve("Giảm theo giá trị đơn hàng") == PromotionKind.OrderValuePercent
                && PromotionKindResolver.Resolve("Giam theo GIA TRI DON HANG") == PromotionKind.OrderValuePercent;
            bool k4 = PromotionKindResolver.Resolve("Giảm theo số lượng mặt hàng") == PromotionKind.QuantityPercent
                && PromotionKindResolver.Resolve("Giam theo so luong") == PromotionKind.QuantityPercent;
            bool k5 = PromotionKindResolver.Resolve("Mua x sản phẩm tặng y sản phẩm") == PromotionKind.BuyXGetY
                && PromotionKindResolver.Resolve("Mua 2 tặng 1") == PromotionKind.BuyXGetY;
            bool k6 = PromotionKindResolver.Resolve("Đồng giá") == PromotionKind.FixedPrice
                && PromotionKindResolver.Resolve("MẶT HÀNG ĐỒNG GIÁ") == PromotionKind.FixedPrice;
            bool k7 = PromotionKindResolver.Resolve("Giảm giá % theo sản phẩm") == PromotionKind.ProductPercent
                && PromotionKindResolver.Resolve("Giảm giá PHẦN TRĂM theo sản phẩm") == PromotionKind.ProductPercent;
            bool k8 = PromotionKindResolver.Resolve("Giảm giá tiền theo sản phẩm") == PromotionKind.ProductAmount
                && PromotionKindResolver.Resolve("GIAM GIA TIEN THEO SAN PHAM") == PromotionKind.ProductAmount;
            bool k9 = PromotionKindResolver.Resolve("Tặng theo trị giá") == PromotionKind.GiftByOrderValue
                && PromotionKindResolver.Resolve("Tặng theo trị giá đơn hàng") == PromotionKind.GiftByOrderValue;
            bool unk = PromotionKindResolver.Resolve("") == PromotionKind.Unknown
                && PromotionKindResolver.Resolve("Khuyến mại lạ chưa hỗ trợ") == PromotionKind.Unknown;

            Check("KM §2 (bảng từ khóa 9 loại không dấu/không phân biệt hoa thường): nhận diện đúng cả 9 loại và loại lạ",
                k1 && k2 && k3 && k4 && k5 && k6 && k7 && k8 && k9 && unk,
                "1=" + k1 + ",2=" + k2 + ",3=" + k3 + ",4=" + k4 + ",5=" + k5 + ",6=" + k6 + ",7=" + k7 + ",8=" + k8 + ",9=" + k9 + ",u=" + unk);
        }

        private static void PromotionPureRuleTests()
        {
            DateTime today = new DateTime(2026, 6, 15);
            var active = new PromotionRow { Name = "KM Active", TypeId = "T1", FromDate = today, ToDate = today.AddDays(5), StoppedFlag = 0 };
            var upcoming = new PromotionRow { Name = "KM Future", TypeId = "T1", FromDate = today.AddDays(1), ToDate = today.AddDays(10), StoppedFlag = 0 };
            var expired = new PromotionRow { Name = "KM Old", TypeId = "T1", FromDate = today.AddDays(-10), ToDate = today.AddDays(-1), StoppedFlag = 0 };
            var stopped = new PromotionRow { Name = "KM Stop", TypeId = "T1", FromDate = today, ToDate = today.AddDays(5), StoppedFlag = 30 };
            var openEnded = new PromotionRow { Name = "KM Open", TypeId = "T1", FromDate = today.AddDays(-1), ToDate = null, StoppedFlag = 0 };

            bool statusOk = PromotionRules.ComputeStatus(active, today) == PromotionStatus.Active
                && PromotionRules.ComputeStatus(openEnded, today) == PromotionStatus.Active
                && PromotionRules.ComputeStatus(upcoming, today) == PromotionStatus.Upcoming
                && PromotionRules.ComputeStatus(expired, today) == PromotionStatus.Expired
                && PromotionRules.ComputeStatus(stopped, today) == PromotionStatus.Stopped
                && PromotionRules.StatusText(PromotionStatus.Active) == "Đang áp dụng"
                && PromotionRules.StatusText(PromotionStatus.Upcoming) == "Chưa tới"
                && PromotionRules.StatusText(PromotionStatus.Expired) == "Hết hạn"
                && PromotionRules.StatusText(PromotionStatus.Stopped) == "Ngừng áp dụng";
            Check("KM §5.5 (quy tắc trạng thái hiển thị): Đang áp dụng, Chưa tới, Hết hạn, Ngừng áp dụng đúng biên ngày", statusOk);

            bool overlapOk = PromotionRules.IntervalsOverlap(new DateTime(2026, 6, 1), new DateTime(2026, 6, 10), new DateTime(2026, 6, 5), new DateTime(2026, 6, 15))
                && PromotionRules.IntervalsOverlap(new DateTime(2026, 6, 1), new DateTime(2026, 6, 10), new DateTime(2026, 6, 10), new DateTime(2026, 6, 20))
                && !PromotionRules.IntervalsOverlap(new DateTime(2026, 6, 1), new DateTime(2026, 6, 10), new DateTime(2026, 6, 11), new DateTime(2026, 6, 20))
                && PromotionRules.IntervalsOverlap(new DateTime(2026, 6, 1), null, new DateTime(2026, 6, 15), new DateTime(2026, 6, 20))
                && !PromotionRules.IntervalsOverlap(new DateTime(2026, 6, 10), null, new DateTime(2026, 6, 1), new DateTime(2026, 6, 9));
            Check("KM §5.2 (quy tắc giao khoảng ngày): nhận diện đúng giao, chạm biên, rời nhau và Đến ngày trống không giới hạn", overlapOk);

            string clipped = PromotionRules.ClipLogText(new string('Z', 300));
            Check("KM §5.5 (cắt chuỗi lưu vết): ClipLogText cắt đúng 255 ký tự", clipped.Length == 255);
        }

        private static Dictionary<PromotionKind, string> EnsureAllPromotionTypesForTest(string db, List<string> seededTypeIds)
        {
            var repo = new PromotionRepository(db);
            var map = new Dictionary<PromotionKind, string>();
            foreach (PromotionTypeItem item in repo.LoadTypes())
            {
                if (item.Kind != PromotionKind.Unknown && !map.ContainsKey(item.Kind))
                    map[item.Kind] = item.Id;
            }

            var defaults = new KeyValuePair<PromotionKind, string>[]
            {
                new KeyValuePair<PromotionKind, string>(PromotionKind.TotalBillPercent, "Giảm giá tổng bill"),
                new KeyValuePair<PromotionKind, string>(PromotionKind.ProductGroupPercent, "Giảm giá theo nhóm hàng"),
                new KeyValuePair<PromotionKind, string>(PromotionKind.OrderValuePercent, "Giảm theo giá trị đơn hàng"),
                new KeyValuePair<PromotionKind, string>(PromotionKind.QuantityPercent, "Giảm theo số lượng mặt hàng"),
                new KeyValuePair<PromotionKind, string>(PromotionKind.BuyXGetY, "Mua x sản phẩm tặng y sản phẩm"),
                new KeyValuePair<PromotionKind, string>(PromotionKind.FixedPrice, "Đồng giá"),
                new KeyValuePair<PromotionKind, string>(PromotionKind.ProductPercent, "Giảm giá % theo sản phẩm"),
                new KeyValuePair<PromotionKind, string>(PromotionKind.ProductAmount, "Giảm giá tiền theo sản phẩm"),
                new KeyValuePair<PromotionKind, string>(PromotionKind.GiftByOrderValue, "Tặng theo trị giá")
            };

            DateTime now = DbHelper.Now();
            foreach (KeyValuePair<PromotionKind, string> kv in defaults)
            {
                if (map.ContainsKey(kv.Key))
                    continue;
                string id = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO DLOAIHINHKHUYENMAI (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_km', @T)",
                    "@I", id, "@N", kv.Value, "@T", now);
                seededTypeIds.Add(id);
                map[kv.Key] = id;
            }
            return map;
        }

        private static void PromotionCrudOverlapEditDeleteDbTests(string db)
        {
            var svc = new PromotionService(AppSession.Current);
            var repo = new PromotionRepository(db);
            var grpRepo = new ProductGroupRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            DateTime baseDate = new DateTime(2005, 6, 1);

            var seededTypeIds = new List<string>();
            var createdPromoIds = new List<string>();
            string unitId = null;
            string grp1Id = null;
            string grp2Id = null;
            string prod1Id = null;
            string prod2Id = null;

            try
            {
                Dictionary<PromotionKind, string> types = EnsureAllPromotionTypesForTest(db, seededTypeIds);
                unitId = DbHelper.NewId();
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_km', @T)",
                    "@I", unitId, "@N", "Ly KM " + tag, "@T", DbHelper.Now());

                string gErr;
                grp1Id = grpRepo.Save(new ProductGroupRow { Name = "Nhóm KM 1 " + tag, DrinkTypeId = "1" }, "selftest_km", out gErr);
                grp2Id = grpRepo.Save(new ProductGroupRow { Name = "Nhóm KM 2 " + tag, ParentId = grp1Id, DrinkTypeId = "1" }, "selftest_km", out gErr);

                prod1Id = DbHelper.NewId();
                prod2Id = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @G, @U, '0', 50000, 30, 'selftest_km', @T)",
                    "@I", prod1Id, "@C", "KMA" + tag, "@N", "Trà Đào KM " + tag, "@G", grp1Id, "@U", unitId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DNHOMMATHANGID, DDONVITINHID, DLOAIMATHANGID, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, @G, @U, '0', 35000, 30, 'selftest_km', @T)",
                    "@I", prod2Id, "@C", "KMB" + tag, "@N", "Bánh Kem KM " + tag, "@G", grp2Id, "@U", unitId, "@T", DbHelper.Now());

                // 1. Lưu hợp lệ đủ 9 loại ở §2 và đọc lại kiểm đúng từng cột DDOTKHUYENMAI + DDOTKHUYENMAICHITIET (§5.1).
                var p1 = new PromotionRow { Name = "KM1 Tổng bill " + tag, TypeId = types[PromotionKind.TotalBillPercent], FromDate = baseDate, ToDate = baseDate.AddDays(5), DiscountPercent = 12.5m, Note = "Tổng bill" };
                PromotionSaveResult r1 = svc.Save(p1, null);
                if (r1.Ok) createdPromoIds.Add(r1.SavedId);
                PromotionRow back1 = r1.Ok ? repo.LoadById(r1.SavedId) : null;
                bool ok1 = r1.Ok && back1 != null && back1.DiscountPercent == 12.5m && repo.LoadDetails(r1.SavedId).Count == 0;

                var p2 = new PromotionRow { Name = "KM2 Theo nhóm " + tag, TypeId = types[PromotionKind.ProductGroupPercent], FromDate = baseDate, ToDate = baseDate.AddDays(5) };
                PromotionSaveResult r2 = svc.Save(p2, new[] { new PromotionDetailRow { ProductGroupId = grp1Id, DiscountPercent = 15m, Note = "N1" } });
                if (r2.Ok) createdPromoIds.Add(r2.SavedId);
                List<PromotionDetailRow> d2 = r2.Ok ? repo.LoadDetails(r2.SavedId) : new List<PromotionDetailRow>();
                bool ok2 = r2.Ok && d2.Count == 1 && d2[0].ProductGroupId == grp1Id && d2[0].DiscountPercent == 15m;

                var p3 = new PromotionRow { Name = "KM3 Giá trị ĐH " + tag, TypeId = types[PromotionKind.OrderValuePercent], FromDate = baseDate, ToDate = baseDate.AddDays(5) };
                PromotionSaveResult r3 = svc.Save(p3, new[]
                {
                    new PromotionDetailRow { OrderValue = 200000m, DiscountPercent = 5m },
                    new PromotionDetailRow { OrderValue = 500000m, DiscountPercent = 8m }
                });
                if (r3.Ok) createdPromoIds.Add(r3.SavedId);
                List<PromotionDetailRow> d3 = r3.Ok ? repo.LoadDetails(r3.SavedId) : new List<PromotionDetailRow>();
                bool ok3 = r3.Ok && d3.Count == 2 && d3[0].OrderValue == 200000m && d3[0].DiscountPercent == 5m && d3[1].OrderValue == 500000m && d3[1].DiscountPercent == 8m;

                var p4 = new PromotionRow { Name = "KM4 Số lượng " + tag, TypeId = types[PromotionKind.QuantityPercent], FromDate = baseDate, ToDate = baseDate.AddDays(5) };
                PromotionSaveResult r4 = svc.Save(p4, new[] { new PromotionDetailRow { ItemQuantity = 3m, DiscountPercent = 10m } });
                if (r4.Ok) createdPromoIds.Add(r4.SavedId);
                List<PromotionDetailRow> d4 = r4.Ok ? repo.LoadDetails(r4.SavedId) : new List<PromotionDetailRow>();
                bool ok4 = r4.Ok && d4.Count == 1 && d4[0].ItemQuantity == 3m && d4[0].DiscountPercent == 10m;

                var p5 = new PromotionRow { Name = "KM5 Mua x tặng y " + tag, TypeId = types[PromotionKind.BuyXGetY], FromDate = baseDate, ToDate = baseDate.AddDays(5) };
                PromotionSaveResult r5 = svc.Save(p5, new[]
                {
                    new PromotionDetailRow { ProductId = prod1Id, BuyQuantity = 2m, GiftQuantity = 1m, GiftProductId = prod2Id },
                    new PromotionDetailRow { ProductId = prod2Id, BuyQuantity = 3m, GiftQuantity = 1m, GiftProductId = null }
                });
                if (r5.Ok) createdPromoIds.Add(r5.SavedId);
                List<PromotionDetailRow> d5 = r5.Ok ? repo.LoadDetails(r5.SavedId) : new List<PromotionDetailRow>();
                bool ok5 = r5.Ok && d5.Count == 2 && d5[0].ProductId == prod1Id && d5[0].BuyQuantity == 2m && d5[0].GiftQuantity == 1m && d5[0].GiftProductId == prod2Id
                    && d5[1].ProductId == prod2Id && string.IsNullOrEmpty(d5[1].GiftProductId);

                var p6 = new PromotionRow { Name = "KM6 Đồng giá " + tag, TypeId = types[PromotionKind.FixedPrice], FromDate = baseDate, ToDate = baseDate.AddDays(5) };
                PromotionSaveResult r6 = svc.Save(p6, new[] { new PromotionDetailRow { ProductId = prod1Id, SalePrice = 29000m } });
                if (r6.Ok) createdPromoIds.Add(r6.SavedId);
                List<PromotionDetailRow> d6 = r6.Ok ? repo.LoadDetails(r6.SavedId) : new List<PromotionDetailRow>();
                bool ok6 = r6.Ok && d6.Count == 1 && d6[0].ProductId == prod1Id && d6[0].SalePrice == 29000m;

                var p7 = new PromotionRow { Name = "KM7 % sản phẩm " + tag, TypeId = types[PromotionKind.ProductPercent], FromDate = baseDate, ToDate = baseDate.AddDays(5) };
                PromotionSaveResult r7 = svc.Save(p7, new[] { new PromotionDetailRow { ProductId = prod1Id, DiscountPercent = 20m } });
                if (r7.Ok) createdPromoIds.Add(r7.SavedId);
                List<PromotionDetailRow> d7 = r7.Ok ? repo.LoadDetails(r7.SavedId) : new List<PromotionDetailRow>();
                bool ok7 = r7.Ok && d7.Count == 1 && d7[0].ProductId == prod1Id && d7[0].DiscountPercent == 20m;

                var p8 = new PromotionRow { Name = "KM8 Tiền SP " + tag, TypeId = types[PromotionKind.ProductAmount], FromDate = baseDate, ToDate = baseDate.AddDays(5) };
                PromotionSaveResult r8 = svc.Save(p8, new[] { new PromotionDetailRow { ProductId = prod1Id, SalePrice = 5000m } });
                if (r8.Ok) createdPromoIds.Add(r8.SavedId);
                List<PromotionDetailRow> d8 = r8.Ok ? repo.LoadDetails(r8.SavedId) : new List<PromotionDetailRow>();
                bool ok8 = r8.Ok && d8.Count == 1 && d8[0].ProductId == prod1Id && d8[0].SalePrice == 5000m;

                var p9 = new PromotionRow { Name = "KM9 Tặng trị giá " + tag, TypeId = types[PromotionKind.GiftByOrderValue], FromDate = baseDate, ToDate = baseDate.AddDays(5) };
                PromotionSaveResult r9 = svc.Save(p9, new[] { new PromotionDetailRow { OrderValue = 200000m, GiftQuantity = 1m, GiftProductId = prod2Id } });
                if (r9.Ok) createdPromoIds.Add(r9.SavedId);
                List<PromotionDetailRow> d9 = r9.Ok ? repo.LoadDetails(r9.SavedId) : new List<PromotionDetailRow>();
                bool ok9 = r9.Ok && d9.Count == 1 && d9[0].OrderValue == 200000m && d9[0].GiftQuantity == 1m && d9[0].GiftProductId == prod2Id;

                Check("KM §5.1 (lưu đủ 9 loại §2): lưu và đọc lại đúng cột DDOTKHUYENMAI + DDOTKHUYENMAICHITIET cho từng loại",
                    ok1 && ok2 && ok3 && ok4 && ok5 && ok6 && ok7 && ok8 && ok9,
                    "1=" + ok1 + ",2=" + ok2 + ",3=" + ok3 + ",4=" + ok4 + ",5=" + ok5 + ",6=" + ok6 + ",7=" + ok7 + ",8=" + ok8 + ",9=" + ok9);

                // 2. Chặn dữ liệu không hợp lệ đủ các ca (§5.1).
                PromotionSaveResult invNoName = svc.Save(new PromotionRow { Name = "  ", TypeId = types[PromotionKind.TotalBillPercent], DiscountPercent = 10m }, null);
                PromotionSaveResult invDupName = svc.Save(new PromotionRow { Name = "km1 tong bill " + tag, TypeId = types[PromotionKind.TotalBillPercent], FromDate = baseDate.AddDays(20), ToDate = baseDate.AddDays(25), DiscountPercent = 10m }, null);
                PromotionSaveResult invNoType = svc.Save(new PromotionRow { Name = "KM Thiếu loại " + tag, TypeId = null, DiscountPercent = 10m }, null);
                PromotionSaveResult invBadDate = svc.Save(new PromotionRow { Name = "KM Ngày ngược " + tag, TypeId = types[PromotionKind.TotalBillPercent], FromDate = baseDate.AddDays(10), ToDate = baseDate.AddDays(5), DiscountPercent = 10m }, null);
                PromotionSaveResult invPct0 = svc.Save(new PromotionRow { Name = "KM Pct0 " + tag, TypeId = types[PromotionKind.TotalBillPercent], FromDate = baseDate.AddDays(20), DiscountPercent = 0m }, null);
                PromotionSaveResult invPct101 = svc.Save(new PromotionRow { Name = "KM Pct101 " + tag, TypeId = types[PromotionKind.TotalBillPercent], FromDate = baseDate.AddDays(20), DiscountPercent = 101m }, null);
                PromotionSaveResult invMissingProd = svc.Save(new PromotionRow { Name = "KM Thiếu MH " + tag, TypeId = types[PromotionKind.ProductPercent], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { ProductId = "", DiscountPercent = 10m } });
                PromotionSaveResult invDupProd = svc.Save(new PromotionRow { Name = "KM Lặp MH " + tag, TypeId = types[PromotionKind.ProductPercent], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { ProductId = prod1Id, DiscountPercent = 10m }, new PromotionDetailRow { ProductId = prod1Id, DiscountPercent = 15m } });
                PromotionSaveResult invDupGrp = svc.Save(new PromotionRow { Name = "KM Lặp nhóm " + tag, TypeId = types[PromotionKind.ProductGroupPercent], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { ProductGroupId = grp1Id, DiscountPercent = 5m }, new PromotionDetailRow { ProductGroupId = grp1Id, DiscountPercent = 10m } });
                PromotionSaveResult invBuyZero = svc.Save(new PromotionRow { Name = "KM Mua 0 " + tag, TypeId = types[PromotionKind.BuyXGetY], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { ProductId = prod1Id, BuyQuantity = 0m, GiftQuantity = 1m } });
                PromotionSaveResult invGiftZero = svc.Save(new PromotionRow { Name = "KM Tặng 0 " + tag, TypeId = types[PromotionKind.BuyXGetY], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { ProductId = prod1Id, BuyQuantity = 2m, GiftQuantity = 0m } });
                PromotionSaveResult invFixedZero = svc.Save(new PromotionRow { Name = "KM ĐG 0 " + tag, TypeId = types[PromotionKind.FixedPrice], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { ProductId = prod1Id, SalePrice = 0m } });
                PromotionSaveResult invAmtZero = svc.Save(new PromotionRow { Name = "KM Tiền 0 " + tag, TypeId = types[PromotionKind.ProductAmount], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { ProductId = prod1Id, SalePrice = 0m } });
                PromotionSaveResult invGiftNoProd = svc.Save(new PromotionRow { Name = "KM Tặng thiếu MH " + tag, TypeId = types[PromotionKind.GiftByOrderValue], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { OrderValue = 100000m, GiftQuantity = 1m, GiftProductId = "" } });
                PromotionSaveResult invQtyZero = svc.Save(new PromotionRow { Name = "KM SL 0 " + tag, TypeId = types[PromotionKind.QuantityPercent], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { ItemQuantity = 0m, DiscountPercent = 5m } });
                PromotionSaveResult invValZero = svc.Save(new PromotionRow { Name = "KM GT 0 " + tag, TypeId = types[PromotionKind.OrderValuePercent], FromDate = baseDate.AddDays(20) }, new[] { new PromotionDetailRow { OrderValue = 0m, DiscountPercent = 5m } });

                bool invalidBlockedOk = !invNoName.Ok && !invDupName.Ok && !invNoType.Ok && !invBadDate.Ok
                    && !invPct0.Ok && !invPct101.Ok && !invMissingProd.Ok && !invDupProd.Ok && !invDupGrp.Ok
                    && !invBuyZero.Ok && !invGiftZero.Ok && !invFixedZero.Ok && !invAmtZero.Ok
                    && !invGiftNoProd.Ok && !invQtyZero.Ok && !invValZero.Ok;
                Check("KM §5.1 (chặn nhập sai mọi loại): thiếu tên/trùng tên/thiếu loại/ngày ngược/tỉ lệ ngoài (0;100]/thiếu MH/lặp MH & nhóm/số <= 0 đều chặn có thông báo",
                    invalidBlockedOk,
                    SingleLine((invNoName.ErrorMessage ?? "") + " | " + (invDupProd.ErrorMessage ?? "")));

                // 3. Chặn trùng đủ các ca (§5.2).
                PromotionSaveResult ovSameOverlap = svc.Save(new PromotionRow
                {
                    Name = "KM Trùng giao " + tag, TypeId = types[PromotionKind.TotalBillPercent],
                    FromDate = baseDate.AddDays(3), ToDate = baseDate.AddDays(8), DiscountPercent = 5m
                }, null);
                PromotionSaveResult ovTouch = svc.Save(new PromotionRow
                {
                    Name = "KM Trùng biên " + tag, TypeId = types[PromotionKind.TotalBillPercent],
                    FromDate = baseDate.AddDays(5), ToDate = baseDate.AddDays(9), DiscountPercent = 5m
                }, null);
                var pDisjoint = new PromotionRow
                {
                    Name = "KM Rời ngày " + tag, TypeId = types[PromotionKind.TotalBillPercent],
                    FromDate = baseDate.AddDays(6), ToDate = null, DiscountPercent = 7m
                };
                PromotionSaveResult ovDisjoint = svc.Save(pDisjoint, null);
                if (ovDisjoint.Ok) createdPromoIds.Add(ovDisjoint.SavedId);

                PromotionSaveResult ovOpenEnded = svc.Save(new PromotionRow
                {
                    Name = "KM Đụng không giới hạn " + tag, TypeId = types[PromotionKind.TotalBillPercent],
                    FromDate = baseDate.AddDays(30), ToDate = baseDate.AddDays(40), DiscountPercent = 6m
                }, null);

                var pStopped = new PromotionRow
                {
                    Name = "KM Đang ngừng " + tag, TypeId = types[PromotionKind.TotalBillPercent],
                    FromDate = baseDate.AddDays(1), ToDate = baseDate.AddDays(4), StoppedFlag = 30, DiscountPercent = 8m
                };
                PromotionSaveResult ovStoppedOk = svc.Save(pStopped, null);
                if (ovStoppedOk.Ok) createdPromoIds.Add(ovStoppedOk.SavedId);

                // Sửa chính p1 không tự báo trùng.
                PromotionRow p1Reload = repo.LoadById(r1.SavedId);
                p1Reload.Note = "Sửa chính nó";
                PromotionSaveResult editSelfOk = svc.Save(p1Reload, null);

                // Bật lại pStopped (NGUNGAPDUNG 30 -> 0) khi đang trùng p1 -> chặn.
                PromotionRow pStoppedReload = repo.LoadById(ovStoppedOk.SavedId);
                pStoppedReload.IsStopped = false;
                PromotionSaveResult reenableBlocked = svc.Save(pStoppedReload, null);

                string expectedOvMsg = PromotionText.OverlapBlocked(p1.Name);
                Check("KM §5.2 (chặn đợt trùng): cùng loại giao/chạm biên/không Đến ngày bị chặn đúng câu; rời ngày/khác loại/ngừng áp dụng lưu được; sửa chính nó OK; bật lại đợt ngừng trùng bị chặn",
                    !ovSameOverlap.Ok && ovSameOverlap.ErrorMessage == expectedOvMsg
                    && !ovTouch.Ok && ovTouch.HasOverlapError
                    && ovDisjoint.Ok && !ovOpenEnded.Ok && ovOpenEnded.ErrorMessage == PromotionText.OverlapBlocked(pDisjoint.Name)
                    && ovStoppedOk.Ok && editSelfOk.Ok && !reenableBlocked.Ok && reenableBlocked.ErrorMessage == expectedOvMsg,
                    SingleLine((ovSameOverlap.ErrorMessage ?? "") + " | " + (reenableBlocked.ErrorMessage ?? "")));

                // 4. Sửa đợt (§5.3): cập nhật chi tiết, người/giờ sửa, giữ nguyên giờ tạo; không đổi loại khi đã có chi tiết; đổi được loại khi chưa có chi tiết.
                PromotionRow p7Before = repo.LoadById(r7.SavedId);
                p7Before.Name = "KM7 % SP Sửa " + tag;
                p7Before.Note = "Cập nhật chi tiết";
                PromotionSaveResult edit7Ok = svc.Save(p7Before, new[]
                {
                    new PromotionDetailRow { ProductId = prod1Id, DiscountPercent = 25m, Note = "Mới 1" },
                    new PromotionDetailRow { ProductId = prod2Id, DiscountPercent = 30m, Note = "Mới 2" }
                });
                PromotionRow p7After = repo.LoadById(r7.SavedId);
                List<PromotionDetailRow> d7After = repo.LoadDetails(r7.SavedId);

                p7After.TypeId = types[PromotionKind.FixedPrice];
                PromotionSaveResult changeTypeBlocked = svc.Save(p7After, new[] { new PromotionDetailRow { ProductId = prod1Id, SalePrice = 20000m } });

                // Đợt Tổng bill (0 chi tiết) đổi sang Đồng giá với ngày rời -> được phép.
                PromotionRow pDisjointReload = repo.LoadById(ovDisjoint.SavedId);
                pDisjointReload.TypeId = types[PromotionKind.FixedPrice];
                pDisjointReload.ToDate = baseDate.AddDays(12);
                PromotionSaveResult changeTypeZeroDetailsOk = svc.Save(pDisjointReload, new[] { new PromotionDetailRow { ProductId = prod2Id, SalePrice = 25000m } });

                Check("KM §5.3 (sửa đợt & khóa đổi loại): ghi lại đủ chi tiết mới, cập nhật người/giờ sửa, chặn đổi loại khi đã có chi tiết, cho đổi khi 0 chi tiết",
                    edit7Ok.Ok && d7After.Count == 2 && d7After[0].DiscountPercent == 25m && d7After[1].DiscountPercent == 30m
                    && !string.IsNullOrEmpty(p7After.ModifiedById) && p7After.ModifiedAt.HasValue
                    && !changeTypeBlocked.Ok && changeTypeBlocked.ErrorMessage == PromotionText.TypeLockedWhenHasDetails
                    && changeTypeZeroDetailsOk.Ok,
                    SingleLine((changeTypeBlocked.ErrorMessage ?? "") + " | d7=" + d7After.Count));

                // 5. Rollback bằng hook cho cả Save và Delete + Xóa cứng đợt và chi tiết kèm TLUUVET (§5.4).
                var pRbSave = new PromotionRow
                {
                    Name = "KM Rollback Save " + tag, TypeId = types[PromotionKind.ProductPercent],
                    FromDate = baseDate.AddDays(60), ToDate = baseDate.AddDays(65)
                };
                bool saveThrew = false;
                try
                {
                    svc.Save(pRbSave, new[] { new PromotionDetailRow { ProductId = prod1Id, DiscountPercent = 10m } },
                        delegate { throw new InvalidOperationException("sim-promo-save-rb"); });
                }
                catch (InvalidOperationException) { saveThrew = true; }
                bool saveRbClean = saveThrew && string.IsNullOrEmpty(pRbSave.Id)
                    && !repo.LoadAll().Exists(x => x.Name == "KM Rollback Save " + tag);

                bool delThrew = false;
                try
                {
                    svc.Delete(r5.SavedId, delegate { throw new InvalidOperationException("sim-promo-del-rb"); });
                }
                catch (InvalidOperationException) { delThrew = true; }
                bool delRbClean = delThrew && repo.LoadById(r5.SavedId) != null && repo.LoadDetails(r5.SavedId).Count == 2;

                int logsBeforeDel = CountActivityLogsForBill(db, r5.SavedId);
                PromotionDeleteResult delOk = svc.Delete(r5.SavedId);
                bool hardDeleted = delOk.Ok && repo.LoadById(r5.SavedId) == null && repo.LoadDetails(r5.SavedId).Count == 0;
                int logsAfterDel = CountActivityLogsForBill(db, r5.SavedId);
                // Giữ lại ID để finally dọn TLUUVET.

                Check("KM §5.4 (xóa cứng & rollback hook): lỗi giữa chừng rollback cả đầu đợt+chi tiết+TLUUVET; xóa thật mất cả đợt và chi tiết trong 1 transaction có lưu vết",
                    saveRbClean && delRbClean && hardDeleted && logsAfterDel > logsBeforeDel,
                    "saveRb=" + saveRbClean + ", delRb=" + delRbClean + ", del=" + hardDeleted + ", logs=" + logsBeforeDel + "->" + logsAfterDel);

                // 6. Tên 255 ký tự + Ghi chú 255 ký tự không làm tràn cột lưu vết TLUUVET (§5.5).
                string name255 = ("KM255_" + tag + "_").PadRight(255, 'A');
                string note255 = ("NOTE255_" + tag + "_").PadRight(255, 'B');
                var p255 = new PromotionRow
                {
                    Name = name255, Note = note255, TypeId = types[PromotionKind.TotalBillPercent],
                    FromDate = baseDate.AddDays(90), ToDate = baseDate.AddDays(95), DiscountPercent = 5m
                };
                PromotionSaveResult r255 = svc.Save(p255, null);
                if (r255.Ok) createdPromoIds.Add(r255.SavedId);
                int log255 = r255.Ok ? CountActivityLogsForBill(db, r255.SavedId) : 0;
                Check("KM §5.5 (biên 255 ký tự): lưu đợt tên & ghi chú đúng 255 ký tự không làm tràn TLUUVET",
                    r255.Ok && log255 >= 1,
                    "ok=" + r255.Ok + ", logs=" + log255 + ", err=" + SingleLine(r255.ErrorMessage));
            }
            finally
            {
                foreach (string pid in createdPromoIds)
                {
                    if (string.IsNullOrEmpty(pid)) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM DDOTKHUYENMAICHITIET WHERE DDOTKHUYENMAIID = @I", "@I", pid);
                    SafeDeleteById(db, "DDOTKHUYENMAI", pid);
                }
                foreach (string tid in seededTypeIds)
                    SafeDeleteById(db, "DLOAIHINHKHUYENMAI", tid);
                if (prod1Id != null) SafeDeleteById(db, "DMATHANG", prod1Id);
                if (prod2Id != null) SafeDeleteById(db, "DMATHANG", prod2Id);
                if (grp2Id != null) SafeDeleteById(db, "DNHOMMATHANG", grp2Id);
                if (grp1Id != null) SafeDeleteById(db, "DNHOMMATHANG", grp1Id);
                if (unitId != null) SafeDeleteById(db, "DDONVITINH", unitId);
            }
        }
    }
}
