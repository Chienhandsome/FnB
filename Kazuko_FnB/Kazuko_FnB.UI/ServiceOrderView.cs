using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Sử dụng dịch vụ" (docs/specs/su-dung-dich-vu.md §2.1, §3.1–§3.6, §4, §6.4–§6.6):
    /// Trái = sơ đồ bàn theo khu vực (TableMapView); Phải = đầu phiếu hóa đơn + lưới chi tiết 7 cột +
    /// thanh nút dòng + cột nút thao tác + MenuPickerView + tổng cộng &amp; nút cuối.
    /// Phần sự kiện và thao tác nghiệp vụ nằm ở ServiceOrderView.Actions.cs.
    /// Không chứa SQL; mọi màu và cỡ chữ lấy từ ThemeTokens/ThemeManager; nút cảm ứng ≥ 44px.
    /// </summary>
    internal sealed partial class ServiceOrderView : Panel
    {
        public const string MenuId = ServiceOrderConstants.MenuId;

        private readonly UserSession _session;
        private readonly TableService _service;
        private readonly bool _canViewPrice;

        private readonly SplitContainer _mainSplit = new SplitContainer { Dock = DockStyle.Fill };
        private readonly TableMapView _tableMap = new TableMapView { Dock = DockStyle.Fill };
        private readonly MenuPickerView _menuPicker = new MenuPickerView { Dock = DockStyle.Right };

        // ---- Đầu phiếu hóa đơn (§2.1) ----
        private readonly Panel _headerPanel = new Panel { Dock = DockStyle.Top };
        private readonly TextBox _txtStartTime = new TextBox { ReadOnly = true };
        private readonly FlatButton _btnStart = new FlatButton { Text = "Bắt đầu", Tag = "primary" };
        private readonly TextBox _txtDate = new TextBox { ReadOnly = true };
        private readonly TextBox _txtBillNo = new TextBox { ReadOnly = true };
        private readonly NumberBox _numGuests = new NumberBox { Minimum = 1m, Maximum = 999m, DecimalPlaces = 0, Value = 1m };
        private readonly LookupCombo _cboCustomer = new LookupCombo();
        private readonly TextBox _txtBillNote = new TextBox();

        // ---- Thanh công cụ trên lưới chi tiết ----
        private readonly FlowLayoutPanel _lineBar = new FlowLayoutPanel { Dock = DockStyle.Top, WrapContents = true, AutoSize = true };
        private readonly FlatButton _btnPlus = new FlatButton { Text = "+" };
        private readonly FlatButton _btnMinus = new FlatButton { Text = "−" };
        private readonly FlatButton _btnSetQty = new FlatButton { Text = "Đặt sl" };
        private readonly FlatButton _btnPrice = new FlatButton { Text = "Đổi giá" };
        private readonly FlatButton _btnNote = new FlatButton { Text = "Ghi chú" };
        private readonly FlatButton _btnLineDiscount = new FlatButton { Text = "CK%" };

        // ---- Lưới chi tiết 7 cột: Tên hàng | ĐVT | SL | Đ giá | CK% | T tiền | Ghi chú ----
        private readonly DataGridView _detailGrid = new DataGridView { Dock = DockStyle.Fill };

        // ---- Cột nút thao tác giữa lưới và cột chọn món ----
        private readonly Panel _actionCol = new Panel { Dock = DockStyle.Right };
        private readonly FlatButton _btnMoveTable = new FlatButton { Text = "Chuyển bàn" };
        private readonly FlatButton _btnMergeTable = new FlatButton { Text = "Gộp bàn" };
        private readonly NumberBox _numAddQty = new NumberBox { Minimum = 0.01m, Maximum = 99999m, DecimalPlaces = 2, Value = 1m };
        private readonly FlatButton _btnAdd = new FlatButton { Text = "<< Thêm", Tag = "primary" };
        private readonly FlatButton _btnReduce = new FlatButton { Text = "Giảm >>" };
        private readonly FlatButton _btnDelete = new FlatButton { Text = "Xóa", Tag = "danger" };
        private readonly FlatButton _btnGroupDiscount = new FlatButton { Text = "% theo nhóm" };
        private readonly FlatButton _btnKitchenPrint = new FlatButton { Text = "In chế biến (F10)" };

        // ---- Phần dưới: Tổng cộng + nút thanh toán/thống kê/in lại ----
        private readonly Panel _bottomPanel = new Panel { Dock = DockStyle.Bottom };
        private readonly TableLayoutPanel _totalsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 3 };
        private readonly NumberBox _numTienHang = new NumberBox { ReadOnly = true, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numTileGiam = new NumberBox { Minimum = 0m, Maximum = 100m, DecimalPlaces = 2 };
        private readonly NumberBox _numTienGiam = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numTilePhi = new NumberBox { Minimum = 0m, Maximum = 100m, DecimalPlaces = 2 };
        private readonly NumberBox _numPhiDichVu = new NumberBox { Minimum = 0m, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numTileThue = new NumberBox { Minimum = 0m, Maximum = 100m, DecimalPlaces = 2 };
        private readonly NumberBox _numTienThue = new NumberBox { ReadOnly = true, Maximum = 9999999999m, DecimalPlaces = 2 };
        private readonly NumberBox _numTongCong = new NumberBox { ReadOnly = true, Maximum = 9999999999m, DecimalPlaces = 2 };

        private readonly FlowLayoutPanel _bottomButtons = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        private readonly FlatButton _btnPay = new FlatButton { Text = "Thanh toán (F11)", Tag = "primary" };
        private readonly FlatButton _btnStats = new FlatButton { Text = "Thống kê (F9)" };
        private readonly FlatButton _btnReprint = new FlatButton { Text = "In lại bill (F8)" };

        private readonly Timer _lockRenewTimer = new Timer();
        private readonly Font _bodyFont;
        private readonly Font _headingFont;

        private ServiceTableInfo _currentTable;
        private ServiceOrderHeader _currentBill;
        private List<ServiceOrderLine> _displayedLines = new List<ServiceOrderLine>();
        private bool _updatingUi;
        private bool _splitPlaced;
        private ThemeTokens _theme = ThemeManager.Current;

        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal string LastInfoForTest { get; private set; }
        internal int DetailDataErrorCountForTest { get; private set; }
        internal TableMapView TableMapForTest { get { return _tableMap; } }
        internal MenuPickerView MenuPickerForTest { get { return _menuPicker; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal FlatButton StartButtonForTest { get { return _btnStart; } }
        internal FlatButton AddButtonForTest { get { return _btnAdd; } }
        internal FlatButton PlusButtonForTest { get { return _btnPlus; } }
        internal FlatButton MinusButtonForTest { get { return _btnMinus; } }
        internal FlatButton ReduceButtonForTest { get { return _btnReduce; } }
        internal FlatButton DeleteButtonForTest { get { return _btnDelete; } }
        internal FlatButton PayButtonForTest { get { return _btnPay; } }
        internal FlatButton KitchenPrintButtonForTest { get { return _btnKitchenPrint; } }
        internal FlatButton MoveTableButtonForTest { get { return _btnMoveTable; } }
        internal NumberBox AddQtyBoxForTest { get { return _numAddQty; } }
        internal NumberBox TongCongBoxForTest { get { return _numTongCong; } }
        internal NumberBox TileGiamBoxForTest { get { return _numTileGiam; } }
        internal NumberBox TienGiamBoxForTest { get { return _numTienGiam; } }
        internal Panel TotalsHostForTest { get { return _totalsGrid; } }
        internal ServiceOrderHeader CurrentBillForTest { get { return _currentBill; } }
        internal Timer LockRenewTimerForTest { get { return _lockRenewTimer; } }

        public ServiceOrderView(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _session = session;
            _service = new TableService(session);
            _canViewPrice = _service.CanViewPrice();

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            BuildHeader(s);
            BuildLineToolbar(s);
            BuildDetailGrid(s);
            BuildActionColumn(s);
            BuildBottomPanel(s);

            var centerRight = new Panel { Dock = DockStyle.Fill };
            var gridHost = new Panel { Dock = DockStyle.Fill };
            gridHost.Controls.Add(_detailGrid);
            gridHost.Controls.Add(_lineBar);

            _menuPicker.Width = (int)(260 * s);
            centerRight.Controls.Add(gridHost);
            centerRight.Controls.Add(_actionCol);
            centerRight.Controls.Add(_menuPicker);

            _mainSplit.Panel1.Controls.Add(_tableMap);
            _mainSplit.Panel2.Controls.Add(centerRight);
            _mainSplit.Panel2.Controls.Add(_headerPanel);
            _mainSplit.Panel2.Controls.Add(_bottomPanel);
            _mainSplit.SizeChanged += delegate { PlaceMainSplit(); };
            Controls.Add(_mainSplit);

            WireEvents();

            _lockRenewTimer.Interval = ServiceOrderConstants.TableLockRenewSeconds * 1000;
            _lockRenewTimer.Tick += delegate
            {
                if (_currentTable != null && _currentBill != null)
                    _service.RenewLock(_currentTable.Id);
            };

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            _menuPicker.SetCanViewPrice(_canViewPrice);
            _totalsGrid.Visible = _canViewPrice;
            _btnPay.Visible = _canViewPrice;

            ReloadAll();
            _tableMap.StartAutoRefresh();
            UpdateControlStates();
        }

        private void PlaceMainSplit()
        {
            if (_splitPlaced || _mainSplit.Width < 500)
                return;
            int want = Math.Max((int)(300 * DeviceDpi / 96f), (int)(_mainSplit.Width * 0.30));
            if (_mainSplit.Width > want + 320)
            {
                _mainSplit.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        private void BuildHeader(float s)
        {
            _headerPanel.Height = (int)(96 * s);
            _headerPanel.Padding = new Padding((int)(6 * s), (int)(4 * s), (int)(6 * s), (int)(4 * s));

            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2 };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 48 * s));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 40 * s));

            _btnStart.Height = Math.Max((int)(44 * s), 44);
            _btnStart.Dock = DockStyle.Fill;
            _btnStart.Margin = new Padding((int)(4 * s), (int)(2 * s), (int)(8 * s), (int)(2 * s));
            _btnStart.Font = _headingFont;

            StyleHeaderBox(_txtStartTime);
            StyleHeaderBox(_txtDate);
            StyleHeaderBox(_txtBillNo);
            StyleHeaderBox(_numGuests);
            StyleHeaderBox(_cboCustomer);
            StyleHeaderBox(_txtBillNote);

            tbl.Controls.Add(MakeLabel("Giờ vào"), 0, 0);
            tbl.Controls.Add(_txtStartTime, 1, 0);
            tbl.Controls.Add(_btnStart, 2, 0);
            tbl.Controls.Add(MakeLabel("Ngày"), 3, 0);
            tbl.Controls.Add(_txtDate, 4, 0);
            tbl.Controls.Add(MakeLabel("Số phiếu"), 5, 0);
            tbl.Controls.Add(_txtBillNo, 6, 0);

            var guestHost = new Panel { Dock = DockStyle.Fill };
            var lblGuests = MakeLabel("Số khách");
            lblGuests.Dock = DockStyle.Left;
            lblGuests.Width = (int)(66 * s);
            _numGuests.Dock = DockStyle.Fill;
            guestHost.Controls.Add(_numGuests);
            guestHost.Controls.Add(lblGuests);
            tbl.Controls.Add(guestHost, 7, 0);

            tbl.Controls.Add(MakeLabel("Khách (F12)"), 0, 1);
            tbl.Controls.Add(_cboCustomer, 1, 1);
            tbl.SetColumnSpan(_cboCustomer, 4);
            tbl.Controls.Add(MakeLabel("Ghi chú"), 5, 1);
            tbl.Controls.Add(_txtBillNote, 6, 1);
            tbl.SetColumnSpan(_txtBillNote, 2);

            _headerPanel.Controls.Add(tbl);
        }

        private void BuildLineToolbar(float s)
        {
            _lineBar.Padding = new Padding((int)(4 * s), (int)(4 * s), (int)(4 * s), (int)(4 * s));
            int h = Math.Max((int)(44 * s), 44);
            SetupBarButton(_btnPlus, (int)(48 * s), h, s);
            SetupBarButton(_btnMinus, (int)(48 * s), h, s);
            SetupBarButton(_btnSetQty, (int)(74 * s), h, s);
            SetupBarButton(_btnPrice, (int)(78 * s), h, s);
            SetupBarButton(_btnNote, (int)(78 * s), h, s);
            SetupBarButton(_btnLineDiscount, (int)(62 * s), h, s);
            _lineBar.Controls.AddRange(new Control[] { _btnPlus, _btnMinus, _btnSetQty, _btnPrice, _btnNote, _btnLineDiscount });
        }

        private void SetupBarButton(FlatButton btn, int w, int h, float s)
        {
            btn.Size = new Size(w, h);
            btn.Font = _bodyFont;
            btn.Margin = new Padding(0, 0, (int)(4 * s), 0);
        }

        private void BuildDetailGrid(float s)
        {
            _detailGrid.ReadOnly = true;
            _detailGrid.AllowUserToAddRows = false;
            _detailGrid.AllowUserToDeleteRows = false;
            _detailGrid.AllowUserToResizeRows = false;
            _detailGrid.MultiSelect = false;
            _detailGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _detailGrid.RowHeadersVisible = false;
            _detailGrid.AutoGenerateColumns = false;
            _detailGrid.Font = _bodyFont;
            _detailGrid.RowTemplate.Height = Math.Max((int)(36 * s), 34);
            _detailGrid.ColumnHeadersHeight = (int)(34 * s);
            _detailGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _detailGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                DetailDataErrorCountForTest++;
                e.ThrowException = false;
            };

            AddCol("colItemName", "Tên hàng", (int)(160 * s), DataGridViewContentAlignment.MiddleLeft, true);
            AddCol("colUnit", "ĐVT", (int)(62 * s), DataGridViewContentAlignment.MiddleCenter, false);
            AddCol("colQty", "SL", (int)(56 * s), DataGridViewContentAlignment.MiddleRight, false);
            AddCol("colPrice", "Đ giá", (int)(86 * s), DataGridViewContentAlignment.MiddleRight, false);
            AddCol("colDisc", "CK%", (int)(54 * s), DataGridViewContentAlignment.MiddleRight, false);
            AddCol("colAmount", "T tiền", (int)(96 * s), DataGridViewContentAlignment.MiddleRight, false);
            AddCol("colNote", "Ghi chú", (int)(100 * s), DataGridViewContentAlignment.MiddleLeft, false);

            if (!_canViewPrice)
            {
                _detailGrid.Columns["colPrice"].Visible = false;
                _detailGrid.Columns["colDisc"].Visible = false;
                _detailGrid.Columns["colAmount"].Visible = false;
            }
        }

        private void AddCol(string name, string header, int width, DataGridViewContentAlignment align, bool fill)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = align;
            if (fill)
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _detailGrid.Columns.Add(col);
        }

        private void BuildActionColumn(float s)
        {
            int colW = (int)(116 * s);
            int btnH = Math.Max((int)(44 * s), 44);
            _actionCol.Width = colW;
            _actionCol.Padding = new Padding((int)(4 * s));

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            int innerW = colW - (int)(12 * s);
            var lblQty = new Label { Text = "Số lượng", Size = new Size(innerW, (int)(22 * s)), Font = _bodyFont, TextAlign = ContentAlignment.BottomLeft };
            _numAddQty.Size = new Size(innerW, (int)(32 * s));
            _numAddQty.Font = _bodyFont;

            FlatButton[] topBtns = { _btnMoveTable, _btnMergeTable };
            foreach (FlatButton b in topBtns)
                AddActionBtn(flow, b, innerW, btnH, s);

            flow.Controls.Add(lblQty);
            flow.Controls.Add(_numAddQty);

            FlatButton[] mainBtns = { _btnAdd, _btnReduce, _btnDelete, _btnGroupDiscount, _btnKitchenPrint };
            foreach (FlatButton b in mainBtns)
                AddActionBtn(flow, b, innerW, btnH, s);

            _actionCol.Controls.Add(flow);
        }

        private void AddActionBtn(FlowLayoutPanel flow, FlatButton btn, int w, int h, float s)
        {
            btn.Size = new Size(w, h);
            btn.Font = _bodyFont;
            btn.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            flow.Controls.Add(btn);
        }

        private void BuildBottomPanel(float s)
        {
            int btnH = Math.Max((int)(44 * s), 44);
            _bottomPanel.Height = Math.Max((int)(148 * s), btnH * 3 + (int)(20 * s));
            _bottomPanel.Padding = new Padding((int)(6 * s), (int)(4 * s), (int)(6 * s), (int)(4 * s));

            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84 * s));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84 * s));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58 * s));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            for (int r = 0; r < 3; r++)
                _totalsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

            StyleTotalBox(_numTienHang, false);
            StyleTotalBox(_numTileGiam, false);
            StyleTotalBox(_numTienGiam, false);
            StyleTotalBox(_numTilePhi, false);
            StyleTotalBox(_numPhiDichVu, false);
            StyleTotalBox(_numTileThue, false);
            StyleTotalBox(_numTienThue, false);
            StyleTotalBox(_numTongCong, true);

            _totalsGrid.Controls.Add(MakeLabel("Tiền hàng"), 0, 0);
            _totalsGrid.Controls.Add(_numTienHang, 1, 0);
            _totalsGrid.Controls.Add(MakeLabel("Giảm giá (%)"), 2, 0);
            _totalsGrid.Controls.Add(_numTileGiam, 3, 0);
            _totalsGrid.Controls.Add(MakeLabel("Tiền"), 4, 0);
            _totalsGrid.Controls.Add(_numTienGiam, 5, 0);

            _totalsGrid.Controls.Add(MakeLabel("Phí DV (%)"), 0, 1);
            _totalsGrid.Controls.Add(_numTilePhi, 1, 1);
            _totalsGrid.Controls.Add(MakeLabel("Tiền phí"), 2, 1);
            _totalsGrid.Controls.Add(_numPhiDichVu, 3, 1);
            _totalsGrid.Controls.Add(MakeLabel("Thuế (%)"), 4, 1);
            var taxHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            taxHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            taxHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            taxHost.Controls.Add(_numTileThue, 0, 0);
            taxHost.Controls.Add(_numTienThue, 1, 0);
            _totalsGrid.Controls.Add(taxHost, 5, 1);

            var lblTotal = MakeLabel("TỔNG CỘNG");
            lblTotal.Font = _headingFont;
            _totalsGrid.Controls.Add(lblTotal, 2, 2);
            _totalsGrid.Controls.Add(_numTongCong, 3, 2);
            _totalsGrid.SetColumnSpan(_numTongCong, 3);

            int btnW = (int)(152 * s);
            _bottomButtons.Width = btnW + (int)(8 * s);
            _bottomButtons.Padding = new Padding((int)(4 * s), 0, 0, 0);
            AddActionBtn(_bottomButtons, _btnPay, btnW, btnH, s);
            AddActionBtn(_bottomButtons, _btnStats, btnW, btnH, s);
            AddActionBtn(_bottomButtons, _btnReprint, btnW, btnH, s);
            _btnPay.Font = _headingFont;

            _bottomPanel.Controls.Add(_totalsGrid);
            _bottomPanel.Controls.Add(_bottomButtons);
        }

        private Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = _bodyFont,
                Padding = new Padding(0, 0, 4, 0)
            };
        }

        private void StyleHeaderBox(Control c)
        {
            c.Dock = DockStyle.Fill;
            c.Font = _bodyFont;
            c.Margin = new Padding(2, 6, 4, 4);
        }

        private void StyleTotalBox(NumberBox box, bool bold)
        {
            box.Dock = DockStyle.Fill;
            box.Font = bold ? _headingFont : _bodyFont;
            box.Margin = new Padding(2, 4, 4, 4);
            box.TextAlign = HorizontalAlignment.Right;
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t ?? ThemeManager.Current;
            ThemeApplier.Apply(this, _theme);
            _mainSplit.BackColor = _theme.Border;
            _tableMap.ApplyTheme(_theme);
            _menuPicker.ApplyTheme(_theme);
            _headerPanel.BackColor = _theme.Surface;
            _bottomPanel.BackColor = _theme.Surface;
            _numTongCong.ForeColor = _theme.Primary;
            Invalidate(true);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed)
                ApplyTheme(ThemeManager.Current);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lockRenewTimer.Stop();
                _lockRenewTimer.Dispose();
                if (_currentTable != null)
                {
                    try { _service.UnlockTable(_currentTable.Id); } catch { }
                }
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _bodyFont.Dispose();
                _headingFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
