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
        private sealed class LoyaltyConfigBackup
        {
            public bool Existed;
            public int OldIntValue;
            public string CreatedId;
        }

        private static void LoyaltyTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_tt",
                    UserName = "selftest_tt",
                    DisplayName = "Selftest ThanThiet",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                LoyaltyPureRuleTests();
                LoyaltyFormulaAndCrudDbTests(db);
                LoyaltyAutoUpgradeAndEdgeDbTests(db);
                LoyaltyUiAndMenuTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void LoyaltyPureRuleTests()
        {
            // Spec §4 #1 (công thức thuần): ban đầu 10, HĐ DIEM 5+7, DIEMGIAM 2, tặng 20 -> 40.
            decimal ptsMode1 = LoyaltyRules.ComputePoints(10m, 240000m, 12m, 2m, 20m, 1, 20000);
            decimal ptsMode0A = LoyaltyRules.ComputePoints(10m, 240000m, 12m, 2m, 20m, 0, 10000); // 10 + (24 - 2) + 20 = 52
            decimal ptsMode0ZeroDiv = LoyaltyRules.ComputePoints(10m, 240000m, 12m, 2m, 20m, 0, 0); // 10 + (0 - 2) + 20 = 28

            // Spec §4 #3: kiểm tra đầu vào phiếu tặng/giảm điểm.
            LoyaltySaveResult rNoNum = LoyaltyRules.ValidateVoucher(new LoyaltyPointVoucherRow { Number = " ", CustomerId = "C1", GiftPoints = 5m, Reason = "Lý do" });
            LoyaltySaveResult rNoCust = LoyaltyRules.ValidateVoucher(new LoyaltyPointVoucherRow { Number = "TD26/00001", CustomerId = "", GiftPoints = 5m, Reason = "Lý do" });
            LoyaltySaveResult rNoReason = LoyaltyRules.ValidateVoucher(new LoyaltyPointVoucherRow { Number = "TD26/00001", CustomerId = "C1", GiftPoints = 5m, Reason = "  " });
            LoyaltySaveResult rBothZero = LoyaltyRules.ValidateVoucher(new LoyaltyPointVoucherRow { Number = "TD26/00001", CustomerId = "C1", GiftPoints = 0m, ReducePoints = 0m, Reason = "Lý do" });
            LoyaltySaveResult rNeg = LoyaltyRules.ValidateVoucher(new LoyaltyPointVoucherRow { Number = "TD26/00001", CustomerId = "C1", GiftPoints = -1m, ReducePoints = 0m, Reason = "Lý do" });
            LoyaltySaveResult rDecFloorZero = LoyaltyRules.ValidateVoucher(new LoyaltyPointVoucherRow { Number = "TD26/00001", CustomerId = "C1", GiftPoints = 0.9m, ReducePoints = 0m, Reason = "Lý do" });
            LoyaltySaveResult rTooBig = LoyaltyRules.ValidateVoucher(new LoyaltyPointVoucherRow { Number = "TD26/00001", CustomerId = "C1", GiftPoints = 1000000000m, ReducePoints = 0m, Reason = "Lý do" });
            LoyaltySaveResult rValid = LoyaltyRules.ValidateVoucher(new LoyaltyPointVoucherRow { Number = "TD26/00001", CustomerId = "C1", GiftPoints = 5.8m, ReducePoints = 0m, Reason = "Tặng điểm" });

            // Spec §4 #4: chặn giảm vượt điểm hiện có, cho giảm bằng đúng điểm hiện có.
            string overErr = LoyaltyRules.ValidateReduceWithinBalance(40m, 0m, 41m);
            string exactOk = LoyaltyRules.ValidateReduceWithinBalance(40m, 0m, 40m);
            string withGiftOk = LoyaltyRules.ValidateReduceWithinBalance(10m, 5m, 15m);

            Check("Thân thiết §4 #1/#3/#4 (luật thuần): công thức 2 chế độ CachTinhDiem & chia 0, kiểm tra đầu vào & chặn giảm vượt điểm",
                ptsMode1 == 40m && ptsMode0A == 52m && ptsMode0ZeroDiv == 28m
                && !rNoNum.Ok && rNoNum.HasNumberError
                && !rNoCust.Ok && rNoCust.HasCustomerError
                && !rNoReason.Ok && rNoReason.HasReasonError
                && !rBothZero.Ok && rBothZero.HasPointsError
                && !rNeg.Ok && rNeg.HasPointsError
                && !rDecFloorZero.Ok && !rTooBig.Ok && rValid.Ok
                && LoyaltyRules.NormalizePointInput(5.8m) == 5m
                && overErr == LoyaltyConstants.ReduceExceedsPoints(40m)
                && exactOk == null && withGiftOk == null,
                SingleLine("m1=" + ptsMode1 + ", m0=" + ptsMode0A + ", div0=" + ptsMode0ZeroDiv + ", over=" + overErr));
        }

        private static void LoyaltyFormulaAndCrudDbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupLoyaltyConfigs(db, new[]
            {
                "CachTinhDiem", "DoanhSoTuongUngVoi1Diem", "TuDongNangCapThanhVienKhiDatHanMuc", "KichHoatLuuVetHoatDong"
            });

            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string custId = DbHelper.NewId();
            var orderIds = new List<string>();
            var voucherIds = new List<string>();

            try
            {
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "CachTinhDiem", 1);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "DoanhSoTuongUngVoi1Diem", 20000);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "TuDongNangCapThanhVienKhiDatHanMuc", 0);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "KichHoatLuuVetHoatDong", 30);

                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_tt')",
                    "@I", storeId, "@N", "CH TT " + tag);
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIACHI, DIENTHOAI, EMAIL, DIEMTICHLUYBANDAU, NOTE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '10 Lê Duẩn', '0909888777', 'tt@kazuko.vn', 10, 'Ghi chú TT', 30, 'selftest_tt', @T)",
                    "@I", custId, "@C", "KHTT" + tag, "@N", "Khách Thân Thiết " + tag, "@T", now);

                // Spec §4 #1: 2 HĐ đã thanh toán LOAI=0 (DIEM 5 & 7, DIEMGIAM 2 ở HĐ sau; TONGCONG 100k & 140k = 240k),
                // 1 HĐ chưa thanh toán (DATHANHTOAN=0) và 1 phiếu nhập (LOAI=1) KHÔNG được tính.
                orderIds.Add(InsertLoyaltyOrder(db, "HD-TT1-" + tag, 0, 30, 30, custId, storeId, 100000m, 5m, 0m, DateTime.Today.AddDays(-2)));
                orderIds.Add(InsertLoyaltyOrder(db, "HD-TT2-" + tag, 0, 30, 30, custId, storeId, 140000m, 7m, 2m, DateTime.Today.AddDays(-1)));
                orderIds.Add(InsertLoyaltyOrder(db, "HD-TT-UNP-" + tag, 0, 30, 0, custId, storeId, 500000m, 25m, 0m, DateTime.Today));
                orderIds.Add(InsertLoyaltyOrder(db, "PN-TT-L1-" + tag, 1, 30, 30, custId, storeId, 300000m, 15m, 0m, DateTime.Today));

                var svc = new LoyaltyService(AppSession.Current);

                // Tạo phiếu tặng 20 điểm với số phiếu tự sinh TDyy/00000.
                string autoNum1 = svc.GenerateNumber(DateTime.Today);
                LoyaltySaveResult gift20 = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = autoNum1,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    GiftPoints = 20m,
                    ReducePoints = 0m,
                    Reason = "Tặng điểm khuyến mãi " + tag,
                    Note = "Phiếu 20 điểm"
                });
                if (gift20.Ok && !string.IsNullOrEmpty(gift20.SavedId))
                    voucherIds.Add(gift20.SavedId);

                string autoNum2 = svc.GenerateNumber(DateTime.Today);
                string prefix = DocumentNumber.LiteralPrefix(LoyaltyConstants.DefaultNumberTemplate, DateTime.Today);
                int seq1 = DocumentNumber.ExtractSequence(autoNum1, prefix);
                int seq2 = DocumentNumber.ExtractSequence(autoNum2, prefix);

                // Kiểm công thức §4 #1 với CachTinhDiem = 1: Doanh số 240.000, Số HĐ 2, Điểm = 10 + (5 + 7 - 2) + 20 = 40.
                LoyaltyCustomerRow rowMode1 = svc.LoadCustomers().Find(r => r.Id == custId);
                LoyaltyCustomerDetail det = svc.LoadCustomerDetail(custId);

                // Đổi CachTinhDiem = 0 (khác 1) & DoanhSoTuongUngVoi1Diem = 10000 -> 10 + (240000/10000 - 2) + 20 = 52.
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "CachTinhDiem", 0);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "DoanhSoTuongUngVoi1Diem", 10000);
                LoyaltyCustomerRow rowMode0 = svc.LoadCustomers().Find(r => r.Id == custId);

                // Đổi DoanhSoTuongUngVoi1Diem = 0 -> phần chia coi là 0 -> 10 + (0 - 2) + 20 = 28.
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "DoanhSoTuongUngVoi1Diem", 0);
                LoyaltyCustomerRow rowDiv0 = svc.LoadCustomers().Find(r => r.Id == custId);

                // Quay lại CachTinhDiem = 1.
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "CachTinhDiem", 1);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "DoanhSoTuongUngVoi1Diem", 20000);

                Check("Thân thiết §4 #1 (công thức DB số cụ thể): ban đầu 10 + HĐ (5+7-2) + tặng 20 = 40 (CachTinhDiem=1), 52 (mode 0, 10k/điểm), 28 (chia 0); Doanh số 240k, 2 HĐ",
                    gift20.Ok && rowMode1 != null
                    && rowMode1.Revenue == 240000m && rowMode1.BillCount == 2 && rowMode1.Points == 40m
                    && det.Bills.Count == 2 && det.Vouchers.Count == 1
                    && rowMode0 != null && rowMode0.Points == 52m
                    && rowDiv0 != null && rowDiv0.Points == 28m,
                    SingleLine("m1=" + (rowMode1 == null ? -1 : rowMode1.Points) + ", rev=" + (rowMode1 == null ? -1 : rowMode1.Revenue)
                        + ", cnt=" + (rowMode1 == null ? -1 : rowMode1.BillCount) + ", m0=" + (rowMode0 == null ? -1 : rowMode0.Points)
                        + ", div0=" + (rowDiv0 == null ? -1 : rowDiv0.Points)));

                // Spec §4 #3: trùng số phiếu bị chặn; số phiếu tự sinh tăng đúng.
                LoyaltySaveResult dupNumRes = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = autoNum1,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    GiftPoints = 1m,
                    Reason = "Trùng số"
                });
                Check("Thân thiết §4 #3 (số phiếu tự sinh & chặn trùng): TDyy/00000 tăng liên tiếp, chặn trùng số phiếu",
                    autoNum1.StartsWith(prefix, StringComparison.Ordinal) && seq1 >= 1 && seq2 == seq1 + 1
                    && !dupNumRes.Ok && dupNumRes.HasNumberError && dupNumRes.ErrorMessage == LoyaltyConstants.NumberDuplicate(autoNum1),
                    SingleLine("n1=" + autoNum1 + ", n2=" + autoNum2 + ", dupErr=" + dupNumRes.ErrorMessage));

                // Spec §4 #4: giảm vượt điểm (41 > 40) bị chặn nêu còn 40 điểm; giảm đúng 40 được;
                // sửa phiếu giảm 40 -> 35 không tự chặn chính nó và giữ người/giờ tạo; xóa phiếu +20 làm điểm âm bị chặn; xóa phiếu -35 được.
                LoyaltySaveResult overReduce = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = "TD-OVER-" + tag,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    ReducePoints = 41m,
                    Reason = "Giảm vượt điểm"
                });

                LoyaltySaveResult exactReduce = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = autoNum2,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    ReducePoints = 40m,
                    Reason = "Đổi quà hết 40 điểm"
                });
                if (exactReduce.Ok && !string.IsNullOrEmpty(exactReduce.SavedId))
                    voucherIds.Add(exactReduce.SavedId);

                LoyaltyPointVoucherRow beforeEdit = svc.LoadVoucherById(exactReduce.SavedId);
                decimal ptsAfterReduce40 = svc.GetCustomerPoints(custId);

                // Sửa chính phiếu giảm 40 -> 35 (điểm hiện có đang là 0, nhưng bỏ chính phiếu này thì có 40 -> phải cho sửa!).
                beforeEdit.ReducePoints = 35m;
                beforeEdit.Reason = "Đổi quà chỉnh còn 35 điểm";
                LoyaltySaveResult editSelfOk = svc.SaveVoucher(beforeEdit);
                LoyaltyPointVoucherRow afterEdit = svc.LoadVoucherById(exactReduce.SavedId);
                decimal ptsAfterEdit35 = svc.GetCustomerPoints(custId);

                // Xóa phiếu tặng +20 sẽ khiến điểm thành 5 - 20 = -15 < 0 -> PHẢI CHẶN.
                LoyaltyDeleteResult delGiftBlocked = svc.DeleteVoucher(gift20.SavedId);
                bool giftStillExists = svc.LoadVoucherById(gift20.SavedId) != null;

                // Xóa phiếu giảm -35 sẽ khiến điểm thành 40 >= 0 -> ĐƯỢC PHÉP (xóa cứng).
                LoyaltyDeleteResult delReduceOk = svc.DeleteVoucher(exactReduce.SavedId);
                bool reduceDeleted = svc.LoadVoucherById(exactReduce.SavedId) == null;
                decimal ptsAfterDelReduce = svc.GetCustomerPoints(custId);

                Check("Thân thiết §4 #4 (chặn giảm vượt điểm, giảm bằng điểm hiện có, sửa không tự chặn, giữ audit tạo, chặn xóa làm âm, xóa cứng hợp lệ)",
                    !overReduce.Ok && overReduce.ErrorMessage == LoyaltyConstants.ReduceExceedsPoints(40m)
                    && exactReduce.Ok && ptsAfterReduce40 == 0m
                    && editSelfOk.Ok && ptsAfterEdit35 == 5m
                    && afterEdit != null && afterEdit.CreatedById == beforeEdit.CreatedById
                    && afterEdit.CreatedAt == beforeEdit.CreatedAt
                    && !string.IsNullOrEmpty(afterEdit.ModifiedById) && afterEdit.ModifiedAt.HasValue
                    && !delGiftBlocked.Ok && delGiftBlocked.ErrorMessage == LoyaltyConstants.DeleteWouldMakeNegative && giftStillExists
                    && delReduceOk.Ok && reduceDeleted && ptsAfterDelReduce == 40m,
                    SingleLine("over=" + overReduce.ErrorMessage + ", edit=" + editSelfOk.Ok + ", delBlk=" + delGiftBlocked.ErrorMessage + ", after=" + ptsAfterDelReduce));
            }
            finally
            {
                foreach (string vid in voucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTANGGIAMDIEM", vid);
                }
                foreach (string oid in orderIds)
                    SafeDeleteById(db, "TDONHANG", oid);
                SafeDeleteById(db, "DKHACHHANG", custId);
                SafeDeleteById(db, "DCUAHANG", storeId);
                RestoreLoyaltyConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static void LoyaltyAutoUpgradeAndEdgeDbTests(string db)
        {
            var cfgRepo = new ConfigRepository(db);
            var cfgBackups = BackupLoyaltyConfigs(db, new[]
            {
                "CachTinhDiem", "DoanhSoTuongUngVoi1Diem", "TuDongNangCapThanhVienKhiDatHanMuc", "KichHoatLuuVetHoatDong"
            });

            string tag = DbHelper.NewId().Substring(0, 8);
            string silverGroupId = null;
            string goldGroupId = null;
            string custId = DbHelper.NewId();
            var voucherIds = new List<string>();

            try
            {
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "CachTinhDiem", 1);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "DoanhSoTuongUngVoi1Diem", 20000);
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "KichHoatLuuVetHoatDong", 30);

                var grpRepo = new CustomerGroupRepository(db);
                string silverName = "Hạng Bạc TT " + tag;
                string goldName = "Hạng Vàng TT " + tag;
                silverGroupId = grpRepo.Save(new CustomerGroupRow { Name = silverName, LoyaltyPoints = 999800m }, "selftest_tt").SavedId;
                goldGroupId = grpRepo.Save(new CustomerGroupRow { Name = goldName, LoyaltyPoints = 999900m }, "selftest_tt").SavedId;

                string custName = "Khách Nâng Hạng " + tag;
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, DIEMTICHLUYBANDAU, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, 999750, 30, 'selftest_tt', @T)",
                    "@I", custId, "@C", "KHNH" + tag, "@N", custName, "@T", DbHelper.Now());

                var svc = new LoyaltyService(AppSession.Current);

                // Spec §4 #5a: Cấu hình tắt -> tặng điểm đạt ngưỡng KHÔNG nâng nhóm.
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "TuDongNangCapThanhVienKhiDatHanMuc", 0);
                LoyaltySaveResult resOff = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = "TD-OFF-" + tag,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    GiftPoints = 60m,
                    Reason = "Đạt ngưỡng khi cấu hình tắt"
                });
                if (resOff.Ok) voucherIds.Add(resOff.SavedId);
                string grpWhenOff = ReadCustomerGroupId(db, custId);
                if (resOff.Ok) svc.DeleteVoucher(resOff.SavedId);

                // Spec §4 #5b: Cấu hình bật -> tặng điểm đạt 999810 (>= 999800) tự nâng ĐÚNG MỘT LẦN lên Hạng Bạc.
                SetLoyaltyConfigInt(db, cfgRepo, cfgBackups, "TuDongNangCapThanhVienKhiDatHanMuc", 30);
                LoyaltySaveResult resUp1 = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = "TD-UP1-" + tag,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    GiftPoints = 60m,
                    Reason = "Tặng đạt ngưỡng Bạc"
                });
                if (resUp1.Ok) voucherIds.Add(resUp1.SavedId);
                string grpAfterUp1 = ReadCustomerGroupId(db, custId);

                // Tặng thêm 10 điểm (999820 < 999900) -> không báo nâng lại.
                LoyaltySaveResult resUp2 = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = "TD-UP2-" + tag,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    GiftPoints = 10m,
                    Reason = "Tặng thêm chưa tới Vàng"
                });
                if (resUp2.Ok) voucherIds.Add(resUp2.SavedId);
                string grpAfterUp2 = ReadCustomerGroupId(db, custId);

                // Giảm 50 điểm (999770 < 999800) -> KHÔNG hạ nhóm.
                LoyaltySaveResult resDrop = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = "TD-DROP-" + tag,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    ReducePoints = 50m,
                    Reason = "Giảm điểm kiểm không hạ nhóm"
                });
                if (resDrop.Ok) voucherIds.Add(resDrop.SavedId);
                string grpAfterDrop = ReadCustomerGroupId(db, custId);

                // Đặt khách sẵn ở nhóm cao hơn (Hạng Vàng 999900), tặng +40 điểm (tổng 999810, dưới Vàng) -> không hạ xuống Bạc.
                BookingExec(db, "UPDATE DKHACHHANG SET DNHOMKHACHHANGID = @G WHERE ID = @I", "@G", goldGroupId, "@I", custId);
                LoyaltySaveResult resHigher = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = "TD-HI-" + tag,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    GiftPoints = 40m,
                    Reason = "Nhóm hiện tại đã cao hơn"
                });
                if (resHigher.Ok) voucherIds.Add(resHigher.SavedId);
                string grpAfterHigher = ReadCustomerGroupId(db, custId);

                Check("Thân thiết §4 #5 (tự nâng nhóm bằng hàm thanh toán): tắt không nâng, bật nâng đúng 1 lần kèm thông báo, giảm điểm không hạ, nhóm cao hơn không đổi",
                    resOff.Ok && string.IsNullOrEmpty(grpWhenOff) && resOff.UpgradedGroupName == null
                    && resUp1.Ok && grpAfterUp1 == silverGroupId
                    && resUp1.UpgradeNotice == LoyaltyConstants.UpgradedGroupMessage(custName, silverName)
                    && resUp2.Ok && grpAfterUp2 == silverGroupId && resUp2.UpgradedGroupName == null
                    && resDrop.Ok && grpAfterDrop == silverGroupId
                    && resHigher.Ok && grpAfterHigher == goldGroupId && resHigher.UpgradedGroupName == null,
                    SingleLine("off=" + grpWhenOff + ", up1=" + resUp1.UpgradedGroupName + ", drop=" + grpAfterDrop + ", hi=" + grpAfterHigher));

                // Spec §4 #6a: Lý do đúng 255 ký tự không làm lưu vết TLUUVET tràn.
                string reason255 = new string('R', 255);
                string note255 = new string('N', 255);
                LoyaltySaveResult res255 = svc.SaveVoucher(new LoyaltyPointVoucherRow
                {
                    Number = "TD-255-" + tag,
                    Date = DateTime.Today,
                    CustomerId = custId,
                    GiftPoints = 5m,
                    Reason = reason255,
                    Note = note255
                });
                if (res255.Ok) voucherIds.Add(res255.SavedId);

                LoyaltyPointVoucherRow back255 = res255.Ok ? svc.LoadVoucherById(res255.SavedId) : null;
                string logNote255 = res255.Ok
                    ? DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null,
                        "SELECT FIRST 1 NOTE FROM TLUUVET WHERE SODONHANG = @I", "@I", res255.SavedId)))
                    : null;

                // Spec §4 #6b: Rollback bằng hook không để lại phiếu, không để lại lưu vết, không nâng nhóm.
                BookingExec(db, "UPDATE DKHACHHANG SET DNHOMKHACHHANGID = NULL WHERE ID = @I", "@I", custId);
                string rbNum = "TD-RB-" + tag;
                bool rbCaught = false;
                try
                {
                    svc.SaveVoucher(new LoyaltyPointVoucherRow
                    {
                        Number = rbNum,
                        Date = DateTime.Today,
                        CustomerId = custId,
                        GiftPoints = 200m,
                        Reason = "Kiểm rollback transaction"
                    }, delegate { throw new InvalidOperationException("Giả lập lỗi giữa transaction tặng điểm"); });
                }
                catch (InvalidOperationException)
                {
                    rbCaught = true;
                }
                int rbVoucherCount = BookingCount(db, "SELECT COUNT(*) FROM TTANGGIAMDIEM WHERE NAME = @N", "@N", rbNum);
                int rbLogCount = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE BAN = @N", "@N", rbNum);
                string grpAfterRb = ReadCustomerGroupId(db, custId);

                Check("Thân thiết §4 #6 (ca biên 255 ký tự & rollback hook): lý do/ghi chú 255 ký tự ghi TTANGGIAMDIEM + TLUUVET không tràn; lỗi giữa transaction rollback sạch phiếu + lưu vết + nâng nhóm",
                    res255.Ok && back255 != null && back255.Reason.Length == 255 && back255.Note.Length == 255
                    && !string.IsNullOrEmpty(logNote255) && logNote255.Length <= 255
                    && rbCaught && rbVoucherCount == 0 && rbLogCount == 0 && string.IsNullOrEmpty(grpAfterRb),
                    SingleLine("ok255=" + res255.Ok + ", logLen=" + (logNote255 == null ? -1 : logNote255.Length)
                        + ", rb=" + rbCaught + "/" + rbVoucherCount + "/" + rbLogCount + "/" + grpAfterRb));
            }
            finally
            {
                foreach (string vid in voucherIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "TTANGGIAMDIEM", vid);
                }
                SafeDeleteById(db, "DKHACHHANG", custId);
                if (!string.IsNullOrEmpty(goldGroupId)) SafeDeleteById(db, "DNHOMKHACHHANG", goldGroupId);
                if (!string.IsNullOrEmpty(silverGroupId)) SafeDeleteById(db, "DNHOMKHACHHANG", silverGroupId);
                RestoreLoyaltyConfigs(db, cfgRepo, cfgBackups);
            }
        }

        private static string InsertLoyaltyOrder(
            string db, string name, int loai, int status, int paidFlag,
            string customerId, string storeId,
            decimal total, decimal diem, decimal diemGiam, DateTime date)
        {
            string id = DbHelper.NewId();
            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, LOAI, STATUS, DATHANHTOAN, NGAY, DKHACHHANGID, DCUAHANGID, TONGCONG, DIEM, DIEMGIAM, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, @L, @S, @P, @D, @KH, @CH, @TC, @DIEM, @DG, 'selftest_tt', @T)",
                "@I", id, "@N", name, "@L", loai, "@S", status, "@P", paidFlag,
                "@D", date.Date, "@KH", customerId, "@CH", storeId,
                "@TC", total, "@DIEM", diem, "@DG", diemGiam, "@T", DbHelper.Now());
            return id;
        }

        private static string ReadCustomerGroupId(string db, string customerId)
        {
            return DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null,
                "SELECT DNHOMKHACHHANGID FROM DKHACHHANG WHERE ID = @I", "@I", customerId)));
        }

        private static Dictionary<string, LoyaltyConfigBackup> BackupLoyaltyConfigs(string db, string[] keys)
        {
            var map = new Dictionary<string, LoyaltyConfigBackup>(StringComparer.Ordinal);
            DbHelper.Read(db, conn =>
            {
                foreach (string k in keys)
                {
                    DataTable dt = DbHelper.Table(conn, null,
                        "SELECT FIRST 1 ID, INTVALUE FROM SCONFIG WHERE NAME = @N", "@N", k);
                    if (dt.Rows.Count > 0)
                        map[k] = new LoyaltyConfigBackup { Existed = true, OldIntValue = DbHelper.Int(dt.Rows[0]["INTVALUE"]) };
                    else
                        map[k] = new LoyaltyConfigBackup { Existed = false };
                }
                return 0;
            });
            return map;
        }

        private static void SetLoyaltyConfigInt(
            string db,
            ConfigRepository cfgRepo,
            Dictionary<string, LoyaltyConfigBackup> backups,
            string key,
            int intValue)
        {
            int updated = cfgRepo.Save(new[] { new ConfigChange { Name = key, DataType = 3, IntValue = intValue } }, "selftest_tt");
            if (updated == 0)
            {
                string id = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO SCONFIG (ID, NAME, DATATYPE, INTVALUE, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 3, @V, 30, 'selftest_tt', @T)",
                    "@I", id, "@N", key, "@V", intValue, "@T", DbHelper.Now());
                LoyaltyConfigBackup b;
                if (backups.TryGetValue(key, out b))
                    b.CreatedId = id;
            }
            SystemConfig.Reload(db);
        }

        private static void RestoreLoyaltyConfigs(
            string db,
            ConfigRepository cfgRepo,
            Dictionary<string, LoyaltyConfigBackup> backups)
        {
            var toRestore = new List<ConfigChange>();
            foreach (KeyValuePair<string, LoyaltyConfigBackup> kv in backups)
            {
                if (kv.Value.Existed)
                    toRestore.Add(new ConfigChange { Name = kv.Key, DataType = 3, IntValue = kv.Value.OldIntValue });
                else if (!string.IsNullOrEmpty(kv.Value.CreatedId))
                    SafeDeleteById(db, "SCONFIG", kv.Value.CreatedId);
            }
            if (toRestore.Count > 0)
                cfgRepo.Save(toRestore.ToArray(), "selftest_tt");
            SystemConfig.Reload(db);
        }
    }
}
