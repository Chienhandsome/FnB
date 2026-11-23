using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
    /// Dialog Thêm/Sửa mặt hàng (spec 1.2/1.2a: nội dung 636×387, TabControl 3 tab, code tay theo layout.xml ở 96 DPI).
    /// Đã áp 5 quyết định của Chiến 21/11: bỏ ô "Giờ hát combo" + loại 7; bỏ radio "Khác" (chỉ 4 radio, loại 4/5/6/7
    /// của dữ liệu cũ giữ nguyên qua nhãn chỉ-xem); Excel/In mã vạch/In danh sách ra khỏi v1 ("chưa hỗ trợ");
    /// tiền decimal; gộp cảnh báo #1/#2 (dừng ở lỗi đầu). Ô tắt theo cấu hình HIỆN NHƯNG MỜ, bố cục không đổi (Q5);
    /// ô bắt buộc đánh dấu " *" theo quy ước của clone (DynamicForm), không nền vàng nhạt.
    /// Giá nhập/giá vốn + link lịch sử nhập bị ẨN khi không có quyền "Xem giá nhập" (mục 4/Q13).
    /// </summary>
    internal sealed class ProductEditDialog : ThemedDialog
    {
        private const int ContentWidth96 = 636;
        private const int ContentHeight96 = 387;
        private const string ShortcutsHint = "(Ctrl+S: Lưu&Mới, Ctrl+L: Lưu, Ctrl+N: Mới, Ctrl+P: In, ESC: Thoát)";
        private const string UnsupportedText = "Chức năng chưa hỗ trợ trong phiên bản này.";

        private readonly string _databasePath;
        private readonly ProductRepository _repo;
        private readonly ProductGroupRepository _groupRepo;
        private readonly FormRepository _formRepo;
        private readonly Random _random = new Random();

        private readonly List<string> _rowIds;
        private int _rowIndex;

        private List<ProductTypeInfo> _types = new List<ProductTypeInfo>();
        private List<LookupItem> _units = new List<LookupItem>();
        private List<ProductGroupRow> _groups = new List<ProductGroupRow>();
        private List<MaterialInfo> _materials = new List<MaterialInfo>();

        private ProductRow _row;
        private bool _isNew;
        private bool _readOnly;
        private bool _loadFailed;
        private bool _loading;
        private string _original = "";

        private readonly bool _canViewPrice;
        private readonly bool _canViewSellHistory;
        private readonly bool _canRemove;

        // ---- Tab 1: Thông tin mặt hàng ----
        private readonly TextBox _code = new TextBox();
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _english = new TextBox();
        private readonly NumericUpDown _sell = new NumericUpDown();
        private readonly NumericUpDown _sell2 = new NumericUpDown();
        private readonly NumericUpDown _sell3 = new NumericUpDown();
        private readonly NumericUpDown _sell4 = new NumericUpDown();
        private readonly LookupCombo _unit = new LookupCombo();
        private readonly LookupCombo _bigUnit = new LookupCombo();
        private readonly NumericUpDown _quyDoi = new NumericUpDown();
        private readonly NumericUpDown _buy = new NumericUpDown();
        private readonly NumericUpDown _cost = new NumericUpDown();
        private readonly LookupCombo _group = new LookupCombo();
        private readonly RadioButton _rBrew = new RadioButton { Text = "Mặt hàng pha chế" };
        private readonly RadioButton _rDual = new RadioButton { Text = "Mặt hàng kiêm vật tư" };
        private readonly RadioButton _rMaterial = new RadioButton { Text = "Vật tư nguyên liệu" };
        private readonly RadioButton _rOpen = new RadioButton { Text = "Mặt hàng mở" };
        private readonly Label _lblLegacyType = new Label { AutoSize = false, Visible = false };
        private readonly CheckBox _chkPriceByTime = new CheckBox { Text = "Giá theo thời giá" };
        private readonly PictureBox _pic = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
        private readonly FlatButton _btnPasteImage = new FlatButton { Text = "▣" };
        private readonly FlatButton _btnPickImage = new FlatButton { Text = "…" };
        private readonly FlatButton _btnClearImage = new FlatButton { Text = "✕" };
        private readonly LinkLabel _lnkBuy = new LinkLabel { Text = "Lịch sử nhập mua hàng", AutoSize = false };
        private readonly LinkLabel _lnkSell = new LinkLabel { Text = "Lịch sử xuất bán hàng", AutoSize = false };
        private readonly Label _lblCode = new Label { Text = "Mã hàng", AutoSize = false };
        private readonly Label _lblName = new Label { Text = "Tên hàng *", AutoSize = false };
        private readonly Label _lblEnglish = new Label { Text = "Tên tiếng anh", AutoSize = false };
        private readonly Label _lblSell = new Label { Text = "Giá bán", AutoSize = false };
        private readonly Label _lblSell2 = new Label { AutoSize = false };
        private readonly Label _lblSell3 = new Label { AutoSize = false };
        private readonly Label _lblSell4 = new Label { AutoSize = false };
        private readonly Label _lblUnit = new Label { Text = "ĐVT bán, định lượng *", AutoSize = false };
        private readonly Label _lblBigUnit = new Label { Text = "ĐVT nhập mua", AutoSize = false };
        private readonly Label _lblQuyDoi = new Label { Text = "Quy đổi", AutoSize = false };
        private readonly Label _lblQuyDoiDetail = new Label { AutoSize = false };
        private readonly Label _lblBuy = new Label { Text = "Giá nhập", AutoSize = false };
        private readonly Label _lblBuyUnit = new Label { AutoSize = false };
        private readonly Label _lblCost = new Label { Text = "Giá vốn", AutoSize = false };
        private readonly Label _lblGroup = new Label { Text = "Nhóm mặt hàng *", AutoSize = false };
        private readonly Label _lblType = new Label { Text = "Loại mặt hàng", AutoSize = false };
        private readonly Label _lblImage = new Label { Text = "Ảnh:", AutoSize = false };

        // ---- Tab 2: Định lượng ----
        private readonly DataGridView _recipeGrid = new DataGridView();
        private readonly FlatButton _btnAddLine = new FlatButton { Text = "Thêm dòng (F4)" };
        private readonly FlatButton _btnDelLine = new FlatButton { Text = "Xóa dòng (F8)" };
        private readonly FlatButton _btnExportRecipe = new FlatButton { Text = "Xuất excel" };
        private readonly TextBox _recipeFilter = new TextBox();
        private readonly Label _recipeTotals = new Label { AutoSize = false };
        private System.Data.DataTable _recipeTable;
        private System.Data.DataView _recipeView;

        // ---- Tab 3: Khác ----
        private readonly CheckBox _chkTempLocked = new CheckBox { Text = "Tạm khóa" };
        private readonly CheckBox _chkByWeight = new CheckBox { Text = "Khối lượng theo cân" };
        private readonly NumericUpDown _numMinStock = new NumericUpDown();
        private readonly Label _lblMinStock = new Label { Text = "Tồn tối thiểu", AutoSize = false };

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
        private readonly TabPage _tabInfo = new TabPage("Thông tin mặt hàng");
        private readonly TabPage _tabRecipe = new TabPage("Định lượng");
        private readonly TabPage _tabOther = new TabPage("Khác");

        /// <summary>ID mặt hàng đã lưu (null nếu chưa lưu lần nào trong phiên dialog).</summary>
        public string SavedId { get; private set; }

        /// <summary>Bản ghi đang sửa đã bị xóa (dialog đóng sau khi xóa).</summary>
        public bool Deleted { get; private set; }

        /// <param name="productId">null = thêm mới.</param>
        /// <param name="presetGroupId">Nhóm chọn sẵn cho mặt hàng mới (null = chưa chọn — người gọi mở hộp chọn nhóm trước).</param>
        /// <param name="rowIds">ID các dòng trên lưới theo thứ tự hiện tại (để F10/F11 chuyển dòng); null = không chuyển được.</param>
        public ProductEditDialog(UserSession session, string productId, string presetGroupId, List<string> rowIds)
            : base("MẶT HÀNG - THÊM MỚI", ShortcutsHint, 652, 555)
        {
            _databasePath = session.DatabasePath;
            _repo = new ProductRepository(_databasePath);
            _groupRepo = new ProductGroupRepository(_databasePath);
            _formRepo = new FormRepository(_databasePath);
            _rowIds = rowIds;
            _rowIndex = rowIds == null ? -1 : rowIds.IndexOf(productId ?? "");
            _isNew = string.IsNullOrEmpty(productId);

            _canViewPrice = AppSession.Can("Xem giá nhập", RoleModes.View);
            _canViewSellHistory = AppSession.Can("Hóa đơn bán hàng", RoleModes.View);
            _canRemove = AppSession.Can(ProductText.FunctionName, RoleModes.Remove);
            // Chỉ có Xem (không Sửa, không Thêm) → mở dialog ở chế độ chỉ đọc (mục 4, mặc định tạm Q13).
            _readOnly = _isNew
                ? !AppSession.Can(ProductText.FunctionName, RoleModes.Add)
                : !AppSession.Can(ProductText.FunctionName, RoleModes.Edit);

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = exit;

            BuildToolbar();
            BuildInfoTab();
            BuildRecipeTab();
            BuildOtherTab();
            _tabs.Font = new Font("Segoe UI", 10f);
            _tabs.TabPages.Add(_tabInfo);
            _tabs.TabPages.Add(_tabRecipe);
            _tabs.TabPages.Add(_tabOther);
            _tabs.Dock = DockStyle.Fill;
            Body.Controls.Add(_tabs);

            LoadLookups();
            if (_isNew)
                NewRecord(presetGroupId);
            else
                LoadRecord(productId);

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
                    Msg.Error(this, "Mặt hàng không còn tồn tại.");
                    Close();
                    return;
                }
                if (_code.Enabled)
                {
                    _code.Focus();
                    _code.SelectAll();
                }
                else
                {
                    _name.Focus();
                    _name.SelectAll();
                }
            };
            FormClosing += OnFormClosing;
        }

        /// <summary>
        /// Spec 1.2: vùng nội dung của tab (TabPage) = 636×387 ở 96 DPI. Đo vùng tab thật rồi nới/co dialog
        /// đúng bằng chênh lệch (phần tab header, viền, toolbar, header/footer dialog tự được tính).
        /// </summary>
        private void FitTabContent()
        {
            float s = Dpi;
            PerformLayout();
            _tabs.PerformLayout();
            // M1: 636×387 là kích thước vùng TabControl (tab page bên trong là 628×361 theo spec §1.2).
            Size target = new Size((int)(ContentWidth96 * s), (int)(ContentHeight96 * s));
            Size cur = _tabs.Size;
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

        private void Place(Control c, TabPage page, int x, int y, int w, int h, int tabIndex)
        {
            float s = Dpi;
            c.SetBounds((int)(x * s), (int)(y * s), (int)(w * s), (int)(h * s));
            c.TabIndex = tabIndex;
            page.Controls.Add(c);
        }

        private void PlaceLabel(Label l, TabPage page, int x, int y)
        {
            float s = Dpi;
            l.SetBounds((int)(x * s), (int)((y + 2) * s), (int)(105 * s), (int)(20 * s));
            l.AutoEllipsis = true;
            l.TabStop = false;
            page.Controls.Add(l);
        }

        private void BuildInfoTab()
        {
            // Vị trí/kích thước theo bảng 1.2 (96 DPI, tọa độ trong tab page 628×361).
            _lblName.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            PlaceLabel(_lblCode, _tabInfo, 7, 10);
            PlaceLabel(_lblName, _tabInfo, 7, 36);
            PlaceLabel(_lblEnglish, _tabInfo, 7, 61);
            PlaceLabel(_lblSell, _tabInfo, 7, 88);
            PlaceLabel(_lblSell2, _tabInfo, 272, 86);
            PlaceLabel(_lblSell3, _tabInfo, 7, 114);
            PlaceLabel(_lblSell4, _tabInfo, 272, 112);
            PlaceLabel(_lblUnit, _tabInfo, 7, 139);
            PlaceLabel(_lblBigUnit, _tabInfo, 7, 166);
            PlaceLabel(_lblQuyDoi, _tabInfo, 272, 167);
            PlaceLabel(_lblBuy, _tabInfo, 7, 192);
            PlaceLabel(_lblCost, _tabInfo, 272, 192);
            PlaceLabel(_lblGroup, _tabInfo, 7, 216);
            PlaceLabel(_lblType, _tabInfo, 7, 246);

            Place(_code, _tabInfo, 118, 6, 87, 20, 0);
            Place(_name, _tabInfo, 118, 32, 309, 20, 1);
            Place(_english, _tabInfo, 118, 58, 309, 20, 2);
            Place(_sell, _tabInfo, 118, 84, 86, 20, 3);
            Place(_sell2, _tabInfo, 349, 84, 78, 20, 4);
            Place(_sell3, _tabInfo, 118, 110, 86, 20, 5);
            Place(_sell4, _tabInfo, 349, 112, 78, 20, 6);
            Place(_unit, _tabInfo, 118, 136, 86, 21, 7);
            Place(_chkPriceByTime, _tabInfo, 349, 138, 103, 17, 8);
            Place(_bigUnit, _tabInfo, 118, 163, 86, 21, 9);
            Place(_quyDoi, _tabInfo, 349, 164, 38, 20, 10);
            Place(_buy, _tabInfo, 118, 190, 86, 20, 11);
            Place(_cost, _tabInfo, 349, 190, 78, 20, 12);
            Place(_group, _tabInfo, 118, 216, 309, 21, 13);
            Place(_rBrew, _tabInfo, 118, 244, 112, 17, 14);
            Place(_rDual, _tabInfo, 118, 267, 125, 17, 15);
            Place(_rMaterial, _tabInfo, 118, 290, 110, 17, 16);
            Place(_rOpen, _tabInfo, 118, 313, 87, 17, 17);
            Place(_lblLegacyType, _tabInfo, 118, 336, 300, 17, 18);
            Place(_lblImage, _tabInfo, 415, 10, 28, 16, 25);
            Place(_pic, _tabInfo, 447, 10, 175, 149, 19);
            Place(_btnPasteImage, _tabInfo, 419, 30, 24, 24, 20);
            Place(_btnPickImage, _tabInfo, 419, 60, 24, 24, 21);
            Place(_btnClearImage, _tabInfo, 419, 90, 24, 24, 22);
            Place(_lnkBuy, _tabInfo, 479, 174, 143, 19, 23);
            Place(_lnkSell, _tabInfo, 479, 196, 136, 19, 24);

            _sell.Minimum = -1000000000000m;
            _sell.Maximum = 1000000000000m;
            _sell.DecimalPlaces = 2;
            _sell.ThousandsSeparator = true;
            _sell2.Minimum = -1000000000000m;
            _sell2.Maximum = 1000000000000m;
            _sell2.DecimalPlaces = 2;
            _sell2.ThousandsSeparator = true;
            _sell3.Minimum = -1000000000000m;
            _sell3.Maximum = 1000000000000m;
            _sell3.DecimalPlaces = 2;
            _sell3.ThousandsSeparator = true;
            _sell4.Minimum = -1000000000000m;
            _sell4.Maximum = 1000000000000m;
            _sell4.DecimalPlaces = 2;
            _sell4.ThousandsSeparator = true;
            _buy.Minimum = -1000000000000m;
            _buy.Maximum = 1000000000000m;
            _buy.DecimalPlaces = 2;
            _buy.ThousandsSeparator = true;
            _cost.Minimum = -10000000000000000m;
            _cost.Maximum = 10000000000000000m;
            _cost.DecimalPlaces = 2;
            _cost.ThousandsSeparator = true;
            _cost.ReadOnly = true;
            _quyDoi.Minimum = 0m;
            _quyDoi.Maximum = 1000000000000m;
            _quyDoi.DecimalPlaces = 2;
            _quyDoi.ThousandsSeparator = true;

            _lblQuyDoiDetail.SetBounds((int)(393 * Dpi), (int)(167 * Dpi), (int)(220 * Dpi), (int)(20 * Dpi));
            _lblQuyDoiDetail.TabStop = false;
            _tabInfo.Controls.Add(_lblQuyDoiDetail);
            _lblBuyUnit.SetBounds((int)(210 * Dpi), (int)(192 * Dpi), (int)(130 * Dpi), (int)(20 * Dpi));
            _lblBuyUnit.TabStop = false;
            _tabInfo.Controls.Add(_lblBuyUnit);

            _btnPasteImage.Font = new Font("Segoe UI", 9f);
            _btnPickImage.Font = new Font("Segoe UI", 9f);
            _btnClearImage.Font = new Font("Segoe UI", 9f);
            _btnPasteImage.Click += delegate { PasteImage(); };
            _btnPickImage.Click += delegate { PickImage(); };
            _btnClearImage.Click += delegate { ClearImage(); };
            _lnkBuy.LinkClicked += delegate { OpenHistory(true); };
            _lnkSell.LinkClicked += delegate { OpenHistory(false); };

            foreach (Control c in new Control[] { _code, _name, _english })
                ((TextBox)c).TextChanged += delegate { OnDirtyChanged(); };
            foreach (NumericUpDown n in new[] { _sell, _sell2, _sell3, _sell4, _quyDoi, _buy, _cost })
                n.ValueChanged += delegate { OnDirtyChanged(); };
            _unit.SelectedIndexChanged += delegate { UpdateUnitLabels(); OnDirtyChanged(); };
            _bigUnit.SelectedIndexChanged += delegate { UpdateUnitLabels(); OnDirtyChanged(); };
            _group.SelectedIndexChanged += delegate { OnGroupChanged(); OnDirtyChanged(); };
            _chkPriceByTime.CheckedChanged += delegate { OnDirtyChanged(); };
            foreach (RadioButton r in new[] { _rBrew, _rDual, _rMaterial, _rOpen })
                r.CheckedChanged += delegate { if (r.Checked) { _lblLegacyType.Visible = false; OnDirtyChanged(); } };
        }

        private void BuildRecipeTab()
        {
            float s = Dpi;
            var bar = new Panel { Dock = DockStyle.Top, Height = (int)(30 * s) };
            _btnAddLine.Height = (int)(26 * s);
            _btnAddLine.Width = (int)(120 * s);
            _btnAddLine.SetBounds(0, (int)(2 * s), _btnAddLine.Width, _btnAddLine.Height);
            _btnDelLine.Height = (int)(26 * s);
            _btnDelLine.Width = (int)(110 * s);
            _btnDelLine.SetBounds(_btnAddLine.Right + (int)(6 * s), (int)(2 * s), _btnDelLine.Width, _btnDelLine.Height);
            _btnExportRecipe.Height = (int)(26 * s);
            _btnExportRecipe.Width = (int)(100 * s);
            _btnExportRecipe.SetBounds(_btnDelLine.Right + (int)(6 * s), (int)(2 * s), _btnExportRecipe.Width, _btnExportRecipe.Height);
            _recipeFilter.Width = (int)(160 * s);
            _recipeFilter.Height = (int)(24 * s);
            _recipeFilter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _recipeFilter.SetBounds(0, (int)(3 * s), _recipeFilter.Width, _recipeFilter.Height);
            _recipeFilter.TextChanged += delegate { ApplyRecipeFilter(); };
            bar.Controls.Add(_btnAddLine);
            bar.Controls.Add(_btnDelLine);
            bar.Controls.Add(_btnExportRecipe);
            bar.Controls.Add(_recipeFilter);
            bar.Resize += delegate
            {
                _recipeFilter.Left = bar.ClientSize.Width - _recipeFilter.Width - (int)(4 * s);
            };
            _btnAddLine.Click += delegate { AddRecipeLine(); };
            _btnDelLine.Click += delegate { DeleteRecipeLine(); };
            _btnExportRecipe.Click += delegate { Msg.Info(this, UnsupportedText); };

            _recipeGrid.Dock = DockStyle.Fill;
            _recipeGrid.AllowUserToAddRows = false;
            _recipeGrid.AllowUserToDeleteRows = false;
            _recipeGrid.AllowUserToResizeRows = false;
            _recipeGrid.AutoGenerateColumns = false;
            _recipeGrid.MultiSelect = false;
            _recipeGrid.RowHeadersVisible = false;
            _recipeGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _recipeGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _recipeGrid.ColumnHeadersHeight = (int)(34 * s);
            _recipeGrid.RowTemplate.Height = (int)(28 * s);
            _recipeGrid.Font = new Font("Segoe UI", 10f);
            _recipeGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _recipeGrid.CellValueChanged += RecipeGrid_CellValueChanged;
            _recipeGrid.CurrentCellDirtyStateChanged += delegate
            {
                if (_recipeGrid.IsCurrentCellDirty)
                    _recipeGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _recipeGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            _recipeGrid.CellParsing += RecipeGrid_CellParsing;
            _recipeGrid.RowsRemoved += delegate { UpdateRecipeTotals(); OnDirtyChanged(); };
            _recipeGrid.SelectionChanged += delegate { UpdateRecipeButtons(); };

            _recipeTotals.Dock = DockStyle.Bottom;
            _recipeTotals.Height = (int)(22 * s);
            _recipeTotals.TextAlign = ContentAlignment.MiddleLeft;
            _recipeTotals.Font = new Font("Segoe UI Semibold", 10f);

            _tabRecipe.Controls.Add(_recipeGrid);
            _tabRecipe.Controls.Add(_recipeTotals);
            _tabRecipe.Controls.Add(bar);
            BuildRecipeColumns();
        }

        private void BuildOtherTab()
        {
            Place(_chkTempLocked, _tabOther, 6, 6, 74, 17, 0);
            Place(_chkByWeight, _tabOther, 6, 29, 121, 17, 1);
            Place(_lblMinStock, _tabOther, 6, 60, 65, 20, 2);
            Place(_numMinStock, _tabOther, 77, 57, 95, 20, 3);
            _numMinStock.Minimum = -10000000000000000m;
            _numMinStock.Maximum = 10000000000000000m;
            _numMinStock.DecimalPlaces = 2;
            _numMinStock.ThousandsSeparator = true;
            _chkTempLocked.CheckedChanged += delegate { OnDirtyChanged(); };
            _chkByWeight.CheckedChanged += delegate { OnDirtyChanged(); };
            _numMinStock.ValueChanged += delegate { OnDirtyChanged(); };
        }

        // ------------------------------------------------------------------ Nạp dữ liệu

        private void LoadLookups()
        {
            _types = _repo.LoadTypes();
            _units = _formRepo.LoadAll("DDONVITINH");
            _groups = _groupRepo.LoadAll();
            _unit.SetItems(_units);
            _bigUnit.SetItems(_units);
            var groupItems = new List<LookupItem>();
            foreach (ProductGroupRow g in _groups)
                groupItems.Add(new LookupItem { Id = g.Id, Name = g.Name });
            _group.SetItems(groupItems);
        }

        private void NewRecord(string presetGroupId)
        {
            _row = ProductRow.NewDefault();
            _isNew = true;
            _materials = _repo.LoadMaterials(null);
            _row.GroupId = presetGroupId;
            if (_units.Count > 0)
                _row.UnitId = _units[0].Id;
            SetTitle("MẶT HÀNG - THÊM MỚI");
            FillControls(new List<RecipeLine>());
            if (SystemConfig.TuDongSinhMaVach)
                RegenerateCode();
        }

        private void LoadRecord(string productId)
        {
            _row = _repo.LoadProduct(productId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = ProductRow.NewDefault();
                return;
            }
            _isNew = false;
            _materials = _repo.LoadMaterials(_row.Id);
            SetTitle("MẶT HÀNG - SỬA");
            FillControls(_repo.LoadRecipeLines(_row.Id));
        }

        private void FillControls(List<RecipeLine> lines)
        {
            _loading = true;
            try
            {
                _code.Text = _row.Code ?? "";
                _name.Text = _row.Name ?? "";
                _english.Text = _row.EnglishName ?? "";
                _sell.Value = Clamp(_sell, _row.SellPrice);
                _sell2.Value = Clamp(_sell2, _row.SellPrice2);
                _sell3.Value = Clamp(_sell3, _row.SellPrice3);
                _sell4.Value = Clamp(_sell4, _row.SellPrice4);
                _buy.Value = Clamp(_buy, _row.BuyPrice);
                _cost.Value = Clamp(_cost, _row.CostPrice);
                _quyDoi.Value = Clamp(_quyDoi, _row.QuyDoi ?? 0m);
                _unit.SelectId(_row.UnitId);
                _bigUnit.SelectId(_row.BigUnitId);
                _group.SelectId(_row.GroupId);
                _chkPriceByTime.Checked = _row.PriceByTime;
                _chkTempLocked.Checked = _row.TempLocked;
                _chkByWeight.Checked = _row.ByWeight;
                _numMinStock.Value = Clamp(_numMinStock, _row.MinStock);
                SetTypeRadios(_row.EffectiveTypeId);
                ShowImage(_row.Image);
                BuildRecipeTable(lines);
                UpdateUnitLabels();
                UpdateRecipeTotals();
            }
            finally
            {
                _loading = false;
            }
            UpdateRecipeButtons();
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
        /// Chọn radio theo loại. Loại 4/5/6/7 (dữ liệu cũ, không tạo mới được qua dialog) thì không tick radio nào
        /// mà hiện nhãn chỉ-xem; khi lưu sẽ giữ nguyên loại cũ trừ khi người dùng tick một radio khác.
        /// </summary>
        private void SetTypeRadios(string typeId)
        {
            _rBrew.Checked = typeId == ProductRules.TypeBrew;
            _rDual.Checked = typeId == ProductRules.TypeDual;
            _rMaterial.Checked = typeId == ProductRules.TypeMaterial;
            _rOpen.Checked = typeId == ProductRules.TypeOpen;
            if (ProductRules.CanCreateType(typeId))
            {
                _lblLegacyType.Visible = false;
            }
            else
            {
                ProductTypeInfo info = FindType(typeId);
                string name = info == null ? ("loại " + typeId) : info.Name;
                _lblLegacyType.Text = "Loại: " + name + " (không tạo mới được qua dialog — giữ nguyên khi lưu)";
                _lblLegacyType.Visible = true;
            }
        }

        private ProductTypeInfo FindType(string typeId)
        {
            foreach (ProductTypeInfo t in _types)
                if (string.Equals(t.Id, typeId, StringComparison.Ordinal))
                    return t;
            return null;
        }

        private string SelectedTypeId()
        {
            if (_rBrew.Checked)
                return ProductRules.TypeBrew;
            if (_rDual.Checked)
                return ProductRules.TypeDual;
            if (_rMaterial.Checked)
                return ProductRules.TypeMaterial;
            if (_rOpen.Checked)
                return ProductRules.TypeOpen;
            // Không tick radio nào = mặt hàng cũ loại 4/5/6/7: giữ nguyên loại đã nạp.
            return _row.TypeId ?? "";
        }

        private bool CurrentTypeHasRecipe()
        {
            ProductTypeInfo info = FindType(SelectedTypeId());
            return info != null && info.HasRecipe;
        }

        // ------------------------------------------------------------------ Cấu hình + quyền (Q5/Q13/mục 4)

        private void ApplyConfigAndPermissions()
        {
            bool ro = _readOnly;
            _lblSell2.Text = SystemConfig.DienGiaiGia2;
            _lblSell3.Text = SystemConfig.DienGiaiGia3;
            _lblSell4.Text = SystemConfig.DienGiaiGia4;

            // Ô tắt theo cấu hình: HIỆN NHƯNG MỜ, bố cục không đổi (Q5).
            _code.Enabled = SystemConfig.SuDungMaHang && !ro;
            _english.Enabled = SystemConfig.SuDungTenTiengAnh && !ro;
            _sell2.Enabled = SystemConfig.SuDungGia2 && !ro;
            _sell3.Enabled = SystemConfig.SuDungGia3 && !ro;
            _sell4.Enabled = SystemConfig.SuDungGia4 && !ro;
            _bigUnit.Enabled = SystemConfig.SuDung2DonViTinh && !ro;
            _chkByWeight.Enabled = SystemConfig.KichHoatSuDungCanDienTu && !ro;

            // Giá vốn luôn mờ (chỉ đọc, do định lượng tính).
            _cost.Enabled = false;
            _cost.TabStop = false;

            // Không có quyền "Xem giá nhập": ẨN giá nhập/giá vốn + nhãn đơn vị + link lịch sử nhập (mục 4/Q13).
            bool showPrice = _canViewPrice;
            _lblBuy.Visible = showPrice;
            _buy.Visible = showPrice;
            _lblBuyUnit.Visible = showPrice;
            _lblCost.Visible = showPrice;
            _cost.Visible = showPrice;
            _lnkBuy.Visible = showPrice && !_isNew;

            _lnkSell.Visible = _canViewSellHistory && !_isNew;

            if (ro)
            {
                foreach (Control c in new Control[] { _name, _sell, _sell2, _sell3, _sell4, _unit, _bigUnit, _quyDoi, _buy, _group })
                    c.Enabled = false;
                _chkPriceByTime.Enabled = false;
                _chkTempLocked.Enabled = false;
                _chkByWeight.Enabled = false;
                _numMinStock.Enabled = false;
                foreach (RadioButton r in new[] { _rBrew, _rDual, _rMaterial, _rOpen })
                    r.Enabled = false;
                _btnPasteImage.Enabled = false;
                _btnPickImage.Enabled = false;
                _btnClearImage.Enabled = false;
                _recipeGrid.ReadOnly = true;
                _btnAddLine.Enabled = false;
                _btnDelLine.Enabled = false;
                SubtitleText = ShortcutsHint + " (chỉ xem — không có quyền sửa)";
            }
            UpdateUnitLabels();
            BuildRecipeColumns();
        }

        private void UpdateUnitLabels()
        {
            string unitName = _unit.Selected == null ? "" : _unit.Selected.Name;
            string bigName = _bigUnit.Selected == null ? "" : _bigUnit.Selected.Name;
            bool both = !string.IsNullOrEmpty(unitName) && !string.IsNullOrEmpty(bigName);
            // Nhãn "(1 {chẵn} = {quy đổi} {lẻ})", chỉ hiện khi cả hai ĐVT được chọn.
            _lblQuyDoiDetail.Visible = both;
            if (both)
            {
                _lblQuyDoiDetail.Text = string.Format("(1 {0} = {1} {2})", bigName,
                    _quyDoi.Value.ToString("#,##0.##", new CultureInfo("vi-VN")), unitName);
            }
            // Nhãn "(/1 {ĐVT chẵn nếu có, không thì ĐVT lẻ})".
            _lblBuyUnit.Text = string.Format("(/1 {0})", bigName.Length > 0 ? bigName : unitName);
            // Quy đổi chỉ bật khi cả hai ĐVT được chọn (và cấu hình 2 ĐVT đang bật).
            _quyDoi.Enabled = SystemConfig.SuDung2DonViTinh && both && !_readOnly;
        }

        private void OnGroupChanged()
        {
            // Đổi nhóm lúc thêm mới → sinh lại mã (ghi đè mã đã gõ), theo 3.5.
            if (_isNew && SystemConfig.TuDongSinhMaVach)
                RegenerateCode();
        }

        /// <summary>Sinh lại mã theo nhóm đang chọn (tiền tố + số ngẫu nhiên, không trùng).</summary>
        private void RegenerateCode()
        {
            ProductGroupRow g = FindGroup(_group.SelectedId);
            if (g == null || string.IsNullOrEmpty(g.Code))
            {
                _code.Text = "";
                return;
            }
            string code = ProductRules.GenerateUniqueCode(g.Code, SystemConfig.ChieuDaiMaVach,
                delegate(string c) { return _repo.IsCodeTaken(c, _row == null ? null : _row.Id); }, _random);
            if (code == null)
            {
                Msg.Warning(this, "Không sinh được mã hàng không trùng, mời bạn nhập tay.");
                return;
            }
            _code.Text = code;
        }

        private ProductGroupRow FindGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return null;
            foreach (ProductGroupRow g in _groups)
                if (string.Equals(g.Id, groupId, StringComparison.Ordinal))
                    return g;
            return null;
        }

        // ------------------------------------------------------------------ Định lượng

        private void BuildRecipeColumns()
        {
            float s = Dpi;
            var vi = new CultureInfo("vi-VN");
            _recipeGrid.Columns.Clear();
            var colMat = new DataGridViewComboBoxColumn
            {
                Name = "colMat",
                HeaderText = "Vật tư",
                DataPropertyName = "MatId",
                DataSource = _materials,
                DisplayMember = "Name",
                ValueMember = "Id",
                Width = (int)(150 * s),
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
            };
            var colQty = new DataGridViewTextBoxColumn
            {
                Name = "colQty",
                HeaderText = "Số lượng",
                DataPropertyName = "Qty",
                Width = (int)(65 * s),
                DefaultCellStyle = { Format = "N4", FormatProvider = vi, Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            var colUnit = new DataGridViewTextBoxColumn
            {
                Name = "colUnit",
                HeaderText = "ĐVT",
                DataPropertyName = "UnitName",
                Width = (int)(50 * s),
                ReadOnly = true
            };
            var colBuy = new DataGridViewTextBoxColumn
            {
                Name = "colBuy",
                HeaderText = "Giá nhập",
                DataPropertyName = "BuyPrice",
                Width = (int)(75 * s),
                ReadOnly = true,
                Visible = _canViewPrice,
                DefaultCellStyle = { Format = "#,##0.##", FormatProvider = vi, Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            var colAmtBuy = new DataGridViewTextBoxColumn
            {
                Name = "colAmtBuy",
                HeaderText = "Thành tiền nhập",
                DataPropertyName = "AmtBuy",
                Width = (int)(85 * s),
                ReadOnly = true,
                Visible = _canViewPrice,
                DefaultCellStyle = { Format = "#,##0.##", FormatProvider = vi, Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            var colCost = new DataGridViewTextBoxColumn
            {
                Name = "colCost",
                HeaderText = "Giá vốn",
                DataPropertyName = "CostPrice",
                Width = (int)(75 * s),
                ReadOnly = true,
                Visible = _canViewPrice,
                DefaultCellStyle = { Format = "#,##0.##", FormatProvider = vi, Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            var colAmtCost = new DataGridViewTextBoxColumn
            {
                Name = "colAmtCost",
                HeaderText = "Thành tiền vốn",
                DataPropertyName = "AmtCost",
                Width = (int)(85 * s),
                ReadOnly = true,
                Visible = _canViewPrice,
                DefaultCellStyle = { Format = "#,##0.##", FormatProvider = vi, Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            _recipeGrid.Columns.AddRange(new DataGridViewColumn[] { colMat, colQty, colUnit, colBuy, colAmtBuy, colCost, colAmtCost });
            if (_recipeGrid.Columns.Count > 0)
                _recipeGrid.Columns[_recipeGrid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void BuildRecipeTable(List<RecipeLine> lines)
        {
            var dt = new System.Data.DataTable();
            dt.Columns.Add("MatId", typeof(string));
            dt.Columns.Add("MatName", typeof(string));
            dt.Columns.Add("MatFold", typeof(string));
            dt.Columns.Add("Qty", typeof(decimal));
            dt.Columns.Add("UnitName", typeof(string));
            dt.Columns.Add("BuyPrice", typeof(decimal));
            dt.Columns.Add("AmtBuy", typeof(decimal));
            dt.Columns.Add("CostPrice", typeof(decimal));
            dt.Columns.Add("AmtCost", typeof(decimal));
            dt.Columns.Add("MatQuyDoi", typeof(decimal));
            dt.Columns.Add("MatHasBigUnit", typeof(bool));
            dt.Columns.Add("MatTypeId", typeof(string));
            dt.Columns.Add("Missing", typeof(bool));
            dt.Columns.Add("OrigMatId", typeof(string));
            dt.BeginLoadData();
            foreach (RecipeLine line in lines)
            {
                var values = new object[dt.Columns.Count];
                values[0] = line.MaterialMissing ? (object)System.DBNull.Value : (object)line.MaterialId;
                values[1] = line.MaterialName ?? "";
                values[2] = CatalogRepository.Fold(line.MaterialName ?? "");
                values[3] = line.Quantity;
                values[4] = line.MaterialUnitName ?? "";
                values[5] = line.BuyPrice;
                values[6] = line.AmountBuy;
                values[7] = line.CostPrice;
                values[8] = line.AmountCost;
                values[9] = line.MaterialQuyDoi ?? (object)System.DBNull.Value;
                values[10] = line.MaterialHasBigUnit;
                values[11] = line.MaterialTypeId ?? "";
                values[12] = line.MaterialMissing;
                values[13] = line.MaterialId ?? "";
                dt.Rows.Add(values);
            }
            dt.EndLoadData();
            _recipeTable = dt;
            _recipeView = new System.Data.DataView(dt);
            _recipeGrid.DataSource = _recipeView;
            MarkOrphanRows();
            ApplyRecipeFilter();
        }

        /// <summary>Dòng mồ côi (vật tư không còn trong DB): hiện ô vật tư trống + dấu lỗi đỏ ở đầu dòng, không lỗi chương trình (3.6).</summary>
        private void MarkOrphanRows()
        {
            foreach (DataGridViewRow row in _recipeGrid.Rows)
            {
                var dataRow = row.DataBoundItem as System.Data.DataRowView;
                if (dataRow == null)
                    continue;
                bool missing = dataRow["Missing"] is bool && (bool)dataRow["Missing"];
                row.ErrorText = missing ? "Vật tư không còn tồn tại — chọn lại hoặc xóa dòng." : "";
            }
        }

        private void ApplyRecipeFilter()
        {
            if (_recipeView == null)
                return;
            string term = CatalogRepository.Fold(_recipeFilter.Text.Trim());
            _recipeView.RowFilter = term.Length == 0
                ? ""
                : "[MatFold] LIKE '%" + CatalogRepository.EscapeLike(term) + "%'";
            MarkOrphanRows();
        }

        private MaterialInfo FindMaterial(string materialId)
        {
            if (string.IsNullOrEmpty(materialId))
                return null;
            foreach (MaterialInfo m in _materials)
                if (string.Equals(m.Id, materialId, StringComparison.Ordinal))
                    return m;
            return null;
        }

        private void RecipeGrid_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (_recipeGrid.Columns[e.ColumnIndex].DataPropertyName != "Qty")
                return;
            string text = e.Value == null ? "" : e.Value.ToString().Trim();
            decimal v;
            var vi = new CultureInfo("vi-VN");
            if (decimal.TryParse(text, NumberStyles.Number, vi, out v) ||
                decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out v))
            {
                e.Value = v;
                e.ParsingApplied = true;
            }
        }

        private void RecipeGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_loading || e.RowIndex < 0 || _recipeView == null)
                return;
            if (e.RowIndex >= _recipeView.Count)
                return;
            System.Data.DataRowView view = _recipeView[e.RowIndex];
            string prop = _recipeGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (prop != "MatId" && prop != "Qty")
                return;
            RecalcRecipeRow(view);
            // Thêm/sửa dòng định lượng mà loại hiện tại có CODINHLUONG = 0 → tự chuyển sang pha chế (3.2).
            string switched = ProductRules.SwitchTypeForRecipe(SelectedTypeId(), CurrentTypeHasRecipe());
            if (switched != SelectedTypeId())
                SetTypeRadios(switched);
            UpdateRecipeTotals();
            MarkOrphanRows();
            OnDirtyChanged();
        }

        private void RecalcRecipeRow(System.Data.DataRowView view)
        {
            string matId = view["MatId"] is System.DBNull ? null : (string)view["MatId"];
            MaterialInfo m = FindMaterial(matId);
            decimal qty = view["Qty"] is System.DBNull ? 0m : (decimal)view["Qty"];
            if (m == null)
            {
                view["UnitName"] = "";
                view["BuyPrice"] = 0m;
                view["AmtBuy"] = 0m;
                view["CostPrice"] = 0m;
                view["AmtCost"] = 0m;
                view["MatTypeId"] = "";
                view["Missing"] = !string.IsNullOrEmpty(matId);
                return;
            }
            decimal amountBuy, amountCost;
            ProductRules.RecipeAmounts(qty, m.BuyPrice, m.QuyDoi, m.HasBigUnit, m.CostPrice, out amountBuy, out amountCost);
            view["MatName"] = m.Name ?? "";
            view["MatFold"] = CatalogRepository.Fold(m.Name ?? "");
            view["UnitName"] = m.UnitName ?? "";
            view["BuyPrice"] = ProductMath.UnitBuyPrice(m.BuyPrice, m.QuyDoi, m.HasBigUnit);
            view["AmtBuy"] = amountBuy;
            view["CostPrice"] = m.CostPrice;
            view["AmtCost"] = amountCost;
            view["MatQuyDoi"] = m.QuyDoi ?? (object)System.DBNull.Value;
            view["MatHasBigUnit"] = m.HasBigUnit;
            view["MatTypeId"] = m.TypeId ?? "";
            view["Missing"] = false;
            view["OrigMatId"] = m.Id;
        }

        private void AddRecipeLine()
        {
            if (_readOnly)
                return;
            _tabs.SelectedTab = _tabRecipe;
            // Xóa lọc trước khi thêm để dòng mới hiện ra và được chọn đúng.
            _recipeFilter.Text = "";
            System.Data.DataRow row = _recipeTable.NewRow();
            row["MatId"] = System.DBNull.Value;
            row["MatName"] = "";
            row["MatFold"] = "";
            row["Qty"] = 1m;
            row["UnitName"] = "";
            row["BuyPrice"] = 0m;
            row["AmtBuy"] = 0m;
            row["CostPrice"] = 0m;
            row["AmtCost"] = 0m;
            row["MatQuyDoi"] = System.DBNull.Value;
            row["MatHasBigUnit"] = false;
            row["MatTypeId"] = "";
            row["Missing"] = false;
            row["OrigMatId"] = "";
            _recipeTable.Rows.Add(row);
            int index = _recipeView.Count - 1;
            if (index >= 0)
            {
                _recipeGrid.ClearSelection();
                _recipeGrid.Rows[index].Selected = true;
                _recipeGrid.CurrentCell = _recipeGrid.Rows[index].Cells["colMat"];
                _recipeGrid.BeginEdit(true);
            }
            // Thêm dòng cho loại CODINHLUONG = 0 → tự chuyển sang pha chế (3.2).
            string switched = ProductRules.SwitchTypeForRecipe(SelectedTypeId(), CurrentTypeHasRecipe());
            if (switched != SelectedTypeId())
                SetTypeRadios(switched);
            UpdateRecipeTotals();
            OnDirtyChanged();
        }

        private void DeleteRecipeLine()
        {
            if (_readOnly || _recipeGrid.CurrentRow == null)
                return;
            var view = _recipeGrid.CurrentRow.DataBoundItem as System.Data.DataRowView;
            if (view == null)
                return;
            view.Row.Delete();
            UpdateRecipeTotals();
            OnDirtyChanged();
        }

        private void UpdateRecipeButtons()
        {
            bool hasRow = _recipeGrid.CurrentRow != null;
            _btnDelLine.Enabled = !_readOnly && hasRow;
            _btnAddLine.Enabled = !_readOnly;
            _btnExportRecipe.Enabled = true;
        }

        /// <summary>
        /// Tính lại tổng định lượng + GIANHAP/GIAVON của mặt hàng (3.4, Q15):
        /// ghi đè theo tổng khi cấu hình tự cập nhật = 30; không thì giữ giá đã nhập tay.
        /// </summary>
        private void UpdateRecipeTotals(bool applyPrices = true)
        {
            if (_recipeTable == null)
                return;
            var lines = new List<RecipeLine>();
            bool hasMaterial = false; // M4: chỉ ghi đè giá khi có ít nhất một dòng đã chọn vật tư.
            foreach (System.Data.DataRow r in _recipeTable.Rows)
            {
                if (r.RowState == System.Data.DataRowState.Deleted)
                    continue;
                if (!(r["MatId"] is System.DBNull) && !string.IsNullOrEmpty((string)r["MatId"]))
                    hasMaterial = true;
                lines.Add(new RecipeLine
                {
                    AmountBuy = r["AmtBuy"] is System.DBNull ? 0m : (decimal)r["AmtBuy"],
                    AmountCost = r["AmtCost"] is System.DBNull ? 0m : (decimal)r["AmtCost"]
                });
            }
            decimal totalBuy, totalCost;
            ProductRules.Totals(lines, out totalBuy, out totalCost);
            var vi = new CultureInfo("vi-VN");
            // M3 (§4): thiếu quyền "Xem giá nhập" thì không hiện tổng tiền nhập/vốn.
            _recipeTotals.Visible = _canViewPrice;
            _recipeTotals.Text = string.Format("TỔNG:  Thành tiền nhập = {0}   ·   Thành tiền vốn = {1}",
                totalBuy.ToString("#,##0.##", vi), totalCost.ToString("#,##0.##", vi));
            // Khi đang nạp (mở dialog) chỉ hiện tổng, KHÔNG ghi đè giá đã lưu; ghi đè chỉ xảy ra khi
            // người dùng đổi định lượng hoặc khi lưu (Q15), có dòng vật tư thật, và có quyền xem giá nhập.
            if (!_loading && applyPrices && hasMaterial && _canViewPrice)
            {
                if (SystemConfig.TuDongCapNhatGiaNhapDinhLuong)
                    _buy.Value = Clamp(_buy, totalBuy);
                if (SystemConfig.TuDongCapNhatGiaVonDinhLuong)
                    _cost.Value = Clamp(_cost, totalCost);
            }
        }

        private List<RecipeLine> CollectLines()
        {
            var lines = new List<RecipeLine>();
            if (_recipeTable == null)
                return lines;
            foreach (System.Data.DataRow r in _recipeTable.Rows)
            {
                if (r.RowState == System.Data.DataRowState.Deleted)
                    continue;
                string matId = r["MatId"] is System.DBNull ? null : (string)r["MatId"];
                lines.Add(new RecipeLine
                {
                    MaterialId = matId,
                    MaterialTypeId = r["MatTypeId"] is System.DBNull ? null : (string)r["MatTypeId"],
                    Quantity = r["Qty"] is System.DBNull ? 0m : (decimal)r["Qty"],
                    MaterialMissing = r["Missing"] is bool && (bool)r["Missing"]
                });
            }
            return lines;
        }

        // ------------------------------------------------------------------ Thu thập + bẩn

        private void CollectRow()
        {
            _row.Code = _code.Text.Trim();
            _row.Name = _name.Text;
            _row.EnglishName = _english.Text.Trim();
            _row.SellPrice = _sell.Value;
            _row.SellPrice2 = _sell2.Value;
            _row.SellPrice3 = _sell3.Value;
            _row.SellPrice4 = _sell4.Value;
            _row.UnitId = _unit.SelectedId;
            string bigId = _bigUnit.SelectedId;
            _row.BigUnitId = string.IsNullOrEmpty(bigId) ? null : bigId;
            // Không dùng ĐVT chẵn thì QUYDOI = NULL (3.1b).
            _row.QuyDoi = string.IsNullOrEmpty(bigId) ? (decimal?)null : _quyDoi.Value;
            _row.BuyPrice = _buy.Value;
            _row.CostPrice = _cost.Value;
            _row.GroupId = _group.SelectedId;
            _row.TypeId = SelectedTypeId();
            _row.PriceByTime = _chkPriceByTime.Checked;
            _row.TempLocked = _chkTempLocked.Checked;
            _row.ByWeight = _chkByWeight.Checked;
            _row.MinStock = _numMinStock.Value;
            // Ảnh nằm sẵn trong _row.Image (Pick/Paste/Clear cập nhật trực tiếp).
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
            sb.Append(_code.Text).Append('|');
            sb.Append(_name.Text).Append('|');
            sb.Append(_english.Text).Append('|');
            sb.Append(_sell.Value).Append('|');
            sb.Append(_sell2.Value).Append('|');
            sb.Append(_sell3.Value).Append('|');
            sb.Append(_sell4.Value).Append('|');
            sb.Append(_unit.SelectedId).Append('|');
            sb.Append(_bigUnit.SelectedId).Append('|');
            sb.Append(_quyDoi.Value).Append('|');
            sb.Append(_buy.Value).Append('|');
            sb.Append(_cost.Value).Append('|');
            sb.Append(_group.SelectedId).Append('|');
            sb.Append(SelectedTypeId()).Append('|');
            sb.Append(_chkPriceByTime.Checked).Append('|');
            sb.Append(_chkTempLocked.Checked).Append('|');
            sb.Append(_chkByWeight.Checked).Append('|');
            sb.Append(_numMinStock.Value).Append('|');
            sb.Append(ImageSignature(_row == null ? null : _row.Image)).Append('|');
            if (_recipeTable != null)
            {
                foreach (System.Data.DataRow r in _recipeTable.Rows)
                {
                    if (r.RowState == System.Data.DataRowState.Deleted)
                        continue;
                    sb.Append(r["MatId"]).Append(':').Append(r["Qty"]).Append(';');
                }
            }
            return sb.ToString();
        }

        private static string ImageSignature(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "";
            int head = 0;
            for (int i = 0; i < Math.Min(16, data.Length); i++)
                head = head * 31 + data[i];
            return data.Length + ":" + head;
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
            string groupId = _row.GroupId;
            NewRecord(groupId);
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
            // Đẩy ô lưới đang sửa dở vào DataTable trước khi tính tổng/thu thập
            // (bấm Ctrl+L khi còn trong ô Số lượng/Vật tư: giá trị chưa commit thì CollectLines đọc thiếu).
            _recipeGrid.EndEdit();
            if (_recipeTable != null)
            {
                System.Windows.Forms.BindingManagerBase recipeBinding = BindingContext[_recipeTable];
                if (recipeBinding != null)
                    recipeBinding.EndCurrentEdit();
            }
            // Chỉ cập nhật nhãn tổng ở bước này; ghi đè GIANHAP/GIAVON (Q15) làm SAU khi kiểm tra hợp lệ (M4).
            UpdateRecipeTotals(false);
            CollectRow();
            List<RecipeLine> lines = CollectLines();

            // Kiểm tra bắt buộc của framework (2.2).
            if (string.IsNullOrWhiteSpace(_row.Name))
            {
                Msg.Warning(this, ProductText.NameEmpty);
                _tabs.SelectedTab = _tabInfo;
                _name.Focus();
                _name.SelectAll();
                return false;
            }
            if (string.IsNullOrEmpty(_row.UnitId))
            {
                Msg.Warning(this, "Trường \"ĐVT bán\" không được trống!");
                _tabs.SelectedTab = _tabInfo;
                _unit.Focus();
                return false;
            }
            if (string.IsNullOrEmpty(_row.GroupId))
            {
                Msg.Warning(this, "Trường \"Nhóm mặt hàng\" không được trống!");
                _tabs.SelectedTab = _tabInfo;
                _group.Focus();
                return false;
            }

            var input = new ProductValidationInput
            {
                TypeId = _row.EffectiveTypeId,
                TypeHasRecipe = CurrentTypeHasRecipe(),
                UseTwoUnits = SystemConfig.SuDung2DonViTinh,
                QuyDoi = _row.QuyDoi ?? 0m,
                UnitId = _row.UnitId,
                BigUnitId = _row.BigUnitId,
                Lines = ToValidationLines(lines)
            };
            ProductValidationResult vr = ProductRules.ValidateBeforeSave(input);
            if (!vr.IsValid)
            {
                Msg.Warning(this, vr.Error);
                if (vr.FocusQuyDoi)
                {
                    _tabs.SelectedTab = _tabInfo;
                    _quyDoi.Focus();
                    _quyDoi.Select(0, _quyDoi.Text.Length);
                }
                else if (vr.ErrorLineIndex >= 0)
                {
                    _tabs.SelectedTab = _tabRecipe;
                    SelectRecipeRow(vr.ErrorLineIndex);
                }
                return false;
            }
            if (vr.Warning != null)
                Msg.Warning(this, vr.Warning);

            // Q15: đã hợp lệ → mới ghi đè giá theo tổng định lượng rồi thu thập lại.
            UpdateRecipeTotals();
            CollectRow();

            var options = new ProductSaveOptions
            {
                AllowDuplicateName = SystemConfig.ChoPhepTrungTenMatHang,
                CodeRequired = SystemConfig.SuDungMaHang,
                AutoUpdateBuyPrice = SystemConfig.TuDongCapNhatGiaNhapDinhLuong,
                AutoUpdateCostPrice = SystemConfig.TuDongCapNhatGiaVonDinhLuong
            };
            ProductSaveResult result;
            try
            {
                result = _repo.SaveProduct(_row, lines, AppSession.UserId, options);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return false;
            }
            if (!result.Ok)
            {
                Msg.Warning(this, result.ErrorMessage);
                _tabs.SelectedTab = _tabInfo;
                if (result.ErrorMessage == ProductText.CodeEmpty ||
                    result.ErrorMessage == ProductText.CodeDuplicate(_row.Code ?? ""))
                {
                    _code.Focus();
                    _code.SelectAll();
                }
                else
                {
                    _name.Focus();
                    _name.SelectAll();
                }
                return false;
            }

            SavedId = result.SavedId;
            _row.Id = result.SavedId;
            bool wasNew = _isNew;
            _isNew = false;
            if (wasNew)
            {
                SetTitle("MẶT HÀNG - SỬA");
                ApplyConfigAndPermissions();
            }
            // Đồng bộ chữ đã chuẩn hóa (tên đã cắt khoảng trắng) về ô nhập.
            _loading = true;
            try
            {
                _name.Text = _row.Name ?? "";
                _code.Text = _row.Code ?? "";
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

        private static List<RecipeValidationLine> ToValidationLines(List<RecipeLine> lines)
        {
            var list = new List<RecipeValidationLine>(lines.Count);
            foreach (RecipeLine line in lines)
                list.Add(new RecipeValidationLine
                {
                    MaterialId = line.MaterialId,
                    MaterialTypeId = line.MaterialTypeId,
                    Quantity = line.Quantity,
                    MaterialMissing = line.MaterialMissing
                });
            return list;
        }

        private void SelectRecipeRow(int index)
        {
            _recipeFilter.Text = "";
            if (_recipeView == null || index < 0 || index >= _recipeView.Count)
                return;
            _recipeGrid.ClearSelection();
            _recipeGrid.Rows[index].Selected = true;
            _recipeGrid.CurrentCell = _recipeGrid.Rows[index].Cells["colQty"];
            _recipeGrid.BeginEdit(true);
        }

        private void DeleteCurrent()
        {
            if (_isNew || _readOnly || !_canRemove)
                return;
            if (!Msg.Confirm(this, ProductText.DeleteConfirm))
                return;
            ProductDeleteResult result;
            try
            {
                result = _repo.DeleteProduct(_row.Id);
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
                // Đang thêm mới: nút Tạo mới sinh lại mã (Q12).
                if (SystemConfig.TuDongSinhMaVach)
                    RegenerateCode();
                return;
            }
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi tạo mặt hàng mới không?"))
                return;
            string groupId = _row.GroupId;
            NewRecord(groupId);
            AfterRecordLoaded();
        }

        private void Navigate(int dir)
        {
            if (_rowIds == null || _rowIds.Count == 0)
                return;
            int target = _rowIndex + dir;
            if (target < 0 || target >= _rowIds.Count)
                return;
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi chuyển sang mặt hàng khác không?"))
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

        /// <summary>Hỏi lưu khi có thay đổi chưa lưu (Q23: Yes/No/Cancel). Trả false nếu phải dừng thao tác đang làm.</summary>
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
            if (DialogResult == DialogResult.OK || _loadFailed || !Dirty)
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

        // ------------------------------------------------------------------ Ảnh / lịch sử

        private void ShowImage(byte[] data)
        {
            Image old = _pic.Image;
            _pic.Image = null;
            if (old != null)
                old.Dispose();
            if (data == null || data.Length == 0)
                return;
            try
            {
                using (var ms = new MemoryStream(data))
                using (Image img = Image.FromStream(ms))
                    _pic.Image = new Bitmap(img);
            }
            catch
            {
                _pic.Image = null;
            }
        }

        private void PickImage()
        {
            if (_readOnly)
                return;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Chọn ảnh mặt hàng";
                dlg.Filter = "Ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tất cả các file (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    byte[] data = File.ReadAllBytes(dlg.FileName);
                    _row.Image = data;
                    ShowImage(data);
                    OnDirtyChanged();
                }
                catch (Exception ex)
                {
                    Msg.Error(this, "Không đọc được ảnh: " + ex.Message);
                }
            }
        }

        private void PasteImage()
        {
            if (_readOnly)
                return;
            if (!Clipboard.ContainsImage())
            {
                Msg.Info(this, "Clipboard không có ảnh.");
                return;
            }
            try
            {
                using (Image img = Clipboard.GetImage())
                using (var ms = new MemoryStream())
                {
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    _row.Image = ms.ToArray();
                }
                ShowImage(_row.Image);
                OnDirtyChanged();
            }
            catch (Exception ex)
            {
                Msg.Error(this, "Không dán được ảnh: " + ex.Message);
            }
        }

        private void ClearImage()
        {
            if (_readOnly)
                return;
            _row.Image = null;
            ShowImage(null);
            OnDirtyChanged();
        }

        private void OpenHistory(bool buy)
        {
            if (_isNew || _row == null || string.IsNullOrEmpty(_row.Id))
                return;
            using (var dlg = new ProductHistoryDialog(_databasePath, _row.Id, _row.Name, buy))
                dlg.ShowDialog(this);
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
            else if (e.KeyCode == Keys.F4 && _tabs.SelectedTab == _tabRecipe)
            {
                AddRecipeLine();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F8 && _tabs.SelectedTab == _tabRecipe)
            {
                DeleteRecipeLine();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // Enter = Lưu & thoát, trừ khi focus đang ở nút, link, combo (chọn mục) hoặc lưới định lượng.
                Control active = ActiveControl;
                if (active is ButtonBase || active is LinkLabel || active is ComboBox || active == _recipeGrid)
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
            _lnkBuy.LinkColor = t.Accent;
            _lnkBuy.ActiveLinkColor = t.Primary;
            _lnkBuy.VisitedLinkColor = t.Accent;
            _lnkSell.LinkColor = t.Accent;
            _lnkSell.ActiveLinkColor = t.Primary;
            _lnkSell.VisitedLinkColor = t.Accent;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_pic.Image != null)
                {
                    _pic.Image.Dispose();
                    _pic.Image = null;
                }
                _shortcutsMenu.Dispose();
                _btnDelete.Font.Dispose();
                _lblName.Font.Dispose();
                _btnPasteImage.Font.Dispose();
                _btnPickImage.Font.Dispose();
                _btnClearImage.Font.Dispose();
                _recipeGrid.Font.Dispose();
                _recipeGrid.ColumnHeadersDefaultCellStyle.Font.Dispose();
                _recipeTotals.Font.Dispose();
                _tabs.Font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
