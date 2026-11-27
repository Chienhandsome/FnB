using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Công nợ khách hàng ban đầu" &amp; "Công nợ nhà cung cấp ban đầu"
    /// (QUẢN TRỊ &gt; Dữ liệu ban đầu, docs/specs/so-du-dau-ky.md §3, P2):
    /// một khung dùng chung hai chế độ (Khách hàng LOAI = 99, Nhà cung cấp LOAI = 98).
    /// Ngày mặc định 31/12 năm trước (khóa nếu thiếu quyền Thay đổi ngày), Tìm không dấu,
    /// tích "Chỉ hiện đối tác có nợ đầu", lưới Mã, Tên, Địa chỉ, Điện thoại, Nợ đầu kỳ
    /// (nhập trực tiếp bằng <see cref="NumericEditGridCell"/>, Enter chuyển dòng),
    /// hàng tổng cộng đậm, Lưu (quyền Sửa), Xuất Excel, cảnh báo khi đóng còn thay đổi chưa lưu.
    /// Phần nạp dữ liệu, lọc, lưu, đóng tab, xuất Excel đặt ở <c>OpeningDebtView.Actions.cs</c>;
    /// phần sửa ô, đổ lưới, hàng tổng cộng, menu ngữ cảnh, đổi theme đặt ở <c>OpeningDebtView.Render.cs</c>.
    /// </summary>
    internal sealed partial class OpeningDebtView : Panel, IConfirmCloseView
    {
        private static readonly object SummaryRowTag = new object();

        private readonly UserSession _session;
        private readonly DebtPartyKind _kind;
        private readonly OpeningDebtService _service;
        private readonly bool _readOnly;

        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly Font _summaryFont;

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnSave = new FlatButton { Text = "Lưu (Ctrl+L)", Tag = "primary" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnRefresh = new FlatButton { Text = "Làm mới (F5)" };
        private readonly Label _summary = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };

        private readonly Panel _filterPanel = new Panel { Dock = DockStyle.Top };
        private readonly DateBox _dtDate = new DateBox(false);
        private readonly InputBox _txtSearch = new InputBox();
        private readonly CheckBox _chkOnlyWithDebt = new CheckBox { Text = "Chỉ hiện đối tác có nợ đầu", AutoSize = true };

        private readonly Panel _warnBanner = new Panel { Dock = DockStyle.Top, Visible = false };
        private readonly Label _lblWarn = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        private readonly OpeningDebtGrid _grid = new OpeningDebtGrid();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        private ThemedContextMenu _gridMenu;
        private ThemeTokens _theme = ThemeManager.Current;
        private List<OpeningDebtPartnerRow> _allItems = new List<OpeningDebtPartnerRow>();
        private List<OpeningDebtPartnerRow> _visibleItems = new List<OpeningDebtPartnerRow>();
        private DateTime _loadedDate;
        private bool _loading;
        private bool _isDirty;
        private bool _updatingCell;
        private bool _cellParseFailed;
        private bool _lastEditRejected;

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }
        internal string LastInfoMessageForTest { get; private set; }
        internal string LastExportMessageForTest { get; private set; }
        internal OpeningDebtSaveResult LastSaveResultForTest { get; private set; }
        internal bool UnsavedPromptTriggeredForTest { get; private set; }
        internal Func<bool> ConfirmUnsavedOverrideForTest { get; set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }
        internal int DataErrorCountForTest { get; private set; }
        internal string DataErrorDetailForTest { get; private set; }

        internal DebtPartyKind PartyKindForTest { get { return _kind; } }
        internal DataGridView GridForTest { get { return _grid; } }
        internal DateBox DateBoxForTest { get { return _dtDate; } }
        internal InputBox SearchBoxForTest { get { return _txtSearch; } }
        internal CheckBox OnlyWithDebtCheckForTest { get { return _chkOnlyWithDebt; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton ExcelButtonForTest { get { return _btnExcel; } }
        internal FlatButton RefreshButtonForTest { get { return _btnRefresh; } }
        internal Label SummaryLabelForTest { get { return _summary; } }
        internal Panel WarningBannerForTest { get { return _warnBanner; } }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal bool IsDirtyForTest { get { return _isDirty; } }

        public OpeningDebtView(UserSession session, DebtPartyKind kind)
        {
            _session = session ?? AppSession.Current;
            _kind = kind;
            _service = new OpeningDebtService(_session);
            _readOnly = !_service.CanEdit(_kind);

            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _summaryFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            _loading = true;
            BuildBar(s);
            BuildFilterBar(s);
            BuildWarningBanner(s);
            BuildGrid(s);

            Controls.Add(_grid);
            Controls.Add(_warnBanner);
            Controls.Add(_filterPanel);
            Controls.Add(_bar);
            _grid.BringToFront();
            _loading = false;

            ApplyPermissions();
            BuildGridMenu();
            WireEvents();

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            ReloadAll(true);
        }

        private void BuildBar(float s)
        {
            _bar.Height = (int)(54 * s);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding((int)(10 * s), (int)(9 * s), (int)(10 * s), (int)(9 * s))
            };
            foreach (FlatButton b in new[] { _btnSave, _btnExcel, _btnRefresh })
            {
                b.Height = (int)(36 * s);
                b.Margin = new Padding(0, 0, (int)(8 * s), 0);
                b.Font = _bodyFont;
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(26 * s);
                flow.Controls.Add(b);
            }

            _summary.Width = (int)(400 * s);
            _summary.Dock = DockStyle.Right;
            _summary.Padding = new Padding(0, 0, (int)(12 * s), 0);
            _summary.Font = _bodyFont;

            _bar.Controls.Add(flow);
            _bar.Controls.Add(_summary);
        }

        private void BuildFilterBar(float s)
        {
            _filterPanel.AutoSize = true;
            _filterPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding((int)(10 * s), (int)(8 * s), (int)(10 * s), (int)(6 * s))
            };

            AddInlineLabel(flow, "Ngày:", s);
            _dtDate.Inner.Font = _bodyFont;
            _dtDate.Width = (int)(138 * s);
            _dtDate.Height = (int)(34 * s);
            _dtDate.Margin = new Padding(0, 0, (int)(14 * s), 0);
            _dtDate.Value = OpeningDebtRules.DefaultOpeningDate(DateTime.Today);
            flow.Controls.Add(_dtDate);

            AddInlineLabel(flow, "Tìm kiếm:", s);
            _txtSearch.Placeholder = _kind == DebtPartyKind.Customer
                ? "Mã, tên khách hàng, địa chỉ, SĐT..."
                : "Mã, tên nhà cung cấp, địa chỉ, SĐT...";
            _txtSearch.Inner.Font = _bodyFont;
            _txtSearch.Width = (int)(280 * s);
            _txtSearch.Height = (int)(34 * s);
            _txtSearch.Margin = new Padding(0, 0, (int)(14 * s), 0);
            flow.Controls.Add(_txtSearch);

            _chkOnlyWithDebt.Font = _bodyFont;
            _chkOnlyWithDebt.Height = (int)(34 * s);
            _chkOnlyWithDebt.Margin = new Padding(0, (int)(6 * s), 0, 0);
            flow.Controls.Add(_chkOnlyWithDebt);

            _filterPanel.Controls.Add(flow);
        }

        private void AddInlineLabel(FlowLayoutPanel flow, string text, float s)
        {
            int w = TextRenderer.MeasureText(text, _bodyFont).Width + (int)Math.Ceiling(8 * s);
            var lbl = new Label
            {
                Text = text,
                AutoSize = false,
                Width = w,
                Height = (int)(34 * s),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont,
                Margin = new Padding(0, 0, (int)(4 * s), 0)
            };
            flow.Controls.Add(lbl);
        }

        private void BuildWarningBanner(float s)
        {
            _warnBanner.Height = (int)(34 * s);
            _warnBanner.Padding = new Padding((int)(12 * s), 0, (int)(12 * s), 0);
            _lblWarn.Font = _bodyFont;
            _warnBanner.Controls.Add(_lblWarn);
        }

        private void BuildGrid(float s)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = _readOnly;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(44 * s);
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(36 * s);
            _grid.RowTemplate.Height = (int)(34 * s);
            _grid.Font = _bodyFont;
            _grid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.EnterAdvanceHandler = AdvanceCellOnEnter;

            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(_grid, true, null);

            string codeHeader = _kind == DebtPartyKind.Customer ? "Mã khách" : "Mã NCC";
            string nameHeader = _kind == DebtPartyKind.Customer ? "Tên khách hàng" : "Tên nhà cung cấp";

            // Đúng 5 cột theo spec §3: Mã, Tên, Địa chỉ, Điện thoại, Nợ đầu kỳ (nhập, >= 0).
            AddGridColumn("colCode", codeHeader, 110, "NCC00001", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);
            DataGridViewTextBoxColumn nameCol = AddGridColumn("colName", nameHeader, 240, "Công ty TNHH Thương Mại Dịch Vụ", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);
            nameCol.MinimumWidth = nameCol.Width;
            nameCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            AddGridColumn("colAddress", "Địa chỉ", 220, "123 Nguyễn Văn Cừ, Q.5, TP.HCM", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);
            AddGridColumn("colPhone", "Điện thoại", 118, "0901234567", typeof(string), null, DataGridViewContentAlignment.MiddleLeft, true, s);

            DataGridViewTextBoxColumn debtCol = AddGridColumn("colOpeningDebt", "Nợ đầu kỳ", 150, "999.999.999", typeof(decimal), "#,##0", DataGridViewContentAlignment.MiddleRight, _readOnly, s);
            NumericEditGridCell.ConfigureColumn(debtCol, 0);

            _empty.Font = _bodyFont;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
        }

        private DataGridViewTextBoxColumn AddGridColumn(
            string name,
            string header,
            int minWidth96,
            string sample,
            Type type,
            string format,
            DataGridViewContentAlignment align,
            bool readOnly,
            float s)
        {
            int w = MeasureColumnWidth(header, sample, minWidth96, s);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = w,
                MinimumWidth = w,
                ValueType = type,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = align;
            col.DefaultCellStyle.NullValue = "";
            if (align == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            if (format != null)
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
            }
            _grid.Columns.Add(col);
            return col;
        }

        private int MeasureColumnWidth(string header, string sample, int minWidth96, float s)
        {
            int w = (int)Math.Ceiling(minWidth96 * s);
            Font hdrFont = _grid.ColumnHeadersDefaultCellStyle.Font ?? _bodyFont;
            int hdrW = TextRenderer.MeasureText(header ?? "", hdrFont).Width + (int)Math.Ceiling(30 * s);
            if (hdrW > w) w = hdrW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, _bodyFont).Width + (int)Math.Ceiling(24 * s);
                if (sampleW > w) w = sampleW;
            }
            return w;
        }

        private void ApplyPermissions()
        {
            bool canEdit = _service.CanEdit(_kind);
            bool canView = _service.CanView(_kind);
            _btnSave.Visible = canEdit;
            _btnSave.Enabled = canEdit;
            _btnExcel.Enabled = canView;
            _grid.ReadOnly = _readOnly;
            bool allowDate = canEdit && _service.CanChangeDate;
            _dtDate.Enabled = allowDate;
            _dtDate.Inner.ReadOnly = !allowDate;
        }

        /// <summary>DataGridView tùy biến phím Enter để chuyển sang ô Nợ đầu kỳ của dòng kế tiếp (spec §5).</summary>
        private sealed class OpeningDebtGrid : DataGridView
        {
            public Func<bool> EnterAdvanceHandler { get; set; }

            private bool TryHandleEnter(Keys keyData)
            {
                if ((keyData & Keys.KeyCode) == Keys.Enter && (keyData & (Keys.Control | Keys.Alt | Keys.Shift)) == 0)
                {
                    if (EnterAdvanceHandler != null && EnterAdvanceHandler())
                        return true;
                }
                return false;
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (TryHandleEnter(keyData))
                    return true;
                return base.ProcessCmdKey(ref msg, keyData);
            }

            protected override bool ProcessDialogKey(Keys keyData)
            {
                if (TryHandleEnter(keyData))
                    return true;
                return base.ProcessDialogKey(keyData);
            }

            protected override bool ProcessDataGridViewKey(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && !e.Control && !e.Alt && !e.Shift)
                {
                    if (EnterAdvanceHandler != null && EnterAdvanceHandler())
                        return true;
                }
                return base.ProcessDataGridViewKey(e);
            }
        }
    }
}
