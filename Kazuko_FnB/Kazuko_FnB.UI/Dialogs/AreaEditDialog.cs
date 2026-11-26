using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
    /// Dialog Thêm/Sửa khu vực (spec 1.3: trang chính + tối đa 4 trang phụ, code tay).
    /// Trang chính: Tên khu vực, Ảnh, Biểu tượng (xem trước), Tab hiển thị, Giá mặt hàng, Kho hàng,
    /// Mẫu hóa đơn, Ghi chú + nhóm giá theo giờ (chỉ hiện khi CoTienGio).
    /// Trang "Mặt hàng mặc định" chỉ hiện khi SuDungMatHangMacDinh; "Bảng giá áp dụng" chỉ khi CoTienGio +
    /// ChoPhepDungNhieuBangGiaTrenBill; "Truy cập" chỉ khi PhanQuyenTruyCapTheoKhuVuc.
    /// Trang "Mặt hàng theo giờ" (SuDungGiaTheoGio) CHƯA dựng: nội dung chưa có trong spec (Q15) và cấu hình
    /// đang tắt trong DB thật — dựng khi có ảnh chụp/bản ghi của app gốc cho trang này.
    /// Combo "Giá mặt hàng" ẩn Giá 2/3/4 khi SuDungGia2/3/4 tắt (3.5); combo Mẫu hóa đơn lọc theo form
    /// "Hóa đơn bán hàng" (3.4). Lưu khu vực + 3 bảng con trong MỘT transaction (5.2).
    /// Đã áp quyết định của Chiến 21/11: bỏ thùng rác (xóa cứng), tiền decimal.
    /// </summary>
    internal sealed class AreaEditDialog : ThemedDialog
    {
        private const int ContentWidth96 = 600;
        private const int ContentHeight96 = 400;
        private const string ShortcutsHint = "(Ctrl+S: Lưu&Mới, Ctrl+L: Lưu, Ctrl+N: Mới, Ctrl+P: In, ESC: Thoát)";
        private const string UnsupportedText = "Chức năng chưa hỗ trợ trong phiên bản này.";

        private readonly string _databasePath;
        private readonly AreaRepository _repo;
        private readonly FormRepository _formRepo;
        private readonly ProductRepository _productRepo;

        private readonly List<string> _rowIds;
        private int _rowIndex;

        private List<LookupItem> _warehouses = new List<LookupItem>();
        private List<LookupItem> _priceLevels = new List<LookupItem>();
        private List<LookupItem> _hourlyPrices = new List<LookupItem>();
        private List<LookupItem> _priceLists = new List<LookupItem>();
        private List<AreaIconRow> _icons = new List<AreaIconRow>();
        private List<ProductRow> _products = new List<ProductRow>();
        private List<LookupItem> _users = new List<LookupItem>();

        private AreaRow _row;
        private bool _isNew;
        private bool _readOnly;
        private bool _loadFailed;
        private bool _loading;
        private string _original = "";

        private readonly bool _canRemove;

        // ---- Tab chính: Khu vực ----
        private readonly TextBox _name = new TextBox();
        private readonly ImageCombo _image = new ImageCombo();
        private readonly LookupCombo _icon = new LookupCombo();
        private readonly PictureBox _picIcon = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
        // B2: TABHIENTHI của app gốc là chỉ số dải tab màn bán hàng: 0 = tab trên, 1 = tab dưới (không phải cờ 30/0).
        private readonly Label _lblShowTab = new Label { Text = "Tab hiển thị" };
        private readonly RadioButton _rTabTop = new RadioButton { Text = "Trên" };
        private readonly RadioButton _rTabBottom = new RadioButton { Text = "Dưới" };
        private readonly LookupCombo _priceLevel = new LookupCombo();
        private readonly LookupCombo _warehouse = new LookupCombo();
        private readonly LookupCombo _template = new LookupCombo();
        private readonly TextBox _note = new TextBox();
        private readonly GroupBox _grpPrice = new GroupBox { Text = "Giá theo giờ", TabStop = false };
        private readonly RadioButton _rByHour = new RadioButton { Text = "Theo giờ" };
        private readonly RadioButton _rByPriceList = new RadioButton { Text = "Theo bảng giá" };
        private readonly Label _lblLegacyCalc = new Label { AutoSize = false, Visible = false };
        private readonly NumberBox _unitPrice = new NumberBox();
        private readonly NumberBox _openAmount = new NumberBox();
        private readonly LookupCombo _priceList = new LookupCombo();
        private readonly LookupCombo _hourlyPrice = new LookupCombo();
        private readonly DateTimePicker _fromHour = new DateTimePicker();
        private readonly DateTimePicker _toHour = new DateTimePicker();
        private readonly Label _lblName = new Label { Text = "Tên khu vực *", AutoSize = false };
        private readonly Label _lblImage = new Label { Text = "Ảnh", AutoSize = false };
        private readonly Label _lblIcon = new Label { Text = "Biểu tượng", AutoSize = false };
        private readonly Label _lblPriceLevel = new Label { Text = "Giá mặt hàng", AutoSize = false };
        private readonly Label _lblWarehouse = new Label { Text = "Kho hàng", AutoSize = false };
        private readonly Label _lblTemplate = new Label { Text = "Mẫu hóa đơn", AutoSize = false };
        private readonly Label _lblNote = new Label { Text = "Ghi chú", AutoSize = false };
        private readonly Label _lblUnitPrice = new Label { Text = "Đơn giá", AutoSize = false };
        private readonly Label _lblOpenAmount = new Label { Text = "Tiền mở bàn", AutoSize = false };
        private readonly Label _lblPriceList = new Label { Text = "Bảng giá", AutoSize = false };
        private readonly Label _lblHourlyPrice = new Label { Text = "Giá theo giờ", AutoSize = false };
        private readonly Label _lblFromHour = new Label { Text = "Từ giờ", AutoSize = false };
        private readonly Label _lblToHour = new Label { Text = "Đến giờ", AutoSize = false };

        // ---- Tab Mặt hàng mặc định ----
        private readonly DataGridView _defaultsGrid = new DataGridView();
        private readonly FlatButton _btnAddLine = new FlatButton { Text = "Thêm dòng (F4)" };
        private readonly FlatButton _btnDelLine = new FlatButton { Text = "Xóa dòng (F8)" };

        // ---- Tab Bảng giá áp dụng / Truy cập ----
        private readonly DataGridView _priceGrid = new DataGridView();
        private readonly DataGridView _accessGrid = new DataGridView();

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
        private readonly TabControl _tabs = new TabControl();
        private readonly TabPage _tabMain = new TabPage("Khu vực");
        private readonly TabPage _tabDefaults = new TabPage("Mặt hàng mặc định");
        private readonly TabPage _tabPrice = new TabPage("Bảng giá áp dụng");
        private readonly TabPage _tabAccess = new TabPage("Truy cập");

        /// <summary>ID khu vực đã lưu (null nếu chưa lưu lần nào trong phiên dialog).</summary>
        public string SavedId { get; private set; }

        /// <summary>Bản ghi đang sửa đã bị xóa (dialog đóng sau khi xóa).</summary>
        public bool Deleted { get; private set; }

        /// <summary>Dùng cho --selftest: số tab đang hiện (chỉ tab chính khi mọi cấu hình tắt).</summary>
        internal int TabCount
        {
            get { return _tabs.TabPages.Count; }
        }

        internal bool SuppressPopup { get; set; }
        internal int DefaultsDataErrorCountForTest { get; private set; }
        internal string DefaultsDataErrorDetailForTest { get; private set; }
        internal DataGridView DefaultsGridForTest { get { return _defaultsGrid; } }
        internal TabControl TabsForTest { get { return _tabs; } }
        internal TabPage DefaultsTabForTest { get { return _tabDefaults; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton AddDefaultLineButtonForTest { get { return _btnAddLine; } }
        internal FlatButton DeleteDefaultLineButtonForTest { get { return _btnDelLine; } }
        internal bool DirtyForTest { get { return Dirty; } }

        /// <param name="areaId">null = thêm mới.</param>
        /// <param name="rowIds">ID các dòng trên cây theo thứ tự hiện tại (để F10/F11 chuyển dòng); null = không chuyển được.</param>
        public AreaEditDialog(UserSession session, string areaId, List<string> rowIds)
            : base("KHU VỰC - THÊM MỚI", ShortcutsHint, 632, 530)
        {
            _databasePath = session.DatabasePath;
            _repo = new AreaRepository(_databasePath);
            _formRepo = new FormRepository(_databasePath);
            _productRepo = new ProductRepository(_databasePath);
            _rowIds = rowIds;
            _rowIndex = rowIds == null ? -1 : rowIds.IndexOf(areaId ?? "");
            _isNew = string.IsNullOrEmpty(areaId);

            _canRemove = AppSession.Can(RoomRules.FunctionName, RoleModes.Remove);
            // Chỉ có Xem (không Sửa, không Thêm) → mở dialog ở chế độ chỉ đọc (mục 4, mặc định tạm Q13).
            _readOnly = _isNew
                ? !AppSession.Can(RoomRules.FunctionName, RoleModes.Add)
                : !AppSession.Can(RoomRules.FunctionName, RoleModes.Edit);

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = exit;

            BuildToolbar();
            BuildMainTab();
            BuildDefaultsTab();
            BuildPriceTab();
            BuildAccessTab();
            _tabs.Font = new Font("Segoe UI", 10f);
            _tabs.TabPages.Add(_tabMain);
            if (SystemConfig.SuDungMatHangMacDinh)
                _tabs.TabPages.Add(_tabDefaults);
            if (SystemConfig.CoTienGio && SystemConfig.ChoPhepDungNhieuBangGiaTrenBill)
                _tabs.TabPages.Add(_tabPrice);
            if (SystemConfig.PhanQuyenTruyCapTheoKhuVuc)
                _tabs.TabPages.Add(_tabAccess);
            _tabs.Dock = DockStyle.Fill;
            Body.Controls.Add(_tabs);

            LoadLookups();
            if (_isNew)
                NewRecord();
            else
                LoadRecord(areaId);

            ApplyConfigAndPermissions();
            UpdateSaveButtons();
            UpdateNavButtons();
            _original = Snapshot();

            KeyDown += OnKeyDown;
            Load += delegate { FitTabContent(); };
            Shown += delegate
            {
                if (_loadFailed)
                {
                    Msg.Error(this, "Khu vực không còn tồn tại.");
                    Close();
                    return;
                }
                _name.Focus();
                _name.SelectAll();
            };
            FormClosing += OnFormClosing;
        }

        /// <summary>
        /// Vùng nội dung của tab (TabPage) = 600×400 ở 96 DPI. Đo vùng tab thật rồi nới/co dialog
        /// đúng bằng chênh lệch (phần tab header, viền, toolbar, header/footer dialog tự được tính).
        /// </summary>
        private void FitTabContent()
        {
            float s = Dpi;
            PerformLayout();
            _tabs.PerformLayout();
            Size target = new Size((int)(ContentWidth96 * s), (int)(ContentHeight96 * s));
            Size cur = _tabMain.ClientSize;
            int dw = target.Width - cur.Width;
            int dh = target.Height - cur.Height;
            if (dw == 0 && dh == 0)
                return;
            Point loc = Location;
            ClientSize = new Size(ClientSize.Width + dw, ClientSize.Height + dh);
            Location = new Point(loc.X - dw / 2, Math.Max(0, loc.Y - dh / 2));
        }

        // ------------------------------------------------------------------ Dựng giao diện

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
            _btnShortcuts.Click += delegate { ShowShortcutsMenu(); };
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
            _shortcutsMenu.AddItem("In (Ctrl+P)", delegate { Msg.Info(this, UnsupportedText); });
            _shortcutsMenu.AddSeparator();
            _shortcutsMenu.AddItem("Thoát (Esc)", delegate { Close(); });
        }

        private void ShowShortcutsMenu()
        {
            _shortcutsMenu.Show(_btnShortcuts, new Point(0, _btnShortcuts.Height));
        }

        private void Place(Control c, Control parent, int x, int y, int w, int h, int tabIndex)
        {
            float s = Dpi;
            c.SetBounds((int)(x * s), (int)(y * s), (int)(w * s), (int)(h * s));
            c.TabIndex = tabIndex;
            parent.Controls.Add(c);
        }

        private void PlaceLabel(Label l, Control parent, int x, int y)
        {
            float s = Dpi;
            l.SetBounds((int)(x * s), (int)((y + 2) * s), (int)(105 * s), (int)(20 * s));
            l.AutoEllipsis = true;
            l.TabStop = false;
            parent.Controls.Add(l);
        }

        private void BuildMainTab()
        {
            // Vị trí/kích thước theo bảng 1.3 (96 DPI, tọa độ trong tab page 600×400).
            _lblName.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            PlaceLabel(_lblName, _tabMain, 7, 10);
            PlaceLabel(_lblImage, _tabMain, 7, 40);
            PlaceLabel(_lblIcon, _tabMain, 278, 40);
            PlaceLabel(_lblPriceLevel, _tabMain, 7, 100);
            PlaceLabel(_lblWarehouse, _tabMain, 7, 130);
            PlaceLabel(_lblTemplate, _tabMain, 7, 160);
            PlaceLabel(_lblNote, _tabMain, 7, 190);

            Place(_name, _tabMain, 118, 6, 280, 20, 0);
            Place(_image, _tabMain, 118, 36, 150, 21, 1);
            Place(_icon, _tabMain, 360, 36, 140, 21, 2);
            Place(_picIcon, _tabMain, 508, 10, 54, 54, 3);
            PlaceLabel(_lblShowTab, _tabMain, 7, 66);
            Place(_rTabTop, _tabMain, 118, 68, 70, 17, 4);
            Place(_rTabBottom, _tabMain, 194, 68, 70, 17, 4);
            Place(_priceLevel, _tabMain, 118, 96, 280, 21, 5);
            Place(_warehouse, _tabMain, 118, 126, 280, 21, 6);
            Place(_template, _tabMain, 118, 156, 280, 21, 7);
            Place(_note, _tabMain, 118, 186, 280, 20, 8);

            // Nhóm giá theo giờ (chỉ hiện khi CoTienGio — xem ApplyConfigAndPermissions).
            _grpPrice.SetBounds((int)(7 * Dpi), (int)(222 * Dpi), (int)(586 * Dpi), (int)(168 * Dpi));
            _tabMain.Controls.Add(_grpPrice);
            Place(_rByHour, _grpPrice, 12, 22, 100, 17, 9);
            Place(_rByPriceList, _grpPrice, 120, 22, 130, 17, 10);
            Place(_lblLegacyCalc, _grpPrice, 12, 44, 560, 17, 11);
            PlaceLabel(_lblUnitPrice, _grpPrice, 12, 68);
            PlaceLabel(_lblOpenAmount, _grpPrice, 250, 68);
            Place(_unitPrice, _grpPrice, 118, 66, 110, 20, 12);
            Place(_openAmount, _grpPrice, 356, 66, 110, 20, 13);
            PlaceLabel(_lblPriceList, _grpPrice, 12, 98);
            Place(_priceList, _grpPrice, 118, 96, 280, 21, 14);
            PlaceLabel(_lblHourlyPrice, _grpPrice, 12, 128);
            PlaceLabel(_lblFromHour, _grpPrice, 280, 128);
            PlaceLabel(_lblToHour, _grpPrice, 440, 128);
            Place(_hourlyPrice, _grpPrice, 118, 126, 150, 21, 15);
            Place(_fromHour, _grpPrice, 330, 126, 100, 20, 16);
            Place(_toHour, _grpPrice, 490, 126, 90, 20, 17);

            _unitPrice.Minimum = 0m;
            _unitPrice.Maximum = 1000000000000m;
            _unitPrice.DecimalPlaces = 2;
            _unitPrice.ThousandsSeparator = true;
            _openAmount.Minimum = 0m;
            _openAmount.Maximum = 1000000000000m;
            _openAmount.DecimalPlaces = 2;
            _openAmount.ThousandsSeparator = true;
            _fromHour.Format = DateTimePickerFormat.Time;
            _fromHour.ShowUpDown = true;
            _fromHour.ShowCheckBox = true;
            _toHour.Format = DateTimePickerFormat.Time;
            _toHour.ShowUpDown = true;
            _toHour.ShowCheckBox = true;

            _icon.SelectedIndexChanged += delegate { UpdateIconPreview(); OnDirtyChanged(); };
            foreach (RadioButton r in new[] { _rByHour, _rByPriceList })
                r.CheckedChanged += delegate { if (r.Checked) { _lblLegacyCalc.Visible = false; UpdatePriceControls(); OnDirtyChanged(); } };

            foreach (Control c in new Control[] { _name, _note })
                ((TextBox)c).TextChanged += delegate { OnDirtyChanged(); };
            foreach (NumericUpDown n in new[] { _unitPrice, _openAmount })
                n.ValueChanged += delegate { OnDirtyChanged(); };
            _image.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _rTabTop.CheckedChanged += delegate { OnDirtyChanged(); };
            _rTabBottom.CheckedChanged += delegate { OnDirtyChanged(); };
            _priceLevel.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _warehouse.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _template.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _priceList.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _hourlyPrice.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _fromHour.ValueChanged += delegate { OnDirtyChanged(); };
            _toHour.ValueChanged += delegate { OnDirtyChanged(); };
        }

        private void BuildDefaultsTab()
        {
            float s = Dpi;
            var bar = new Panel { Dock = DockStyle.Top, Height = (int)(30 * s) };
            _btnAddLine.Height = (int)(26 * s);
            _btnAddLine.Width = (int)(120 * s);
            _btnAddLine.SetBounds(0, (int)(2 * s), _btnAddLine.Width, _btnAddLine.Height);
            _btnDelLine.Height = (int)(26 * s);
            _btnDelLine.Width = (int)(110 * s);
            _btnDelLine.SetBounds(_btnAddLine.Right + (int)(6 * s), (int)(2 * s), _btnDelLine.Width, _btnDelLine.Height);
            bar.Controls.Add(_btnAddLine);
            bar.Controls.Add(_btnDelLine);
            _btnAddLine.Click += delegate { AddDefaultLine(); };
            _btnDelLine.Click += delegate { DeleteDefaultLine(); };

            _defaultsGrid.Dock = DockStyle.Fill;
            _defaultsGrid.AllowUserToAddRows = false;
            _defaultsGrid.AllowUserToDeleteRows = false;
            _defaultsGrid.AllowUserToResizeRows = false;
            _defaultsGrid.AutoGenerateColumns = false;
            _defaultsGrid.MultiSelect = false;
            _defaultsGrid.RowHeadersVisible = false;
            _defaultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _defaultsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _defaultsGrid.ColumnHeadersHeight = (int)(34 * s);
            _defaultsGrid.RowTemplate.Height = (int)(28 * s);
            _defaultsGrid.Font = new Font("Segoe UI", 10f);
            _defaultsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            var colProduct = new DataGridViewComboBoxColumn
            {
                Name = "colProduct",
                HeaderText = "Mặt hàng",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DisplayMember = "Name",
                ValueMember = "Id",
                FlatStyle = FlatStyle.Flat
            };
            _defaultsGrid.Columns.Add(colProduct);
            var colQty = new DataGridViewTextBoxColumn
            {
                Name = "colQty",
                HeaderText = "Số lượng",
                Width = (int)(120 * s)
            };
            NumericEditGridCell.ConfigureColumn(colQty, 2);
            _defaultsGrid.Columns.Add(colQty);
            _defaultsGrid.CellParsing += delegate(object sender, DataGridViewCellParsingEventArgs e) { NumericEditGridCell.ParseCell(_defaultsGrid, e, 0m); };
            _defaultsGrid.CellValueChanged += delegate { OnDirtyChanged(); };
            _defaultsGrid.CellEndEdit += delegate { OnDirtyChanged(); };
            _defaultsGrid.CurrentCellDirtyStateChanged += delegate { NumericEditGridCell.HandleDirtyStateChanged(_defaultsGrid, OnDirtyChanged); };
            _defaultsGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                DefaultsDataErrorCountForTest++;
                DefaultsDataErrorDetailForTest = NumericEditGridCell.AppendDataErrorDetail(DefaultsDataErrorDetailForTest, _defaultsGrid, e);
                e.ThrowException = false;
            };

            _tabDefaults.Controls.Add(_defaultsGrid);
            _tabDefaults.Controls.Add(bar);
        }

        private void BuildPriceTab()
        {
            BuildCheckGrid(_priceGrid, "Bảng giá");
            _tabPrice.Controls.Add(_priceGrid);
        }

        private void BuildAccessTab()
        {
            BuildCheckGrid(_accessGrid, "Người dùng");
            _tabAccess.Controls.Add(_accessGrid);
        }

        private void BuildCheckGrid(DataGridView grid, string nameHeader)
        {
            float s = Dpi;
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoGenerateColumns = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(34 * s);
            grid.RowTemplate.Height = (int)(28 * s);
            grid.Font = new Font("Segoe UI", 10f);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colSel",
                HeaderText = "",
                Width = (int)(40 * s),
                ReadOnly = false
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = nameHeader,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += delegate { OnDirtyChanged(); };
        }

        // ------------------------------------------------------------------ Nạp dữ liệu

        private void LoadLookups()
        {
            float s = Dpi;
            _warehouses = _formRepo.LoadAll("DKHOHANG");
            _priceLists = _formRepo.LoadAll("DBANGGIA");
            _hourlyPrices = _formRepo.LoadAll("DGIAMATHANG");
            _icons = _repo.LoadIcons();
            _products = _productRepo.LoadProducts();
            _users = _repo.LoadUsersForAccess();

            var iconItems = new List<LookupItem>();
            foreach (AreaIconRow i in _icons)
                iconItems.Add(new LookupItem { Id = i.Id, Name = i.Name });
            _icon.SetItems(iconItems);
            _warehouse.SetItems(_warehouses);
            _priceList.SetItems(_priceLists);
            _hourlyPrice.SetItems(_hourlyPrices);
            _image.Load(new ImageRepository(_databasePath).LoadAll(), (int)(20 * s));

            var colProduct = _defaultsGrid.Columns["colProduct"] as DataGridViewComboBoxColumn;
            if (colProduct != null)
                colProduct.DataSource = _products;
        }

        private void NewRecord()
        {
            _row = AreaRow.NewDefault();
            _isNew = true;
            // Ảnh 05: Kho hàng mặc định là "KHO BÁN HÀNG"; không có kho đó thì lấy kho đầu danh sách.
            if (_warehouses.Count > 0)
            {
                LookupItem def = _warehouses.Find(w => string.Equals(
                    ProductText.NormalizeName(w.Name), "KHO BÁN HÀNG", StringComparison.OrdinalIgnoreCase));
                _row.WarehouseId = (def ?? _warehouses[0]).Id;
            }
            SetTitle("KHU VỰC - THÊM MỚI");
            FillControls(new List<PriceAssignment>(), new List<string>(), new List<AreaDefaultItem>());
        }

        private void LoadRecord(string areaId)
        {
            _row = _repo.Load(areaId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = AreaRow.NewDefault();
                return;
            }
            _isNew = false;
            SetTitle("KHU VỰC - SỬA");
            FillControls(_repo.LoadPriceAssignments(areaId), _repo.LoadAccessUserIds(areaId),
                _repo.LoadDefaultItems(areaId));
        }

        private void FillControls(List<PriceAssignment> prices, List<string> accessUserIds, List<AreaDefaultItem> defaults)
        {
            _loading = true;
            try
            {
                _name.Text = _row.Name ?? "";
                _image.SelectedId = _row.ImageId;
                _icon.SelectId(_row.IconId);
                // Giá trị lạ (vd 30 do bản clone cũ đã ghi) hiện mặc định "Trên"; lưu lại sẽ chuẩn hóa về 0.
                _rTabBottom.Checked = _row.ShowTab == 1;
                _rTabTop.Checked = _row.ShowTab != 1;
                _priceLevels = RoomRules.FilterPriceLevels(_formRepo.LoadAll("DGIAMATHANG"),
                    SystemConfig.SuDungGia2, SystemConfig.SuDungGia3, SystemConfig.SuDungGia4, _row.PriceLevelId);
                _priceLevel.SetItems(_priceLevels);
                _priceLevel.SelectId(_row.PriceLevelId);
                _warehouse.SelectId(_row.WarehouseId);
                var templates = new List<LookupItem>();
                templates.Add(new LookupItem { Id = null, Name = "(Dùng mẫu mặc định)" });
                templates.AddRange(_repo.LoadTemplates(_row.TemplateId));
                _template.SetItems(templates);
                _template.SelectId(_row.TemplateId);
                _note.Text = _row.Note ?? "";
                _unitPrice.Value = Clamp(_unitPrice, _row.UnitPrice);
                _openAmount.Value = Clamp(_openAmount, _row.OpenAmount);
                _priceList.SelectId(_row.PriceListId);
                _hourlyPrice.SelectId(_row.HourlyPriceId);
                SetHour(_fromHour, _row.FromHour);
                SetHour(_toHour, _row.ToHour);
                SetCalcRadios(_row.CalcHour);
                BuildCheckGridRows(_priceGrid, _priceLists, AssignedIds(prices));
                BuildCheckGridRows(_accessGrid, _users, new HashSet<string>(accessUserIds ?? new List<string>(), StringComparer.Ordinal));
                BuildDefaultsGridRows(defaults);
                UpdateIconPreview();
                UpdatePriceControls();
            }
            finally
            {
                _loading = false;
            }
        }

        private static void SetHour(DateTimePicker picker, DateTime? value)
        {
            if (value.HasValue)
            {
                picker.Value = value.Value;
                picker.Checked = true;
            }
            else
            {
                picker.Value = DateTime.Today;
                picker.Checked = false;
            }
        }

        private static HashSet<string> AssignedIds(List<PriceAssignment> prices)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (prices != null)
            {
                foreach (PriceAssignment p in prices)
                    if (!string.IsNullOrEmpty(p.PriceListId))
                        ids.Add(p.PriceListId);
            }
            return ids;
        }

        private static void BuildCheckGridRows(DataGridView grid, List<LookupItem> items, HashSet<string> checkedIds)
        {
            grid.Rows.Clear();
            if (items == null)
                return;
            foreach (LookupItem item in items)
            {
                int i = grid.Rows.Add(checkedIds.Contains(item.Id), item.Name);
                grid.Rows[i].Tag = item.Id;
            }
        }

        private void BuildDefaultsGridRows(List<AreaDefaultItem> defaults)
        {
            _defaultsGrid.Rows.Clear();
            if (defaults == null)
                return;
            foreach (AreaDefaultItem d in defaults)
            {
                if (string.IsNullOrEmpty(d.ProductId))
                    continue;
                int i = _defaultsGrid.Rows.Add();
                _defaultsGrid.Rows[i].Cells["colProduct"].Value = d.ProductId;
                _defaultsGrid.Rows[i].Cells["colQty"].Value = d.Quantity;
            }
        }

        private static decimal Clamp(NumericUpDown box, decimal value)
        {
            if (value < box.Minimum)
                return box.Minimum;
            if (value > box.Maximum)
                return box.Maximum;
            return value;
        }

        /// <summary>
        /// Chọn radio theo CACHTINHGIO của khu vực (chỉ hai giá trị trên dialog). Giá trị lạ thì không tick
        /// radio nào mà hiện nhãn chỉ-xem; khi lưu sẽ giữ nguyên giá trị cũ trừ khi người dùng tick một radio khác.
        /// </summary>
        private void SetCalcRadios(int value)
        {
            string key = RoomRules.CalcModeKey(value);
            _rByHour.Checked = key == "hour";
            _rByPriceList.Checked = key == "pricelist";
            if (key == null)
            {
                _lblLegacyCalc.Text = "Cách tính giờ: " + value + " (chưa rõ — giữ nguyên khi lưu)";
                _lblLegacyCalc.Visible = true;
            }
            else
            {
                _lblLegacyCalc.Visible = false;
            }
        }

        /// <summary>Giá trị CACHTINHGIO sẽ lưu: radio được chọn, hoặc giữ nguyên giá trị đã nạp khi không tick radio nào.</summary>
        private int SelectedCalcValue()
        {
            if (_rByPriceList.Checked)
                return RoomCalc.ByPriceList;
            if (_rByHour.Checked)
                return RoomCalc.ByHour;
            // Không tick radio nào (giá trị lạ, hoặc nhóm giá đang ẩn vì CoTienGio = 0): giữ nguyên.
            return _row == null ? RoomCalc.ByHour : _row.CalcHour;
        }

        private void UpdateIconPreview()
        {
            Image old = _picIcon.Image;
            _picIcon.Image = null;
            if (old != null)
                old.Dispose();
            string iconId = _icon.SelectedId;
            if (string.IsNullOrEmpty(iconId))
                return;
            try
            {
                byte[] data = _repo.GetIconImage(iconId);
                if (data == null || data.Length == 0)
                    return;
                using (var ms = new MemoryStream(data))
                using (Image img = Image.FromStream(ms))
                    _picIcon.Image = new Bitmap(img);
            }
            catch
            {
                _picIcon.Image = null;
            }
        }

        // ------------------------------------------------------------------ Cấu hình + quyền (3.10/mục 4)

        private void ApplyConfigAndPermissions()
        {
            bool ro = _readOnly;
            _grpPrice.Visible = SystemConfig.CoTienGio;
            UpdatePriceControls();

            if (ro)
            {
                foreach (Control c in new Control[] { _name, _image, _icon, _priceLevel, _warehouse, _template, _note, _unitPrice, _openAmount, _priceList, _hourlyPrice, _fromHour, _toHour })
                    c.Enabled = false;
                _rTabTop.Enabled = false;
                _rTabBottom.Enabled = false;
                foreach (RadioButton r in new[] { _rByHour, _rByPriceList })
                    r.Enabled = false;
                _defaultsGrid.ReadOnly = true;
                _priceGrid.ReadOnly = true;
                _accessGrid.ReadOnly = true;
                _btnAddLine.Enabled = false;
                _btnDelLine.Enabled = false;
                SubtitleText = ShortcutsHint + " (chỉ xem — không có quyền sửa)";
            }
            UpdateDefaultsButtons();
        }

        /// <summary>
        /// Ô giá chỉ hiện khi radio tương ứng được chọn (1.3: Bảng giá khi theo bảng giá; Đơn giá, Giá theo giờ,
        /// Từ giờ, Đến giờ khi theo giờ). Giá trị trong ô ẩn vẫn giữ nguyên khi lưu (không xóa).
        /// </summary>
        private void UpdatePriceControls()
        {
            bool byHour = _rByHour.Checked;
            bool byPrice = _rByPriceList.Checked;
            _unitPrice.Visible = byHour;
            _lblUnitPrice.Visible = byHour;
            _hourlyPrice.Visible = byHour;
            _lblHourlyPrice.Visible = byHour;
            _fromHour.Visible = byHour;
            _lblFromHour.Visible = byHour;
            _toHour.Visible = byHour;
            _lblToHour.Visible = byHour;
            _priceList.Visible = byPrice;
            _lblPriceList.Visible = byPrice;
        }

        private void UpdateDefaultsButtons()
        {
            bool hasRow = _defaultsGrid.CurrentRow != null;
            _btnAddLine.Enabled = !_readOnly;
            _btnDelLine.Enabled = !_readOnly && hasRow;
        }

        // ------------------------------------------------------------------ Trạng thái bẩn

        private void OnDirtyChanged()
        {
            if (_loading)
                return;
            UpdateSaveButtons();
            UpdateDefaultsButtons();
        }

        private void UpdateSaveButtons()
        {
            bool dirty = Dirty;
            _btnSave.Enabled = dirty && !_readOnly;
            _btnSaveNew.Enabled = dirty && !_readOnly;
            _btnSaveClose.Enabled = dirty && !_readOnly;
            _btnDelete.Enabled = !_isNew && _canRemove; // Q13: xóa chỉ cần quyền Xóa, như xóa từ lưới.
        }

        private void UpdateNavButtons()
        {
            _btnPrev.Enabled = _rowIds != null && _rowIndex > 0;
            _btnNext.Enabled = _rowIds != null && _rowIndex >= 0 && _rowIndex < _rowIds.Count - 1;
            _btnNew.Enabled = !_readOnly;
        }

        private bool Dirty
        {
            get { return _original == null || Snapshot() != _original || _defaultsGrid.IsCurrentCellDirty; }
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_name.Text).Append('|');
            sb.Append(_image.SelectedId).Append('|');
            sb.Append(_icon.SelectedId).Append('|');
            sb.Append(_rTabBottom.Checked).Append('|');
            sb.Append(_priceLevel.SelectedId).Append('|');
            sb.Append(_warehouse.SelectedId).Append('|');
            sb.Append(_template.SelectedId).Append('|');
            sb.Append(_note.Text).Append('|');
            sb.Append(SelectedCalcValue()).Append('|');
            sb.Append(_unitPrice.Value).Append('|');
            sb.Append(_openAmount.Value).Append('|');
            sb.Append(_priceList.SelectedId).Append('|');
            sb.Append(_hourlyPrice.SelectedId).Append('|');
            sb.Append(_fromHour.Checked ? _fromHour.Value.ToString("HH:mm") : "").Append('|');
            sb.Append(_toHour.Checked ? _toHour.Value.ToString("HH:mm") : "").Append('|');
            sb.Append(CheckGridSnapshot(_priceGrid)).Append('|');
            sb.Append(CheckGridSnapshot(_accessGrid)).Append('|');
            foreach (DataGridViewRow r in _defaultsGrid.Rows)
            {
                object p = r.Cells["colProduct"].Value;
                object q = r.Cells["colQty"].Value;
                if (p != null)
                    sb.Append(p).Append(':').Append(q).Append(';');
            }
            return sb.ToString();
        }

        private static string CheckGridSnapshot(DataGridView grid)
        {
            var sb = new StringBuilder();
            foreach (DataGridViewRow r in grid.Rows)
            {
                if (r.Cells["colSel"].Value is bool && (bool)r.Cells["colSel"].Value)
                    sb.Append(r.Tag).Append(';');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Lưu / Xóa

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

        private bool DoSave()
        {
            if (_readOnly)
                return false;
            _defaultsGrid.EndEdit();
            CollectRow();

            string error = RoomRules.ValidateArea(_row.Name, SystemConfig.CoTienGio, _row.CalcHour,
                _row.PriceListId, _row.UnitPrice, _row.OpenAmount);
            if (error != null)
            {
                Msg.Warning(this, error);
                _tabs.SelectedTab = _tabMain;
                if (error == RoomText.AreaNameEmpty)
                {
                    _name.Focus();
                    _name.SelectAll();
                }
                else if (error == RoomText.PriceListRequired)
                    _priceList.Focus();
                else
                    _unitPrice.Focus();
                return false;
            }

            AreaSaveResult result;
            try
            {
                result = _repo.Save(_row, CollectPriceAssignments(), CollectAccessUserIds(),
                    CollectDefaultItems(), AppSession.UserId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return false;
            }
            if (!result.Ok)
            {
                Msg.Warning(this, result.ErrorMessage);
                _tabs.SelectedTab = _tabMain;
                _name.Focus();
                _name.SelectAll();
                return false;
            }

            SavedId = result.SavedId;
            _row.Id = result.SavedId;
            bool wasNew = _isNew;
            _isNew = false;
            if (wasNew)
            {
                SetTitle("KHU VỰC - SỬA");
                ApplyConfigAndPermissions();
            }
            // Đồng bộ chữ đã chuẩn hóa (tên đã cắt khoảng trắng) về ô nhập.
            _loading = true;
            try
            {
                _name.Text = _row.Name ?? "";
            }
            finally
            {
                _loading = false;
            }
            _original = Snapshot();
            UpdateSaveButtons();
            UpdateNavButtons();
            return true;
        }

        private void CollectRow()
        {
            _row.Name = _name.Text;
            _row.ImageId = _image.SelectedId;
            _row.IconId = _icon.SelectedId;
            _row.ShowTab = _rTabBottom.Checked ? 1 : 0;
            _row.PriceLevelId = _priceLevel.SelectedId;
            _row.WarehouseId = _warehouse.SelectedId;
            _row.TemplateId = _template.SelectedId;
            _row.Note = _note.Text;
            _row.CalcHour = SelectedCalcValue();
            _row.UnitPrice = _unitPrice.Value;
            _row.OpenAmount = _openAmount.Value;
            _row.PriceListId = _priceList.SelectedId;
            _row.HourlyPriceId = _hourlyPrice.SelectedId;
            _row.FromHour = _fromHour.Checked ? (DateTime?)_fromHour.Value : null;
            _row.ToHour = _toHour.Checked ? (DateTime?)_toHour.Value : null;
        }

        private List<PriceAssignment> CollectPriceAssignments()
        {
            var list = new List<PriceAssignment>();
            foreach (DataGridViewRow r in _priceGrid.Rows)
            {
                if (r.Cells["colSel"].Value is bool && (bool)r.Cells["colSel"].Value && r.Tag is string)
                    list.Add(new PriceAssignment { PriceListId = (string)r.Tag });
            }
            return list;
        }

        private List<string> CollectAccessUserIds()
        {
            var list = new List<string>();
            foreach (DataGridViewRow r in _accessGrid.Rows)
            {
                if (r.Cells["colSel"].Value is bool && (bool)r.Cells["colSel"].Value && r.Tag is string)
                    list.Add((string)r.Tag);
            }
            return list;
        }

        private List<AreaDefaultItem> CollectDefaultItems()
        {
            var list = new List<AreaDefaultItem>();
            foreach (DataGridViewRow r in _defaultsGrid.Rows)
            {
                object p = r.Cells["colProduct"].Value;
                if (p == null || string.IsNullOrEmpty(Convert.ToString(p)))
                    continue;
                decimal qty;
                if (!NumberInput.TryParse(r.Cells["colQty"].Value, out qty) || qty <= 0m)
                    continue;
                list.Add(new AreaDefaultItem { ProductId = Convert.ToString(p), Quantity = qty });
            }
            return list;
        }

        private void DeleteCurrent()
        {
            if (_isNew || !_canRemove)
                return;
            if (!Msg.Confirm(this, ProductText.DeleteConfirm))
                return;
            string error;
            try
            {
                error = _repo.Delete(_row.Id);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (error != null)
            {
                Msg.Warning(this, error);
                return;
            }
            Deleted = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        // ------------------------------------------------------------------ Dòng mặt hàng mặc định

        private void AddDefaultLine()
        {
            if (_readOnly)
                return;
            _defaultsGrid.EndEdit();
            int i = _defaultsGrid.Rows.Add();
            _defaultsGrid.Rows[i].Cells["colQty"].Value = 1m;
            _defaultsGrid.ClearSelection();
            _defaultsGrid.Rows[i].Selected = true;
            _defaultsGrid.CurrentCell = _defaultsGrid.Rows[i].Cells["colProduct"];
            _defaultsGrid.BeginEdit(true);
            OnDirtyChanged();
        }

        private void DeleteDefaultLine()
        {
            if (_readOnly || _defaultsGrid.CurrentRow == null)
                return;
            _defaultsGrid.EndEdit();
            _defaultsGrid.Rows.Remove(_defaultsGrid.CurrentRow);
            OnDirtyChanged();
        }

        // ------------------------------------------------------------------ Tạo mới / chuyển dòng

        private void NewBlank()
        {
            if (_readOnly)
                return;
            if (_isNew)
                return;
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi tạo khu vực mới không?"))
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
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi chuyển sang khu vực khác không?"))
                return;
            string id = _rowIds[target];
            _rowIndex = target;
            LoadRecord(id);
            AfterRecordLoaded();
        }

        /// <summary>Làm mới trạng thái sau khi nạp xong một bản ghi (mới hoặc chuyển dòng).</summary>
        private void AfterRecordLoaded()
        {
            ApplyConfigAndPermissions();
            UpdateSaveButtons();
            UpdateNavButtons();
            _original = Snapshot();
        }

        /// <summary>Hỏi lưu khi có thay đổi chưa lưu (Yes/No/Cancel). Trả false nếu phải dừng thao tác đang làm.</summary>
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

        // ------------------------------------------------------------------ Phím tắt

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // F10 là phím hệ thống (kích hoạt menu) nên KeyDown có thể không nhận được — chặn ở đây cho chắc.
            if (keyData == Keys.F10)
            {
                Navigate(-1);
                return true;
            }
            if (keyData == Keys.F11)
            {
                Navigate(1);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.L)
            {
                SaveStay();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.S)
            {
                SaveAndNew();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.N)
            {
                NewBlank();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Alt && e.KeyCode == Keys.O)
            {
                SaveAndClose();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F4 && _tabs.SelectedTab == _tabDefaults)
            {
                AddDefaultLine();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F8 && _tabs.SelectedTab == _tabDefaults)
            {
                DeleteDefaultLine();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // Enter = Lưu & thoát, trừ khi focus đang ở nút, link, combo (chọn mục) hoặc lưới.
                Control active = ActiveControl;
                if (active is ButtonBase || active is LinkLabel || active is ComboBox
                    || active == _defaultsGrid || _defaultsGrid.ContainsFocus
                    || active == _priceGrid || active == _accessGrid)
                    return;
                SaveAndClose();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            _image.BackColor = t.Surface;
            _image.ForeColor = t.Text;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_picIcon.Image != null)
                {
                    _picIcon.Image.Dispose();
                    _picIcon.Image = null;
                }
                _lblName.Font.Dispose();
                _btnDelete.Font.Dispose();
                _shortcutsMenu.Dispose();
                _defaultsGrid.Font.Dispose();
                _defaultsGrid.ColumnHeadersDefaultCellStyle.Font.Dispose();
                _priceGrid.Font.Dispose();
                _priceGrid.ColumnHeadersDefaultCellStyle.Font.Dispose();
                _accessGrid.Font.Dispose();
                _accessGrid.ColumnHeadersDefaultCellStyle.Font.Dispose();
                _tabs.Font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
