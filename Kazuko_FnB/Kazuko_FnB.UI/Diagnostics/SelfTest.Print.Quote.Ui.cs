using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void PrintP2QuoteUiTests(
            string db,
            UserSession adminSession,
            P2PrintFixture fx,
            List<PrintTemplateRecord> orderedQuoteTemplates,
            string sampleQuoteId)
        {
            // 1. Hộp "IN BÁO CÁO" cho Báo giá: tiêu đề, ô thả 2 mẫu chọn sẵn "Báo Giá A4", Thực hiện/Thoát, không Gửi mail & Thiết kế.
            var printers = new List<string> { "Máy in A4 giả" };
            using (var dlg = new PrintTemplateDialog(orderedQuoteTemplates, printers, "Máy in A4 giả", PrintDialogOptions.ForQuoteReportDialog()))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.ShowInTaskbar = false;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                dlg.PerformLayout();
                Application.DoEvents();
                try
                {
                    bool ok = dlg.Text == PrintConstants.PrintReportDialogTitle
                        && dlg.TemplateComboForTest.Visible
                        && dlg.TemplateComboForTest.Items.Count == 2
                        && string.Equals(Convert.ToString(dlg.TemplateComboForTest.SelectedItem), PrintConstants.DefaultQuoteTemplateName, StringComparison.OrdinalIgnoreCase)
                        && dlg.OkButtonForTest.Text == "Thực hiện"
                        && dlg.CancelButtonForTest.Text == "Thoát"
                        && dlg.RadioPreviewForTest.Checked
                        && !ContainsTextRecursive(dlg, "Gửi mail")
                        && !ContainsTextRecursive(dlg, "Thiết kế");
                    Check("In P2 UI hộp IN BÁO CÁO (Báo giá): tiêu đề, ô thả 2 mẫu chọn sẵn Báo Giá A4, Thực hiện/Thoát, không Gửi mail & Thiết kế", ok);
                }
                finally { dlg.Close(); }
            }

            // 2. QuoteEditDialog: bật/tắt theo nút Lưu, lưu hỏng (thiếu số phiếu) không gọi in, Lưu & In in thẳng, Lưu & Xem in chọn mẫu 2.
            using (var dlg = new QuoteEditDialog(adminSession, null, null))
            {
                dlg.SuppressPopup = true;
                dlg.WindowState = FormWindowState.Normal;
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    var rec = new CapturingPrintOutputRecorder();
                    int pickerCalls = 0;
                    dlg.PrintOutputHandlerForTest = rec;
                    dlg.PrintTemplatePickerForTest = delegate (IWin32Window o, IList<PrintTemplateRecord> list)
                    {
                        pickerCalls++;
                        return new PrintSelectionResult { Template = list[list.Count - 1], Action = PrintActionKind.Preview };
                    };

                    bool cleanDisabled = !dlg.SavePrintButtonForTest.Enabled && !dlg.SaveViewPrintButtonForTest.Enabled;
                    dlg.TenKhachBoxForTest.Text = "Khách thử";
                    dlg.NumberBoxForTest.Text = "";
                    bool dirtyEnabled = dlg.SavePrintButtonForTest.Enabled && dlg.SaveViewPrintButtonForTest.Enabled;

                    // Thiếu Số phiếu -> cả hai nút không gọi in.
                    dlg.SavePrintButtonForTest.PerformClick();
                    bool warn1 = dlg.LastWarningMessageForTest == QuoteText.Required("Số phiếu");
                    dlg.SaveViewPrintButtonForTest.PerformClick();
                    bool noPrintOnFail = rec.PrintCount == 0 && rec.PreviewCount == 0 && pickerCalls == 0;

                    Check("In P2 UI QuoteEditDialog (bật/tắt & lưu hỏng không in): theo nút Lưu; thiếu số phiếu không gọi in",
                        cleanDisabled && dirtyEnabled && warn1 && noPrintOnFail,
                        "clean=" + cleanDisabled + ",dirty=" + dirtyEnabled + ",warn=" + warn1);

                    // Lưu & In: thêm hàng + phí VC 15.000 (100.000 + 15.000 = 115.000) -> tự lưu, in thẳng Báo Giá A4, hộp vẫn mở.
                    dlg.NumberBoxForTest.Text = "BG26/P2UI1";
                    dlg.TenKhachBoxForTest.Text = "Khách BG Lưu & In";
                    SelectProductRow(dlg.ProductGridForTest, fx.CoffeeId);
                    dlg.QtyBoxForTest.Value = 4m;
                    dlg.AddProductButtonForTest.PerformClick();
                    dlg.PhiVanChuyenBoxForTest.Value = 15000m;
                    Application.DoEvents();

                    dlg.SavePrintButtonForTest.PerformClick();
                    Application.DoEvents();
                    if (!string.IsNullOrEmpty(dlg.SavedId))
                        fx.CreatedQuoteIds.Add(dlg.SavedId);

                    bool directOk = !string.IsNullOrEmpty(dlg.SavedId)
                        && pickerCalls == 0
                        && rec.PrintCount == 1
                        && rec.PreviewCount == 0
                        && rec.LastTemplate != null
                        && string.Equals(rec.LastTemplate.Name, PrintConstants.DefaultQuoteTemplateName, StringComparison.OrdinalIgnoreCase)
                        && PageTextsContain(rec.LastPageTexts, "115")
                        && !dlg.IsDisposed && dlg.Visible;

                    // Lưu & Xem in: sửa phí VC thành 25.000 (125.000) -> tự lưu trước, hiện hộp chọn mẫu 2, khóa bấm lặp.
                    dlg.PhiVanChuyenBoxForTest.Value = 25000m;
                    Application.DoEvents();
                    bool reentrantBlocked = false;
                    dlg.PrintTemplatePickerForTest = delegate (IWin32Window o, IList<PrintTemplateRecord> list)
                    {
                        pickerCalls++;
                        reentrantBlocked = !dlg.SaveAndViewPrint();
                        PrintTemplateRecord other = list[0];
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (!string.Equals((list[i].Name ?? "").Trim(), PrintConstants.DefaultQuoteTemplateName, StringComparison.OrdinalIgnoreCase))
                            {
                                other = list[i];
                                break;
                            }
                        }
                        return new PrintSelectionResult { Template = other, Action = PrintActionKind.Preview };
                    };
                    dlg.SaveViewPrintButtonForTest.PerformClick();
                    Application.DoEvents();

                    QuoteRow savedBack = new QuoteRepository(db).LoadQuote(dlg.SavedId);
                    bool viewPrintOk = pickerCalls == 1
                        && reentrantBlocked
                        && rec.PreviewCount == 1
                        && rec.LastTemplate != null
                        && rec.LastTemplate.Name != PrintConstants.DefaultQuoteTemplateName
                        && savedBack != null && savedBack.TongCong == 125000m
                        && PageTextsContain(rec.LastPageTexts, "125")
                        && !dlg.IsDisposed && dlg.Visible;

                    Check("In P2 UI QuoteEditDialog (Lưu & In + Lưu & Xem in): tự lưu số mới, Lưu & In in thẳng A4, Lưu & Xem in chọn mẫu 2, hộp vẫn mở",
                        directOk && viewPrintOk, "dir=" + directOk + ",vp=" + viewPrintOk + ",re=" + reentrantBlocked);

                    // Mẫu hỏng trên Lưu & In: phiếu VẪN đã lưu, báo lỗi nêu tên mẫu, không văng.
                    dlg.PhiVanChuyenBoxForTest.Value = 30000m; // 130.000
                    dlg.PrintTemplateLoaderForTest = delegate
                    {
                        return new List<PrintTemplateRecord>
                        {
                            new PrintTemplateRecord { Id = "badbg", Name = "Mẫu BG lỗi test", TemplateXml = "<Report><Invalid" }
                        };
                    };
                    dlg.SavePrintButtonForTest.PerformClick();
                    Application.DoEvents();
                    QuoteRow savedAfterBroken = new QuoteRepository(db).LoadQuote(dlg.SavedId);
                    bool brokenOk = savedAfterBroken != null && savedAfterBroken.TongCong == 130000m
                        && rec.ErrorMessages.Count > 0
                        && rec.ErrorMessages[rec.ErrorMessages.Count - 1].IndexOf("Mẫu BG lỗi test", StringComparison.Ordinal) >= 0;
                    Check("In P2 UI QuoteEditDialog (mẫu hỏng khi Lưu & In): phiếu vẫn đã lưu & báo lỗi nêu tên mẫu",
                        brokenOk, rec.ErrorMessages.Count > 0 ? rec.ErrorMessages[rec.ErrorMessages.Count - 1] : "no-err");
                }
                finally { dlg.Close(); }
            }

            // 3. Chế độ chỉ đọc (chỉ có quyền Xem): cả 2 nút Lưu & In / Lưu & Xem in tắt như nút Lưu.
            var viewOnlyPerms = new FunctionPermissions(false);
            viewOnlyPerms.Add(QuoteConstants.FunctionId, QuoteConstants.FunctionName, RoleModes.View);
            var viewOnlySession = new UserSession
            {
                UserId = "u_bg_ro", UserName = "u_bg_ro", DisplayName = "Chỉ xem BG",
                IsAdmin = false, DatabasePath = db, Permissions = viewOnlyPerms
            };
            using (var dlgRo = new QuoteEditDialog(viewOnlySession, sampleQuoteId, null))
            {
                dlgRo.SuppressPopup = true;
                Check("In P2 UI QuoteEditDialog (chế độ chỉ đọc): nút Lưu & In và Lưu & Xem in tắt như nút Lưu",
                    !dlgRo.SaveButtonForTest.Enabled
                    && !dlgRo.SavePrintButtonForTest.Enabled
                    && !dlgRo.SaveViewPrintButtonForTest.Enabled);
            }

            // 4. QuoteListView nút In: không chọn dòng -> báo câu chuẩn; có chọn dòng -> mở hộp và in phiếu đang chọn.
            using (var host = MakeOffscreenHost())
            {
                var view = new QuoteListView(adminSession) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    view.FromDateBoxForTest.Value = new DateTime(2026, 11, 1);
                    view.ToDateBoxForTest.Value = new DateTime(2026, 11, 30);
                    view.Reload();
                    Application.DoEvents();

                    var listRec = new CapturingPrintOutputRecorder();
                    int listPickerCalls = 0;
                    view.PrintOutputHandlerForTest = listRec;
                    view.PrintTemplatePickerForTest = delegate (IWin32Window o, IList<PrintTemplateRecord> list)
                    {
                        listPickerCalls++;
                        return new PrintSelectionResult { Template = FindTemplateByName(list, PrintConstants.DefaultQuoteTemplateName), Action = PrintActionKind.Preview };
                    };

                    view.GridForTest.ClearSelection();
                    view.PrintButtonForTest.PerformClick();
                    bool noSelOk = view.LastInfoMessageForTest == PrintConstants.SelectQuoteToPrintMessage
                        && listPickerCalls == 0 && listRec.PreviewCount == 0;

                    bool selectedSample = false;
                    for (int r = 0; r < view.GridForTest.Rows.Count; r++)
                    {
                        var tag = view.GridForTest.Rows[r].Tag as QuoteListRow;
                        if (tag != null && tag.Id == sampleQuoteId)
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
                        && PageTextsContain(listRec.LastPageTexts, "BG26/P2001");

                    Check("In P2 UI QuoteListView (nút In danh sách): không chọn báo chuẩn; có chọn mở hộp và in phiếu đang chọn",
                        noSelOk && selOk, "noSel=" + noSelOk + ",sel=" + selOk);
                }
                finally { host.Close(); }
            }
        }
    }
}
