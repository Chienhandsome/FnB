using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
    /// Màn "Khách đặt hàng" (docs/specs/khach-dat-hang.md §1.1): panel lọc + cây Phương thức đặt
    /// bên trái; thanh nút + lưới chứng từ bên phải. Mỗi dòng TDATHANG một hàng (§6.2 Q6).
    /// Mở dialog sửa bằng BookingEditDialog (dùng chung với "Theo dõi đặt phòng" sau này).
    /// </summary>
    internal sealed class BookingListView : Panel
    {
        private readonly string _databasePath;
        private readonly BookingRepository _repo;
        private readonly FormRepository _formRepo;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly TextBox _txtNumber = new TextBox();
        // dd/MM/yyyy cố định như app gốc (không theo định dạng ngày của máy, vd. M/d/yyyy).
        private readonly DateTimePicker _dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
        private readonly DateTimePicker _dtTo = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
        private readonly LookupCombo _customer = new LookupCombo();
        private readonly FlatButton _btnFilter = new FlatButton { Text = "Lọc (F3)" };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill };
        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnAdd = new FlatButton { Text = "Thêm mới (Insert)" };
        private readonly FlatButton _btnEdit = new FlatButton { Text = "Chỉnh sửa (F4)" };
        private readonly FlatButton _btnRemove = new FlatButton { Text = "Xóa (Del)", Tag = "danger" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In" };
        private readonly FlatButton _btnTotal = new FlatButton { Text = "Tổng" };
        private readonly FlatButton _btnAnalysis = new FlatButton { Text = "P.tích" };
        private readonly FlatButton _refresh = new FlatButton { Text = "Làm mới" };
        private readonly Label _count = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        // Pane dưới lưới (app gốc: Thông tin + các tab liên quan dựng từ metadata — xem DetailRepository).
        private readonly Panel _bottom = new Panel { Dock = DockStyle.Bottom };
        private readonly TabControl _infoTabs = new TabControl { Dock = DockStyle.Fill };
        private readonly Label _lblCreated = new Label { AutoSize = true };
        private readonly Label _lblCreatedBy = new Label { AutoSize = true };
        private readonly Label _lblModified = new Label { AutoSize = true };
        private readonly Label _lblModifiedBy = new Label { AutoSize = true };
        private readonly ImageList _treeIcons = new ImageList { ColorDepth = ColorDepth.Depth32Bit };
        private DetailTabsHost _detail;

        private List<BookingListRow> _rows = new List<BookingListRow>();
        private bool _canAdd;
        private bool _canEdit;
        private bool _canRemove;
        private bool _loading;

        public BookingListView(string databasePath)
        {
            _databasePath = databasePath;
            _repo = new BookingRepository(databasePath);
            _formRepo = new FormRepository(databasePath);
            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            // Chặn Reload từ AfterSelect của cây trong lúc dựng (lưới chưa có cột).
            _loading = true;

            _canAdd = AppSession.Can(BookingText.FunctionName, RoleModes.Add);
            _canEdit = AppSession.Can(BookingText.FunctionName, RoleModes.Edit);
            _canRemove = AppSession.Can(BookingText.FunctionName, RoleModes.Remove);

            BuildFilterPanel(s);
            BuildBar(s);
            BuildGrid(s);
            BuildBottom(s);

            _split.Panel1.Controls.Add(_tree);
            // Dock theo thứ tự z từ SAU ra TRƯỚC: ô Fill phải ở trước cùng (BringToFront) để không bị panel lọc/tiêu đề đè
            // (trước đây cây bị che nên chỉ thấy nút cuối).
            _tree.BringToFront();
            _split.Panel2.Controls.Add(_grid);
            _split.Panel2.Controls.Add(_bar);
            _split.Panel2.Controls.Add(_bottom);
            _grid.BringToFront();
            _split.SplitterDistance = (int)(300 * s);
            _split.FixedPanel = FixedPanel.Panel1;
            Controls.Add(_split);
            _loading = false;

            _grid.RowPostPaint += DrawRowNumber;
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    EditBooking();
            };
            _grid.SelectionChanged += delegate { if (!_loading) { UpdateButtons(); UpdateInfo(); } };
            _grid.KeyDown += OnKeyDown;
            _tree.KeyDown += OnKeyDown;
            KeyDown += OnKeyDown;

            ThemeManager.ThemeChanged += OnThemeChanged;
            Reload();
        }

        // ------------------------------------------------------------------ Dựng giao diện

        private void BuildFilterPanel(float s)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = (int)(132 * s) };
            AddFilterLabel(panel, "Số phiếu", 8, 10, s);
            _txtNumber.SetBounds((int)(80 * s), (int)(8 * s), (int)(200 * s), (int)(24 * s));
            _txtNumber.Font = new Font("Segoe UI", 10f);
            _txtNumber.KeyDown += OnKeyDown;
            panel.Controls.Add(_txtNumber);

            AddFilterLabel(panel, "Ngày", 8, 42, s);
            var lblFrom = new Label { Text = "Từ:", AutoSize = true };
            lblFrom.SetBounds((int)(80 * s), (int)(44 * s), (int)(28 * s), (int)(20 * s));
            _dtFrom.SetBounds((int)(110 * s), (int)(40 * s), (int)(120 * s), (int)(24 * s));
            _dtFrom.Value = DateTime.Today.AddDays(-30);
            _dtFrom.KeyDown += OnKeyDown;
            var lblTo = new Label { Text = "Đến:", AutoSize = true };
            lblTo.SetBounds((int)(80 * s), (int)(72 * s), (int)(28 * s), (int)(20 * s));
            _dtTo.SetBounds((int)(110 * s), (int)(68 * s), (int)(120 * s), (int)(24 * s));
            _dtTo.Value = DateTime.Today;
            _dtTo.KeyDown += OnKeyDown;
            panel.Controls.Add(lblFrom);
            panel.Controls.Add(_dtFrom);
            panel.Controls.Add(lblTo);
            panel.Controls.Add(_dtTo);

            AddFilterLabel(panel, "Khách hàng", 8, 100, s);
            _customer.SetBounds((int)(80 * s), (int)(98 * s), (int)(200 * s), (int)(24 * s));
            _customer.Font = new Font("Segoe UI", 10f);
            _customer.KeyDown += OnKeyDown;
            panel.Controls.Add(_customer);

            // K4: nút "Lọc (F3)" nằm ở thanh nút bên phải (BuildBar), không ở panel lọc.
            _btnFilter.Tag = "primary";
            _btnFilter.Click += delegate { Reload(); };

            _split.Panel1.Controls.Add(panel);

            var treeTitle = new Label
            {
                Text = "Phương thức đặt",
                Dock = DockStyle.Top,
                Height = (int)(26 * s),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 10f)
            };
            _split.Panel1.Controls.Add(treeTitle);
            _tree.AfterSelect += delegate { if (!_loading) Reload(); };
            LoadTreeNodes();
        }

        private static void AddFilterLabel(Panel panel, string text, int x, int y, float s)
        {
            var lbl = new Label { Text = text, AutoSize = true };
            lbl.SetBounds((int)(x * s), (int)(y * s), (int)(68 * s), (int)(20 * s));
            panel.Controls.Add(lbl);
        }

        // Icon cây như app gốc: Tất cả (quả cầu xanh), Chưa thiết lập (sao vàng), phương thức (thoi tím).
        private const int IconAll = 0;
        private const int IconUnset = 1;
        private const int IconMethod = 2;

        private void EnsureTreeIcons()
        {
            if (_treeIcons.Images.Count > 0)
                return;
            float s = DeviceDpi / 96f;
            int size = Math.Max(16, (int)(16 * s));
            _treeIcons.ImageSize = new Size(size, size);
            for (int kind = 0; kind < 3; kind++)
            {
                var bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    var box = new Rectangle(1, 1, size - 3, size - 3);
                    if (kind == IconAll)
                    {
                        using (var fill = new SolidBrush(Color.FromArgb(60, 150, 220)))
                        using (var line = new Pen(Color.FromArgb(30, 100, 170)))
                        {
                            g.FillEllipse(fill, box);
                            g.DrawEllipse(line, box);
                            g.DrawArc(line, box.X + box.Width / 4, box.Y, box.Width / 2, box.Height, 90, 180);
                            g.DrawLine(line, box.X, box.Y + box.Height / 2, box.Right, box.Y + box.Height / 2);
                        }
                    }
                    else if (kind == IconUnset)
                    {
                        using (var fill = new SolidBrush(Color.FromArgb(240, 190, 40)))
                        {
                            var pts = new PointF[10];
                            float cx = size / 2f, cy = size / 2f, ro = size / 2f - 1, ri = ro * 0.45f;
                            for (int i = 0; i < 10; i++)
                            {
                                double a = -Math.PI / 2 + i * Math.PI / 5;
                                float r = i % 2 == 0 ? ro : ri;
                                pts[i] = new PointF(cx + (float)(r * Math.Cos(a)), cy + (float)(r * Math.Sin(a)));
                            }
                            g.FillPolygon(fill, pts);
                        }
                    }
                    else
                    {
                        using (var fill = new SolidBrush(Color.FromArgb(170, 80, 200)))
                        using (var line = new Pen(Color.FromArgb(110, 40, 140)))
                        {
                            var pts = new[]
                            {
                                new Point(size / 2, 1), new Point(size - 2, size / 2),
                                new Point(size / 2, size - 2), new Point(1, size / 2)
                            };
                            g.FillPolygon(fill, pts);
                            g.DrawPolygon(line, pts);
                        }
                    }
                }
                _treeIcons.Images.Add(bmp);
            }
        }

        private void LoadTreeNodes()
        {
            EnsureTreeIcons();
            _tree.ImageList = _treeIcons;
            _tree.Nodes.Clear();
            _tree.Nodes.Add(new TreeNode("Tất cả") { Tag = null, ImageIndex = IconAll, SelectedImageIndex = IconAll });
            _tree.Nodes.Add(new TreeNode("Chưa thiết lập") { Tag = "", ImageIndex = IconUnset, SelectedImageIndex = IconUnset });
            try
            {
                foreach (LookupItem m in _formRepo.LoadAll("DPHUONGTHUCDAT"))
                    _tree.Nodes.Add(new TreeNode(m.Name) { Tag = m.Id, ImageIndex = IconMethod, SelectedImageIndex = IconMethod });
            }
            catch (DataAccessException)
            {
                // Cây vẫn có Tất cả / Chưa thiết lập; lưới báo lỗi khi nạp.
            }
            try
            {
                _customer.SetItems(_formRepo.LoadAll("DKHACHHANG"));
            }
            catch (DataAccessException)
            {
                // Ô khách hàng để trống; lưới vẫn nạp được.
            }
            _tree.SelectedNode = _tree.Nodes[0];
        }

        private void BuildBottom(float s)
        {
            _bottom.Height = (int)(190 * s);
            var info = new TabPage("Thông tin");
            AddInfoLabel(info, "Khởi tạo:", _lblCreated, 10, 12, 140, s);
            AddInfoLabel(info, "Khởi tạo bởi:", _lblCreatedBy, 340, 12, 140, s);
            AddInfoLabel(info, "Sửa đổi gần nhất:", _lblModified, 10, 42, 140, s);
            AddInfoLabel(info, "Sửa đổi bởi:", _lblModifiedBy, 340, 42, 140, s);
            _infoTabs.TabPages.Add(info);
            _bottom.Controls.Add(_infoTabs);
            _detail = new DetailTabsHost(_infoTabs, _databasePath, "TDATHANG", this);
            _detail.Attach();
        }

        private static void AddInfoLabel(TabPage page, string caption, Label value, int x, int y, int capW, float s)
        {
            var cap = new Label { Text = caption, AutoSize = true };
            cap.Location = new Point((int)(x * s), (int)(y * s));
            value.Location = new Point((int)((x + capW) * s), (int)(y * s));
            page.Controls.Add(cap);
            page.Controls.Add(value);
        }

        private void UpdateInfo()
        {
            string id = CurrentBookingId();
            if (_detail != null)
                _detail.SetMaster(id);
            var row = _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as BookingListRow;
            _lblCreated.Text = row == null ? "" : FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row == null ? "" : (row.CreatedByName ?? "");
            _lblModified.Text = row == null ? "" : FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row == null ? "" : (row.ModifiedByName ?? "");
        }

        private static string FormatAuditTime(DateTime? t)
        {
            return t.HasValue ? t.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "";
        }

        private void BuildBar(float s)
        {
            _bar.Height = (int)(52 * s);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding((int)(8 * s), (int)(8 * s), (int)(8 * s), (int)(8 * s))
            };
            _btnAdd.Tag = null;
            _btnRemove.Tag = "danger";
            foreach (FlatButton b in new[] { _btnFilter, _btnAdd, _btnEdit, _btnRemove, _btnExcel, _btnPrint, _btnTotal, _btnAnalysis, _refresh })
            {
                b.Height = (int)(34 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                b.Font = new Font("Segoe UI", 10f);
                // Rộng theo chữ (trước đây nút bị cắt "Thêm ...", "Chỉnh ...").
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(28 * s);
                flow.Controls.Add(b);
            }
            _btnAdd.Click += delegate { AddBooking(); };
            _btnEdit.Click += delegate { EditBooking(); };
            _btnRemove.Click += delegate { DeleteBooking(); };
            _btnExcel.Click += delegate { Msg.Info(this, BookingText.Unsupported); };
            _btnPrint.Click += delegate { Msg.Info(this, BookingText.Unsupported); };
            _btnTotal.Click += delegate { Msg.Info(this, BookingText.Unsupported); };
            _btnAnalysis.Click += delegate { Msg.Info(this, BookingText.Unsupported); };
            _refresh.Click += delegate { Reload(); };
            _count.Width = (int)(150 * s);
            _count.Dock = DockStyle.Right;
            _count.Font = new Font("Segoe UI", 9.5f);
            _bar.Controls.Add(flow);
            _bar.Controls.Add(_count);
        }

        private void BuildGrid(float s)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.RowTemplate.Height = (int)(28 * s);
            _grid.Font = new Font("Segoe UI", 10f);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            AddGridColumn("colDate", "Ngày", 90, typeof(DateTime), "dd/MM/yyyy");
            AddGridColumn("colNumber", "Số phiếu", 110, typeof(string), null);
            AddGridColumn("colCustomer", "Tên khách", 160, typeof(string), null);
            AddGridColumn("colAddress", "Địa chỉ", 170, typeof(string), null);
            AddGridColumn("colPhone", "Điện thoại", 110, typeof(string), null);
            AddGridColumn("colEmail", "Email", 150, typeof(string), null);
            AddGridColumn("colTotal", "Tổng cộng", 110, typeof(decimal), "N0");
            AddGridColumn("colMethod", "Phương thức đặt", 110, typeof(string), null);
            AddGridColumn("colPurpose", "Mục đích đặt", 130, typeof(string), null);
            AddGridColumn("colFromTime", "Từ giờ", 80, typeof(DateTime), "HH:mm");
            AddGridColumn("colToTime", "Đến giờ", 80, typeof(DateTime), "HH:mm");
            AddGridColumn("colBookTime", "Giờ đặt", 130, typeof(DateTime), "dd/MM/yyyy HH:mm");
            AddGridColumn("colRoom", "Bàn", 110, typeof(string), null);
            AddGridColumn("colCustomerName", "Khách hàng", 130, typeof(string), null);
            AddGridColumn("colGoods", "Tiền hàng", 110, typeof(decimal), "N0");
            AddGridColumn("colDiscount", "Tiền giảm giá", 110, typeof(decimal), "N0");
            AddGridColumn("colDiscountPct", "Tỉ lệ giảm giá", 80, typeof(decimal), "#,##0.##");
            AddGridColumn("colNote", "Ghi chú", 180, typeof(string), null);
            _grid.Columns["colTotal"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["colGoods"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["colDiscount"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["colDiscountPct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["colFromTime"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns["colToTime"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
            ApplyTheme(ThemeManager.Current);
        }

        private void AddGridColumn(string name, string header, int width96, Type type, string format)
        {
            float s = DeviceDpi / 96f;
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = (int)(width96 * s),
                ValueType = type,
                ReadOnly = true
            };
            if (format != null)
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = new CultureInfo("vi-VN");
            }
            _grid.Columns.Add(col);
        }

        private void PositionEmpty()
        {
            _empty.SetBounds(0, _grid.Height / 2 - 15, _grid.Width, 30);
        }

        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_grid.RowHeadersVisible)
                return;
            string text = (e.RowIndex + 1).ToString();
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, ThemeManager.Current.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        // ------------------------------------------------------------------ Nạp + thao tác

        private BookingFilter CurrentFilter()
        {
            string method = null;
            if (_tree.SelectedNode != null)
                method = _tree.SelectedNode.Tag as string;
            return new BookingFilter
            {
                NumberText = _txtNumber.Text,
                FromDate = _dtFrom.Value.Date,
                ToDate = _dtTo.Value.Date,
                CustomerId = _customer.SelectedId,
                MethodId = method
            };
        }

        private void Reload()
        {
            _loading = true;
            try
            {
                _rows = _repo.LoadBookings(CurrentFilter());
                _empty.Visible = _rows.Count == 0;
                _empty.Text = "Không có phiếu đặt nào trong khoảng lọc.";
            }
            catch (DataAccessException ex)
            {
                _rows = new List<BookingListRow>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được dữ liệu: " + ex.Message;
            }
            FillGrid();
            _count.Text = _rows.Count + " phiếu";
            _loading = false;
            UpdateButtons();
            UpdateInfo();
            ApplyTheme(ThemeManager.Current);
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();
            foreach (BookingListRow row in _rows)
            {
                int i = _grid.Rows.Add(
                    row.Ngay, row.Name, row.TenKhach, row.DiaChi, row.DienThoai, row.Email,
                    row.TongCong, row.MethodName, row.PurposeName,
                    row.TuGio, row.DenGio, row.GioDat, row.RoomName, row.CustomerName,
                    row.TienHang, row.TienGiamGia, row.TileGiamGia, row.Note);
                _grid.Rows[i].Tag = row;
            }
            _grid.ClearSelection();
        }

        private List<string> RowIds()
        {
            var ids = new List<string>(_rows.Count);
            foreach (BookingListRow r in _rows)
                ids.Add(r.Id);
            return ids;
        }

        private string CurrentBookingId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var row = _grid.CurrentRow.Tag as BookingListRow;
            return row == null ? null : row.Id;
        }

        private void AddBooking()
        {
            if (!_canAdd)
                return;
            using (var dlg = new BookingEditDialog(AppSession.Current, null, RowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void EditBooking()
        {
            if (!_canEdit) // K5: thiếu quyền Sửa thì không mở dialog "SỬA".
                return;
            string id = CurrentBookingId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new BookingEditDialog(AppSession.Current, id, RowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void DeleteBooking()
        {
            if (!_canRemove)
                return;
            string id = CurrentBookingId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!Msg.Confirm(this, BookingText.DeleteConfirm))
                return;
            BookingDeleteResult result;
            try
            {
                result = _repo.DeleteBooking(id);
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
            ReloadAndSelect(null);
        }

        private void ReloadAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
            {
                foreach (DataGridViewRow r in _grid.Rows)
                {
                    var row = r.Tag as BookingListRow;
                    if (row != null && row.Id == id)
                    {
                        r.Selected = true;
                        _grid.CurrentCell = r.Cells[0];
                        break;
                    }
                }
            }
        }

        private void UpdateButtons()
        {
            bool hasRow = CurrentBookingId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Đang gõ trong ô nhập thì Insert/Delete/F4 là của ô đó (vd. F4 mở dropdown), không phải phím màn hình.
            bool inEditor = sender is TextBox || sender is ComboBox || sender is DateTimePicker;
            if (e.KeyCode == Keys.F3)
            {
                // K4: F3 chạy lọc (như nút "Lọc (F3)"), rồi đưa con trỏ về ô Số phiếu.
                Reload();
                _txtNumber.Focus();
                _txtNumber.SelectAll();
                e.Handled = true;
                return;
            }
            if (inEditor)
                return;
            if (e.KeyCode == Keys.Insert && _canAdd)
            {
                AddBooking();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4)
            {
                EditBooking();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && _canRemove)
            {
                DeleteBooking();
                e.Handled = true;
            }
        }

        // ------------------------------------------------------------------ Giao diện

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
            _bar.BackColor = t.Background;
            _count.BackColor = Color.Transparent;
            _count.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = t.TextMuted;
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _bottom.BackColor = t.Background;
            _infoTabs.BackColor = t.Background;
            _infoTabs.ForeColor = t.Text;
            foreach (TabPage p in _infoTabs.TabPages)
                p.BackColor = t.Background;
            if (_detail != null)
                _detail.ApplyTheme(t);
            _refresh.SurfaceColor = t.Background;
            _refresh.FillColor = t.Surface;
            _refresh.TextColor = t.Text;
            _refresh.BorderColor = Draw.StrongBorder(t);
            _refresh.Invalidate();
            ThemeApplier.ApplyGrid(_grid, t);
            UpdateButtons();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _treeIcons.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
