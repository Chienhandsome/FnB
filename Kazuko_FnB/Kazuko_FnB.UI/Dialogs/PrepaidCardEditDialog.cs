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

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem chi tiết thẻ trả trước (DTHETRATRUOC, spec docs/specs/danh-muc-the-tra-truoc.md §2, §6).
    /// Bố cục gọn (không cuộn dọc ở 1366×768 150% DPI): thông tin thẻ, 3 ô Tổng nạp / Tổng sử dụng / Còn lại một hàng,
    /// hai tab Lịch sử nạp tiền (kèm Nạp tiền / Chi trả tiền / Sửa phiếu / Xóa phiếu) và Lịch sử sử dụng.
    /// Phần thao tác lưu và giao dịch nạp/chi đặt ở <c>PrepaidCardEditDialog.Actions.cs</c>.
    /// </summary>
    internal sealed partial class PrepaidCardEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 700;
        private const int BodyHeight96 = 340;

        private readonly UserSession _session;
        private readonly PrepaidCardService _service;
        private readonly PrepaidCardVoucherService _voucherService;
        private readonly FormRepository _formRepo;
        private readonly string _presetGroupId;
        private readonly bool _explicitReadOnly;

        private PrepaidCardRow _row;
        private bool _isNew;
        private bool _readOnly;
        private bool _loading;
        private bool _loadFailed;
        private bool _anyVoucherChanged;
        private string _original = "";
        private List<PrepaidCardVoucherItem> _vouchers = new List<PrepaidCardVoucherItem>();
        private List<PrepaidCardUsageItem> _usages = new List<PrepaidCardUsageItem>();
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _code = new TextBox { MaxLength = PrepaidCardText.MaxTextLength };
        private readonly LookupCombo _group = new LookupCombo();
        private readonly DateBox _expiryDate = new DateBox(true);
        private readonly CheckBox _chkLocked = new CheckBox { Text = "Khóa thẻ", AutoSize = true, UseMnemonic = false };
        private readonly TextBox _note = new TextBox { MaxLength = PrepaidCardText.MaxTextLength };

        private readonly TextBox _txtTotalLoaded = new TextBox { ReadOnly = true, TextAlign = HorizontalAlignment.Right, TabStop = false };
        private readonly TextBox _txtTotalUsed = new TextBox { ReadOnly = true, TextAlign = HorizontalAlignment.Right, TabStop = false };
        private readonly TextBox _txtRemaining = new TextBox { ReadOnly = true, TextAlign = HorizontalAlignment.Right, TabStop = false };

        private readonly ThemedTabControl _tabs = new ThemedTabControl();
        private readonly Panel _voucherBar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnTopUp = new FlatButton { Text = "Nạp tiền" };
        private readonly FlatButton _btnWithdraw = new FlatButton { Text = "Chi trả tiền" };
        private readonly FlatButton _btnEditVoucher = new FlatButton { Text = "Sửa phiếu" };
        private readonly FlatButton _btnDeleteVoucher = new FlatButton { Text = "Xóa phiếu" };
        private readonly DataGridView _voucherGrid = new DataGridView();
        private readonly DataGridView _usageGrid = new DataGridView();
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnClose;
        private Font _summaryFont;

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }
        public bool AnyChanged { get { return AnySaved || _anyVoucherChanged; } }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal string LastConfirmPromptForTest { get; private set; }
        internal Func<string, bool> ConfirmLockedExpiredOverrideForTest { get; set; }
        internal Func<string, bool> ConfirmDeleteVoucherOverrideForTest { get; set; }
        internal Func<PhieuThuDialog, DialogResult> VoucherDialogOpenerForTest { get; set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal TextBox CodeBoxForTest { get { return _code; } }
        internal LookupCombo GroupComboForTest { get { return _group; } }
        internal DateBox ExpiryDateBoxForTest { get { return _expiryDate; } }
        internal CheckBox LockedCheckForTest { get { return _chkLocked; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal TextBox TotalLoadedBoxForTest { get { return _txtTotalLoaded; } }
        internal TextBox TotalUsedBoxForTest { get { return _txtTotalUsed; } }
        internal TextBox RemainingBoxForTest { get { return _txtRemaining; } }
        internal TabControl TabsForTest { get { return _tabs; } }
        internal DataGridView VoucherGridForTest { get { return _voucherGrid; } }
        internal DataGridView UsageGridForTest { get { return _usageGrid; } }
        internal FlatButton TopUpButtonForTest { get { return _btnTopUp; } }
        internal FlatButton WithdrawButtonForTest { get { return _btnWithdraw; } }
        internal FlatButton EditVoucherButtonForTest { get { return _btnEditVoucher; } }
        internal FlatButton DeleteVoucherButtonForTest { get { return _btnDeleteVoucher; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        public PrepaidCardEditDialog(UserSession session, string cardId, string presetGroupId, bool readOnly = false)
            : base("THẺ TRẢ TRƯỚC - THÊM MỚI", "Thông tin thẻ trả trước", BodyWidth96, 456)
        {
            _session = session ?? AppSession.Current;
            _service = new PrepaidCardService(_session);
            _voucherService = new PrepaidCardVoucherService(_session);
            _formRepo = new FormRepository(_session.DatabasePath);
            _presetGroupId = presetGroupId;
            _explicitReadOnly = readOnly;
            _isNew = string.IsNullOrEmpty(cardId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildFields();
            BuildTabs();
            LoadLookups();

            if (_isNew)
                InitNewRecord(_presetGroupId);
            else
                LoadExistingRecord(cardId);

            ApplyPermissions();
            HookChangeEvents();
            _original = Snapshot();
            UpdateButtons();
            UpdateTransactionButtons();
            ApplyDialogTheme();

            FormClosing += OnFormClosing;
            Shown += delegate
            {
                if (_loadFailed)
                {
                    if (!SuppressPopup)
                        Msg.Error(this, PrepaidCardText.NotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                {
                    _code.Focus();
                    _code.SelectAll();
                }
            };
        }

        private void BuildFields()
        {
            float s = Dpi;
            _summaryFont = new Font("Segoe UI Semibold", 9.5f);
            int pad = (int)(12 * s);
            int w = Body.ClientSize.Width;

            int leftCapW = MeasureCaption("Ngày hết hạn", s, 94);
            int leftEditX = pad + leftCapW + (int)(6 * s);
            int midGap = (int)(14 * s);
            int halfRightX = w / 2 + (int)(4 * s);
            int rightCapW = MeasureCaption("Nhóm thẻ", s, 72);
            int rightEditX = halfRightX + rightCapW + (int)(6 * s);
            int rightEditW = Math.Max((int)(140 * s), w - rightEditX - pad);
            int leftEditW = Math.Max((int)(140 * s), halfRightX - midGap - leftEditX);

            int tab = 0;
            PlaceField("Mã thẻ *", _code, pad, (int)(8 * s), leftCapW, leftEditX, leftEditW, (int)(26 * s), ref tab);
            PlaceField("Nhóm thẻ", _group, halfRightX, (int)(8 * s), rightCapW, rightEditX, rightEditW, (int)(26 * s), ref tab);

            int dateW = Math.Min((int)(130 * s), Math.Max((int)(105 * s), leftEditW - (int)(88 * s)));
            PlaceField("Ngày hết hạn", _expiryDate, pad, (int)(40 * s), leftCapW, leftEditX, dateW, (int)(28 * s), ref tab);
            Size chkSz = _chkLocked.GetPreferredSize(Size.Empty);
            _chkLocked.SetBounds(leftEditX + dateW + (int)(8 * s), (int)(44 * s), chkSz.Width, chkSz.Height);
            _chkLocked.TabIndex = tab++;
            Body.Controls.Add(_chkLocked);

            PlaceField("Ghi chú", _note, halfRightX, (int)(40 * s), rightCapW, rightEditX, rightEditW, (int)(26 * s), ref tab);

            // 3 ô Tổng nạp / Tổng sử dụng / Còn lại trên cùng một hàng.
            int sumY = (int)(74 * s);
            int sumGap = (int)(10 * s);
            int totalW = w - 2 * pad;
            int colW = (totalW - 2 * sumGap) / 3;
            PlaceSummaryBox("Tổng nạp:", _txtTotalLoaded, pad, sumY, colW, s);
            PlaceSummaryBox("Tổng sử dụng:", _txtTotalUsed, pad + colW + sumGap, sumY, colW, s);
            PlaceSummaryBox("Còn lại:", _txtRemaining, pad + 2 * (colW + sumGap), sumY, totalW - 2 * (colW + sumGap), s);

            _lblError.SetBounds(pad, (int)(312 * s), totalW, (int)(24 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private void PlaceField(string caption, Control editor, int x, int y, int capW, int editX, int editW, int h, ref int tabIndex)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = false
            };
            lbl.SetBounds(x, y, capW, Math.Min(h, (int)(26 * Dpi)));
            editor.SetBounds(editX, y, editW, h);
            editor.TabIndex = tabIndex++;
            _fieldLabels.Add(lbl);
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
        }

        private void PlaceSummaryBox(string caption, TextBox box, int x, int y, int width, float s)
        {
            int capW = TextRenderer.MeasureText(caption, Font).Width + (int)(8 * s);
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = false
            };
            lbl.SetBounds(x, y + (int)(2 * s), capW, (int)(24 * s));
            box.Font = _summaryFont;
            box.SetBounds(x + capW + (int)(2 * s), y + (int)(1 * s), Math.Max((int)(70 * s), width - capW - (int)(2 * s)), (int)(26 * s));
            _fieldLabels.Add(lbl);
            Body.Controls.Add(lbl);
            Body.Controls.Add(box);
        }

        private void BuildTabs()
        {
            float s = Dpi;
            int pad = (int)(12 * s);
            _tabs.SetBounds(pad, (int)(108 * s), Body.ClientSize.Width - 2 * pad, (int)(198 * s));

            var tabTopUp = new TabPage("Lịch sử nạp tiền");
            _voucherBar.Height = (int)(34 * s);
            _btnTopUp.Tag = "primary";
            _btnDeleteVoucher.Tag = "danger";
            _btnTopUp.Click += delegate { OpenTopUpVoucher(); };
            _btnWithdraw.Click += delegate { OpenWithdrawVoucher(); };
            _btnEditVoucher.Click += delegate { OpenEditSelectedVoucher(); };
            _btnDeleteVoucher.Click += delegate { DeleteSelectedVoucher(); };
            LayoutVoucherActionBar();
            _voucherBar.Controls.Add(_btnTopUp);
            _voucherBar.Controls.Add(_btnWithdraw);
            _voucherBar.Controls.Add(_btnEditVoucher);
            _voucherBar.Controls.Add(_btnDeleteVoucher);

            ConfigureHistoryGrid(_voucherGrid);
            AddHistoryColumn(_voucherGrid, "vh_num", "Số phiếu", 130, typeof(string), null);
            AddHistoryColumn(_voucherGrid, "vh_date", "Ngày", 100, typeof(DateTime), "dd/MM/yyyy");
            AddHistoryColumn(_voucherGrid, "vh_thu", "Số tiền nạp", 140, typeof(decimal), "#,##0.##");
            AddHistoryColumn(_voucherGrid, "vh_chi", "Chi trả", 140, typeof(decimal), "#,##0.##");
            _voucherGrid.Columns[_voucherGrid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _voucherGrid.SelectionChanged += delegate { UpdateTransactionButtons(); };
            _voucherGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    OpenEditSelectedVoucher();
            };

            tabTopUp.Controls.Add(_voucherGrid);
            tabTopUp.Controls.Add(_voucherBar);

            var tabUsage = new TabPage("Lịch sử sử dụng");
            ConfigureHistoryGrid(_usageGrid);
            AddHistoryColumn(_usageGrid, "uh_num", "Số phiếu", 180, typeof(string), null);
            AddHistoryColumn(_usageGrid, "uh_date", "Ngày", 120, typeof(DateTime), "dd/MM/yyyy");
            AddHistoryColumn(_usageGrid, "uh_used", "Sử dụng", 160, typeof(decimal), "#,##0.##");
            _usageGrid.Columns[_usageGrid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            tabUsage.Controls.Add(_usageGrid);

            _tabs.TabPages.Add(tabTopUp);
            _tabs.TabPages.Add(tabUsage);
            Body.Controls.Add(_tabs);
        }

        private void LayoutVoucherActionBar()
        {
            float s = Dpi;
            int x = (int)(4 * s);
            int y = (int)(3 * s);
            int h = (int)(28 * s);
            FlatButton[] buttons = { _btnTopUp, _btnWithdraw, _btnEditVoucher, _btnDeleteVoucher };
            foreach (FlatButton b in buttons)
            {
                int w = Math.Max((int)(78 * s), TextRenderer.MeasureText(b.Text, Font).Width + (int)(22 * s));
                b.SetBounds(x, y, w, h);
                b.Radius = (int)(6 * s);
                x = b.Right + (int)(6 * s);
            }
        }

        private void ConfigureHistoryGrid(DataGridView g)
        {
            float s = Dpi;
            g.Dock = DockStyle.Fill;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AutoGenerateColumns = false;
            g.MultiSelect = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = (int)(28 * s);
            g.RowTemplate.Height = (int)(24 * s);
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        }

        private void AddHistoryColumn(DataGridView g, string name, string header, int width96, Type type, string format)
        {
            float s = Dpi;
            int headerNeed = TextRenderer.MeasureText(header, Font).Width + (int)(24 * s);
            int minW = Math.Max((int)(60 * s), headerNeed);
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                MinimumWidth = minW,
                Width = Math.Max((int)(width96 * s), minW)
            };
            if (type == typeof(decimal))
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = NumberInput.ViFormat;
            }
            else if (type == typeof(DateTime))
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
            }
            g.Columns.Add(col);
        }

        private int MeasureCaption(string text, float s, int min96)
        {
            int need = TextRenderer.MeasureText(text, Font).Width + (int)(10 * s);
            return Math.Max((int)(min96 * s), need);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);

            _expiryDate.ApplyTheme(t, t.Background);
            if (!_expiryDate.Enabled)
            {
                _expiryDate.FieldColor = disabledBack;
                _expiryDate.Inner.BackColor = disabledBack;
                _expiryDate.Invalidate();
            }

            _code.BackColor = _code.ReadOnly ? disabledBack : t.Surface;
            _group.BackColor = _group.Enabled ? t.Surface : disabledBack;
            _note.BackColor = _note.ReadOnly ? disabledBack : t.Surface;
            _chkLocked.BackColor = Color.Transparent;
            _chkLocked.ForeColor = t.Text;

            Color summaryBack = Draw.Mix(t.Surface, t.Primary, 0.06f);
            _txtTotalLoaded.BackColor = summaryBack;
            _txtTotalLoaded.ForeColor = t.Text;
            _txtTotalUsed.BackColor = summaryBack;
            _txtTotalUsed.ForeColor = t.Text;
            _txtRemaining.BackColor = summaryBack;
            _txtRemaining.ForeColor = t.Primary;

            _tabs.BackColor = t.Background;
            _tabs.ForeColor = t.Text;
            foreach (TabPage p in _tabs.TabPages)
                p.BackColor = t.Background;
            _voucherBar.BackColor = t.Background;
            ThemeApplier.ApplyGrid(_voucherGrid, t);
            ThemeApplier.ApplyGrid(_usageGrid, t);

            foreach (FlatButton b in new[] { _btnTopUp, _btnWithdraw, _btnEditVoucher, _btnDeleteVoucher })
            {
                b.SurfaceColor = t.Background;
                b.Invalidate();
            }

            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _summaryFont != null)
                _summaryFont.Dispose();
            base.Dispose(disposing);
        }
    }
}
