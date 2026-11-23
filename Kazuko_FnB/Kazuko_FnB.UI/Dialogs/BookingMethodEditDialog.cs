using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Thêm / sửa một thư mục "Phương thức đặt" của màn Khách đặt hàng (tên + ghi chú). Tên bắt buộc và không
    /// trùng; thư mục mới nằm dưới <c>parent</c> (null = gốc).
    /// </summary>
    internal sealed class BookingMethodEditDialog : ThemedDialog
    {
        private readonly BookingMethodRepository _repo;
        private readonly BookingMethodRow _row;
        private readonly InputBox _name = new InputBox();
        private readonly InputBox _note = new InputBox();

        /// <summary>Id đã lưu (null nếu chưa lưu).</summary>
        public string SavedId { get; private set; }

        /// <param name="existing">null = thêm mới.</param>
        /// <param name="parent">Thư mục cha khi thêm mới (null = gốc).</param>
        public BookingMethodEditDialog(UserSession session, BookingMethodRow existing, BookingMethodRow parent)
            : base(existing == null ? "THƯ MỤC - THÊM MỚI" : "THƯ MỤC - SỬA",
                   existing != null ? "Phương thức đặt" : (parent == null ? "Phương thức đặt (thư mục gốc)" : "Thư mục con của: " + parent.Name),
                   440, 260)
        {
            _repo = new BookingMethodRepository(session.DatabasePath);
            _row = existing ?? new BookingMethodRow { ParentId = parent == null ? "" : parent.Id };
            float s = Dpi;

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            FlatButton save = AddButton("Lưu", "primary", delegate { DoSave(); });
            CancelButton = exit;
            AcceptButton = save;

            var lblName = new Label { Text = "Tên thư mục *", AutoSize = false, AutoEllipsis = true, TabStop = false };
            lblName.SetBounds((int)(10 * s), (int)(14 * s), (int)(110 * s), (int)(32 * s));
            lblName.TextAlign = ContentAlignment.MiddleLeft;
            var lblNote = new Label { Text = "Ghi chú", AutoSize = false, AutoEllipsis = true, TabStop = false };
            lblNote.SetBounds((int)(10 * s), (int)(56 * s), (int)(110 * s), (int)(32 * s));
            lblNote.TextAlign = ContentAlignment.MiddleLeft;
            _name.SetBounds((int)(126 * s), (int)(14 * s), (int)(280 * s), (int)(32 * s));
            _note.SetBounds((int)(126 * s), (int)(56 * s), (int)(280 * s), (int)(32 * s));
            _name.Inner.Font = new Font("Segoe UI", 10f);
            _note.Inner.Font = new Font("Segoe UI", 10f);
            _name.Inner.MaxLength = 255;
            _note.Inner.MaxLength = 255;
            _name.TabIndex = 0;
            _note.TabIndex = 1;
            Body.Controls.Add(lblName);
            Body.Controls.Add(lblNote);
            Body.Controls.Add(_name);
            Body.Controls.Add(_note);

            _name.Text = _row.Name ?? "";
            _note.Text = _row.Note ?? "";
            Shown += delegate
            {
                _name.Inner.Focus();
                _name.Inner.SelectAll();
            };
        }

        private void DoSave()
        {
            _row.Name = _name.Text;
            _row.Note = _note.Text;
            string error;
            string id;
            try
            {
                id = _repo.Save(_row, AppSession.UserId, out error);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return;
            }
            if (error != null)
            {
                Msg.Warning(this, error);
                _name.Inner.Focus();
                _name.Inner.SelectAll();
                return;
            }
            SavedId = id;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
