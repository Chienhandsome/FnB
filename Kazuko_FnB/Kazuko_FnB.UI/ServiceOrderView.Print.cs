using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần in hóa đơn thanh toán, in tạm tính, in lại bill và in bếp/chế biến của <see cref="ServiceOrderView"/> (patch sddv-d).
    /// Toàn bộ dữ liệu in đọc lại từ CSDL qua <see cref="TableService"/>; khóa bấm hai lần qua coordinator;
    /// hỗ trợ tiêm IPrintOutputHandler &amp; IKitchenPrinterStore để selftest kiểm đủ hành vi không cần máy in thật.
    /// </summary>
    internal sealed partial class ServiceOrderView
    {
        private readonly FlatButton _btnTempPrint = new FlatButton { Text = "In tạm tính" };
        private readonly ServiceInvoicePrintCoordinator _invoicePrintCoordinator = new ServiceInvoicePrintCoordinator();
        private readonly KitchenPrintCoordinator _kitchenPrintCoordinator = new KitchenPrintCoordinator();
        private string _lastPaidBillId;

        internal FlatButton TempPrintButtonForTest { get { return _btnTempPrint; } }
        internal FlatButton StatsButtonForTest { get { return _btnStats; } }
        internal string LastPaidBillIdForTest
        {
            get { return _lastPaidBillId; }
            set { _lastPaidBillId = value; }
        }
        internal string LastPrintMessageForTest { get; private set; }
        internal IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> PrintTemplatePickerForTest { get; set; }
        internal Func<string, List<PrintTemplateRecord>> PrintTemplateLoaderForTest { get; set; }
        internal IKitchenPrinterStore KitchenPrinterStoreForTest { get; set; }
        internal Func<PrintTemplateRecord> KitchenTemplateOverrideForTest { get; set; }

        private void InitPrintControls(float s)
        {
            int btnH = Math.Max((int)(44 * s), 44);
            FlowLayoutPanel actionFlow = _actionCol.Controls.Count > 0 ? _actionCol.Controls[0] as FlowLayoutPanel : null;
            if (actionFlow != null)
            {
                int innerW = _actionCol.Width - (int)(12 * s);
                AddActionBtn(actionFlow, _btnTempPrint, innerW, btnH, s);
            }
            _btnTempPrint.Visible = _canViewPrice;
        }

        private void DoTempPrint()
        {
            if (!_canViewPrice)
                return;
            if (_currentBill == null || _currentTable == null)
            {
                ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }
            if (_invoicePrintCoordinator.IsBusy)
                return;

            TempPrintPrepareResult prep = _service.PrepareAndRecordTempPrint(_currentBill.Id, _currentTable.Id, null);
            if (prep.NeedsReLogin && !SuppressPopup)
            {
                using (var dlg = new ServiceReLoginDialog(_session.DatabasePath, prep.RequiredFunctionId, "In tạm tính"))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK || dlg.VerifiedSession == null)
                        return;
                    prep = _service.PrepareAndRecordTempPrint(_currentBill.Id, _currentTable.Id, dlg.VerifiedSession);
                }
            }
            if (!prep.Ok)
            {
                ShowWarning(prep.ErrorMessage);
                return;
            }

            // Bàn chuyển ngay sang trạng thái "Đã in tạm tính" (ResolveTableStatus theo SOLANINTAMTINH > 0).
            _currentBill = _service.LoadBill(_currentBill.Id);
            _tableMap.ReloadAll();
            _currentTable = _tableMap.FindTable(_currentTable.Id);
            if (_currentTable != null)
                _tableMap.SelectTableVisual(_currentTable.Id);
            RenderCurrentBill();

            IKitchenPrinterStore store = KitchenPrinterStoreForTest ?? KitchenPrintCoordinator.CreateDefaultPrinterStore();
            _invoicePrintCoordinator.PrintTempBill(
                this,
                _btnTempPrint,
                _session.DatabasePath,
                _session,
                prep.Document,
                store,
                PrintOutputHandlerForTest,
                PrintTemplatePickerForTest,
                PrintTemplateLoaderForTest,
                delegate (string msg) { LastPrintMessageForTest = msg; });
        }

        private void DoReprintBill()
        {
            if (_invoicePrintCoordinator.IsBusy)
                return;

            string targetBillId = !string.IsNullOrEmpty(_lastPaidBillId)
                ? _lastPaidBillId
                : _service.FindLatestPaidBillId(_currentTable != null ? _currentTable.Id : null);

            TempPrintPrepareResult prep = _service.PrepareReprintPaidBill(targetBillId, null);
            if (prep.NeedsReLogin && !SuppressPopup)
            {
                using (var dlg = new ServiceReLoginDialog(_session.DatabasePath, prep.RequiredFunctionId, "In lại bill"))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK || dlg.VerifiedSession == null)
                        return;
                    prep = _service.PrepareReprintPaidBill(targetBillId, dlg.VerifiedSession);
                }
            }
            if (!prep.Ok)
            {
                ShowWarning(prep.ErrorMessage);
                return;
            }

            IKitchenPrinterStore store = KitchenPrinterStoreForTest ?? KitchenPrintCoordinator.CreateDefaultPrinterStore();
            _invoicePrintCoordinator.ReprintPaidBill(
                this,
                _btnReprint,
                _session.DatabasePath,
                _session,
                prep.Document,
                store,
                PrintOutputHandlerForTest,
                PrintTemplatePickerForTest,
                PrintTemplateLoaderForTest,
                delegate (string msg) { LastPrintMessageForTest = msg; });
        }

        internal bool DoPrintAfterPayment(PaymentExecutionResult result)
        {
            if (result == null || !result.Ok)
                return false;
            if (!string.IsNullOrEmpty(result.PrimaryBillId))
                _lastPaidBillId = result.PrimaryBillId;
            if (!result.PrintAfterClose)
                return true;

            var billIds = result.PaidBillIds != null && result.PaidBillIds.Count > 0
                ? result.PaidBillIds
                : (!string.IsNullOrEmpty(result.PrimaryBillId) ? new List<string> { result.PrimaryBillId } : new List<string>());

            IKitchenPrinterStore store = KitchenPrinterStoreForTest ?? KitchenPrintCoordinator.CreateDefaultPrinterStore();
            bool allOk = true;
            for (int i = 0; i < billIds.Count; i++)
            {
                ServiceInvoicePrintDocument doc = _service.LoadInvoiceForPrint(billIds[i]);
                bool ok = _invoicePrintCoordinator.PrintAfterPayment(
                    this,
                    _btnPay,
                    _session.DatabasePath,
                    _session,
                    doc,
                    store,
                    PrintOutputHandlerForTest,
                    PrintTemplateLoaderForTest,
                    delegate (string msg) { LastPrintMessageForTest = msg; });
                if (!ok)
                    allOk = false;
            }
            return allOk;
        }

        private void DoKitchenPrint(bool autoPrint)
        {
            if (_currentBill == null || _currentTable == null)
            {
                if (!autoPrint)
                    ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }
            if (_kitchenPrintCoordinator.IsBusy)
                return;

            IKitchenPrinterStore store = KitchenPrinterStoreForTest ?? KitchenPrintCoordinator.CreateDefaultPrinterStore();
            KitchenPrintPlan plan = _service.BuildKitchenPrintPlan(_currentBill.Id, store);

            if (!plan.Ok && !plan.NeedsPrinterSetup)
            {
                if (!autoPrint)
                    ShowWarning(plan.ErrorMessage);
                return;
            }

            if (autoPrint || SuppressPopup)
            {
                if (plan.NeedsPrinterSetup)
                {
                    if (!autoPrint)
                        ShowWarning(plan.ErrorMessage);
                    return;
                }
                _kitchenPrintCoordinator.ExecuteBatches(
                    this,
                    _btnKitchenPrint,
                    _service,
                    _session.DatabasePath,
                    _session,
                    plan,
                    autoPrint,
                    PrintOutputHandlerForTest,
                    KitchenTemplateOverrideForTest,
                    delegate (string msg) { LastPrintMessageForTest = msg; });
                return;
            }

            using (var dlg = new KitchenPrintDialog(_service, _session, _currentBill.Id, store))
            {
                dlg.PrintOutputHandlerForTest = PrintOutputHandlerForTest;
                dlg.TemplateOverrideForTest = KitchenTemplateOverrideForTest;
                dlg.ShowDialog(this);
            }
        }
    }
}
