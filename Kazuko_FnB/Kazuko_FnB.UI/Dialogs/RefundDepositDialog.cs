using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Phiếu chi trả lại tiền đặt trước khi khách đặt cọc dư (docs/specs/su-dung-dich-vu.md §3.7.2,
    /// docs/specs/quy-tien.md §4.2 Q7 &amp; §7).
    /// Ghi thật xuống TTHUCHI (LOAI = 1, KHONGTHAYDOICONGNO = 30); đóng mà chưa lưu thì cảnh báo rõ và hỏi xác nhận.
    /// </summary>
    internal sealed class RefundDepositDialog : ThemedDialog
    {
        private readonly TableService _service;
        private readonly PaymentExecutionResult _payment;
        private readonly Panel _warningPanel = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _lblWarning = new Label { Dock = DockStyle.Fill, AutoSize = false };
        private readonly TextBox _txtNumber = new TextBox();
        private readonly DateBox _dtDate = new DateBox();
        private readonly TextBox _txtCustomer = new TextBox();
        private readonly NumberBox _numAmount = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly CheckBox _chkTransfer = new CheckBox { Text = "Chuyển khoản từ tài khoản ngân hàng", AutoSize = true };
        private readonly LookupCombo _cboBank = new LookupCombo();
        private readonly LookupCombo _cboReason = new LookupCombo();
        private readonly CheckBox _chkNoDebtChange = new CheckBox { Text = "Không thay đổi công nợ", Checked = true, Enabled = false, AutoSize = true };
        private readonly TextBox _txtNote = new TextBox();
        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnClose;

        private bool _saved;

        public RefundVoucherResult SavedResult { get; private set; }
        public bool DismissedWithoutSave { get; private set; }

        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal bool DismissWarningShownForTest { get; private set; }
        internal Func<bool> ConfirmDismissOverrideForTest { get; set; }
        internal NumberBox AmountBoxForTest { get { return _numAmount; } }
        internal CheckBox TransferCheckForTest { get { return _chkTransfer; } }
        internal LookupCombo BankComboForTest { get { return _cboBank; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal bool RefundWarningVisibleForTest { get { return _warningPanel.Visible; } }
        internal string RefundWarningTextForTest { get { return _lblWarning.Text ?? ""; } }

        public RefundDepositDialog(TableService service, PaymentExecutionResult payment, List<LookupItem> bankAccounts)
            : base("PHIẾU CHI — TRẢ LẠI TIỀN ĐẶT TRƯỚC",
                "Khách đã đặt trước dư cho hóa đơn " + (payment != null ? payment.PaidBillNames : "") + ". Lưu để trừ đúng quỹ tiền.",
                520, 470)
        {
            if (service == null) throw new ArgumentNullException("service");
            if (payment == null) throw new ArgumentNullException("payment");
            _service = service;
            _payment = payment;
            float s = Dpi;

            _btnClose = AddButton("Đóng", null, delegate { Close(); });
            _btnSave = AddButton("Lưu phiếu chi", "primary", delegate { Save(); });
            _btnClose.Height = Math.Max((int)(44 * s), 44);
            _btnSave.Height = Math.Max((int)(44 * s), 44);
            CancelButton = _btnClose;
            AcceptButton = _btnSave;

            string number;
            try { number = _service.GenerateRefundVoucherNumber(); }
            catch { number = ""; }

            _txtNumber.Text = number;
            _dtDate.Value = payment.PaymentDate == DateTime.MinValue ? DateTime.Today : payment.PaymentDate.Date;
            _txtCustomer.Text = payment.CustomerName ?? "";
            _txtCustomer.ReadOnly = !string.IsNullOrEmpty(payment.CustomerId);
            _numAmount.Value = Math.Max(0m, Math.Min(_numAmount.Maximum, payment.ExcessDeposit));
            _txtNote.Text = ServicePaymentConstants.RefundDepositNote(payment.PaidBillNames);

            var banks = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            if (bankAccounts != null)
                banks.AddRange(bankAccounts);
            _cboBank.SetItems(banks);
            _cboBank.SelectId("");
            _cboBank.Enabled = false;

            List<LookupItem> reasons;
            try { reasons = _service.LoadExpenseReasons(); }
            catch { reasons = new List<LookupItem>(); }
            var reasonItems = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            reasonItems.AddRange(reasons);
            _cboReason.SetItems(reasonItems);
            _cboReason.SelectId(reasons.Count > 0 ? reasons[0].Id : "");

            _chkTransfer.CheckedChanged += delegate
            {
                _cboBank.Enabled = _chkTransfer.Checked;
                if (!_chkTransfer.Checked)
                    _cboBank.SelectId("");
            };

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 9, Height = (int)(336 * s) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148 * s));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 9; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));

            AddRow(grid, 0, "Số phiếu chi:", _txtNumber);
            AddRow(grid, 1, "Ngày chi:", _dtDate);
            AddRow(grid, 2, "Người nhận:", _txtCustomer);
            AddRow(grid, 3, "Số tiền trả lại:", _numAmount);
            AddRow(grid, 4, "Hình thức:", _chkTransfer);
            AddRow(grid, 5, "Tài khoản chi:", _cboBank);
            AddRow(grid, 6, "Lý do chi:", _cboReason);
            AddRow(grid, 7, "Công nợ:", _chkNoDebtChange);
            AddRow(grid, 8, "Diễn giải:", _txtNote);

            bool hasWarning = !string.IsNullOrWhiteSpace(payment.RefundDepositWarning);
            if (hasWarning)
            {
                _warningPanel.Visible = true;
                _warningPanel.Height = (int)(74 * s);
                _warningPanel.Padding = new Padding((int)(10 * s), (int)(6 * s), (int)(10 * s), (int)(6 * s));
                _lblWarning.Text = payment.RefundDepositWarning;
                _lblWarning.TextAlign = ContentAlignment.MiddleLeft;
                _lblWarning.Font = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize, FontStyle.Bold);
                _warningPanel.Controls.Add(_lblWarning);
            }

            Body.Controls.Add(grid);
            if (hasWarning)
            {
                Body.Controls.Add(_warningPanel);
                FitBody(520, 440);
            }
            ApplyDialogTheme();
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            if (_warningPanel != null)
            {
                _warningPanel.BackColor = Draw.Mix(t.Surface, t.Danger, 0.12f);
                _lblWarning.BackColor = Color.Transparent;
                _lblWarning.ForeColor = t.Danger;
            }
        }

        private void Save()
        {
            var input = new RefundVoucherInput
            {
                Number = (_txtNumber.Text ?? "").Trim(),
                Date = _dtDate.Value.Date,
                PrimaryBillId = _payment.PrimaryBillId,
                CustomerId = _payment.CustomerId,
                CustomerName = (_txtCustomer.Text ?? "").Trim(),
                Address = _payment.CustomerAddress ?? "",
                StoreId = _payment.StoreId,
                ReasonId = _cboReason.SelectedId,
                Note = (_txtNote.Text ?? "").Trim(),
                Amount = _numAmount.Value,
                Transfer = _chkTransfer.Checked,
                BankAccountId = _chkTransfer.Checked ? _cboBank.SelectedId : null
            };

            RefundVoucherResult res = _service.SaveRefundDepositVoucher(input);
            if (!res.Ok)
            {
                LastWarningForTest = res.ErrorMessage;
                if (!SuppressPopup)
                    Msg.Warning(this, res.ErrorMessage);
                return;
            }

            _saved = true;
            SavedResult = res;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (_saved || e.Cancel)
                return;

            // §3.7.2 & quy-tien.md §4.2 Q7: đóng phiếu chi không lưu -> cảnh báo rõ và hỏi xác nhận.
            DismissWarningShownForTest = true;
            bool confirmDismiss;
            if (ConfirmDismissOverrideForTest != null)
                confirmDismiss = ConfirmDismissOverrideForTest();
            else if (SuppressPopup)
                confirmDismiss = true;
            else
                confirmDismiss = Msg.Confirm(this, ServicePaymentConstants.RefundDismissWarning);

            if (!confirmDismiss)
            {
                e.Cancel = true;
                return;
            }
            DismissedWithoutSave = true;
        }

        private static void AddRow(TableLayoutPanel grid, int row, string text, Control ctrl)
        {
            grid.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            ctrl.Dock = DockStyle.Fill;
            grid.Controls.Add(ctrl, 1, row);
        }
    }
}
