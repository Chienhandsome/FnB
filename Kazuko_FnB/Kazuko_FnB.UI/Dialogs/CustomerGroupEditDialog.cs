using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Dialog Thêm/Sửa/Xem nhóm khách hàng (DNHOMKHACHHANG, spec docs/specs/danh-muc-khach-hang.md §4.2, §5, §9.0, §9.0.2).
    /// Bố cục 2 cột gọn, không cuộn dọc ở 1366×768 (100/125/150% DPI).
    /// Ô "Tỉ lệ giảm giá" (giảm tổng) LUÔN hiện (§9.0 A2); các ô tỉ lệ giảm giới hạn 0..100% (§9.0 Q9).
    /// Chế độ chỉ đọc (Q10): mọi ô khóa, không có nút Lưu. Phần bố cục &amp; theme nằm ở <c>CustomerGroupEditDialog.Layout.cs</c>.
    /// </summary>
    internal sealed partial class CustomerGroupEditDialog : ThemedDialog
    {
        private const int BodyWidth96 = 740;
        private const int BodyHeight96 = 310;

        private sealed class RateRow
        {
            public Label Caption;
            public NumberBox Box;
            public Label Percent;
        }

        private readonly CustomerGroupRepository _repo;
        private readonly string _databasePath;
        private readonly string _parentId;
        private readonly bool _readOnly;

        private CustomerGroupRow _row;
        private bool _isNew;
        private bool _loading;
        private string _original = "";

        private readonly TextBox _name = new TextBox { MaxLength = CustomerText.MaxTextLength };
        private readonly ImageCombo _image = new ImageCombo();
        private readonly NumberBox _points = new NumberBox();
        private readonly TextBox _note = new TextBox { Multiline = true, MaxLength = CustomerText.MaxTextLength, ScrollBars = ScrollBars.Vertical };

        private readonly NumberBox _discountTotal = new NumberBox();
        private readonly NumberBox _discountGoods = new NumberBox();
        private readonly NumberBox _discountHour = new NumberBox();
        private readonly NumberBox _discountFood = new NumberBox();
        private readonly NumberBox _discountDrink = new NumberBox();
        private readonly NumberBox _discountService = new NumberBox();
        private readonly NumberBox _discountOther = new NumberBox();

        private RateRow _rowTotal;
        private RateRow _rowGoods;
        private RateRow _rowHour;
        private RateRow _rowFood;
        private RateRow _rowDrink;
        private RateRow _rowService;
        private RateRow _rowOther;

        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };
        private Font _sectionFont;

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly FlatButton _btnClose;

        /// <summary>ID nhóm đã lưu (null nếu chưa lưu).</summary>
        public string SavedId { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal TextBox NameBoxForTest { get { return _name; } }
        internal ImageCombo ImageComboForTest { get { return _image; } }
        internal NumberBox PointsBoxForTest { get { return _points; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal NumberBox DiscountTotalBoxForTest { get { return _discountTotal; } }
        internal NumberBox DiscountGoodsBoxForTest { get { return _discountGoods; } }
        internal NumberBox DiscountHourBoxForTest { get { return _discountHour; } }
        internal NumberBox DiscountFoodBoxForTest { get { return _discountFood; } }
        internal NumberBox DiscountDrinkBoxForTest { get { return _discountDrink; } }
        internal NumberBox DiscountServiceBoxForTest { get { return _discountService; } }
        internal NumberBox DiscountOtherBoxForTest { get { return _discountOther; } }
        internal Label ErrorLabelForTest { get { return _lblError; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SaveCloseButtonForTest { get { return _btnSaveClose; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }

        public CustomerGroupEditDialog(UserSession session, CustomerGroupRow existing, string parentId, bool readOnly = false)
            : base("NHÓM KHÁCH HÀNG - THÊM MỚI", "Thông tin nhóm khách hàng", BodyWidth96, 434)
        {
            _databasePath = session.DatabasePath;
            _repo = new CustomerGroupRepository(_databasePath);
            _parentId = parentId;
            _isNew = existing == null || string.IsNullOrEmpty(existing.Id);
            _row = existing ?? CustomerGroupRow.NewDefault();
            _readOnly = readOnly || (_isNew ? !CustomerRules.Can(session, RoleModes.Add) : !CustomerRules.Can(session, RoleModes.Edit));

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = _btnClose;

            BuildFields();
            LoadImages();
            BindRowToControls();
            ApplyDiscountVisibility(SystemConfig.CoTienGio, SystemConfig.GopChungGiamGiaTienGioVaTienHang);
            ApplyReadOnlyState();
            HookChangeEvents();
            _original = Snapshot();
            UpdateButtons();

            FormClosing += OnFormClosing;
            Shown += delegate
            {
                if (!_readOnly)
                {
                    _name.Focus();
                    _name.SelectAll();
                }
            };
        }

        private void LoadImages()
        {
            float s = Dpi;
            try
            {
                _image.Load(new ImageRepository(_databasePath).LoadAll(), (int)(20 * s));
            }
            catch (Exception)
            {
                _image.Load(new List<ImageRecord>(), (int)(20 * s));
            }
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                SetTitle(_readOnly ? "NHÓM KHÁCH HÀNG - XEM CHI TIẾT"
                    : (_isNew ? "NHÓM KHÁCH HÀNG - THÊM MỚI" : "NHÓM KHÁCH HÀNG - SỬA"));
                _name.Text = _row.Name ?? "";
                _image.SelectedId = _row.ImageId;
                _points.Value = Clamp(_row.LoyaltyPoints, _points.Minimum, _points.Maximum);
                _note.Text = _row.Note ?? "";
                _discountTotal.Value = Clamp(_row.DiscountTotal, 0m, 100m);
                _discountGoods.Value = Clamp(_row.DiscountGoods, 0m, 100m);
                _discountHour.Value = Clamp(_row.DiscountHour, 0m, 100m);
                _discountFood.Value = Clamp(_row.DiscountFood, 0m, 100m);
                _discountDrink.Value = Clamp(_row.DiscountDrink, 0m, 100m);
                _discountService.Value = Clamp(_row.DiscountService, 0m, 100m);
                _discountOther.Value = Clamp(_row.DiscountOther, 0m, 100m);
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private static decimal Clamp(decimal v, decimal min, decimal max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        private void ApplyReadOnlyState()
        {
            _name.ReadOnly = _readOnly;
            _image.Enabled = !_readOnly;
            _points.ReadOnly = _readOnly;
            _points.Enabled = !_readOnly;
            _note.ReadOnly = _readOnly;
            foreach (NumberBox nb in AllDiscountBoxes())
            {
                nb.ReadOnly = _readOnly;
                nb.Enabled = !_readOnly;
            }

            // §9.0.2 Q10: chế độ chỉ đọc không có nút Lưu.
            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly;
            _btnSaveClose.Visible = !_readOnly;
            SubtitleText = _readOnly
                ? "Chế độ chỉ xem (không có quyền sửa)"
                : "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
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
            _name.TextChanged += onChanged;
            _image.SelectedIndexChanged += onChanged;
            _points.ValueChanged += onChanged;
            _note.TextChanged += onChanged;
            foreach (NumberBox nb in AllDiscountBoxes())
                nb.ValueChanged += onChanged;
        }

        private NumberBox[] AllDiscountBoxes()
        {
            return new[] { _discountTotal, _discountGoods, _discountHour, _discountFood, _discountDrink, _discountService, _discountOther };
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_name.Text).Append('|');
            sb.Append(_image.SelectedId).Append('|');
            sb.Append(_points.Value).Append('|');
            sb.Append(_note.Text).Append('|');
            foreach (NumberBox nb in AllDiscountBoxes())
                sb.Append(nb.Value).Append('|');
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
            _btnSaveNew.Enabled = canSave;
            _btnSaveClose.Enabled = canSave;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Control | Keys.L))
                {
                    if (_btnSave.Enabled) SaveStay();
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S))
                {
                    if (_btnSaveNew.Enabled) SaveAndNew();
                    return true;
                }
                if (keyData == Keys.Enter && !_note.Focused)
                {
                    SaveAndClose();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SaveStay() { DoSave(); }

        private void SaveAndNew()
        {
            if (!DoSave())
                return;
            _isNew = true;
            _row = CustomerGroupRow.NewDefault();
            BindRowToControls();
            _original = Snapshot();
            UpdateButtons();
            _name.Focus();
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

            _row.Name = _name.Text ?? "";
            _row.ImageId = string.IsNullOrEmpty(_image.SelectedId) ? null : _image.SelectedId;
            _row.LoyaltyPoints = _points.Value;
            _row.Note = _note.Text ?? "";
            _row.DiscountTotal = _discountTotal.Value;
            _row.DiscountGoods = _discountGoods.Value;
            _row.DiscountHour = _discountHour.Value;
            _row.DiscountFood = _discountFood.Value;
            _row.DiscountDrink = _discountDrink.Value;
            _row.DiscountService = _discountService.Value;
            _row.DiscountOther = _discountOther.Value;
            if (_isNew)
                _row.ParentId = _parentId ?? "";

            CustomerGroupSaveResult res;
            try
            {
                res = _repo.Save(_row, AppSession.UserId ?? "admin");
            }
            catch (DataAccessException ex)
            {
                ShowValidationError("Không ghi được dữ liệu: " + ex.Message);
                return false;
            }

            if (!res.Ok)
            {
                ShowValidationError(res.ErrorMessage);
                _name.Focus();
                _name.SelectAll();
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            _isNew = false;
            SetTitle("NHÓM KHÁCH HÀNG - SỬA");
            _original = Snapshot();
            UpdateButtons();
            return true;
        }

        private void ShowValidationError(string message)
        {
            LastErrorForTest = message;
            _lblError.Text = message ?? "";
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
    }
}
