using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "Chọn dữ liệu — Mời bạn chọn Khu vực" (theo khuôn ProductGroupPickerDialog, spec 2.1):
    /// hiện khi Thêm bàn mà chưa chọn khu vực (đang ở "Tất cả" hoặc "Chưa thiết lập").
    /// Danh sách trống thì hiện dòng "(Chưa có dữ liệu nào, vui lòng tạo mới trước)"; nút Chấp nhận mờ tới khi chọn.
    /// </summary>
    internal sealed class AreaPickerDialog : ThemedDialog
    {
        private const string EmptyItemText = "(Chưa có dữ liệu nào, vui lòng tạo mới trước)";

        private readonly ListBox _list = new ListBox();
        private readonly FlatButton _btnOk;
        private readonly List<AreaRow> _areas = new List<AreaRow>();

        /// <summary>ID khu vực đã chọn (null nếu hủy).</summary>
        public string SelectedAreaId { get; private set; }

        public AreaPickerDialog(UserSession session)
            : base("Chọn dữ liệu", "Mời bạn chọn Khu vực", 400, 300)
        {
            FlatButton cancel = AddButton("Hủy bỏ", null, delegate { Close(); });
            _btnOk = AddButton("Chấp nhận", "primary", delegate
            {
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _areas.Count)
                    SelectedAreaId = _areas[_list.SelectedIndex].Id;
                DialogResult = DialogResult.OK;
                Close();
            });
            CancelButton = cancel;
            _btnOk.Enabled = false;

            _list.Dock = DockStyle.Fill;
            _list.DisplayMember = "Name";
            _list.DoubleClick += delegate
            {
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _areas.Count)
                    _btnOk.PerformClick();
            };
            _list.SelectedIndexChanged += delegate
            {
                _btnOk.Enabled = _list.SelectedIndex >= 0 && _list.SelectedIndex < _areas.Count;
            };
            Body.Controls.Add(_list);

            var repo = new AreaRepository(session.DatabasePath);
            _areas.AddRange(repo.LoadAll());
            if (_areas.Count == 0)
            {
                _list.Items.Add(EmptyItemText);
                _list.Enabled = false;
            }
            else
            {
                foreach (AreaRow a in _areas)
                    _list.Items.Add(a.Name);
                _list.SelectedIndex = 0;
            }
        }
    }
}
