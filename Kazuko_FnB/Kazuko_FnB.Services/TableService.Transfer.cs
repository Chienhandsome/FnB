using System;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        private ServiceTransferRepository TransferRepo()
        {
            return new ServiceTransferRepository(_databasePath);
        }

        // ------------------------------------------------------------------ Phân quyền Chuyển / Gộp / Chuyển món / Hủy (§4)

        public bool CanMoveTable(UserSession overrideSession = null)
        {
            return HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncChuyenBan);
        }

        public bool CanMergeTable(UserSession overrideSession = null)
        {
            return HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncGopBan);
        }

        public bool CanTransferItem(UserSession overrideSession = null)
        {
            return HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncChuyenMatHang);
        }

        public bool CanCancelBill(UserSession overrideSession = null)
        {
            return HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncHuyHoaDon);
        }

        // ------------------------------------------------------------------ 1. Chuyển bàn (§3.8, ban-khu-vuc.md §2)

        /// <summary>
        /// Chuyển hóa đơn đang mở sang bàn trống trong MỘT transaction:
        /// - Kiểm quyền ChuyenBan, chưa thanh toán, chưa in tạm tính (trừ khi cờ cho phép), khóa nhiều máy trạm.
        /// - Cập nhật giá hàng theo khu vực mới (GiaMatHangKhiChuyenBan) và tính lại tổng bằng OrderCalculator.
        /// - Đổi TDONHANG.DBANID (trigger P3 tự cập nhật DBAN.TDONHANGID), giữ nguyên NHOMGUID và TDATHANGID.
        /// - Chuyển khóa bàn sang bàn đích và ghi lưu vết TLUUVET (ChuyenBan).
        /// </summary>
        public TableTransferResult MoveTable(MoveTableRequest req, UserSession overrideSession = null)
        {
            if (req == null || string.IsNullOrEmpty(req.SourceBillId) || string.IsNullOrEmpty(req.SourceTableId))
                return TableTransferResult.Fail(ServiceOrderConstants.SelectTableFirst);
            if (string.IsNullOrEmpty(req.TargetTableId) || string.Equals(req.SourceTableId, req.TargetTableId, StringComparison.Ordinal))
                return TableTransferResult.Fail(ServiceTransferConstants.ChooseDifferentTable);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncChuyenBan))
                return TableTransferResult.ReLogin(ServiceOrderConstants.FuncChuyenBan);

            bool checkLock = SystemConfig.HeThongChayNhieuMayTram;
            bool allowAfterTemp = SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh;
            MoveTablePriceMode priceMode = TableTransferRules.NormalizePriceMode(SystemConfig.GiaMatHangKhiChuyenBan);
            decimal roundStep = SystemConfig.LamTronTien;

            TableTransferResult outcome = null;
            TransferRepo().RunInTransaction((conn, tx) =>
            {
                int srcTempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, req.SourceBillId, req.SourceTableId, out srcTempPrint))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableAlreadyPaidBlocked);
                    return;
                }
                if (srcTempPrint > 0 && !allowAfterTemp)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableTempPrintedBlocked);
                    return;
                }

                ServiceTableInfo srcTable = ServiceTransferRepository.LoadTableIn(conn, tx, req.SourceTableId);
                ServiceTableInfo dstTable = ServiceTransferRepository.LoadTableIn(conn, tx, req.TargetTableId);
                if (srcTable == null || dstTable == null)
                {
                    outcome = TableTransferResult.Fail("Bàn không còn tồn tại.");
                    return;
                }

                if (checkLock)
                {
                    string holder;
                    if (ServiceOrderRepository.IsTableLockedByOtherIn(conn, tx, srcTable.Id, actor.UserName, out holder))
                    {
                        outcome = TableTransferResult.Fail(ServiceTransferConstants.TableLockedBy(srcTable.Name, holder));
                        return;
                    }
                    if (ServiceOrderRepository.IsTableLockedByOtherIn(conn, tx, dstTable.Id, actor.UserName, out holder))
                    {
                        outcome = TableTransferResult.Fail(ServiceTransferConstants.TableLockedBy(dstTable.Name, holder));
                        return;
                    }
                }

                if (ServiceOrderRepository.HasActiveBillOnTableIn(conn, tx, dstTable.Id))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.DestTableInUseForMove);
                    return;
                }

                ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, tx, req.SourceBillId);
                if (bill == null || bill.PaidFlag != 0)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableAlreadyPaidBlocked);
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);

                // Cập nhật giá theo khu vực đích (GiaMatHangKhiChuyenBan) và tính lại bằng OrderCalculator.
                ApplyAreaPriceUpdateIn(conn, tx, bill, dstTable.AreaPriceLevelId, priceMode, actor.UserId, now);
                RecalcAndSaveHeaderIn(conn, tx, bill, roundStep, now);

                int updated = ServiceTransferRepository.UpdateBillTableIn(conn, tx, bill.Id, dstTable.Id, actor.UserId, now);
                if (updated != 1)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableAlreadyPaidBlocked);
                    return;
                }

                ServiceTransferRepository.TransferTableLockIn(conn, tx, srcTable.Id, dstTable.Id, actor.UserName, checkLock);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.ChuyenBan,
                        BillId = bill.Id,
                        TableName = dstTable.Name,
                        Note = string.Format("Chuyển hóa đơn {0} từ bàn {1} sang bàn {2}", bill.Name, srcTable.Name, dstTable.Name),
                        Amount = bill.TongCong,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = now
                    }
                });

                if (req.TestHookBeforeCommit != null)
                    req.TestHookBeforeCommit();

                ServiceOrderHeader reloaded = ServiceOrderRepository.LoadBillIn(conn, tx, bill.Id);
                outcome = new TableTransferResult
                {
                    Ok = true,
                    SourceBillId = bill.Id,
                    TargetBillId = bill.Id,
                    TargetTableId = dstTable.Id,
                    TargetBill = reloaded
                };
            });

            return outcome ?? TableTransferResult.Fail("Không thể chuyển bàn.");
        }

        // ------------------------------------------------------------------ 2. Gộp bàn (§3.8, Q19, ban-khu-vuc.md §2)

        /// <summary>
        /// Gộp bàn nguồn sang bàn đích (đang có khách) trong MỘT transaction:
        /// - Kiểm quyền GopBan, cả hai bill chưa thanh toán và chưa in tạm tính (trừ khi cho phép), khóa nhiều máy trạm.
        /// - Từ chối nếu bill nguồn tạo từ đặt hàng (TDATHANGID), tỉ lệ giảm giá hai bill khác nhau,
        ///   hoặc bill nguồn có phiếu thu tạm ứng còn hiệu lực (Q19: TTHUCHI.TDONHANGID, STATUS &lt;&gt; -30).
        /// - Lưu bản hủy bill nguồn vào TDONHANGHUY ("Gộp vào hóa đơn ..."), cập nhật giá dòng theo khu vực đích,
        ///   chuyển TDONHANGCHITIET và TINCHEBIEN sang bill đích, xóa cứng bill nguồn (trigger trả bàn nguồn về trống),
        ///   tính lại bill đích bằng OrderCalculator (giữ nguyên NHOMGUID và TDATHANGID của bill đích),
        ///   chuyển khóa bàn và ghi TLUUVET (GopBan) cho cả hai bill.
        /// </summary>
        public TableTransferResult MergeTable(MergeTableRequest req, UserSession overrideSession = null)
        {
            if (req == null || string.IsNullOrEmpty(req.SourceBillId) || string.IsNullOrEmpty(req.SourceTableId))
                return TableTransferResult.Fail(ServiceOrderConstants.SelectTableFirst);
            if (string.IsNullOrEmpty(req.TargetTableId) || string.Equals(req.SourceTableId, req.TargetTableId, StringComparison.Ordinal))
                return TableTransferResult.Fail(ServiceTransferConstants.ChooseDifferentTable);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncGopBan))
                return TableTransferResult.ReLogin(ServiceOrderConstants.FuncGopBan);

            bool checkLock = SystemConfig.HeThongChayNhieuMayTram;
            bool allowAfterTemp = SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh;
            MoveTablePriceMode priceMode = TableTransferRules.NormalizePriceMode(SystemConfig.GiaMatHangKhiChuyenBan);
            decimal roundStep = SystemConfig.LamTronTien;

            TableTransferResult outcome = null;
            TransferRepo().RunInTransaction((conn, tx) =>
            {
                int srcTempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, req.SourceBillId, req.SourceTableId, out srcTempPrint))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableAlreadyPaidBlocked);
                    return;
                }
                if (srcTempPrint > 0 && !allowAfterTemp)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableTempPrintedBlocked);
                    return;
                }

                ServiceTableInfo srcTable = ServiceTransferRepository.LoadTableIn(conn, tx, req.SourceTableId);
                ServiceTableInfo dstTable = ServiceTransferRepository.LoadTableIn(conn, tx, req.TargetTableId);
                if (srcTable == null || dstTable == null)
                {
                    outcome = TableTransferResult.Fail("Bàn không còn tồn tại.");
                    return;
                }

                if (checkLock)
                {
                    string holder;
                    if (ServiceOrderRepository.IsTableLockedByOtherIn(conn, tx, srcTable.Id, actor.UserName, out holder))
                    {
                        outcome = TableTransferResult.Fail(ServiceTransferConstants.TableLockedBy(srcTable.Name, holder));
                        return;
                    }
                    if (ServiceOrderRepository.IsTableLockedByOtherIn(conn, tx, dstTable.Id, actor.UserName, out holder))
                    {
                        outcome = TableTransferResult.Fail(ServiceTransferConstants.TableLockedBy(dstTable.Name, holder));
                        return;
                    }
                }

                if (string.IsNullOrEmpty(dstTable.ActiveBillId))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.DestTableEmptyForMerge);
                    return;
                }

                int dstTempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, dstTable.ActiveBillId, dstTable.Id, out dstTempPrint))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableAlreadyPaidBlocked);
                    return;
                }
                if (dstTempPrint > 0 && !allowAfterTemp)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableTempPrintedBlocked);
                    return;
                }

                ServiceOrderHeader srcBill = ServiceOrderRepository.LoadBillIn(conn, tx, req.SourceBillId);
                ServiceOrderHeader dstBill = ServiceOrderRepository.LoadBillIn(conn, tx, dstTable.ActiveBillId);
                if (srcBill == null || dstBill == null || srcBill.PaidFlag != 0 || dstBill.PaidFlag != 0)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableAlreadyPaidBlocked);
                    return;
                }

                if (!string.IsNullOrEmpty(srcBill.BookingId))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.MergeBlockedHasBooking);
                    return;
                }

                if (srcBill.TileGiamGia != dstBill.TileGiamGia)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.MergeBlockedDifferentDiscount);
                    return;
                }

                if (ServiceTransferRepository.HasActiveAdvanceVoucherIn(conn, tx, srcBill.Id))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.MergeBlockedHasAdvance(srcTable.Name));
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string cashierName = !string.IsNullOrEmpty(actor.DisplayName) ? actor.DisplayName : (actor.UserName ?? "");
                string cancelReason = ServiceTransferConstants.MergeCancelledReason(dstBill.Name);

                string huyId = ServiceTransferRepository.InsertCancelledBillSnapshotIn(
                    conn, tx, srcBill.Id, srcTable.Name, cancelReason, cashierName, actor.UserId, now);

                if (req.TestHookAfterSnapshot != null)
                    req.TestHookAfterSnapshot();

                ApplyAreaPriceUpdateIn(conn, tx, srcBill, dstTable.AreaPriceLevelId, priceMode, actor.UserId, now);

                ServiceTransferRepository.MoveAllLinesAndKitchenIn(conn, tx, srcBill.Id, dstBill.Id, actor.UserId, now);
                ServiceTransferRepository.HardDeleteBillIn(conn, tx, srcBill.Id);

                ServiceOrderHeader mergedTarget = ServiceOrderRepository.LoadBillIn(conn, tx, dstBill.Id);
                RecalcAndSaveHeaderIn(conn, tx, mergedTarget, roundStep, now);

                ServiceTransferRepository.TransferTableLockIn(conn, tx, srcTable.Id, dstTable.Id, actor.UserName, checkLock);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.GopBan,
                        BillId = srcBill.Id,
                        TableName = srcTable.Name,
                        Note = string.Format("Gộp bàn {0} ({1}) vào bàn {2} ({3})", srcTable.Name, srcBill.Name, dstTable.Name, dstBill.Name),
                        Amount = srcBill.TongCong,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = now
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.GopBan,
                        BillId = dstBill.Id,
                        TableName = dstTable.Name,
                        Note = string.Format("Nhận gộp từ bàn {0} ({1}) sang bàn {2} ({3})", srcTable.Name, srcBill.Name, dstTable.Name, dstBill.Name),
                        Amount = mergedTarget.TongCong,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = now
                    }
                });

                ServiceOrderHeader finalTarget = ServiceOrderRepository.LoadBillIn(conn, tx, dstBill.Id);
                outcome = new TableTransferResult
                {
                    Ok = true,
                    SourceBillId = srcBill.Id,
                    TargetBillId = dstBill.Id,
                    TargetTableId = dstTable.Id,
                    TargetBill = finalTarget,
                    CancelledBillHuyId = huyId
                };
            });

            return outcome ?? TableTransferResult.Fail("Không thể gộp bàn.");
        }

        private static void ApplyAreaPriceUpdateIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader bill,
            string targetAreaPriceLevelId,
            MoveTablePriceMode priceMode,
            string actorId,
            DateTime now)
        {
            if (priceMode == MoveTablePriceMode.KeepCurrent || bill == null || bill.Lines == null)
                return;

            foreach (ServiceOrderLine line in bill.Lines)
            {
                if (line == null || line.IsComboChild || line.IsMaterialExport || string.IsNullOrEmpty(line.ProductId))
                    continue;
                ServiceMenuItem product = ServiceOrderRepository.LoadProductIn(conn, tx, line.ProductId);
                if (product == null)
                    continue;

                bool isOpen = product.ProductTypeId == ProductRules.TypeOpen;
                decimal areaPrice = OrderCalculator.ResolveAreaPrice(product, targetAreaPriceLevelId);
                decimal newPrice = TableTransferRules.ResolveTransferredUnitPrice(
                    line.UnitPrice, areaPrice, priceMode, isOpen, product.PriceByTime);

                if (newPrice != line.UnitPrice)
                {
                    line.UnitPrice = newPrice;
                    OrderCalculator.RecalcLine(line);
                    ServiceOrderRepository.UpdateLineIn(conn, tx, line, actorId, now);
                }
            }
        }
    }
}
