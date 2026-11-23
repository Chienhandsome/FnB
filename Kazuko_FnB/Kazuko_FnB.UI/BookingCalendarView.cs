using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Theo dõi đặt phòng": lưới lịch Gantt (dòng = phòng theo loại phòng, cột = ngày),
    /// kéo thả/co giãn thanh đặt phòng, menu chuột phải, mở dialog đặt phòng dùng chung
    /// với chức năng Khách đặt hàng. Spec: <c>docs/specs/theo-doi-dat-phong.md</c>.
    ///
    /// Bố cục (spec §1.1, ảnh 01): panel trái-trên 200×44 chứa ngày xem + 4 nút /4 /2 2x 4x
    /// + combo lọc loại phòng (rỗng = tất cả), đè lên 2 hàng tiêu đề của lưới; lưới có 2 cột
    /// cố định (loại phòng 90, phòng 109), 2 hàng tiêu đề cố định (tháng + ngày), cột Tổng 40.
    /// Quyết định của Chiến 23/11: giữ y hệt app gốc (§5.1–§5.6) — xem ghi chú ở từng chỗ.
    /// </summary>
    internal sealed class BookingCalendarView : Panel
    {
        /// <summary>ID menu trong SMENU ("Theo dõi đặt phòng").</summary>
        public const string MenuId = "a5142f0f-bc85-4b6e-b1da-6c4436e07a3f";

        // Kích thước ở 96 DPI (spec §1.1); nhân với DeviceDpi/96 khi dùng.
        private const int TypeColWidth96 = 90;
        private const int RoomColWidth96 = 109;
        private const int TotalColWidth96 = 40;
        private const int HeaderRowHeight96 = 24;
        private const int DataRowHeight96 = 26;
        private const int TopPanelWidth96 = 200;
        private const int TopPanelHeight96 = 44;

        // Màu cố định của app gốc (không có trong theme token — ghi rõ nguồn ở đây).
        private static readonly Color MonthHeaderBack = Color.FromArgb(112, 128, 144); // SlateGray
        private static readonly Color GroupRowBack = Color.FromArgb(105, 105, 105);   // DimGray
        private static readonly Color TotalRowBack = Color.FromArgb(255, 255, 224);    // LightYellow
        private static readonly Color TodayBack = Color.FromArgb(211, 211, 211);       // LightGray
        private static readonly Color RoomNameBack = Color.FromArgb(0, 128, 255);      // xanh ô tên phòng
        private static readonly Color DragGhostBack = Color.FromArgb(176, 196, 222);   // LightSteelBlue
        private static readonly Color TotalRowFore = Color.FromArgb(139, 0, 0);        // đỏ đậm

        private readonly string _databasePath;
        private readonly BookingCalendarRepository _repo;
        private readonly BookingRepository _bookingRepo;
        private readonly bool _canAdd;
        private readonly bool _canEdit;
        private readonly bool _canRemove;

        private readonly Panel _topPanel;
        private readonly DateTimePicker _dtNgay;
        private readonly LookupCombo _cboType;
        private readonly CalendarGrid _grid;
        private readonly ThemedContextMenu _menu;
        private readonly ThemedContextMenu _dayMenu;
        private readonly ToolStripMenuItem _daySubMenu;

        private DateTime _from = DateTime.Today;
        private int _days = 7;
        private string _typeId = "";
        private CalendarData _data = new CalendarData();
        private BookingCalendarRules.CalendarTotals _totals;
        private bool _loadingTypeCombo;

        public BookingCalendarView(string databasePath)
        {
            _databasePath = databasePath;
            _repo = new BookingCalendarRepository(databasePath);
            _bookingRepo = new BookingRepository(databasePath);
            // Quyền dùng chung chức năng "Đặt hàng" (spec §4 + Q7 mặc định tạm).
            _canAdd = AppSession.Can(BookingText.FunctionName, RoleModes.Add);
            _canEdit = AppSession.Can(BookingText.FunctionName, RoleModes.Edit);
            _canRemove = AppSession.Can(BookingText.FunctionName, RoleModes.Remove);

            float s = DeviceDpi / 96f;
            Dock = DockStyle.Fill;
            BackColor = ThemeManager.Current.Background;

            _grid = new CalendarGrid(this)
            {
                Dock = DockStyle.Fill,
                TabStop = true
            };
            Controls.Add(_grid);

            _topPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size((int)(TopPanelWidth96 * s), (int)(TopPanelHeight96 * s)),
                BackColor = ThemeManager.Current.Surface
            };
            _dtNgay = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = _from,
                Location = new Point((int)(1 * s), (int)(1 * s)),
                Size = new Size((int)(95 * s), (int)(20 * s)),
                Font = new Font("Segoe UI", 10f)
            };
            _dtNgay.ValueChanged += delegate { _from = _dtNgay.Value.Date; Reload(); };
            _topPanel.Controls.Add(_dtNgay);

            string[] dayOps = { "/4", "/2", "2x", "4x" };
            int[] dayOpX = { 95, 121, 147, 173 };
            for (int i = 0; i < dayOps.Length; i++)
            {
                string op = dayOps[i];
                var btn = new FlatButton
                {
                    Text = op,
                    Location = new Point((int)(dayOpX[i] * s), 0),
                    Size = new Size((int)(27 * s), (int)(23 * s)),
                    Font = new Font("Segoe UI", 9f),
                    FillColor = ThemeManager.Current.Primary,
                    TextColor = ThemeManager.Current.OnPrimary,
                    SurfaceColor = ThemeManager.Current.Surface,
                    Radius = 4
                };
                btn.Click += delegate { _days = BookingCalendarRules.ChangeDaysCount(_days, op); Reload(); };
                btn.MouseUp += DayButton_MouseUp;
                _topPanel.Controls.Add(btn);
            }

            _cboType = new LookupCombo
            {
                Location = new Point((int)(1 * s), (int)(23 * s)),
                Size = new Size((int)(198 * s), (int)(21 * s)),
                Font = new Font("Segoe UI", 10f)
            };
            _cboType.ValueChanged += delegate
            {
                if (_loadingTypeCombo)
                    return;
                _typeId = _cboType.SelectedId ?? "";
                Reload();
            };
            _topPanel.Controls.Add(_cboType);
            Controls.Add(_topPanel);
            _topPanel.BringToFront();

            // Menu chuột phải trên lưới (spec §2.2.4).
            _menu = new ThemedContextMenu();
            _menu.AddItem("Tạo đặt phòng", Keys.Insert, delegate { CreateBooking(); });
            _menu.AddItem("Sửa đặt phòng", Keys.F4, delegate { EditSelected(); });
            _menu.AddItem("Xóa đặt phòng", Keys.Delete, delegate { DeleteSelected(); });
            _menu.AddSeparator();
            _menu.AddItem("Tìm kiếm đặt trước", delegate { Msg.Warning(this, CalendarText.Unsupported); });
            _menu.AddSeparator();
            _menu.AddItem("Làm mới", Keys.F5, delegate { Reload(); });
            _menu.Opening += Menu_Opening;
            _grid.ContextMenuStrip = _menu;

            // Menu chuột phải trên nút số ngày: "Số ngày xem" (spec §2.1).
            _dayMenu = new ThemedContextMenu();
            _daySubMenu = new ToolStripMenuItem("Số ngày xem") { Font = new Font("Segoe UI", 10f) };
            foreach (int d in new[] { 7, 15, 30, 60, 90 })
            {
                int days = d;
                var item = new ToolStripMenuItem(days.ToString(CultureInfo.InvariantCulture) + " ngày")
                {
                    Font = new Font("Segoe UI", 10f)
                };
                item.Click += delegate { _days = days; Reload(); };
                _daySubMenu.DropDownItems.Add(item);
            }
            _daySubMenu.DropDownItems.Add(new ToolStripSeparator());
            AddMonthItem("Tháng này", 0);
            AddMonthItem("Tháng trước", -1);
            AddMonthItem("Tháng sau", 1);
            _dayMenu.Items.Add(_daySubMenu);
            _dayMenu.Opening += delegate
            {
                _daySubMenu.DropDown.Renderer = new ThemedMenuRenderer(ThemeManager.Current);
                foreach (ToolStripItem item in _daySubMenu.DropDownItems)
                {
                    var menuItem = item as ToolStripMenuItem;
                    if (menuItem != null && menuItem.Text.EndsWith(" ngày", StringComparison.Ordinal))
                    {
                        int d;
                        menuItem.Checked = int.TryParse(menuItem.Text.Replace(" ngày", ""),
                            out d) && d == _days;
                    }
                }
            };

            ThemeManager.ThemeChanged += OnThemeChanged;
            Reload();
        }

        private void AddMonthItem(string text, int offsetMonths)
        {
            var item = new ToolStripMenuItem(text) { Font = new Font("Segoe UI", 10f) };
            item.Click += delegate
            {
                DateTime from;
                int days;
                BookingCalendarRules.MonthViewRange(DateTime.Today, offsetMonths, out from, out days);
                _from = from;
                _days = days;
                _dtNgay.Value = _from;
                Reload();
            };
            _daySubMenu.DropDownItems.Add(item);
        }

        internal bool CanEditCalendar { get { return _canEdit; } }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            BackColor = ThemeManager.Current.Background;
            _topPanel.BackColor = ThemeManager.Current.Surface;
            Invalidate(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void DayButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                _dayMenu.Show((Control)sender, e.Location);
        }

        /// <summary>Tải lại lưới, giữ vị trí cuộn (spec §2.1).</summary>
        public void Reload()
        {
            try
            {
                _data = _repo.LoadCalendar(_from, _days, _typeId);
                _totals = BookingCalendarRules.BuildTotals(_data, _from, _days);
                RefreshTypeCombo();
                _grid.SetData(_data, _totals, _from, _days);
            }
            catch (FbException ex)
            {
                Msg.Error(this, "Không thể tải dữ liệu đặt phòng: " + ex.Message);
            }
        }

        private void RefreshTypeCombo()
        {
            var items = new List<LookupItem>();
            foreach (CalendarRoomType type in _data.Types)
                items.Add(new LookupItem { Id = type.Id, Name = type.Name });
            // SetItems/SelectId đều bắn ValueChanged — chặn để không Reload đệ quy.
            _loadingTypeCombo = true;
            try
            {
                string selected = _cboType.SelectedId;
                _cboType.SetItems(items);
                // Rỗng = tất cả (ảnh 01: combo trống khi không lọc).
                _cboType.SelectId(string.IsNullOrEmpty(selected) ? "" : selected);
            }
            finally
            {
                _loadingTypeCombo = false;
            }
        }

        private void Menu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool hasBar = _grid.SelectedBar != null;
            bool hasRegion = _grid.HasRegion;
            _menu.Items[0].Enabled = _canAdd && hasRegion;
            _menu.Items[1].Enabled = _canEdit && hasBar;
            _menu.Items[2].Enabled = _canRemove && hasBar;
            _menu.Items[4].Enabled = true;
            _menu.Items[6].Enabled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // F4 trên DateTimePicker/ComboBox là mở dropdown của chính nó — nhường cho control.
            Form form = FindForm();
            Control focused = form == null ? null : form.ActiveControl;
            bool focusOnPicker = focused == _dtNgay || focused == _cboType;
            if (e.KeyCode == Keys.Insert)
            {
                CreateBooking();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4 && !focusOnPicker)
            {
                EditSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                Reload();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void CreateBooking()
        {
            if (!_canAdd)
                return;
            BookingCalendarPreset preset;
            if (!_grid.TryBuildPreset(out preset))
            {
                Msg.Warning(this, CalendarText.SelectAreaFirst);
                return;
            }
            OpenBookingDialog(null, preset);
        }

        private void EditSelected()
        {
            if (!_canEdit)
                return;
            CalendarBookingBar bar = _grid.SelectedBar;
            if (bar == null)
                return;
            OpenBookingDialog(bar.Id, null);
        }

        private void DeleteSelected()
        {
            if (!_canRemove)
                return;
            CalendarBookingBar bar = _grid.SelectedBar;
            if (bar == null)
                return;
            if (!Msg.Confirm(this, CalendarText.DeleteConfirm))
                return;
            // Xóa cứng cả đoàn, chặn khi đã đặt tiền hoặc đã tạo hóa đơn (giống Khách đặt hàng).
            BookingDeleteResult result = _bookingRepo.DeleteBooking(bar.Id);
            if (!result.Ok)
            {
                // Thông báo của repo dùng chữ "đặt hàng"; màn lịch dùng chữ "đặt phòng" (§5.6).
                string text = result.ErrorMessage == BookingText.DeleteBlocked
                    ? CalendarText.DeleteBlocked
                    : result.ErrorMessage;
                Msg.Warning(this, text);
                return;
            }
            Reload();
        }

        /// <summary>
        /// Mở dialog đặt phòng dùng chung với Khách đặt hàng, ở chế độ "từ lịch":
        /// không có thanh công cụ Trước/Sau (spec §1.4).
        /// </summary>
        private void OpenBookingDialog(string bookingId, BookingCalendarPreset preset)
        {
            using (var dlg = new BookingEditDialog(AppSession.Current, bookingId, null, true, preset))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    Reload();
            }
        }

        /// <summary>Nhấp vào thanh vượt ngoài cửa sổ: hỏi mở dialog sửa (spec §2.2.3).</summary>
        internal void AskOpenOutOfWindowBar(CalendarBookingBar bar)
        {
            if (Msg.Confirm(this, CalendarText.OutOfWindow))
                OpenBookingDialog(bar.Id, null);
        }

        /// <summary>Kéo thả xong: xác nhận rồi ghi 1 transaction (spec §2.2.2, §3.4).</summary>
        internal void CommitDrag(CalendarBookingBar bar, string newRoomId, string newRoomName,
            DateTime newTuNgay, DateTime newDenNgay)
        {
            List<string> changes = BookingCalendarRules.BuildDragChanges(
                bar.RoomName, newRoomName, bar.TuNgay, bar.DenNgay, newTuNgay, newDenNgay);
            if (changes.Count == 0)
                return;
            string message = BookingCalendarRules.BuildDragConfirmMessage(bar.TenKhach, changes);
            if (!Msg.Confirm(this, message))
            {
                _grid.Invalidate();
                return;
            }
            BookingCalendarMoveResult result = _repo.ApplyDragMove(
                bar.Id, newRoomId, newTuNgay.Date, newDenNgay.Date, AppSession.UserId ?? "");
            if (!result.Ok)
            {
                Msg.Error(this, result.ErrorMessage);
            }
            Reload();
        }

        /// <summary>Tooltip của một thanh (spec §2.2.1): tên khách, từ/đến ngày, mục đích, yêu cầu khác.</summary>
        internal static string BuildBarTooltip(CalendarBookingBar bar)
        {
            return bar.TenKhach +
                   "\nTừ ngày: " + bar.TuNgay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +
                   "\nĐến ngày: " + bar.DenNgay.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +
                   "\nMục đích: " + bar.PurposeName +
                   "\nYêu cầu khác: " + bar.Note;
        }

        internal static Color ColorFromArgbOr(int argb, Color fallback)
        {
            // ARGB 0 = không đặt màu: dùng màu mặc định. (Màu có alpha = 0 nhưng RGB khác 0
            // không xảy ra trong dữ liệu thật; coi như không đặt.)
            if (argb == 0)
                return fallback;
            Color c = Color.FromArgb(argb);
            return c.A == 0 ? fallback : c;
        }

        /// <summary>Lưới lịch vẽ tay: 2 cột cố định + 2 hàng tiêu đề cố định, thanh đặt phòng kéo thả được.</summary>
        private sealed class CalendarGrid : Panel
        {
            private enum RowKind { Group, Room, Total }
            private enum TotalKind { Sold, Remaining, Total, Capacity }

            private sealed class GridRow
            {
                public RowKind Kind;
                public CalendarRoomType Type;   // Group + Room
                public CalendarRoom Room;       // Room
                public TotalKind Total;         // Total
                public string Label;            // Total
                public int Top;                 // y trong content
            }

            private sealed class HitInfo
            {
                public bool IsBar;
                public CalendarBookingBar Bar;
                public int RowIndex = -1;
                public int RoomIndex = -1;
                public int DayIndex = -1; // -1 = không phải cột ngày, -2 = cột Tổng
                public bool IsLeftHandle;
                public bool IsRightHandle;
            }

            private readonly BookingCalendarView _owner;
            private CalendarData _data = new CalendarData();
            private BookingCalendarRules.CalendarTotals _totals;
            private DateTime _from = DateTime.Today;
            private int _days = 7;
            private List<GridRow> _rows = new List<GridRow>();
            private List<GridRow> _roomRows = new List<GridRow>();
            private bool _rowsDirty = true;
            private readonly ToolTip _tooltip = new ToolTip();
            private readonly Font _font = new Font("Segoe UI", 10f);
            private readonly Font _fontBold = new Font("Segoe UI", 10f, FontStyle.Bold);

            private string _selectedBarId;
            private bool _hasRegion;
            private int _regionFirstRow, _regionLastRow, _regionFirstDay, _regionLastDay;

            private bool _dragCandidate;
            private bool _dragging;
            private CalendarBookingBar _dragBar;
            private int _dragMode; // 0 = di chuyển, -1 = co đầu trái, 1 = co đầu phải
            private Point _dragStartPoint;
            private int _dragStartRoomIndex;
            private int _dragStartFirstDay, _dragStartLastDay;
            private int _dragRoomIndex, _dragFirstDay, _dragLastDay;

            private bool _selecting;
            private int _selectAnchorRow, _selectAnchorDay;
            private string _hoverBarId;

            public CalendarGrid(BookingCalendarView owner)
            {
                _owner = owner;
                DoubleBuffered = true;
                AutoScroll = true;
                BackColor = ThemeManager.Current.Background;
                ThemeManager.ThemeChanged += OnThemeChanged;
            }

            private void OnThemeChanged(object sender, EventArgs e)
            {
                BackColor = ThemeManager.Current.Background;
                Invalidate();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    ThemeManager.ThemeChanged -= OnThemeChanged;
                    _font.Dispose();
                    _fontBold.Dispose();
                    _tooltip.Dispose();
                }
                base.Dispose(disposing);
            }

            private float S { get { return DeviceDpi / 96f; } }
            private int TypeW { get { return (int)(TypeColWidth96 * S); } }
            private int RoomW { get { return (int)(RoomColWidth96 * S); } }
            private int TotalW { get { return (int)(TotalColWidth96 * S); } }
            private int HeaderH { get { return (int)(HeaderRowHeight96 * S); } }
            private int RowH { get { return (int)(DataRowHeight96 * S); } }
            private int LeftW { get { return TypeW + RoomW; } }
            private int HeaderH2 { get { return HeaderH * 2; } }
            private int DayW
            {
                get
                {
                    int w96 = BookingCalendarRules.DayColumnWidth((int)(ClientSize.Width / S), _days);
                    return (int)(w96 * S);
                }
            }
            private int ContentW { get { return LeftW + _days * DayW + TotalW; } }
            private int ContentH { get { return HeaderH2 + _rows.Count * RowH; } }

            public CalendarBookingBar SelectedBar
            {
                get { return _selectedBarId == null ? null : FindBar(_selectedBarId); }
            }

            public bool HasRegion { get { return _hasRegion; } }

            private CalendarBookingBar FindBar(string id)
            {
                foreach (CalendarBookingBar bar in _data.Bars)
                    if (bar.Id == id)
                        return bar;
                return null;
            }

            public void SetData(CalendarData data, BookingCalendarRules.CalendarTotals totals,
                DateTime from, int days)
            {
                Point scroll = AutoScrollPosition;
                _data = data;
                _totals = totals;
                _from = from;
                _days = days;
                _rowsDirty = true;
                if (_selectedBarId != null && FindBar(_selectedBarId) == null)
                    _selectedBarId = null;
                _hasRegion = false;
                _dragCandidate = false;
                _dragging = false;
                _dragBar = null;
                _selecting = false;
                RebuildRows();
                AutoScrollMinSize = new Size(ContentW, ContentH);
                // Giữ vị trí cuộn (AutoScrollPosition nhận giá trị dương).
                AutoScrollPosition = new Point(Math.Abs(scroll.X), Math.Abs(scroll.Y));
                Invalidate();
            }

            private void RebuildRows()
            {
                _rows = new List<GridRow>();
                _roomRows = new List<GridRow>();
                foreach (CalendarRoomType type in _data.Types)
                {
                    _rows.Add(new GridRow { Kind = RowKind.Group, Type = type });
                    foreach (CalendarRoom room in _data.Rooms)
                    {
                        if (room.TypeId == type.Id)
                        {
                            var row = new GridRow { Kind = RowKind.Room, Room = room, Type = type };
                            _rows.Add(row);
                            _roomRows.Add(row);
                        }
                    }
                }
                _rows.Add(new GridRow { Kind = RowKind.Total, Total = TotalKind.Sold, Label = "Đã bán" });
                _rows.Add(new GridRow { Kind = RowKind.Total, Total = TotalKind.Remaining, Label = "Còn lại" });
                _rows.Add(new GridRow { Kind = RowKind.Total, Total = TotalKind.Total, Label = "Tổng cộng" });
                _rows.Add(new GridRow { Kind = RowKind.Total, Total = TotalKind.Capacity, Label = "Công suất" });
                for (int i = 0; i < _rows.Count; i++)
                    _rows[i].Top = HeaderH2 + i * RowH;
                _rowsDirty = false;
            }

            /// <summary>Dữ liệu mồi cho dialog đặt phòng từ vùng ô đã chọn (spec §1.4).</summary>
            public bool TryBuildPreset(out BookingCalendarPreset preset)
            {
                preset = null;
                if (!_hasRegion)
                    return false;
                var roomIds = new List<string>();
                var roomNames = new List<string>();
                for (int r = _regionFirstRow; r <= _regionLastRow; r++)
                {
                    GridRow row = _rows[r];
                    if (row.Kind == RowKind.Room)
                    {
                        roomIds.Add(row.Room.Id);
                        roomNames.Add(row.Room.Name);
                    }
                }
                if (roomIds.Count == 0)
                    return false;
                // Giờ vào/ra = ngày min/max của vùng (00:00) — spec §3.5.
                preset = new BookingCalendarPreset
                {
                    RoomIds = roomIds,
                    RoomNames = roomNames,
                    TuGio = _from.AddDays(_regionFirstDay),
                    DenGio = _from.AddDays(_regionLastDay)
                };
                return true;
            }

            private Point ContentPoint(Point clientPt)
            {
                return new Point(clientPt.X - AutoScrollPosition.X, clientPt.Y - AutoScrollPosition.Y);
            }

            private HitInfo HitTest(Point clientPt)
            {
                Point p = ContentPoint(clientPt);
                var hit = new HitInfo();
                if (p.Y < HeaderH2 || p.X < 0 || p.Y >= HeaderH2 + _rows.Count * RowH)
                    return hit;
                int rowIndex = (p.Y - HeaderH2) / RowH;
                if (rowIndex < 0 || rowIndex >= _rows.Count)
                    return hit;
                hit.RowIndex = rowIndex;
                GridRow row = _rows[rowIndex];
                if (row.Kind == RowKind.Room)
                {
                    for (int i = 0; i < _roomRows.Count; i++)
                    {
                        if (_roomRows[i] == row)
                        {
                            hit.RoomIndex = i;
                            break;
                        }
                    }
                }
                if (p.X < LeftW)
                    return hit;
                int x = p.X - LeftW;
                if (x < _days * DayW)
                    hit.DayIndex = x / DayW;
                else if (x < _days * DayW + TotalW)
                    hit.DayIndex = -2;
                if (row.Kind == RowKind.Room && hit.DayIndex >= 0)
                {
                    foreach (CalendarBookingBar bar in _data.Bars)
                    {
                        if (bar.RoomId != row.Room.Id)
                            continue;
                        int first, last;
                        if (!BookingCalendarRules.TryClipBar(bar.TuNgay, bar.DenNgay, _from, _days, out first, out last))
                            continue;
                        if (hit.DayIndex >= first && hit.DayIndex <= last)
                        {
                            hit.IsBar = true;
                            hit.Bar = bar;
                            int barLeft = LeftW + first * DayW;
                            int barRight = LeftW + (last + 1) * DayW;
                            int handle = (int)(8 * S);
                            hit.IsLeftHandle = p.X - barLeft <= handle;
                            hit.IsRightHandle = barRight - p.X <= handle;
                            break;
                        }
                    }
                }
                return hit;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                Focus();
                _tooltip.Hide(this);
                HitInfo hit = HitTest(e.Location);
                if (e.Button == MouseButtons.Right)
                {
                    if (hit.IsBar)
                    {
                        _selectedBarId = hit.Bar.Id;
                        _hasRegion = false;
                        Invalidate();
                    }
                    else if (hit.RowIndex >= 0 && _rows[hit.RowIndex].Kind == RowKind.Room && hit.DayIndex >= 0)
                    {
                        _regionFirstRow = _regionLastRow = hit.RowIndex;
                        _regionFirstDay = _regionLastDay = hit.DayIndex;
                        _hasRegion = true;
                        _selectedBarId = null;
                        Invalidate();
                    }
                    return; // ContextMenuStrip tự hiện.
                }
                if (e.Button != MouseButtons.Left)
                    return;
                if (hit.IsBar)
                {
                    bool wasSelected = _selectedBarId == hit.Bar.Id;
                    _selectedBarId = hit.Bar.Id;
                    _hasRegion = false;
                    Invalidate();
                    if (BookingCalendarRules.IsOutOfWindow(hit.Bar.TuNgay, hit.Bar.DenNgay, _from, _days))
                    {
                        // Thanh vượt ngoài cửa sổ: không kéo thả được — hỏi mở dialog sửa (§2.2.3).
                        if (!wasSelected)
                            _owner.AskOpenOutOfWindowBar(hit.Bar);
                        return;
                    }
                    if (!_owner.CanEditCalendar)
                        return;
                    _dragCandidate = true;
                    _dragBar = hit.Bar;
                    _dragMode = hit.IsLeftHandle ? -1 : (hit.IsRightHandle ? 1 : 0);
                    _dragStartPoint = ContentPoint(e.Location);
                    _dragStartRoomIndex = hit.RoomIndex;
                    int first, last;
                    BookingCalendarRules.TryClipBar(hit.Bar.TuNgay, hit.Bar.DenNgay, _from, _days, out first, out last);
                    _dragStartFirstDay = first;
                    _dragStartLastDay = last;
                    _dragRoomIndex = _dragStartRoomIndex;
                    _dragFirstDay = first;
                    _dragLastDay = last;
                    return;
                }
                if (hit.RowIndex >= 0 && _rows[hit.RowIndex].Kind == RowKind.Room && hit.DayIndex >= 0)
                {
                    _selectedBarId = null;
                    _selecting = true;
                    _selectAnchorRow = hit.RowIndex;
                    _selectAnchorDay = hit.DayIndex;
                    _regionFirstRow = _regionLastRow = hit.RowIndex;
                    _regionFirstDay = _regionLastDay = hit.DayIndex;
                    _hasRegion = true;
                    Invalidate();
                    return;
                }
                _selectedBarId = null;
                _hasRegion = false;
                Invalidate();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (_dragCandidate && !_dragging)
                {
                    Point cur = ContentPoint(e.Location);
                    if (Math.Abs(cur.X - _dragStartPoint.X) >= 3 || Math.Abs(cur.Y - _dragStartPoint.Y) >= 3)
                        _dragging = true;
                }
                if (_dragging)
                {
                    Point cur = ContentPoint(e.Location);
                    int dayDelta = (int)Math.Round((cur.X - _dragStartPoint.X) / (double)DayW,
                        MidpointRounding.AwayFromZero);
                    int rowDelta = (int)Math.Round((cur.Y - _dragStartPoint.Y) / (double)RowH,
                        MidpointRounding.AwayFromZero);
                    int newFirst = _dragStartFirstDay + dayDelta;
                    int newLast = _dragStartLastDay + dayDelta;
                    if (_dragMode == 0)
                    {
                        // Di chuyển: kẹp cả thanh vào trong cửa sổ xem.
                        if (newFirst < 0) { newLast -= newFirst; newFirst = 0; }
                        if (newLast > _days - 1) { newFirst -= newLast - (_days - 1); newLast = _days - 1; }
                        _dragFirstDay = newFirst;
                        _dragLastDay = newLast;
                        int newRoom = _dragStartRoomIndex + rowDelta;
                        if (newRoom < 0) newRoom = 0;
                        if (newRoom > _roomRows.Count - 1) newRoom = _roomRows.Count - 1;
                        _dragRoomIndex = newRoom;
                    }
                    else if (_dragMode == -1)
                    {
                        if (newFirst < 0) newFirst = 0;
                        if (newFirst > _dragStartLastDay) newFirst = _dragStartLastDay;
                        _dragFirstDay = newFirst;
                        _dragLastDay = _dragStartLastDay;
                    }
                    else
                    {
                        if (newLast > _days - 1) newLast = _days - 1;
                        if (newLast < _dragStartFirstDay) newLast = _dragStartFirstDay;
                        _dragFirstDay = _dragStartFirstDay;
                        _dragLastDay = newLast;
                    }
                    Invalidate();
                    return;
                }
                if (_selecting)
                {
                    HitInfo hit = HitTest(e.Location);
                    if (hit.RowIndex >= 0 && hit.DayIndex >= 0)
                    {
                        _regionFirstRow = Math.Min(_selectAnchorRow, hit.RowIndex);
                        _regionLastRow = Math.Max(_selectAnchorRow, hit.RowIndex);
                        _regionFirstDay = Math.Min(_selectAnchorDay, hit.DayIndex);
                        _regionLastDay = Math.Max(_selectAnchorDay, hit.DayIndex);
                        Invalidate();
                    }
                    return;
                }
                HitInfo hover = HitTest(e.Location);
                if (hover.IsBar)
                {
                    Cursor = hover.IsLeftHandle || hover.IsRightHandle ? Cursors.SizeWE : Cursors.Hand;
                    if (_hoverBarId != hover.Bar.Id)
                    {
                        _hoverBarId = hover.Bar.Id;
                        _tooltip.Show(BookingCalendarView.BuildBarTooltip(hover.Bar), this,
                            e.Location.X + 16, e.Location.Y + 16);
                    }
                }
                else
                {
                    Cursor = Cursors.Default;
                    if (_hoverBarId != null)
                    {
                        _hoverBarId = null;
                        _tooltip.Hide(this);
                    }
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                {
                    base.OnMouseUp(e);
                    return;
                }
                if (_dragging)
                {
                    _dragging = false;
                    _dragCandidate = false;
                    CalendarBookingBar bar = _dragBar;
                    _dragBar = null;
                    Invalidate();
                    if (bar != null && _roomRows.Count > 0 &&
                        BookingCalendarRules.ValidateDrop(_dragRoomIndex, _roomRows.Count,
                            _dragFirstDay, _dragLastDay, _days))
                    {
                        GridRow targetRow = _roomRows[_dragRoomIndex];
                        _owner.CommitDrag(bar, targetRow.Room.Id, targetRow.Room.Name,
                            _from.AddDays(_dragFirstDay), _from.AddDays(_dragLastDay));
                    }
                    // Sai vị trí: trở về vị trí cũ, không hỏi gì (spec §3.3).
                    return;
                }
                _dragCandidate = false;
                _dragBar = null;
                _selecting = false;
                base.OnMouseUp(e);
            }

            protected override void OnDoubleClick(EventArgs e)
            {
                MouseEventArgs me = e as MouseEventArgs;
                if (me != null && me.Button == MouseButtons.Left)
                {
                    HitInfo hit = HitTest(me.Location);
                    if (hit.IsBar && !BookingCalendarRules.IsOutOfWindow(
                        hit.Bar.TuNgay, hit.Bar.DenNgay, _from, _days))
                        _owner.EditSelected();
                }
                base.OnDoubleClick(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hoverBarId = null;
                _tooltip.Hide(this);
                base.OnMouseLeave(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (_rowsDirty)
                    RebuildRows();
                Graphics g = e.Graphics;
                ThemeTokens t = ThemeManager.Current;
                using (var back = new SolidBrush(t.Background))
                    g.FillRectangle(back, ClientRectangle);
                AutoScrollMinSize = new Size(ContentW, ContentH);
                Point scroll = AutoScrollPosition;
                // 1. Vùng chính (ô ngày + thanh + cột Tổng), cuộn cả 2 chiều.
                g.SetClip(new Rectangle(LeftW, HeaderH2, ClientSize.Width - LeftW, ClientSize.Height - HeaderH2));
                g.TranslateTransform(scroll.X, scroll.Y);
                PaintDayArea(g, t);
                g.ResetTransform();
                // 2. Dải tiêu đề (tháng + ngày), chỉ cuộn ngang.
                g.SetClip(new Rectangle(LeftW, 0, ClientSize.Width - LeftW, HeaderH2));
                g.TranslateTransform(scroll.X, 0);
                PaintHeader(g, t);
                g.ResetTransform();
                // 3. Dải cột trái (loại + phòng), chỉ cuộn dọc.
                g.SetClip(new Rectangle(0, HeaderH2, LeftW, ClientSize.Height - HeaderH2));
                g.TranslateTransform(0, scroll.Y);
                PaintLeftStrip(g, t);
                g.ResetTransform();
                // 4. Góc trái-trên (panel ngày/lọc đè lên phần lớn; chỉ lộ 4px dưới cùng).
                g.SetClip(new Rectangle(0, 0, LeftW, HeaderH2));
                using (var brush = new SolidBrush(MonthHeaderBack))
                    g.FillRectangle(brush, 0, 0, LeftW, HeaderH2);
                g.ResetClip();
            }

            private void PaintDayArea(Graphics g, ThemeTokens t)
            {
                if (_totals == null)
                    return;
                int visibleTop = -AutoScrollPosition.Y;
                int visibleBottom = visibleTop + ClientSize.Height;
                // Cột ngày hôm nay nền xám nhạt (LightGray).
                int todayDay = (int)(DateTime.Today - _from).TotalDays;
                if (todayDay >= 0 && todayDay < _days)
                {
                    using (var brush = new SolidBrush(TodayBack))
                        g.FillRectangle(brush, LeftW + todayDay * DayW, HeaderH2,
                            DayW, _rows.Count * RowH);
                }
                using (var borderPen = new Pen(t.Border))
                using (var groupBrush = new SolidBrush(GroupRowBack))
                using (var totalBrush = new SolidBrush(TotalRowBack))
                using (var cellBrush = new SolidBrush(t.Surface))
                {
                    foreach (GridRow row in _rows)
                    {
                        if (row.Top + RowH < visibleTop || row.Top > visibleBottom)
                            continue;
                        int y = row.Top;
                        if (row.Kind == RowKind.Group)
                        {
                            g.FillRectangle(groupBrush, LeftW, y, _days * DayW + TotalW, RowH);
                            int[] counts;
                            if (_totals.SoldPerDayByType.TryGetValue(row.Type.Id, out counts))
                            {
                                for (int d = 0; d < _days; d++)
                                    DrawCellText(g, CountText(counts[d]), DayRect(d, y),
                                        Color.White, _fontBold, true);
                            }
                            DrawCellText(g, GroupTotalText(row.Type.Id), TotalRect(y),
                                Color.White, _fontBold, true);
                        }
                        else if (row.Kind == RowKind.Room)
                        {
                            g.FillRectangle(cellBrush, LeftW, y, _days * DayW + TotalW, RowH);
                            DrawCellText(g, RoomTotalText(row.Room.Id), TotalRect(y),
                                t.Text, _font, true);
                        }
                        else
                        {
                            g.FillRectangle(totalBrush, LeftW, y, _days * DayW + TotalW, RowH);
                            for (int d = 0; d < _days; d++)
                                DrawCellText(g, TotalDayText(row.Total, d), DayRect(d, y),
                                    TotalRowFore, _fontBold, true);
                            DrawCellText(g, TotalSumText(row.Total), TotalRect(y),
                                TotalRowFore, _fontBold, true);
                        }
                        // Kẻ dọc các cột ngày + cột Tổng.
                        for (int d = 0; d <= _days; d++)
                            g.DrawLine(borderPen, LeftW + d * DayW, y, LeftW + d * DayW, y + RowH);
                        g.DrawLine(borderPen, LeftW + _days * DayW + TotalW, y,
                            LeftW + _days * DayW + TotalW, y + RowH);
                        g.DrawLine(borderPen, LeftW, y + RowH - 1, ContentW, y + RowH - 1);
                    }
                }
                // Thanh đặt phòng.
                foreach (CalendarBookingBar bar in _data.Bars)
                {
                    if (_dragging && _dragBar == bar)
                        continue;
                    PaintBar(g, bar, FindRoomRow(bar.RoomId), true);
                }
                // Thanh đang kéo (vẽ ở vị trí mới).
                if (_dragging && _dragBar != null && _roomRows.Count > 0)
                {
                    GridRow targetRow = _roomRows[_dragRoomIndex];
                    PaintGhostBar(g, targetRow.Top);
                }
                // Vùng ô đã chọn.
                if (_hasRegion)
                {
                    int x = LeftW + _regionFirstDay * DayW;
                    int y = HeaderH2 + _regionFirstRow * RowH;
                    int w = (_regionLastDay - _regionFirstDay + 1) * DayW;
                    int h = (_regionLastRow - _regionFirstRow + 1) * RowH;
                    using (var fill = new SolidBrush(Color.FromArgb(80, 0, 120, 215)))
                        g.FillRectangle(fill, x, y, w, h);
                    using (var pen = new Pen(Color.FromArgb(0, 120, 215), 2))
                        g.DrawRectangle(pen, x, y, w - 1, h - 1);
                }
                // Khung chọn quanh thanh đang chọn + 2 tay nắm (spec §1.2).
                if (_selectedBarId != null && !_dragging)
                {
                    CalendarBookingBar bar = FindBar(_selectedBarId);
                    if (bar != null)
                    {
                        GridRow row = FindRoomRow(bar.RoomId);
                        int first, last;
                        if (row != null && BookingCalendarRules.TryClipBar(
                            bar.TuNgay, bar.DenNgay, _from, _days, out first, out last))
                        {
                            Rectangle rect = BarRect(row.Top, first, last);
                            using (var pen = new Pen(Color.Black, 2))
                                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                            DrawHandles(g, rect);
                        }
                    }
                }
            }

            private GridRow FindRoomRow(string roomId)
            {
                foreach (GridRow row in _roomRows)
                    if (row.Room.Id == roomId)
                        return row;
                return null;
            }

            private Rectangle BarRect(int rowTop, int firstDay, int lastDay)
            {
                return new Rectangle(LeftW + firstDay * DayW + 1, rowTop + 2,
                    (lastDay - firstDay + 1) * DayW - 2, RowH - 4);
            }

            private Rectangle DayRect(int day, int rowTop)
            {
                return new Rectangle(LeftW + day * DayW, rowTop, DayW, RowH);
            }

            private Rectangle TotalRect(int rowTop)
            {
                return new Rectangle(LeftW + _days * DayW, rowTop, TotalW, RowH);
            }

            private void PaintBar(Graphics g, CalendarBookingBar bar, GridRow row, bool withArrow)
            {
                if (row == null)
                    return;
                int first, last;
                if (!BookingCalendarRules.TryClipBar(bar.TuNgay, bar.DenNgay, _from, _days, out first, out last))
                    return;
                Rectangle rect = BarRect(row.Top, first, last);
                Color fill = BookingCalendarView.ColorFromArgbOr(bar.ColorArgb, Color.Orange);
                using (var brush = new SolidBrush(fill))
                    g.FillRectangle(brush, rect);
                DrawBarText(g, bar.TenKhach, rect, Color.White, withArrow);
            }

            private void PaintGhostBar(Graphics g, int rowTop)
            {
                Rectangle rect = BarRect(rowTop, _dragFirstDay, _dragLastDay);
                using (var brush = new SolidBrush(DragGhostBack))
                    g.FillRectangle(brush, rect);
                DrawBarText(g, _dragBar.TenKhach, rect, Color.Black, false);
                using (var pen = new Pen(Color.Black, 2))
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                DrawHandles(g, rect);
            }

            private void DrawHandles(Graphics g, Rectangle rect)
            {
                int h = (int)(8 * S);
                using (var brush = new SolidBrush(Color.Black))
                {
                    g.FillRectangle(brush, rect.X - h / 2, rect.Y + rect.Height / 2 - h / 2, h, h);
                    g.FillRectangle(brush, rect.X + rect.Width - h / 2, rect.Y + rect.Height / 2 - h / 2, h, h);
                }
            }

            private void DrawBarText(Graphics g, string text, Rectangle rect, Color fore, bool withArrow)
            {
                int arrowW = withArrow ? (int)(16 * S) : 0;
                var textRect = new Rectangle(rect.X + (int)(4 * S), rect.Y, rect.Width - (int)(4 * S) - arrowW, rect.Height);
                TextRenderer.DrawText(g, text, _font, textRect, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                if (withArrow)
                {
                    // Mũi tên ▶ ở cuối thanh (ảnh 01: có ở mọi thanh đặt phòng).
                    var arrowRect = new Rectangle(rect.Right - arrowW, rect.Y, arrowW, rect.Height);
                    TextRenderer.DrawText(g, "▶", _fontBold, arrowRect, fore,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
            }

            private void PaintHeader(Graphics g, ThemeTokens t)
            {
                using (var backBrush = new SolidBrush(MonthHeaderBack))
                using (var borderPen = new Pen(t.Border))
                {
                    g.FillRectangle(backBrush, LeftW, 0, _days * DayW + TotalW, HeaderH2);
                    // Hàng tháng: hợp nhất các ngày cùng tháng.
                    int d = 0;
                    while (d < _days)
                    {
                        DateTime day = _from.AddDays(d);
                        int end = d;
                        while (end + 1 < _days && _from.AddDays(end + 1).Month == day.Month &&
                               _from.AddDays(end + 1).Year == day.Year)
                            end++;
                        var rect = new Rectangle(LeftW + d * DayW, 0, (end - d + 1) * DayW, HeaderH);
                        string label = "Tháng " + day.Month.ToString(CultureInfo.InvariantCulture) +
                                       " - " + day.Year.ToString(CultureInfo.InvariantCulture);
                        TextRenderer.DrawText(g, label, _font, rect, Color.White,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                        d = end + 1;
                    }
                    // Hàng ngày: "23.T2" (giữ bug T4/T5 đảo — xem BookingCalendarRules.WeekdayLabel).
                    for (int i = 0; i < _days; i++)
                    {
                        DateTime day = _from.AddDays(i);
                        Color fore = day.DayOfWeek == DayOfWeek.Sunday ? Color.Yellow : Color.White;
                        TextRenderer.DrawText(g, BookingCalendarRules.WeekdayLabel(day), _font,
                            DayRect(i, HeaderH), fore,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.SingleLine);
                    }
                    // Kẻ dọc: hàng tháng chỉ ở ranh giới tháng (ô tháng hợp nhất),
                    // hàng ngày ở mọi ranh giới ngày.
                    for (int i = 0; i <= _days; i++)
                        g.DrawLine(borderPen, LeftW + i * DayW, HeaderH, LeftW + i * DayW, HeaderH2);
                    int m = 0;
                    while (m < _days)
                    {
                        DateTime day = _from.AddDays(m);
                        int end = m;
                        while (end + 1 < _days && _from.AddDays(end + 1).Month == day.Month &&
                               _from.AddDays(end + 1).Year == day.Year)
                            end++;
                        g.DrawLine(borderPen, LeftW + m * DayW, 0, LeftW + m * DayW, HeaderH);
                        m = end + 1;
                    }
                    g.DrawLine(borderPen, LeftW + _days * DayW + TotalW, 0,
                        LeftW + _days * DayW + TotalW, HeaderH2);
                    g.DrawLine(borderPen, LeftW, HeaderH2 - 1, ContentW, HeaderH2 - 1);
                }
            }

            private void PaintLeftStrip(Graphics g, ThemeTokens t)
            {
                int visibleTop = -AutoScrollPosition.Y;
                int visibleBottom = visibleTop + ClientSize.Height;
                using (var borderPen = new Pen(t.Border))
                using (var groupBrush = new SolidBrush(GroupRowBack))
                using (var totalBrush = new SolidBrush(TotalRowBack))
                {
                    foreach (GridRow row in _rows)
                    {
                        if (row.Top + RowH < visibleTop || row.Top > visibleBottom)
                            continue;
                        int y = row.Top;
                        if (row.Kind == RowKind.Group)
                        {
                            g.FillRectangle(groupBrush, 0, y, LeftW, RowH);
                            DrawCellText(g, row.Type.Name, new Rectangle(0, y, TypeW, RowH),
                                Color.White, _fontBold, true);
                            DrawCellText(g, "Phòng", new Rectangle(TypeW, y, RoomW, RowH),
                                Color.White, _fontBold, true);
                        }
                        else if (row.Kind == RowKind.Room)
                        {
                            Color typeBack = BookingCalendarView.ColorFromArgbOr(row.Type.BackColorArgb, t.Surface);
                            Color typeFore = BookingCalendarView.ColorFromArgbOr(row.Type.ForeColorArgb, t.Text);
                            using (var brush = new SolidBrush(typeBack))
                                g.FillRectangle(brush, 0, y, TypeW, RowH);
                            DrawCellText(g, row.Type.Name, new Rectangle(0, y, TypeW, RowH),
                                typeFore, _font, true);
                            // Ô tên phòng: màu của phiếu đầu tiên của phòng, không có phiếu thì xanh (0,128,255).
                            Color roomBack = RoomNameBack;
                            foreach (CalendarBookingBar bar in _data.Bars)
                            {
                                if (bar.RoomId == row.Room.Id)
                                {
                                    roomBack = BookingCalendarView.ColorFromArgbOr(bar.ColorArgb, Color.Orange);
                                    break;
                                }
                            }
                            using (var brush = new SolidBrush(roomBack))
                                g.FillRectangle(brush, TypeW, y, RoomW, RowH);
                            DrawCellText(g, row.Room.Name, new Rectangle(TypeW, y, RoomW, RowH),
                                Color.White, _font, true);
                        }
                        else
                        {
                            g.FillRectangle(totalBrush, 0, y, LeftW, RowH);
                            DrawCellText(g, row.Label, new Rectangle(TypeW, y, RoomW, RowH),
                                TotalRowFore, _fontBold, true);
                        }
                        g.DrawLine(borderPen, TypeW, y, TypeW, y + RowH);
                        g.DrawLine(borderPen, LeftW, y, LeftW, y + RowH);
                        g.DrawLine(borderPen, 0, y + RowH - 1, LeftW, y + RowH - 1);
                    }
                }
            }

            private void DrawCellText(Graphics g, string text, Rectangle rect, Color fore, Font font, bool centered)
            {
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                        TextFormatFlags.EndEllipsis;
                flags |= centered ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
                TextRenderer.DrawText(g, text, font, rect, fore, flags);
            }

            private string CountText(int n)
            {
                return n == 0 ? "-" : n.ToString(CultureInfo.InvariantCulture);
            }

            private string GroupTotalText(string typeId)
            {
                int total = 0;
                foreach (CalendarRoom room in _data.Rooms)
                {
                    if (room.TypeId != typeId)
                        continue;
                    int days;
                    if (_totals.TotalDaysByRoom.TryGetValue(room.Id, out days))
                        total += days;
                }
                return CountText(total);
            }

            private string RoomTotalText(string roomId)
            {
                int days;
                if (!_totals.TotalDaysByRoom.TryGetValue(roomId, out days))
                    return "-";
                return CountText(days);
            }

            private string TotalDayText(TotalKind kind, int day)
            {
                switch (kind)
                {
                    case TotalKind.Sold: return CountText(_totals.SoldPerDay[day]);
                    case TotalKind.Remaining: return CountText(_totals.RemainingPerDay[day]);
                    case TotalKind.Total: return _data.TotalRooms.ToString(CultureInfo.InvariantCulture);
                    default:
                        return _totals.CapacityPerDay[day] == 0
                            ? "-"
                            : _totals.CapacityPerDay[day].ToString("0.0", CultureInfo.InvariantCulture) + "%";
                }
            }

            private string TotalSumText(TotalKind kind)
            {
                switch (kind)
                {
                    case TotalKind.Sold: return CountText(_totals.TotalSoldDays);
                    case TotalKind.Remaining: return CountText(_totals.TotalRemainingDays);
                    case TotalKind.Total: return (_data.TotalRooms * _days).ToString(CultureInfo.InvariantCulture);
                    default:
                        return _totals.TotalCapacity == 0
                            ? "-"
                            : _totals.TotalCapacity.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                }
            }
        }
    }
}
