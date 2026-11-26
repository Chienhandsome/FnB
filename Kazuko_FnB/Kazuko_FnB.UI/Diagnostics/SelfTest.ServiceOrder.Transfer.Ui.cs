using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        // ------------------------------------------------------------------ F. Kiểm thử giao diện thật (MoveMergeTableDialog & ServiceOrderView)

        private static void ServiceOrderTransferUiTests(string db, ConfigRepository cfgRepo, UserSession admin, TransferTestFixture fx)
        {
            ThemeTokens savedTheme = ThemeManager.Current;
            try
            {
                SetTransferConfigInt(db, cfgRepo, fx, "NhapLyDoKhiHuyHoaDon", 30);
                SetTransferConfigInt(db, cfgRepo, fx, "ChoPhepThayDoiHoaDonSauKhiInTamTinh", 30);

                var svc = new TableService(admin);
                ServiceOrderHeader uiBill1 = OpenTransferBill(svc, fx, fx.TableA1Id, fx.ProdDrinkId, 4m, null);
                ServiceOrderHeader uiBill2 = OpenTransferBill(svc, fx, fx.TableA2Id, fx.ProdFoodId, 1m, null);
                List<ServiceAreaInfo> areas = svc.LoadAreas();
                List<ServiceTableInfo> tables = svc.LoadTables();

                // 1. MoveMergeTableDialog chế độ Chuyển bàn: chọn bàn đang dùng chặn, chọn chính bàn nguồn chặn, chọn bàn trống OK, nút >= 44px, đổi theme.
                using (var dlg = new MoveMergeTableDialog(TableTransferMode.MoveTable, fx.TableA1Id, uiBill1.TableName, areas, tables))
                {
                    dlg.SuppressPopup = true;
                    dlg.StartPosition = FormStartPosition.Manual;
                    dlg.Location = new Point(-3000, -3000);
                    dlg.Show();
                    Application.DoEvents();
                    try
                    {
                        bool okBtnTouch = dlg.OkButtonForTest.Height >= 44;

                        // Chọn bàn đang có khách -> bấm Đồng ý -> cảnh báo nguyên văn §3.8.
                        TableTileButton busyTile = dlg.TableMapForTest.FindTileForTest(fx.TableA2Id);
                        busyTile.PerformClick();
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();
                        string warnBusy = dlg.LastWarningForTest;

                        // Chọn chính bàn nguồn -> cảnh báo "Mời bạn chọn bàn khác".
                        TableTileButton selfTile = dlg.TableMapForTest.FindTileForTest(fx.TableA1Id);
                        selfTile.PerformClick();
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();
                        string warnSelf = dlg.LastWarningForTest;

                        ThemeManager.Apply(BuiltInThemes.Ocean);
                        Application.DoEvents();
                        bool themeOk = dlg.TableMapForTest.BackColor == BuiltInThemes.Ocean.Background;

                        // Chọn bàn trống TableB1 -> bấm Đồng ý -> DialogResult.OK và trả đúng bàn đích.
                        TableTileButton emptyTile = dlg.TableMapForTest.FindTileForTest(fx.TableB1Id);
                        emptyTile.PerformClick();
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();

                        Check("SDDV ChuyenGop UI §3.8/§7 MoveMergeTableDialog (Chuyển bàn): chặn bàn đang dùng & trùng bàn đúng nguyên văn, chọn bàn trống OK, nút >= 44px, đổi theme",
                            okBtnTouch && themeOk
                            && warnBusy == ServiceTransferConstants.DestTableInUseForMove
                            && warnSelf == ServiceTransferConstants.ChooseDifferentTable
                            && dlg.Accepted
                            && dlg.SelectedTargetTable != null && dlg.SelectedTargetTable.Id == fx.TableB1Id,
                            "busy=" + warnBusy + ", self=" + warnSelf);
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                // 2. MoveMergeTableDialog chế độ Gộp bàn: chọn bàn trống chặn đúng nguyên văn §3.8, chọn bàn đang dùng OK.
                using (var dlg = new MoveMergeTableDialog(TableTransferMode.MergeTable, fx.TableA1Id, uiBill1.TableName, areas, tables))
                {
                    dlg.SuppressPopup = true;
                    dlg.StartPosition = FormStartPosition.Manual;
                    dlg.Location = new Point(-3000, -3000);
                    dlg.Show();
                    Application.DoEvents();
                    try
                    {
                        TableTileButton emptyTile = dlg.TableMapForTest.FindTileForTest(fx.TableB1Id);
                        emptyTile.PerformClick();
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();
                        string warnEmpty = dlg.LastWarningForTest;

                        TableTileButton busyTile = dlg.TableMapForTest.FindTileForTest(fx.TableA2Id);
                        busyTile.PerformClick();
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();

                        Check("SDDV ChuyenGop UI §3.8/§7 MoveMergeTableDialog (Gộp bàn): chọn bàn trống chặn đúng nguyên văn, chọn bàn đang có khách OK",
                            warnEmpty == ServiceTransferConstants.DestTableEmptyForMerge
                            && dlg.Accepted
                            && dlg.SelectedTargetTable != null && dlg.SelectedTargetTable.Id == fx.TableA2Id,
                            warnEmpty);
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                // 3. MoveMergeTableDialog chế độ Chuyển mặt hàng: bỏ tích "Chuyển toàn bộ", nhập SL=2, chọn bàn đang có khách OK.
                using (var dlg = new MoveMergeTableDialog(TableTransferMode.TransferItem, fx.TableA1Id, uiBill1.TableName, areas, tables, uiBill1.Lines[0]))
                {
                    dlg.SuppressPopup = true;
                    dlg.StartPosition = FormStartPosition.Manual;
                    dlg.Location = new Point(-3000, -3000);
                    dlg.Show();
                    Application.DoEvents();
                    try
                    {
                        dlg.TransferAllCheckForTest.Checked = false;
                        dlg.TransferQtyBoxForTest.Value = 2m;
                        TableTileButton busyTile = dlg.TableMapForTest.FindTileForTest(fx.TableA2Id);
                        busyTile.PerformClick();
                        dlg.OkButtonForTest.PerformClick();
                        Application.DoEvents();

                        Check("SDDV ChuyenGop UI §3.8 MoveMergeTableDialog (Chuyển món): nhập SL=2, chọn bàn đích đang có khách trả đúng SL và bàn đích",
                            dlg.Accepted && dlg.TransferQty == 2m
                            && dlg.SelectedTargetTable != null && dlg.SelectedTargetTable.Id == fx.TableA2Id);
                    }
                    finally { if (!dlg.IsDisposed) dlg.Close(); }
                }

                // 4. ServiceOrderView thật: chọn bàn -> 4 nút patch (c) bật & >= 44px -> bấm Chuyển bàn thật -> bấm Hủy bill thật -> bàn về trống!
                using (var host = MakeOffscreenHost())
                {
                    host.ClientSize = new Size(1360, 760);
                    var view = new ServiceOrderView(admin) { Dock = DockStyle.Fill, SuppressPopup = true };
                    view.TableMapForTest.StopAutoRefresh();
                    host.Controls.Add(view);
                    host.Show();
                    Application.DoEvents();
                    try
                    {
                        TableTileButton tileA1 = view.TableMapForTest.FindTileForTest(fx.TableA1Id);
                        tileA1.PerformClick();
                        Application.DoEvents();

                        bool allEnabledAndTouch = view.MoveTableButtonForTest.Enabled && view.MoveTableButtonForTest.Height >= 44
                            && view.MergeTableButtonForTest.Enabled && view.MergeTableButtonForTest.Height >= 44
                            && view.TransferItemButtonForTest.Enabled && view.TransferItemButtonForTest.Height >= 44
                            && view.CancelBillButtonForTest.Enabled && view.CancelBillButtonForTest.Height >= 44;

                        // Bấm nút Chuyển bàn thật qua sự kiện Click: chuyển uiBill1 từ TableA1 sang TableB1.
                        ServiceTableInfo targetB1 = view.TableMapForTest.FindTable(fx.TableB1Id);
                        view.TargetTableOverrideForTest = () => targetB1;
                        view.MoveTableButtonForTest.PerformClick();
                        Application.DoEvents();
                        bool movedViaUi = view.LastTransferResultForTest != null && view.LastTransferResultForTest.Ok
                            && view.CurrentBillForTest != null && view.CurrentBillForTest.TableId == fx.TableB1Id
                            && view.LastInfoForTest != ServiceOrderConstants.NotSupportedYet;

                        // Bấm nút Hủy bill thật qua sự kiện Click: hủy uiBill1 trên TableB1 -> CurrentBill về null và TableB1 về Trống!
                        view.CancelReasonOverrideForTest = () => "Hủy từ UI selftest";
                        view.CancelBillButtonForTest.PerformClick();
                        Application.DoEvents();
                        TableTransferResult cancelRes = view.LastTransferResultForTest;
                        if (cancelRes != null && !string.IsNullOrEmpty(cancelRes.CancelledBillHuyId))
                            fx.CreatedCancelIds.Add(cancelRes.CancelledBillHuyId);
                        ServiceTableInfo b1After = view.TableMapForTest.FindTable(fx.TableB1Id);

                        Check("SDDV ChuyenGop UI §3.8/§7 ServiceOrderView: 4 nút Chuyển/Gộp/Chuyển món/Hủy bill bật & >= 44px; bấm Chuyển bàn & Hủy bill thật cập nhật sơ đồ bàn về Trống",
                            allEnabledAndTouch && movedViaUi
                            && cancelRes != null && cancelRes.Ok
                            && view.CurrentBillForTest == null
                            && b1After != null && b1After.Status == TableStatus.Empty,
                            "touch=" + allEnabledAndTouch + ", moved=" + movedViaUi + ", status=" + (b1After != null ? b1After.Status.ToString() : "null"));
                    }
                    finally { host.Close(); }
                }

                CleanupSingleBill(db, uiBill2.Id, fx.TableA2Id);
            }
            finally
            {
                ThemeManager.Apply(savedTheme);
            }
        }
    }
}
