using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho phiếu Nạp tiền (LOAI = 0) và Chi trả tiền (LOAI = 1) gắn thẻ trả trước
    /// (DTHETRATRUOCID, spec docs/specs/danh-muc-the-tra-truoc.md §3).
    /// Dùng lại đúng đường ghi/xóa/sinh số của <see cref="VoucherRepository"/>, kiểm vượt quỹ của
    /// <see cref="VoucherService"/> và công thức số dư thẻ duy nhất của <see cref="ServicePaymentRepository"/>.
    /// </summary>
    public sealed class PrepaidCardVoucherService
    {
        private readonly UserSession _session;
        private readonly PrepaidCardRepository _cardRepo;
        private readonly VoucherRepository _voucherRepo;
        private readonly ActivityLogger _activityLogger;

        public PrepaidCardVoucherService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _cardRepo = new PrepaidCardRepository(session.DatabasePath);
            _voucherRepo = new VoucherRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return PrepaidCardRules.Can(_session, RoleModes.View); } }
        public bool CanTopUp { get { return PrepaidCardRules.CanTopUp(_session); } }
        public bool CanWithdraw { get { return PrepaidCardRules.CanWithdraw(_session); } }
        public bool CanEdit { get { return PrepaidCardRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return PrepaidCardRules.Can(_session, RoleModes.Remove); } }
        public bool CanChangeDate { get { return PrepaidCardRules.CanChangeVoucherDate(_session); } }

        public string GenerateNumber(VoucherKind kind)
        {
            return _voucherRepo.GenerateNumber(kind);
        }

        public string LoadLatestReasonId(VoucherKind kind)
        {
            return _cardRepo.LoadLatestCardVoucherReasonId(kind);
        }

        public VoucherRow LoadById(string voucherId, string expectedCardId = null)
        {
            if (!CanView || string.IsNullOrEmpty(voucherId))
                return null;
            VoucherRow row = _voucherRepo.LoadById(voucherId);
            return PrepaidCardRules.IsEditableCardVoucher(row, expectedCardId) ? row : null;
        }

        /// <summary>
        /// Tạo mới hoặc sửa phiếu nạp/chi trả thẻ trong một transaction duy nhất.
        /// Tùy chọn <paramref name="testHookAfterWrite"/> dùng cho selftest kiểm rollback.
        /// </summary>
        public VoucherSaveResult Save(VoucherRow row, Action testHookAfterWrite = null)
        {
            if (row == null)
                return VoucherSaveResult.Fail(PrepaidCardText.VoucherNotFound);

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew)
            {
                bool allowed = row.Kind == VoucherKind.Expense ? CanWithdraw : CanTopUp;
                if (!allowed)
                    return VoucherSaveResult.Fail(PrepaidCardText.NoPermission);
            }
            else if (!CanEdit)
            {
                return VoucherSaveResult.Fail(PrepaidCardText.NoPermission);
            }

            row.IsAdvance = false;
            row.PayrollId = null;
            // Chiến chốt 28/11: phiếu nạp/chi thẻ có tính vào công nợ như phiếu thu/chi thường gắn khách (không ép KHONGTHAYDOICONGNO).
            if (!row.Transfer)
                row.BankAccountId = null;

            VoucherRules.NormalizeLinks(row);
            row.Amount = Math.Round(row.Amount, 2, MidpointRounding.AwayFromZero);

            string validationErr = PrepaidCardRules.ValidateVoucher(row);
            if (validationErr != null)
                return VoucherSaveResult.Fail(validationErr);

            VoucherSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _cardRepo.RunInTransaction((conn, tx) =>
                {
                    VoucherRow existing = null;
                    if (!isNew)
                    {
                        existing = VoucherRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = VoucherSaveResult.Fail(PrepaidCardText.VoucherNotFound);
                            return;
                        }
                        if (!PrepaidCardRules.IsEditableCardVoucher(existing, row.PrepaidCardId))
                        {
                            outcome = VoucherSaveResult.Fail(PrepaidCardText.NotCardVoucher);
                            return;
                        }
                        row.Kind = existing.Kind;
                        row.PrepaidCardId = existing.PrepaidCardId;
                        if (!CanChangeDate)
                            row.Date = existing.Date.Date;
                    }

                    PrepaidCardRow card = PrepaidCardRepository.LoadByIdIn(conn, tx, row.PrepaidCardId);
                    if (card == null)
                    {
                        outcome = VoucherSaveResult.Fail(PrepaidCardText.NotFound);
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(row.ReasonId)
                        && !PrepaidCardRepository.IsReasonMatchingKindIn(conn, tx, row.ReasonId, row.Kind))
                    {
                        outcome = VoucherSaveResult.Fail(PrepaidCardText.ReasonKindMismatch);
                        return;
                    }

                    if (VoucherRepository.IsNumberTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = VoucherSaveResult.Fail(VoucherConstants.NumberDuplicate(row.Name));
                        return;
                    }

                    // Chiến chốt 28/11: KHÔNG chặn chi trả vượt số dư và KHÔNG chặn sửa phiếu làm số dư thẻ âm (như gốc).

                    // Kiểm chi tiền mặt vượt quỹ bằng đúng hàm chung của VoucherService.
                    string overdraftErr = VoucherService.CheckCashOverdraftIn(conn, tx, row, existing);
                    if (overdraftErr != null)
                    {
                        outcome = VoucherSaveResult.Fail(overdraftErr);
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = VoucherRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        VoucherRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (VoucherRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = VoucherSaveResult.Fail(PrepaidCardText.VoucherNotFound);
                            return;
                        }
                    }

                    string kindLabel = row.Kind == VoucherKind.Expense ? "chi trả tiền" : "nạp tiền";
                    string note = string.Format("{0} phiếu {1} thẻ {2} số {3} (Số tiền: {4})",
                        isNew ? "Tạo" : "Sửa",
                        kindLabel,
                        card.Code ?? "",
                        row.Name ?? "",
                        PrepaidCardRules.FormatMoney(row.Amount));
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = PrepaidCardText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = PrepaidCardRules.ClipLogText(row.Name),
                            Note = PrepaidCardRules.ClipLogText(note),
                            Amount = row.Amount,
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookAfterWrite != null)
                        testHookAfterWrite();

                    outcome = new VoucherSaveResult
                    {
                        Ok = true,
                        SavedId = row.Id,
                        SavedNumber = row.Name
                    };
                });
            }
            catch
            {
                if (assignedNewId)
                    row.Id = null;
                throw;
            }

            if ((outcome == null || !outcome.Ok) && assignedNewId)
                row.Id = null;
            return outcome ?? VoucherSaveResult.Fail("Không thể lưu phiếu thẻ trả trước.");
        }

        /// <summary>
        /// Xóa cứng phiếu nạp/chi trả thẻ nếu không làm số dư thẻ âm, hoàn quỹ và ghi lưu vết trong một transaction.
        /// </summary>
        public VoucherDeleteResult Delete(string voucherId, string expectedCardId = null, Action testHookAfterDelete = null)
        {
            if (string.IsNullOrEmpty(voucherId))
                return VoucherDeleteResult.Fail(PrepaidCardText.VoucherNotFound);
            if (!CanRemove)
                return VoucherDeleteResult.Fail(PrepaidCardText.NoPermission);

            VoucherDeleteResult outcome = null;
            _cardRepo.RunInTransaction((conn, tx) =>
            {
                VoucherRow existing = VoucherRepository.LoadByIdIn(conn, tx, voucherId);
                if (existing == null)
                {
                    outcome = VoucherDeleteResult.Fail(PrepaidCardText.VoucherNotFound);
                    return;
                }
                if (!PrepaidCardRules.IsEditableCardVoucher(existing, expectedCardId))
                {
                    outcome = VoucherDeleteResult.Fail(PrepaidCardText.NotCardVoucher);
                    return;
                }

                // Chiến chốt 28/11: KHÔNG chặn xóa phiếu làm số dư thẻ âm (như gốc).
                if (VoucherRepository.DeleteIn(conn, tx, voucherId) == 0)
                {
                    outcome = VoucherDeleteResult.Fail(PrepaidCardText.VoucherNotFound);
                    return;
                }

                PrepaidCardRow card = PrepaidCardRepository.LoadByIdIn(conn, tx, existing.PrepaidCardId);
                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string kindLabel = existing.Kind == VoucherKind.Expense ? "chi trả tiền" : "nạp tiền";
                string note = string.Format("Xóa phiếu {0} thẻ {1} số {2} (Số tiền: {3})",
                    kindLabel,
                    card == null ? "" : (card.Code ?? ""),
                    existing.Name ?? "",
                    PrepaidCardRules.FormatMoney(existing.Amount));
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = PrepaidCardText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = PrepaidCardRules.ClipLogText(existing.Name),
                        Note = PrepaidCardRules.ClipLogText(note),
                        Amount = existing.Amount,
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookAfterDelete != null)
                    testHookAfterDelete();

                outcome = new VoucherDeleteResult { Ok = true };
            });

            return outcome ?? VoucherDeleteResult.Fail("Không thể xóa phiếu thẻ trả trước.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }
    }
}
