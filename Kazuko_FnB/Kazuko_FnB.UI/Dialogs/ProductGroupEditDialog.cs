using System;
using System.Drawing;
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
    /// Dialog Thêm/Sửa nhóm mặt hàng (spec 1.3: nội dung 395×137, code tay theo layout.xml ở 96 DPI).
    /// Tên nhóm bắt buộc + không trùng; Đánh mã sản phẩm = tiền tố sinh mã; Loại đồ mặc định '1' (Đồ uống).
    /// </summary>
    internal sealed class ProductGroupEditDialog : ThemedDialog
    {
        private const string HintText = "(Ví dụ bạn quy định mã: BIA cho nhóm Bia, hệ thống sẽ tự động sinh các mã trong nhóm này là BIA001, BIA002...)";

        private readonly ProductGroupRepository _repo;
        private readonly ProductGroupRow _row;
        private readonly string _parentId;
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _code = new TextBox();
        private readonly LookupCombo _drinkType = new LookupCombo();
        private readonly ImageCombo _image = new ImageCombo();
        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSaveClose;
        private readonly Label _lblHint = new Label { Text = HintText, AutoSize = false, TabStop = false, Font = new Font("Segoe UI", 9f) };
        private string _original = "";
        private bool _loading;

        /// <summary>ID nhóm đã lưu (null nếu chưa lưu).</summary>
        public string SavedId { get; private set; }

        /// <param name="existing">null = thêm mới.</param>
        /// <param name="parentId">Nhóm cha khi "Thêm con" (null = nhóm gốc).</param>
        public ProductGroupEditDialog(UserSession session, ProductGroupRow existing, string parentId)
            : base(existing == null ? "NHÓM MẶT HÀNG - THÊM MỚI" : "NHÓM MẶT HÀNG - SỬA", "Nhóm mặt hàng", 420, 280)
        {
            FitBody(395, 137); // spec 1.3: nội dung 395×137 ở 96 DPI
            _repo = new ProductGroupRepository(session.DatabasePath);
            _row = existing ?? new ProductGroupRow();
            _parentId = parentId;
            float s = Dpi;

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveClose = AddButton("Lưu & thoát", "primary", delegate { SaveAndClose(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { SaveAndNew(); });
            _btnSave = AddButton("Lưu", null, delegate { SaveStay(); });
            CancelButton = exit;
            CompactFooter(); // 4 nút phải vừa chân dialog rộng 395

            // Vị trí/kích thước theo bảng 1.3 (96 DPI, tọa độ trong nội dung 395×137).
            var lblName = new Label { Text = "Tên nhóm mặt hàng", AutoSize = false, AutoEllipsis = true, TabStop = false };
            lblName.SetBounds((int)(10 * s), (int)(13 * s), (int)(100 * s), (int)(20 * s));
            var lblCode = new Label { Text = "Đánh mã sản phẩm", AutoSize = false, AutoEllipsis = true, TabStop = false };
            lblCode.SetBounds((int)(10 * s), (int)(39 * s), (int)(100 * s), (int)(20 * s));
            var lblHint = _lblHint;
            lblHint.SetBounds((int)(117 * s), (int)(59 * s), (int)(271 * s), (int)(44 * s));
            var lblDrink = new Label { Text = "Loại đồ", AutoSize = false, AutoEllipsis = true, TabStop = false };
            lblDrink.SetBounds((int)(10 * s), (int)(110 * s), (int)(100 * s), (int)(20 * s));
            var lblImage = new Label { Text = "Ảnh:", AutoSize = false, TabStop = false };
            lblImage.SetBounds((int)(289 * s), (int)(13 * s), (int)(40 * s), (int)(20 * s));
            _name.SetBounds((int)(117 * s), (int)(10 * s), (int)(147 * s), (int)(20 * s));
            _code.SetBounds((int)(117 * s), (int)(36 * s), (int)(147 * s), (int)(20 * s));
            _drinkType.SetBounds((int)(117 * s), (int)(106 * s), (int)(147 * s), (int)(21 * s));
            _image.SetBounds((int)(335 * s), (int)(10 * s), (int)(54 * s), (int)(21 * s));
            _name.TabIndex = 0;
            _code.TabIndex = 1;
            _drinkType.TabIndex = 2;
            _image.TabIndex = 3;
            Body.Controls.Add(lblName);
            Body.Controls.Add(lblCode);
            Body.Controls.Add(lblHint);
            Body.Controls.Add(lblDrink);
            Body.Controls.Add(lblImage);
            Body.Controls.Add(_name);
            Body.Controls.Add(_code);
            Body.Controls.Add(_drinkType);
            Body.Controls.Add(_image);

            _drinkType.SetItems(new FormRepository(session.DatabasePath).LoadAll("DLOAIDO"));
            _image.Load(new ImageRepository(session.DatabasePath).LoadAll(), (int)(20 * s));

            _loading = true;
            try
            {
                _name.Text = _row.Name ?? "";
                _code.Text = _row.Code ?? "";
                _drinkType.SelectId(string.IsNullOrEmpty(_row.DrinkTypeId) ? "1" : _row.DrinkTypeId);
                _image.SelectedId = _row.ImageId;
            }
            finally
            {
                _loading = false;
            }
            _original = Snapshot();

            _name.TextChanged += delegate { UpdateSaveButtons(); };
            _code.TextChanged += delegate { UpdateSaveButtons(); };
            _drinkType.SelectedIndexChanged += delegate { UpdateSaveButtons(); };
            _image.SelectedIndexChanged += delegate { UpdateSaveButtons(); };
            FormClosing += OnFormClosing;
            UpdateSaveButtons();

            Shown += delegate
            {
                _name.Focus();
                _name.SelectAll();
            };
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_name.Text).Append('|');
            sb.Append(_code.Text).Append('|');
            sb.Append(_drinkType.SelectedId).Append('|');
            sb.Append(_image.SelectedId);
            return sb.ToString();
        }

        private bool Dirty
        {
            get { return Snapshot() != _original; }
        }

        private void UpdateSaveButtons()
        {
            if (_loading)
                return;
            bool ok = _name.Text.Trim().Length > 0 && Dirty;
            _btnSave.Enabled = ok;
            _btnSaveNew.Enabled = ok;
            _btnSaveClose.Enabled = ok;
        }

        // M9: phím tắt như dialog mặt hàng (Ctrl+L = Lưu, Ctrl+S = Lưu & Mới). Thanh Trước/Sau/Tạo mới chưa dựng
        // (lệch kích thước nội dung 395×137 của spec 1.3 — chờ Chiến chốt).
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.L))
            {
                if (_btnSave.Enabled)
                    SaveStay();
                return true;
            }
            if (keyData == (Keys.Control | Keys.S))
            {
                if (_btnSaveNew.Enabled)
                    SaveAndNew();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SaveStay()
        {
            DoSave();
        }

        private void SaveAndNew()
        {
            if (!DoSave())
                return;
            _row.Id = null;
            _row.Name = "";
            _row.Code = "";
            _row.DrinkTypeId = "1";
            _row.ImageId = null;
            _loading = true;
            try
            {
                _name.Text = "";
                _code.Text = "";
                _drinkType.SelectId("1");
                _image.SelectedId = "";
            }
            finally
            {
                _loading = false;
            }
            SetTitle("NHÓM MẶT HÀNG - THÊM MỚI");
            _original = Snapshot();
            UpdateSaveButtons();
            _name.Focus();
        }

        private void SaveAndClose()
        {
            if (!DoSave())
                return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool DoSave()
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                Msg.Warning(this, "Trường \"Tên nhóm\" không được trống!");
                _name.Focus();
                return false;
            }
            _row.Name = _name.Text.Trim();
            _row.Code = _code.Text.Trim();
            _row.DrinkTypeId = _drinkType.SelectedId ?? "1";
            _row.ImageId = _image.SelectedId;
            if (string.IsNullOrEmpty(_row.Id))
                _row.ParentId = _parentId;
            string error;
            string savedId;
            try
            {
                savedId = _repo.Save(_row, AppSession.UserId, out error);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return false;
            }
            if (error != null)
            {
                Msg.Warning(this, error);
                _name.Focus();
                _name.SelectAll();
                return false;
            }
            SavedId = savedId;
            _row.Id = savedId;
            SetTitle("NHÓM MẶT HÀNG - SỬA");
            _original = Snapshot();
            UpdateSaveButtons();
            return true;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK || !Dirty)
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
            _image.BackColor = t.Surface;
            _image.ForeColor = t.Text;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _lblHint.Font.Dispose();
            base.Dispose(disposing);
        }
    }
}
