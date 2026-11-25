using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Công thức thuần <c>decimal</c> cho báo cáo "Tổng hợp kết quả kinh doanh"
    /// (docs/specs/tong-hop-ket-qua-kinh-doanh.md):
    /// tính khoảng kỳ trước (C2), dựng cây dòng theo LOAI 0..9, tăng/giảm (C1),
    /// biểu tượng ▲/▼ (C7) và 3 cột tỉ lệ theo bảng §2.1 (làm tròn về chẵn).
    /// </summary>
    public static class BusinessResultCalculator
    {
        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>
        /// Tính khoảng ngày "Tháng trước" theo quy tắc C2:
        /// Từ trước = Từ.AddMonths(-1);
        /// Đến trước = nếu Đến là ngày cuối tháng thì là ngày cuối của tháng trước, ngược lại Đến.AddMonths(-1).
        /// </summary>
        public static BusinessResultPeriodRange CalculatePreviousPeriod(DateTime fromDate, DateTime toDate)
        {
            DateTime from = fromDate.Date;
            DateTime to = toDate.Date;

            DateTime prevFrom = from.AddMonths(-1);
            DateTime shiftedTo = to.AddMonths(-1);

            bool toIsEndOfMonth = to.Day == DateTime.DaysInMonth(to.Year, to.Month);
            DateTime prevTo = toIsEndOfMonth
                ? new DateTime(shiftedTo.Year, shiftedTo.Month, DateTime.DaysInMonth(shiftedTo.Year, shiftedTo.Month))
                : shiftedTo;

            return new BusinessResultPeriodRange
            {
                FromDate = prevFrom,
                ToDate = prevTo
            };
        }

        /// <summary>
        /// Công thức tỉ lệ theo §2.1:
        /// nếu giá trị dòng = 0 hoặc mẫu số = 0 thì "-";
        /// ngược lại Math.Round(giá trị × 100 / mẫu số, MidpointRounding.ToEven) + "%".
        /// Giá trị khác 0 nhưng làm tròn ra 0 vẫn hiện "0%".
        /// </summary>
        public static string FormatRatio(decimal value, decimal denominator)
        {
            if (value == 0m || denominator == 0m)
                return BusinessResultConstants.RatioDash;

            decimal rounded = Math.Round(value * 100m / denominator, 0, MidpointRounding.ToEven);
            return rounded.ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>Biểu tượng tăng/giảm theo dấu: &gt; 0 → ▲, &lt; 0 → ▼, = 0 → trống (C7).</summary>
        public static string TrendSymbolOf(decimal change)
        {
            if (change > 0m)
                return BusinessResultConstants.TrendUp;
            if (change < 0m)
                return BusinessResultConstants.TrendDown;
            return "";
        }

        /// <summary>Định dạng tiền VNĐ số nguyên có dấu phân nhóm nghìn (n0, vi-VN).</summary>
        public static string FormatMoney(decimal value)
        {
            return value.ToString("n0", ViCulture);
        }

        /// <summary>
        /// Dựng danh sách dòng báo cáo theo đúng thứ tự LOAI 0..9, số mục, nhãn,
        /// tăng/giảm = kỳ này − kỳ trước cho mọi dòng (kể cả LÃI/LỖ, C1) và đủ 3 cột tỉ lệ (§2.1).
        /// </summary>
        public static List<BusinessResultRow> BuildRows(BusinessResultRawData raw)
        {
            BusinessResultRawData data = raw ?? new BusinessResultRawData();
            BusinessResultBillTotals curBills = data.CurrentBills ?? new BusinessResultBillTotals();
            BusinessResultBillTotals prevBills = data.PreviousBills ?? new BusinessResultBillTotals();

            List<BusinessResultNamedAmount> revGroups = FilterAndSortGroups(data.RevenueGroups);
            List<BusinessResultNamedAmount> varGroups = FilterAndSortGroups(data.VariableCostGroups);
            List<BusinessResultNamedAmount> fixedReasons = CopyReasons(data.FixedCostReasons);
            List<BusinessResultNamedAmount> otherReasons = CopyReasons(data.OtherCostReasons);

            // Các tổng mẫu số kỳ này theo §2.1 và tổng 2 kỳ cho dòng cấp trên.
            decimal dtCur = curBills.Revenue;
            decimal dtPrev = prevBills.Revenue;

            // N_tong = tổng THANGNAY của các dòng LOAI 1 (các nhóm hàng + Giảm giá dương + Phí DV + Thuế; không Tiền giờ) theo C4.
            decimal nTongCur = curBills.Discount + curBills.ServiceFee + curBills.Tax;
            foreach (BusinessResultNamedAmount g in revGroups)
                nTongCur += g.CurrentAmount;

            decimal dpCur = SumCurrent(fixedReasons);
            decimal dpPrev = SumPrevious(fixedReasons);

            decimal bpCur = SumCurrent(varGroups);
            decimal bpPrev = SumPrevious(varGroups);

            decimal ckCur = SumCurrent(otherReasons);
            decimal ckPrev = SumPrevious(otherReasons);

            decimal cpCur = dpCur + bpCur + ckCur;
            decimal cpPrev = dpPrev + bpPrev + ckPrev;

            decimal plCur = dtCur - cpCur;
            decimal plPrev = dtPrev - cpPrev;

            var rows = new List<BusinessResultRow>(16 + revGroups.Count + varGroups.Count + fixedReasons.Count + otherReasons.Count);

            // LOAI 0: I. DOANH THU (đậm, cột % cố định "100%" kể cả khi 0).
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiRevenue,
                "I.",
                BusinessResultConstants.LabelRevenue,
                "",
                dtCur,
                BusinessResultConstants.RatioFull,
                "",
                dtPrev));

            // LOAI 1: con 1, 2, 3... liền nhau (nhóm hàng theo tên trước, rồi Giảm giá, Phí dịch vụ, Thuế; không có Tiền giờ).
            int revIdx = 1;
            foreach (BusinessResultNamedAmount g in revGroups)
            {
                rows.Add(CreateRow(
                    BusinessResultConstants.LoaiRevenueDetail,
                    (revIdx++).ToString(CultureInfo.InvariantCulture),
                    g.Name,
                    "",
                    g.CurrentAmount,
                    FormatRatio(g.CurrentAmount, nTongCur),
                    "",
                    g.PreviousAmount));
            }
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiRevenueDetail,
                (revIdx++).ToString(CultureInfo.InvariantCulture),
                BusinessResultConstants.LabelDiscount,
                "",
                curBills.Discount,
                FormatRatio(curBills.Discount, nTongCur),
                "",
                prevBills.Discount));
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiRevenueDetail,
                (revIdx++).ToString(CultureInfo.InvariantCulture),
                BusinessResultConstants.LabelServiceFee,
                "",
                curBills.ServiceFee,
                FormatRatio(curBills.ServiceFee, nTongCur),
                "",
                prevBills.ServiceFee));
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiRevenueDetail,
                (revIdx++).ToString(CultureInfo.InvariantCulture),
                BusinessResultConstants.LabelTax,
                "",
                curBills.Tax,
                FormatRatio(curBills.Tax, nTongCur),
                "",
                prevBills.Tax));

            // LOAI 2: II. CHI PHÍ (đậm, cột 3 cố định "100%").
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiCost,
                "II.",
                BusinessResultConstants.LabelCost,
                FormatRatio(cpCur, dtCur),
                cpCur,
                "",
                BusinessResultConstants.RatioFull,
                cpPrev));

            // LOAI 3: A. Định phí (cột 3 cố định "100%").
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiFixedCost,
                "A.",
                BusinessResultConstants.LabelFixedCost,
                FormatRatio(dpCur, dtCur),
                dpCur,
                FormatRatio(dpCur, cpCur),
                BusinessResultConstants.RatioFull,
                dpPrev));

            // LOAI 4: chi tiết Định phí (1..n, hiện đủ kể cả 0/0, giữ nguyên thứ tự DB).
            for (int i = 0; i < fixedReasons.Count; i++)
            {
                BusinessResultNamedAmount r = fixedReasons[i];
                rows.Add(CreateRow(
                    BusinessResultConstants.LoaiFixedCostDetail,
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    r.Name,
                    FormatRatio(r.CurrentAmount, dtCur),
                    r.CurrentAmount,
                    FormatRatio(r.CurrentAmount, cpCur),
                    FormatRatio(r.CurrentAmount, dpCur),
                    r.PreviousAmount));
            }

            // LOAI 5: B. Biến phí (cột 3 cố định "100%").
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiVariableCost,
                "B.",
                BusinessResultConstants.LabelVariableCost,
                FormatRatio(bpCur, dtCur),
                bpCur,
                FormatRatio(bpCur, cpCur),
                BusinessResultConstants.RatioFull,
                bpPrev));

            // LOAI 6: chi tiết Biến phí (1..n, chỉ nhóm có kỳ này hoặc kỳ trước ≠ 0).
            for (int i = 0; i < varGroups.Count; i++)
            {
                BusinessResultNamedAmount g = varGroups[i];
                rows.Add(CreateRow(
                    BusinessResultConstants.LoaiVariableCostDetail,
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    g.Name,
                    FormatRatio(g.CurrentAmount, dtCur),
                    g.CurrentAmount,
                    FormatRatio(g.CurrentAmount, cpCur),
                    FormatRatio(g.CurrentAmount, bpCur),
                    g.PreviousAmount));
            }

            // LOAI 7: C. Chi khác (cột 3 cố định "100%").
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiOtherCost,
                "C.",
                BusinessResultConstants.LabelOtherCost,
                FormatRatio(ckCur, dtCur),
                ckCur,
                FormatRatio(ckCur, cpCur),
                BusinessResultConstants.RatioFull,
                ckPrev));

            // LOAI 8: chi tiết Chi khác (1..n, hiện đủ kể cả 0/0, giữ nguyên thứ tự DB).
            for (int i = 0; i < otherReasons.Count; i++)
            {
                BusinessResultNamedAmount r = otherReasons[i];
                rows.Add(CreateRow(
                    BusinessResultConstants.LoaiOtherCostDetail,
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    r.Name,
                    FormatRatio(r.CurrentAmount, dtCur),
                    r.CurrentAmount,
                    FormatRatio(r.CurrentAmount, cpCur),
                    FormatRatio(r.CurrentAmount, ckCur),
                    r.PreviousAmount));
            }

            // LOAI 9: III. LÃI/LỖ (đậm; %/DT trống, % = /DT_tong, cột 3 = /CP_tong; Tăng/giảm = plCur - plPrev theo C1).
            rows.Add(CreateRow(
                BusinessResultConstants.LoaiProfitLoss,
                "III.",
                BusinessResultConstants.LabelProfitLoss,
                "",
                plCur,
                FormatRatio(plCur, dtCur),
                FormatRatio(plCur, cpCur),
                plPrev));

            return rows;
        }

        private static BusinessResultRow CreateRow(
            int loai,
            string itemNumber,
            string content,
            string percentOfRevenue,
            decimal currentValue,
            string percentMain,
            string percentThird,
            decimal previousValue)
        {
            decimal change = currentValue - previousValue;
            return new BusinessResultRow
            {
                Loai = loai,
                ItemNumber = itemNumber ?? "",
                Content = content ?? "",
                PercentOfRevenue = percentOfRevenue ?? "",
                CurrentValue = currentValue,
                PercentMain = percentMain ?? "",
                PercentThird = percentThird ?? "",
                TrendSymbol = TrendSymbolOf(change),
                ChangeAmount = change,
                PreviousValue = previousValue,
                IsBold = loai == BusinessResultConstants.LoaiRevenue
                    || loai == BusinessResultConstants.LoaiCost
                    || loai == BusinessResultConstants.LoaiProfitLoss,
                IsGroupHeader = loai == BusinessResultConstants.LoaiFixedCost
                    || loai == BusinessResultConstants.LoaiVariableCost
                    || loai == BusinessResultConstants.LoaiOtherCost
            };
        }

        private static List<BusinessResultNamedAmount> FilterAndSortGroups(List<BusinessResultNamedAmount> source)
        {
            var result = new List<BusinessResultNamedAmount>();
            if (source == null)
                return result;

            foreach (BusinessResultNamedAmount item in source)
            {
                if (item == null)
                    continue;
                if (item.CurrentAmount == 0m && item.PreviousAmount == 0m)
                    continue;
                string name = string.IsNullOrEmpty(item.Name) ? BusinessResultConstants.UngroupedName : item.Name;
                result.Add(new BusinessResultNamedAmount
                {
                    Id = item.Id,
                    Name = name,
                    CurrentAmount = item.CurrentAmount,
                    PreviousAmount = item.PreviousAmount
                });
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result;
        }

        private static List<BusinessResultNamedAmount> CopyReasons(List<BusinessResultNamedAmount> source)
        {
            var result = new List<BusinessResultNamedAmount>();
            if (source == null)
                return result;

            foreach (BusinessResultNamedAmount item in source)
            {
                if (item == null)
                    continue;
                result.Add(new BusinessResultNamedAmount
                {
                    Id = item.Id,
                    Name = item.Name ?? "",
                    CurrentAmount = item.CurrentAmount,
                    PreviousAmount = item.PreviousAmount
                });
            }
            return result;
        }

        private static decimal SumCurrent(List<BusinessResultNamedAmount> items)
        {
            decimal sum = 0m;
            foreach (BusinessResultNamedAmount item in items)
                sum += item.CurrentAmount;
            return sum;
        }

        private static decimal SumPrevious(List<BusinessResultNamedAmount> items)
        {
            decimal sum = 0m;
            foreach (BusinessResultNamedAmount item in items)
                sum += item.PreviousAmount;
            return sum;
        }
    }
}
