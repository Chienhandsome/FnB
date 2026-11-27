using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        // ------------------------------------------------------------------ Đổi giá / Ghi chú / Giảm giá / Thuế / Phí

        public ServiceOpResult ChangeLinePrice(string billId, string tableId, string lineId, decimal newPrice, UserSession overrideSession = null)
        {
            if (newPrice < 0m)
                return ServiceOpResult.Fail("Đơn giá không được âm.");
            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncSuaDonGia))
                return ServiceOpResult.ReLogin(ServiceOrderConstants.FuncSuaDonGia);

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
                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                ServiceOrderLine line = FindEditableParentLine(header, lineId);
                if (line == null)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                decimal oldPrice = line.UnitPrice;
                line.UnitPrice = newPrice;
                if (newPrice == 0m)
                {
                    line.KhuyenMai = 0;
                    line.DiscountByMoney = false;
                    line.DiscountRate = 0m;
                    line.DiscountAmount = 0m;
                }
                OrderCalculator.RecalcLine(line);
                ServiceOrderRepository.UpdateLineIn(conn, tx, line, actor.UserId, now);
                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, SystemConfig.ChoPhepAmKho, actor.UserId);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.DoiGiaBan,
                        BillId = header.Id,
                        TableName = header.TableName,
                        Note = string.Format("Đổi giá {0}: {1} -> {2}", line.ItemName, oldPrice, newPrice),
                        ItemName = line.ItemName,
                        Quantity = line.Quantity,
                        UnitPrice = newPrice,
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
            return outcome ?? ServiceOpResult.Fail("Không thể đổi đơn giá.");
        }

        public ServiceOpResult ChangeLineNote(string billId, string tableId, string lineId, string note)
        {
            ServiceOpResult outcome = null;
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
                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                line.Note = (note ?? "").Trim();
                ServiceOrderRepository.UpdateLineIn(conn, tx, line, _session.UserId, now);
                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, SystemConfig.LamTronTien);
                outcome = ServiceOpResult.Success(loaded);
            });
            return outcome ?? ServiceOpResult.Fail("Không thể cập nhật ghi chú.");
        }

        public ServiceOpResult ChangeLineDiscount(
            string billId,
            string tableId,
            string lineId,
            decimal discountRate,
            decimal discountAmount,
            bool discountByMoney,
            UserSession overrideSession = null)
        {
            if (discountRate < 0m || discountRate > 100m || discountAmount < 0m)
                return ServiceOpResult.Fail("Giá trị giảm giá không hợp lệ.");
            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncGiamGiaMatHang))
                return ServiceOpResult.ReLogin(ServiceOrderConstants.FuncGiamGiaMatHang);

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
                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                ServiceOrderLine line = FindEditableParentLine(header, lineId);
                if (line == null)
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.SelectLineToReduce);
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                // Người dùng sửa tay giảm giá dòng: bỏ cờ KM của dòng đó rồi so lớn hơn với khuyến mại.
                line.KhuyenMai = 0;
                line.DiscountByMoney = discountByMoney;
                line.DiscountRate = discountRate;
                line.DiscountAmount = discountAmount;
                OrderCalculator.RecalcLine(line);
                ServiceOrderRepository.UpdateLineIn(conn, tx, line, actor.UserId, now);
                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, SystemConfig.ChoPhepAmKho, actor.UserId);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.DatGiamGiaMatHang,
                        BillId = header.Id,
                        TableName = header.TableName,
                        Note = string.Format("Giảm giá {0}: {1}%", line.ItemName, line.DiscountRate),
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
            return outcome ?? ServiceOpResult.Fail("Không thể đặt giảm giá dòng.");
        }

        public ServiceOpResult ApplyGroupDiscount(string billId, string tableId, GroupDiscountInput input, UserSession overrideSession = null)
        {
            if (input == null)
                return ServiceOpResult.Fail("Thiếu dữ liệu giảm giá theo nhóm.");
            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncGiamGiaMatHang))
                return ServiceOpResult.ReLogin(ServiceOrderConstants.FuncGiamGiaMatHang);

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
                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                foreach (ServiceOrderLine line in header.Lines)
                {
                    if (line.IsComboChild || line.IsMaterialExport || line.IsPromoGift)
                        continue;
                    decimal rate = RateForDrinkType(line.DrinkTypeId, input);
                    line.KhuyenMai = 0;
                    line.DiscountByMoney = false;
                    line.DiscountRate = rate;
                    OrderCalculator.RecalcLine(line);
                    ServiceOrderRepository.UpdateLineIn(conn, tx, line, actor.UserId, now);
                }
                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, SystemConfig.ChoPhepAmKho, actor.UserId);
                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded, promoWarning);
            });
            return outcome ?? ServiceOpResult.Fail("Không thể áp dụng giảm giá theo nhóm.");
        }

        public ServiceOpResult UpdateBillFinancials(
            string billId,
            string tableId,
            decimal tileGiamGia,
            decimal tienGiamGia,
            bool giamTheoTien,
            decimal tilePhiDichVu,
            decimal phiDichVu,
            bool phiDichVuTheoTien,
            decimal tileThue,
            UserSession overrideSession = null)
        {
            UserSession actor = overrideSession ?? _session;
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
                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);

                bool discountChanged = tileGiamGia != header.TileGiamGia || tienGiamGia != header.TienGiamGia || giamTheoTien != header.GiamTheoTien;
                if (discountChanged && (!SystemConfig.ChoPhepNhapGiamGia || !HasFunction(actor, ServiceOrderConstants.FuncThayDoiGiamGiaTong)))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.NoPermission);
                    return;
                }
                bool feeChanged = tilePhiDichVu != header.TilePhiDichVu || phiDichVu != header.PhiDichVu || phiDichVuTheoTien != header.PhiDichVuTheoTien;
                if (feeChanged && (!SystemConfig.CoPhiDichVu || !HasFunction(actor, ServiceOrderConstants.FuncThayDoiPhiDichVu)))
                {
                    outcome = ServiceOpResult.Fail(ServiceOrderConstants.NoPermission);
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                if (discountChanged)
                {
                    // Người dùng sửa tay giảm giá tổng: bỏ cờ giảm tổng KM rồi so lớn hơn với khuyến mại.
                    header.BillDiscountFromPromo = false;
                    header.TileGiamGia = Math.Max(0m, tileGiamGia);
                    header.TienGiamGia = Math.Max(0m, tienGiamGia);
                    header.GiamTheoTien = giamTheoTien;
                }
                header.TilePhiDichVu = Math.Max(0m, tilePhiDichVu);
                header.PhiDichVu = Math.Max(0m, phiDichVu);
                header.PhiDichVuTheoTien = phiDichVuTheoTien;
                header.TileThue = Math.Max(0m, tileThue);

                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, header, roundStep, now, SystemConfig.ChoPhepAmKho, actor.UserId);
                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded, promoWarning);
            });

            return outcome ?? ServiceOpResult.Fail("Không thể cập nhật tổng hóa đơn.");
        }

        public ServiceOpResult UpdateBillMeta(string billId, string tableId, string customerId, int guestCount, string note)
        {
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
                ServiceOrderHeader header = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                header.CustomerId = string.IsNullOrEmpty(customerId) ? null : customerId;
                header.GuestCount = Math.Max(1, guestCount);
                header.Note = (note ?? "").Trim();
                RecalcAndSaveHeaderIn(conn, tx, header, roundStep, now);
                ServiceOrderHeader loaded = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                PopulatePromotionLabelsIn(conn, tx, loaded, roundStep);
                outcome = ServiceOpResult.Success(loaded);
            });

            return outcome ?? ServiceOpResult.Fail("Không thể cập nhật thông tin hóa đơn.");
        }
    }
}
