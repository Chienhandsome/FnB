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
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn chỉ đọc "Kiểm soát order" (HOẠT ĐỘNG ▸ Kiểm soát order, docs/specs/kiem-soat-order.md):
    /// đối chiếu số order giấy (TDONHANG.SOORDER) với hóa đơn bán trong khoảng ngày biên đóng.
    /// Gồm thanh lọc (Từ ngày, Đến ngày, Xem, xuất Excel từng lưới), thanh tóm tắt 6 chỉ số,
    /// lưới "Đã dùng" và lưới "Cần kiểm tra" (Viết sai / Trùng / Thiếu).
    /// Không chứa SQL; không ghi DB; không ghi lưu vết; mọi màu/cỡ chữ từ ThemeTokens / ThemeManager.
    /// </summary>
    internal sealed partial class OrderControlView : Panel
    {
        private readonly OrderControlService _service;

        private readonly FlowLayoutPanel _topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            AutoScroll = false
        };
        private readonly DateBox _fromDate = new DateBox(false);
        private readonly DateBox _toDate = new DateBox(false);
        private readonly FlatButton _btnView = new FlatButton { Text = "Xem (F5)", Tag = "primary" };
        private readonly FlatButton _btnExportUsed = new FlatButton { Text = "Xuất Excel (Đã dùng)" };
        private readonly FlatButton _btnExportIssues = new FlatButton { Text = "Xuất Excel (Cần kiểm tra)" };

        private readonly Panel _summaryPanel = new Panel { Dock = DockStyle.Top };
        private readonly TableLayoutPanel _summaryTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        private readonly List<SummaryCard> _summaryCards = new List<SummaryCard>(6);
        private readonly Dictionary<string, SummaryCard> _summaryByKey =
            new Dictionary<string, SummaryCard>(StringComparer.Ordinal);

        private readonly Panel _emptyBanner = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _emptyLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            Text = OrderControlConstants.EmptyDataMessage
        };

        private readonly SplitContainer _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        private readonly Label _usedTitle = new Label
        {
            Text = "ĐÃ DÙNG",
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        private readonly DataGridView _usedGrid = new DataGridView { Dock = DockStyle.Fill };

        private readonly Label _issueTitle = new Label
        {
            Text = "CẦN KIỂM TRA",
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        private readonly DataGridView _issueGrid = new DataGridView { Dock = DockStyle.Fill };

        private readonly ThemedContextMenu _usedMenu;
        private readonly ThemedContextMenu _issueMenu;
        private readonly Dictionary<string, int> _baseUsedWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _baseIssueWidths = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _smallFont;
        private readonly Font _summaryValueFont;
        private readonly float _scale;

        private ThemeTokens _theme = ThemeManager.Current;
        private OrderControlAnalysisResult _lastResult = new OrderControlAnalysisResult();
        private bool _splitPlaced;
        private bool _loading = true;

        /// <summary>Tắt hộp thoại modal khi chụp màn hình (--render-screen) hoặc selftest.</summary>
        internal bool SuppressPopup { get; set; }

        /// <summary>Ghi đè hộp thoại lưu/mở file Excel khi chạy selftest.</summary>
        internal IGridExportOutput GridExportOutputForTest { get; set; }

        internal string LastWarningMessageForTest { get; private set; }
        internal string LastErrorMessageForTest { get; private set; }
        internal string LastExportMessageForTest { get; private set; }
        internal int LoadCountForTest { get; private set; }
        internal OrderControlAnalysisResult LastAnalysisForTest { get { return _lastResult; } }

        internal FlowLayoutPanel TopBarForTest { get { return _topBar; } }
        internal DateBox FromDateBoxForTest { get { return _fromDate; } }
        internal DateBox ToDateBoxForTest { get { return _toDate; } }
        internal FlatButton ViewButtonForTest { get { return _btnView; } }
        internal FlatButton ExportUsedButtonForTest { get { return _btnExportUsed; } }
        internal FlatButton ExportIssueButtonForTest { get { return _btnExportIssues; } }
        internal DataGridView UsedGridForTest { get { return _usedGrid; } }
        internal DataGridView IssueGridForTest { get { return _issueGrid; } }
        internal Panel EmptyBannerForTest { get { return _emptyBanner; } }
        internal Label EmptyBannerLabelForTest { get { return _emptyLabel; } }
        internal ThemedContextMenu UsedMenuForTest { get { return _usedMenu; } }
        internal ThemedContextMenu IssueMenuForTest { get { return _issueMenu; } }
        internal Font HeadingFontForTest { get { return _headingFont; } }

        public OrderControlView(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _service = new OrderControlService(session);

            DoubleBuffered = true;
            AutoScroll = false;
            _scale = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);
            _smallFont = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);
            _summaryValueFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize + 3f, FontStyle.Bold);

            BuildTopBar(_scale);
            BuildSummaryPanel(_scale);
            BuildEmptyBanner(_scale);
            BuildUsedPane(_scale);
            BuildIssuePane(_scale);

            _usedMenu = DetailGridMenu.AttachMenu(_usedGrid, this, ReloadData);
            _issueMenu = DetailGridMenu.AttachMenu(_issueGrid, this, ReloadData);

            _split.SplitterWidth = Math.Max(5, (int)(6 * _scale));
            _split.SizeChanged += delegate { PlaceSplit(); };

            Controls.Add(_split);
            Controls.Add(_emptyBanner);
            Controls.Add(_summaryPanel);
            Controls.Add(_topBar);
            _split.BringToFront();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);

            InitializeData();
        }

        // ------------------------------------------------------------------ Dựng giao diện

        private void BuildTopBar(float s)
        {
            _topBar.Padding = new Padding((int)(10 * s), (int)(6 * s), (int)(10 * s), (int)(6 * s));
            _topBar.Paint += delegate (object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawLine(pen, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
            };

            _fromDate.Inner.Font = _bodyFont;
            _fromDate.Size = new Size((int)(134 * s), (int)(32 * s));
            _fromDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));

            _toDate.Inner.Font = _bodyFont;
            _toDate.Size = new Size((int)(134 * s), (int)(32 * s));
            _toDate.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(12 * s), (int)(3 * s));

            StyleTopButton(_btnView, s, 96, 8);
            _btnView.Click += delegate { ReloadData(); };

            StyleTopButton(_btnExportUsed, s, 156, 8);
            _btnExportUsed.Click += delegate { ExportUsedGrid(); };

            StyleTopButton(_btnExportIssues, s, 178, 0);
            _btnExportIssues.Click += delegate { ExportIssueGrid(); };

            _topBar.Controls.Add(MakeFlowLabel("Từ ngày", s));
            _topBar.Controls.Add(_fromDate);
            _topBar.Controls.Add(MakeFlowLabel("Đến ngày", s));
            _topBar.Controls.Add(_toDate);
            _topBar.Controls.Add(_btnView);
            _topBar.Controls.Add(_btnExportUsed);
            _topBar.Controls.Add(_btnExportIssues);
        }

        private void StyleTopButton(FlatButton btn, float s, int minWidthAt96, int rightMarginAt96)
        {
            btn.Font = _bodyFont;
            int width = Math.Max(
                (int)Math.Ceiling(minWidthAt96 * s),
                TextRenderer.MeasureText(btn.Text, _bodyFont).Width + (int)Math.Ceiling(24 * s));
            btn.Size = new Size(width, (int)(32 * s));
            btn.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(rightMarginAt96 * s), (int)(3 * s));
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
                UseMnemonic = false,
                Margin = new Padding((int)(4 * s), (int)(2 * s), 0, (int)(2 * s))
            };
        }

        private void BuildSummaryPanel(float s)
        {
            _summaryPanel.Height = (int)(74 * s);
            _summaryPanel.Padding = new Padding((int)(10 * s), (int)(8 * s), (int)(10 * s), (int)(6 * s));

            _summaryTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 6; i++)
                _summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6f));

            AddSummaryCard("max", "Lớn nhất", "—", false, 0, s);
            AddSummaryCard("min", "Nhỏ nhất", "—", false, 1, s);
            AddSummaryCard("used", "Số order đã dùng", "0", false, 2, s);
            AddSummaryCard("dup", "Số trùng", "0", true, 3, s);
            AddSummaryCard("invalid", "Số lỗi viết sai", "0", true, 4, s);
            AddSummaryCard("missing", "Số thiếu", "0", true, 5, s);

            _summaryPanel.Controls.Add(_summaryTable);
        }

        private void AddSummaryCard(string key, string caption, string initialValue, bool isAlertMetric, int col, float s)
        {
            var box = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(col == 0 ? 0 : (int)(6 * s), 0, 0, 0),
                Padding = new Padding((int)(10 * s), (int)(5 * s), (int)(10 * s), (int)(5 * s)),
                Tag = "surface"
            };
            box.Paint += delegate (object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Draw.StrongBorder(_theme)))
                    e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
            };

            var lblCaption = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = (int)(20 * s),
                Font = _smallFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false,
                Tag = "muted"
            };
            var lblValue = new Label
            {
                Text = initialValue,
                Dock = DockStyle.Fill,
                Font = _summaryValueFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };

            box.Controls.Add(lblValue);
            box.Controls.Add(lblCaption);
            _summaryTable.Controls.Add(box, col, 0);

            var card = new SummaryCard
            {
                Key = key,
                Box = box,
                CaptionLabel = lblCaption,
                ValueLabel = lblValue,
                IsAlertMetric = isAlertMetric
            };
            _summaryCards.Add(card);
            _summaryByKey[key] = card;
        }

        private void BuildEmptyBanner(float s)
        {
            _emptyBanner.Height = (int)(32 * s);
            _emptyBanner.Padding = new Padding((int)(12 * s), (int)(4 * s), (int)(12 * s), (int)(4 * s));
            _emptyLabel.Font = _bodyFont;
            _emptyBanner.Controls.Add(_emptyLabel);
        }

        private void BuildUsedPane(float s)
        {
            _usedTitle.Font = _headingFont;
            _usedTitle.Height = (int)(30 * s);
            _usedTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            ConfigureGrid(_usedGrid, s);
            AddGridColumn(_usedGrid, _baseUsedWidths, s, "colOrderNumber", "Số order", 96,
                DataGridViewContentAlignment.MiddleRight, typeof(int), "0", CultureInfo.InvariantCulture, false, "1000000");
            AddGridColumn(_usedGrid, _baseUsedWidths, s, "colBillNumber", "Số bill", 170,
                DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, true, "HD261128-0001 (trùng)");
            AddGridColumn(_usedGrid, _baseUsedWidths, s, "colBillDate", "Ngày", 104,
                DataGridViewContentAlignment.MiddleCenter, typeof(DateTime), "dd/MM/yyyy", CultureInfo.InvariantCulture, false, "28/11/2026");

            _split.Panel1.Controls.Add(_usedGrid);
            _split.Panel1.Controls.Add(_usedTitle);
            _usedGrid.BringToFront();
        }

        private void BuildIssuePane(float s)
        {
            _issueTitle.Font = _headingFont;
            _issueTitle.Height = (int)(30 * s);
            _issueTitle.Padding = new Padding((int)(10 * s), 0, (int)(8 * s), 0);

            ConfigureGrid(_issueGrid, s);
            AddGridColumn(_issueGrid, _baseIssueWidths, s, "colIssueType", "Loại", 94,
                DataGridViewContentAlignment.MiddleCenter, typeof(string), null, null, false, "Viết sai");
            AddGridColumn(_issueGrid, _baseIssueWidths, s, "colIssueMessage", "Nội dung", 280,
                DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, true, "Trùng số order 100 (bill 'HD01', 'HD02')");
            AddGridColumn(_issueGrid, _baseIssueWidths, s, "colIssueRef", "Ngày/Bill", 176,
                DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, false, "28/11/2026 · HD261128-0001");

            _split.Panel2.Controls.Add(_issueGrid);
            _split.Panel2.Controls.Add(_issueTitle);
            _issueGrid.BringToFront();
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
            grid.ScrollBars = ScrollBars.Both;
            grid.Font = _bodyFont;
            grid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(36 * s);
            grid.RowTemplate.Height = (int)(34 * s);

            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(grid, true, null);
        }

        private void AddGridColumn(
            DataGridView grid,
            Dictionary<string, int> baseWidths,
            float s,
            string name,
            string header,
            int minWidthAt96,
            DataGridViewContentAlignment alignment,
            Type valueType,
            string format,
            IFormatProvider formatProvider,
            bool fill,
            string sample)
        {
            int pad = (int)Math.Ceiling(22 * s);
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

            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                MinimumWidth = Math.Max(headerW, (int)Math.Ceiling(minWidthAt96 * s)),
                AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
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

            grid.Columns.Add(col);
            if (!fill)
                baseWidths[name] = width;
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            int minLeft = (int)(320 * _scale);
            int minRight = (int)(420 * _scale);
            if (_split.Width > minLeft + minRight + _split.SplitterWidth)
            {
                int want = (int)(_split.Width * 0.44f);
                if (want < minLeft) want = minLeft;
                int max = _split.Width - minRight - _split.SplitterWidth;
                if (want > max) want = max;
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                ReloadData();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _bodyFont.Dispose();
                _headingFont.Dispose();
                _smallFont.Dispose();
                _summaryValueFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class SummaryCard
        {
            public string Key;
            public Panel Box;
            public Label CaptionLabel;
            public Label ValueLabel;
            public bool IsAlertMetric;
            public bool IsHighlighted;
        }
    }
}
