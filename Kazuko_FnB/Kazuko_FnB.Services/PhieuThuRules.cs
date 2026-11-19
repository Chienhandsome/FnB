using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật của form "Phiếu thu" (đọc từ SFORM.CODE của app gốc), viết thuần để kiểm thử được.
    /// Xem docs/specs/pilot-phieu-thu.md mục 3. Không đụng DB, không đụng giao diện.
    /// </summary>
    public static class PhieuThuRules
    {
        /// <summary>Các mục của ô "Loại đối tượng", theo đúng thứ tự trong layout gốc.</summary>
        public const int OtherObject = 0;
        public const int Employee = 1;
        public const int Customer = 2;
        public const int Supplier = 3;

        /// <summary>cboLOAIDOITUONG_SelectedIndexChanged: mục nào bật ô nào (chỉ số khác 0–3 thì tắt hết).</summary>
        public static ObjectFieldState ForObjectType(int selectedIndex)
        {
            return new ObjectFieldState
            {
                Customer = selectedIndex == Customer,
                Employee = selectedIndex == Employee,
                Name = selectedIndex == OtherObject,
                Supplier = selectedIndex == Supplier,
                Address = selectedIndex == OtherObject
            };
        }

        /// <summary>Chọn nhân viên: tên = tên nhân viên, địa chỉ trống.</summary>
        public static void FillFromEmployee(string employeeName, out string name, out string address)
        {
            name = employeeName ?? "";
            address = "";
        }

        /// <summary>Chọn khách hàng/nhà cung cấp: tên và địa chỉ của họ (bỏ chọn thì cả hai trống).</summary>
        public static void FillFromParty(string partyName, string partyAddress, out string name, out string address)
        {
            name = partyName ?? "";
            address = partyAddress ?? "";
        }

        /// <summary>Tích "Chuyển vào tài khoản" thì ô tài khoản ngân hàng được bật.</summary>
        public static bool BankAccountEnabled(bool transfer)
        {
            return transfer;
        }

        public const string BankAccountMissing = "Mời bạn chọn tài khoản ngân hàng";

        /// <summary>
        /// Kiểm tra trước khi lưu. Trả null nếu hợp lệ, ngược lại là thông báo.
        /// Luật tài khoản ngân hàng lấy nguyên văn từ mapper_BeforeSave của form gốc; câu báo thiếu trường bắt buộc
        /// do bản viết lại tự đặt (thông báo của framework gốc chưa biết nguyên văn).
        /// </summary>
        public static string Validate(IList<string> missingRequiredLabels, bool transfer, bool hasBankAccount)
        {
            if (missingRequiredLabels != null && missingRequiredLabels.Count > 0)
                return "Mời bạn nhập: " + string.Join(", ", new List<string>(missingRequiredLabels).ToArray());
            if (transfer && !hasBankAccount)
                return BankAccountMissing;
            return null;
        }
    }
}
