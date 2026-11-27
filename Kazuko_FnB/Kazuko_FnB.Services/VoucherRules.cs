using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật nghiệp vụ thuần cho Phiếu thu / Phiếu chi và danh mục Quỹ (docs/specs/phieu-thu-chi.md §2, §3, §5).
    /// Không đụng DB, không đụng giao diện.
    /// </summary>
    public static class VoucherRules
    {
        /// <summary>Kiểm tra đầu vào phiếu thu / chi trước khi ghi DB.</summary>
        public static string Validate(VoucherRow row)
        {
            if (row == null)
                return VoucherConstants.VoucherNotFound;

            var missing = new List<string>();
            string number = (row.Name ?? "").Trim();
            if (number.Length == 0)
                missing.Add("Số phiếu");
            if (string.IsNullOrWhiteSpace(row.StoreId))
                missing.Add("Cửa hàng");
            if (missing.Count > 0)
                return "Mời bạn nhập: " + string.Join(", ", missing.ToArray());

            if (row.Amount <= 0m)
                return VoucherConstants.AmountMustBePositive;

            if (row.Transfer && string.IsNullOrWhiteSpace(row.BankAccountId))
                return VoucherConstants.BankAccountMissing;

            string lenErr = CheckLength(number, "Số phiếu")
                ?? CheckLength(row.ObjectName, "Tên đối tượng")
                ?? CheckLength(row.Address, "Địa chỉ")
                ?? CheckLength(row.Description, "Diễn giải")
                ?? CheckLength(row.OriginalDoc, "Chứng từ gốc")
                ?? CheckLength(row.Note, "Ghi chú");
            if (lenErr != null)
                return lenErr;

            return null;
        }

        private static string CheckLength(string value, string label)
        {
            if (value != null && value.Length > VoucherConstants.MaxTextLength)
                return VoucherConstants.FieldTooLong(label, VoucherConstants.MaxTextLength);
            return null;
        }

        /// <summary>
        /// Kiểm tra phiếu có thuộc chứng từ gốc khác không (spec §2 &amp; §5 T2):
        /// TDATHANGID, TDONHANGID, TBANGLUONGID, DTHETRATRUOCID, LAPHIEUTHUCONGNO, LATAMUNG,
        /// hoặc lý do Số dư đầu kỳ / Chuyển quỹ -> không cho sửa/xóa ở màn Phiếu thu chi.
        /// Trả về câu báo rõ ràng nếu bị chặn; null nếu được phép.
        /// </summary>
        public static string GetLinkedBlockMessage(VoucherRow row)
        {
            if (row == null)
                return null;
            if (!string.IsNullOrWhiteSpace(row.BookingId))
                return VoucherConstants.LockedLinkedVoucher("phiếu đặt hàng (tiền cọc)");
            if (!string.IsNullOrWhiteSpace(row.OrderId))
                return VoucherConstants.LockedLinkedVoucher("hóa đơn bán hàng");
            if (!string.IsNullOrWhiteSpace(row.PayrollId))
                return VoucherConstants.LockedLinkedVoucher("bảng lương");
            if (!string.IsNullOrWhiteSpace(row.PrepaidCardId))
                return VoucherConstants.LockedLinkedVoucher("thẻ trả trước");
            if (row.IsDebtReceipt || row.RawLoai == 2)
                return VoucherConstants.LockedLinkedVoucher("phiếu thu công nợ");
            if (row.IsAdvance)
                return VoucherConstants.LockedLinkedVoucher("tạm ứng");
            if (string.Equals(row.ReasonId, FundReasons.OpeningBalanceId, StringComparison.OrdinalIgnoreCase))
                return VoucherConstants.LockedLinkedVoucher("số dư đầu kỳ");
            if (string.Equals(row.ReasonId, FundReasons.TransferId, StringComparison.OrdinalIgnoreCase))
                return VoucherConstants.LockedLinkedVoucher("chuyển quỹ");
            return null;
        }

        /// <summary>
        /// Kiểm tra chi tiền mặt vượt quỹ (spec §2 &amp; §5 T3):
        /// chỉ áp cho phiếu chi tiền mặt (LOAI = 1, CHUYENKHOAN = 0).
        /// Khi sửa phiếu chi tiền mặt cũ cùng cửa hàng, cộng hoàn lại số cũ trước khi trừ số mới.
        /// </summary>
        public static string CheckCashOverdraft(VoucherRow newRow, VoucherRow existingRow, decimal currentStoreCashBalance)
        {
            if (newRow == null || newRow.Kind != VoucherKind.Expense || newRow.Transfer)
                return null;

            decimal available = currentStoreCashBalance;
            if (existingRow != null
                && existingRow.Kind == VoucherKind.Expense
                && !existingRow.Transfer
                && string.Equals(existingRow.StoreId ?? "", newRow.StoreId ?? "", StringComparison.Ordinal))
            {
                available += existingRow.Amount;
            }

            if (available - newRow.Amount < 0m)
                return VoucherConstants.CashOverdraftBlocked(available, newRow.Amount);
            return null;
        }

        /// <summary>Chuẩn hóa liên kết đối tượng theo LOAIDOITUONG (0..3) và tài khoản theo CHUYENKHOAN.</summary>
        public static void NormalizeLinks(VoucherRow row)
        {
            if (row == null)
                return;
            row.Name = (row.Name ?? "").Trim();
            row.ObjectName = (row.ObjectName ?? "").Trim();
            row.Address = (row.Address ?? "").Trim();
            row.Description = (row.Description ?? "").Trim();
            row.OriginalDoc = (row.OriginalDoc ?? "").Trim();
            row.Note = (row.Note ?? "").Trim();

            if (row.ObjectType < PhieuThuRules.OtherObject || row.ObjectType > PhieuThuRules.Supplier)
                row.ObjectType = PhieuThuRules.OtherObject;

            if (row.ObjectType != PhieuThuRules.Employee)
                row.EmployeeId = null;
            if (row.ObjectType != PhieuThuRules.Customer)
                row.CustomerId = null;
            if (row.ObjectType != PhieuThuRules.Supplier)
                row.SupplierId = null;
            if (!row.Transfer)
                row.BankAccountId = null;
        }

        /// <summary>Lọc nhanh danh sách phiếu theo chuỗi tìm không phân biệt hoa/thường/dấu tiếng Việt.</summary>
        public static bool MatchesSearch(VoucherRow row, string query)
        {
            if (row == null)
                return false;
            string q = CatalogRepository.Fold((query ?? "").Trim());
            if (q.Length == 0)
                return true;

            string[] fields =
            {
                row.Name ?? "",
                row.ObjectName ?? "",
                row.Address ?? "",
                row.ReasonName ?? "",
                row.Description ?? "",
                row.OriginalDoc ?? "",
                row.StoreName ?? "",
                row.BankAccountName ?? "",
                row.Note ?? ""
            };
            for (int i = 0; i < fields.Length; i++)
            {
                if (CatalogRepository.Fold(fields[i]).IndexOf(q, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        public static bool CanChangeDate(UserSession session)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return session.Permissions != null
                && session.Permissions.Can(VoucherConstants.FunctionDateChange, RoleModes.View);
        }

        public static bool CanVoucher(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            return session.Permissions.Can(VoucherConstants.FunctionListVoucher, bit)
                || session.Permissions.Can(VoucherConstants.FunctionThuChi, bit);
        }

        public static bool CanReasonCatalog(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            return session.Permissions != null
                && session.Permissions.Can(VoucherConstants.FunctionReasonCatalog, bit);
        }

        public static bool CanBankAccountCatalog(UserSession session, int bit)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            // Một số DB không tách riêng SFUNCTION cho tài khoản ngân hàng: cho phép theo tên hoặc fallback quyền Quỹ.
            if (session.Permissions.Can(VoucherConstants.FunctionBankAccountCatalog, bit))
                return true;
            if (session.Permissions.ModeByName(VoucherConstants.FunctionBankAccountCatalog) == RoleModes.Locked
                && session.Permissions.Can(VoucherConstants.FunctionListVoucher, bit))
                return true;
            return false;
        }
    }
}
