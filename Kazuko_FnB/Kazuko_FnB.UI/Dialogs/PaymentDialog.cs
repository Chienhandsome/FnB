using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "Xác nhận thanh toán" (docs/specs/su-dung-dich-vu.md §3.7, §3.7.1–§3.7.3, §6.5, §6.6).
    /// Dùng chung cho bill đơn và nhóm bill (Thanh toán chung). Mọi ô số dùng NumberBox; màu/cỡ chữ từ ThemeTokens/ThemeManager.
    /// Phần sự kiện, hộp con và thực thi thanh toán ở PaymentDialog.Actions.cs.
    /// </summary>
    internal sealed partial class PaymentDialog : ThemedDialog
    {
        private readonly TableService _service;
        private readonly UserSession _session;
        private readonly PaymentDialogContext _ctx;
        private readonly List<string> _billIds = new List<string>();
        private readonly ServiceInvoicePrintCoordinator _invoicePrintCoordinator = new ServiceInvoicePrintCoordinator();

        private readonly TableLayoutPanel _grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3 };
        private readonly LookupCombo _cboCustomer = new LookupCombo();
        private readonly NumberBox _numTotalBills = new NumberBox { ReadOnly = true, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numDeposit = new NumberBox { ReadOnly = true, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numOldDebt = new NumberBox { ReadOnly = true, Minimum = -9999999999m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numNetDue = new NumberBox { ReadOnly = true, Minimum = -9999999999m, Maximum = 9999999999m, DecimalPlaces = 2 };

        private readonly NumberBox _numVoucher = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly FlatButton _btnVoucher = new FlatButton { Text = "Mã voucher" };
        private readonly NumberBox _numPrepaid = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly FlatButton _btnPrepaid = new FlatButton { Text = "Chọn thẻ" };
        private readonly NumberBox _numPoints = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly FlatButton _btnPoints = new FlatButton { Text = "Dùng điểm" };

        private readonly NumberBox _numCard = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numTransfer = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly LookupCombo _cboBank = new LookupCombo();
        private readonly NumberBox _numCash = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly CheckBox _chkDebt = new CheckBox { Text = "Khách nợ", AutoSize = true };
        private readonly NumberBox _numChange = new NumberBox { ReadOnly = true, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly TextBox _txtOrderNumbers = new TextBox();
        private readonly LookupCombo _cboEmployee = new LookupCombo();
        private readonly Label _lblStatus = new Label { Dock = DockStyle.Top, AutoSize = false };

        private readonly FlatButton _btnOk;
        private readonly FlatButton _btnNoPrint;
        private readonly FlatButton _btnTempPrint;
        private readonly FlatButton _btnCancel;

        private CustomerPaymentState _customerState;
        private string _voucherId;
        private string _prepaidCardId;
        private decimal _prepaidCardBalance;
        private bool _updating;
        private bool _showBankRow;
        private bool _showCardRow;
        private bool _showTransferRow;
        private bool _showVoucherRow;
        private bool _showPrepaidRow;
        private bool _showPointsRow;
        private bool _showDebtCheck;
        private bool _showOrderRow;
        private bool _showEmployeeRow;

        public PaymentExecutionResult ExecutionResult { get; private set; }

        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal string LastInfoForTest { get; private set; }
        internal string LastPrintMessageForTest { get; private set; }
        internal IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal Func<string, List<PrintTemplateRecord>> PrintTemplateLoaderForTest { get; set; }
        internal bool AutoConfirmPromptsForTest { get; set; }
        internal LookupCombo CustomerComboForTest { get { return _cboCustomer; } }
        internal LookupCombo BankComboForTest { get { return _cboBank; } }
        internal bool BankRowVisibleForTest { get { return _showBankRow; } }
        internal NumberBox NetDueBoxForTest { get { return _numNetDue; } }
        internal NumberBox CashBoxForTest { get { return _numCash; } }
        internal NumberBox TransferBoxForTest { get { return _numTransfer; } }
        internal NumberBox CardBoxForTest { get { return _numCard; } }
        internal NumberBox VoucherBoxForTest { get { return _numVoucher; } }
        internal NumberBox PrepaidBoxForTest { get { return _numPrepaid; } }
        internal NumberBox PointsBoxForTest { get { return _numPoints; } }
        internal NumberBox ChangeBoxForTest { get { return _numChange; } }
        internal CheckBox DebtCheckForTest { get { return _chkDebt; } }
        internal FlatButton OkButtonForTest { get { return _btnOk; } }
        internal FlatButton NoPrintButtonForTest { get { return _btnNoPrint; } }
        internal FlatButton TempPrintButtonForTest { get { return _btnTempPrint; } }
        internal Label StatusLabelForTest { get { return _lblStatus; } }

        public PaymentDialog(TableService service, UserSession session, PaymentDialogContext context)
            : base(
                context != null && context.IsGroupPayment ? "XÁC NHẬN THANH TOÁN CHUNG" : "XÁC NHẬN THANH TOÁN",
                BuildSubtitle(context), 580, 620)
        {
            if (service == null) throw new ArgumentNullException("service");
            if (session == null) throw new ArgumentNullException("session");
            if (context == null) throw new ArgumentNullException("context");

            _service = service;
            _session = session;
            _ctx = context;
            _customerState = context.InitialCustomerState ?? new CustomerPaymentState { PointRate = SystemConfig.QuyDoi1DiemSangTien };
            foreach (PaymentBillSlice b in context.Bills)
                _billIds.Add(b.BillId);

            _showCardRow = SystemConfig.CoThanhToanThe;
            _showTransferRow = SystemConfig.CoThanhToanChuyenKhoan;
            // §3.7 & §7: ô Tài khoản hiện khi có ô thẻ HOẶC ô chuyển khoản (khác gốc chỉ hiện theo cờ chuyển khoản).
            _showBankRow = _showCardRow || _showTransferRow;
            _showVoucherRow = SystemConfig.CoThanhToanVoucher;
            _showPrepaidRow = SystemConfig.SuDungTheTraTruoc;
            _showPointsRow = SystemConfig.SuDungDiemTichLuyDeThanhToan;
            _showDebtCheck = SystemConfig.ChoPhepKhachNo;
            _showOrderRow = SystemConfig.BatChucNangKiemSoatOrder;
            _showEmployeeRow = SystemConfig.BatBuocNhapNhanVienBanHang;

            float s = Dpi;
            int btnH = Math.Max((int)(44 * s), 44);
            _btnCancel = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            _btnTempPrint = AddButton("In thử bill (F8)", null, delegate { DoTempPrint(); });
            _btnNoPrint = AddButton("Đóng bill không in (F9)", null, delegate { SubmitPayment(false); });
            _btnOk = AddButton("Thanh toán & In", "primary", delegate { SubmitPayment(true); });
            foreach (FlatButton b in new[] { _btnCancel, _btnTempPrint, _btnNoPrint, _btnOk })
                b.Height = btnH;
            CompactFooter();

            _btnNoPrint.Enabled = !SystemConfig.BatBuocInKhiThanhToan;
            _btnTempPrint.Visible = _service.CanTempPrint();
            CancelButton = _btnCancel;
            AcceptButton = _btnOk;

            BuildBody(s);
            BindData();
            WireEvents();
            ApplyCustomerLinkedCardIfAny();
            ResetDefaultCash();
            UpdateChangeAndStatus();
            ApplyDialogTheme();
            Shown += delegate { if (_numCash.Enabled) _numCash.Focus(); };
        }

        private static string BuildSubtitle(PaymentDialogContext ctx)
        {
            if (ctx == null || ctx.Bills == null || ctx.Bills.Count == 0)
                return "";
            if (ctx.Bills.Count == 1)
                return string.Format("Bàn {0} — Hóa đơn {1}", ctx.Bills[0].TableName, ctx.Bills[0].BillName);
            var parts = new List<string>(ctx.Bills.Count);
            foreach (PaymentBillSlice b in ctx.Bills)
                parts.Add(b.TableName);
            return string.Format("Thanh toán chung {0} bàn: {1}", ctx.Bills.Count, string.Join(", ", parts.ToArray()));
        }

        private void BuildBody(float s)
        {
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146 * s));
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114 * s));

            int row = 0;
            AddRow(ref row, "Khách hàng:", _cboCustomer, null, s);
            AddRow(ref row, "Tiền hóa đơn:", _numTotalBills, null, s);
            if (_ctx.TotalDeposit != 0m)
                AddRow(ref row, "Đặt trước:", _numDeposit, null, s);
            AddRow(ref row, "Nợ cũ:", _numOldDebt, null, s);

            _numNetDue.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 1f, FontStyle.Bold);
            AddRow(ref row, "PHẢI THANH TOÁN:", _numNetDue, null, s);

            if (_showVoucherRow)
            {
                _numVoucher.ReadOnly = SystemConfig.LuaChonVoucherTuDanhSach;
                AddRow(ref row, "Voucher:", _numVoucher, _btnVoucher, s);
            }
            if (_showPrepaidRow)
                AddRow(ref row, "Thẻ trả trước:", _numPrepaid, _btnPrepaid, s);
            if (_showPointsRow)
                AddRow(ref row, "Trừ tích lũy:", _numPoints, _btnPoints, s);
            if (_showCardRow)
                AddRow(ref row, "Thẻ ATM:", _numCard, null, s);
            if (_showTransferRow)
                AddRow(ref row, "Chuyển khoản:", _numTransfer, null, s);
            if (_showBankRow)
                AddRow(ref row, "Tài khoản:", _cboBank, null, s);

            AddRow(ref row, "Khách đưa:", _numCash, _showDebtCheck ? (Control)_chkDebt : null, s);
            AddRow(ref row, "Trả lại:", _numChange, null, s);

            if (_showOrderRow)
                AddRow(ref row, "Số order:", _txtOrderNumbers, null, s);
            if (_showEmployeeRow)
                AddRow(ref row, "Nhân viên:", _cboEmployee, null, s);

            _grid.RowCount = row;
            int gridH = row * (int)(36 * s) + (int)(6 * s);
            _grid.Height = gridH;

            _lblStatus.Height = (int)(26 * s);
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _lblStatus.Font = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize, FontStyle.Bold);

            Body.Controls.Add(_lblStatus);
            Body.Controls.Add(_grid);

            int bodyH = gridH + _lblStatus.Height + (int)(28 * s);
            FitBody(560, Math.Max(280, (int)(bodyH / s)));
        }

        private void AddRow(ref int row, string caption, Control main, Control extra, float s)
        {
            _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));
            var lbl = new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize)
            };
            main.Dock = DockStyle.Fill;
            main.Margin = new Padding(2, 4, 4, 4);
            if (main is NumberBox)
                ((NumberBox)main).TextAlign = HorizontalAlignment.Right;

            _grid.Controls.Add(lbl, 0, row);
            _grid.Controls.Add(main, 1, row);
            if (extra != null)
            {
                extra.Dock = DockStyle.Fill;
                extra.Margin = new Padding(4, 3, 0, 3);
                _grid.Controls.Add(extra, 2, row);
            }
            else
            {
                _grid.SetColumnSpan(main, 2);
            }
            row++;
        }

        private void BindData()
        {
            _updating = true;
            try
            {
                var custItems = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
                foreach (CustomerLookupItem c in _ctx.Customers)
                    custItems.Add(new LookupItem { Id = c.Id, Name = c.Name });
                _cboCustomer.SetItems(custItems);
                _cboCustomer.SelectId(_ctx.InitialCustomerId ?? "");

                var bankItems = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
                bankItems.AddRange(_ctx.BankAccounts);
                _cboBank.SetItems(bankItems);
                _cboBank.SelectId("");

                if (_showEmployeeRow)
                {
                    var empItems = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
                    empItems.AddRange(_ctx.Employees);
                    _cboEmployee.SetItems(empItems);
                    _cboEmployee.SelectId("");
                }

                _numTotalBills.Value = _ctx.TotalBills;
                _numDeposit.Value = _ctx.TotalDeposit;
                _numOldDebt.Value = _customerState.OldDebt;
                _numNetDue.Value = CurrentNetDue();
            }
            finally { _updating = false; }
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            if (_numNetDue != null)
                _numNetDue.ForeColor = t.Primary;
            if (_lblStatus != null)
                _lblStatus.ForeColor = t.Danger;
        }
    }
}
