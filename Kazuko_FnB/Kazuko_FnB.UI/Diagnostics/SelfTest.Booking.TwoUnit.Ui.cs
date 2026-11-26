using System;
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
        private static void BookingTwoUnitUiTests(string db)
        {
            var configRepo = new ConfigRepository(db);
            UserSession savedSession = AppSession.Current;
            bool oldTwoUnitCfg = false;
            bool oldMultiRowCfg = false;
            bool configChanged = false;

            string groupId = DbHelper.NewId();
            string unitOddId = DbHelper.NewId();
            string unitEvenId = DbHelper.NewId();
            string prod1Id = DbHelper.NewId();
            string prod2Id = DbHelper.NewId();

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
                    "INSERT INTO \"DNHOMMATHANG\" (\"ID\", \"NAME\", \"CODE\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @c, 30, 'selftest')",
                    "@id", groupId, "@n", "NHÓM THỬ ĐH 2ĐVT", "@c", "DH2GRP");
                BookingExec(db,
                    "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", unitOddId, "@n", "LonDH2");
                BookingExec(db,
                    "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", unitEvenId, "@n", "ThùngDH2");
                BookingExec(db,
                    "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"GIABAN\", \"STATUS\", \"USERCREATEDID\") " +
                    "VALUES (@id, @c, @n, @g, @u, @p, 30, 'selftest')",
                    "@id", prod1Id, "@c", "DH1UNIT", "@n", "Mặt hàng ĐH 1 ĐVT", "@g", groupId, "@u", unitOddId, "@p", 20000m);
                BookingExec(db,
                    "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"DDONVITINHCHANID\", \"QUYDOI\", \"GIABAN\", \"GIABANCHAN\", \"STATUS\", \"USERCREATEDID\") " +
                    "VALUES (@id, @c, @n, @g, @u1, @u2, 24, @p1, @p2, 30, 'selftest')",
                    "@id", prod2Id, "@c", "DH2UNIT", "@n", "Mặt hàng ĐH 2 ĐVT", "@g", groupId,
                    "@u1", unitOddId, "@u2", unitEvenId, "@p1", 15000m, "@p2", 340000m);

                configChanged = true;
                configRepo.Save(new[]
                {
                    new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = 30 },
                    new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = 0 }
                }, "selftest");
                SystemConfig.Reload(db);

                // 1. QuantityDialog thật: ô lẻ mặc định bằng ô SL ngoài hộp.
                var realItem = new BookingSaleItem
                {
                    Id = prod2Id,
                    Name = "Mặt hàng ĐH 2 ĐVT",
                    UnitId = unitOddId,
                    UnitName = "LonDH2",
                    BigUnitId = unitEvenId,
                    BigUnitName = "ThùngDH2",
                    SellPrice = 15000m,
                    BigSellPrice = 340000m,
                    QuyDoi = 24m
                };
                using (var qdlg = new QuantityDialog(realItem, 2.5m))
                {
                    qdlg.StartPosition = FormStartPosition.Manual;
                    qdlg.Location = new Point(-20000, -20000);
                    qdlg.Show();
                    Application.DoEvents();
                    bool qdlgOk = qdlg.OddQuantity == 2.5m && qdlg.EvenQuantity == 0m;
                    qdlg.Close();
                    Check("Đặt hàng 2 ĐVT UI [QuantityDialog thật]: ô lẻ mặc định bằng ô SL (2.5), ô chẵn mặc định 0", qdlgOk);
                }

                // 2. BookingEditDialog thật (Show() ngoài màn hình), NhapMotMatHangNhieuLanTrongPhieu = 0.
                using (var dlg = OpenOffscreenBookingDialog(adminSession))
                {
                    try
                    {
                        bool selected = SelectBookingProductRow(dlg.ProductGridForTest, prod2Id);
                        Check("Đặt hàng 2 ĐVT UI: chọn được mặt hàng 2 ĐVT trên lưới chọn hàng", selected);

                        // 2a. Hộp bấm Hủy bỏ -> không thêm dòng, không cảnh báo, không đánh dấu đã sửa.
                        dlg.QtyBoxForTest.Value = 2m;
                        dlg.QuantityPromptOverrideForTest = (it, q) => new BookingQuantityPromptResult
                        {
                            Confirmed = false, OddQuantity = 5m, EvenQuantity = 1m
                        };
                        dlg.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();
                        Check("Đặt hàng 2 ĐVT UI [hộp Hủy bỏ]: lưới 0 dòng, tổng 0, không cảnh báo, không đánh dấu đã sửa",
                            dlg.DetailGridForTest.Rows.Count == 0 && dlg.TongCongBoxForTest.Value == 0m
                            && dlg.LastWarningForTest == null && !dlg.DirtyForTest && !dlg.SaveButtonForTest.Enabled);

                        // 2b. Chưa có dòng lẻ, ô SL 2, hộp lẻ 0 chẵn 0 -> không thêm dòng, có cảnh báo, không đánh dấu đã sửa.
                        dlg.QuantityPromptOverrideForTest = (it, q) => new BookingQuantityPromptResult
                        {
                            Confirmed = true, OddQuantity = 0m, EvenQuantity = 0m
                        };
                        dlg.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();
                        Check("Đặt hàng 2 ĐVT UI [lẻ 0 chẵn 0 khi chưa có lẻ]: 0 dòng, có cảnh báo, không đánh dấu đã sửa",
                            dlg.DetailGridForTest.Rows.Count == 0 && dlg.TongCongBoxForTest.Value == 0m
                            && dlg.LastWarningForTest == BookingText.EnterQuantity
                            && !dlg.DirtyForTest && !dlg.SaveButtonForTest.Enabled);

                        // 2c. Ca bắt buộc FAIL nếu còn dùng số lẻ trong hộp: ô SL 2, hộp lẻ 5, chẵn 0 -> lưới hiện 2 (KHÔNG phải 5).
                        decimal capturedOuterQty = -1m;
                        decimal realDialogDefaultOdd = -1m;
                        dlg.QtyBoxForTest.Value = 2m;
                        dlg.QuantityPromptOverrideForTest = (it, q) =>
                        {
                            capturedOuterQty = q;
                            using (var probe = new QuantityDialog(it, q))
                                realDialogDefaultOdd = probe.OddQuantity;
                            return new BookingQuantityPromptResult { Confirmed = true, OddQuantity = 5m, EvenQuantity = 0m };
                        };
                        dlg.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool gridOdd2Ok = dlg.DetailGridForTest.Rows.Count == 1
                            && Convert.ToString(dlg.DetailGridForTest.Rows[0].Cells["d_unit"].Value) == "LonDH2"
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_qty"].Value) == 2m
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_price"].Value) == 15000m
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_amount"].Value) == 30000m;
                        Check("Đặt hàng 2 ĐVT UI [ô SL 2, hộp lẻ 5 -> lưới hiện 2]: lưới, tổng tiền, trạng thái sửa đúng và ô lẻ hộp mặc định = ô SL",
                            capturedOuterQty == 2m && realDialogDefaultOdd == 2m
                            && gridOdd2Ok && dlg.TongCongBoxForTest.Value == 30000m
                            && dlg.LastWarningForTest == null && dlg.DirtyForTest && dlg.SaveButtonForTest.Enabled,
                            "rows=" + dlg.DetailGridForTest.Rows.Count + ", qty=" + (dlg.DetailGridForTest.Rows.Count > 0 ? Convert.ToString(dlg.DetailGridForTest.Rows[0].Cells["d_qty"].Value) : "-") + ", tc=" + dlg.TongCongBoxForTest.Value);

                        // 2d. Đã có dòng lẻ 2, ô SL 2, hộp lẻ 0 chẵn 0 -> cộng ô SL thành 4, không cảnh báo.
                        dlg.QtyBoxForTest.Value = 2m;
                        dlg.QuantityPromptOverrideForTest = (it, q) => new BookingQuantityPromptResult
                        {
                            Confirmed = true, OddQuantity = 0m, EvenQuantity = 0m
                        };
                        dlg.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool gridOdd4Ok = dlg.DetailGridForTest.Rows.Count == 1
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_qty"].Value) == 4m
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_amount"].Value) == 60000m;
                        Check("Đặt hàng 2 ĐVT UI [đã có lẻ 2, ô SL 2, hộp 0/0 -> thành 4]: lưới hiện 4, tổng 60000, không cảnh báo",
                            gridOdd4Ok && dlg.TongCongBoxForTest.Value == 60000m && dlg.LastWarningForTest == null);

                        // 2e. Đã có dòng lẻ 4, ô SL 0, hộp lẻ 3 chẵn 1 -> vừa cảnh báo vừa thêm dòng chẵn 1 giá 340000, tổng 400000.
                        dlg.QtyBoxForTest.Value = 0m;
                        dlg.QuantityPromptOverrideForTest = (it, q) => new BookingQuantityPromptResult
                        {
                            Confirmed = true, OddQuantity = 3m, EvenQuantity = 1m
                        };
                        dlg.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool bothWarnAndEvenOk = dlg.DetailGridForTest.Rows.Count == 2
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_qty"].Value) == 4m
                            && Convert.ToString(dlg.DetailGridForTest.Rows[1].Cells["d_unit"].Value) == "ThùngDH2"
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[1].Cells["d_qty"].Value) == 1m
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[1].Cells["d_price"].Value) == 340000m
                            && Convert.ToDecimal(dlg.DetailGridForTest.Rows[1].Cells["d_amount"].Value) == 340000m;
                        Check("Đặt hàng 2 ĐVT UI [vừa cảnh báo vừa thêm chẵn]: lẻ giữ 4, chẵn thêm 1, tổng 400000, có cảnh báo",
                            bothWarnAndEvenOk && dlg.TongCongBoxForTest.Value == 400000m
                            && dlg.LastWarningForTest == BookingText.EnterQuantity);
                    }
                    finally { dlg.Close(); }
                }

                // 3. Luật 6 từ trạng thái dialog sạch: ô SL 0, lẻ hộp 3, chẵn 1 -> cảnh báo + thêm dòng chẵn + đánh dấu đã sửa.
                using (var dlgClean = OpenOffscreenBookingDialog(adminSession))
                {
                    try
                    {
                        SelectBookingProductRow(dlgClean.ProductGridForTest, prod2Id);
                        dlgClean.QtyBoxForTest.Value = 0m;
                        dlgClean.QuantityPromptOverrideForTest = (it, q) => new BookingQuantityPromptResult
                        {
                            Confirmed = true, OddQuantity = 3m, EvenQuantity = 1m
                        };
                        dlgClean.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool cleanEvenOk = dlgClean.DetailGridForTest.Rows.Count == 1
                            && Convert.ToString(dlgClean.DetailGridForTest.Rows[0].Cells["d_unit"].Value) == "ThùngDH2"
                            && Convert.ToDecimal(dlgClean.DetailGridForTest.Rows[0].Cells["d_qty"].Value) == 1m
                            && Convert.ToDecimal(dlgClean.DetailGridForTest.Rows[0].Cells["d_price"].Value) == 340000m;
                        Check("Đặt hàng 2 ĐVT UI [luật 6 từ dialog sạch]: vừa cảnh báo vừa thêm chẵn 1, tổng 340000 và bật đã sửa",
                            cleanEvenOk && dlgClean.TongCongBoxForTest.Value == 340000m
                            && dlgClean.LastWarningForTest == BookingText.EnterQuantity
                            && dlgClean.DirtyForTest && dlgClean.SaveButtonForTest.Enabled);
                    }
                    finally { dlgClean.Close(); }
                }

                // 4. Khi NhapMotMatHangNhieuLanTrongPhieu = 30: dòng lẻ 2 ĐVT vẫn gộp, dòng chẵn thêm mới; nhánh 1 ĐVT thêm mới như cũ.
                configRepo.Save(new[]
                {
                    new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = 30 }
                }, "selftest");
                SystemConfig.Reload(db);

                using (var dlgMulti = OpenOffscreenBookingDialog(adminSession))
                {
                    try
                    {
                        SelectBookingProductRow(dlgMulti.ProductGridForTest, prod2Id);
                        dlgMulti.QtyBoxForTest.Value = 2m;
                        dlgMulti.QuantityPromptOverrideForTest = (it, q) => new BookingQuantityPromptResult
                        {
                            Confirmed = true, OddQuantity = 5m, EvenQuantity = 1m
                        };
                        dlgMulti.AddProductButtonForTest.PerformClick();
                        dlgMulti.QtyBoxForTest.Value = 3m;
                        dlgMulti.QuantityPromptOverrideForTest = (it, q) => new BookingQuantityPromptResult
                        {
                            Confirmed = true, OddQuantity = 0m, EvenQuantity = 2m
                        };
                        dlgMulti.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();

                        bool twoUnitMultiOk = dlgMulti.DetailGridForTest.Rows.Count == 3
                            && Convert.ToString(dlgMulti.DetailGridForTest.Rows[0].Cells["d_unit"].Value) == "LonDH2"
                            && Convert.ToDecimal(dlgMulti.DetailGridForTest.Rows[0].Cells["d_qty"].Value) == 5m
                            && Convert.ToString(dlgMulti.DetailGridForTest.Rows[1].Cells["d_unit"].Value) == "ThùngDH2"
                            && Convert.ToDecimal(dlgMulti.DetailGridForTest.Rows[1].Cells["d_qty"].Value) == 1m
                            && Convert.ToString(dlgMulti.DetailGridForTest.Rows[2].Cells["d_unit"].Value) == "ThùngDH2"
                            && Convert.ToDecimal(dlgMulti.DetailGridForTest.Rows[2].Cells["d_qty"].Value) == 2m
                            && dlgMulti.TongCongBoxForTest.Value == 1095000m;
                        Check("Đặt hàng 2 ĐVT UI [NhapMotMatHangNhieuLan=30]: lẻ vẫn gộp (2+3=5), chẵn thêm dòng mới (1 và 2)", twoUnitMultiOk,
                            "rows=" + dlgMulti.DetailGridForTest.Rows.Count + ", tc=" + dlgMulti.TongCongBoxForTest.Value);

                        // Tên ca đã lỗi thời, giữ để khớp mốc; luật mới theo spec đợt 2 (Chiến chốt 26/11):
                        // nhánh 1 ĐVT luôn gộp dù NhapMotMatHangNhieuLanTrongPhieu = 30.
                        SelectBookingProductRow(dlgMulti.ProductGridForTest, prod1Id);
                        dlgMulti.QtyBoxForTest.Value = 0m;
                        dlgMulti.AddProductButtonForTest.PerformClick();
                        bool singleZeroWarn = dlgMulti.LastWarningForTest == BookingText.EnterQuantity
                            && dlgMulti.DetailGridForTest.Rows.Count == 3
                            && dlgMulti.TongCongBoxForTest.Value == 1095000m;
                        dlgMulti.QtyBoxForTest.Value = 2m;
                        dlgMulti.AddProductButtonForTest.PerformClick();
                        dlgMulti.AddProductButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool singleMultiOk = singleZeroWarn && dlgMulti.DetailGridForTest.Rows.Count == 4
                            && dlgMulti.LastWarningForTest == null
                            && Convert.ToDecimal(dlgMulti.DetailGridForTest.Rows[3].Cells["d_qty"].Value) == 4m
                            && Convert.ToDecimal(dlgMulti.DetailGridForTest.Rows[3].Cells["d_price"].Value) == 20000m
                            && Convert.ToDecimal(dlgMulti.DetailGridForTest.Rows[3].Cells["d_amount"].Value) == 80000m
                            && dlgMulti.TongCongBoxForTest.Value == 1175000m;
                        Check("Đặt hàng 1 ĐVT UI [nhánh 1 ĐVT giữ nguyên]: SL=0 cảnh báo; thêm 2 lần khi không cho gộp tạo 2 dòng riêng", singleMultiOk,
                            "rows=" + dlgMulti.DetailGridForTest.Rows.Count + ", tc=" + dlgMulti.TongCongBoxForTest.Value);
                    }
                    finally { dlgMulti.Close(); }
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
                BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" IN (@a, @b)", "@a", prod1Id, "@b", prod2Id);
                BookingExec(db, "DELETE FROM \"DNHOMMATHANG\" WHERE \"ID\" = @id", "@id", groupId);
                BookingExec(db, "DELETE FROM \"DDONVITINH\" WHERE \"ID\" IN (@a, @b)", "@a", unitOddId, "@b", unitEvenId);
            }
        }

        private static BookingEditDialog OpenOffscreenBookingDialog(UserSession session)
        {
            var dlg = new BookingEditDialog(session, null, null);
            dlg.SuppressPopup = true;
            dlg.WindowState = FormWindowState.Normal;
            dlg.StartPosition = FormStartPosition.Manual;
            dlg.Location = new Point(-20000, -20000);
            dlg.Show();
            Application.DoEvents();
            return dlg;
        }

        private static bool SelectBookingProductRow(DataGridView grid, string productId)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                var item = grid.Rows[i].Tag as BookingSaleItem;
                if (item != null && string.Equals(item.Id, productId, StringComparison.Ordinal))
                {
                    grid.ClearSelection();
                    grid.Rows[i].Selected = true;
                    grid.CurrentCell = grid.Rows[i].Cells[0];
                    return true;
                }
            }
            return false;
        }
    }
}
