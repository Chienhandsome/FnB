using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Sơ đồ bàn chỉ đọc theo khu vực với 2 dải tab trên (TABHIENTHI=0) và dưới (TABHIENTHI=1, ẩn nếu rỗng)
    /// (docs/specs/su-dung-dich-vu.md §2.1, §2.3, §3.1). Không chứa SQL; mọi màu/cỡ chữ lấy từ ThemeTokens/ThemeManager.
    /// </summary>
    internal sealed class TableMapView : Panel
    {
        private readonly SplitContainer _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Panel2Collapsed = true
        };
        private readonly ThemedTabControl _topTabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private readonly ThemedTabControl _bottomTabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private readonly ThemedContextMenu _emptyMenu = new ThemedContextMenu();
        private readonly ThemedContextMenu _busyMenu = new ThemedContextMenu();
        private readonly Timer _refreshTimer = new Timer();

        private readonly Dictionary<string, TableTileButton> _tilesById = new Dictionary<string, TableTileButton>(StringComparer.Ordinal);
        private readonly List<ServiceAreaInfo> _areas = new List<ServiceAreaInfo>();
        private readonly List<ServiceTableInfo> _tables = new List<ServiceTableInfo>();

        private Func<List<ServiceAreaInfo>> _areasLoader;
        private Func<List<ServiceTableInfo>> _tablesLoader;
        private string _selectedTableId;
        private ServiceTableInfo _contextTable;
        private bool _showBillNote;
        private int _sizeMode;
        private ThemeTokens _theme = ThemeManager.Current;

        public event EventHandler<ServiceTableInfo> TableSelected;
        public event EventHandler<ServiceTableInfo> TableDoubleClicked;
        public event EventHandler<ServiceTableInfo> OpenTableRequested;
        public event EventHandler<ServiceTableInfo> PayRequested;
        public event EventHandler<ServiceTableInfo> GroupPayRequested;
        public event EventHandler<string> UnsupportedActionRequested;

        internal ThemedTabControl TopTabsForTest { get { return _topTabs; } }
        internal ThemedTabControl BottomTabsForTest { get { return _bottomTabs; } }
        internal bool BottomSectionHiddenForTest { get { return _split.Panel2Collapsed; } }
        internal Timer RefreshTimerForTest { get { return _refreshTimer; } }

        internal TableTileButton FindTileForTest(string tableId)
        {
            TableTileButton btn;
            return tableId != null && _tilesById.TryGetValue(tableId, out btn) ? btn : null;
        }

        public TableMapView()
        {
            DoubleBuffered = true;
            _split.Panel1.Controls.Add(_topTabs);
            _split.Panel2.Controls.Add(_bottomTabs);
            Controls.Add(_split);

            BuildContextMenus();

            _refreshTimer.Interval = ServiceOrderConstants.TableMapRefreshIntervalMs;
            _refreshTimer.Tick += delegate { RefreshTablesQuietly(); };

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
        }

        public void BindLoaders(Func<List<ServiceAreaInfo>> areasLoader, Func<List<ServiceTableInfo>> tablesLoader)
        {
            _areasLoader = areasLoader;
            _tablesLoader = tablesLoader;
        }

        public void StartAutoRefresh()
        {
            _refreshTimer.Interval = ServiceOrderConstants.TableMapRefreshIntervalMs;
            if (!_refreshTimer.Enabled)
                _refreshTimer.Start();
        }

        public void StopAutoRefresh()
        {
            if (_refreshTimer.Enabled)
                _refreshTimer.Stop();
        }

        public void ReloadAll()
        {
            if (_areasLoader == null || _tablesLoader == null)
                return;
            _showBillNote = SystemConfig.HienThiGhiChuTrenGiaoDienBan;
            _sizeMode = SystemConfig.KichThuocHienThiOGiaoDienDichVu;

            List<ServiceAreaInfo> areas = _areasLoader() ?? new List<ServiceAreaInfo>();
            List<ServiceTableInfo> tables = _tablesLoader() ?? new List<ServiceTableInfo>();
            RenderData(areas, tables);
        }

        public void RefreshTablesQuietly()
        {
            if (_areasLoader == null || _tablesLoader == null || IsDisposed)
                return;
            try
            {
                ReloadAll();
            }
            catch
            {
                // Spec §3.1: làm tươi nền dùng kết nối riêng, lỗi bỏ qua.
            }
        }

        public void RenderData(List<ServiceAreaInfo> areas, List<ServiceTableInfo> tables)
        {
            string topAreaId = SelectedAreaId(_topTabs);
            string bottomAreaId = SelectedAreaId(_bottomTabs);

            _areas.Clear();
            if (areas != null)
                _areas.AddRange(areas);
            _tables.Clear();
            if (tables != null)
                _tables.AddRange(tables);

            var topAreas = _areas.FindAll(a => a.ShowTab != 1);
            var bottomAreas = _areas.FindAll(a => a.ShowTab == 1);

            SuspendLayout();
            try
            {
                _tilesById.Clear();
                PopulateTabs(_topTabs, topAreas, topAreaId);
                PopulateTabs(_bottomTabs, bottomAreas, bottomAreaId);
                _split.Panel2Collapsed = bottomAreas.Count == 0;
                if (!_split.Panel2Collapsed && _split.Height > 200)
                    _split.SplitterDistance = Math.Max(100, _split.Height / 2);
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        public void SelectTableVisual(string tableId)
        {
            _selectedTableId = tableId;
            foreach (KeyValuePair<string, TableTileButton> kv in _tilesById)
                kv.Value.SetSelected(string.Equals(kv.Key, tableId, StringComparison.Ordinal));
        }

        public ServiceTableInfo FindTable(string tableId)
        {
            if (string.IsNullOrEmpty(tableId))
                return null;
            return _tables.Find(t => string.Equals(t.Id, tableId, StringComparison.Ordinal));
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t ?? ThemeManager.Current;
            BackColor = _theme.Background;
            _split.BackColor = _theme.Border;
            _split.Panel1.BackColor = _theme.Background;
            _split.Panel2.BackColor = _theme.Background;
            _topTabs.Invalidate(true);
            _bottomTabs.Invalidate(true);
            foreach (TableTileButton tile in _tilesById.Values)
                tile.ApplyTheme(_theme);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (!IsDisposed)
                ApplyTheme(ThemeManager.Current);
        }

        private void BuildContextMenus()
        {
            _emptyMenu.AddItem("Mở bàn", delegate
            {
                if (_contextTable != null && OpenTableRequested != null)
                    OpenTableRequested(this, _contextTable);
            });
            _busyMenu.AddItem("Thanh toán (F11)", delegate
            {
                if (_contextTable != null && PayRequested != null)
                    PayRequested(this, _contextTable);
            });
            _busyMenu.AddItem("Thanh toán chung", delegate
            {
                if (_contextTable != null && GroupPayRequested != null)
                    GroupPayRequested(this, _contextTable);
            });
            _busyMenu.AddItem("Chuyển bàn", delegate { RaiseUnsupported("Chuyển bàn"); });
            _busyMenu.AddItem("Gộp bàn", delegate { RaiseUnsupported("Gộp bàn"); });
            _busyMenu.AddSeparator();
            _busyMenu.AddItem("Hủy hóa đơn", delegate { RaiseUnsupported("Hủy hóa đơn"); });
        }

        private void RaiseUnsupported(string name)
        {
            if (UnsupportedActionRequested != null)
                UnsupportedActionRequested(this, name);
        }

        private void PopulateTabs(ThemedTabControl tabs, List<ServiceAreaInfo> areas, string previousAreaId)
        {
            tabs.TabPages.Clear();
            float s = DeviceDpi / 96f;
            Size tileSize = TileSizeFor(_sizeMode, s);
            int selectedIndex = 0;

            for (int i = 0; i < areas.Count; i++)
            {
                ServiceAreaInfo area = areas[i];
                if (!string.IsNullOrEmpty(previousAreaId) && string.Equals(area.Id, previousAreaId, StringComparison.Ordinal))
                    selectedIndex = i;

                var page = new TabPage(area.Name)
                {
                    Tag = area.Id,
                    BackColor = _theme.Background,
                    Padding = new Padding((int)(6 * s))
                };
                var flow = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    WrapContents = true,
                    BackColor = _theme.Background,
                    Padding = new Padding((int)(4 * s))
                };

                List<ServiceTableInfo> areaTables = _tables.FindAll(tb => string.Equals(tb.AreaId, area.Id, StringComparison.Ordinal));
                areaTables.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

                foreach (ServiceTableInfo table in areaTables)
                {
                    var tile = new TableTileButton(table, _showBillNote, tileSize, _theme);
                    tile.SetSelected(string.Equals(table.Id, _selectedTableId, StringComparison.Ordinal));
                    ServiceTableInfo captured = table;
                    tile.Click += delegate
                    {
                        SelectTableVisual(captured.Id);
                        if (TableSelected != null)
                            TableSelected(this, captured);
                    };
                    tile.DoubleClick += delegate
                    {
                        SelectTableVisual(captured.Id);
                        if (TableDoubleClicked != null)
                            TableDoubleClicked(this, captured);
                    };
                    tile.MouseUp += delegate(object sender, MouseEventArgs e)
                    {
                        if (e.Button != MouseButtons.Right)
                            return;
                        _contextTable = captured;
                        SelectTableVisual(captured.Id);
                        bool busy = captured.Status == TableStatus.InUse || captured.Status == TableStatus.TempPrinted;
                        ThemedContextMenu menu = busy ? _busyMenu : _emptyMenu;
                        menu.Show(tile, e.Location);
                    };
                    _tilesById[table.Id] = tile;
                    flow.Controls.Add(tile);
                }

                page.Controls.Add(flow);
                tabs.TabPages.Add(page);
            }

            if (tabs.TabPages.Count > 0)
                tabs.SelectedIndex = Math.Min(selectedIndex, tabs.TabPages.Count - 1);
        }

        private static string SelectedAreaId(TabControl tabs)
        {
            return tabs.SelectedTab != null ? tabs.SelectedTab.Tag as string : null;
        }

        private static Size TileSizeFor(int sizeMode, float s)
        {
            // Cỡ ô theo KichThuocHienThiOGiaoDienDichVu (0..3), đảm bảo tối thiểu 44px cho cảm ứng (§7).
            switch (sizeMode)
            {
                case 1: return new Size((int)(108 * s), Math.Max((int)(72 * s), (int)(48 * s)));
                case 2: return new Size((int)(96 * s), Math.Max((int)(60 * s), (int)(46 * s)));
                case 3: return new Size((int)(88 * s), Math.Max((int)(52 * s), (int)(44 * s)));
                default: return new Size((int)(122 * s), (int)(86 * s));
            }
        }

        /// <summary>Ánh xạ trạng thái bàn sang bộ màu (nền, viền, chữ) hoàn toàn từ ThemeTokens (§3.1, §7).</summary>
        internal static void ResolveTileColors(TableStatus status, ThemeTokens t, out Color fill, out Color border, out Color fore)
        {
            fore = t.Text;
            switch (status)
            {
                case TableStatus.TempPrinted:
                    fill = Draw.Mix(t.Surface, t.Info, 0.28f);
                    border = t.Info;
                    break;
                case TableStatus.InUse:
                    fill = Draw.Mix(t.Surface, t.Primary, 0.24f);
                    border = t.Primary;
                    break;
                case TableStatus.NeedCleaning:
                    fill = Draw.Mix(t.Surface, t.Accent, 0.30f);
                    border = t.Accent;
                    break;
                case TableStatus.Cleaning:
                    fill = Draw.Mix(t.Surface, t.Success, 0.24f);
                    border = t.Success;
                    break;
                case TableStatus.UnderRepair:
                    fill = Draw.Mix(t.Surface, t.Danger, 0.24f);
                    border = t.Danger;
                    break;
                case TableStatus.Reserved:
                    fill = Draw.Mix(t.Surface, t.Info, 0.14f);
                    border = t.Accent;
                    break;
                default:
                    fill = t.Surface;
                    border = Draw.StrongBorder(t);
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAutoRefresh();
                _refreshTimer.Dispose();
                ThemeManager.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Ô bàn tự vẽ theo ThemeTokens, hỗ trợ cảm ứng (tối thiểu ≥ 44px) và hiển thị đủ thông tin trạng thái.</summary>
    internal sealed class TableTileButton : Control
    {
        private readonly ServiceTableInfo _table;
        private readonly bool _showBillNote;
        private ThemeTokens _theme;
        private bool _selected;
        private bool _hover;
        private Color _borderColor;

        public ServiceTableInfo TableInfo { get { return _table; } }
        internal Color BorderColorForTest { get { return _borderColor; } }
        internal void PerformClick() { OnClick(EventArgs.Empty); }

        public TableTileButton(ServiceTableInfo table, bool showBillNote, Size size, ThemeTokens theme)
        {
            _table = table;
            _showBillNote = showBillNote;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.StandardDoubleClick, true);
            Size = size;
            Margin = new Padding((int)(5 * DeviceDpi / 96f));
            Cursor = Cursors.Hand;
            Text = table.Name ?? "";
            ApplyTheme(theme);
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;
            _selected = selected;
            Invalidate();
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t ?? ThemeManager.Current;
            Color fill;
            Color border;
            Color fore;
            TableMapView.ResolveTileColors(_table.Status, _theme, out fill, out border, out fore);
            BackColor = fill;
            ForeColor = fore;
            _borderColor = border;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            ThemeTokens t = _theme ?? ThemeManager.Current;
            float s = DeviceDpi / 96f;

            g.Clear(Parent != null ? Parent.BackColor : t.Background);

            Color fill = _hover ? Draw.Mix(BackColor, t.Primary, 0.10f) : BackColor;
            float penWidth = _selected ? 2.6f * s : 1.4f * s;
            Color border = _selected ? t.Primary : _borderColor;

            var rect = new RectangleF(1.5f * s, 1.5f * s, Width - 3.5f * s, Height - 3.5f * s);
            using (GraphicsPath path = Draw.Rounded(rect, (int)(8 * s)))
            {
                using (var brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
                using (var pen = new Pen(border, penWidth))
                    g.DrawPath(pen, path);
            }

            int pad = (int)(6 * s);
            int y = pad;
            using (var titleFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold))
            using (var subFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize - 0.5f))
            {
                var nameRect = new Rectangle(pad, y, Width - pad * 2, (int)(20 * s));
                TextRenderer.DrawText(g, _table.Name ?? "", titleFont, nameRect, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                y += nameRect.Height;

                string statusLine = ServiceOrderConstants.TableStatusLabel(_table.Status);
                if (_table.BillStartTime.HasValue && (_table.Status == TableStatus.InUse || _table.Status == TableStatus.TempPrinted))
                    statusLine = _table.BillStartTime.Value.ToString("HH:mm") + " • " + statusLine;
                var statusRect = new Rectangle(pad, y, Width - pad * 2, (int)(18 * s));
                TextRenderer.DrawText(g, statusLine, subFont, statusRect, Draw.Mix(ForeColor, t.TextMuted, 0.35f),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                y += statusRect.Height;

                string extra = "";
                if (_table.Status == TableStatus.Reserved && !string.IsNullOrEmpty(_table.ReservationText))
                    extra = _table.ReservationText;
                else if (_showBillNote && !string.IsNullOrEmpty(_table.BillNote))
                    extra = _table.BillNote;

                if (!string.IsNullOrEmpty(extra) && Height - y >= (int)(14 * s))
                {
                    var extraRect = new Rectangle(pad, y, Width - pad * 2, Height - y - pad / 2);
                    TextRenderer.DrawText(g, extra, subFont, extraRect, t.TextMuted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
        }
    }
}
