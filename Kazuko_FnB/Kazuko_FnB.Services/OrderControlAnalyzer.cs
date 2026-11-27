using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Hàm thuần phân tích số order giấy (TDONHANG.SOORDER) cho màn "Kiểm soát order"
    /// (docs/specs/kiem-soat-order.md §2, §3). Không phụ thuộc DB hay UI.
    /// - Tách theo dấu phẩy, bỏ khoảng trắng hai đầu và bỏ phần rỗng.
    /// - Số nguyên hợp lệ chỉ gồm chữ số ASCII '0'..'9' và &lt;= int.MaxValue.
    /// - Quét dải thiếu bằng phép trừ trực tiếp (O(1) mỗi khoảng trống), không lặp từng số.
    /// </summary>
    public static class OrderControlAnalyzer
    {
        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>Kiểm tra khoảng ngày lọc: Từ ngày không được sau Đến ngày.</summary>
        public static string ValidateDateRange(DateTime fromDate, DateTime toDate)
        {
            if (fromDate.Date > toDate.Date)
                return OrderControlConstants.InvalidDateRangeMessage;
            return null;
        }

        /// <summary>
        /// Phân tích một token số order: chỉ chấp nhận chuỗi không rỗng gồm thuần chữ số ASCII '0'..'9'
        /// (không dấu +/-, không dấu chấm/phẩy, không chữ số Unicode khác) và giá trị &lt;= int.MaxValue.
        /// </summary>
        public static bool TryParseOrderNumber(string token, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(token))
                return false;

            long acc = 0;
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (c < '0' || c > '9')
                    return false;
                acc = acc * 10L + (c - '0');
                if (acc > int.MaxValue)
                    return false;
            }

            value = (int)acc;
            return true;
        }

        /// <summary>Phân tích danh sách hóa đơn thành lưới Đã dùng, lưới Cần kiểm tra và tóm tắt.</summary>
        public static OrderControlAnalysisResult Analyze(IEnumerable<OrderControlBillRow> bills)
        {
            var result = new OrderControlAnalysisResult();
            if (bills == null)
                return result;

            var orderedBills = new List<IndexedBill>();
            int seq = 0;
            foreach (OrderControlBillRow bill in bills)
            {
                if (bill == null)
                    continue;
                orderedBills.Add(new IndexedBill { Index = seq++, Bill = bill });
            }

            if (orderedBills.Count == 0)
                return result;

            orderedBills.Sort(delegate (IndexedBill a, IndexedBill b)
            {
                int cmp = DateTime.Compare(a.Bill.BillDate, b.Bill.BillDate);
                if (cmp != 0)
                    return cmp;
                cmp = string.CompareOrdinal(a.Bill.BillNumber ?? "", b.Bill.BillNumber ?? "");
                if (cmp != 0)
                    return cmp;
                return a.Index.CompareTo(b.Index);
            });

            var occurrences = new Dictionary<int, List<OrderControlBillRow>>();
            var invalidIssues = new List<OrderControlIssueRow>();

            for (int i = 0; i < orderedBills.Count; i++)
            {
                OrderControlBillRow bill = orderedBills[i].Bill;
                string raw = bill.OrderNumbers;
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                string billNumber = bill.BillNumber ?? "";
                string[] parts = raw.Split(',');
                for (int p = 0; p < parts.Length; p++)
                {
                    string token = parts[p].Trim();
                    if (token.Length == 0)
                        continue;

                    int orderNo;
                    if (!TryParseOrderNumber(token, out orderNo))
                    {
                        invalidIssues.Add(new OrderControlIssueRow
                        {
                            Kind = OrderControlIssueKind.Invalid,
                            TypeDisplay = OrderControlConstants.IssueTypeInvalid,
                            Message = string.Format(
                                CultureInfo.InvariantCulture,
                                "Lỗi số order '{0}' ở bill '{1}'",
                                token,
                                billNumber),
                            DateOrBillDisplay = FormatDateAndBill(bill.BillDate, billNumber),
                            BillId = bill.BillId ?? "",
                            BillNumber = billNumber,
                            BillDate = bill.BillDate
                        });
                        continue;
                    }

                    List<OrderControlBillRow> list;
                    if (!occurrences.TryGetValue(orderNo, out list))
                    {
                        list = new List<OrderControlBillRow>(1);
                        occurrences[orderNo] = list;
                    }
                    list.Add(bill);
                }
            }

            var sortedNumbers = new List<int>(occurrences.Keys);
            sortedNumbers.Sort();

            var duplicateIssues = new List<OrderControlIssueRow>();
            for (int i = 0; i < sortedNumbers.Count; i++)
            {
                int num = sortedNumbers[i];
                List<OrderControlBillRow> list = occurrences[num];
                OrderControlBillRow first = list[0];
                string firstBillNo = first.BillNumber ?? "";
                bool isDup = list.Count > 1;

                result.UsedRows.Add(new OrderControlUsedRow
                {
                    OrderNumber = num,
                    FirstBillId = first.BillId ?? "",
                    FirstBillNumber = firstBillNo,
                    FirstBillDate = first.BillDate,
                    IsDuplicate = isDup,
                    BillDisplay = isDup ? (firstBillNo + " (trùng)") : firstBillNo
                });

                if (isDup)
                {
                    var quotedBills = new List<string>(list.Count);
                    var distinctBills = new List<string>(list.Count);
                    var seenNames = new HashSet<string>(StringComparer.Ordinal);
                    for (int k = 0; k < list.Count; k++)
                    {
                        string bn = list[k].BillNumber ?? "";
                        quotedBills.Add("'" + bn + "'");
                        if (seenNames.Add(bn) && bn.Length > 0)
                            distinctBills.Add(bn);
                    }

                    duplicateIssues.Add(new OrderControlIssueRow
                    {
                        Kind = OrderControlIssueKind.Duplicate,
                        TypeDisplay = OrderControlConstants.IssueTypeDuplicate,
                        Message = string.Format(
                            CultureInfo.InvariantCulture,
                            "Trùng số order {0} (bill {1})",
                            num.ToString(CultureInfo.InvariantCulture),
                            string.Join(", ", quotedBills.ToArray())),
                        DateOrBillDisplay = FormatDateAndBill(first.BillDate, string.Join(", ", distinctBills.ToArray())),
                        BillId = first.BillId ?? "",
                        BillNumber = firstBillNo,
                        BillDate = first.BillDate
                    });
                }
            }

            var missingIssues = new List<OrderControlIssueRow>();
            long totalMissing = 0L;
            for (int i = 0; i < sortedNumbers.Count - 1; i++)
            {
                long curr = sortedNumbers[i];
                long next = sortedNumbers[i + 1];
                long gap = next - curr - 1L;
                if (gap <= 0L)
                    continue;

                int startMissing = (int)(curr + 1L);
                int endMissing = (int)(next - 1L);
                totalMissing += gap;

                string msg = gap == 1L
                    ? "Thiếu order " + startMissing.ToString(CultureInfo.InvariantCulture)
                    : "Thiếu order " + startMissing.ToString(CultureInfo.InvariantCulture)
                        + "-" + endMissing.ToString(CultureInfo.InvariantCulture);

                missingIssues.Add(new OrderControlIssueRow
                {
                    Kind = OrderControlIssueKind.Missing,
                    TypeDisplay = OrderControlConstants.IssueTypeMissing,
                    Message = msg,
                    DateOrBillDisplay = "",
                    MissingStart = startMissing,
                    MissingEnd = endMissing,
                    MissingCount = gap
                });
            }

            result.IssueRows.AddRange(invalidIssues);
            result.IssueRows.AddRange(duplicateIssues);
            result.IssueRows.AddRange(missingIssues);

            result.Summary.HasUsedOrders = sortedNumbers.Count > 0;
            if (sortedNumbers.Count > 0)
            {
                result.Summary.MinOrder = sortedNumbers[0];
                result.Summary.MaxOrder = sortedNumbers[sortedNumbers.Count - 1];
            }
            result.Summary.UsedCount = sortedNumbers.Count;
            result.Summary.DuplicateCount = duplicateIssues.Count;
            result.Summary.InvalidCount = invalidIssues.Count;
            result.Summary.MissingCount = totalMissing;

            return result;
        }

        /// <summary>Định dạng số đếm kiểu vi-VN (1.000.000), không phụ thuộc culture của máy Windows.</summary>
        public static string FormatCount(long value)
        {
            return value.ToString("n0", ViCulture);
        }

        /// <summary>Định dạng số order nguyên không dấu ngăn nghìn, không phụ thuộc culture máy.</summary>
        public static string FormatOrderNumber(int value)
        {
            return value.ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>Định dạng ngày dd/MM/yyyy không phụ thuộc culture máy.</summary>
        public static string FormatDate(DateTime date)
        {
            return date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private static string FormatDateAndBill(DateTime date, string billText)
        {
            string d = FormatDate(date);
            if (string.IsNullOrEmpty(billText))
                return d;
            return d + " · " + billText;
        }

        private sealed class IndexedBill
        {
            public int Index;
            public OrderControlBillRow Bill;
        }
    }
}
