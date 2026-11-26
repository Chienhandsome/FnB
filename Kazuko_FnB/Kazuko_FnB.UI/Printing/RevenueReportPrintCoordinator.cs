using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Điều phối in báo cáo cho <see cref="RevenueReportView"/> (tách riêng để giữ RevenueReportView gọn).
    /// Dùng chung <see cref="FormPrintCoordinator"/> để đọc 4 mẫu STEMPLATE, khóa nút, mở hộp chọn mẫu và xuất báo cáo.
    /// </summary>
    internal sealed class RevenueReportPrintCoordinator
    {
        private readonly FormPrintCoordinator _core = new FormPrintCoordinator();

        public bool IsBusy
        {
            get { return _core.IsBusy; }
        }

        public bool Print(
            Control hostControl,
            Control triggerButton,
            string databasePath,
            IList<RevenueBillRow> visibleBills,
            DateTime fromDate,
            DateTime toDate,
            IPrintOutputHandler outputOverride,
            Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> templatePickerOverride,
            Action<string> recordMessage)
        {
            return _core.Print(
                hostControl,
                triggerButton,
                databasePath,
                PrintConstants.RevenueReportFormId,
                delegate (CompanyPrintInfo company)
                {
                    return RevenueReportPrintDataBuilder.Build(visibleBills, fromDate, toDate, company);
                },
                outputOverride,
                templatePickerOverride,
                recordMessage);
        }

        public PrintJobData BuildJobData(
            string databasePath,
            IList<RevenueBillRow> visibleBills,
            DateTime fromDate,
            DateTime toDate)
        {
            CompanyPrintInfo company = FormPrintCoordinator.LoadCompanyInfoSafe(databasePath);
            return RevenueReportPrintDataBuilder.Build(visibleBills, fromDate, toDate, company);
        }
    }
}
