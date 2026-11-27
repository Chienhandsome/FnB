using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho màn Tính lương / Chấm công (patch P3a, spec docs/specs/nhan-su-tinh-luong.md §1–§4).
    /// - Chế độ Tính lương: đọc/ghi cả TBANGLUONGTONGHOP và TBANGLUONGCHITIET, hỗ trợ Tải lại toàn bảng &amp; từng nhân viên.
    /// - Chế độ Chấm công: KHÔNG đọc tiền và TUYỆT ĐỐI KHÔNG tạo/sửa TBANGLUONGTONGHOP khi lưu.
    /// - Lưu và Xóa bảng lương chạy trong một transaction kèm ghi 1 dòng lưu vết (cắt chuỗi tối đa 255 ký tự).
    /// </summary>
    public sealed class PayrollService
    {
        private readonly UserSession _session;
        private readonly PayrollMode _mode;
        private readonly PayrollRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public PayrollService(UserSession session, PayrollMode mode)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _mode = mode;
            _repo = new PayrollRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public PayrollMode Mode { get { return _mode; } }
        public bool CanView { get { return PayrollRules.Can(_session, _mode, RoleModes.View); } }
        public bool CanAdd { get { return PayrollRules.Can(_session, _mode, RoleModes.Add); } }
        public bool CanEdit { get { return PayrollRules.Can(_session, _mode, RoleModes.Edit); } }
        public bool CanRemove { get { return PayrollRules.Can(_session, _mode, RoleModes.Remove); } }

        public List<PayrollPeriodRow> LoadPeriods()
        {
            if (!CanView)
                return new List<PayrollPeriodRow>();
            return _repo.LoadPeriods();
        }

        /// <summary>
        /// Nạp lưới cho một kỳ bảng lương đúng theo chế độ (Tính lương hoặc Chấm công).
        /// Ở chế độ Tính lương: NV đã lưu giữ nguyên tiền snapshot, NV chưa lưu tính từ danh mục + thưởng/phạt/tạm ứng.
        /// Ở chế độ Chấm công: chỉ nạp nhân viên, ca và chấm công (không đọc tiền).
        /// </summary>
        public PayrollSheetData LoadSheet(string payrollId)
        {
            if (!CanView || string.IsNullOrEmpty(payrollId))
                return null;

            // Chiến chốt 28/11: chế độ Chấm công chỉ ẩn cột tiền trên lưới; dữ liệu nạp và lưu giống hệt chế độ Tính lương (như gốc).
            PayrollSheetData sheet = _repo.LoadSheetForPayroll(payrollId);
            if (sheet == null)
                return null;

            if (sheet.Shifts.Count == 0)
                sheet.Warning = PayrollText.NoShiftsWarning;

            for (int i = 0; i < sheet.Employees.Count; i++)
            {
                PayrollEmployeeEntry emp = sheet.Employees[i];
                if (!emp.HasSavedSummary)
                    PayrollCalculator.RecalculateEmployee(sheet.Period.Year, sheet.Period.Month, emp, sheet.Shifts);
                else
                    PayrollCalculator.RecalculateAttendanceCountsOnly(sheet.Period.Year, sheet.Period.Month, emp, sheet.Shifts);
            }
            return sheet;
        }

        /// <summary>
        /// Tải lại toàn bảng (chỉ chế độ Tính lương): lấy lại lương ca/tháng/cách tính/nghỉ CN/T7 từ DNHANVIEN
        /// và thưởng/phạt/tạm ứng từ DB, thêm nhân viên hoạt động mới vào lưới, giữ nguyên chấm công đang hiện và tính lại.
        /// </summary>
        public bool ReloadAllFromCatalog(PayrollSheetData sheet, out string errorMessage)
        {
            errorMessage = null;
            if (_mode != PayrollMode.Payroll || !CanEdit)
            {
                errorMessage = PayrollText.NoPermission;
                return false;
            }
            if (sheet == null || sheet.Period == null)
            {
                errorMessage = PayrollText.PeriodNotFound;
                return false;
            }

            List<PayrollShiftInfo> freshShifts = _repo.LoadActiveShifts();
            sheet.Shifts = freshShifts;
            sheet.Warning = freshShifts.Count == 0 ? PayrollText.NoShiftsWarning : null;

            Dictionary<string, PayrollLiveSnapshot> snapshots = _repo.LoadLiveSnapshots(sheet.Period.Year, sheet.Period.Month);
            var existingById = new Dictionary<string, PayrollEmployeeEntry>(StringComparer.Ordinal);
            foreach (PayrollEmployeeEntry emp in sheet.Employees)
                existingById[emp.EmployeeId] = emp;

            // Cập nhật nhân viên đã có trên lưới (giữ nguyên Attendance và SummaryId).
            foreach (PayrollEmployeeEntry emp in sheet.Employees)
            {
                PayrollLiveSnapshot snap;
                if (snapshots.TryGetValue(emp.EmployeeId, out snap))
                    ApplyLiveSnapshot(emp, snap);
                PayrollCalculator.RecalculateEmployee(sheet.Period.Year, sheet.Period.Month, emp, sheet.Shifts);
            }

            // Bổ sung nhân viên mới thêm vào danh mục (STATUS = 30) chưa có trên lưới.
            foreach (KeyValuePair<string, PayrollLiveSnapshot> kv in snapshots)
            {
                PayrollLiveSnapshot snap = kv.Value;
                if (!snap.IsActiveEmployee || existingById.ContainsKey(snap.EmployeeId))
                    continue;
                var added = new PayrollEmployeeEntry { EmployeeId = snap.EmployeeId };
                ApplyLiveSnapshot(added, snap);
                PayrollCalculator.RecalculateEmployee(sheet.Period.Year, sheet.Period.Month, added, sheet.Shifts);
                sheet.Employees.Add(added);
            }

            sheet.Employees.Sort(delegate(PayrollEmployeeEntry a, PayrollEmployeeEntry b)
            {
                int cmp = StringComparer.CurrentCultureIgnoreCase.Compare(a.EmployeeName ?? "", b.EmployeeName ?? "");
                return cmp != 0 ? cmp : string.CompareOrdinal(a.EmployeeId ?? "", b.EmployeeId ?? "");
            });
            return true;
        }

        /// <summary>Tải lại thông số lương + thưởng/phạt/tạm ứng cho một nhân viên (giữ nguyên chấm công của nhân viên đó).</summary>
        public bool ReloadEmployeeFromCatalog(PayrollSheetData sheet, string employeeId, out string errorMessage)
        {
            errorMessage = null;
            if (_mode != PayrollMode.Payroll || !CanEdit)
            {
                errorMessage = PayrollText.NoPermission;
                return false;
            }
            if (sheet == null || sheet.Period == null || string.IsNullOrEmpty(employeeId))
            {
                errorMessage = PayrollText.PeriodNotFound;
                return false;
            }

            PayrollEmployeeEntry target = sheet.Employees.Find(e => string.Equals(e.EmployeeId, employeeId, StringComparison.Ordinal));
            if (target == null)
            {
                errorMessage = EmployeeText.NotFound;
                return false;
            }

            Dictionary<string, PayrollLiveSnapshot> snaps = _repo.LoadLiveSnapshots(sheet.Period.Year, sheet.Period.Month, employeeId);
            PayrollLiveSnapshot snap;
            if (!snaps.TryGetValue(employeeId, out snap))
            {
                errorMessage = EmployeeText.NotFound;
                return false;
            }

            ApplyLiveSnapshot(target, snap);
            PayrollCalculator.RecalculateEmployee(sheet.Period.Year, sheet.Period.Month, target, sheet.Shifts);
            return true;
        }

        /// <summary>Thêm mới kỳ bảng lương (kiểm tháng 1..12, năm 0..2999, chặn trùng tháng/năm, ghi lưu vết).</summary>
        public PayrollCreateResult CreatePeriod(int month, int year, string note = "", Action testHookAfterWrite = null)
        {
            if (!CanAdd)
                return PayrollCreateResult.Fail(PayrollText.NoPermission);

            string err = PayrollRules.ValidatePeriod(month, year);
            if (err != null)
                return PayrollCreateResult.Fail(err);

            PayrollCreateResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                if (PayrollRepository.ExistsPeriodIn(conn, tx, month, year))
                {
                    outcome = PayrollCreateResult.Fail(PayrollText.PeriodDuplicate(month, year));
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                var row = new PayrollPeriodRow
                {
                    Id = DbHelper.NewId(),
                    Name = PayrollText.FormatPeriodName(month, year),
                    Month = month,
                    Year = year,
                    Note = PayrollText.Clip(note),
                    Status = PayrollRepository.ActiveStatus,
                    CreatedById = _session.UserId ?? "",
                    CreatedAt = serverNow
                };

                PayrollRepository.InsertPeriodIn(conn, tx, row);
                WriteAuditIn(conn, tx, row.Id, row.Name, "Thêm mới bảng lương " + row.Name, serverNow);

                if (testHookAfterWrite != null)
                    testHookAfterWrite();

                outcome = new PayrollCreateResult { Ok = true, Created = row };
            });

            return outcome ?? PayrollCreateResult.Fail("Không thể thêm bảng lương.");
        }

        /// <summary>
        /// Lưu (Cập nhật) bảng lương hoặc chấm công trong MỘT transaction.
        /// - Chế độ Tính lương: upsert TBANGLUONGTONGHOP (bỏ qua NV chưa có dòng tổng hợp mà không có chấm công và không có tiền)
        ///   và đồng bộ TBANGLUONGCHITIET (thêm/sửa ô 1..3, xóa ô trống, dọn dòng trùng khóa).
        /// - Chế độ Chấm công: giống hệt chế độ Tính lương khi lưu (Chiến chốt 28/11: như gốc, Cập nhật vẫn ghi cả tiền); chỉ khác là lưới ẩn cột tiền.
        /// </summary>
        public PayrollSaveResult SaveSheet(PayrollSheetData sheet, Action testHookAfterWrite = null)
        {
            if (!CanEdit)
                return PayrollSaveResult.Fail(PayrollText.NoPermission);
            if (sheet == null || sheet.Period == null || string.IsNullOrEmpty(sheet.Period.Id))
                return PayrollSaveResult.Fail(PayrollText.PeriodNotFound);

            PayrollSaveResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                PayrollPeriodRow existingPeriod = PayrollRepository.LoadPeriodByIdIn(conn, tx, sheet.Period.Id);
                if (existingPeriod == null)
                {
                    outcome = PayrollSaveResult.Fail(PayrollText.PeriodNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string userId = _session.UserId ?? "";
                int savedEmpCount = 0;
                var committedSummaryIds = new Dictionary<PayrollEmployeeEntry, string>();

                // Chiến chốt 28/11: cả hai chế độ (Tính lương và Chấm công) đều ghi TBANGLUONGTONGHOP như gốc.
                {
                    Dictionary<string, int> scales = PayrollRepository.ReadNumericScalesIn(conn, tx, PayrollText.SummaryTableName);
                    List<string> dupSummaryIds;
                    Dictionary<string, string> existingSummaries = PayrollRepository.LoadSummaryIdsByEmployeeIn(
                        conn, tx, existingPeriod.Id, out dupSummaryIds);

                    foreach (string dupId in dupSummaryIds)
                        PayrollRepository.DeleteSummaryByIdIn(conn, tx, dupId);

                    for (int i = 0; i < sheet.Employees.Count; i++)
                    {
                        PayrollEmployeeEntry emp = sheet.Employees[i];
                        if (emp == null || string.IsNullOrEmpty(emp.EmployeeId))
                            continue;

                        PayrollCalculator.RecalculateEmployee(existingPeriod.Year, existingPeriod.Month, emp, sheet.Shifts);

                        string existingSumId;
                        bool hasDbSummary = existingSummaries.TryGetValue(emp.EmployeeId, out existingSumId);
                        bool hasActivity = PayrollCalculator.HasAttendanceOrMoney(emp);

                        if (hasDbSummary)
                        {
                            PayrollRepository.UpdateSummaryIn(conn, tx, existingSumId, emp, scales, userId, serverNow);
                            committedSummaryIds[emp] = existingSumId;
                            savedEmpCount++;
                        }
                        else if (hasActivity)
                        {
                            string newSumId = PayrollRepository.InsertSummaryIn(conn, tx, existingPeriod.Id, emp, scales, userId, serverNow);
                            committedSummaryIds[emp] = newSumId;
                            savedEmpCount++;
                        }
                    }
                }

                // Đồng bộ TBANGLUONGCHITIET (cả hai chế độ): khóa duy nhất (nhân viên, ca, ngày đủ 4 chữ số năm).
                int insertedDetails = 0;
                int updatedDetails = 0;
                int deletedDetails = 0;
                int removedDupDetails = 0;

                List<PayrollDetailRecord> existingDetails = PayrollRepository.LoadDetailRowsIn(conn, tx, existingPeriod.Id);
                var primaryByKey = new Dictionary<string, PayrollDetailRecord>(StringComparer.Ordinal);
                foreach (PayrollDetailRecord d in existingDetails)
                {
                    string key = DetailKey(d.EmployeeId, d.ShiftId, d.Date);
                    if (!primaryByKey.ContainsKey(key))
                    {
                        primaryByKey[key] = d;
                    }
                    else
                    {
                        PayrollRepository.DeleteDetailByIdIn(conn, tx, d.Id);
                        removedDupDetails++;
                    }
                }

                int totalDays = PayrollCalculator.DaysInMonth(existingPeriod.Year, existingPeriod.Month);
                int safeYear = existingPeriod.Year < 1 ? 1 : existingPeriod.Year;
                var handledKeys = new HashSet<string>(StringComparer.Ordinal);

                if (totalDays > 0)
                {
                    for (int i = 0; i < sheet.Employees.Count; i++)
                    {
                        PayrollEmployeeEntry emp = sheet.Employees[i];
                        if (emp == null || string.IsNullOrEmpty(emp.EmployeeId))
                            continue;

                        for (int s = 0; s < sheet.Shifts.Count; s++)
                        {
                            PayrollShiftInfo sh = sheet.Shifts[s];
                            if (sh == null || string.IsNullOrEmpty(sh.Id))
                                continue;

                            for (int day = 1; day <= totalDays; day++)
                            {
                                var date = new DateTime(safeYear, existingPeriod.Month, day);
                                string key = DetailKey(emp.EmployeeId, sh.Id, date);
                                handledKeys.Add(key);
                                AttendanceStatus targetStatus = emp.GetStatus(sh.Id, date);

                                PayrollDetailRecord current;
                                bool exists = primaryByKey.TryGetValue(key, out current);

                                if (targetStatus == AttendanceStatus.Empty)
                                {
                                    if (exists)
                                    {
                                        PayrollRepository.DeleteDetailByIdIn(conn, tx, current.Id);
                                        deletedDetails++;
                                    }
                                }
                                else if (exists)
                                {
                                    if (current.Status != targetStatus)
                                    {
                                        PayrollRepository.UpdateDetailStatusIn(conn, tx, current.Id, targetStatus, userId, serverNow);
                                        updatedDetails++;
                                    }
                                }
                                else
                                {
                                    PayrollRepository.InsertDetailIn(conn, tx, existingPeriod.Id, emp.EmployeeId, sh.Id, date, targetStatus, userId, serverNow);
                                    insertedDetails++;
                                }
                            }
                        }
                    }
                }

                // Dọn các dòng chi tiết có TRANGTHAI = 0 còn sót ngoài phạm vi (nếu có).
                foreach (KeyValuePair<string, PayrollDetailRecord> kv in primaryByKey)
                {
                    if (!handledKeys.Contains(kv.Key) && kv.Value.Status == AttendanceStatus.Empty)
                    {
                        PayrollRepository.DeleteDetailByIdIn(conn, tx, kv.Value.Id);
                        deletedDetails++;
                    }
                }

                string auditNote = string.Format("Cập nhật bảng lương {0} ({1} nhân viên)", existingPeriod.Name ?? "", savedEmpCount);
                WriteAuditIn(conn, tx, existingPeriod.Id, existingPeriod.Name, auditNote, serverNow);

                if (testHookAfterWrite != null)
                    testHookAfterWrite();

                foreach (KeyValuePair<PayrollEmployeeEntry, string> kv in committedSummaryIds)
                    kv.Key.SummaryId = kv.Value;

                outcome = new PayrollSaveResult
                {
                    Ok = true,
                    SavedEmployeeCount = savedEmpCount,
                    InsertedDetailCount = insertedDetails,
                    UpdatedDetailCount = updatedDetails,
                    DeletedDetailCount = deletedDetails,
                    RemovedDuplicateDetailCount = removedDupDetails
                };
            });

            return outcome ?? PayrollSaveResult.Fail("Không thể cập nhật bảng lương.");
        }

        /// <summary>
        /// Xóa bảng lương: chặn nếu đã có phiếu chi lương (TTHUCHI.TBANGLUONGID) và nêu số phiếu;
        /// ngược lại xóa sạch cả 3 bảng (TBANGLUONGCHITIET, TBANGLUONGTONGHOP, TBANGLUONG) trong 1 transaction kèm lưu vết.
        /// </summary>
        public PayrollDeleteResult DeletePeriod(string payrollId, Action testHookAfterWrite = null)
        {
            if (!CanRemove)
                return PayrollDeleteResult.Fail(PayrollText.NoPermission);
            if (string.IsNullOrEmpty(payrollId))
                return PayrollDeleteResult.Fail(PayrollText.PeriodNotFound);

            PayrollDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                PayrollPeriodRow existing = PayrollRepository.LoadPeriodByIdIn(conn, tx, payrollId);
                if (existing == null)
                {
                    outcome = PayrollDeleteResult.Fail(PayrollText.PeriodNotFound);
                    return;
                }

                List<string> vouchers = PayrollRepository.FindSalaryPayoutVoucherNumbersIn(conn, tx, payrollId);
                if (vouchers.Count > 0)
                {
                    outcome = new PayrollDeleteResult
                    {
                        Ok = false,
                        BlockingVoucherNumbers = vouchers,
                        ErrorMessage = PayrollText.DeleteBlockedByVouchers(existing.Name, vouchers)
                    };
                    return;
                }

                if (PayrollRepository.DeletePeriodCascadeIn(conn, tx, payrollId) == 0)
                {
                    outcome = PayrollDeleteResult.Fail(PayrollText.PeriodNotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                WriteAuditIn(conn, tx, existing.Id, existing.Name, "Xóa bảng lương " + (existing.Name ?? ""), serverNow);

                if (testHookAfterWrite != null)
                    testHookAfterWrite();

                outcome = new PayrollDeleteResult { Ok = true };
            });

            return outcome ?? PayrollDeleteResult.Fail("Không thể xóa bảng lương.");
        }

        private void WriteAuditIn(
            FirebirdSql.Data.FirebirdClient.FbConnection conn,
            FirebirdSql.Data.FirebirdClient.FbTransaction tx,
            string periodId,
            string periodName,
            string note,
            DateTime serverNow)
        {
            _activityLogger.WriteBatch(conn, tx, new[]
            {
                new ActivityLogRequest
                {
                    FunctionName = PayrollRules.FunctionNameOf(_mode),
                    Kind = ActivityKind.Khac,
                    BillId = periodId ?? "",
                    TableName = PayrollText.Clip(periodName),
                    Note = PayrollText.Clip(note),
                    Account = ResolveAccount(),
                    UserId = _session.UserId ?? "",
                    Time = serverNow
                }
            });
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ApplyLiveSnapshot(PayrollEmployeeEntry target, PayrollLiveSnapshot snap)
        {
            target.EmployeeCode = snap.EmployeeCode ?? "";
            target.EmployeeName = snap.EmployeeName ?? "";
            target.IsActiveEmployee = snap.IsActiveEmployee;
            target.SalaryMode = snap.SalaryMode;
            target.ShiftSalary = snap.ShiftSalary;
            target.MonthlySalary = snap.MonthlySalary;
            target.OffSunday = snap.OffSunday;
            target.OffSaturday = snap.OffSaturday;
            target.Bonus = snap.Bonus;
            target.Penalty = snap.Penalty;
            target.Advance = snap.Advance;
        }

        private static string DetailKey(string employeeId, string shiftId, DateTime date)
        {
            return (employeeId ?? "") + "|" + (shiftId ?? "") + "|" + date.ToString("yyyy-MM-dd");
        }
    }
}
