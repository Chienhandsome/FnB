using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    internal sealed partial class OrderControlView
    {
        // ------------------------------------------------------------------ Dữ liệu & Hành động

        private void InitializeData()
        {
            _loading = true;
            try
            {
                DateTime today = _service.GetServerDate();
                _fromDate.Value = today;
                _toDate.Value = today;
            }
            catch (DataAccessException ex)
            {
                LastErrorMessageForTest = "Không lấy được ngày máy chủ: " + ex.Message;
                _fromDate.Value = DateTime.Today;
                _toDate.Value = DateTime.Today;
                if (!SuppressPopup)
                    Msg.Error(this, LastErrorMessageForTest);
            }
            finally
            {
                _loading = false;
            }
            ReloadData();
        }

        /// <summary>Kiểm tra khoảng ngày, tải hóa đơn từ DB và cập nhật hai lưới cùng nhãn tóm tắt.</summary>
        public void ReloadData()
        {
            if (_loading)
                return;

            DateTime from = _fromDate.Value.Date;
            DateTime to = _toDate.Value.Date;

            string dateError = OrderControlAnalyzer.ValidateDateRange(from, to);
            if (dateError != null)
            {
                LastWarningMessageForTest = dateError;
                if (!SuppressPopup)
                    Msg.Warning(this, dateError);
                return;
            }

            LastWarningMessageForTest = null;
            LastErrorMessageForTest = null;

            try
            {
                string serviceError;
                OrderControlAnalysisResult result = _service.LoadAnalysis(from, to, out serviceError);
                if (serviceError != null)
                {
                    LastWarningMessageForTest = serviceError;
                    RenderResult(new OrderControlAnalysisResult());
                    if (!SuppressPopup)
                        Msg.Warning(this, serviceError);
                    return;
                }

                RenderResult(result);
                LoadCountForTest++;
            }
            catch (DataAccessException ex)
            {
                LastErrorMessageForTest = "Không đọc được dữ liệu kiểm soát order: " + ex.Message;
                RenderResult(new OrderControlAnalysisResult());
                if (!SuppressPopup)
                    Msg.Error(this, LastErrorMessageForTest);
            }
        }

        /// <summary>Xuất lưới "Đã dùng" ra file Excel (.xls) qua GridExcelExporter.</summary>
        public bool ExportUsedGrid()
        {
            LastExportMessageForTest = null;
            string dateTag = _fromDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                + "_" + _toDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return GridExcelExporter.Export(
                this,
                _usedGrid,
                "KiemSoatOrder_DaDung_" + dateTag,
                GridExportOutputForTest,
                delegate (string msg) { LastExportMessageForTest = msg; });
        }

        /// <summary>Xuất lưới "Cần kiểm tra" ra file Excel (.xls) qua GridExcelExporter.</summary>
        public bool ExportIssueGrid()
        {
            LastExportMessageForTest = null;
            string dateTag = _fromDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                + "_" + _toDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return GridExcelExporter.Export(
                this,
                _issueGrid,
                "KiemSoatOrder_CanKiemTra_" + dateTag,
                GridExportOutputForTest,
                delegate (string msg) { LastExportMessageForTest = msg; });
        }

        private void RenderResult(OrderControlAnalysisResult result)
        {
            _lastResult = result ?? new OrderControlAnalysisResult();
            OrderControlSummary sum = _lastResult.Summary ?? new OrderControlSummary();

            SetSummaryCard("max", sum.MaxOrder.HasValue ? OrderControlAnalyzer.FormatOrderNumber(sum.MaxOrder.Value) : "—", sum.MaxOrder.HasValue);
            SetSummaryCard("min", sum.MinOrder.HasValue ? OrderControlAnalyzer.FormatOrderNumber(sum.MinOrder.Value) : "—", sum.MinOrder.HasValue);
            SetSummaryCard("used", OrderControlAnalyzer.FormatCount(sum.UsedCount), sum.UsedCount > 0);
            SetSummaryCard("dup", OrderControlAnalyzer.FormatCount(sum.DuplicateCount), sum.DuplicateCount > 0);
            SetSummaryCard("invalid", OrderControlAnalyzer.FormatCount(sum.InvalidCount), sum.InvalidCount > 0);
            SetSummaryCard("missing", OrderControlAnalyzer.FormatCount(sum.MissingCount), sum.MissingCount > 0);

            _usedGrid.Rows.Clear();
            foreach (OrderControlUsedRow row in _lastResult.UsedRows)
            {
                int idx = _usedGrid.Rows.Add(
                    row.OrderNumber,
                    row.BillDisplay ?? "",
                    row.FirstBillDate);
                _usedGrid.Rows[idx].Tag = row;
            }
            AdjustNonFillWidths(_usedGrid, _baseUsedWidths, 280);
            _usedGrid.ClearSelection();

            _issueGrid.Rows.Clear();
            foreach (OrderControlIssueRow issue in _lastResult.IssueRows)
            {
                int idx = _issueGrid.Rows.Add(
                    issue.TypeDisplay ?? "",
                    issue.Message ?? "",
                    issue.DateOrBillDisplay ?? "");
                _issueGrid.Rows[idx].Tag = issue;
            }
            AdjustNonFillWidths(_issueGrid, _baseIssueWidths, 320);
            _issueGrid.ClearSelection();

            _usedTitle.Text = "ĐÃ DÙNG (" + OrderControlAnalyzer.FormatCount(_lastResult.UsedRows.Count) + ")";
            _issueTitle.Text = "CẦN KIỂM TRA (" + OrderControlAnalyzer.FormatCount(_lastResult.IssueRows.Count) + ")";

            _emptyLabel.Text = OrderControlConstants.EmptyDataMessage;
            _emptyBanner.Visible = !_lastResult.HasAnyData;

            ApplyGridRowColors(_theme);
            ApplySummaryCardColors(_theme);
        }

        private void SetSummaryCard(string key, string text, bool highlighted)
        {
            SummaryCard card;
            if (!_summaryByKey.TryGetValue(key, out card))
                return;
            card.ValueLabel.Text = text;
            card.IsHighlighted = highlighted;
        }

        private void AdjustNonFillWidths(DataGridView grid, Dictionary<string, int> baseWidths, int maxWidthAt96)
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
                    int needed = TextRenderer.MeasureText(text, _bodyFont).Width + pad;
                    if (needed > target)
                        target = needed;
                }
                if (target > maxW)
                    target = maxW;
                if (col.Width != target)
                    col.Width = target;
            }
        }

        // ------------------------------------------------------------------ Giao diện / Theme

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
            ForeColor = t.Text;

            _topBar.BackColor = t.Background;
            _fromDate.ApplyTheme(t, t.Background);
            _toDate.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyButton(_btnView, t, "primary");
            ThemeApplier.ApplyButton(_btnExportUsed, t, null);
            ThemeApplier.ApplyButton(_btnExportIssues, t, null);

            _summaryPanel.BackColor = t.Background;
            _summaryTable.BackColor = t.Background;
            ApplySummaryCardColors(t);

            _emptyBanner.BackColor = Draw.Mix(t.Surface, t.Info, 0.10f);
            _emptyLabel.BackColor = Color.Transparent;
            _emptyLabel.ForeColor = t.TextMuted;

            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _usedTitle.ForeColor = t.Primary;
            _issueTitle.ForeColor = t.Primary;

            ThemeApplier.ApplyGrid(_usedGrid, t);
            ThemeApplier.ApplyGrid(_issueGrid, t);
            ApplyGridRowColors(t);

            _topBar.Invalidate();
            _usedGrid.Invalidate();
            _issueGrid.Invalidate();
        }

        private void ApplySummaryCardColors(ThemeTokens t)
        {
            for (int i = 0; i < _summaryCards.Count; i++)
            {
                SummaryCard card = _summaryCards[i];
                card.Box.BackColor = t.Surface;
                card.CaptionLabel.ForeColor = t.TextMuted;
                if (card.IsAlertMetric)
                    card.ValueLabel.ForeColor = card.IsHighlighted ? t.Danger : t.TextMuted;
                else
                    card.ValueLabel.ForeColor = card.IsHighlighted ? t.Primary : t.TextMuted;
                card.Box.Invalidate();
            }
        }

        private void ApplyGridRowColors(ThemeTokens t)
        {
            for (int i = 0; i < _usedGrid.Rows.Count; i++)
            {
                DataGridViewRow gridRow = _usedGrid.Rows[i];
                var data = gridRow.Tag as OrderControlUsedRow;
                if (data != null && data.IsDuplicate)
                {
                    gridRow.DefaultCellStyle.BackColor = UsedDuplicateBackColor(t);
                    gridRow.DefaultCellStyle.ForeColor = UsedDuplicateForeColor(t);
                }
                else
                {
                    gridRow.DefaultCellStyle.BackColor = Color.Empty;
                    gridRow.DefaultCellStyle.ForeColor = Color.Empty;
                }
            }

            for (int i = 0; i < _issueGrid.Rows.Count; i++)
            {
                DataGridViewRow gridRow = _issueGrid.Rows[i];
                var issue = gridRow.Tag as OrderControlIssueRow;
                if (issue == null)
                    continue;
                gridRow.DefaultCellStyle.BackColor = IssueRowBackColor(issue.Kind, t);
                gridRow.DefaultCellStyle.ForeColor = IssueRowForeColor(issue.Kind, t);
            }
        }

        internal static Color UsedDuplicateBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Danger, 0.08f);
        }

        internal static Color UsedDuplicateForeColor(ThemeTokens t)
        {
            return t.Danger;
        }

        internal static Color IssueRowBackColor(OrderControlIssueKind kind, ThemeTokens t)
        {
            if (kind == OrderControlIssueKind.Invalid)
                return Draw.Mix(t.Surface, t.Danger, 0.10f);
            if (kind == OrderControlIssueKind.Duplicate)
                return Draw.Mix(t.Surface, t.Primary, 0.10f);
            return Draw.Mix(t.Surface, t.Info, 0.08f);
        }

        internal static Color IssueRowForeColor(OrderControlIssueKind kind, ThemeTokens t)
        {
            if (kind == OrderControlIssueKind.Invalid)
                return t.Danger;
            if (kind == OrderControlIssueKind.Duplicate)
                return t.Primary;
            return t.Info;
        }

        // ------------------------------------------------------------------ Hỗ trợ selftest & --render-screen

        internal string SummaryValueTextForTest(string key)
        {
            SummaryCard card;
            return _summaryByKey.TryGetValue(key, out card) ? (card.ValueLabel.Text ?? "") : "";
        }

        internal bool DispatchCmdKeyForTest(Keys keyData)
        {
            var m = new Message();
            return ProcessCmdKey(ref m, keyData);
        }

        internal void Preview(string option)
        {
            string opt = (option ?? "").Trim();
            if (opt.Length == 0)
                return;
            string[] parts = opt.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return;
            if (parts[0] == "from")
            {
                _fromDate.Inner.Text = parts[1];
                _fromDate.Commit();
                ReloadData();
            }
            else if (parts[0] == "to")
            {
                _toDate.Inner.Text = parts[1];
                _toDate.Commit();
                ReloadData();
            }
        }
    }
}
