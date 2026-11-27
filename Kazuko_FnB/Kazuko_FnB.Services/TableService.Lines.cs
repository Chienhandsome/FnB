using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        // ------------------------------------------------------------------ Gọi món (§3.4, §6.6)

        public ServiceOpResult AddItem(
            string billId,
            string tableId,
            string productId,
            decimal quantity,
            decimal? overridePrice = null,
            string overrideName = null,
            string overrideUnitId = null,
            string note = "",
            List<ServiceRecipeComponent> openComboComponents = null)
        {
            if (string.IsNullOrEmpty(productId))
                return ServiceOpResult.Fail(ServiceOrderConstants.SelectProductFirst);
            if (quantity <= 0m)
                return ServiceOpResult.Fail(ServiceOrderConstants.EnterQuantity);

            ServiceOpResult outcome = null;
            decimal roundStep = SystemConfig.LamTronTien;
            bool allowNegativeStock = SystemConfig.ChoPhepAmKho;

            _repo.RunInTransaction((conn, tx) =>
            {
                int tempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, billId, tableId, out tempPrint))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.BillAlreadyPaid);
                    return;
                }

                ServiceMenuItem product = ServiceOrderRepository.LoadProductIn(conn, tx, productId);
                if (product == null || product.Status != 30)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.ProductDeleted);
                    return;
                }
                if (product.TempLocked)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.ProductTempLocked);
                    return;
                }
                if (product.ProductTypeId == ProductRules.TypeHourlyService)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.HourlyServiceNotSupported);
                    return;
                }

                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, header.WarehouseId);

                if (!allowNegativeStock)
                {
                    string stockErr = CheckStockForAdditionIn(conn, tx, header, product, quantity, warehouseId, openComboComponents);
                    if (stockErr != null)
                    {
                        outcome = ServiceOpResult.Fail(stockErr);
                        return;
                    }
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                decimal price = overridePrice.HasValue
                    ? overridePrice.Value
                    : OrderCalculator.ResolveAreaPrice(product, header.PriceLevelId);
                string itemName = string.IsNullOrWhiteSpace(overrideName) ? product.Name : overrideName.Trim();
                string unitId = string.IsNullOrEmpty(overrideUnitId) ? product.UnitId : overrideUnitId;

                AddLineCoreIn(conn, tx, header, product, quantity, price, itemName, unitId, note ?? "", openComboComponents, now);
                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, allowNegativeStock);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.ThemMon,
                        BillId = header.Id,
                        TableName = header.TableName,
                        Note = "Thêm " + itemName,
                        ItemName = itemName,
                        Quantity = quantity,
                        UnitPrice = price,
                        Amount = quantity * price,
                        Time = now
                    }
                });

                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded, promoWarning);
            });

            return outcome ?? ServiceOpResult.Fail("Không thể thêm mặt hàng.");
        }

        // ------------------------------------------------------------------ Tăng / Giảm / Đặt SL / Xóa dòng (§3.5, §6.4, §6.6)

        public ServiceOpResult IncreaseLineQty(string billId, string tableId, string lineId, decimal delta)
        {
            if (string.IsNullOrEmpty(lineId))
                return ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
            if (delta <= 0m)
                return ServiceOpResult.Fail(ServiceOrderConstants.EnterQuantity);

            ServiceOpResult outcome = null;
            decimal roundStep = SystemConfig.LamTronTien;
            bool allowNegativeStock = SystemConfig.ChoPhepAmKho;

            _repo.RunInTransaction((conn, tx) =>
            {
                int tempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, billId, tableId, out tempPrint))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.BillAlreadyPaid);
                    return;
                }
                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                ServiceOrderLine line = FindEditableParentLine(header, lineId);
                if (line == null)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
                    return;
                }

                if (!allowNegativeStock && !string.IsNullOrEmpty(line.ProductId))
                {
                    ServiceMenuItem prod = ServiceOrderRepository.LoadProductIn(conn, tx, line.ProductId);
                    string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, line.WarehouseId ?? header.WarehouseId);
                    string stockErr = CheckStockForAdditionIn(conn, tx, header, prod, delta, warehouseId, null);
                    if (stockErr != null)
                    {
                        outcome = ServiceOpResult.Fail(stockErr);
                        return;
                    }
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                line.Quantity += delta;
                OrderCalculator.RecalcLine(line);
                ServiceOrderRepository.UpdateLineIn(conn, tx, line, _session.UserId, now);
                ServiceOrderRepository.UpdateComboChildrenQtyIn(conn, tx, billId, line.ComboId, line.Quantity, _session.UserId, now);
                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, allowNegativeStock);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.TangMon,
                        BillId = header.Id,
                        TableName = header.TableName,
                        Note = "Tăng số lượng " + line.ItemName + " lên " + line.Quantity,
                        ItemName = line.ItemName,
                        Quantity = delta,
                        UnitPrice = line.UnitPrice,
                        Amount = line.Amount,
                        Time = now
                    }
                });

                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded, promoWarning);
            });

            return outcome ?? ServiceOpResult.Fail("Không thể tăng số lượng.");
        }

        public ServiceOpResult ReduceLineQty(
            string billId,
            string tableId,
            string lineId,
            decimal reduceBy,
            bool keepMinOne,
            UserSession overrideSession = null)
        {
            if (string.IsNullOrEmpty(lineId))
                return ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
            if (reduceBy <= 0m)
                return ServiceOpResult.Fail(ServiceOrderConstants.EnterQuantity);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncXoaGiamMon))
                return ServiceOpResult.ReLogin(ServiceOrderConstants.FuncXoaGiamMon);
            if (SystemConfig.NhapMatKhauGiamDo)
                return ServiceOpResult.Fail(ServiceOrderConstants.ReducePasswordBlocked);

            ServiceOpResult outcome = null;
            decimal roundStep = SystemConfig.LamTronTien;

            _repo.RunInTransaction((conn, tx) =>
            {
                int tempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, billId, tableId, out tempPrint))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.BillAlreadyPaid);
                    return;
                }
                if (tempPrint > 0 && !CanModifyAfterTempPrint(actor))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.CannotReduceTempPrinted);
                    return;
                }

                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                ServiceOrderLine line = FindEditableParentLine(header, lineId);
                if (line == null)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
                    return;
                }

                decimal targetQty;
                if (keepMinOne)
                {
                    if (line.Quantity <= 1m)
                    {
                        outcome = ServiceOpResult.Success(header);
                        return;
                    }
                    targetQty = Math.Max(1m, line.Quantity - reduceBy);
                }
                else
                {
                    if (line.Quantity < reduceBy + 1m)
                    {
                        outcome = ServiceOpResult.Fail(ServiceOrderConstants.CannotReduceExceedsCurrent(reduceBy, line.Quantity));
                        return;
                    }
                    targetQty = line.Quantity - reduceBy;
                }

                string kitchenErr = CheckKitchenPrintBeforeReduceIn(conn, tx, header, line, targetQty, actor, false);
                if (kitchenErr != null)
                {
                    outcome = ServiceOpResult.Fail(kitchenErr);
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                decimal oldQty = line.Quantity;
                line.Quantity = targetQty;
                OrderCalculator.RecalcLine(line);
                ServiceOrderRepository.UpdateLineIn(conn, tx, line, actor.UserId, now);
                ServiceOrderRepository.UpdateComboChildrenQtyIn(conn, tx, billId, line.ComboId, line.Quantity, actor.UserId, now);
                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, SystemConfig.ChoPhepAmKho, actor.UserId);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.GiamMon,
                        BillId = header.Id,
                        TableName = header.TableName,
                        Note = string.Format("Giảm {0}: {1} -> {2}", line.ItemName, oldQty, targetQty),
                        ItemName = line.ItemName,
                        Quantity = oldQty - targetQty,
                        UnitPrice = line.UnitPrice,
                        Amount = line.Amount,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = now
                    }
                });

                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded, promoWarning);
            });

            return outcome ?? ServiceOpResult.Fail("Không thể giảm số lượng.");
        }

        public ServiceOpResult SetLineQty(
            string billId,
            string tableId,
            string lineId,
            decimal newQty,
            UserSession overrideSession = null)
        {
            if (string.IsNullOrEmpty(lineId))
                return ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
            if (newQty <= 0m)
                return ServiceOpResult.Fail(ServiceOrderConstants.EnterQuantity);

            UserSession actor = overrideSession ?? _session;
            ServiceOpResult outcome = null;
            decimal roundStep = SystemConfig.LamTronTien;
            bool allowNegativeStock = SystemConfig.ChoPhepAmKho;

            _repo.RunInTransaction((conn, tx) =>
            {
                int tempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, billId, tableId, out tempPrint))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.BillAlreadyPaid);
                    return;
                }

                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                ServiceOrderLine line = FindEditableParentLine(header, lineId);
                if (line == null)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
                    return;
                }
                if (newQty == line.Quantity)
                {
                    outcome = ServiceOpResult.Success(header);
                    return;
                }

                if (newQty < line.Quantity)
                {
                    if (!HasFunction(actor, ServiceOrderConstants.FuncXoaGiamMon))
                    {
                        outcome = ServiceOpResult.ReLogin(ServiceOrderConstants.FuncXoaGiamMon);
                        return;
                    }
                    if (SystemConfig.NhapMatKhauGiamDo)
                    {
                        outcome = ServiceOpResult.Fail(ServiceOrderConstants.ReducePasswordBlocked);
                        return;
                    }
                    if (tempPrint > 0 && !CanModifyAfterTempPrint(actor))
                    {
                        outcome = ServiceOpResult.Fail(ServiceOrderConstants.CannotSetQtyTempPrinted);
                        return;
                    }
                    string kitchenErr = CheckKitchenPrintBeforeReduceIn(conn, tx, header, line, newQty, actor, false);
                    if (kitchenErr != null)
                    {
                        outcome = ServiceOpResult.Fail(kitchenErr);
                        return;
                    }
                }
                else if (!allowNegativeStock && !string.IsNullOrEmpty(line.ProductId))
                {
                    ServiceMenuItem prod = ServiceOrderRepository.LoadProductIn(conn, tx, line.ProductId);
                    string warehouseId = ServiceOrderRepository.ResolveWarehouseIdIn(conn, tx, line.WarehouseId ?? header.WarehouseId);
                    string stockErr = CheckStockForAdditionIn(conn, tx, header, prod, newQty - line.Quantity, warehouseId, null);
                    if (stockErr != null)
                    {
                        outcome = ServiceOpResult.Fail(stockErr);
                        return;
                    }
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                decimal oldQty = line.Quantity;
                line.Quantity = newQty;
                OrderCalculator.RecalcLine(line);
                ServiceOrderRepository.UpdateLineIn(conn, tx, line, actor.UserId, now);
                ServiceOrderRepository.UpdateComboChildrenQtyIn(conn, tx, billId, line.ComboId, line.Quantity, actor.UserId, now);
                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, allowNegativeStock, actor.UserId);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.DatSoLuong,
                        BillId = header.Id,
                        TableName = header.TableName,
                        Note = string.Format("Đặt SL {0}: {1} -> {2}", line.ItemName, oldQty, newQty),
                        ItemName = line.ItemName,
                        Quantity = newQty,
                        UnitPrice = line.UnitPrice,
                        Amount = line.Amount,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = now
                    }
                });

                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded, promoWarning);
            });

            return outcome ?? ServiceOpResult.Fail("Không thể đặt số lượng.");
        }

        public ServiceOpResult DeleteLine(
            string billId,
            string tableId,
            string lineId,
            string reason = "",
            UserSession overrideSession = null)
        {
            if (string.IsNullOrEmpty(lineId))
                return ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncXoaGiamMon))
                return ServiceOpResult.ReLogin(ServiceOrderConstants.FuncXoaGiamMon);
            if (SystemConfig.NhapMatKhauGiamDo)
                return ServiceOpResult.Fail(ServiceOrderConstants.ReducePasswordBlocked);
            if (SystemConfig.KichHoatLuuVetHoatDong && SystemConfig.NhapLyDoKhiXoaMon && string.IsNullOrWhiteSpace(reason))
                return ServiceOpResult.Fail(ServiceOrderConstants.DeleteReasonRequired);

            ServiceOpResult outcome = null;
            decimal roundStep = SystemConfig.LamTronTien;

            _repo.RunInTransaction((conn, tx) =>
            {
                int tempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, billId, tableId, out tempPrint))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.BillAlreadyPaid);
                    return;
                }
                if (tempPrint > 0 && !CanModifyAfterTempPrint(actor))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.CannotDeleteTempPrinted);
                    return;
                }

                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                ServiceOrderLine line = FindEditableParentLine(header, lineId);
                if (line == null)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
                    return;
                }

                string kitchenErr = CheckKitchenPrintBeforeReduceIn(conn, tx, header, line, 0m, actor, true);
                if (kitchenErr != null)
                {
                    outcome = ServiceOpResult.Fail(kitchenErr);
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                ServiceOrderRepository.DeleteLineAndComboChildrenIn(conn, tx, billId, line.Id, line.ComboId);
                header.Lines.RemoveAll(x => x.Id == line.Id || (!string.IsNullOrEmpty(line.ComboId) && x.ComboParentId == line.ComboId));
                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, SystemConfig.ChoPhepAmKho, actor.UserId);

                string note = string.IsNullOrWhiteSpace(reason)
                    ? "Xóa món " + line.ItemName
                    : "Xóa món " + line.ItemName + " (Lý do: " + reason.Trim() + ")";
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.XoaMon,
                        BillId = header.Id,
                        TableName = header.TableName,
                        Note = note,
                        ItemName = line.ItemName,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Amount = line.Amount,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = now
                    }
                });

                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded, promoWarning);
            });

            return outcome ?? ServiceOpResult.Fail("Không thể xóa mặt hàng.");
        }
    }
}
