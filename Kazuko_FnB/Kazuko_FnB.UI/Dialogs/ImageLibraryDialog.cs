using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Quản trị ▸ Thư viện ảnh. Ảnh cùng tên xếp thành một nhóm (Số, Người, Ưu tiên…).
    /// Thêm mới (nhiều file), Thay thế ảnh đang chọn, Xóa (chặn nếu còn dữ liệu dùng ảnh).
    /// </summary>
    internal sealed class ImageLibraryDialog : ThemedDialog
    {
        private readonly ImageRepository _repo;
        private readonly ListView _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.LargeIcon,
            MultiSelect = false,
            ShowGroups = true,
            ShowItemToolTips = true,
            HideSelection = false
        };
        private readonly ImageList _thumbs = new ImageList { ColorDepth = ColorDepth.Depth32Bit };
        private readonly FlatButton _replace, _delete;

        public ImageLibraryDialog(UserSession session)
            : base("QUẢN LÝ THƯ VIỆN ẢNH", "Ảnh dùng chung cho nhóm, mặt hàng, logo…", 820, 600)
        {
            _repo = new ImageRepository(session.DatabasePath);
            float s = Dpi;
            int px = (int)(40 * s);
            _thumbs.ImageSize = new Size(px, px);
            _list.LargeImageList = _thumbs;

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _delete = AddButton("Xóa", "danger", delegate { DeleteSelected(); });
            _replace = AddButton("Thay thế", null, delegate { ReplaceSelected(); });
            AddButton("Thêm mới", "primary", delegate { AddNew(); });
            CancelButton = exit;

            _list.SelectedIndexChanged += delegate { UpdateButtons(); };
            _list.ItemSelectionChanged += delegate { UpdateButtons(); };
            Body.Controls.Add(_list);
            Reload();
        }

        private void UpdateButtons()
        {
            bool has = _list.SelectedItems.Count == 1;
            _replace.Enabled = has;
            _delete.Enabled = has;
        }

        private void Reload()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            _list.Groups.Clear();
            foreach (Image im in _thumbs.Images)
                im.Dispose();
            _thumbs.Images.Clear();

            var groups = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRecord r in _repo.LoadAll())
            {
                string g = string.IsNullOrWhiteSpace(r.Name) ? "(Chưa đặt tên)" : r.Name;
                ListViewGroup grp;
                if (!groups.TryGetValue(g, out grp))
                {
                    grp = new ListViewGroup(g, g);
                    groups[g] = grp;
                    _list.Groups.Add(grp);
                }
                _thumbs.Images.Add(ImageUtil.Thumb(r.Data, _thumbs.ImageSize.Width));
                var item = new ListViewItem("", _thumbs.Images.Count - 1) { Tag = r, ToolTipText = g, Group = grp };
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            SubtitleText = _list.Items.Count + " ảnh";
            UpdateButtons();
        }

        private ImageRecord Selected()
        {
            return _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as ImageRecord : null;
        }

        private void AddNew()
        {
            using (var dlg = new OpenFileDialog { Filter = ImageUtil.FileFilter, Multiselect = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                string defName = Path.GetFileNameWithoutExtension(dlg.FileNames[0]);
                string name = PromptDialog.Ask(this, "Tên ảnh", "Nhập tên (ảnh cùng tên sẽ xếp chung một nhóm)", defName);
                if (name == null)
                    return;
                try
                {
                    foreach (string f in dlg.FileNames)
                    {
                        byte[] data;
                        try
                        {
                            data = ImageUtil.ReadValidated(f);
                        }
                        catch (Exception)
                        {
                            Msg.Warning(this, ImageUtil.LoadError);
                            continue;
                        }
                        _repo.Add(name, data, AppSession.UserId);
                    }
                }
                catch (DataAccessException ex)
                {
                    Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                }
                Reload();
            }
        }

        private void ReplaceSelected()
        {
            ImageRecord r = Selected();
            if (r == null)
                return;
            byte[] data;
            if (!ImagePickerDialog.Pick(this, "Thay thế ảnh", r.Data, out data) || data == null)
                return;
            try
            {
                _repo.Replace(r.Id, data, AppSession.UserId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return;
            }
            Reload();
        }

        private void DeleteSelected()
        {
            ImageRecord r = Selected();
            if (r == null)
                return;
            string name = string.IsNullOrWhiteSpace(r.Name) ? "(Chưa đặt tên)" : r.Name;
            int used = _repo.CountUsage(r.Id);
            if (used > 0)
            {
                Msg.Warning(this, ReferenceChecker.BlockedMessage("ảnh", name, used));
                return;
            }
            if (!Msg.Confirm(this, "Bạn có chắc chắn muốn xóa ảnh '" + name + "' không?"))
                return;
            try
            {
                _repo.Delete(r.Id);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không xóa được: " + ex.Message);
                return;
            }
            Reload();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _thumbs.Dispose();
            base.Dispose(disposing);
        }
    }
}
