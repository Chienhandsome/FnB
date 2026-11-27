using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp xác nhận trước khi Xuất lại định lượng (docs/specs/xuat-lai-dinh-luong.md §2, §5):
    /// nêu rõ khoảng ngày, số hóa đơn, số dòng sẽ xóa và sinh lại, cảnh báo không hoàn tác được;
    /// bắt gõ đúng từ XACNHAN (không phân biệt hoa thường) mới mở nút Đồng ý; nút Hủy là mặc định.
    /// </summary>
    internal sealed class ConfirmMaterialRegenDialog : ThemedDialog
    {
        private readonly Panel _summaryCard = new Panel { Tag = "surface" };
        private readonly Label _lblSummary = new Label { AutoSize = false, TextAlign = ContentAlignment.TopLeft };
        private readonly Label _lblWarning = new Label { AutoSize = false, TextAlign = ContentAlignment.TopLeft };
        private readonly Label _lblPrompt = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private readonly InputBox _txtConfirm = new InputBox { IsPassword = false, Placeholder = MaterialRegenConstants.ConfirmWord };
        private readonly FlatButton _btnCancel;
        private readonly FlatButton _btnConfirm;
        private readonly Font _bodyFont;
        private readonly Font _warnFont;
        private string _enteredText = "";

        public ConfirmMaterialRegenDialog(MaterialRegenPreview preview, string storeLabel)
            : base("XÁC NHẬN XUẤT LẠI ĐỊNH LƯỢNG", "Thao tác này làm thay đổi tồn kho và giá vốn", 500, 340)
        {
            float s = Dpi;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _warnFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize, FontStyle.Bold);

            _btnCancel = AddButton("Hủy", null, delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            });
            _btnConfirm = AddButton("Đồng ý", "danger", delegate
            {
                UpdateConfirmState();
                if (!MaterialRegenRules.IsConfirmWordValid(_enteredText))
                    return;
                DialogResult = DialogResult.OK;
                Close();
            });
            _btnConfirm.Enabled = false;

            // Spec §5: nút Hủy là mặc định (Enter không tự kích hoạt nút nguy hiểm).
            CancelButton = _btnCancel;
            AcceptButton = _btnCancel;

            FitBody(500, 244);

            int pad = (int)(16 * s);
            int contentW = (int)(468 * s);

            string scope = string.IsNullOrWhiteSpace(storeLabel) ? MaterialRegenConstants.AllStoresLabel : storeLabel.Trim();
            string summaryText = preview == null
                ? ""
                : string.Format(
                    "Khoảng ngày: {0:dd/MM/yyyy} – {1:dd/MM/yyyy}   ({2})\r\n" +
                    "Số hóa đơn đủ điều kiện: {3}\r\n" +
                    "Số dòng xuất vật tư sẽ xóa: {4}\r\n" +
                    "Số dòng xuất vật tư sẽ sinh lại: {5}",
                    preview.FromDate,
                    preview.ToDate,
                    scope,
                    preview.EligibleBillCount,
                    preview.ExistingExportLineCount,
                    preview.NewExportLineCount);

            _summaryCard.SetBounds(pad, (int)(12 * s), contentW, (int)(92 * s));
            _lblSummary.Font = _bodyFont;
            _lblSummary.Text = summaryText;
            _lblSummary.SetBounds((int)(12 * s), (int)(8 * s), contentW - (int)(24 * s), (int)(76 * s));
            _summaryCard.Controls.Add(_lblSummary);

            _lblWarning.Font = _warnFont;
            _lblWarning.Text = MaterialRegenConstants.ConfirmWarning;
            _lblWarning.SetBounds(pad, (int)(112 * s), contentW, (int)(44 * s));

            _lblPrompt.Font = _bodyFont;
            _lblPrompt.Text = "Gõ đúng từ XACNHAN để xác nhận:";
            _lblPrompt.SetBounds(pad, (int)(162 * s), contentW, (int)(24 * s));

            _txtConfirm.Inner.Font = _bodyFont;
            _txtConfirm.SetBounds(pad, (int)(190 * s), contentW, (int)(36 * s));
            _txtConfirm.TextChanged += delegate { UpdateConfirmState(); };

            Body.Controls.Add(_summaryCard);
            Body.Controls.Add(_lblWarning);
            Body.Controls.Add(_lblPrompt);
            Body.Controls.Add(_txtConfirm);

            Shown += delegate { _btnCancel.Focus(); };
        }

        public string EnteredText
        {
            get { return _enteredText; }
        }

        internal bool IsConfirmEnabled
        {
            get { return _btnConfirm.Enabled; }
        }

        internal FlatButton CancelButtonForTest { get { return _btnCancel; } }
        internal FlatButton ConfirmButtonForTest { get { return _btnConfirm; } }
        internal InputBox ConfirmInputForTest { get { return _txtConfirm; } }
        internal Label SummaryLabelForTest { get { return _lblSummary; } }
        internal Label WarningLabelForTest { get { return _lblWarning; } }

        internal void UpdateConfirmState()
        {
            _enteredText = _txtConfirm.Text ?? "";
            _btnConfirm.Enabled = MaterialRegenRules.IsConfirmWordValid(_enteredText);
        }

        internal void SetConfirmTextForTest(string text)
        {
            _txtConfirm.Text = text ?? "";
            UpdateConfirmState();
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            _summaryCard.BackColor = t.Surface;
            _lblSummary.BackColor = t.Surface;
            _lblSummary.ForeColor = t.Text;
            _lblWarning.ForeColor = t.Danger;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bodyFont.Dispose();
                _warnFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
