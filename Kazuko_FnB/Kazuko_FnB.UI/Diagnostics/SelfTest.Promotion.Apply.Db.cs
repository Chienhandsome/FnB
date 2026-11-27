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
        private sealed class PromoApplyDbFixture
        {
            public string StoreId;
            public string WarehouseId;
            public string AreaId;
            public string Table1Id;
            public string Table2Id;
            public string UnitId;
            public string GroupParentId;
            public string GroupChildId;
            public string GroupOtherId;
            public string ProdAId;       // 50.000, GroupOther
            public string ProdChildId;   // 40.000, GroupChild (con của GroupParent)
            public string ProdCId;       // 30.000, GroupOther
            public string ProdGift1Id;   // 25.000, tồn 20
            public string ProdGift2Id;   // 35.000, tồn 20
            public string ProdGiftLowId; // 20.000, tồn 1
            public string StockOrderId;
            public readonly List<string> SeededTypeIds = new List<string>();
            public readonly List<string> CreatedPromoIds = new List<string>();
            public readonly List<string> CreatedBillIds = new List<string>();
            public readonly List<string> TemporarilyStoppedPromoIds = new List<string>();
            public Dictionary<PromotionKind, string> Types;
        }

        private static PromoApplyDbFixture SeedPromoApplyFixture(string db)
        {
            var fx = new PromoApplyDbFixture();
            string tag = DbHelper.NewId().Substring(0, 6);
            DateTime now = new InvoiceNumberRepository(db).GetServerTimestamp();

            // Tạm dừng các đợt KM đang bật sẵn trên DB mẫu (nếu có) để ca test độc lập, rồi khôi phục trong finally.
            DbHelper.Read(db, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID FROM DDOTKHUYENMAI WHERE STATUS = 30 AND COALESCE(NGUNGAPDUNG, 0) <> 30");
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["ID"]);
                    if (!string.IsNullOrEmpty(id))
                        fx.TemporarilyStoppedPromoIds.Add(id);
                }
                return 0;
            });
            foreach (string id in fx.TemporarilyStoppedPromoIds)
                BookingExec(db, "UPDATE DDOTKHUYENMAI SET NGUNGAPDUNG = 30 WHERE ID = @I", "@I", id);

            fx.Types = EnsureAllPromotionTypesForTest(db, fx.SeededTypeIds);

            var grpRepo = new ProductGroupRepository(db);
            string gErr;
            fx.GroupParentId = grpRepo.Save(new ProductGroupRow { Name = "KM2 Nhóm Cha " + tag, DrinkTypeId = "1" }, "selftest_km2", out gErr);
            fx.GroupChildId = grpRepo.Save(new ProductGroupRow { Name = "KM2 Nhóm Con " + tag, ParentId = fx.GroupParentId, DrinkTypeId = "1" }, "selftest_km2", out gErr);
            fx.GroupOtherId = grpRepo.Save(new ProductGroupRow { Name = "KM2 Nhóm Khác " + tag, DrinkTypeId = "0" }, "selftest_km2", out gErr);

            DbHelper.InTransaction(db, (conn, tx) =>
            {
                fx.StoreId = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT FIRST 1 ID FROM DCUAHANG WHERE STATUS = 30 ORDER BY NAME"));
                fx.UnitId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, 'selftest_km2', @T)",
                    "@I", fx.UnitId, "@N", "Phần KM2 " + tag, "@T", now);

                fx.WarehouseId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHOHANG (ID, NAME, STATUS, DCUAHANGID, USERCREATEDID, TIMECREATED) VALUES (@I, @N, 30, @S, 'selftest_km2', @T)",
                    "@I", fx.WarehouseId, "@N", "KHO KM2 " + tag, "@S", fx.StoreId, "@T", now);

                fx.AreaId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DKHUVUC (ID, NAME, DKHOHANGID, DGIAMATHANGID, CACHTINHGIO, TABHIENTHI, STATUS, PARENTID, PARENTDIR, SORTORDER, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, @W, '1', 1, 0, 30, '', '', 'ZZZ9', 'selftest_km2', @T)",
                    "@I", fx.AreaId, "@N", "KV KM2 " + tag, "@W", fx.WarehouseId, "@T", now);

                fx.Table1Id = InsertTestTable(conn, tx, "KM2-B1-" + tag, fx.AreaId, 0, now);
                fx.Table2Id = InsertTestTable(conn, tx, "KM2-B2-" + tag, fx.AreaId, 0, now);

                fx.ProdAId = InsertTestProduct(conn, tx, "KM2 Mon A " + tag, fx.GroupOtherId, fx.UnitId, "0", 50000m, 50000m, 0, now);
                fx.ProdChildId = InsertTestProduct(conn, tx, "KM2 Mon Con " + tag, fx.GroupChildId, fx.UnitId, "0", 40000m, 40000m, 0, now);
                fx.ProdCId = InsertTestProduct(conn, tx, "KM2 Mon C " + tag, fx.GroupOtherId, fx.UnitId, "0", 30000m, 30000m, 0, now);
                fx.ProdGift1Id = InsertTestProduct(conn, tx, "KM2 Qua 1 " + tag, fx.GroupOtherId, fx.UnitId, "0", 25000m, 25000m, 0, now);
                fx.ProdGift2Id = InsertTestProduct(conn, tx, "KM2 Qua 2 " + tag, fx.GroupOtherId, fx.UnitId, "0", 35000m, 35000m, 0, now);
                fx.ProdGiftLowId = InsertTestProduct(conn, tx, "KM2 Qua Ton It " + tag, fx.GroupOtherId, fx.UnitId, "0", 20000m, 20000m, 0, now);

                fx.StockOrderId = DbHelper.NewId();
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHONHAPID, DCUAHANGID, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 97, 30, 0, @D, @W, @S, 'selftest_km2', @T)",
                    "@I", fx.StockOrderId, "@N", "TONDAU-KM2-" + tag, "@D", now.Date, "@W", fx.WarehouseId, "@S", fx.StoreId, "@T", now);

                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdAId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdChildId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdCId, fx.UnitId, fx.WarehouseId, 200m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdGift1Id, fx.UnitId, fx.WarehouseId, 20m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdGift2Id, fx.UnitId, fx.WarehouseId, 20m, now);
                InsertOpeningStockLine(conn, tx, fx.StockOrderId, fx.ProdGiftLowId, fx.UnitId, fx.WarehouseId, 1m, now);
            });

            return fx;
        }

        private static void CleanupPromoApplyFixture(string db, PromoApplyDbFixture fx)
        {
            if (fx == null)
                return;
            try
            {
                foreach (string bid in fx.CreatedBillIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", bid);
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", bid);
                }
                foreach (string pid in fx.CreatedPromoIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", pid);
                    BookingExec(db, "DELETE FROM DDOTKHUYENMAICHITIET WHERE DDOTKHUYENMAIID = @I", "@I", pid);
                    SafeDeleteById(db, "DDOTKHUYENMAI", pid);
                }
                foreach (string tid in fx.SeededTypeIds)
                    SafeDeleteById(db, "DLOAIHINHKHUYENMAI", tid);
                if (fx.StockOrderId != null)
                {
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", fx.StockOrderId);
                    BookingExec(db, "DELETE FROM TDONHANG WHERE ID = @I", "@I", fx.StockOrderId);
                }
                foreach (string mid in new[] { fx.ProdAId, fx.ProdChildId, fx.ProdCId, fx.ProdGift1Id, fx.ProdGift2Id, fx.ProdGiftLowId })
                    if (mid != null) SafeDeleteById(db, "DMATHANG", mid);
                foreach (string gid in new[] { fx.GroupChildId, fx.GroupParentId, fx.GroupOtherId })
                    if (gid != null) SafeDeleteById(db, "DNHOMMATHANG", gid);
                foreach (string tid in new[] { fx.Table1Id, fx.Table2Id })
                    if (tid != null) SafeDeleteById(db, "DBAN", tid);
                if (fx.AreaId != null) SafeDeleteById(db, "DKHUVUC", fx.AreaId);
                if (fx.WarehouseId != null) SafeDeleteById(db, "DKHOHANG", fx.WarehouseId);
                if (fx.UnitId != null) SafeDeleteById(db, "DDONVITINH", fx.UnitId);
            }
            finally
            {
                foreach (string id in fx.TemporarilyStoppedPromoIds)
                    BookingExec(db, "UPDATE DDOTKHUYENMAI SET NGUNGAPDUNG = 0 WHERE ID = @I", "@I", id);
            }
        }

        private static string SaveTempPromo(
            PromotionService promoSvc, PromoApplyDbFixture fx,
            string name, PromotionKind kind, DateTime? from, DateTime? to, int stopped, decimal headerPct,
            params PromotionDetailRow[] details)
        {
            var row = new PromotionRow
            {
                Name = name,
                TypeId = fx.Types[kind],
                FromDate = from,
                ToDate = to,
                StoppedFlag = stopped,
                DiscountPercent = headerPct
            };
            PromotionSaveResult res = promoSvc.Save(row, details);
            if (!res.Ok)
                throw new InvalidOperationException("Không tạo được đợt KM thử: " + res.ErrorMessage);
            fx.CreatedPromoIds.Add(res.SavedId);
            return res.SavedId;
        }

        private static void StopPromoDirect(string db, params string[] promoIds)
        {
            foreach (string id in promoIds)
                if (!string.IsNullOrEmpty(id))
                    BookingExec(db, "UPDATE DDOTKHUYENMAI SET NGUNGAPDUNG = 30 WHERE ID = @I", "@I", id);
        }

        private static void ClearBillLinesForNextScenario(string db, TableService svc, ServiceOrderHeader bill, string tableId)
        {
            ServiceOrderHeader cur = svc.LoadBill(bill.Id);
            foreach (ServiceOrderLine l in cur.Lines)
            {
                if (!l.IsComboChild && !l.IsMaterialExport && !l.IsPromoGift)
                    svc.DeleteLine(bill.Id, tableId, l.Id, "next");
            }
            BookingExec(db, "UPDATE TDONHANG SET TILEGIAMGIA = 0, TIENGIAMGIA = 0, GIAMTHEOTIEN = 0, TILEGIAMGIATONG = 0, TIENGIAMGIATONG = 0, GIAMTONGTHEOTIEN = 0, TILEPHIDICHVU = 0, PHIDICHVU = 0, TILETHUE = 0, TIENTHUE = 0 WHERE ID = @I", "@I", bill.Id);
        }

        private static void PromotionApplyDbIntegrationTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            bool oldNegStock = SystemConfig.ChoPhepAmKho;
            bool oldFee = SystemConfig.CoPhiDichVu;
            bool oldTax = SystemConfig.CoThueSuat;
            bool oldAllowDisc = SystemConfig.ChoPhepNhapGiamGia;
            bool oldMultiLine = SystemConfig.NhapMotMatHangNhieuLanTrongPhieu;
            bool oldKitchenBlock = SystemConfig.KhongDuocGiamDoSauKhiInPhaChe;
            bool oldDefaultItems = SystemConfig.SuDungMatHangMacDinh;
            bool oldReqEmp = SystemConfig.BatBuocNhapNhanVienBanHang;
            bool oldReqOrder = SystemConfig.BatChucNangKiemSoatOrder;
            decimal oldDefDisc = SystemConfig.MacDinhGiamGia;
            decimal oldDefFee = SystemConfig.MacDinhPhiDichVu;
            decimal oldDefTax = SystemConfig.MacDinhThueSuat;

            cfgRepo.Save(new[]
            {
                new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "CoPhiDichVu", DataType = 3, IntValue = 30 },
                new ConfigChange { Name = "CoThueSuat", DataType = 3, IntValue = 30 },
                new ConfigChange { Name = "ChoPhepNhapGiamGia", DataType = 3, IntValue = 30 },
                new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "KhongDuocGiamDoSauKhiInPhaChe", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "SuDungMatHangMacDinh", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "BatBuocNhapNhanVienBanHang", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "BatChucNangKiemSoatOrder", DataType = 3, IntValue = 0 },
                new ConfigChange { Name = "MacDinhGiamGia", DataType = 3, IntValue = 0, DecimalValue = 0m },
                new ConfigChange { Name = "MacDinhPhiDichVu", DataType = 3, IntValue = 0, DecimalValue = 0m },
                new ConfigChange { Name = "MacDinhThueSuat", DataType = 3, IntValue = 0, DecimalValue = 0m }
            }, "selftest_km2");
            SystemConfig.Reload(db);

            var session = new UserSession
            {
                UserId = "selftest-km2",
                UserName = "selftest_km2",
                DisplayName = "SelfTest KM2",
                IsAdmin = true,
                DatabasePath = db,
                Permissions = new FunctionPermissions(true)
            };

            PromoApplyDbFixture fx = SeedPromoApplyFixture(db);
            try
            {
                var promoSvc = new PromotionService(session);
                var svc = new TableService(session);
                DateTime today = promoSvc.GetServerDate();
                string tag = DbHelper.NewId().Substring(0, 6);

                ServiceOpResult opened = svc.OpenTable(fx.Table1Id);
                fx.CreatedBillIds.Add(opened.Bill.Id);
                string billId = opened.Bill.Id;

                // 1. Không có đợt nào hiệu lực -> kết quả bán hàng không đổi (§6.7).
                ServiceOpResult baseAdd = svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 2m);
                Check("KM P2 §6.7 (DB không có đợt hiệu lực): bán món A 50k x 2 = 100k, không giảm, KHUYENMAI=0",
                    baseAdd.Ok && baseAdd.Bill.Lines.Count == 1 && baseAdd.Bill.Lines[0].KhuyenMai == 0
                    && baseAdd.Bill.Lines[0].Amount == 100000m && !baseAdd.Bill.BillDiscountFromPromo && baseAdd.Bill.TongCong == 100000m);

                // 2. §6.1: % SP 10% món A + % nhóm 15% nhóm cha (món nhóm con nhận) + sửa tay lớn hơn/nhỏ hơn.
                ClearBillLinesForNextScenario(db, svc, opened.Bill, fx.Table1Id);
                string pSp10 = SaveTempPromo(promoSvc, fx, "KM2 SP10 " + tag, PromotionKind.ProductPercent, today.AddDays(-1), today.AddDays(1), 0, 0m,
                    new PromotionDetailRow { ProductId = fx.ProdAId, DiscountPercent = 10m });
                string pGrp15 = SaveTempPromo(promoSvc, fx, "KM2 Grp15 " + tag, PromotionKind.ProductGroupPercent, today.AddDays(-1), today.AddDays(1), 0, 0m,
                    new PromotionDetailRow { ProductGroupId = fx.GroupParentId, DiscountPercent = 15m });

                svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 2m);
                svc.AddItem(billId, fx.Table1Id, fx.ProdChildId, 2m);
                ServiceOpResult rLinePct = svc.AddItem(billId, fx.Table1Id, fx.ProdCId, 1m);
                ServiceOrderLine lA = rLinePct.Bill.Lines.Find(x => x.ProductId == fx.ProdAId);
                ServiceOrderLine lChild = rLinePct.Bill.Lines.Find(x => x.ProductId == fx.ProdChildId);
                ServiceOrderLine lC = rLinePct.Bill.Lines.Find(x => x.ProductId == fx.ProdCId);

                ServiceOpResult rManHigh = svc.ChangeLineDiscount(billId, fx.Table1Id, lA.Id, 25m, 0m, false);
                ServiceOrderLine lAManHigh = rManHigh.Bill.Lines.Find(x => x.Id == lA.Id);
                ServiceOpResult rManLow = svc.ChangeLineDiscount(billId, fx.Table1Id, lA.Id, 5m, 0m, false);
                ServiceOrderLine lAManLow = rManLow.Bill.Lines.Find(x => x.Id == lA.Id);

                Check("KM P2 §6.1 (DB): % SP 10% (50k x 2=90k, KM=30), % nhóm cha 15% áp nhóm con (68k), tay 25% thắng KM (KM=0), tay 5% thua KM 10% (KM=30)",
                    lA != null && lA.DiscountRate == 10m && lA.Amount == 90000m && lA.KhuyenMai == 30
                    && lChild != null && lChild.DiscountRate == 15m && lChild.Amount == 68000m && lChild.KhuyenMai == 30
                    && lC != null && lC.DiscountRate == 0m && lC.Amount == 30000m && lC.KhuyenMai == 0
                    && lAManHigh.DiscountRate == 25m && lAManHigh.KhuyenMai == 0 && lAManHigh.Amount == 75000m
                    && lAManLow.DiscountRate == 10m && lAManLow.KhuyenMai == 30 && lAManLow.Amount == 90000m);
                StopPromoDirect(db, pSp10, pGrp15);

                // 3. §6.2: Giảm tiền SP 5k/đv (không dưới 0) + Đồng giá 30k cho món 50k và đồng giá cao hơn không tác dụng.
                ClearBillLinesForNextScenario(db, svc, opened.Bill, fx.Table1Id);
                string pAmt5 = SaveTempPromo(promoSvc, fx, "KM2 TienSP " + tag, PromotionKind.ProductAmount, today, today, 0, 0m,
                    new PromotionDetailRow { ProductId = fx.ProdAId, SalePrice = 5000m },
                    new PromotionDetailRow { ProductId = fx.ProdCId, SalePrice = 50000m });
                svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 2m);
                ServiceOpResult rAmt = svc.AddItem(billId, fx.Table1Id, fx.ProdCId, 1m);
                ServiceOrderLine aAmt = rAmt.Bill.Lines.Find(x => x.ProductId == fx.ProdAId);
                ServiceOrderLine cAmt = rAmt.Bill.Lines.Find(x => x.ProductId == fx.ProdCId);
                StopPromoDirect(db, pAmt5);

                ClearBillLinesForNextScenario(db, svc, opened.Bill, fx.Table1Id);
                string pFix = SaveTempPromo(promoSvc, fx, "KM2 DongGia " + tag, PromotionKind.FixedPrice, today, today, 0, 0m,
                    new PromotionDetailRow { ProductId = fx.ProdAId, SalePrice = 30000m },
                    new PromotionDetailRow { ProductId = fx.ProdCId, SalePrice = 60000m });
                svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 2m);
                ServiceOpResult rFix = svc.AddItem(billId, fx.Table1Id, fx.ProdCId, 1m);
                ServiceOrderLine aFix = rFix.Bill.Lines.Find(x => x.ProductId == fx.ProdAId);
                ServiceOrderLine cFix = rFix.Bill.Lines.Find(x => x.ProductId == fx.ProdCId);
                StopPromoDirect(db, pFix);

                Check("KM P2 §6.2 (DB): giảm tiền 5k/đv x 2 = 90k và chặn 0đ; đồng giá 30k cho món 50k x 2 = 60k, đồng giá 60k > 30k không tác dụng",
                    aAmt.Amount == 90000m && aAmt.DiscountAmount == 10000m && aAmt.KhuyenMai == 30
                    && cAmt.Amount == 0m && cAmt.KhuyenMai == 30
                    && aFix.Amount == 60000m && aFix.KhuyenMai == 30
                    && cFix.Amount == 30000m && cFix.KhuyenMai == 0);

                // 4. §6.3 + §6.7: Tổng bill 10% (qua OrderCalculator có phí DV 5% + thuế 10%); Theo giá trị (520k/250k/150k); Theo SL; Giữ giảm tay tổng lớn hơn.
                ClearBillLinesForNextScenario(db, svc, opened.Bill, fx.Table1Id);
                string pBill10 = SaveTempPromo(promoSvc, fx, "KM2 TongBill10 " + tag, PromotionKind.TotalBillPercent, today, today, 0, 10m);
                svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 4m); // 200.000
                decimal roundStep = SystemConfig.LamTronTien;
                ServiceOpResult rFeeTax = svc.UpdateBillFinancials(billId, fx.Table1Id, 10m, 20000m, false, 5m, 0m, false, 10m);
                ServiceOrderTotals expFeeTax = OrderCalculator.CalcTotalsFromSubtotals(
                    200000m, 200000m, 10m, 0m, false, 5m, 0m, false, 10m, 0m, roundStep);
                ServiceOrderValidationResult valFeeTax = OrderCalculator.ValidateInvoice(rFeeTax.Bill, rFeeTax.Bill.Lines, roundStep);

                ServiceOpResult rManBill = svc.UpdateBillFinancials(billId, fx.Table1Id, 15m, 0m, false, 0m, 0m, false, 0m);
                ServiceOpResult rAfterAddKeepMan = svc.IncreaseLineQty(billId, fx.Table1Id, rManBill.Bill.Lines[0].Id, 1m);
                StopPromoDirect(db, pBill10);

                ClearBillLinesForNextScenario(db, svc, opened.Bill, fx.Table1Id);
                string pVal = SaveTempPromo(promoSvc, fx, "KM2 GiaTri " + tag, PromotionKind.OrderValuePercent, today, today, 0, 0m,
                    new PromotionDetailRow { OrderValue = 200000m, DiscountPercent = 5m },
                    new PromotionDetailRow { OrderValue = 500000m, DiscountPercent = 8m });
                ServiceOpResult rv150 = svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 3m); // 150k -> 0%
                ServiceOpResult rv250 = svc.SetLineQty(billId, fx.Table1Id, rv150.Bill.Lines[0].Id, 5m); // 250k -> 5%
                ServiceOpResult rv550 = svc.SetLineQty(billId, fx.Table1Id, rv150.Bill.Lines[0].Id, 11m); // 550k -> 8%
                ServiceOpResult rvBack150 = svc.SetLineQty(billId, fx.Table1Id, rv150.Bill.Lines[0].Id, 3m); // 150k -> tự rút về 0%
                StopPromoDirect(db, pVal);

                ClearBillLinesForNextScenario(db, svc, opened.Bill, fx.Table1Id);
                string pQty = SaveTempPromo(promoSvc, fx, "KM2 SoLuong " + tag, PromotionKind.QuantityPercent, today, today, 0, 0m,
                    new PromotionDetailRow { ItemQuantity = 3m, DiscountPercent = 6m },
                    new PromotionDetailRow { ItemQuantity = 6m, DiscountPercent = 11m });
                ServiceOpResult rq2 = svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 2m);
                ServiceOpResult rq4 = svc.SetLineQty(billId, fx.Table1Id, rq2.Bill.Lines[0].Id, 4m);
                ServiceOpResult rq6 = svc.SetLineQty(billId, fx.Table1Id, rq2.Bill.Lines[0].Id, 6m);
                StopPromoDirect(db, pQty);

                Check("KM P2 §6.3 & §6.7 (DB): tổng bill 10% + phí 5% + thuế 10% khớp OrderCalculator (ValidateInvoice OK); giá trị 150k/250k/550k/150k; SL 2/4/6; giữ tay 15%",
                    rFeeTax.Bill.TileGiamGia == 10m && rFeeTax.Bill.BillDiscountFromPromo && rFeeTax.Bill.PhiDichVu == expFeeTax.PhiDichVu
                    && rFeeTax.Bill.TienThue == expFeeTax.TienThue && rFeeTax.Bill.TongCong == expFeeTax.TongCong && valFeeTax.IsValid
                    && rManBill.Bill.TileGiamGia == 15m && !rManBill.Bill.BillDiscountFromPromo
                    && rAfterAddKeepMan.Bill.TileGiamGia == 15m && !rAfterAddKeepMan.Bill.BillDiscountFromPromo
                    && rv150.Bill.TileGiamGia == 0m && rv250.Bill.TileGiamGia == 5m && rv550.Bill.TileGiamGia == 8m
                    && rvBack150.Bill.TileGiamGia == 0m && !rvBack150.Bill.BillDiscountFromPromo
                    && rq2.Bill.TileGiamGia == 0m && rq4.Bill.TileGiamGia == 6m && rq6.Bill.TileGiamGia == 11m);

                // 5. §6.4: Mua 2 tặng 1 (5->2, 1->0, 5->3 còn 1, xóa mua->xóa tặng) + chặn âm kho dòng tặng + trừ kho khi thanh toán.
                ClearBillLinesForNextScenario(db, svc, opened.Bill, fx.Table1Id);
                string pBuy2Get1 = SaveTempPromo(promoSvc, fx, "KM2 Mua2Tang1 " + tag, PromotionKind.BuyXGetY, today, today, 0, 0m,
                    new PromotionDetailRow { ProductId = fx.ProdAId, BuyQuantity = 2m, GiftQuantity = 1m, GiftProductId = fx.ProdGift1Id });
                ServiceOpResult bg1 = svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 1m);
                bool bg1NoGift = !bg1.Bill.Lines.Exists(x => x.IsPromoGift);

                ServiceOpResult bg5 = svc.SetLineQty(billId, fx.Table1Id, bg1.Bill.Lines[0].Id, 5m);
                ServiceOrderLine g5 = bg5.Bill.Lines.Find(x => x.IsPromoGift);

                ServiceOpResult bg3 = svc.SetLineQty(billId, fx.Table1Id, bg1.Bill.Lines[0].Id, 3m);
                ServiceOrderLine g3 = bg3.Bill.Lines.Find(x => x.IsPromoGift);

                ServiceOpResult bgDel = svc.DeleteLine(billId, fx.Table1Id, bg1.Bill.Lines[0].Id, "del");
                bool bgDelCleared = bgDel.Bill.Lines.Count == 0;
                StopPromoDirect(db, pBuy2Get1);

                // Chặn âm kho dòng tặng (GiftLow tồn = 1): mua 5 cần tặng 2 -> không thêm dòng tặng + có cảnh báo; mua 2 cần tặng 1 -> thêm và thanh toán trừ kho về 0.
                string pBuyLow = SaveTempPromo(promoSvc, fx, "KM2 Mua2TangIt " + tag, PromotionKind.BuyXGetY, today, today, 0, 0m,
                    new PromotionDetailRow { ProductId = fx.ProdAId, BuyQuantity = 2m, GiftQuantity = 1m, GiftProductId = fx.ProdGiftLowId });
                ServiceOpResult lowOver = svc.AddItem(billId, fx.Table1Id, fx.ProdAId, 5m);
                bool blockedGiftStock = lowOver.Ok && !lowOver.Bill.Lines.Exists(x => x.IsPromoGift) && !string.IsNullOrEmpty(lowOver.PromotionWarning);

                ServiceOpResult lowFit = svc.SetLineQty(billId, fx.Table1Id, lowOver.Bill.Lines[0].Id, 2m);
                ServiceOrderLine gLow = lowFit.Bill.Lines.Find(x => x.IsPromoGift);

                // Thanh toán bill để kiểm trừ kho thật của dòng tặng VÀ kiểm bất biến bill đã thanh toán (§6.6).
                PaymentExecutionResult payRes = svc.ExecutePayment(new PaymentRequest
                {
                    PrimaryBillId = billId,
                    PrimaryTableId = fx.Table1Id,
                    CashTendered = lowFit.Bill.TongCong,
                    PrintAfterClose = false
                });
                decimal stockAfterPay = DbHelper.Read(db, conn =>
                    ServiceOrderRepository.GetStockBalanceIn(conn, null, fx.ProdGiftLowId, fx.WarehouseId, ""));
                StopPromoDirect(db, pBuyLow);

                // Tạo đợt mới 30% tổng bill sau khi bill đã thanh toán -> bill đã thanh toán KHÔNG đổi (§6.6).
                string pPostPay = SaveTempPromo(promoSvc, fx, "KM2 SauTT " + tag, PromotionKind.TotalBillPercent, today, today, 0, 30m);
                ServiceOrderHeader paidReload = svc.LoadBill(billId);
                StopPromoDirect(db, pPostPay);

                Check("KM P2 §6.4 & §6.6 (DB): Mua 2 tặng 1 (1->0, 5->2, 3->1, xóa->xóa), chặn âm kho dòng tặng có cảnh báo, thanh toán trừ kho dòng tặng (1->0), bill đã TT không đổi khi đợt đổi",
                    bg1NoGift && g5 != null && g5.ProductId == fx.ProdGift1Id && g5.Quantity == 2m && g5.UnitPrice == 0m && g5.Amount == 0m && g5.KhuyenMai == 30
                    && g3 != null && g3.Quantity == 1m && bgDelCleared
                    && blockedGiftStock && gLow != null && gLow.Quantity == 1m
                    && payRes.Ok && stockAfterPay == 0m
                    && paidReload != null && paidReload.PaidFlag == 30 && paidReload.TileGiamGia == 0m && paidReload.TongCong == 100000m,
                    "warn=" + SingleLine(lowOver.PromotionWarning) + ", stock=" + stockAfterPay + ", paidTc=" + (paidReload == null ? -1 : paidReload.TongCong));

                // 6. §6.5 & §6.6: Tặng theo trị giá (300k->Gift1x1, 150k->0, 550k->Gift2x2) + đợt ngừng/hết hạn/chưa tới không áp dụng.
                ServiceOpResult opened2 = svc.OpenTable(fx.Table2Id);
                fx.CreatedBillIds.Add(opened2.Bill.Id);
                string bill2Id = opened2.Bill.Id;

                string pGiftVal = SaveTempPromo(promoSvc, fx, "KM2 TangTriGia " + tag, PromotionKind.GiftByOrderValue, today, today, 0, 0m,
                    new PromotionDetailRow { OrderValue = 200000m, GiftQuantity = 1m, GiftProductId = fx.ProdGift1Id },
                    new PromotionDetailRow { OrderValue = 500000m, GiftQuantity = 2m, GiftProductId = fx.ProdGift2Id });
                // Thêm đợt ngừng / hết hạn / chưa tới cùng lúc để kiểm không tác dụng.
                string pStop = SaveTempPromo(promoSvc, fx, "KM2 Ngung " + tag, PromotionKind.TotalBillPercent, today.AddDays(-5), today.AddDays(5), 30, 50m);
                string pExp = SaveTempPromo(promoSvc, fx, "KM2 HetHan " + tag, PromotionKind.TotalBillPercent, today.AddDays(-10), today.AddDays(-1), 0, 40m);
                string pFut = SaveTempPromo(promoSvc, fx, "KM2 ChuaToi " + tag, PromotionKind.TotalBillPercent, today.AddDays(1), today.AddDays(10), 0, 35m);

                ServiceOpResult gv150 = svc.AddItem(bill2Id, fx.Table2Id, fx.ProdAId, 3m); // 150k
                ServiceOrderLine bought2 = gv150.Bill.Lines.Find(x => !x.IsPromoGift);
                ServiceOpResult gv300 = svc.SetLineQty(bill2Id, fx.Table2Id, bought2.Id, 6m); // 300k
                ServiceOrderLine gift300 = gv300.Bill.Lines.Find(x => x.IsPromoGift);
                ServiceOpResult gv550 = svc.SetLineQty(bill2Id, fx.Table2Id, bought2.Id, 11m); // 550k
                ServiceOrderLine gift550 = gv550.Bill.Lines.Find(x => x.IsPromoGift);
                StopPromoDirect(db, pGiftVal, pStop, pExp, pFut);

                Check("KM P2 §6.5 & §6.6 (DB): Tặng trị giá 150k->0, 300k->Gift1x1, 550k->Gift2x2; đợt ngừng/hết hạn/chưa tới không áp dụng",
                    !gv150.Bill.Lines.Exists(x => x.IsPromoGift) && gv150.Bill.TileGiamGia == 0m
                    && gift300 != null && gift300.ProductId == fx.ProdGift1Id && gift300.Quantity == 1m && gift300.Amount == 0m
                    && gift550 != null && gift550.ProductId == fx.ProdGift2Id && gift550.Quantity == 2m && gv550.Bill.Lines.FindAll(x => x.IsPromoGift).Count == 1
                    && gv550.Bill.TileGiamGia == 0m);
            }
            finally
            {
                CleanupPromoApplyFixture(db, fx);
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = oldNegStock ? 30 : 0 },
                    new ConfigChange { Name = "CoPhiDichVu", DataType = 3, IntValue = oldFee ? 30 : 0 },
                    new ConfigChange { Name = "CoThueSuat", DataType = 3, IntValue = oldTax ? 30 : 0 },
                    new ConfigChange { Name = "ChoPhepNhapGiamGia", DataType = 3, IntValue = oldAllowDisc ? 30 : 0 },
                    new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = oldMultiLine ? 30 : 0 },
                    new ConfigChange { Name = "KhongDuocGiamDoSauKhiInPhaChe", DataType = 3, IntValue = oldKitchenBlock ? 30 : 0 },
                    new ConfigChange { Name = "SuDungMatHangMacDinh", DataType = 3, IntValue = oldDefaultItems ? 30 : 0 },
                    new ConfigChange { Name = "BatBuocNhapNhanVienBanHang", DataType = 3, IntValue = oldReqEmp ? 30 : 0 },
                    new ConfigChange { Name = "BatChucNangKiemSoatOrder", DataType = 3, IntValue = oldReqOrder ? 30 : 0 },
                    new ConfigChange { Name = "MacDinhGiamGia", DataType = 3, IntValue = (int)oldDefDisc, DecimalValue = oldDefDisc },
                    new ConfigChange { Name = "MacDinhPhiDichVu", DataType = 3, IntValue = (int)oldDefFee, DecimalValue = oldDefFee },
                    new ConfigChange { Name = "MacDinhThueSuat", DataType = 3, IntValue = (int)oldDefTax, DecimalValue = oldDefTax }
                }, "selftest_km2");
                SystemConfig.Reload(db);
            }
        }
    }
}
