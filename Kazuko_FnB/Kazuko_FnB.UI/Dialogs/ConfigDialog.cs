using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Quản trị ▸ Cấu hình toàn hệ thống. Nhóm ở bên trái, các ô của nhóm ở bên phải (cuộn dọc khi dài).
    /// Ô tích nằm hai cột; ô số/chữ/combo/ảnh mỗi ô một hàng; ô phụ (Tab = 30) thụt vào sau ô cha.
    /// Ghi dữ liệu chỉ bật khi có thay đổi; Thoát khi chưa ghi thì hỏi.
    /// </summary>
    internal sealed class ConfigDialog : ThemedDialog, IScreenPreview
    {
        private readonly ConfigViewModel _vm;
        private readonly ListBox _groups = new ListBox { BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
        private readonly FlowLayoutPanel _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            Tag = "surface"
        };
        private readonly TextBox _search = new TextBox { Dock = DockStyle.Top };
        private readonly FlatButton _save;
        private const int EM_SETCUEBANNER = 0x1501;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private bool _building;
        private int _pageWidth;

        public ConfigDialog(UserSession session)
            : base("CẤU HÌNH TOÀN HỆ THỐNG", "Thông tin cấu hình toàn bộ hệ thống", 840, 680)
        {
            _vm = new ConfigViewModel(session.DatabasePath);
            float s = Dpi;

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _save = AddButton("Ghi dữ liệu", "primary", delegate { DoSave(); });
            _save.Enabled = false;
            CancelButton = exit;

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding((int)(10 * s), 0, 0, 0) };
            _search.Margin = new Padding(0, 0, 0, (int)(6 * s));
            right.Controls.Add(_flow);
            right.Controls.Add(new Panel { Dock = DockStyle.Top, Height = (int)(6 * s) });
            right.Controls.Add(_search);

            _groups.Dock = DockStyle.Left;
            _groups.Width = (int)(200 * s);
            _groups.ItemHeight = (int)(26 * s);
            _groups.IntegralHeight = false;
            foreach (ConfigGroup g in _vm.Groups)
                _groups.Items.Add(g.Name);
            _groups.SelectedIndexChanged += delegate { if (!_building) ShowGroupPage(); };

            Body.Controls.Add(right);
            Body.Controls.Add(_groups);

            _search.TextChanged += delegate { ShowCurrent(); };
            _search.HandleCreated += delegate { SendMessage(_search.Handle, EM_SETCUEBANNER, (IntPtr)1, "Tìm kiếm (Ctrl + F)"); };
            // Rộng vùng nội dung: tính từ kích thước hộp thoại (ổn định cả khi chưa hiển thị).
            _pageWidth = ClientSize.Width - (int)(200 * s) - (int)(24 * s) - (int)(10 * s) - (int)(24 * s);

            FormClosing += OnFormClosing;
            if (_groups.Items.Count > 0)
                _groups.SelectedIndex = 0;
            _search.Select();
        }

        // ------------------------------------------------------------------ trang

        private void ShowGroupPage()
        {
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            if (!string.IsNullOrWhiteSpace(_search.Text))
            {
                SubtitleText = "Kết quả tìm \"" + _search.Text.Trim() + "\" trong mọi nhóm";
                BuildPage(_vm.Search(_search.Text));
                return;
            }
            SubtitleText = "Thông tin cấu hình toàn bộ hệ thống";
            if (_groups.SelectedIndex >= 0 && _groups.SelectedIndex < _vm.Groups.Count)
                BuildPage(_vm.ItemsOf(_vm.Groups[_groups.SelectedIndex]));
            else
                BuildPage(new List<ConfigItem>());
        }

        private void BuildPage(List<ConfigItem> items)
        {
            _building = true;
            _flow.SuspendLayout();
            foreach (Control c in _flow.Controls.Cast<Control>().ToList())
            {
                _flow.Controls.Remove(c);
                c.Dispose();
            }
            float s = Dpi;
            int full = _pageWidth - (int)(8 * s);
            int half = full / 2 - (int)(4 * s);
            foreach (ConfigItem it in items)
                _flow.Controls.Add(BuildRow(it, full, half, s));
            _flow.ResumeLayout(true);
            ApplyDialogTheme();
            _building = false;
        }

        private Control BuildRow(ConfigItem it, int full, int half, float s)
        {
            int rowH = (int)(34 * s);
            int indent = it.Tab == 30 ? (int)(22 * s) : 0;
            string name = it.Name;

            switch (it.Control)
            {
                case ConfigControl.Check:
                {
                    var cb = new CheckBox
                    {
                        Text = it.Caption,
                        AutoSize = false,
                        // Caption dài thì chiếm cả hàng (CheckBox không tự xuống dòng).
                        Width = (it.Caption != null && it.Caption.Length > 32 ? full : half) - indent,
                        Height = rowH,
                        Margin = new Padding(indent, 0, 4, 0),
                        Checked = (bool)_vm.Get(name),
                        CheckAlign = ContentAlignment.MiddleLeft,
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    cb.CheckedChanged += delegate { _vm.Set(name, cb.Checked); UpdateSaveButton(); };
                    return cb;
                }

                case ConfigControl.Image:
                    return BuildImageRow(it, full, s);

                default:
                {
                    bool multi = it.Control == ConfigControl.MultiLine;
                    int h = multi ? (int)(78 * s) : rowH;
                    var row = new Panel { Width = full - indent, Height = h, Margin = new Padding(indent, 0, 4, 0), Tag = "surface" };
                    int labelW = (int)((full - indent) * 0.5);
                    row.Controls.Add(new Label
                    {
                        Text = it.Caption,
                        AutoSize = false,
                        Left = 0,
                        Top = 0,
                        Width = labelW - 6,
                        Height = multi ? (int)(30 * s) : h,
                        TextAlign = ContentAlignment.MiddleLeft
                    });
                    Control input = BuildInput(it, row.Width - labelW, multi ? h - (int)(6 * s) : h - (int)(8 * s));
                    input.Left = labelW;
                    input.Top = (int)(3 * s);
                    row.Controls.Add(input);
                    return row;
                }
            }
        }

        private Control BuildInput(ConfigItem it, int width, int height)
        {
            string name = it.Name;
            switch (it.Control)
            {
                case ConfigControl.Number:
                {
                    var n = new NumericUpDown
                    {
                        Minimum = int.MinValue,
                        Maximum = int.MaxValue,
                        DecimalPlaces = 0,
                        ThousandsSeparator = true,
                        Width = Math.Min(width, (int)(Dpi * 170)),
                        Value = Convert.ToDecimal(_vm.Get(name) ?? 0m)
                    };
                    n.ValueChanged += delegate { _vm.Set(name, n.Value); UpdateSaveButton(); };
                    return n;
                }
                case ConfigControl.Combo:
                {
                    var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
                    foreach (string t in it.ComboItems)
                        cb.Items.Add(t);
                    int idx = (int)_vm.Get(name);
                    cb.SelectedIndex = idx >= 0 && idx < cb.Items.Count ? idx : (cb.Items.Count > 0 ? 0 : -1);
                    cb.SelectedIndexChanged += delegate { _vm.Set(name, Math.Max(0, cb.SelectedIndex)); UpdateSaveButton(); };
                    return cb;
                }
                case ConfigControl.Lookup:
                {
                    var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
                    cb.Items.Add(new Choice("", ""));
                    foreach (KeyValuePair<string, string> p in _vm.LookupOf(it))
                        cb.Items.Add(new Choice(p.Key, p.Value));
                    string cur = (string)_vm.Get(name) ?? "";
                    int sel = 0;
                    for (int k = 0; k < cb.Items.Count; k++)
                        if (string.Equals(((Choice)cb.Items[k]).Key, cur, StringComparison.OrdinalIgnoreCase))
                            sel = k;
                    cb.SelectedIndex = sel;
                    cb.SelectedIndexChanged += delegate
                    {
                        var c = cb.SelectedItem as Choice;
                        if (_building || c == null)
                            return;
                        _vm.Set(name, c.Key);
                        UpdateSaveButton();
                    };
                    return cb;
                }
                default:
                {
                    var tb = new TextBox
                    {
                        Width = width,
                        Text = (string)_vm.Get(name) ?? "",
                        Multiline = it.Control == ConfigControl.MultiLine,
                        ScrollBars = it.Control == ConfigControl.MultiLine ? ScrollBars.Vertical : ScrollBars.None
                    };
                    if (tb.Multiline)
                        tb.Height = height;
                    tb.TextChanged += delegate { _vm.Set(name, tb.Text); UpdateSaveButton(); };
                    return tb;
                }
            }
        }

        private Control BuildImageRow(ConfigItem it, int full, float s)
        {
            string name = it.Name;
            int h = (int)(96 * s);
            var row = new Panel { Width = full, Height = h, Margin = new Padding(0, 0, 4, 0), Tag = "surface" };
            row.Controls.Add(new Label { Text = it.Caption, AutoSize = false, Left = 0, Top = 0, Width = (int)(full * 0.5) - 6, Height = (int)(34 * s) });
            var pic = new PictureBox
            {
                Left = (int)(full * 0.5),
                Top = (int)(3 * s),
                Width = (int)(110 * s),
                Height = (int)(86 * s),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            ShowImage(pic, _vm.LoadImage(it));
            row.Controls.Add(pic);
            var choose = new FlatButton { Text = "Chọn…", Left = pic.Right + (int)(8 * s), Top = pic.Top, Width = (int)(80 * s), Height = (int)(34 * s), Radius = (int)(7 * s) };
            choose.Click += delegate
            {
                byte[] cur = _vm.LoadImage(it);
                byte[] data;
                if (!ImagePickerDialog.Pick(this, "Chọn ảnh", cur, out data))
                    return;
                try
                {
                    ShowImage(pic, data);
                }
                catch (Exception)
                {
                    Msg.Warning(this, ImageUtil.LoadError);
                    return;
                }
                _vm.Set(name, data);
                UpdateSaveButton();
            };
            row.Controls.Add(choose);
            var clear = new FlatButton { Text = "Bỏ ảnh", Left = choose.Left, Top = choose.Bottom + (int)(6 * s), Width = (int)(80 * s), Height = (int)(34 * s), Radius = (int)(7 * s) };
            clear.Click += delegate
            {
                ShowImage(pic, null);
                _vm.Set(name, null);
                UpdateSaveButton();
            };
            row.Controls.Add(clear);
            return row;
        }

        private static void ShowImage(PictureBox pic, byte[] data)
        {
            if (pic.Image != null)
            {
                pic.Image.Dispose();
                pic.Image = null;
            }
            if (data == null || data.Length == 0)
                return;
            using (var ms = new MemoryStream(data))
            using (Image img = Image.FromStream(ms))
                pic.Image = new Bitmap(img);
        }

        // ------------------------------------------------------------------ lưu / thoát

        private void UpdateSaveButton()
        {
            _save.Enabled = _vm.IsDirty;
        }

        private void DoSave()
        {
            try
            {
                _vm.Save(AppSession.UserId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return;
            }
            UpdateSaveButton();
            ShowCurrent();
            SubtitleText = "Đã ghi dữ liệu lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && _vm.IsDirty &&
                !Msg.Confirm(this, "Dữ liệu của bạn đã thay đổi, bạn có muốn hủy bỏ các thay đổi để thực hiện tiếp không?"))
                e.Cancel = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                _search.Focus();
                _search.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            _groups.BackColor = Theme.ThemeManager.Current.Surface;
            _groups.ForeColor = Theme.ThemeManager.Current.Text;
        }

        // ------------------------------------------------------------------ lệnh --render-screen

        public void Preview(string option)
        {
            if (option.StartsWith("group=", StringComparison.Ordinal))
            {
                int i = _groups.Items.IndexOf(option.Substring(6));
                if (i >= 0)
                    _groups.SelectedIndex = i;
            }
            else if (option.StartsWith("search=", StringComparison.Ordinal))
            {
                _search.Text = option.Substring(7);
            }
        }
    }
}
