using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void StockTransferDbTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string whSrcId = DbHelper.NewId();
            string whDstId = DbHelper.NewId();
            string whDstAllowNegId = DbHelper.NewId();
            string empOutId = DbHelper.NewId();
            string empInId = DbHelper.NewId();
            string unitOddId = DbHelper.NewId();
            string unitEvenId = DbHelper.NewId();
            string prod1Id = DbHelper.NewId();
            string prod2Id = DbHelper.NewId();
            string prod2UnitId = DbHelper.NewId();
            var createdOrderIds = new List<string>();

            var cfgRepo = new ConfigRepository(db);
            int origLogFlag = SystemConfig.GetInt(ActivityConstants.ConfigKey);
            int origAllowNegSys = SystemConfig.GetInt("ChoPhepAmKho");

            try
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = 30 },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = 0 }
                }, "selftest_xck");
                SystemConfig.Reload(db);

                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", storeId, "@N", "CH ChuyenKho " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_xck', @T)",
                    "@I", whSrcId, "@N", "Kho Nguon " + tag, "@S", storeId, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 0, 30, 'selftest_xck', @T)",
                    "@I", whDstId, "@N", "Kho Dich " + tag, "@S", storeId, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_xck', @T)",
                    "@I", whDstAllowNegId, "@N", "Kho Dich ChoAm " + tag, "@S", storeId, "@T", DbHelper.Now());
                BookingExec(db, "INSERT INTO DNHANVIEN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", empOutId, "@N", "NV Xuat CK " + tag);
                BookingExec(db, "INSERT INTO DNHANVIEN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", empInId, "@N", "NV Nhap CK " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", unitOddId, "@N", "Lon " + tag);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_xck')",
                    "@I", unitEvenId, "@N", "Thùng " + tag);

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '2', @U, 50000, 80000, 55000, 30, 'selftest_xck', @T)",
                    "@I", prod1Id, "@C", "CK1-" + tag, "@N", "Trà Oolong " + tag, "@U", unitOddId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U, 30000, 45000, 32000, 30, 'selftest_xck', @T)",
                    "@I", prod2Id, "@C", "CK2-" + tag, "@N", "Siro Đào " + tag, "@U", unitOddId, "@T", DbHelper.Now());
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, NAME, DLOAIMATHANGID, DDONVITINHID, DDONVITINHCHANID, QUYDOI, GIANHAP, GIABAN, GIAVON, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, '0', @U1, @U2, 24, 240000, 300000, 250000, 30, 'selftest_xck', @T)",
                    "@I", prod2UnitId, "@C", "CK3-" + tag, "@N", "Sữa Hộp 2DVT " + tag, "@U1", unitOddId, "@U2", unitEvenId, "@T", DbHelper.Now());

                // Seed tồn kho nguồn: prod1 = 30, prod2 = 20, prod2Unit = 10 lon (chưa đủ 2 thùng = 48 lon!).
                var purSvc = new PurchaseService(AppSession.Current);
                PurchaseSaveResult seed1 = purSvc.Save(new PurchaseRow
                {
                    Name = "PN-SEED-CK-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whSrcId,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine { ProductId = prod1Id, ProductName = "Trà Oolong " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 30m, UnitPrice = 50000m },
                        new PurchaseLine { ProductId = prod2Id, ProductName = "Siro Đào " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 20m, UnitPrice = 30000m },
                        new PurchaseLine { ProductId = prod2UnitId, ProductName = "Sữa Hộp 2DVT " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m, RawQuantity = 10m, UnitPrice = 10000m }
                    }
                });
                if (seed1.Ok) createdOrderIds.Add(seed1.SavedId);

                var trSvc = new StockDocumentService(AppSession.Current, StockDocumentKind.Transfer);

                // §7 #2: Kho nhập trùng kho xuất bị chặn.
                StockDocumentSaveResult rSameWh = trSvc.Save(new StockDocumentRow
                {
                    Name = "CK-SAME-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whSrcId,
                    DestWarehouseId = whSrcId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 2m, UnitPrice = 50000m }
                    }
                });

                // §7 #2: Lưu chuyển kho 2 mặt hàng kèm thuế 8% & phí VC 15.000 -> kiểm dòng nhập + dòng xuất đối ứng + tồn 2 kho.
                string trNum1 = trSvc.GenerateNumber(DateTime.Today);
                var tr1 = new StockDocumentRow
                {
                    Name = trNum1,
                    Date = DateTime.Today,
                    SourceWarehouseId = whSrcId,
                    DestWarehouseId = whDstId,
                    ExportEmployeeId = empOutId,
                    ImportEmployeeId = empInId,
                    Description = "Chuyển kho nội bộ " + tag,
                    Note = "Chuyển đợt 1",
                    TileGiamGia = 10m,
                    GiamTheoTien = false,
                    TileThue = 8m,
                    PhiVanChuyen = 15000m,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, ProductName = "Trà Oolong " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 10m, UnitPrice = 50000m },
                        new StockDocumentLine { ProductId = prod2Id, ProductName = "Siro Đào " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 5m, UnitPrice = 30000m }
                    }
                };
                // TIENHANG = 500.000 + 150.000 = 650.000; GG 10% = 65.000; sau GG = 585.000; thuế 8% = 46.800; phí = 15.000 => TONGCONG = 646.800.
                StockDocumentSaveResult rTr1 = trSvc.Save(tr1);
                if (rTr1.Ok) createdOrderIds.Add(rTr1.SavedId);
                StockDocumentRow backTr1 = rTr1.Ok ? trSvc.LoadById(rTr1.SavedId) : null;
                int totalLines1 = rTr1.Ok ? BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", rTr1.SavedId) : 0;
                int importLines1 = rTr1.Ok ? BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I AND SLNHAP > 0 AND DKHOHANGID = @W", "@I", rTr1.SavedId, "@W", whDstId) : 0;
                int mirrorLines1 = rTr1.Ok ? BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I AND SLXUAT > 0 AND DKHOHANGID = @W", "@I", rTr1.SavedId, "@W", whSrcId) : 0;

                decimal src1 = trSvc.GetStockBalance(prod1Id, whSrcId);
                decimal dst1 = trSvc.GetStockBalance(prod1Id, whDstId);
                decimal src2 = trSvc.GetStockBalance(prod2Id, whSrcId);
                decimal dst2 = trSvc.GetStockBalance(prod2Id, whDstId);

                Check("Chuyển kho §7 #2 (dòng nhập + dòng xuất đối ứng, tồn 2 kho, thuế/phí như phiếu nhập, chặn trùng kho): 2 nhập + 2 đối ứng, nguồn giảm/đích tăng đúng",
                    !rSameWh.Ok && rSameWh.ErrorMessage == StockDocumentConstants.SameWarehouseBlocked
                    && rTr1.Ok && backTr1 != null && backTr1.Loai == 3 && backTr1.Status == 30 && backTr1.PaidFlag == 30
                    && backTr1.Lines.Count == 2 && totalLines1 == 4 && importLines1 == 2 && mirrorLines1 == 2
                    && backTr1.TienHang == 650000m && backTr1.TienGiamGia == 65000m && backTr1.TienThue == 46800m
                    && backTr1.PhiVanChuyen == 15000m && backTr1.TongCong == 646800m
                    && src1 == 20m && dst1 == 10m && src2 == 15m && dst2 == 5m,
                    SingleLine(rTr1.ErrorMessage ?? ("lines=" + totalLines1 + ", src/dst=" + src1 + "/" + dst1 + ", tong=" + (backTr1 == null ? 0m : backTr1.TongCong))));

                // §7 #3: ĐVT chẵn: kho nguồn chỉ có 10 lon -> chuyển 2 thùng (quy đổi 24 = 48 lon) PHẢI BỊ CHẶN (kiểm theo 48, không theo 2!).
                StockDocumentSaveResult r2uBlocked = trSvc.Save(new StockDocumentRow
                {
                    Name = "CK-2U-BLK-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whSrcId,
                    DestWarehouseId = whDstId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine
                        {
                            ProductId = prod2UnitId, ProductName = "Sữa Hộp 2DVT " + tag,
                            UnitId = unitEvenId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m,
                            RawQuantity = 2m, UnitPrice = 240000m
                        }
                    }
                });

                // Nhập thêm 40 lon vào kho nguồn (tổng 50 lon) -> chuyển 2 thùng (48 lon) thành công, tồn nguồn = 2, tồn đích = 48.
                PurchaseSaveResult seedMore = purSvc.Save(new PurchaseRow
                {
                    Name = "PN-SEED-2U-" + tag,
                    Date = DateTime.Today,
                    WarehouseId = whSrcId,
                    Lines = new List<PurchaseLine>
                    {
                        new PurchaseLine { ProductId = prod2UnitId, ProductName = "Sữa Hộp 2DVT " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m, RawQuantity = 40m, UnitPrice = 10000m }
                    }
                });
                if (seedMore.Ok) createdOrderIds.Add(seedMore.SavedId);

                var tr2u = new StockDocumentRow
                {
                    Name = "CK-2U-OK-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whSrcId,
                    DestWarehouseId = whDstId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine
                        {
                            ProductId = prod2UnitId, ProductName = "Sữa Hộp 2DVT " + tag,
                            UnitId = unitEvenId, BaseUnitId = unitOddId, BigUnitId = unitEvenId, QuyDoi = 24m,
                            RawQuantity = 2m, UnitPrice = 240000m
                        }
                    }
                };
                StockDocumentSaveResult r2uOk = trSvc.Save(tr2u);
                if (r2uOk.Ok) createdOrderIds.Add(r2uOk.SavedId);
                decimal src2u = trSvc.GetStockBalance(prod2UnitId, whSrcId);
                decimal dst2u = trSvc.GetStockBalance(prod2UnitId, whDstId);
                int mirror2uMatch = r2uOk.Ok
                    ? BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I AND SLXUAT = 48 AND SLXUATCHUAQUYDOI = 2 AND DKHOHANGID = @W", "@I", r2uOk.SavedId, "@W", whSrcId)
                    : 0;

                Check("Chuyển kho §7 #3 (ĐVT chẵn kiểm tồn theo số đã quy đổi SLNHAP=48): tồn 10 chặn chuyển 2 thùng (48), nhập thêm 40 chuyển đạt (nguồn 2, đích 48)",
                    !r2uBlocked.Ok
                    && r2uBlocked.ErrorMessage.IndexOf("10", StringComparison.Ordinal) >= 0
                    && r2uBlocked.ErrorMessage.IndexOf("48", StringComparison.Ordinal) >= 0
                    && r2uOk.Ok && src2u == 2m && dst2u == 48m && mirror2uMatch == 1,
                    SingleLine(r2uBlocked.ErrorMessage ?? ("src2u=" + src2u + ", dst2u=" + dst2u)));

                // §7 #4: Sửa chuyển kho -> dòng xuất đối ứng sinh lại, KHÔNG nhân đôi (vẫn đúng 4 dòng DB cho 2 mặt hàng).
                backTr1.Lines[0].RawQuantity = 12m;
                backTr1.Lines[1].RawQuantity = 7m;
                StockDocumentSaveResult rEdit = trSvc.Save(backTr1);
                int totalLinesAfterEdit = BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", backTr1.Id);
                int importLinesAfterEdit = BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I AND SLNHAP > 0", "@I", backTr1.Id);
                int mirrorLinesAfterEdit = BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I AND SLXUAT > 0", "@I", backTr1.Id);
                decimal src1Edit = trSvc.GetStockBalance(prod1Id, whSrcId);
                decimal dst1Edit = trSvc.GetStockBalance(prod1Id, whDstId);

                // Xuất 9 prod1 từ kho đích (tồn kho đích còn 3) -> hạ chuyển kho từ 12 xuống 5 hoặc xóa PHẢI BỊ CHẶN vì kho đích thành âm (-4 / -9).
                var expSvc = new StockDocumentService(AppSession.Current, StockDocumentKind.Export);
                StockDocumentSaveResult expFromDst = expSvc.Save(new StockDocumentRow
                {
                    Name = "PX-DST-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whDstId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, ProductName = "Trà Oolong " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 9m, UnitPrice = 50000m }
                    }
                });
                if (expFromDst.Ok) createdOrderIds.Add(expFromDst.SavedId);

                StockDocumentRow trToShrink = trSvc.LoadById(backTr1.Id);
                trToShrink.Lines[0].RawQuantity = 5m;
                StockDocumentSaveResult rShrinkBlocked = trSvc.Save(trToShrink);
                StockDocumentDeleteResult rDelDestNegBlocked = trSvc.Delete(backTr1.Id);

                // Trên kho đích CHOPHEPAMKHO=30 -> sửa/xóa gây âm kho đích được phép.
                var trAllowDst = new StockDocumentRow
                {
                    Name = "CK-ALLOWDST-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whSrcId,
                    DestWarehouseId = whDstAllowNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, ProductName = "Trà Oolong " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 5m, UnitPrice = 50000m }
                    }
                };
                StockDocumentSaveResult rTrAllow = trSvc.Save(trAllowDst);
                if (rTrAllow.Ok) createdOrderIds.Add(rTrAllow.SavedId);
                StockDocumentSaveResult expAllowDst = expSvc.Save(new StockDocumentRow
                {
                    Name = "PX-ALLOWDST-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whDstAllowNegId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod1Id, ProductName = "Trà Oolong " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 4m, UnitPrice = 50000m }
                    }
                });
                if (expAllowDst.Ok) createdOrderIds.Add(expAllowDst.SavedId);
                StockDocumentDeleteResult rDelAllowDst = trSvc.Delete(trAllowDst.Id);

                // §7 #4: Xóa phiếu chuyển kho xóa cả dòng nhập lẫn dòng đối ứng, hoàn tồn 2 kho; rollback hook khi lỗi giữa chừng.
                bool saveRbOk = false;
                var rbTr = new StockDocumentRow
                {
                    Name = "CK-RB-" + tag,
                    Date = DateTime.Today,
                    SourceWarehouseId = whSrcId,
                    DestWarehouseId = whDstId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod2Id, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 1m, UnitPrice = 30000m }
                    }
                };
                try
                {
                    trSvc.Save(rbTr, delegate { throw new InvalidOperationException("Giả lập lỗi giữa Save chuyển kho"); });
                }
                catch (InvalidOperationException)
                {
                    saveRbOk = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE NAME = @N", "@N", "CK-RB-" + tag) == 0
                        && string.IsNullOrEmpty(rbTr.Id);
                }

                bool delRbOk = false;
                try
                {
                    trSvc.Delete(tr2u.Id, delegate { throw new InvalidOperationException("Giả lập lỗi giữa Delete chuyển kho"); });
                }
                catch (InvalidOperationException)
                {
                    delRbOk = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", tr2u.Id) == 1
                        && BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", tr2u.Id) == 2;
                }

                StockDocumentDeleteResult rDel2u = trSvc.Delete(tr2u.Id);
                int leftHdr2u = BookingCount(db, "SELECT COUNT(*) FROM TDONHANG WHERE ID = @I", "@I", tr2u.Id);
                int leftLn2u = BookingCount(db, "SELECT COUNT(*) FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", tr2u.Id);
                int delLog2u = BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", tr2u.Id);
                decimal src2uRestored = trSvc.GetStockBalance(prod2UnitId, whSrcId);
                decimal dst2uRestored = trSvc.GetStockBalance(prod2UnitId, whDstId);

                // §7 #5: Số phiếu chuyển kho dài 255 ký tự không làm lưu vết tràn.
                string num255 = "CK255-" + tag + "-" + new string('C', 255 - 7 - tag.Length);
                var tr255 = new StockDocumentRow
                {
                    Name = num255,
                    Date = DateTime.Today,
                    SourceWarehouseId = whSrcId,
                    DestWarehouseId = whDstId,
                    Lines = new List<StockDocumentLine>
                    {
                        new StockDocumentLine { ProductId = prod2Id, ProductName = "Siro Đào " + tag, UnitId = unitOddId, BaseUnitId = unitOddId, RawQuantity = 1m, UnitPrice = 30000m }
                    }
                };
                StockDocumentSaveResult r255 = trSvc.Save(tr255);
                if (r255.Ok) createdOrderIds.Add(r255.SavedId);
                int log255 = r255.Ok ? BookingCount(db, "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", r255.SavedId) : 0;

                Check("Chuyển kho §7 #4 & #5 (sửa không nhân đôi dòng đối ứng, chặn âm kho đích, xóa mất cả dòng đối ứng, rollback hook, số phiếu 255 ký tự): đạt toàn bộ",
                    rEdit.Ok && totalLinesAfterEdit == 4 && importLinesAfterEdit == 2 && mirrorLinesAfterEdit == 2
                    && src1Edit == 18m && dst1Edit == 12m
                    && !rShrinkBlocked.Ok && !rDelDestNegBlocked.Ok && rDelAllowDst.Ok
                    && saveRbOk && delRbOk && rDel2u.Ok && leftHdr2u == 0 && leftLn2u == 0 && delLog2u >= 2
                    && src2uRestored == 50m && dst2uRestored == 0m
                    && r255.Ok && num255.Length == 255 && log255 >= 1,
                    SingleLine("editLines=" + totalLinesAfterEdit + ", shrinkBlk=" + (!rShrinkBlocked.Ok) + ", delLeft=" + leftLn2u + ", rb=" + saveRbOk + "/" + delRbOk + ", log255=" + log255));
            }
            finally
            {
                cfgRepo.Save(new[]
                {
                    new ConfigChange { Name = ActivityConstants.ConfigKey, DataType = 3, IntValue = origLogFlag },
                    new ConfigChange { Name = "ChoPhepAmKho", DataType = 3, IntValue = origAllowNegSys }
                }, "selftest_xck");
                SystemConfig.Reload(db);

                foreach (string oid in createdOrderIds)
                {
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", oid);
                    BookingExec(db, "DELETE FROM TDONHANGCHITIET WHERE TDONHANGID = @I", "@I", oid);
                    SafeDeleteById(db, "TDONHANG", oid);
                }
                SafeDeleteById(db, "DMATHANG", prod1Id);
                SafeDeleteById(db, "DMATHANG", prod2Id);
                SafeDeleteById(db, "DMATHANG", prod2UnitId);
                SafeDeleteById(db, "DDONVITINH", unitOddId);
                SafeDeleteById(db, "DDONVITINH", unitEvenId);
                SafeDeleteById(db, "DNHANVIEN", empOutId);
                SafeDeleteById(db, "DNHANVIEN", empInId);
                SafeDeleteById(db, "DKHOHANG", whSrcId);
                SafeDeleteById(db, "DKHOHANG", whDstId);
                SafeDeleteById(db, "DKHOHANG", whDstAllowNegId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }
    }
}
