using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Nhóm loại control trong layout gốc (gom các lớp No1Lib.* và System.Windows.Forms.* về vài loại).</summary>
    public enum LayoutKind
    {
        Unknown,
        Label,
        Text,
        Number,
        Date,
        Combo,
        Lookup,
        Check,
        Button,
        Container,
        Mapper
    }

    /// <summary>Một control trong <c>SFORM.AELAYOUT</c>: loại, tên, chữ, vị trí gốc, thuộc tính.</summary>
    public sealed class LayoutControl
    {
        public LayoutControl()
        {
            Items = new List<string>();
            Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Name { get; set; }
        /// <summary>Tên lớp rút gọn, vd. "No1TextBox".</summary>
        public string TypeName { get; set; }
        public LayoutKind Kind { get; set; }
        public string Text { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int TabIndex { get; set; }
        public bool Enabled { get; set; }
        /// <summary>Danh sách mục của ô chọn (ComboBox).</summary>
        public List<string> Items { get; private set; }
        public Dictionary<string, string> Props { get; private set; }

        public int CenterY
        {
            get { return Y + Height / 2; }
        }

        public string Prop(string name)
        {
            string v;
            return Props.TryGetValue(name, out v) ? v : null;
        }
    }

    /// <summary>Một dòng ánh xạ trường của <c>AeController</c>: cột dữ liệu ↔ nhãn ↔ ô nhập.</summary>
    public sealed class FieldMap
    {
        public string DataField { get; set; }
        /// <summary>false = bắt buộc nhập (<c>AllowEmpty = False</c>); null = layout không nói gì.</summary>
        public bool? AllowEmpty { get; set; }
        public bool? AllowDuplicate { get; set; }
        public string LabelControl { get; set; }
        public string EditControl { get; set; }

        public bool Required
        {
            get { return AllowEmpty.HasValue && !AllowEmpty.Value; }
        }
    }

    /// <summary>Mô tả một màn hình nhập liệu suy ra từ layout gốc.</summary>
    public sealed class ScreenLayout
    {
        public ScreenLayout()
        {
            Controls = new List<LayoutControl>();
            Maps = new List<FieldMap>();
        }

        public int Width { get; set; }
        public int Height { get; set; }
        /// <summary>Bảng đích của form (thuộc tính Table của AeController); null nếu layout không nêu.</summary>
        public string Table { get; set; }
        public List<LayoutControl> Controls { get; private set; }
        public List<FieldMap> Maps { get; private set; }

        public LayoutControl Find(string name)
        {
            foreach (LayoutControl c in Controls)
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    return c;
            return null;
        }

        public FieldMap MapOfEditor(string controlName)
        {
            foreach (FieldMap m in Maps)
                if (string.Equals(m.EditControl, controlName, StringComparison.OrdinalIgnoreCase))
                    return m;
            return null;
        }
    }
}
