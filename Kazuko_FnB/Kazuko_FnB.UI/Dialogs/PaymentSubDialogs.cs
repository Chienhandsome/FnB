using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Hộp nhập mã hoặc chọn voucher từ danh sách (DVOUCHER, §3.7).</summary>
    internal sealed class VoucherPickDialog : ThemedDialog
    {
        private readonly bool _fromList;
        private readonly Func<string, VoucherInfo> _validator;
        private readonly TextBox _txtCode = new TextBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly List<VoucherInfo> _vouchers;

        public VoucherInfo SelectedVoucher { get; private set; }
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }

        public VoucherPickDialog(bool fromList, List<VoucherInfo> availableVouchers, Func<string, VoucherInfo> validator)
            : base("CHỌN VOUCHER / COUPON",
                fromList ? "Chọn một voucher còn hiệu lực trong danh sách." : "Nhập mã voucher để áp dụng cho hóa đơn.",
                fromList ? 520 : 400, fromList ? 380 : 210)
        {
            _fromList = fromList;
            _vouchers = availableVouchers ?? new List<VoucherInfo>();
            _validator = validator;
            float s = Dpi;

            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = AddButton("Chấp nhận", "primary", delegate { AcceptSelection(); });

            if (_fromList)
            {
                _grid.Dock = DockStyle.Fill;
                _grid.ReadOnly = true;
                _grid.AllowUserToAddRows = false;
                _grid.AllowUserToDeleteRows = false;
                _grid.AllowUserToResizeRows = false;
                _grid.RowHeadersVisible = false;
                _grid.AutoGenerateColumns = false;
                _grid.MultiSelect = false;
                _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                _grid.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
                _grid.RowTemplate.Height = Math.Max((int)(32 * s), 30);
                _grid.ColumnHeadersHeight = (int)(32 * s);
                _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCode", HeaderText = "Mã voucher", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
                var colVal = new DataGridViewTextBoxColumn { Name = "colVal", HeaderText = "Giá trị", Width = (int)(120 * s) };
                colVal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                _grid.Columns.Add(colVal);
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colExp", HeaderText = "Hạn dùng", Width = (int)(110 * s) });

                foreach (VoucherInfo v in _vouchers)
                {
                    int idx = _grid.Rows.Add(
                        v.Code ?? "",
                        NumberInput.FormatForDisplay(v.Value, 2, true),
                        v.ExpiryDate.HasValue ? v.ExpiryDate.Value.ToString("dd/MM/yyyy") : "");
                    _grid.Rows[idx].Tag = v;
                }
                _grid.CellDoubleClick += delegate { AcceptSelection(); };
                Body.Controls.Add(_grid);
            }
            else
            {
                var lbl = new Label { Text = "Mã voucher:", Dock = DockStyle.Top, Height = (int)(24 * s) };
                _txtCode.Dock = DockStyle.Top;
                _txtCode.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize + 1f);
                Body.Padding = new Padding((int)(16 * s), (int)(12 * s), (int)(16 * s), 0);
                Body.Controls.Add(_txtCode);
                Body.Controls.Add(lbl);
                Shown += delegate { _txtCode.Focus(); };
            }
        }

        private void AcceptSelection()
        {
            if (_fromList)
            {
                if (_grid.CurrentRow == null)
                {
                    Warn(ServicePaymentConstants.VoucherRequiredFromList);
                    return;
                }
                var v = _grid.CurrentRow.Tag as VoucherInfo;
                if (v == null)
                {
                    Warn(ServicePaymentConstants.VoucherRequiredFromList);
                    return;
                }
                SelectedVoucher = v;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            string code = (_txtCode.Text ?? "").Trim();
            if (code.Length == 0)
            {
                Warn(ServicePaymentConstants.VoucherCodeEmpty);
                return;
            }
            VoucherInfo info = _validator != null ? _validator(code) : null;
            if (info == null || !info.IsValid)
            {
                Warn(info != null ? info.Error : ServicePaymentConstants.VoucherNotFound(code));
                _txtCode.SelectAll();
                _txtCode.Focus();
                return;
            }
            SelectedVoucher = info;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Warn(string msg)
        {
            LastWarningForTest = msg;
            if (!SuppressPopup)
                Msg.Warning(this, msg);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            if (_fromList && _grid != null)
                ThemeApplier.ApplyGrid(_grid, ThemeManager.Current);
        }
    }

    /// <summary>Hộp nhập mã thẻ trả trước (DTHETRATRUOC, §3.7).</summary>
    internal sealed class PrepaidCardInputDialog : ThemedDialog
    {
        private readonly Func<string, PrepaidCardInfo> _validator;
        private readonly TextBox _txtCode = new TextBox();

        public PrepaidCardInfo SelectedCard { get; private set; }
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }

        public PrepaidCardInputDialog(Func<string, PrepaidCardInfo> validator)
            : base("THẺ TRẢ TRƯỚC", "Nhập mã thẻ trả trước để kiểm tra số dư và áp dụng.", 400, 210)
        {
            _validator = validator;
            float s = Dpi;
            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AcceptButton = AddButton("Chấp nhận", "primary", delegate { AcceptCode(); });

            var lbl = new Label { Text = "Mã thẻ:", Dock = DockStyle.Top, Height = (int)(24 * s) };
            _txtCode.Dock = DockStyle.Top;
            _txtCode.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize + 1f);
            Body.Padding = new Padding((int)(16 * s), (int)(12 * s), (int)(16 * s), 0);
            Body.Controls.Add(_txtCode);
            Body.Controls.Add(lbl);
            Shown += delegate { _txtCode.Focus(); };
        }

        private void AcceptCode()
        {
            string code = (_txtCode.Text ?? "").Trim();
            if (code.Length == 0)
            {
                Warn(ServicePaymentConstants.PrepaidCodeEmpty);
                return;
            }
            PrepaidCardInfo info = _validator != null ? _validator(code) : null;
            if (info == null || !info.IsValid)
            {
                Warn(info != null ? info.Error : ServicePaymentConstants.PrepaidNotFound(code));
                _txtCode.SelectAll();
                _txtCode.Focus();
                return;
            }
            SelectedCard = info;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Warn(string msg)
        {
            LastWarningForTest = msg;
            if (!SuppressPopup)
                Msg.Warning(this, msg);
        }
    }

    /// <summary>
    /// Hộp trừ tích lũy (đồng bộ 2 chiều điểm ↔ tiền + nút "Sử dụng hết" + chặn vượt điểm và vượt tổng tiền,
    /// theo pos-tap-nghiep-vu-khach-hang-the.md §2).
    /// </summary>
    internal sealed class RedeemPointsDialog : ThemedDialog
    {
        private readonly decimal _availablePoints;
        private readonly decimal _netDue;
        private readonly decimal _pointRate;
        private readonly NumberBox _numAvailPoints = new NumberBox { ReadOnly = true, Maximum = 999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numAvailMoney = new NumberBox { ReadOnly = true, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numUsePoints = new NumberBox { Minimum = 0m, Maximum = 999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numUseMoney = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private bool _syncing;

        public decimal UsedPoints { get { return _numUsePoints.Value; } }
        public decimal UsedMoney { get { return _numUseMoney.Value; } }
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }

        public RedeemPointsDialog(string customerName, decimal availablePoints, decimal netDue, decimal pointRate)
            : base("TRỪ ĐIỂM TÍCH LŨY", "Khách hàng: " + (customerName ?? ""), 440, 320)
        {
            _availablePoints = Math.Max(0m, availablePoints);
            _netDue = Math.Max(0m, netDue);
            _pointRate = pointRate;
            float s = Dpi;

            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Sử dụng hết", null, delegate { UseAll(); });
            AcceptButton = AddButton("Đồng ý", "primary", delegate { AcceptPoints(); });

            _numAvailPoints.Value = _availablePoints;
            _numAvailMoney.Value = PaymentAllocationRules.CalcMoneyFromPoints(_availablePoints, _pointRate);

            _numUsePoints.ValueChanged += delegate { if (!_syncing) SyncFromPoints(); };
            _numUseMoney.ValueChanged += delegate { if (!_syncing) SyncFromMoney(); };

            var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 4, Height = (int)(164 * s) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148 * s));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 4; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));

            AddRow(grid, 0, "Điểm hiện có:", _numAvailPoints);
            AddRow(grid, 1, "Giá trị quy đổi:", _numAvailMoney);
            AddRow(grid, 2, "Điểm sử dụng:", _numUsePoints);
            AddRow(grid, 3, "Số tiền trừ:", _numUseMoney);

            Body.Controls.Add(grid);
            Shown += delegate { _numUsePoints.Focus(); };
        }

        private void SyncFromPoints()
        {
            _syncing = true;
            try
            {
                decimal money = PaymentAllocationRules.CalcMoneyFromPoints(_numUsePoints.Value, _pointRate);
                _numUseMoney.Value = Math.Min(_numUseMoney.Maximum, Math.Max(0m, money));
            }
            finally { _syncing = false; }
        }

        private void SyncFromMoney()
        {
            _syncing = true;
            try
            {
                decimal pts = PaymentAllocationRules.CalcPointsFromMoney(_numUseMoney.Value, _pointRate);
                _numUsePoints.Value = Math.Min(_numUsePoints.Maximum, Math.Max(0m, pts));
            }
            finally { _syncing = false; }
        }

        private void UseAll()
        {
            if (_pointRate <= 0m)
            {
                Warn(ServicePaymentConstants.PointRateNotConfigured);
                return;
            }
            decimal maxPts = PaymentAllocationRules.CalcMaxRedeemablePoints(_availablePoints, _netDue, _pointRate);
            _syncing = true;
            try
            {
                _numUsePoints.Value = maxPts;
                _numUseMoney.Value = Math.Min(_netDue, PaymentAllocationRules.CalcMoneyFromPoints(maxPts, _pointRate));
            }
            finally { _syncing = false; }
            AcceptPoints();
        }

        private void AcceptPoints()
        {
            if (_pointRate <= 0m)
            {
                Warn(ServicePaymentConstants.PointRateNotConfigured);
                return;
            }
            if (_numUsePoints.Value <= 0m || _numUsePoints.Value > _availablePoints)
            {
                Warn(ServicePaymentConstants.PointsRangeWarning(_availablePoints));
                return;
            }
            if (_numUseMoney.Value > _netDue)
            {
                Warn(ServicePaymentConstants.PointsOverDue);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Warn(string msg)
        {
            LastWarningForTest = msg;
            if (!SuppressPopup)
                Msg.Warning(this, msg);
        }

        private static void AddRow(TableLayoutPanel panel, int row, string label, Control ctrl)
        {
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            ctrl.Dock = DockStyle.Fill;
            panel.Controls.Add(ctrl, 1, row);
        }
    }

    /// <summary>
    /// Hộp chọn nhiều bàn đang có khách cho chức năng "Thanh toán chung" (§3.7.3 Q14):
    /// bàn đang chọn được tick sẵn; bắt buộc chọn ít nhất 2 bàn.
    /// </summary>
    internal sealed class GroupPaymentTablePickerDialog : ThemedDialog
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly FlatButton _btnOk;

        public List<string> SelectedBillIds { get; private set; }
        internal DataGridView GridForTest { get { return _grid; } }
        internal FlatButton OkButtonForTest { get { return _btnOk; } }
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }

        public GroupPaymentTablePickerDialog(List<BusyTableForGroupItem> busyTables, string currentTableId)
            : base("THANH TOÁN CHUNG — CHỌN BÀN",
                "Tick ít nhất 2 bàn đang có khách để thanh toán chung một lần.", 680, 440)
        {
            SelectedBillIds = new List<string>();
            float s = Dpi;

            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            _btnOk = AddButton("Tiếp tục thanh toán", "primary", delegate { AcceptSelection(); });
            AcceptButton = _btnOk;

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _grid.RowTemplate.Height = Math.Max((int)(34 * s), 32);
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colSel", HeaderText = "Chọn", Width = (int)(56 * s) });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTable", HeaderText = "Bàn", Width = (int)(110 * s), ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colArea", HeaderText = "Khu vực", Width = (int)(110 * s), ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBill", HeaderText = "Số phiếu", Width = (int)(100 * s), ReadOnly = true });
            var colTotal = new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "Tổng tiền", Width = (int)(110 * s), ReadOnly = true };
            colTotal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(colTotal);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCust", HeaderText = "Khách hàng", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });

            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            if (busyTables != null)
            {
                foreach (BusyTableForGroupItem item in busyTables)
                {
                    bool preChecked = !string.IsNullOrEmpty(currentTableId)
                        && string.Equals(item.TableId, currentTableId, StringComparison.Ordinal);
                    int idx = _grid.Rows.Add(
                        preChecked,
                        item.TableName ?? "",
                        item.AreaName ?? "",
                        item.BillName ?? "",
                        NumberInput.FormatForDisplay(item.TongCong, 2, true),
                        item.CustomerName ?? "");
                    _grid.Rows[idx].Tag = item;
                }
            }

            Body.Controls.Add(_grid);
        }

        private void AcceptSelection()
        {
            _grid.EndEdit();
            var ids = new List<string>();
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var item = r.Tag as BusyTableForGroupItem;
                if (item == null)
                    continue;
                object val = r.Cells["colSel"].Value;
                if (val is bool && (bool)val)
                    ids.Add(item.BillId);
            }
            if (ids.Count < 2)
            {
                LastWarningForTest = ServicePaymentConstants.GroupMinTables;
                if (!SuppressPopup)
                    Msg.Warning(this, ServicePaymentConstants.GroupMinTables);
                return;
            }
            SelectedBillIds = ids;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            if (_grid != null)
                ThemeApplier.ApplyGrid(_grid, ThemeManager.Current);
        }
    }
}
