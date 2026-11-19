using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Controls
{
    /// <summary>Ô chọn ảnh từ thư viện (SIMAGE): mỗi dòng có ảnh nhỏ và tên. Giá trị = ID ảnh (null/"" = không chọn).</summary>
    internal sealed class ImageCombo : ComboBox
    {
        private sealed class Entry
        {
            public string Id;
            public string Name;
            public Bitmap Thumb;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private int _size;

        public ImageCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
        }

        public void Load(IEnumerable<ImageRecord> images, int thumbSize)
        {
            _size = thumbSize;
            ItemHeight = thumbSize + 6;
            Items.Clear();
            foreach (Entry e in _entries)
                if (e.Thumb != null)
                    e.Thumb.Dispose();
            _entries.Clear();
            _entries.Add(new Entry { Id = "", Name = "" });
            foreach (ImageRecord r in images)
                _entries.Add(new Entry { Id = r.Id, Name = r.Name, Thumb = ImageUtil.Thumb(r.Data, thumbSize) });
            foreach (Entry e in _entries)
                Items.Add(e.Name);
            SelectedIndex = 0;
        }

        public string SelectedId
        {
            get { return SelectedIndex >= 0 && SelectedIndex < _entries.Count ? _entries[SelectedIndex].Id : ""; }
            set
            {
                for (int i = 0; i < _entries.Count; i++)
                    if (string.Equals(_entries[i].Id, value ?? "", StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedIndex = i;
                        return;
                    }
                SelectedIndex = 0;
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _entries.Count)
                return;
            e.DrawBackground();
            Entry en = _entries[e.Index];
            int pad = 3;
            if (en.Thumb != null)
                e.Graphics.DrawImage(en.Thumb, e.Bounds.X + pad, e.Bounds.Y + pad, _size, _size);
            Rectangle text = new Rectangle(e.Bounds.X + _size + pad * 3, e.Bounds.Y, e.Bounds.Width - _size - pad * 3, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, en.Name, Font, text, e.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (Entry e in _entries)
                    if (e.Thumb != null)
                        e.Thumb.Dispose();
            base.Dispose(disposing);
        }
    }
}
