using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần phím tắt, xác nhận đóng, theme, đo độ rộng nhãn/cột và chụp thử giao diện cho <see cref="StockTakeEditDialog"/>.</summary>
    internal sealed partial class StockTakeEditDialog
    {
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK || _loadFailed || !Dirty || SuppressPopup)
                return;
            DialogResult answer = MessageBox.Show(this, "Dữ liệu đã thay đổi. Bạn có muốn lưu lại không?",
                "Xác nhận", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (answer == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (answer == DialogResult.Yes && !DoSave())
                e.Cancel = true;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                _txtSearch.Focus();
                _txtSearch.SelectAll();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.L && !_readOnly) { SaveStay(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S && !_readOnly && _service.CanAdd) { SaveAndNew(); e.Handled = true; }
            else if (e.KeyCode == Keys.F8 && _detailGrid.ContainsFocus && !_readOnly) { DeleteLine(); e.Handled = true; }
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        // ------------------------------------------------------------------ Màu chênh lệch suy từ ThemeTokens (không hard-code màu)

        internal static Color ShortageRowBackColor(ThemeTokens t)
        {
            return Draw.Mix(t.Surface, t.Danger, 0.18f);
        }

        internal static Color ShortageRowForeColor(ThemeTokens t)
        {
            return t.Danger;
        }

        internal static Color SurplusRowBackColor(ThemeTokens t)
        {
            Color warnBase = Draw.ShiftHsl(t.Danger, 44f, 0.16f);
            return Draw.Mix(t.Surface, warnBase, 0.28f);
        }

        internal static Color SurplusRowForeColor(ThemeTokens t)
        {
            return t.Text;
        }

        internal static void StyleDiffRow(DataGridViewRow row, StockTakeDiffTone tone, ThemeTokens t)
        {
            if (row == null)
                return;
            if (tone == StockTakeDiffTone.Shortage)
            {
                row.DefaultCellStyle.BackColor = ShortageRowBackColor(t);
                row.DefaultCellStyle.ForeColor = ShortageRowForeColor(t);
                row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Primary, t.Danger, 0.50f);
                row.DefaultCellStyle.SelectionForeColor = t.OnPrimary;
            }
            else if (tone == StockTakeDiffTone.Surplus)
            {
                row.DefaultCellStyle.BackColor = SurplusRowBackColor(t);
                row.DefaultCellStyle.ForeColor = SurplusRowForeColor(t);
                Color warnSel = Draw.ShiftHsl(t.Danger, 44f, 0.10f);
                row.DefaultCellStyle.SelectionBackColor = Draw.Mix(t.Surface, warnSel, 0.48f);
                row.DefaultCellStyle.SelectionForeColor = t.Text;
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.Empty;
                row.DefaultCellStyle.ForeColor = Color.Empty;
                row.DefaultCellStyle.SelectionBackColor = Color.Empty;
                row.DefaultCellStyle.SelectionForeColor = Color.Empty;
            }
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            _dtNgay.ApplyTheme(t, t.Background);
            if (_productGrid != null)
                ThemeApplier.ApplyGrid(_productGrid, t);
            if (_detailGrid != null)
            {
                ThemeApplier.ApplyGrid(_detailGrid, t);
                foreach (DataGridViewRow r in _detailGrid.Rows)
                {
                    var line = r.Tag as StockTakeLine;
                    if (line != null)
                        StyleDiffRow(r, line.Tone, t);
                }
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FormClosing += OnFormClosing;
        }

        private static int MeasureMaxLabelWidth(string[] texts, Font font, float s)
        {
            int pad = (int)Math.Ceiling(16 * s);
            int max = 0;
            foreach (string t in texts)
            {
                int w = TextRenderer.MeasureText(t, font).Width + pad;
                if (w > max) max = w;
            }
            return max;
        }

        private static int MeasureGridColumnWidth(DataGridView g, string header, string sample, int minWidth96, float s)
        {
            int w = (int)Math.Ceiling(minWidth96 * s);
            Font hdrFont = g.ColumnHeadersDefaultCellStyle.Font ?? g.Font;
            int hdrW = TextRenderer.MeasureText(header ?? "", hdrFont).Width + (int)Math.Ceiling(28 * s);
            if (hdrW > w) w = hdrW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, g.Font).Width + (int)Math.Ceiling(24 * s);
                if (sampleW > w) w = sampleW;
            }
            return w;
        }

        public static StockTakeEditDialog CreateForPreview(UserSession session)
        {
            return new StockTakeEditDialog(session, null, false, true)
            {
                SuppressPopup = true,
                WindowState = FormWindowState.Normal,
                ClientSize = new Size(1320, 760)
            };
        }

        public void Preview(string option)
        {
            if (string.IsNullOrEmpty(option))
                return;
            if (option.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
            {
                LoadRecord(option.Substring(3));
                _original = Snapshot();
                ApplyPermissions();
                UpdateButtons();
            }
        }
    }
}
