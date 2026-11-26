using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Điều phối in phiếu Khách đặt hàng và Báo giá: đọc lại phiếu từ CSDL qua <see cref="DocumentPrintRepository"/>,
    /// dựng dữ liệu qua builder và dùng chung <see cref="FormPrintCoordinator"/> để tránh nhân đôi mã.
    /// </summary>
    internal sealed class DocumentPrintCoordinator
    {
        private readonly FormPrintCoordinator _core = new FormPrintCoordinator();

        public bool IsBusy
        {
            get { return _core.IsBusy; }
        }

        public bool PrintBooking(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            UserSession session,
            string bookingId,
            bool directDefault,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            if (_core.IsBusy)
                return false;

            IPrintOutputHandler output = outputOverride ?? new WinFormsPrintOutputHandler();
            IWin32Window owner = hostControl == null ? null : hostControl.FindForm();

            BookingPrintDocument doc;
            try
            {
                doc = new DocumentPrintRepository(databasePath).LoadBookingForPrint(bookingId);
            }
            catch (DataAccessException ex)
            {
                string msg = "Không đọc được dữ liệu phiếu đặt hàng để in: " + ex.Message;
                if (recordMessage != null)
                    recordMessage(msg);
                output.ShowError(owner, msg);
                return false;
            }

            if (doc == null)
            {
                if (recordMessage != null)
                    recordMessage(BookingText.NotFound);
                output.ShowError(owner, BookingText.NotFound);
                return false;
            }

            string printedBy = ResolvePrintedBy(session);
            PrintDialogOptions options = PrintDialogOptions.ForBookingReportDialog();
            Func<CompanyPrintInfo, PrintJobData> buildJob = delegate (CompanyPrintInfo company)
            {
                return BookingPrintDataBuilder.Build(doc, company, printedBy);
            };

            if (directDefault)
            {
                return _core.PrintDirectForTable(
                    hostControl,
                    triggerButton,
                    databasePath,
                    PrintConstants.BookingTableDescId,
                    options,
                    buildJob,
                    outputOverride,
                    templateLoaderOverride,
                    recordMessage);
            }

            return _core.PrintForTable(
                hostControl,
                triggerButton,
                databasePath,
                PrintConstants.BookingTableDescId,
                options,
                buildJob,
                outputOverride,
                templatePickerOverride,
                templateLoaderOverride,
                recordMessage);
        }

        public bool PrintQuote(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            UserSession session,
            string quoteId,
            bool directDefault,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Func<string, List<PrintTemplateRecord>> templateLoaderOverride,
            Action<string> recordMessage)
        {
            if (_core.IsBusy)
                return false;

            IPrintOutputHandler output = outputOverride ?? new WinFormsPrintOutputHandler();
            IWin32Window owner = hostControl == null ? null : hostControl.FindForm();

            QuotePrintDocument doc;
            try
            {
                doc = new DocumentPrintRepository(databasePath).LoadQuoteForPrint(quoteId);
            }
            catch (DataAccessException ex)
            {
                string msg = "Không đọc được dữ liệu báo giá để in: " + ex.Message;
                if (recordMessage != null)
                    recordMessage(msg);
                output.ShowError(owner, msg);
                return false;
            }

            if (doc == null)
            {
                if (recordMessage != null)
                    recordMessage(QuoteText.NotFound);
                output.ShowError(owner, QuoteText.NotFound);
                return false;
            }

            string printedBy = ResolvePrintedBy(session);
            PrintDialogOptions options = PrintDialogOptions.ForQuoteReportDialog();
            Func<CompanyPrintInfo, PrintJobData> buildJob = delegate (CompanyPrintInfo company)
            {
                return QuotePrintDataBuilder.Build(doc, company, printedBy);
            };

            if (directDefault)
            {
                return _core.PrintDirectForTable(
                    hostControl,
                    triggerButton,
                    databasePath,
                    PrintConstants.QuoteTableDescId,
                    options,
                    buildJob,
                    outputOverride,
                    templateLoaderOverride,
                    recordMessage);
            }

            return _core.PrintForTable(
                hostControl,
                triggerButton,
                databasePath,
                PrintConstants.QuoteTableDescId,
                options,
                buildJob,
                outputOverride,
                templatePickerOverride,
                templateLoaderOverride,
                recordMessage);
        }

        public static string ResolvePrintedBy(UserSession session)
        {
            UserSession effective = session ?? AppSession.Current;
            if (effective == null)
                return "";
            if (!string.IsNullOrWhiteSpace(effective.DisplayName))
                return effective.DisplayName.Trim();
            return (effective.UserName ?? "").Trim();
        }
    }
}
