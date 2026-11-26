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
    /// Hộp thoại chọn bàn đích dùng chung bản đồ khu vực (TableMapView) cho 3 chế độ (§2.2, §2.3, §3.8):
    /// Chuyển bàn (đích trống), Gộp bàn (đích đang dùng), và Chuyển mặt hàng sang bill khác (đích đang dùng + chọn SL).
    /// Double-click ô bàn = xác nhận chọn (OK). Mọi màu/cỡ chữ lấy từ ThemeTokens/ThemeManager; nút ≥ 44px.
    /// </summary>
    internal sealed class MoveMergeTableDialog : ThemedDialog
    {
        private readonly TableTransferMode _mode;
        private readonly string _sourceTableId;
        private readonly decimal _maxItemQty;
        private readonly TableMapView _tableMap = new TableMapView { Dock = DockStyle.Fill, PickerMode = true };
        private readonly Panel _qtyPanel = new Panel { Dock = DockStyle.Top };
        private readonly CheckBox _chkAll = new CheckBox { AutoSize = true };
        private readonly NumberBox _numQty = new NumberBox { Minimum = 0.01m, Maximum = 99999m, DecimalPlaces = 2 };
        private readonly Label _lblSelected = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleLeft };
        private readonly FlatButton _btnOk;
        private readonly Font _bodyFont;
        private readonly Font _headingFont;

        private ServiceTableInfo _selectedTable;

        public ServiceTableInfo SelectedTargetTable { get { return _selectedTable; } }
        /// <summary>True sau khi người dùng xác nhận chọn hợp lệ.</summary>
        public bool Accepted { get; private set; }
        /// <summary>Null khi chuyển toàn bộ dòng; số dương khi tách một phần (chỉ dùng cho TransferItem).</summary>
        public decimal? TransferQty { get; private set; }

        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal TableMapView TableMapForTest { get { return _tableMap; } }
        internal FlatButton OkButtonForTest { get { return _btnOk; } }
        internal CheckBox TransferAllCheckForTest { get { return _chkAll; } }
        internal NumberBox TransferQtyBoxForTest { get { return _numQty; } }

        public MoveMergeTableDialog(
            TableTransferMode mode,
            string sourceTableId,
            string sourceTableName,
            List<ServiceAreaInfo> areas,
            List<ServiceTableInfo> tables,
            ServiceOrderLine transferLine = null)
            : base(TitleFor(mode), SubtitleFor(mode, sourceTableName, transferLine), 760, 540)
        {
            _mode = mode;
            _sourceTableId = sourceTableId ?? "";
            _maxItemQty = transferLine != null && transferLine.Quantity > 0m ? transferLine.Quantity : 1m;
            float s = Dpi;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            int btnH = Math.Max((int)(44 * s), 44);
            FooterPanel.Height = Math.Max((int)(64 * s), btnH + (int)(20 * s));
            FlatButton cancelBtn = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            cancelBtn.Height = btnH;
            CancelButton = cancelBtn;

            _btnOk = AddButton("Đồng ý", "primary", delegate { TryAccept(); });
            _btnOk.Height = btnH;
            AcceptButton = _btnOk;

            if (_mode == TableTransferMode.TransferItem)
                BuildQtyStrip(s, transferLine);

            _lblSelected.Font = _headingFont;
            _lblSelected.Height = (int)(30 * s);
            _lblSelected.Padding = new Padding((int)(4 * s), 0, 0, 0);
            _lblSelected.Text = "Bàn đích đang chọn: (chưa chọn)";

            _tableMap.StopAutoRefresh();
            _tableMap.TableSelected += delegate(object sender, ServiceTableInfo t)
            {
                _selectedTable = t;
                UpdateSelectedLabel();
            };
            _tableMap.TableDoubleClicked += delegate(object sender, ServiceTableInfo t)
            {
                _selectedTable = t;
                UpdateSelectedLabel();
                TryAccept();
            };
            _tableMap.RenderData(areas ?? new List<ServiceAreaInfo>(), tables ?? new List<ServiceTableInfo>());

            Body.Controls.Add(_tableMap);
            Body.Controls.Add(_lblSelected);
            if (_mode == TableTransferMode.TransferItem)
                Body.Controls.Add(_qtyPanel);

            ApplyDialogTheme();
        }

        private void BuildQtyStrip(float s, ServiceOrderLine line)
        {
            _qtyPanel.Height = (int)(48 * s);
            _qtyPanel.Padding = new Padding((int)(4 * s), (int)(6 * s), (int)(4 * s), (int)(6 * s));

            string qtyText = NumberInput.FormatForDisplay(_maxItemQty, 2, true);
            _chkAll.Text = "Chuyển toàn bộ dòng (SL: " + qtyText + ")";
            _chkAll.Font = _bodyFont;
            _chkAll.Checked = _maxItemQty <= 1m;
            _chkAll.Dock = DockStyle.Left;
            _chkAll.Padding = new Padding(0, 0, (int)(16 * s), 0);

            var lblQty = new Label
            {
                Text = "Số lượng chuyển:",
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont,
                Padding = new Padding((int)(8 * s), 0, (int)(6 * s), 0)
            };

            _numQty.Maximum = Math.Max(0.01m, _maxItemQty);
            _numQty.Value = _maxItemQty > 1m ? 1m : _maxItemQty;
            _numQty.Width = (int)(110 * s);
            _numQty.Font = _bodyFont;
            _numQty.Dock = DockStyle.Left;
            _numQty.Enabled = !_chkAll.Checked;

            _chkAll.CheckedChanged += delegate { _numQty.Enabled = !_chkAll.Checked; };

            // Thứ tự Add cho DockStyle.Left: thêm từ phải sang trái để hiển thị đúng [CheckBox] [Label] [NumberBox].
            _qtyPanel.Controls.Add(_numQty);
            _qtyPanel.Controls.Add(lblQty);
            _qtyPanel.Controls.Add(_chkAll);
        }

        private void UpdateSelectedLabel()
        {
            if (_selectedTable == null)
            {
                _lblSelected.Text = "Bàn đích đang chọn: (chưa chọn)";
                return;
            }
            string status = ServiceOrderConstants.TableStatusLabel(_selectedTable.Status);
            _lblSelected.Text = string.Format("Bàn đích đang chọn: {0} ({1} — {2})",
                _selectedTable.Name ?? "", _selectedTable.AreaName ?? "", status);
        }

        private void TryAccept()
        {
            if (_selectedTable == null)
            {
                Warn(ServiceTransferConstants.ChooseDifferentTable);
                return;
            }

            if (string.Equals(_selectedTable.Id, _sourceTableId, StringComparison.Ordinal))
            {
                Warn(ServiceTransferConstants.ChooseDifferentTable);
                return;
            }

            bool targetHasBill = !string.IsNullOrEmpty(_selectedTable.ActiveBillId)
                || _selectedTable.Status == TableStatus.InUse
                || _selectedTable.Status == TableStatus.TempPrinted;

            if (_mode == TableTransferMode.MoveTable)
            {
                if (targetHasBill)
                {
                    Warn(ServiceTransferConstants.DestTableInUseForMove);
                    return;
                }
            }
            else if (_mode == TableTransferMode.MergeTable)
            {
                if (!targetHasBill)
                {
                    Warn(ServiceTransferConstants.DestTableEmptyForMerge);
                    return;
                }
            }
            else
            {
                if (!targetHasBill)
                {
                    Warn(ServiceTransferConstants.DestTableEmptyForItemTransfer);
                    return;
                }
                if (!_chkAll.Checked && (_numQty.Value <= 0m || _numQty.Value > _maxItemQty))
                {
                    Warn(ServiceTransferConstants.TransferItemInvalidQty);
                    return;
                }
            }

            TransferQty = _mode != TableTransferMode.TransferItem || _chkAll.Checked ? (decimal?)null : _numQty.Value;
            Accepted = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Warn(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            if (_tableMap != null)
                _tableMap.ApplyTheme(t);
            if (_qtyPanel != null)
            {
                _qtyPanel.BackColor = t.Surface;
                _chkAll.ForeColor = t.Text;
            }
            if (_lblSelected != null)
                _lblSelected.ForeColor = t.Primary;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bodyFont.Dispose();
                _headingFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private static string TitleFor(TableTransferMode mode)
        {
            switch (mode)
            {
                case TableTransferMode.MergeTable: return "GỘP BÀN";
                case TableTransferMode.TransferItem: return "CHUYỂN MẶT HÀNG";
                default: return "CHUYỂN BÀN";
            }
        }

        private static string SubtitleFor(TableTransferMode mode, string sourceTableName, ServiceOrderLine line)
        {
            string tbl = sourceTableName ?? "";
            switch (mode)
            {
                case TableTransferMode.MergeTable:
                    return string.Format("Chọn bàn đang có khách để gộp hóa đơn từ bàn '{0}' (double-click để chọn).", tbl);
                case TableTransferMode.TransferItem:
                    string itemName = line != null ? (line.ItemName ?? "") : "";
                    string qty = line != null ? NumberInput.FormatForDisplay(line.Quantity, 2, true) : "0";
                    return string.Format("Chọn bàn đang có khách để chuyển món '{0}' (SL: {1}) từ bàn '{2}'.", itemName, qty, tbl);
                default:
                    return string.Format("Chọn bàn trống để chuyển hóa đơn từ bàn '{0}' (double-click để chọn).", tbl);
            }
        }
    }
}
