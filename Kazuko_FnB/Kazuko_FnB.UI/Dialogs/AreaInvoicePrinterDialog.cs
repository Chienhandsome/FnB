using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Cấu hình máy in hóa đơn theo khu vực (QUẢN TRỊ, docs/specs/chien-chot-28-11.md mục 2): mỗi khu vực chọn một máy in
    /// cài trên máy này. Lưu vào tệp cục bộ theo máy (<see cref="IKitchenPrinterStore"/>, khóa
    /// <see cref="KitchenPrintRules.AreaInvoicePrinterKey"/> = IN_HD_KV_{id khu vực}), không đụng CSDL.
    /// Để trống = dùng máy in mặc định của Windows.
    /// </summary>
    internal sealed class AreaInvoicePrinterDialog : ThemedDialog
    {
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill };
        private readonly Label _lblHint = new Label { Dock = DockStyle.Top, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly IKitchenPrinterStore _store;
        private readonly List<AreaRow> _areas;
        private readonly Font _bodyFont;

        public AreaInvoicePrinterDialog(UserSession session, IKitchenPrinterStore store = null, IList<AreaRow> areas = null)
            : base("CẤU HÌNH MÁY IN HÓA ĐƠN THEO KHU VỰC", "Chọn máy in cho từng khu vực, lưu trên máy này", 640, 440)
        {
            float s = Dpi;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _store = store ?? KitchenPrintCoordinator.CreateDefaultPrinterStore();
            _areas = areas != null
                ? new List<AreaRow>(areas)
                : new AreaRepository(session.DatabasePath).LoadAll();

            AddButton("Lưu", "primary", delegate { SaveConfig(); });
            AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            FitBody(640, 330);

            _lblHint.Font = _bodyFont;
            _lblHint.Height = (int)(44 * s);
            _lblHint.Padding = new Padding((int)(4 * s), 0, 0, 0);

            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoGenerateColumns = false;
            _grid.Font = _bodyFont;
            _grid.RowTemplate.Height = Math.Max((int)(34 * s), 32);
            _grid.ColumnHeadersHeight = (int)(32 * s);
            _grid.DataError += delegate (object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colArea", HeaderText = "Khu vực", ReadOnly = true, Width = (int)(260 * s),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None, SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "colPrinter", HeaderText = "Máy in hóa đơn", FlatStyle = FlatStyle.Flat,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable
            });

            Body.Controls.Add(_grid);
            Body.Controls.Add(_lblHint);
            LoadRows();
        }

        internal DataGridView GridForTest { get { return _grid; } }
        internal Label HintForTest { get { return _lblHint; } }

        private void LoadRows()
        {
            var printerCol = (DataGridViewComboBoxColumn)_grid.Columns["colPrinter"];
            printerCol.Items.Clear();
            printerCol.Items.Add(""); // trống = máy in mặc định của Windows
            List<string> installed = _store.GetInstalledPrinters();
            for (int i = 0; i < installed.Count; i++)
                printerCol.Items.Add(installed[i]);

            _grid.Rows.Clear();
            for (int i = 0; i < _areas.Count; i++)
            {
                string saved = _store.GetConfiguredPrinter(KitchenPrintRules.AreaInvoicePrinterKey(_areas[i].Id)) ?? "";
                int idx = _grid.Rows.Add(_areas[i].Name ?? "", "");
                _grid.Rows[idx].Cells["colPrinter"].Value = printerCol.Items.Contains(saved) ? saved : "";
            }

            string mode = SystemConfig.InHoaDonTheoKhuVuc == 0
                ? "Lưu ý: cấu hình \"In hóa đơn theo khu vực\" đang tắt nên hóa đơn dùng máy in mặc định; bật cấu hình này để áp dụng bảng dưới."
                : "Chọn máy in cho từng khu vực rồi bấm Lưu. Để trống = máy in mặc định của Windows.";
            _lblHint.Text = installed.Count == 0 ? "Chưa tìm thấy máy in nào được cài trên máy này. " + mode : mode;
        }

        /// <summary>Ghi cấu hình máy in cho mọi khu vực vào tệp cục bộ rồi đóng hộp.</summary>
        internal void SaveConfig()
        {
            for (int i = 0; i < _grid.Rows.Count && i < _areas.Count; i++)
            {
                string val = (Convert.ToString(_grid.Rows[i].Cells["colPrinter"].Value) ?? "").Trim();
                _store.SetConfiguredPrinter(KitchenPrintRules.AreaInvoicePrinterKey(_areas[i].Id), val);
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            _lblHint.ForeColor = t.TextMuted;
            ThemeApplier.Apply(_grid, t);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _bodyFont.Dispose();
            base.Dispose(disposing);
        }
    }
}
