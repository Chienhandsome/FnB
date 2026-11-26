using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Dialog "ĐẶT HÀNG - THÊM MỚI / SỬA" (docs/specs/khach-dat-hang.md §1.2): mở tối đa hóa,
    /// nội dung gốc 1020×451, 2 panel trái/phải + dải dưới có 2 lưới. Dùng chung với
    /// "Theo dõi đặt phòng" sau này (cùng dialog TDATHANGAe của app gốc).
    /// Đã áp 3 quyết định của Chiến 21/11: giảm giá làm tròn LamTron bội 1000 (Q1);
    /// câu "Giờ ra phải sau giờ vào", so cả ngày+giờ (§5.4); giữ đặt theo đoàn nhiều bàn (§5.6).
    /// Enter không lưu; ba nút Lưu mờ tới khi có thay đổi; không hard-code màu (ô màu là dữ liệu, lưu ARGB).
    /// </summary>
    internal sealed class BookingEditDialog : ThemedDialog
    {
        private const int ContentWidth96 = 1020;
        private const int ContentHeight96 = 451;
        private const string ShortcutsHint = "(F8: Chọn phòng, F10/F11: Trước/Sau, Ctrl+L: Lưu, Ctrl+S: Lưu&Mới, Ctrl+N: Mới, ESC: Thoát)";

        private readonly UserSession _session;
        private readonly string _databasePath;
        private readonly BookingRepository _repo;
        private readonly FormRepository _formRepo;
        private readonly PhieuThuRepository _phieuThuRepo;

        private readonly List<string> _rowIds;

        /// <summary>Mở từ màn lịch "Theo dõi đặt phòng": ẩn thanh công cụ Trước/Sau/Tạo mới.</summary>
        private readonly bool _fromCalendar;

        private int _rowIndex;

        private BookingRow _row;
        private bool _isNew;
        private bool _readOnly;
        private bool _loadFailed;
        private bool _loading;
        private string _original = "";

        private readonly bool _canAdd;
        private readonly bool _canEdit;
        private readonly bool _canRemove;

        private List<string> _roomIds = new List<string>();
        private List<string> _roomNames = new List<string>();
        private List<BookingLine> _lines = new List<BookingLine>();
        private List<BookingSaleItem> _saleItems = new List<BookingSaleItem>();
        private Dictionary<string, BookingSaleItem> _saleById = new Dictionary<string, BookingSaleItem>(StringComparer.Ordinal);
        private List<BookingRoomItem> _allRooms = new List<BookingRoomItem>();
        private int _customerPriceType;
        private BookingDepositInfo _deposit;
        private int _colorArgb = -23296;

        private bool _discountByMoney;
        private bool _recalc;
        private bool _cellParseFailed;
        private bool _useTwoUnits;
        private bool _mergeAllowed;
        private bool _sortByCode;
        private decimal _roundStep = 1000m;

        // ---- Thanh công cụ + chân dialog ----
        private readonly FlatButton _btnShortcuts = new FlatButton { Text = "Phím tắt ▾" };
        private readonly FlatButton _btnPrev = new FlatButton { Text = "Trước (F10)" };
        private readonly FlatButton _btnNext = new FlatButton { Text = "Sau (F11)" };
        private readonly FlatButton _btnNew = new FlatButton { Text = "Tạo mới" };
        private readonly FlatButton _btnDelete = new FlatButton { Text = "Xóa", Tag = "danger" };
        private readonly ThemedContextMenu _shortcutsMenu = new ThemedContextMenu();
        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;

        // ---- Panel trái (§1.2) ----
        private readonly DateTimePicker _dtNgay = new DateTimePicker { Format = DateTimePickerFormat.Short };
        private readonly TextBox _txtNumber = new TextBox();
        private readonly DateTimePicker _dtGioDat = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true };
        private readonly FlatButton _btnDeposit = new FlatButton { Text = "Đặt tiền trước" };
        private readonly Label _lblReceiptNo = new Label { AutoSize = true, Visible = false };
        private readonly Label _lblDepositAmount = new Label { AutoSize = true, Visible = false };
        private readonly Label _lblInvoiceNo = new Label { AutoSize = true, Visible = false };
        private readonly DateTimePicker _dtTuGio = new DateTimePicker();
        private readonly DateTimePicker _dtDenGio = new DateTimePicker();
        private readonly Label _lblTuGio = new Label { Text = "Giờ vào", AutoSize = false };
        private readonly Label _lblDenGio = new Label { Text = "Giờ ra", AutoSize = false };
        private readonly Button _btnColor = new Button { Text = "Màu" };
        private readonly TextBox _txtRooms = new TextBox { ReadOnly = true };
        private readonly FlatButton _btnPickRoom = new FlatButton { Text = "Chọn (F8)" };
        private readonly LookupCombo _customer = new LookupCombo();
        private readonly TextBox _txtTenKhach = new TextBox();
        private readonly TextBox _txtDienThoai = new TextBox();
        private readonly TextBox _txtDiaChi = new TextBox();
        private readonly LookupCombo _method = new LookupCombo();
        private readonly LookupCombo _purpose = new LookupCombo();
        private readonly TextBox _txtNote = new TextBox { Multiline = true };

        // ---- Panel phải: tiền ----
        private readonly NumericUpDown _numTienHang = new NumericUpDown();
        private readonly NumericUpDown _numTileGiamGia = new NumericUpDown();
        private readonly NumericUpDown _numTienGiamGia = new NumericUpDown();
        private readonly NumericUpDown _numTileThue = new NumericUpDown();
        private readonly NumericUpDown _numTienThue = new NumericUpDown();
        private readonly NumericUpDown _numPhiVanChuyen = new NumericUpDown();
        private readonly NumericUpDown _numTongCong = new NumericUpDown();

        // ---- Dải dưới: 2 lưới ----
        private readonly TextBox _txtSearch = new TextBox();
        private readonly NumericUpDown _numQty = new NumericUpDown();
        private readonly FlatButton _btnAddProduct = new FlatButton { Text = "Thêm" };
        private readonly DataGridView _productGrid = new DataGridView();
        private readonly FlatButton _btnDeleteLine = new FlatButton { Text = "Xóa dòng (F8)" };
        private readonly FlatButton _btnImportExcel = new FlatButton { Text = "Nhập từ excel" };
        private readonly FlatButton _btnExportExcel = new FlatButton { Text = "Xuất excel" };
        private readonly DataGridView _detailGrid = new DataGridView();

        /// <summary>ID phiếu đặt đã lưu (null nếu chưa lưu lần nào trong phiên dialog).</summary>
        public string SavedId { get; private set; }

        /// <summary>Bản ghi đang sửa đã bị xóa (dialog đóng sau khi xóa).</summary>
        public bool Deleted { get; private set; }

        internal bool SuppressPopup { get; set; }
        internal string LastWarningForTest { get; private set; }
        internal int DetailDataErrorCountForTest { get; private set; }
        internal Func<BookingSaleItem, decimal, BookingQuantityPromptResult> QuantityPromptOverrideForTest { get; set; }
        internal NumericUpDown QtyBoxForTest { get { return _numQty; } }
        internal FlatButton AddProductButtonForTest { get { return _btnAddProduct; } }
        internal DataGridView ProductGridForTest { get { return _productGrid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal NumericUpDown TongCongBoxForTest { get { return _numTongCong; } }
        internal bool DirtyForTest { get { return Dirty; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }

        /// <param name="bookingId">null = thêm mới.</param>
        /// <param name="rowIds">ID các dòng trên lưới theo thứ tự hiện tại (để F10/F11 chuyển dòng).</param>
        public BookingEditDialog(UserSession session, string bookingId, List<string> rowIds)
            : this(session, bookingId, rowIds, false, null)
        {
        }

        /// <param name="fromCalendar">true = mở từ màn lịch: ẩn thanh công cụ Trước/Sau/Tạo mới.</param>
        /// <param name="preset">Dữ liệu mồi từ vùng ô đã chọn trên lưới lịch (null = không mồi).</param>
        public BookingEditDialog(UserSession session, string bookingId, List<string> rowIds, bool fromCalendar, BookingCalendarPreset preset)
            : base("ĐẶT HÀNG - THÊM MỚI", ShortcutsHint, 1044, 571)
        {
            _session = session;
            _databasePath = session.DatabasePath;
            _repo = new BookingRepository(_databasePath);
            _formRepo = new FormRepository(_databasePath);
            _phieuThuRepo = new PhieuThuRepository(_databasePath);
            _rowIds = rowIds;
            _rowIndex = rowIds == null ? -1 : rowIds.IndexOf(bookingId ?? "");
            _isNew = string.IsNullOrEmpty(bookingId);
            _fromCalendar = fromCalendar;

            _canAdd = AppSession.Can(BookingText.FunctionName, RoleModes.Add);
            _canEdit = AppSession.Can(BookingText.FunctionName, RoleModes.Edit);
            _canRemove = AppSession.Can(BookingText.FunctionName, RoleModes.Remove);
            // Chỉ có Xem (không Sửa, không Thêm) → mở dialog ở chế độ chỉ đọc (mục 4).
            _readOnly = _isNew
                ? !AppSession.Can(BookingText.FunctionName, RoleModes.Add)
                : !AppSession.Can(BookingText.FunctionName, RoleModes.Edit);

            // K3: FixedDialog (ThemedDialog) không cho phóng to/thu nhỏ → đổi sang Sizable để mở tối đa hóa (§1.2).
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            WindowState = FormWindowState.Maximized;

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = exit;

            if (!_fromCalendar)
                BuildToolbar();
            BuildTopPanel();
            BuildBottom();
            WireDirty();

            LoadLookups();
            if (_isNew)
            {
                NewRecord();
                if (preset != null && preset.RoomIds.Count > 0)
                {
                    _roomIds = new List<string>(preset.RoomIds);
                    _roomNames = new List<string>(preset.RoomNames);
                    _row.TuGio = preset.TuGio;
                    _row.DenGio = preset.DenGio;
                    FillControls();
                }
            }
            else
                LoadRecord(bookingId);

            // Chụp trạng thái sạch TRƯỚC khi cập nhật nút: ba nút Lưu mờ tới khi có thay đổi (§2).
            _original = Snapshot();
            ApplyConfigAndPermissions();
            UpdateSaveButtons();
            UpdateNavButtons();

            KeyDown += OnKeyDown;
            Load += delegate { FitBody(ContentWidth96, ContentHeight96); };
            Shown += delegate
            {
                if (_loadFailed)
                {
                    if (!SuppressPopup)
                        Msg.Error(this, BookingText.NotFound);
                    Close();
                    return;
                }
                if (_txtNumber.Enabled)
                {
                    _txtNumber.Focus();
                    _txtNumber.SelectAll();
                }
            };
        }

        // ------------------------------------------------------------------ Dựng giao diện

        /// <summary>Ghi nhận thay đổi trên các ô nhập để bật/tắt nút Lưu.</summary>
        private void WireDirty()
        {
            _txtNumber.TextChanged += delegate { OnDirtyChanged(); };
            _txtTenKhach.TextChanged += delegate { OnDirtyChanged(); };
            _txtDienThoai.TextChanged += delegate { OnDirtyChanged(); };
            _txtDiaChi.TextChanged += delegate { OnDirtyChanged(); };
            _txtNote.TextChanged += delegate { OnDirtyChanged(); };
            _dtNgay.ValueChanged += delegate { OnDirtyChanged(); };
            _dtGioDat.ValueChanged += delegate { OnDirtyChanged(); };
            _dtTuGio.ValueChanged += delegate { OnDirtyChanged(); };
            _dtDenGio.ValueChanged += delegate { OnDirtyChanged(); };
            _method.ValueChanged += delegate { OnDirtyChanged(); };
            _purpose.ValueChanged += delegate { OnDirtyChanged(); };
        }

        private void BuildToolbar()
        {
            float s = Dpi;
            var bar = new Panel { Dock = DockStyle.Top, Height = (int)(30 * s) };
            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            foreach (FlatButton b in new[] { _btnShortcuts, _btnPrev, _btnNext, _btnNew })
            {
                b.Height = (int)(26 * s);
                b.Margin = new Padding(0, (int)(2 * s), (int)(6 * s), 0);
                left.Controls.Add(b);
            }
            _btnShortcuts.Width = (int)(100 * s);
            _btnPrev.Width = (int)(98 * s);
            _btnNext.Width = (int)(92 * s);
            _btnNew.Width = (int)(88 * s);
            _btnShortcuts.Click += delegate { _shortcutsMenu.Show(_btnShortcuts, new Point(0, _btnShortcuts.Height)); };
            _btnPrev.Click += delegate { Navigate(-1); };
            _btnNext.Click += delegate { Navigate(1); };
            _btnNew.Click += delegate { NewBlank(); };
            _btnDelete.Click += delegate { DeleteCurrent(); };
            _btnDelete.Height = (int)(26 * s);
            _btnDelete.Width = (int)(70 * s);
            _btnDelete.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _btnDelete.Dock = DockStyle.Right;
            _btnDelete.Margin = new Padding(0, (int)(2 * s), 0, 0);
            bar.Controls.Add(left);
            bar.Controls.Add(_btnDelete);
            Body.Controls.Add(bar);

            _shortcutsMenu.AddItem("Lưu (Ctrl+L)", delegate { SaveStay(); });
            _shortcutsMenu.AddItem("Lưu & Mới (Ctrl+S)", delegate { SaveAndNew(); });
            _shortcutsMenu.AddItem("Tạo mới (Ctrl+N)", delegate { NewBlank(); });
            _shortcutsMenu.AddSeparator();
            _shortcutsMenu.AddItem("Thoát (Esc)", delegate { Close(); });
        }

        private static Label FieldLabel(string text)
        {
            return new Label { Text = text, AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        }

        private void Place(Control c, Control parent, int x, int y, int w, int h)
        {
            float s = Dpi;
            c.SetBounds((int)(x * s), (int)(y * s), (int)(w * s), (int)(h * s));
            parent.Controls.Add(c);
        }

        private void BuildTopPanel()
        {
            float s = Dpi;
            var top = new Panel { Dock = DockStyle.Top, Height = (int)(249 * s) };
            Body.Controls.Add(top);

            // Hàng 1: Ngày / Số phiếu / Đặt lúc / Đặt tiền trước.
            Place(FieldLabel("Ngày"), top, 0, 7, 86, 20);
            Place(_dtNgay, top, 91, 7, 118, 20);
            _dtNgay.Font = new Font("Segoe UI", 10f);
            Place(FieldLabel("Số phiếu"), top, 214, 7, 51, 20);
            Place(_txtNumber, top, 270, 7, 127, 20);
            _txtNumber.Font = new Font("Segoe UI", 10f);
            Place(FieldLabel("Đặt lúc"), top, 402, 9, 48, 20);
            Place(_dtGioDat, top, 456, 5, 60, 20);
            _dtGioDat.Font = new Font("Segoe UI", 10f);
            Place(_btnDeposit, top, 522, 4, 138, 23);
            _btnDeposit.Click += delegate { OpenDeposit(); };

            Place(_lblReceiptNo, top, 522, 34, 200, 20);
            Place(_lblDepositAmount, top, 522, 57, 200, 20);
            Place(_lblInvoiceNo, top, 522, 82, 200, 20);

            // Hàng 2: Giờ vào / Giờ ra / Màu. (K2: nhánh HOTEL Từ/Đến ngày đã bỏ — F&B thuần chỉ dùng POS, quyết định 19/11.)
            Place(_lblTuGio, top, 0, 34, 86, 20);
            Place(_dtTuGio, top, 92, 34, 117, 20);
            SetupDateTime(_dtTuGio);
            Place(_lblDenGio, top, 214, 34, 51, 20);
            Place(_dtDenGio, top, 270, 34, 127, 20);
            SetupDateTime(_dtDenGio);
            Place(FieldLabel("Màu:"), top, 402, 34, 48, 20);
            Place(_btnColor, top, 456, 34, 60, 20);
            _btnColor.Click += delegate { PickColor(); };

            // Hàng 3: Phòng/Bàn + Chọn.
            Place(FieldLabel(BookingRules.RoomLabel), top, 0, 60, 86, 20);
            Place(_txtRooms, top, 91, 60, 306, 20);
            _txtRooms.Font = new Font("Segoe UI", 10f);
            Place(_btnPickRoom, top, 403, 57, 113, 26);
            _btnPickRoom.Click += delegate { OpenRoomPicker(); };

            // Hàng 4–8: khách hàng, người đặt, địa chỉ, phương thức, ghi chú.
            Place(FieldLabel("Khách hàng"), top, 0, 87, 86, 20);
            Place(_customer, top, 92, 87, 424, 21);
            _customer.Font = new Font("Segoe UI", 10f);
            _customer.ValueChanged += delegate { OnCustomerChanged(); };
            Place(FieldLabel("Người đặt:"), top, 0, 114, 86, 20);
            Place(_txtTenKhach, top, 92, 114, 128, 20);
            _txtTenKhach.Font = new Font("Segoe UI", 10f);
            Place(FieldLabel("Điện thoại"), top, 224, 114, 60, 20);
            Place(_txtDienThoai, top, 290, 114, 226, 20);
            _txtDienThoai.Font = new Font("Segoe UI", 10f);
            Place(FieldLabel("Địa chỉ"), top, 0, 141, 86, 20);
            Place(_txtDiaChi, top, 92, 141, 424, 20);
            _txtDiaChi.Font = new Font("Segoe UI", 10f);
            Place(FieldLabel("Đặt qua"), top, 0, 167, 86, 20);
            Place(_method, top, 92, 167, 128, 21);
            _method.Font = new Font("Segoe UI", 10f);
            Place(FieldLabel("Mục đích"), top, 224, 167, 60, 20);
            Place(_purpose, top, 290, 167, 226, 21);
            _purpose.Font = new Font("Segoe UI", 10f);
            Place(FieldLabel("Yêu cầu khác"), top, 0, 194, 86, 20);
            Place(_txtNote, top, 92, 194, 424, 51);
            _txtNote.Font = new Font("Segoe UI", 10f);

            BuildMoneyPanel(top);
        }

        private void SetupDateTime(DateTimePicker dt)
        {
            dt.Format = DateTimePickerFormat.Custom;
            dt.CustomFormat = "dd/MM/yyyy HH:mm";
            dt.ShowUpDown = true;
            dt.ShowCheckBox = true;
            dt.Checked = false;
            dt.Font = new Font("Segoe UI", 10f);
        }

        private void BuildMoneyPanel(Panel top)
        {
            float s = Dpi;
            var box = new GroupBox
            {
                Text = "Tiền",
                Dock = DockStyle.Right,
                Width = (int)(301 * s),
                Font = new Font("Segoe UI Semibold", 10f)
            };
            top.Controls.Add(box);

            Place(FieldLabel("Tiền hàng"), box, 6, 10, 118, 20);
            Place(_numTienHang, box, 129, 6, 163, 26);
            SetupMoneyBox(_numTienHang, true);
            Place(FieldLabel("Giảm giá"), box, 6, 42, 118, 20);
            Place(_numTileGiamGia, box, 129, 38, 42, 26);
            SetupPercentBox(_numTileGiamGia);
            var pct1 = new Label { Text = "%", AutoSize = true };
            Place(pct1, box, 175, 42, 20, 20);
            Place(_numTienGiamGia, box, 204, 38, 88, 26);
            SetupMoneyBox(_numTienGiamGia, false);
            Place(FieldLabel("Thuế"), box, 6, 74, 118, 20);
            Place(_numTileThue, box, 129, 70, 42, 26);
            SetupPercentBox(_numTileThue);
            var pct2 = new Label { Text = "%", AutoSize = true };
            Place(pct2, box, 175, 74, 20, 20);
            Place(_numTienThue, box, 204, 70, 88, 26);
            SetupMoneyBox(_numTienThue, true);
            Place(FieldLabel("Phí vận chuyển"), box, 6, 106, 118, 20);
            Place(_numPhiVanChuyen, box, 129, 102, 163, 26);
            SetupMoneyBox(_numPhiVanChuyen, false);
            Place(FieldLabel("Tổng cộng"), box, 6, 138, 118, 20);
            Place(_numTongCong, box, 129, 134, 163, 26);
            SetupMoneyBox(_numTongCong, true);
            _numTongCong.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

            _numTileGiamGia.Enter += delegate { _discountByMoney = false; };
            _numTienGiamGia.Enter += delegate { _discountByMoney = true; };
            _numTileGiamGia.ValueChanged += delegate { if (!_loading && !_recalc) { RecalcTotals(); OnDirtyChanged(); } };
            _numTienGiamGia.ValueChanged += delegate { if (!_loading && !_recalc) { RecalcTotals(); OnDirtyChanged(); } };
            _numTileThue.ValueChanged += delegate { if (!_loading && !_recalc) { RecalcTotals(); OnDirtyChanged(); } };
            _numPhiVanChuyen.ValueChanged += delegate { if (!_loading && !_recalc) { RecalcTotals(); OnDirtyChanged(); } };
        }

        private static void SetupMoneyBox(NumericUpDown num, bool readOnly)
        {
            num.DecimalPlaces = 0;
            num.ThousandsSeparator = true;
            num.Minimum = 0m;
            num.Maximum = 999999999999m;
            num.TextAlign = HorizontalAlignment.Right;
            num.Font = new Font("Segoe UI", 10f);
            num.ReadOnly = readOnly;
        }

        private static void SetupPercentBox(NumericUpDown num)
        {
            num.DecimalPlaces = 2;
            num.Minimum = 0m;
            num.Maximum = 100m;
            num.TextAlign = HorizontalAlignment.Right;
            num.Font = new Font("Segoe UI", 10f);
        }

        private void BuildBottom()
        {
            float s = Dpi;
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            split.SplitterDistance = (int)(445 * s);
            Body.Controls.Add(split);

            // Trái: tìm mặt hàng + lưới chọn hàng.
            var searchRow = new Panel { Dock = DockStyle.Top, Height = (int)(30 * s) };
            var lblSearch = new Label { Text = "Tìm (F3)", AutoSize = true };
            lblSearch.SetBounds(0, (int)(6 * s), (int)(60 * s), (int)(20 * s));
            _txtSearch.SetBounds((int)(64 * s), (int)(4 * s), (int)(200 * s), (int)(24 * s));
            _txtSearch.Font = new Font("Segoe UI", 10f);
            _txtSearch.TextChanged += delegate { RebuildProductGrid(); };
            var lblQty = new Label { Text = "Số lượng", AutoSize = true };
            lblQty.SetBounds((int)(272 * s), (int)(6 * s), (int)(62 * s), (int)(20 * s));
            _numQty.SetBounds((int)(338 * s), (int)(4 * s), (int)(70 * s), (int)(24 * s));
            _numQty.DecimalPlaces = 2;
            _numQty.Minimum = 0m;
            _numQty.Maximum = 1000000m;
            _numQty.Value = 1m;
            _numQty.Font = new Font("Segoe UI", 10f);
            _numQty.TextAlign = HorizontalAlignment.Right;
            _numQty.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddProduct();
                    e.Handled = true;
                }
            };
            _btnAddProduct.SetBounds((int)(414 * s), (int)(2 * s), (int)(64 * s), (int)(26 * s));
            _btnAddProduct.Tag = "primary";
            _btnAddProduct.Click += delegate { AddProduct(); };
            searchRow.Controls.Add(lblSearch);
            searchRow.Controls.Add(_txtSearch);
            searchRow.Controls.Add(lblQty);
            searchRow.Controls.Add(_numQty);
            searchRow.Controls.Add(_btnAddProduct);
            split.Panel1.Controls.Add(searchRow);

            SetupGrid(_productGrid, true);
            _productGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "p_name", HeaderText = "Tên hàng", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            AddMoneyColumn(_productGrid, "p_price", "Giá bán", 80);
            AddMoneyColumn(_productGrid, "p_quydoi", "Quy đổi", 60, "#,##0.##");
            _productGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "p_unit", HeaderText = "ĐVT", Width = (int)(60 * s), ReadOnly = true });
            _productGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "p_bigunit", HeaderText = "ĐVT chẵn", Width = (int)(70 * s), ReadOnly = true });
            AddMoneyColumn(_productGrid, "p_bigprice", "Giá bán chẵn", 90);
            _productGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    AddProduct();
            };
            split.Panel1.Controls.Add(_productGrid);

            // Phải: lưới chi tiết + nút dòng.
            var lineRow = new Panel { Dock = DockStyle.Top, Height = (int)(30 * s) };
            _btnDeleteLine.SetBounds(0, (int)(2 * s), (int)(110 * s), (int)(26 * s));
            _btnDeleteLine.Tag = "danger";
            _btnDeleteLine.Click += delegate { DeleteLine(); };
            _btnImportExcel.SetBounds((int)(116 * s), (int)(2 * s), (int)(110 * s), (int)(26 * s));
            _btnImportExcel.Click += delegate { Msg.Info(this, BookingText.Unsupported); };
            _btnExportExcel.SetBounds((int)(232 * s), (int)(2 * s), (int)(90 * s), (int)(26 * s));
            _btnExportExcel.Click += delegate { Msg.Info(this, BookingText.Unsupported); };
            lineRow.Controls.Add(_btnDeleteLine);
            lineRow.Controls.Add(_btnImportExcel);
            lineRow.Controls.Add(_btnExportExcel);
            split.Panel2.Controls.Add(lineRow);

            SetupGrid(_detailGrid, false);
            _detailGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "d_name", HeaderText = "Mặt hàng", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _detailGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "d_unit", HeaderText = "ĐVT", Width = (int)(60 * s), ReadOnly = true });
            AddMoneyColumn(_detailGrid, "d_qty", "Số lượng", 70, "#,##0.##");
            AddMoneyColumn(_detailGrid, "d_price", "Đơn giá", 90);
            AddMoneyColumn(_detailGrid, "d_disc", "Giảm giá %", 80, "#,##0.##");
            AddMoneyColumn(_detailGrid, "d_amount", "Thành tiền", 100);
            _detailGrid.Columns["d_amount"].ReadOnly = true;
            _detailGrid.CellParsing += DetailCellParsing;
            _detailGrid.CellEndEdit += DetailCellEndEdit;
            _detailGrid.DataError += DetailDataError;
            split.Panel2.Controls.Add(_detailGrid);
        }

        private void SetupGrid(DataGridView grid, bool readOnly)
        {
            float s = Dpi;
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = readOnly;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.AutoGenerateColumns = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(30 * s);
            grid.RowTemplate.Height = (int)(26 * s);
            grid.Font = new Font("Segoe UI", 10f);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
        }

        private void AddMoneyColumn(DataGridView grid, string name, string header, int width96)
        {
            AddMoneyColumn(grid, name, header, width96, "N0");
        }

        private void AddMoneyColumn(DataGridView grid, string name, string header, int width96, string format)
        {
            float s = Dpi;
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = (int)(width96 * s),
                CellTemplate = new NumericEditGridCell(),
                ValueType = typeof(decimal),
                DefaultCellStyle = { Format = format, FormatProvider = new CultureInfo("vi-VN"), Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            grid.Columns.Add(col);
        }

        // ------------------------------------------------------------------ Nạp dữ liệu

        private void LoadLookups()
        {
            _useTwoUnits = SystemConfig.SuDung2DonViTinh;
            _mergeAllowed = !SystemConfig.NhapMotMatHangNhieuLanTrongPhieu;
            // C1: app gốc (grMatHang_CustomLoadData) — SapXepThuTuTheo == 0 → sắp theo CODE, khác 0 → theo NAME.
            _sortByCode = SystemConfig.SapXepThuTuTheo == 0;
            _roundStep = SystemConfig.LamTronTien;
            try
            {
                _customer.SetItems(_formRepo.LoadAll("DKHACHHANG"));
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không đọc được danh sách khách hàng: " + ex.Message);
            }
            try
            {
                _method.SetItems(_formRepo.LoadAll("DPHUONGTHUCDAT"));
            }
            catch (DataAccessException)
            {
                // Ô phương thức để trống được.
            }
            try
            {
                _purpose.SetItems(_formRepo.LoadAll("DMUCDICHDAT"));
            }
            catch (DataAccessException)
            {
                // Ô mục đích để trống được.
            }
            try
            {
                _saleItems = _repo.LoadSaleItems(_sortByCode);
                _saleById.Clear();
                foreach (BookingSaleItem item in _saleItems)
                    _saleById[item.Id] = item;
                _allRooms = _repo.LoadRooms();
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không đọc được danh sách mặt hàng/bàn: " + ex.Message);
            }
            RebuildProductGrid();
        }

        private void NewRecord()
        {
            _row = BookingRow.NewDefault();
            try
            {
                _row.Name = _repo.GenerateNumber();
            }
            catch (DataAccessException)
            {
                _row.Name = "";
            }
            _isNew = true;
            _roomIds = new List<string>();
            _roomNames = new List<string>();
            _lines = new List<BookingLine>();
            _customerPriceType = 0;
            _discountByMoney = false;
            _deposit = null;
            _colorArgb = _row.ColorArgb;
            FillControls();
            SetTitle("ĐẶT HÀNG - THÊM MỚI");
        }

        private void LoadRecord(string bookingId)
        {
            BookingRow row = null;
            try
            {
                row = _repo.LoadBooking(bookingId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không đọc được phiếu đặt: " + ex.Message);
            }
            if (row == null)
            {
                _loadFailed = true;
                _row = BookingRow.NewDefault();
                return;
            }
            _row = row;
            _isNew = false;
            _roomIds = new List<string>();
            _roomNames = new List<string>();
            if (row.GroupRows != null)
            {
                foreach (GroupRowRef g in row.GroupRows)
                {
                    _roomIds.Add(g.RoomId);
                    _roomNames.Add(RoomNameOf(g.RoomId));
                }
            }
            _lines = row.Lines ?? new List<BookingLine>();
            _discountByMoney = row.GiamTheoTien;
            _colorArgb = row.ColorArgb;
            _deposit = null;
            _customerPriceType = 0;
            if (!string.IsNullOrEmpty(row.CustomerId))
            {
                try
                {
                    BookingCustomerInfo c = _repo.LoadCustomer(row.CustomerId);
                    if (c != null)
                        _customerPriceType = c.PriceType;
                }
                catch (DataAccessException)
                {
                    // Giữ loại giá mặc định 0.
                }
            }
            FillControls();
            SetTitle("ĐẶT HÀNG - SỬA");
        }

        private string RoomNameOf(string roomId)
        {
            foreach (BookingRoomItem r in _allRooms)
            {
                if (string.Equals(r.Id, roomId, StringComparison.Ordinal))
                    return r.Name;
            }
            return "";
        }

        private void FillControls()
        {
            _loading = true;
            try
            {
                _dtNgay.Value = _row.Ngay == DateTime.MinValue ? DateTime.Today : _row.Ngay;
                _txtNumber.Text = _row.Name ?? "";
                _dtGioDat.Value = _row.GioDat == DateTime.MinValue ? DateTime.Now : _row.GioDat;
                _dtTuGio.Value = _row.TuGio ?? DateTime.Now;
                // K1: phiếu mới có sẵn giờ (đã tick) — ô DateTimePicker chưa tick thì bị khóa nhập,
                // người dùng gõ giờ không được còn Lưu thì báo "Giờ vào không được trống".
                _dtTuGio.Checked = _row.TuGio != null || _isNew;
                _dtDenGio.Value = _row.DenGio ?? DateTime.Now;
                _dtDenGio.Checked = _row.DenGio != null || _isNew;
                _txtRooms.Text = string.Join(", ", _roomNames.ToArray());
                _customer.SelectId(_row.CustomerId);
                _txtTenKhach.Text = _row.TenKhach ?? "";
                _txtDienThoai.Text = _row.DienThoai ?? "";
                _txtDiaChi.Text = _row.DiaChi ?? "";
                _method.SelectId(_row.MethodId);
                _purpose.SelectId(_row.PurposeId);
                _txtNote.Text = _row.Note ?? "";
                _numTienHang.Value = ClampMoney(_row.TienHang);
                _numTileGiamGia.Value = ClampPercent(_row.TileGiamGia);
                _numTienGiamGia.Value = ClampMoney(_row.TienGiamGia);
                _numTileThue.Value = ClampPercent(_row.TileThue);
                _numTienThue.Value = ClampMoney(_row.TienThue);
                _numPhiVanChuyen.Value = ClampMoney(_row.PhiVanChuyen);
                _numTongCong.Value = ClampMoney(_row.TongCong);
                RebuildDetailGrid();
                LoadDepositLabels();
            }
            finally
            {
                _loading = false;
            }
        }

        private static decimal ClampMoney(decimal v)
        {
            if (v < 0m)
                return 0m;
            if (v > 999999999999m)
                return 999999999999m;
            return v;
        }

        private static decimal ClampPercent(decimal v)
        {
            if (v < 0m)
                return 0m;
            if (v > 100m)
                return 100m;
            return v;
        }

        private void LoadDepositLabels()
        {
            _lblReceiptNo.Visible = false;
            _lblDepositAmount.Visible = false;
            _lblInvoiceNo.Visible = false;
            if (_isNew || _row == null || string.IsNullOrEmpty(_row.Id))
                return;
            try
            {
                _deposit = _phieuThuRepo.LoadDeposit(_row.Id);
                if (_deposit != null)
                {
                    _lblReceiptNo.Text = "Số phiếu thu: " + _deposit.Number;
                    _lblDepositAmount.Text = "Số tiền đặt: " + _deposit.Amount.ToString("N0", new CultureInfo("vi-VN"));
                    _lblReceiptNo.Visible = true;
                    _lblDepositAmount.Visible = true;
                }
                string invoice = _repo.LoadInvoiceName(_row.Id);
                if (!string.IsNullOrEmpty(invoice))
                {
                    _lblInvoiceNo.Text = "Số hóa đơn: " + invoice;
                    _lblInvoiceNo.Visible = true;
                }
            }
            catch (DataAccessException)
            {
                _deposit = null;
            }
        }

        // ------------------------------------------------------------------ Lưới hàng

        private void RebuildProductGrid()
        {
            string f = (_txtSearch.Text ?? "").Trim();
            var vi = new CultureInfo("vi-VN").CompareInfo;
            _productGrid.Rows.Clear();
            foreach (BookingSaleItem item in _saleItems)
            {
                if (f.Length > 0
                    && vi.IndexOf(item.Name ?? "", f, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0
                    && vi.IndexOf(item.Code ?? "", f, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0)
                    continue;
                int i = _productGrid.Rows.Add(item.Name, item.SellPrice, item.QuyDoi, item.UnitName, item.BigUnitName, item.BigSellPrice);
                _productGrid.Rows[i].Tag = item;
            }
        }

        private void RebuildDetailGrid()
        {
            _detailGrid.Rows.Clear();
            foreach (BookingLine line in _lines)
            {
                int i = _detailGrid.Rows.Add(line.ProductName, line.UnitName, line.Quantity, line.Price, line.DiscountPct, line.Amount);
                _detailGrid.Rows[i].Tag = line;
            }
        }

        private BookingSaleItem SelectedSaleItem()
        {
            if (_productGrid.CurrentRow == null)
                return null;
            return _productGrid.CurrentRow.Tag as BookingSaleItem;
        }

        private void AddProduct()
        {
            LastWarningForTest = null;
            if (_readOnly)
                return;
            BookingSaleItem item = SelectedSaleItem();
            if (item == null)
                return;
            decimal outerQty = _numQty.Value;
            BookingAddItemOutcome outcome;
            if (_useTwoUnits && item.HasBigUnit)
            {
                BookingQuantityPromptResult prompt = PromptTwoUnitQuantity(item, outerQty);
                outcome = BookingItemRules.AddTwoUnitItem(_lines, item, _customerPriceType, outerQty,
                    prompt.OddQuantity, prompt.EvenQuantity, prompt.Confirmed, _mergeAllowed);
            }
            else
            {
                outcome = BookingItemRules.AddSingleUnitItem(_lines, item, _customerPriceType, outerQty);
            }
            if (outcome.Changed)
            {
                RebuildDetailGrid();
                RecalcTotals();
                OnDirtyChanged();
            }
            if (!string.IsNullOrEmpty(outcome.WarningMessage))
            {
                ShowWarning(outcome.WarningMessage);
                _numQty.Focus();
            }
        }

        private BookingQuantityPromptResult PromptTwoUnitQuantity(BookingSaleItem item, decimal outerQty)
        {
            if (QuantityPromptOverrideForTest != null)
                return QuantityPromptOverrideForTest(item, outerQty) ?? new BookingQuantityPromptResult();
            using (var dlg = new QuantityDialog(item, outerQty))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return new BookingQuantityPromptResult { Confirmed = false };
                return new BookingQuantityPromptResult { Confirmed = true, OddQuantity = dlg.OddQuantity, EvenQuantity = dlg.EvenQuantity };
            }
        }

        private void ShowWarning(string message)
        {
            LastWarningForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void DetailCellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _lines.Count || e.ColumnIndex < 0) return;
            string col = _detailGrid.Columns[e.ColumnIndex].Name;
            if (col != "d_qty" && col != "d_price" && col != "d_disc") return;
            decimal parsed;
            if (NumberInput.TryParse(e.Value, out parsed))
            {
                _cellParseFailed = false;
                e.Value = parsed;
            }
            else
            {
                _cellParseFailed = true;
                BookingLine line = _lines[e.RowIndex];
                e.Value = col == "d_qty" ? line.Quantity : (col == "d_price" ? line.Price : line.DiscountPct);
            }
            e.ParsingApplied = true;
        }

        private void DetailDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            DetailDataErrorCountForTest++;
            e.ThrowException = false;
            if (e.RowIndex < 0 || e.RowIndex >= _lines.Count) return;
            BookingLine line = _lines[e.RowIndex];
            DataGridViewRow r = _detailGrid.Rows[e.RowIndex];
            r.Cells["d_qty"].Value = line.Quantity;
            r.Cells["d_price"].Value = line.Price;
            r.Cells["d_disc"].Value = line.DiscountPct;
        }

        private void DetailCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            LastWarningForTest = null;
            bool parseFailed = _cellParseFailed;
            _cellParseFailed = false;
            if (e.RowIndex < 0 || e.RowIndex >= _lines.Count || e.ColumnIndex < 0) return;
            BookingLine line = _lines[e.RowIndex];
            DataGridViewRow r = _detailGrid.Rows[e.RowIndex];
            string col = _detailGrid.Columns[e.ColumnIndex].Name;
            if (col == "d_qty")
            {
                object raw = parseFailed ? null : r.Cells["d_qty"].Value;
                BookingGridQtyEditResult res = BookingItemRules.EvaluateEditedQuantity(raw, line.Quantity);
                r.Cells["d_qty"].Value = res.Quantity;
                if (!res.Accepted) { ShowWarning(res.WarningMessage); return; }
                if (!res.Changed) return;
                line.Quantity = res.Quantity;
            }
            else if (col == "d_price" || col == "d_disc")
            {
                bool isPrice = col == "d_price";
                decimal oldVal = isPrice ? line.Price : line.DiscountPct;
                decimal v;
                if (parseFailed || !NumberInput.TryParse(r.Cells[col].Value, out v))
                {
                    r.Cells[col].Value = oldVal;
                    return;
                }
                decimal clamped = isPrice ? Math.Max(0m, v) : Math.Min(100m, Math.Max(0m, v));
                r.Cells[col].Value = clamped;
                if (clamped == oldVal) return;
                if (isPrice) line.Price = clamped; else line.DiscountPct = clamped;
            }
            else return;
            line.Amount = BookingRules.CalcLineAmount(line.Quantity, line.Price, line.DiscountPct);
            r.Cells["d_amount"].Value = line.Amount;
            RecalcTotals();
            OnDirtyChanged();
        }

        private void DeleteLine()
        {
            if (_readOnly)
                return;
            if (_detailGrid.CurrentRow == null || _detailGrid.CurrentRow.Index < 0
                || _detailGrid.CurrentRow.Index >= _lines.Count)
                return;
            _lines.RemoveAt(_detailGrid.CurrentRow.Index);
            RebuildDetailGrid();
            RecalcTotals();
            OnDirtyChanged();
        }

        // ------------------------------------------------------------------ Tính tiền

        private void RecalcTotals()
        {
            if (_loading)
                return;
            foreach (BookingLine line in _lines)
                line.Amount = BookingRules.CalcLineAmount(line.Quantity, line.Price, line.DiscountPct);
            BookingRules.BookingTotals t = BookingRules.CalcTotals(_lines,
                _numTileGiamGia.Value, _numTienGiamGia.Value, _discountByMoney,
                _numTileThue.Value, _numPhiVanChuyen.Value, _roundStep);
            _recalc = true;
            try
            {
                _numTienHang.Value = ClampMoney(t.TienHang);
                _numTileGiamGia.Value = ClampPercent(t.TileGiamGia);
                _numTienGiamGia.Value = ClampMoney(t.TienGiamGia);
                _numTienThue.Value = ClampMoney(t.TienThue);
                _numTongCong.Value = ClampMoney(t.TongCong);
            }
            finally
            {
                _recalc = false;
            }
        }

        private void OnCustomerChanged()
        {
            if (_loading)
                return;
            string id = _customer.SelectedId;
            if (string.IsNullOrEmpty(id))
            {
                _customerPriceType = 0;
                if (SystemConfig.SuDungGiaTheoKhach)
                    RepriceLines();
                OnDirtyChanged();
                return;
            }
            BookingCustomerInfo c = null;
            try
            {
                c = _repo.LoadCustomer(id);
            }
            catch (DataAccessException)
            {
                // Giữ nguyên tên/địa chỉ đã gõ.
            }
            if (c != null)
            {
                _txtTenKhach.Text = c.Name ?? "";
                _txtDiaChi.Text = c.Address ?? "";
                _txtDienThoai.Text = c.Phone ?? "";
                _row.Email = c.Email;
                _customerPriceType = c.PriceType;
                if (SystemConfig.SuDungGiaTheoKhach)
                    RepriceLines();
            }
            OnDirtyChanged();
        }

        private void RepriceLines()
        {
            foreach (BookingLine line in _lines)
            {
                BookingSaleItem item;
                if (!_saleById.TryGetValue(line.ProductId ?? "", out item))
                    continue;
                // Dòng chẵn giữ giá chẵn; chỉ dòng lẻ tính lại theo loại giá khách.
                if (string.Equals(line.UnitId, item.UnitId, StringComparison.Ordinal))
                    line.Price = item.PriceForCustomer(_customerPriceType);
                line.Amount = BookingRules.CalcLineAmount(line.Quantity, line.Price, line.DiscountPct);
            }
            RebuildDetailGrid();
            RecalcTotals();
        }

        // ------------------------------------------------------------------ Lưu / Xóa

        private void CollectRow()
        {
            _row.Ngay = _dtNgay.Value.Date;
            _row.Name = (_txtNumber.Text ?? "").Trim();
            _row.GioDat = _dtNgay.Value.Date + _dtGioDat.Value.TimeOfDay;
            _row.TuGio = _dtTuGio.Checked ? (DateTime?)_dtTuGio.Value : null;
            _row.DenGio = _dtDenGio.Checked ? (DateTime?)_dtDenGio.Value : null;
            DateTime tuNgay, denNgay;
            BookingRules.DeriveDates(_row.TuGio, _row.DenGio, out tuNgay, out denNgay);
            _row.TuNgay = tuNgay;
            _row.DenNgay = denNgay;
            _row.CustomerId = _customer.SelectedId;
            _row.TenKhach = (_txtTenKhach.Text ?? "").Trim();
            _row.DienThoai = (_txtDienThoai.Text ?? "").Trim();
            _row.DiaChi = (_txtDiaChi.Text ?? "").Trim();
            _row.MethodId = _method.SelectedId;
            _row.PurposeId = _purpose.SelectedId;
            _row.Note = _txtNote.Text ?? "";
            _row.ColorArgb = _colorArgb;
            _row.TienHang = _numTienHang.Value;
            _row.TileGiamGia = _numTileGiamGia.Value;
            _row.TienGiamGia = _numTienGiamGia.Value;
            _row.GiamTheoTien = _discountByMoney;
            _row.TileThue = _numTileThue.Value;
            _row.TienThue = _numTienThue.Value;
            _row.PhiVanChuyen = _numPhiVanChuyen.Value;
            _row.TongCong = _numTongCong.Value;
        }

        private bool DoSave()
        {
            if (_readOnly)
                return false;
            _detailGrid.EndEdit();
            CollectRow();
            var input = new BookingRules.BookingValidationInput
            {
                RoomIds = _roomIds,
                Number = _row.Name,
                TenKhach = _row.TenKhach,
                TuGio = _row.TuGio,
                DenGio = _row.DenGio,
                GioDat = _row.GioDat
            };
            BookingRules.BookingValidationResult vr = BookingRules.ValidateBeforeSave(input);
            if (!vr.IsValid)
            {
                Msg.Warning(this, vr.Error);
                if (vr.Error == BookingText.SelectRoomFirst)
                    _btnPickRoom.Focus();
                else if (vr.Error == BookingText.Required("Số phiếu"))
                    FocusText(_txtNumber);
                else if (vr.Error == BookingText.Required("Người đặt"))
                    FocusText(_txtTenKhach);
                else if (vr.Error == BookingText.Required("Giờ vào"))
                    _dtTuGio.Focus();
                else if (vr.Error == BookingText.Required("Giờ ra"))
                    _dtDenGio.Focus();
                else if (vr.Error == BookingText.DateOrder)
                    _dtDenGio.Focus();
                return false;
            }

            BookingSaveResult result;
            try
            {
                result = _repo.SaveBooking(_row, _lines, _roomIds, AppSession.UserId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return false;
            }
            if (!result.Ok)
            {
                Msg.Warning(this, result.ErrorMessage);
                if (result.ErrorMessage == BookingText.NumberDuplicate(_row.Name ?? ""))
                    FocusText(_txtNumber);
                return false;
            }

            SavedId = result.SavedId;
            _row.Id = result.SavedId;
            bool wasNew = _isNew;
            _isNew = false;
            if (wasNew)
            {
                SetTitle("ĐẶT HÀNG - SỬA");
                ApplyConfigAndPermissions();
            }
            LoadDepositLabels();
            _original = Snapshot();
            UpdateSaveButtons();
            UpdateNavButtons();
            return true;
        }

        private void FocusText(TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }

        private void SaveStay()
        {
            DoSave();
        }

        private void SaveAndNew()
        {
            if (!DoSave())
                return;
            NewRecord();
            AfterRecordLoaded();
        }

        private void SaveAndClose()
        {
            if (!DoSave())
                return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DeleteCurrent()
        {
            if (_isNew || _readOnly || !_canRemove)
                return;
            if (!Msg.Confirm(this, BookingText.DeleteConfirm))
                return;
            BookingDeleteResult result;
            try
            {
                result = _repo.DeleteBooking(_row.Id);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!result.Ok)
            {
                Msg.Warning(this, result.ErrorMessage);
                return;
            }
            Deleted = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        // ------------------------------------------------------------------ Tạo mới / chuyển dòng

        private void NewBlank()
        {
            if (_readOnly)
                return;
            if (_isNew)
            {
                NewRecord();
                AfterRecordLoaded();
                return;
            }
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi tạo phiếu đặt mới không?"))
                return;
            NewRecord();
            AfterRecordLoaded();
        }

        private void Navigate(int dir)
        {
            if (_rowIds == null || _rowIds.Count == 0)
                return;
            int target = _rowIndex + dir;
            if (target < 0 || target >= _rowIds.Count)
                return;
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi chuyển sang phiếu đặt khác không?"))
                return;
            string id = _rowIds[target];
            _rowIndex = target;
            LoadRecord(id);
            AfterRecordLoaded();
        }

        private void AfterRecordLoaded()
        {
            _original = Snapshot();
            ApplyConfigAndPermissions();
            UpdateSaveButtons();
            UpdateNavButtons();
        }

        /// <summary>Hỏi lưu khi có thay đổi chưa lưu (Q14, như khuôn Q23: Yes/No/Cancel).</summary>
        private bool ConfirmDiscardOrSave(string text)
        {
            if (!Dirty)
                return true;
            DialogResult answer = MessageBox.Show(this, text, "Xác nhận", MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (answer == DialogResult.Cancel)
                return false;
            if (answer == DialogResult.Yes)
                return DoSave();
            return true;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK || _loadFailed || !Dirty || SuppressPopup)
                return;
            DialogResult answer = MessageBox.Show(this, "Dữ liệu đã thay đổi. Bạn có muốn lưu lại không?",
                "Xác nhận", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (answer == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (answer == DialogResult.Yes && !DoSave())
                e.Cancel = true;
        }

        // ------------------------------------------------------------------ Phòng / màu / cọc

        private void OpenRoomPicker()
        {
            if (_readOnly)
                return;
            using (var dlg = new PickRoomDialog(_session, _roomIds))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                _roomIds = dlg.SelectedRoomIds;
                _roomNames = dlg.SelectedRoomNames;
                _txtRooms.Text = string.Join(", ", _roomNames.ToArray());
                OnDirtyChanged();
            }
        }

        private void PickColor()
        {
            if (_readOnly)
                return;
            using (var dlg = new ColorDialog { Color = Color.FromArgb(_colorArgb), FullOpen = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                _colorArgb = dlg.Color.ToArgb();
                ApplyColorButton();
                OnDirtyChanged();
            }
        }

        private void ApplyColorButton()
        {
            Color c = Color.FromArgb(_colorArgb);
            _btnColor.BackColor = c;
            double lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            _btnColor.ForeColor = lum < 128 ? Color.White : Color.Black;
            _btnColor.FlatStyle = FlatStyle.Flat;
        }

        private void OpenDeposit()
        {
            if (_readOnly)
                return;
            if (!DoSave())
                return;
            var preset = new PhieuThuDepositPreset
            {
                BookingId = _row.Id,
                BookingNumber = _row.Name,
                CustomerId = _row.CustomerId,
                CustomerName = _row.TenKhach,
                Address = _row.DiaChi,
                Amount = _row.TongCong,
                ExistingReceiptId = _deposit == null ? null : _deposit.Id
            };
            using (var dlg = new PhieuThuDialog(_session, preset))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    LoadDepositLabels();
            }
        }

        // ------------------------------------------------------------------ Trạng thái

        private void ApplyConfigAndPermissions()
        {
            _numTileThue.Enabled = SystemConfig.CoThueSuat && !_readOnly;
            _numTienThue.Enabled = SystemConfig.CoThueSuat;
            _numPhiVanChuyen.Enabled = SystemConfig.CoPhiVanChuyen && !_readOnly;
            _numTileGiamGia.Enabled = !_readOnly;
            _numTienGiamGia.Enabled = !_readOnly;

            _dtNgay.Enabled = !_readOnly;
            _txtNumber.ReadOnly = _readOnly;
            _dtGioDat.Enabled = !_readOnly;
            _dtTuGio.Enabled = !_readOnly;
            _dtDenGio.Enabled = !_readOnly;
            _btnPickRoom.Enabled = !_readOnly;
            _btnDeposit.Enabled = !_readOnly;
            _btnColor.Enabled = !_readOnly;
            _customer.Enabled = !_readOnly;
            _txtTenKhach.ReadOnly = _readOnly;
            _txtDienThoai.ReadOnly = _readOnly;
            _txtDiaChi.ReadOnly = _readOnly;
            _method.Enabled = !_readOnly;
            _purpose.Enabled = !_readOnly;
            _txtNote.ReadOnly = _readOnly;
            _btnAddProduct.Enabled = !_readOnly;
            _numQty.Enabled = !_readOnly;
            _btnDeleteLine.Enabled = !_readOnly;
            _detailGrid.ReadOnly = _readOnly;
            ApplyColorButton();
        }

        private void OnDirtyChanged()
        {
            if (_loading)
                return;
            UpdateSaveButtons();
        }

        private void UpdateSaveButtons()
        {
            bool dirty = Dirty;
            _btnSave.Enabled = dirty && !_readOnly;
            _btnSaveNew.Enabled = dirty && !_readOnly;
            _btnSaveClose.Enabled = dirty && !_readOnly;
            _btnDelete.Enabled = !_isNew && _canRemove && !_readOnly;
        }

        private void UpdateNavButtons()
        {
            _btnPrev.Enabled = _rowIds != null && _rowIndex > 0;
            _btnNext.Enabled = _rowIds != null && _rowIndex >= 0 && _rowIndex < _rowIds.Count - 1;
            _btnNew.Enabled = !_readOnly;
        }

        private bool Dirty
        {
            get { return _original == null || Snapshot() != _original; }
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_txtNumber.Text).Append('|');
            sb.Append(_dtNgay.Value).Append('|');
            sb.Append(_dtGioDat.Value.TimeOfDay).Append('|');
            sb.Append(_dtTuGio.Checked ? _dtTuGio.Value.ToString() : "").Append('|');
            sb.Append(_dtDenGio.Checked ? _dtDenGio.Value.ToString() : "").Append('|');
            sb.Append(string.Join(",", _roomIds.ToArray())).Append('|');
            sb.Append(_customer.SelectedId).Append('|');
            sb.Append(_txtTenKhach.Text).Append('|');
            sb.Append(_txtDienThoai.Text).Append('|');
            sb.Append(_txtDiaChi.Text).Append('|');
            sb.Append(_method.SelectedId).Append('|');
            sb.Append(_purpose.SelectedId).Append('|');
            sb.Append(_txtNote.Text).Append('|');
            sb.Append(_colorArgb).Append('|');
            sb.Append(_numTileGiamGia.Value).Append('|');
            sb.Append(_numTienGiamGia.Value).Append('|');
            sb.Append(_discountByMoney).Append('|');
            sb.Append(_numTileThue.Value).Append('|');
            sb.Append(_numPhiVanChuyen.Value).Append('|');
            foreach (BookingLine l in _lines)
            {
                sb.Append(l.ProductId).Append(':').Append(l.UnitId).Append(':');
                sb.Append(l.Quantity).Append(':').Append(l.Price).Append(':').Append(l.DiscountPct).Append(';');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Phím tắt / giao diện

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.L)
            {
                SaveStay();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveAndNew();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.N)
            {
                NewBlank();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F8)
            {
                if (_detailGrid.ContainsFocus)
                    DeleteLine();
                else
                    OpenRoomPicker();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F10)
            {
                Navigate(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F11)
            {
                Navigate(1);
                e.Handled = true;
            }
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            if (_productGrid != null)
                ThemeApplier.ApplyGrid(_productGrid, ThemeManager.Current);
            if (_detailGrid != null)
                ThemeApplier.ApplyGrid(_detailGrid, ThemeManager.Current);
            if (_btnColor != null)
                ApplyColorButton();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FormClosing += OnFormClosing;
        }
    }
}
