using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại Thêm / Sửa / Xem phiếu tặng/giảm điểm (TTANGGIAMDIEM, docs/specs/khach-hang-than-thiet.md §3).
    /// Bố cục gọn, cảm ứng được, không cuộn dọc ở 1366×768 (100–150% DPI).
    /// Khóa ngày nếu không có quyền "Thay đổi ngày"; khóa khách hàng khi mở từ tab;
    /// chỉ đọc khi thiếu quyền Thêm/Sửa.
    /// </summary>
    internal sealed class LoyaltyPointDialog : ThemedDialog
    {
        private const int BodyWidth96 = 520;
        private const int BodyHeight96 = 340;

        private readonly UserSession _session;
        private readonly LoyaltyService _service;
        private readonly string _presetCustomerId;
        private readonly bool _lockCustomer;
        private readonly bool _readOnly;

        private LoyaltyPointVoucherRow _row;
        private bool _isNew;
        private bool _loading;
        private bool _loadFailed;
        private string _original = "";
        private readonly List<Label> _fieldLabels = new List<Label>();

        private readonly DateBox _date = new DateBox(false);
        private readonly TextBox _number = new TextBox { MaxLength = LoyaltyConstants.MaxTextLength };
        private readonly LookupCombo _customer = new LookupCombo();
        private readonly NumberBox _giftPoints = new NumberBox();
        private readonly NumberBox _reducePoints = new NumberBox();
        private readonly TextBox _reason = new TextBox { MaxLength = LoyaltyConstants.MaxTextLength };
        private readonly TextBox _note = new TextBox { Multiline = true, MaxLength = LoyaltyConstants.MaxTextLength, ScrollBars = ScrollBars.Vertical };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };
        private readonly FlatButton _btnSave, _btnSaveNew, _btnSaveClose, _btnClose;

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }
        public string LastUpgradeNotice { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal DateBox DateBoxForTest { get { return _date; } }
        internal TextBox NumberBoxForTest { get { return _number; } }
        internal LookupCombo CustomerComboForTest { get { return _customer; } }
        internal NumberBox GiftPointsBoxForTest { get { return _giftPoints; } }
        internal NumberBox ReducePointsBoxForTest { get { return _reducePoints; } }
        internal TextBox ReasonBoxForTest { get { return _reason; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }
        internal List<Label> FieldLabelsForTest { get { return _fieldLabels; } }

        public LoyaltyPointDialog(UserSession session, string voucherId, string presetCustomerId, bool lockCustomer, bool readOnly = false)
            : base("TẶNG / GIẢM ĐIỂM - THÊM MỚI", "Phiếu tặng điểm - giảm điểm khách hàng", BodyWidth96, 456)
        {
            _session = session ?? AppSession.Current;
            _service = new LoyaltyService(_session);
            _presetCustomerId = presetCustomerId;
            _lockCustomer = lockCustomer && !string.IsNullOrEmpty(presetCustomerId);
            _isNew = string.IsNullOrEmpty(voucherId);
            _readOnly = readOnly || (_isNew ? !_service.CanAdd : !_service.CanEdit);

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildFields();
            LoadCustomers();

            if (_isNew)
                InitNewRecord(_presetCustomerId);
            else
                LoadExistingRecord(voucherId);

            ApplyPermissions();
            HookChangeEvents();
            _original = Snapshot();
            UpdateButtons();
            ApplyDialogTheme();

            FormClosing += OnFormClosing;
            Shown += delegate
            {
                if (_loadFailed)
                {
                    if (!SuppressPopup)
                        Msg.Error(this, LoyaltyConstants.VoucherNotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                {
                    _giftPoints.Focus();
                    _giftPoints.Select(0, _giftPoints.Text.Length);
                }
            };
        }

        private void BuildFields()
        {
            float s = Dpi;
            ConfigurePointBox(_giftPoints);
            ConfigurePointBox(_reducePoints);

            string[] captions = { "Ngày *", "Số phiếu *", "Khách hàng *", "Điểm tặng", "Điểm giảm", "Lý do *", "Ghi chú" };
            int maxCap = (int)(115 * s);
            foreach (string cap in captions)
            {
                int need = TextRenderer.MeasureText(cap, Font).Width + (int)(12 * s);
                if (need > maxCap) maxCap = need;
            }
            int leftPad = (int)(14 * s), gap = (int)(8 * s), editX = leftPad + maxCap + gap;
            int editW = Math.Max((int)(220 * s), Body.ClientSize.Width - editX - leftPad);

            int tab = 0;
            PlaceRow(captions[0], _date, leftPad, (int)(12 * s), maxCap, editX, editW, (int)(28 * s), ref tab);
            PlaceRow(captions[1], _number, leftPad, (int)(48 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[2], _customer, leftPad, (int)(82 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[3], _giftPoints, leftPad, (int)(116 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[4], _reducePoints, leftPad, (int)(150 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[5], _reason, leftPad, (int)(184 * s), maxCap, editX, editW, (int)(26 * s), ref tab);
            PlaceRow(captions[6], _note, leftPad, (int)(218 * s), maxCap, editX, editW, (int)(80 * s), ref tab);

            _lblError.SetBounds(leftPad, (int)(304 * s), Body.ClientSize.Width - 2 * leftPad, (int)(28 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private static void ConfigurePointBox(NumberBox box)
        {
            box.DecimalPlaces = 0;
            box.Minimum = 0m;
            box.Maximum = LoyaltyConstants.MaxPointValue;
            box.TextAlign = HorizontalAlignment.Right;
        }

        private void PlaceRow(string caption, Control editor, int x, int y, int labelW, int editX, int editW, int h, ref int tabIndex)
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
            lbl.SetBounds(x, y, labelW, Math.Min(h, (int)(26 * Dpi)));
            editor.SetBounds(editX, y, editW, h);
            editor.TabIndex = tabIndex++;
            _fieldLabels.Add(lbl);
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
        }

        private void LoadCustomers()
        {
            var items = new List<LookupItem>();
            try
            {
                List<CustomerLookupItem> list = _service.LoadCustomerLookup();
                foreach (CustomerLookupItem c in list)
                {
                    if (c == null || string.IsNullOrEmpty(c.Id))
                        continue;
                    string label = string.IsNullOrEmpty(c.Code) ? c.Name : (c.Name + " (" + c.Code + ")");
                    items.Add(new LookupItem { Id = c.Id, Name = label });
                }
            }
            catch (Exception)
            {
                // Giữ danh sách rỗng nếu DB lỗi.
            }
            _customer.SetItems(items);
        }

        private void InitNewRecord(string customerId)
        {
            _isNew = true;
            DateTime today;
            string number;
            try
            {
                today = _service.GetServerDate();
                number = _service.GenerateNumber(today);
            }
            catch (Exception)
            {
                today = DateTime.Today;
                number = DocumentNumber.Format(LoyaltyConstants.DefaultNumberTemplate, today, 1);
            }
            _row = LoyaltyPointVoucherRow.NewDefault(today, customerId);
            _row.Number = number;
            SetTitle("TẶNG / GIẢM ĐIỂM - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string voucherId)
        {
            _isNew = false;
            _row = _service.LoadVoucherById(voucherId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = LoyaltyPointVoucherRow.NewDefault(DateTime.Today, _presetCustomerId);
                return;
            }
            SetTitle(_readOnly ? "TẶNG / GIẢM ĐIỂM - XEM CHI TIẾT" : "TẶNG / GIẢM ĐIỂM - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _date.Value = _row.Date == DateTime.MinValue ? DateTime.Today : _row.Date.Date;
                _number.Text = _row.Number ?? "";
                _customer.SelectId(_row.CustomerId);
                _giftPoints.Value = ClampPoint(_row.GiftPoints);
                _reducePoints.Value = ClampPoint(_row.ReducePoints);
                _reason.Text = _row.Reason ?? "";
                _note.Text = _row.Note ?? "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private static decimal ClampPoint(decimal value)
        {
            if (value < 0m) return 0m;
            if (value > LoyaltyConstants.MaxPointValue) return LoyaltyConstants.MaxPointValue;
            return Math.Floor(value);
        }

        private void ApplyPermissions()
        {
            bool canEditDate = !_readOnly && _service.CanChangeDate;
            _date.Enabled = canEditDate;
            _date.Inner.ReadOnly = !canEditDate;

            _number.ReadOnly = _readOnly;
            _customer.Enabled = !_readOnly && !_lockCustomer;
            _giftPoints.ReadOnly = _readOnly;
            _giftPoints.Enabled = !_readOnly;
            _reducePoints.ReadOnly = _readOnly;
            _reducePoints.Enabled = !_readOnly;
            _reason.ReadOnly = _readOnly;
            _note.ReadOnly = _readOnly;

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;

            if (_readOnly)
                SubtitleText = "Chế độ chỉ xem (không có quyền sửa)";
            else
                SubtitleText = "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
        }

        private void HookChangeEvents()
        {
            EventHandler onChanged = delegate
            {
                if (_loading)
                    return;
                ClearError();
                UpdateButtons();
            };
            _date.ValueChanged += onChanged;
            _number.TextChanged += onChanged;
            _customer.SelectedIndexChanged += onChanged;
            _giftPoints.ValueChanged += onChanged;
            _reducePoints.ValueChanged += onChanged;
            _reason.TextChanged += onChanged;
            _note.TextChanged += onChanged;
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|');
            sb.Append(_number.Text).Append('|');
            sb.Append(_customer.SelectedId).Append('|');
            sb.Append(_giftPoints.Value.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(_reducePoints.Value.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(_reason.Text).Append('|');
            sb.Append(_note.Text);
            return sb.ToString();
        }

        private bool Dirty
        {
            get { return !_readOnly && Snapshot() != _original; }
        }

        private void UpdateButtons()
        {
            if (_readOnly)
                return;
            bool canSave = Dirty || _isNew;
            _btnSave.Enabled = canSave;
            _btnSaveNew.Enabled = canSave && _service.CanAdd;
            _btnSaveClose.Enabled = canSave;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Control | Keys.L)) { if (_btnSave.Enabled) SaveStay(); return true; }
                if (keyData == (Keys.Control | Keys.S)) { if (_btnSaveNew.Visible && _btnSaveNew.Enabled) SaveAndNew(); return true; }
                if (keyData == Keys.Enter && !_note.Focused) { SaveAndClose(); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SaveStay() { DoSave(); }

        private void SaveAndNew()
        {
            if (!DoSave())
                return;
            InitNewRecord(_presetCustomerId);
            ApplyPermissions();
            ApplyDialogTheme();
            _original = Snapshot();
            UpdateButtons();
            _giftPoints.Focus();
        }

        private void SaveAndClose()
        {
            if (!DoSave())
                return;
            DialogResult = DialogResult.OK;
            Close();
        }

        internal bool DoSave()
        {
            if (_readOnly)
                return false;

            _date.Commit();
            _row.Date = _date.Value;
            _row.Number = _number.Text ?? "";
            _row.CustomerId = _customer.SelectedId;
            _row.GiftPoints = Math.Floor(_giftPoints.Value);
            _row.ReducePoints = Math.Floor(_reducePoints.Value);
            _row.Reason = _reason.Text ?? "";
            _row.Note = _note.Text ?? "";

            LoyaltySaveResult res;
            try
            {
                res = _service.SaveVoucher(_row);
            }
            catch (DataAccessException ex)
            {
                ShowValidationError("Không ghi được dữ liệu: " + ex.Message);
                return false;
            }

            if (!res.Ok)
            {
                ShowValidationError(res.ErrorMessage);
                if (res.HasNumberError && !_number.ReadOnly) { _number.Focus(); _number.SelectAll(); }
                else if (res.HasCustomerError && _customer.Enabled) { _customer.Focus(); }
                else if (res.HasPointsError && _giftPoints.Enabled) { _giftPoints.Focus(); }
                else if (res.HasReasonError && !_reason.ReadOnly) { _reason.Focus(); _reason.SelectAll(); }
                else if (res.HasNoteError && !_note.ReadOnly) { _note.Focus(); }
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;
            _isNew = false;
            SetTitle("TẶNG / GIẢM ĐIỂM - SỬA");
            _original = Snapshot();
            UpdateButtons();

            if (!string.IsNullOrEmpty(res.UpgradeNotice))
            {
                LastUpgradeNotice = res.UpgradeNotice;
                if (!SuppressPopup)
                    Msg.Info(this, res.UpgradeNotice);
            }
            return true;
        }

        private void ShowValidationError(string message)
        {
            LastErrorForTest = message;
            _lblError.Text = (message ?? "").Replace(Environment.NewLine, "  |  ");
            _lblError.Visible = !string.IsNullOrEmpty(message);
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ClearError()
        {
            LastErrorForTest = null;
            _lblError.Text = "";
            _lblError.Visible = false;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_readOnly || DialogResult == DialogResult.OK || !Dirty || SuppressPopup)
                return;
            DialogResult answer = MessageBox.Show(this, "Dữ liệu đã thay đổi. Bạn có muốn lưu lại không?",
                "Xác nhận", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (answer == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (answer == DialogResult.Yes && !DoSave())
                e.Cancel = true;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);

            _date.ApplyTheme(t, t.Background);
            if (!_date.Enabled)
            {
                _date.FieldColor = disabledBack;
                _date.Inner.BackColor = disabledBack;
                _date.Invalidate();
            }

            _number.BackColor = _number.ReadOnly ? disabledBack : t.Surface;
            _customer.BackColor = _customer.Enabled ? t.Surface : disabledBack;
            _giftPoints.BackColor = _giftPoints.Enabled && !_giftPoints.ReadOnly ? t.Surface : disabledBack;
            _reducePoints.BackColor = _reducePoints.Enabled && !_reducePoints.ReadOnly ? t.Surface : disabledBack;
            _reason.BackColor = _reason.ReadOnly ? disabledBack : t.Surface;
            _note.BackColor = _note.ReadOnly ? disabledBack : t.Surface;

            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
