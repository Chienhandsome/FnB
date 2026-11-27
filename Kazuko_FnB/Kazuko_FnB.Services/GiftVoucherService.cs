using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Danh mục voucher (DVOUCHER, spec docs/specs/danh-muc-voucher.md §2):
    /// kiểm quyền SFUNCTION, kiểm Mã bắt buộc/không trùng (UNICODE_CI_AI), Giá trị &gt; 0,
    /// Hạn sử dụng không trước Ngày phát hành, chặn sửa trường chính khi đã có hóa đơn tham chiếu
    /// (chỉ cho sửa Ghi chú), xóa cứng chỉ khi không có TDONHANG nào tham chiếu (mọi STATUS/DATHANHTOAN),
    /// ghi lưu vết TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class GiftVoucherService
    {
        private readonly UserSession _session;
        private readonly GiftVoucherRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public GiftVoucherService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new GiftVoucherRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, ResolveAccount(), session.UserId);
        }

        public bool CanView { get { return GiftVoucherRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return GiftVoucherRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return GiftVoucherRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return GiftVoucherRules.Can(_session, RoleModes.Remove); } }

        public List<GiftVoucherRow> LoadAll()
        {
            if (!CanView)
                return new List<GiftVoucherRow>();
            return _repo.LoadAll();
        }

        public GiftVoucherRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public DateTime GetServerDate()
        {
            return _repo.GetServerDate();
        }

        public GiftVoucherSaveResult Save(GiftVoucherRow row, Action testHookBeforeCommit = null)
        {
            if (row == null)
                return GiftVoucherSaveResult.Fail("Không có dữ liệu voucher.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return GiftVoucherSaveResult.Fail(GiftVoucherText.NoPermission);
            if (!isNew && !CanEdit)
                return GiftVoucherSaveResult.Fail(GiftVoucherText.NoPermission);

            GiftVoucherSaveResult precheck = GiftVoucherRules.ValidateVoucher(row, false);
            if (!precheck.Ok)
                return precheck;

            GiftVoucherSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    if (!isNew)
                    {
                        GiftVoucherRow existing = GiftVoucherRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = GiftVoucherSaveResult.Fail(GiftVoucherText.NotFound);
                            return;
                        }
                        if (existing.HasAnyBillRef && GiftVoucherRules.HasLockedFieldChanges(existing, row))
                        {
                            outcome = GiftVoucherSaveResult.Fail(
                                GiftVoucherText.UsedVoucherLocked(existing.AnyRefBillName),
                                hasLockedFieldError: true);
                            return;
                        }
                    }

                    string excludeId = isNew ? "" : row.Id;
                    bool dupCode = GiftVoucherRepository.IsCodeTakenIn(conn, tx, row.Code, excludeId);
                    GiftVoucherSaveResult dupCheck = GiftVoucherRules.ValidateVoucher(row, dupCode);
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
                        row.Status = GiftVoucherRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        GiftVoucherRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (GiftVoucherRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = GiftVoucherSaveResult.Fail(GiftVoucherText.NotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới voucher: " : "Cập nhật voucher: ") + (row.Code ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = GiftVoucherText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = GiftVoucherRules.ClipLogText(row.Code),
                            Note = GiftVoucherRules.ClipLogText(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    if (testHookBeforeCommit != null)
                        testHookBeforeCommit();

                    outcome = new GiftVoucherSaveResult
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
            return outcome ?? GiftVoucherSaveResult.Fail("Không thể lưu voucher.");
        }

        public GiftVoucherDeleteResult Delete(string id, Action testHookBeforeCommit = null)
        {
            if (string.IsNullOrEmpty(id))
                return GiftVoucherDeleteResult.Fail(GiftVoucherText.NotFound);
            if (!CanRemove)
                return GiftVoucherDeleteResult.Fail(GiftVoucherText.NoPermission);

            GiftVoucherDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                GiftVoucherRow existing = GiftVoucherRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = GiftVoucherDeleteResult.Fail(GiftVoucherText.NotFound);
                    return;
                }

                string refBill = GiftVoucherRepository.FindFirstAnyRefBillNameIn(conn, tx, id);
                if (!string.IsNullOrEmpty(refBill))
                {
                    outcome = GiftVoucherDeleteResult.Fail(
                        GiftVoucherText.DeleteBlockedByBill(refBill),
                        refBill);
                    return;
                }

                if (GiftVoucherRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = GiftVoucherDeleteResult.Fail(GiftVoucherText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = GiftVoucherText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = GiftVoucherRules.ClipLogText(existing.Code),
                        Note = GiftVoucherRules.ClipLogText("Xóa voucher: " + (existing.Code ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                if (testHookBeforeCommit != null)
                    testHookBeforeCommit();

                outcome = new GiftVoucherDeleteResult { Ok = true };
            });

            return outcome ?? GiftVoucherDeleteResult.Fail("Không thể xóa voucher.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(GiftVoucherRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
