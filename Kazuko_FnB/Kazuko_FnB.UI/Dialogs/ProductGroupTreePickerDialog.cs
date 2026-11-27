using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp chọn Nhóm mặt hàng trên cây nhóm (DNHOMMATHANG, spec docs/specs/danh-muc-khuyen-mai.md §2).
    /// </summary>
    internal sealed class ProductGroupTreePickerDialog : ThemedDialog
    {
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill };
        private readonly FlatButton _btnOk;
        private readonly IList<ProductGroupRow> _groups;

        public ProductGroupRow SelectedGroup { get; private set; }

        internal TreeView TreeForTest { get { return _tree; } }
        internal FlatButton OkButtonForTest { get { return _btnOk; } }

        public ProductGroupTreePickerDialog(IList<ProductGroupRow> groups)
            : base("CHỌN NHÓM MẶT HÀNG", "Chọn một nhóm mặt hàng trên cây nhóm", 440, 420)
        {
            _groups = groups ?? new List<ProductGroupRow>();
            FlatButton cancel = AddButton("Hủy bỏ", null, delegate { Close(); });
            _btnOk = AddButton("Chấp nhận", "primary", delegate { AcceptSelection(); });
            CancelButton = cancel;
            _btnOk.Enabled = false;

            float s = Dpi;
            _tree.Font = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _tree.ItemHeight = (int)(28 * s);
            _tree.HideSelection = false;
            _tree.ShowLines = true;
            _tree.ShowRootLines = true;
            _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _tree.DrawNode += DrawNode;
            _tree.AfterSelect += delegate { _btnOk.Enabled = _tree.SelectedNode != null && _tree.SelectedNode.Tag is ProductGroupRow; };
            _tree.NodeMouseDoubleClick += delegate(object sender, TreeNodeMouseClickEventArgs e)
            {
                if (e.Node != null && e.Node.Tag is ProductGroupRow)
                {
                    _tree.SelectedNode = e.Node;
                    AcceptSelection();
                }
            };
            Body.Controls.Add(_tree);

            PopulateTree();
            ApplyDialogTheme();
        }

        private void PopulateTree()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();

            var byId = new Dictionary<string, ProductGroupRow>(StringComparer.Ordinal);
            foreach (ProductGroupRow g in _groups)
                if (!string.IsNullOrEmpty(g.Id))
                    byId[g.Id] = g;

            var byParent = new Dictionary<string, List<ProductGroupRow>>(StringComparer.Ordinal);
            var roots = new List<ProductGroupRow>();
            foreach (ProductGroupRow g in _groups)
            {
                if (string.IsNullOrEmpty(g.ParentId) || !byId.ContainsKey(g.ParentId) || g.ParentId == g.Id)
                {
                    roots.Add(g);
                    continue;
                }
                List<ProductGroupRow> kids;
                if (!byParent.TryGetValue(g.ParentId, out kids))
                    byParent[g.ParentId] = kids = new List<ProductGroupRow>();
                kids.Add(g);
            }

            var cmp = new GroupComparer();
            roots.Sort(cmp);
            foreach (ProductGroupRow r in roots)
                AddNode(_tree.Nodes, r, byParent, cmp, 0);

            _tree.ExpandAll();
            if (_tree.Nodes.Count > 0)
                _tree.SelectedNode = _tree.Nodes[0];
            _tree.EndUpdate();
        }

        private static void AddNode(
            TreeNodeCollection into,
            ProductGroupRow group,
            Dictionary<string, List<ProductGroupRow>> byParent,
            GroupComparer cmp,
            int depth)
        {
            var tn = new TreeNode(group.Name ?? "") { Tag = group };
            into.Add(tn);
            List<ProductGroupRow> kids;
            if (depth < 64 && byParent.TryGetValue(group.Id, out kids))
            {
                kids.Sort(cmp);
                foreach (ProductGroupRow k in kids)
                    AddNode(tn.Nodes, k, byParent, cmp, depth + 1);
            }
        }

        private void AcceptSelection()
        {
            var g = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as ProductGroupRow;
            if (g == null)
                return;
            SelectedGroup = g;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || e.Bounds.IsEmpty)
                return;
            ThemeTokens t = ThemeManager.Current;
            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            Color back = selected ? t.Primary : _tree.BackColor;
            Color fore = selected ? t.OnPrimary : t.Text;
            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, _tree.Font, e.Bounds, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        private sealed class GroupComparer : IComparer<ProductGroupRow>
        {
            private static readonly CompareInfo Vi = new CultureInfo("vi-VN").CompareInfo;

            public int Compare(ProductGroupRow a, ProductGroupRow b)
            {
                int c = string.CompareOrdinal(a.SortOrder ?? "", b.SortOrder ?? "");
                return c != 0 ? c : Vi.Compare(a.Name ?? "", b.Name ?? "", CompareOptions.IgnoreCase);
            }
        }
    }
}
