using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần của Danh mục ca làm việc (DCALAMVIEC, docs/specs/nhan-su-danh-muc.md §3).
    /// Không đụng DB, không đụng UI — dùng chung cho Service, UI và selftest.
    /// </summary>
    public static class ShiftRules
    {
        /// <summary>So trùng tên ca theo collation UNICODE_CI_AI (không phân biệt hoa/thường và dấu, không cắt khoảng trắng).</summary>
        public static bool AreNamesDuplicate(string left, string right)
        {
            return string.Equals(ShiftText.FoldNoTrim(left), ShiftText.FoldNoTrim(right), StringComparison.Ordinal);
        }

        /// <summary>Chuẩn hóa giờ ca về ngày cố định 2000-01-01, chỉ giữ giờ:phút (bỏ giây).</summary>
        public static DateTime? NormalizeShiftTime(DateTime? value)
        {
            if (!value.HasValue)
                return null;
            DateTime d = value.Value;
            return new DateTime(2000, 1, 1, d.Hour, d.Minute, 0);
        }

        /// <summary>Định dạng giờ ca trên lưới (HH:mm), rỗng nếu chưa thiết lập.</summary>
        public static string FormatShiftTime(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : "";
        }

        /// <summary>Hai mốc giờ có trùng nhau theo giờ:phút không (ca không có thời lượng, T4)?</summary>
        public static bool AreSameHourMinute(DateTime from, DateTime to)
        {
            return from.Hour == to.Hour && from.Minute == to.Minute;
        }

        /// <summary>
        /// Kiểm tra dữ liệu ca làm việc trước khi lưu theo spec §3:
        /// Tên bắt buộc (<= 255), Ghi chú (<= 255), Tỉ lệ lương 0..1000,
        /// khi bật máy chấm công bắt buộc Từ giờ &amp; Đến giờ và không được trùng nhau (ca qua đêm hợp lệ), không trùng Tên.
        /// </summary>
        public static ShiftSaveResult Validate(ShiftRow row, bool useTimekeeper, bool duplicateName)
        {
            var result = new ShiftSaveResult();
            if (row == null)
                return ShiftSaveResult.Fail("Không có dữ liệu ca làm việc.");

            var errors = new List<string>();
            string name = row.Name ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                result.HasNameError = true;
                errors.Add(ShiftText.NameEmpty);
            }
            if (name.Length > ShiftText.MaxTextLength)
            {
                result.HasNameError = true;
                errors.Add(ShiftText.FieldTooLong("Tên ca làm việc", ShiftText.MaxTextLength));
            }
            if (row.Note != null && row.Note.Length > ShiftText.MaxTextLength)
            {
                result.HasNoteError = true;
                errors.Add(ShiftText.FieldTooLong("Ghi chú", ShiftText.MaxTextLength));
            }

            if (row.SalaryRate < ShiftText.MinSalaryRate || row.SalaryRate > ShiftText.MaxSalaryRate)
            {
                result.HasSalaryRateError = true;
                errors.Add(ShiftText.SalaryRateInvalid);
            }

            if (useTimekeeper)
            {
                if (!row.FromTime.HasValue)
                {
                    result.HasFromTimeError = true;
                    errors.Add(ShiftText.FromTimeEmpty);
                }
                if (!row.ToTime.HasValue)
                {
                    result.HasToTimeError = true;
                    errors.Add(ShiftText.ToTimeEmpty);
                }
                if (row.FromTime.HasValue && row.ToTime.HasValue
                    && AreSameHourMinute(row.FromTime.Value, row.ToTime.Value))
                {
                    result.HasToTimeError = true;
                    errors.Add(ShiftText.ZeroDuration);
                }
            }

            if (errors.Count == 0 && duplicateName)
            {
                result.HasNameError = true;
                errors.Add(ShiftText.NameDuplicate(name));
            }

            if (errors.Count > 0)
            {
                result.ErrorMessage = string.Join(Environment.NewLine, errors.ToArray());
                return result;
            }

            result.Ok = true;
            return result;
        }

        /// <summary>Thông báo chặn xóa ca làm việc nêu rõ số nhân viên và dòng chấm công đang dùng theo spec §3.</summary>
        public static string BuildReferenceBlockedMessage(string shiftName, IList<ReferenceBlock> blocks)
        {
            var parts = new List<string>();
            int total = 0;
            if (blocks != null)
            {
                foreach (ReferenceBlock b in blocks)
                {
                    if (b == null || b.Count <= 0)
                        continue;
                    total += b.Count;
                    parts.Add(b.Count + " " + LabelForTable(b.TableName));
                }
            }
            if (parts.Count == 0)
                return ReferenceChecker.BlockedMessage("ca làm việc", shiftName ?? "", total);
            return string.Format("Không thể xóa ca làm việc '{0}'. Ca làm việc đang được dùng: {1}.",
                shiftName ?? "", string.Join(", ", parts.ToArray()));
        }

        private static string LabelForTable(string tableName)
        {
            foreach (ShiftReferenceColumn rc in ShiftRepository.ReferenceColumns)
            {
                if (string.Equals(rc.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                    return rc.DisplayLabel;
            }
            return string.IsNullOrEmpty(tableName) ? "dữ liệu liên kết" : tableName;
        }

        /// <summary>Kiểm quyền theo tên chức năng "Danh mục ca làm việc"; Admin luôn được.</summary>
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return AppSession.Can(session, ShiftText.FunctionName, bit);
        }

        public static bool CanCurrent(int bit)
        {
            return Can(AppSession.Current, bit);
        }
    }
}
