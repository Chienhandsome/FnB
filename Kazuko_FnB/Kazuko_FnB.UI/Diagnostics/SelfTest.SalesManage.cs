using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void SalesManageTests(string db)
        {
            SalesManagePureTests();
            SalesManageRepoAndUiTests(db);
        }

        private static void SalesManagePureTests()
        {
            // 1. OrderModePolicy.Evaluate — Quản lý bán hàng (OrderMode.Manage)
            OrderModeCapabilities capManage = OrderModePolicy.Evaluate(
                OrderMode.Manage,
                hasBill: true, hasSelectedLine: true, isGroupBill: false,
                canViewPrice: true, canEditManage: true,
                canEditUnitPrice: true, canEditLineDiscount: true,
                canEditBillDiscount: true, canEditServiceFee: true, canEditTax: true,
                canChangeCustomerInAdjust: false, canChangeDateConfig: true, enableCheckItemsInManage: true);

            Check("QLBH §1.1/§7 OrderModePolicy(Manage, đủ quyền): mở sửa dòng, tổng tiền, khách, ngày, kiểm đồ, cập nhật, hủy, in lại",
                !capManage.ShowGroupBillWarning && capManage.RightPaneEnabled && capManage.DateRangeEditable
                && capManage.ShowCancelBillButton && capManage.CancelBillEnabled
                && capManage.ShowMenuPicker && capManage.ShowLineEditButtons
                && capManage.ShowReturnButton && capManage.ReturnButtonEnabled
                && capManage.ShowReprintButton && capManage.ReprintButtonEnabled
                && capManage.ShowUpdateButton && capManage.UpdateButtonEnabled
                && capManage.HeaderCustomerEnabled && capManage.HeaderDateEditable
                && capManage.TotalsDiscountEnabled && capManage.CanAddLine && capManage.CanModifySelectedLine);

            // 2. OrderModePolicy.Evaluate — Điều chỉnh hóa đơn (OrderMode.Adjust)
            OrderModeCapabilities capAdjustWithCust = OrderModePolicy.Evaluate(
                OrderMode.Adjust,
                hasBill: true, hasSelectedLine: true, isGroupBill: false,
                canViewPrice: true, canEditManage: false,
                canEditUnitPrice: true, canEditLineDiscount: true,
                canEditBillDiscount: true, canEditServiceFee: true, canEditTax: true,
                canChangeCustomerInAdjust: true, canChangeDateConfig: true, enableCheckItemsInManage: false);
            OrderModeCapabilities capAdjustNoCust = OrderModePolicy.Evaluate(
                OrderMode.Adjust,
                hasBill: true, hasSelectedLine: true, isGroupBill: false,
                canViewPrice: false, canEditManage: false,
                canEditUnitPrice: false, canEditLineDiscount: false,
                canEditBillDiscount: false, canEditServiceFee: false, canEditTax: false,
                canChangeCustomerInAdjust: false, canChangeDateConfig: true, enableCheckItemsInManage: false);

            Check("QLBH §1.1/§7 OrderModePolicy(Adjust): khóa ngày/hủy/in lại/thêm-bớt/tổng tiền, mở Trả đồ & Cập nhật, mở Khách hàng đúng quyền ThayDoiKhachHangTrongDieuChinhHoaDon",
                !capAdjustWithCust.DateRangeEditable && !capAdjustWithCust.ShowCancelBillButton
                && !capAdjustWithCust.ShowReprintButton && !capAdjustWithCust.ShowMenuPicker && !capAdjustWithCust.ShowLineEditButtons
                && !capAdjustWithCust.HeaderDateEditable && !capAdjustWithCust.TotalsDiscountEnabled
                && capAdjustWithCust.ShowReturnButton && capAdjustWithCust.ReturnButtonEnabled && capAdjustWithCust.ReturnButtonText == "Trả đồ"
                && capAdjustWithCust.UpdateButtonEnabled && capAdjustWithCust.HeaderCustomerEnabled
                && !capAdjustNoCust.HeaderCustomerEnabled && !capAdjustNoCust.ShowUpdateButton);

            // 3. OrderModePolicy.Evaluate — Bill thanh toán chung (NHOMGUID != "")
            OrderModeCapabilities capGroupManage = OrderModePolicy.Evaluate(
                OrderMode.Manage,
                hasBill: true, hasSelectedLine: true, isGroupBill: true,
                canViewPrice: true, canEditManage: true,
                canEditUnitPrice: true, canEditLineDiscount: true,
                canEditBillDiscount: true, canEditServiceFee: true, canEditTax: true,
                canChangeCustomerInAdjust: true, canChangeDateConfig: true, enableCheckItemsInManage: true);
            OrderModeCapabilities capGroupAdjust = OrderModePolicy.Evaluate(
                OrderMode.Adjust,
                hasBill: true, hasSelectedLine: true, isGroupBill: true,
                canViewPrice: true, canEditManage: true,
                canEditUnitPrice: true, canEditLineDiscount: true,
                canEditBillDiscount: true, canEditServiceFee: true, canEditTax: true,
                canChangeCustomerInAdjust: true, canChangeDateConfig: true, enableCheckItemsInManage: true);

            Check("QLBH §6.3/§7 OrderModePolicy(Bill thanh toán chung): chỉ xem, khóa sửa/trả đồ/cập nhật/hủy, vẫn cho In lại ở Manage và ẩn ở Adjust",
                capGroupManage.ShowGroupBillWarning && !capGroupManage.CanAddLine && !capGroupManage.ReturnButtonEnabled
                && !capGroupManage.UpdateButtonEnabled && !capGroupManage.CancelBillEnabled
                && capGroupManage.ShowReprintButton && capGroupManage.ReprintButtonEnabled
                && capGroupAdjust.ShowGroupBillWarning && !capGroupAdjust.ShowReprintButton);

            // 4. ReturnItemRules — gộp nhóm, loại KM/XUATVATTU/giờ/combo con, cân bằng SLTRALAI+SLSUDUNG=SLORDER, vượt SL về 0, dòng âm giữ giá & %CK
            var lines = new List<ServiceOrderLine>
            {
                new ServiceOrderLine { Id = "l1", ProductId = "p1", ItemName = "Bia Tiger", UnitId = "u1", UnitName = "Lon", UnitPrice = 25000m, DiscountRate = 10m, Quantity = 3m, ProductTypeId = "0" },
                new ServiceOrderLine { Id = "l2", ProductId = "p1", ItemName = "Bia Tiger", UnitId = "u1", UnitName = "Lon", UnitPrice = 25000m, DiscountRate = 10m, Quantity = 2m, ProductTypeId = "0" },
                new ServiceOrderLine { Id = "l3", ProductId = "KM", ItemName = "Khuyen mai", UnitId = "u1", UnitName = "Lon", UnitPrice = 25000m, DiscountRate = 0m, Quantity = 1m, ProductTypeId = "0" },
                new ServiceOrderLine { Id = "l4", ProductId = "p2", ItemName = "Nguyen lieu", UnitId = "u1", UnitName = "Kg", UnitPrice = 0m, DiscountRate = 0m, Quantity = 4m, XuatVatTu = 30, ProductTypeId = "0" },
                new ServiceOrderLine { Id = "l5", ProductId = "p3", ItemName = "Gio hat", UnitId = "u1", UnitName = "Gio", UnitPrice = 100000m, DiscountRate = 0m, Quantity = 1m, ProductTypeId = ProductRules.TypeHourlyService },
                new ServiceOrderLine { Id = "l6", ProductId = "p4", ItemName = "Mon con combo", UnitId = "u1", UnitName = "Phan", UnitPrice = 0m, DiscountRate = 0m, Quantity = 2m, ComboParentId = "parent-1", ProductTypeId = "0" },
                new ServiceOrderLine { Id = "l7", ProductId = "p5", ItemName = "Nuoc suoi", UnitId = "u1", UnitName = "Chai", UnitPrice = 10000m, DiscountRate = 0m, Quantity = 2m, ProductTypeId = "0" },
                new ServiceOrderLine { Id = "l8", ProductId = "p5", ItemName = "Nuoc suoi", UnitId = "u1", UnitName = "Chai", UnitPrice = 10000m, DiscountRate = 0m, Quantity = -2m, ProductTypeId = "0" }
            };

            List<ReturnableItemRow> groups = ReturnItemRules.BuildReturnableGroups(lines);
            bool groupOk = groups.Count == 1
                && groups[0].ProductId == "p1"
                && groups[0].OrderQty == 5m
                && groups[0].UsedQty == 5m
                && groups[0].ReturnQty == 0m;

            ReturnItemRules.ApplyReturnQty(groups[0], 2m);
            bool afterRet2 = groups[0].ReturnQty == 2m && groups[0].UsedQty == 3m;

            ReturnItemRules.ApplyUsedQty(groups[0], 4m);
            bool afterUsed4 = groups[0].UsedQty == 4m && groups[0].ReturnQty == 1m;

            ReturnItemRules.ApplyReturnQty(groups[0], 9m);
            bool afterOverflow = groups[0].ReturnQty == 0m && groups[0].UsedQty == 5m;

            ReturnItemRules.ApplyReturnQty(groups[0], 2m);
            ServiceOrderLine retLine = ReturnItemRules.CreateReturnLine("bill-1", groups[0], 2m, "ret-1", "cb-1");
            bool retLineOk = retLine.Quantity == -2m
                && retLine.UnitPrice == 25000m
                && retLine.DiscountRate == 10m
                && retLine.Amount == -45000m
                && !retLine.IsMaterialExport && !retLine.IsComboChild;

            Check("QLBH §2.3/§7 ReturnItemRules: gộp nhóm loại KM/XUATVATTU/giờ/combo con, cân bằng SLTRALAI+SLSUDUNG=SLORDER, vượt SL về 0, dòng âm giữ giá & %CK",
                groupOk && afterRet2 && afterUsed4 && afterOverflow && retLineOk,
                "grp=" + groups.Count + ", ret2=" + afterRet2 + ", used4=" + afterUsed4 + ", ovf=" + afterOverflow + ", amt=" + retLine.Amount);

            // 5. OrderModePolicy.ApplyCustomerGroupDiscount & RecalcBillTotalsInPlace (Q4)
            var bill = new ServiceOrderHeader
            {
                Id = "b-q4",
                Lines = new List<ServiceOrderLine>
                {
                    new ServiceOrderLine { Id = "a", ProductId = "p1", DrinkTypeId = "1", ItemName = "Cafe", Quantity = 2m, UnitPrice = 50000m, DiscountRate = 0m },
                    new ServiceOrderLine { Id = "b", ProductId = "KM", DrinkTypeId = "1", ItemName = "Tang kem", Quantity = 1m, UnitPrice = 20000m }
                }
            };
            OrderModePolicy.ApplyCustomerGroupDiscount(bill, new CustomerGroupRow
            {
                Id = "vip",
                Name = "VIP",
                DiscountDrink = 10m,
                DiscountGoods = 5m
            }, false, 0m);
            // Cafe 2 * 50.000 * 0.9 = 90.000 + KM 20.000 = 110.000; bill CK 5% = 5.500 -> TongCong = 104.500.
            bool vipApplied = bill.Lines[0].DiscountRate == 10m && bill.Lines[1].DiscountRate == 0m
                && bill.TienHang == 110000m && bill.TileGiamGia == 5m && bill.TienGiamGia == 5500m && bill.TongCong == 104500m;

            OrderModePolicy.ApplyCustomerGroupDiscount(bill, null, false, 0m);
            bool clearedCustomer = bill.Lines[0].DiscountRate == 0m && bill.TienHang == 120000m
                && bill.TileGiamGia == 0m && bill.TienGiamGia == 0m && bill.TongCong == 120000m;

            Check("QLBH §6.3 Q4/§7 Đổi khách hàng áp giảm giá nhóm khách lên dòng (trừ KM) & bill; xóa khách đưa giảm giá về 0",
                vipApplied && clearedCustomer,
                "vip=" + vipApplied + ", clear=" + clearedCustomer + ", total=" + bill.TongCong);

            // 6. MatchesBillFilter & cảnh báo §6.4
            var item = new PaidBillListItem { Name = "HD-001", TableName = "VIP 02", CustomerName = "Nguyễn Văn A", Note = "Tiệc sinh nhật" };
            bool filterOk = OrderModePolicy.MatchesBillFilter(item, "vip 02")
                && OrderModePolicy.MatchesBillFilter(item, "nguyễn")
                && OrderModePolicy.MatchesBillFilter(item, "sinh nhật")
                && !OrderModePolicy.MatchesBillFilter(item, "không khớp");
            string warn64 = SalesManageConstants.AlreadyRefundedDepositWarning(100000m, 150000m);
            Check("QLBH §1/§6.4 Bộ lọc chuỗi tìm số phiếu/bàn/khách/ghi chú và định dạng cảnh báo đã chi đặt trước",
                filterOk && warn64.Contains("đã chi trước đó") && warn64.Contains("100.000") && warn64.Contains("150.000"),
                "filter=" + filterOk + ", warnOk=" + warn64.Contains("100.000"));
        }
    }
}
