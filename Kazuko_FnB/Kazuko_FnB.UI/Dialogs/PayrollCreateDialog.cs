using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại gọn để thêm mới kỳ bảng lương (TBANGLUONG): chọn Tháng (1–12), Năm (0–2999),
    /// mặc định tháng/năm hiện tại, tên tự sinh "Tháng m/yyyy", ghi chú tùy chọn.
    /// </summary>
    internal sealed class PayrollCreateDialog : ThemedDialog
    {
        private const int BodyWidth96 = 400;
        private const int BodyHeight96 = 188;

        private readonly PayrollService _service;

        private readonly NumberBox _numMonth = new NumberBox
        {
            Minimum = PayrollText.MinMonth,
            Maximum = PayrollText.MaxMonth,
            DecimalPlaces = 0,
            ThousandsSeparator = false,
            TextAlign = HorizontalAlignment.Right
        };
        private readonly NumberBox _numYear = new NumberBox
        {
            Minimum = PayrollText.MinYear,
            Maximum = PayrollText.MaxYear,
            DecimalPlaces = 0,
            ThousandsSeparator = false,
            TextAlign = HorizontalAlignment.Right
        };
        private readonly TextBox _txtName = new TextBox { ReadOnly = true };
        private readonly TextBox _txtNote = new TextBox { MaxLength = PayrollText.MaxTextLength };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnClose;

        public PayrollPeriodRow CreatedPeriod { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal NumberBox MonthBoxForTest { get { return _numMonth; } }
        internal NumberBox YearBoxForTest { get { return _numYear; } }
        internal TextBox NameBoxForTest { get { return _txtName; } }
        internal TextBox NoteBoxForTest { get { return _txtNote; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }

        public PayrollCreateDialog(PayrollService service)
            : base("THÊM BẢNG LƯƠNG", "Chọn tháng và năm cho bảng lương mới", BodyWidth96, 304)
        {
            if (service == null)
                throw new ArgumentNullException("service");
            _service = service;

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSave = AddButton("Lưu", "primary", delegate { DoSave(); });
            CancelButton = _btnClose;

            BuildFields();

            DateTime today = DateTime.Today;
            _numMonth.Value = today.Month;
            _numYear.Value = Math.Min(PayrollText.MaxYear, Math.Max(PayrollText.MinYear, today.Year));
            UpdateGeneratedName();

            _numMonth.ValueChanged += delegate { ClearError(); UpdateGeneratedName(); };
            _numYear.ValueChanged += delegate { ClearError(); UpdateGeneratedName(); };
            _txtNote.TextChanged += delegate { ClearError(); };

            ApplyDialogTheme();
            Shown += delegate { _numMonth.Focus(); };
        }

        private void BuildFields()
        {
            float s = Dpi;
            string[] captions = { "Tháng *", "Năm *", "Tên bảng lương", "Ghi chú" };
            int labelW = (int)(116 * s);
            foreach (string cap in captions)
            {
                int w = TextRenderer.MeasureText(cap, Font).Width + (int)(12 * s);
                if (w > labelW) labelW = w;
            }

            int left = (int)(14 * s);
            int gap = (int)(8 * s);
            int editX = left + labelW + gap;
            int editW = Math.Max((int)(180 * s), Body.ClientSize.Width - editX - left);
            int numW = (int)(108 * s);
            int rowH = (int)(26 * s);
            int tab = 0;

            PlaceRow(captions[0], _numMonth, left, (int)(14 * s), labelW, editX, numW, rowH, ref tab);
            PlaceRow(captions[1], _numYear, left, (int)(48 * s), labelW, editX, numW, rowH, ref tab);
            PlaceRow(captions[2], _txtName, left, (int)(82 * s), labelW, editX, editW, rowH, ref tab);
            PlaceRow(captions[3], _txtNote, left, (int)(116 * s), labelW, editX, editW, rowH, ref tab);

            _lblError.SetBounds(left, (int)(152 * s), Body.ClientSize.Width - 2 * left, (int)(26 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private void PlaceRow(string caption, Control editor, int x, int y, int labelW, int editX, int editW, int h, ref int tabIndex)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                TabStop = false
            };
            lbl.SetBounds(x, y, labelW, h);
            editor.SetBounds(editX, y, editW, h);
            editor.TabIndex = tabIndex++;
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
        }

        private void UpdateGeneratedName()
        {
            int month = (int)_numMonth.Value;
            int year = (int)_numYear.Value;
            _txtName.Text = PayrollText.FormatPeriodName(month, year);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter || keyData == (Keys.Control | Keys.S) || keyData == (Keys.Control | Keys.L))
            {
                DoSave();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        internal bool DoSave()
        {
            int month = (int)_numMonth.Value;
            int year = (int)_numYear.Value;
            string note = (_txtNote.Text ?? "").Trim();

            PayrollCreateResult res;
            try
            {
                res = _service.CreatePeriod(month, year, note);
            }
            catch (DataAccessException ex)
            {
                ShowError("Không lưu được bảng lương: " + ex.Message);
                return false;
            }

            if (!res.Ok)
            {
                ShowError(res.ErrorMessage ?? "Không thể thêm bảng lương.");
                return false;
            }

            ClearError();
            CreatedPeriod = res.Created;
            DialogResult = DialogResult.OK;
            Close();
            return true;
        }

        private void ShowError(string message)
        {
            LastErrorForTest = message;
            _lblError.Text = (message ?? "").Replace(Environment.NewLine, "  |  ");
            _lblError.Visible = !string.IsNullOrEmpty(message);
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ClearError()
        {
            LastErrorForTest = null;
            _lblError.Text = "";
            _lblError.Visible = false;
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            _numMonth.BackColor = t.Surface;
            _numMonth.ForeColor = t.Text;
            _numYear.BackColor = t.Surface;
            _numYear.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
