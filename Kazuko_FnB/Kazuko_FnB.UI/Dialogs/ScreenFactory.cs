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
                // Phiếu thu / Phiếu chi dựng từ layout gốc (docs/specs/phieu-thu-chi.md).
                { "preview.phieuthu", session => new PhieuThuDialog(session) },
                { "preview.phieuchi", session => new PhieuThuDialog(session, VoucherKind.Expense) },
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
                { "preview.salesmanage", session => new SalesManagePreviewForm(session, BillManageMode.Manage) },
                { "preview.billadjust", session => new SalesManagePreviewForm(session, BillManageMode.Adjust) },
                { "preview.purchase", session => new PurchasePreviewForm(session) },
                { "preview.purchaseedit", session => PurchaseEditDialog.CreateForPreview(session) },
                { "preview.stockexport", session => new StockDocumentPreviewForm(session, StockDocumentKind.Export) },
                { "preview.stockexportedit", session => StockDocumentEditDialog.CreateForPreview(session, StockDocumentKind.Export) },
                { "preview.stocktransfer", session => new StockDocumentPreviewForm(session, StockDocumentKind.Transfer) },
                { "preview.stocktransferedit", session => StockDocumentEditDialog.CreateForPreview(session, StockDocumentKind.Transfer) },
                { "preview.stocktake", session => new StockTakePreviewForm(session) },
                { "preview.stocktakeedit", session => StockTakeEditDialog.CreateForPreview(session) },
                { "preview.inventory", session => new InventoryPreviewForm(session) },
                { "preview.debtcustomer", session => new DebtPreviewForm(session, DebtPartyKind.Customer) },
                { "preview.debtsupplier", session => new DebtPreviewForm(session, DebtPartyKind.Supplier) },
                { "preview.loyalty", session => new LoyaltyPreviewForm(session) },
                { "preview.openingstock", session => new OpeningStockPreviewForm(session) },
                { "preview.openingdebtcustomer", session => new OpeningDebtPreviewForm(session, DebtPartyKind.Customer) },
                { "preview.openingdebtsupplier", session => new OpeningDebtPreviewForm(session, DebtPartyKind.Supplier) },
                { "preview.materialregen", session => new MaterialRegenPreviewForm(session) },
                { "preview.promotionedit", session => new PromotionEditDialog(session, null, false) },
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
            // Màn "Quản lý bán hàng" và "Điều chỉnh hóa đơn" (cùng form, khác chế độ).
            if (string.Equals(node.Id, SalesManageView.ManageMenuId, StringComparison.Ordinal))
                return new SalesManageView(session, BillManageMode.Manage);
            if (string.Equals(node.Id, SalesManageView.AdjustMenuId, StringComparison.Ordinal))
                return new SalesManageView(session, BillManageMode.Adjust);
            // Màn "Danh mục phiếu thu" / "Danh mục phiếu chi" (docs/specs/phieu-thu-chi.md §1).
            if (string.Equals(node.FormId, VoucherConstants.IncomeListFormId, StringComparison.OrdinalIgnoreCase))
                return new VoucherListView(session, VoucherKind.Income);
            if (string.Equals(node.FormId, VoucherConstants.ExpenseListFormId, StringComparison.OrdinalIgnoreCase))
                return new VoucherListView(session, VoucherKind.Expense);
            // Màn "Quản lý nhập kho" (KHO HÀNG > Nhập hàng vào kho, TDONHANG LOAI = 1, docs/specs/phieu-nhap-kho.md).
            if (IsPurchaseListMenu(node, session))
                return new PurchaseListView(session);
            // Màn "Quản lý xuất kho" (LOAI = 2) và "Quản lý chuyển kho" (LOAI = 3, docs/specs/phieu-xuat-chuyen-kho.md).
            if (IsStockExportListMenu(node, session))
                return new StockDocumentListView(session, StockDocumentKind.Export);
            if (IsStockTransferListMenu(node, session))
                return new StockDocumentListView(session, StockDocumentKind.Transfer);
            // Màn "Quản lý kiểm kê" (KHO HÀNG > Kiểm kê kho, TDONHANG LOAI = 4, docs/specs/kiem-ke-kho.md).
            if (IsStockTakeListMenu(node, session))
                return new StockTakeListView(session);
            // Màn "Tồn kho" (KHO HÀNG > Tồn kho, chỉ đọc, docs/specs/ton-kho.md).
            if (IsInventoryMenu(node))
                return new InventoryView(session);
            // Màn "Tính lại giá vốn" (KHO HÀNG > Tính lại giá vốn, docs/specs/tinh-lai-gia-von.md).
            if (IsCostRecalcMenu(node))
                return new CostRecalcView(session);
            // Màn "Xuất lại định lượng" (KHO HÀNG > Xuất lại định lượng, docs/specs/xuat-lai-dinh-luong.md).
            if (IsMaterialRegenMenu(node))
                return new MaterialRegenView(session);
            // Màn "Công nợ khách hàng" & "Công nợ nhà cung cấp" (CÔNG NỢ, docs/specs/cong-no.md).
            if (IsCustomerDebtMenu(node))
                return new DebtListView(session, DebtPartyKind.Customer);
            if (IsSupplierDebtMenu(node))
                return new DebtListView(session, DebtPartyKind.Supplier);
            // Màn "Khách hàng thân thiết" (KHÁCH HÀNG > Khách hàng thân thiết, docs/specs/khach-hang-than-thiet.md).
            if (IsLoyaltyMenu(node))
                return new LoyaltyView(session);
            // Màn "Tồn kho ban đầu" (QUẢN TRỊ > Dữ liệu ban đầu > Tồn kho ban đầu, docs/specs/so-du-dau-ky.md P1).
            if (IsOpeningStockMenu(node))
                return new OpeningStockView(session);
            // Màn "Công nợ khách hàng ban đầu" & "Công nợ nhà cung cấp ban đầu" (QUẢN TRỊ > Dữ liệu ban đầu, docs/specs/so-du-dau-ky.md P2).
            if (IsOpeningCustomerDebtMenu(node))
                return new OpeningDebtView(session, DebtPartyKind.Customer);
            if (IsOpeningSupplierDebtMenu(node))
                return new OpeningDebtView(session, DebtPartyKind.Supplier);
            // Màn "Danh mục đợt khuyến mại" (KHÁCH HÀNG > Danh mục đợt khuyến mại, DDOTKHUYENMAI).
            if (IsPromotionMenu(node, session))
            {
                string stableId = !string.IsNullOrEmpty(node.StableDescId)
                    ? node.StableDescId
                    : new Data.CatalogRepository(session.DatabasePath).FindIdByTable(PromotionText.TableName);
                if (!string.IsNullOrEmpty(stableId))
                    return new CatalogView(session.DatabasePath, stableId,
                        string.IsNullOrWhiteSpace(node.Title) ? PromotionText.FunctionName : node.Title);
            }
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
                    if (string.Equals(table, "TTHUCHI", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isExpense = node.Loai == 1
                            || ((node.Title ?? "").IndexOf("chi", StringComparison.OrdinalIgnoreCase) >= 0
                                && (node.Title ?? "").IndexOf("thu", StringComparison.OrdinalIgnoreCase) < 0);
                        return new VoucherListView(session, isExpense ? VoucherKind.Expense : VoucherKind.Income);
                    }
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

        /// <summary>Mục "KHO HÀNG > Nhập hàng vào kho": mở lưới Quản lý nhập kho (TDONHANG LOAI = 1).</summary>
        internal static bool IsPurchaseListMenu(MenuNode node, UserSession session)
        {
            if (node == null || session == null)
                return false;
            string title = (node.Title ?? "").Trim();
            if (string.Equals(title, "Nhập hàng vào kho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Quản lý nhập kho", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (node.Operation == 0 && node.Loai == PurchaseConstants.OrderType && !string.IsNullOrEmpty(node.StableDescId))
            {
                try
                {
                    return string.Equals(
                        new Data.FormRepository(session.DatabasePath).TableNameOf(node.StableDescId),
                        "TDONHANG",
                        StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>Mục "KHO HÀNG > Xuất khác": mở lưới Quản lý xuất kho (TDONHANG LOAI = 2).</summary>
        internal static bool IsStockExportListMenu(MenuNode node, UserSession session)
        {
            if (node == null || session == null)
                return false;
            string title = (node.Title ?? "").Trim();
            if (string.Equals(title, "Xuất khác", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Xuất kho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Quản lý xuất kho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Xuất hàng khác", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return IsStockOrderListMenuByType(node, session, StockDocumentConstants.ExportOrderType);
        }

        /// <summary>Mục "KHO HÀNG > Chuyển kho": mở lưới Quản lý chuyển kho (TDONHANG LOAI = 3).</summary>
        internal static bool IsStockTransferListMenu(MenuNode node, UserSession session)
        {
            if (node == null || session == null)
                return false;
            string title = (node.Title ?? "").Trim();
            if (string.Equals(title, "Chuyển kho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Quản lý chuyển kho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Chuyển kho nội bộ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return IsStockOrderListMenuByType(node, session, StockDocumentConstants.TransferOrderType);
        }

        /// <summary>Mục "KHO HÀNG > Kiểm kê kho": mở lưới Quản lý kiểm kê (TDONHANG LOAI = 4).</summary>
        internal static bool IsStockTakeListMenu(MenuNode node, UserSession session)
        {
            if (node == null || session == null)
                return false;
            string title = (node.Title ?? "").Trim();
            if (string.Equals(title, "Kiểm kê kho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Quản lý kiểm kê", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Phiếu kiểm kê", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return IsStockOrderListMenuByType(node, session, StockTakeConstants.OrderType);
        }

        /// <summary>Mục "KHO HÀNG > Tồn kho": mở màn xem tồn theo kho (chỉ đọc).</summary>
        internal static bool IsInventoryMenu(MenuNode node)
        {
            if (node == null)
                return false;
            string title = (node.Title ?? "").Trim();
            return string.Equals(title, "Tồn kho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Xem tồn kho", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mục "KHO HÀNG > Tính lại giá vốn": mở màn tính lại giá vốn.</summary>
        internal static bool IsCostRecalcMenu(MenuNode node)
        {
            if (node == null)
                return false;
            return string.Equals((node.Title ?? "").Trim(), CostRecalcConstants.FunctionName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mục "KHO HÀNG > Xuất lại định lượng": mở màn xuất lại định lượng.</summary>
        internal static bool IsMaterialRegenMenu(MenuNode node)
        {
            if (node == null)
                return false;
            string title = (node.Title ?? "").Trim();
            if (string.Equals(title, MaterialRegenConstants.FunctionName, StringComparison.OrdinalIgnoreCase))
                return true;
            string folded = Data.CatalogRepository.Fold(title);
            return string.Equals(folded, "xuat lai dinh luong", StringComparison.Ordinal);
        }

        /// <summary>Mục "CÔNG NỢ > Công nợ khách hàng": mở màn công nợ khách hàng.</summary>
        internal static bool IsCustomerDebtMenu(MenuNode node)
        {
            if (node == null)
                return false;
            string title = (node.Title ?? "").Trim();
            return string.Equals(title, DebtConstants.CustomerDebtFunctionName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Công nợ khách", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mục "CÔNG NỢ > Công nợ nhà cung cấp": mở màn công nợ nhà cung cấp.</summary>
        internal static bool IsSupplierDebtMenu(MenuNode node)
        {
            if (node == null)
                return false;
            string title = (node.Title ?? "").Trim();
            return string.Equals(title, DebtConstants.SupplierDebtFunctionName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, "Công nợ NCC", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mục "KHÁCH HÀNG > Khách hàng thân thiết": mở màn khách hàng thân thiết.</summary>
        internal static bool IsLoyaltyMenu(MenuNode node)
        {
            if (node == null)
                return false;
            string title = (node.Title ?? "").Trim();
            return string.Equals(title, LoyaltyConstants.FunctionName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mục "Tồn kho ban đầu" (nhận diện theo tên không dấu, không phân biệt hoa thường).</summary>
        internal static bool IsOpeningStockMenu(MenuNode node)
        {
            if (node == null)
                return false;
            string folded = Data.CatalogRepository.Fold((node.Title ?? "").Trim());
            return string.Equals(folded, "ton kho ban dau", StringComparison.Ordinal);
        }

        /// <summary>Mục "Công nợ khách hàng ban đầu" (kể cả tên gốc thiếu dấu "ban đâu").</summary>
        internal static bool IsOpeningCustomerDebtMenu(MenuNode node)
        {
            if (node == null)
                return false;
            string folded = Data.CatalogRepository.Fold((node.Title ?? "").Trim());
            return string.Equals(folded, "cong no khach hang ban dau", StringComparison.Ordinal);
        }

        /// <summary>Mục "Công nợ nhà cung cấp ban đầu" (nhận diện theo tên không dấu, không phân biệt hoa thường).</summary>
        internal static bool IsOpeningSupplierDebtMenu(MenuNode node)
        {
            if (node == null)
                return false;
            string folded = Data.CatalogRepository.Fold((node.Title ?? "").Trim());
            return string.Equals(folded, "cong no nha cung cap ban dau", StringComparison.Ordinal);
        }

        /// <summary>Mục "KHÁCH HÀNG > Danh mục đợt khuyến mại": mở CatalogView chế độ đợt khuyến mại (DDOTKHUYENMAI).</summary>
        internal static bool IsPromotionMenu(MenuNode node, UserSession session)
        {
            if (node == null || session == null)
                return false;
            string title = (node.Title ?? "").Trim();
            if (string.Equals(title, PromotionText.FunctionName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(node.StableDescId))
            {
                try
                {
                    return string.Equals(
                        new Data.FormRepository(session.DatabasePath).TableNameOf(node.StableDescId),
                        PromotionText.TableName,
                        StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        private static bool IsStockOrderListMenuByType(MenuNode node, UserSession session, int orderType)
        {
            if (node.Operation == 0 && node.Loai == orderType && !string.IsNullOrEmpty(node.StableDescId))
            {
                try
                {
                    return string.Equals(
                        new Data.FormRepository(session.DatabasePath).TableNameOf(node.StableDescId),
                        "TDONHANG",
                        StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>Mục "Tạo phiếu thu": thao tác 1 trên bảng TTHUCHI với LOAI = 0.</summary>
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

        /// <summary>Mục "Tạo phiếu chi": thao tác 1 trên bảng TTHUCHI với LOAI = 1.</summary>
        internal static bool IsNewExpenseVoucher(MenuNode node, UserSession session)
        {
            if (node == null || session == null || node.Operation != 1 || node.Loai != 1 || string.IsNullOrEmpty(node.StableDescId))
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
                    using (var dlg = new PhieuThuDialog(session, VoucherKind.Income))
                        dlg.ShowDialog(owner);
                }
                catch (Exception ex)
                {
                    Msg.Error(owner, "Không mở được phiếu thu: " + ex.Message);
                }
                return true;
            }
            if (IsNewExpenseVoucher(node, session))
            {
                try
                {
                    using (var dlg = new PhieuThuDialog(session, VoucherKind.Expense))
                        dlg.ShowDialog(owner);
                }
                catch (Exception ex)
                {
                    Msg.Error(owner, "Không mở được phiếu chi: " + ex.Message);
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
