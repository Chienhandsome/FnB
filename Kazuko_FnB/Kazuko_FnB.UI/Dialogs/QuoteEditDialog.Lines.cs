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

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần lưới chọn mặt hàng, lưới chi tiết, thêm/sửa/xóa dòng và nhập Excel của QuoteEditDialog.</summary>
    internal sealed partial class QuoteEditDialog
    {
        private void BuildBottom()
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            Body.Controls.Add(split);
            bool placed = false;
            split.SizeChanged += delegate
            {
                if (!placed && split.Width > (int)(760 * s))
                {
                    split.SplitterDistance = (int)(500 * s);
                    placed = true;
                }
            };

            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = (int)(40 * s),
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(0, (int)(2 * s), 0, (int)(2 * s))
            };
            int lblSearchW = MeasureMaxLabelWidth(new[] { "Tìm (F3)" }, bodyFont, s);
            int lblQtyW = MeasureMaxLabelWidth(new[] { "Số lượng" }, bodyFont, s);
            int qtyBoxW = TextRenderer.MeasureText("9999.99", bodyFont).Width + SystemInformation.VerticalScrollBarWidth + (int)Math.Ceiling(18 * s);
            int btnAddW = TextRenderer.MeasureText(_btnAddProduct.Text, bodyFont).Width + (int)Math.Ceiling(26 * s);
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblSearchW));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lblQtyW));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, qtyBoxW));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, btnAddW + (int)(6 * s)));
            searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblSearch = FieldLabel("Tìm (F3)", bodyFont);
            _txtSearch.Font = bodyFont;
            _txtSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _txtSearch.TextChanged += delegate { RebuildProductGrid(); };

            var lblQty = new Label { Text = "Số lượng", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = bodyFont };
            _numQty.DecimalPlaces = 2;
            _numQty.Minimum = 0m;
            _numQty.Maximum = 1000000m;
            _numQty.Value = 1m;
            _numQty.Font = bodyFont;
            _numQty.TextAlign = HorizontalAlignment.Right;
            _numQty.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _numQty.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddProduct();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            _btnAddProduct.Font = bodyFont;
            _btnAddProduct.Size = new Size(btnAddW, (int)(34 * s));
            _btnAddProduct.Margin = new Padding((int)(6 * s), 0, 0, 0);
            _btnAddProduct.Anchor = AnchorStyles.Right;
            _btnAddProduct.Click += delegate { AddProduct(); };

            searchRow.Controls.Add(lblSearch, 0, 0);
            searchRow.Controls.Add(_txtSearch, 1, 0);
            searchRow.Controls.Add(lblQty, 2, 0);
            searchRow.Controls.Add(_numQty, 3, 0);
            searchRow.Controls.Add(_btnAddProduct, 4, 0);

            SetupGrid(_productGrid, true, s);
            AddTextColumn(_productGrid, "p_code", "Mã hàng", 80, "MH0001", true, false, s);
            AddTextColumn(_productGrid, "p_name", "Tên hàng", 160, "Bia lon Sài Gòn", true, true, s);
            AddTextColumn(_productGrid, "p_unit", "ĐVT", 62, "Thùng", true, false, s);
            AddMoneyColumn(_productGrid, "p_price", "Giá bán", 86, "999.999.999", "N0", s);
            AddTextColumn(_productGrid, "p_bigunit", "ĐVT chẵn", 74, "Thùng", true, false, s);
            AddMoneyColumn(_productGrid, "p_bigprice", "Giá bán chẵn", 98, "999.999.999", "N0", s);
            AddTextColumn(_productGrid, "p_warranty", "Bảo hành", 76, "12 tháng", true, false, s);
            _productGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                    AddProduct();
            };
            split.Panel1.Controls.Add(_productGrid);
            split.Panel1.Controls.Add(searchRow);

            var lineRow = new Panel { Dock = DockStyle.Top, Height = (int)(40 * s) };
            var lineFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, (int)(3 * s), 0, 0)
            };
            foreach (FlatButton b in new[] { _btnDeleteLine, _btnImportExcel, _btnExportExcel })
            {
                b.Font = bodyFont;
                b.Height = (int)(34 * s);
                b.Width = TextRenderer.MeasureText(b.Text, bodyFont).Width + (int)(22 * s);
                b.Margin = new Padding(0, 0, (int)(6 * s), 0);
                lineFlow.Controls.Add(b);
            }
            _btnDeleteLine.Click += delegate { DeleteLine(); };
            _btnImportExcel.Click += delegate { ImportExcel(); };
            _btnExportExcel.Click += delegate { ShowInfo(QuoteText.Unsupported); };
            lineRow.Controls.Add(lineFlow);

            SetupGrid(_detailGrid, false, s);
            AddTextColumn(_detailGrid, "d_code", "Mã hàng", 80, "MH0001", true, false, s);
            AddTextColumn(_detailGrid, "d_name", "Mặt hàng", 160, "Bia lon Sài Gòn", true, true, s);
            AddTextColumn(_detailGrid, "d_unit", "ĐVT", 64, "Thùng", true, false, s);
            AddMoneyColumn(_detailGrid, "d_qty", "Số lượng", 80, "999,99", "#,##0.##", s);
            AddMoneyColumn(_detailGrid, "d_price", "Đơn giá", 92, "999.999.999", "N0", s);
            AddMoneyColumn(_detailGrid, "d_disc", "% giảm", 74, "100,00", "#,##0.##", s);
            AddMoneyColumn(_detailGrid, "d_amount", "Thành tiền", 104, "999.999.999", "N0", s);
            _detailGrid.Columns["d_amount"].ReadOnly = true;
            AddTextColumn(_detailGrid, "d_note", "Ghi chú", 120, "Ghi chú dòng", false, false, s);
            _detailGrid.CellEndEdit += DetailCellEndEdit;
            _detailGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
                if (e.RowIndex >= 0 && e.RowIndex < _lines.Count)
                    SyncDetailRow(e.RowIndex);
            };
            split.Panel2.Controls.Add(_detailGrid);
            split.Panel2.Controls.Add(lineRow);
        }

        private void LoadLookups()
        {
            _useTwoUnits = SystemConfig.SuDung2DonViTinh;
            _allowMultipleRowsPerItem = SystemConfig.NhapMotMatHangNhieuLanTrongPhieu;
            _sortByCode = SystemConfig.SapXepThuTuTheo == 0;
            _roundStep = SystemConfig.LamTronTien;

            var customers = new List<LookupItem> { new LookupItem { Id = "", Name = "" } };
            try
            {
                customers.AddRange(_formRepo.LoadAll("DKHACHHANG"));
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được danh sách khách hàng: " + ex.Message);
            }
            _customer.SetItems(customers);

            try
            {
                _saleItems = _service.LoadSaleItems(_sortByCode);
                _units = _service.LoadUnits();
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được danh sách mặt hàng: " + ex.Message);
            }
            RebuildProductGrid();
        }

        private void NewRecord()
        {
            _row = QuoteRow.NewDefault();
            try
            {
                _row.Name = _service.GenerateNumber(_row.Ngay);
            }
            catch (DataAccessException)
            {
                _row.Name = "";
            }
            _isNew = true;
            _readOnly = !_service.CanAdd;
            _lines = new List<QuoteLine>();
            _discountByMoney = false;
            _totals = new QuoteTotals();
            _totalsDirty = false;
            FillControls();
            SetTitle("BÁO GIÁ - THÊM MỚI");
        }

        private void LoadRecord(string quoteId)
        {
            QuoteRow row = null;
            try
            {
                row = _service.LoadQuote(quoteId);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không đọc được phiếu báo giá: " + ex.Message);
            }
            if (row == null)
            {
                _loadFailed = true;
                _row = QuoteRow.NewDefault();
                _totals = new QuoteTotals();
                return;
            }
            _row = row;
            _isNew = false;
            _readOnly = !_service.CanEdit;
            _lines = row.Lines ?? new List<QuoteLine>();
            _discountByMoney = row.GiamTheoTien;
            _totals = new QuoteTotals
            {
                TienHang = row.TienHang,
                TileGiamGia = row.TileGiamGia,
                TienGiamGia = row.TienGiamGia,
                TienThue = row.TienThue,
                TongCong = row.TongCong
            };
            _totalsDirty = false;
            FillControls();
            SetTitle("BÁO GIÁ - SỬA");
        }

        private void FillControls()
        {
            _loading = true;
            try
            {
                _dtNgay.Value = _row.Ngay == DateTime.MinValue ? DateTime.Today : _row.Ngay;
                _txtNumber.Text = _row.Name ?? "";
                if (string.IsNullOrEmpty(_row.CustomerId))
                    _customer.SelectedIndex = _customer.Items.Count > 0 ? 0 : -1;
                else
                    _customer.SelectId(_row.CustomerId);
                _txtTenKhach.Text = _row.TenKhach ?? "";
                _txtDiaChi.Text = _row.DiaChi ?? "";
                _txtDienThoai.Text = _row.DienThoai ?? "";
                _txtEmail.Text = _row.Email ?? "";
                _txtNote.Text = _row.Note ?? "";
                ConfigureDiscountPercentBounds(_discountByMoney);
                SetNumericDisplay(_numTienHang, _totals.TienHang);
                SetNumericDisplay(_numTileGiamGia, _totals.TileGiamGia);
                SetNumericDisplay(_numTienGiamGia, _totals.TienGiamGia);
                SetNumericDisplay(_numTileThue, _row.TileThue);
                SetNumericDisplay(_numTienThue, _totals.TienThue);
                SetNumericDisplay(_numPhiVanChuyen, _row.PhiVanChuyen);
                SetNumericDisplay(_numTongCong, _totals.TongCong);
                RebuildDetailGrid();
            }
            finally
            {
                _loading = false;
            }
        }

        private void RebuildProductGrid()
        {
            string f = (_txtSearch.Text ?? "").Trim();
            CompareInfo vi = CultureInfo.GetCultureInfo("vi-VN").CompareInfo;
            _productGrid.Rows.Clear();
            foreach (QuoteSaleItem item in _saleItems)
            {
                if (f.Length > 0
                    && vi.IndexOf(item.Name ?? "", f, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0
                    && vi.IndexOf(item.Code ?? "", f, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0)
                    continue;
                int i = _productGrid.Rows.Add(item.Code, item.Name, item.UnitName, item.SellPrice,
                    item.BigUnitName, item.BigSellPrice, item.Warranty);
                _productGrid.Rows[i].Tag = item;
            }
        }

        private void RebuildDetailGrid()
        {
            _detailGrid.Rows.Clear();
            foreach (QuoteLine line in _lines)
            {
                int i = _detailGrid.Rows.Add(line.ProductCode, line.ProductName, line.UnitName,
                    line.Quantity, line.Price, line.DiscountPct, line.Amount, line.Note);
                _detailGrid.Rows[i].Tag = line;
            }
        }

        private void SyncDetailRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _lines.Count || rowIndex >= _detailGrid.Rows.Count)
                return;
            QuoteLine line = _lines[rowIndex];
            DataGridViewRow r = _detailGrid.Rows[rowIndex];
            r.Cells["d_qty"].Value = line.Quantity;
            r.Cells["d_price"].Value = line.Price;
            r.Cells["d_disc"].Value = line.DiscountPct;
            r.Cells["d_amount"].Value = line.Amount;
            r.Cells["d_note"].Value = line.Note ?? "";
        }

        private void AddProduct()
        {
            if (_readOnly || _productGrid.CurrentRow == null)
                return;
            var item = _productGrid.CurrentRow.Tag as QuoteSaleItem;
            if (item == null)
                return;

            decimal outerQty = _numQty.Value;
            QuoteAddItemOutcome outcome;
            if (_useTwoUnits && item.HasBigUnit)
            {
                QuoteQuantityPromptResult prompt = PromptTwoUnitQuantity(item, outerQty);
                outcome = QuoteItemRules.AddTwoUnitItem(
                    _lines, item, outerQty, prompt.OddQuantity, prompt.EvenQuantity,
                    prompt.Confirmed, _allowMultipleRowsPerItem);
            }
            else
            {
                outcome = QuoteItemRules.AddSingleUnitItem(_lines, item, outerQty);
            }

            if (!string.IsNullOrEmpty(outcome.WarningMessage))
            {
                ShowWarning(outcome.WarningMessage);
                _numQty.Focus();
            }
            if (outcome.Changed)
            {
                RebuildDetailGrid();
                RecalcTotals();
                OnDirtyChanged();
            }
        }

        private QuoteQuantityPromptResult PromptTwoUnitQuantity(QuoteSaleItem item, decimal outerQty)
        {
            if (QuantityPromptOverrideForTest != null)
                return QuantityPromptOverrideForTest(item, outerQty);
            using (var dlg = new QuoteQuantityDialog(item, outerQty))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return new QuoteQuantityPromptResult { Confirmed = false };
                return new QuoteQuantityPromptResult
                {
                    Confirmed = true,
                    OddQuantity = dlg.OddQuantity,
                    EvenQuantity = dlg.EvenQuantity
                };
            }
        }

        private void DetailCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _lines.Count)
                return;
            QuoteLine line = _lines[e.RowIndex];
            DataGridViewRow r = _detailGrid.Rows[e.RowIndex];

            decimal parsedQty;
            bool qtyParsed = TryReadCellDecimal(r.Cells["d_qty"].Value, out parsedQty);
            decimal effectiveQty;
            string qtyWarning;
            if (!qtyParsed || !QuoteItemRules.ValidateEditedQuantity(parsedQty, line.Quantity, out effectiveQty, out qtyWarning))
            {
                r.Cells["d_qty"].Value = line.Quantity;
                ShowWarning(QuoteText.EnterQuantity);
                return;
            }

            decimal price;
            if (!TryReadCellDecimal(r.Cells["d_price"].Value, out price) || price < 0m)
                price = line.Price;
            decimal disc;
            if (!TryReadCellDecimal(r.Cells["d_disc"].Value, out disc))
                disc = line.DiscountPct;
            if (disc < 0m) disc = 0m;
            if (disc > 100m) disc = 100m;

            line.Quantity = effectiveQty;
            line.Price = price;
            line.DiscountPct = disc;
            line.Amount = QuoteCalculator.CalcLineAmount(line.Quantity, line.Price, line.DiscountPct);
            line.Note = Convert.ToString(r.Cells["d_note"].Value) ?? "";

            SyncDetailRow(e.RowIndex);
            RecalcTotals();
            OnDirtyChanged();
        }

        private static bool TryReadCellDecimal(object v, out decimal value)
        {
            value = 0m;
            if (v == null || v is DBNull)
                return false;
            if (v is decimal)
            {
                value = (decimal)v;
                return true;
            }
            string s = (Convert.ToString(v) ?? "").Trim();
            const NumberStyles plain = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
            if (decimal.TryParse(s, plain, CultureInfo.InvariantCulture, out value))
                return true;
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out value))
                return true;
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        internal void CommitDetailCellEditForTest(int rowIndex, string columnName, object newValue)
        {
            if (rowIndex < 0 || rowIndex >= _detailGrid.Rows.Count)
                return;
            DataGridViewColumn col = _detailGrid.Columns[columnName];
            if (col == null)
                return;
            _detailGrid.Rows[rowIndex].Cells[columnName].Value = newValue;
            DetailCellEndEdit(_detailGrid, new DataGridViewCellEventArgs(col.Index, rowIndex));
        }

        private void DeleteLine()
        {
            if (_readOnly || _detailGrid.CurrentRow == null)
                return;
            int idx = _detailGrid.CurrentRow.Index;
            if (idx < 0 || idx >= _lines.Count)
                return;
            _lines.RemoveAt(idx);
            RebuildDetailGrid();
            RecalcTotals();
            OnDirtyChanged();
        }

        private void ImportExcel()
        {
            if (_readOnly)
                return;
            using (var dlg = new QuoteExcelImportDialog(_saleItems, _units, SystemConfig.SuDungMaHang, _useTwoUnits))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                ApplyExcelImportResult(dlg.Result);
            }
        }

        internal void ApplyExcelImportResult(QuoteExcelImportResult importResult)
        {
            if (importResult == null || !importResult.Ok)
                return;
            if (importResult.Lines != null && importResult.Lines.Count > 0)
            {
                _lines.AddRange(importResult.Lines);
                RebuildDetailGrid();
                RecalcTotals();
                OnDirtyChanged();
            }
            if (importResult.SkippedNegativeCount > 0)
                ShowInfo(QuoteText.SkippedNegativeNotice(importResult.SkippedNegativeCount));
        }
    }
}
