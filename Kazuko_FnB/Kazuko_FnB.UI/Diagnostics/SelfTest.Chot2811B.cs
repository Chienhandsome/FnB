using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    /// <summary>Selftest cho spec docs/specs/chot-2811-b.md (mục A: Tạm ứng, B: Tồn đầu; mục C ở SelfTest.Chot2811B.Payroll.cs).</summary>
    public static partial class SelfTest
    {
        private static void Chot2811BTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_2811b",
                    UserName = "selftest_2811b",
                    DisplayName = "Selftest Chot2811B",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                Chot2811BSalaryAdvanceTests(db);
                Chot2811BOpeningStockTests(db);
                Chot2811BPayrollCreateTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        // ------------------------------------------------------------------ Mục A: Tạm ứng lương

        private static void Chot2811BSalaryAdvanceTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string empId = DbHelper.NewId();
            string vEarlyId = DbHelper.NewId();
            string vMidId = DbHelper.NewId();
            string vLateId = DbHelper.NewId();
            DateTime dEarly = new DateTime(2021, 1, 10);
            DateTime dMid = new DateTime(2021, 6, 15);
            DateTime dLate = new DateTime(2022, 11, 20);

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_2811b')",
                    "@I", storeId, "@N", "CH 2811B A " + tag);
                BookingExec(db,
                    "INSERT INTO DNHANVIEN (ID, NAME, CACHTINHLUONG, LUONGCA, LUONGTHANG, NGHICHUNHAT, NGHITHU7, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @N, 30, 150000, 0, 0, 0, 30, 'selftest_2811b', @T)",
                    "@I", empId, "@N", "NV 2811B A " + tag, "@T", now);

                Action<string, string, DateTime, decimal> seedAdv = (id, num, dt, amt) =>
                {
                    BookingExec(db,
                        "INSERT INTO TTHUCHI (ID, NAME, NGAY, LOAI, LOAIDOITUONG, LATAMUNG, DNHANVIENID, TENDOITUONG, DCUAHANGID, DIENGIAI, THU, CHI, CHUYENKHOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                        "VALUES (@I, @N, @D, 1, 1, 30, @E, @EN, @S, @DG, 0, @A, 0, 30, 'selftest_2811b', @T)",
                        "@I", id, "@N", num, "@D", dt, "@E", empId, "@EN", "NV 2811B A " + tag,
                        "@S", storeId, "@DG", "Tạm ứng " + tag, "@A", amt, "@T", now);
                };
                seedAdv(vEarlyId, "PC21/A1-" + tag, dEarly, 100000m);
                seedAdv(vMidId, "PC21/A2-" + tag, dMid, 200000m);
                seedAdv(vLateId, "PC22/A3-" + tag, dLate, 300000m);

                var adminSess = new UserSession
                {
                    UserId = "selftest_2811b",
                    UserName = "selftest_2811b",
                    DisplayName = "Selftest 2811B",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH 2811B A " + tag }
                };

                using (var host = MakeOffscreenHost())
                {
                    var view = new SalaryAdvanceListView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        bool defaultUnchecked = !view.FromDateCheckForTest.Checked
                            && !view.ToDateCheckForTest.Checked
                            && !view.FromDateBoxForTest.NullableValue.HasValue
                            && !view.ToDateBoxForTest.NullableValue.HasValue;

                        view.SearchBoxForTest.Text = tag;
                        Application.DoEvents();
                        bool allVisible = view.GridForTest.Rows.Count == 3
                            && view.TotalAmountForTest == 600000m
                            && HasAdvanceRow(view.GridForTest, vEarlyId)
                            && HasAdvanceRow(view.GridForTest, vMidId)
                            && HasAdvanceRow(view.GridForTest, vLateId);

                        Check("Chốt 28/11-B §A #1 (Tạm ứng mở màn không lọc): hai ô Từ/Đến mặc định bỏ tích và trống, thấy đủ 3 phiếu nhiều năm",
                            defaultUnchecked && allVisible,
                            SingleLine("defUnchk=" + defaultUnchecked + ", rows=" + view.GridForTest.Rows.Count + ", tot=" + view.TotalAmountForTest));

                        // Tích Từ = 15/06/2021 (Đến bỏ tích) -> giữ 2 phiếu (dMid, dLate).
                        view.FromDateCheckForTest.Checked = true;
                        view.FromDateBoxForTest.Value = dMid;
                        Application.DoEvents();
                        bool fromOnlyOk = view.FromDateCheckForTest.Checked
                            && !view.ToDateCheckForTest.Checked
                            && view.GridForTest.Rows.Count == 2
                            && view.TotalAmountForTest == 500000m
                            && !HasAdvanceRow(view.GridForTest, vEarlyId)
                            && HasAdvanceRow(view.GridForTest, vMidId)
                            && HasAdvanceRow(view.GridForTest, vLateId);

                        Check("Chốt 28/11-B §A #2 (Tạm ứng tích Từ ngày): tích Từ=15/06/2021, bỏ tích Đến chỉ lọc các phiếu từ ngày đó",
                            fromOnlyOk,
                            SingleLine("rows=" + view.GridForTest.Rows.Count + ", tot=" + view.TotalAmountForTest));

                        // Bỏ tích Từ, tích Đến = 15/06/2021 -> giữ 2 phiếu (dEarly, dMid gồm cả ngày Đến).
                        view.FromDateCheckForTest.Checked = false;
                        view.ToDateCheckForTest.Checked = true;
                        view.ToDateBoxForTest.Value = dMid;
                        Application.DoEvents();
                        bool toOnlyOk = !view.FromDateCheckForTest.Checked
                            && view.ToDateCheckForTest.Checked
                            && view.GridForTest.Rows.Count == 2
                            && view.TotalAmountForTest == 300000m
                            && HasAdvanceRow(view.GridForTest, vEarlyId)
                            && HasAdvanceRow(view.GridForTest, vMidId)
                            && !HasAdvanceRow(view.GridForTest, vLateId);

                        Check("Chốt 28/11-B §A #3 (Tạm ứng tích Đến ngày): bỏ tích Từ, tích Đến=15/06/2021 lọc đúng gồm cả ngày Đến",
                            toOnlyOk,
                            SingleLine("rows=" + view.GridForTest.Rows.Count + ", tot=" + view.TotalAmountForTest));

                        // Tích cả hai: Từ = 01/06/2021, Đến = 30/06/2021 -> chỉ 1 phiếu dMid; bỏ tích cả hai -> lại đủ 3 phiếu.
                        view.FromDateCheckForTest.Checked = true;
                        view.FromDateBoxForTest.Value = new DateTime(2021, 6, 1);
                        view.ToDateBoxForTest.Value = new DateTime(2021, 6, 30);
                        Application.DoEvents();
                        bool bothRangeOk = view.FromDateCheckForTest.Checked
                            && view.ToDateCheckForTest.Checked
                            && view.GridForTest.Rows.Count == 1
                            && view.TotalAmountForTest == 200000m
                            && HasAdvanceRow(view.GridForTest, vMidId);

                        view.FromDateCheckForTest.Checked = false;
                        view.ToDateCheckForTest.Checked = false;
                        Application.DoEvents();
                        bool resetAllOk = view.GridForTest.Rows.Count == 3 && view.TotalAmountForTest == 600000m;

                        Check("Chốt 28/11-B §A #4 (Tạm ứng tích cả Từ và Đến): lọc đúng khoảng giữa và bỏ tích hiện lại đủ 3 phiếu",
                            bothRangeOk && resetAllOk,
                            SingleLine("both=" + bothRangeOk + ", reset=" + resetAllOk));
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                SafeDeleteById(db, "TTHUCHI", vEarlyId);
                SafeDeleteById(db, "TTHUCHI", vMidId);
                SafeDeleteById(db, "TTHUCHI", vLateId);
                SafeDeleteById(db, "DNHANVIEN", empId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }

        private static bool HasAdvanceRow(DataGridView grid, string id)
        {
            foreach (DataGridViewRow r in grid.Rows)
            {
                var row = r.Tag as VoucherRow;
                if (row != null && string.Equals(row.Id, id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ Mục B: Tồn kho ban đầu

        private static void Chot2811BOpeningStockTests(string db)
        {
            string tag = DbHelper.NewId().Substring(0, 8);
            string storeId = DbHelper.NewId();
            string whId = DbHelper.NewId();
            string unitId = DbHelper.NewId();
            string prod1 = DbHelper.NewId();
            string prod2 = DbHelper.NewId();
            string barcode1 = "89311" + tag;
            string barcode2 = "89322" + tag;

            try
            {
                DateTime now = DbHelper.Now();
                BookingExec(db, "INSERT INTO DCUAHANG (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_2811b')",
                    "@I", storeId, "@N", "CH 2811B B " + tag);
                BookingExec(db, "INSERT INTO DKHOHANG (ID, NAME, DCUAHANGID, CHOPHEPAMKHO, STATUS, USERCREATEDID, TIMECREATED) VALUES (@I, @N, @S, 30, 30, 'selftest_2811b', @T)",
                    "@I", whId, "@N", "Kho 2811B B " + tag, "@S", storeId, "@T", now);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = @W WHERE ID = @I", "@W", whId, "@I", storeId);
                BookingExec(db, "INSERT INTO DDONVITINH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_2811b')",
                    "@I", unitId, "@N", "Hộp " + tag);

                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, MASANCO, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @B, @N, '0', @U, 10000, 20000, 30, 'selftest_2811b', @T)",
                    "@I", prod1, "@C", "OS1-" + tag, "@B", barcode1, "@N", "Hàng Mã Sẵn 1 " + tag, "@U", unitId, "@T", now);
                BookingExec(db,
                    "INSERT INTO DMATHANG (ID, CODE, MASANCO, NAME, DLOAIMATHANGID, DDONVITINHID, GIAVON, GIABAN, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @B, @N, '2', @U, 25000, 40000, 30, 'selftest_2811b', @T)",
                    "@I", prod2, "@C", "OS2-" + tag, "@B", barcode2, "@N", "Hàng Mã Sẵn 2 " + tag, "@U", unitId, "@T", now);

                var adminSess = new UserSession
                {
                    UserId = "selftest_2811b",
                    UserName = "selftest_2811b",
                    DisplayName = "Selftest 2811B",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true),
                    CurrentStore = new StoreRecord { Id = storeId, Name = "CH 2811B B " + tag }
                };

                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1366, 768);
                    var view = new OpeningStockView(adminSess) { Dock = DockStyle.Fill, SuppressPopup = true };
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        view.SearchBoxForTest.Text = tag;
                        Application.DoEvents();

                        DataGridView grid = view.GridForTest;
                        DataGridViewColumn colCode = grid.Columns["colCode"];
                        DataGridViewColumn colBarcode = grid.Columns["colBarcode"];
                        DataGridViewRow r1 = view.FindRowByProductIdForTest(prod1);
                        DataGridViewRow r2 = view.FindRowByProductIdForTest(prod2);

                        int totalColW = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
                        foreach (DataGridViewColumn c in grid.Columns)
                            if (c.Visible) totalColW += c.Width;
                        bool noHScroll1366 = totalColW <= grid.ClientSize.Width;

                        bool colPresentAndValOk = colCode != null && colBarcode != null
                            && colBarcode.HeaderText == "Mã sẵn có"
                            && colBarcode.Index == colCode.Index + 1
                            && colBarcode.ReadOnly
                            && r1 != null && Convert.ToString(r1.Cells["colBarcode"].Value) == barcode1
                            && r2 != null && Convert.ToString(r2.Cells["colBarcode"].Value) == barcode2
                            && noHScroll1366;

                        Check("Chốt 28/11-B §B #1 (Tồn đầu cột Mã sẵn có): có mặt ngay sau Mã hàng, chỉ đọc, đúng MASANCO, không cuộn ngang 1366x768",
                            colPresentAndValOk,
                            SingleLine("idx=" + (colBarcode == null ? -1 : colBarcode.Index) + ", b1=" + (r1 == null ? "" : Convert.ToString(r1.Cells["colBarcode"].Value)) + ", colW=" + totalColW + "/" + grid.ClientSize.Width));

                        // Tìm theo MASANCO -> chỉ ra đúng prod2.
                        view.SearchBoxForTest.Text = barcode2;
                        Application.DoEvents();
                        bool searchBarcodeOk = view.FindRowByProductIdForTest(prod2) != null
                            && view.FindRowByProductIdForTest(prod1) == null;

                        Check("Chốt 28/11-B §B #2 (Tồn đầu tìm theo mã sẵn có): gõ MASANCO lọc ra đúng dòng tương ứng",
                            searchBarcodeOk,
                            SingleLine("found2=" + (view.FindRowByProductIdForTest(prod2) != null) + ", found1=" + (view.FindRowByProductIdForTest(prod1) != null)));

                        // Hộp Tổng giá trị dưới lưới: chỉ đọc, bằng tổng Thành tiền và đổi khi sửa Tồn đầu / Giá vốn.
                        view.SearchBoxForTest.Text = tag;
                        Application.DoEvents();
                        r1 = view.FindRowByProductIdForTest(prod1);
                        r2 = view.FindRowByProductIdForTest(prod2);

                        bool totalInitOk = view.BottomBarForTest.Visible
                            && view.TotalValueLabelForTest.Text.IndexOf("Tổng giá trị", StringComparison.Ordinal) >= 0
                            && view.TotalValueBoxForTest.ReadOnly
                            && view.TotalValueBoxForTest.Value == 0m;

                        if (r1 != null && r2 != null)
                        {
                            view.CommitCellEditForTest(r1.Index, "colQty", 4m);
                            view.CommitCellEditForTest(r2.Index, "colQty", 2m);
                            Application.DoEvents();
                        }
                        DataGridViewRow sumRow = grid.Rows.Count >= 3 ? grid.Rows[grid.Rows.Count - 1] : null;
                        bool afterQtyOk = view.TotalValueBoxForTest.Value == 90000m
                            && sumRow != null && Convert.ToDecimal(sumRow.Cells["colAmount"].Value) == 90000m;

                        if (r1 != null)
                        {
                            view.CommitCellEditForTest(r1.Index, "colCost", 15000m);
                            Application.DoEvents();
                        }
                        bool afterCostOk = view.TotalValueBoxForTest.Value == 110000m
                            && sumRow != null && Convert.ToDecimal(sumRow.Cells["colAmount"].Value) == 110000m;

                        Check("Chốt 28/11-B §B #3 (Tồn đầu hộp Tổng giá trị): ô số chỉ đọc dưới lưới đồng bộ hàng Tổng cộng và đổi theo khi sửa SL/Giá vốn",
                            totalInitOk && afterQtyOk && afterCostOk,
                            SingleLine("init=" + totalInitOk + ", qty90k=" + afterQtyOk + ", cost110k=" + afterCostOk + ", boxVal=" + view.TotalValueBoxForTest.Value));
                    }
                    finally { host.Close(); }
                }
            }
            finally
            {
                CleanupOpeningWarehouseOrders(db, new[] { whId });
                SafeDeleteById(db, "DMATHANG", prod1);
                SafeDeleteById(db, "DMATHANG", prod2);
                SafeDeleteById(db, "DDONVITINH", unitId);
                BookingExec(db, "UPDATE DCUAHANG SET DKHOHANGID = NULL WHERE ID = @I", "@I", storeId);
                SafeDeleteById(db, "DKHOHANG", whId);
                SafeDeleteById(db, "DCUAHANG", storeId);
            }
        }
    }
}
