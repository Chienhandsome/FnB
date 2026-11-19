using System;
using System.Drawing;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Thêm mới / sửa nhóm người dùng: tên (bắt buộc, không trùng), ảnh (từ thư viện), ghi chú.</summary>
    internal sealed class GroupEditDialog : ThemedDialog
    {
        private readonly GroupRepository _repo;
        private readonly GroupRow _row;
        private readonly TextBox _name = new TextBox();
        private readonly ImageCombo _image = new ImageCombo();
        private readonly TextBox _note = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
        private readonly FlatButton _save;
        private readonly string _original;

        /// <summary>ID nhóm đã lưu (null nếu chưa lưu).</summary>
        public string SavedId { get; private set; }

        public GroupEditDialog(UserSession session, GroupRow existing)
            : base(existing == null ? "Thêm mới nhóm người dùng" : "Sửa nhóm người dùng", "Nhóm người dùng", 640, 380)
        {
            _repo = new GroupRepository(session.DatabasePath);
            _row = existing ?? new GroupRow();
            float s = Dpi;

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _save = AddButton("Ghi dữ liệu", "primary", delegate { DoSave(); });
            CancelButton = exit;

            int labelW = (int)(180 * s), pad = (int)(8 * s), rowH = (int)(32 * s);
            int fieldX = labelW + pad, fieldW = ClientSize.Width - fieldX - (int)(30 * s);
            int y = (int)(14 * s);
            AddLabel("Tên nhóm người dùng", y, labelW, rowH);
            _name.SetBounds(fieldX, y + (int)(3 * s), fieldW - (int)(190 * s), (int)(26 * s));
            AddLabel("Ảnh:", y, 0, rowH).Left = fieldX + _name.Width + pad;
            _image.SetBounds(fieldX + _name.Width + pad + (int)(40 * s), y, (int)(150 * s) - (int)(40 * s), (int)(32 * s));
            y += rowH + pad;
            AddLabel("Ghi chú:", y, labelW, rowH);
            _note.SetBounds(fieldX, y + (int)(3 * s), fieldW, (int)(150 * s));

            Body.Controls.Add(_name);
            Body.Controls.Add(_image);
            Body.Controls.Add(_note);

            _image.Load(new ImageRepository(session.DatabasePath).LoadAll(), (int)(20 * s));
            _name.Text = _row.Name ?? "";
            _note.Text = _row.Note ?? "";
            _image.SelectedId = _row.ImageId;
            _original = Snapshot();

            _name.TextChanged += delegate { UpdateButton(); };
            _note.TextChanged += delegate { UpdateButton(); };
            _image.SelectedIndexChanged += delegate { UpdateButton(); };
            FormClosing += OnFormClosing;
            UpdateButton();
        }

        private Label AddLabel(string text, int y, int w, int h)
        {
            var l = new Label { Text = text, AutoSize = false, Left = 0, Top = y, Width = Math.Max(w, 40), Height = h, TextAlign = ContentAlignment.MiddleLeft };
            Body.Controls.Add(l);
            return l;
        }

        private string Snapshot()
        {
            return _name.Text + "|" + _image.SelectedId + "|" + _note.Text;
        }

        private bool Dirty
        {
            get { return Snapshot() != _original; }
        }

        private void UpdateButton()
        {
            _save.Enabled = _name.Text.Trim().Length > 0 && Dirty;
        }

        private void DoSave()
        {
            _row.Name = _name.Text.Trim();
            _row.Note = _note.Text;
            _row.ImageId = _image.SelectedId;
            try
            {
                if (_repo.IsNameTaken(_row.Name, _row.Id))
                {
                    Msg.Warning(this, "Tên nhóm người dùng '" + _row.Name + "' đã tồn tại.");
                    return;
                }
                SavedId = _repo.Save(_row, AppSession.UserId);
            }
            catch (FbException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK && e.CloseReason == CloseReason.UserClosing && Dirty &&
                !Msg.Confirm(this, "Dữ liệu của bạn đã thay đổi, bạn có muốn hủy bỏ các thay đổi để thực hiện tiếp không?"))
                e.Cancel = true;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            _image.BackColor = Theme.ThemeManager.Current.Surface;
            _image.ForeColor = Theme.ThemeManager.Current.Text;
        }
    }
}
