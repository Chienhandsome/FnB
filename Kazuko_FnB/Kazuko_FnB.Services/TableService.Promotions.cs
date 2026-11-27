using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        // ------------------------------------------------------------------ Áp dụng khuyến mại khi bán (spec danh-muc-khuyen-mai.md P2 §4)

        public static string FormatPromotionTag(string promotionName)
        {
            string trimmed = (promotionName ?? "").Trim();
            return trimmed.Length > 0 ? ("KM: " + trimmed) : "KM";
        }

        /// <summary>
        /// Tính lại toàn bộ khuyến mại (dòng, tổng hóa đơn, dòng tặng 0 đồng có kiểm tồn kho) cho hóa đơn chưa thanh toán,
        /// ghi các thay đổi vào TDONHANGCHITIET và gọi OrderCalculator qua RecalcAndSaveHeaderIn.
        /// Hóa đơn đã thanh toán (PaidFlag != 0) KHÔNG bao giờ tự tính lại.
        /// </summary>
        private string ApplyPromotionsAndRecalcIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader header,
            decimal roundStep,
            DateTime now,
            bool allowNegativeStock,
            string actorId = null)
        {
            if (header == null || header.PaidFlag != 0)
                return null;

            string effectiveActor = string.IsNullOrEmpty(actorId) ? (_session.UserId ?? "") : actorId;
            DateTime saleDate = header.Ngay == DateTime.MinValue ? now.Date : header.Ngay.Date;
            List<ActivePromotionBundle> activePromos = PromotionRepository.LoadActiveBundlesIn(conn, tx, saleDate);

            bool hasExistingPromoState = header.BillDiscountFromPromo
                || (header.Lines != null && header.Lines.Exists(l => l != null && l.KhuyenMai == 30));

            // Nhanh gọn khi không có đợt hiệu lực và bill không mang cờ KM: giữ nguyên hành vi & kết quả cũ 100%.
            if (activePromos.Count == 0 && !hasExistingPromoState)
            {
                header.BillPromotionName = "";
                RecalcAndSaveHeaderIn(conn, tx, header, roundStep, now);
                return null;
            }

            Dictionary<string, string> groupParents = NeedProductGroupHierarchy(activePromos)
                ? ProductGroupRepository.LoadParentMapIn(conn, tx)
                : null;

            List<ServiceOrderLine> regularLines;
            List<ServiceOrderLine> existingGifts;
            PromotionApplyInput input = BuildPromotionInput(header, saleDate, roundStep, activePromos, groupParents, out regularLines, out existingGifts);
            PromotionApplyResult res = PromotionApplier.Apply(input);

            ApplyLinePromotionResultsIn(conn, tx, regularLines, res.Lines, effectiveActor, now);

            string promoWarning = SyncGiftLinesIn(
                conn, tx, header, existingGifts, res.Gifts, allowNegativeStock, effectiveActor, now);

            header.BillDiscountFromPromo = res.UsePromotionBillDiscount;
            header.GiamTheoTien = res.EffectiveBillDiscountByMoney;
            header.TileGiamGia = res.EffectiveBillDiscountRate;
            header.TienGiamGia = res.EffectiveBillDiscountAmount;
            header.BillPromotionName = res.UsePromotionBillDiscount ? (res.BillPromotionName ?? "") : "";

            RecalcAndSaveHeaderIn(conn, tx, header, roundStep, now);
            return promoWarning;
        }

        /// <summary>
        /// Nạp nhãn hiển thị nguồn khuyến mại (PromotionLabel / BillPromotionName) cho bill vừa đọc từ DB (chỉ đọc, không ghi DB).
        /// Hóa đơn đã thanh toán không chạy lại PromotionApplier.
        /// </summary>
        private static void PopulatePromotionLabelsIn(
            FbConnection conn,
            FbTransaction tx,
            ServiceOrderHeader header,
            decimal roundStep)
        {
            if (header == null || header.Lines == null)
                return;

            foreach (ServiceOrderLine l in header.Lines)
            {
                if (l == null)
                    continue;
                if (l.IsPromoGift)
                    l.PromotionLabel = !string.IsNullOrEmpty(l.Note) ? l.Note : "KM";
                else if (l.IsPromoDiscounted)
                    l.PromotionLabel = "KM";
                else
                    l.PromotionLabel = "";
            }
            header.BillPromotionName = "";

            if (header.PaidFlag != 0)
                return;

            bool needLookup = header.BillDiscountFromPromo
                || header.Lines.Exists(l => l != null && l.IsPromoDiscounted);
            if (!needLookup)
                return;

            DateTime saleDate = header.Ngay == DateTime.MinValue ? DateTime.Today : header.Ngay.Date;
            List<ActivePromotionBundle> activePromos = PromotionRepository.LoadActiveBundlesIn(conn, tx, saleDate);
            if (activePromos.Count == 0)
                return;

            Dictionary<string, string> groupParents = NeedProductGroupHierarchy(activePromos)
                ? ProductGroupRepository.LoadParentMapIn(conn, tx)
                : null;

            List<ServiceOrderLine> regularLines;
            List<ServiceOrderLine> existingGifts;
            PromotionApplyInput input = BuildPromotionInput(header, saleDate, roundStep, activePromos, groupParents, out regularLines, out existingGifts);
            PromotionApplyResult res = PromotionApplier.Apply(input);

            var byLineId = new Dictionary<string, ServiceOrderLine>(StringComparer.Ordinal);
            foreach (ServiceOrderLine l in regularLines)
            {
                if (!string.IsNullOrEmpty(l.Id))
                    byLineId[l.Id] = l;
            }
            foreach (PromotionLineApplyResult lr in res.Lines)
            {
                ServiceOrderLine line;
                if (lr.IsPromotionDiscount && !string.IsNullOrEmpty(lr.LineId) && byLineId.TryGetValue(lr.LineId, out line) && line.IsPromoDiscounted)
                    line.PromotionLabel = FormatPromotionTag(lr.PromotionName);
            }
            if (header.BillDiscountFromPromo && res.UsePromotionBillDiscount)
                header.BillPromotionName = res.BillPromotionName ?? "";
        }

        /// <summary>
        /// Khi chọn/nạp lại một hóa đơn chưa thanh toán: nếu có đợt khuyến mại đang hiệu lực hoặc bill đang mang cờ KM,
        /// đồng bộ lại khuyến mại trong một transaction; không có đợt và không có cờ KM thì không ghi DB.
        /// </summary>
        private ServiceOpResult RefreshUnpaidBillOnSelect(ServiceOrderHeader bill, string tableId)
        {
            if (bill == null || bill.PaidFlag != 0)
                return ServiceOpResult.Success(bill);

            decimal roundStep = SystemConfig.LamTronTien;
            bool allowNegativeStock = SystemConfig.ChoPhepAmKho;

            ServiceOpResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                int tempPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, bill.Id, tableId, out tempPrint))
                {
                    ServiceOrderHeader current = ServiceOrderRepository.LoadBillIn(conn, tx, bill.Id);
                    PopulatePromotionLabelsIn(conn, tx, current, roundStep);
                    outcome = ServiceOpResult.Success(current);
                    return;
                }

                ServiceOrderHeader fresh = ServiceOrderRepository.LoadBillIn(conn, tx, bill.Id);
                if (fresh == null || fresh.PaidFlag != 0)
                {
                    PopulatePromotionLabelsIn(conn, tx, fresh, roundStep);
                    outcome = ServiceOpResult.Success(fresh);
                    return;
                }

                DateTime now = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                DateTime saleDate = fresh.Ngay == DateTime.MinValue ? now.Date : fresh.Ngay.Date;
                List<ActivePromotionBundle> activePromos = PromotionRepository.LoadActiveBundlesIn(conn, tx, saleDate);
                bool hasPromoState = fresh.BillDiscountFromPromo
                    || (fresh.Lines != null && fresh.Lines.Exists(l => l != null && l.KhuyenMai == 30));

                if (activePromos.Count == 0 && !hasPromoState)
                {
                    PopulatePromotionLabelsIn(conn, tx, fresh, roundStep);
                    outcome = ServiceOpResult.Success(fresh);
                    return;
                }

                string promoWarning = ApplyPromotionsAndRecalcIn(conn, tx, fresh, roundStep, now, allowNegativeStock);
                ServiceOrderHeader reloaded = ServiceOrderRepository.LoadBillIn(conn, tx, bill.Id);
                PopulatePromotionLabelsIn(conn, tx, reloaded, roundStep);
                outcome = ServiceOpResult.Success(reloaded, promoWarning);
            });

            return outcome ?? ServiceOpResult.Success(bill);
        }

        private static bool NeedProductGroupHierarchy(List<ActivePromotionBundle> activePromos)
        {
            if (activePromos == null)
                return false;
            for (int i = 0; i < activePromos.Count; i++)
            {
                if (activePromos[i] != null && activePromos[i].Header != null
                    && activePromos[i].Header.Kind == PromotionKind.ProductGroupPercent)
                    return true;
            }
            return false;
        }

        private static PromotionApplyInput BuildPromotionInput(
            ServiceOrderHeader header,
            DateTime saleDate,
            decimal roundStep,
            List<ActivePromotionBundle> activePromos,
            Dictionary<string, string> groupParents,
            out List<ServiceOrderLine> regularLines,
            out List<ServiceOrderLine> existingGifts)
        {
            regularLines = new List<ServiceOrderLine>();
            existingGifts = new List<ServiceOrderLine>();

            var input = new PromotionApplyInput
            {
                SaleDate = saleDate,
                RoundStep = roundStep,
                GroupParents = groupParents,
                Promotions = activePromos ?? new List<ActivePromotionBundle>(),
                ManualBillDiscountRate = header.BillDiscountFromPromo ? 0m : header.TileGiamGia,
                ManualBillDiscountAmount = header.BillDiscountFromPromo ? 0m : header.TienGiamGia,
                ManualBillDiscountByMoney = !header.BillDiscountFromPromo && header.GiamTheoTien
            };

            if (header.Lines != null)
            {
                foreach (ServiceOrderLine l in header.Lines)
                {
                    if (l == null || l.IsComboChild || l.IsMaterialExport)
                        continue;
                    if (l.IsPromoGift)
                    {
                        existingGifts.Add(l);
                        continue;
                    }
                    regularLines.Add(l);
                    bool lineFromPromo = l.KhuyenMai == 30;
                    input.Lines.Add(new PromotionBillLineInput
                    {
                        LineId = l.Id,
                        ProductId = l.ProductId,
                        ProductGroupId = l.ProductGroupId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        ManualDiscountRate = lineFromPromo ? 0m : l.DiscountRate,
                        ManualDiscountAmount = lineFromPromo ? 0m : l.DiscountAmount,
                        ManualDiscountByMoney = !lineFromPromo && l.DiscountByMoney
                    });
                }
            }
            return input;
        }

        private static void ApplyLinePromotionResultsIn(
            FbConnection conn,
            FbTransaction tx,
            List<ServiceOrderLine> regularLines,
            List<PromotionLineApplyResult> results,
            string actorId,
            DateTime now)
        {
            var byId = new Dictionary<string, ServiceOrderLine>(StringComparer.Ordinal);
            foreach (ServiceOrderLine l in regularLines)
            {
                if (!string.IsNullOrEmpty(l.Id))
                    byId[l.Id] = l;
            }

            foreach (PromotionLineApplyResult lr in results)
            {
                ServiceOrderLine line;
                if (string.IsNullOrEmpty(lr.LineId) || !byId.TryGetValue(lr.LineId, out line))
                    continue;

                bool oldByMoney = line.DiscountByMoney;
                decimal oldRate = line.DiscountRate;
                decimal oldDiscAmt = line.DiscountAmount;
                decimal oldAmt = line.Amount;
                int oldKm = line.KhuyenMai;

                line.DiscountByMoney = lr.EffectiveDiscountByMoney;
                line.DiscountRate = lr.EffectiveDiscountRate;
                line.DiscountAmount = lr.EffectiveDiscountAmount;
                line.KhuyenMai = lr.IsPromotionDiscount ? 30 : 0;
                line.PromotionLabel = lr.IsPromotionDiscount ? FormatPromotionTag(lr.PromotionName) : "";
                OrderCalculator.RecalcLine(line);

                if (oldByMoney != line.DiscountByMoney
                    || oldRate != line.DiscountRate
                    || oldDiscAmt != line.DiscountAmount
                    || oldAmt != line.Amount
                    || oldKm != line.KhuyenMai)
                {
                    ServiceOrderRepository.UpdateLineIn(conn, tx, line, actorId, now);
                }
            }
        }
    }
}
