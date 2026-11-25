using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Danh mục hóa đơn hủy" (docs/specs/danh-muc-hoa-don-huy.md): chỉ xem, không có nút Thêm/Sửa/Xóa/Excel/In.
    /// Thanh trên: Từ, Đến (để trống mặc định, lọc theo NGAYHUY), Lọc (chỉ lọc lưới chi tiết theo TENHANG/MAHANG), Refresh.
    /// Dòng ghi chú Q3; hai lưới master (22 cột cuộn ngang) – detail (7 cột) chỉ đọc, dùng DetailGridMenu.
    /// Không chứa SQL; mọi màu và cỡ chữ lấy từ ThemeTokens / ThemeManager.
    /// </summary>
    internal sealed class CancelledBillView : Panel
    {
        public const string MenuId = CancelledBillConstants.MenuId;

        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        private readonly CancelledBillRepository _repository;

        private readonly FlowLayoutPanel _topBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly DateBox _fromDate = new DateBox(true);
        private readonly DateBox _toDate = new DateBox(true);
        private readonly InputBox _txtFilter = new InputBox();
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Refresh", Tag = "primary" };

        private readonly Panel _notePanel = new Panel { Dock = DockStyle.Top };
        private readonly Label _noteLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            UseMnemonic = false,
            Tag = "muted",
            Text = CancelledBillConstants.DiscountNote
        };
        private readonly ToolTip _tip = new ToolTip();

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };

        private readonly Label _masterTitle = new Label { Text = "HÓA ĐƠN HỦY", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DataGridView _masterGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _masterEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có hóa đơn hủy trong khoảng đã chọn", Visible = false };

        private readonly Label _detailTitle = new Label { Text = "CHI TIẾT HÓA ĐƠN HỦY", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DataGridView _detailGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _detailEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có chi tiết hóa đơn hủy", Visible = false };

        private readonly ThemedContextMenu _masterMenu;
        private readonly ThemedContextMenu _detailMenu;
        private readonly Dictionary<string, int> _baseMasterWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _baseDetailWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _smallFont;

        private List<CancelledBillRow> _bills = new List<CancelledBillRow>();
        private List<CancelledBillLine> _lines = new List<CancelledBillLine>();
        private bool _loading = true;
        private bool _splitPlaced;

        /// <summary>Tắt hộp thoại báo lỗi modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressErrorPopup { get; set; }

        public CancelledBillView(UserSession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            _repository = new CancelledBillRepository(session.DatabasePath);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _smallFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);

            BuildTopBar(s);
            BuildNoteBar(s);
            BuildMasterPane(s);
            BuildDetailPane(s);

            _masterMenu = DetailGridMenu.AttachMenu(_masterGrid, this, ReloadBills);
            _detailMenu = DetailGridMenu.AttachMenu(_detailGrid, this, ReloadLines);

            _split.SplitterWidth = Math.Max(5, (int)(6 * s));
            _split.SizeChanged += delegate { PlaceSplit(); };

            Controls.Add(_split);
            Controls.Add(_notePanel);
            Controls.Add(_topBar);
            _split.BringToFront();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            _loading = false;
            ReloadBills();
        }

        // ------------------------------------------------------------------ Dựng giao diện

        private void BuildTopBar(float s)
        {
            _topBar.Padding = new Padding((int)(10 * s), (int)(6 * s), (int)(10 * s), (int)(6 * s));

            _fromDate.Inner.Font = _bodyFont;
            _fromDate.Size = new Size((int)(142 * s), (int)(32 * s));
            _fromDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _fromDate.ValueChanged += delegate { if (!_loading) ReloadBills(); };

            _toDate.Inner.Font = _bodyFont;
            _toDate.Size = new Size((int)(142 * s), (int)(32 * s));
            _toDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(16 * s), (int)(3 * s));
            _toDate.ValueChanged += delegate { if (!_loading) ReloadBills(); };

            _txtFilter.Inner.Font = _bodyFont;
            _txtFilter.Placeholder = "Lọc tên hàng, mã hàng...";
            _txtFilter.Size = new Size((int)(250 * s), (int)(32 * s));
            _txtFilter.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _txtFilter.TextChanged += delegate { if (!_loading) ApplyDetailFilter(); };

            _btnRefresh.Font = _bodyFont;
            int refreshW = Math.Max((int)(96 * s), TextRenderer.MeasureText(_btnRefresh.Text, _bodyFont).Width + (int)(28 * s));
            _btnRefresh.Size = new Size(refreshW, (int)(32 * s));
            _btnRefresh.Margin = new Padding((int)(2 * s), (int)(3 * s), 0, (int)(3 * s));
            _btnRefresh.Click += delegate { ReloadBills(); };

            _topBar.Controls.Add(MakeFlowLabel("Từ", s));
            _topBar.Controls.Add(_fromDate);
            _topBar.Controls.Add(MakeFlowLabel("Đến", s));
            _topBar.Controls.Add(_toDate);
            _topBar.Controls.Add(MakeFlowLabel("Lọc", s));
            _topBar.Controls.Add(_txtFilter);
            _topBar.Controls.Add(_btnRefresh);
        }

        private void BuildNoteBar(float s)
        {
            _notePanel.Height = (int)(28 * s);
            _notePanel.Padding = new Padding((int)(12 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));
            _noteLabel.Font = _smallFont;
            _tip.SetToolTip(_noteLabel, CancelledBillConstants.DiscountNote);
            _notePanel.Controls.Add(_noteLabel);
        }

        private void BuildMasterPane(float s)
        {
            _masterTitle.Font = _headingFont;
            _masterTitle.Height = (int)(30 * s);
            _masterTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            ConfigureGrid(_masterGrid, s);
            _masterGrid.ScrollBars = ScrollBars.Both;

            // Đúng 22 cột theo thứ tự bảng spec §1 (Q8). Không cột Bàn, không cột Tổng cộng, không cột Fill để cuộn ngang tự nhiên.
            AddMasterTextColumn(s, "colName", "Số phiếu", 115, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "HD261125-0001");
            AddMasterTextColumn(s, "colNote", "Ghi chú", 145, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Ghi chú hóa đơn hủy");
            AddMasterTextColumn(s, "colNgay", "Ngày", 98, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "dd/MM/yyyy", CultureInfo.InvariantCulture, "25/11/2026");
            AddMasterTextColumn(s, "colKhachHang", "Khách hàng", 135, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Khách hàng lẻ");
            AddMasterTextColumn(s, "colNhanVien", "Nhân viên", 125, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Nguyễn Văn A");
            AddMasterTextColumn(s, "colThuNgan", "Thu ngân hủy", 125, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Thu ngân 01");
            AddMasterTextColumn(s, "colGioHuy", "Giờ hủy", 78, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "HH:mm", CultureInfo.InvariantCulture, "23:59");
            AddMasterTextColumn(s, "colNgayHuy", "Ngày hủy", 98, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "dd/MM/yyyy", CultureInfo.InvariantCulture, "25/11/2026");
            AddMasterTextColumn(s, "colDoiTra", "Đổi trả", 88, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddMasterCheckColumn(s, "colDaThanhToan", "Đã thanh toán", 112);
            AddMasterTextColumn(s, "colGioThanhToan", "Giờ thanh toán", 122, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "dd/MM HH:mm", CultureInfo.InvariantCulture, "25/11 23:59");
            AddMasterTextColumn(s, "colTraLai", "Trả lại", 88, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddMasterTextColumn(s, "colTienHang", "Tiền hàng", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddMasterTextColumn(s, "colTiLeThue", "Tỉ lệ thuế", 92, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "100");
            AddMasterTextColumn(s, "colTienThue", "Tiền thuế", 96, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddMasterTextColumn(s, "colTiLeGiamGia", "Tỉ lệ giảm giá (%)", 138, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "0.##", ViCulture, "100,5");
            AddMasterTextColumn(s, "colTienGiamGia", "Tiền giảm giá", 112, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddMasterTextColumn(s, "colPhiVanChuyen", "Phí vận chuyển", 120, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddMasterTextColumn(s, "colThanhToanBoi", "Thanh toán bởi", 125, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Thu ngân 01");
            AddMasterTextColumn(s, "colLyDoHuy", "Lý do hủy", 165, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Khách đổi ý không dùng");
            AddMasterTextColumn(s, "colTienGio", "Tiền giờ", 92, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddMasterTextColumn(s, "colPhiDichVu", "Phí dịch vụ", 98, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");

            _masterGrid.SelectionChanged += delegate
            {
                if (!_loading)
                    ReloadLines();
            };

            _masterEmpty.Font = _bodyFont;
            _masterEmpty.Height = (int)(32 * s);

            _split.Panel1.Controls.Add(_masterGrid);
            _split.Panel1.Controls.Add(_masterEmpty);
            _split.Panel1.Controls.Add(_masterTitle);
            _masterGrid.BringToFront();
        }

        private void BuildDetailPane(float s)
        {
            _detailTitle.Font = _headingFont;
            _detailTitle.Height = (int)(30 * s);
            _detailTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            ConfigureGrid(_detailGrid, s);
            _detailGrid.ScrollBars = ScrollBars.Both;

            // Đúng 7 cột theo thứ tự ViewConfig ở spec §1 (Q9): Ghi chú, Mã hàng, Tên hàng, ĐVT, Số lượng, Đơn giá, Thành tiền.
            AddDetailColumn(s, "colDetailNote", "Ghi chú", 160, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, false, "Ghi chú mặt hàng");
            AddDetailColumn(s, "colDetailMaHang", "Mã hàng", 112, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, false, "MH0001");
            AddDetailColumn(s, "colDetailTenHang", "Tên hàng", 220, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, true, "Cà phê sữa đá");
            AddDetailColumn(s, "colDetailDvt", "ĐVT", 82, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, false, "Ly");
            AddDetailColumn(s, "colDetailSoLuong", "Số lượng", 96, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "#,##0.##", false, "100,5");
            AddDetailColumn(s, "colDetailDonGia", "Đơn giá", 112, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", false, "1.000.000");
            AddDetailColumn(s, "colDetailThanhTien", "Thành tiền", 122, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", false, "10.000.000");

            _detailEmpty.Font = _bodyFont;
            _detailEmpty.Height = (int)(32 * s);

            _split.Panel2.Controls.Add(_detailGrid);
            _split.Panel2.Controls.Add(_detailEmpty);
            _split.Panel2.Controls.Add(_detailTitle);
            _detailGrid.BringToFront();
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
            grid.ColumnHeadersHeight = (int)(36 * s);
            grid.RowTemplate.Height = (int)(36 * s); // đủ lớn cho thao tác chạm
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(grid, true, null);
        }

        private int MeasureColumnPadding(float s)
        {
            return (int)Math.Ceiling(22 * s);
        }

        private int MeasureColumnWidth(string header, int minWidthAt96, float s, string sample)
        {
            int pad = MeasureColumnPadding(s);
            int width = (int)Math.Ceiling(minWidthAt96 * s);
            int headerW = TextRenderer.MeasureText(header ?? "", _headingFont).Width + pad;
            if (headerW > width)
                width = headerW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, _bodyFont).Width + pad;
                if (sampleW > width)
                    width = sampleW;
            }
            return width;
        }

        private void AddMasterTextColumn(
            float s, string name, string header, int minWidthAt96,
            DataGridViewContentAlignment alignment, Type valueType,
            string format, IFormatProvider formatProvider, string sample)
        {
            int width = MeasureColumnWidth(header, minWidthAt96, s, sample);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                MinimumWidth = width,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ValueType = valueType,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = alignment;
            col.DefaultCellStyle.NullValue = "";
            if (alignment == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            else if (alignment == DataGridViewContentAlignment.MiddleCenter)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (!string.IsNullOrEmpty(format))
                col.DefaultCellStyle.Format = format;
            if (formatProvider != null)
                col.DefaultCellStyle.FormatProvider = formatProvider;

            _masterGrid.Columns.Add(col);
            _baseMasterWidths[name] = width;
        }

        private void AddMasterCheckColumn(float s, string name, string header, int minWidthAt96)
        {
            int width = MeasureColumnWidth(header, minWidthAt96, s, null);
            var col = new DataGridViewCheckBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                MinimumWidth = width,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ThreeState = false,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _masterGrid.Columns.Add(col);
            _baseMasterWidths[name] = width;
        }

        private void AddDetailColumn(
            float s, string name, string header, int minWidthAt96,
            DataGridViewContentAlignment alignment, Type valueType,
            string format, bool fill, string sample)
        {
            int width = MeasureColumnWidth(header, minWidthAt96, s, sample);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                MinimumWidth = width,
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
                ValueType = valueType,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = alignment;
            col.DefaultCellStyle.NullValue = "";
            if (alignment == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (!string.IsNullOrEmpty(format))
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = ViCulture;
            }
            _detailGrid.Columns.Add(col);
            if (!fill)
                _baseDetailWidths[name] = width;
        }

        private void AdjustColumnWidths(DataGridView grid, Dictionary<string, int> baseWidths, int maxWidthAt96)
        {
            float s = DeviceDpi / 96f;
            int pad = MeasureColumnPadding(s);
            int maxW = (int)Math.Ceiling(maxWidthAt96 * s);

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col is DataGridViewCheckBoxColumn)
                    continue;
                int baseWidth;
                if (!baseWidths.TryGetValue(col.Name, out baseWidth))
                    continue;
                int target = baseWidth;
                for (int r = 0; r < grid.Rows.Count; r++)
                {
                    object formatted = grid.Rows[r].Cells[col.Index].FormattedValue;
                    string text = Convert.ToString(formatted);
                    if (string.IsNullOrEmpty(text))
                        continue;
                    int needed = TextRenderer.MeasureText(text, _bodyFont).Width + pad;
                    if (needed > target)
                        target = needed;
                }
                if (target > maxW)
                    target = maxW;
                if (col.Width != target)
                    col.Width = target;
            }
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            float s = DeviceDpi / 96f;
            int minTop = (int)(160 * s);
            int minBottom = (int)(120 * s);
            if (_split.Height > minTop + minBottom + _split.SplitterWidth)
            {
                int want = (int)(_split.Height * 0.60f);
                if (want < minTop) want = minTop;
                int max = _split.Height - minBottom - _split.SplitterWidth;
                if (want > max) want = max;
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        // ------------------------------------------------------------------ Dữ liệu

        public void ReloadBills()
        {
            _loading = true;
            try
            {
                DateTime? from = _fromDate.NullableValue;
                DateTime? to = _toDate.NullableValue;

                _bills = _repository.LoadBills(from, to);
                _masterGrid.Rows.Clear();
                foreach (CancelledBillRow bill in _bills)
                {
                    int idx = _masterGrid.Rows.Add(
                        bill.Name ?? "",
                        bill.Note ?? "",
                        (object)bill.Ngay,
                        bill.KhachHang ?? "",
                        bill.NhanVien ?? "",
                        bill.ThuNgan ?? "",
                        (object)bill.GioHuy,
                        (object)bill.NgayHuy,
                        (object)bill.DoiTra,
                        bill.IsPaid,
                        (object)bill.GioThanhToan,
                        (object)bill.TraLai,
                        (object)bill.TienHang,
                        (object)bill.TiLeThue,
                        (object)bill.TienThue,
                        (object)bill.TiLeGiamGia,
                        (object)bill.TienGiamGia,
                        (object)bill.PhiVanChuyen,
                        bill.ThanhToanBoi ?? "",
                        bill.LyDoHuy ?? "",
                        (object)bill.TienGio,
                        (object)bill.PhiDichVu);
                    _masterGrid.Rows[idx].Tag = bill;
                }
                AdjustColumnWidths(_masterGrid, _baseMasterWidths, 360);
                _masterEmpty.Visible = _bills.Count == 0;

                // Sau mỗi lần tải lưới trên (mở màn, đổi ngày, Refresh, Refresh từ menu chuột phải):
                // luôn chọn dòng đầu nếu có (không giữ vị trí dòng cũ theo P2) và nạp chi tiết của nó;
                // không có dòng nào thì xóa trống lưới chi tiết. KHÔNG xóa ô Lọc (_txtFilter) theo Q11.
                _masterGrid.ClearSelection();
                if (_masterGrid.Rows.Count > 0)
                {
                    int firstCol = FirstVisibleMasterColumnIndex();
                    if (firstCol >= 0)
                        _masterGrid.CurrentCell = _masterGrid.Rows[0].Cells[firstCol];
                    _masterGrid.Rows[0].Selected = true;
                    ReloadLines();
                }
                else
                {
                    _masterGrid.CurrentCell = null;
                    _lines.Clear();
                    _detailGrid.Rows.Clear();
                    _detailEmpty.Text = "Không có chi tiết hóa đơn hủy";
                    _detailEmpty.Visible = true;
                }
            }
            catch (DataAccessException ex)
            {
                _bills.Clear();
                _masterGrid.Rows.Clear();
                _lines.Clear();
                _detailGrid.Rows.Clear();
                _masterEmpty.Visible = true;
                _detailEmpty.Text = "Không đọc được danh sách hóa đơn hủy";
                _detailEmpty.Visible = true;
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được danh sách hóa đơn hủy: " + ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private CancelledBillRow SelectedBill()
        {
            if (_masterGrid.SelectedRows.Count > 0)
                return _masterGrid.SelectedRows[0].Tag as CancelledBillRow;
            if (_masterGrid.CurrentRow != null && _masterGrid.CurrentRow.Selected)
                return _masterGrid.CurrentRow.Tag as CancelledBillRow;
            return null;
        }

        public void ReloadLines()
        {
            CancelledBillRow bill = SelectedBill();
            if (bill == null || string.IsNullOrEmpty(bill.Id))
            {
                _lines.Clear();
                _detailGrid.Rows.Clear();
                _detailEmpty.Text = "Không có chi tiết hóa đơn hủy";
                _detailEmpty.Visible = true;
                return;
            }

            try
            {
                _lines = _repository.LoadLines(bill.Id);
                ApplyDetailFilter();
            }
            catch (DataAccessException ex)
            {
                _lines.Clear();
                _detailGrid.Rows.Clear();
                _detailEmpty.Text = "Không đọc được chi tiết hóa đơn hủy";
                _detailEmpty.Visible = true;
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được chi tiết hóa đơn hủy: " + ex.Message);
            }
        }

        private void ApplyDetailFilter()
        {
            string query = _txtFilter.Text ?? "";
            _detailGrid.Rows.Clear();
            foreach (CancelledBillLine line in _lines)
            {
                if (!CancelledBillFilter.MatchesLine(line, query))
                    continue;
                int idx = _detailGrid.Rows.Add(
                    line.Note ?? "",
                    line.MaHang ?? "",
                    line.TenHang ?? "",
                    line.Dvt ?? "",
                    (object)line.SoLuong,
                    (object)line.DonGia,
                    (object)line.ThanhTien);
                _detailGrid.Rows[idx].Tag = line;
            }
            AdjustColumnWidths(_detailGrid, _baseDetailWidths, 320);
            _detailGrid.ClearSelection();
            _detailEmpty.Text = _lines.Count == 0
                ? "Hóa đơn hủy không có dòng chi tiết"
                : "Không có dòng chi tiết khớp chuỗi lọc";
            _detailEmpty.Visible = _detailGrid.Rows.Count == 0;
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal DateTime? FromDateValue { get { return _fromDate.NullableValue; } }
        internal DateTime? ToDateValue { get { return _toDate.NullableValue; } }
        internal string FilterText { get { return _txtFilter.Text ?? ""; } }
        internal bool NoteVisible { get { return _notePanel.Visible && _noteLabel.Visible; } }
        internal string NoteText { get { return _noteLabel.Text ?? ""; } }
        internal int MasterRowCount { get { return _masterGrid.Rows.Count; } }
        internal int DetailRowCount { get { return _detailGrid.Rows.Count; } }
        internal DateBox FromDateBoxForTest { get { return _fromDate; } }
        internal DateBox ToDateBoxForTest { get { return _toDate; } }
        internal InputBox FilterBoxForTest { get { return _txtFilter; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal DataGridView MasterGridForTest { get { return _masterGrid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal ThemedContextMenu MasterMenuForTest { get { return _masterMenu; } }
        internal ThemedContextMenu DetailMenuForTest { get { return _detailMenu; } }
        internal Font HeadingFontForTest { get { return _headingFont; } }

        internal List<string> MasterColumnHeaders()
        {
            var headers = new List<string>(_masterGrid.Columns.Count);
            foreach (DataGridViewColumn col in _masterGrid.Columns)
                headers.Add(col.HeaderText);
            return headers;
        }

        internal List<string> DetailColumnHeaders()
        {
            var headers = new List<string>(_detailGrid.Columns.Count);
            foreach (DataGridViewColumn col in _detailGrid.Columns)
                headers.Add(col.HeaderText);
            return headers;
        }

        internal int FindMasterRowIndexById(string billHuyId)
        {
            for (int i = 0; i < _masterGrid.Rows.Count; i++)
            {
                var row = _masterGrid.Rows[i].Tag as CancelledBillRow;
                if (row != null && string.Equals(row.Id, billHuyId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        internal object GetMasterFormattedValue(int rowIndex, string columnName)
        {
            if (rowIndex < 0 || rowIndex >= _masterGrid.Rows.Count)
                return null;
            DataGridViewColumn col = _masterGrid.Columns[columnName];
            if (col == null)
                return null;
            return _masterGrid.Rows[rowIndex].Cells[col.Index].FormattedValue;
        }

        internal object GetDetailFormattedValue(int rowIndex, string columnName)
        {
            if (rowIndex < 0 || rowIndex >= _detailGrid.Rows.Count)
                return null;
            DataGridViewColumn col = _detailGrid.Columns[columnName];
            if (col == null)
                return null;
            return _detailGrid.Rows[rowIndex].Cells[col.Index].FormattedValue;
        }

        private int FirstVisibleMasterColumnIndex()
        {
            for (int i = 0; i < _masterGrid.Columns.Count; i++)
                if (_masterGrid.Columns[i].Visible)
                    return i;
            return -1;
        }

        /// <summary>Chỉ thao tác trực tiếp lên lưới (đổi CurrentCell/Selected); để sự kiện SelectionChanged tự nạp chi tiết.</summary>
        internal bool SelectMasterRow(int index)
        {
            int firstCol = FirstVisibleMasterColumnIndex();
            if (index < 0 || index >= _masterGrid.Rows.Count || firstCol < 0)
                return false;
            _masterGrid.CurrentCell = _masterGrid.Rows[index].Cells[firstCol];
            _masterGrid.Rows[index].Selected = true;
            return true;
        }

        internal bool SelectMasterById(string billHuyId)
        {
            int idx = FindMasterRowIndexById(billHuyId);
            return idx >= 0 && SelectMasterRow(idx);
        }

        internal void Preview(string option)
        {
            string[] parts = (option ?? "").Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return;
            if (parts[0] == "from")
            {
                _fromDate.Inner.Text = parts[1];
                _fromDate.Commit();
            }
            else if (parts[0] == "to")
            {
                _toDate.Inner.Text = parts[1];
                _toDate.Commit();
            }
            else if (parts[0] == "filter")
            {
                _txtFilter.Text = parts[1];
            }
            else if (parts[0] == "select" && _masterGrid.Rows.Count > 0)
            {
                int idx;
                if (int.TryParse(parts[1], out idx) && idx >= 0 && idx < _masterGrid.Rows.Count)
                    SelectMasterRow(idx);
            }
            else if (parts[0] == "scroll")
            {
                int colIdx;
                if (int.TryParse(parts[1], out colIdx) && colIdx >= 0 && colIdx < _masterGrid.Columns.Count)
                    _masterGrid.FirstDisplayedScrollingColumnIndex = colIdx;
            }
        }

        // ------------------------------------------------------------------ Giao diện / Theme

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;

            _topBar.BackColor = t.Background;
            _notePanel.BackColor = Draw.Mix(t.Surface, t.Info, 0.08f);
            _noteLabel.BackColor = Color.Transparent;
            _noteLabel.ForeColor = t.TextMuted;

            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _masterTitle.ForeColor = t.Primary;
            _detailTitle.ForeColor = t.Primary;

            _fromDate.ApplyTheme(t, t.Background);
            _toDate.ApplyTheme(t, t.Background);

            Color strong = Draw.StrongBorder(t);
            ThemeApplier.ApplyInput(_txtFilter, t, t.Background, strong);
            ThemeApplier.ApplyButton(_btnRefresh, t, "primary");
            ThemeApplier.ApplyGrid(_masterGrid, t);
            ThemeApplier.ApplyGrid(_detailGrid, t);

            _masterEmpty.BackColor = t.Background;
            _masterEmpty.ForeColor = t.TextMuted;
            _detailEmpty.BackColor = t.Background;
            _detailEmpty.ForeColor = t.TextMuted;

            _masterGrid.Invalidate();
            _detailGrid.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _tip.Dispose();
                _bodyFont.Dispose();
                _headingFont.Dispose();
                _smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
