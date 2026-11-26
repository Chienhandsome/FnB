using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Services;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Bảng tra: id mục menu (MenuNode.Id) → hộp thoại chức năng thật.
    /// Mục chưa đăng ký vẫn mở tab tạm "sẽ được bổ sung". Mỗi chức năng đăng ký một dòng ở đây.
    /// </summary>
    internal static class ScreenFactory
    {
        private static readonly Dictionary<string, Func<UserSession, Form>> Map =
            new Dictionary<string, Func<UserSession, Form>>(StringComparer.Ordinal)
            {
                { "admin.config", session => new ConfigDialog(session) },
                { "admin.images", session => new ImageLibraryDialog(session) },
                { "admin.users", session => new UserManagerDialog(session) },
                { "admin.themes", session => new ThemeChooserDialog(session) },
                // Bản thử "Phiếu thu" dựng từ layout gốc (docs/specs/pilot-phieu-thu.md); cũng mở được từ menu Quỹ ▸ Tạo phiếu thu.
                { "preview.phieuthu", session => new PhieuThuDialog(session) },
                { "sys.password", session => new ChangePasswordDialog(session) },
                // Chỉ để chụp thử bằng --render-screen (không có mục menu):
                { "preview.booking", session => new BookingPreviewForm(session) },
                { "preview.quote", session => new QuotePreviewForm(session) },
                { "preview.quoteedit", session => QuoteEditDialog.CreateForPreview(session) },
                { "preview.fund", session => new FundPreviewForm(session) },
                { "preview.activitylog", session => new ActivityLogPreviewForm(session) },
                { "preview.cancelledbill", session => new CancelledBillPreviewForm(session) },
                { "preview.itemsales", session => new ItemSalesPreviewForm(session) },
                { "preview.itemsalesdetail", session => ItemSalesDetailDialog.CreateForPreview(session) },
                { "preview.printtemplate", session => Printing.PrintTemplateDialog.CreateForPreview(session) },
                { "preview.revenue", session => new RevenueReportPreviewForm(session) },
                { "preview.dailyactivity", session => new DailyActivityPreviewForm(session) },
                { "preview.businessresult", session => new BusinessResultPreviewForm(session) },
                { "preview.service", session => new ServicePreviewForm(session) },
                { "preview.useredit", session => new UserEditDialog(session, null, null, null) },
                { "preview.groupedit", session => new GroupEditDialog(session, null) }
            };

        public static bool Has(string id)
        {
            return id != null && Map.ContainsKey(id);
        }

        /// <summary>
        /// Màn nội dung (mở trong tab) cho mục menu dựa vào bảng nhập liệu: danh mục phẳng hoặc cây nhóm + lưới.
        /// Trả null nếu mục này chưa có màn (khi đó mở tab tạm "sẽ được bổ sung").
        /// </summary>
        public static Control CreateView(MenuNode node, UserSession session)
        {
            if (node == null || session == null || string.IsNullOrEmpty(session.DatabasePath))
                return null;
            // Màn lịch "Theo dõi đặt phòng": form tự viết, không gắn bảng chuẩn
            // (STABLEDESCID = NULL, OPERATION = 4) nên không đi qua CatalogView.
            if (string.Equals(node.Id, BookingCalendarView.MenuId, StringComparison.Ordinal))
                return new BookingCalendarView(session.DatabasePath);
            // Màn "Tồn quỹ": form tự viết (OPERATION = 3, không có STABLEDESCID).
            if (string.Equals(node.Id, FundBalanceView.MenuId, StringComparison.Ordinal))
                return new FundBalanceView(session);
            // Màn "Lưu vết hoạt động": form tự viết (OPERATION = 4, không có STABLEDESCID).
            if (string.Equals(node.Id, ActivityLogView.MenuId, StringComparison.Ordinal))
                return new ActivityLogView(session);
            // Màn "Danh mục hóa đơn hủy": form tự viết chỉ xem (OPERATION = 4).
            if (string.Equals(node.Id, CancelledBillView.MenuId, StringComparison.Ordinal))
                return new CancelledBillView(session);
            // Màn "Thống kê mặt hàng bán": form tự viết chỉ xem (OPERATION = 4).
            if (string.Equals(node.Id, ItemSalesView.MenuId, StringComparison.Ordinal))
                return new ItemSalesView(session);
            // Màn "Thống kê doanh thu": form tự viết chỉ xem (OPERATION = 4).
            if (string.Equals(node.Id, RevenueReportView.MenuId, StringComparison.Ordinal))
                return new RevenueReportView(session);
            // Màn "Chi tiết hoạt động ngày": báo cáo chỉ xem (OPERATION = 5).
            if (string.Equals(node.Id, DailyActivityView.MenuId, StringComparison.Ordinal))
                return new DailyActivityView(session);
            // Màn "Tổng hợp kết quả kinh doanh": báo cáo chỉ xem (OPERATION = 5).
            if (string.Equals(node.Id, BusinessResultView.MenuId, StringComparison.Ordinal))
                return new BusinessResultView(session);
            // Màn "Sử dụng dịch vụ" (OPERATION = 4, không có STABLEDESCID).
            if (string.Equals(node.Id, ServiceOrderView.MenuId, StringComparison.Ordinal))
                return new ServiceOrderView(session);
            if (string.IsNullOrEmpty(node.StableDescId) || node.Operation != 0)
                return null;
            try
            {
                if (!new Data.CatalogRepository(session.DatabasePath).IsSupported(node.StableDescId))
                {
                    // Màn chứng từ TDATHANG (TABLETYPE = 3) không mở được qua CatalogView: màn riêng.
                    string table = new Data.FormRepository(session.DatabasePath).TableNameOf(node.StableDescId);
                    if (string.Equals(table, "TDATHANG", StringComparison.OrdinalIgnoreCase))
                        return new BookingListView(session.DatabasePath);
                    if (string.Equals(table, "TBAOGIA", StringComparison.OrdinalIgnoreCase))
                        return new QuoteListView(session);
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
            return new CatalogView(session.DatabasePath, node.StableDescId, node.Title);
        }

        /// <summary>Tạo form cho id (null nếu chưa có). Dùng cho --render-screen; "catalog:TÊNBẢNG" dựng khung chụp màn danh mục.</summary>
        public static Form Create(string id, UserSession session)
        {
            if (id != null && id.StartsWith("catalog:", StringComparison.Ordinal))
                return new CatalogPreviewForm(session, id.Substring(8));
            Func<UserSession, Form> f;
            return id != null && Map.TryGetValue(id, out f) ? f(session) : null;
        }

        /// <summary>Mục "Tạo phiếu thu": thao tác 1 trên bảng TTHUCHI với LOAI = 0 (LOAI = 1 là phiếu chi, chưa làm).</summary>
        internal static bool IsNewIncomeVoucher(MenuNode node, UserSession session)
        {
            if (node == null || session == null || node.Operation != 1 || node.Loai != 0 || string.IsNullOrEmpty(node.StableDescId))
                return false;
            try
            {
                return new Data.FormRepository(session.DatabasePath).TableNameOf(node.StableDescId) == "TTHUCHI";
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Mở hộp thoại (modal) nếu mục này đã có màn thật; trả về true nếu đã xử lý.</summary>
        public static bool TryOpen(IWin32Window owner, MenuNode node, UserSession session)
        {
            if (IsNewIncomeVoucher(node, session))
            {
                try
                {
                    using (var dlg = new PhieuThuDialog(session))
                        dlg.ShowDialog(owner);
                }
                catch (Exception ex)
                {
                    Msg.Error(owner, "Không mở được phiếu thu: " + ex.Message);
                }
                return true;
            }
            using (Form form = Create(node.Id, session))
            {
                if (form == null)
                    return false;
                form.ShowDialog(owner);
                return true;
            }
        }
    }
}
