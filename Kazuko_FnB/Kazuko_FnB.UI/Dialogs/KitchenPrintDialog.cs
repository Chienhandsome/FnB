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
    /// Hộp thoại "IN CHẾ BIẾN" (theo goc-InCheBien-code.cs.txt):
    /// - Khi đã thiết lập máy in: hiển thị các tab nhóm phiếu bếp (Đồ ăn / Đồ uống / gộp theo máy in), bấm "In chế biến" để in và ghi TINCHEBIEN.
    /// - Khi chưa thiết lập máy in (hoặc bấm "Thiết lập máy in"): hiển thị lưới gán máy in theo loại đồ (hoặc theo khu vực + loại đồ) và lưu.
    /// Không chứa SQL; dùng ThemeTokens/ThemeManager; nút cảm ứng >= 44px.
    /// </summary>
    internal sealed class KitchenPrintDialog : ThemedDialog
    {
        private readonly TableService _service;
        private readonly UserSession _session;
        private readonly string _billId;
        private readonly IKitchenPrinterStore _printerStore;
        private readonly KitchenPrintCoordinator _coordinator = new KitchenPrintCoordinator();

        private readonly ThemedTabControl _tabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private readonly Panel _setupPanel = new Panel { Dock = DockStyle.Fill };
        private readonly DataGridView _setupGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _lblHint = new Label { Dock = DockStyle.Top, AutoSize = false };

        private readonly FlatButton _btnPrint;
        private readonly FlatButton _btnSetup;
        private readonly FlatButton _btnSaveSetup;
        private readonly FlatButton _btnCancelSetup;
        private readonly FlatButton _btnClose;

        private readonly Font _bodyFont;
        private readonly Font _boldFont;
        private List<KitchenPrinterSetupRow> _setupRows = new List<KitchenPrinterSetupRow>();
        private KitchenPrintPlan _currentPlan;
        private bool _inSetupMode;

        public bool PrintedSuccessfully { get; private set; }
        public bool NoItemsToPrint { get; private set; }

        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal Func<PrintTemplateRecord> TemplateOverrideForTest { get; set; }
        internal FlatButton PrintButtonForTest { get { return _btnPrint; } }
        internal FlatButton SetupButtonForTest { get { return _btnSetup; } }
        internal FlatButton SaveSetupButtonForTest { get { return _btnSaveSetup; } }
        internal DataGridView SetupGridForTest { get { return _setupGrid; } }
        internal ThemedTabControl TabsForTest { get { return _tabs; } }
        internal bool InSetupModeForTest { get { return _inSetupMode; } }
        internal KitchenPrintPlan CurrentPlanForTest { get { return _currentPlan; } }

        public KitchenPrintDialog(
            TableService service,
            UserSession session,
            string billId,
            IKitchenPrinterStore printerStore = null,
            bool suppressPopup = false)
            : base("IN CHẾ BIẾN", "Kiểm tra món cần in xuống bar/bếp hoặc thiết lập máy in", 640, 460)
        {
            if (service == null) throw new ArgumentNullException("service");
            if (session == null) throw new ArgumentNullException("session");
            _service = service;
            _session = session;
            _billId = billId;
            _printerStore = printerStore ?? KitchenPrintCoordinator.CreateDefaultPrinterStore();
            SuppressPopup = suppressPopup;

            float s = Dpi;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _boldFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            int btnH = Math.Max((int)(44 * s), 44);
            _btnClose = AddButton("Đóng", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            _btnCancelSetup = AddButton("Hủy bỏ", null, delegate { ReloadPlan(false); });
            _btnSaveSetup = AddButton("Lưu thiết lập", "primary", delegate { SavePrinterSetup(); });
            _btnSetup = AddButton("Thiết lập máy in", null, delegate { EnterSetupMode(); });
            _btnPrint = AddButton("In chế biến", "primary", delegate { ExecutePrint(false); });
            foreach (FlatButton b in new[] { _btnClose, _btnCancelSetup, _btnSaveSetup, _btnSetup, _btnPrint })
                b.Height = btnH;
            CompactFooter();
            CancelButton = _btnClose;

            BuildBody(s);
            ApplyDialogTheme();
            ReloadPlan(false);
        }

        private void BuildBody(float s)
        {
            _lblHint.Height = (int)(26 * s);
            _lblHint.Font = _bodyFont;
            _lblHint.TextAlign = ContentAlignment.MiddleLeft;

            BuildSetupGrid(s);
            _setupPanel.Controls.Add(_setupGrid);
            _setupPanel.Controls.Add(_lblHint);

            Body.Controls.Add(_tabs);
            Body.Controls.Add(_setupPanel);
            FitBody(620, 340);
        }

        private void BuildSetupGrid(float s)
        {
            _setupGrid.AllowUserToAddRows = false;
            _setupGrid.AllowUserToDeleteRows = false;
            _setupGrid.AllowUserToResizeRows = false;
            _setupGrid.RowHeadersVisible = false;
            _setupGrid.AutoGenerateColumns = false;
            _setupGrid.Font = _bodyFont;
            _setupGrid.RowTemplate.Height = Math.Max((int)(34 * s), 32);
            _setupGrid.ColumnHeadersHeight = (int)(32 * s);
            _setupGrid.DataError += delegate (object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };

            var colArea = new DataGridViewTextBoxColumn
            {
                Name = "colArea",
                HeaderText = "Khu vực",
                Width = (int)(150 * s),
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var colType = new DataGridViewTextBoxColumn
            {
                Name = "colType",
                HeaderText = "Loại đồ",
                Width = (int)(140 * s),
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            var colPrinter = new DataGridViewComboBoxColumn
            {
                Name = "colPrinter",
                HeaderText = "Máy in",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            _setupGrid.Columns.AddRange(new DataGridViewColumn[] { colArea, colType, colPrinter });
        }

        public void ReloadPlan(bool autoPrint)
        {
            _currentPlan = _service.BuildKitchenPrintPlan(_billId, _printerStore);
            NoItemsToPrint = _currentPlan.NoItemsToPrint;

            if (_currentPlan.NeedsPrinterSetup)
            {
                EnterSetupMode();
                return;
            }

            if (!_currentPlan.Ok)
            {
                SetSetupMode(false);
                _tabs.TabPages.Clear();
                _btnPrint.Enabled = false;
                if (!autoPrint && !string.IsNullOrEmpty(_currentPlan.ErrorMessage))
                    Warn(_currentPlan.ErrorMessage);
                return;
            }

            SetSetupMode(false);
            RenderBatches(_currentPlan.Batches);
            _btnPrint.Enabled = _currentPlan.Batches.Count > 0;
        }

        private void RenderBatches(List<KitchenPrintBatch> batches)
        {
            _tabs.TabPages.Clear();
            float s = Dpi;
            bool showPrice = SystemConfig.InDonGiaTrenMauInBep;
            ThemeTokens t = ThemeManager.Current;

            for (int i = 0; i < batches.Count; i++)
            {
                KitchenPrintBatch batch = batches[i];
                var page = new TabPage(batch.Caption) { BackColor = t.Background, Padding = new Padding((int)(6 * s)) };
                var lblPrinter = new Label
                {
                    Dock = DockStyle.Top,
                    Height = (int)(26 * s),
                    Font = _boldFont,
                    ForeColor = t.Primary,
                    Text = "Máy in: " + (batch.PrinterName ?? ""),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    RowHeadersVisible = false,
                    AutoGenerateColumns = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    Font = _bodyFont,
                    ColumnHeadersHeight = (int)(30 * s)
                };
                grid.RowTemplate.Height = Math.Max((int)(32 * s), 30);
                grid.DataError += delegate (object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };

                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Mặt hàng", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnit", HeaderText = "ĐVT", Width = (int)(70 * s), SortMode = DataGridViewColumnSortMode.NotSortable });
                var colQty = new DataGridViewTextBoxColumn { Name = "colQty", HeaderText = "SL", Width = (int)(66 * s), SortMode = DataGridViewColumnSortMode.NotSortable };
                colQty.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                grid.Columns.Add(colQty);
                if (showPrice)
                {
                    var colPrice = new DataGridViewTextBoxColumn { Name = "colPrice", HeaderText = "Đơn giá", Width = (int)(92 * s), SortMode = DataGridViewColumnSortMode.NotSortable };
                    colPrice.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    grid.Columns.Add(colPrice);
                }
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNote", HeaderText = "Ghi chú", Width = (int)(150 * s), SortMode = DataGridViewColumnSortMode.NotSortable });

                for (int r = 0; r < batch.Items.Count; r++)
                {
                    KitchenPendingItem it = batch.Items[r];
                    string qtyTxt = NumberInput.FormatForDisplay(it.Quantity, 2, true);
                    if (showPrice)
                        grid.Rows.Add(it.ItemName, it.UnitName, qtyTxt, NumberInput.FormatForDisplay(it.UnitPrice, 0, true), it.Note);
                    else
                        grid.Rows.Add(it.ItemName, it.UnitName, qtyTxt, it.Note);
                }

                ThemeApplier.Apply(grid, t);
                page.Controls.Add(grid);
                page.Controls.Add(lblPrinter);
                _tabs.TabPages.Add(page);
            }
        }

        public void EnterSetupMode()
        {
            SetSetupMode(true);
            KitchenPrintSettings settings = TableService.ReadKitchenSettings();
            List<ServiceAreaInfo> areas = settings.ByArea ? _service.LoadAreas() : null;
            _setupRows = KitchenPrintRules.BuildSetupRows(settings, areas, _printerStore);

            _setupGrid.Columns["colArea"].Visible = settings.ByArea;
            var printerCol = (DataGridViewComboBoxColumn)_setupGrid.Columns["colPrinter"];
            printerCol.Items.Clear();
            List<string> installed = _printerStore.GetInstalledPrinters();
            for (int i = 0; i < installed.Count; i++)
                printerCol.Items.Add(installed[i]);

            _setupGrid.Rows.Clear();
            for (int i = 0; i < _setupRows.Count; i++)
            {
                KitchenPrinterSetupRow r = _setupRows[i];
                int idx = _setupGrid.Rows.Add(r.AreaName, r.DrinkTypeLabel, "");
                if (!string.IsNullOrEmpty(r.PrinterName) && printerCol.Items.Contains(r.PrinterName))
                    _setupGrid.Rows[idx].Cells["colPrinter"].Value = r.PrinterName;
                else
                    _setupGrid.Rows[idx].Cells["colPrinter"].Value = null;
            }

            _lblHint.Text = installed.Count == 0
                ? "Chưa tìm thấy máy in nào được cài trên máy."
                : "Chọn máy in tương ứng cho từng loại đồ rồi bấm 'Lưu thiết lập'.";
        }

        private void SavePrinterSetup()
        {
            List<string> installed = _printerStore.GetInstalledPrinters();
            for (int i = 0; i < _setupGrid.Rows.Count; i++)
            {
                string val = Convert.ToString(_setupGrid.Rows[i].Cells["colPrinter"].Value) ?? "";
                if (val.Trim().Length == 0 || !ContainsIgnoreCase(installed, val.Trim()))
                {
                    Warn(ServicePrintConstants.KitchenSelectPrinterPrompt);
                    _setupGrid.ClearSelection();
                    _setupGrid.Rows[i].Selected = true;
                    return;
                }
            }

            for (int i = 0; i < _setupGrid.Rows.Count && i < _setupRows.Count; i++)
            {
                string val = (Convert.ToString(_setupGrid.Rows[i].Cells["colPrinter"].Value) ?? "").Trim();
                _printerStore.SetConfiguredPrinter(_setupRows[i].ConfigKey, val);
            }

            ReloadPlan(false);
        }

        private void ExecutePrint(bool autoPrint)
        {
            if (_inSetupMode || _currentPlan == null || _currentPlan.NeedsPrinterSetup)
            {
                Warn(ServicePrintConstants.KitchenPrinterNotConfigured);
                return;
            }
            if (_currentPlan.Batches == null || _currentPlan.Batches.Count == 0)
            {
                if (!autoPrint)
                    Warn(ServicePrintConstants.KitchenNoRemainingItem);
                return;
            }

            bool ok = _coordinator.ExecuteBatches(
                this,
                _btnPrint,
                _service,
                _session.DatabasePath,
                _session,
                _currentPlan,
                autoPrint,
                PrintOutputHandlerForTest,
                TemplateOverrideForTest,
                delegate (string msg) { LastWarningForTest = msg; });

            if (ok)
            {
                PrintedSuccessfully = true;
                if (!autoPrint)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }

        private void SetSetupMode(bool setup)
        {
            _inSetupMode = setup;
            _setupPanel.Visible = setup;
            _tabs.Visible = !setup;
            _btnSaveSetup.Visible = setup;
            _btnCancelSetup.Visible = setup;
            _btnSetup.Visible = !setup;
            _btnPrint.Visible = !setup;
            AcceptButton = setup ? _btnSaveSetup : _btnPrint;
        }

        private void Warn(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private static bool ContainsIgnoreCase(List<string> list, string target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], target, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            _lblHint.ForeColor = t.TextMuted;
            ThemeApplier.Apply(_setupGrid, t);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bodyFont.Dispose();
                _boldFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
