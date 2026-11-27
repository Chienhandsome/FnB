using System;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Nhận diện và mở màn cho hai mục Quản trị nhỏ (P-A, docs/specs/quan-tri-nho.md §2, §3):
    /// Thông báo sửa chữa (TSUACHUA, SMENU ZZZ009) và Danh mục mật khẩu wifi (DMATKHAUWIFI, ZZZ007).
    /// </summary>
    internal static partial class ScreenFactory
    {
        private static Control TryCreateSmallAdminCatalogView(MenuNode node, UserSession session)
        {
            if (IsRepairNoticeMenu(node, session))
            {
                string stableId = !string.IsNullOrEmpty(node.StableDescId)
                    ? node.StableDescId
                    : new CatalogRepository(session.DatabasePath).FindIdByTable(RepairNoticeText.TableName);
                if (!string.IsNullOrEmpty(stableId))
                    return new CatalogView(session.DatabasePath, stableId,
                        string.IsNullOrWhiteSpace(node.Title) ? RepairNoticeText.FunctionName : node.Title);
            }
            if (IsWifiPasswordMenu(node, session))
            {
                string stableId = !string.IsNullOrEmpty(node.StableDescId)
                    ? node.StableDescId
                    : new CatalogRepository(session.DatabasePath).FindIdByTable(WifiPasswordText.TableName);
                if (!string.IsNullOrEmpty(stableId))
                    return new CatalogView(session.DatabasePath, stableId,
                        string.IsNullOrWhiteSpace(node.Title) ? WifiPasswordText.FunctionName : node.Title);
            }
            if (IsAllWarehousesInventoryMenu(node))
                return new InventoryView(session, true);
            if (IsOrderControlMenu(node))
                return new OrderControlView(session);
            return null;
        }

        private static Form TryCreateSmallAdminPreview(string id, UserSession session)
        {
            if (string.Equals(id, "preview.ordercontrol", StringComparison.Ordinal))
                return new OrderControlPreviewForm(session);
            return null;
        }

        /// <summary>Mục "HOẠT ĐỘNG > Kiểm soát order": nhận diện theo tên không dấu không phân biệt hoa thường hoặc SFUNCTIONID.</summary>
        internal static bool IsOrderControlMenu(MenuNode node)
        {
            if (node == null)
                return false;
            if (string.Equals(CatalogRepository.Fold((node.Title ?? "").Trim()), "kiem soat order", StringComparison.Ordinal))
                return true;
            return !string.IsNullOrEmpty(node.FunctionId)
                && string.Equals(node.FunctionId, OrderControlConstants.FunctionId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mục "KHO HÀNG > Tồn nhiều kho" (SMENU ZZZ012): mở màn Tồn kho ở chế độ tất cả các kho.</summary>
        internal static bool IsAllWarehousesInventoryMenu(MenuNode node)
        {
            if (node == null)
                return false;
            return string.Equals(CatalogRepository.Fold((node.Title ?? "").Trim()), "ton nhieu kho", StringComparison.Ordinal);
        }

        /// <summary>Mục "Thông báo sửa chữa": nhận diện theo tên không dấu không phân biệt hoa thường hoặc bảng TSUACHUA.</summary>
        internal static bool IsRepairNoticeMenu(MenuNode node, UserSession session)
        {
            if (node == null)
                return false;
            if (string.Equals(CatalogRepository.Fold((node.Title ?? "").Trim()), "thong bao sua chua", StringComparison.Ordinal))
                return true;
            if (session == null || string.IsNullOrEmpty(node.StableDescId))
                return false;
            try
            {
                return string.Equals(
                    new FormRepository(session.DatabasePath).TableNameOf(node.StableDescId),
                    RepairNoticeText.TableName,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Mục "Danh mục mật khẩu wifi": nhận diện theo tên không dấu không phân biệt hoa thường hoặc bảng DMATKHAUWIFI.</summary>
        internal static bool IsWifiPasswordMenu(MenuNode node, UserSession session)
        {
            if (node == null)
                return false;
            if (string.Equals(CatalogRepository.Fold((node.Title ?? "").Trim()), "danh muc mat khau wifi", StringComparison.Ordinal))
                return true;
            if (session == null || string.IsNullOrEmpty(node.StableDescId))
                return false;
            try
            {
                return string.Equals(
                    new FormRepository(session.DatabasePath).TableNameOf(node.StableDescId),
                    WifiPasswordText.TableName,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
