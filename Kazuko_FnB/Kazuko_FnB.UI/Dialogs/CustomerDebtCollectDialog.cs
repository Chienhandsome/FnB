using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại "Thu nợ theo đơn" cho khách hàng (docs/specs/cong-no.md §3, §6 #5, §7 T1):
    /// liệt kê các bill còn nợ của khách, cột "Lần này" mặc định phân bổ từ đơn cũ nhất,
    /// chọn tiền mặt hoặc chuyển khoản + tài khoản ngân hàng, ghi MỘT phiếu thu TTHUCHI
    /// (LOAI = 0, LAPHIEUTHUCONGNO = 30) kèm các dòng TCHITIETTHANHTOAN trong một transaction.
    /// Phần nạp dữ liệu, phân bổ, sự kiện lưới và lưu đặt ở <c>CustomerDebtCollectDialog.Actions.cs</c>.
    /// </summary>
    internal sealed partial class CustomerDebtCollectDialog : ThemedDialog
    {
        private readonly UserSession _session;
        private readonly DebtService _service;
        private readonly FormRepository _formRepo;
        private readonly DebtPartnerRow _customer;
        private readonly bool _readOnly;

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly List<FieldFrame> _frames = new List<FieldFrame>();

        private readonly TextBox _txtCustomer = new TextBox { ReadOnly = true };
        private readonly Label _lblCurrentDebt = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly TextBox _txtNumber = new TextBox();
        private readonly DateBox _dtDate = new DateBox();
        private readonly LookupCombo _store = new LookupCombo();
        private readonly LookupCombo _reason = new LookupCombo();
        private readonly NumberBox _numAmount = new NumberBox { DecimalPlaces = 0, Minimum = 0m, Maximum = 1000000000000m, TextAlign = HorizontalAlignment.Right };
        private readonly FlatButton _btnAutoAlloc = new FlatButton { Text = "Phân bổ cũ nhất" };
        private readonly CheckBox _chkTransfer = new CheckBox { Text = "Chuyển khoản", AutoSize = true };
        private readonly LookupCombo _bank = new LookupCombo();
        private readonly TextBox _txtDescription = new TextBox();
        private readonly TextBox _txtNote = new TextBox();

        private readonly DataGridView _billGrid = new DataGridView();
        private readonly Label _emptyBills = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        private readonly Label _lblAllocSummary = new Label { AutoSize = false, Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleRight };

        private readonly FlatButton _btnClose;
        private readonly FlatButton _btnSave;

        private List<DebtAllocationLine> _allocations = new List<DebtAllocationLine>();
        private bool _syncing;
        private bool _cellParseFailed;

        public bool SavedOk { get; private set; }
        public string SavedVoucherId { get; private set; }
        public string SavedVoucherNumber { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal int DataErrorCountForTest { get; private set; }
        internal string DataErrorDetailForTest { get; private set; }
        internal TextBox NumberBoxForTest { get { return _txtNumber; } }
        internal DateBox DateBoxForTest { get { return _dtDate; } }
        internal LookupCombo StoreComboForTest { get { return _store; } }
        internal LookupCombo ReasonComboForTest { get { return _reason; } }
        internal NumberBox AmountBoxForTest { get { return _numAmount; } }
        internal FlatButton AutoAllocButtonForTest { get { return _btnAutoAlloc; } }
        internal CheckBox TransferCheckForTest { get { return _chkTransfer; } }
        internal LookupCombo BankComboForTest { get { return _bank; } }
        internal TextBox DescriptionBoxForTest { get { return _txtDescription; } }
        internal TextBox NoteBoxForTest { get { return _txtNote; } }
        internal DataGridView BillGridForTest { get { return _billGrid; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }

        public CustomerDebtCollectDialog(UserSession session, DebtPartnerRow customer, bool readOnly = false)
            : base(
                BuildTitle(session, readOnly),
                BuildSubtitle(session, readOnly),
                820,
                600)
        {
            if (customer == null)
                throw new ArgumentNullException("customer");

            _session = session ?? AppSession.Current;
            _customer = customer;
            _service = new DebtService(_session);
            _formRepo = new FormRepository(_session.DatabasePath);
            _readOnly = readOnly || !_service.CanCollectCustomer;

            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);

            float s = Dpi;
            BuildFormLayout(s);
            BuildBillGrid(s);

            _btnClose = AddButton("Đóng", null, delegate { Close(); });
            _btnSave = AddButton("Lưu (Ctrl+L)", "primary", delegate { Save(); });
            CancelButton = _btnClose;

            LoadLookups();
            LoadOpenBills();
            ApplyModeState();
            ApplyDialogTheme();

            Shown += delegate
            {
                if (!_readOnly && _allocations.Count > 0)
                    _numAmount.Focus();
            };
        }

        private static string BuildTitle(UserSession session, bool readOnly)
        {
            bool ro = readOnly || !DebtRules.CanCollectCustomerDebt(session ?? AppSession.Current);
            return ro ? "THU NỢ THEO ĐƠN — CHỈ XEM" : "THU NỢ KHÁCH HÀNG THEO ĐƠN";
        }

        private static string BuildSubtitle(UserSession session, bool readOnly)
        {
            bool ro = readOnly || !DebtRules.CanCollectCustomerDebt(session ?? AppSession.Current);
            return ro
                ? "Chế độ chỉ xem (không có quyền tạo phiếu thu)."
                : "Phân bổ tiền thu vào từng hóa đơn còn nợ (mặc định từ đơn cũ nhất). Ctrl+L: Lưu, ESC: Đóng.";
        }

        private void BuildFormLayout(float s)
        {
            int lbl1W = MeasureMaxWidth(new[] { "Khách hàng", "Số phiếu *", "Cửa hàng *", "Số tiền thu *", "Hình thức", "Diễn giải" }, _bodyFont, (int)(16 * s));
            int lbl2W = MeasureMaxWidth(new[] { "Tổng nợ hiện tại", "Ngày", "Lý do thu", "Tài khoản NH", "Ghi chú" }, _bodyFont, (int)(16 * s));

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                RowCount = 6,
                Padding = new Padding(0, 0, 0, (int)(8 * s)),
                Height = (int)(6 * 36 * s + 10 * s)
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lbl1W));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lbl2W));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            for (int r = 0; r < 6; r++)
                top.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));

            // Hàng 0: Khách hàng | Tổng nợ hiện tại
            _txtCustomer.Font = _bodyFont;
            _txtCustomer.Text = string.IsNullOrEmpty(_customer.Code)
                ? (_customer.Name ?? "")
                : string.Format("{0} — {1}", _customer.Code, _customer.Name ?? "");
            _lblCurrentDebt.Font = _headingFont;
            _lblCurrentDebt.Dock = DockStyle.Fill;
            _lblCurrentDebt.Text = DebtConstants.FormatMoneyVi(_customer.RemainingDebt) + " đ";

            AddPair(top, 0, 0, "Khách hàng", _txtCustomer, s);
            AddPair(top, 0, 2, "Tổng nợ hiện tại", _lblCurrentDebt, s);

            // Hàng 1: Số phiếu * | Ngày
            _txtNumber.Font = _bodyFont;
            _dtDate.Inner.Font = _bodyFont;
            _frames.Add(_dtDate);
            AddPair(top, 1, 0, "Số phiếu *", _txtNumber, s);
            AddPair(top, 1, 2, "Ngày", _dtDate, s);

            // Hàng 2: Cửa hàng * | Lý do thu
            _store.Font = _bodyFont;
            _reason.Font = _bodyFont;
            AddPair(top, 2, 0, "Cửa hàng *", _store, s);
            AddPair(top, 2, 2, "Lý do thu", _reason, s);

            // Hàng 3: Số tiền thu * (+ nút Phân bổ cũ nhất) | (trống hoặc mở rộng)
            _numAmount.Font = _bodyFont;
            _numAmount.ValueChanged += delegate
            {
                if (!_syncing && !_readOnly)
                    ApplyAutoAllocate(_numAmount.Value);
            };
            _btnAutoAlloc.Font = _bodyFont;
            _btnAutoAlloc.Height = (int)(30 * s);
            _btnAutoAlloc.Width = TextRenderer.MeasureText(_btnAutoAlloc.Text, _bodyFont).Width + (int)(20 * s);
            _btnAutoAlloc.Click += delegate
            {
                if (!_readOnly)
                    ApplyAutoAllocate(_numAmount.Value);
            };

            var amountHost = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, (int)(2 * s), (int)(10 * s), (int)(2 * s))
            };
            amountHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            amountHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _btnAutoAlloc.Width + (int)(6 * s)));
            amountHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _numAmount.Dock = DockStyle.Fill;
            _numAmount.Margin = new Padding(0, (int)(2 * s), (int)(6 * s), 0);
            _btnAutoAlloc.Anchor = AnchorStyles.Right;
            _btnAutoAlloc.Margin = new Padding(0);
            amountHost.Controls.Add(_numAmount, 0, 0);
            amountHost.Controls.Add(_btnAutoAlloc, 1, 0);

            top.Controls.Add(MakeFieldLabel("Số tiền thu *"), 0, 3);
            top.Controls.Add(amountHost, 1, 3);
            top.SetColumnSpan(amountHost, 3);

            // Hàng 4: Hình thức (Chuyển khoản) | Tài khoản ngân hàng
            _chkTransfer.Font = _bodyFont;
            _chkTransfer.Anchor = AnchorStyles.Left;
            _chkTransfer.CheckedChanged += delegate
            {
                _bank.Enabled = !_readOnly && _chkTransfer.Checked;
                if (!_chkTransfer.Checked)
                    _bank.SelectId(null);
            };
            _bank.Font = _bodyFont;
            AddPair(top, 4, 0, "Hình thức", _chkTransfer, s);
            AddPair(top, 4, 2, "Tài khoản NH", _bank, s);

            // Hàng 5: Diễn giải | Ghi chú
            _txtDescription.Font = _bodyFont;
            _txtNote.Font = _bodyFont;
            AddPair(top, 5, 0, "Diễn giải", _txtDescription, s);
            AddPair(top, 5, 2, "Ghi chú", _txtNote, s);

            _lblAllocSummary.Font = _headingFont;
            _lblAllocSummary.Height = (int)(28 * s);
            _lblAllocSummary.Padding = new Padding(0, (int)(4 * s), (int)(4 * s), 0);

            var gridHeader = new Label
            {
                Text = "Danh sách hóa đơn còn nợ (nhập số tiền thu ở cột \"Lần này\")",
                Dock = DockStyle.Top,
                Height = (int)(26 * s),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _headingFont
            };

            Body.Controls.Add(_billGrid);
            Body.Controls.Add(gridHeader);
            Body.Controls.Add(_lblAllocSummary);
            Body.Controls.Add(top);
            _billGrid.BringToFront();
        }

        private void BuildBillGrid(float s)
        {
            _billGrid.Dock = DockStyle.Fill;
            _billGrid.AllowUserToAddRows = false;
            _billGrid.AllowUserToDeleteRows = false;
            _billGrid.AllowUserToResizeRows = false;
            _billGrid.AutoGenerateColumns = false;
            _billGrid.MultiSelect = false;
            _billGrid.ScrollBars = ScrollBars.Both;
            _billGrid.RowHeadersVisible = true;
            _billGrid.RowHeadersWidth = (int)(40 * s);
            _billGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _billGrid.EditMode = DataGridViewEditMode.EditOnEnter;
            _billGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _billGrid.ColumnHeadersHeight = (int)(34 * s);
            _billGrid.RowTemplate.Height = (int)(30 * s);
            _billGrid.Font = _bodyFont;
            _billGrid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            _billGrid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(_billGrid, true, null);

            AddGridCol("b_num", "Số phiếu", 130, "HD26/00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);
            AddGridCol("b_date", "Ngày", 104, "00/00/0000", typeof(DateTime), "dd/MM/yyyy", DataGridViewContentAlignment.MiddleCenter, true, s);
            AddGridCol("b_total", "Tổng cộng", 135, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight, true, s);
            AddGridCol("b_rem", "Còn nợ", 135, "999.999.999", typeof(decimal), "N0", DataGridViewContentAlignment.MiddleRight, true, s);
            DataGridViewTextBoxColumn payCol = AddGridCol("b_pay", "Lần này", 145, "999.999.999", typeof(decimal), "#,##0", DataGridViewContentAlignment.MiddleRight, false, s);
            NumericEditGridCell.ConfigureColumn(payCol, 0);
            payCol.MinimumWidth = payCol.Width;
            payCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _billGrid.CellParsing += OnBillCellParsing;
            _billGrid.CellEndEdit += OnBillCellEndEdit;
            _billGrid.CurrentCellDirtyStateChanged += delegate
            {
                NumericEditGridCell.HandleDirtyStateChanged(_billGrid, null);
            };
            _billGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                DataErrorCountForTest++;
                DataErrorDetailForTest = NumericEditGridCell.AppendDataErrorDetail(DataErrorDetailForTest, _billGrid, e);
                e.ThrowException = false;
                if (e.RowIndex >= 0 && e.RowIndex < _allocations.Count)
                    SyncBillRow(e.RowIndex);
            };
            _billGrid.RowPostPaint += DrawBillRowNumber;

            _emptyBills.Font = _bodyFont;
            _emptyBills.Text = DebtConstants.NoOpenBillsMessage;
            _billGrid.Controls.Add(_emptyBills);
            _billGrid.Resize += delegate { _emptyBills.SetBounds(0, _billGrid.Height / 2 - 15, _billGrid.Width, 30); };
        }

        private void AddPair(TableLayoutPanel panel, int row, int col, string caption, Control editor, float s)
        {
            panel.Controls.Add(MakeFieldLabel(caption), col, row);
            Control host = editor;
            if (editor is DateBox)
            {
                host.Height = (int)(30 * s);
            }
            else if (editor is TextBox || editor is ComboBox)
            {
                host.Height = (int)(28 * s);
            }
            host.Anchor = editor is CheckBox ? AnchorStyles.Left : (AnchorStyles.Left | AnchorStyles.Right);
            host.Margin = new Padding(0, (int)(3 * s), col == 0 ? (int)(12 * s) : 0, (int)(3 * s));
            panel.Controls.Add(host, col + 1, row);
        }

        private Label MakeFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont
            };
        }

        private DataGridViewTextBoxColumn AddGridCol(
            string name, string header, int minWidth96, string sample,
            Type type, string format, DataGridViewContentAlignment align, bool readOnly, float s)
        {
            int w = (int)Math.Ceiling(minWidth96 * s);
            int hdrW = TextRenderer.MeasureText(header, _headingFont).Width + (int)Math.Ceiling(28 * s);
            if (hdrW > w) w = hdrW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sw = TextRenderer.MeasureText(sample, _bodyFont).Width + (int)Math.Ceiling(24 * s);
                if (sw > w) w = sw;
            }
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = w,
                ValueType = type,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = align;
            if (align == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (format != null)
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
            }
            _billGrid.Columns.Add(col);
            return col;
        }

        private static int MeasureMaxWidth(string[] texts, Font font, int pad)
        {
            int max = 0;
            foreach (string t in texts)
            {
                int w = TextRenderer.MeasureText(t, font).Width + pad;
                if (w > max) max = w;
            }
            return max;
        }
    }
}
