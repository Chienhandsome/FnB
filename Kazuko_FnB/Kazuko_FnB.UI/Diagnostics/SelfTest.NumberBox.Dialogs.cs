using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void NumberBoxDialogTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            var configRepo = new ConfigRepository(db);
            bool oldTwoUnitCfg = false;
            bool oldMultiRowCfg = false;
            bool configChanged = false;

            string areaId = DbHelper.NewId();
            string roomId = DbHelper.NewId();
            string groupId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prodId = DbHelper.NewId();
            string bookingId = null;

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

                configChanged = true;
                configRepo.Save(new[]
                {
                    new ConfigChange { Name = "SuDung2DonViTinh", DataType = 3, IntValue = 0 },
                    new ConfigChange { Name = "NhapMotMatHangNhieuLanTrongPhieu", DataType = 3, IntValue = 0 }
                }, "selftest");
                SystemConfig.Reload(db);

                BookingExec(db,
                    "INSERT INTO \"DKHUVUC\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", areaId, "@n", "KV THỬ NHẬP SỐ P1");
                BookingExec(db,
                    "INSERT INTO \"DBAN\" (\"ID\", \"NAME\", \"DKHUVUCID\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @a, 30, 'selftest')",
                    "@id", roomId, "@n", "Bàn thử NS1", "@a", areaId);
                BookingExec(db,
                    "INSERT INTO \"DNHOMMATHANG\" (\"ID\", \"NAME\", \"CODE\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @c, 30, 'selftest')",
                    "@id", groupId, "@n", "NHÓM THỬ NS1", "@c", "NSP1GRP");
                BookingExec(db,
                    "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                    "@id", unitId, "@n", "LyNS1");
                BookingExec(db,
                    "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"GIABAN\", \"STATUS\", \"USERCREATEDID\") " +
                    "VALUES (@id, @c, @n, @g, @u, @p, 30, 'selftest')",
                    "@id", prodId, "@c", "NSP1MH", "@n", "Mặt hàng thử NS1", "@g", groupId, "@u", unitId, "@p", 15000m);

                var bookingRepo = new BookingRepository(db);
                DateTime now = DateTime.Now;
                var row = BookingRow.NewDefault();
                row.Name = "DH99/98801";
                row.Ngay = DateTime.Today;
                row.GioDat = now;
                row.TuGio = now;
                row.DenGio = now.AddHours(2);
                row.TuNgay = now.Date;
                row.DenNgay = now.AddHours(2).Date;
                row.TenKhach = "Khách thử NS1";
                row.TienHang = 30000m;
                row.TileGiamGia = 10m;
                row.TienGiamGia = 3000m;
                row.GiamTheoTien = false;
                row.TongCong = 27000m;
                var lines = new List<BookingLine>
                {
                    new BookingLine { ProductId = prodId, UnitId = unitId, Quantity = 2m, Price = 15000m, DiscountPct = 0m, Amount = 30000m }
                };
                BookingSaveResult saved = bookingRepo.SaveBooking(row, lines, new List<string> { roomId }, "selftest");
                if (saved.Ok)
                    bookingId = saved.SavedId;

                foreach (string cultureName in new[] { "en-US", "vi-VN" })
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    TestBookingDialogNumberBox(adminSession, bookingId, prodId, cultureName);
                    TestProductDialogNumberBox(db, adminSession, prodId, cultureName);
                    TestIntegerDialogsNumberBox(adminSession, cultureName);
                    TestDynamicFormAndPhieuThuNumberBox(adminSession, cultureName);
                    NumberBoxThousandsDialogTests(adminSession, bookingId, cultureName);
                }

                TestNoRawNumericUpDownAcrossDialogs(adminSession, bookingId, prodId, areaId, roomId);
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
                if (!string.IsNullOrEmpty(bookingId))
                {
                    BookingExec(db, "DELETE FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" = @id", "@id", bookingId);
                    BookingExec(db, "DELETE FROM \"TDATHANG\" WHERE \"ID\" = @id", "@id", bookingId);
                }
                BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" = @id", "@id", prodId);
                BookingExec(db, "DELETE FROM \"DNHOMMATHANG\" WHERE \"ID\" = @id", "@id", groupId);
                BookingExec(db, "DELETE FROM \"DDONVITINH\" WHERE \"ID\" = @id", "@id", unitId);
                BookingExec(db, "DELETE FROM \"DBAN\" WHERE \"ID\" = @id", "@id", roomId);
                BookingExec(db, "DELETE FROM \"DKHUVUC\" WHERE \"ID\" = @id", "@id", areaId);
            }
        }

        private static void TestBookingDialogNumberBox(UserSession session, string bookingId, string prodId, string cultureTag)
        {
            if (string.IsNullOrEmpty(bookingId))
            {
                Check("Nhập số P1 [BookingEditDialog " + cultureTag + "]: dữ liệu mẫu phiếu đặt", false, "save booking failed");
                return;
            }
            using (BookingEditDialog dlg = OpenOffscreenBookingDialog(session, bookingId))
            {
                try
                {
                    // Ô tổng cộng chỉ đọc hiện kiểu vi-VN ("27.000") kể cả trên en-US; Tiền hàng hiện "30.000".
                    bool displayViOk = dlg.TongCongBoxForTest.Text == "27.000" && dlg.TienHangBoxForTest.Text == "30.000";

                    // A4: vào rồi ra ô tiền giảm giá không sửa -> không dirty và không đổi cờ giảm theo tiền.
                    bool startClean = !dlg.DirtyForTest && !dlg.DiscountByMoneyForTest;
                    FocusControlReal(dlg, dlg.TienGiamGiaBoxForTest);
                    string editDiscountText = dlg.TienGiamGiaBoxForTest.Text;
                    FocusControlReal(dlg, dlg.TileGiamGiaBoxForTest);
                    FocusControlReal(dlg, dlg.QtyBoxForTest);
                    bool leaveCleanOk = startClean && editDiscountText == "3000"
                        && !dlg.DirtyForTest && !dlg.DiscountByMoneyForTest
                        && dlg.TienGiamGiaBoxForTest.Value == 3000m && dlg.TienGiamGiaBoxForTest.Text == "3.000";

                    // A5: ô tiền giảm giá (trước đây DecimalPlaces = 0) gõ 15,5 -> Value 15.5m và hiện "15,5".
                    TypeCharsIntoNumberBox(dlg, dlg.TienGiamGiaBoxForTest, "15,5", true);
                    FocusControlReal(dlg, dlg.QtyBoxForTest);
                    bool moneyFracOk = dlg.TienGiamGiaBoxForTest.Value == 15.5m
                        && dlg.TienGiamGiaBoxForTest.Text == "15,5" && dlg.DiscountByMoneyForTest;

                    // A9: gõ 2.5 vào ô Số lượng rồi PerformClick nút Thêm NGAY khi chưa rời ô -> dùng đúng 2,5 (2 + 2,5 = 4,5).
                    var qtyBox = dlg.QtyBoxForTest as NumberBox;
                    bool sel = SelectBookingProductRow(dlg.ProductGridForTest, prodId);
                    TypeCharsIntoNumberBox(dlg, qtyBox, "2.5", true);
                    dlg.AddProductButtonForTest.PerformClick();
                    Application.DoEvents();
                    decimal lineQty = dlg.DetailGridForTest.Rows.Count > 0
                        ? Convert.ToDecimal(dlg.DetailGridForTest.Rows[0].Cells["d_qty"].Value) : -1m;
                    bool addImmediateOk = sel && qtyBox != null && lineQty == 4.5m;

                    Check("Nhập số P1 [BookingEditDialog " + cultureTag + " A4/A5/A9]: tổng vi-VN, vào ra tiền giảm không dirty/không đổi kiểu, 15,5 hiện 15,5, gõ SL 2.5 bấm Thêm ngay ra 4,5",
                        displayViOk && leaveCleanOk && moneyFracOk && addImmediateOk,
                        "vi=" + displayViOk + "(" + dlg.TongCongBoxForTest.Text + "), clean=" + leaveCleanOk
                        + ", frac=" + moneyFracOk + "(" + dlg.TienGiamGiaBoxForTest.Value + "/" + dlg.TienGiamGiaBoxForTest.Text + ")"
                        + ", add=" + addImmediateOk + "(qty=" + lineQty + ")");
                }
                finally { dlg.Close(); }
            }
        }

        private static void TestProductDialogNumberBox(string db, UserSession session, string prodId, string cultureTag)
        {
            BookingExec(db, "UPDATE \"DMATHANG\" SET \"GIABAN\" = 15000 WHERE \"ID\" = @id", "@id", prodId);
            using (var dlg = new ProductEditDialog(session, prodId, null, null))
            {
                dlg.SuppressPopup = true;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    NumberBox sellBox = dlg.SellPriceBoxForTest;
                    Control other = FindFirstOtherSelectable(dlg, sellBox);
                    TypeCharsIntoNumberBox(dlg, sellBox, "15.5", true);
                    string typing = sellBox.Text;
                    if (other != null)
                        FocusControlReal(dlg, other);
                    dlg.SaveButtonForTest.PerformClick();
                    Application.DoEvents();
                    ProductRow back = new ProductRepository(db).LoadProduct(prodId);
                    bool savedOk = typing == "15,5" && sellBox.Value == 15.5m && sellBox.Text == "15,5"
                        && back != null && back.SellPrice == 15.5m;
                    Check("Nhập số P1 [ProductEditDialog " + cultureTag + " A2/A5]: giá bán gõ 15.5 -> hiện '15,5' & lưu DB đúng 15,5",
                        savedOk,
                        "typing=" + typing + ", val=" + sellBox.Value + ", text=" + sellBox.Text
                        + ", db=" + (back == null ? "null" : back.SellPrice.ToString(CultureInfo.InvariantCulture)));
                }
                finally { dlg.Close(); }
            }
        }

        private static void TestIntegerDialogsNumberBox(UserSession session, string cultureTag)
        {
            // QuickAddRoomDialog: ô số chữ số là số nguyên (N5), gõ 2.5 giữ số cũ 2.
            using (var dlg = new QuickAddRoomDialog(session))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    NumberBox digits = dlg.DigitLengthBoxForTest;
                    Control other = FindFirstOtherSelectable(dlg, digits);
                    TypeCharsIntoNumberBox(dlg, digits, "2.5", true);
                    string typing = digits.Text;
                    if (other != null)
                        FocusControlReal(dlg, other);
                    bool ok = other != null && typing == "2,5" && digits.DecimalPlaces == 0 && digits.Value == 2m && digits.Text == "2";
                    Check("Nhập số P1 [QuickAddRoomDialog " + cultureTag + " A10 N5]: ô số nguyên gõ 2.5 giữ số cũ 2",
                        ok, "typing=" + typing + ", val=" + digits.Value + ", text=" + digits.Text + ", other=" + (other != null));
                }
                finally { dlg.Close(); }
            }

            // ConfigDialog: ô số cấu hình kiểu số nguyên (N5), gõ 2.5 giữ số cũ, không đánh dấu dirty.
            using (var cfg = new ConfigDialog(session))
            {
                cfg.StartPosition = FormStartPosition.Manual;
                cfg.Location = new Point(-20000, -20000);
                cfg.Show();
                Application.DoEvents();
                try
                {
                    NumberBox num = FindAnyNumberBoxInConfigDialog(cfg);
                    if (num != null)
                    {
                        decimal oldVal = num.Value;
                        Control other = FindFirstOtherSelectable(cfg, num);
                        TypeCharsIntoNumberBox(cfg, num, "2.5", true);
                        if (other != null)
                            FocusControlReal(cfg, other);
                        bool ok = other != null && num.DecimalPlaces == 0 && num.Value == oldVal;
                        Check("Nhập số P1 [ConfigDialog " + cultureTag + " A10 N5]: ô cấu hình số nguyên gõ 2.5 giữ số cũ",
                            ok, "old=" + oldVal + ", cur=" + num.Value + ", dp=" + num.DecimalPlaces);
                    }
                    else
                    {
                        Check("Nhập số P1 [ConfigDialog " + cultureTag + " A10 N5]: tìm thấy ô NumberBox trên trang cấu hình", false, "no NumberBox");
                    }
                }
                finally { cfg.Close(); }
            }
        }

        private static void TestDynamicFormAndPhieuThuNumberBox(UserSession session, string cultureTag)
        {
            // PhieuThuDialog: numTHU là NumberBox với DecimalPlaces = 2, gõ 15.5 -> 15,5; gõ 15000 -> "15.000".
            using (var dlg = new PhieuThuDialog(session))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    NumberBox numThu = FindNamedNumberBox(dlg, "numTHU");
                    Control other = FindFirstOtherSelectable(dlg, numThu);
                    bool found = numThu != null && other != null;
                    bool fracOk = false, intDispOk = false;
                    if (found)
                    {
                        TypeCharsIntoNumberBox(dlg, numThu, "15.5", true);
                        FocusControlReal(dlg, other);
                        fracOk = numThu.DecimalPlaces == 2 && numThu.Value == 15.5m && numThu.Text == "15,5";

                        TypeCharsIntoNumberBox(dlg, numThu, "15000", true);
                        FocusControlReal(dlg, other);
                        intDispOk = numThu.Value == 15000m && numThu.Text == "15.000";
                    }
                    Check("Nhập số P1 [PhieuThuDialog/DynamicForm " + cultureTag + " A5]: numTHU cho 2 số lẻ (15.5->15,5; 15000->'15.000')",
                        found && fracOk && intDispOk,
                        "found=" + found + ", frac=" + fracOk + ", disp=" + (numThu == null ? "-" : numThu.Text));
                }
                finally { dlg.Close(); }
            }

            // DynamicForm trực tiếp: kiểm phân biệt ô tiền (DecimalPlaces=0 -> 2) và ô không phải tiền (DecimalPlaces=0 -> số nguyên N5).
            var layout = new ScreenLayout { Width = 400, Height = 120, Table = "TEST" };
            var moneyCtrl = new LayoutControl { Name = "numSOTIEN", Kind = LayoutKind.Number, X = 100, Y = 10, Width = 120, Height = 24, Enabled = true, TabIndex = 0 };
            moneyCtrl.Props["DecimalPlaces"] = "0";
            moneyCtrl.Props["ThousandsSeparator"] = "True";
            var intCtrl = new LayoutControl { Name = "numTHUTU", Kind = LayoutKind.Number, X = 100, Y = 50, Width = 120, Height = 24, Enabled = true, TabIndex = 1 };
            intCtrl.Props["DecimalPlaces"] = "0";
            layout.Controls.Add(moneyCtrl);
            layout.Controls.Add(intCtrl);
            layout.Maps.Add(new FieldMap { DataField = "SOTIEN", EditControl = "numSOTIEN" });
            layout.Maps.Add(new FieldMap { DataField = "THUTU", EditControl = "numTHUTU" });

            using (var host = new Form())
            {
                host.StartPosition = FormStartPosition.Manual;
                host.Location = new Point(-20000, -20000);
                host.Size = new Size(420, 200);
                var df = new DynamicForm(layout) { Dock = DockStyle.Fill };
                host.Controls.Add(df);
                host.Show();
                Application.DoEvents();
                try
                {
                    var mBox = df.Editor("numSOTIEN") as NumberBox;
                    var iBox = df.Editor("numTHUTU") as NumberBox;
                    bool typesOk = mBox != null && iBox != null && mBox.DecimalPlaces == 2 && iBox.DecimalPlaces == 0;
                    bool behOk = false;
                    if (typesOk)
                    {
                        iBox.Value = 3m;
                        TypeCharsIntoNumberBox(host, mBox, "15.5", true);
                        FocusControlReal(host, iBox);
                        TypeCharsIntoNumberBox(host, iBox, "2.5", true);
                        FocusControlReal(host, mBox);
                        behOk = mBox.Value == 15.5m && iBox.Value == 3m;
                    }
                    Check("Nhập số P1 [DynamicForm " + cultureTag + " N3/N5]: ô tiền khai 0 thành 2 số lẻ, ô không phải tiền khai 0 là số nguyên",
                        typesOk && behOk,
                        "types=" + typesOk + ", m=" + (mBox == null ? "-" : mBox.Value.ToString(CultureInfo.InvariantCulture))
                        + ", i=" + (iBox == null ? "-" : iBox.Value.ToString(CultureInfo.InvariantCulture)));
                }
                finally { host.Close(); }
            }
        }

        private static void TestNoRawNumericUpDownAcrossDialogs(UserSession session, string bookingId, string prodId, string areaId, string roomId)
        {
            var rawFound = new List<string>();
            var noThousandsFound = new List<string>();
            int totalNumberBoxes = 0;

            Action<string, Form> scan = (label, f) =>
            {
                try
                {
                    ScanRawNumericUpDown(f, label, rawFound, noThousandsFound, ref totalNumberBoxes);
                }
                finally
                {
                    f.Dispose();
                }
            };

            scan("BookingEditDialog", new BookingEditDialog(session, bookingId, null) { SuppressPopup = true });
            scan("ProductEditDialog", new ProductEditDialog(session, prodId, null, null) { SuppressPopup = true });
            scan("QuickAddRoomDialog", new QuickAddRoomDialog(session));
            using (var cfg = new ConfigDialog(session))
            {
                FindAnyNumberBoxInConfigDialog(cfg);
                ScanRawNumericUpDown(cfg, "ConfigDialog", rawFound, noThousandsFound, ref totalNumberBoxes);
            }
            scan("PhieuThuDialog", new PhieuThuDialog(session));
            scan("QuoteEditDialog", QuoteEditDialog.CreateForPreview(session));
            scan("QuantityDialog", new QuantityDialog(new BookingSaleItem { Name = "SP", UnitName = "Ly" }, 1m));
            scan("QuoteQuantityDialog", new QuoteQuantityDialog(new QuoteSaleItem { Name = "SP", UnitName = "Ly" }, 1m));
            scan("RoomEditDialog", new RoomEditDialog(session, roomId, areaId, null));
            scan("AreaEditDialog", new AreaEditDialog(session, areaId, null));

            Check("Nhập số P1 [A12/N2 quét cây control]: không còn NumericUpDown trực tiếp & mọi NumberBox đều bật ThousandsSeparator",
                rawFound.Count == 0 && noThousandsFound.Count == 0 && totalNumberBoxes >= 30,
                "raw=" + rawFound.Count + (rawFound.Count > 0 ? " [" + string.Join(", ", rawFound.ToArray()) + "]" : "")
                + ", noSep=" + noThousandsFound.Count + (noThousandsFound.Count > 0 ? " [" + string.Join(", ", noThousandsFound.ToArray()) + "]" : "")
                + ", numberBoxes=" + totalNumberBoxes);
        }

        private static void ScanRawNumericUpDown(Control root, string path, List<string> rawFound, List<string> noThousandsFound, ref int numberBoxCount)
        {
            if (root.GetType() == typeof(NumericUpDown))
                rawFound.Add(path + "/" + root.Name);
            else if (root is NumberBox)
            {
                numberBoxCount++;
                if (!((NumberBox)root).ThousandsSeparator)
                    noThousandsFound.Add(path + "/" + root.Name);
            }
            foreach (Control c in root.Controls)
                ScanRawNumericUpDown(c, path, rawFound, noThousandsFound, ref numberBoxCount);
        }

        private static NumberBox FindFirstNumberBox(Control root)
        {
            var nb = root as NumberBox;
            if (nb != null)
                return nb;
            foreach (Control c in root.Controls)
            {
                NumberBox found = FindFirstNumberBox(c);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static NumberBox FindNamedNumberBox(Control root, string name)
        {
            var nb = root as NumberBox;
            if (nb != null && string.Equals(nb.Name, name, StringComparison.OrdinalIgnoreCase))
                return nb;
            foreach (Control c in root.Controls)
            {
                NumberBox found = FindNamedNumberBox(c, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static NumberBox FindAnyNumberBoxInConfigDialog(ConfigDialog cfg)
        {
            NumberBox found = FindFirstNumberBox(cfg);
            if (found != null)
                return found;
            ListBox groups = FindFirstListBox(cfg);
            if (groups == null)
                return null;
            for (int i = 0; i < groups.Items.Count; i++)
            {
                groups.SelectedIndex = i;
                Application.DoEvents();
                found = FindFirstNumberBox(cfg);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static ListBox FindFirstListBox(Control root)
        {
            var lb = root as ListBox;
            if (lb != null)
                return lb;
            foreach (Control c in root.Controls)
            {
                ListBox found = FindFirstListBox(c);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Control FindFirstOtherSelectable(Control root, Control exclude)
        {
            foreach (Control c in root.Controls)
            {
                if (c == exclude)
                    continue;
                if (c.Visible && c.Enabled && c.CanSelect && !(c.Parent is NumericUpDown)
                    && (c is TextBox || c is ComboBox || c is DataGridView || c is ListBox || c is Button || c is FlatButton))
                    return c;
                Control inner = FindFirstOtherSelectable(c, exclude);
                if (inner != null)
                    return inner;
            }
            return null;
        }
    }
}
