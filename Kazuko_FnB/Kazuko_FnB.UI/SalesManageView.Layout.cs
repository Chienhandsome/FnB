using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    internal sealed partial class SalesManageView
    {
        private void BuildLeftPane(float s)
        {
            _leftTopBar.Padding = new Padding((int)(6 * s), (int)(6 * s), (int)(6 * s), (int)(6 * s));

            int ctrlH = (int)(32 * s);
            int btnH = Math.Max((int)(36 * s), 36);

            _dtFrom.Inner.Font = _bodyFont;
            _dtFrom.Size = new Size((int)(124 * s), ctrlH);
            _dtFrom.Margin = new Padding((int)(2 * s), (int)(4 * s), (int)(8 * s), (int)(4 * s));

            _dtTo.Inner.Font = _bodyFont;
            _dtTo.Size = new Size((int)(124 * s), ctrlH);
            _dtTo.Margin = new Padding((int)(2 * s), (int)(4 * s), (int)(8 * s), (int)(4 * s));

            _txtFilter.Inner.Font = _bodyFont;
            _txtFilter.Placeholder = "Lọc số phiếu, khách, bàn...";
            _txtFilter.Size = new Size((int)(180 * s), ctrlH);
            _txtFilter.Margin = new Padding((int)(2 * s), (int)(4 * s), (int)(8 * s), (int)(4 * s));

            _btnReload.Font = _bodyFont;
            _btnReload.Size = new Size((int)(96 * s), btnH);
            _btnReload.Margin = new Padding((int)(2 * s), (int)(2 * s), (int)(6 * s), (int)(2 * s));

            _btnCancelBill.Font = _bodyFont;
            _btnCancelBill.Size = new Size((int)(96 * s), btnH);
            _btnCancelBill.Margin = new Padding((int)(2 * s), (int)(2 * s), 0, (int)(2 * s));

            _leftTopBar.Controls.Add(MakeFlowLabel("Từ", s));
            _leftTopBar.Controls.Add(_dtFrom);
            _leftTopBar.Controls.Add(MakeFlowLabel("Đến", s));
            _leftTopBar.Controls.Add(_dtTo);
            _leftTopBar.Controls.Add(MakeFlowLabel("Lọc", s));
            _leftTopBar.Controls.Add(_txtFilter);
            _leftTopBar.Controls.Add(_btnReload);
            _leftTopBar.Controls.Add(_btnCancelBill);

            BuildBillListGrid(s);
            _leftEmpty.Font = _bodyFont;
            _leftEmpty.Height = (int)(32 * s);

            _leftPane.Controls.Add(_billGrid);
            _leftPane.Controls.Add(_leftEmpty);
            _leftPane.Controls.Add(_leftTopBar);
            _billGrid.BringToFront();
        }

        private void BuildBillListGrid(float s)
        {
            _billGrid.ReadOnly = true;
            _billGrid.AllowUserToAddRows = false;
            _billGrid.AllowUserToDeleteRows = false;
            _billGrid.AllowUserToResizeRows = false;
            _billGrid.AllowUserToOrderColumns = true;
            _billGrid.MultiSelect = false;
            _billGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _billGrid.RowHeadersVisible = false;
            _billGrid.AutoGenerateColumns = false;
            _billGrid.ScrollBars = ScrollBars.Both;
            _billGrid.Font = _bodyFont;
            _billGrid.ColumnHeadersDefaultCellStyle.Font = _headingFont;
            _billGrid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _billGrid.ColumnHeadersHeight = (int)(34 * s);
            _billGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _billGrid.RowTemplate.Height = Math.Max((int)(34 * s), 34);
            _billGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };

            AddBillCol("colBillNo", "Số phiếu", (int)(108 * s), DataGridViewContentAlignment.MiddleLeft, false);
            AddBillCol("colBillDate", "Ngày", (int)(92 * s), DataGridViewContentAlignment.MiddleCenter, false);
            AddBillCol("colCustCode", "Mã khách", (int)(86 * s), DataGridViewContentAlignment.MiddleLeft, false);
            AddBillCol("colCustName", "Khách hàng", (int)(135 * s), DataGridViewContentAlignment.MiddleLeft, false);
            AddBillCol("colTongCong", "Tổng cộng", (int)(102 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTienGiam", "Tiền giảm giá", (int)(98 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTileGiam", "Tỉ lệ giảm giá", (int)(92 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTienThue", "Tiền thuế", (int)(88 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTileThue", "Tỉ lệ thuế", (int)(82 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTienHang", "Tiền hàng", (int)(98 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colGioTT", "Giờ thanh toán", (int)(112 * s), DataGridViewContentAlignment.MiddleCenter, false);
            AddBillCol("colKhachDua", "Khách đưa", (int)(96 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTraLai", "Trả lại", (int)(86 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colVoucher", "Voucher", (int)(84 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTheTT", "Thẻ tt", (int)(84 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTruTichLuy", "Trừ tích lũy", (int)(92 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTienMat", "Tiền mặt", (int)(96 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colChuyenKhoan", "Chuyển khoản", (int)(102 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colThe", "Thẻ ATM", (int)(88 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTable", "Bàn", (int)(96 * s), DataGridViewContentAlignment.MiddleLeft, false);
            AddBillCol("colBatDau", "Bắt đầu", (int)(74 * s), DataGridViewContentAlignment.MiddleCenter, false);
            AddBillCol("colKetThuc", "Kết thúc", (int)(74 * s), DataGridViewContentAlignment.MiddleCenter, false);
            AddBillCol("colTienGio", "Tiền giờ", (int)(82 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTileGiamGio", "TL giảm giờ", (int)(86 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTienGiamGio", "Tiền giảm giờ", (int)(96 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colGuests", "Số khách", (int)(76 * s), DataGridViewContentAlignment.MiddleRight, false);
            AddBillCol("colPhiDV", "Phí dịch vụ", (int)(88 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colTilePhiDV", "TL phí DV", (int)(82 * s), DataGridViewContentAlignment.MiddleRight, true);
            AddBillCol("colOrderNo", "Số order", (int)(96 * s), DataGridViewContentAlignment.MiddleLeft, false);
        }

        private void AddBillCol(string name, string header, int width, DataGridViewContentAlignment align, bool isMoney)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Visible = !isMoney || _canViewPrice
            };
            col.DefaultCellStyle.Alignment = align;
            if (align == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            else if (align == DataGridViewContentAlignment.MiddleCenter)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _billGrid.Columns.Add(col);
        }

        private void BuildRightPane(float s)
        {
            _groupBanner.Height = (int)(32 * s);
            _groupBanner.Padding = new Padding((int)(10 * s), (int)(3 * s), (int)(10 * s), (int)(3 * s));
            _lblGroupBanner.Font = _headingFont;
            _groupBanner.Controls.Add(_lblGroupBanner);

            BuildRightHeader(s);
            BuildLineToolbar(s);
            BuildDetailGrid(s);
            BuildActionColumn(s);
            BuildBottomPanel(s);

            var centerRight = new Panel { Dock = DockStyle.Fill };
            var gridHost = new Panel { Dock = DockStyle.Fill };
            gridHost.Controls.Add(_detailGrid);
            gridHost.Controls.Add(_lineBar);

            _menuPicker.Width = (int)(250 * s);
            _menuPicker.SetCanViewPrice(_canViewPrice);

            centerRight.Controls.Add(gridHost);
            centerRight.Controls.Add(_actionCol);
            centerRight.Controls.Add(_menuPicker);

            _rightPane.Controls.Add(centerRight);
            _rightPane.Controls.Add(_headerPanel);
            _rightPane.Controls.Add(_groupBanner);
            _rightPane.Controls.Add(_bottomPanel);
        }

        private void BuildRightHeader(float s)
        {
            _headerPanel.Height = (int)(94 * s);
            _headerPanel.Padding = new Padding((int)(6 * s), (int)(4 * s), (int)(6 * s), (int)(4 * s));

            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2 };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66 * s));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 42 * s));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 42 * s));

            StyleHeaderBox(_txtStartTime);
            StyleHeaderBox(_dtBillDate);
            _dtBillDate.Inner.Font = _bodyFont;
            StyleHeaderBox(_txtBillNo);
            StyleHeaderBox(_numGuests);
            StyleHeaderBox(_cboCustomer);
            StyleHeaderBox(_txtBillNote);

            tbl.Controls.Add(MakeLabel("Giờ vào"), 0, 0);
            tbl.Controls.Add(_txtStartTime, 1, 0);
            tbl.Controls.Add(MakeLabel("Ngày"), 2, 0);
            tbl.Controls.Add(_dtBillDate, 3, 0);
            tbl.Controls.Add(MakeLabel("Số phiếu"), 4, 0);
            tbl.Controls.Add(_txtBillNo, 5, 0);
            tbl.Controls.Add(MakeLabel("Số khách"), 6, 0);
            tbl.Controls.Add(_numGuests, 7, 0);

            tbl.Controls.Add(MakeLabel("Khách (F12)"), 0, 1);
            tbl.Controls.Add(_cboCustomer, 1, 1);
            tbl.SetColumnSpan(_cboCustomer, 3);
            tbl.Controls.Add(MakeLabel("Ghi chú"), 4, 1);
            tbl.Controls.Add(_txtBillNote, 5, 1);
            tbl.SetColumnSpan(_txtBillNote, 3);

            _headerPanel.Controls.Add(tbl);
        }

        private void BuildLineToolbar(float s)
        {
            _lineBar.Padding = new Padding((int)(4 * s), (int)(4 * s), (int)(4 * s), (int)(4 * s));
            int h = Math.Max((int)(44 * s), 44);
            SetupBarButton(_btnPlus, (int)(46 * s), h, s);
            SetupBarButton(_btnMinus, (int)(46 * s), h, s);
            SetupBarButton(_btnSetQty, (int)(72 * s), h, s);
            SetupBarButton(_btnPrice, (int)(76 * s), h, s);
            SetupBarButton(_btnNote, (int)(76 * s), h, s);
            SetupBarButton(_btnLineDiscount, (int)(60 * s), h, s);
            _lineBar.Controls.AddRange(new Control[] { _btnPlus, _btnMinus, _btnSetQty, _btnPrice, _btnNote, _btnLineDiscount });
        }

        private void BuildDetailGrid(float s)
        {
            _detailGrid.ReadOnly = true;
            _detailGrid.AllowUserToAddRows = false;
            _detailGrid.AllowUserToDeleteRows = false;
            _detailGrid.AllowUserToResizeRows = false;
            _detailGrid.MultiSelect = false;
            _detailGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _detailGrid.RowHeadersVisible = false;
            _detailGrid.AutoGenerateColumns = false;
            _detailGrid.Font = _bodyFont;
            _detailGrid.RowTemplate.Height = Math.Max((int)(36 * s), 34);
            _detailGrid.ColumnHeadersHeight = (int)(34 * s);
            _detailGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _detailGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                DetailDataErrorCountForTest++;
                e.ThrowException = false;
            };

            AddDetailCol("colItemName", "Tên hàng", (int)(150 * s), DataGridViewContentAlignment.MiddleLeft, true, false);
            AddDetailCol("colUnit", "ĐVT", (int)(58 * s), DataGridViewContentAlignment.MiddleCenter, false, false);
            AddDetailCol("colQty", "SL", (int)(54 * s), DataGridViewContentAlignment.MiddleRight, false, false);
            AddDetailCol("colPrice", "Đ giá", (int)(82 * s), DataGridViewContentAlignment.MiddleRight, false, true);
            AddDetailCol("colDisc", "CK%", (int)(52 * s), DataGridViewContentAlignment.MiddleRight, false, true);
            AddDetailCol("colAmount", "T tiền", (int)(92 * s), DataGridViewContentAlignment.MiddleRight, false, true);
            AddDetailCol("colNote", "Ghi chú", (int)(96 * s), DataGridViewContentAlignment.MiddleLeft, false, false);
        }

        private void AddDetailCol(string name, string header, int width, DataGridViewContentAlignment align, bool fill, bool isMoney)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Visible = !isMoney || _canViewPrice
            };
            col.DefaultCellStyle.Alignment = align;
            if (fill)
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _detailGrid.Columns.Add(col);
        }

        private void BuildActionColumn(float s)
        {
            int colW = (int)(112 * s);
            int btnH = Math.Max((int)(44 * s), 44);
            _actionCol.Width = colW;
            _actionCol.Padding = new Padding((int)(4 * s));

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            int innerW = colW - (int)(12 * s);
            _lblAddQty.Size = new Size(innerW, (int)(22 * s));
            _lblAddQty.Font = _bodyFont;
            _numAddQty.Size = new Size(innerW, (int)(32 * s));
            _numAddQty.Font = _bodyFont;

            flow.Controls.Add(_lblAddQty);
            flow.Controls.Add(_numAddQty);
            foreach (FlatButton b in new[] { _btnAdd, _btnReduce, _btnDelete, _btnGroupDiscount, _btnReturn })
            {
                b.Size = new Size(innerW, btnH);
                b.Font = _bodyFont;
                b.Margin = new Padding(0, (int)(3 * s), 0, (int)(3 * s));
                flow.Controls.Add(b);
            }
            _actionCol.Controls.Add(flow);
        }

        private void BuildBottomPanel(float s)
        {
            int btnH = Math.Max((int)(44 * s), 44);
            _bottomPanel.Height = Math.Max((int)(136 * s), btnH * 2 + (int)(28 * s));
            _bottomPanel.Padding = new Padding((int)(6 * s), (int)(4 * s), (int)(6 * s), (int)(4 * s));

            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82 * s));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82 * s));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58 * s));
            _totalsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            for (int r = 0; r < 3; r++)
                _totalsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

            StyleTotalBox(_numTienHang, false);
            StyleTotalBox(_numTileGiam, false);
            StyleTotalBox(_numTienGiam, false);
            StyleTotalBox(_numTilePhi, false);
            StyleTotalBox(_numPhiDichVu, false);
            StyleTotalBox(_numTileThue, false);
            StyleTotalBox(_numTienThue, false);
            StyleTotalBox(_numTongCong, true);

            _totalsGrid.Controls.Add(MakeLabel("Tiền hàng"), 0, 0);
            _totalsGrid.Controls.Add(_numTienHang, 1, 0);
            _totalsGrid.Controls.Add(MakeLabel("Giảm giá (%)"), 2, 0);
            _totalsGrid.Controls.Add(_numTileGiam, 3, 0);
            _totalsGrid.Controls.Add(MakeLabel("Tiền"), 4, 0);
            _totalsGrid.Controls.Add(_numTienGiam, 5, 0);

            _totalsGrid.Controls.Add(MakeLabel("Phí DV (%)"), 0, 1);
            _totalsGrid.Controls.Add(_numTilePhi, 1, 1);
            _totalsGrid.Controls.Add(MakeLabel("Tiền phí"), 2, 1);
            _totalsGrid.Controls.Add(_numPhiDichVu, 3, 1);
            _totalsGrid.Controls.Add(MakeLabel("Thuế (%)"), 4, 1);
            var taxHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            taxHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            taxHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            taxHost.Controls.Add(_numTileThue, 0, 0);
            taxHost.Controls.Add(_numTienThue, 1, 0);
            _totalsGrid.Controls.Add(taxHost, 5, 1);

            var lblTotal = MakeLabel("TỔNG CỘNG");
            lblTotal.Font = _headingFont;
            _totalsGrid.Controls.Add(lblTotal, 2, 2);
            _totalsGrid.Controls.Add(_numTongCong, 3, 2);
            _totalsGrid.SetColumnSpan(_numTongCong, 3);
            _totalsGrid.Visible = _canViewPrice;

            int btnW = (int)(164 * s);
            _bottomButtons.Width = btnW + (int)(14 * s);
            _bottomButtons.Padding = new Padding((int)(6 * s), (int)(4 * s), 0, 0);
            _btnUpdate.Size = new Size(btnW, btnH);
            _btnUpdate.Font = _headingFont;
            _btnUpdate.Margin = new Padding(0, (int)(2 * s), 0, (int)(6 * s));
            _btnReprint.Size = new Size(btnW, btnH);
            _btnReprint.Font = _bodyFont;
            _btnReprint.Margin = new Padding(0, (int)(2 * s), 0, 0);
            _bottomButtons.Controls.Add(_btnUpdate);
            _bottomButtons.Controls.Add(_btnReprint);

            _bottomPanel.Controls.Add(_totalsGrid);
            _bottomPanel.Controls.Add(_bottomButtons);
        }

        private Label MakeFlowLabel(string text, float s)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(TextRenderer.MeasureText(text, _bodyFont).Width + (int)(8 * s), (int)(32 * s)),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = _bodyFont,
                Margin = new Padding((int)(4 * s), (int)(4 * s), 0, (int)(2 * s))
            };
        }

        private Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = _bodyFont,
                Padding = new Padding(0, 0, 4, 0)
            };
        }

        private void SetupBarButton(FlatButton btn, int w, int h, float s)
        {
            btn.Size = new Size(w, h);
            btn.Font = _bodyFont;
            btn.Margin = new Padding(0, 0, (int)(4 * s), 0);
        }

        private void StyleHeaderBox(Control c)
        {
            c.Dock = DockStyle.Fill;
            c.Font = _bodyFont;
            c.Margin = new Padding(2, 5, 4, 4);
        }

        private void StyleTotalBox(NumberBox box, bool bold)
        {
            box.Dock = DockStyle.Fill;
            box.Font = bold ? _headingFont : _bodyFont;
            box.Margin = new Padding(2, 4, 4, 4);
            box.TextAlign = HorizontalAlignment.Right;
        }
    }
}
