using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>Giao diện xác nhận khi đóng tab đang có thay đổi chưa cập nhật (P3).</summary>
    internal interface IConfirmCloseView
    {
        bool ConfirmClose();
    }

    /// <summary>
    /// Màn dùng chung cho "Quản lý bán hàng" và "Điều chỉnh hóa đơn"
    /// (docs/specs/quan-ly-ban-hang-dieu-chinh-hoa-don.md §1, §1.1, §2–§7):
    /// Trái = danh sách bill đã thanh toán (LOAI=0, DATHANHTOAN=30) + lọc ngày/chuỗi + Tải dữ liệu + Hủy phiếu (chỉ Manage);
    /// Phải = khung hóa đơn nhà hàng chỉnh theo chế độ qua OrderModePolicy (sửa trong bộ nhớ, chỉ ghi DB khi bấm Thanh toán).
    /// Bố cục ở SalesManageView.Layout.cs; hiển thị ở SalesManageView.Render.cs;
    /// sự kiện, sửa dòng và cập nhật/trả đồ/hủy/in ở SalesManageView.Actions.cs, .EditLines.cs, .Commit.cs.
    /// </summary>
    internal sealed partial class SalesManageView : Panel, IConfirmCloseView
    {
        public const string ManageMenuId = SalesManageConstants.ManageMenuId;
        public const string AdjustMenuId = SalesManageConstants.AdjustMenuId;

        private readonly UserSession _session;
        private readonly BillManageMode _mode;
        private readonly BillAdjustService _adjustService;
        private readonly TableService _tableService;
        private readonly bool _canViewPrice;
        private readonly ServiceInvoicePrintCoordinator _invoicePrintCoordinator = new ServiceInvoicePrintCoordinator();

        private readonly SplitContainer _mainSplit = new SplitContainer { Dock = DockStyle.Fill };

        // ---- Trái: bộ lọc + lưới danh sách bill đã thanh toán ----
        private readonly Panel _leftPane = new Panel { Dock = DockStyle.Fill };
        private readonly FlowLayoutPanel _leftTopBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly DateBox _dtFrom = new DateBox(false);
        private readonly DateBox _dtTo = new DateBox(false);
        private readonly InputBox _txtFilter = new InputBox();
        private readonly FlatButton _btnReload = new FlatButton { Text = "Tải dữ liệu", Tag = "primary" };
        private readonly FlatButton _btnCancelBill = new FlatButton { Text = "Hủy phiếu", Tag = "danger" };
        private readonly DataGridView _billGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _leftEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có hóa đơn đã thanh toán trong khoảng ngày đã chọn", Visible = false };
        private readonly Timer _loadTimer = new Timer { Interval = 120 };

        // ---- Phải: form hóa đơn theo chế độ ----
        private readonly Panel _rightPane = new Panel { Dock = DockStyle.Fill };
        private readonly Panel _groupBanner = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _lblGroupBanner = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Text = SalesManageConstants.GroupBillBannerText };

        private readonly Panel _headerPanel = new Panel { Dock = DockStyle.Top };
        private readonly TextBox _txtStartTime = new TextBox { ReadOnly = true };
        private readonly DateBox _dtBillDate = new DateBox(false);
        private readonly TextBox _txtBillNo = new TextBox { ReadOnly = true };
        private readonly NumberBox _numGuests = new NumberBox { Minimum = 1m, Maximum = 999m, DecimalPlaces = 0, Value = 1m };
        private readonly LookupCombo _cboCustomer = new LookupCombo();
        private readonly TextBox _txtBillNote = new TextBox();

        private readonly FlowLayoutPanel _lineBar = new FlowLayoutPanel { Dock = DockStyle.Top, WrapContents = true, AutoSize = true };
        private readonly FlatButton _btnPlus = new FlatButton { Text = "+" };
        private readonly FlatButton _btnMinus = new FlatButton { Text = "−" };
        private readonly FlatButton _btnSetQty = new FlatButton { Text = "Đặt sl" };
        private readonly FlatButton _btnPrice = new FlatButton { Text = "Đổi giá" };
        private readonly FlatButton _btnNote = new FlatButton { Text = "Ghi chú" };
        private readonly FlatButton _btnLineDiscount = new FlatButton { Text = "CK%" };

        private readonly DataGridView _detailGrid = new DataGridView { Dock = DockStyle.Fill };

        private readonly Panel _actionCol = new Panel { Dock = DockStyle.Right };
        private readonly NumberBox _numAddQty = new NumberBox { Minimum = 0.01m, Maximum = 99999m, DecimalPlaces = 2, Value = 1m };
        private readonly Label _lblAddQty = new Label { Text = "Số lượng", TextAlign = ContentAlignment.BottomLeft };
        private readonly FlatButton _btnAdd = new FlatButton { Text = "<< Thêm", Tag = "primary" };
        private readonly FlatButton _btnReduce = new FlatButton { Text = "Giảm >>" };
        private readonly FlatButton _btnDelete = new FlatButton { Text = "Xóa", Tag = "danger" };
        private readonly FlatButton _btnGroupDiscount = new FlatButton { Text = "% theo nhóm" };
        private readonly FlatButton _btnReturn = new FlatButton { Text = "Trả đồ" };

        private readonly MenuPickerView _menuPicker = new MenuPickerView { Dock = DockStyle.Right };

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
        private readonly FlatButton _btnUpdate = new FlatButton { Text = "Thanh toán (F11)", Tag = "primary" };
        private readonly FlatButton _btnReprint = new FlatButton { Text = "In lại bill (F8)" };

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _smallFont;
        private ThemeTokens _theme = ThemeManager.Current;

        private List<PaidBillListItem> _allBills = new List<PaidBillListItem>();
        private List<PaidBillListItem> _filteredBills = new List<PaidBillListItem>();
        private List<CustomerLookupItem> _customers = new List<CustomerLookupItem>();
        private PaidBillDetail _loadedDetail;
        private ServiceOrderHeader _workingBill;
        private List<ServiceOrderLine> _displayedLines = new List<ServiceOrderLine>();
        private readonly List<ActivityLogRequest> _pendingLogs = new List<ActivityLogRequest>();
        private string _pendingLoadBillId;
        private string _activeBillId;
        private bool _isDirty;
        private bool _updatingUi;
        private bool _loadingList;
        private bool _splitPlaced;

        // ---- Thuộc tính kiểm thử tự động ----
        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal string LastInfoForTest { get; private set; }
        internal string LastPrintMessageForTest { get; private set; }
        internal bool UnsavedPromptTriggeredForTest { get; private set; }
        internal Func<bool> ConfirmUnsavedOverrideForTest { get; set; }
        internal IPrintOutputHandler PrintOutputHandlerForTest { get; set; }
        internal Func<IWin32Window, IList<PrintTemplateRecord>, PrintSelectionResult> PrintTemplatePickerForTest { get; set; }
        internal Func<string, List<PrintTemplateRecord>> PrintTemplateLoaderForTest { get; set; }
        internal IKitchenPrinterStore KitchenPrinterStoreForTest { get; set; }
        internal PaymentDialogContext LastPreparedPaymentForTest { get; private set; }
        internal int DetailDataErrorCountForTest { get; private set; }

        internal BillManageMode ModeForTest { get { return _mode; } }
        internal DateBox FromDateBoxForTest { get { return _dtFrom; } }
        internal DateBox ToDateBoxForTest { get { return _dtTo; } }
        internal InputBox FilterBoxForTest { get { return _txtFilter; } }
        internal FlatButton ReloadButtonForTest { get { return _btnReload; } }
        internal FlatButton CancelBillButtonForTest { get { return _btnCancelBill; } }
        internal DataGridView BillGridForTest { get { return _billGrid; } }
        internal Panel RightPaneForTest { get { return _rightPane; } }
        internal Panel GroupBannerForTest { get { return _groupBanner; } }
        internal DateBox BillDateBoxForTest { get { return _dtBillDate; } }
        internal LookupCombo CustomerComboForTest { get { return _cboCustomer; } }
        internal NumberBox GuestsBoxForTest { get { return _numGuests; } }
        internal TextBox BillNoteBoxForTest { get { return _txtBillNote; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal MenuPickerView MenuPickerForTest { get { return _menuPicker; } }
        internal FlatButton AddButtonForTest { get { return _btnAdd; } }
        internal FlatButton PlusButtonForTest { get { return _btnPlus; } }
        internal FlatButton MinusButtonForTest { get { return _btnMinus; } }
        internal FlatButton ReduceButtonForTest { get { return _btnReduce; } }
        internal FlatButton DeleteButtonForTest { get { return _btnDelete; } }
        internal FlatButton PriceButtonForTest { get { return _btnPrice; } }
        internal FlatButton LineDiscountButtonForTest { get { return _btnLineDiscount; } }
        internal FlatButton GroupDiscountButtonForTest { get { return _btnGroupDiscount; } }
        internal FlatButton ReturnButtonForTest { get { return _btnReturn; } }
        internal FlatButton UpdateButtonForTest { get { return _btnUpdate; } }
        internal FlatButton ReprintButtonForTest { get { return _btnReprint; } }
        internal NumberBox TileGiamBoxForTest { get { return _numTileGiam; } }
        internal NumberBox TienGiamBoxForTest { get { return _numTienGiam; } }
        internal NumberBox TongCongBoxForTest { get { return _numTongCong; } }
        internal ServiceOrderHeader WorkingBillForTest { get { return _workingBill; } }
        internal bool IsDirtyForTest { get { return _isDirty; } }

        public SalesManageView(UserSession session, BillManageMode mode)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _session = session;
            _mode = mode;
            _adjustService = new BillAdjustService(session);
            _tableService = new TableService(session);
            _canViewPrice = _tableService.CanViewPrice();

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);
            _smallFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);

            BuildLeftPane(s);
            BuildRightPane(s);

            _mainSplit.Panel1.Controls.Add(_leftPane);
            _mainSplit.Panel2.Controls.Add(_rightPane);
            _mainSplit.SizeChanged += delegate { PlaceMainSplit(); };
            Controls.Add(_mainSplit);

            WireEvents();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            DateTime businessDate = _adjustService.GetBusinessDate();
            _loadingList = true;
            try
            {
                _dtFrom.Value = businessDate;
                _dtTo.Value = businessDate;
            }
            finally { _loadingList = false; }

            LoadLookups();
            ReloadBillList(null);
            ApplyModeAndControlStates();
        }

        private void PlaceMainSplit()
        {
            if (_splitPlaced || _mainSplit.Width < 600)
                return;
            int want = Math.Max((int)(360 * DeviceDpi / 96f), (int)(_mainSplit.Width * 0.36));
            if (_mainSplit.Width > want + 420)
            {
                _mainSplit.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _loadTimer.Stop();
                _loadTimer.Dispose();
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _bodyFont.Dispose();
                _headingFont.Dispose();
                _smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
