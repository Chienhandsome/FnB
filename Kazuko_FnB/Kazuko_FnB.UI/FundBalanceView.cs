using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Màn "Tồn quỹ" (chỉ xem), docs/specs/quy-tien.md §4.7: cây quỹ bên trái, bộ lọc ngày/cửa hàng,
    /// hàng số tổng và lưới thu chi kèm tồn cộng dồn. Mọi phép tính ở FundLedgerRules.
    /// </summary>
    internal sealed class FundBalanceView : Panel
    {
        /// <summary>ID mục menu "Tồn quỹ" (form tự viết của app gốc, không gắn bảng chuẩn).</summary>
        public const string MenuId = "380a7acb-1b00-49ba-875a-26757f9f3579";

        private const string Hidden = "—";

        private sealed class Node
        {
            public FundNodeKind Kind;
            public string AccountId;
        }

        private sealed class Card
        {
            public Panel Box;
            public Label Caption;
            public Label Value;
        }

        private readonly UserSession _session;
        private readonly FundRepository _repository;
        private readonly SplitContainer _split = new SplitContainer { Dock = DockStyle.Fill };
        private readonly TreeView _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, FullRowSelect = true, ShowLines = false };
        private readonly DateBox _from = new DateBox();
        private readonly DateBox _to = new DateBox();
        private readonly ComboBox _store = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FlatButton _refresh = new FlatButton { Text = "Làm mới (F5)", Tag = "primary" };
        private readonly FlowLayoutPanel _filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly FlowLayoutPanel _summary = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly Label _note = new Label { Dock = DockStyle.Top, AutoSize = false, AutoEllipsis = true, Tag = "muted" };
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _empty = new Label { Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Text = "Không có thu chi trong khoảng đã chọn" };
        private readonly ToolTip _tip = new ToolTip();
        private readonly List<FieldFrame> _frames = new List<FieldFrame>();
        private readonly Card _openingCard;
        private readonly Card _openingEnteredCard;
        private readonly Card _incomeCard;
        private readonly Card _expenseCard;
        private readonly Card _closingCard;
        private readonly Card _transferCard;
        private readonly Card _cardCard;

        private FundData _data;
        private Node _selected = new Node { Kind = FundNodeKind.All };
        private bool _loading = true;
        private bool _splitPlaced;

        public FundBalanceView(UserSession session)
        {
            _session = session;
            _repository = new FundRepository(session.DatabasePath);
            DoubleBuffered = true;
            float s = DeviceDpi / 96f;
            var font = new Font("Segoe UI", 10f);

            BuildFilters(s, font);
            _openingCard = AddCard(s, "Tồn đầu kỳ");
            _openingEnteredCard = AddCard(s, "Nhập số dư đầu kỳ");
            _incomeCard = AddCard(s, "Thu");
            _expenseCard = AddCard(s, "Chi");
            _closingCard = AddCard(s, "Tồn cuối kỳ");
            _transferCard = AddCard(s, "Chuyển khoản (ròng)");
            _cardCard = AddCard(s, "Quẹt thẻ (ròng)");
            _summary.Padding = new Padding((int)(6 * s), 0, (int)(6 * s), (int)(4 * s));
            _note.Height = (int)(24 * s);
            _note.Padding = new Padding((int)(10 * s), 0, (int)(10 * s), 0);
            _note.Font = font;
            BuildGrid(s, font);

            var treeTitle = new Label
            {
                Text = "Quỹ",
                Dock = DockStyle.Top,
                Height = (int)(32 * s),
                Padding = new Padding((int)(8 * s), 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 10f)
            };
            _tree.Font = font;
            _tree.ItemHeight = (int)(32 * s); // đủ cao để chạm trên màn cảm ứng
            _tree.AfterSelect += delegate(object sender, TreeViewEventArgs e)
            {
                if (_loading || !(e.Node.Tag is Node))
                    return;
                _selected = (Node)e.Node.Tag;
                ShowLedger();
            };

            _split.Panel1.Controls.Add(_tree);
            _split.Panel1.Controls.Add(treeTitle);
            _split.Panel2.Controls.Add(_grid);
            _split.Panel2.Controls.Add(_empty);
            _split.Panel2.Controls.Add(_note);
            _split.Panel2.Controls.Add(_summary);
            _split.Panel2.Controls.Add(_filters);
            _grid.BringToFront();
            _split.FixedPanel = FixedPanel.Panel1;
            _split.SizeChanged += delegate { PlaceSplit(); };
            Controls.Add(_split);

            ThemeManager.ThemeChanged += OnThemeChanged;
            ApplyTheme(ThemeManager.Current);
            _loading = false;
            Reload();
        }

        // ------------------------------------------------------------------ Dựng giao diện

        private void BuildFilters(float s, Font font)
        {
            _filters.Padding = new Padding((int)(8 * s), (int)(6 * s), (int)(8 * s), (int)(4 * s));
            // Mặc định: từ ngày 01 tháng này đến hôm nay.
            DateTime today = DateTime.Today;
            _from.Value = new DateTime(today.Year, today.Month, 1);
            _to.Value = today;
            _from.Inner.Font = font;
            _to.Inner.Font = font;
            _store.Font = font;
            var storeFrame = new FieldFrame(_store, 0);
            _frames.Add(_from);
            _frames.Add(_to);
            _frames.Add(storeFrame);

            AddFilter(s, font, "Từ ngày", _from, 130);
            AddFilter(s, font, "Đến ngày", _to, 130);
            AddFilter(s, font, "Cửa hàng", storeFrame, 230);
            _refresh.Size = new Size((int)(130 * s), (int)(34 * s));
            _refresh.Font = font;
            _refresh.Margin = new Padding((int)(6 * s), (int)(2 * s), 0, (int)(2 * s));
            _refresh.Click += delegate { Reload(); };
            _filters.Controls.Add(_refresh);

            _from.ValueChanged += delegate { if (!_loading) Reload(); };
            _to.ValueChanged += delegate { if (!_loading) Reload(); };
            _store.SelectedIndexChanged += delegate { if (!_loading) ShowLedger(); };
        }

        private void AddFilter(float s, Font font, string caption, Control field, int width)
        {
            _filters.Controls.Add(new Label
            {
                Text = caption,
                AutoSize = false,
                Size = new Size(TextRenderer.MeasureText(caption, font).Width + (int)(6 * s), (int)(34 * s)),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = font,
                Margin = new Padding((int)(4 * s), (int)(2 * s), 0, (int)(2 * s))
            });
            field.Size = new Size((int)(width * s), (int)(32 * s));
            field.Margin = new Padding((int)(2 * s), (int)(3 * s), (int)(8 * s), (int)(3 * s));
            _filters.Controls.Add(field);
        }

        private Card AddCard(float s, string caption)
        {
            var card = new Card
            {
                Box = new Panel { Tag = "surface", Size = new Size((int)(168 * s), (int)(58 * s)), Margin = new Padding((int)(4 * s)) },
                Caption = new Label { Text = caption, Tag = "muted", Dock = DockStyle.Top, Height = (int)(24 * s), Padding = new Padding((int)(8 * s), (int)(4 * s), 0, 0), Font = new Font("Segoe UI", 9f) },
                Value = new Label { Dock = DockStyle.Fill, Padding = new Padding((int)(8 * s), 0, (int)(8 * s), 0), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", 13f), AutoEllipsis = true }
            };
            card.Box.Controls.Add(card.Value);
            card.Box.Controls.Add(card.Caption);
            _summary.Controls.Add(card.Box);
            return card;
        }

        private void BuildGrid(float s, Font font)
        {
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.Font = font;
            _grid.RowTemplate.Height = (int)(30 * s);
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(32 * s);
            AddColumn(s, "colTime", "Thời gian", 160, false);
            AddColumn(s, "colNumber", "Số chứng từ", 118, false);
            AddColumn(s, "colType", "Loại", 145, false);
            AddColumn(s, "colFund", "Quỹ", 170, false);
            AddColumn(s, "colAccount", "Tài khoản", 100, false);
            AddColumn(s, "colIncome", "Thu", 100, true);
            AddColumn(s, "colExpense", "Chi", 100, true);
            AddColumn(s, "colBalance", "Tồn", 110, true);
            _grid.Columns["colAccount"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns["colAccount"].MinimumWidth = (int)(100 * s);
            _empty.Height = (int)(32 * s);
            _empty.Font = font;
        }

        private void AddColumn(float s, string name, string header, int width, bool money)
        {
            var column = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = (int)(width * s),
                SortMode = DataGridViewColumnSortMode.NotSortable // tồn cộng dồn chỉ đúng theo thứ tự thời gian
            };
            if (money)
            {
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            _grid.Columns.Add(column);
        }

        private void PlaceSplit()
        {
            if (_splitPlaced)
                return;
            int want = (int)(250 * DeviceDpi / 96f);
            if (_split.Width > want + (int)(200 * DeviceDpi / 96f))
            {
                _split.SplitterDistance = want;
                _splitPlaced = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                Reload();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ Dữ liệu

        private void Reload()
        {
            try
            {
                _data = _repository.Load(_to.Value);
            }
            catch (Exception ex) when (ex is DataAccessException || ex is InvalidDataException || ex is InvalidOperationException)
            {
                _data = null;
                _grid.Rows.Clear();
                _tree.Nodes.Clear();
                SetCards(null);
                ShowNote("Không đọc được dữ liệu quỹ: " + ex.Message);
                return;
            }
            FillStores();
            ShowLedger();
        }

        private List<string> UserStoreIds()
        {
            return _session.Stores == null ? new List<string>() : _session.Stores.Select(x => x.Id).ToList();
        }

        private void FillStores()
        {
            var keep = _store.SelectedItem as StoreRecord;
            List<string> allowed = FundLedgerRules.AllowedStores(_session.IsAdmin,
                _data.Stores.Select(x => x.Id).ToList(), UserStoreIds());

            bool was = _loading;
            _loading = true;
            _store.Items.Clear();
            _store.Items.Add(new StoreRecord { Id = "", Name = "Tất cả cửa hàng" });
            foreach (StoreRecord store in _data.Stores.Where(x => allowed.Contains(x.Id)))
                _store.Items.Add(store);
            int index = 0;
            for (int i = 0; i < _store.Items.Count; i++)
                if (keep != null && ((StoreRecord)_store.Items[i]).Id == keep.Id)
                    index = i;
            _store.SelectedIndex = index;
            _loading = was;
        }

        private FundQuery Query(FundStoreScope scope, Node node)
        {
            return new FundQuery
            {
                From = _from.Value,
                To = _to.Value,
                Scope = scope,
                Node = node.Kind,
                AccountId = node.AccountId,
                KnownAccountIds = _data.Accounts.Select(x => x.Id).ToList()
            };
        }

        private void ShowLedger()
        {
            if (_data == null)
                return;
            var selectedStore = _store.SelectedItem as StoreRecord;
            FundStoreScope scope = FundLedgerRules.ResolveScope(_session.IsAdmin,
                _data.Stores.Select(x => x.Id).ToList(), UserStoreIds(), selectedStore == null ? "" : selectedStore.Id);

            // Dựng sổ "Tất cả" trước để biết cần nút "Chưa xác định tài khoản" và tài khoản ngừng dùng nào còn số liệu.
            FundLedger all = FundLedgerRules.Build(_data.Sources, Query(scope, new Node { Kind = FundNodeKind.All }));
            FillTree(all);
            FundLedger ledger = FundLedgerRules.Build(_data.Sources, Query(scope, _selected));

            Dictionary<string, string> accountNames = _data.Accounts.ToDictionary(x => x.Id, x => x.Name);
            _grid.Rows.Clear();
            foreach (FundLedgerLine line in ledger.Lines)
            {
                string account = "";
                if (line.Fund == FundKind.Account && !accountNames.TryGetValue(line.AccountId, out account))
                    account = "Chưa xác định";
                int row = _grid.Rows.Add(
                    line.Time.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                    line.Number,
                    line.TypeText,
                    line.Fund == FundKind.Cash ? "Tiền mặt" : (line.IsCard ? "Tài khoản (quẹt thẻ)" : "Tài khoản"),
                    account,
                    line.Income == 0m ? "" : Money(line.Income),
                    line.Expense == 0m ? "" : Money(line.Expense),
                    line.Balance.HasValue ? Money(line.Balance.Value) : "");
                if (line.IsOpening)
                    _grid.Rows[row].DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            }
            _grid.ClearSelection();
            _grid.Columns["colBalance"].Visible = ledger.ShowBalance;
            _empty.Visible = ledger.Lines.Count == 0;
            SetCards(ledger);
            ShowNote(NoteText(scope, ledger, accountNames));
        }

        private void FillTree(FundLedger all)
        {
            var withData = new HashSet<string>(all.Lines.Where(x => x.Fund == FundKind.Account).Select(x => x.AccountId));
            bool was = _loading;
            _loading = true;
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            TreeNode root = Make("Tất cả", FundNodeKind.All, null);
            root.Nodes.Add(Make("Tiền mặt", FundNodeKind.Cash, null));
            TreeNode accounts = Make("Tài khoản", FundNodeKind.AccountAll, null);
            // Tài khoản ngừng dùng chỉ hiện khi còn số liệu, để tổng các tài khoản vẫn bằng quỹ Tài khoản.
            foreach (FundAccount account in _data.Accounts.Where(x => x.Active || withData.Contains(x.Id)))
                accounts.Nodes.Add(Make(account.Active ? account.Name : account.Name + " (ngừng dùng)",
                    FundNodeKind.Account, account.Id));
            if (all.HasUnknownAccount)
                accounts.Nodes.Add(Make("Chưa xác định tài khoản", FundNodeKind.UnknownAccount, null));
            root.Nodes.Add(accounts);
            _tree.Nodes.Add(root);
            _tree.ExpandAll();

            TreeNode current = Find(root, _selected) ?? root;
            _selected = (Node)current.Tag;
            _tree.SelectedNode = current;
            _tree.EndUpdate();
            _loading = was;
        }

        private static TreeNode Make(string text, FundNodeKind kind, string accountId)
        {
            return new TreeNode(text) { Tag = new Node { Kind = kind, AccountId = accountId } };
        }

        private static TreeNode Find(TreeNode from, Node wanted)
        {
            var node = (Node)from.Tag;
            if (node.Kind == wanted.Kind && node.AccountId == wanted.AccountId)
                return from;
            foreach (TreeNode child in from.Nodes)
            {
                TreeNode found = Find(child, wanted);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static string Money(decimal value)
        {
            return value.ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN"));
        }

        private void SetCards(FundLedger ledger)
        {
            bool balance = ledger != null && ledger.ShowBalance;
            _openingCard.Value.Text = balance ? Money(ledger.OpeningBalance) : Hidden;
            _closingCard.Value.Text = balance ? Money(ledger.ClosingBalance) : Hidden;
            _incomeCard.Value.Text = ledger == null ? Hidden : Money(ledger.Income);
            _expenseCard.Value.Text = ledger == null ? Hidden : Money(ledger.Expense);
            _openingEnteredCard.Box.Visible = balance && ledger.OpeningEntered != 0m;
            _openingEnteredCard.Value.Text = balance ? Money(ledger.OpeningEntered) : Hidden;

            bool oneAccount = ledger != null && _selected.Kind == FundNodeKind.Account;
            _transferCard.Box.Visible = oneAccount;
            _cardCard.Box.Visible = oneAccount;
            if (oneAccount)
            {
                _transferCard.Value.Text = Money(ledger.TransferNet);
                _cardCard.Value.Text = Money(ledger.CardNet);
            }
        }

        private string NoteText(FundStoreScope scope, FundLedger ledger, Dictionary<string, string> accountNames)
        {
            var notes = new List<string>();
            if (_from.Value > _to.Value)
                notes.Add("\"Từ ngày\" đang sau \"Đến ngày\".");
            if (!scope.CoversAll && scope.StoreIds.Count == 0)
                notes.Add("Bạn chưa được gán cửa hàng nào.");
            if (!ledger.ShowBalance)
                notes.Add("Tồn tài khoản chỉ hiện khi xem tất cả cửa hàng.");

            var missing = new List<string>();
            Dictionary<string, string> storeNames = _data.Stores.ToDictionary(x => x.Id, x => x.Name);
            foreach (string id in ledger.CashStoresWithoutOpening)
                missing.Add("tiền mặt " + (storeNames.ContainsKey(id) ? storeNames[id] : id));
            foreach (string id in ledger.AccountsWithoutOpening)
                if (accountNames.ContainsKey(id) && _data.Accounts.Any(x => x.Id == id && x.Active))
                    missing.Add(accountNames[id]);
            if (missing.Count > 0)
                notes.Add("Chưa nhập số dư đầu kỳ: " + string.Join(", ", missing) + ".");
            return string.Join("  ", notes);
        }

        private void ShowNote(string text)
        {
            _note.Text = text;
            _tip.SetToolTip(_note, text);
            _note.Visible = text.Length > 0;
        }

        /// <summary>Chỉ dùng cho --render-screen: "node=cash|account|all", "from=dd/MM/yyyy".</summary>
        internal void Preview(string option)
        {
            string[] parts = (option ?? "").Split(new[] { '=' }, 2);
            if (parts.Length != 2 || _data == null)
                return;
            if (parts[0] == "node")
            {
                FundAccount first = _data.Accounts.FirstOrDefault(x => x.Active);
                if (parts[1] == "cash")
                    _selected = new Node { Kind = FundNodeKind.Cash };
                else if (parts[1] == "account" && first != null)
                    _selected = new Node { Kind = FundNodeKind.Account, AccountId = first.Id };
                else
                    _selected = new Node { Kind = FundNodeKind.All };
                ShowLedger();
            }
            DateTime from;
            if (parts[0] == "from" && DateTime.TryParseExact(parts[1], "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out from))
                _from.Value = from;
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
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _tree.BackColor = t.Surface;
            _tree.ForeColor = t.Text;
            _tree.BorderStyle = BorderStyle.None;
            _filters.BackColor = t.Background;
            _summary.BackColor = t.Background;
            foreach (FieldFrame frame in _frames)
                frame.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyButton(_refresh, t, "primary");
            ThemeApplier.ApplyGrid(_grid, t);
            _empty.BackColor = t.Background;
            _empty.ForeColor = t.TextMuted;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
