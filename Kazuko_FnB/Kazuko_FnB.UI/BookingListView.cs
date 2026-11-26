using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Khách đặt hàng" (docs/specs/khach-dat-hang.md §1.1): panel lọc + cây Phương thức đặt
    /// bên trái; thanh nút + lưới chứng từ bên phải. Mỗi dòng TDATHANG một hàng (§6.2 Q6).
    /// Mở dialog sửa bằng BookingEditDialog (dùng chung với "Theo dõi đặt phòng" sau này).
    /// </summary>
    internal sealed partial class BookingListView : Panel
    {
        private readonly string _databasePath;
        private readonly BookingRepository _repo;
        private readonly FormRepository _formRepo;

        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly InputBox _txtNumber = new InputBox(); // ô bo góc như ô tìm của các màn danh mục
        private readonly List<FieldFrame> _frames = new List<FieldFrame>(); // khung bo góc bọc ô ngày/khách hàng
        // dd/MM/yyyy cố định như app gốc (không theo định dạng ngày của máy, vd. M/d/yyyy).
        private readonly DateBox _dtFrom = new DateBox();
        private readonly DateBox _dtTo = new DateBox();
        private readonly LookupCombo _customer = new LookupCombo();
        private readonly FlatButton _btnFilter = new FlatButton { Text = "Lọc (F3)" };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill };
        private readonly Panel _bar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _btnAdd = new FlatButton { Text = "Thêm mới (Insert)" };
        private readonly FlatButton _btnEdit = new FlatButton { Text = "Chỉnh sửa (F4)" };
        private readonly FlatButton _btnRemove = new FlatButton { Text = "Xóa (Del)", Tag = "danger" };
        private readonly FlatButton _btnExcel = new FlatButton { Text = "Xuất excel" };
        private readonly FlatButton _btnPrint = new FlatButton { Text = "In" };
        private readonly FlatButton _btnTotal = new FlatButton { Text = "Tổng" };
        private readonly FlatButton _btnAnalysis = new FlatButton { Text = "P.tích" };
        private readonly FlatButton _refresh = new FlatButton { Text = "Làm mới" };
        private readonly Label _count = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _empty = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

        // Pane dưới lưới (app gốc: Thông tin + các tab liên quan dựng từ metadata — xem DetailRepository).
        private readonly Panel _bottom = new Panel { Dock = DockStyle.Bottom };
        private readonly TabControl _infoTabs = new ThemedTabControl { Dock = DockStyle.Fill };
        private readonly Label _lblCreated = new Label { AutoSize = true };
        private readonly Label _lblCreatedBy = new Label { AutoSize = true };
        private readonly Label _lblModified = new Label { AutoSize = true };
        private readonly Label _lblModifiedBy = new Label { AutoSize = true };
        private readonly ImageList _treeIcons = new ImageList { ColorDepth = ColorDepth.Depth32Bit };
        // Thanh nút trên cây thư mục "Phương thức đặt" (icon vector Segoe MDL2 Assets, viền mảnh như màn mặt hàng/bàn).
        private readonly Panel _treeBar = new Panel { Dock = DockStyle.Top };
        private readonly FlatButton _folderAdd = new FlatButton { Text = "", Font = new Font("Segoe MDL2 Assets", 10f), BorderWidth = 1.2f };
        private readonly FlatButton _folderEdit = new FlatButton { Text = "", Font = new Font("Segoe MDL2 Assets", 10f), BorderWidth = 1.2f };
        private BookingMethodRepository _methodRepo;
        private DetailTabsHost _detail;

        private List<BookingListRow> _rows = new List<BookingListRow>();
        private bool _canAdd;
        private bool _canEdit;
        private bool _canRemove;
        private bool _loading;

        public BookingListView(string databasePath)
        {
            _databasePath = databasePath;
            _repo = new BookingRepository(databasePath);
            _formRepo = new FormRepository(databasePath);
            _methodRepo = new BookingMethodRepository(databasePath);
            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            // Chặn Reload từ AfterSelect của cây trong lúc dựng (lưới chưa có cột).
            _loading = true;

            _canAdd = AppSession.Can(BookingText.FunctionName, RoleModes.Add);
            _canEdit = AppSession.Can(BookingText.FunctionName, RoleModes.Edit);
            _canRemove = AppSession.Can(BookingText.FunctionName, RoleModes.Remove);

            BuildFilterPanel(s);
            BuildBar(s);
            BuildGrid(s);
            BuildBottom(s);

            BuildFolderUi(s);
            _split.Panel1.Controls.Add(_treeBar);
            _split.Panel1.Controls.Add(_tree);
            // Dock theo thứ tự z từ SAU ra TRƯỚC: thanh nút rồi ô Fill phải ở trước cùng (BringToFront) để không bị
            // panel lọc/tiêu đề đè (trước đây cây bị che nên chỉ thấy nút cuối).
            _treeBar.BringToFront();
            _tree.BringToFront();
            _tree.Font = new Font("Segoe UI", 10f); // cùng font/chiều cao dòng với cây của CatalogView
            _tree.ItemHeight = (int)(26 * s);
            _split.Panel2.Controls.Add(_grid);
            _split.Panel2.Controls.Add(_bar);
            _split.Panel2.Controls.Add(new PaneSplitter()); // thanh kéo đổi chiều cao pane dưới (dock ngay trên pane)
            _split.Panel2.Controls.Add(_bottom);
            _grid.BringToFront();
            _split.FixedPanel = FixedPanel.Panel1;
            _split.SizeChanged += delegate { PlaceSplit(); };
            Controls.Add(_split);
            _loading = false;

            _grid.RowPostPaint += DrawRowNumber;
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    EditBooking();
            };
            _grid.SelectionChanged += delegate { if (!_loading) { UpdateButtons(); UpdateInfo(); } };
            BuildGridMenu();
            _grid.MouseDown += GridMouseDown;
            _grid.KeyDown += OnKeyDown;
            _tree.KeyDown += OnKeyDown;
            KeyDown += OnKeyDown;

            ThemeManager.ThemeChanged += OnThemeChanged;
            Reload();
        }

        // ------------------------------------------------------------------ Dựng giao diện

        private bool _splitPlaced;

        /// <summary>Đặt độ rộng cột trái = 300 (như CatalogView) khi vùng đã đủ rộng: SplitterDistance chỉ hợp lệ sau khi có kích thước.</summary>
        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            int want = (int)(300 * DeviceDpi / 96f);
            if (_split.Width > want + (int)(200 * DeviceDpi / 96f))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        private void BuildFilterPanel(float s)
        {
            // Lưới 2 cột (nhãn cố định | ô nhập giãn hết chiều ngang) nên mọi ô cùng lề, cùng bề rộng, cùng font,
            // ô ngày không bị cắt chữ (trước đây đặt từng ô theo tọa độ tay).
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding((int)(8 * s), (int)(6 * s), (int)(8 * s), (int)(6 * s)),
                Height = (int)(4 * 36 * s) + (int)(12 * s)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112 * s));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 4; i++)
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));
            Font fieldFont = new Font("Segoe UI", 10f);
            _txtNumber.Inner.KeyDown += OnKeyDown;
            _dtFrom.Value = DateTime.Today.AddDays(-30);
            _dtFrom.Inner.KeyDown += OnKeyDown;
            _dtTo.Value = DateTime.Today;
            _dtTo.Inner.KeyDown += OnKeyDown;
            _customer.KeyDown += OnKeyDown;
            AddFilterRow(panel, 0, "Số phiếu", _txtNumber, fieldFont, s);
            AddFilterRow(panel, 1, "Từ ngày", _dtFrom, fieldFont, s);
            AddFilterRow(panel, 2, "Đến ngày", _dtTo, fieldFont, s);
            AddFilterRow(panel, 3, "Khách hàng", _customer, fieldFont, s);

            // K4: nút "Lọc (F3)" nằm ở thanh nút bên phải (BuildBar), không ở panel lọc.
            _btnFilter.Click += delegate { Reload(); };

            _split.Panel1.Controls.Add(panel);

            var treeTitle = new Label
            {
                Text = "Phương thức đặt",
                Dock = DockStyle.Top,
                Height = (int)(26 * s),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 10f)
            };
            _split.Panel1.Controls.Add(treeTitle);
            _tree.AfterSelect += delegate { if (!_loading) Reload(); };
            LoadTreeNodes();
        }

        private void AddFilterRow(TableLayoutPanel panel, int row, string caption, Control field, Font font, float s)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = font
            };
            var input = field as InputBox;
            var date = field as DateBox;
            if (input != null)
                input.Inner.Font = font;
            else if (date != null)
                date.Inner.Font = font;
            else
                field.Font = font;
            // Cả bốn ô lọc cùng kiểu bo góc: InputBox, DateBox (tự có khung), combo được bọc khung FieldFrame.
            Control host = field;
            if (date != null)
            {
                _frames.Add(date);
            }
            else if (field is ComboBox)
            {
                ((ComboBox)field).FlatStyle = FlatStyle.Flat;
                var frame = new FieldFrame(field, 0);
                _frames.Add(frame);
                host = frame;
            }
            host.Height = (int)(32 * s);
            host.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            host.Margin = new Padding(0, (int)(2 * s), 0, (int)(2 * s));
            panel.Controls.Add(lbl, 0, row);
            panel.Controls.Add(host, 1, row);
        }

        // Icon cây như app gốc: Tất cả (quả cầu xanh), Chưa thiết lập (sao vàng), phương thức (thoi tím).
        private const int IconAll = 0;
        private const int IconUnset = 1;
        private const int IconMethod = 2;

        private void EnsureTreeIcons()
        {
            if (_treeIcons.Images.Count > 0)
                return;
            float s = DeviceDpi / 96f;
            int size = Math.Max(16, (int)(16 * s));
            _treeIcons.ImageSize = new Size(size, size);
            for (int kind = 0; kind < 3; kind++)
            {
                var bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    var box = new Rectangle(1, 1, size - 3, size - 3);
                    if (kind == IconAll)
                    {
                        using (var fill = new SolidBrush(Color.FromArgb(60, 150, 220)))
                        using (var line = new Pen(Color.FromArgb(30, 100, 170)))
                        {
                            g.FillEllipse(fill, box);
                            g.DrawEllipse(line, box);
                            g.DrawArc(line, box.X + box.Width / 4, box.Y, box.Width / 2, box.Height, 90, 180);
                            g.DrawLine(line, box.X, box.Y + box.Height / 2, box.Right, box.Y + box.Height / 2);
                        }
                    }
                    else if (kind == IconUnset)
                    {
                        using (var fill = new SolidBrush(Color.FromArgb(240, 190, 40)))
                        {
                            var pts = new PointF[10];
                            float cx = size / 2f, cy = size / 2f, ro = size / 2f - 1, ri = ro * 0.45f;
                            for (int i = 0; i < 10; i++)
                            {
                                double a = -Math.PI / 2 + i * Math.PI / 5;
                                float r = i % 2 == 0 ? ro : ri;
                                pts[i] = new PointF(cx + (float)(r * Math.Cos(a)), cy + (float)(r * Math.Sin(a)));
                            }
                            g.FillPolygon(fill, pts);
                        }
                    }
                    else
                    {
                        using (var fill = new SolidBrush(Color.FromArgb(170, 80, 200)))
                        using (var line = new Pen(Color.FromArgb(110, 40, 140)))
                        {
                            var pts = new[]
                            {
                                new Point(size / 2, 1), new Point(size - 2, size / 2),
                                new Point(size / 2, size - 2), new Point(1, size / 2)
                            };
                            g.FillPolygon(fill, pts);
                            g.DrawPolygon(line, pts);
                        }
                    }
                }
                _treeIcons.Images.Add(bmp);
            }
        }

        private void LoadTreeNodes()
        {
            EnsureTreeIcons();
            _tree.ImageList = _treeIcons;
            _tree.Nodes.Clear();
            _tree.Nodes.Add(new TreeNode("Tất cả") { Tag = null, ImageIndex = IconAll, SelectedImageIndex = IconAll });
            _tree.Nodes.Add(new TreeNode("Chưa thiết lập") { Tag = "", ImageIndex = IconUnset, SelectedImageIndex = IconUnset });
            AddMethodFolders();
            try
            {
                _customer.SetItems(_formRepo.LoadAll("DKHACHHANG"));
            }
            catch (DataAccessException)
            {
                // Ô khách hàng để trống; lưới vẫn nạp được.
            }
            _tree.SelectedNode = _tree.Nodes[0];
        }

        // ------------------------------------------------------------------ Thư mục "Phương thức đặt" (thêm / sửa / xóa)

        /// <summary>Nạp các thư mục (DPHUONGTHUCDAT) thành cây có cấp con, đặt sau nút "Tất cả" và "Chưa thiết lập".</summary>
        private void AddMethodFolders()
        {
            try
            {
                List<BookingMethodRow> rows = _methodRepo.LoadAll();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (BookingMethodRow r in rows)
                    ids.Add(r.Id);
                var byParent = new Dictionary<string, List<BookingMethodRow>>(StringComparer.Ordinal);
                foreach (BookingMethodRow r in rows)
                {
                    // Cha rỗng hoặc không còn tồn tại → coi như thư mục gốc (không để mất nút).
                    string key = string.IsNullOrEmpty(r.ParentId) || !ids.Contains(r.ParentId) ? "" : r.ParentId;
                    List<BookingMethodRow> list;
                    if (!byParent.TryGetValue(key, out list))
                        byParent[key] = list = new List<BookingMethodRow>();
                    list.Add(r);
                }
                AddFolderNodes(_tree.Nodes, "", byParent, new HashSet<string>(StringComparer.Ordinal), 0);
            }
            catch (DataAccessException)
            {
                // Cây vẫn có Tất cả / Chưa thiết lập; lưới báo lỗi khi nạp.
            }
        }

        private void AddFolderNodes(TreeNodeCollection into, string parentKey, Dictionary<string, List<BookingMethodRow>> byParent,
            HashSet<string> seen, int depth)
        {
            List<BookingMethodRow> kids;
            if (depth > 32 || !byParent.TryGetValue(parentKey, out kids))
                return;
            foreach (BookingMethodRow m in kids)
            {
                if (!seen.Add(m.Id))
                    continue; // chống vòng cha-con trong dữ liệu hỏng
                var node = new TreeNode(m.Name) { Tag = m.Id, ImageIndex = IconMethod, SelectedImageIndex = IconMethod };
                into.Add(node);
                AddFolderNodes(node.Nodes, m.Id, byParent, seen, depth + 1);
            }
        }

        /// <summary>Dựng lại cây sau khi thêm/sửa/xóa; chọn lại nút <paramref name="selectId"/> (không có thì "Tất cả").</summary>
        private void ReloadTree(string selectId)
        {
            bool wasLoading = _loading;
            _loading = true;
            try
            {
                _tree.BeginUpdate();
                _tree.Nodes.Clear();
                _tree.Nodes.Add(new TreeNode("Tất cả") { Tag = null, ImageIndex = IconAll, SelectedImageIndex = IconAll });
                _tree.Nodes.Add(new TreeNode("Chưa thiết lập") { Tag = "", ImageIndex = IconUnset, SelectedImageIndex = IconUnset });
                AddMethodFolders();
                TreeNode pick = string.IsNullOrEmpty(selectId) ? null : FindFolder(_tree.Nodes, selectId);
                if (pick != null)
                {
                    for (TreeNode p = pick.Parent; p != null; p = p.Parent)
                        p.Expand();
                }
                _tree.SelectedNode = pick ?? _tree.Nodes[0];
                _tree.EndUpdate();
            }
            finally
            {
                _loading = wasLoading;
            }
            if (!wasLoading)
                Reload();
            UpdateTreeButtons();
        }

        private static TreeNode FindFolder(TreeNodeCollection nodes, string id)
        {
            foreach (TreeNode n in nodes)
            {
                if (string.Equals(n.Tag as string, id, StringComparison.Ordinal))
                    return n;
                TreeNode inner = FindFolder(n.Nodes, id);
                if (inner != null)
                    return inner;
            }
            return null;
        }

        /// <summary>Id thư mục đang chọn; null khi đang chọn "Tất cả"/"Chưa thiết lập" (không sửa/xóa được).</summary>
        private string SelectedFolderId()
        {
            string id = _tree.SelectedNode == null ? null : _tree.SelectedNode.Tag as string;
            return string.IsNullOrEmpty(id) ? null : id;
        }

        private static void CollectFolderIds(TreeNode node, List<string> ids)
        {
            string id = node.Tag as string;
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
            foreach (TreeNode child in node.Nodes)
                CollectFolderIds(child, ids);
        }

        private void AddFolder(bool underSelected)
        {
            if (!_canAdd)
                return;
            BookingMethodRow parent = null;
            if (underSelected)
            {
                string pid = SelectedFolderId();
                if (pid == null)
                    return;
                parent = _methodRepo.Load(pid);
                if (parent == null)
                {
                    Msg.Warning(this, BookingMethodRepository.Gone);
                    ReloadTree(null);
                    return;
                }
            }
            using (var dlg = new BookingMethodEditDialog(AppSession.Current, null, parent))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ReloadTree(dlg.SavedId);
            }
        }

        private void EditFolder()
        {
            string id = SelectedFolderId();
            if (!_canEdit || id == null)
                return;
            BookingMethodRow row = _methodRepo.Load(id);
            if (row == null)
            {
                Msg.Warning(this, BookingMethodRepository.Gone);
                ReloadTree(null);
                return;
            }
            using (var dlg = new BookingMethodEditDialog(AppSession.Current, row, null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ReloadTree(dlg.SavedId);
            }
        }

        private void DeleteFolder()
        {
            string id = SelectedFolderId();
            if (!_canRemove || id == null)
                return;
            string name = _tree.SelectedNode.Text;
            if (!Msg.Confirm(this, "Bạn có muốn xóa thư mục '" + name + "' vĩnh viễn khỏi hệ thống không?"))
                return;
            string error;
            try
            {
                error = _methodRepo.Delete(id);
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
            ReloadTree(null);
        }

        private void UpdateTreeButtons()
        {
            bool real = SelectedFolderId() != null;
            _folderAdd.Enabled = _canAdd;
            _folderEdit.Enabled = _canEdit && real;
        }

        private ThemedContextMenu _treeMenu;

        private void BuildFolderUi(float s)
        {
            // Thanh nút nhỏ trên cây: chỉ thêm và sửa, đồng nhất với màn mặt hàng/bàn (xóa/làm mới nằm ở menu chuột phải và phím Del).
            _treeBar.Height = (int)(36 * s);
            int x = (int)(6 * s);
            int size = (int)(28 * s);
            foreach (FlatButton b in new[] { _folderAdd, _folderEdit })
            {
                b.SetBounds(x, (int)(4 * s), size, size);
                x += size + (int)(6 * s);
                _treeBar.Controls.Add(b);
            }
            _folderAdd.Click += delegate { AddFolder(false); };
            _folderEdit.Click += delegate { EditFolder(); };

            _treeMenu = new ThemedContextMenu();
            ToolStripMenuItem add = _treeMenu.AddItem("Thêm thư mục", Keys.Insert, delegate { AddFolder(false); });
            ToolStripMenuItem addChild = _treeMenu.AddItem("Thêm thư mục con", delegate { AddFolder(true); });
            ToolStripMenuItem edit = _treeMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditFolder(); });
            ToolStripMenuItem del = _treeMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteFolder(); });
            _treeMenu.AddSeparator();
            _treeMenu.AddItem("Refresh", delegate { ReloadTree(SelectedFolderId()); });
            _treeMenu.Opening += delegate
            {
                bool real = SelectedFolderId() != null;
                add.Enabled = _canAdd;
                addChild.Enabled = _canAdd && real;
                edit.Enabled = _canEdit && real;
                del.Enabled = _canRemove && real;
            };
            _tree.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right)
                    return;
                TreeNode node = _tree.GetNodeAt(e.X, e.Y);
                if (node != null)
                    _tree.SelectedNode = node;
                _treeMenu.Show(_tree, e.Location);
            };
            _tree.AfterSelect += delegate { UpdateTreeButtons(); };
            UpdateTreeButtons();
        }

        // ------------------------------------------------------------------ Menu chuột phải của lưới

        private ThemedContextMenu _gridMenu;

        /// <summary>
        /// Menu chuột phải lưới phiếu đặt, theo menu chuẩn của lưới app gốc (No1DataGrid) như các màn danh mục khác:
        /// Thêm mới / Chỉnh sửa / Xóa, Sắp xếp theo, Refresh, In danh sách, Sao chép, Cột hiển thị, Tự động dãn cột, Thuộc tính.
        /// (Bỏ Thùng rác/Khôi phục theo quyết định bỏ thùng rác; spec Q11 mặc định bỏ cả Sao chép — để lại cho thống nhất
        /// với các màn khác, chờ Chiến chốt.)
        /// </summary>
        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem add = _gridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddBooking(); });
            ToolStripMenuItem edit = _gridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditBooking(); });
            ToolStripMenuItem del = _gridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteBooking(); });
            _gridMenu.AddSeparator();
            ToolStripMenuItem sort = _gridMenu.AddItem("Sắp xếp theo", null);
            AddSortItem(sort, "Ngày (mới nhất trước)", "colDate", false);
            AddSortItem(sort, "Ngày (cũ nhất trước)", "colDate", true);
            AddSortItem(sort, "Số phiếu (A → Z)", "colNumber", true);
            AddSortItem(sort, "Số phiếu (Z → A)", "colNumber", false);
            AddSortItem(sort, "Tên khách (A → Z)", "colCustomer", true);
            AddSortItem(sort, "Tổng cộng (cao → thấp)", "colTotal", false);
            AddSortItem(sort, "Tổng cộng (thấp → cao)", "colTotal", true);
            _gridMenu.AddItem("Refresh", delegate { Reload(); });
            _gridMenu.AddItem("In danh sách", delegate { Msg.Info(this, BookingText.Unsupported); });
            _gridMenu.AddSeparator();
            ToolStripMenuItem copy = _gridMenu.AddItem("Sao chép", null);
            var copyCell = new ToolStripMenuItem("Sao chép ô") { Font = _gridMenu.Font };
            copyCell.Click += delegate { DetailGridMenu.CopyCell(_grid); };
            var copySel = new ToolStripMenuItem("Sao chép vùng chọn") { Font = _gridMenu.Font };
            copySel.Click += delegate { DetailGridMenu.CopySelection(_grid); };
            copy.DropDownItems.Add(copyCell);
            copy.DropDownItems.Add(copySel);
            ToolStripMenuItem columns = _gridMenu.AddItem("Cột hiển thị", null);
            _gridMenu.AddItem("Tự động dãn cột", delegate { DetailGridMenu.AutoFit(_grid); });
            _gridMenu.AddItem("Thuộc tính", delegate { Msg.Info(this, BookingText.Unsupported); });

            _gridMenu.Opening += delegate
            {
                bool hasRow = CurrentBookingId() != null;
                add.Enabled = _canAdd;
                edit.Enabled = _canEdit && hasRow;
                del.Enabled = _canRemove && hasRow;
                copy.Enabled = _grid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(columns, _grid, _gridMenu.Font);
            };
        }

        private void AddSortItem(ToolStripMenuItem parent, string text, string column, bool asc)
        {
            var item = new ToolStripMenuItem(text) { Font = _gridMenu.Font };
            item.Click += delegate
            {
                DataGridViewColumn col = _grid.Columns[column];
                if (col != null)
                    _grid.Sort(col, asc ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending);
            };
            parent.DropDownItems.Add(item);
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                _grid.ClearSelection();
                int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : 0;
                _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[col];
                _grid.Rows[hit.RowIndex].Selected = true;
            }
            else
            {
                _grid.ClearSelection();
            }
            _gridMenu.Show(_grid, e.Location);
        }

        private void BuildBottom(float s)
        {
            _bottom.Height = (int)(220 * s);
            var info = new TabPage("Thông tin") { Font = new Font("Segoe UI", 10f) };
            AddInfoLabel(info, "Khởi tạo:", _lblCreated, 10, 12, 150, s);
            AddInfoLabel(info, "Khởi tạo bởi:", _lblCreatedBy, 330, 12, 115, s);
            AddInfoLabel(info, "Sửa đổi gần nhất:", _lblModified, 10, 42, 150, s);
            AddInfoLabel(info, "Sửa đổi bởi:", _lblModifiedBy, 330, 42, 115, s);
            _infoTabs.TabPages.Add(info);
            _bottom.Controls.Add(_infoTabs);
            _detail = new DetailTabsHost(_infoTabs, _databasePath, "TDATHANG", this);
            _detail.Attach();
        }

        private static void AddInfoLabel(TabPage page, string caption, Label value, int x, int y, int capW, float s)
        {
            var cap = new Label { Text = caption, AutoSize = true };
            cap.Location = new Point((int)(x * s), (int)(y * s));
            value.Location = new Point((int)((x + capW) * s), (int)(y * s));
            page.Controls.Add(cap);
            page.Controls.Add(value);
        }

        private void UpdateInfo()
        {
            string id = CurrentBookingId();
            if (_detail != null)
                _detail.SetMaster(id);
            var row = _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as BookingListRow;
            _lblCreated.Text = row == null ? "" : FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row == null ? "" : (row.CreatedByName ?? "");
            _lblModified.Text = row == null ? "" : FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row == null ? "" : (row.ModifiedByName ?? "");
        }

        private static string FormatAuditTime(DateTime? t)
        {
            return t.HasValue ? t.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "";
        }

        private void BuildBar(float s)
        {
            _bar.Height = (int)(52 * s);
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                // Cùng số đo thanh nút của CatalogView (lề 10, nút cao 32) để các màn danh mục đồng bộ.
                Padding = new Padding((int)(10 * s), (int)(10 * s), (int)(10 * s), (int)(10 * s))
            };
            _btnAdd.Tag = "primary"; // "Thêm mới" là nút chính ở mọi màn danh mục
            _btnRemove.Tag = "danger";
            foreach (FlatButton b in new[] { _btnFilter, _btnAdd, _btnEdit, _btnRemove, _btnExcel, _btnPrint, _btnTotal, _btnAnalysis, _refresh })
            {
                b.Height = (int)(32 * s);
                b.Margin = new Padding(0, 0, (int)(5 * s), 0);
                b.Font = new Font("Segoe UI", 9.5f); // cùng cỡ chữ nút các màn danh mục khác → 9 nút vừa một hàng
                // Rộng theo chữ (trước đây nút bị cắt "Thêm ...", "Chỉnh ...").
                b.Width = TextRenderer.MeasureText(b.Text, b.Font).Width + (int)(22 * s);
                flow.Controls.Add(b);
            }
            _btnAdd.Click += delegate { AddBooking(); };
            _btnEdit.Click += delegate { EditBooking(); };
            _btnRemove.Click += delegate { DeleteBooking(); };
            _btnExcel.Click += delegate { ExportGridToExcel(); };
            _btnPrint.Click += delegate { PrintSelectedBooking(); };
            _btnTotal.Click += delegate { Msg.Info(this, BookingText.Unsupported); };
            _btnAnalysis.Click += delegate { Msg.Info(this, BookingText.Unsupported); };
            _refresh.Click += delegate { Reload(); };
            _count.Width = (int)(100 * s);
            _count.Dock = DockStyle.Right;
            _count.Padding = new Padding(0, 0, (int)(12 * s), 0); // lề phải: nhãn đếm không còn sát mép
            _count.Font = new Font("Segoe UI", 9.5f);
            _bar.Controls.Add(flow);
            _bar.Controls.Add(_count);
        }

        private void BuildGrid(float s)
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.RowTemplate.Height = (int)(28 * s);
            _grid.Font = new Font("Segoe UI", 10f);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            AddGridColumn("colDate", "Ngày", 90, typeof(DateTime), "dd/MM/yyyy");
            AddGridColumn("colNumber", "Số phiếu", 110, typeof(string), null);
            AddGridColumn("colCustomer", "Tên khách", 160, typeof(string), null);
            AddGridColumn("colAddress", "Địa chỉ", 170, typeof(string), null);
            AddGridColumn("colPhone", "Điện thoại", 110, typeof(string), null);
            AddGridColumn("colEmail", "Email", 150, typeof(string), null);
            AddGridColumn("colTotal", "Tổng cộng", 110, typeof(decimal), "N0");
            AddGridColumn("colMethod", "Phương thức đặt", 150, typeof(string), null);
            AddGridColumn("colPurpose", "Mục đích đặt", 130, typeof(string), null);
            AddGridColumn("colFromTime", "Từ giờ", 80, typeof(DateTime), "HH:mm");
            AddGridColumn("colToTime", "Đến giờ", 80, typeof(DateTime), "HH:mm");
            AddGridColumn("colBookTime", "Giờ đặt", 130, typeof(DateTime), "dd/MM/yyyy HH:mm");
            AddGridColumn("colRoom", "Bàn", 110, typeof(string), null);
            AddGridColumn("colCustomerName", "Khách hàng", 130, typeof(string), null);
            AddGridColumn("colGoods", "Tiền hàng", 110, typeof(decimal), "N0");
            AddGridColumn("colDiscount", "Tiền giảm giá", 110, typeof(decimal), "N0");
            AddGridColumn("colDiscountPct", "Tỉ lệ giảm giá", 80, typeof(decimal), "#,##0.##");
            AddGridColumn("colNote", "Ghi chú", 180, typeof(string), null);
            _grid.Columns["colTotal"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["colGoods"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["colDiscount"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["colDiscountPct"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["colFromTime"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns["colToTime"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Controls.Add(_empty);
            _grid.Resize += delegate { PositionEmpty(); };
            ApplyTheme(ThemeManager.Current);
        }

        private void AddGridColumn(string name, string header, int width96, Type type, string format)
        {
            float s = DeviceDpi / 96f;
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = (int)(width96 * s),
                ValueType = type,
                ReadOnly = true
            };
            if (format != null)
            {
                col.DefaultCellStyle.Format = format;
                col.DefaultCellStyle.FormatProvider = new CultureInfo("vi-VN");
            }
            _grid.Columns.Add(col);
        }

        private void PositionEmpty()
        {
            _empty.SetBounds(0, _grid.Height / 2 - 15, _grid.Width, 30);
        }

        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_grid.RowHeadersVisible)
                return;
            string text = (e.RowIndex + 1).ToString();
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, ThemeManager.Current.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        // ------------------------------------------------------------------ Nạp + thao tác

        private BookingFilter CurrentFilter()
        {
            string method = null;
            List<string> methodIds = null;
            if (_tree.SelectedNode != null)
            {
                method = _tree.SelectedNode.Tag as string;
                if (!string.IsNullOrEmpty(method))
                {
                    // Chọn một thư mục = lấy cả các thư mục con của nó.
                    methodIds = new List<string>();
                    CollectFolderIds(_tree.SelectedNode, methodIds);
                }
            }
            return new BookingFilter
            {
                NumberText = _txtNumber.Text,
                FromDate = _dtFrom.Value.Date,
                ToDate = _dtTo.Value.Date,
                CustomerId = _customer.SelectedId,
                MethodId = method,
                MethodIds = methodIds
            };
        }

        private void Reload()
        {
            _loading = true;
            try
            {
                _rows = _repo.LoadBookings(CurrentFilter());
                _empty.Visible = _rows.Count == 0;
                _empty.Text = "Không có phiếu đặt nào trong khoảng lọc.";
            }
            catch (DataAccessException ex)
            {
                _rows = new List<BookingListRow>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được dữ liệu: " + ex.Message;
            }
            FillGrid();
            _count.Text = _rows.Count + " phiếu";
            _loading = false;
            UpdateButtons();
            UpdateInfo();
            ApplyTheme(ThemeManager.Current);
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();
            foreach (BookingListRow row in _rows)
            {
                int i = _grid.Rows.Add(
                    row.Ngay, row.Name, row.TenKhach, row.DiaChi, row.DienThoai, row.Email,
                    row.TongCong, row.MethodName, row.PurposeName,
                    row.TuGio, row.DenGio, row.GioDat, row.RoomName, row.CustomerName,
                    row.TienHang, row.TienGiamGia, row.TileGiamGia, row.Note);
                _grid.Rows[i].Tag = row;
            }
            _grid.ClearSelection();
        }

        private List<string> RowIds()
        {
            var ids = new List<string>(_rows.Count);
            foreach (BookingListRow r in _rows)
                ids.Add(r.Id);
            return ids;
        }

        private string CurrentBookingId()
        {
            if (_grid.CurrentRow == null)
                return null;
            var row = _grid.CurrentRow.Tag as BookingListRow;
            return row == null ? null : row.Id;
        }

        private void AddBooking()
        {
            if (!_canAdd)
                return;
            using (var dlg = new BookingEditDialog(AppSession.Current, null, RowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void EditBooking()
        {
            if (!_canEdit) // K5: thiếu quyền Sửa thì không mở dialog "SỬA".
                return;
            string id = CurrentBookingId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new BookingEditDialog(AppSession.Current, id, RowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void DeleteBooking()
        {
            if (!_canRemove)
                return;
            string id = CurrentBookingId();
            if (string.IsNullOrEmpty(id))
                return;
            if (!Msg.Confirm(this, BookingText.DeleteConfirm))
                return;
            BookingDeleteResult result;
            try
            {
                result = _repo.DeleteBooking(id);
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
            ReloadAndSelect(null);
        }

        private void ReloadAndSelect(string id)
        {
            Reload();
            if (!string.IsNullOrEmpty(id))
            {
                foreach (DataGridViewRow r in _grid.Rows)
                {
                    var row = r.Tag as BookingListRow;
                    if (row != null && row.Id == id)
                    {
                        r.Selected = true;
                        _grid.CurrentCell = r.Cells[0];
                        break;
                    }
                }
            }
        }

        private void UpdateButtons()
        {
            bool hasRow = CurrentBookingId() != null;
            _btnAdd.Enabled = _canAdd;
            _btnEdit.Enabled = _canEdit && hasRow;
            _btnRemove.Enabled = _canRemove && hasRow;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Đang gõ trong ô nhập thì Insert/Delete/F4 là của ô đó (vd. F4 mở dropdown), không phải phím màn hình.
            bool inEditor = sender is TextBox || sender is ComboBox || sender is DateTimePicker;
            if (e.KeyCode == Keys.F3)
            {
                // K4: F3 chạy lọc (như nút "Lọc (F3)"), rồi đưa con trỏ về ô Số phiếu.
                Reload();
                _txtNumber.Inner.Focus();
                _txtNumber.Inner.SelectAll();
                e.Handled = true;
                return;
            }
            if (inEditor)
                return;
            // Đang ở cây thư mục: Insert/F4/Del là thêm/sửa/xóa THƯ MỤC, không phải phiếu đặt.
            if (sender == _tree)
            {
                if (e.KeyCode == Keys.Insert)
                    AddFolder(false);
                else if (e.KeyCode == Keys.F4)
                    EditFolder();
                else if (e.KeyCode == Keys.Delete)
                    DeleteFolder();
                else
                    return;
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Insert && _canAdd)
            {
                AddBooking();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4)
            {
                EditBooking();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && _canRemove)
            {
                DeleteBooking();
                e.Handled = true;
            }
        }

        // ------------------------------------------------------------------ Giao diện

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
            _bar.BackColor = t.Background;
            _count.BackColor = Color.Transparent;
            _count.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = t.TextMuted;
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _bottom.BackColor = t.Background;
            _tree.BackColor = t.Surface;
            _tree.ForeColor = t.Text;
            foreach (FieldFrame f in _frames)
                f.ApplyTheme(t, t.Background);
            _infoTabs.BackColor = t.Background;
            _infoTabs.ForeColor = t.Text;
            foreach (TabPage p in _infoTabs.TabPages)
                p.BackColor = t.Background;
            if (_detail != null)
                _detail.ApplyTheme(t);
            _refresh.SurfaceColor = t.Background;
            _refresh.FillColor = t.Surface;
            _refresh.TextColor = t.Text;
            _refresh.BorderColor = Draw.StrongBorder(t);
            _refresh.Invalidate();
            ThemeApplier.ApplyGrid(_grid, t);
            UpdateButtons();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _treeIcons.Dispose();
                if (_gridMenu != null)
                    _gridMenu.Dispose();
                if (_treeMenu != null)
                    _treeMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
