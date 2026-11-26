using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Tập luật thuần quyết định ẩn/hiện và bật/khóa thành phần UI theo chế độ hóa đơn
    /// (docs/specs/quan-ly-ban-hang-dieu-chinh-hoa-don.md §1.1, §6.3, §8), kèm lọc lưới trái và áp giảm giá nhóm khách (Q4).
    /// Không đụng DB, không đụng UI.
    /// </summary>
    public static class OrderModePolicy
    {
        public static OrderMode FromManageMode(BillManageMode mode)
        {
            return mode == BillManageMode.Adjust ? OrderMode.Adjust : OrderMode.Manage;
        }

        public static string FunctionNameOf(BillManageMode mode)
        {
            return mode == BillManageMode.Adjust
                ? ActivityConstants.FuncDieuChinhHoaDon
                : ActivityConstants.FuncQuanLyBanHang;
        }

        /// <summary>
        /// Đánh giá đầy đủ cờ giao diện theo bảng §1.1 và quyết định §6.3 (bill nhóm chặn sửa/trả đồ/hủy, vẫn xem &amp; in lại).
        /// </summary>
        public static OrderModeCapabilities Evaluate(
            OrderMode mode,
            bool hasBill,
            bool hasSelectedLine,
            bool isGroupBill,
            bool canViewPrice,
            bool canEditManage,
            bool canEditUnitPrice,
            bool canEditLineDiscount,
            bool canEditBillDiscount,
            bool canEditServiceFee,
            bool canEditTax,
            bool canChangeCustomerInAdjust,
            bool canChangeDateConfig,
            bool enableCheckItemsInManage)
        {
            bool isManage = mode == OrderMode.Manage;
            bool isAdjust = mode == OrderMode.Adjust;
            bool editableBill = hasBill && !isGroupBill;
            bool manageCanMutate = isManage && editableBill;
            bool adjustCanMutate = isAdjust && editableBill;

            var caps = new OrderModeCapabilities
            {
                Mode = mode,
                RightPaneEnabled = hasBill,
                DateRangeEditable = !isAdjust,
                ShowCancelBillButton = isManage,
                CancelBillEnabled = manageCanMutate,
                ShowMoveMergeStatsButtons = mode == OrderMode.Sale,
                ShowMenuPicker = !isAdjust,
                ShowLineEditButtons = !isAdjust,
                ShowReturnButton = isAdjust || (isManage && enableCheckItemsInManage),
                ReturnButtonText = isAdjust ? "Trả đồ" : "Kiểm đồ",
                ReturnButtonEnabled = (isAdjust && adjustCanMutate) || (isManage && enableCheckItemsInManage && manageCanMutate),
                // §1.1: In lại bill luôn hiện ở Quản lý bán hàng (kể cả bill nhóm theo §6.3); ẩn ở Điều chỉnh hóa đơn.
                ShowReprintButton = isManage,
                ReprintButtonEnabled = isManage && hasBill,
                ShowUpdateButton = canViewPrice,
                UpdateButtonEnabled = canViewPrice && (manageCanMutate || adjustCanMutate),
                // §1.1: Đầu form: Manage bật; Adjust chỉ ô Khách hàng bật và chỉ khi có quyền ThayDoiKhachHangTrongDieuChinhHoaDon.
                HeaderCustomerEnabled = manageCanMutate || (adjustCanMutate && canChangeCustomerInAdjust),
                HeaderGuestsEnabled = manageCanMutate,
                HeaderNoteEnabled = manageCanMutate,
                HeaderDateEditable = manageCanMutate && canChangeDateConfig,
                // §1.1: Khối tổng cộng: Manage bật theo quyền; Adjust vô hiệu hóa hoàn toàn.
                TotalsDiscountEnabled = manageCanMutate && canViewPrice && canEditBillDiscount,
                TotalsFeeEnabled = manageCanMutate && canViewPrice && canEditServiceFee,
                TotalsTaxEnabled = manageCanMutate && canViewPrice && canEditTax,
                // Nút sửa dòng: chỉ Manage.
                CanAddLine = manageCanMutate,
                CanModifySelectedLine = manageCanMutate && hasSelectedLine,
                CanEditUnitPrice = manageCanMutate && hasSelectedLine && canViewPrice && canEditUnitPrice,
                CanEditLineDiscount = manageCanMutate && hasSelectedLine && canViewPrice && canEditLineDiscount,
                CanApplyGroupDiscount = manageCanMutate && canViewPrice && canEditLineDiscount,
                ShowGroupBillWarning = hasBill && isGroupBill
            };

            // Khi user không có quyền sửa Quản lý bán hàng vẫn cho xem bill; chặn khi bấm Cập nhật hoặc tắt nút nếu cần.
            // Theo §2.2: "Bấm Thanh toán -> Chế độ Quản lý bán hàng: cần quyền sửa QuanLyBanHang (CanEdit), không có thì dừng".
            // Do đó giữ UpdateButtonEnabled để khi bấm sẽ kiểm tra và báo rõ, trừ bill nhóm (bị chặn hẳn).
            if (isManage && !canEditManage)
            {
                // Vẫn giữ UpdateButtonEnabled theo §2.2 để thông báo thiếu quyền khi bấm; cờ CanEdit kiểm trong BillAdjustService.
            }

            return caps;
        }

        /// <summary>
        /// Áp lại giảm giá theo nhóm khách hàng lên bill trong bộ nhớ (Q4, §6.3, nha-hang.md §5).
        /// Chạy ở cả hai chế độ Quản lý bán hàng và Điều chỉnh hóa đơn; bỏ chọn khách (group == null) đặt giảm theo nhóm về 0.
        /// Không nâng hay hạ nhóm khách.
        /// </summary>
        public static void ApplyCustomerGroupDiscount(
            ServiceOrderHeader bill,
            CustomerGroupRow group,
            bool gopChungGiamGia,
            decimal roundStep)
        {
            if (bill == null)
                return;

            decimal foodRate = group != null ? ClampRate(group.DiscountFood) : 0m;
            decimal drinkRate = group != null ? ClampRate(group.DiscountDrink) : 0m;
            decimal serviceRate = group != null ? ClampRate(group.DiscountService) : 0m;
            decimal otherRate = group != null ? ClampRate(group.DiscountOther) : 0m;

            if (bill.Lines != null)
            {
                foreach (ServiceOrderLine line in bill.Lines)
                {
                    if (line == null || line.IsComboChild || line.IsMaterialExport)
                        continue;
                    if (string.Equals(line.ProductId, "KM", StringComparison.OrdinalIgnoreCase))
                        continue;

                    decimal lineRate;
                    switch (line.DrinkTypeId ?? "1")
                    {
                        case "0": lineRate = foodRate; break;
                        case "1": lineRate = drinkRate; break;
                        case "2": lineRate = serviceRate; break;
                        case "3": lineRate = otherRate; break;
                        default: lineRate = 0m; break;
                    }

                    line.DiscountByMoney = false;
                    line.DiscountRate = lineRate;
                    OrderCalculator.RecalcLine(line);
                }
            }

            decimal billRate = 0m;
            if (group != null)
            {
                if (gopChungGiamGia)
                    billRate = ClampRate(group.DiscountTotal);
                else
                    billRate = group.DiscountGoods > 0m ? ClampRate(group.DiscountGoods) : ClampRate(group.DiscountTotal);
            }

            bill.GiamTheoTien = false;
            bill.TileGiamGia = billRate;
            bill.TienGiamGia = 0m;

            RecalcBillTotalsInPlace(bill, roundStep);
        }

        /// <summary>Tính lại toàn bộ các dòng và tổng đầu phiếu của bill trong bộ nhớ bằng OrderCalculator.</summary>
        public static void RecalcBillTotalsInPlace(ServiceOrderHeader bill, decimal roundStep)
        {
            if (bill == null)
                return;
            if (bill.Lines != null)
            {
                foreach (ServiceOrderLine line in bill.Lines)
                {
                    if (line != null && !line.IsComboChild && !line.IsMaterialExport)
                        OrderCalculator.RecalcLine(line);
                }
            }

            ServiceOrderTotals totals = OrderCalculator.CalcTotals(
                bill.Lines,
                bill.TileGiamGia,
                bill.TienGiamGia,
                bill.GiamTheoTien,
                bill.TilePhiDichVu,
                bill.PhiDichVu,
                bill.PhiDichVuTheoTien,
                bill.TileThue,
                bill.BillKhac,
                roundStep);

            bill.TienHang = totals.TienHang;
            bill.TienHangChuaGiam = totals.TienHangChuaGiam;
            bill.GiamGiaMatHang = totals.GiamGiaMatHang;
            bill.TileGiamGia = totals.TileGiamGia;
            bill.TienGiamGia = totals.TienGiamGia;
            bill.TilePhiDichVu = totals.TilePhiDichVu;
            bill.PhiDichVu = totals.PhiDichVu;
            bill.TienThue = totals.TienThue;
            bill.TongCong = totals.TongCong;
            bill.TienPhaiTra = totals.TienPhaiTra;
        }

        /// <summary>Lọc nhanh danh sách bill trái theo chuỗi gõ ở ô Lọc (txtLoc), bỏ dấu/hoa thường.</summary>
        public static bool MatchesBillFilter(PaidBillListItem item, string query)
        {
            if (item == null)
                return false;
            string q = CatalogRepository.Fold(query ?? "").Trim();
            if (q.Length == 0)
                return true;

            string[] fields =
            {
                item.Name ?? "",
                item.CustomerName ?? "",
                item.CustomerCode ?? "",
                item.TableName ?? "",
                item.OrderNumbers ?? "",
                item.Note ?? "",
                item.UserThanhToanName ?? ""
            };
            for (int i = 0; i < fields.Length; i++)
            {
                if (CatalogRepository.Fold(fields[i]).IndexOf(q, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static decimal ClampRate(decimal value)
        {
            if (value < 0m) return 0m;
            if (value > 100m) return 100m;
            return value;
        }
    }
}
