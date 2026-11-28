using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần của Danh mục nhân viên (DNHANVIEN, docs/specs/nhan-su-danh-muc.md §2).
    /// Không đụng DB, không đụng UI — dùng chung cho Service, UI và selftest.
    /// </summary>
    public static class EmployeeRules
    {
        /// <summary>So trùng tên nhân viên theo collation UNICODE_CI_AI (không phân biệt hoa/thường và dấu, không cắt khoảng trắng).</summary>
        public static bool AreNamesDuplicate(string left, string right)
        {
            return string.Equals(EmployeeText.FoldNoTrim(left), EmployeeText.FoldNoTrim(right), StringComparison.Ordinal);
        }

        /// <summary>So trùng mã nhân viên (không phân biệt hoa/thường, T2).</summary>
        public static bool AreCodesDuplicate(string left, string right)
        {
            string a = (left ?? "").Trim();
            string b = (right ?? "").Trim();
            if (a.Length == 0 || b.Length == 0)
                return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsValidSalaryMode(int mode)
        {
            return mode == EmployeeSalaryMode.ByShift
                || mode == EmployeeSalaryMode.ByMonth
                || mode == EmployeeSalaryMode.MonthByShift;
        }

        public static string SalaryModeDisplayText(int mode)
        {
            switch (mode)
            {
                case EmployeeSalaryMode.ByMonth:
                    return "Lương tháng theo ngày";
                case EmployeeSalaryMode.MonthByShift:
                    return "Lương tháng theo ca";
                default:
                    return "Lương theo ca";
            }
        }

        public static bool IsShiftSalaryLocked(int salaryMode)
        {
            return salaryMode == EmployeeSalaryMode.ByMonth || salaryMode == EmployeeSalaryMode.MonthByShift;
        }

        public static bool IsMonthlySalaryLocked(int salaryMode)
        {
            return salaryMode == EmployeeSalaryMode.ByShift;
        }

        public static bool AreOffDayFlagsEnabled(int salaryMode)
        {
            return salaryMode == EmployeeSalaryMode.ByMonth;
        }

        /// <summary>
        /// Kiểm tra dữ liệu nhân viên trước khi lưu theo spec §2:
        /// Tên bắt buộc (<= 255), Mã &amp; Ca bắt buộc khi bật máy chấm công, độ dài chữ <= 255,
        /// cách tính lương hợp lệ (0/30/60), lương không âm và tối đa 9 chữ số phần nguyên, không trùng Tên/Mã.
        /// </summary>
        public static EmployeeSaveResult Validate(
            EmployeeRow row,
            bool useTimekeeper,
            bool duplicateName,
            bool duplicateCode)
        {
            var result = new EmployeeSaveResult();
            if (row == null)
                return EmployeeSaveResult.Fail("Không có dữ liệu nhân viên.");

            var errors = new List<string>();
            string name = row.Name ?? "";
            string code = row.Code ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                result.HasNameError = true;
                errors.Add(EmployeeText.NameEmpty);
            }
            if (name.Length > EmployeeText.MaxTextLength)
            {
                result.HasNameError = true;
                errors.Add(EmployeeText.FieldTooLong("Tên nhân viên", EmployeeText.MaxTextLength));
            }

            if (useTimekeeper && string.IsNullOrWhiteSpace(code))
            {
                result.HasCodeError = true;
                errors.Add(EmployeeText.CodeEmpty);
            }
            if (code.Length > EmployeeText.MaxTextLength)
            {
                result.HasCodeError = true;
                errors.Add(EmployeeText.FieldTooLong("Mã nhân viên", EmployeeText.MaxTextLength));
            }

            if (row.Phone != null && row.Phone.Length > EmployeeText.MaxTextLength)
            {
                result.HasPhoneError = true;
                errors.Add(EmployeeText.FieldTooLong("Điện thoại", EmployeeText.MaxTextLength));
            }
            if (row.Address != null && row.Address.Length > EmployeeText.MaxTextLength)
            {
                result.HasAddressError = true;
                errors.Add(EmployeeText.FieldTooLong("Địa chỉ", EmployeeText.MaxTextLength));
            }
            if (row.Note != null && row.Note.Length > EmployeeText.MaxTextLength)
            {
                result.HasNoteError = true;
                errors.Add(EmployeeText.FieldTooLong("Ghi chú", EmployeeText.MaxTextLength));
            }

            if (useTimekeeper && string.IsNullOrWhiteSpace(row.ShiftId))
            {
                result.HasShiftError = true;
                errors.Add(EmployeeText.ShiftEmpty);
            }

            if (!IsValidSalaryMode(row.SalaryMode))
            {
                errors.Add(EmployeeText.SalaryModeInvalid);
            }
            if (row.ShiftSalary < 0m || row.ShiftSalary > EmployeeText.MaxSalary)
            {
                result.HasShiftSalaryError = true;
                errors.Add(EmployeeText.ShiftSalaryInvalid);
            }
            if (row.MonthlySalary < 0m || row.MonthlySalary > EmployeeText.MaxSalary)
            {
                result.HasMonthlySalaryError = true;
                errors.Add(EmployeeText.MonthlySalaryInvalid);
            }

            if (errors.Count == 0)
            {
                if (duplicateName)
                {
                    result.HasNameError = true;
                    errors.Add(EmployeeText.NameDuplicate(name));
                }
                if (duplicateCode)
                {
                    result.HasCodeError = true;
                    errors.Add(EmployeeText.CodeDuplicate(code));
                }
            }

            if (errors.Count > 0)
            {
                result.ErrorMessage = string.Join(Environment.NewLine, errors.ToArray());
                return result;
            }

            result.Ok = true;
            return result;
        }

        /// <summary>Thông báo chặn xóa nhân viên nêu rõ từng nơi dùng và số lượng theo spec §2.</summary>
        public static string BuildReferenceBlockedMessage(string employeeName, IList<ReferenceBlock> blocks)
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
                return ReferenceChecker.BlockedMessage("nhân viên", employeeName ?? "", total);
            return string.Format("Không thể xóa nhân viên '{0}'. Nhân viên đang được dùng: {1}.",
                employeeName ?? "", string.Join(", ", parts.ToArray()));
        }

        private static string LabelForTable(string tableName)
        {
            foreach (EmployeeReferenceColumn rc in EmployeeRepository.ReferenceColumns)
            {
                if (string.Equals(rc.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                    return rc.DisplayLabel;
            }
            return string.IsNullOrEmpty(tableName) ? "dữ liệu liên kết" : tableName;
        }

        /// <summary>Kiểm quyền theo tên chức năng "Danh mục nhân viên"; Admin luôn được.</summary>
        public static bool Can(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return AppSession.Can(session, EmployeeText.FunctionName, bit);
        }

        public static bool CanCurrent(int bit)
        {
            return Can(AppSession.Current, bit);
        }
    }
}
