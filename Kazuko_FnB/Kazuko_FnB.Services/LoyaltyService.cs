using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ màn Khách hàng thân thiết &amp; phiếu tặng/giảm điểm TTANGGIAMDIEM
    /// (docs/specs/khach-hang-than-thiet.md §2–§3):
    /// - Đọc khách hàng kèm Doanh số, Số hóa đơn, Điểm tích lũy (công thức riêng §2).
    /// - Thêm/sửa/xóa phiếu tặng/giảm điểm trong MỘT transaction kèm lưu vết TLUUVET (cắt chuỗi <= 255).
    /// - Chặn giảm điểm vượt điểm hiện có, chặn xóa làm điểm khách âm.
    /// - Tự nâng nhóm khách sau khi tặng điểm bằng đúng hàm ServicePaymentRepository.TryUpgradeCustomerGroupIn.
    /// </summary>
    public sealed class LoyaltyService
    {
        private readonly UserSession _session;
        private readonly LoyaltyRepository _repo;
        private readonly CustomerRepository _customerRepo;
        private readonly InvoiceNumberRepository _timeRepo;
        private readonly ActivityLogger _activityLogger;

        public LoyaltyService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new LoyaltyRepository(session.DatabasePath);
            _customerRepo = new CustomerRepository(session.DatabasePath);
            _timeRepo = new InvoiceNumberRepository(session.DatabasePath);
            _activityLogger = new ActivityLogger(session.DatabasePath, session.UserName, session.UserId);
        }

        public bool CanView { get { return LoyaltyRules.CanView(_session); } }
        public bool CanAdd { get { return LoyaltyRules.CanAdd(_session); } }
        public bool CanEdit { get { return LoyaltyRules.CanEdit(_session); } }
        public bool CanRemove { get { return LoyaltyRules.CanRemove(_session); } }
        public bool CanChangeDate { get { return LoyaltyRules.CanChangeDate(_session); } }

        public DateTime GetServerDate()
        {
            return _timeRepo.GetServerTimestamp().Date;
        }

        public string GenerateNumber(DateTime? date = null)
        {
            return _repo.GenerateNumber((date ?? GetServerDate()).Date);
        }

        public List<LoyaltyCustomerRow> LoadCustomers()
        {
            if (!CanView)
                return new List<LoyaltyCustomerRow>();
            return _repo.LoadCustomers(SystemConfig.CachTinhDiem, SystemConfig.DoanhSoTuongUngVoi1Diem);
        }

        public List<LoyaltyGroupNode> LoadGroups()
        {
            if (!CanView)
                return new List<LoyaltyGroupNode>();
            return _repo.LoadGroups();
        }

        public LoyaltyCustomerDetail LoadCustomerDetail(string customerId)
        {
            if (!CanView || string.IsNullOrEmpty(customerId))
                return new LoyaltyCustomerDetail();
            return _repo.LoadCustomerDetail(customerId);
        }

        public LoyaltyPointVoucherRow LoadVoucherById(string voucherId)
        {
            if (!CanView || string.IsNullOrEmpty(voucherId))
                return null;
            return _repo.LoadVoucherById(voucherId);
        }

        public List<CustomerLookupItem> LoadCustomerLookup()
        {
            return _customerRepo.LoadLookup();
        }

        public decimal GetCustomerPoints(string customerId, string excludeVoucherId = null)
        {
            if (string.IsNullOrEmpty(customerId))
                return 0m;
            return _repo.GetCustomerPointsForScreen(
                customerId,
                SystemConfig.CachTinhDiem,
                SystemConfig.DoanhSoTuongUngVoi1Diem,
                excludeVoucherId);
        }

        /// <summary>
        /// Lưu (thêm mới hoặc sửa) phiếu tặng/giảm điểm TTANGGIAMDIEM trong MỘT transaction cùng lưu vết
        /// và tự nâng nhóm (nếu điểm tặng &gt; 0 và cấu hình TuDongNangCapThanhVienKhiDatHanMuc bật).
        /// Tham số <paramref name="testHookAfterWrite"/> chỉ dùng cho selftest mô phỏng lỗi để kiểm rollback.
        /// </summary>
        public LoyaltySaveResult SaveVoucher(LoyaltyPointVoucherRow row, Action testHookAfterWrite = null)
        {
            if (row == null)
                return LoyaltySaveResult.Fail(LoyaltyConstants.VoucherNotFound);

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew ? !CanAdd : !CanEdit)
                return LoyaltySaveResult.Fail(LoyaltyConstants.NoPermission);

            LoyaltySaveResult valid = LoyaltyRules.ValidateVoucher(row);
            if (!valid.Ok)
                return valid;

            row.Number = (row.Number ?? "").Trim();
            row.Reason = (row.Reason ?? "").Trim();
            row.Note = (row.Note ?? "").Trim();
            row.GiftPoints = LoyaltyRules.NormalizePointInput(row.GiftPoints);
            row.ReducePoints = LoyaltyRules.NormalizePointInput(row.ReducePoints);

            int cachTinhDiem = SystemConfig.CachTinhDiem;
            int doanhSo1Diem = SystemConfig.DoanhSoTuongUngVoi1Diem;
            bool autoUpgrade = SystemConfig.TuDongNangCapThanhVienKhiDatHanMuc;

            LoyaltySaveResult outcome = null;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    LoyaltyPointVoucherRow existing = null;
                    if (!isNew)
                    {
                        existing = LoyaltyRepository.LoadVoucherByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = LoyaltySaveResult.Fail(LoyaltyConstants.VoucherNotFound);
                            return;
                        }
                    }

                    string custName, custAddr, cardId, groupId;
                    if (!ServicePaymentRepository.CustomerExistsIn(
                        conn, tx, row.CustomerId, out custName, out custAddr, out cardId, out groupId))
                    {
                        outcome = new LoyaltySaveResult
                        {
                            Ok = false,
                            HasCustomerError = true,
                            ErrorMessage = LoyaltyConstants.CustomerNotFound
                        };
                        return;
                    }

                    if (LoyaltyRepository.IsNumberTakenIn(conn, tx, row.Number, isNew ? "" : row.Id))
                    {
                        outcome = new LoyaltySaveResult
                        {
                            Ok = false,
                            HasNumberError = true,
                            ErrorMessage = LoyaltyConstants.NumberDuplicate(row.Number)
                        };
                        return;
                    }

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (!CanChangeDate)
                        row.Date = isNew ? serverNow.Date : existing.Date.Date;
                    else if (row.Date == DateTime.MinValue)
                        row.Date = serverNow.Date;
                    else
                        row.Date = row.Date.Date;

                    decimal basePoints = LoyaltyRepository.GetCustomerPointsForScreenIn(
                        conn, tx, row.CustomerId, cachTinhDiem, doanhSo1Diem, isNew ? null : row.Id);
                    string reduceErr = LoyaltyRules.ValidateReduceWithinBalance(basePoints, row.GiftPoints, row.ReducePoints);
                    if (reduceErr != null)
                    {
                        outcome = new LoyaltySaveResult
                        {
                            Ok = false,
                            HasPointsError = true,
                            ErrorMessage = reduceErr
                        };
                        return;
                    }

                    if (!isNew && !string.Equals(existing.CustomerId, row.CustomerId, StringComparison.Ordinal))
                    {
                        decimal oldCustAfter = LoyaltyRepository.GetCustomerPointsForScreenIn(
                            conn, tx, existing.CustomerId, cachTinhDiem, doanhSo1Diem, row.Id);
                        if (oldCustAfter < 0m)
                        {
                            outcome = LoyaltySaveResult.Fail(LoyaltyConstants.DeleteWouldMakeNegative);
                            return;
                        }
                    }

                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        row.Status = LoyaltyConstants.ActiveStatus;
                        row.CreatedById = LoyaltyConstants.ClipText(_session.UserId ?? "", 36);
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        LoyaltyRepository.InsertVoucherIn(conn, tx, row);
                    }
                    else
                    {
                        row.CreatedById = existing.CreatedById;
                        row.CreatedAt = existing.CreatedAt;
                        row.ModifiedById = LoyaltyConstants.ClipText(_session.UserId ?? "", 36);
                        row.ModifiedAt = serverNow;
                        if (LoyaltyRepository.UpdateVoucherIn(conn, tx, row) == 0)
                        {
                            outcome = LoyaltySaveResult.Fail(LoyaltyConstants.VoucherNotFound);
                            return;
                        }
                    }

                    string upgradedGroupName = null;
                    if (row.GiftPoints > 0m && autoUpgrade)
                    {
                        upgradedGroupName = ServicePaymentRepository.TryUpgradeCustomerGroupIn(
                            conn, tx, row.CustomerId, cachTinhDiem, doanhSo1Diem, _session.UserId, serverNow);
                    }

                    string tableName = LoyaltyConstants.ClipText(row.Number);
                    string actionVerb = isNew ? "Thêm" : "Sửa";
                    string logNote = LoyaltyConstants.ClipText(string.Format(
                        "{0} phiếu điểm {1} - KH: {2} (+{3}/-{4}) - Lý do: {5}",
                        actionVerb,
                        row.Number,
                        custName,
                        LoyaltyConstants.FormatPointsVi(row.GiftPoints),
                        LoyaltyConstants.FormatPointsVi(row.ReducePoints),
                        row.Reason));

                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = LoyaltyConstants.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = tableName,
                            Note = logNote,
                            Quantity = row.GiftPoints - row.ReducePoints,
                            Account = _session.UserName,
                            UserId = _session.UserId,
                            Time = serverNow
                        }
                    });

                    if (testHookAfterWrite != null)
                        testHookAfterWrite();

                    outcome = new LoyaltySaveResult
                    {
                        Ok = true,
                        SavedId = row.Id,
                        SavedNumber = row.Number,
                        CustomerName = custName,
                        UpgradedGroupName = upgradedGroupName
                    };
                });
            }
            catch
            {
                if (isNew)
                    ResetNew(row);
                throw;
            }

            if (isNew && (outcome == null || !outcome.Ok))
                ResetNew(row);

            return outcome ?? LoyaltySaveResult.Fail("Không thể lưu phiếu tặng/giảm điểm.");
        }

        /// <summary>
        /// Xóa cứng phiếu tặng/giảm điểm trong MỘT transaction cùng lưu vết sau khi kiểm xóa không làm điểm khách âm.
        /// </summary>
        public LoyaltyDeleteResult DeleteVoucher(string voucherId, Action testHookAfterWrite = null)
        {
            if (!CanRemove)
                return LoyaltyDeleteResult.Fail(LoyaltyConstants.NoPermission);
            if (string.IsNullOrEmpty(voucherId))
                return LoyaltyDeleteResult.Fail(LoyaltyConstants.VoucherNotFound);

            int cachTinhDiem = SystemConfig.CachTinhDiem;
            int doanhSo1Diem = SystemConfig.DoanhSoTuongUngVoi1Diem;

            LoyaltyDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                LoyaltyPointVoucherRow existing = LoyaltyRepository.LoadVoucherByIdIn(conn, tx, voucherId);
                if (existing == null)
                {
                    outcome = LoyaltyDeleteResult.Fail(LoyaltyConstants.VoucherNotFound);
                    return;
                }

                decimal pointsAfterDelete = LoyaltyRepository.GetCustomerPointsForScreenIn(
                    conn, tx, existing.CustomerId, cachTinhDiem, doanhSo1Diem, existing.Id);
                if (pointsAfterDelete < 0m)
                {
                    outcome = LoyaltyDeleteResult.Fail(LoyaltyConstants.DeleteWouldMakeNegative);
                    return;
                }

                if (LoyaltyRepository.DeleteVoucherIn(conn, tx, existing.Id) == 0)
                {
                    outcome = LoyaltyDeleteResult.Fail(LoyaltyConstants.VoucherNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string tableName = LoyaltyConstants.ClipText(existing.Number);
                string logNote = LoyaltyConstants.ClipText(string.Format(
                    "Xóa phiếu điểm {0} - KH: {1} (+{2}/-{3}) - Lý do: {4}",
                    existing.Number,
                    existing.CustomerName,
                    LoyaltyConstants.FormatPointsVi(existing.GiftPoints),
                    LoyaltyConstants.FormatPointsVi(existing.ReducePoints),
                    existing.Reason));

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = LoyaltyConstants.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = tableName,
                        Note = logNote,
                        Quantity = -(existing.GiftPoints - existing.ReducePoints),
                        Account = _session.UserName,
                        UserId = _session.UserId,
                        Time = serverNow
                    }
                });

                if (testHookAfterWrite != null)
                    testHookAfterWrite();

                outcome = new LoyaltyDeleteResult { Ok = true };
            });

            return outcome ?? LoyaltyDeleteResult.Fail("Không thể xóa phiếu tặng/giảm điểm.");
        }

        private static void ResetNew(LoyaltyPointVoucherRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }
    }
}
