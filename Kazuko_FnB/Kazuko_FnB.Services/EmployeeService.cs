using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Danh mục nhân viên (DNHANVIEN, docs/specs/nhan-su-danh-muc.md §2):
    /// kiểm quyền theo tên chức năng, đọc cờ TinhLuongSuDungMayChamCong mỗi lần lưu,
    /// kiểm bắt buộc/độ dài/trùng Tên (UNICODE_CI_AI) &amp; Mã (không phân biệt hoa thường),
    /// giữ nguyên giá trị các ô bị khóa/ẩn (CODE, DCALAMVIECID, LUONGCA/LUONGTHANG, NGHICHUNHAT/NGHITHU7, LUONGTHEOCA, SIMAGEID),
    /// xóa cứng chặn khi còn tham chiếu ở 8 bảng, ghi lưu vết TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class EmployeeService
    {
        private readonly UserSession _session;
        private readonly EmployeeRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public EmployeeService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new EmployeeRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return EmployeeRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return EmployeeRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return EmployeeRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return EmployeeRules.Can(_session, RoleModes.Remove); } }

        /// <summary>Đọc lại cấu hình TinhLuongSuDungMayChamCong mới nhất từ CSDL của phiên.</summary>
        public bool ReadUseTimekeeper()
        {
            UserSession current = AppSession.Current;
            if (current != null && string.Equals(current.DatabasePath, _session.DatabasePath, StringComparison.OrdinalIgnoreCase))
            {
                SystemConfig.Reload(_session.DatabasePath);
                return SystemConfig.TinhLuongSuDungMayChamCong;
            }
            Dictionary<string, ConfigValue> values = new ConfigRepository(_session.DatabasePath).LoadValues();
            ConfigValue v;
            return values.TryGetValue(EmployeeText.ConfigUseTimekeeperKey, out v) && v != null && v.IntValue == SystemConfig.On;
        }

        public List<EmployeeRow> LoadAll()
        {
            if (!CanView)
                return new List<EmployeeRow>();
            return _repo.LoadAll();
        }

        public EmployeeRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public List<LookupItem> LoadActiveShifts()
        {
            return _repo.LoadActiveShifts();
        }

        public List<ReferenceBlock> CheckReferences(string id)
        {
            return _repo.CheckReferences(id);
        }

        public EmployeeSaveResult Save(EmployeeRow row)
        {
            if (row == null)
                return EmployeeSaveResult.Fail("Không có dữ liệu nhân viên.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return EmployeeSaveResult.Fail(EmployeeText.NoPermission);
            if (!isNew && !CanEdit)
                return EmployeeSaveResult.Fail(EmployeeText.NoPermission);

            bool useTimekeeper = ReadUseTimekeeper();
            EmployeeSaveResult precheck = EmployeeRules.Validate(row, useTimekeeper, false, false);
            if (!precheck.Ok)
                return precheck;

            EmployeeSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    EmployeeRow existing = null;
                    if (!isNew)
                    {
                        existing = EmployeeRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = EmployeeSaveResult.Fail(EmployeeText.NotFound);
                            return;
                        }
                    }

                    // Khi tắt máy chấm công: giữ nguyên CODE và DCALAMVIECID cũ khi sửa; dòng mới để trống ca.
                    if (!useTimekeeper)
                    {
                        if (existing != null)
                        {
                            row.Code = existing.Code ?? "";
                            row.ShiftId = existing.ShiftId;
                        }
                        else
                        {
                            row.ShiftId = null;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(row.ShiftId))
                    {
                        bool keepOldShift = existing != null
                            && string.Equals(existing.ShiftId, row.ShiftId, StringComparison.Ordinal);
                        if (!keepOldShift && !EmployeeRepository.ShiftExistsIn(conn, tx, row.ShiftId))
                        {
                            outcome = EmployeeSaveResult.Fail(EmployeeText.ShiftInvalid, false, false, true);
                            return;
                        }
                    }

                    string excludeId = isNew ? "" : row.Id;
                    if (EmployeeRepository.IsNameTakenIn(conn, tx, row.Name, excludeId))
                    {
                        outcome = EmployeeSaveResult.Fail(EmployeeText.NameDuplicate(row.Name), true, false, false);
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(row.Code)
                        && EmployeeRepository.IsCodeTakenIn(conn, tx, row.Code, excludeId))
                    {
                        outcome = EmployeeSaveResult.Fail(EmployeeText.CodeDuplicate(row.Code), false, true, false);
                        return;
                    }

                    row.ShiftSalary = Math.Round(row.ShiftSalary, 2, MidpointRounding.AwayFromZero);
                    row.MonthlySalary = Math.Round(row.MonthlySalary, 2, MidpointRounding.AwayFromZero);
                    row.OffSunday = row.OffSunday == EmployeeText.FlagOn ? EmployeeText.FlagOn : EmployeeText.FlagOff;
                    row.OffSaturday = row.OffSaturday == EmployeeText.FlagOn ? EmployeeText.FlagOn : EmployeeText.FlagOff;

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = EmployeeRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        EmployeeRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        // Giá trị các ô bị khóa theo cách tính lương phải giữ nguyên giá trị cũ trong DB (không đặt 0).
                        if (EmployeeRules.IsMonthlySalaryLocked(row.SalaryMode))
                            row.MonthlySalary = existing.MonthlySalary;
                        if (EmployeeRules.IsShiftSalaryLocked(row.SalaryMode))
                            row.ShiftSalary = existing.ShiftSalary;
                        if (!EmployeeRules.AreOffDayFlagsEnabled(row.SalaryMode))
                        {
                            row.OffSunday = existing.OffSunday;
                            row.OffSaturday = existing.OffSaturday;
                        }
                        row.LegacyShiftSalary = existing.LegacyShiftSalary;
                        row.ImageId = existing.ImageId;
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (EmployeeRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = EmployeeSaveResult.Fail(EmployeeText.NotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới nhân viên: " : "Cập nhật nhân viên: ") + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = EmployeeText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = Clip(row.Name),
                            Note = Clip(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    outcome = new EmployeeSaveResult
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

            return outcome ?? EmployeeSaveResult.Fail("Không thể lưu nhân viên.");
        }

        public EmployeeDeleteResult Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return EmployeeDeleteResult.Fail(EmployeeText.NotFound);
            if (!CanRemove)
                return EmployeeDeleteResult.Fail(EmployeeText.NoPermission);

            EmployeeDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                EmployeeRow existing = EmployeeRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = EmployeeDeleteResult.Fail(EmployeeText.NotFound);
                    return;
                }

                List<ReferenceBlock> blocks = EmployeeRepository.CheckReferencesIn(conn, tx, id);
                int totalRefs = 0;
                foreach (ReferenceBlock b in blocks)
                    totalRefs += b.Count;
                if (totalRefs > 0)
                {
                    outcome = new EmployeeDeleteResult
                    {
                        Ok = false,
                        ReferenceCount = totalRefs,
                        Blocks = blocks,
                        ErrorMessage = EmployeeRules.BuildReferenceBlockedMessage(existing.Name, blocks)
                    };
                    return;
                }

                if (EmployeeRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = EmployeeDeleteResult.Fail(EmployeeText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = EmployeeText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = Clip(existing.Name),
                        Note = Clip("Xóa nhân viên: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new EmployeeDeleteResult
                {
                    Ok = true,
                    Blocks = blocks
                };
            });

            return outcome ?? EmployeeDeleteResult.Fail("Không thể xóa nhân viên.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(EmployeeRow row)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
        }

        private static string Clip(string text)
        {
            text = text ?? "";
            return text.Length > 255 ? text.Substring(0, 255) : text;
        }
    }
}
