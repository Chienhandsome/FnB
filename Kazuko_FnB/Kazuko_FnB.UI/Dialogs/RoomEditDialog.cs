using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// Dialog Thêm/Sửa bàn (spec 1.2: trang "Thông tin bàn" + trang "Bảng giá áp dụng", code tay).
    /// Ô giá theo giờ (Bảng giá/Đơn giá/Tiền mở bàn/Cách tính giờ) CHỈ hiện khi CoTienGio; trang "Bảng giá áp dụng"
    /// chỉ hiện khi CoTienGio + ChoPhepDungNhieuBangGiaTrenBill; hàng IP công tắc chỉ hiện khi CoTienGio +
    /// CoSuDungCongTacDienTu + admin (3.10). Nút Bật/Tắt đèn qua IP chưa hỗ trợ (không có giao thức trong spec).
    /// CACHTINHGIO qua hằng RoomCalc (Q8 chưa xác minh mã số enum — xem RoomCalc).
    /// Đã áp quyết định của Chiến 21/11: bỏ thùng rác (xóa cứng), tiền decimal.
    /// </summary>
    internal sealed class RoomEditDialog : ThemedDialog
    {
        private const int ContentWidth96 = 520;
        private const int ContentHeight96 = 340;
        private const string ShortcutsHint = "(Ctrl+S: Lưu&Mới, Ctrl+L: Lưu, Ctrl+N: Mới, Ctrl+P: In, ESC: Thoát)";
        private const string UnsupportedText = "Chức năng chưa hỗ trợ trong phiên bản này.";

        private readonly string _databasePath;
        private readonly RoomRepository _repo;
        private readonly AreaRepository _areaRepo;
        private readonly FormRepository _formRepo;

        private readonly List<string> _rowIds;
        private int _rowIndex;

        private List<AreaRow> _areas = new List<AreaRow>();
        private List<LookupItem> _groups = new List<LookupItem>();
        private List<LookupItem> _types = new List<LookupItem>();
        private List<LookupItem> _priceLists = new List<LookupItem>();

        private RoomRow _row;
        private bool _isNew;
        private bool _readOnly;
        private bool _loadFailed;
        private bool _loading;
        private string _original = "";

        private readonly bool _canRemove;

        // ---- Tab 1: Thông tin bàn ----
        private readonly TextBox _name = new TextBox();
        private readonly LookupCombo _area = new LookupCombo();
        private readonly LookupCombo _group = new LookupCombo();
        private readonly LookupCombo _type = new LookupCombo();
        private readonly TextBox _note = new TextBox();
        private readonly GroupBox _grpPrice = new GroupBox { Text = "Giá theo giờ", TabStop = false };
        private readonly RadioButton _rByArea = new RadioButton { Text = "Theo khu vực" };
        private readonly RadioButton _rByHour = new RadioButton { Text = "Theo giờ" };
        private readonly RadioButton _rByPriceList = new RadioButton { Text = "Theo bảng giá" };
        private readonly Label _lblLegacyCalc = new Label { AutoSize = false, Visible = false };
        private readonly NumericUpDown _unitPrice = new NumericUpDown();
        private readonly NumericUpDown _openAmount = new NumericUpDown();
        private readonly LookupCombo _priceList = new LookupCombo();
        private readonly TextBox _switchIp = new TextBox();
        private readonly FlatButton _btnSwitchOn = new FlatButton { Text = "Bật" };
        private readonly FlatButton _btnSwitchOff = new FlatButton { Text = "Tắt" };
        private readonly Label _lblName = new Label { Text = "Tên bàn *", AutoSize = false };
        private readonly Label _lblArea = new Label { Text = "Khu vực *", AutoSize = false };
        private readonly Label _lblGroup = new Label { Text = "Nhóm hiển thị", AutoSize = false };
        private readonly Label _lblType = new Label { Text = "Loại phòng", AutoSize = false };
        private readonly Label _lblNote = new Label { Text = "Ghi chú", AutoSize = false };
        private readonly Label _lblUnitPrice = new Label { Text = "Đơn giá", AutoSize = false };
        private readonly Label _lblOpenAmount = new Label { Text = "Tiền mở bàn", AutoSize = false };
        private readonly Label _lblPriceList = new Label { Text = "Bảng giá", AutoSize = false };
        private readonly Label _lblSwitchIp = new Label { Text = "IP công tắc điện", AutoSize = false };

        // ---- Tab 2: Bảng giá áp dụng ----
        private readonly DataGridView _priceGrid = new DataGridView();

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
        private readonly TabPage _tabInfo = new TabPage("Thông tin bàn");
        private readonly TabPage _tabPrice = new TabPage("Bảng giá áp dụng");

        /// <summary>ID bàn đã lưu (null nếu chưa lưu lần nào trong phiên dialog).</summary>
        public string SavedId { get; private set; }

        /// <summary>Bản ghi đang sửa đã bị xóa (dialog đóng sau khi xóa).</summary>
        public bool Deleted { get; private set; }

        /// <summary>Dùng cho --selftest: tab "Bảng giá áp dụng" có đang hiện không (chỉ khi CoTienGio + ChoPhepDungNhieuBangGiaTrenBill).</summary>
        internal bool HasPriceTab
        {
            get { return _tabs.TabPages.Contains(_tabPrice); }
        }

        /// <param name="roomId">null = thêm mới.</param>
        /// <param name="presetAreaId">Khu vực chọn sẵn cho bàn mới (null = chưa chọn — người gọi mở hộp chọn khu vực trước).</param>
        /// <param name="rowIds">ID các dòng trên lưới theo thứ tự hiện tại (để F10/F11 chuyển dòng); null = không chuyển được.</param>
        public RoomEditDialog(UserSession session, string roomId, string presetAreaId, List<string> rowIds)
            : base("BÀN - THÊM MỚI", ShortcutsHint, 552, 470)
        {
            _databasePath = session.DatabasePath;
            _repo = new RoomRepository(_databasePath);
            _areaRepo = new AreaRepository(_databasePath);
            _formRepo = new FormRepository(_databasePath);
            _rowIds = rowIds;
            _rowIndex = rowIds == null ? -1 : rowIds.IndexOf(roomId ?? "");
            _isNew = string.IsNullOrEmpty(roomId);

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
            BuildInfoTab();
            BuildPriceTab();
            _tabs.Font = new Font("Segoe UI", 10f);
            _tabs.TabPages.Add(_tabInfo);
            if (SystemConfig.CoTienGio && SystemConfig.ChoPhepDungNhieuBangGiaTrenBill)
                _tabs.TabPages.Add(_tabPrice);
            _tabs.Dock = DockStyle.Fill;
            Body.Controls.Add(_tabs);

            LoadLookups();
            if (_isNew)
                NewRecord(presetAreaId);
            else
                LoadRecord(roomId);

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
                    Msg.Error(this, "Bàn không còn tồn tại.");
                    Close();
                    return;
                }
                _name.Focus();
                _name.SelectAll();
            };
            FormClosing += OnFormClosing;
        }

        /// <summary>
        /// Vùng nội dung của tab (TabPage) = 520×340 ở 96 DPI. Đo vùng tab thật rồi nới/co dialog
        /// đúng bằng chênh lệch (phần tab header, viền, toolbar, header/footer dialog tự được tính).
        /// </summary>
        private void FitTabContent()
        {
            float s = Dpi;
            PerformLayout();
            _tabs.PerformLayout();
            Size target = new Size((int)(ContentWidth96 * s), (int)(ContentHeight96 * s));
            Size cur = _tabInfo.ClientSize;
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

        private void BuildInfoTab()
        {
            // Vị trí/kích thước theo bảng 1.2 (96 DPI, tọa độ trong tab page 520×340).
            _lblName.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _lblArea.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            PlaceLabel(_lblName, _tabInfo, 7, 10);
            PlaceLabel(_lblArea, _tabInfo, 7, 40);
            PlaceLabel(_lblGroup, _tabInfo, 7, 70);
            PlaceLabel(_lblType, _tabInfo, 7, 100);
            PlaceLabel(_lblNote, _tabInfo, 7, 130);

            Place(_name, _tabInfo, 118, 6, 280, 20, 0);
            Place(_area, _tabInfo, 118, 36, 280, 21, 1);
            Place(_group, _tabInfo, 118, 66, 280, 21, 2);
            Place(_type, _tabInfo, 118, 96, 280, 21, 3);
            Place(_note, _tabInfo, 118, 126, 280, 20, 4);

            // Nhóm giá theo giờ (chỉ hiện khi CoTienGio — xem ApplyConfigAndPermissions).
            _grpPrice.SetBounds((int)(7 * Dpi), (int)(160 * Dpi), (int)(500 * Dpi), (int)(165 * Dpi));
            _tabInfo.Controls.Add(_grpPrice);
            Place(_rByArea, _grpPrice, 12, 22, 120, 17, 5);
            Place(_rByHour, _grpPrice, 140, 22, 100, 17, 6);
            Place(_rByPriceList, _grpPrice, 248, 22, 130, 17, 7);
            Place(_lblLegacyCalc, _grpPrice, 12, 44, 470, 17, 8);
            PlaceLabel(_lblUnitPrice, _grpPrice, 12, 68);
            PlaceLabel(_lblOpenAmount, _grpPrice, 250, 68);
            Place(_unitPrice, _grpPrice, 118, 66, 110, 20, 9);
            Place(_openAmount, _grpPrice, 356, 66, 110, 20, 10);
            PlaceLabel(_lblPriceList, _grpPrice, 12, 98);
            Place(_priceList, _grpPrice, 118, 96, 280, 21, 11);
            PlaceLabel(_lblSwitchIp, _grpPrice, 12, 128);
            Place(_switchIp, _grpPrice, 118, 126, 180, 20, 12);
            Place(_btnSwitchOn, _grpPrice, 306, 124, 60, 24, 13);
            Place(_btnSwitchOff, _grpPrice, 372, 124, 60, 24, 14);

            _unitPrice.Minimum = 0m;
            _unitPrice.Maximum = 1000000000000m;
            _unitPrice.DecimalPlaces = 2;
            _unitPrice.ThousandsSeparator = true;
            _openAmount.Minimum = 0m;
            _openAmount.Maximum = 1000000000000m;
            _openAmount.DecimalPlaces = 2;
            _openAmount.ThousandsSeparator = true;

            _btnSwitchOn.Click += delegate { Msg.Info(this, UnsupportedText); };
            _btnSwitchOff.Click += delegate { Msg.Info(this, UnsupportedText); };
            foreach (RadioButton r in new[] { _rByArea, _rByHour, _rByPriceList })
                r.CheckedChanged += delegate { if (r.Checked) { _lblLegacyCalc.Visible = false; UpdatePriceControls(); OnDirtyChanged(); } };

            foreach (Control c in new Control[] { _name, _note, _switchIp })
                ((TextBox)c).TextChanged += delegate { OnDirtyChanged(); };
            foreach (NumericUpDown n in new[] { _unitPrice, _openAmount })
                n.ValueChanged += delegate { OnDirtyChanged(); };
            _area.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _group.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _type.SelectedIndexChanged += delegate { OnDirtyChanged(); };
            _priceList.SelectedIndexChanged += delegate { OnDirtyChanged(); };
        }

        private void BuildPriceTab()
        {
            float s = Dpi;
            _priceGrid.Dock = DockStyle.Fill;
            _priceGrid.AllowUserToAddRows = false;
            _priceGrid.AllowUserToDeleteRows = false;
            _priceGrid.AllowUserToResizeRows = false;
            _priceGrid.AutoGenerateColumns = false;
            _priceGrid.MultiSelect = false;
            _priceGrid.RowHeadersVisible = false;
            _priceGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _priceGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _priceGrid.ColumnHeadersHeight = (int)(34 * s);
            _priceGrid.RowTemplate.Height = (int)(28 * s);
            _priceGrid.Font = new Font("Segoe UI", 10f);
            _priceGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _priceGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colSel",
                HeaderText = "",
                Width = (int)(40 * s),
                ReadOnly = false
            });
            _priceGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Bảng giá",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });
            _priceGrid.CurrentCellDirtyStateChanged += delegate
            {
                if (_priceGrid.IsCurrentCellDirty)
                    _priceGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _priceGrid.CellValueChanged += delegate { OnDirtyChanged(); };
            _tabPrice.Controls.Add(_priceGrid);
        }

        // ------------------------------------------------------------------ Nạp dữ liệu

        private void LoadLookups()
        {
            _areas = _areaRepo.LoadAll();
            _groups = _formRepo.LoadAll("DNHOMHIENTHI");
            _types = _formRepo.LoadAll("DLOAIPHONG");
            _priceLists = _formRepo.LoadAll("DBANGGIA");
            var areaItems = new List<LookupItem>();
            foreach (AreaRow a in _areas)
                areaItems.Add(new LookupItem { Id = a.Id, Name = a.Name });
            _area.SetItems(areaItems);
            _group.SetItems(_groups);
            _type.SetItems(_types);
            _priceList.SetItems(_priceLists);
            BuildPriceGridRows(new List<PriceAssignment>());
        }

        private void BuildPriceGridRows(List<PriceAssignment> assigned)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (assigned != null)
            {
                foreach (PriceAssignment p in assigned)
                    if (!string.IsNullOrEmpty(p.PriceListId))
                        ids.Add(p.PriceListId);
            }
            _priceGrid.Rows.Clear();
            foreach (LookupItem item in _priceLists)
            {
                int i = _priceGrid.Rows.Add(ids.Contains(item.Id), item.Name);
                _priceGrid.Rows[i].Tag = item.Id;
            }
        }

        private void NewRecord(string presetAreaId)
        {
            _row = RoomRow.NewDefault();
            _isNew = true;
            _row.AreaId = presetAreaId;
            SetTitle("BÀN - THÊM MỚI");
            FillControls(new List<PriceAssignment>());
        }

        private void LoadRecord(string roomId)
        {
            _row = _repo.LoadRoom(roomId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = RoomRow.NewDefault();
                return;
            }
            _isNew = false;
            SetTitle("BÀN - SỬA");
            FillControls(_repo.LoadPriceAssignments(roomId));
        }

        private void FillControls(List<PriceAssignment> prices)
        {
            _loading = true;
            try
            {
                _name.Text = _row.Name ?? "";
                _area.SelectId(_row.AreaId);
                _group.SelectId(_row.DisplayGroupId);
                _type.SelectId(_row.RoomTypeId);
                _note.Text = _row.Note ?? "";
                _unitPrice.Value = Clamp(_unitPrice, _row.UnitPrice);
                _openAmount.Value = Clamp(_openAmount, _row.OpenAmount);
                _priceList.SelectId(_row.PriceListId);
                _switchIp.Text = _row.SwitchIp ?? "";
                SetCalcRadios(_row.CalcHour);
                BuildPriceGridRows(prices);
                UpdatePriceControls();
            }
            finally
            {
                _loading = false;
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
        /// Chọn radio theo CACHTINHGIO. Giá trị lạ (không nằm trong ba hằng RoomCalc) thì không tick radio nào
        /// mà hiện nhãn chỉ-xem; khi lưu sẽ giữ nguyên giá trị cũ trừ khi người dùng tick một radio khác.
        /// </summary>
        private void SetCalcRadios(int value)
        {
            string key = RoomRules.CalcModeKey(value);
            _rByArea.Checked = key == "area";
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
            if (_rByHour.Checked)
                return RoomCalc.ByHour;
            if (_rByPriceList.Checked)
                return RoomCalc.ByPriceList;
            if (_rByArea.Checked)
                return RoomCalc.ByArea;
            // Không tick radio nào (giá trị lạ, hoặc nhóm giá đang ẩn vì CoTienGio = 0): giữ nguyên.
            return _row == null ? RoomCalc.ByArea : _row.CalcHour;
        }

        // ------------------------------------------------------------------ Cấu hình + quyền (3.10/mục 4)

        private void ApplyConfigAndPermissions()
        {
            bool ro = _readOnly;
            bool priceOn = SystemConfig.CoTienGio;
            _grpPrice.Visible = priceOn;
            bool ipOn = priceOn && SystemConfig.CoSuDungCongTacDienTu && AppSession.Current != null && AppSession.Current.IsAdmin;
            _lblSwitchIp.Visible = ipOn;
            _switchIp.Visible = ipOn;
            _btnSwitchOn.Visible = ipOn;
            _btnSwitchOff.Visible = ipOn;
            UpdatePriceControls();

            if (ro)
            {
                foreach (Control c in new Control[] { _name, _area, _group, _type, _note, _unitPrice, _openAmount, _priceList, _switchIp })
                    c.Enabled = false;
                foreach (RadioButton r in new[] { _rByArea, _rByHour, _rByPriceList })
                    r.Enabled = false;
                _btnSwitchOn.Enabled = false;
                _btnSwitchOff.Enabled = false;
                _priceGrid.ReadOnly = true;
                SubtitleText = ShortcutsHint + " (chỉ xem — không có quyền sửa)";
            }
        }

        /// <summary>
        /// Ô giá chỉ hiện khi radio tương ứng được chọn (1.2: Bảng giá ẩn khi không chọn theo bảng giá,
        /// Đơn giá chỉ khi chọn theo giờ). Giá trị trong ô ẩn vẫn giữ nguyên khi lưu (không xóa).
        /// </summary>
        private void UpdatePriceControls()
        {
            _unitPrice.Visible = _rByHour.Checked;
            _lblUnitPrice.Visible = _rByHour.Checked;
            _priceList.Visible = _rByPriceList.Checked;
            _lblPriceList.Visible = _rByPriceList.Checked;
        }

        // ------------------------------------------------------------------ Trạng thái bẩn

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
            get { return _original == null || Snapshot() != _original; }
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_name.Text).Append('|');
            sb.Append(_area.SelectedId).Append('|');
            sb.Append(_group.SelectedId).Append('|');
            sb.Append(_type.SelectedId).Append('|');
            sb.Append(_note.Text).Append('|');
            sb.Append(SelectedCalcValue()).Append('|');
            sb.Append(_unitPrice.Value).Append('|');
            sb.Append(_openAmount.Value).Append('|');
            sb.Append(_priceList.SelectedId).Append('|');
            sb.Append(_switchIp.Text).Append('|');
            foreach (DataGridViewRow r in _priceGrid.Rows)
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
            string areaId = _row.AreaId;
            NewRecord(areaId);
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
            CollectRow();

            string error = RoomRules.ValidateRoom(_row.Name, _row.AreaId, SystemConfig.CoTienGio, _row.CalcHour,
                _row.PriceListId, _row.UnitPrice, _row.OpenAmount);
            if (error != null)
            {
                Msg.Warning(this, error);
                _tabs.SelectedTab = _tabInfo;
                if (error == RoomText.RoomNameEmpty)
                {
                    _name.Focus();
                    _name.SelectAll();
                }
                else
                {
                    _area.Focus();
                }
                return false;
            }

            RoomSaveResult result;
            try
            {
                result = _repo.SaveRoom(_row, CollectPriceAssignments(), AppSession.UserId);
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
                SetTitle("BÀN - SỬA");
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
            _row.AreaId = _area.SelectedId;
            _row.DisplayGroupId = _group.SelectedId;
            _row.RoomTypeId = _type.SelectedId;
            _row.Note = _note.Text;
            _row.CalcHour = SelectedCalcValue();
            _row.UnitPrice = _unitPrice.Value;
            _row.OpenAmount = _openAmount.Value;
            _row.PriceListId = _priceList.SelectedId;
            _row.SwitchIp = _switchIp.Text;
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

        private void DeleteCurrent()
        {
            if (_isNew || !_canRemove)
                return;
            if (!Msg.Confirm(this, ProductText.DeleteConfirm))
                return;
            RoomDeleteResult result;
            try
            {
                result = _repo.DeleteRoom(_row.Id);
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
                return;
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi tạo bàn mới không?"))
                return;
            string areaId = _row.AreaId;
            NewRecord(areaId);
            AfterRecordLoaded();
        }

        private void Navigate(int dir)
        {
            if (_rowIds == null || _rowIds.Count == 0)
                return;
            int target = _rowIndex + dir;
            if (target < 0 || target >= _rowIds.Count)
                return;
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi chuyển sang bàn khác không?"))
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
            else if (e.KeyCode == Keys.Enter)
            {
                // Enter = Lưu & thoát, trừ khi focus đang ở nút, link, combo (chọn mục) hoặc lưới bảng giá.
                Control active = ActiveControl;
                if (active is ButtonBase || active is LinkLabel || active is ComboBox || active == _priceGrid)
                    return;
                SaveAndClose();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lblName.Font.Dispose();
                _lblArea.Font.Dispose();
                _btnDelete.Font.Dispose();
                _shortcutsMenu.Dispose();
                _priceGrid.Font.Dispose();
                _priceGrid.ColumnHeadersDefaultCellStyle.Font.Dispose();
                _tabs.Font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
