using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Danh mục thẻ trả trước (DTHETRATRUOC, spec docs/specs/danh-muc-the-tra-truoc.md §2):
    /// kiểm quyền SFUNCTION, kiểm Mã thẻ bắt buộc/không trùng (UNICODE_CI_AI), cho phép sửa mã/khóa/hạn dùng tự do,
    /// chặn xóa khi còn phiếu thu chi (STATUS &lt;&gt; -30) hoặc hóa đơn hoặc khách giữ thẻ (nêu đủ số từng loại),
    /// ghi lưu vết TLUUVET trong cùng transaction (cắt 255 ký tự).
    /// </summary>
    public sealed class PrepaidCardService
    {
        private readonly UserSession _session;
        private readonly PrepaidCardRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public PrepaidCardService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new PrepaidCardRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return PrepaidCardRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return PrepaidCardRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return PrepaidCardRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return PrepaidCardRules.Can(_session, RoleModes.Remove); } }
        public bool CanTopUp { get { return PrepaidCardRules.CanTopUp(_session); } }
        public bool CanWithdraw { get { return PrepaidCardRules.CanWithdraw(_session); } }

        public List<PrepaidCardRow> LoadAll()
        {
            if (!CanView)
                return new List<PrepaidCardRow>();
            return _repo.LoadAll();
        }

        public PrepaidCardRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public DateTime GetServerDate()
        {
            return _repo.GetServerDate();
        }

        public List<PrepaidCardVoucherItem> LoadVoucherHistory(string cardId)
        {
            if (!CanView || string.IsNullOrEmpty(cardId))
                return new List<PrepaidCardVoucherItem>();
            return _repo.LoadVoucherHistory(cardId);
        }

        public List<PrepaidCardUsageItem> LoadUsageHistory(string cardId)
        {
            if (!CanView || string.IsNullOrEmpty(cardId))
                return new List<PrepaidCardUsageItem>();
            return _repo.LoadUsageHistory(cardId);
        }

        public PrepaidCardSaveResult Save(PrepaidCardRow row, Action testHookBeforeCommit = null)
        {
            if (row == null)
                return PrepaidCardSaveResult.Fail("Không có dữ liệu thẻ trả trước.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return PrepaidCardSaveResult.Fail(PrepaidCardText.NoPermission);
            if (!isNew && !CanEdit)
                return PrepaidCardSaveResult.Fail(PrepaidCardText.NoPermission);

            PrepaidCardSaveResult precheck = PrepaidCardRules.ValidateCard(row, false);
            if (!precheck.Ok)
                return precheck;

            PrepaidCardSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    PrepaidCardRow existing = null;
                    if (!isNew)
                    {
                        existing = PrepaidCardRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = PrepaidCardSaveResult.Fail(PrepaidCardText.NotFound);
                            return;
                        }
                    }

                    string excludeId = isNew ? "" : row.Id;
                    bool dupCode = PrepaidCardRepository.IsCodeTakenIn(conn, tx, row.Code, excludeId);
                    PrepaidCardSaveResult dupCheck = PrepaidCardRules.ValidateCard(row, dupCode);
                    if (!dupCheck.Ok)
                    {
                        outcome = dupCheck;
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = PrepaidCardRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        PrepaidCardRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (PrepaidCardRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = PrepaidCardSaveResult.Fail(PrepaidCardText.NotFound);
                            return;
                        }
                    }

                    string actionPrefix;
                    if (isNew)
                        actionPrefix = row.Locked ? "Thêm mới và khóa thẻ trả trước: " : "Thêm mới thẻ trả trước: ";
                    else if (existing != null && existing.Locked != row.Locked)
                        actionPrefix = row.Locked ? "Khóa thẻ trả trước: " : "Mở khóa thẻ trả trước: ";
                    else
                        actionPrefix = "Cập nhật thẻ trả trước: ";

                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = PrepaidCardText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = PrepaidCardRules.ClipLogText(row.Code),
                            Note = PrepaidCardRules.ClipLogText(actionPrefix + (row.Code ?? "")),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookBeforeCommit != null)
                        testHookBeforeCommit();

                    outcome = new PrepaidCardSaveResult
                    {
                        Ok = true,
                        SavedId = row.Id
                    };
                });
            }
            catch
            {
                if (assignedNewId)
                    ResetNew(row);
                throw;
            }

            if ((outcome == null || !outcome.Ok) && assignedNewId)
                ResetNew(row);
            return outcome ?? PrepaidCardSaveResult.Fail("Không thể lưu thẻ trả trước.");
        }

        public PrepaidCardDeleteResult Delete(string id, Action testHookBeforeCommit = null)
        {
            if (string.IsNullOrEmpty(id))
                return PrepaidCardDeleteResult.Fail(PrepaidCardText.NotFound);
            if (!CanRemove)
                return PrepaidCardDeleteResult.Fail(PrepaidCardText.NoPermission);

            PrepaidCardDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                PrepaidCardRow existing = PrepaidCardRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = PrepaidCardDeleteResult.Fail(PrepaidCardText.NotFound);
                    return;
                }

                PrepaidCardReferenceCounts refs = PrepaidCardRepository.CountReferencesIn(conn, tx, id);
                if (refs.HasAny)
                {
                    outcome = new PrepaidCardDeleteResult
                    {
                        Ok = false,
                        VoucherCount = refs.VoucherCount,
                        OrderCount = refs.OrderCount,
                        CustomerCount = refs.CustomerCount,
                        ErrorMessage = PrepaidCardRules.FormatDeleteBlockedMessage(refs)
                    };
                    return;
                }

                if (PrepaidCardRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = PrepaidCardDeleteResult.Fail(PrepaidCardText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = PrepaidCardText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = PrepaidCardRules.ClipLogText(existing.Code),
                        Note = PrepaidCardRules.ClipLogText("Xóa thẻ trả trước: " + (existing.Code ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookBeforeCommit != null)
                    testHookBeforeCommit();

                outcome = new PrepaidCardDeleteResult { Ok = true };
            });

            return outcome ?? PrepaidCardDeleteResult.Fail("Không thể xóa thẻ trả trước.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(PrepaidCardRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
