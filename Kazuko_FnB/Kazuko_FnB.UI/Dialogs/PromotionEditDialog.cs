using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem chi tiết Đợt khuyến mại (DDOTKHUYENMAI + DDOTKHUYENMAICHITIET,
    /// spec docs/specs/danh-muc-khuyen-mai.md P1 §2, §3, §7).
    /// Bố cục gọn, không cuộn dọc ở 1366×768; phần dưới đổi tự động theo 9 loại khuyến mại.
    /// Phần lưới chi tiết ở <c>PromotionEditDialog.Details.cs</c>; nạp/lưu/quyền/theme ở <c>PromotionEditDialog.Actions.cs</c>.
    /// </summary>
    internal sealed partial class PromotionEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 860;
        private const int BodyHeight96 = 460;

        private readonly UserSession _session;
        private readonly PromotionService _service;
        private readonly bool _readOnly;

        private PromotionRow _row;
        private List<PromotionTypeItem> _types = new List<PromotionTypeItem>();
        private List<PromotionDetailRow> _details = new List<PromotionDetailRow>();
        private List<ProductGroupRow> _productGroups;
        private List<PromotionProductItem> _products;

        private bool _isNew;
        private int _existingDetailCount;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly TextBox _txtName = new TextBox { MaxLength = PromotionText.MaxTextLength };
        private readonly ComboBox _cboType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DateBox _dtFrom = new DateBox(true);
        private readonly DateBox _dtTo = new DateBox(true);
        private readonly CheckBox _chkStopped = new CheckBox
        {
            Text = "Ngừng áp dụng",
            AutoSize = true,
            UseMnemonic = false
        };
        private readonly TextBox _txtNote = new TextBox { MaxLength = PromotionText.MaxTextLength };

        private readonly Panel _detailHost = new Panel { Dock = DockStyle.Fill };
        private readonly Panel _pnlTotalBill = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly NumberBox _numHeaderPercent = new NumberBox();
        private readonly Label _lblTotalBillCaption = new Label
        {
            Text = "Tỉ lệ giảm tổng bill (%) *",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };
        private readonly Label _lblTotalBillHint = new Label
        {
            Text = "(Áp dụng giảm % trên tổng hóa đơn, giá trị trong khoảng (0; 100])",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Tag = "muted",
            UseMnemonic = false
        };

        private readonly Panel _pnlDetailGrid = new Panel { Dock = DockStyle.Fill, Visible = false };
        private readonly Panel _detailToolPanel = new Panel { Dock = DockStyle.Top };
        private readonly FlowLayoutPanel _detailToolFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        private readonly FlatButton _btnAddLine = new FlatButton { Text = "Thêm dòng", Tag = "primary" };
        private readonly FlatButton _btnPickGift = new FlatButton { Text = "Chọn hàng tặng...", Visible = false };
        private readonly FlatButton _btnSameGift = new FlatButton { Text = "Tặng cùng món", Visible = false };
        private readonly FlatButton _btnDeleteLine = new FlatButton { Text = "Xóa dòng", Tag = "danger" };
        private readonly DataGridView _detailGrid = new DataGridView();
        private readonly Label _lblNoTypeHint = new Label
        {
            Text = "Chọn loại hình khuyến mại để cấu hình chi tiết.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Tag = "muted",
            UseMnemonic = false
        };
        private readonly Label _lblError = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Visible = false,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnClose;

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal TextBox NameBoxForTest { get { return _txtName; } }
        internal ComboBox TypeComboForTest { get { return _cboType; } }
        internal DateBox FromDateBoxForTest { get { return _dtFrom; } }
        internal DateBox ToDateBoxForTest { get { return _dtTo; } }
        internal CheckBox StoppedCheckForTest { get { return _chkStopped; } }
        internal TextBox NoteBoxForTest { get { return _txtNote; } }
        internal NumberBox HeaderPercentBoxForTest { get { return _numHeaderPercent; } }
        internal Panel TotalBillPanelForTest { get { return _pnlTotalBill; } }
        internal Panel DetailGridPanelForTest { get { return _pnlDetailGrid; } }
        internal DataGridView DetailGridForTest { get { return _detailGrid; } }
        internal FlatButton AddLineButtonForTest { get { return _btnAddLine; } }
        internal FlatButton PickGiftButtonForTest { get { return _btnPickGift; } }
        internal FlatButton SameGiftButtonForTest { get { return _btnSameGift; } }
        internal FlatButton DeleteLineButtonForTest { get { return _btnDeleteLine; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }
        internal List<PromotionDetailRow> DetailsForTest { get { return _details; } }
        internal int DetailDataErrorCountForTest { get; private set; }
        internal string DetailDataErrorDetailForTest { get; private set; }
        internal Func<IList<ProductGroupRow>, ProductGroupRow> GroupPickerOpenerForTest { get; set; }
        internal Func<IList<PromotionProductItem>, string, PromotionProductItem> ProductPickerOpenerForTest { get; set; }

        public PromotionEditDialog(UserSession session, string promotionId, bool readOnly = false)
            : base("ĐỢT KHUYẾN MẠI - THÊM MỚI", "Thông tin đợt khuyến mại", BodyWidth96, 576)
        {
            _session = session ?? AppSession.Current;
            _service = new PromotionService(_session);
            _isNew = string.IsNullOrEmpty(promotionId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildDetailHost();
            BuildErrorBar();
            BuildHeaderPanel();

            LoadTypes();
            if (_isNew)
                InitNewRecord();
            else
                LoadExistingRecord(promotionId);

            ApplyPermissions();
            HookHeaderEvents();
            _original = Snapshot();
            UpdateButtons();
            ApplyDialogTheme();

            FormClosing += OnFormClosing;
            Shown += delegate
            {
                if (_loadFailed)
                {
                    if (!SuppressPopup)
                        Msg.Error(this, PromotionText.NotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                {
                    _txtName.Focus();
                    _txtName.SelectAll();
                }
            };
        }

        private void BuildHeaderPanel()
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);

            string[] col0Caps = { "Tên đợt *", "Loại hình *", "Từ ngày", "Ghi chú" };
            string[] col2Caps = { "Đến ngày" };
            int col0W = MeasureMaxCaptionWidth(col0Caps, bodyFont, s);
            int col2W = MeasureMaxCaptionWidth(col2Caps, bodyFont, s);

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(152 * s),
                Padding = new Padding(0, 0, 0, (int)(4 * s))
            };
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col0W));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col2W));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
            for (int r = 0; r < 4; r++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));

            _txtName.Font = bodyFont;
            _txtName.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _cboType.Font = bodyFont;
            _cboType.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _chkStopped.Font = bodyFont;
            _chkStopped.Anchor = AnchorStyles.Left;
            _chkStopped.Margin = new Padding((int)(10 * s), 0, 0, 0);
            _dtFrom.Inner.Font = bodyFont;
            _dtFrom.Height = (int)(28 * s);
            _dtFrom.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _dtTo.Inner.Font = bodyFont;
            _dtTo.Height = (int)(28 * s);
            _dtTo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _txtNote.Font = bodyFont;
            _txtNote.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            grid.Controls.Add(MakeFieldLabel("Tên đợt *", bodyFont), 0, 0);
            grid.Controls.Add(_txtName, 1, 0);
            grid.SetColumnSpan(_txtName, 3);

            grid.Controls.Add(MakeFieldLabel("Loại hình *", bodyFont), 0, 1);
            grid.Controls.Add(_cboType, 1, 1);
            grid.Controls.Add(_chkStopped, 2, 1);
            grid.SetColumnSpan(_chkStopped, 2);

            grid.Controls.Add(MakeFieldLabel("Từ ngày", bodyFont), 0, 2);
            grid.Controls.Add(_dtFrom, 1, 2);
            grid.Controls.Add(MakeFieldLabel("Đến ngày", bodyFont), 2, 2);
            grid.Controls.Add(_dtTo, 3, 2);

            grid.Controls.Add(MakeFieldLabel("Ghi chú", bodyFont), 0, 3);
            grid.Controls.Add(_txtNote, 1, 3);
            grid.SetColumnSpan(_txtNote, 3);

            top.Controls.Add(grid);
            Body.Controls.Add(top);
        }

        private void BuildErrorBar()
        {
            float s = Dpi;
            _lblError.Height = (int)(24 * s);
            _lblError.Font = new Font(ThemeManager.BodyFontName, ThemeManager.SmallFontSize);
            Body.Controls.Add(_lblError);
        }

        private Label MakeFieldLabel(string text, Font font)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                Font = font,
                TabStop = false
            };
            _fieldLabels.Add(lbl);
            return lbl;
        }

        private static int MeasureMaxCaptionWidth(string[] captions, Font font, float s)
        {
            int max = (int)(86 * s);
            foreach (string c in captions)
            {
                int need = TextRenderer.MeasureText(c, font).Width + (int)(16 * s);
                if (need > max)
                    max = need;
            }
            return max;
        }

        private PromotionTypeItem SelectedTypeItem()
        {
            return _cboType.SelectedItem as PromotionTypeItem;
        }

        private PromotionKind CurrentKind()
        {
            PromotionTypeItem item = SelectedTypeItem();
            return item == null ? PromotionKind.Unknown : item.Kind;
        }
    }
}
