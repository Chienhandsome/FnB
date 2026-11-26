using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        // ------------------------------------------------------------------ C. Gộp bàn trên DB (§3.8, Q19, §4, §7)

        private static void ServiceOrderMergeTableDbTests(string db, ConfigRepository cfgRepo, UserSession admin, TransferTestFixture fx)
        {
            SetTransferConfigInt(db, cfgRepo, fx, "HeThongChayNhieuMayTram", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "ChoPhepThayDoiHoaDonSauKhiInTamTinh", 0);
            SetTransferConfigInt(db, cfgRepo, fx, "GiaMatHangKhiChuyenBan", 2);
            SetTransferConfigInt(db, cfgRepo, fx, "CoPhiDichVu", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "CoThueSuat", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "ChoPhepNhapGiamGia", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "KichHoatLuuVetHoatDong", 30);

            var svc = new TableService(admin);
            var noPerm = new TableService(new UserSession
            {
                UserId = "u-mrg-noperm",
                UserName = "mrg_noperm",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = new FunctionPermissions(false)
            });

            DateTime today = new InvoiceNumberRepository(db).GetServerTimestamp().Date;
            string tag = DbHelper.NewId().Substring(0, 5);
            string dstBookingId = DbHelper.NewId();
            fx.CreatedBookingIds.Add(dstBookingId);
            BookingExec(db,
                "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, GIODAT, DBANID, TENKHACH, STATUS, USERCREATEDID) " +
                "VALUES (@I, @G, @N, @D, @D, @D, @D, @T, 'Khach Dst', 30, 'selftest')",
                "@I", dstBookingId, "@G", DbHelper.NewId(), "@N", "DH-MG-" + tag, "@D", today, "@T", fx.TableA2Id);

            // Nguồn TableA1: 2x 30.000 = 60.000; Đích TableA2: 1x 50.000 = 50.000 (có TDATHANGID và NHOMGUID).
            ServiceOrderHeader bSrc = OpenTransferBill(svc, fx, fx.TableA1Id, fx.ProdDrinkId, 2m, null);
            ServiceOrderHeader bDst = OpenTransferBill(svc, fx, fx.TableA2Id, fx.ProdFoodId, 1m, dstBookingId);
            string dstGroupGuid = DbHelper.NewId();
            BookingExec(db, "UPDATE TDONHANG SET NHOMGUID = @G WHERE ID = @I", "@G", dstGroupGuid, "@I", bDst.Id);

            // 1. Chặn thiếu quyền, đích trống, bill nguồn từ đặt hàng, khác % giảm giá, bill đích đã in tạm tính.
            TableTransferResult rNoPerm = noPerm.MergeTable(new MergeTableRequest { SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id });
            TableTransferResult rEmptyDst = svc.MergeTable(new MergeTableRequest { SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableB1Id });
            TableTransferResult rFromBooking = svc.MergeTable(new MergeTableRequest { SourceBillId = bDst.Id, SourceTableId = fx.TableA2Id, TargetTableId = fx.TableA1Id });

            svc.UpdateBillFinancials(bDst.Id, fx.TableA2Id, 10m, 0m, false, 5m, 0m, false, 10m);
            TableTransferResult rDiffDisc = svc.MergeTable(new MergeTableRequest { SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id });

            svc.UpdateBillFinancials(bSrc.Id, fx.TableA1Id, 10m, 0m, false, 0m, 0m, false, 0m);
            BookingExec(db, "UPDATE TDONHANG SET SOLANINTAMTINH = 1 WHERE ID = @I", "@I", bDst.Id);
            TableTransferResult rDstTemp = svc.MergeTable(new MergeTableRequest { SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id });
            BookingExec(db, "UPDATE TDONHANG SET SOLANINTAMTINH = 0 WHERE ID = @I", "@I", bDst.Id);

            Check("SDDV GopBan §3.8/§4/§7 Điều kiện chặn: thiếu quyền, đích trống, nguồn từ đặt hàng, khác % giảm giá, đích in tạm tính",
                !rNoPerm.Ok && rNoPerm.NeedsReLogin && rNoPerm.RequiredFunctionId == ServiceOrderConstants.FuncGopBan
                && !rEmptyDst.Ok && rEmptyDst.ErrorMessage == ServiceTransferConstants.DestTableEmptyForMerge
                && !rFromBooking.Ok && rFromBooking.ErrorMessage == ServiceTransferConstants.MergeBlockedHasBooking
                && !rDiffDisc.Ok && rDiffDisc.ErrorMessage == ServiceTransferConstants.MergeBlockedDifferentDiscount
                && !rDstTemp.Ok && rDstTemp.ErrorMessage == ServiceTransferConstants.TableTempPrintedBlocked);

            // 2. Q19: Bill nguồn có phiếu thu tạm ứng còn hiệu lực -> từ chối gộp, không bill nào đổi; xóa mềm (STATUS=-30) -> cho gộp!
            string advId = DbHelper.NewId();
            fx.CreatedVoucherIds.Add(advId);
            BookingExec(db,
                "INSERT INTO TTHUCHI (ID, NAME, LOAI, NGAY, TIMECREATED, THU, CHI, TDONHANGID, DCUAHANGID, STATUS, USERCREATEDID) " +
                "VALUES (@I, @N, 0, @D, @D, 50000, 0, @B, @S, 30, 'selftest')",
                "@I", advId, "@N", "PT-TU-MG-" + tag, "@D", today, "@B", bSrc.Id, "@S", fx.StoreId);

            TableTransferResult rAdvBlocked = svc.MergeTable(new MergeTableRequest { SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id });
            int srcStillExists = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", bSrc.Id)));

            BookingExec(db, "UPDATE TTHUCHI SET STATUS = -30 WHERE ID = @I", "@I", advId);

            bool caughtMergeCrash = false;
            try
            {
                svc.MergeTable(new MergeTableRequest
                {
                    SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id,
                    TestHookAfterSnapshot = delegate { throw new InvalidOperationException("Simulated merge fail"); }
                });
            }
            catch (InvalidOperationException) { caughtMergeCrash = true; }
            int huyAfterCrash = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TDONHANGHUY WHERE TDONHANGID = @I", "@I", bSrc.Id)));

            BookingExec(db,
                "INSERT INTO TINCHEBIEN (ID, TDONHANGID, TENHANG, SOLUONG, STATUS, USERCREATEDID) VALUES (@I, @B, @N, 2, 30, 'selftest')",
                "@I", DbHelper.NewId(), "@B", bSrc.Id, "@N", bSrc.Lines[0].ItemName);

            TableTransferResult merged = svc.MergeTable(new MergeTableRequest { SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id });
            if (merged.Ok && !string.IsNullOrEmpty(merged.CancelledBillHuyId))
                fx.CreatedCancelIds.Add(merged.CancelledBillHuyId);

            int srcAfterMerge = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", bSrc.Id)));
            string srcTableBillAfter = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT TDONHANGID FROM DBAN WHERE ID = @I", "@I", fx.TableA1Id)));
            int dstKitchenCount = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", bDst.Id)));
            int huyAfterMerge = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANGHUY WHERE TDONHANGID = @I", "@I", bSrc.Id)));
            int logSrc = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @B AND PHANLOAI = @K", "@B", bSrc.Id, "@K", (int)ActivityKind.GopBan)));
            int logDst = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @B AND PHANLOAI = @K", "@B", bDst.Id, "@K", (int)ActivityKind.GopBan)));

            // Tính tay khớp OrderCalculator: TienHang=110k, Giam 10%=11k -> sauGiam=99k, Phi 5%=4.95k->5k -> 104k, Thue 10%=10.4k->10k -> TongCong=114k!
            ServiceOrderHeader t = merged.TargetBill;
            Check("SDDV GopBan §3.8/Q19/§7 Q19 chặn gộp khi có tạm ứng (xóa mềm cho gộp) + rollback sạch + sau gộp: nguồn xóa, TDONHANGHUY có, dòng+TINCHEBIEN chuyển, giữ TDATHANGID/NHOMGUID, OrderCalculator tính đúng 114.000, TLUUVET đủ 2 bill",
                !rAdvBlocked.Ok && rAdvBlocked.ErrorMessage != null && rAdvBlocked.ErrorMessage.Contains("tạm ứng") && srcStillExists == 1
                && caughtMergeCrash && huyAfterCrash == 0
                && merged.Ok && t != null
                && srcAfterMerge == 0 && string.IsNullOrEmpty(srcTableBillAfter)
                && t.Lines.Count == 2 && dstKitchenCount == 1 && huyAfterMerge == 1
                && t.BookingId == dstBookingId && t.GroupGuid == dstGroupGuid
                && t.TienHang == 110000m && t.TienGiamGia == 11000m && t.PhiDichVu == 5000m && t.TienThue == 10000m && t.TongCong == 114000m
                && logSrc >= 1 && logDst >= 1,
                t != null ? ("th=" + t.TienHang + ", tc=" + t.TongCong + ", lines=" + t.Lines.Count) : (merged.ErrorMessage ?? "null"));

            CleanupSingleBill(db, bDst.Id, fx.TableA2Id);
        }

        // ------------------------------------------------------------------ D. Chuyển mặt hàng sang bill khác (§1, §4, ban-khu-vuc.md §2)

        private static void ServiceOrderTransferItemDbTests(string db, ConfigRepository cfgRepo, UserSession admin, TransferTestFixture fx)
        {
            SetTransferConfigInt(db, cfgRepo, fx, "SuDungChucNangInXuongBep", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "InDoUong", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "InDoAn", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "ChoPhepThayDoiHoaDonSauKhiInTamTinh", 0);
            SetTransferConfigInt(db, cfgRepo, fx, "KichHoatLuuVetHoatDong", 30);

            var svc = new TableService(admin);
            var noPerm = new TableService(new UserSession
            {
                UserId = "u-trfi-noperm",
                UserName = "trfi_noperm",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = new FunctionPermissions(false)
            });

            ServiceOrderHeader bSrc = OpenTransferBill(svc, fx, fx.TableA1Id, fx.ProdDrinkId, 5m, null); // 5x 30k = 150k
            ServiceOrderLine drinkLine = bSrc.Lines[0];
            svc.ChangeLineNote(bSrc.Id, fx.TableA1Id, drinkLine.Id, "Ít đường");
            svc.ChangeLineDiscount(bSrc.Id, fx.TableA1Id, drinkLine.Id, 10m, 0m, false); // 10% -> 135k
            bSrc = svc.AddItem(bSrc.Id, fx.TableA1Id, fx.ProdComboId, 1m).Bill; // combo 55k + 1 dòng con SL=2 -> 190k
            ServiceOrderLine comboParent = bSrc.Lines.Find(l => l.ProductId == fx.ProdComboId && !l.IsComboChild);

            ServiceOrderHeader bDst = OpenTransferBill(svc, fx, fx.TableA2Id, fx.ProdFoodId, 1m, null); // 1x 50k = 50k

            // Chặn thiếu quyền và bàn đích trống.
            TableTransferResult rNoPerm = noPerm.TransferItem(new TransferItemRequest
            {
                SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id, LineId = drinkLine.Id, TransferQty = 2m
            });
            TableTransferResult rEmptyDst = svc.TransferItem(new TransferItemRequest
            {
                SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableB1Id, LineId = drinkLine.Id, TransferQty = 2m
            });

            // Ghi TINCHEBIEN: món uống đã in 4/5. Chuyển 2 -> còn 3 -> chênh lệch = 4 - 3 = 1 (-1 nguồn, +1 đích).
            BookingExec(db,
                "INSERT INTO TINCHEBIEN (ID, TDONHANGID, TENHANG, SOLUONG, STATUS, USERCREATEDID) VALUES (@I, @B, @N, 4, 30, 'selftest')",
                "@I", DbHelper.NewId(), "@B", bSrc.Id, "@N", drinkLine.ItemName);

            // Rollback khi lỗi giữa chừng.
            bool caughtItemCrash = false;
            try
            {
                svc.TransferItem(new TransferItemRequest
                {
                    SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id, LineId = drinkLine.Id, TransferQty = 2m,
                    TestHookBeforeCommit = delegate { throw new InvalidOperationException("Simulated item transfer fail"); }
                });
            }
            catch (InvalidOperationException) { caughtItemCrash = true; }
            ServiceOrderHeader srcAfterCrash = svc.LoadBill(bSrc.Id);

            // Tách chuyển một phần SL=2 sang bDst.
            TableTransferResult splitRes = svc.TransferItem(new TransferItemRequest
            {
                SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id, LineId = drinkLine.Id, TransferQty = 2m
            });

            decimal srcKitchenNet = DbHelper.Read(db, conn => ServiceOrderRepository.GetPrintedKitchenQtyIn(conn, null, bSrc.Id, drinkLine.ItemName));
            decimal dstKitchenNet = DbHelper.Read(db, conn => ServiceOrderRepository.GetPrintedKitchenQtyIn(conn, null, bDst.Id, drinkLine.ItemName));
            int logItemSrc = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @B AND PHANLOAI = @K", "@B", bSrc.Id, "@K", (int)ActivityKind.ChuyenMatHang)));
            int logItemDst = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @B AND PHANLOAI = @K", "@B", bDst.Id, "@K", (int)ActivityKind.ChuyenMatHang)));

            // Nguồn còn: 3x 30k (-10% = 81k) + combo 55k = 136k. Đích có: 50k + 2x 30k (-10% = 54k) = 104k.
            ServiceOrderLine dstMovedLine = splitRes.Ok
                ? splitRes.TargetBill.Lines.Find(l => l.ProductId == fx.ProdDrinkId)
                : null;

            Check("SDDV ChuyenMatHang §3.8/§4/§7 Chặn quyền/đích trống + rollback + tách SL=2/5: giữ ĐG/CK10%/ghi chú, TINCHEBIEN nguồn=3 đích=1, tổng nguồn=136k đích=104k, TLUUVET đủ 2 bill",
                !rNoPerm.Ok && rNoPerm.NeedsReLogin && rNoPerm.RequiredFunctionId == ServiceOrderConstants.FuncChuyenMatHang
                && !rEmptyDst.Ok && rEmptyDst.ErrorMessage == ServiceTransferConstants.DestTableEmptyForItemTransfer
                && caughtItemCrash && srcAfterCrash.Lines.Find(l => l.Id == drinkLine.Id).Quantity == 5m
                && splitRes.Ok && splitRes.SourceBill.TongCong == 136000m && splitRes.TargetBill.TongCong == 104000m
                && dstMovedLine != null && dstMovedLine.Quantity == 2m && dstMovedLine.DiscountRate == 10m && dstMovedLine.Note == "Ít đường"
                && srcKitchenNet == 3m && dstKitchenNet == 1m
                && logItemSrc >= 1 && logItemDst >= 1,
                "src=" + (splitRes.SourceBill != null ? splitRes.SourceBill.TongCong.ToString() : "null")
                + ", dst=" + (splitRes.TargetBill != null ? splitRes.TargetBill.TongCong.ToString() : "null")
                + ", k=" + srcKitchenNet + "/" + dstKitchenNet);

            // Chuyển nguyên dòng combo (kèm con combo) sang bDst -> nguồn còn 81k, đích lên 159k và có cả cha + con combo.
            TableTransferResult fullComboRes = svc.TransferItem(new TransferItemRequest
            {
                SourceBillId = bSrc.Id, SourceTableId = fx.TableA1Id, TargetTableId = fx.TableA2Id, LineId = comboParent.Id, TransferQty = null
            });
            bool dstHasComboAndChild = fullComboRes.Ok
                && fullComboRes.TargetBill.Lines.Exists(l => l.ProductId == fx.ProdComboId && !l.IsComboChild)
                && fullComboRes.TargetBill.Lines.Exists(l => l.ComboParentId == comboParent.ComboId);
            Check("SDDV ChuyenMatHang §3.8 Chuyển nguyên dòng combo (kèm dòng con): nguồn còn 81.000, đích lên 159.000 và giữ đủ cha + con combo",
                fullComboRes.Ok && dstHasComboAndChild
                && fullComboRes.SourceBill.TongCong == 81000m && fullComboRes.TargetBill.TongCong == 159000m);

            CleanupSingleBill(db, bSrc.Id, fx.TableA1Id);
            CleanupSingleBill(db, bDst.Id, fx.TableA2Id);
        }

        // ------------------------------------------------------------------ E. Hủy bill trên DB + Danh mục hóa đơn hủy (§3.8, Q20, §7)

        private static void ServiceOrderCancelBillDbTests(string db, ConfigRepository cfgRepo, UserSession admin, TransferTestFixture fx)
        {
            SetTransferConfigInt(db, cfgRepo, fx, "HeThongChayNhieuMayTram", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "ThoiGianChoPhepHuyBill", 50);
            SetTransferConfigInt(db, cfgRepo, fx, "NhapLyDoKhiHuyHoaDon", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "ChoPhepNhapGiamGia", 30);
            SetTransferConfigInt(db, cfgRepo, fx, "KichHoatLuuVetHoatDong", 30);

            var svc = new TableService(admin);
            var noPerm = new TableService(new UserSession
            {
                UserId = "u-cnl-noperm",
                UserName = "cnl_noperm",
                IsAdmin = false,
                DatabasePath = db,
                Permissions = new FunctionPermissions(false)
            });

            DateTime serverNow = new InvoiceNumberRepository(db).GetServerTimestamp();
            DateTime today = serverNow.Date;
            string tag = DbHelper.NewId().Substring(0, 5);

            // Tạo phiếu đặt + phiếu thu cọc 100.000 để kiểm: hủy bill chưa thanh toán KHÔNG xóa phiếu thu đặt cọc và không đổi quỹ (quy-tien.md §4.2).
            string bkId = DbHelper.NewId();
            fx.CreatedBookingIds.Add(bkId);
            BookingExec(db,
                "INSERT INTO TDATHANG (ID, GUID, NAME, NGAY, TUNGAY, DENNGAY, GIODAT, DBANID, TENKHACH, STATUS, USERCREATEDID) " +
                "VALUES (@I, @G, @N, @D, @D, @D, @D, @T, 'Khach Huy', 30, 'selftest')",
                "@I", bkId, "@G", DbHelper.NewId(), "@N", "DH-HUY-" + tag, "@D", today, "@T", fx.TableA1Id);
            string depVoucherId = DbHelper.NewId();
            fx.CreatedVoucherIds.Add(depVoucherId);
            BookingExec(db,
                "INSERT INTO TTHUCHI (ID, NAME, LOAI, NGAY, TIMECREATED, THU, CHI, TDATHANGID, DCUAHANGID, STATUS, USERCREATEDID) " +
                "VALUES (@I, @N, 0, @D, @D, 100000, 0, @BK, @S, 30, 'selftest')",
                "@I", depVoucherId, "@N", "PT-COC-HUY-" + tag, "@D", today, "@BK", bkId, "@S", fx.StoreId);

            // Mở bill trên TableA1: 2x Tra Dao (60k) + 1x Combo (55k + con combo) + 1 dòng XUATVATTU=30 + 1 TINCHEBIEN + giảm tổng 10% (12k).
            ServiceOrderHeader bill = OpenTransferBill(svc, fx, fx.TableA1Id, fx.ProdDrinkId, 2m, bkId);
            bill = svc.AddItem(bill.Id, fx.TableA1Id, fx.ProdComboId, 1m).Bill;
            svc.UpdateBillFinancials(bill.Id, fx.TableA1Id, 10m, 0m, false, 0m, 0m, false, 0m); // TIENHANG=115k -> giảm 10% = 11.5k -> 12k
            bill = svc.LoadBill(bill.Id);

            BookingExec(db,
                "INSERT INTO TDONHANGCHITIET (ID, TDONHANGID, DMATHANGID, TENHANG, SLXUAT, SLXUATCHUAQUYDOI, SLNHAP, SLNHAPCHUAQUYDOI, DONGIA, THANHTIEN, XUATVATTU, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @B, @P, 'Vat tu xuat an', 1, 1, 0, 0, 0, 0, 30, 30, 'selftest', @T)",
                "@I", DbHelper.NewId(), "@B", bill.Id, "@P", fx.ProdFoodId, "@T", serverNow);
            BookingExec(db,
                "INSERT INTO TINCHEBIEN (ID, TDONHANGID, TENHANG, SOLUONG, STATUS, USERCREATEDID) VALUES (@I, @B, 'TRF Tra Dao', 2, 30, 'selftest')",
                "@I", DbHelper.NewId(), "@B", bill.Id);

            // 1. Chặn: thiếu quyền, lý do rỗng khi NhapLyDoKhiHuyHoaDon=30, thuộc nhóm gộp NHOMGUID, quá ThoiGianChoPhepHuyBill (50p).
            TableTransferResult rNoPerm = noPerm.CancelBill(new CancelBillRequest { BillId = bill.Id, TableId = fx.TableA1Id, Reason = "Test" });
            TableTransferResult rNoReason = svc.CancelBill(new CancelBillRequest { BillId = bill.Id, TableId = fx.TableA1Id, Reason = "  " });

            BookingExec(db, "UPDATE TDONHANG SET NHOMGUID = @G WHERE ID = @I", "@G", DbHelper.NewId(), "@I", bill.Id);
            TableTransferResult rGroup = svc.CancelBill(new CancelBillRequest { BillId = bill.Id, TableId = fx.TableA1Id, Reason = "Hủy nhóm" });
            BookingExec(db, "UPDATE TDONHANG SET NHOMGUID = NULL WHERE ID = @I", "@I", bill.Id);

            DateTime origStart = bill.BatDau;
            BookingExec(db, "UPDATE TDONHANG SET BATDAU = @B WHERE ID = @I", "@B", serverNow.AddMinutes(-65), "@I", bill.Id);
            TableTransferResult rExpired = svc.CancelBill(new CancelBillRequest { BillId = bill.Id, TableId = fx.TableA1Id, Reason = "Quá hạn" });
            BookingExec(db, "UPDATE TDONHANG SET BATDAU = @B WHERE ID = @I", "@B", origStart, "@I", bill.Id);

            Check("SDDV HuyBill §3.8/§4/§7 Chặn thiếu quyền, thiếu lý do, bill thuộc nhóm gộp NHOMGUID, quá thời gian ThoiGianChoPhepHuyBill (50 phút)",
                !rNoPerm.Ok && rNoPerm.NeedsReLogin && rNoPerm.RequiredFunctionId == ServiceOrderConstants.FuncHuyHoaDon
                && !rNoReason.Ok && rNoReason.ErrorMessage == ServiceTransferConstants.CancelReasonRequired
                && !rGroup.Ok && rGroup.ErrorMessage == ServiceTransferConstants.CancelBlockedInGroup
                && !rExpired.Ok && rExpired.ErrorMessage != null && rExpired.ErrorMessage.Contains("50"));

            // 2. Q20: Bill có tạm ứng còn hiệu lực (TTHUCHI.TDONHANGID, STATUS=30) -> chặn hủy, bill & phiếu giữ nguyên; xóa mềm -> hủy được!
            string advId = DbHelper.NewId();
            fx.CreatedVoucherIds.Add(advId);
            BookingExec(db,
                "INSERT INTO TTHUCHI (ID, NAME, LOAI, NGAY, TIMECREATED, THU, CHI, TDONHANGID, DCUAHANGID, STATUS, USERCREATEDID) " +
                "VALUES (@I, @N, 0, @D, @D, 40000, 0, @B, @S, 30, 'selftest')",
                "@I", advId, "@N", "PT-TU-HUY-" + tag, "@D", today, "@B", bill.Id, "@S", fx.StoreId);

            TableTransferResult rAdv = svc.CancelBill(new CancelBillRequest { BillId = bill.Id, TableId = fx.TableA1Id, Reason = "Khách hủy" });
            int billStillThere = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", bill.Id)));
            int advStillActive = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TTHUCHI WHERE ID = @I AND STATUS = 30", "@I", advId)));

            BookingExec(db, "UPDATE TTHUCHI SET STATUS = -30 WHERE ID = @I", "@I", advId);

            // 3. Lỗi giữa chừng sau ghi TDONHANGHUY -> rollback sạch (bill vẫn còn, 0 TDONHANGHUY, 0 TLUUVET HuyBill).
            bool caughtCancelCrash = false;
            try
            {
                svc.CancelBill(new CancelBillRequest
                {
                    BillId = bill.Id, TableId = fx.TableA1Id, Reason = "Khách đổi ý",
                    TestHookAfterSnapshot = delegate { throw new InvalidOperationException("Simulated cancel fail"); }
                });
            }
            catch (InvalidOperationException) { caughtCancelCrash = true; }
            int billAfterCrash = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", bill.Id)));
            int huyAfterCrash = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANGHUY WHERE TDONHANGID = @I", "@I", bill.Id)));

            Check("SDDV HuyBill §3.8/Q20/§7 Q20 chặn hủy khi có tạm ứng (bill & phiếu giữ nguyên) và rollback sạch khi lỗi giữa chừng",
                !rAdv.Ok && rAdv.ErrorMessage != null && rAdv.ErrorMessage.Contains("tạm ứng")
                && billStillThere == 1 && advStillActive == 1
                && caughtCancelCrash && billAfterCrash == 1 && huyAfterCrash == 0);

            // 4. Hủy bill thành công sau khi tạm ứng đã xóa mềm.
            string cancelReason = "Khách đổi ý không dùng " + tag;
            TableTransferResult cancelled = svc.CancelBill(new CancelBillRequest { BillId = bill.Id, TableId = fx.TableA1Id, Reason = cancelReason });
            if (cancelled.Ok && !string.IsNullOrEmpty(cancelled.CancelledBillHuyId))
                fx.CreatedCancelIds.Add(cancelled.CancelledBillHuyId);

            int billLeft = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", bill.Id)));
            int linesLeft = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", bill.Id)));
            int kitchenLeft = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM TINCHEBIEN WHERE TDONHANGID = @I", "@I", bill.Id)));
            string tableBillAfter = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT TDONHANGID FROM DBAN WHERE ID = @I", "@I", fx.TableA1Id)));
            string savedBan = DbHelper.Read(db, conn => DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT BAN FROM TDONHANGHUY WHERE ID = @I", "@I", cancelled.CancelledBillHuyId)));
            int huyLogs = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @B AND PHANLOAI = @K", "@B", bill.Id, "@K", (int)ActivityKind.HuyBill)));
            int depVoucherStillThere = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TTHUCHI WHERE ID = @I AND STATUS = 30", "@I", depVoucherId)));

            // Kiểm thêm bản hủy bill nhóm có USERTHANHTOANID rỗng qua ArchiveAndDeleteBillIn -> THANHTOANBOI rỗng (§7).
            ServiceOrderHeader grpBill = OpenTransferBill(svc, fx, fx.TableA2Id, fx.ProdFoodId, 1m, null);
            BookingExec(db, "UPDATE TDONHANG SET NHOMGUID = @G WHERE ID = @I", "@G", DbHelper.NewId(), "@I", grpBill.Id);
            string grpHuyId = null;
            DbHelper.InTransaction(db, (conn, tx) =>
            {
                grpHuyId = ServiceTransferRepository.ArchiveAndDeleteBillIn(
                    conn, tx, grpBill.Id, grpBill.TableName, "Hủy bill nhóm " + tag, admin.DisplayName, admin.UserId, serverNow);
            });
            if (!string.IsNullOrEmpty(grpHuyId))
                fx.CreatedCancelIds.Add(grpHuyId);

            // Mở màn Danh mục hóa đơn hủy thật (CancelledBillView) kiểm tra hiển thị đúng cả 2 bản hủy (§7).
            bool cancelledViewOk = false;
            string viewDetail = "";
            using (var host = MakeOffscreenHost())
            {
                host.ClientSize = new Size(1280, 720);
                var cView = new CancelledBillView(admin) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(cView);
                host.Show();
                Application.DoEvents();
                try
                {
                    int idxSingle = cView.FindMasterRowIndexById(cancelled.CancelledBillHuyId);
                    int idxGroup = cView.FindMasterRowIndexById(grpHuyId);
                    bool selectedOk = idxSingle >= 0 && cView.SelectMasterRow(idxSingle);
                    Application.DoEvents();

                    string shownNo = idxSingle >= 0 ? Convert.ToString(cView.GetMasterFormattedValue(idxSingle, "colName")) : "";
                    string shownReason = idxSingle >= 0 ? Convert.ToString(cView.GetMasterFormattedValue(idxSingle, "colLyDoHuy")) : "";
                    string shownPaidBy = idxSingle >= 0 ? Convert.ToString(cView.GetMasterFormattedValue(idxSingle, "colThanhToanBoi")) : "x";
                    string shownGrpPaidBy = idxGroup >= 0 ? Convert.ToString(cView.GetMasterFormattedValue(idxGroup, "colThanhToanBoi")) : "x";
                    decimal savedDiscMoney = DbHelper.Read(db, conn => DbHelper.Dec(DbHelper.Scalar(conn, null,
                        "SELECT TIENGIAMGIA FROM TDONHANGHUY WHERE ID = @I", "@I", cancelled.CancelledBillHuyId)));

                    // Chi tiết chỉ gồm 2 dòng chính (Tra Dao + Combo cha); KHÔNG dính dòng XUATVATTU=30 hay combo con!
                    int detailCnt = cView.DetailRowCount;
                    bool noExportOrChild = true;
                    for (int i = 0; i < detailCnt; i++)
                    {
                        string ten = Convert.ToString(cView.GetDetailFormattedValue(i, "colDetailTenHang"));
                        if (ten.Contains("Vat tu xuat") || ten.Contains("TRF Banh Con"))
                            noExportOrChild = false;
                    }

                    cancelledViewOk = selectedOk && idxGroup >= 0
                        && shownNo == bill.Name && shownReason == cancelReason
                        && shownPaidBy == "" && shownGrpPaidBy == ""
                        && savedDiscMoney == 12000m
                        && detailCnt == 2 && noExportOrChild;
                    viewDetail = "idx=" + idxSingle + "/" + idxGroup + ", det=" + detailCnt + ", disc=" + savedDiscMoney;
                }
                finally { host.Close(); }
            }

            Check("SDDV HuyBill §3.8/§7 Hủy bill xóa cứng + bàn trống + TLUUVET + giữ phiếu thu cọc + mở Danh mục hóa đơn hủy: đúng số phiếu, lý do, BAN, THANHTOANBOI rỗng, TIENGIAMGIA=12k, chi tiết bỏ XUATVATTU & combo con",
                cancelled.Ok && billLeft == 0 && linesLeft == 0 && kitchenLeft == 0
                && string.IsNullOrEmpty(tableBillAfter)
                && savedBan == bill.TableName
                && huyLogs >= 1 && depVoucherStillThere == 1
                && cancelledViewOk,
                "ban=" + savedBan + ", " + viewDetail);
        }
    }
}
