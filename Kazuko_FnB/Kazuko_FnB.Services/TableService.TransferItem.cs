using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        // ------------------------------------------------------------------ 3. Chuyển mặt hàng sang bill khác (§1, §4, ban-khu-vuc.md §2)

        /// <summary>
        /// Chuyển một phần hoặc toàn bộ một dòng món (kèm con combo) sang hóa đơn đang mở trên bàn đích trong MỘT transaction:
        /// - Quyền ChuyenMatHangSangHoaDonKhac, cả hai bill chưa thanh toán và chưa in tạm tính (trừ khi cho phép).
        /// - Điều chỉnh TINCHEBIEN hai chiều theo GetChenhLechInCheBienChuyenDo.
        /// - Tính lại tổng cả hai hóa đơn bằng OrderCalculator và ghi TLUUVET (ChuyenMatHang) trên cả hai bill.
        /// </summary>
        public TableTransferResult TransferItem(TransferItemRequest req, UserSession overrideSession = null)
        {
            if (req == null || string.IsNullOrEmpty(req.SourceBillId) || string.IsNullOrEmpty(req.SourceTableId))
                return TableTransferResult.Fail(ServiceOrderConstants.SelectTableFirst);
            if (string.IsNullOrEmpty(req.LineId))
                return TableTransferResult.Fail(ServiceTransferConstants.TransferItemSelectLine);
            if (string.IsNullOrEmpty(req.TargetTableId) || string.Equals(req.SourceTableId, req.TargetTableId, StringComparison.Ordinal))
                return TableTransferResult.Fail(ServiceTransferConstants.ChooseDifferentTable);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncChuyenMatHang))
                return TableTransferResult.ReLogin(ServiceOrderConstants.FuncChuyenMatHang);

            bool checkLock = SystemConfig.HeThongChayNhieuMayTram;
            bool allowAfterTemp = SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh;
            decimal roundStep = SystemConfig.LamTronTien;
            bool kitchenEnabled = SystemConfig.SuDungChucNangInXuongBep;
            bool inFood = SystemConfig.InDoAn;
            bool inDrink = SystemConfig.InDoUong;
            bool inService = SystemConfig.InDichVu;
            bool inOther = SystemConfig.InDoKhac;

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
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.DestTableEmptyForItemTransfer);
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

                ServiceOrderLine rawLine = srcBill.Lines.Find(l => string.Equals(l.Id, req.LineId, StringComparison.Ordinal));
                if (rawLine == null || rawLine.IsMaterialExport)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TransferItemSelectLine);
                    return;
                }
                if (rawLine.IsComboChild)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TransferItemChildNotAllowed);
                    return;
                }
                if (rawLine.IsPromoGift)
                {
                    outcome = TableTransferResult.Fail("Không thể chuyển dòng hàng tặng khuyến mại.");
                    return;
                }

                bool moveAll = !req.TransferQty.HasValue
                    || req.TransferQty.Value < 0m
                    || req.TransferQty.Value == rawLine.Quantity;
                decimal moveQty = moveAll ? rawLine.Quantity : req.TransferQty.Value;
                if (!moveAll && (moveQty <= 0m || moveQty > rawLine.Quantity))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TransferItemInvalidQty);
                    return;
                }

                decimal remainingQty = moveAll ? 0m : (rawLine.Quantity - moveQty);
                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);

                List<ServiceOrderLine> comboChildren = string.IsNullOrEmpty(rawLine.ComboId)
                    ? new List<ServiceOrderLine>()
                    : srcBill.Lines.FindAll(l => string.Equals(l.ComboParentId, rawLine.ComboId, StringComparison.Ordinal));

                AdjustKitchenTransferForLineIn(
                    conn, tx, srcBill.Id, dstBill.Id, rawLine, comboChildren,
                    remainingQty, moveQty, kitchenEnabled, inFood, inDrink, inService, inOther, actor.UserId, now);

                if (moveAll)
                {
                    ServiceTransferRepository.MoveSingleLineAndComboChildrenIn(
                        conn, tx, srcBill.Id, dstBill.Id, rawLine.Id, rawLine.ComboId, actor.UserId, now);
                }
                else
                {
                    SplitLineToTargetIn(conn, tx, srcBill.Id, dstBill.Id, rawLine, comboChildren, remainingQty, moveQty, actor.UserId, now);
                }

                ServiceOrderHeader freshSrc = ServiceOrderRepository.LoadBillIn(conn, tx, srcBill.Id);
                ServiceOrderHeader freshDst = ServiceOrderRepository.LoadBillIn(conn, tx, dstBill.Id);
                ApplyPromotionsAndRecalcIn(conn, tx, freshSrc, roundStep, now, SystemConfig.ChoPhepAmKho, actor.UserId);
                ApplyPromotionsAndRecalcIn(conn, tx, freshDst, roundStep, now, SystemConfig.ChoPhepAmKho, actor.UserId);

                decimal movedGross = moveQty * rawLine.UnitPrice;
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.ChuyenMatHang,
                        BillId = srcBill.Id,
                        TableName = srcTable.Name,
                        Note = string.Format("Chuyển {0} (SL: {1}) từ bàn {2} sang bàn {3}", rawLine.ItemName, moveQty, srcTable.Name, dstTable.Name),
                        ItemName = rawLine.ItemName,
                        Quantity = moveQty,
                        UnitPrice = rawLine.UnitPrice,
                        Amount = movedGross,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = now
                    },
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.ChuyenMatHang,
                        BillId = dstBill.Id,
                        TableName = dstTable.Name,
                        Note = string.Format("Nhận chuyển {0} (SL: {1}) từ bàn {2} sang bàn {3}", rawLine.ItemName, moveQty, srcTable.Name, dstTable.Name),
                        ItemName = rawLine.ItemName,
                        Quantity = moveQty,
                        UnitPrice = rawLine.UnitPrice,
                        Amount = movedGross,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = now
                    }
                });

                if (req.TestHookBeforeCommit != null)
                    req.TestHookBeforeCommit();

                ServiceOrderHeader outSrc = ServiceOrderRepository.LoadBillIn(conn, tx, srcBill.Id);
                ServiceOrderHeader outDst = ServiceOrderRepository.LoadBillIn(conn, tx, dstBill.Id);
                PopulatePromotionLabelsIn(conn, tx, outSrc, roundStep);
                PopulatePromotionLabelsIn(conn, tx, outDst, roundStep);
                outcome = new TableTransferResult
                {
                    Ok = true,
                    SourceBillId = srcBill.Id,
                    TargetBillId = dstBill.Id,
                    TargetTableId = dstTable.Id,
                    SourceBill = outSrc,
                    TargetBill = outDst
                };
            });

            return outcome ?? TableTransferResult.Fail("Không thể chuyển mặt hàng.");
        }

        private static void SplitLineToTargetIn(
            FbConnection conn,
            FbTransaction tx,
            string srcBillId,
            string dstBillId,
            ServiceOrderLine rawLine,
            List<ServiceOrderLine> comboChildren,
            decimal remainingQty,
            decimal moveQty,
            string actorId,
            DateTime now)
        {
            decimal origQty = rawLine.Quantity;
            decimal movedDiscAmt = 0m;
            if (rawLine.DiscountByMoney && origQty > 0m)
            {
                movedDiscAmt = Math.Round(rawLine.DiscountAmount * moveQty / origQty, 0, MidpointRounding.AwayFromZero);
                rawLine.DiscountAmount = rawLine.DiscountAmount - movedDiscAmt;
            }

            rawLine.Quantity = remainingQty;
            OrderCalculator.RecalcLine(rawLine);
            ServiceOrderRepository.UpdateLineIn(conn, tx, rawLine, actorId, now);
            if (comboChildren.Count > 0)
                ServiceOrderRepository.UpdateComboChildrenQtyIn(conn, tx, srcBillId, rawLine.ComboId, remainingQty, actorId, now);

            string newComboId = DbHelper.NewId();
            var dstLine = new ServiceOrderLine
            {
                Id = DbHelper.NewId(),
                OrderId = dstBillId,
                ProductId = rawLine.ProductId,
                ProductGroupId = rawLine.ProductGroupId,
                ProductTypeId = rawLine.ProductTypeId,
                DrinkTypeId = rawLine.DrinkTypeId,
                ItemName = rawLine.ItemName,
                UnitId = rawLine.UnitId,
                WarehouseId = rawLine.WarehouseId,
                Quantity = moveQty,
                UnitPrice = rawLine.UnitPrice,
                CostPrice = rawLine.CostPrice,
                DiscountRate = rawLine.DiscountRate,
                DiscountAmount = movedDiscAmt,
                DiscountByMoney = rawLine.DiscountByMoney,
                KhuyenMai = rawLine.KhuyenMai,
                Note = rawLine.Note,
                ComboId = newComboId,
                ComboParentId = "",
                ComboQty = 0m
            };
            OrderCalculator.RecalcLine(dstLine);
            ServiceOrderRepository.InsertLineIn(conn, tx, dstLine, actorId, now);

            foreach (ServiceOrderLine child in comboChildren)
            {
                var dstChild = new ServiceOrderLine
                {
                    Id = DbHelper.NewId(),
                    OrderId = dstBillId,
                    ProductId = child.ProductId,
                    ProductTypeId = child.ProductTypeId,
                    DrinkTypeId = child.DrinkTypeId,
                    ItemName = child.ItemName,
                    UnitId = child.UnitId,
                    WarehouseId = child.WarehouseId,
                    Quantity = moveQty * child.ComboQty,
                    UnitPrice = 0m,
                    CostPrice = 0m,
                    DiscountRate = 0m,
                    DiscountAmount = 0m,
                    DiscountByMoney = false,
                    Amount = 0m,
                    Note = child.Note,
                    ComboId = DbHelper.NewId(),
                    ComboParentId = newComboId,
                    ComboQty = child.ComboQty
                };
                ServiceOrderRepository.InsertLineIn(conn, tx, dstChild, actorId, now);
            }
        }

        private static void AdjustKitchenTransferForLineIn(
            FbConnection conn,
            FbTransaction tx,
            string srcBillId,
            string dstBillId,
            ServiceOrderLine parentLine,
            List<ServiceOrderLine> comboChildren,
            decimal remainingParentQty,
            decimal movedParentQty,
            bool kitchenEnabled,
            bool inFood,
            bool inDrink,
            bool inService,
            bool inOther,
            string actorId,
            DateTime now)
        {
            if (!kitchenEnabled)
                return;

            if (comboChildren != null && comboChildren.Count > 0)
            {
                foreach (ServiceOrderLine child in comboChildren)
                {
                    bool childDrinkOn = TableTransferRules.IsDrinkTypeKitchenEnabled(child.DrinkTypeId, inFood, inDrink, inService, inOther);
                    decimal printed = ServiceOrderRepository.GetPrintedKitchenQtyIn(conn, tx, srcBillId, child.ItemName);
                    decimal diff = TableTransferRules.CalcKitchenTransferDiff(
                        printed, remainingParentQty * child.ComboQty, movedParentQty * child.ComboQty, true, childDrinkOn);
                    if (diff > 0m)
                    {
                        ServiceTransferRepository.InsertKitchenPrintAdjustmentIn(
                            conn, tx, srcBillId, child.ItemName, -diff, child.Note, child.UnitId, child.DrinkTypeId, child.UnitPrice, actorId, now);
                        ServiceTransferRepository.InsertKitchenPrintAdjustmentIn(
                            conn, tx, dstBillId, child.ItemName, diff, child.Note, child.UnitId, child.DrinkTypeId, child.UnitPrice, actorId, now);
                    }
                }
                return;
            }

            bool drinkOn = TableTransferRules.IsDrinkTypeKitchenEnabled(parentLine.DrinkTypeId, inFood, inDrink, inService, inOther);
            decimal parentPrinted = ServiceOrderRepository.GetPrintedKitchenQtyIn(conn, tx, srcBillId, parentLine.ItemName);
            decimal parentDiff = TableTransferRules.CalcKitchenTransferDiff(
                parentPrinted, remainingParentQty, movedParentQty, true, drinkOn);
            if (parentDiff > 0m)
            {
                ServiceTransferRepository.InsertKitchenPrintAdjustmentIn(
                    conn, tx, srcBillId, parentLine.ItemName, -parentDiff, parentLine.Note, parentLine.UnitId, parentLine.DrinkTypeId, parentLine.UnitPrice, actorId, now);
                ServiceTransferRepository.InsertKitchenPrintAdjustmentIn(
                    conn, tx, dstBillId, parentLine.ItemName, parentDiff, parentLine.Note, parentLine.UnitId, parentLine.DrinkTypeId, parentLine.UnitPrice, actorId, now);
            }
        }
    }
}
