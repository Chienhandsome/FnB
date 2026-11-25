using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần dựng lưới 9 cột, đổ dữ liệu, co giãn cột theo DPI, áp theme / dòng đậm và cuộn xuống cuối
    /// của <see cref="BusinessResultView"/>.
    /// </summary>
    internal sealed partial class BusinessResultView
    {
        private void BuildGrid()
        {
            float s = _scale;
            int pad = (int)(10 * s);
            _gridHost.Padding = new Padding(pad, (int)(8 * s), pad, pad);

            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.Font = _bodyFont;
            _grid.ColumnHeadersDefaultCellStyle.Font = _boldFont;
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(38 * s);
            _grid.RowTemplate.Height = (int)(36 * s);

            PropertyInfo prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (prop != null)
                prop.SetValue(_grid, true, null);

            // Đúng 9 cột từ trái sang phải theo spec §1 & §2.1, không cột ẩn, không sắp xếp khi bấm tiêu đề.
            AddColumn("colMuc", "Mục", 54, DataGridViewContentAlignment.MiddleCenter, typeof(string), null, null, "III.");
            AddColumn("colNoiDung", "Nội dung", 280, DataGridViewContentAlignment.MiddleLeft, typeof(string), null, null, "Xây dựng, sửa chữa, thiết kế");
            AddColumn("colPctDt", "%/DT", 68, DataGridViewContentAlignment.MiddleRight, typeof(string), null, null, "100%");
            AddColumn("colGiaTri", "Giá trị", 130, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "-999.999.999");
            AddColumn("colPct", "%", 64, DataGridViewContentAlignment.MiddleRight, typeof(string), null, null, "100%");
            AddColumn("colPctCp", "%/CP", 68, DataGridViewContentAlignment.MiddleRight, typeof(string), null, null, "100%");
            AddColumn("colTrend", "▲/▼", 48, DataGridViewContentAlignment.MiddleCenter, typeof(string), null, null, "▲/▼");
            AddColumn("colTangGiam", "Tăng giảm so với tháng trước", 200, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "-999.999.999");
            AddColumn("colThangTruoc", "KQKD tháng trước", 145, DataGridViewContentAlignment.MiddleRight, typeof(decimal), "n0", ViCulture, "-999.999.999");

            _grid.SizeChanged += delegate { AdjustColumnWidths(); };
            _gridHost.Controls.Add(_grid);
        }

        private void AddColumn(
            string name,
            string header,
            int minWidthAt96,
            DataGridViewContentAlignment alignment,
            Type valueType,
            string format,
            IFormatProvider formatProvider,
            string sample)
        {
            int pad = (int)Math.Ceiling(22 * _scale);
            int width = (int)Math.Ceiling(minWidthAt96 * _scale);
            int headerW = TextRenderer.MeasureText(header ?? "", _boldFont).Width + pad;
            if (headerW > width)
                width = headerW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, _boldFont).Width + pad;
                if (sampleW > width)
                    width = sampleW;
            }

            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                MinimumWidth = width,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                ValueType = valueType,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            col.DefaultCellStyle.Alignment = alignment;
            col.DefaultCellStyle.NullValue = "";
            if (alignment == DataGridViewContentAlignment.MiddleRight)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            else if (alignment == DataGridViewContentAlignment.MiddleCenter)
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            if (!string.IsNullOrEmpty(format))
                col.DefaultCellStyle.Format = format;
            if (formatProvider != null)
                col.DefaultCellStyle.FormatProvider = formatProvider;

            _grid.Columns.Add(col);
            _baseColWidths[name] = width;
        }

        private void BindRowsToGrid()
        {
            _grid.SuspendLayout();
            try
            {
                _grid.Rows.Clear();
                foreach (BusinessResultRow row in _rows)
                {
                    int idx = _grid.Rows.Add(
                        row.ItemNumber,
                        row.Content,
                        row.PercentOfRevenue,
                        row.CurrentValue,
                        row.PercentMain,
                        row.PercentThird,
                        row.TrendSymbol,
                        row.ChangeAmount,
                        row.PreviousValue);
                    _grid.Rows[idx].Tag = row;
                }
                ApplyRowStyles();
                AdjustColumnWidths();
                _grid.ClearSelection();
            }
            finally
            {
                _grid.ResumeLayout();
            }
        }

        private void AdjustColumnWidths()
        {
            if (_grid.Columns.Count == 0)
                return;

            int pad = (int)Math.Ceiling(22 * _scale);
            int indentPx = (int)Math.Ceiling(16 * _scale);
            int totalOtherW = 0;

            foreach (DataGridViewColumn col in _grid.Columns)
            {
                int baseW;
                if (!_baseColWidths.TryGetValue(col.Name, out baseW))
                    continue;

                int target = baseW;
                bool isContentCol = col.Name == "colNoiDung";
                for (int r = 0; r < _grid.Rows.Count; r++)
                {
                    string text = Convert.ToString(_grid.Rows[r].Cells[col.Index].FormattedValue);
                    if (string.IsNullOrEmpty(text))
                        continue;
                    var item = _grid.Rows[r].Tag as BusinessResultRow;
                    Font f = item != null && item.IsBold ? _boldFont : (item != null && item.IsGroupHeader ? _subHeadingFont : _bodyFont);
                    int extra = isContentCol && item != null && !item.IsBold && !item.IsGroupHeader ? indentPx : 0;
                    int needed = TextRenderer.MeasureText(text, f).Width + pad + extra;
                    if (needed > target)
                        target = needed;
                }

                if (col.Width != target)
                    col.Width = target;
                if (!isContentCol)
                    totalOtherW += col.Width;
            }

            DataGridViewColumn contentCol = _grid.Columns["colNoiDung"];
            if (contentCol != null && _grid.ClientSize.Width > 0)
            {
                int scrollW = _grid.DisplayedRowCount(false) < _grid.Rows.Count
                    ? SystemInformation.VerticalScrollBarWidth
                    : 0;
                int avail = _grid.ClientSize.Width - totalOtherW - scrollW - 4;
                if (avail > contentCol.Width)
                    contentCol.Width = avail;
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            _theme = t;
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;

            _topBar.BackColor = t.Background;
            _fromDate.ApplyTheme(t, t.Background);
            _toDate.ApplyTheme(t, t.Background);
            _storeFrame.ApplyTheme(t, t.Background);
            ThemeApplier.ApplyButton(_btnLoad, t, "primary");
            ThemeApplier.ApplyButton(_btnPrint, t, null);

            _headerBar.BackColor = t.Surface;
            _lblTitle.BackColor = Color.Transparent;
            _lblTitle.ForeColor = t.Primary;
            _lblPeriodInfo.BackColor = Color.Transparent;
            _lblPeriodInfo.ForeColor = t.TextMuted;
            _headerBar.Invalidate();

            _gridHost.BackColor = t.Background;
            ThemeApplier.ApplyGrid(_grid, t);
            ApplyRowStyles();
        }

        private void ApplyRowStyles()
        {
            Color majorBg = Draw.Mix(_theme.Surface, _theme.Primary, 0.10f);
            Color profitBg = Draw.Mix(_theme.Surface, _theme.Primary, 0.16f);
            Color groupBg = Draw.Mix(_theme.Surface, _theme.Primary, 0.05f);
            int indentPx = (int)Math.Ceiling(16 * _scale);

            for (int r = 0; r < _grid.Rows.Count; r++)
            {
                DataGridViewRow gridRow = _grid.Rows[r];
                var item = gridRow.Tag as BusinessResultRow;
                if (item == null)
                    continue;

                DataGridViewCellStyle style = gridRow.DefaultCellStyle;
                style.SelectionBackColor = _theme.Primary;
                style.SelectionForeColor = _theme.OnPrimary;

                if (item.Loai == BusinessResultConstants.LoaiProfitLoss)
                {
                    style.Font = _boldFont;
                    style.BackColor = profitBg;
                    style.ForeColor = _theme.Primary;
                }
                else if (item.IsBold)
                {
                    style.Font = _boldFont;
                    style.BackColor = majorBg;
                    style.ForeColor = _theme.Text;
                }
                else if (item.IsGroupHeader)
                {
                    style.Font = _subHeadingFont;
                    style.BackColor = groupBg;
                    style.ForeColor = _theme.Text;
                }
                else
                {
                    style.Font = _bodyFont;
                    style.BackColor = _theme.Surface;
                    style.ForeColor = _theme.Text;
                    gridRow.Cells["colNoiDung"].Style.Padding = new Padding(indentPx, 0, 0, 0);
                }
            }
            _grid.Invalidate();
        }

        internal void ScrollToBottom()
        {
            if (_grid.Rows.Count == 0)
                return;
            int last = _grid.Rows.Count - 1;
            int visibleRows = _grid.DisplayedRowCount(false);
            int targetFirst = visibleRows > 0 ? Math.Max(0, _grid.Rows.Count - visibleRows) : last;
            _grid.FirstDisplayedScrollingRowIndex = targetFirst;
            while (_grid.FirstDisplayedScrollingRowIndex < last)
            {
                Rectangle rect = _grid.GetRowDisplayRectangle(last, false);
                if (rect.Height >= _grid.Rows[last].Height)
                    break;
                _grid.FirstDisplayedScrollingRowIndex++;
            }
        }
    }
}
