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
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Lưu vết hoạt động" (docs/specs/luu-vet-hoat-dong.md):
    /// khung trái "HÓA ĐƠN" (Từ/Đến, Số HĐ, Tải dữ liệu + lưới hóa đơn 2 nhánh),
    /// khung phải "CHI TIẾT THAO TÁC" (Tài khoản, Lọc dữ liệu, nút Xóa lưu vết ẩn + lưới đúng 9 cột).
    /// Không chứa SQL; mọi màu và cỡ chữ lấy từ ThemeTokens / ThemeManager.
    /// </summary>
    internal sealed class ActivityLogView : Panel
    {
        public const string MenuId = ActivityConstants.MenuId;

        private readonly UserSession _session;
        private readonly ActivityLogger _logger;
        private readonly bool _canDelete;

        private readonly Panel _warningPanel = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _warningLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };

        // Khung trái: HÓA ĐƠN
        private readonly Label _leftTitle = new Label { Text = "HÓA ĐƠN", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly TableLayoutPanel _leftFilters = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, RowCount = 2 };
        private readonly DateBox _fromDate = new DateBox();
        private readonly DateBox _toDate = new DateBox();
        private readonly InputBox _txtBillNumber = new InputBox();
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Tải dữ liệu", Tag = "primary" };
        private readonly DataGridView _billGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _billEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có hóa đơn trong khoảng đã chọn", Visible = false };

        // Khung phải: CHI TIẾT THAO TÁC
        private readonly Label _rightTitle = new Label { Text = "CHI TIẾT THAO TÁC", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly FlowLayoutPanel _rightFilters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly ComboBox _cboAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly InputBox _txtFilter = new InputBox();
        private readonly FlatButton _btnDelete = new FlatButton { Text = "Xóa lưu vết", Tag = "danger", Visible = false };
        private readonly DataGridView _logGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _logEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Chọn một hóa đơn bên trái để xem chi tiết thao tác", Visible = false };

        private readonly List<FieldFrame> _frames = new List<FieldFrame>();
        private readonly Dictionary<string, int> _baseLogColumnWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _boldGridFont;

        private ThemeTokens _theme = ThemeManager.Current;
        private List<ActivityBillItem> _bills = new List<ActivityBillItem>();
        private List<ActivityLogEntry> _logs = new List<ActivityLogEntry>();
        private bool _loggingEnabled = true;
        private bool _warningPopupShown;
        private bool _loading = true;
        private bool _splitPlaced;

        /// <summary>Tắt hộp thoại cảnh báo modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressWarningPopup { get; set; }

        public ActivityLogView(UserSession session)
            : this(session, null)
        {
        }

        internal ActivityLogView(UserSession session, bool? forceCtrlShiftHeld)
        {
            if (session == null) throw new ArgumentNullException("session");
            _session = session;
            _logger = new ActivityLogger(session.DatabasePath, session.UserName, session.UserId);

            // Spec §3.4: nút Xóa lưu vết chỉ hiện khi admin VÀ giữ Ctrl+Shift lúc mở màn.
            bool ctrlShiftHeld = forceCtrlShiftHeld.HasValue
                ? forceCtrlShiftHeld.Value
                : Control.ModifierKeys == (Keys.Control | Keys.Shift);
            _canDelete = ActivityLogger.CanShowDeleteButton(_session, ctrlShiftHeld);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _boldGridFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            BuildWarningBanner(s);
            BuildLeftPane(s);
            BuildRightPane(s);

            _split.FixedPanel = FixedPanel.Panel1;
            _split.SizeChanged += delegate { PlaceSplit(); };

            Controls.Add(_split);
            Controls.Add(_warningPanel);
            _split.BringToFront();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            InitializeData();
        }

        // ------------------------------------------------------------------ Dựng giao diện

        private void BuildWarningBanner(float s)
        {
            _warningPanel.Height = (int)(54 * s);
            _warningPanel.Padding = new Padding((int)(12 * s), (int)(6 * s), (int)(12 * s), (int)(6 * s));
            _warningLabel.Font = _headingFont;
            _warningLabel.Text = ActivityConstants.DisabledWarningText;
            _warningPanel.Controls.Add(_warningLabel);
        }

        private void BuildLeftPane(float s)
        {
            _leftTitle.Font = _headingFont;
            _leftTitle.Height = (int)(32 * s);
            _leftTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            _leftFilters.Padding = new Padding((int)(8 * s), (int)(4 * s), (int)(8 * s), (int)(6 * s));
            _leftFilters.Height = (int)(86 * s);
            _leftFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58 * s));
            _leftFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _leftFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42 * s));
            _leftFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _leftFilters.RowStyles.Add(new RowStyle(SizeType.Absolute, 38 * s));
            _leftFilters.RowStyles.Add(new RowStyle(SizeType.Absolute, 38 * s));

            _fromDate.Inner.Font = _bodyFont;
            _fromDate.Height = (int)(32 * s);
            _fromDate.Dock = DockStyle.Fill;
            _fromDate.Margin = new Padding(0, (int)(3 * s), (int)(6 * s), (int)(3 * s));
            _fromDate.ValueChanged += delegate { if (!_loading) ReloadBills(); };
            _frames.Add(_fromDate);

            _toDate.Inner.Font = _bodyFont;
            _toDate.Height = (int)(32 * s);
            _toDate.Dock = DockStyle.Fill;
            _toDate.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            _toDate.ValueChanged += delegate { if (!_loading) ReloadBills(); };
            _frames.Add(_toDate);

            _txtBillNumber.Inner.Font = _bodyFont;
            _txtBillNumber.Placeholder = "Nhập số HĐ...";
            _txtBillNumber.Height = (int)(32 * s);
            _txtBillNumber.Dock = DockStyle.Fill;
            _txtBillNumber.Margin = new Padding(0, (int)(3 * s), (int)(6 * s), (int)(3 * s));
            _txtBillNumber.Inner.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ReloadBills();
                }
            };

            _btnRefresh.Font = _bodyFont;
            _btnRefresh.Height = (int)(32 * s);
            _btnRefresh.Dock = DockStyle.Fill;
            _btnRefresh.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
            _btnRefresh.Click += delegate { ReloadBills(); };

            _leftFilters.Controls.Add(MakeFilterLabel("Từ", s), 0, 0);
            _leftFilters.Controls.Add(_fromDate, 1, 0);
            _leftFilters.Controls.Add(MakeFilterLabel("Đến", s), 2, 0);
            _leftFilters.Controls.Add(_toDate, 3, 0);

            _leftFilters.Controls.Add(MakeFilterLabel("Số HĐ", s), 0, 1);
            _leftFilters.Controls.Add(_txtBillNumber, 1, 1);
            _leftFilters.SetColumnSpan(_txtBillNumber, 2);
            _leftFilters.Controls.Add(_btnRefresh, 3, 1);

            ConfigureGrid(_billGrid, s);
            AddTextColumn(_billGrid, s, "colBillName", "Hóa đơn", 118);
            AddTextColumn(_billGrid, s, "colBillTable", "Bàn", 92);
            DataGridViewTextBoxColumn statusCol = AddTextColumn(_billGrid, s, "colBillStatus", "Trạng thái", 120);
            statusCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            statusCol.MinimumWidth = (int)(110 * s);

            _billGrid.CellFormatting += BillGrid_CellFormatting;
            _billGrid.SelectionChanged += delegate
            {
                if (!_loading)
                    ReloadLogs();
            };

            _billEmpty.Font = _bodyFont;
            _billEmpty.Height = (int)(32 * s);

            _split.Panel1.Controls.Add(_billGrid);
            _split.Panel1.Controls.Add(_billEmpty);
            _split.Panel1.Controls.Add(_leftFilters);
            _split.Panel1.Controls.Add(_leftTitle);
            _billGrid.BringToFront();
        }

        private void BuildRightPane(float s)
        {
            _rightTitle.Font = _headingFont;
            _rightTitle.Height = (int)(32 * s);
            _rightTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            _rightFilters.Padding = new Padding((int)(8 * s), (int)(4 * s), (int)(8 * s), (int)(6 * s));

            _cboAccount.Font = _bodyFont;
            var accountFrame = new FieldFrame(_cboAccount, 0);
            accountFrame.Size = new Size((int)(190 * s), (int)(32 * s));
            accountFrame.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _frames.Add(accountFrame);
            _cboAccount.SelectedIndexChanged += delegate { if (!_loading) ReloadLogs(); };

            _txtFilter.Inner.Font = _bodyFont;
            _txtFilter.Placeholder = "Gõ để lọc chi tiết...";
            _txtFilter.Size = new Size((int)(240 * s), (int)(32 * s));
            _txtFilter.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _txtFilter.TextChanged += delegate { if (!_loading) ApplyLogFilter(); };

            _btnDelete.Font = _bodyFont;
            _btnDelete.Size = new Size((int)(120 * s), (int)(34 * s));
            _btnDelete.Margin = new Padding((int)(4 * s), (int)(2 * s), 0, (int)(2 * s));
            _btnDelete.Visible = _canDelete;
            _btnDelete.Click += BtnDelete_Click;

            _rightFilters.Controls.Add(MakeFlowLabel("Tài khoản", s));
            _rightFilters.Controls.Add(accountFrame);
            _rightFilters.Controls.Add(MakeFlowLabel("Lọc dữ liệu", s));
            _rightFilters.Controls.Add(_txtFilter);
            _rightFilters.Controls.Add(_btnDelete);

            // Đúng 9 cột lưới phải theo thứ tự spec §1; mọi cột không sắp xếp; không có Tên hàng/SL/Đơn giá/Thành tiền.
            // Đo sẵn theo font đậm của lưới (Hủy bill, Gộp bàn) để mọi dòng đậm/thường hiện đủ chữ, không bị "...".
            ConfigureGrid(_logGrid, s);
            AddMeasuredLogColumn(s, "colNgay", "Ngày", 108, "25/11/2026", "30/12/2026");
            AddMeasuredLogColumn(s, "colGio", "Giờ", 92, "23:59:59", "00:00:00");
            AddMeasuredLogColumn(s, "colSoDonHang", "Số đơn hàng", 128, "HD261125-0001", "LV-HUY-01");
            DataGridViewTextBoxColumn noteCol = AddTextColumn(_logGrid, s, "colNoiDung", "Nội dung", 240);
            noteCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            noteCol.MinimumWidth = (int)(200 * s);
            AddMeasuredLogColumn(s, "colTaiKhoan", "Tài khoản", 122, "Administrator", "thungan_01");
            AddMeasuredLogColumn(s, "colThietBi", "Thiết bị", 140, "DESKTOP-0000000", Environment.MachineName);
            AddMeasuredLogColumn(s, "colBan", "Bàn", 118, "Bàn Chụp Hủy", "Bàn VIP 01");
            AddMeasuredLogColumn(s, "colChucNang", "Chức năng", 164,
                ActivityConstants.FuncSuDungDichVu,
                ActivityConstants.FuncQuanLyBanHang,
                ActivityConstants.FuncDieuChinhHoaDon,
                ActivityConstants.FuncPosCamUng);
            AddMeasuredLogColumn(s, "colPhanLoai", "Phân loại", 190,
                ActivityKindInfo.DisplayName(ActivityKind.DatGiamGiaMatHang),
                ActivityKindInfo.DisplayName(ActivityKind.ChuyenMatHang),
                ActivityKindInfo.DisplayName(ActivityKind.DatSoLuong));

            _logGrid.CellFormatting += LogGrid_CellFormatting;

            _logEmpty.Font = _bodyFont;
            _logEmpty.Height = (int)(32 * s);

            _split.Panel2.Controls.Add(_logGrid);
            _split.Panel2.Controls.Add(_logEmpty);
            _split.Panel2.Controls.Add(_rightFilters);
            _split.Panel2.Controls.Add(_rightTitle);
            _logGrid.BringToFront();
        }

        private Label MakeFilterLabel(string text, float s)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont,
                Margin = new Padding(0, (int)(2 * s), 0, (int)(2 * s))
            };
        }

        private Label MakeFlowLabel(string text, float s)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(TextRenderer.MeasureText(text, _bodyFont).Width + (int)(8 * s), (int)(34 * s)),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont,
                Margin = new Padding((int)(4 * s), (int)(2 * s), 0, (int)(2 * s))
            };
        }

        private void ConfigureGrid(DataGridView grid, float s)
        {
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.Font = _bodyFont;
            grid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(34 * s);
            grid.RowTemplate.Height = (int)(34 * s); // đủ lớn cho thao tác chạm
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(grid, true, null);
        }

        private static DataGridViewTextBoxColumn AddTextColumn(DataGridView grid, float s, string name, string header, int widthAt96)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = (int)(widthAt96 * s),
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            grid.Columns.Add(col);
            return col;
        }

        private static int MeasureLogColumnPadding(float s)
        {
            return (int)Math.Ceiling(20 * s);
        }

        private int MeasureLogColumnWidth(string header, int minWidthAt96, float s, params string[] boldSamples)
        {
            int pad = MeasureLogColumnPadding(s);
            int width = (int)Math.Ceiling(minWidthAt96 * s);
            int headerW = TextRenderer.MeasureText(header ?? "", _headingFont).Width + pad;
            if (headerW > width)
                width = headerW;
            if (boldSamples != null)
            {
                for (int i = 0; i < boldSamples.Length; i++)
                {
                    if (string.IsNullOrEmpty(boldSamples[i]))
                        continue;
                    int sampleW = TextRenderer.MeasureText(boldSamples[i], _boldGridFont).Width + pad;
                    if (sampleW > width)
                        width = sampleW;
                }
            }
            return width;
        }

        private DataGridViewTextBoxColumn AddMeasuredLogColumn(float s, string name, string header, int minWidthAt96, params string[] boldSamples)
        {
            int width = MeasureLogColumnWidth(header, minWidthAt96, s, boldSamples);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _logGrid.Columns.Add(col);
            _baseLogColumnWidths[name] = width;
            return col;
        }

        private void AdjustLogFixedColumnWidths()
        {
            float s = DeviceDpi / 96f;
            int pad = MeasureLogColumnPadding(s);
            foreach (DataGridViewColumn col in _logGrid.Columns)
            {
                int baseWidth;
                if (!_baseLogColumnWidths.TryGetValue(col.Name, out baseWidth))
                    continue;
                int target = baseWidth;
                for (int r = 0; r < _logGrid.Rows.Count; r++)
                {
                    var entry = _logGrid.Rows[r].Tag as ActivityLogEntry;
                    bool isBold = entry != null &&
                        (entry.Tone == ActivityRowTone.DangerBold || entry.Tone == ActivityRowTone.SuccessBold);
                    Font font = isBold ? _boldGridFont : _bodyFont;
                    string text = Convert.ToString(_logGrid.Rows[r].Cells[col.Index].Value);
                    if (string.IsNullOrEmpty(text))
                        continue;
                    int needed = TextRenderer.MeasureText(text, font).Width + pad;
                    if (needed > target)
                        target = needed;
                }
                if (col.Width != target)
                    col.Width = target;
            }
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int want = (int)(390 * s);
            if (_split.Width > want + (int)(320 * s))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            MaybeShowDisabledWarningPopup();
        }

        private void MaybeShowDisabledWarningPopup()
        {
            if (_loggingEnabled || _warningPopupShown || SuppressWarningPopup || !Visible || !IsHandleCreated)
                return;
            _warningPopupShown = true;
            BeginInvoke((Action)delegate
            {
                if (!IsDisposed)
                    Msg.Warning(this, ActivityConstants.DisabledWarningText);
            });
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ReloadBills();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ Dữ liệu

        private void InitializeData()
        {
            _loading = true;
            try
            {
                DateTime dbToday = DateTime.Today;
                try
                {
                    dbToday = _logger.GetServerDate();
                    _loggingEnabled = _logger.IsEnabledFresh();
                }
                catch (DataAccessException)
                {
                    _loggingEnabled = SystemConfig.KichHoatLuuVetHoatDong;
                }
                _fromDate.Value = dbToday;
                _toDate.Value = dbToday;
                _warningPanel.Visible = !_loggingEnabled;

                LoadAccountDropdown();
            }
            finally
            {
                _loading = false;
            }
            ReloadBills();
        }

        private void LoadAccountDropdown()
        {
            _cboAccount.Items.Clear();
            _cboAccount.Items.Add(new ActivityAccountItem { Id = "", DisplayName = "(Tất cả)", FilterValue = "" });
            try
            {
                foreach (ActivityAccountItem item in _logger.LoadAccounts())
                    _cboAccount.Items.Add(item);
            }
            catch (DataAccessException)
            {
                // Giữ mục Tất cả nếu không đọc được danh sách tài khoản.
            }
            _cboAccount.SelectedIndex = 0;
        }

        public void ReloadBills()
        {
            _loading = true;
            try
            {
                _bills = _logger.LoadBills(_fromDate.Value, _toDate.Value, _txtBillNumber.Text);
                _billGrid.Rows.Clear();
                foreach (ActivityBillItem bill in _bills)
                {
                    int idx = _billGrid.Rows.Add(bill.Number, bill.TableName, bill.StatusText);
                    _billGrid.Rows[idx].Tag = bill;
                }
                _billGrid.ClearSelection();
                _billGrid.CurrentCell = null;
                _billEmpty.Visible = _bills.Count == 0;

                // Chưa chọn hóa đơn ở lưới trái → lưới phải trống (§1).
                _logs.Clear();
                _logGrid.Rows.Clear();
                _logEmpty.Text = "Chọn một hóa đơn bên trái để xem chi tiết thao tác";
                _logEmpty.Visible = true;
            }
            catch (DataAccessException ex)
            {
                _bills.Clear();
                _billGrid.Rows.Clear();
                _logs.Clear();
                _logGrid.Rows.Clear();
                _billEmpty.Visible = true;
                _logEmpty.Visible = true;
                if (!SuppressWarningPopup)
                    Msg.Error(this, "Không đọc được danh sách hóa đơn: " + ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private ActivityBillItem SelectedBill()
        {
            if (_billGrid.SelectedRows.Count == 0)
                return null;
            return _billGrid.SelectedRows[0].Tag as ActivityBillItem;
        }

        private string SelectedAccountFilter()
        {
            var item = _cboAccount.SelectedItem as ActivityAccountItem;
            return item == null ? "" : (item.FilterValue ?? "");
        }

        public void ReloadLogs()
        {
            ActivityBillItem bill = SelectedBill();
            if (bill == null || string.IsNullOrEmpty(bill.Id))
            {
                _logs.Clear();
                _logGrid.Rows.Clear();
                _logEmpty.Text = "Chọn một hóa đơn bên trái để xem chi tiết thao tác";
                _logEmpty.Visible = true;
                return;
            }

            try
            {
                _logs = _logger.LoadLogs(bill.Id, SelectedAccountFilter());
                ApplyLogFilter();
            }
            catch (DataAccessException ex)
            {
                _logs.Clear();
                _logGrid.Rows.Clear();
                _logEmpty.Text = "Không đọc được lưu vết thao tác";
                _logEmpty.Visible = true;
                if (!SuppressWarningPopup)
                    Msg.Error(this, "Không đọc được lưu vết hoạt động: " + ex.Message);
            }
        }

        private void ApplyLogFilter()
        {
            string filter = _txtFilter.Text ?? "";
            _logGrid.Rows.Clear();
            foreach (ActivityLogEntry entry in _logs)
            {
                if (!ActivityLogger.MatchesFilter(entry, filter))
                    continue;
                int idx = _logGrid.Rows.Add(
                    entry.Date == DateTime.MinValue ? "" : entry.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    entry.Time == DateTime.MinValue ? "" : entry.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    entry.BillNumber ?? "",
                    entry.Note ?? "",
                    entry.Account ?? "",
                    entry.Device ?? "",
                    entry.TableName ?? "",
                    entry.FunctionName ?? "",
                    entry.KindDisplayName);
                _logGrid.Rows[idx].Tag = entry;
            }
            AdjustLogFixedColumnWidths();
            _logGrid.ClearSelection();
            _logEmpty.Text = "Không có lưu vết thao tác";
            _logEmpty.Visible = _logGrid.Rows.Count == 0;
        }

        // ------------------------------------------------------------------ Tô màu (spec §1, §2.3)

        private void BillGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.RowIndex >= _billGrid.Rows.Count)
                    return;
                var bill = _billGrid.Rows[e.RowIndex].Tag as ActivityBillItem;
                if (bill == null)
                    return;
                if (bill.Kind == ActivityBillKind.Cancelled)
                    e.CellStyle.ForeColor = _theme.Danger;
                else if (bill.Kind == ActivityBillKind.InUse)
                    e.CellStyle.ForeColor = _theme.Info;
                else
                    e.CellStyle.ForeColor = _theme.Text;
            }
            catch (Exception)
            {
                // Lỗi định dạng một dòng không làm sập lưới (§2.3, §7).
            }
        }

        private void LogGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.RowIndex >= _logGrid.Rows.Count)
                    return;
                var entry = _logGrid.Rows[e.RowIndex].Tag as ActivityLogEntry;
                if (entry == null)
                    return;
                switch (entry.Tone)
                {
                    case ActivityRowTone.DangerBold:
                        e.CellStyle.ForeColor = _theme.Danger;
                        e.CellStyle.Font = _boldGridFont;
                        break;
                    case ActivityRowTone.Info:
                        e.CellStyle.ForeColor = _theme.Info;
                        break;
                    case ActivityRowTone.SuccessBold:
                        e.CellStyle.ForeColor = _theme.Success;
                        e.CellStyle.Font = _boldGridFont;
                        break;
                    case ActivityRowTone.Danger:
                        e.CellStyle.ForeColor = _theme.Danger;
                        break;
                    default:
                        e.CellStyle.ForeColor = _theme.Text;
                        break;
                }
            }
            catch (Exception)
            {
                // Lỗi định dạng một dòng không làm sập lưới (§2.3, §7).
            }
        }

        // ------------------------------------------------------------------ Xóa toàn bộ lưu vết (spec §3.4)

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (!_canDelete || !_session.IsAdmin)
                return;
            using (var dlg = new ConfirmDeleteActivityLogDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    _logger.DeleteAll(_session, dlg.EnteredPassword);
                    ReloadBills();
                }
                catch (Exception ex) when (ex is DataAccessException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
                {
                    Msg.Error(this, "Không xóa được lưu vết: " + ex.Message);
                }
            }
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal bool WarningBannerVisible { get { return _warningPanel.Visible; } }
        internal string WarningBannerText { get { return _warningLabel.Text; } }
        internal bool DeleteButtonVisible { get { return _btnDelete.Visible; } }
        internal int BillRowCount { get { return _billGrid.Rows.Count; } }
        internal int LogRowCount { get { return _logGrid.Rows.Count; } }
        internal DateTime FromDateValue { get { return _fromDate.Value; } }
        internal DateTime ToDateValue { get { return _toDate.Value; } }
        internal Font BoldGridFont { get { return _boldGridFont; } }

        internal void SetDateRangeForTest(DateTime fromDate, DateTime toDate)
        {
            _loading = true;
            try
            {
                _fromDate.Value = fromDate.Date;
                _toDate.Value = toDate.Date;
            }
            finally
            {
                _loading = false;
            }
            ReloadBills();
        }

        internal int LogColumnWidth(string columnName)
        {
            DataGridViewColumn col = _logGrid.Columns[columnName];
            return col == null ? -1 : col.Width;
        }

        internal bool IsLogColumnFill(string columnName)
        {
            DataGridViewColumn col = _logGrid.Columns[columnName];
            return col != null && col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill;
        }

        internal int FindBillRowIndex(string billId)
        {
            for (int i = 0; i < _billGrid.Rows.Count; i++)
            {
                var bill = _billGrid.Rows[i].Tag as ActivityBillItem;
                if (bill != null && string.Equals(bill.Id, billId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        internal int FindLogRowIndex(ActivityKind kind)
        {
            for (int i = 0; i < _logGrid.Rows.Count; i++)
            {
                var entry = _logGrid.Rows[i].Tag as ActivityLogEntry;
                if (entry != null && entry.KindValue == (int)kind)
                    return i;
            }
            return -1;
        }

        internal string GetLogCellText(int rowIndex, string columnName)
        {
            if (rowIndex < 0 || rowIndex >= _logGrid.Rows.Count)
                return "";
            DataGridViewColumn col = _logGrid.Columns[columnName];
            if (col == null)
                return "";
            return Convert.ToString(_logGrid.Rows[rowIndex].Cells[col.Index].Value) ?? "";
        }

        internal DataGridViewCellStyle GetBillCellStyle(int rowIndex, int columnIndex = 0)
        {
            return CaptureFormattedCellStyle(_billGrid, rowIndex, columnIndex);
        }

        internal DataGridViewCellStyle GetLogCellStyle(int rowIndex, int columnIndex = 0)
        {
            return CaptureFormattedCellStyle(_logGrid, rowIndex, columnIndex);
        }

        private static DataGridViewCellStyle CaptureFormattedCellStyle(DataGridView grid, int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count || columnIndex < 0 || columnIndex >= grid.Columns.Count)
                return null;
            DataGridViewCellStyle captured = null;
            DataGridViewCellFormattingEventHandler spy = delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex == rowIndex && e.ColumnIndex == columnIndex && e.CellStyle != null)
                    captured = e.CellStyle.Clone();
            };
            grid.CellFormatting += spy;
            try
            {
                object unused = grid.Rows[rowIndex].Cells[columnIndex].FormattedValue;
                return captured;
            }
            finally
            {
                grid.CellFormatting -= spy;
            }
        }

        internal List<string> LogColumnHeaders()
        {
            var headers = new List<string>(_logGrid.Columns.Count);
            foreach (DataGridViewColumn col in _logGrid.Columns)
                headers.Add(col.HeaderText);
            return headers;
        }

        internal bool SelectBillById(string billId)
        {
            for (int i = 0; i < _billGrid.Rows.Count; i++)
            {
                var bill = _billGrid.Rows[i].Tag as ActivityBillItem;
                if (bill != null && string.Equals(bill.Id, billId, StringComparison.Ordinal))
                {
                    _billGrid.ClearSelection();
                    _billGrid.Rows[i].Selected = true;
                    ReloadLogs();
                    return true;
                }
            }
            return false;
        }

        internal void SetFilterText(string text)
        {
            _txtFilter.Text = text ?? "";
            ApplyLogFilter();
        }

        internal void Preview(string option)
        {
            string[] parts = (option ?? "").Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return;
            DateTime d;
            if (parts[0] == "from" && DateTime.TryParseExact(parts[1], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                _fromDate.Value = d;
            else if (parts[0] == "to" && DateTime.TryParseExact(parts[1], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                _toDate.Value = d;
            else if (parts[0] == "bill")
            {
                _txtBillNumber.Text = parts[1];
                ReloadBills();
            }
            else if (parts[0] == "select" && _billGrid.Rows.Count > 0)
            {
                int idx;
                if (int.TryParse(parts[1], out idx) && idx >= 0 && idx < _billGrid.Rows.Count)
                {
                    _billGrid.Rows[idx].Selected = true;
                    ReloadLogs();
                }
            }
        }

        // ------------------------------------------------------------------ Giao diện / Theme

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;

            _warningPanel.BackColor = Draw.Mix(t.Surface, t.Danger, 0.10f);
            _warningLabel.ForeColor = t.Danger;

            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _leftTitle.ForeColor = t.Primary;
            _rightTitle.ForeColor = t.Primary;
            _leftFilters.BackColor = t.Background;
            _rightFilters.BackColor = t.Background;

            foreach (FieldFrame frame in _frames)
                frame.ApplyTheme(t, t.Background);

            Color strong = Draw.StrongBorder(t);
            ThemeApplier.ApplyInput(_txtBillNumber, t, t.Background, strong);
            ThemeApplier.ApplyInput(_txtFilter, t, t.Background, strong);
            ThemeApplier.ApplyButton(_btnRefresh, t, "primary");
            ThemeApplier.ApplyButton(_btnDelete, t, "danger");
            ThemeApplier.ApplyGrid(_billGrid, t);
            ThemeApplier.ApplyGrid(_logGrid, t);

            _billEmpty.BackColor = t.Background;
            _billEmpty.ForeColor = t.TextMuted;
            _logEmpty.BackColor = t.Background;
            _logEmpty.ForeColor = t.TextMuted;

            _billGrid.Invalidate();
            _logGrid.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _bodyFont.Dispose();
                _headingFont.Dispose();
                _boldGridFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
