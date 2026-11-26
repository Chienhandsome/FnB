using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Điều phối thực thi in bếp / chế biến cho danh sách <see cref="KitchenPrintBatch"/>:
    /// nạp mẫu từ STEMPLATE theo MauInCheBien (hoặc dùng mẫu 80mm tối giản dựng sẵn), in ra từng máy in bếp
    /// (hỗ trợ InMoiDoRa1To, SoLienInCheBien, InThem1LienTaiQuay), và ghi nhận TINCHEBIEN sau khi in thành công.
    /// </summary>
    internal sealed class KitchenPrintCoordinator
    {
        private bool _isBusy;

        public bool IsBusy
        {
            get { return _isBusy; }
        }

        public static IKitchenPrinterStore CreateDefaultPrinterStore()
        {
            return new FileKitchenPrinterStore(LoadInstalledPrinters);
        }

        public static List<string> LoadInstalledPrinters()
        {
            var list = new List<string>();
            try
            {
                foreach (string p in PrinterSettings.InstalledPrinters)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        list.Add(p);
                }
            }
            catch
            {
            }
            return list;
        }

        /// <summary>
        /// Thực thi in tất cả các nhóm phiếu bếp trong kế hoạch. Mỗi nhóm in xong sẽ ghi nhận vào TINCHEBIEN.
        /// Nếu một nhóm in lỗi (không có máy in/mẫu hỏng), báo lỗi rõ tên mẫu, không văng và KHÔNG đánh dấu TINCHEBIEN cho nhóm lỗi.
        /// </summary>
        public bool ExecuteBatches(
            Control hostControl,
            Control triggerButton,
            TableService service,
            string databasePath,
            UserSession session,
            KitchenPrintPlan plan,
            bool autoPrint,
            IPrintOutputHandler outputOverride,
            Func<PrintTemplateRecord> templateOverride,
            Action<string> recordMessage)
        {
            if (_isBusy)
                return false;

            IPrintOutputHandler output = outputOverride ?? new WinFormsPrintOutputHandler();
            IWin32Window owner = hostControl == null ? null : hostControl.FindForm();
            if (plan == null || !plan.Ok || plan.Batches == null || plan.Batches.Count == 0)
            {
                string msg = plan != null && !string.IsNullOrEmpty(plan.ErrorMessage)
                    ? plan.ErrorMessage
                    : ServicePrintConstants.KitchenNoRemainingItem;
                if (!autoPrint)
                {
                    if (recordMessage != null)
                        recordMessage(msg);
                    output.ShowInfo(owner, msg);
                }
                return false;
            }

            _isBusy = true;
            bool oldEnabled = triggerButton != null && triggerButton.Enabled;
            Cursor oldCursor = hostControl != null ? hostControl.Cursor : Cursors.Default;
            try
            {
                if (triggerButton != null)
                    triggerButton.Enabled = false;
                if (hostControl != null)
                    hostControl.Cursor = Cursors.WaitCursor;

                KitchenPrintSettings settings = TableService.ReadKitchenSettings();
                PrintTemplateRecord template = ResolveKitchenTemplate(databasePath, settings.IncludeUnitPrice, templateOverride);
                Dictionary<string, bool> configFlags = new PrintTemplateRepository(databasePath).LoadConfigVisibilityForTemplate(template);
                CompanyPrintInfo company = FormPrintCoordinator.LoadCompanyInfoSafe(databasePath);
                string printedBy = DocumentPrintCoordinator.ResolvePrintedBy(session);
                int printSeq = service.GetNextKitchenPrintSeq(plan.BillId);
                DateTime now = DateTime.Now;
                string counterPrinter = ServiceInvoicePrintCoordinator.GetDefaultWindowsPrinter();

                bool allOk = true;
                for (int b = 0; b < plan.Batches.Count; b++)
                {
                    KitchenPrintBatch batch = plan.Batches[b];
                    if (batch == null || batch.Items == null || batch.Items.Count == 0)
                        continue;

                    bool batchPrinted = PrintSingleBatch(
                        owner,
                        plan,
                        batch,
                        printSeq,
                        template,
                        configFlags,
                        company,
                        printedBy,
                        now,
                        settings,
                        counterPrinter,
                        output);

                    if (!batchPrinted)
                    {
                        allOk = false;
                        continue;
                    }

                    string recErr = service.RecordKitchenPrintedBatch(plan.BillId, plan.TableName, batch, printSeq, autoPrint);
                    if (!string.IsNullOrEmpty(recErr))
                    {
                        if (recordMessage != null)
                            recordMessage(recErr);
                        output.ShowError(owner, recErr);
                        allOk = false;
                    }
                    printSeq++;
                }

                return allOk;
            }
            catch (Exception ex)
            {
                string msg = "Không thể in chế biến: " + ex.Message;
                if (recordMessage != null)
                    recordMessage(msg);
                output.ShowError(owner, msg);
                return false;
            }
            finally
            {
                if (triggerButton != null)
                    triggerButton.Enabled = oldEnabled;
                if (hostControl != null)
                    hostControl.Cursor = oldCursor;
                _isBusy = false;
            }
        }

        private static bool PrintSingleBatch(
            IWin32Window owner,
            KitchenPrintPlan plan,
            KitchenPrintBatch batch,
            int printSeq,
            PrintTemplateRecord template,
            Dictionary<string, bool> configFlags,
            CompanyPrintInfo company,
            string printedBy,
            DateTime printedAt,
            KitchenPrintSettings settings,
            string counterPrinter,
            IPrintOutputHandler output)
        {
            var slips = new List<IList<KitchenPendingItem>>();
            if (settings.OneItemPerSlip)
            {
                for (int i = 0; i < batch.Items.Count; i++)
                    slips.Add(new List<KitchenPendingItem> { batch.Items[i] });
            }
            else
            {
                slips.Add(batch.Items);
            }

            int copies = Math.Max(1, Math.Min(5, settings.Copies));
            for (int s = 0; s < slips.Count; s++)
            {
                using (PrintJobData job = KitchenTicketPrintDataBuilder.Build(
                    plan.BillName,
                    plan.TableName,
                    plan.AreaName,
                    batch.Caption,
                    printSeq,
                    slips[s],
                    company,
                    printedBy,
                    printedAt))
                {
                    job.ConfigVisibilityById = configFlags;
                    var sel = new PrintSelectionResult
                    {
                        Template = template,
                        Action = PrintActionKind.Print,
                        PrinterName = batch.PrinterName
                    };

                    for (int c = 0; c < copies; c++)
                    {
                        if (!PrintWorkflowService.Execute(owner, sel, job, output))
                            return false;
                    }

                    if (settings.ExtraCounterCopy && !string.IsNullOrWhiteSpace(counterPrinter))
                    {
                        var counterSel = new PrintSelectionResult
                        {
                            Template = template,
                            Action = PrintActionKind.Print,
                            PrinterName = counterPrinter
                        };
                        if (!PrintWorkflowService.Execute(owner, counterSel, job, output))
                            return false;
                    }
                }
            }
            return true;
        }

        internal static PrintTemplateRecord ResolveKitchenTemplate(
            string databasePath,
            bool includePrice,
            Func<PrintTemplateRecord> templateOverride)
        {
            if (templateOverride != null)
                return templateOverride();

            string cfgTemplate = SystemConfig.MauInCheBien;
            if (!string.IsNullOrWhiteSpace(cfgTemplate) && !string.IsNullOrWhiteSpace(databasePath))
            {
                try
                {
                    PrintTemplateRecord dbTpl = new PrintTemplateRepository(databasePath).LoadTemplateByIdOrName(cfgTemplate);
                    if (dbTpl != null)
                        return dbTpl;
                }
                catch (DataAccessException)
                {
                }
            }
            return KitchenTicketPrintDataBuilder.CreateBuiltInTemplate(includePrice);
        }
    }
}
