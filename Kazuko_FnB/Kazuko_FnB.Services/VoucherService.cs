using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Phiếu thu / Phiếu chi (docs/specs/phieu-thu-chi.md §2, §4, §5):
    /// - Một transaction duy nhất cho mọi thao tác ghi/xóa (lỗi tự rollback).
    /// - Kiểm tra quyền, trường bắt buộc, trùng số phiếu, phiếu liên kết (T2), chi tiền mặt vượt quỹ (T3) qua FundRepository + FundLedgerRules.
    /// - Xóa cứng kèm ghi lưu vết TLUUVET trong cùng transaction (T4).
    /// </summary>
    public sealed class VoucherService
    {
        private readonly UserSession _session;
        private readonly VoucherRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public VoucherService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new VoucherRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, session.UserName, session.UserId);
        }

        public bool CanView { get { return VoucherRules.CanVoucher(_session, RoleModes.View); } }
        public bool CanAdd { get { return VoucherRules.CanVoucher(_session, RoleModes.Add); } }
        public bool CanEdit { get { return VoucherRules.CanVoucher(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return VoucherRules.CanVoucher(_session, RoleModes.Remove); } }
        public bool CanChangeDate { get { return VoucherRules.CanChangeDate(_session); } }

        public string GenerateNumber(VoucherKind kind)
        {
            return _repo.GenerateNumber(kind);
        }

        public VoucherRow LoadById(string id)
        {
            return _repo.LoadById(id);
        }

        public List<VoucherRow> LoadList(VoucherFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            if (!CanView)
                return new List<VoucherRow>();

            List<VoucherRow> rows = _repo.LoadList(filter);
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
                rows = rows.FindAll(r => VoucherRules.MatchesSearch(r, filter.SearchText));
            return rows;
        }

        /// <summary>
        /// Lưu phiếu thu / chi mới hoặc cập nhật phiếu đã lưu trong một transaction duy nhất.
        /// Tùy chọn <paramref name="testHookAfterWrite"/> chỉ dùng cho selftest mô phỏng lỗi để kiểm rollback.
        /// </summary>
        public VoucherSaveResult Save(VoucherRow row, Action testHookAfterWrite = null)
        {
            if (row == null)
                return VoucherSaveResult.Fail(VoucherConstants.VoucherNotFound);

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return VoucherSaveResult.Fail(VoucherConstants.NoPermission);
            if (!isNew && !CanEdit)
                return VoucherSaveResult.Fail(VoucherConstants.NoPermission);

            VoucherRules.NormalizeLinks(row);
            string validationError = VoucherRules.Validate(row);
            if (validationError != null)
                return VoucherSaveResult.Fail(validationError);

            VoucherSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    VoucherRow existing = null;
                    if (!isNew)
                    {
                        existing = VoucherRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = VoucherSaveResult.Fail(VoucherConstants.VoucherNotFound);
                            return;
                        }
                        string linkedErr = VoucherRules.GetLinkedBlockMessage(existing);
                        if (linkedErr != null)
                        {
                            outcome = VoucherSaveResult.Fail(linkedErr);
                            return;
                        }
                        // Giữ nguyên loại phiếu gốc khi sửa; khóa ngày nếu không có quyền đổi ngày.
                        row.Kind = existing.Kind;
                        if (!CanChangeDate)
                            row.Date = existing.Date;
                    }

                    if (VoucherRepository.IsNumberTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = VoucherSaveResult.Fail(VoucherConstants.NumberDuplicate(row.Name));
                        return;
                    }

                    // §2 & §5 T3: Chi tiền mặt không được làm quỹ tiền mặt của cửa hàng bị âm (tính qua FundRepository + FundLedgerRules).
                    string overdraftErr = CheckCashOverdraftIn(conn, tx, row, existing);
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
                            outcome = VoucherSaveResult.Fail(VoucherConstants.VoucherNotFound);
                            return;
                        }
                    }

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
            return outcome ?? VoucherSaveResult.Fail("Không thể lưu phiếu.");
        }

        /// <summary>Xóa cứng phiếu thu / chi thường và ghi lưu vết TLUUVET trong một transaction duy nhất (§2, §4 #8, §5 T4).</summary>
        public VoucherDeleteResult Delete(string id, Action testHookAfterDelete = null)
        {
            if (string.IsNullOrEmpty(id))
                return VoucherDeleteResult.Fail(VoucherConstants.VoucherNotFound);
            if (!CanRemove)
                return VoucherDeleteResult.Fail(VoucherConstants.NoPermission);

            VoucherDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                VoucherRow existing = VoucherRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = VoucherDeleteResult.Fail(VoucherConstants.VoucherNotFound);
                    return;
                }

                string linkedErr = VoucherRules.GetLinkedBlockMessage(existing);
                if (linkedErr != null)
                {
                    outcome = VoucherDeleteResult.Fail(linkedErr);
                    return;
                }

                if (VoucherRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = VoucherDeleteResult.Fail(VoucherConstants.VoucherNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string kindText = existing.Kind == VoucherKind.Income ? "phiếu thu" : "phiếu chi";
                string funcName = existing.Kind == VoucherKind.Income ? "Danh mục phiếu thu" : "Danh mục phiếu chi";
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = funcName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = existing.Name ?? "",
                        Note = string.Format("Xóa {0} số {1} (Số tiền: {2})",
                            kindText, existing.Name ?? "", VoucherConstants.FormatMoneyVi(existing.Amount)),
                        Amount = existing.Amount,
                        Account = _session.UserName,
                        UserId = _session.UserId,
                        Time = serverNow
                    }
                });

                if (testHookAfterDelete != null)
                    testHookAfterDelete();

                outcome = new VoucherDeleteResult { Ok = true };
            });

            return outcome ?? VoucherDeleteResult.Fail("Không thể xóa phiếu.");
        }

        /// <summary>Kiểm tra chi tiền mặt vượt quỹ trong transaction (dùng chung cho Phiếu chi và Tạm ứng lương).</summary>
        public static string CheckCashOverdraftIn(FbConnection conn, FbTransaction tx, VoucherRow row, VoucherRow existing)
        {
            if (row == null || row.Kind != VoucherKind.Expense || row.Transfer)
                return null;
            decimal currentCash = GetCurrentStoreCashBalanceIn(conn, tx, row.StoreId);
            return VoucherRules.CheckCashOverdraft(row, existing, currentCash);
        }

        /// <summary>Tính số dư tiền mặt hiện có của một cửa hàng qua FundRepository + FundLedgerRules (không viết lại công thức quỹ).</summary>
        public static decimal GetCurrentStoreCashBalanceIn(FbConnection conn, FbTransaction tx, string storeId)
        {
            DateTime horizon = new DateTime(2099, 12, 30);
            FundData data = FundRepository.LoadIn(conn, tx, horizon);
            FundLedger ledger = FundLedgerRules.Build(data.Sources, new FundQuery
            {
                From = new DateTime(1900, 1, 1),
                To = horizon,
                Node = FundNodeKind.Cash,
                KnownAccountIds = data.Accounts.Select(a => a.Id).ToList(),
                Scope = new FundStoreScope
                {
                    CoversAll = false,
                    StoreIds = new List<string> { storeId ?? "" }
                }
            });
            return ledger.ClosingBalance;
        }
    }
}
