using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI
{
    /// <summary>Ô chọn có gõ tìm (autocomplete) trên một danh sách <see cref="LookupItem"/>; gõ chữ không có trong danh sách thì bỏ chọn.</summary>
    internal sealed class LookupCombo : ComboBox
    {
        public event EventHandler ValueChanged;

        public LookupCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDown;
            AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            AutoCompleteSource = AutoCompleteSource.ListItems;
            DisplayMember = "Name";
            SelectedIndexChanged += delegate { Raise(); };
        }

        public void SetItems(IList<LookupItem> items)
        {
            BeginUpdate();
            Items.Clear();
            foreach (LookupItem i in items)
                Items.Add(i);
            EndUpdate();
            SelectedIndex = -1;
        }

        public LookupItem Selected
        {
            get { return SelectedItem as LookupItem; }
        }

        public string SelectedId
        {
            get { return Selected == null ? null : Selected.Id; }
        }

        /// <summary>Chọn theo ID (null = bỏ chọn).</summary>
        public void SelectId(string id)
        {
            for (int i = 0; i < Items.Count; i++)
                if (((LookupItem)Items[i]).Id == id)
                {
                    SelectedIndex = i;
                    return;
                }
            SelectedIndex = -1;
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            // Chữ gõ dở không khớp mục nào thì xóa, để không hiểu nhầm là đã chọn.
            if (Selected == null && !string.IsNullOrEmpty(Text))
            {
                Text = "";
                SelectedIndex = -1;
            }
        }

        private void Raise()
        {
            var h = ValueChanged;
            if (h != null) h(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Dựng một màn nhập liệu từ <see cref="ScreenLayout"/> (suy ra từ layout gốc). Không chép tọa độ tuyệt đối:
    /// các control cùng hàng (theo tọa độ gốc) được gom thành một hàng, giữ thứ tự trái → phải và tỷ lệ độ rộng;
    /// hàng xếp đều nhau, nhãn nằm bên trái ô nhập (hoặc phía trên nếu layout gốc đặt nhãn trên ô).
    /// Màu và cỡ chữ theo theme và DPI. Trường bắt buộc có dấu * ở nhãn.
    /// </summary>
    internal sealed class DynamicForm : Panel
    {
        private sealed class Item
        {
            public LayoutControl Label;
            public LayoutControl Edit;
            public FieldMap Map;
            public bool LabelAbove;
            public Label LabelCtl;
            public Control EditCtl;
            public int Left { get { return Label != null ? Math.Min(Label.X, Edit.X) : Edit.X; } }
            public int Right { get { return Edit.X + Edit.Width; } }
        }

        private sealed class Row
        {
            public int CenterY;
            public List<Item> Items = new List<Item>();
            public bool HasLabelAbove;
        }

        private readonly ScreenLayout _layout;
        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<string, Item> _byEditor = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        private readonly Font _font = new Font("Segoe UI", 9f);
        private readonly List<string> _skipped = new List<string>();
        private ThemeTokens _theme = ThemeManager.Current;
        private bool _built;

        public DynamicForm(ScreenLayout layout)
        {
            _layout = layout;
            DoubleBuffered = true;
            AutoScroll = true;
            BuildItems();
            BuildRows();
            CreateControls();
            _built = true;
            Relayout();
            ApplyTheme(_theme);
        }

        /// <summary>Control không dựng được (loại chưa hỗ trợ), để báo cáo/kiểm thử.</summary>
        public IList<string> Skipped
        {
            get { return _skipped; }
        }

        public IEnumerable<string> EditorNames
        {
            get { return _byEditor.Keys; }
        }

        /// <summary>Ô nhập theo tên control trong layout gốc (null nếu không có).</summary>
        public Control Editor(string name)
        {
            Item i;
            return _byEditor.TryGetValue(name, out i) ? i.EditCtl : null;
        }

        public Label LabelOf(string editorName)
        {
            Item i;
            return _byEditor.TryGetValue(editorName, out i) ? i.LabelCtl : null;
        }

        public FieldMap MapOf(string editorName)
        {
            Item i;
            return _byEditor.TryGetValue(editorName, out i) ? i.Map : null;
        }

        /// <summary>Chiều cao nội dung cần có (px thiết bị) ở độ rộng hiện tại.</summary>
        public int ContentHeight
        {
            get { return RowsHeight() + Padding.Vertical; }
        }

        // ------------------------------------------------------------------ Dựng mô hình

        private static bool IsEditor(LayoutKind k)
        {
            return k == LayoutKind.Text || k == LayoutKind.Number || k == LayoutKind.Date
                || k == LayoutKind.Combo || k == LayoutKind.Lookup || k == LayoutKind.Check;
        }

        private void BuildItems()
        {
            var items = new List<Item>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (FieldMap m in _layout.Maps)
            {
                if (string.IsNullOrEmpty(m.EditControl))
                    continue;
                LayoutControl edit = _layout.Find(m.EditControl);
                if (edit == null || !IsEditor(edit.Kind))
                {
                    _skipped.Add(m.EditControl);
                    continue;
                }
                LayoutControl label = string.IsNullOrEmpty(m.LabelControl) ? null : _layout.Find(m.LabelControl);
                if (label != null && label.Kind != LayoutKind.Label)
                    label = null;
                var it = new Item { Edit = edit, Label = label, Map = m };
                it.LabelAbove = label != null && label.Y + label.Height <= edit.Y + 2 && Math.Abs(label.X - edit.X) < 60;
                items.Add(it);
                used.Add(edit.Name);
                if (label != null)
                    used.Add(label.Name);
            }

            // Ô nhập không có dòng ánh xạ (vd. không có trong mapper) vẫn hiện.
            foreach (LayoutControl c in _layout.Controls)
                if (IsEditor(c.Kind) && !used.Contains(c.Name))
                {
                    items.Add(new Item { Edit = c });
                    used.Add(c.Name);
                }

            foreach (LayoutControl c in _layout.Controls)
                if (c.Kind == LayoutKind.Unknown && !used.Contains(c.Name) && c.TypeName != null && !c.TypeName.StartsWith("Krypton", StringComparison.Ordinal))
                    _skipped.Add(c.Name + " (" + c.TypeName + ")");

            // Gom hàng theo tâm dọc của ô nhập (sai lệch ≤ 8 px gốc là cùng hàng).
            items.Sort(delegate (Item a, Item b) { return a.Edit.CenterY.CompareTo(b.Edit.CenterY); });
            foreach (Item it in items)
            {
                Row row = null;
                foreach (Row r in _rows)
                    if (Math.Abs(r.CenterY - it.Edit.CenterY) <= 8)
                    {
                        row = r;
                        break;
                    }
                if (row == null)
                {
                    row = new Row { CenterY = it.Edit.CenterY };
                    _rows.Add(row);
                }
                row.Items.Add(it);
                row.HasLabelAbove |= it.LabelAbove;
                _byEditor[it.Edit.Name] = it;
            }
            _rows.Sort(delegate (Row a, Row b) { return a.CenterY.CompareTo(b.CenterY); });
            foreach (Row r in _rows)
                r.Items.Sort(delegate (Item a, Item b) { return a.Left.CompareTo(b.Left); });
        }

        private void BuildRows()
        {
            // Căn thẳng các ô nhập cùng cột: X lệch ≤ 3 px gốc thì coi là một.
            var xs = new List<int>();
            foreach (Row r in _rows)
                foreach (Item it in r.Items)
                    if (it.Label != null && !it.LabelAbove)
                        xs.Add(it.Edit.X);
            xs.Sort();
            var snap = new Dictionary<int, int>();
            int i = 0;
            while (i < xs.Count)
            {
                int j = i;
                while (j + 1 < xs.Count && xs[j + 1] - xs[i] <= 3)
                    j++;
                for (int k = i; k <= j; k++)
                    snap[xs[k]] = xs[i];
                i = j + 1;
            }
            foreach (Row r in _rows)
                foreach (Item it in r.Items)
                {
                    int sx;
                    if (it.Label != null && !it.LabelAbove && snap.TryGetValue(it.Edit.X, out sx) && sx != it.Edit.X)
                    {
                        it.Edit.Width += it.Edit.X - sx;
                        it.Edit.X = sx;
                    }
                }
        }

        // ------------------------------------------------------------------ Dựng control

        private void CreateControls()
        {
            int order = 0;
            var all = new List<Item>();
            foreach (Row r in _rows)
                all.AddRange(r.Items);
            all.Sort(delegate (Item a, Item b) { return a.Edit.TabIndex.CompareTo(b.Edit.TabIndex); });

            foreach (Item it in all)
            {
                Control ctl = CreateEditor(it.Edit);
                ctl.Name = it.Edit.Name;
                ctl.Enabled = it.Edit.Enabled;
                ctl.TabIndex = order++;
                it.EditCtl = ctl;
                Controls.Add(ctl);

                if (it.Label != null)
                {
                    string text = it.Label.Text;
                    if (it.Map != null && it.Map.Required)
                        text += " *";
                    var lbl = new Label
                    {
                        Text = text,
                        AutoSize = false,
                        UseMnemonic = false,
                        Name = it.Label.Name,
                        AutoEllipsis = true,
                        TextAlign = it.LabelAbove ? ContentAlignment.BottomLeft : ContentAlignment.MiddleLeft,
                        Font = _font,
                        TabStop = false
                    };
                    it.LabelCtl = lbl;
                    Controls.Add(lbl);
                }
            }
        }

        private Control CreateEditor(LayoutControl c)
        {
            switch (c.Kind)
            {
                case LayoutKind.Number:
                {
                    var n = new NumericUpDown { Font = _font, TextAlign = HorizontalAlignment.Right };
                    int dp;
                    if (int.TryParse(c.Prop("DecimalPlaces"), NumberStyles.Integer, CultureInfo.InvariantCulture, out dp))
                        n.DecimalPlaces = Math.Max(0, Math.Min(6, dp));
                    n.ThousandsSeparator = string.Equals(c.Prop("ThousandsSeparator"), "True", StringComparison.OrdinalIgnoreCase);
                    decimal min, max;
                    n.Minimum = decimal.TryParse(c.Prop("Minimum"), NumberStyles.Any, CultureInfo.InvariantCulture, out min) ? min : 0m;
                    n.Maximum = decimal.TryParse(c.Prop("Maximum"), NumberStyles.Any, CultureInfo.InvariantCulture, out max) ? max : 1000000000m;
                    return n;
                }
                case LayoutKind.Date:
                    return new DateTimePicker { Font = _font, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
                case LayoutKind.Combo:
                {
                    var cb = new ComboBox { Font = _font, DropDownStyle = ComboBoxStyle.DropDownList };
                    foreach (string s in c.Items)
                        cb.Items.Add(s);
                    cb.SelectedIndex = -1;
                    return cb;
                }
                case LayoutKind.Lookup:
                    return new LookupCombo { Font = _font };
                case LayoutKind.Check:
                    return new CheckBox { Font = _font, Text = c.Text, AutoSize = false, UseMnemonic = false };
                default:
                    return new TextBox { Font = _font };
            }
        }

        // ------------------------------------------------------------------ Xếp hàng

        private float Dpi
        {
            get { return DeviceDpi / 96f; }
        }

        private int EditHeight
        {
            get { return (int)(26 * Dpi); }
        }

        private int RowGap
        {
            get { return (int)(8 * Dpi); }
        }

        private int LabelAboveHeight
        {
            get { return (int)(18 * Dpi); }
        }

        private int RowsHeight()
        {
            int h = 0;
            foreach (Row r in _rows)
                h += RowHeight(r) + RowGap;
            return h;
        }

        private int RowHeight(Row r)
        {
            return EditHeight + (r.HasLabelAbove ? LabelAboveHeight : 0);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Relayout();
        }

        private void Relayout()
        {
            // Chưa dựng xong control (Controls.Add kích hoạt layout giữa chừng) thì chưa xếp.
            if (!_built || _layout.Width <= 0 || _rows.Count == 0)
                return;
            int inner = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            float u = inner / (float)_layout.Width;
            int gap = (int)(6 * Dpi);

            // Cột ô nhập đủ chỗ cho nhãn dài nhất của cột đó (đo chữ thật), phần dư lấy từ độ rộng ô nhập.
            var needEx = new Dictionary<int, int>();
            foreach (Row r in _rows)
                foreach (Item it in r.Items)
                {
                    if (it.LabelCtl == null || it.LabelAbove)
                        continue;
                    int lx = Padding.Left + (int)Math.Round(it.Label.X * u);
                    int need = lx + TextRenderer.MeasureText(it.LabelCtl.Text, _font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + gap;
                    int cur;
                    if (!needEx.TryGetValue(it.Edit.X, out cur) || need > cur)
                        needEx[it.Edit.X] = need;
                }

            int y = Padding.Top;
            foreach (Row r in _rows)
            {
                int pushed = 0;
                int rowTop = y;
                int editTop = rowTop + (r.HasLabelAbove ? LabelAboveHeight : 0);
                for (int idx = 0; idx < r.Items.Count; idx++)
                {
                    Item it = r.Items[idx];
                    Item next = idx + 1 < r.Items.Count ? r.Items[idx + 1] : null;
                    int ex = Padding.Left + (int)Math.Round(it.Edit.X * u);
                    int right = Padding.Left + (int)Math.Round((it.Edit.X + it.Edit.Width) * u);
                    int need;
                    if (it.LabelCtl != null && !it.LabelAbove && needEx.TryGetValue(it.Edit.X, out need) && need > ex)
                        ex = need;
                    // Ô tích phía trước cần thêm chỗ: đẩy ô này sang phải (thu hẹp bớt độ rộng).
                    if (pushed > 0)
                        ex += pushed;
                    // Không để ô nhập tràn ra ngoài mép phải.
                    right = Math.Min(right, Padding.Left + inner);
                    int ew = Math.Max((int)(40 * Dpi), right - ex);

                    if (it.EditCtl is CheckBox)
                    {
                        // Đủ rộng cho chữ (layout gốc dùng cỡ chữ nhỏ hơn), nhưng không đè lên ô kế bên.
                        int want = TextRenderer.MeasureText(it.EditCtl.Text, _font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + (int)(38 * Dpi);
                        int cw = Math.Max(ew, want);
                        if (next != null)
                        {
                            int avail = Padding.Left + (int)Math.Round(next.Left * u) - gap - ex;
                            pushed = Math.Max(0, cw - Math.Max(avail, ew));
                        }
                        it.EditCtl.SetBounds(ex, editTop, cw, EditHeight);
                    }
                    else
                    {
                        it.EditCtl.SetBounds(ex, editTop, ew, EditHeight);
                    }

                    if (it.LabelCtl != null)
                    {
                        if (it.LabelAbove)
                        {
                            it.LabelCtl.SetBounds(ex, rowTop, ew, LabelAboveHeight);
                        }
                        else
                        {
                            int lx = Padding.Left + (int)Math.Round(it.Label.X * u) + pushed;
                            int lw = Math.Max(1, ex - lx - gap);
                            it.LabelCtl.SetBounds(lx, editTop, lw, EditHeight);
                        }
                    }
                }
                y += RowHeight(r) + RowGap;
            }
            AutoScrollMinSize = new Size(0, y + Padding.Bottom);
        }

        // ------------------------------------------------------------------ Giao diện

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            BackColor = t.Background;
            foreach (Row r in _rows)
                foreach (Item it in r.Items)
                {
                    if (it.LabelCtl != null)
                    {
                        it.LabelCtl.BackColor = Color.Transparent;
                        it.LabelCtl.ForeColor = t.Text;
                    }
                    Control c = it.EditCtl;
                    if (c == null)
                        continue;
                    if (c is CheckBox)
                    {
                        c.BackColor = Color.Transparent;
                        c.ForeColor = t.Text;
                    }
                    else if (c is DateTimePicker)
                    {
                        // DateTimePicker không tô được nền ô chọn; chỉ chỉnh màu chữ/nền phần hiển thị.
                        c.BackColor = t.Surface;
                        c.ForeColor = t.Text;
                    }
                    else
                    {
                        var combo = c as ComboBox;
                        if (combo != null)
                            combo.FlatStyle = FlatStyle.Standard;
                        c.BackColor = c.Enabled ? t.Surface : Draw.Mix(t.Background, t.TextMuted, 0.14f);
                        c.ForeColor = t.Text;
                    }
                }
            Invalidate(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _font.Dispose();
            base.Dispose(disposing);
        }
    }
}
