using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        /// <summary>
        /// Kiểm chống cắt chữ trên giao diện thật của Báo giá (r3):
        /// - Mọi nhãn trên QuoteEditDialog (1320x760) và QuoteListView (1360x760) có Width/Height đủ chứa chữ trên 1 dòng.
        /// - Mọi cột của cả 4 lưới (2 ở dialog, 2 ở danh sách) không xuống dòng tiêu đề và Width >= đo chữ tiêu đề + 8px.
        /// - Cột Ngày >= bề rộng "00/00/0000" + 8px; cột ĐVT chi tiết >= bề rộng "Thùng" + 8px;
        ///   ô tỉ lệ giảm đủ chứa "-999.99"; dialog không phải cuộn dọc ở 1320x760.
        /// </summary>
        private static void QuoteLayoutTests(UserSession adminSession, string sampleQuoteId)
        {
            // 1. QuoteEditDialog ở kích thước thật 1320x760.
            using (QuoteEditDialog dlg = QuoteEditDialog.CreateForPreview(adminSession))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.ClientSize = new Size(1320, 760);
                dlg.Show();
                if (!string.IsNullOrEmpty(sampleQuoteId))
                    dlg.Preview("id=" + sampleQuoteId);
                dlg.PerformLayout();
                Application.DoEvents();
                try
                {
                    string[] dlgLabels =
                    {
                        "Ngày", "Số phiếu", "Khách hàng", "Tên khách", "Điện thoại", "Địa chỉ", "Email", "Ghi chú",
                        "Tiền hàng", "Giảm giá", "Thuế", "Phí vận chuyển", "Tổng cộng", "Tìm (F3)", "Số lượng"
                    };
                    string labelErr;
                    bool dlgLabelsOk = CheckLabelsFitOneLine(dlg, dlgLabels, out labelErr);
                    Check("Báo giá Layout (r3 QuoteEditDialog 1320x760): mọi nhãn đủ rộng/cao trên một dòng (không cắt chữ)",
                        dlgLabelsOk, labelErr);

                    string prodGridErr, detGridErr;
                    bool prodGridOk = CheckGridColumnWidths(dlg.ProductGridForTest, out prodGridErr);
                    bool detGridOk = CheckGridColumnWidths(dlg.DetailGridForTest, out detGridErr);

                    DataGridView dg = dlg.DetailGridForTest;
                    int unitNeed = TextRenderer.MeasureText("Thùng", dg.Font).Width + 8;
                    bool unitColOk = dg.Columns["d_unit"] != null && dg.Columns["d_unit"].Width >= unitNeed;

                    NumericUpDown pctBox = dlg.TileGiamGiaBoxForTest;
                    int pctTextNeed = TextRenderer.MeasureText("-999.99", pctBox.Font).Width;
                    int spinW = pctBox.Controls.Count > 0 ? pctBox.Controls[0].Width : SystemInformation.VerticalScrollBarWidth;
                    int pctAvail = pctBox.ClientSize.Width - spinW;
                    bool pctBoxOk = pctAvail >= pctTextNeed;
                    bool noVScroll = !dlg.AutoScroll;

                    Check("Báo giá Layout (r3 QuoteEditDialog): 2 lưới không cắt tiêu đề, ĐVT >= 'Thùng', ô % >= '-999.99', không cuộn dọc",
                        prodGridOk && detGridOk && unitColOk && pctBoxOk && noVScroll,
                        "prod=" + prodGridErr + "; det=" + detGridErr + "; unit=" + (dg.Columns["d_unit"] == null ? 0 : dg.Columns["d_unit"].Width) + "/" + unitNeed
                        + "; pct=" + pctAvail + "/" + pctTextNeed);
                }
                finally { dlg.Close(); }
            }

            // 2. QuoteListView ở kích thước thật 1360x760.
            using (var host = MakeOffscreenHost())
            {
                host.ClientSize = new Size(1360, 760);
                var view = new QuoteListView(adminSession) { Dock = DockStyle.Fill, SuppressPopup = true };
                host.Controls.Add(view);
                host.Show();
                host.PerformLayout();
                Application.DoEvents();
                try
                {
                    string[] listLabels = { "Từ ngày", "Đến ngày", "Số phiếu", "Khách hàng" };
                    string listLabelErr;
                    bool listLabelsOk = CheckLabelsFitOneLine(view, listLabels, out listLabelErr);

                    string mainGridErr, subGridErr;
                    bool mainGridOk = CheckGridColumnWidths(view.GridForTest, out mainGridErr);
                    bool subGridOk = CheckGridColumnWidths(view.DetailGridForTest, out subGridErr);

                    DataGridView mg = view.GridForTest;
                    int dateNeed = TextRenderer.MeasureText("00/00/0000", mg.Font).Width + 8;
                    bool dateColOk = mg.Columns["colDate"] != null && mg.Columns["colDate"].Width >= dateNeed;

                    Check("Báo giá Layout (r3 QuoteListView 1360x760): nhãn lọc không cắt, 2 lưới không cắt tiêu đề, cột Ngày >= '00/00/0000'",
                        listLabelsOk && mainGridOk && subGridOk && dateColOk,
                        "lbl=" + listLabelErr + "; main=" + mainGridErr + "; sub=" + subGridErr
                        + "; date=" + (mg.Columns["colDate"] == null ? 0 : mg.Columns["colDate"].Width) + "/" + dateNeed);
                }
                finally { host.Close(); }
            }
        }

        private static bool CheckLabelsFitOneLine(Control root, string[] requiredTexts, out string detail)
        {
            var found = new Dictionary<string, Label>(StringComparer.Ordinal);
            CollectLabels(root, found);
            var issues = new List<string>();
            foreach (string text in requiredTexts)
            {
                Label lbl;
                if (!found.TryGetValue(text, out lbl))
                {
                    issues.Add("thiếu nhãn '" + text + "'");
                    continue;
                }
                Size sz = TextRenderer.MeasureText(lbl.Text, lbl.Font);
                if (lbl.Width < sz.Width || lbl.Height < sz.Height)
                    issues.Add("'" + text + "' " + lbl.Width + "x" + lbl.Height + " < " + sz.Width + "x" + sz.Height);
            }
            detail = string.Join(", ", issues.ToArray());
            return issues.Count == 0;
        }

        private static void CollectLabels(Control parent, Dictionary<string, Label> map)
        {
            foreach (Control c in parent.Controls)
            {
                var lbl = c as Label;
                if (lbl != null && !string.IsNullOrEmpty(lbl.Text) && !map.ContainsKey(lbl.Text))
                    map[lbl.Text] = lbl;
                if (c.HasChildren)
                    CollectLabels(c, map);
            }
        }

        private static bool CheckGridColumnWidths(DataGridView grid, out string detail)
        {
            var issues = new List<string>();
            if (grid.ColumnHeadersDefaultCellStyle.WrapMode != DataGridViewTriState.False)
                issues.Add("WrapMode != False");
            foreach (DataGridViewColumn col in grid.Columns)
            {
                Font hdrFont = col.HeaderCell.Style.Font ?? grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
                int need = TextRenderer.MeasureText(col.HeaderText ?? "", hdrFont).Width + 8;
                if (col.Width < need)
                    issues.Add(col.Name + "('" + col.HeaderText + "') w=" + col.Width + " < " + need);
                if (col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill && col.MinimumWidth < need)
                    issues.Add(col.Name + " minW=" + col.MinimumWidth + " < " + need);
            }
            detail = string.Join(", ", issues.ToArray());
            return issues.Count == 0;
        }
    }
}
