using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần đổ dữ liệu, bố cục chiều cao lưới (không cuộn dọc lồng lưới), cuộn dọc và áp theme của <see cref="DailyActivitySections"/>.
    /// </summary>
    internal sealed partial class DailyActivitySections
    {
        /// <summary>Đổ dữ liệu và kết quả tổng đã tải vào đầu trang, 4 khối và khối tổng hợp 8 dòng.</summary>
        public void BindData(
            DailyActivityReportData data,
            DailyActivitySummaryResult summary,
            DateTime fromDate,
            DateTime toDate,
            string cashierDisplay,
            string storeDisplay)
        {
            DailyCompanyInfo comp = data.Company ?? new DailyCompanyInfo();
            _lblCompanyName.Text = comp.Name ?? "";
            var detailParts = new List<string>(3);
            if (!string.IsNullOrWhiteSpace(comp.Address)) detailParts.Add("Địa chỉ: " + comp.Address.Trim());
            if (!string.IsNullOrWhiteSpace(comp.Phone)) detailParts.Add("ĐT: " + comp.Phone.Trim());
            if (!string.IsNullOrWhiteSpace(comp.Email)) detailParts.Add("Email: " + comp.Email.Trim());
            _lblCompanyDetails.Text = string.Join("   |   ", detailParts.ToArray());

            _lblFilterSummary.Text =
                "Từ ngày " + fromDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +
                "   Đến ngày " + toDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) +
                "   |   Thu ngân: " + (string.IsNullOrWhiteSpace(cashierDisplay) ? DailyActivityConstants.AllLabel : cashierDisplay) +
                "   |   Cửa hàng: " + (string.IsNullOrWhiteSpace(storeDisplay) ? DailyActivityConstants.AllLabel : storeDisplay);

            BindSales(data.SaleBills, summary.SaleTotals);
            BindItems(data.SoldItems, summary.ItemTotals);
            BindPurchases(data.Purchases, summary.PurchaseTotals);
            BindCashFlows(data.CashFlows, summary.CashFlowTotals);
            BindOverall(summary.Overall);

            ApplySummaryRowStyles(_salesGrid);
            ApplySummaryRowStyles(_itemsGrid);
            ApplySummaryRowStyles(_purchasesGrid);
            ApplySummaryRowStyles(_cashFlowGrid);

            LayoutContent();
        }

        private void BindSales(List<DailySaleBillRow> bills, DailySaleBillTotals totals)
        {
            _salesGrid.Rows.Clear();
            for (int i = 0; i < bills.Count; i++)
            {
                DailySaleBillRow b = bills[i];
                int idx = _salesGrid.Rows.Add(
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    b.Name ?? "",
                    (object)b.PaidTime,
                    b.CustomerName ?? "",
                    b.TongCong,
                    b.TienMat,
                    b.ChuyenKhoan,
                    b.The,
                    b.Voucher,
                    b.TruTichLuy,
                    b.TheTraTruoc,
                    b.CongNo);
                _salesGrid.Rows[idx].Tag = b;
            }
            int totIdx = _salesGrid.Rows.Add(
                "", DailyActivityConstants.TotalCaption, null, "",
                totals.TongCong, totals.TienMat, totals.ChuyenKhoan, totals.The,
                totals.Voucher, totals.TruTichLuy, totals.TheTraTruoc, totals.CongNo);
            _salesGrid.Rows[totIdx].Tag = GrandTotalRowTag;
            AdjustColumns(_salesGrid, _salesBaseWidths, 360);
            _salesGrid.ClearSelection();
        }

        private void BindItems(List<DailySoldItemRow> items, DailySoldItemTotals totals)
        {
            _itemsGrid.Rows.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                DailySoldItemRow item = items[i];
                int idx = _itemsGrid.Rows.Add(
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    item.ProductCode ?? "",
                    item.ItemName ?? "",
                    item.UnitName ?? "",
                    item.Quantity,
                    item.UnitPrice,
                    item.DiscountRate,
                    item.Amount);
                _itemsGrid.Rows[idx].Tag = item;
            }
            // Đủ 4 dòng tổng §2.5 đúng thứ tự và nhãn; dòng đầu có cả tổng số lượng.
            int r1 = _itemsGrid.Rows.Add("", "", DailyActivityConstants.ItemGrossCaption, "", totals.TotalQuantity, null, null, totals.GrossAmount);
            int r2 = _itemsGrid.Rows.Add("", "", DailyActivityConstants.ItemDiscountCaption, "", null, null, null, totals.ItemDiscount);
            int r3 = _itemsGrid.Rows.Add("", "", DailyActivityConstants.BillDiscountCaption, "", null, null, null, totals.BillDiscount);
            int r4 = _itemsGrid.Rows.Add("", "", DailyActivityConstants.ItemNetTotalCaption, "", null, null, null, totals.NetTotal);
            _itemsGrid.Rows[r1].Tag = SummaryRowTag;
            _itemsGrid.Rows[r2].Tag = SummaryRowTag;
            _itemsGrid.Rows[r3].Tag = SummaryRowTag;
            _itemsGrid.Rows[r4].Tag = GrandTotalRowTag;
            AdjustColumns(_itemsGrid, _itemsBaseWidths, 380);
            _itemsGrid.ClearSelection();
        }

        private void BindPurchases(List<DailyPurchaseRow> purchases, DailyPurchaseTotals totals)
        {
            _purchasesGrid.Rows.Clear();
            _hasPurchases = purchases.Count > 0;
            _purchasesSection.Visible = _hasPurchases;
            if (!_hasPurchases)
                return;

            for (int i = 0; i < purchases.Count; i++)
            {
                DailyPurchaseRow p = purchases[i];
                decimal debt = DailyActivitySummary.CalculatePurchaseRemainingDebt(p.TongCong, p.TienThanhToan);
                int idx = _purchasesGrid.Rows.Add(
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    p.Name ?? "",
                    p.SupplierName ?? "",
                    p.TongCong,
                    p.TienThanhToan,
                    debt);
                _purchasesGrid.Rows[idx].Tag = p;
            }
            int totIdx = _purchasesGrid.Rows.Add(
                "", DailyActivityConstants.TotalCaption, "",
                totals.TongCong, totals.TienThanhToan, totals.RemainingDebt);
            _purchasesGrid.Rows[totIdx].Tag = GrandTotalRowTag;
            AdjustColumns(_purchasesGrid, _purchasesBaseWidths, 360);
            _purchasesGrid.ClearSelection();
        }

        private void BindCashFlows(List<DailyCashFlowRow> cashFlows, DailyCashFlowTotals totals)
        {
            _cashFlowGrid.Rows.Clear();
            for (int i = 0; i < cashFlows.Count; i++)
            {
                DailyCashFlowRow cf = cashFlows[i];
                int idx = _cashFlowGrid.Rows.Add(
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    cf.Name ?? "",
                    cf.Description ?? "",
                    cf.ObjectName ?? "",
                    cf.CashIn,
                    cf.CashOut,
                    cf.TransferIn,
                    cf.TransferOut);
                _cashFlowGrid.Rows[idx].Tag = cf;
            }
            int totIdx = _cashFlowGrid.Rows.Add(
                "", DailyActivityConstants.TotalCaption, "", "",
                totals.CashIn, totals.CashOut, totals.TransferIn, totals.TransferOut);
            _cashFlowGrid.Rows[totIdx].Tag = GrandTotalRowTag;
            AdjustColumns(_cashFlowGrid, _cashFlowBaseWidths, 360);
            _cashFlowGrid.ClearSelection();
        }

        private void BindOverall(DailyActivityOverallSummary overall)
        {
            decimal[] vals = DailyActivitySummary.OverallValues(overall);
            for (int i = 0; i < 8; i++)
                _overallValLabels[i].Text = DailyActivitySummary.FormatOverallValue(i, vals[i]);
        }

        private void AdjustColumns(DataGridView grid, Dictionary<string, int> baseWidths, int maxWidthAt96)
        {
            int pad = (int)Math.Ceiling(22 * _scale);
            int maxW = (int)Math.Ceiling(maxWidthAt96 * _scale);
            foreach (DataGridViewColumn col in grid.Columns)
            {
                int baseW;
                if (!baseWidths.TryGetValue(col.Name, out baseW))
                    continue;
                int target = baseW;
                for (int r = 0; r < grid.Rows.Count; r++)
                {
                    string text = Convert.ToString(grid.Rows[r].Cells[col.Index].FormattedValue);
                    if (string.IsNullOrEmpty(text))
                        continue;
                    bool isSum = grid.Rows[r].Tag is string;
                    int needed = TextRenderer.MeasureText(text, isSum ? _headingFont : _bodyFont).Width + pad;
                    if (needed > target)
                        target = needed;
                }
                if (target > maxW) target = maxW;
                if (col.Width != target) col.Width = target;
            }
        }

        public void LayoutContent()
        {
            if (_inLayout)
                return;
            _inLayout = true;
            try
            {
                int scrollBarW = SystemInformation.VerticalScrollBarWidth;
                int availW = Math.Max(320, _scrollHost.ClientSize.Width);
                if (!_scrollHost.VerticalScroll.Visible && _scrollHost.Width > scrollBarW + 320)
                    availW = _scrollHost.Width - scrollBarW - 2;

                int padX = (int)(14 * _scale);
                int gapY = (int)(14 * _scale);
                int innerW = Math.Max(260, availW - padX * 2);
                int y = (int)(10 * _scale);

                y = LayoutHeader(padX, y, innerW) + gapY;
                y = LayoutGridSection(_salesSection, _salesTitle, _salesGrid, padX, y, innerW) + gapY;
                y = LayoutGridSection(_itemsSection, _itemsTitle, _itemsGrid, padX, y, innerW) + gapY;
                if (_hasPurchases)
                    y = LayoutGridSection(_purchasesSection, _purchasesTitle, _purchasesGrid, padX, y, innerW) + gapY;
                y = LayoutGridSection(_cashFlowSection, _cashFlowTitle, _cashFlowGrid, padX, y, innerW) + gapY;
                y = LayoutOverallSection(padX, y, innerW) + (int)(16 * _scale);

                Point savedScroll = _scrollHost.AutoScrollPosition;
                _page.SetBounds(savedScroll.X, savedScroll.Y, availW, y);
            }
            finally
            {
                _inLayout = false;
            }
        }

        private int LayoutHeader(int padX, int y, int innerW)
        {
            int p = (int)(12 * _scale);
            int curY = (int)(8 * _scale);
            int textW = Math.Max(100, innerW - p * 2);

            bool hasName = !string.IsNullOrWhiteSpace(_lblCompanyName.Text);
            _lblCompanyName.Visible = hasName;
            if (hasName)
            {
                _lblCompanyName.SetBounds(p, curY, textW, (int)(22 * _scale));
                curY += _lblCompanyName.Height + (int)(2 * _scale);
            }

            bool hasDetails = !string.IsNullOrWhiteSpace(_lblCompanyDetails.Text);
            _lblCompanyDetails.Visible = hasDetails;
            if (hasDetails)
            {
                _lblCompanyDetails.SetBounds(p, curY, textW, (int)(20 * _scale));
                curY += _lblCompanyDetails.Height + (int)(6 * _scale);
            }

            _lblReportTitle.SetBounds(p, curY, textW, (int)(30 * _scale));
            curY += _lblReportTitle.Height + (int)(2 * _scale);

            _lblFilterSummary.SetBounds(p, curY, textW, (int)(24 * _scale));
            curY += _lblFilterSummary.Height + (int)(8 * _scale);

            _headerCard.SetBounds(padX, y, innerW, curY);
            return y + curY;
        }

        private int LayoutGridSection(Panel section, Label title, DataGridView grid, int padX, int y, int innerW)
        {
            int titleH = (int)(28 * _scale);
            title.SetBounds(0, 0, innerW, titleH);

            int totalColsW = 0;
            foreach (DataGridViewColumn col in grid.Columns)
                if (col.Visible) totalColsW += col.Width;
            bool needHScroll = totalColsW > (innerW - 4);

            int rowsH = 0;
            for (int r = 0; r < grid.Rows.Count; r++)
                rowsH += grid.Rows[r].Height;

            int gridH = grid.ColumnHeadersHeight + rowsH + 4 + (needHScroll ? SystemInformation.HorizontalScrollBarHeight : 0);
            grid.SetBounds(0, titleH + (int)(2 * _scale), innerW, gridH);

            int sectionH = titleH + (int)(2 * _scale) + gridH;
            section.SetBounds(padX, y, innerW, sectionH);
            return y + sectionH;
        }

        private int LayoutOverallSection(int padX, int y, int innerW)
        {
            int titleH = (int)(28 * _scale);
            int cardW = Math.Min(innerW, (int)(520 * _scale));
            int cardH = (int)(30 * _scale) * 8 + (int)(20 * _scale);
            _overallTitle.SetBounds(0, 0, cardW, titleH);
            _overallCard.SetBounds(0, titleH + (int)(2 * _scale), cardW, cardH);

            int sectionH = titleH + (int)(2 * _scale) + cardH;
            _overallSection.SetBounds(padX, y, innerW, sectionH);
            return y + sectionH;
        }

        private void ForwardWheelToScrollHost(int delta)
        {
            int step = -(delta / 120) * (int)(48 * _scale);
            if (step == 0)
                step = delta < 0 ? (int)(48 * _scale) : -(int)(48 * _scale);
            int currentY = -_scrollHost.AutoScrollPosition.Y;
            int maxY = Math.Max(0, _page.Height - _scrollHost.ClientSize.Height);
            int nextY = Math.Max(0, Math.Min(maxY, currentY + step));
            _scrollHost.AutoScrollPosition = new Point(0, nextY);
        }

        public void ScrollToBottom()
        {
            LayoutContent();
            _scrollHost.PerformLayout();
            int maxY = Math.Max(0, _page.Height - _scrollHost.ClientSize.Height);
            _scrollHost.AutoScrollPosition = new Point(0, maxY);
            _scrollHost.PerformLayout();
        }

        public void ScrollToTop()
        {
            _scrollHost.AutoScrollPosition = new Point(0, 0);
            _scrollHost.PerformLayout();
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            _scrollHost.BackColor = t.Background;
            _page.BackColor = t.Background;

            _headerCard.BackColor = t.Surface;
            _lblCompanyName.ForeColor = t.Text;
            _lblCompanyDetails.ForeColor = t.TextMuted;
            _lblReportTitle.ForeColor = t.Primary;
            _lblFilterSummary.ForeColor = t.TextMuted;

            ApplySectionTheme(_salesSection, _salesTitle, _salesGrid, t);
            ApplySectionTheme(_itemsSection, _itemsTitle, _itemsGrid, t);
            ApplySectionTheme(_purchasesSection, _purchasesTitle, _purchasesGrid, t);
            ApplySectionTheme(_cashFlowSection, _cashFlowTitle, _cashFlowGrid, t);

            _overallSection.BackColor = t.Background;
            _overallTitle.ForeColor = t.Primary;
            _overallCard.BackColor = t.Surface;
            _overallTable.BackColor = t.Surface;
            for (int i = 0; i < 8; i++)
            {
                _overallCapLabels[i].BackColor = Color.Transparent;
                _overallValLabels[i].BackColor = Color.Transparent;
                _overallCapLabels[i].ForeColor = i < 2 ? t.Primary : t.TextMuted;
                _overallValLabels[i].ForeColor = i < 2 ? t.Primary : t.Text;
            }

            _headerCard.Invalidate();
            _overallCard.Invalidate();
        }

        private void ApplySectionTheme(Panel section, Label title, DataGridView grid, ThemeTokens t)
        {
            section.BackColor = t.Background;
            title.BackColor = Color.Transparent;
            title.ForeColor = t.Primary;
            ThemeApplier.ApplyGrid(grid, t);
            ApplySummaryRowStyles(grid);
        }

        private void ApplySummaryRowStyles(DataGridView grid)
        {
            Color subBg = Draw.Mix(_theme.Surface, _theme.Primary, 0.09f);
            Color grandBg = Draw.Mix(_theme.Surface, _theme.Primary, 0.16f);
            for (int r = 0; r < grid.Rows.Count; r++)
            {
                string tag = grid.Rows[r].Tag as string;
                if (tag == null)
                    continue;
                bool isGrand = string.Equals(tag, GrandTotalRowTag, StringComparison.Ordinal);
                DataGridViewCellStyle style = grid.Rows[r].DefaultCellStyle;
                style.Font = _headingFont;
                style.BackColor = isGrand ? grandBg : subBg;
                style.ForeColor = isGrand ? _theme.Primary : _theme.Text;
                style.SelectionBackColor = _theme.Primary;
                style.SelectionForeColor = _theme.OnPrimary;
            }
            grid.Invalidate();
        }

        private void PaintCardBorder(object sender, PaintEventArgs e)
        {
            var c = sender as Control;
            if (c == null)
                return;
            using (var pen = new Pen(Draw.StrongBorder(_theme)))
                e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        }

        public void Dispose()
        {
            foreach (ThemedContextMenu m in _menus)
                if (m != null) m.Dispose();
            _menus.Clear();
        }
    }
}
