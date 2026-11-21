using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Form phụ "Lịch sử mua/bán" (spec 1.4: 526×440): lưới Ngày · Giờ · (Nhà cung cấp | Khách hàng) · Đơn giá.
    /// Mua: TDONHANG.LOAI = 1; bán: LOAI = 0 AND DATHANHTOAN = 30, xếp theo NGAY, GIOTHANHTOAN.
    /// </summary>
    internal sealed class ProductHistoryDialog : ThemedDialog
    {
        private readonly DataTable _table = new DataTable();
        private readonly DataView _view;
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _filter = new TextBox();

        public ProductHistoryDialog(string databasePath, string productId, string productName, bool buy)
            : base(buy ? "LỊCH SỬ MUA HÀNG" : "LỊCH SỬ BÁN HÀNG", "Tên mặt hàng: " + productName, 550, 470)
        {
            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            CancelButton = exit;
            float s = Dpi;

            var top = new Panel { Dock = DockStyle.Top, Height = (int)(36 * s) };
            var lblFilter = new Label { Text = "Lọc dữ liệu:", AutoSize = true };
            lblFilter.Location = new Point(0, (int)(10 * s));
            _filter.Location = new Point((int)(86 * s), (int)(6 * s));
            _filter.Size = new Size((int)(220 * s), (int)(24 * s));
            _filter.TextChanged += delegate { ApplyFilter(); };
            top.Controls.Add(lblFilter);
            top.Controls.Add(_filter);
            Body.Controls.Add(top);

            _table.Columns.Add("Date", typeof(DateTime));
            _table.Columns.Add("Time", typeof(string));
            _table.Columns.Add("Partner", typeof(string));
            _table.Columns.Add("PartnerFold", typeof(string));
            _table.Columns.Add("Price", typeof(decimal));
            _view = new DataView(_table);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoGenerateColumns = false;
            _grid.DataSource = _view;
            var colDate = new DataGridViewTextBoxColumn
            {
                Name = "colDate", DataPropertyName = "Date", HeaderText = "Ngày",
                Width = (int)(110 * s), DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" }
            };
            var colTime = new DataGridViewTextBoxColumn
            {
                Name = "colTime", DataPropertyName = "Time", HeaderText = "Giờ", Width = (int)(70 * s)
            };
            var colPartner = new DataGridViewTextBoxColumn
            {
                Name = "colPartner", DataPropertyName = "Partner",
                HeaderText = buy ? "Nhà cung cấp" : "Khách hàng", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };
            var colPrice = new DataGridViewTextBoxColumn
            {
                Name = "colPrice", DataPropertyName = "Price", HeaderText = "Đơn giá",
                Width = (int)(110 * s),
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "#,##0.##" }
            };
            _grid.Columns.AddRange(colDate, colTime, colPartner, colPrice);
            Body.Controls.Add(_grid);

            var repo = new ProductRepository(databasePath);
            List<ProductHistoryRow> rows = buy ? repo.LoadBuyHistory(productId) : repo.LoadSellHistory(productId);
            foreach (ProductHistoryRow r in rows)
            {
                DataRow row = _table.NewRow();
                row["Date"] = r.Date.HasValue ? (object)r.Date.Value : DBNull.Value;
                row["Time"] = r.Time ?? "";
                row["Partner"] = r.PartnerName ?? "";
                row["PartnerFold"] = CatalogRepository.Fold(r.PartnerName);
                row["Price"] = r.Price;
                _table.Rows.Add(row);
            }

            Shown += delegate { _filter.Focus(); };
        }

        private void ApplyFilter()
        {
            string q = CatalogRepository.Fold(_filter.Text.Trim());
            if (string.IsNullOrEmpty(q))
            {
                _view.RowFilter = "";
                return;
            }
            _view.RowFilter = "[PartnerFold] LIKE '%" + CatalogRepository.EscapeLike(q) + "%'";
        }
    }
}
