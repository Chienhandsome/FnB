using System;
using System.Collections.Generic;
using System.Linq;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dựng sổ quỹ từ giao dịch: mốc số dư đầu kỳ, phạm vi cửa hàng, nút cây, tồn đầu/cộng dồn/cuối.
    /// Xem docs/specs/quy-tien.md §4.4, §4.7. Công thức tiền nằm ở <see cref="FundRules"/>.
    /// Tồn đầu kỳ và phát sinh trong kỳ đi qua cùng một danh sách, chỉ khác khoảng ngày.
    /// </summary>
    public static class FundLedgerRules
    {
        private sealed class Item
        {
            public FundSource Source;
            public FundEntry Entry;
            public DateTime Time;
            public bool IsOpening;
        }

        private sealed class Anchor
        {
            public FundSource Source;
            public DateTime Time;
        }

        /// <summary>
        /// "Thời điểm quỹ", dùng cho cả lọc ngày lẫn so mốc. Hóa đơn bán: giờ thanh toán (trống thì NGAY).
        /// Chứng từ khác: NGAY nếu có giờ; nếu không, lấy giờ từ TIMECREATED chỉ khi cùng ngày.
        /// </summary>
        public static DateTime FundTime(FundSource source)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (source.Kind == FundSourceKind.Sale && source.PaidTime.HasValue)
                return source.PaidTime.Value;
            if (source.DocumentDate.TimeOfDay == TimeSpan.Zero && source.CreatedTime.HasValue &&
                source.CreatedTime.Value.Date == source.DocumentDate.Date)
                return source.CreatedTime.Value;
            return source.DocumentDate;
        }

        /// <summary>
        /// Phạm vi cửa hàng theo user. Người không phải admin chỉ xem cửa hàng được gán; chưa được gán mà hệ thống
        /// chỉ có một cửa hàng thì xem cửa hàng đó. selectedStoreId rỗng = "Tất cả cửa hàng".
        /// </summary>
        public static FundStoreScope ResolveScope(bool isAdmin, IList<string> allStoreIds, IList<string> userStoreIds,
            string selectedStoreId)
        {
            List<string> allowed = AllowedStores(isAdmin, allStoreIds, userStoreIds);
            bool chosen = !string.IsNullOrEmpty(selectedStoreId);
            List<string> ids = chosen ? allowed.Where(i => i == selectedStoreId).ToList() : allowed;
            bool coversAll = (isAdmin && !chosen) ||
                             (allStoreIds.Count > 0 && allStoreIds.All(i => ids.Contains(i)));
            return new FundStoreScope { CoversAll = coversAll, StoreIds = ids };
        }

        public static List<string> AllowedStores(bool isAdmin, IList<string> allStoreIds, IList<string> userStoreIds)
        {
            if (isAdmin)
                return allStoreIds.ToList();
            List<string> mine = allStoreIds.Where(i => userStoreIds.Contains(i)).ToList();
            if (mine.Count == 0 && userStoreIds.Count == 0 && allStoreIds.Count == 1)
                return allStoreIds.ToList();
            return mine;
        }

        public static FundLedger Build(IEnumerable<FundSource> sources, FundQuery query)
        {
            if (sources == null) throw new ArgumentNullException("sources");
            if (query == null) throw new ArgumentNullException("query");

            List<FundSource> list = sources.ToList();
            Dictionary<string, Anchor> anchors = FindAnchors(list);
            var known = new HashSet<string>(query.KnownAccountIds ?? new List<string>(), StringComparer.Ordinal);
            bool coversAll = query.Scope.CoversAll;

            var ledger = new FundLedger
            {
                Lines = new List<FundLedgerLine>(),
                // Q13: tồn tài khoản (và "Tất cả" vì có chứa tài khoản) chỉ hiện khi xem mọi cửa hàng.
                ShowBalance = query.Node == FundNodeKind.Cash || coversAll,
                CashStoresWithoutOpening = new List<string>(),
                AccountsWithoutOpening = new List<string>()
            };

            var items = new List<Item>();
            foreach (FundSource source in list)
            {
                DateTime time = FundTime(source);
                bool isOpening = IsOpening(source);
                foreach (FundEntry entry in Entries(source))
                {
                    Anchor anchor;
                    if (anchors.TryGetValue(Key(entry.Fund, source.StoreId, entry.AccountId), out anchor))
                    {
                        // Chỉ cộng giao dịch sau mốc, cộng chính phiếu mốc.
                        if (!ReferenceEquals(anchor.Source, source) && time <= anchor.Time)
                            continue;
                    }

                    bool inScope = coversAll || query.Scope.StoreIds.Contains(source.StoreId ?? "");
                    // Số dư đầu kỳ của tài khoản không chia theo cửa hàng: lọc một phần cửa hàng thì không hiện.
                    if (isOpening && entry.Fund == FundKind.Account && !coversAll)
                        inScope = false;
                    if (!inScope)
                        continue;

                    bool unknown = entry.Fund == FundKind.Account && !known.Contains(entry.AccountId);
                    if (unknown)
                        ledger.HasUnknownAccount = true;
                    if (!NodeMatches(query, entry, unknown))
                        continue;

                    items.Add(new Item { Source = source, Entry = entry, Time = time, IsOpening = isOpening });
                }
            }

            DateTime from = query.From.Date;
            DateTime toExclusive = query.To.Date.AddDays(1);
            decimal running = items.Where(i => i.Time < from).Sum(i => i.Entry.Amount);
            if (ledger.ShowBalance)
                ledger.OpeningBalance = running;

            foreach (Item item in items.Where(i => i.Time >= from && i.Time < toExclusive)
                         .OrderBy(i => i.Time).ThenByDescending(i => i.IsOpening)
                         .ThenBy(i => i.Source.Number, StringComparer.Ordinal))
            {
                decimal amount = item.Entry.Amount;
                running += amount;
                var line = new FundLedgerLine
                {
                    Time = item.Time,
                    Number = item.Source.Number,
                    TypeText = TypeText(item.Source, amount),
                    Fund = item.Entry.Fund,
                    AccountId = item.Entry.AccountId,
                    IsCard = item.Entry.IsCard,
                    IsOpening = item.IsOpening,
                    Income = amount > 0m ? amount : 0m,
                    Expense = amount < 0m ? -amount : 0m,
                    Balance = ledger.ShowBalance ? running : (decimal?)null
                };
                ledger.Lines.Add(line);

                if (item.IsOpening)
                {
                    if (ledger.ShowBalance)
                        ledger.OpeningEntered += amount;
                    continue;
                }
                ledger.Income += line.Income;
                ledger.Expense += line.Expense;
                if (item.Entry.Fund == FundKind.Account)
                {
                    if (item.Entry.IsCard)
                        ledger.CardNet += amount;
                    else
                        ledger.TransferNet += amount;
                }
            }

            if (ledger.ShowBalance)
                ledger.ClosingBalance = running;

            AddMissingOpenings(ledger, query, anchors);
            return ledger;
        }

        private static IEnumerable<FundEntry> Entries(FundSource source)
        {
            switch (source.Kind)
            {
                case FundSourceKind.Sale:
                    return FundRules.ForBill(source.Bill);
                case FundSourceKind.Voucher:
                    return FundRules.ForVoucher(source.Voucher);
                default:
                    return FundRules.ForStockDocument(source.Stock);
            }
        }

        private static bool IsOpening(FundSource source)
        {
            return source.Kind == FundSourceKind.Voucher && source.ReasonId == FundReasons.OpeningBalanceId;
        }

        private static string Key(FundKind fund, string storeId, string accountId)
        {
            return fund == FundKind.Cash ? "C:" + (storeId ?? "") : "A:" + (accountId ?? "");
        }

        /// <summary>Mốc mới nhất của từng quỹ. Lấy từ phiếu (không từ dòng tiền) để số dư đầu kỳ bằng 0 vẫn là mốc.</summary>
        private static Dictionary<string, Anchor> FindAnchors(List<FundSource> sources)
        {
            var anchors = new Dictionary<string, Anchor>(StringComparer.Ordinal);
            foreach (FundSource source in sources)
            {
                if (!IsOpening(source) || source.Voucher.Status == FundRules.DeletedStatus)
                    continue;
                bool account = source.Voucher.TransferFlag != 0;
                // Dòng "chưa xác định tài khoản" không có mốc.
                if (account && string.IsNullOrEmpty(source.Voucher.AccountId))
                    continue;
                string key = Key(account ? FundKind.Account : FundKind.Cash, source.StoreId, source.Voucher.AccountId);
                DateTime time = FundTime(source);
                Anchor current;
                if (!anchors.TryGetValue(key, out current) || time > current.Time)
                    anchors[key] = new Anchor { Source = source, Time = time };
            }
            return anchors;
        }

        private static bool NodeMatches(FundQuery query, FundEntry entry, bool unknown)
        {
            switch (query.Node)
            {
                case FundNodeKind.Cash:
                    return entry.Fund == FundKind.Cash;
                case FundNodeKind.AccountAll:
                    return entry.Fund == FundKind.Account;
                case FundNodeKind.Account:
                    return entry.Fund == FundKind.Account && entry.AccountId == query.AccountId;
                case FundNodeKind.UnknownAccount:
                    return unknown;
                default:
                    return true;
            }
        }

        private static void AddMissingOpenings(FundLedger ledger, FundQuery query, Dictionary<string, Anchor> anchors)
        {
            if (query.Node == FundNodeKind.All || query.Node == FundNodeKind.Cash)
                foreach (string store in query.Scope.StoreIds)
                    if (!anchors.ContainsKey(Key(FundKind.Cash, store, null)))
                        ledger.CashStoresWithoutOpening.Add(store);

            if (!ledger.ShowBalance || query.Node == FundNodeKind.Cash || query.Node == FundNodeKind.UnknownAccount)
                return;
            IEnumerable<string> accounts = query.Node == FundNodeKind.Account
                ? new[] { query.AccountId }
                : (IEnumerable<string>)(query.KnownAccountIds ?? new List<string>());
            foreach (string id in accounts)
                if (!anchors.ContainsKey(Key(FundKind.Account, null, id)))
                    ledger.AccountsWithoutOpening.Add(id);
        }

        private static string TypeText(FundSource source, decimal amount)
        {
            if (source.Kind == FundSourceKind.Sale)
                return "Bán hàng";
            if (source.Kind == FundSourceKind.Stock)
                return source.Stock.Type == FundRules.SupplierReturnType ? "Trả hàng nhà cung cấp" : "Nhập hàng";
            if (source.ReasonId == FundReasons.OpeningBalanceId)
                return "Số dư đầu kỳ";
            if (source.ReasonId == FundReasons.TransferId)
                return "Chuyển quỹ";
            return amount >= 0m ? "Phiếu thu" : "Phiếu chi";
        }
    }
}
