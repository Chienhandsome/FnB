using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void SalesManageListAndUpdateDbTests(string db, UserSession admin, SalesManageTestFixture fx)
        {
            var svc = new BillAdjustService(admin);
            List<PaidBillListItem> list = svc.LoadPaidBills(fx.BusinessDate, fx.BusinessDate);
            List<PaidBillListItem> mine = list.FindAll(x => x.Name != null && x.Name.StartsWith("QLBH-", StringComparison.Ordinal));
            bool onlyPaidSorted = mine.Count == 4
                && !mine.Exists(x => x.Id == fx.OpenBillId)
                && mine[0].Id == fx.Bill1Id && mine[1].Id == fx.Bill2Id && mine[2].Id == fx.Bill3Id && mine[3].Id == fx.GroupBillId;
            List<PaidBillListItem> past = svc.LoadPaidBills(fx.BusinessDate.AddDays(-10), fx.BusinessDate.AddDays(-5));
            bool dateFiltered = !past.Exists(x => x.Name != null && x.Name.StartsWith("QLBH-", StringComparison.Ordinal));

            Check("QLBH §1/§7 Danh sách trái chỉ nạp bill LOAI=0 DATHANHTOAN=30, sắp xếp theo NGAY/GIOTHANHTOAN, lọc khoảng ngày và loại bill đang mở",
                onlyPaidSorted && dateFiltered,
                "mine=" + mine.Count + ", dateFiltered=" + dateFiltered);

            // P1: sửa trong bộ nhớ không chạm DB; rollback toàn phần khi lỗi giữa transaction; commit 1 transaction đồng bộ dòng + XUATVATTU + TLUUVET và giữ USERTHANHTOANID/GIOTHANHTOAN/NGAY.
            PaidBillDetail detail1 = svc.LoadPaidBillDetail(fx.Bill1Id);
            string origPayUser = detail1.Payment.UserThanhToanId;
            DateTime? origPayTime = detail1.Payment.GioThanhToan;
            DateTime origDate = detail1.Header.Ngay.Date;

            ServiceOrderHeader working = BillAdjustService.CloneWorkingBill(detail1.Header);
            working.Lines[0].Quantity += 1m; // Bia: 2 -> 3 (150k)
            working.Lines[1].Quantity += 1m; // Cafe Pha: 1 -> 2 (60k) -> vật tư Bot Cafe: 2 -> 4
            working.Note = "Da sua QLBH";
            OrderModePolicy.RecalcBillTotalsInPlace(working, SystemConfig.LamTronTien);

            decimal dbGrandBeforeCommit = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null, "SELECT TONGCONG FROM TDONHANG WHERE ID = @I", "@I", fx.Bill1Id)));
            bool dbUntouchedBeforeCommit = dbGrandBeforeCommit == 130000m;

            var reqBase = new BillUpdateRequest
            {
                Mode = BillManageMode.Manage,
                WorkingBill = working,
                OriginalDate = origDate,
                AllowDateChange = false,
                CashTendered = 210000m,
                PendingLogs = new List<ActivityLogRequest>
                {
                    new ActivityLogRequest { Kind = ActivityKind.TangMon, BillId = working.Id, TableName = working.TableName, Note = "Tăng 1 Bia & 1 Cafe Pha" }
                }
            };

            bool caughtRollback = false;
            try
            {
                reqBase.TestHookAfterUpdate = delegate { throw new InvalidOperationException("Simulated P1 failure"); };
                svc.UpdatePaidBill(reqBase, admin);
            }
            catch (InvalidOperationException)
            {
                caughtRollback = true;
            }
            reqBase.TestHookAfterUpdate = null;

            decimal dbGrandAfterRollback = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null, "SELECT TONGCONG FROM TDONHANG WHERE ID = @I", "@I", fx.Bill1Id)));
            PaymentExecutionResult updateRes = svc.UpdatePaidBill(reqBase, admin);

            decimal dbGrandAfter = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null, "SELECT TONGCONG FROM TDONHANG WHERE ID = @I", "@I", fx.Bill1Id)));
            decimal matQtyAfter = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                "SELECT COALESCE(SUM(SLXUAT), 0) FROM TDONHANGCHITIET WHERE TDONHANGID = @I AND XUATVATTU = 30 AND STATUS <> -30", "@I", fx.Bill1Id)));
            PaidBillDetail reloaded1 = svc.LoadPaidBillDetail(fx.Bill1Id);
            int logCountManage = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @C", "@I", working.Id, "@C", ActivityConstants.FuncQuanLyBanHang)));

            bool metaPreserved = reloaded1.Payment.UserThanhToanId == origPayUser
                && reloaded1.Payment.GioThanhToan.HasValue && origPayTime.HasValue
                && Math.Abs((reloaded1.Payment.GioThanhToan.Value - origPayTime.Value).TotalSeconds) < 2
                && reloaded1.Header.Ngay.Date == origDate;

            Check("QLBH §2.1/§2.2/§5 P1/§7 Sửa trong bộ nhớ không đổi DB, rollback toàn phần khi lỗi, commit 1 transaction cập nhật dòng/XUATVATTU/TLUUVET và giữ nguyên USERTHANHTOANID/GIOTHANHTOAN/NGAY",
                dbUntouchedBeforeCommit && caughtRollback && dbGrandAfterRollback == 130000m
                && updateRes.Ok && dbGrandAfter == 210000m && matQtyAfter == 4m && metaPreserved && logCountManage >= 1,
                "ok=" + updateRes.Ok + ", grand=" + dbGrandAfter + ", mat=" + matQtyAfter + ", meta=" + metaPreserved + ", logs=" + logCountManage);
        }

        private static void SalesManageStockDebtAndAdjustDbTests(string db, ConfigRepository cfgRepo, UserSession admin, SalesManageTestFixture fx)
        {
            var svc = new BillAdjustService(admin);
            PaidBillDetail detail1 = svc.LoadPaidBillDetail(fx.Bill1Id);

            // 1. Âm kho (§6.6): ChoPhepAmKho = 0 chặn vượt tồn (tồn ProdLowStock = 2, đặt 5); ChoPhepAmKho = 30 cho phép.
            SetSalesManageConfigInt(db, cfgRepo, fx, "ChoPhepAmKho", 0);
            ServiceOrderHeader workStock = BillAdjustService.CloneWorkingBill(detail1.Header);
            workStock.Lines.Add(new ServiceOrderLine
            {
                Id = DbHelper.NewId(), OrderId = workStock.Id, ProductId = fx.ProdLowStockId, ItemName = "QLBH Ruou It",
                UnitId = fx.UnitId, WarehouseId = fx.WarehouseId, Quantity = 5m, UnitPrice = 40000m, ProductTypeId = ProductRules.TypeDual
            });
            OrderModePolicy.RecalcBillTotalsInPlace(workStock, SystemConfig.LamTronTien);
            PaymentExecutionResult stockBlocked = svc.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Manage, WorkingBill = workStock, OriginalDate = workStock.Ngay, CashTendered = workStock.TongCong
            }, admin);

            SetSalesManageConfigInt(db, cfgRepo, fx, "ChoPhepAmKho", 30);
            PaymentExecutionResult stockAllowed = svc.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Manage, WorkingBill = workStock, OriginalDate = workStock.Ngay, CashTendered = workStock.TongCong
            }, admin);
            // Đưa SL ProdLowStock về 1 (<= tồn 2) trước khi khóa lại ChoPhepAmKho = 0 cho các bước kiểm thử sau.
            workStock.Lines[workStock.Lines.Count - 1].Quantity = 1m;
            OrderModePolicy.RecalcBillTotalsInPlace(workStock, SystemConfig.LamTronTien);
            svc.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Manage, WorkingBill = workStock, OriginalDate = workStock.Ngay, CashTendered = workStock.TongCong
            }, admin);
            SetSalesManageConfigInt(db, cfgRepo, fx, "ChoPhepAmKho", 0);

            Check("QLBH §6.6/§7 Chặn âm kho khi ChoPhepAmKho=0 và cho phép khi ChoPhepAmKho=30",
                !stockBlocked.Ok && !string.IsNullOrEmpty(stockBlocked.ErrorMessage) && stockAllowed.Ok,
                "blk=" + stockBlocked.Ok + ", alw=" + stockAllowed.Ok + ", alwErr=" + (stockAllowed.ErrorMessage ?? ""));

            // 2. Khách nợ (§6.6) & Q1/P7 KHÔNG tự nâng/hạ hạng thành viên khi sửa bill đã thanh toán.
            SetSalesManageConfigInt(db, cfgRepo, fx, "ChoPhepKhachNo", 30);
            SetSalesManageConfigInt(db, cfgRepo, fx, "TuDongNangCapThanhVienKhiDatHanMuc", 30);
            PaidBillDetail dAfterStock = svc.LoadPaidBillDetail(fx.Bill1Id);
            ServiceOrderHeader workDebt = BillAdjustService.CloneWorkingBill(dAfterStock.Header);
            workDebt.CustomerId = "";
            workDebt.CustomerName = "";
            PaymentExecutionResult debtNoCust = svc.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Manage, WorkingBill = workDebt, OriginalDate = workDebt.Ngay, ConfirmDebt = true, CashTendered = 0m
            }, admin);

            workDebt.CustomerId = fx.Customer1Id;
            workDebt.CustomerName = "Khach Bac " + fx.Tag;
            PaymentExecutionResult debtWithCust = svc.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Manage, WorkingBill = workDebt, OriginalDate = workDebt.Ngay,
                CustomerId = fx.Customer1Id, ConfirmDebt = true, CashTendered = 100000m
            }, admin);

            string cust1GroupAfter = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT DNHOMKHACHHANGID FROM DKHACHHANG WHERE ID = @I", "@I", fx.Customer1Id)));
            decimal debtSaved = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null, "SELECT CONGNO FROM TDONHANG WHERE ID = @I", "@I", fx.Bill1Id)));

            Check("QLBH §6.6/§6.3 Q1 P7/§7 Chặn nợ không có khách trong DKHACHHANG, cho phép nợ khi có khách, và KHÔNG tự nâng hạng thành viên khi sửa bill cũ",
                !debtNoCust.Ok && debtWithCust.Ok && debtSaved > 0m && cust1GroupAfter == fx.CustGroupSilverId,
                "noCust=" + debtNoCust.Ok + ", ok=" + debtWithCust.Ok + ", debt=" + debtSaved + ", grpSame=" + (cust1GroupAfter == fx.CustGroupSilverId));

            // 3. Điều chỉnh hóa đơn: đổi khách Q4 + quyền ThayDoiKhachHangTrongDieuChinhHoaDon + Trả đồ (§2.3, GIOTRADO không hỏi mật khẩu, dòng âm, TLUUVET CHUCNANG = Điều chỉnh hóa đơn).
            SetSalesManageConfigInt(db, cfgRepo, fx, "NhapMatKhauGiamDo", 30);
            var permsAdjustNoCust = new FunctionPermissions(false);
            permsAdjustNoCust.Add(SalesManageConstants.FuncDieuChinhHoaDon, "Điều chỉnh hóa đơn", RoleModes.View | RoleModes.Edit);
            permsAdjustNoCust.Add(ServiceOrderConstants.FuncThanhToan, "Thanh toán", RoleModes.View | RoleModes.Edit);
            permsAdjustNoCust.Add(ServiceOrderConstants.FuncXoaGiamMon, "Xóa/Giảm món", RoleModes.View | RoleModes.Edit);
            var sessAdjustNoCust = new UserSession { UserId = "u-adj-1", UserName = "adj1", IsAdmin = false, DatabasePath = db, Permissions = permsAdjustNoCust };
            var svcAdjustNoCust = new BillAdjustService(sessAdjustNoCust);

            var permsAdjustWithCust = new FunctionPermissions(false);
            permsAdjustWithCust.Add(SalesManageConstants.FuncDieuChinhHoaDon, "Điều chỉnh hóa đơn", RoleModes.View | RoleModes.Edit);
            permsAdjustWithCust.Add(SalesManageConstants.FuncThayDoiKhachHangDieuChinh, "Thay đổi khách hàng", RoleModes.View | RoleModes.Edit);
            permsAdjustWithCust.Add(ServiceOrderConstants.FuncThanhToan, "Thanh toán", RoleModes.View | RoleModes.Edit);
            permsAdjustWithCust.Add(ServiceOrderConstants.FuncXoaGiamMon, "Xóa/Giảm món", RoleModes.View | RoleModes.Edit);
            var sessAdjustWithCust = new UserSession { UserId = "u-adj-2", UserName = "adj2", IsAdmin = false, DatabasePath = db, Permissions = permsAdjustWithCust };
            var svcAdjustWithCust = new BillAdjustService(sessAdjustWithCust);

            var permsNoReturn = new FunctionPermissions(false);
            permsNoReturn.Add(SalesManageConstants.FuncDieuChinhHoaDon, "Điều chỉnh hóa đơn", RoleModes.View | RoleModes.Edit);
            var svcNoReturn = new BillAdjustService(new UserSession { UserId = "u-adj-3", UserName = "adj3", IsAdmin = false, DatabasePath = db, Permissions = permsNoReturn });

            ServiceOpResult retBlockedNoPerm = svcNoReturn.RecordReturnTime(fx.Bill1Id);
            ServiceOpResult retTimeOk = svcAdjustWithCust.RecordReturnTime(fx.Bill1Id);
            bool gioTraDoWritten = DbHelper.Read(db, conn => DbHelper.Scalar(conn, null, "SELECT GIOTRADO FROM TDONHANG WHERE ID = @I", "@I", fx.Bill1Id) != null);

            PaidBillDetail dBeforeAdj = svcAdjustWithCust.LoadPaidBillDetail(fx.Bill1Id);
            ServiceOrderHeader workAdj = BillAdjustService.CloneWorkingBill(dBeforeAdj.Header);
            svcAdjustWithCust.ApplyCustomerChangeInMemory(workAdj, fx.Customer2Id, "Khach Vang " + fx.Tag);
            List<ReturnableItemRow> retRows = ReturnItemRules.BuildReturnableGroups(workAdj.Lines);
            ReturnableItemRow beerRow = retRows.Find(r => r.ProductId == fx.ProdNormalId);
            ReturnItemRules.ApplyReturnQty(beerRow, 1m);
            workAdj.Lines.Add(ReturnItemRules.CreateReturnLine(workAdj.Id, beerRow, 1m, DbHelper.NewId(), DbHelper.NewId()));
            OrderModePolicy.RecalcBillTotalsInPlace(workAdj, SystemConfig.LamTronTien);

            // Người không có quyền đổi khách trong Điều chỉnh -> bị chặn.
            PaymentExecutionResult adjCustDenied = svcAdjustNoCust.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Adjust, WorkingBill = workAdj, OriginalDate = workAdj.Ngay,
                CustomerId = fx.Customer2Id, CashTendered = workAdj.TongCong
            }, sessAdjustNoCust);

            // Người có quyền đổi khách + trả đồ -> thành công (kèm CK 50.000 + tài khoản để kiểm PaymentDialog nạp sẵn ở bước UI).
            decimal adjTransfer = workAdj.TongCong >= 50000m ? 50000m : 0m;
            PaymentExecutionResult adjOk = svcAdjustWithCust.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Adjust, WorkingBill = workAdj, OriginalDate = workAdj.Ngay,
                CustomerId = fx.Customer2Id, Transfer = adjTransfer, AccountId = adjTransfer > 0m ? fx.BankId : null,
                CashTendered = Math.Max(0m, workAdj.TongCong - adjTransfer),
                PendingLogs = new List<ActivityLogRequest>
                {
                    new ActivityLogRequest { Kind = ActivityKind.TraDo, BillId = workAdj.Id, TableName = workAdj.TableName, Note = "Trả 1 Bia" }
                }
            }, sessAdjustWithCust);

            int negLineCount = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I AND SLXUAT < 0 AND STATUS <> -30", "@I", fx.Bill1Id)));
            int logCountAdjust = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @C", "@I", workAdj.Id, "@C", ActivityConstants.FuncDieuChinhHoaDon)));

            Check("QLBH §2.3/§6.3 Q4/§7 Trả đồ ghi GIOTRADO theo quyền XoaGiamMon (không hỏi mật khẩu), ghi dòng âm, áp CK nhóm khách mới, chặn đổi khách khi thiếu quyền, ghi TLUUVET đúng CHUCNANG",
                !retBlockedNoPerm.Ok && retBlockedNoPerm.NeedsReLogin && retTimeOk.Ok && gioTraDoWritten
                && !adjCustDenied.Ok && adjCustDenied.NeedsReLogin
                && adjOk.Ok && negLineCount == 1 && logCountAdjust >= 1,
                "retPerm=" + retBlockedNoPerm.NeedsReLogin + ", gio=" + gioTraDoWritten + ", custDeny=" + adjCustDenied.NeedsReLogin + ", adjOk=" + adjOk.Ok + ", neg=" + negLineCount);
        }

        private static void SalesManageDepositAndCancelDbTests(string db, ConfigRepository cfgRepo, UserSession admin, SalesManageTestFixture fx)
        {
            var svc = new BillAdjustService(admin);

            // 1. §6.4 Đặt trước dư & cảnh báo phiếu chi trả đặt trước đã lập trước đó (Bill2: tổng 300k -> giảm còn 250k, cọc 400k + cọc xóa mềm 500k bị bỏ qua, đã chi trước 100k).
            PaidBillDetail d2 = svc.LoadPaidBillDetail(fx.Bill2Id);
            ServiceOrderHeader work2 = BillAdjustService.CloneWorkingBill(d2.Header);
            work2.Lines[0].Quantity = 5m; // 5 x 50k = 250k
            OrderModePolicy.RecalcBillTotalsInPlace(work2, SystemConfig.LamTronTien);
            PaymentExecutionResult res2 = svc.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Manage, WorkingBill = work2, OriginalDate = work2.Ngay,
                CustomerId = d2.Header.CustomerId, CashTendered = 0m
            }, admin);

            Check("QLBH §6.4/§7 Bill đặt trước 400k (bỏ cọc xóa mềm 500k), đã chi trước 100k, sửa bill xuống 250k -> ExcessDeposit=150k và cảnh báo rõ đã chi trước 100.000",
                res2.Ok && res2.ExcessDeposit == 150000m
                && res2.AlreadyRefundedDeposit == 100000m
                && !string.IsNullOrEmpty(res2.RefundDepositWarning)
                && res2.RefundDepositWarning.Contains("100.000"),
                "ok=" + res2.Ok + ", excess=" + res2.ExcessDeposit + ", prev=" + res2.AlreadyRefundedDeposit + ", err=" + (res2.ErrorMessage ?? ""));

            // 2. §6.3 Bill thanh toán chung (NHOMGUID != '') bị chặn sửa/trả đồ/cập nhật/hủy ở tầng Service.
            PaidBillDetail dg = svc.LoadPaidBillDetail(fx.GroupBillId);
            PaymentDialogContext grpPrep = svc.PrepareUpdatePayment(dg.Header, BillManageMode.Manage);
            PaymentExecutionResult grpUpd = svc.UpdatePaidBill(new BillUpdateRequest
            {
                Mode = BillManageMode.Manage, WorkingBill = dg.Header, OriginalDate = dg.Header.Ngay, CashTendered = 50000m
            }, admin);
            ServiceOpResult grpRet = svc.RecordReturnTime(fx.GroupBillId);
            BillCancelPaidResult grpCan = svc.CancelPaidBill(new BillCancelPaidRequest { BillId = fx.GroupBillId, ConfirmRelatedVouchers = true }, admin);

            Check("QLBH §6.3/§7 Bill thanh toán chung bị chặn tuyệt đối ở PrepareUpdatePayment, UpdatePaidBill, RecordReturnTime và CancelPaidBill",
                !grpPrep.Ok && !grpUpd.Ok && !grpRet.Ok && !grpCan.Ok
                && grpUpd.ErrorMessage == SalesManageConstants.GroupBillEditBlocked
                && grpRet.ErrorMessage == SalesManageConstants.GroupBillReturnBlocked
                && grpCan.ErrorMessage == SalesManageConstants.GroupBillCancelBlocked,
                "prep=" + grpPrep.Ok + ", upd=" + (grpUpd.ErrorMessage ?? "") + ", ret=" + (grpRet.ErrorMessage ?? "") + ", can=" + (grpCan.ErrorMessage ?? ""));

            // 3. §2.4 Hủy bill đã thanh toán (Bill3): kiểm quyền, cảnh báo TTHUCHI liên quan, lý do bắt buộc, rollback, P4 TIENGIAMGIA bằng tiền, giữ TTHUCHI, xóa cứng TDONHANG/CT/TINCHEBIEN, ghi TLUUVET.
            SetSalesManageConfigInt(db, cfgRepo, fx, "NhapLyDoKhiHuyHoaDon", 30);
            var svcNoCancel = new BillAdjustService(new UserSession
            {
                UserId = "u-nocancel", UserName = "nocancel", IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            });
            BillCancelPaidResult canNoPerm = svcNoCancel.CancelPaidBill(new BillCancelPaidRequest { BillId = fx.Bill3Id, Reason = "Test" }, svcNoCancel.Session);
            BillCancelPaidResult canWarnVoucher = svc.CancelPaidBill(new BillCancelPaidRequest { BillId = fx.Bill3Id, Reason = "Khach huy", ConfirmRelatedVouchers = false }, admin);
            BillCancelPaidResult canNoReason = svc.CancelPaidBill(new BillCancelPaidRequest { BillId = fx.Bill3Id, Reason = "", ConfirmRelatedVouchers = true }, admin);

            bool caughtCancelRollback = false;
            try
            {
                svc.CancelPaidBill(new BillCancelPaidRequest
                {
                    BillId = fx.Bill3Id, Reason = "Test rollback", ConfirmRelatedVouchers = true,
                    TestHookAfterSnapshot = delegate { throw new InvalidOperationException("Simulated cancel fail"); }
                }, admin);
            }
            catch (InvalidOperationException)
            {
                caughtCancelRollback = true;
            }
            int bill3AfterRb = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", fx.Bill3Id)));

            BillCancelPaidResult canOk = svc.CancelPaidBill(new BillCancelPaidRequest
            {
                BillId = fx.Bill3Id, Reason = "Khach doi y", ConfirmRelatedVouchers = true
            }, admin);
            if (canOk.Ok && !string.IsNullOrEmpty(canOk.CancelledBillHuyId))
                fx.CreatedCancelIds.Add(canOk.CancelledBillHuyId);

            int bill3Left = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", fx.Bill3Id)));
            int lines3Left = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", fx.Bill3Id)));
            int kitchen3Left = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", fx.Bill3Id)));
            int voucher3Left = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TTHUCHI WHERE ID = @I AND STATUS = 30", "@I", fx.Bill3RelatedVoucherId)));
            decimal cancelDiscMoney = string.IsNullOrEmpty(canOk.CancelledBillHuyId) ? -1m : DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                "SELECT TIENGIAMGIA FROM TDONHANGHUY WHERE ID = @I", "@I", canOk.CancelledBillHuyId)));
            int cancelDetailCount = string.IsNullOrEmpty(canOk.CancelledBillHuyId) ? -1 : DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TDONHANGHUYCHITIET WHERE TDONHANGHUYID = @I", "@I", canOk.CancelledBillHuyId)));
            int cancelLogCount = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I AND CHUCNANG = @C", "@I", fx.Bill3Id, "@C", ActivityConstants.FuncQuanLyBanHang)));

            Check("QLBH §2.4/§5 P4/§6.3/§7 Hủy bill đã thanh toán: kiểm quyền, cảnh báo TTHUCHI (giữ nguyên phiếu thu chi), bắt lý do, rollback sạch, xóa cứng bill/chi tiết/bếp, snapshot TDONHANGHUY (TIENGIAMGIA bằng tiền = 20.000, không chép vật tư/combo con) & ghi TLUUVET",
                !canNoPerm.Ok && canNoPerm.NeedsReLogin
                && !canWarnVoucher.Ok && canWarnVoucher.NeedsVoucherWarningConfirmation
                && !canNoReason.Ok
                && caughtCancelRollback && bill3AfterRb == 1
                && canOk.Ok && bill3Left == 0 && lines3Left == 0 && kitchen3Left == 0
                && voucher3Left == 1 && cancelDiscMoney == 20000m && cancelDetailCount == 1 && cancelLogCount >= 1,
                "ok=" + canOk.Ok + ", left=" + bill3Left + ", vch=" + voucher3Left + ", discMoney=" + cancelDiscMoney + ", det=" + cancelDetailCount);
        }
    }
}
