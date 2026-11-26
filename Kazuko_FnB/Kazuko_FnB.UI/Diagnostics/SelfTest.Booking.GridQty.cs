using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void BookingGridQtyTests(string db)
        {
            BookingGridQtyRuleTests();
            BookingGridQtyUiTests(db);
        }

        private static void BookingGridQtyRuleTests()
        {
            var item1 = new BookingSaleItem
            {
                Id = "DH_GQ_P1",
                Code = "GQ1",
                Name = "Cà phê sữa đá",
                UnitId = "U_LY",
                UnitName = "Ly",
                SellPrice = 15000m,
                SellPrice2 = 14000m
            };

            // 1. Gộp 1 ĐVT: 2 rồi 3 ra 1 dòng 5; dòng cũ sửa đơn giá tay và % giảm khác 0 giữ nguyên hai giá trị đó.
            var lines = new List<BookingLine>();
            BookingAddItemOutcome o1 = BookingItemRules.AddSingleUnitItem(lines, item1, 0, 2m);
            BookingAddItemOutcome o2 = BookingItemRules.AddSingleUnitItem(lines, item1, 0, 3m);
            Check("Đặt hàng đợt 2 [thuần gộp 1 ĐVT]: thêm 2 rồi 3 ra 1 dòng SL 5, giá 15000, thành tiền 75000",
                o1.Changed && o1.WarningMessage == null && o2.Changed && o2.WarningMessage == null
                && lines.Count == 1 && lines[0].Quantity == 5m && lines[0].Price == 15000m
                && lines[0].DiscountPct == 0m && lines[0].Amount == 75000m,
                "cnt=" + lines.Count + ", qty=" + (lines.Count > 0 ? lines[0].Quantity.ToString() : "-"));

            lines[0].Price = 18000m;
            lines[0].DiscountPct = 10m;
            lines[0].Amount = BookingRules.CalcLineAmount(5m, 18000m, 10m);
            BookingAddItemOutcome o3 = BookingItemRules.AddSingleUnitItem(lines, item1, 1, 2m);
            Check("Đặt hàng đợt 2 [thuần gộp 1 ĐVT giữ giá & % giảm]: dòng cũ giá 18000 giảm 10% + thêm 2 -> SL 7 giữ giá 18000, giảm 10%, tiền 113400",
                o3.Changed && o3.WarningMessage == null && lines.Count == 1
                && lines[0].Quantity == 7m && lines[0].Price == 18000m && lines[0].DiscountPct == 10m
                && lines[0].Amount == 113400m,
                "qty=" + lines[0].Quantity + ", p=" + lines[0].Price + ", d=" + lines[0].DiscountPct + ", a=" + lines[0].Amount);

            BookingAddItemOutcome oZero = BookingItemRules.AddSingleUnitItem(lines, item1, 0, 0m);
            BookingAddItemOutcome oNeg = BookingItemRules.AddSingleUnitItem(lines, item1, 0, -2m);
            Check("Đặt hàng đợt 2 [thuần 1 ĐVT SL <= 0]: SL 0 và âm đều cảnh báo EnterQuantity, không đổi dòng",
                !oZero.Changed && oZero.WarningMessage == BookingText.EnterQuantity
                && !oNeg.Changed && oNeg.WarningMessage == BookingText.EnterQuantity
                && lines.Count == 1 && lines[0].Quantity == 7m);

            // 2. Hàm thuần NumberInput: chấm/phẩy đều là dấu thập phân; 2 dấu trở lên, chữ, trống, null -> không đọc được.
            decimal p1600, pDot, pComma, p16, pPct, pNeg, pBad;
            bool parseValidOk = NumberInput.TryParse("1600", out p1600) && p1600 == 1600m
                && NumberInput.TryParse("2.5", out pDot) && pDot == 2.5m
                && NumberInput.TryParse("2,5", out pComma) && pComma == 2.5m
                && NumberInput.TryParse("1.600", out p16) && p16 == 1.6m
                && NumberInput.TryParse("7.5", out pPct) && pPct == 7.5m
                && NumberInput.TryParse("-1", out pNeg) && pNeg == -1m;
            bool parseInvalidOk = !NumberInput.TryParse("abc", out pBad)
                && !NumberInput.TryParse("", out pBad)
                && !NumberInput.TryParse("   ", out pBad)
                && !NumberInput.TryParse((object)null, out pBad)
                && !NumberInput.TryParse(DBNull.Value, out pBad)
                && !NumberInput.TryParse("1.500.000", out pBad)
                && !NumberInput.TryParse("1,500.5", out pBad)
                && !NumberInput.TryParse("1.500,5", out pBad);
            Check("Đặt hàng đợt 2 [thuần NumberInput.TryParse]: 1600->1600, 2.5/2,5->2,5, 1.600->1,6, 7.5->7,5, -1->-1; chữ/trống/null/2 dấu từ chối",
                parseValidOk && parseInvalidOk,
                "valid=" + parseValidOk + ", invalid=" + parseInvalidOk);

            // 3. Hàm thuần đổi số sang chữ để sửa: không có dấu ngăn nghìn, giữ đủ phần thập phân (kể cả scale .00 / .50 từ DB).
            string e1500 = NumberInput.FormatForEdit(1500m);
            string e1500Db = NumberInput.FormatForEdit(1500.00m);
            string e25 = NumberInput.FormatForEdit(2.5m);
            string ePriceFrac = NumberInput.FormatForEdit(15000.5m);
            string ePriceFracDb = NumberInput.FormatForEdit(15000.50m);
            Check("Đặt hàng đợt 2 [thuần NumberInput.FormatForEdit]: 1500->1500, 2,5->2,5, 15000,5->15000,5 (không dấu ngăn nghìn, giữ đủ phần lẻ)",
                e1500 == "1500" && e1500Db == "1500" && e25 == "2,5" && ePriceFrac == "15000,5" && ePriceFracDb == "15000,5",
                "1500=" + e1500 + ", 2,5=" + e25 + ", 15000,5=" + ePriceFrac);

            // 4. EvaluateEditedQuantity: chặn 0, âm, chữ, trống, null, DBNull, 2 dấu; không đổi (kể cả 0) không báo; dương nhận đúng.
            BookingGridQtyEditResult r0 = BookingItemRules.EvaluateEditedQuantity("0", 2m);
            BookingGridQtyEditResult rNeg = BookingItemRules.EvaluateEditedQuantity("-1", 2m);
            BookingGridQtyEditResult rText = BookingItemRules.EvaluateEditedQuantity("abc", 2m);
            BookingGridQtyEditResult rEmpty = BookingItemRules.EvaluateEditedQuantity("", 2m);
            BookingGridQtyEditResult rNull = BookingItemRules.EvaluateEditedQuantity(null, 2m);
            BookingGridQtyEditResult rDbNull = BookingItemRules.EvaluateEditedQuantity(DBNull.Value, 2m);
            BookingGridQtyEditResult rTwoDot = BookingItemRules.EvaluateEditedQuantity("1.500.000", 2m);
            BookingGridQtyEditResult rMixedSep = BookingItemRules.EvaluateEditedQuantity("1,500.5", 2m);
            Check("Đặt hàng đợt 2 [thuần sửa SL lưới chặn lỗi]: 0, -1, chữ, trống, null, DBNull, 2 dấu trả SL cũ 2 và báo EnterQuantity",
                !r0.Accepted && !r0.Changed && r0.Quantity == 2m && r0.WarningMessage == BookingText.EnterQuantity
                && !rNeg.Accepted && !rNeg.Changed && rNeg.Quantity == 2m && rNeg.WarningMessage == BookingText.EnterQuantity
                && !rText.Accepted && !rText.Changed && rText.Quantity == 2m && rText.WarningMessage == BookingText.EnterQuantity
                && !rEmpty.Accepted && !rEmpty.Changed && rEmpty.Quantity == 2m && rEmpty.WarningMessage == BookingText.EnterQuantity
                && !rNull.Accepted && !rNull.Changed && rNull.Quantity == 2m && rNull.WarningMessage == BookingText.EnterQuantity
                && !rDbNull.Accepted && !rDbNull.Changed && rDbNull.Quantity == 2m && rDbNull.WarningMessage == BookingText.EnterQuantity
                && !rTwoDot.Accepted && !rTwoDot.Changed && rTwoDot.Quantity == 2m && rTwoDot.WarningMessage == BookingText.EnterQuantity
                && !rMixedSep.Accepted && !rMixedSep.Changed && rMixedSep.Quantity == 2m && rMixedSep.WarningMessage == BookingText.EnterQuantity);

            BookingGridQtyEditResult rSame2 = BookingItemRules.EvaluateEditedQuantity(2m, 2m);
            BookingGridQtyEditResult rSame0 = BookingItemRules.EvaluateEditedQuantity(0m, 0m);
            BookingGridQtyEditResult rZeroToNeg = BookingItemRules.EvaluateEditedQuantity("-1", 0m);
            BookingGridQtyEditResult rZeroToEmpty = BookingItemRules.EvaluateEditedQuantity("", 0m);
            Check("Đặt hàng đợt 2 [thuần sửa SL lưới không đổi]: giữ nguyên (kể cả dòng cũ SL 0) không báo; dòng SL 0 gõ âm/trống vẫn chặn",
                rSame2.Accepted && !rSame2.Changed && rSame2.Quantity == 2m && rSame2.WarningMessage == null
                && rSame0.Accepted && !rSame0.Changed && rSame0.Quantity == 0m && rSame0.WarningMessage == null
                && !rZeroToNeg.Accepted && rZeroToNeg.Quantity == 0m && rZeroToNeg.WarningMessage == BookingText.EnterQuantity
                && !rZeroToEmpty.Accepted && rZeroToEmpty.Quantity == 0m && rZeroToEmpty.WarningMessage == BookingText.EnterQuantity);

            BookingGridQtyEditResult r1600 = BookingItemRules.EvaluateEditedQuantity("1600", 2m);
            BookingGridQtyEditResult rDot = BookingItemRules.EvaluateEditedQuantity("2.5", 2m);
            BookingGridQtyEditResult rComma = BookingItemRules.EvaluateEditedQuantity("2,5", 2m);
            BookingGridQtyEditResult r16 = BookingItemRules.EvaluateEditedQuantity("1.600", 2m);
            Check("Đặt hàng đợt 2 [thuần sửa SL lưới hợp lệ]: 1600->1600, 2.5->2,5, 2,5->2,5, 1.600->1,6",
                r1600.Accepted && r1600.Changed && r1600.Quantity == 1600m && r1600.WarningMessage == null
                && rDot.Accepted && rDot.Changed && rDot.Quantity == 2.5m && rDot.WarningMessage == null
                && rComma.Accepted && rComma.Changed && rComma.Quantity == 2.5m && rComma.WarningMessage == null
                && r16.Accepted && r16.Changed && r16.Quantity == 1.6m && r16.WarningMessage == null,
                "1600=" + r1600.Quantity + ", 2.5=" + rDot.Quantity + ", 2,5=" + rComma.Quantity + ", 1.600=" + r16.Quantity);
        }

        private static void BookingGridQtyUiTests(string db)
        {
            var configRepo = new ConfigRepository(db);
            var bookingRepo = new BookingRepository(db);
            UserSession savedSession = AppSession.Current;
            bool oldTwoUnitCfg = false;
            bool oldMultiRowCfg = false;
            bool configChanged = false;

            string areaId = DbHelper.NewId();
            string roomId = DbHelper.NewId();
            string groupId = DbHelper.NewId();
            string unitOddId = DbHelper.NewId();
            string unitEvenId = DbHelper.NewId();
            string prod1Id = DbHelper.NewId();
            string prod2Id = DbHelper.NewId();
            string savedBookingId = null;

            try
            {
                var adminSession = new UserSession
                {
                    UserId = "selftest",
                    UserName = "selftest",
                    DisplayName = "Quản trị",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };
                AppSession.Current = adminSession;
                SystemConfig.Reload(db);
                oldTwoUnitCfg = SystemConfig.SuDung2DonViTinh;
                oldMultiRowCfg = SystemConfig.NhapMotMatHangNhieuLanTrongPhieu;

                BookingExec(db,
                    "INSERT INTO \"DKHUVUC\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", areaId, "@n", "KV THỬ ĐH ĐỢT 2");
                BookingExec(db,
                    "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DKHUVUCID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @a, 30, 'selftest')",
                    "@id", roomId, "@n", "Bàn thử ĐH2", "@a", areaId);
                BookingExec(db,
                    "INSERT INTO \"DNHOMMATHANG\" (\"ID\", \"NAME\", \"CODE\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @c, 30, 'selftest')",
                    "@id", groupId, "@n", "NHÓM THỬ ĐH ĐỢT 2", "@c", "DHGQGRP");
                BookingExec(db,
                    "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", unitOddId, "@n", "LyGQ");
                BookingExec(db,
                    "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", unitEvenId, "@n", "ThùngGQ");
                BookingExec(db,
                    "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"GIABAN\", \"STATUS\", \"USERCREATEDID\") " +
                    "VALUES (@id, @c, @n, @g, @u, @p, 30, 'selftest')",
                    "@id", prod1Id, "@c", "GQ1UNIT", "@n", "Mặt hàng GQ 1 ĐVT", "@g", groupId, "@u", unitOddId, "@p", 15000m);
                BookingExec(db,
                    "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"DDONVITINHCHANID\", \"QUYDOI\", \"GIABAN\", \"GIABANCHAN\", \"STATUS\", \"USERCREATEDID\") " +
                    "VALUES (@id, @c, @n, @g, @u1, @u2, 24, @p1, @p2, 30, 'selftest')",
                    "@id", prod2Id, "@c", "GQ2UNIT", "@n", "Mặt hàng GQ 2 ĐVT", "@g", groupId,
                    "@u1", unitOddId, "@u2", unitEvenId, "@p1", 15000m, "@p2", 340000m);

                // Tạo phiếu mẫu đã lưu: dòng 1 SL 2 giá 15000; dòng 2 (dữ liệu cũ) SL 0 giá 15000,5 (có phần lẻ).
                DateTime now = DateTime.Now;
                var row = BookingRow.NewDefault();
                row.Name = "DH99/98765"; row.Ngay = DateTime.Today; row.GioDat = now;
                row.TuGio = now; row.DenGio = now.AddHours(2); row.TuNgay = now.Date; row.DenNgay = now.AddHours(2).Date;
                row.TenKhach = "Khách thử lưới SL"; row.TienHang = 30000m; row.TongCong = 30000m;
                var seedLines = new List<BookingLine>
                {
                    new BookingLine { ProductId = prod1Id, UnitId = unitOddId, Quantity = 2m, Price = 15000m, DiscountPct = 0m, Amount = 30000m },
                    new BookingLine { ProductId = prod2Id, UnitId = unitOddId, Quantity = 0m, Price = 15000.5m, DiscountPct = 0m, Amount = 0m }
                };
                BookingSaveResult save = bookingRepo.SaveBooking(row, seedLines, new List<string> { roomId }, "selftest");
                Check("Đặt hàng đợt 2 UI [dữ liệu mẫu]: lưu được phiếu gồm dòng SL 2 giá 15000 và dòng cũ SL 0 giá 15000,5",
                    save.Ok && !string.IsNullOrEmpty(save.SavedId), save.ErrorMessage);
                if (save.Ok) savedBookingId = save.SavedId;

                if (savedBookingId != null)
                {
                    using (var dlg = OpenOffscreenBookingDialog(adminSession, savedBookingId))
                    {
                        try
                        {
                            DataGridView grid = dlg.DetailGridForTest;
                            int rowQty2 = FindDetailRowByName(grid, "Mặt hàng GQ 1 ĐVT");
                            int rowQty0 = FindDetailRowByName(grid, "Mặt hàng GQ 2 ĐVT");
                            bool cleanStart = !dlg.DirtyForTest && !dlg.SaveButtonForTest.Enabled
                                && dlg.DetailDataErrorCountForTest == 0
                                && grid.Rows.Count == 2 && rowQty2 >= 0 && rowQty0 >= 0 && rowQty2 != rowQty0
                                && dlg.TongCongBoxForTest.Value == 30000m;

                            // 1. Chặn SL 0, -1, chữ, trống, 2 dấu; vào rồi ra không đổi (SL 2 & SL 0) không báo, không DataError, không dirty.
                            string initText;
                            bool blockedOk = rowQty2 >= 0;
                            foreach (string bad in new[] { "0", "-1", "abc", "", "1.500.000", "1,500.5" })
                            {
                                blockedOk = blockedOk && EditDetailCellReal(grid, rowQty2, "d_qty", bad, out initText)
                                    && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_qty"].Value) == 2m
                                    && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_amount"].Value) == 30000m
                                    && dlg.TongCongBoxForTest.Value == 30000m
                                    && dlg.LastWarningForTest == BookingText.EnterQuantity
                                    && !dlg.DirtyForTest && !dlg.SaveButtonForTest.Enabled;
                            }
                            bool noChange2Ok = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_qty", null, out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_qty"].Value) == 2m
                                && dlg.LastWarningForTest == null && !dlg.DirtyForTest;
                            bool noChange0Ok = rowQty0 >= 0 && EditDetailCellReal(grid, rowQty0, "d_qty", null, out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty0].Cells["d_qty"].Value) == 0m
                                && dlg.LastWarningForTest == null && !dlg.DirtyForTest;

                            Check("Đặt hàng đợt 2 UI [lưới thật chặn SL 0/-1/chữ/trống/2 dấu & không đổi]: trả 2, tổng 30000, không dirty, 0 DataError",
                                cleanStart && blockedOk && noChange2Ok && noChange0Ok && dlg.DetailDataErrorCountForTest == 0,
                                "clean=" + cleanStart + ",blk=" + blockedOk + ",nc2=" + noChange2Ok + ",nc0=" + noChange0Ok
                                + ",de=" + dlg.DetailDataErrorCountForTest);

                            // 2. Vào sửa đơn giá 15000 và đơn giá có phần lẻ 15000,5: EditingControl.Text không có dấu chấm ngăn nghìn, thoát không đổi giữ nguyên phần lẻ.
                            string priceIntEditText = null, priceFracEditText = null;
                            bool priceIntEditOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_price", null, out priceIntEditText)
                                && priceIntEditText == "15000" && !dlg.DirtyForTest;
                            bool priceFracEditOk = rowQty0 >= 0 && EditDetailCellReal(grid, rowQty0, "d_price", null, out priceFracEditText)
                                && priceFracEditText == "15000,5"
                                && Convert.ToDecimal(grid.Rows[rowQty0].Cells["d_price"].Value) == 15000.5m
                                && !dlg.DirtyForTest && dlg.LastWarningForTest == null;

                            Check("Đặt hàng đợt 2 UI [EditingControl khi vào sửa]: đơn giá 15000->'15000', 15000,5->'15000,5' (không dấu chấm nghìn, thoát giữ nguyên phần lẻ)",
                                priceIntEditOk && priceFracEditOk && dlg.DetailDataErrorCountForTest == 0,
                                "p15000=" + priceIntEditText + ", p15000_5=" + priceFracEditText
                                + ", val=" + (rowQty0 >= 0 ? Convert.ToString(grid.Rows[rowQty0].Cells["d_price"].Value) : "-"));

                            // 3. Nhập 1600 -> giá trị 1600, FormattedValue '1.600', vào sửa lại EditingControl.Text '1600'; gõ 2.5 & 2,5 -> 2,5; gõ 7.5 ở % -> 7,5.
                            string qty1600EditText = null, qty25EditText = null;
                            bool q1600Ok = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_qty", "1600", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_qty"].Value) == 1600m
                                && Convert.ToString(grid.Rows[rowQty2].Cells["d_qty"].FormattedValue) == "1.600"
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_amount"].Value) == 24000000m
                                && dlg.TongCongBoxForTest.Value == 24000000m
                                && dlg.LastWarningForTest == null && dlg.DirtyForTest && dlg.SaveButtonForTest.Enabled;
                            bool q1600ReEditOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_qty", null, out qty1600EditText)
                                && qty1600EditText == "1600";

                            bool dotOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_qty", "2.5", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_qty"].Value) == 2.5m
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_amount"].Value) == 37500m
                                && dlg.TongCongBoxForTest.Value == 37500m
                                && dlg.LastWarningForTest == null;
                            bool dotReEditOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_qty", null, out qty25EditText)
                                && qty25EditText == "2,5";

                            bool commaOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_qty", "1.600", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_qty"].Value) == 1.6m
                                && EditDetailCellReal(grid, rowQty2, "d_qty", "2,5", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_qty"].Value) == 2.5m
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_amount"].Value) == 37500m
                                && dlg.TongCongBoxForTest.Value == 37500m
                                && dlg.LastWarningForTest == null;

                            bool discDotOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_qty", "3", out initText)
                                && EditDetailCellReal(grid, rowQty2, "d_disc", "7.5", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_disc"].Value) == 7.5m
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_amount"].Value) == 41625m
                                && dlg.TongCongBoxForTest.Value == 41625m
                                && dlg.LastWarningForTest == null;

                            Check("Đặt hàng đợt 2 UI [lưới thật nhập số]: 1600 hiện 1.600 & sửa '1600'; 2.5/2,5->2,5; 1.600->1,6; cột % 7.5->7,5",
                                q1600Ok && q1600ReEditOk && dotOk && dotReEditOk && commaOk && discDotOk
                                && dlg.DetailDataErrorCountForTest == 0,
                                "1600=" + q1600Ok + ",re1600=" + qty1600EditText + ",dot=" + dotOk + ",re25=" + qty25EditText
                                + ",comma=" + commaOk + ",disc75=" + discDotOk + ",de=" + dlg.DetailDataErrorCountForTest);

                            // 4. Gõ chữ / trống / 2 dấu ở cả 3 cột (d_qty, d_price, d_disc): 0 DataError, không treo, giữ giá trị cũ.
                            bool badQtyOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_qty", "xyz", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_qty"].Value) == 3m
                                && dlg.LastWarningForTest == BookingText.EnterQuantity;
                            bool badPriceOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_price", "abc", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_price"].Value) == 15000m
                                && dlg.LastWarningForTest == null
                                && EditDetailCellReal(grid, rowQty2, "d_price", "1,500.5", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_price"].Value) == 15000m
                                && dlg.LastWarningForTest == null;
                            bool badDiscOk = rowQty2 >= 0 && EditDetailCellReal(grid, rowQty2, "d_disc", "abc", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_disc"].Value) == 7.5m
                                && dlg.LastWarningForTest == null
                                && EditDetailCellReal(grid, rowQty2, "d_disc", "", out initText)
                                && Convert.ToDecimal(grid.Rows[rowQty2].Cells["d_disc"].Value) == 7.5m
                                && dlg.LastWarningForTest == null;

                            Check("Đặt hàng đợt 2 UI [gõ chữ/2 dấu/trống ở cả 3 cột]: 0 DataError, không treo, giữ giá trị cũ (chỉ d_qty cảnh báo)",
                                badQtyOk && badPriceOk && badDiscOk && dlg.DetailDataErrorCountForTest == 0,
                                "qty=" + badQtyOk + ",price=" + badPriceOk + ",disc=" + badDiscOk + ",de=" + dlg.DetailDataErrorCountForTest);

                            // 5. Dòng cũ đang có SL 0: sửa đơn giá & % giảm giữ hành vi kẹp (âm->0, % kẹp 0..100), không bị báo lỗi SL.
                            bool zeroRowPriceOk = rowQty0 >= 0 && EditDetailCellReal(grid, rowQty0, "d_price", "20000", out initText)
                                && dlg.LastWarningForTest == null
                                && Convert.ToDecimal(grid.Rows[rowQty0].Cells["d_price"].Value) == 20000m
                                && Convert.ToDecimal(grid.Rows[rowQty0].Cells["d_qty"].Value) == 0m
                                && Convert.ToDecimal(grid.Rows[rowQty0].Cells["d_amount"].Value) == 0m;

                            bool zeroRowClampOk = rowQty0 >= 0 && EditDetailCellReal(grid, rowQty0, "d_price", "-500", out initText)
                                && dlg.LastWarningForTest == null
                                && Convert.ToDecimal(grid.Rows[rowQty0].Cells["d_price"].Value) == 0m
                                && EditDetailCellReal(grid, rowQty0, "d_disc", "150", out initText)
                                && dlg.LastWarningForTest == null
                                && Convert.ToDecimal(grid.Rows[rowQty0].Cells["d_disc"].Value) == 100m;

                            Check("Đặt hàng đợt 2 UI [dòng cũ SL 0 sửa đơn giá/% giảm]: không bị báo SL, đơn giá âm về 0, % giảm kẹp 0..100, 0 DataError",
                                zeroRowPriceOk && zeroRowClampOk && dlg.DetailDataErrorCountForTest == 0,
                                "price=" + zeroRowPriceOk + ",clamp=" + zeroRowClampOk + ",de=" + dlg.DetailDataErrorCountForTest);
                        }
                        finally { dlg.Close(); }
                    }
                }

                // Ca c: NhapMotMatHangNhieuLanTrongPhieu = 30, bấm nút Thêm thật 2 lần (SL 2 rồi 3) với hàng 1 ĐVT -> 1 dòng 5.
                configChanged = true;
                configRepo.Save(new[]
                {
                    new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = 30 },
                    new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = 30 }
                }, "selftest");
                SystemConfig.Reload(db);

                using (var dlgMerge = OpenOffscreenBookingDialog(adminSession, null))
                {
                    try
                    {
                        bool sel = SelectBookingProductRow(dlgMerge.ProductGridForTest, prod1Id);
                        dlgMerge.QtyBoxForTest.Value = 2m;
                        dlgMerge.AddProductButtonForTest.PerformClick();
                        dlgMerge.QtyBoxForTest.Value = 3m;
                        dlgMerge.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();

                        DataGridView g = dlgMerge.DetailGridForTest;
                        bool mergedOk = sel && g.Rows.Count == 1
                            && Convert.ToDecimal(g.Rows[0].Cells["d_qty"].Value) == 5m
                            && Convert.ToDecimal(g.Rows[0].Cells["d_price"].Value) == 15000m
                            && Convert.ToDecimal(g.Rows[0].Cells["d_amount"].Value) == 75000m
                            && dlgMerge.TongCongBoxForTest.Value == 75000m
                            && dlgMerge.LastWarningForTest == null && dlgMerge.DirtyForTest;
                        Check("Đặt hàng đợt 2 UI [gộp 1 ĐVT khi NhapMotMatHangNhieuLan=30]: bấm Thêm SL 2 rồi 3 -> đúng 1 dòng SL 5, tổng 75000",
                            mergedOk, "rows=" + g.Rows.Count + ", tc=" + dlgMerge.TongCongBoxForTest.Value);
                    }
                    finally { dlgMerge.Close(); }
                }

                // SuDung2DonViTinh = 0 và NhapMotMatHangNhieuLanTrongPhieu = 30: hàng 2 ĐVT cũng vào nhánh 1 ĐVT và gộp 2 + 3 = 5.
                configRepo.Save(new[]
                {
                    new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = 0 },
                    new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = 30 }
                }, "selftest");
                SystemConfig.Reload(db);

                using (var dlgOffTwoUnit = OpenOffscreenBookingDialog(adminSession, null))
                {
                    try
                    {
                        bool sel = SelectBookingProductRow(dlgOffTwoUnit.ProductGridForTest, prod2Id);
                        dlgOffTwoUnit.QtyBoxForTest.Value = 2m;
                        dlgOffTwoUnit.AddProductButtonForTest.PerformClick();
                        dlgOffTwoUnit.QtyBoxForTest.Value = 3m;
                        dlgOffTwoUnit.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();

                        DataGridView g = dlgOffTwoUnit.DetailGridForTest;
                        bool mergedOffOk = sel && g.Rows.Count == 1
                            && Convert.ToDecimal(g.Rows[0].Cells["d_qty"].Value) == 5m
                            && Convert.ToDecimal(g.Rows[0].Cells["d_price"].Value) == 15000m
                            && Convert.ToDecimal(g.Rows[0].Cells["d_amount"].Value) == 75000m
                            && dlgOffTwoUnit.TongCongBoxForTest.Value == 75000m
                            && dlgOffTwoUnit.LastWarningForTest == null;
                        Check("Đặt hàng đợt 2 UI [SuDung2DonViTinh=0, NhapMotMatHangNhieuLan=30]: bấm Thêm SL 2 rồi 3 -> đúng 1 dòng SL 5",
                            mergedOffOk, "rows=" + g.Rows.Count + ", tc=" + dlgOffTwoUnit.TongCongBoxForTest.Value);
                    }
                    finally { dlgOffTwoUnit.Close(); }
                }
            }
            finally
            {
                if (configChanged)
                {
                    configRepo.Save(new[]
                    {
                        new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = oldTwoUnitCfg ? 30 : 0 },
                        new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = oldMultiRowCfg ? 30 : 0 }
                    }, "selftest");
                }
                AppSession.Current = savedSession;
                SystemConfig.Reload(db);
                if (!string.IsNullOrEmpty(savedBookingId))
                {
                    BookingExec(db, "DELETE FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" = @id", "@id", savedBookingId);
                    BookingExec(db, "DELETE FROM \"TDATHANG\" WHERE \"ID\" = @id", "@id", savedBookingId);
                }
                BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" IN (@a, @b)", "@a", prod1Id, "@b", prod2Id);
                BookingExec(db, "DELETE FROM \"DNHOMMATHANG\" WHERE \"ID\" = @id", "@id", groupId);
                BookingExec(db, "DELETE FROM \"DDONVITINH\" WHERE \"ID\" IN (@a, @b)", "@a", unitOddId, "@b", unitEvenId);
                BookingExec(db, "DELETE FROM \"DBAN\" WHERE \"ID\" = @id", "@id", roomId);
                BookingExec(db, "DELETE FROM \"DKHUVUC\" WHERE \"ID\" = @id", "@id", areaId);
            }
        }

        private static BookingEditDialog OpenOffscreenBookingDialog(UserSession session, string bookingId)
        {
            var dlg = new BookingEditDialog(session, bookingId, null);
            dlg.SuppressPopup = true;
            dlg.WindowState = FormWindowState.Normal;
            dlg.StartPosition = FormStartPosition.Manual;
            dlg.Location = new Point(-20000, -20000);
            dlg.Show();
            Application.DoEvents();
            return dlg;
        }

        private static int FindDetailRowByName(DataGridView grid, string productName)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                if (string.Equals(Convert.ToString(grid.Rows[i].Cells["d_name"].Value), productName, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static bool EditDetailCellReal(DataGridView grid, int rowIndex, string colName, string newText, out string initialEditText)
        {
            initialEditText = null;
            grid.Focus();
            grid.CurrentCell = grid.Rows[rowIndex].Cells[colName];
            bool began = grid.BeginEdit(true);
            var editor = grid.EditingControl as TextBox;
            if (!began || editor == null)
                return false;
            initialEditText = editor.Text;
            if (newText != null)
                editor.Text = newText;
            grid.EndEdit();
            Application.DoEvents();
            return true;
        }
    }
}
