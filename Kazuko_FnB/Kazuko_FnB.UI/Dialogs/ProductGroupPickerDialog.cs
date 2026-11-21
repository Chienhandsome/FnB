using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "Chọn dữ liệu — Mời bạn chọn Nhóm mặt hàng" (spec 2.2): hiện khi Thêm mặt hàng mà chưa chọn nhóm.
    /// Danh sách trống thì hiện dòng "(Chưa có dữ liệu nào, vui lòng tạo mới trước)"; nút Chấp nhận mờ tới khi chọn.
    /// </summary>
    internal sealed class ProductGroupPickerDialog : ThemedDialog
    {
        private const string EmptyItemText = "(Chưa có dữ liệu nào, vui lòng tạo mới trước)";

        private readonly ListBox _list = new ListBox();
        private readonly FlatButton _btnOk;
        private readonly List<ProductGroupRow> _groups = new List<ProductGroupRow>();

        /// <summary>ID nhóm đã chọn (null nếu hủy).</summary>
        public string SelectedGroupId { get; private set; }

        public ProductGroupPickerDialog(UserSession session)
            : base("Chọn dữ liệu", "Mời bạn chọn Nhóm mặt hàng", 400, 300)
        {
            FlatButton cancel = AddButton("Hủy bỏ", null, delegate { Close(); });
            _btnOk = AddButton("Chấp nhận", "primary", delegate
            {
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _groups.Count)
                    SelectedGroupId = _groups[_list.SelectedIndex].Id;
                DialogResult = DialogResult.OK;
                Close();
            });
            CancelButton = cancel;
            _btnOk.Enabled = false;

            _list.Dock = DockStyle.Fill;
            _list.DisplayMember = "Name";
            _list.DoubleClick += delegate
            {
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _groups.Count)
                    _btnOk.PerformClick();
            };
            _list.SelectedIndexChanged += delegate
            {
                _btnOk.Enabled = _list.SelectedIndex >= 0 && _list.SelectedIndex < _groups.Count;
            };
            Body.Controls.Add(_list);

            var repo = new ProductGroupRepository(session.DatabasePath);
            _groups.AddRange(repo.LoadAll());
            if (_groups.Count == 0)
            {
                _list.Items.Add(EmptyItemText);
                _list.Enabled = false;
            }
            else
            {
                foreach (ProductGroupRow g in _groups)
                    _list.Items.Add(g.Name);
                _list.SelectedIndex = 0;
            }
        }
    }
}
