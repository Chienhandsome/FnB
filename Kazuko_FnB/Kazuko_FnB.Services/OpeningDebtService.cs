using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Công nợ ban đầu của khách hàng (LOAI = 99) và nhà cung cấp (LOAI = 98)
    /// (docs/specs/so-du-dau-ky.md §3, P2):
    /// - Mỗi đối tác tối đa một phiếu TDONHANG (NAME = 'CONGNODAU', DATHANHTOAN = 30, STATUS = 30, TIENTHANHTOAN = 0).
    /// - Lưu MỘT transaction cho mọi dòng đã đổi kèm lưu vết TLUUVET cho từng đối tác đổi (cắt chuỗi 255).
    /// - Chặn nợ âm nêu rõ tên đối tác (T1).
    /// - Chặn giảm hoặc xóa nợ đầu của khách thấp hơn tổng TCHITIETTHANHTOAN.SOTIEN đã phân bổ vào phiếu đó (T4).
    /// </summary>
    public sealed class OpeningDebtService
    {
        private enum ChangeAction
        {
            Insert = 0,
            Update = 1,
            Delete = 2
        }

        private sealed class PlannedChange
        {
            public ChangeAction Action;
            public OpeningDebtPartnerRow Item;
            public string PartnerName;
            public string ExistingOrderId;
            public decimal OldAmount;
            public decimal NewAmount;
            public DateTime SaveDate;
        }

        private readonly UserSession _session;
        private readonly OpeningDebtRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public OpeningDebtService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new OpeningDebtRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView(DebtPartyKind kind)
        {
            return OpeningDebtRules.CanView(_session, kind);
        }

        public bool CanEdit(DebtPartyKind kind)
        {
            return OpeningDebtRules.CanEdit(_session, kind);
        }

        public bool CanChangeDate
        {
            get { return OpeningDebtRules.CanChangeDate(_session); }
        }

        /// <summary>Nạp danh sách đối tác và công nợ ban đầu hiện có theo chế độ (kiểm quyền Xem).</summary>
        public OpeningDebtDocument LoadDocument(DebtPartyKind kind)
        {
            DateTime defaultDate = OpeningDebtRules.DefaultOpeningDate(DateTime.Today);
            if (!CanView(kind))
                return new OpeningDebtDocument { PartyKind = kind, Date = defaultDate };
            return _repo.LoadDocument(kind, defaultDate);
        }

        /// <summary>
        /// Lưu mọi thay đổi công nợ ban đầu trong MỘT transaction duy nhất (TDONHANG + TLUUVET).
        /// Kiểm tra toàn bộ điều kiện hợp lệ (kể cả số đã thu theo đơn của khách hàng) TRƯỚC khi ghi
        /// để tránh ghi dở dang nếu một đối tác vi phạm.
        /// Tham số <paramref name="testHookAfterWrite"/> chỉ dùng cho selftest mô phỏng lỗi rollback.
        /// </summary>
        public OpeningDebtSaveResult Save(OpeningDebtSaveRequest request, Action testHookAfterWrite = null)
        {
            if (request == null)
                return OpeningDebtSaveResult.Fail("Thiếu dữ liệu công nợ ban đầu cần lưu.");

            DebtPartyKind kind = request.PartyKind;
            if (!CanEdit(kind))
                return OpeningDebtSaveResult.Fail(OpeningDebtConstants.NoPermission);

            string validationError = OpeningDebtRules.Validate(request);
            if (validationError != null)
                return OpeningDebtSaveResult.Fail(validationError);

            List<OpeningDebtPartnerRow> deduped = DeduplicateByPartner(request.Items);
            OpeningDebtSaveResult outcome = null;

            _repo.RunInTransaction((conn, tx) =>
            {
                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                DateTime defaultDate = OpeningDebtRules.DefaultOpeningDate(serverNow);
                DateTime requestedDate = request.Date == DateTime.MinValue ? defaultDate : request.Date.Date;

                var planned = new List<PlannedChange>();
                int duplicatePartners = 0;

                // Pha 1 (chỉ đọc): kiểm tra toàn bộ đối tác trước khi thực hiện bất kỳ lệnh ghi nào.
                foreach (OpeningDebtPartnerRow item in deduped)
                {
                    List<OpeningDebtOrderHeader> existingOrders =
                        OpeningDebtRepository.FindPartnerOrdersIn(conn, tx, kind, item.PartnerId);
                    if (existingOrders.Count > 1)
                        duplicatePartners++;
                    OpeningDebtOrderHeader existing = existingOrders.Count > 0 ? existingOrders[0] : null;
                    decimal dbAmount = existing != null ? existing.Amount : 0m;

                    DateTime targetDate;
                    if (!CanChangeDate)
                    {
                        targetDate = existing != null && existing.Date != DateTime.MinValue
                            ? existing.Date.Date
                            : defaultDate;
                    }
                    else
                    {
                        targetDate = requestedDate;
                    }

                    bool amountChanged = item.OpeningDebt != dbAmount;
                    bool dateUpdateNeeded = existing != null
                        && item.OpeningDebt > 0m
                        && CanChangeDate
                        && request.DateChanged
                        && existing.Date.Date != targetDate;

                    if (!amountChanged && !dateUpdateNeeded)
                        continue;

                    string dbPartnerName;
                    bool exists = OpeningDebtRepository.PartnerActiveExistsIn(conn, tx, kind, item.PartnerId, out dbPartnerName);
                    string partnerLabel = !string.IsNullOrWhiteSpace(item.Name)
                        ? item.Name
                        : (!string.IsNullOrWhiteSpace(dbPartnerName) ? dbPartnerName : (item.Code ?? item.PartnerId));

                    if (!exists && item.OpeningDebt > 0m)
                    {
                        outcome = OpeningDebtSaveResult.Fail(OpeningDebtConstants.PartnerNotFound);
                        return;
                    }

                    // Spec §3 (T4): không cho giảm hoặc xóa nợ đầu của khách thấp hơn số đã thu theo đơn.
                    if (kind == DebtPartyKind.Customer && existing != null)
                    {
                        decimal allocatedPaid = OpeningDebtRepository.GetAllocatedPaidIn(conn, tx, existing.Id);
                        if (item.OpeningDebt < allocatedPaid)
                        {
                            outcome = OpeningDebtSaveResult.Fail(
                                OpeningDebtConstants.BelowCollectedBlocked(partnerLabel, allocatedPaid));
                            return;
                        }
                    }

                    ChangeAction action;
                    if (item.OpeningDebt == 0m)
                    {
                        if (existing == null)
                            continue;
                        action = ChangeAction.Delete;
                    }
                    else if (existing == null)
                    {
                        action = ChangeAction.Insert;
                    }
                    else
                    {
                        action = ChangeAction.Update;
                    }

                    planned.Add(new PlannedChange
                    {
                        Action = action,
                        Item = item,
                        PartnerName = partnerLabel,
                        ExistingOrderId = existing != null ? existing.Id : null,
                        OldAmount = dbAmount,
                        NewAmount = item.OpeningDebt,
                        SaveDate = targetDate
                    });
                }

                string dupWarning = duplicatePartners > 0
                    ? OpeningDebtConstants.DuplicateOrdersWarning(duplicatePartners)
                    : null;

                if (planned.Count == 0)
                {
                    if (testHookAfterWrite != null)
                        testHookAfterWrite();
                    outcome = new OpeningDebtSaveResult
                    {
                        Ok = true,
                        ChangedCount = 0,
                        Warning = dupWarning
                    };
                    return;
                }

                // Pha 2 (ghi trong cùng một transaction): tạo / cập nhật / xóa phiếu và gom lưu vết.
                string actorId = _session.UserId ?? "";
                string account = !string.IsNullOrEmpty(_session.UserName) ? _session.UserName : (_session.DisplayName ?? "");
                string storeId = _session.CurrentStore != null ? _session.CurrentStore.Id : null;
                string funcName = OpeningDebtRules.FunctionName(kind);
                string kindLabel = kind == DebtPartyKind.Customer ? "khách hàng" : "nhà cung cấp";

                var logs = new List<ActivityLogRequest>(planned.Count);
                var savedIds = new List<string>();
                int created = 0;
                int updated = 0;
                int deleted = 0;
                decimal totalSaved = 0m;

                for (int i = 0; i < planned.Count; i++)
                {
                    PlannedChange ch = planned[i];
                    DateTime stamp = serverNow.AddSeconds(i);
                    string affectedOrderId;
                    string note;

                    if (ch.Action == ChangeAction.Insert)
                    {
                        affectedOrderId = DbHelper.NewId();
                        OpeningDebtRepository.InsertOrderIn(
                            conn, tx, kind, affectedOrderId, ch.SaveDate, ch.Item.PartnerId, storeId,
                            ch.NewAmount, actorId, stamp);
                        ch.Item.OrderId = affectedOrderId;
                        ch.Item.OrderDate = ch.SaveDate;
                        savedIds.Add(affectedOrderId);
                        created++;
                        totalSaved += ch.NewAmount;
                        note = string.Format(
                            "Tạo công nợ ban đầu {0}: {1} ({2})",
                            kindLabel,
                            ch.PartnerName,
                            DebtConstants.FormatMoneyVi(ch.NewAmount));
                    }
                    else if (ch.Action == ChangeAction.Update)
                    {
                        affectedOrderId = ch.ExistingOrderId;
                        OpeningDebtRepository.UpdateOrderIn(
                            conn, tx, kind, affectedOrderId, ch.SaveDate, ch.Item.PartnerId,
                            ch.NewAmount, actorId, stamp);
                        ch.Item.OrderId = affectedOrderId;
                        ch.Item.OrderDate = ch.SaveDate;
                        savedIds.Add(affectedOrderId);
                        updated++;
                        totalSaved += ch.NewAmount;
                        note = string.Format(
                            "Cập nhật công nợ ban đầu {0}: {1} ({2} -> {3})",
                            kindLabel,
                            ch.PartnerName,
                            DebtConstants.FormatMoneyVi(ch.OldAmount),
                            DebtConstants.FormatMoneyVi(ch.NewAmount));
                    }
                    else
                    {
                        affectedOrderId = ch.ExistingOrderId;
                        OpeningDebtRepository.DeleteOrderIn(conn, tx, kind, affectedOrderId);
                        ch.Item.OrderId = null;
                        ch.Item.OrderDate = null;
                        deleted++;
                        note = string.Format(
                            "Xóa công nợ ban đầu {0}: {1} (trước đó: {2})",
                            kindLabel,
                            ch.PartnerName,
                            DebtConstants.FormatMoneyVi(ch.OldAmount));
                    }

                    logs.Add(new ActivityLogRequest
                    {
                        FunctionName = funcName,
                        Kind = ActivityKind.Khac,
                        BillId = affectedOrderId ?? "",
                        TableName = PurchaseRules.Clip(ch.PartnerName, OpeningDebtConstants.MaxTextLength),
                        Note = PurchaseRules.Clip(note, OpeningDebtConstants.MaxTextLength),
                        Amount = ch.NewAmount,
                        Account = account,
                        UserId = actorId,
                        Time = stamp
                    });
                }

                _activityLogger.WriteBatch(conn, tx, logs);

                if (testHookAfterWrite != null)
                    testHookAfterWrite();

                outcome = new OpeningDebtSaveResult
                {
                    Ok = true,
                    ChangedCount = planned.Count,
                    CreatedCount = created,
                    UpdatedCount = updated,
                    DeletedCount = deleted,
                    TotalSavedDebt = totalSaved,
                    SavedOrderIds = savedIds,
                    Warning = dupWarning
                };
            });

            return outcome ?? OpeningDebtSaveResult.Fail("Không thể lưu công nợ ban đầu.");
        }

        private static List<OpeningDebtPartnerRow> DeduplicateByPartner(IList<OpeningDebtPartnerRow> items)
        {
            var result = new List<OpeningDebtPartnerRow>();
            if (items == null)
                return result;

            var indexByPartner = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (OpeningDebtPartnerRow item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.PartnerId))
                    continue;
                int existingIdx;
                if (indexByPartner.TryGetValue(item.PartnerId, out existingIdx))
                {
                    result[existingIdx] = item;
                }
                else
                {
                    indexByPartner[item.PartnerId] = result.Count;
                    result.Add(item);
                }
            }
            return result;
        }
    }
}
