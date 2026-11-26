using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP2BookingUiTests(
            string db,
            UserSession adminSession,
            P2PrintFixture fx,
            List<PrintTemplateRecord> orderedBookingTemplates,
            string sampleBookingId)
        {
            // 1. Hộp "IN BÁO CÁO" cho Đặt hàng: tiêu đề, ComboBox 5 mẫu đúng thứ tự, chọn sẵn A4, Thực hiện/Thoát, không có Gửi mail & Thiết kế.
            var printers = new List<string> { "Máy in 80mm giả", "Máy in A4 giả" };
            using (var dlg = new PrintTemplateDialog(orderedBookingTemplates, printers, "Máy in A4 giả", PrintDialogOptions.ForBookingReportDialog()))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.ShowInTaskbar = false;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                dlg.PerformLayout();
                Application.DoEvents();
                try
                {
                    bool titleOk = dlg.Text == PrintConstants.PrintReportDialogTitle;
                    bool comboOk = dlg.TemplateComboForTest.Visible
                        && dlg.TemplateComboForTest.Items.Count == 5
                        && string.Equals(Convert.ToString(dlg.TemplateComboForTest.SelectedItem), PrintConstants.DefaultBookingTemplateName, StringComparison.OrdinalIgnoreCase);
                    bool btnsOk = dlg.OkButtonForTest.Text == "Thực hiện" && dlg.CancelButtonForTest.Text == "Thoát";
                    bool noMailOrDesign = !ContainsTextRecursive(dlg, "Gửi mail") && !ContainsTextRecursive(dlg, "Thiết kế");
                    bool defaultPreviewOk = dlg.RadioPreviewForTest.Checked && !dlg.PrinterComboForTest.Enabled;
                    bool noScrollOk = !dlg.BodyPanelForTest.AutoScroll && !dlg.BodyPanelForTest.VerticalScroll.Visible;

                    Check("In P2 UI hộp IN BÁO CÁO (Đặt hàng): tiêu đề, ô thả 5 mẫu chọn sẵn A4, Thực hiện/Thoát, không Gửi mail & Thiết kế",
                        titleOk && comboOk && btnsOk && noMailOrDesign && defaultPreviewOk && noScrollOk,
                        "title=" + titleOk + ",combo=" + comboOk + ",btns=" + btnsOk + ",noMD=" + noMailOrDesign + ",prev=" + defaultPreviewOk + ",scroll=" + noScrollOk);
                }
                finally { dlg.Close(); }
            }

            // Fallback chọn mẫu đầu khi không có mẫu mặc định theo tên.
            var fallbackList = new List<PrintTemplateRecord>
            {
                new PrintTemplateRecord { Id = "f1", Name = "Mẫu đặt hàng 80 mm" },
                new PrintTemplateRecord { Id = "f2", Name = "Mẫu đặt hàng A5" }
            };
            using (var dlgFb = new PrintTemplateDialog(fallbackList, printers, "Máy in A4 giả", PrintDialogOptions.ForBookingReportDialog()))
            {
                Check("In P2 UI hộp IN BÁO CÁO (fallback mẫu đầu): không có 'Mẫu đặt hàng A4' thì chọn mẫu đầu",
                    dlgFb.TemplateComboForTest.SelectedIndex == 0);
            }

            // 2. BookingEditDialog: lưu hỏng (thiếu người đặt / thiếu số phiếu) thì KHÔNG gọi in.
            using (var dlg = OpenOffscreenBookingDialog(adminSession))
            {
                try
                {
                    var rec = new CapturingPrintOutputRecorder();
                    int pickerCalls = 0;
                    dlg.PrintOutputHandlerForTest = rec;
                    dlg.PrintTemplatePickerForTest = delegate (IWin32Window o, IList<PrintTemplateRecord> list)
                    {
                        pickerCalls++;
                        return new PrintSelectionResult { Template = list[0], Action = PrintActionKind.Preview };
                    };

                    bool cleanDisabled = !dlg.SavePrintButtonForTest.Enabled && !dlg.SaveViewPrintButtonForTest.Enabled;
                    dlg.SetRoomsForTest(new List<string> { fx.Room1Id }, new List<string> { "Phòng VIP 1" });
                    bool dirtyEnabled = dlg.SavePrintButtonForTest.Enabled && dlg.SaveViewPrintButtonForTest.Enabled;

                    // Thiếu Người đặt -> bấm cả 2 nút đều bị chặn, KHÔNG gọi in.
                    dlg.TenKhachBoxForTest.Text = "";
                    dlg.SavePrintButtonForTest.PerformClick();
                    bool warnMissingName = dlg.LastWarningForTest == BookingText.Required("Người đặt");
                    dlg.SaveViewPrintButtonForTest.PerformClick();
                    bool noPrintOnMissingName = rec.PrintCount == 0 && rec.PreviewCount == 0 && pickerCalls == 0;

                    // Thiếu Số phiếu -> bấm Lưu & In bị chặn, KHÔNG gọi in.
                    dlg.TenKhachBoxForTest.Text = "Khách hợp lệ";
                    dlg.NumberBoxForTest.Text = "";
                    dlg.SavePrintButtonForTest.PerformClick();
                    bool warnMissingNum = dlg.LastWarningForTest == BookingText.Required("Số phiếu") && rec.PrintCount == 0;

                    Check("In P2 UI BookingEditDialog (bật/tắt & lưu hỏng không in): theo nút Lưu; thiếu người đặt/số phiếu không gọi in",
                        cleanDisabled && dirtyEnabled && warnMissingName && noPrintOnMissingName && warnMissingNum,
                        "clean=" + cleanDisabled + ",dirty=" + dirtyEnabled + ",name=" + warnMissingName + ",num=" + warnMissingNum);

                    // 3. Lưu & In: sửa Phí VC lên 25.000 -> tự lưu trước rồi in thẳng mẫu A4 (KHÔNG hiện hộp), trang in có số mới, hộp vẫn mở.
                    dlg.NumberBoxForTest.Text = "DH26/P2UI1";
                    dlg.TenKhachBoxForTest.Text = "Khách Lưu & In";
                    SelectBookingProductRow(dlg.ProductGridForTest, fx.CoffeeId);
                    dlg.QtyBoxForTest.Value = 4m;
                    dlg.AddProductButtonForTest.PerformClick(); // 4 x 25.000 = 100.000
                    dlg.PhiVanChuyenBoxForTest.Value = 25000m;   // tổng = 125.000
                    Application.DoEvents();

                    dlg.SavePrintButtonForTest.PerformClick();
                    Application.DoEvents();
                    if (!string.IsNullOrEmpty(dlg.SavedId))
                        fx.CreatedBookingIds.Add(dlg.SavedId);

                    bool directOk = !string.IsNullOrEmpty(dlg.SavedId)
                        && pickerCalls == 0
                        && rec.PrintCount == 1
                        && rec.PreviewCount == 0
                        && rec.LastTemplate != null
                        && rec.LastTemplate.Name == PrintConstants.DefaultBookingTemplateName
                        && PageTextsContain(rec.LastPageTexts, "125")
                        && !dlg.IsDisposed && dlg.Visible;

                    // 4. Lưu & Xem in: sửa Phí VC lên 35.000 (tổng 135.000) -> tự lưu trước, hiện hộp chọn A5 -> in A5 có số mới 135.000, khóa bấm 2 lần.
                    dlg.PhiVanChuyenBoxForTest.Value = 35000m;
                    Application.DoEvents();
                    bool reentrantBlocked = false;
                    dlg.PrintTemplatePickerForTest = delegate (IWin32Window o, IList<PrintTemplateRecord> list)
                    {
                        pickerCalls++;
                        reentrantBlocked = !dlg.SaveAndViewPrint();
                        PrintTemplateRecord a5 = FindTemplateByName(list, "Mẫu đặt hàng A5");
                        return new PrintSelectionResult { Template = a5, Action = PrintActionKind.Preview };
                    };
                    dlg.SaveViewPrintButtonForTest.PerformClick();
                    Application.DoEvents();

                    BookingRow savedBack = new BookingRepository(db).LoadBooking(dlg.SavedId);
                    bool viewPrintA5Ok = pickerCalls == 1
                        && reentrantBlocked
                        && rec.PreviewCount == 1
                        && rec.LastTemplate != null
                        && rec.LastTemplate.Name == "Mẫu đặt hàng A5"
                        && savedBack != null && savedBack.TongCong == 135000m
                        && PageTextsContain(rec.LastPageTexts, "135")
                        && !dlg.IsDisposed && dlg.Visible;

                    Check("In P2 UI BookingEditDialog (Lưu & In + Lưu & Xem in): tự lưu số mới, Lưu & In in thẳng A4, Lưu & Xem in chọn A5, hộp vẫn mở, khóa bấm lặp",
                        directOk && viewPrintA5Ok,
                        "dir=" + directOk + ",vp=" + viewPrintA5Ok + ",re=" + reentrantBlocked);

                    // 5. Mẫu hỏng trên Lưu & In: phiếu VẪN đã lưu, báo lỗi nêu tên mẫu, không văng.
                    dlg.PhiVanChuyenBoxForTest.Value = 40000m; // tổng 140.000
                    dlg.PrintTemplateLoaderForTest = delegate
                    {
                        return new List<PrintTemplateRecord>
                        {
                            new PrintTemplateRecord { Id = "broken", Name = "Mẫu ĐH lỗi test", TemplateXml = "<Report><Bad" }
                        };
                    };
                    dlg.SavePrintButtonForTest.PerformClick();
                    Application.DoEvents();
                    BookingRow savedAfterBroken = new BookingRepository(db).LoadBooking(dlg.SavedId);
                    bool brokenOk = savedAfterBroken != null && savedAfterBroken.TongCong == 140000m
                        && rec.ErrorMessages.Count > 0
                        && rec.ErrorMessages[rec.ErrorMessages.Count - 1].IndexOf("Mẫu ĐH lỗi test", StringComparison.Ordinal) >= 0;
                    Check("In P2 UI BookingEditDialog (mẫu hỏng khi Lưu & In): phiếu vẫn đã lưu & báo lỗi nêu tên mẫu",
                        brokenOk, rec.ErrorMessages.Count > 0 ? rec.ErrorMessages[rec.ErrorMessages.Count - 1] : "no-err");
                }
                finally { dlg.Close(); }
            }

            // 6. Chế độ chỉ đọc (chỉ có quyền Xem): cả 2 nút Lưu & In / Lưu & Xem in tắt như nút Lưu.
            var viewOnlyPerms = new FunctionPermissions(false);
            viewOnlyPerms.Add(BookingText.FunctionId, BookingText.FunctionName, RoleModes.View);
            var viewOnlySession = new UserSession
            {
                UserId = "u_dh_ro", UserName = "u_dh_ro", DisplayName = "Chỉ xem ĐH",
                IsAdmin = false, DatabasePath = db, Permissions = viewOnlyPerms
            };
            UserSession prev = AppSession.Current;
            AppSession.Current = viewOnlySession;
            try
            {
                using (var dlgRo = new BookingEditDialog(viewOnlySession, sampleBookingId, null))
                {
                    dlgRo.SuppressPopup = true;
                    Check("In P2 UI BookingEditDialog (chế độ chỉ đọc): nút Lưu & In và Lưu & Xem in tắt như nút Lưu",
                        !dlgRo.SaveButtonForTest.Enabled
                        && !dlgRo.SavePrintButtonForTest.Enabled
                        && !dlgRo.SaveViewPrintButtonForTest.Enabled);
                }
            }
            finally { AppSession.Current = prev; }

            // 7. BookingListView nút In: không chọn dòng -> báo câu chuẩn; có chọn dòng -> mở hộp & in phiếu đang chọn.
            using (var host = MakeOffscreenHost())
            {
                var view = new BookingListView(db) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.FromDateBoxForTest.Value = new DateTime(2026, 11, 1);
                    view.ToDateBoxForTest.Value = new DateTime(2026, 11, 30);
                    view.ReloadForTest();
                    Application.DoEvents();

                    var listRec = new CapturingPrintOutputRecorder();
                    int listPickerCalls = 0;
                    view.PrintOutputHandlerForTest = listRec;
                    view.PrintTemplatePickerForTest = delegate (IWin32Window o, IList<PrintTemplateRecord> list)
                    {
                        listPickerCalls++;
                        return new PrintSelectionResult { Template = FindTemplateByName(list, PrintConstants.DefaultBookingTemplateName), Action = PrintActionKind.Preview };
                    };

                    // Không chọn dòng -> báo câu chuẩn, không mở picker.
                    view.GridForTest.ClearSelection();
                    view.PrintButtonForTest.PerformClick();
                    bool noSelOk = view.LastInfoMessageForTest == PrintConstants.SelectBookingToPrintMessage
                        && listPickerCalls == 0 && listRec.PreviewCount == 0;

                    // Chọn đúng phiếu mẫu -> bấm In mở hộp và in phiếu đó.
                    bool selectedSample = false;
                    for (int r = 0; r < view.GridForTest.Rows.Count; r++)
                    {
                        var tag = view.GridForTest.Rows[r].Tag as BookingListRow;
                        if (tag != null && tag.Id == sampleBookingId)
                        {
                            view.GridForTest.Rows[r].Selected = true;
                            view.GridForTest.CurrentCell = view.GridForTest.Rows[r].Cells[0];
                            selectedSample = true;
                            break;
                        }
                    }
                    view.PrintButtonForTest.PerformClick();
                    Application.DoEvents();
                    bool selOk = selectedSample && listPickerCalls == 1 && listRec.PreviewCount == 1
                        && PageTextsContain(listRec.LastPageTexts, "DH26/P2001");

                    Check("In P2 UI BookingListView (nút In danh sách): không chọn báo chuẩn; có chọn mở hộp và in phiếu đang chọn",
                        noSelOk && selOk, "noSel=" + noSelOk + ",sel=" + selOk);
                }
                finally { host.Close(); }
            }
        }
    }
}
