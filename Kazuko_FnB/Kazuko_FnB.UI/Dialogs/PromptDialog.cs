using System;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Hộp nhập một dòng chữ (ví dụ đặt tên ảnh). Trả về null nếu bấm Hủy.</summary>
    internal sealed class PromptDialog : ThemedDialog
    {
        private readonly TextBox _text = new TextBox { Dock = DockStyle.Top };

        private PromptDialog(string title, string prompt, string initial)
            : base(title, prompt, 440, 190)
        {
            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            Kazuko_FnB.UI.Controls.FlatButton ok = AddButton("Đồng ý", "primary", delegate
            {
                if (_text.Text.Trim().Length == 0)
                {
                    Msg.Warning(this, "Bạn chưa nhập nội dung.");
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            });
            AcceptButton = ok;
            _text.Text = initial ?? "";
            _text.Margin = new Padding(0);
            Body.Padding = new Padding((int)(16 * Dpi), (int)(20 * Dpi), (int)(16 * Dpi), 0);
            Body.Controls.Add(_text);
            Shown += delegate { _text.SelectAll(); _text.Focus(); };
        }

        public static string Ask(IWin32Window owner, string title, string prompt, string initial)
        {
            using (var dlg = new PromptDialog(title, prompt, initial))
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg._text.Text.Trim() : null;
        }
    }
}
