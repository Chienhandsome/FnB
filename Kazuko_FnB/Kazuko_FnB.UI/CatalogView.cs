using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn danh mục dùng chung (chỉ xem): cây nhóm bên trái (nếu danh mục có nhóm), lưới bên phải, ô tìm (F3), Làm mới.
    /// Cột, nhãn, thứ tự lấy từ metadata của DB (SCOLUMN/SGRID); chỉ hiện dòng đang dùng (STATUS = 30).
    /// Tìm không phân biệt hoa thường và dấu tiếng Việt. Chọn một nhóm thì lọc theo nhóm đó và mọi nhóm con.
    /// Hai danh mục đã có Thêm/Sửa/Xóa: Danh mục mặt hàng (DMATHANG, _productMode) và Danh mục bàn khu vực
    /// (DBAN, _roomMode, docs/specs/danh-muc-ban-khu-vuc.md); các danh mục khác vẫn chỉ xem (xem docs/specs/goi-demo-ui.md, B2).
    /// </summary>
    internal sealed partial class CatalogView : Panel
    {
        private const string ColSearch = "_search";
        private const string ColCategory = "_cat";
        private const string ColId = "_id";

        private readonly string _databasePath;
        private readonly string _stableDescId;
        private readonly string _title;

        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly InputBox _search = new InputBox(); // ô tìm bo góc, chữ gợi ý nhạt theo theme
        private readonly FlatButton _refresh = new FlatButton { Text = "Làm mới" };
        private readonly Label _count = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
        private readonly DataGridView _grid = new DataGridView();
        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill };

        private CatalogDefinition _def;
        private DataTable _table;
        private DataView _view;
        private int _total;
        private string _error;
        private ThemeTokens _theme = ThemeManager.Current;
        private Color _page = ThemeManager.Current.Background;
        private bool _loading;
        private bool _splitPlaced;

        // ---- Chế độ Danh mục mặt hàng (DMATHANG, spec docs/specs/danh-muc-mat-hang.md §1.1) ----
        private const string UnsetTag = "<unset>";
        private const string UnsupportedText = "Chức năng chưa hỗ trợ trong phiên bản này.";
        private bool _productMode;
        private List<ProductRow> _productRows = new List<ProductRow>();
        private ProductRepository _productRepo;
        private ProductGroupRepository _groupRepo;
        private bool _canAdd;
        private bool _canEdit;
        private bool _canRemove;
        private bool _canViewBuyPrice;
        private readonly FlatButton _btnAdd = new FlatButton { Text = "Thêm mới" };
        private readonly FlatButton _btnEdit = new FlatButton { Text = "Chỉnh sửa" };
        private readonly FlatButton _btnRemove = new FlatButton { Text = "Xóa" };
        private readonly FlatButton _btnExcelAdd = new FlatButton { Text = "Thêm excel" };
        private readonly FlatButton _btnExcelExport = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnExcelImport = new FlatButton { Text = "Import định lượng" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In" };
        private readonly FlatButton _btnPrintBarcode = new FlatButton { Text = "In mã vạch" };
        private readonly Panel _groupBar = new Panel { Dock = DockStyle.Top, Visible = false };
        // Icon vector (Segoe MDL2 Assets, như InputBox) thay cho ký tự "+" và "✎" bị răng cưa.
        // Nút icon nhỏ: glyph 10 pt, viền mảnh 1.2 để khỏi nặng hơn các nút chữ trên thanh.
        private readonly FlatButton _groupAdd = new FlatButton { Text = "", Font = new Font("Segoe MDL2 Assets", 10f), BorderWidth = 1.2f };
        private readonly FlatButton _groupEdit = new FlatButton { Text = "", Font = new Font("Segoe MDL2 Assets", 10f), BorderWidth = 1.2f };
        private readonly Panel _bottom = new Panel { Dock = DockStyle.Bottom, Visible = false };
        private readonly PaneSplitter _paneSplitter = new PaneSplitter { Visible = false };
        private readonly TabControl _infoTabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private Label _areaEmpty;
        private readonly Label _lblCreated = new Label { AutoSize = true };
        private readonly Label _lblCreatedBy = new Label { AutoSize = true };
        private readonly Label _lblModified = new Label { AutoSize = true };
        private readonly Label _lblModifiedBy = new Label { AutoSize = true };
        private readonly DataGridView _areaGrid = new DataGridView();
        private ThemedContextMenu _gridMenu;
        private ThemedContextMenu _treeMenu;
        private ToolStripMenuItem _colSubmenu;
        private Font _removeFont;

        // ---- Chế độ Danh mục bàn khu vực (DBAN, spec docs/specs/danh-muc-ban-khu-vuc.md §1) ----
        // Cây khu vực (DKHUVUC) + lưới bàn (DBAN). Quyền dùng chung với "Danh mục bàn khu vực" (SFUNCTION
        // 194c969a-962f-47c8-b808-0330e2a60350): Thêm nhanh + quản lý khu vực đi theo quyền Thêm/Sửa (Q13).
        private bool _roomMode;
        private List<RoomRow> _roomRows = new List<RoomRow>();
        private RoomRepository _roomRepo;
        private AreaRepository _areaRepo;
        private readonly FlatButton _btnQuickAdd = new FlatButton { Text = "Thêm nhanh" };
        private readonly FlatButton _btnTotal = new FlatButton { Text = "Tổng" };
        private readonly ThemedContextMenu _roomGridMenu = new ThemedContextMenu();
        private readonly ThemedContextMenu _roomTreeMenu = new ThemedContextMenu();
        private TabPage _areaTabPage;
        // Các tab liên quan dưới lưới bàn (Bảng giá theo bàn, Đặt hàng, Đơn hàng…), dựng theo metadata như app gốc.
        private DetailTabsHost _roomDetail;

        public CatalogView(string databasePath, string stableDescId, string title)
        {
            _databasePath = databasePath;
            _stableDescId = stableDescId;
            _title = title;
            DoubleBuffered = true;
            float s = DeviceDpi / 96f;

            _bar.Height = (int)(52 * s);
            _bar.Controls.Add(_search);
            _bar.Controls.Add(_refresh);
            _bar.Controls.Add(_count);

            _search.Inner.Font = new Font("Segoe UI", 10f);
            _search.Placeholder = "Tìm... (F3)";
            _search.Height = (int)(32 * s); // cao bằng nút trên thanh (LayoutBar: bar 52 - 2×lề 10)
            _search.TextChanged += delegate { ApplyFilter(); };
            _refresh.Click += delegate { Reload(); };
            _count.Font = new Font("Segoe UI", 9.5f);
            _empty.Font = new Font("Segoe UI", 11f);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.RowTemplate.Height = (int)(28 * s);
            _grid.Font = new Font("Segoe UI", 10f);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.AccessibleName = _title;
            typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_grid, true, null);
            _grid.DataBindingComplete += delegate { _grid.ClearSelection(); };
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
            _grid.RowPostPaint += DrawRowNumber;
            _grid.SelectionChanged += delegate
            {
                if (!_loading)
                {
                    if (_productMode)
                    {
                        UpdateAuditPanel();
                        UpdateProductButtons();
                    }
                    else if (_roomMode)
                    {
                        UpdateRoomAuditPanel();
                        UpdateRoomButtons();
                    }
                    else if (_customerMode)
                    {
                        UpdateCustomerAuditPanel();
                        UpdateCustomerButtons();
                    }
                    else if (IsFundCatalogMode)
                    {
                        UpdateFundCatalogAuditPanel();
                        UpdateFundCatalogButtons();
                    }
                    else if (_warehouseMode)
                    {
                        UpdateWarehouseAuditPanel();
                        UpdateWarehouseButtons();
                    }
                    else if (_storeMode)
                    {
                        UpdateStoreAuditPanel();
                        UpdateStoreButtons();
                    }
                    else if (_supplierMode)
                    {
                        UpdateSupplierAuditPanel();
                        UpdateSupplierButtons();
                    }
                    else if (_giftVoucherMode)
                    {
                        UpdateGiftVoucherAuditPanel();
                        UpdateGiftVoucherButtons();
                    }
                    else if (_promotionMode)
                    {
                        UpdatePromotionAuditPanel();
                        UpdatePromotionButtons();
                    }
                    else if (_employeeMode)
                    {
                        UpdateEmployeeAuditPanel();
                        UpdateEmployeeButtons();
                    }
                    else if (_shiftMode)
                    {
                        UpdateShiftAuditPanel();
                        UpdateShiftButtons();
                    }
                }
            };
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0)
                    return;
                if (_productMode)
                    EditProduct();
                else if (_roomMode)
                    EditRoom();
                else if (_customerMode)
                    OpenCustomerFromGrid();
                else if (IsFundCatalogMode)
                    OpenFundCatalogFromGrid();
                else if (_warehouseMode)
                    OpenWarehouseFromGrid();
                else if (_storeMode)
                    OpenStoreFromGrid();
                else if (_supplierMode)
                    OpenSupplierFromGrid();
                else if (_giftVoucherMode)
                    OpenGiftVoucherFromGrid();
                else if (_promotionMode)
                    OpenPromotionFromGrid();
                else if (_employeeMode)
                    OpenEmployeeFromGrid();
                else if (_shiftMode)
                    OpenShiftFromGrid();
            };
            _grid.MouseDown += GridMouseDown;
            _tree.MouseDown += TreeMouseDown;
            _tree.NodeMouseDoubleClick += TreeNodeMouseDoubleClick;
            _tree.AfterLabelEdit += TreeAfterLabelEdit;

            _tree.HideSelection = false;
            _tree.ShowLines = true;
            _tree.ShowRootLines = true;
            _tree.Font = new Font("Segoe UI", 10f);
            _tree.ItemHeight = (int)(26 * s);
            _tree.ShowNodeToolTips = true;
            _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            _tree.DrawNode += DrawTreeNode;
            _tree.AfterSelect += delegate
            {
                ApplyFilter();
                if (_productMode)
                    UpdateProductButtons();
                else if (_roomMode)
                    UpdateRoomButtons();
                else if (_customerMode)
                    UpdateCustomerButtons();
                else if (_supplierMode)
                    UpdateSupplierButtons();
                else if (_giftVoucherMode)
                    UpdateGiftVoucherButtons();
            };

            _split.FixedPanel = FixedPanel.Panel1;
            _split.Panel1.Controls.Add(_tree);
            _split.Panel2.Controls.Add(_grid);
            _split.Panel1Collapsed = true;
            _split.SizeChanged += delegate { PlaceSplit(); };

            Controls.Add(_split);
            Controls.Add(_bar);
            _bar.Resize += delegate { LayoutBar(); };

            BuildProductUi();
            BuildRoomUi();
            BuildCustomerUi();
            BuildFundCatalogUi();
            BuildWarehouseUi();
            BuildStoreUi();
            BuildSupplierUi();
            BuildGiftVoucherUi();
            BuildPromotionUi();
            BuildEmployeeUi();
            BuildShiftUi();
            Reload();
            ApplyTheme(_theme, _page);
        }

        /// <summary>Số dòng đang hiện trên lưới (sau tìm/lọc nhóm).</summary>
        public int VisibleRows
        {
            get { return _view == null ? 0 : _view.Count; }
        }

        public int TotalRows
        {
            get { return _total; }
        }

        public string ErrorText
        {
            get { return _error; }
        }

        public CatalogDefinition Definition
        {
            get { return _def; }
        }

        // ------------------------------------------------------------------ Nạp dữ liệu

        public void Reload()
        {
            _loading = true;
            try
            {
                _error = null;
                var repo = new CatalogRepository(_databasePath);
                _def = repo.LoadDefinition(_stableDescId);
                if (_def == null)
                    throw new InvalidOperationException("Không tìm thấy định nghĩa danh mục.");
                CatalogData data = repo.Load(_def);
                _productMode = string.Equals(_def.TableName, "DMATHANG", StringComparison.OrdinalIgnoreCase);
                _roomMode = string.Equals(_def.TableName, "DBAN", StringComparison.OrdinalIgnoreCase);
                _customerMode = string.Equals(_def.TableName, "DKHACHHANG", StringComparison.OrdinalIgnoreCase);
                _reasonMode = string.Equals(_def.TableName, "DLYDOTHUCHI", StringComparison.OrdinalIgnoreCase);
                _bankAccountMode = string.Equals(_def.TableName, "DTAIKHOANNGANHANG", StringComparison.OrdinalIgnoreCase);
                _warehouseMode = string.Equals(_def.TableName, "DKHOHANG", StringComparison.OrdinalIgnoreCase);
                _storeMode = string.Equals(_def.TableName, "DCUAHANG", StringComparison.OrdinalIgnoreCase);
                _supplierMode = string.Equals(_def.TableName, "DNHACUNGCAP", StringComparison.OrdinalIgnoreCase);
                _giftVoucherMode = string.Equals(_def.TableName, "DVOUCHER", StringComparison.OrdinalIgnoreCase);
                _promotionMode = string.Equals(_def.TableName, "DDOTKHUYENMAI", StringComparison.OrdinalIgnoreCase);
                _employeeMode = string.Equals(_def.TableName, "DNHANVIEN", StringComparison.OrdinalIgnoreCase);
                _shiftMode = string.Equals(_def.TableName, "DCALAMVIEC", StringComparison.OrdinalIgnoreCase);
                _grid.MultiSelect = _productMode;
                if (_productMode)
                {
                    LoadProductData(data);
                }
                else if (_roomMode)
                {
                    LoadRoomData(data);
                }
                else if (_customerMode)
                {
                    LoadCustomerData(data);
                }
                else if (IsFundCatalogMode)
                {
                    LoadFundCatalogData(data);
                }
                else if (_warehouseMode)
                {
                    LoadWarehouseData(data);
                }
                else if (_storeMode)
                {
                    LoadStoreData(data);
                }
                else if (_supplierMode)
                {
                    LoadSupplierData(data);
                }
                else if (_giftVoucherMode)
                {
                    LoadGiftVoucherData(data);
                }
                else if (_promotionMode)
                {
                    LoadPromotionData(data);
                }
                else if (_employeeMode)
                {
                    LoadEmployeeData(data);
                }
                else if (_shiftMode)
                {
                    LoadShiftData(data);
                }
                else
                {
                    _total = data.Rows.Count;
                    BuildColumns();
                    BuildTree(data);
                    BuildTable(data);
                }
                if (!_productMode)
                    HideBarcodePanel();
                if (!_giftVoucherMode)
                {
                    _chkVoucherUsableOnly.Visible = false;
                    _voucherTotalBar.Visible = false;
                }
                if (!_promotionMode)
                    _promoFilterBar.Visible = false;
                if (_customerMode)
                {
                    ApplyCustomerBarVisibility();
                }
                else if (IsFundCatalogMode)
                {
                    ApplyFundCatalogBarVisibility();
                }
                else if (_warehouseMode)
                {
                    ApplyWarehouseBarVisibility();
                }
                else if (_storeMode)
                {
                    ApplyStoreBarVisibility();
                }
                else if (_supplierMode)
                {
                    ApplySupplierBarVisibility();
                }
                else if (_giftVoucherMode)
                {
                    ApplyGiftVoucherBarVisibility();
                }
                else if (_promotionMode)
                {
                    ApplyPromotionBarVisibility();
                }
                else if (_employeeMode)
                {
                    ApplyEmployeeBarVisibility();
                }
                else if (_shiftMode)
                {
                    ApplyShiftBarVisibility();
                }
                else
                {
                    _groupBar.Visible = _productMode || _roomMode;
                    _bottom.Visible = _productMode || _roomMode;
                    _paneSplitter.Visible = _bottom.Visible;
                    _btnAdd.Visible = _productMode || _roomMode;
                    _btnEdit.Visible = _productMode || _roomMode;
                    _btnRemove.Visible = _productMode || _roomMode;
                    _btnExcelAdd.Visible = _productMode || _roomMode;
                    _btnExcelExport.Visible = _productMode || _roomMode;
                    _btnExcelImport.Visible = _productMode;
                    _btnPrint.Visible = _productMode || _roomMode;
                    _btnPrintBarcode.Visible = _productMode;
                    // Q16: Thêm excel / Xuất excel / In / Tổng có hiện trên toolbar ở chế độ bàn nhưng chưa implement.
                    _btnQuickAdd.Visible = _roomMode;
                    _btnTotal.Visible = _roomMode;
                }
            }
            catch (Exception ex)
            {
                _error = "Không đọc được dữ liệu: " + ex.Message;
                _total = 0;
                _grid.DataSource = null;
                _grid.Columns.Clear();
                _view = null;
            }
            finally
            {
                _loading = false;
            }
            ApplyFilter();
        }

        private void BuildColumns()
        {
            float s = DeviceDpi / 96f;
            var vi = new CultureInfo("vi-VN");
            _grid.DataSource = null;
            _grid.Columns.Clear();
            for (int i = 0; i < _def.Columns.Count; i++)
            {
                CatalogColumn c = _def.Columns[i];
                DataGridViewColumn gc;
                if (c.ClrType == typeof(bool))
                    gc = new DataGridViewCheckBoxColumn();
                else
                    gc = new DataGridViewTextBoxColumn();
                gc.Name = "c" + i;
                gc.DataPropertyName = "c" + i;
                gc.HeaderText = c.Caption;
                gc.SortMode = DataGridViewColumnSortMode.Automatic;
                gc.ReadOnly = true;
                int w = c.Width > 0 ? c.Width : DefaultWidth(c);
                // Đủ chỗ cho tiêu đề (cộng chỗ cho mũi tên sắp xếp), không để tiêu đề bị cắt.
                int headerNeed = TextRenderer.MeasureText(c.Caption, _grid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(30 * s);
                gc.Width = Math.Max(Math.Max((int)(60 * s), (int)(w * s)), headerNeed);
                gc.MinimumWidth = (int)(50 * s);
                if (c.ClrType == typeof(decimal))
                {
                    gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    gc.DefaultCellStyle.Format = string.Equals(c.Format, "n0", StringComparison.OrdinalIgnoreCase) ? "N0" : "#,##0.##";
                    gc.DefaultCellStyle.FormatProvider = vi;
                }
                else if (c.ClrType == typeof(DateTime))
                {
                    gc.DefaultCellStyle.Format = string.Equals(c.Format, "TIME", StringComparison.OrdinalIgnoreCase) ? "HH:mm" : "dd/MM/yyyy";
                }
                else if (c.ClrType == typeof(bool))
                {
                    gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }
                _grid.Columns.Add(gc);
            }
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private static int DefaultWidth(CatalogColumn c)
        {
            if (c.Name == "NAME")
                return 220;
            if (c.ClrType == typeof(bool))
                return 80;
            if (c.ClrType == typeof(decimal))
                return 110;
            if (c.ClrType == typeof(DateTime))
                return 100;
            return 150;
        }

        private readonly Dictionary<string, CatalogNode> _nodes = new Dictionary<string, CatalogNode>(StringComparer.Ordinal);

        private void BuildTree(CatalogData data)
        {
            string selectedId = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as string;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            _nodes.Clear();
            if (_def.CategoryTable == null)
            {
                _split.Panel1Collapsed = true;
                _tree.EndUpdate();
                return;
            }
            _split.Panel1Collapsed = false;
            _splitPlaced = false;
            PlaceSplit();

            foreach (CatalogNode n in data.Nodes)
                _nodes[n.Id] = n;

            var root = new TreeNode("Tất cả") { Tag = null };
            _tree.Nodes.Add(root);
            // Cây có "Chưa thiết lập" ở các chế độ danh mục có cây nhóm.
            if (_productMode || _roomMode || _customerMode || _supplierMode || _giftVoucherMode)
                root.Nodes.Add(new TreeNode("Chưa thiết lập") { Tag = UnsetTag });

            var byParent = new Dictionary<string, List<CatalogNode>>(StringComparer.Ordinal);
            var tops = new List<CatalogNode>();
            foreach (CatalogNode n in data.Nodes)
            {
                if (string.IsNullOrEmpty(n.ParentId) || !_nodes.ContainsKey(n.ParentId) || n.ParentId == n.Id)
                {
                    tops.Add(n);
                    continue;
                }
                List<CatalogNode> list;
                if (!byParent.TryGetValue(n.ParentId, out list))
                    byParent[n.ParentId] = list = new List<CatalogNode>();
                list.Add(n);
            }
            var cmp = new NodeComparer();
            tops.Sort(cmp);
            foreach (CatalogNode n in tops)
                AddNode(root.Nodes, n, byParent, cmp, 0);
            root.Expand();

            TreeNode pick = selectedId == null ? root : FindNode(root, selectedId) ?? root;
            _tree.SelectedNode = pick;
            _tree.EndUpdate();
        }

        /// <summary>Đặt độ rộng cây nhóm khi vùng đã đủ rộng (SplitterDistance chỉ hợp lệ sau khi có kích thước).</summary>
        private void PlaceSplit()
        {
            if (_splitPlaced || _split.Panel1Collapsed)
                return;
            int want = (int)(300 * DeviceDpi / 96f);
            if (_split.Width > want + (int)(200 * DeviceDpi / 96f))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        private static void AddNode(TreeNodeCollection into, CatalogNode n, Dictionary<string, List<CatalogNode>> byParent, NodeComparer cmp, int depth)
        {
            var tn = new TreeNode(n.Name) { Tag = n.Id };
            into.Add(tn);
            List<CatalogNode> kids;
            if (depth < 64 && byParent.TryGetValue(n.Id, out kids))
            {
                kids.Sort(cmp);
                foreach (CatalogNode k in kids)
                    AddNode(tn.Nodes, k, byParent, cmp, depth + 1);
            }
        }

        /// <summary>Tô dòng đang chọn bằng màu của theme (mặc định của Windows là xanh dương, lạc tông).</summary>
        private void DrawTreeNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null || e.Bounds.IsEmpty)
                return;
            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            Color back = selected ? _theme.Primary : _tree.BackColor;
            Color fore = selected ? _theme.OnPrimary : _theme.Text;
            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.NodeFont ?? _tree.Font, e.Bounds, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        private static TreeNode FindNode(TreeNode from, string id)
        {
            foreach (TreeNode c in from.Nodes)
            {
                if ((c.Tag as string) == id)
                    return c;
                TreeNode f = FindNode(c, id);
                if (f != null)
                    return f;
            }
            return null;
        }

        private sealed class NodeComparer : IComparer<CatalogNode>
        {
            private static readonly CompareInfo Vi = new CultureInfo("vi-VN").CompareInfo;

            public int Compare(CatalogNode a, CatalogNode b)
            {
                int c = string.CompareOrdinal(a.SortOrder ?? "", b.SortOrder ?? "");
                return c != 0 ? c : Vi.Compare(a.Name ?? "", b.Name ?? "", CompareOptions.IgnoreCase);
            }
        }

        private void BuildTable(CatalogData data)
        {
            var dt = new DataTable();
            for (int i = 0; i < _def.Columns.Count; i++)
                dt.Columns.Add("c" + i, _def.Columns[i].ClrType);
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (CatalogRow r in data.Rows)
            {
                var values = new object[dt.Columns.Count];
                sb.Length = 0;
                for (int i = 0; i < _def.Columns.Count; i++)
                {
                    object v = r.Values[i];
                    values[i] = v ?? DBNull.Value;
                    if (v == null)
                        continue;
                    if (v is string)
                        sb.Append((string)v);
                    else if (v is DateTime)
                        sb.Append(((DateTime)v).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                    else if (v is bool)
                        continue;
                    else
                        sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                    sb.Append(' ');
                }
                values[_def.Columns.Count] = r.Id;
                values[_def.Columns.Count + 1] = CatalogRepository.Fold(sb.ToString());
                values[_def.Columns.Count + 2] = CatalogRepository.CategoryPath(_nodes, r.CategoryId);
                dt.Rows.Add(values);
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        // ------------------------------------------------------------------ Tìm và lọc

        /// <summary>Đặt ô tìm (dùng cho kiểm thử và --render-screen).</summary>
        public void SetSearch(string text)
        {
            _search.Text = text ?? "";
        }

        /// <summary>Chọn nhóm theo tên (so khớp không phân biệt dấu); dùng cho kiểm thử và --render-screen.</summary>
        public bool SelectCategory(string name)
        {
            string target = CatalogRepository.Fold(name);
            TreeNode f = FindByName(_tree.Nodes, target);
            if (f == null)
                return false;
            _tree.SelectedNode = f;
            return true;
        }

        private static TreeNode FindByName(TreeNodeCollection nodes, string foldedName)
        {
            foreach (TreeNode n in nodes)
            {
                if (CatalogRepository.Fold(n.Text) == foldedName)
                    return n;
                TreeNode f = FindByName(n.Nodes, foldedName);
                if (f != null)
                    return f;
            }
            return null;
        }

        private void ApplyFilter()
        {
            if (_loading)
                return;
            if (_view != null)
            {
                var parts = new List<string>();
                string term = CatalogRepository.Fold(_search.Text.Trim());
                if (term.Length > 0)
                {
                    string escaped = CatalogRepository.EscapeLike(term);
                    if (_customerMode && IsCustomerNoteVisible())
                        parts.Add("([" + ColSearch + "] LIKE '%" + escaped + "%' OR [" + ColSearchNote + "] LIKE '%" + escaped + "%')");
                    else
                        parts.Add("[" + ColSearch + "] LIKE '%" + escaped + "%'");
                }
                string catId = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as string;
                if (!string.IsNullOrEmpty(catId))
                {
                    // "Chưa thiết lập": mặt hàng không có nhóm (đường đi nhóm rỗng).
                    if (catId == UnsetTag)
                        parts.Add("[" + ColCategory + "] = ''");
                    else
                        parts.Add("[" + ColCategory + "] LIKE '%|" + CatalogRepository.EscapeLike(catId) + "|%'");
                }
                if (_giftVoucherMode && _chkVoucherUsableOnly.Checked)
                    parts.Add("[" + ColUsable + "] = true");
                AppendPromotionFilterParts(parts);
                _view.RowFilter = string.Join(" AND ", parts.ToArray());
                _grid.ClearSelection();
                if (_promotionMode)
                    ApplyPromotionRowColors();
            }
            UpdateCount();
            UpdateGiftVoucherTotalBar();
        }

        private void UpdateCount()
        {
            int shown = VisibleRows;
            if (_error != null)
            {
                _count.Text = "";
                _empty.Text = _error;
            }
            else
            {
                _count.Text = shown == _total ? _total + " dòng" : shown + " / " + _total + " dòng";
                _empty.Text = _total == 0 ? "Chưa có dữ liệu" : "Không tìm thấy dòng nào";
            }
            _empty.Visible = _error != null || shown == 0;
            _search.Enabled = _error == null;
            PositionEmpty();
        }

        private void PositionEmpty()
        {
            float s = DeviceDpi / 96f;
            int h = (int)(60 * s);
            // Căn giữa vùng dưới tiêu đề (trước đây dính sát tiêu đề nên lưới rỗng nhìn lệch lên trên).
            int free = _grid.ClientSize.Height - _grid.ColumnHeadersHeight - h;
            int top = _grid.ColumnHeadersHeight + Math.Max((int)(24 * s), free / 2);
            _empty.SetBounds(0, top, _grid.ClientSize.Width, h);
        }

        // ------------------------------------------------------------------ Bố cục và giao diện

        private void LayoutBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(10 * s);
            int h = _bar.ClientSize.Height - 2 * pad;
            if (!_productMode && !_roomMode && !_customerMode && !IsFundCatalogMode && !_warehouseMode && !_storeMode && !_supplierMode && !_giftVoucherMode && !_promotionMode && !_employeeMode && !_shiftMode)
            {
                int btnW = (int)(100 * s);
                _search.SetBounds(pad, pad + (h - _search.Height) / 2, (int)(340 * s), _search.Height);
                _refresh.SetBounds(_search.Right + pad, pad, btnW, h);
                _count.SetBounds(_refresh.Right + pad, pad, Math.Max(0, _bar.ClientSize.Width - _refresh.Right - 2 * pad), h);
                return;
            }
            _search.SetBounds(pad, pad + (h - _search.Height) / 2, (int)(260 * s), _search.Height);
            int x = _search.Right + pad;
            FlatButton[] buttons = _roomMode
                // Q16: Thêm excel / Xuất excel / In / Tổng hiện ở chế độ bàn nhưng chưa implement.
                ? new[] { _btnAdd, _btnEdit, _btnRemove, _refresh, _btnQuickAdd, _btnExcelAdd, _btnExcelExport, _btnPrint, _btnTotal }
                : (_customerMode
                    ? new[] { _btnAdd, _btnEdit, _btnRemove, _refresh, _btnExcelAdd, _btnExcelExport, _btnPrint }
                    : ((IsFundCatalogMode || _warehouseMode || _storeMode || _supplierMode || _giftVoucherMode || _promotionMode || _employeeMode || _shiftMode)
                        ? new[] { _btnAdd, _btnEdit, _btnRemove, _refresh, _btnExcelExport }
                        : new[] { _btnAdd, _btnEdit, _btnRemove, _refresh, _btnExcelAdd, _btnExcelExport, _btnExcelImport, _btnPrint, _btnPrintBarcode }));
            using (Graphics g = CreateGraphics())
            {
                foreach (FlatButton b in buttons)
                {
                    if ((_customerMode || IsFundCatalogMode || _warehouseMode || _storeMode || _supplierMode || _giftVoucherMode || _promotionMode || _employeeMode || _shiftMode) && !b.Visible)
                        continue;
                    int w = TextRenderer.MeasureText(g, b.Text, b.Font).Width + (int)(28 * s);
                    b.SetBounds(x, pad, w, h);
                    x = b.Right + pad;
                }
            }
            if (_giftVoucherMode && _chkVoucherUsableOnly.Visible)
            {
                Size chkSz = _chkVoucherUsableOnly.GetPreferredSize(Size.Empty);
                int chkH = Math.Min(h, chkSz.Height);
                _chkVoucherUsableOnly.SetBounds(x, pad + (h - chkH) / 2, chkSz.Width, chkH);
                x = _chkVoucherUsableOnly.Right + pad;
            }
            _count.SetBounds(x, pad, Math.Max(0, _bar.ClientSize.Width - x - pad), h);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutBar();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F3 || keyData == (Keys.Control | Keys.F))
            {
                _search.Inner.Focus();
                _search.Inner.SelectAll();
                return true;
            }
            // Chỉ khi focus đang ở lưới/cây (không phải ô tìm): Insert thêm, F4 sửa, Del xóa.
            if ((_productMode || _roomMode || _customerMode || IsFundCatalogMode || _warehouseMode || _storeMode || _supplierMode || _giftVoucherMode || _promotionMode || _employeeMode || _shiftMode) && !_search.Inner.Focused && (_grid.Focused || _tree.Focused))
            {
                if (keyData == Keys.Insert)
                {
                    if (_productMode) AddProduct(); else if (_roomMode) AddRoom(); else if (_customerMode) AddCustomer(); else if (_warehouseMode) AddWarehouse(); else if (_storeMode) AddStore(); else if (_supplierMode) AddSupplier(); else if (_giftVoucherMode) AddGiftVoucher(); else if (_promotionMode) AddPromotion(); else if (_employeeMode) AddEmployee(); else if (_shiftMode) AddShift(); else AddFundCatalogItem();
                    return true;
                }
                if (keyData == Keys.F4)
                {
                    if (_productMode) EditProduct(); else if (_roomMode) EditRoom(); else if (_customerMode) OpenCustomerFromGrid(); else if (_warehouseMode) OpenWarehouseFromGrid(); else if (_storeMode) OpenStoreFromGrid(); else if (_supplierMode) OpenSupplierFromGrid(); else if (_giftVoucherMode) OpenGiftVoucherFromGrid(); else if (_promotionMode) OpenPromotionFromGrid(); else if (_employeeMode) OpenEmployeeFromGrid(); else if (_shiftMode) OpenShiftFromGrid(); else OpenFundCatalogFromGrid();
                    return true;
                }
                if (keyData == Keys.Delete)
                {
                    if (_productMode) DeleteProduct(); else if (_roomMode) DeleteRoom(); else if (_customerMode) DeleteCustomer(); else if (_warehouseMode) DeleteWarehouse(); else if (_storeMode) DeleteStore(); else if (_supplierMode) DeleteSupplier(); else if (_giftVoucherMode) DeleteGiftVoucher(); else if (_promotionMode) DeletePromotion(); else if (_employeeMode) DeleteEmployee(); else if (_shiftMode) DeleteShift(); else DeleteFundCatalogItem();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void ApplyTheme(ThemeTokens t, Color page)
        {
            _theme = t;
            _page = page;
            ThemeApplier.Apply(this, t);
            BackColor = page;
            _bar.BackColor = page;
            _count.BackColor = Color.Transparent;
            _count.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = _error != null ? t.Danger : t.TextMuted;
            ThemeApplier.ApplyInput(_search, t, page, Draw.StrongBorder(t));
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = page;
            _split.Panel2.BackColor = page;
            _refresh.SurfaceColor = page;
            _refresh.FillColor = t.Surface;
            _refresh.TextColor = t.Text;
            _refresh.BorderColor = Draw.StrongBorder(t);
            _refresh.Invalidate();
            if (_productMode || _roomMode || _customerMode || IsFundCatalogMode || _warehouseMode || _storeMode || _supplierMode || _giftVoucherMode || _promotionMode || _employeeMode || _shiftMode)
            {
                _groupBar.BackColor = page;
                _bottom.BackColor = page;
                _infoTabs.BackColor = page;
                _infoTabs.ForeColor = t.Text;
                foreach (TabPage p in _infoTabs.TabPages)
                    p.BackColor = page;
                _areaGrid.BackgroundColor = t.Surface;
                ThemeApplier.ApplyGrid(_areaGrid, t);
                _areaGrid.BorderStyle = BorderStyle.None;
                if (_areaEmpty != null)
                    PaneGrid.ApplyEmptyTheme(_areaGrid, _areaEmpty, t);
                if (_roomDetail != null)
                    _roomDetail.ApplyTheme(t);
                if (_productMode)
                {
                    UpdateProductButtons();
                    UpdateAuditPanel();
                }
                else if (_roomMode)
                {
                    UpdateRoomButtons();
                    UpdateRoomAuditPanel();
                }
                else if (_customerMode)
                {
                    UpdateCustomerButtons();
                    UpdateCustomerAuditPanel();
                }
                else if (_warehouseMode)
                {
                    UpdateWarehouseButtons();
                    UpdateWarehouseAuditPanel();
                }
                else if (_storeMode)
                {
                    UpdateStoreButtons();
                    UpdateStoreAuditPanel();
                }
                else if (_supplierMode)
                {
                    UpdateSupplierButtons();
                    UpdateSupplierAuditPanel();
                }
                else if (_giftVoucherMode)
                {
                    ApplyGiftVoucherTheme(t, page);
                    UpdateGiftVoucherButtons();
                    UpdateGiftVoucherAuditPanel();
                }
                else if (_promotionMode)
                {
                    ApplyPromotionTheme(t, page);
                    UpdatePromotionButtons();
                    UpdatePromotionAuditPanel();
                }
                else if (_employeeMode)
                {
                    UpdateEmployeeButtons();
                    UpdateEmployeeAuditPanel();
                }
                else if (_shiftMode)
                {
                    UpdateShiftButtons();
                    UpdateShiftAuditPanel();
                }
                else
                {
                    UpdateFundCatalogButtons();
                    UpdateFundCatalogAuditPanel();
                }
            }
            ApplyBarcodeTheme(t, page);
            Invalidate(true);
        }

        // ------------------------------------------------------------------ Danh mục mặt hàng

        private void BuildProductUi()
        {
            float s = DeviceDpi / 96f;
            _btnAdd.Tag = "primary";
            _btnRemove.Tag = "danger";
            _removeFont = new Font("Segoe UI Semibold", 10f);
            _btnRemove.Font = _removeFont;
            _btnAdd.Click += delegate { if (_roomMode) AddRoom(); else if (_customerMode) AddCustomer(); else if (IsFundCatalogMode) AddFundCatalogItem(); else if (_warehouseMode) AddWarehouse(); else if (_storeMode) AddStore(); else if (_supplierMode) AddSupplier(); else if (_giftVoucherMode) AddGiftVoucher(); else if (_promotionMode) AddPromotion(); else if (_employeeMode) AddEmployee(); else if (_shiftMode) AddShift(); else AddProduct(); };
            _btnEdit.Click += delegate { if (_roomMode) EditRoom(); else if (_customerMode) EditCustomer(); else if (IsFundCatalogMode) EditFundCatalogItem(); else if (_warehouseMode) EditWarehouse(); else if (_storeMode) EditStore(); else if (_supplierMode) EditSupplier(); else if (_giftVoucherMode) EditGiftVoucher(); else if (_promotionMode) EditPromotion(); else if (_employeeMode) EditEmployee(); else if (_shiftMode) EditShift(); else EditProduct(); };
            _btnRemove.Click += delegate { if (_roomMode) DeleteRoom(); else if (_customerMode) DeleteCustomer(); else if (IsFundCatalogMode) DeleteFundCatalogItem(); else if (_warehouseMode) DeleteWarehouse(); else if (_storeMode) DeleteStore(); else if (_supplierMode) DeleteSupplier(); else if (_giftVoucherMode) DeleteGiftVoucher(); else if (_promotionMode) DeletePromotion(); else if (_employeeMode) DeleteEmployee(); else if (_shiftMode) DeleteShift(); else DeleteProduct(); };
            _btnExcelAdd.Click += delegate { Msg.Info(this, UnsupportedText); };
            _btnExcelExport.Click += delegate { ExportGridToExcel(); };
            _btnExcelImport.Click += delegate { Msg.Info(this, UnsupportedText); };
            _btnPrint.Click += delegate { Msg.Info(this, UnsupportedText); };
            _btnPrintBarcode.Click += delegate { OpenBarcodePanel(); };
            FlatButton[] barButtons = { _btnAdd, _btnEdit, _btnRemove, _btnExcelAdd, _btnExcelExport, _btnExcelImport, _btnPrint, _btnPrintBarcode };
            foreach (FlatButton b in barButtons)
            {
                b.Visible = false;
                _bar.Controls.Add(b);
            }

            BuildBarcodeUi();

            _groupBar.Height = (int)(36 * s);
            _groupAdd.SetBounds((int)(6 * s), (int)(4 * s), (int)(28 * s), (int)(28 * s));
            _groupEdit.SetBounds((int)(40 * s), (int)(4 * s), (int)(28 * s), (int)(28 * s));
            _groupAdd.Click += delegate { if (_roomMode) AddArea(null); else if (_customerMode) AddCustomerGroup(null); else if (_supplierMode) AddSupplierGroup(null); else if (_giftVoucherMode) AddGiftVoucherGroup(null); else AddGroup(null); };
            _groupEdit.Click += delegate { if (_roomMode) EditArea(); else if (_customerMode) EditCustomerGroup(); else if (_supplierMode) EditSupplierGroup(); else if (_giftVoucherMode) EditGiftVoucherGroup(); else EditGroup(); };
            _groupBar.Controls.Add(_groupAdd);
            _groupBar.Controls.Add(_groupEdit);
            _split.Panel1.Controls.Add(_groupBar);

            _bottom.Height = (int)(220 * s);
            BuildInfoTab();
            BuildAreaTab();
            _bottom.Controls.Add(_infoTabs);
            // Thanh kéo nằm giữa lưới và pane dưới (thêm trước pane để dock ngay trên nó).
            _split.Panel2.Controls.Add(_paneSplitter);
            _split.Panel2.Controls.Add(_bottom);

            BuildGridMenu();
            BuildTreeMenu();
        }

        private void BuildInfoTab()
        {
            var page = new TabPage("Thông tin") { Font = new Font("Segoe UI", 10f) }; // cùng cỡ chữ lưới/cây
            AddAuditLabel(page, "Khởi tạo:", _lblCreated, 10, 12, 150);
            AddAuditLabel(page, "Khởi tạo bởi:", _lblCreatedBy, 330, 12, 115);
            AddAuditLabel(page, "Sửa đổi gần nhất:", _lblModified, 10, 42, 150);
            AddAuditLabel(page, "Sửa đổi bởi:", _lblModifiedBy, 330, 42, 115);
            _infoTabs.TabPages.Add(page);
        }

        private void AddAuditLabel(TabPage page, string caption, Label value, int x, int y, int capW)
        {
            float s = DeviceDpi / 96f;
            var cap = new Label { Text = caption, AutoSize = true };
            cap.Location = new Point((int)(x * s), (int)(y * s));
            value.Location = new Point((int)((x + capW) * s), (int)(y * s));
            page.Controls.Add(cap);
            page.Controls.Add(value);
        }

        /// <summary>
        /// Tab Khu vực (Q2): lưới chỉ-xem đúng 13 cột theo ảnh app gốc + cột chọn dòng; rỗng
        /// (điều kiện liên quan giữa mặt hàng và khu vực chưa xác minh nên chưa nạp dữ liệu).
        /// </summary>
        private void BuildAreaTab()
        {
            var page = new TabPage("Khu vực");
            _areaGrid.Dock = DockStyle.Fill;
            _areaGrid.ReadOnly = true;
            _areaGrid.AllowUserToAddRows = false;
            _areaGrid.AllowUserToDeleteRows = false;
            _areaGrid.RowHeadersVisible = false;
            _areaGrid.AutoGenerateColumns = false;
            _areaGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _areaGrid.MultiSelect = false;
            _areaGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "a_sel", HeaderText = "", Width = 32 });
            string[] cols = { "Ghi chú", "Tên khu vực", "Mẫu hóa đơn", "Kho hàng", "Tab hiển thị", "Giá mặt hàng", "Biểu tượng", "Tiền mở bàn", "Giá theo giờ", "Từ giờ", "Đến giờ", "Đơn giá", "Bảng giá" };
            for (int i = 0; i < cols.Length; i++)
                _areaGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "a_" + i, HeaderText = cols[i], Width = 110, ReadOnly = true });
            PaneGrid.Configure(_areaGrid);
            PaneGrid.FitHeaderWidths(_areaGrid); // tiêu đề như "Giá mặt hàng" không bị cắt
            _areaGrid.Columns[_areaGrid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _areaEmpty = PaneGrid.AttachEmpty(_areaGrid, "Chưa có dữ liệu");
            _areaEmpty.Visible = true;
            page.Controls.Add(_areaGrid);
            DetailGridMenu.Attach(_areaGrid, this, delegate { Reload(); }); // menu chuột phải của lưới pane (hình 1)
            _areaTabPage = page;
            _infoTabs.TabPages.Add(page);
        }

        private void LoadProductData(CatalogData data)
        {
            _productRepo = _productRepo ?? new ProductRepository(_databasePath);
            _groupRepo = _groupRepo ?? new ProductGroupRepository(_databasePath);
            _canAdd = AppSession.Can(ProductText.FunctionName, RoleModes.Add);
            _canEdit = AppSession.Can(ProductText.FunctionName, RoleModes.Edit);
            _canRemove = AppSession.Can(ProductText.FunctionName, RoleModes.Remove);
            _canViewBuyPrice = AppSession.Can("Xem giá nhập", RoleModes.View);
            _productRows = _productRepo.LoadProducts();
            _total = _productRows.Count;
            // BuildTree trước: BuildProductTable cần _nodes để tính đường đi nhóm.
            BuildTree(data);
            BuildProductColumns();
            BuildProductTable();
            // Tab "Khu vực" chỉ thuộc chế độ mặt hàng: thêm lại khi quay về chế độ này (chế độ bàn đã gỡ).
            if (_roomDetail != null)
                _roomDetail.Detach();
            if (_areaTabPage != null && !_infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Add(_areaTabPage);
            UpdateProductButtons();
            UpdateAuditPanel();
        }

        /// <summary>Bộ cột lưới thật §1.1 (KHÔNG dùng SGRID). Cột điều kiện chỉ hiện khi cấu hình bật / có quyền.</summary>
        private void BuildProductColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.ColumnHeadersHeight = (int)(48 * s);

            AddProductColumn("p_name", "Tên mặt hàng", 141, typeof(string), null);
            if (SystemConfig.SuDungMaSanCo)
                AddProductColumn("p_barcode", "Mã sẵn có", 100, typeof(string), null);
            if (SystemConfig.SuDungTenTiengAnh)
                AddProductColumn("p_english", "Tên tiếng anh", 140, typeof(string), null);
            AddProductColumn("p_group", "Nhóm mặt hàng", 213, typeof(string), null);
            AddProductColumn("p_type", "Loại mặt hàng", 130, typeof(string), null);
            AddProductColumn("p_unit", "Đơn vị tính", 78, typeof(string), null);
            AddProductColumn("p_sell", "Giá bán", 66, typeof(decimal), "#,##0.##");
            if (SystemConfig.SuDungGia2)
                AddProductColumn("p_sell2", SystemConfig.DienGiaiGia2, 80, typeof(decimal), "#,##0.##");
            if (SystemConfig.SuDungGia3)
                AddProductColumn("p_sell3", SystemConfig.DienGiaiGia3, 80, typeof(decimal), "#,##0.##");
            if (SystemConfig.SuDungGia4)
                AddProductColumn("p_sell4", SystemConfig.DienGiaiGia4, 80, typeof(decimal), "#,##0.##");
            if (_canViewBuyPrice)
                AddProductColumn("p_buy", "Giá nhập", 107, typeof(decimal), "#,##0.##");
            AddProductColumn("p_bigunit", "Đơn vị tính chẵn", 80, typeof(string), null);
            AddProductColumn("p_quydoi", "Quy đổi", 80, typeof(decimal), "#,##0.##");
            AddProductColumn("p_bigsell", "Giá bán chẵn", 80, typeof(decimal), "#,##0.##");
            AddProductColumn("p_locked", "Tạm khóa", 80, typeof(bool), null);
            AddProductColumn("p_timeprice", "Giá theo thời giá", 80, typeof(bool), null);
            // Q12: cột Mã hàng nằm cuối cùng của lưới khi SuDungMaHang bật.
            if (SystemConfig.SuDungMaHang)
                AddProductColumn("p_code", "Mã hàng", 100, typeof(string), null);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void AddProductColumn(string name, string header, int width96, Type type, string format)
        {
            float s = DeviceDpi / 96f;
            DataGridViewColumn gc;
            if (type == typeof(bool))
                gc = new DataGridViewCheckBoxColumn();
            else
                gc = new DataGridViewTextBoxColumn();
            gc.Name = name;
            gc.DataPropertyName = name;
            gc.HeaderText = header;
            gc.SortMode = DataGridViewColumnSortMode.Automatic;
            gc.ReadOnly = true;
            int headerNeed = TextRenderer.MeasureText(header, _grid.ColumnHeadersDefaultCellStyle.Font).Width + (int)(30 * s);
            gc.Width = Math.Max(Math.Max((int)(60 * s), (int)(width96 * s)), headerNeed);
            gc.MinimumWidth = (int)(50 * s);
            if (type == typeof(decimal))
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                gc.DefaultCellStyle.Format = format;
                gc.DefaultCellStyle.FormatProvider = new CultureInfo("vi-VN");
            }
            else if (type == typeof(bool))
            {
                gc.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            _grid.Columns.Add(gc);
        }

        private void BuildProductTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("p_name", typeof(string));
            dt.Columns.Add("p_code", typeof(string));
            dt.Columns.Add("p_barcode", typeof(string));
            dt.Columns.Add("p_english", typeof(string));
            dt.Columns.Add("p_group", typeof(string));
            dt.Columns.Add("p_type", typeof(string));
            dt.Columns.Add("p_unit", typeof(string));
            dt.Columns.Add("p_sell", typeof(decimal));
            dt.Columns.Add("p_sell2", typeof(decimal));
            dt.Columns.Add("p_sell3", typeof(decimal));
            dt.Columns.Add("p_sell4", typeof(decimal));
            dt.Columns.Add("p_buy", typeof(decimal));
            dt.Columns.Add("p_bigunit", typeof(string));
            dt.Columns.Add("p_quydoi", typeof(decimal));
            dt.Columns.Add("p_bigsell", typeof(decimal));
            dt.Columns.Add("p_locked", typeof(bool));
            dt.Columns.Add("p_timeprice", typeof(bool));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (ProductRow r in _productRows)
            {
                var values = new object[dt.Columns.Count];
                values[0] = (object)r.Name ?? DBNull.Value;
                values[1] = (object)r.Code ?? DBNull.Value;
                values[2] = (object)r.Barcode ?? DBNull.Value;
                values[3] = (object)r.EnglishName ?? DBNull.Value;
                values[4] = (object)r.GroupName ?? DBNull.Value;
                values[5] = (object)r.TypeName ?? DBNull.Value;
                values[6] = (object)r.UnitName ?? DBNull.Value;
                values[7] = r.SellPrice;
                values[8] = r.SellPrice2;
                values[9] = r.SellPrice3;
                values[10] = r.SellPrice4;
                values[11] = r.BuyPrice;
                values[12] = (object)r.BigUnitName ?? DBNull.Value;
                values[13] = r.QuyDoi ?? 0m; // §1.1: hiện 0 khi chưa có ĐVT chẵn.
                values[14] = r.BigSellPrice.HasValue ? (object)r.BigSellPrice.Value : DBNull.Value;
                values[15] = r.TempLocked;
                values[16] = r.PriceByTime;
                values[17] = r.Id;
                sb.Length = 0;
                sb.Append(r.Name).Append(' ');
                sb.Append(r.Code).Append(' ');
                sb.Append(r.GroupName).Append(' ');
                sb.Append(r.TypeName).Append(' ');
                sb.Append(r.UnitName).Append(' ');
                sb.Append(r.Barcode);
                values[18] = CatalogRepository.Fold(sb.ToString());
                values[19] = CatalogRepository.CategoryPath(_nodes, r.GroupId);
                dt.Rows.Add(values);
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        // ------------------------------------------------------------------ Danh mục bàn khu vực (DBAN)

        private void BuildRoomUi()
        {
            _btnQuickAdd.Tag = "primary";
            _btnQuickAdd.Click += delegate { QuickAddRooms(); };
            _btnTotal.Click += delegate { Msg.Info(this, UnsupportedText); };
            FlatButton[] barButtons = { _btnQuickAdd, _btnTotal };
            foreach (FlatButton b in barButtons)
            {
                b.Visible = false;
                _bar.Controls.Add(b);
            }
            BuildRoomGridMenu();
            BuildRoomTreeMenu();
        }

        private void LoadRoomData(CatalogData data)
        {
            _roomRepo = _roomRepo ?? new RoomRepository(_databasePath);
            _areaRepo = _areaRepo ?? new AreaRepository(_databasePath);
            _canAdd = AppSession.Can(RoomRules.FunctionName, RoleModes.Add);
            _canEdit = AppSession.Can(RoomRules.FunctionName, RoleModes.Edit);
            _canRemove = AppSession.Can(RoomRules.FunctionName, RoleModes.Remove);
            _roomRows = _roomRepo.LoadRooms();
            _total = _roomRows.Count;
            // BuildTree trước: BuildRoomTable cần _nodes để tính đường đi khu vực.
            BuildTree(data);
            BuildRoomColumns();
            BuildRoomTable();
            // Tab "Khu vực" là của chế độ mặt hàng, không có ở chế độ bàn (Q1: lưới bàn chỉ có 5 cột + panel Thông tin).
            if (_areaTabPage != null && _infoTabs.TabPages.Contains(_areaTabPage))
                _infoTabs.TabPages.Remove(_areaTabPage);
            // Các tab liên quan của bàn (như app gốc): Bảng giá theo bàn, Đặt hàng, Đơn hàng (5 form)…
            if (_roomDetail == null)
                _roomDetail = new DetailTabsHost(_infoTabs, _databasePath, "DBAN", this);
            _roomDetail.Attach();
            _roomDetail.ApplyTheme(ThemeManager.Current);
            _roomDetail.Refresh();
            UpdateRoomButtons();
            UpdateRoomAuditPanel();
        }

        /// <summary>Bộ cột lưới thật §1.1a + Q1 (KHÔNG dùng SGRID): Tên bàn, Khu vực, Nhóm hiển thị, Loại phòng, Ghi chú;
        /// các cột giá theo giờ chỉ hiện khi CoTienGio bật (DB hiện = 0 nên ẩn), IP công tắc chỉ khi admin bật đủ 2 cấu hình.</summary>
        private void BuildRoomColumns()
        {
            float s = DeviceDpi / 96f;
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = true;
            _grid.RowHeadersWidth = (int)(40 * s);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.ColumnHeadersHeight = (int)(48 * s);

            AddProductColumn("r_name", "Tên bàn", 150, typeof(string), null);
            AddProductColumn("r_area", "Khu vực", 150, typeof(string), null);
            AddProductColumn("r_group", "Nhóm hiển thị", 150, typeof(string), null);
            AddProductColumn("r_type", "Loại phòng", 130, typeof(string), null);
            if (SystemConfig.CoTienGio)
            {
                AddProductColumn("r_calc", "Cách tính giờ", 110, typeof(string), null);
                AddProductColumn("r_price", "Đơn giá", 90, typeof(decimal), "#,##0.##");
                AddProductColumn("r_pricelist", "Bảng giá", 130, typeof(string), null);
                AddProductColumn("r_open", "Tiền mở bàn", 100, typeof(decimal), "#,##0.##");
            }
            AddProductColumn("r_note", "Ghi chú", 200, typeof(string), null);
            if (SystemConfig.CoTienGio && SystemConfig.CoSuDungCongTacDienTu
                && AppSession.Current != null && AppSession.Current.IsAdmin)
                AddProductColumn("r_ip", "IP công tắc điện", 130, typeof(string), null);
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void BuildRoomTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("r_name", typeof(string));
            dt.Columns.Add("r_area", typeof(string));
            dt.Columns.Add("r_group", typeof(string));
            dt.Columns.Add("r_type", typeof(string));
            dt.Columns.Add("r_calc", typeof(string));
            dt.Columns.Add("r_price", typeof(decimal));
            dt.Columns.Add("r_pricelist", typeof(string));
            dt.Columns.Add("r_open", typeof(decimal));
            dt.Columns.Add("r_note", typeof(string));
            dt.Columns.Add("r_ip", typeof(string));
            dt.Columns.Add(ColId, typeof(string));
            dt.Columns.Add(ColSearch, typeof(string));
            dt.Columns.Add(ColCategory, typeof(string));

            var sb = new StringBuilder();
            dt.BeginLoadData();
            foreach (RoomRow r in _roomRows)
            {
                var values = new object[dt.Columns.Count];
                values[0] = (object)r.Name ?? DBNull.Value;
                values[1] = (object)r.AreaName ?? DBNull.Value;
                values[2] = (object)r.DisplayGroupName ?? DBNull.Value;
                values[3] = (object)r.RoomTypeName ?? DBNull.Value;
                values[4] = RoomRules.CalcModeText(r.CalcHour);
                values[5] = r.UnitPrice;
                values[6] = (object)r.PriceListName ?? DBNull.Value;
                values[7] = r.OpenAmount;
                values[8] = (object)r.Note ?? DBNull.Value;
                values[9] = (object)r.SwitchIp ?? DBNull.Value;
                values[10] = r.Id;
                sb.Length = 0;
                sb.Append(r.Name).Append(' ');
                sb.Append(r.AreaName).Append(' ');
                sb.Append(r.DisplayGroupName).Append(' ');
                sb.Append(r.RoomTypeName).Append(' ');
                sb.Append(r.Note);
                values[11] = CatalogRepository.Fold(sb.ToString());
                values[12] = CatalogRepository.CategoryPath(_nodes, r.AreaId);
                dt.Rows.Add(values);
            }
            dt.EndLoadData();

            _table = dt;
            _view = new DataView(dt);
            _grid.DataSource = _view;
            _grid.ClearSelection();
        }

        private string CurrentRoomId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null)
                return null;
            return drv[ColId] as string;
        }

        private RoomRow CurrentRoomRow()
        {
            string id = CurrentRoomId();
            if (id == null)
                return null;
            foreach (RoomRow r in _roomRows)
            {
                if (r.Id == id)
                    return r;
            }
            return null;
        }

        private List<string> RoomRowIds()
        {
            var ids = new List<string>();
            foreach (DataRowView drv in _view)
                ids.Add(drv[ColId] as string);
            return ids;
        }

        private List<string> AreaRowIds()
        {
            var ids = new List<string>();
            if (_tree.Nodes.Count > 0)
            {
                foreach (TreeNode n in _tree.Nodes[0].Nodes)
                {
                    string id = n.Tag as string;
                    if (!string.IsNullOrEmpty(id) && id != UnsetTag)
                        ids.Add(id);
                }
            }
            return ids;
        }

        private void RefreshRoomAndSelect(string id)
        {
            Reload();
            if (id != null)
                SelectRoomRow(id);
        }

        private void SelectRoomRow(string id)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var drv = row.DataBoundItem as DataRowView;
                if (drv != null && (drv[ColId] as string) == id)
                {
                    _grid.CurrentCell = row.Cells[0];
                    return;
                }
            }
        }

        private void AddRoom()
        {
            if (!_roomMode || !_canAdd)
                return;
            string areaId = SelectedGroupId();
            if (string.IsNullOrEmpty(areaId) || areaId == UnsetTag)
            {
                // Chưa chọn khu vực (đang ở "Tất cả"/"Chưa thiết lập") → hỏi khu vực trước (§2.1).
                using (var picker = new AreaPickerDialog(AppSession.Current))
                {
                    if (picker.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(picker.SelectedAreaId))
                        return;
                    areaId = picker.SelectedAreaId;
                }
            }
            using (var dlg = new RoomEditDialog(AppSession.Current, null, areaId, RoomRowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    RefreshRoomAndSelect(dlg.SavedId);
            }
        }

        private void EditRoom()
        {
            if (!_roomMode || !_canEdit)
                return;
            RoomRow row = CurrentRoomRow();
            if (row == null)
                return;
            using (var dlg = new RoomEditDialog(AppSession.Current, row.Id, row.AreaId, RoomRowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    RefreshRoomAndSelect(dlg.SavedId);
            }
        }

        private void DeleteRoom()
        {
            if (!_roomMode || !_canRemove)
                return;
            RoomRow row = CurrentRoomRow();
            if (row == null)
                return;
            if (!Msg.Confirm(this, "Bạn có chắc chắn xóa bàn '" + row.Name + "' không?"))
                return;
            RoomDeleteResult result;
            try
            {
                result = _roomRepo.DeleteRoom(row.Id);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!result.Ok)
            {
                Msg.Warning(this, result.ErrorMessage);
                return;
            }
            Reload();
        }

        private void QuickAddRooms()
        {
            if (!_roomMode || !_canAdd)
                return;
            using (var dlg = new QuickAddRoomDialog(AppSession.Current))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.CreatedCount > 0)
                    Reload();
            }
        }

        // ---- Khu vực (DKHUVUC): + và ✎ trên thanh panel nhóm, đổi tên trực tiếp trên cây ----

        private void AddArea(string parentId)
        {
            if (!_roomMode || !_canAdd)
                return;
            using (var dlg = new AreaEditDialog(AppSession.Current, null, AreaRowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    RefreshRoomAndSelect(null);
            }
        }

        private void EditArea()
        {
            if (!_roomMode || !_canEdit)
                return;
            string areaId = SelectedGroupId();
            if (string.IsNullOrEmpty(areaId) || areaId == UnsetTag)
                return;
            AreaRow row = _areaRepo.Load(areaId);
            if (row == null)
            {
                Msg.Warning(this, "Khu vực này không còn tồn tại trong dữ liệu.");
                Reload();
                return;
            }
            using (var dlg = new AreaEditDialog(AppSession.Current, row.Id, AreaRowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    RefreshRoomAndSelect(null);
            }
        }

        private void DeleteArea()
        {
            if (!_roomMode || !_canRemove)
                return;
            string areaId = SelectedGroupId();
            if (string.IsNullOrEmpty(areaId) || areaId == UnsetTag)
                return;
            AreaRow row = _areaRepo.Load(areaId);
            if (row == null)
            {
                Msg.Warning(this, "Khu vực này không còn tồn tại trong dữ liệu.");
                Reload();
                return;
            }
            if (!Msg.Confirm(this, "Bạn có chắc chắn xóa khu vực '" + row.Name + "' không?"))
                return;
            string error;
            try
            {
                error = _areaRepo.Delete(areaId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (error != null)
            {
                Msg.Warning(this, error);
                return;
            }
            Reload();
        }

        private void RenameSelectedArea()
        {
            if (!_roomMode || !_canEdit)
                return;
            if (_tree.SelectedNode != null && _tree.SelectedNode.Tag as string != null
                && (string)_tree.SelectedNode.Tag != UnsetTag)
                _tree.SelectedNode.BeginEdit();
        }

        private void RenameAreaLabel(NodeLabelEditEventArgs e)
        {
            if (e.Label == null || _tree.SelectedNode == null)
            {
                e.CancelEdit = true;
                return;
            }
            string id = _tree.SelectedNode.Tag as string;
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
            {
                e.CancelEdit = true;
                return;
            }
            string name = e.Label.Trim();
            if (name.Length == 0)
            {
                Msg.Warning(this, RoomText.AreaNameEmpty);
                e.CancelEdit = true;
                return;
            }
            // Đổi tên = nạp khu vực rồi Save lại (giữ nguyên bảng giá/truy cập/mặt hàng mặc định gắn kèm).
            AreaRow area = _areaRepo.Load(id);
            if (area == null)
            {
                Msg.Warning(this, RoomText.AreaGone);
                e.CancelEdit = true;
                return;
            }
            area.Name = name;
            AreaSaveResult saved;
            try
            {
                saved = _areaRepo.Save(area, _areaRepo.LoadPriceAssignments(id), _areaRepo.LoadAccessUserIds(id),
                    _areaRepo.LoadDefaultItems(id), AppSession.UserId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không lưu được dữ liệu: " + ex.Message);
                e.CancelEdit = true;
                return;
            }
            if (!saved.Ok)
            {
                Msg.Warning(this, saved.ErrorMessage);
                e.CancelEdit = true;
                return;
            }
            Reload();
        }

        private void UpdateRoomButtons()
        {
            if (!_roomMode)
                return;
            bool hasRow = CurrentRoomId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
            _btnQuickAdd.Enabled = _canAdd;
            string areaId = SelectedGroupId();
            bool realArea = !string.IsNullOrEmpty(areaId) && areaId != UnsetTag;
            _groupAdd.Enabled = _canAdd;
            _groupEdit.Enabled = _canEdit && realArea;
            _tree.LabelEdit = _canEdit;
        }

        private void UpdateRoomAuditPanel()
        {
            if (_roomDetail != null)
                _roomDetail.SetMaster(CurrentRoomId());
            RoomRow row = CurrentRoomRow();
            if (row == null)
            {
                _lblCreated.Text = "";
                _lblCreatedBy.Text = "";
                _lblModified.Text = "";
                _lblModifiedBy.Text = "";
                return;
            }
            _lblCreated.Text = FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row.CreatedByName ?? "";
            _lblModified.Text = FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row.ModifiedByName ?? "";
        }

        private void SortRooms(string column, bool asc)
        {
            if (_view == null)
                return;
            _view.Sort = "[" + column + "] " + (asc ? "ASC" : "DESC");
        }

        // ---- Menu chuột phải (bàn/khu vực) ----

        private void BuildRoomGridMenu()
        {
            _roomGridMenu.AddItem("Thêm bàn", Keys.Insert, delegate { AddRoom(); });
            _roomGridMenu.AddItem("Thêm nhanh", delegate { QuickAddRooms(); });
            _roomGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditRoom(); });
            _roomGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteRoom(); });
            _roomGridMenu.AddSeparator();
            var sort = new ToolStripMenuItem("Sắp xếp theo") { Font = _roomGridMenu.Font };
            AddRoomSortItem(sort, "Tên bàn (A → Z)", "r_name", true);
            AddRoomSortItem(sort, "Tên bàn (Z → A)", "r_name", false);
            _roomGridMenu.Items.Add(sort);
            _roomGridMenu.AddItem("Refresh", delegate { Reload(); });
            _roomGridMenu.AddItem("In danh sách", delegate { Msg.Info(this, UnsupportedText); });
            _roomGridMenu.AddSeparator();
            _roomGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _roomGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _roomGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _roomGridMenu.Opening += delegate { UpdateRoomGridMenuState(); };
        }

        private void AddRoomSortItem(ToolStripMenuItem parent, string text, string key, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _roomGridMenu.Font };
            item.Click += delegate { SortRooms(key, asc); };
            parent.DropDownItems.Add(item);
        }

        private void UpdateRoomGridMenuState()
        {
            bool hasRow = CurrentRoomId() != null;
            foreach (ToolStripItem item in _roomGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm bàn": mi.Enabled = _canAdd; break;
                    case "Thêm nhanh": mi.Enabled = _canAdd; break;
                    case "Chỉnh sửa": mi.Enabled = _canEdit && hasRow; break;
                    case "Xóa": mi.Enabled = _canRemove && hasRow; break;
                }
            }
        }

        private void BuildRoomTreeMenu()
        {
            _roomTreeMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddArea(null); });
            _roomTreeMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditArea(); });
            _roomTreeMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteArea(); });
            _roomTreeMenu.AddItem("Đổi tên", Keys.F2, delegate { RenameSelectedArea(); });
            _roomTreeMenu.AddItem("Refresh", delegate { Reload(); });
            _roomTreeMenu.AddItem("Mở rộng", delegate { ExpandSelectedGroup(true); });
            _roomTreeMenu.AddItem("Thu gọn", delegate { ExpandSelectedGroup(false); });
            _roomTreeMenu.Opening += delegate { UpdateRoomTreeMenuState(); };
        }

        private void UpdateRoomTreeMenuState()
        {
            string areaId = SelectedGroupId();
            bool realArea = !string.IsNullOrEmpty(areaId) && areaId != UnsetTag;
            foreach (ToolStripItem item in _roomTreeMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm mới": mi.Enabled = _canAdd; break;
                    case "Chỉnh sửa": mi.Enabled = _canEdit && realArea; break;
                    case "Xóa": mi.Enabled = _canRemove && realArea; break;
                    case "Đổi tên": mi.Enabled = _canEdit && realArea; break;
                }
            }
        }

        /// <summary>Vẽ số thứ tự (STT) lên row header, theo thứ tự đang hiện trên lưới.</summary>
        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if ((!_productMode && !_roomMode && !_customerMode && !IsFundCatalogMode && !_warehouseMode && !_storeMode && !_supplierMode && !_giftVoucherMode && !_promotionMode && !_employeeMode && !_shiftMode) || !_grid.RowHeadersVisible)
                return;
            string text = (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(0, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, _grid.RowHeadersDefaultCellStyle.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        // ------------------------------------------------------------------ Dòng đang chọn / CRUD

        private string CurrentProductId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var drv = _grid.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null)
                return null;
            return Convert.ToString(drv[ColId]);
        }

        private ProductRow CurrentProductRow()
        {
            string id = CurrentProductId();
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (ProductRow r in _productRows)
            {
                if (string.Equals(r.Id, id, StringComparison.Ordinal))
                    return r;
            }
            return null;
        }

        private List<string> ProductRowIds()
        {
            var ids = new List<string>();
            if (_view != null)
            {
                foreach (DataRowView drv in _view)
                    ids.Add(Convert.ToString(drv[ColId]));
            }
            return ids;
        }

        private string SelectedGroupId()
        {
            if (_tree.SelectedNode == null)
                return null;
            return _tree.SelectedNode.Tag as string;
        }

        private void SelectTreeNode(string id)
        {
            if (_tree.Nodes.Count == 0)
                return;
            TreeNode f = FindNode(_tree.Nodes[0], id);
            if (f != null)
            {
                f.EnsureVisible();
                _tree.SelectedNode = f;
            }
        }

        private void SelectProductRow(string id)
        {
            if (_view == null || string.IsNullOrEmpty(id))
                return;
            for (int i = 0; i < _view.Count; i++)
            {
                if (string.Equals(Convert.ToString(_view[i][ColId]), id, StringComparison.Ordinal))
                {
                    _grid.ClearSelection();
                    _grid.CurrentCell = _grid.Rows[i].Cells[0];
                    _grid.Rows[i].Selected = true;
                    return;
                }
            }
        }

        private void RefreshProductAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
                SelectProductRow(id);
        }

        private void AddProduct()
        {
            if (!_productMode || !_canAdd)
                return;
            string groupId = SelectedGroupId();
            if (string.IsNullOrEmpty(groupId) || groupId == UnsetTag)
            {
                using (var picker = new ProductGroupPickerDialog(AppSession.Current))
                {
                    if (picker.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(picker.SelectedGroupId))
                        return;
                    groupId = picker.SelectedGroupId;
                }
            }
            using (var dlg = new ProductEditDialog(AppSession.Current, null, groupId, ProductRowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    RefreshProductAndSelect(dlg.SavedId);
            }
        }

        private void EditProduct()
        {
            if (!_productMode || !_canEdit)
                return;
            string id = CurrentProductId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new ProductEditDialog(AppSession.Current, id, null, ProductRowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    RefreshProductAndSelect(dlg.SavedId);
            }
        }

        private void DeleteProduct()
        {
            if (!_productMode || !_canRemove)
                return;
            ProductRow row = CurrentProductRow();
            if (row == null)
                return;
            if (!Msg.Confirm(this, ProductText.DeleteConfirm))
                return;
            ProductDeleteResult result;
            try
            {
                result = _productRepo.DeleteProduct(row.Id);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!result.Ok)
            {
                Msg.Warning(this, result.ErrorMessage);
                return;
            }
            RefreshProductAndSelect(null);
        }

        private void UpdateProductButtons()
        {
            if (!_productMode)
                return;
            bool hasRow = CurrentProductId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            _groupAdd.Enabled = _canAdd;
            _groupEdit.Enabled = _canEdit && realGroup;
            _tree.LabelEdit = _canEdit;
        }

        private void UpdateAuditPanel()
        {
            if (!_productMode)
                return;
            ProductRow row = CurrentProductRow();
            if (row == null)
            {
                _lblCreated.Text = "";
                _lblCreatedBy.Text = "";
                _lblModified.Text = "";
                _lblModifiedBy.Text = "";
                return;
            }
            _lblCreated.Text = FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row.CreatedByName ?? "";
            _lblModified.Text = FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row.ModifiedByName ?? "";
        }

        private static string FormatAuditTime(DateTime? t)
        {
            return t.HasValue ? t.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "";
        }

        // ------------------------------------------------------------------ Nhóm mặt hàng

        private void AddGroup(string parentId)
        {
            if (!_productMode || !_canAdd)
                return;
            if (parentId == UnsetTag)
                parentId = null;
            using (var dlg = new ProductGroupEditDialog(AppSession.Current, null, parentId))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(dlg.SavedId))
                    return;
                Reload();
                SelectTreeNode(dlg.SavedId);
            }
        }

        private void EditGroup()
        {
            if (!_productMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            ProductGroupRow group = _groupRepo.Load(id);
            if (group == null)
                return;
            using (var dlg = new ProductGroupEditDialog(AppSession.Current, group, null))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                Reload();
                SelectTreeNode(id);
            }
        }

        private void DeleteGroup()
        {
            if (!_productMode || !_canRemove)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            ProductGroupRow group = _groupRepo.Load(id);
            if (group == null)
                return;
            if (!Msg.Confirm(this, ProductText.DeleteConfirm))
                return;
            string error;
            try
            {
                error = _groupRepo.Delete(id);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (error != null)
            {
                Msg.Warning(this, error);
                return;
            }
            Reload();
        }

        private void RenameSelectedGroup()
        {
            if (!_productMode || !_canEdit)
                return;
            string id = SelectedGroupId();
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
                return;
            if (_tree.SelectedNode != null)
                _tree.SelectedNode.BeginEdit();
        }

        private void TreeAfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (_roomMode)
            {
                RenameAreaLabel(e);
                return;
            }
            if (_customerMode)
            {
                RenameCustomerGroupLabel(e);
                return;
            }
            if (_supplierMode)
            {
                RenameSupplierGroupLabel(e);
                return;
            }
            if (_giftVoucherMode)
            {
                RenameGiftVoucherGroupLabel(e);
                return;
            }
            if (!_productMode || e.Label == null)
                return;
            string id = e.Node.Tag as string;
            if (string.IsNullOrEmpty(id) || id == UnsetTag)
            {
                e.CancelEdit = true;
                return;
            }
            string name = e.Label.Trim();
            if (name.Length == 0)
            {
                e.CancelEdit = true;
                return;
            }
            ProductGroupRow group = _groupRepo.Load(id);
            if (group == null)
            {
                e.CancelEdit = true;
                return;
            }
            group.Name = name;
            string error;
            try
            {
                _groupRepo.Save(group, AppSession.UserId, out error);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                e.CancelEdit = true;
                return;
            }
            if (error != null)
            {
                Msg.Warning(this, error);
                e.CancelEdit = true;
                return;
            }
            Reload();
            SelectTreeNode(id);
        }

        private void ExpandSelectedGroup(bool expand)
        {
            TreeNode node = _tree.SelectedNode;
            if (node == null)
                return;
            if (expand)
                node.ExpandAll();
            else
                node.Collapse();
        }

        // ------------------------------------------------------------------ Menu chuột phải

        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            _gridMenu.AddItem("Thêm Mặt hàng", Keys.Insert, delegate { AddProduct(); });
            _gridMenu.AddItem("Thêm nhanh (excel)", delegate { Msg.Info(this, UnsupportedText); });
            _gridMenu.AddItem("Cập nhật nhanh (excel)", delegate { Msg.Info(this, UnsupportedText); });
            _gridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditProduct(); });
            _gridMenu.AddItem("Đặt giá trị", delegate { Msg.Info(this, UnsupportedText); });
            _gridMenu.AddItem("Lọc theo giá trị", delegate { FilterByValue(); });
            var sort = new ToolStripMenuItem("Sắp xếp theo") { Font = _gridMenu.Font };
            AddSortItem(sort, "Tên mặt hàng (A → Z)", "p_name", true);
            AddSortItem(sort, "Tên mặt hàng (Z → A)", "p_name", false);
            AddSortItem(sort, "Giá bán (tăng dần)", "p_sell", true);
            AddSortItem(sort, "Giá bán (giảm dần)", "p_sell", false);
            _gridMenu.Items.Add(sort);
            _gridMenu.AddItem("Refresh", delegate { Reload(); });
            _gridMenu.AddItem("In danh sách", delegate { Msg.Info(this, UnsupportedText); });
            _gridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _gridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _gridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteProduct(); });
            _gridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _colSubmenu = new ToolStripMenuItem("Cột hiển thị") { Font = _gridMenu.Font };
            _gridMenu.Items.Add(_colSubmenu);
            _gridMenu.AddItem("Thuộc tính", delegate { Msg.Info(this, UnsupportedText); });
            _gridMenu.Opening += delegate { UpdateGridMenuState(); };
        }

        private void AddSortItem(ToolStripMenuItem parent, string text, string key, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _gridMenu.Font };
            item.Click += delegate { SortProducts(key, asc); };
            parent.DropDownItems.Add(item);
        }

        private static bool IsToggleableColumn(string name)
        {
            switch (name)
            {
                case "p_code":
                case "p_barcode":
                case "p_english":
                case "p_sell2":
                case "p_sell3":
                case "p_sell4":
                case "p_buy":
                    return true;
                default:
                    return false;
            }
        }

        private void UpdateGridMenuState()
        {
            if (_gridMenu == null)
                return;
            bool hasRow = CurrentProductId() != null;
            foreach (ToolStripItem item in _gridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm Mặt hàng": mi.Enabled = _canAdd; break;
                    case "Chỉnh sửa": mi.Enabled = _canEdit && hasRow; break;
                    case "Xóa": mi.Enabled = _canRemove && hasRow; break;
                    case "Đặt giá trị":
                    case "Lọc theo giá trị":
                    case "Sao chép ô":
                    case "Sao chép vùng chọn":
                    case "Thuộc tính": mi.Enabled = hasRow; break;
                }
            }
            if (_colSubmenu != null)
            {
                _colSubmenu.DropDownItems.Clear();
                foreach (DataGridViewColumn col in _grid.Columns)
                {
                    if (!IsToggleableColumn(col.Name))
                        continue;
                    string key = col.Name;
                    var item = new ToolStripMenuItem(col.HeaderText)
                    {
                        Font = _gridMenu.Font,
                        CheckOnClick = true,
                        Checked = col.Visible
                    };
                    item.Click += delegate
                    {
                        DataGridViewColumn c = _grid.Columns[key];
                        if (c != null)
                            c.Visible = !c.Visible;
                    };
                    _colSubmenu.DropDownItems.Add(item);
                }
                _colSubmenu.Enabled = _colSubmenu.DropDownItems.Count > 0;
            }
        }

        private void BuildTreeMenu()
        {
            _treeMenu = new ThemedContextMenu();
            _treeMenu.AddItem("Thêm mới", delegate { AddGroup(null); });
            _treeMenu.AddItem("Thêm con", delegate { AddGroup(SelectedGroupId()); });
            _treeMenu.AddItem("Chỉnh sửa", delegate { EditGroup(); });
            _treeMenu.AddItem("Sắp xếp theo", delegate { Msg.Info(this, UnsupportedText); });
            _treeMenu.AddItem("Refresh", delegate { Reload(); });
            _treeMenu.AddItem("Mở rộng", delegate { ExpandSelectedGroup(true); });
            _treeMenu.AddItem("Thu gọn", delegate { ExpandSelectedGroup(false); });
            _treeMenu.AddItem("Xóa", delegate { DeleteGroup(); });
            _treeMenu.AddItem("Đổi tên", delegate { RenameSelectedGroup(); });
            _treeMenu.AddItem("Biểu tượng", delegate { Msg.Info(this, UnsupportedText); });
            _treeMenu.AddItem("Thuộc tính", delegate { Msg.Info(this, UnsupportedText); });
            _treeMenu.Opening += delegate { UpdateTreeMenuState(); };
        }

        private void UpdateTreeMenuState()
        {
            if (_treeMenu == null)
                return;
            string groupId = SelectedGroupId();
            bool realGroup = !string.IsNullOrEmpty(groupId) && groupId != UnsetTag;
            bool hasKids = _tree.SelectedNode != null && _tree.SelectedNode.Nodes.Count > 0;
            foreach (ToolStripItem item in _treeMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm mới": mi.Enabled = _canAdd; break;
                    case "Thêm con": mi.Enabled = _canAdd && realGroup; break;
                    case "Chỉnh sửa": mi.Enabled = _canEdit && realGroup; break;
                    case "Xóa": mi.Enabled = _canRemove && realGroup; break;
                    case "Đổi tên": mi.Enabled = _canEdit && realGroup; break;
                    case "Mở rộng":
                    case "Thu gọn": mi.Enabled = hasKids; break;
                }
            }
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if ((!_productMode && !_roomMode && !_customerMode && !IsFundCatalogMode && !_warehouseMode && !_storeMode && !_supplierMode && !_giftVoucherMode && !_promotionMode && !_employeeMode && !_shiftMode) || e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                _grid.ClearSelection();
                _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[0];
                _grid.Rows[hit.RowIndex].Selected = true;
            }
            else
            {
                _grid.ClearSelection();
            }
            if (_roomMode)
                _roomGridMenu.Show(_grid, e.Location);
            else if (_customerMode)
                _customerGridMenu.Show(_grid, e.Location);
            else if (IsFundCatalogMode)
                _fundGridMenu.Show(_grid, e.Location);
            else if (_warehouseMode)
                _warehouseGridMenu.Show(_grid, e.Location);
            else if (_storeMode)
                _storeGridMenu.Show(_grid, e.Location);
            else if (_supplierMode)
                _supplierGridMenu.Show(_grid, e.Location);
            else if (_giftVoucherMode)
                _giftVoucherGridMenu.Show(_grid, e.Location);
            else if (_promotionMode)
                _promotionGridMenu.Show(_grid, e.Location);
            else if (_employeeMode)
                _employeeGridMenu.Show(_grid, e.Location);
            else if (_shiftMode)
                _shiftGridMenu.Show(_grid, e.Location);
            else
                _gridMenu.Show(_grid, e.Location);
        }

        private void TreeMouseDown(object sender, MouseEventArgs e)
        {
            if ((!_productMode && !_roomMode && !_customerMode && !_supplierMode && !_giftVoucherMode) || e.Button != MouseButtons.Right)
                return;
            TreeNode node = _tree.GetNodeAt(e.X, e.Y);
            if (node != null)
                _tree.SelectedNode = node;
            if (_roomMode)
                _roomTreeMenu.Show(_tree, e.Location);
            else if (_customerMode)
                _customerTreeMenu.Show(_tree, e.Location);
            else if (_supplierMode)
                _supplierTreeMenu.Show(_tree, e.Location);
            else if (_giftVoucherMode)
                _giftVoucherTreeMenu.Show(_tree, e.Location);
            else
                _treeMenu.Show(_tree, e.Location);
        }

        // ------------------------------------------------------------------ Hành động menu lưới

        private void CopyCell()
        {
            if (_grid.CurrentCell == null)
                return;
            object v = _grid.CurrentCell.Value;
            Clipboard.SetText(v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private void CopySelection()
        {
            if (_grid.SelectedCells.Count == 0)
                return;
            var cells = new List<DataGridViewCell>();
            foreach (DataGridViewCell c in _grid.SelectedCells)
                if (c.OwningColumn != null && c.OwningColumn.Visible)
                    cells.Add(c);
            if (cells.Count == 0)
                return;
            cells.Sort(delegate(DataGridViewCell a, DataGridViewCell b)
            {
                int r = a.RowIndex.CompareTo(b.RowIndex);
                return r != 0 ? r : a.ColumnIndex.CompareTo(b.ColumnIndex);
            });
            var sb = new StringBuilder();
            int lastRow = -1;
            foreach (DataGridViewCell c in cells)
            {
                if (lastRow >= 0)
                    sb.Append(c.RowIndex == lastRow ? "\t" : "\r\n");
                object v = c.Value;
                sb.Append(v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture));
                lastRow = c.RowIndex;
            }
            Clipboard.SetText(sb.ToString());
        }

        private void AutoFitColumns()
        {
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                if (col.Visible)
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            if (_grid.Columns.Count > 0)
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private void FilterByValue()
        {
            if (_grid.CurrentCell == null)
                return;
            object v = _grid.CurrentCell.Value;
            if (v == null)
                return;
            _search.Text = Convert.ToString(v, CultureInfo.InvariantCulture);
            _search.Inner.Focus();
            _search.Inner.SelectAll();
        }

        private void SortProducts(string key, bool asc)
        {
            if (_view == null)
                return;
            _view.Sort = "[" + key + "] " + (asc ? "ASC" : "DESC");
            _grid.ClearSelection();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _search.Inner.Font.Dispose();
                _count.Font.Dispose();
                _empty.Font.Dispose();
                _tree.Font.Dispose();
                _grid.Font.Dispose();
                _grid.ColumnHeadersDefaultCellStyle.Font.Dispose();
                if (_removeFont != null)
                    _removeFont.Dispose();
                if (_gridMenu != null)
                    _gridMenu.Dispose();
                if (_treeMenu != null)
                    _treeMenu.Dispose();
                _roomGridMenu.Dispose();
                _roomTreeMenu.Dispose();
                _customerGridMenu.Dispose();
                _customerTreeMenu.Dispose();
                _fundGridMenu.Dispose();
                _warehouseGridMenu.Dispose();
                _storeGridMenu.Dispose();
                _supplierGridMenu.Dispose();
                _supplierTreeMenu.Dispose();
                DisposeGiftVoucherUi();
                DisposePromotionUi();
                DisposeBarcodeUi();
            }
            base.Dispose(disposing);
        }
    }
}
