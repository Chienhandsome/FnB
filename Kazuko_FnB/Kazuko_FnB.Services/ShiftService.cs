using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ cho Danh mục ca làm việc (DCALAMVIEC, docs/specs/nhan-su-danh-muc.md §3):
    /// kiểm quyền theo tên chức năng, đọc cờ TinhLuongSuDungMayChamCong mỗi lần lưu,
    /// chuẩn hóa TUGIO/DENGIO về 2000-01-01 HH:mm (cho phép ca qua đêm, chặn 0 thời lượng khi bật máy chấm công),
    /// giữ nguyên TUGIO/DENGIO cũ khi tắt máy chấm công, tỉ lệ lương 0..1000,
    /// xóa cứng chặn khi còn nhân viên hoặc dòng chấm công dùng, ghi lưu vết TLUUVET trong cùng transaction.
    /// </summary>
    public sealed class ShiftService
    {
        private readonly UserSession _session;
        private readonly ShiftRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public ShiftService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new ShiftRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return ShiftRules.Can(_session, RoleModes.View); } }
        public bool CanAdd { get { return ShiftRules.Can(_session, RoleModes.Add); } }
        public bool CanEdit { get { return ShiftRules.Can(_session, RoleModes.Edit); } }
        public bool CanRemove { get { return ShiftRules.Can(_session, RoleModes.Remove); } }

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
            return values.TryGetValue(ShiftText.ConfigUseTimekeeperKey, out v) && v != null && v.IntValue == SystemConfig.On;
        }

        public List<ShiftRow> LoadAll()
        {
            if (!CanView)
                return new List<ShiftRow>();
            return _repo.LoadAll();
        }

        public ShiftRow LoadById(string id)
        {
            if (!CanView)
                return null;
            return _repo.LoadById(id);
        }

        public List<ReferenceBlock> CheckReferences(string id)
        {
            return _repo.CheckReferences(id);
        }

        public ShiftSaveResult Save(ShiftRow row)
        {
            if (row == null)
                return ShiftSaveResult.Fail("Không có dữ liệu ca làm việc.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return ShiftSaveResult.Fail(ShiftText.NoPermission);
            if (!isNew && !CanEdit)
                return ShiftSaveResult.Fail(ShiftText.NoPermission);

            bool useTimekeeper = ReadUseTimekeeper();
            ShiftSaveResult precheck = ShiftRules.Validate(row, useTimekeeper, false);
            if (!precheck.Ok)
                return precheck;

            ShiftSaveResult outcome = null;
            bool assignedNewId = false;
            try
            {
                _repo.RunInTransaction((conn, tx) =>
                {
                    ShiftRow existing = null;
                    if (!isNew)
                    {
                        existing = ShiftRepository.LoadByIdIn(conn, tx, row.Id);
                        if (existing == null)
                        {
                            outcome = ShiftSaveResult.Fail(ShiftText.NotFound);
                            return;
                        }
                    }

                    if (ShiftRepository.IsNameTakenIn(conn, tx, row.Name, isNew ? "" : row.Id))
                    {
                        outcome = ShiftSaveResult.Fail(ShiftText.NameDuplicate(row.Name), true);
                        return;
                    }

                    if (useTimekeeper)
                    {
                        row.FromTime = ShiftRules.NormalizeShiftTime(row.FromTime);
                        row.ToTime = ShiftRules.NormalizeShiftTime(row.ToTime);
                    }
                    else
                    {
                        row.FromTime = existing != null ? existing.FromTime : null;
                        row.ToTime = existing != null ? existing.ToTime : null;
                    }

                    row.SalaryRate = Math.Round(row.SalaryRate, 2, MidpointRounding.AwayFromZero);

                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    if (isNew)
                    {
                        row.Id = DbHelper.NewId();
                        assignedNewId = true;
                        row.Status = ShiftRepository.ActiveStatus;
                        row.CreatedById = _session.UserId ?? "";
                        row.CreatedAt = serverNow;
                        row.ModifiedById = null;
                        row.ModifiedAt = null;
                        ShiftRepository.InsertIn(conn, tx, row);
                    }
                    else
                    {
                        row.ModifiedById = _session.UserId ?? "";
                        row.ModifiedAt = serverNow;
                        if (ShiftRepository.UpdateIn(conn, tx, row) == 0)
                        {
                            outcome = ShiftSaveResult.Fail(ShiftText.NotFound);
                            return;
                        }
                    }

                    string note = (isNew ? "Thêm mới ca làm việc: " : "Cập nhật ca làm việc: ") + (row.Name ?? "");
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = ShiftText.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = row.Id,
                            TableName = Clip(row.Name),
                            Note = Clip(note),
                            Account = ResolveAccount(),
                            UserId = _session.UserId ?? "",
                            Time = serverNow
                        }
                    });

                    outcome = new ShiftSaveResult
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

            return outcome ?? ShiftSaveResult.Fail("Không thể lưu ca làm việc.");
        }

        public ShiftDeleteResult Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return ShiftDeleteResult.Fail(ShiftText.NotFound);
            if (!CanRemove)
                return ShiftDeleteResult.Fail(ShiftText.NoPermission);

            ShiftDeleteResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                ShiftRow existing = ShiftRepository.LoadByIdIn(conn, tx, id);
                if (existing == null)
                {
                    outcome = ShiftDeleteResult.Fail(ShiftText.NotFound);
                    return;
                }

                List<ReferenceBlock> blocks = ShiftRepository.CheckReferencesIn(conn, tx, id);
                int totalRefs = 0;
                foreach (ReferenceBlock b in blocks)
                    totalRefs += b.Count;
                if (totalRefs > 0)
                {
                    outcome = new ShiftDeleteResult
                    {
                        Ok = false,
                        ReferenceCount = totalRefs,
                        Blocks = blocks,
                        ErrorMessage = ShiftRules.BuildReferenceBlockedMessage(existing.Name, blocks)
                    };
                    return;
                }

                if (ShiftRepository.DeleteIn(conn, tx, id) == 0)
                {
                    outcome = ShiftDeleteResult.Fail(ShiftText.NotFound);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ShiftText.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = existing.Id,
                        TableName = Clip(existing.Name),
                        Note = Clip("Xóa ca làm việc: " + (existing.Name ?? "")),
                        Account = ResolveAccount(),
                        UserId = _session.UserId ?? "",
                        Time = serverNow
                    }
                });

                outcome = new ShiftDeleteResult
                {
                    Ok = true,
                    Blocks = blocks
                };
            });

            return outcome ?? ShiftDeleteResult.Fail("Không thể xóa ca làm việc.");
        }

        private string ResolveAccount()
        {
            if (!string.IsNullOrEmpty(_session.UserName))
                return _session.UserName;
            return _session.DisplayName ?? "";
        }

        private static void ResetNew(ShiftRow row)
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
