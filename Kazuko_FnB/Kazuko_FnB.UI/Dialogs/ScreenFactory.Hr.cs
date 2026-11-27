using System;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Nhận diện và mở màn cho hai danh mục nhóm Nhân sự (docs/specs/nhan-su-danh-muc.md):
    /// NHÂN SỰ > Danh mục nhân viên (DNHANVIEN, SORTORDER ZZZY) và Danh mục ca làm việc (DCALAMVIEC, ZZZZ).
    /// </summary>
    internal static partial class ScreenFactory
    {
        private static Control TryCreateHrCatalogView(MenuNode node, UserSession session)
        {
            if (IsEmployeeCatalogMenu(node, session))
            {
                string stableId = !string.IsNullOrEmpty(node.StableDescId)
                    ? node.StableDescId
                    : new CatalogRepository(session.DatabasePath).FindIdByTable(EmployeeText.TableName);
                if (!string.IsNullOrEmpty(stableId))
                    return new CatalogView(session.DatabasePath, stableId,
                        string.IsNullOrWhiteSpace(node.Title) ? EmployeeText.FunctionName : node.Title);
            }
            if (IsShiftCatalogMenu(node, session))
            {
                string stableId = !string.IsNullOrEmpty(node.StableDescId)
                    ? node.StableDescId
                    : new CatalogRepository(session.DatabasePath).FindIdByTable(ShiftText.TableName);
                if (!string.IsNullOrEmpty(stableId))
                    return new CatalogView(session.DatabasePath, stableId,
                        string.IsNullOrWhiteSpace(node.Title) ? ShiftText.FunctionName : node.Title);
            }
            if (IsRewardPenaltyMenu(node, session))
            {
                string stableId = !string.IsNullOrEmpty(node.StableDescId)
                    ? node.StableDescId
                    : new CatalogRepository(session.DatabasePath).FindIdByTable(RewardPenaltyText.TableName);
                if (!string.IsNullOrEmpty(stableId))
                    return new CatalogView(session.DatabasePath, stableId,
                        string.IsNullOrWhiteSpace(node.Title) ? RewardPenaltyText.FunctionName : node.Title);
            }
            if (IsSalaryAdvanceMenu(node))
                return new SalaryAdvanceListView(session);
            return null;
        }

        /// <summary>Mục "NHÂN SỰ > Danh mục nhân viên": nhận diện theo tên không dấu không phân biệt hoa thường hoặc bảng DNHANVIEN.</summary>
        internal static bool IsEmployeeCatalogMenu(MenuNode node, UserSession session)
        {
            if (node == null) return false;
            if (string.Equals(CatalogRepository.Fold((node.Title ?? "").Trim()), "danh muc nhan vien", StringComparison.Ordinal))
                return true;
            if (session == null || string.IsNullOrEmpty(node.StableDescId)) return false;
            try
            {
                return string.Equals(
                    new FormRepository(session.DatabasePath).TableNameOf(node.StableDescId),
                    EmployeeText.TableName,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        /// <summary>Mục "NHÂN SỰ > Danh mục ca làm việc": nhận diện theo tên không dấu không phân biệt hoa thường hoặc bảng DCALAMVIEC.</summary>
        internal static bool IsShiftCatalogMenu(MenuNode node, UserSession session)
        {
            if (node == null) return false;
            if (string.Equals(CatalogRepository.Fold((node.Title ?? "").Trim()), "danh muc ca lam viec", StringComparison.Ordinal))
                return true;
            if (session == null || string.IsNullOrEmpty(node.StableDescId)) return false;
            try
            {
                return string.Equals(
                    new FormRepository(session.DatabasePath).TableNameOf(node.StableDescId),
                    ShiftText.TableName,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        /// <summary>Mục "NHÂN SỰ > Thưởng phạt": nhận diện theo tên không dấu không phân biệt hoa thường hoặc bảng TTHUONGPHAT.</summary>
        internal static bool IsRewardPenaltyMenu(MenuNode node, UserSession session)
        {
            if (node == null) return false;
            string folded = CatalogRepository.Fold((node.Title ?? "").Trim());
            if (string.Equals(folded, "thuong phat", StringComparison.Ordinal)
                || string.Equals(folded, "quan ly thuong phat", StringComparison.Ordinal))
                return true;
            if (session == null || string.IsNullOrEmpty(node.StableDescId)) return false;
            try
            {
                return string.Equals(
                    new FormRepository(session.DatabasePath).TableNameOf(node.StableDescId),
                    RewardPenaltyText.TableName,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        /// <summary>Mục "NHÂN SỰ > Tạm ứng lương": nhận diện theo tên không dấu không phân biệt hoa thường hoặc SFUNCTIONID.</summary>
        internal static bool IsSalaryAdvanceMenu(MenuNode node)
        {
            if (node == null) return false;
            if (string.Equals(CatalogRepository.Fold((node.Title ?? "").Trim()), "tam ung luong", StringComparison.Ordinal))
                return true;
            return !string.IsNullOrEmpty(node.FunctionId)
                && string.Equals(node.FunctionId, SalaryAdvanceText.FunctionId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
