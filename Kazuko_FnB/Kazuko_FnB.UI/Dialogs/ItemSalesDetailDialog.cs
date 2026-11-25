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

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Dialog chỉ xem "Chi tiết bán hàng theo mặt hàng" mở khi nhấp đúp một dòng trên màn Thống kê mặt hàng bán
    /// (docs/specs/thong-ke-mat-hang-ban.md §2.0, §2.4).
    /// Lưới trên: danh sách hóa đơn (22 cột đúng thứ tự ViewConfig gốc), chọn bill đầu khi mở.
    /// Lưới dưới: chi tiết hóa đơn đang chọn (11 cột), dòng của mặt hàng đang xem tô đậm + màu cảnh báo từ ThemeTokens
    /// kể cả khi dòng đang được chọn. Cỡ dialog tự nhân theo DPI từ ThemedDialog.
    /// </summary>
    internal sealed class ItemSalesDetailDialog : ThemedDialog, IScreenPreview
    {
        private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

        private readonly ItemSalesRepository _repository;
        private readonly UserSession _previewSession;
        private BillReportScope _scope;
        private ItemSalesDetailKey _detailKey;
        private int _previewSelectIndex;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };

        private readonly Label _billsTitle = new Label { Text = "DANH MỤC HÓA ĐƠN", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DataGridView _billsGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _billsEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có hóa đơn phù hợp", Visible = false };

        private readonly Label _linesTitle = new Label { Text = "CHI TIẾT HÓA ĐƠN", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
        private readonly DataGridView _linesGrid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _linesEmpty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có dòng chi tiết", Visible = false };

        private readonly FlatButton _btnExit;
        private readonly Dictionary<string, int> _baseBillWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _baseLineWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _boldGridFont;

        private ThemeTokens _theme = ThemeManager.Current;
        private List<ItemSalesBillRow> _bills = new List<ItemSalesBillRow>();
        private List<ItemSalesBillLine> _lines = new List<ItemSalesBillLine>();
        private bool _loading = true;
        private bool _splitPlaced;

        /// <summary>Tắt hộp thoại báo lỗi modal khi chụp màn hình hoặc selftest.</summary>
        internal bool SuppressErrorPopup { get; set; }

        public ItemSalesDetailDialog(string databasePath, BillReportScope scope, ItemSalesDetailKey detailKey)
            : this(databasePath, scope, detailKey, null)
        {
        }

        private ItemSalesDetailDialog(string databasePath, BillReportScope scope, ItemSalesDetailKey detailKey, UserSession previewSession)
            : base("Chi tiết bán hàng theo mặt hàng", BuildSubtitle(scope, detailKey), 1140, 660)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");

            _repository = new ItemSalesRepository(databasePath);
            _scope = scope ?? new BillReportScope { FromDate = DateTime.Today, ToDate = DateTime.Today, IsAdmin = true };
            _detailKey = detailKey;
            _previewSession = previewSession;

            float s = Dpi;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size((int)(820 * s), (int)(500 * s));

            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _boldGridFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            BuildBillsPane(s);
            BuildLinesPane(s);

            DetailGridMenu.Attach(_billsGrid, this, ReloadBills);
            DetailGridMenu.Attach(_linesGrid, this, ReloadLines);

            _split.SplitterWidth = Math.Max(5, (int)(6 * s));
            _split.SizeChanged += delegate { PlaceSplit(); };
            Body.Controls.Add(_split);

            _btnExit = AddButton("Thoát", null, delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            });
            CancelButton = _btnExit;

            ApplyDialogTheme();

            _loading = false;
            ReloadBills();
        }

        /// <summary>Dựng dialog cho lệnh <c>--render-screen preview.itemsalesdetail</c>.</summary>
        internal static ItemSalesDetailDialog CreateForPreview(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            var repo = new ItemSalesRepository(session.DatabasePath);
            DateTime today = repo.GetServerDate();
            var scope = new BillReportScope
            {
                FromDate = today,
                ToDate = today,
                StoreId = "",
                UserId = session.UserId,
                IsAdmin = session.IsAdmin
            };
            ItemSalesDetailKey key = ResolvePreviewKey(repo, scope, 0);
            var dlg = new ItemSalesDetailDialog(session.DatabasePath, scope, key, session)
            {
                SuppressErrorPopup = true
            };
            return dlg;
        }

        private static ItemSalesDetailKey ResolvePreviewKey(ItemSalesRepository repo, BillReportScope scope, int selectIndex)
        {
            try
            {
                bool sortByCode = repo.ShouldSortByCode();
                List<ItemSalesRow> rows = repo.LoadItemSales(scope, ItemSalesCostMode.LineCost, sortByCode);
                if (rows.Count == 0)
                    return null;
                int idx = selectIndex >= 0 && selectIndex < rows.Count ? selectIndex : 0;
                ItemSalesRow picked = rows[idx];
                return new ItemSalesDetailKey
                {
                    ProductId = picked.ProductId,
                    ItemName = picked.ItemName,
                    UnitPrice = picked.UnitPrice,
                    DiscountRate = picked.DiscountRate,
                    UnitCost = picked.UnitCost
                };
            }
            catch (DataAccessException)
            {
                return null;
            }
        }

        private static string BuildSubtitle(BillReportScope scope, ItemSalesDetailKey key)
        {
            if (key == null)
                return "Danh sách hóa đơn chứa mặt hàng đã chọn";
            string range = scope == null
                ? ""
                : string.Format(CultureInfo.InvariantCulture, " — Từ {0:dd/MM/yyyy} đến {1:dd/MM/yyyy}", scope.FromDate, scope.ToDate);
            return string.Format(ViCulture, "{0} · Đơn giá: {1:n0} · Giảm giá: {2:0.##}%{3}",
                key.ItemName ?? "", key.UnitPrice, key.DiscountRate, range);
        }

        // ------------------------------------------------------------------ Dựng giao diện

        private void BuildBillsPane(float s)
        {
            _billsTitle.Font = _headingFont;
            _billsTitle.Height = (int)(30 * s);
            _billsTitle.Padding = new Padding((int)(6 * s), 0, (int)(6 * s), 0);

            ConfigureGrid(_billsGrid, s);
            _billsGrid.ScrollBars = ScrollBars.Both;

            // Đúng 22 cột theo ViewConfig ở spec §2.0:
            // Ghi chú, Số phiếu, Ngày, Khách hàng, Mã khách, Địa chỉ, Điện thoại, Tổng cộng,
            // Phí vận chuyển, Giảm giá, % giảm giá, Thuế, % thuế, Tiền hàng, Diễn giải,
            // Giờ thanh toán, Khách đưa, Trả lại, Thanh toán bởi, Số phiếu đặt hàng, Giao hàng, Đổi trả.
            AddBillCol(s, "colNote", "Ghi chú", 140, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Ghi chú hóa đơn");
            AddBillCol(s, "colName", "Số phiếu", 115, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "HD261125-0001");
            AddBillCol(s, "colNgay", "Ngày", 98, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "dd/MM/yyyy", CultureInfo.InvariantCulture, "25/11/2026");
            AddBillCol(s, "colKhachHang", "Khách hàng", 135, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Khách hàng lẻ");
            AddBillCol(s, "colMaKhach", "Mã khách", 96, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "KH00001");
            AddBillCol(s, "colDiaChi", "Địa chỉ", 150, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "123 Lê Lợi");
            AddBillCol(s, "colDienThoai", "Điện thoại", 108, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "0901234567");
            AddBillCol(s, "colTongCong", "Tổng cộng", 106, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddBillCol(s, "colPhiVanChuyen", "Phí vận chuyển", 118, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddBillCol(s, "colGiamGia", "Giảm giá", 98, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddBillCol(s, "colTiLeGiamGia", "% giảm giá", 96, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "0.##", ViCulture, "100,5");
            AddBillCol(s, "colThue", "Thuế", 92, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddBillCol(s, "colTiLeThue", "% thuế", 82, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "0.##", ViCulture, "10,5");
            AddBillCol(s, "colTienHang", "Tiền hàng", 106, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddBillCol(s, "colDienGiai", "Diễn giải", 140, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Diễn giải hóa đơn");
            AddBillCol(s, "colGioThanhToan", "Giờ thanh toán", 136, DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, "25/11/2026 23:59");
            AddBillCol(s, "colKhachDua", "Khách đưa", 104, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "10.000.000");
            AddBillCol(s, "colTraLai", "Trả lại", 96, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");
            AddBillCol(s, "colThanhToanBoi", "Thanh toán bởi", 126, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Thu ngân 01");
            AddBillCol(s, "colSoPhieuDatHang", "Số phiếu đặt hàng", 136, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "DH261125-001");
            AddBillCol(s, "colGiaoHang", "Giao hàng", 112, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Giao tận nơi");
            AddBillCol(s, "colDoiTra", "Đổi trả", 92, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "1.000.000");

            _billsGrid.SelectionChanged += delegate
            {
                if (!_loading)
                    ReloadLines();
            };

            _billsEmpty.Font = _bodyFont;
            _billsEmpty.Height = (int)(30 * s);

            _split.Panel1.Controls.Add(_billsGrid);
            _split.Panel1.Controls.Add(_billsEmpty);
            _split.Panel1.Controls.Add(_billsTitle);
            _billsGrid.BringToFront();
        }

        private void BuildLinesPane(float s)
        {
            _linesTitle.Font = _headingFont;
            _linesTitle.Height = (int)(30 * s);
            _linesTitle.Padding = new Padding((int)(6 * s), 0, (int)(6 * s), 0);

            ConfigureGrid(_linesGrid, s);
            _linesGrid.ScrollBars = ScrollBars.Both;

            // Đúng 11 cột theo spec §2.0:
            // Ghi chú, Tên mặt hàng, ĐVT, Mã hàng, Bảo hành, Mã sẵn có, Bảo hành (dòng), CK%, Thành tiền, Đơn giá, S.lượng.
            AddLineCol(s, "colLineNote", "Ghi chú", 130, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "Ghi chú dòng");
            AddLineCol(s, "colLineTenMatHang", "Tên mặt hàng", 190, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "Trà sữa trân châu đường đen");
            AddLineCol(s, "colLineDvt", "ĐVT", 76, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "Ly");
            AddLineCol(s, "colLineMaHang", "Mã hàng", 96, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "MH0001");
            AddLineCol(s, "colLineBaoHanh", "Bảo hành", 92, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "12 tháng");
            AddLineCol(s, "colLineMaSanCo", "Mã sẵn có", 98, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "89300001");
            AddLineCol(s, "colLineBaoHanhDong", "Bảo hành (dòng)", 122, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, "6 tháng");
            AddLineCol(s, "colLineCk", "CK%", 74, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "0.##", "100,5");
            AddLineCol(s, "colLineThanhTien", "Thành tiền", 108, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", "10.000.000");
            AddLineCol(s, "colLineDonGia", "Đơn giá", 102, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", "1.000.000");
            AddLineCol(s, "colLineSoLuong", "S.lượng", 88, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "#,##0.##", "1.000,5");

            _linesGrid.CellFormatting += LinesGrid_CellFormatting;

            _linesEmpty.Font = _bodyFont;
            _linesEmpty.Height = (int)(30 * s);

            _split.Panel2.Controls.Add(_linesGrid);
            _split.Panel2.Controls.Add(_linesEmpty);
            _split.Panel2.Controls.Add(_linesTitle);
            _linesGrid.BringToFront();
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
            grid.RowTemplate.Height = (int)(34 * s);
            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(grid, true, null);
        }

        private int MeasureColumnWidth(string header, int minWidthAt96, float s, string sample, Font sampleFont)
        {
            int pad = (int)Math.Ceiling(22 * s);
            int width = (int)Math.Ceiling(minWidthAt96 * s);
            int headerW = TextRenderer.MeasureText(header ?? "", _headingFont).Width + pad;
            if (headerW > width)
                width = headerW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, sampleFont).Width + pad;
                if (sampleW > width)
                    width = sampleW;
            }
            return width;
        }

        private void AddBillCol(
            float s, string name, string header, int minWidthAt96,
            DataGridViewContentAlignment alignment, Type valueType,
            string format, IFormatProvider formatProvider, string sample)
        {
            int width = MeasureColumnWidth(header, minWidthAt96, s, sample, _bodyFont);
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

            _billsGrid.Columns.Add(col);
            _baseBillWidths[name] = width;
        }

        private void AddLineCol(
            float s, string name, string header, int minWidthAt96,
            DataGridViewContentAlignment alignment, Type valueType,
            string format, string sample)
        {
            int width = MeasureColumnWidth(header, minWidthAt96, s, sample, _boldGridFont);
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
            if (!string.IsNullOrEmpty(format))
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = ViCulture;
            }

            _linesGrid.Columns.Add(col);
            _baseLineWidths[name] = width;
        }

        private void AdjustColumnWidths(DataGridView grid, Dictionary<string, int> baseWidths, bool measureBold, int maxWidthAt96)
        {
            float s = Dpi;
            int pad = (int)Math.Ceiling(22 * s);
            int maxW = (int)Math.Ceiling(maxWidthAt96 * s);
            Font font = measureBold ? _boldGridFont : _bodyFont;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                int baseWidth;
                if (!baseWidths.TryGetValue(col.Name, out baseWidth))
                    continue;
                int target = baseWidth;
                for (int r = 0; r < grid.Rows.Count; r++)
                {
                    string text = Convert.ToString(grid.Rows[r].Cells[col.Index].FormattedValue);
                    if (string.IsNullOrEmpty(text))
                        continue;
                    int needed = TextRenderer.MeasureText(text, font).Width + pad;
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
            float s = Dpi;
            int minTop = (int)(150 * s);
            int minBottom = (int)(140 * s);
            if (_split.Height > minTop + minBottom + _split.SplitterWidth)
            {
                int want = (int)(_split.Height * 0.52f);
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
                SubtitleText = BuildSubtitle(_scope, _detailKey);
                _bills = _detailKey == null
                    ? new List<ItemSalesBillRow>()
                    : _repository.LoadDetailBills(_scope, _detailKey);

                _billsGrid.Rows.Clear();
                foreach (ItemSalesBillRow bill in _bills)
                {
                    int idx = _billsGrid.Rows.Add(
                        bill.Note ?? "",
                        bill.Name ?? "",
                        (object)bill.Ngay,
                        bill.CustomerName ?? "",
                        bill.CustomerCode ?? "",
                        bill.CustomerAddress ?? "",
                        bill.CustomerPhone ?? "",
                        (object)bill.TotalAmount,
                        (object)bill.ShippingFee,
                        (object)bill.DiscountAmount,
                        (object)bill.DiscountRate,
                        (object)bill.TaxAmount,
                        (object)bill.TaxRate,
                        (object)bill.GoodsAmount,
                        bill.Description ?? "",
                        (object)bill.PaidTime,
                        (object)bill.CashTendered,
                        (object)bill.ChangeAmount,
                        bill.PaidBy ?? "",
                        bill.BookingNumber ?? "",
                        bill.Delivery ?? "",
                        (object)bill.ReturnAmount);
                    _billsGrid.Rows[idx].Tag = bill;
                }
                AdjustColumnWidths(_billsGrid, _baseBillWidths, false, 340);
                _billsEmpty.Visible = _bills.Count == 0;

                _billsGrid.ClearSelection();
                if (_billsGrid.Rows.Count > 0)
                {
                    _billsGrid.CurrentCell = _billsGrid.Rows[0].Cells[0];
                    _billsGrid.Rows[0].Selected = true;
                    ReloadLines();
                }
                else
                {
                    _billsGrid.CurrentCell = null;
                    _lines.Clear();
                    _linesGrid.Rows.Clear();
                    _linesEmpty.Visible = true;
                }
            }
            catch (DataAccessException ex)
            {
                _bills.Clear();
                _billsGrid.Rows.Clear();
                _lines.Clear();
                _linesGrid.Rows.Clear();
                _billsEmpty.Visible = true;
                _linesEmpty.Visible = true;
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được chi tiết hóa đơn: " + ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private ItemSalesBillRow SelectedBill()
        {
            if (_billsGrid.SelectedRows.Count > 0)
                return _billsGrid.SelectedRows[0].Tag as ItemSalesBillRow;
            if (_billsGrid.CurrentRow != null && _billsGrid.CurrentRow.Selected)
                return _billsGrid.CurrentRow.Tag as ItemSalesBillRow;
            return null;
        }

        public void ReloadLines()
        {
            ItemSalesBillRow bill = SelectedBill();
            if (bill == null || string.IsNullOrEmpty(bill.Id))
            {
                _lines.Clear();
                _linesGrid.Rows.Clear();
                _linesEmpty.Visible = true;
                return;
            }

            try
            {
                _lines = _repository.LoadDetailBillLines(bill.Id);
                _linesGrid.Rows.Clear();
                foreach (ItemSalesBillLine line in _lines)
                {
                    int idx = _linesGrid.Rows.Add(
                        line.Note ?? "",
                        line.ProductName ?? "",
                        line.ProductUnitName ?? "",
                        line.ProductCode ?? "",
                        line.ProductWarranty ?? "",
                        line.Barcode ?? "",
                        line.LineWarranty ?? "",
                        (object)line.DiscountRate,
                        (object)line.Amount,
                        (object)line.UnitPrice,
                        (object)line.QuantityOut);
                    _linesGrid.Rows[idx].Tag = line;
                }
                AdjustColumnWidths(_linesGrid, _baseLineWidths, true, 320);
                _linesGrid.ClearSelection();
                _linesEmpty.Visible = _lines.Count == 0;
            }
            catch (DataAccessException ex)
            {
                _lines.Clear();
                _linesGrid.Rows.Clear();
                _linesEmpty.Visible = true;
                if (!SuppressErrorPopup)
                    Msg.Error(this, "Không đọc được dòng chi tiết hóa đơn: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ Tô màu dòng mặt hàng đang xem (§2.0)

        private void LinesGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _linesGrid.Rows.Count)
                return;
            var line = _linesGrid.Rows[e.RowIndex].Tag as ItemSalesBillLine;
            if (line == null)
                return;

            bool isHighlighted = _detailKey != null
                && !string.IsNullOrEmpty(_detailKey.ProductId)
                && string.Equals(line.ProductId, _detailKey.ProductId, StringComparison.Ordinal);

            if (isHighlighted)
            {
                e.CellStyle.ForeColor = _theme.Danger;
                e.CellStyle.SelectionForeColor = _theme.Danger;
                e.CellStyle.SelectionBackColor = Draw.Mix(_theme.Surface, _theme.Danger, 0.14f);
                e.CellStyle.Font = _boldGridFont;
            }
            else
            {
                e.CellStyle.ForeColor = _theme.Text;
                e.CellStyle.Font = _bodyFont;
            }
        }

        // ------------------------------------------------------------------ Xem trước (--render-screen) & Selftest

        public void Preview(string option)
        {
            string[] parts = (option ?? "").Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return;

            DateTime d;
            bool needReload = false;
            if (parts[0] == "from" && DateTime.TryParseExact(parts[1], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                _scope.FromDate = d.Date;
                needReload = true;
            }
            else if (parts[0] == "to" && DateTime.TryParseExact(parts[1], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                _scope.ToDate = d.Date;
                needReload = true;
            }
            else if (parts[0] == "select")
            {
                int idx;
                if (int.TryParse(parts[1], out idx) && idx >= 0)
                {
                    _previewSelectIndex = idx;
                    needReload = true;
                }
            }
            else if (parts[0] == "bill")
            {
                int idx;
                if (int.TryParse(parts[1], out idx) && idx >= 0 && idx < _billsGrid.Rows.Count)
                    SelectBillRow(idx);
            }

            if (needReload && _previewSession != null)
            {
                _detailKey = ResolvePreviewKey(_repository, _scope, _previewSelectIndex);
                ReloadBills();
            }
        }

        internal DataGridView BillsGridForTest { get { return _billsGrid; } }
        internal DataGridView LinesGridForTest { get { return _linesGrid; } }
        internal FlatButton ExitButtonForTest { get { return _btnExit; } }
        internal int BillRowCount { get { return _billsGrid.Rows.Count; } }
        internal int LineRowCount { get { return _linesGrid.Rows.Count; } }

        internal List<string> BillColumnHeaders()
        {
            var list = new List<string>(_billsGrid.Columns.Count);
            foreach (DataGridViewColumn col in _billsGrid.Columns)
                list.Add(col.HeaderText);
            return list;
        }

        internal List<string> LineColumnHeaders()
        {
            var list = new List<string>(_linesGrid.Columns.Count);
            foreach (DataGridViewColumn col in _linesGrid.Columns)
                list.Add(col.HeaderText);
            return list;
        }

        internal int FindBillRowIndexById(string billId)
        {
            for (int i = 0; i < _billsGrid.Rows.Count; i++)
            {
                var b = _billsGrid.Rows[i].Tag as ItemSalesBillRow;
                if (b != null && string.Equals(b.Id, billId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        internal int FindLineRowIndexByProductId(string productId)
        {
            for (int i = 0; i < _linesGrid.Rows.Count; i++)
            {
                var l = _linesGrid.Rows[i].Tag as ItemSalesBillLine;
                if (l != null && string.Equals(l.ProductId, productId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        internal bool SelectBillRow(int index)
        {
            if (index < 0 || index >= _billsGrid.Rows.Count || _billsGrid.Columns.Count == 0)
                return false;
            _billsGrid.CurrentCell = _billsGrid.Rows[index].Cells[0];
            _billsGrid.Rows[index].Selected = true;
            return true;
        }

        internal DataGridViewCellStyle CaptureLineCellStyle(int rowIndex, int colIndex = 1)
        {
            if (rowIndex < 0 || rowIndex >= _linesGrid.Rows.Count || colIndex < 0 || colIndex >= _linesGrid.Columns.Count)
                return null;
            DataGridViewCellStyle captured = null;
            DataGridViewCellFormattingEventHandler spy = delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex == rowIndex && e.ColumnIndex == colIndex && e.CellStyle != null)
                    captured = e.CellStyle.Clone();
            };
            _linesGrid.CellFormatting += spy;
            try
            {
                object unused = _linesGrid.Rows[rowIndex].Cells[colIndex].FormattedValue;
                return captured;
            }
            finally
            {
                _linesGrid.CellFormatting -= spy;
            }
        }

        // ------------------------------------------------------------------ Theme

        protected override void ApplyDialogTheme()
        {
            _theme = ThemeManager.Current;
            base.ApplyDialogTheme();

            _split.BackColor = Draw.StrongBorder(_theme);
            _split.Panel1.BackColor = _theme.Background;
            _split.Panel2.BackColor = _theme.Background;
            _billsTitle.ForeColor = _theme.Primary;
            _linesTitle.ForeColor = _theme.Primary;
            ThemeApplier.ApplyGrid(_billsGrid, _theme);
            ThemeApplier.ApplyGrid(_linesGrid, _theme);
            _billsEmpty.BackColor = _theme.Background;
            _billsEmpty.ForeColor = _theme.TextMuted;
            _linesEmpty.BackColor = _theme.Background;
            _linesEmpty.ForeColor = _theme.TextMuted;
            _billsGrid.Invalidate();
            _linesGrid.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bodyFont.Dispose();
                _headingFont.Dispose();
                _boldGridFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
