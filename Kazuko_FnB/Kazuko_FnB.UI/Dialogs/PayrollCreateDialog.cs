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
    /// mặc định tháng/năm hiện tại, Số phiếu mặc định "Tháng m/yyyy" sửa tay được, ghi chú tùy chọn;
    /// hỗ trợ Lưu, Lưu &amp; Mới, Lưu &amp; In (mở <see cref="PayrollPreviewForm"/>) và hỏi xác nhận khi thoát có thay đổi.
    /// </summary>
    internal sealed class PayrollCreateDialog : ThemedDialog
    {
        private const int BodyWidth96 = 480;
        private const int BodyHeight96 = 188;

        private readonly PayrollService _service;
        private readonly UserSession _session;

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
        private readonly TextBox _txtName = new TextBox { MaxLength = PayrollText.MaxTextLength };
        private readonly TextBox _txtNote = new TextBox { MaxLength = PayrollText.MaxTextLength };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnSavePrint;
        private readonly FlatButton _btnClose;

        private bool _loading;
        private bool _updatingAutoName;
        private bool _nameEditedByUser;
        private bool _closingAfterSave;
        private string _original = "";

        public PayrollPeriodRow CreatedPeriod { get; private set; }
        public bool AnySaved { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool UnsavedPromptTriggeredForTest { get; private set; }
        internal Func<string, bool> ConfirmDiscardOverrideForTest { get; set; }
        internal Action<PayrollPreviewForm, PayrollPeriodRow> PrintPreviewOpenerForTest { get; set; }
        internal bool IsDirtyForTest { get { return IsDirty; } }
        internal NumberBox MonthBoxForTest { get { return _numMonth; } }
        internal NumberBox YearBoxForTest { get { return _numYear; } }
        internal TextBox NameBoxForTest { get { return _txtName; } }
        internal TextBox NoteBoxForTest { get { return _txtNote; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }
        internal FlatButton SavePrintButtonForTest { get { return _btnSavePrint; } }
        internal FlatButton CloseButtonForTest { get { return _btnClose; } }

        public PayrollCreateDialog(PayrollService service, UserSession session = null)
            : base("THÊM BẢNG LƯƠNG", "Chọn tháng, năm và số phiếu cho bảng lương mới", BodyWidth96, 304)
        {
            if (service == null)
                throw new ArgumentNullException("service");
            _service = service;
            _session = session ?? AppSession.Current;

            FitBody(BodyWidth96, BodyHeight96);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSavePrint = AddButton("Lưu & In", null, delegate { DoSaveAndPrint(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { DoSaveAndNew(); });
            _btnSave = AddButton("Lưu", "primary", delegate { DoSave(); });
            CompactFooter();
            CancelButton = _btnClose;

            BuildFields();
            ResetToDefault();

            _numMonth.ValueChanged += OnPeriodFieldChanged;
            _numYear.ValueChanged += OnPeriodFieldChanged;
            _txtName.TextChanged += OnNameTextChanged;
            _txtNote.TextChanged += delegate { if (!_loading) ClearError(); };
            FormClosing += OnFormClosing;

            ApplyDialogTheme();
            Shown += delegate { _numMonth.Focus(); };
        }

        private void BuildFields()
        {
            float s = Dpi;
            string[] captions = { "Tháng *", "Năm *", "Số phiếu *", "Ghi chú" };
            int labelW = (int)(108 * s);
            foreach (string cap in captions)
            {
                int w = TextRenderer.MeasureText(cap, Font).Width + (int)(12 * s);
                if (w > labelW) labelW = w;
            }

            int left = (int)(14 * s);
            int gap = (int)(8 * s);
            int editX = left + labelW + gap;
            int editW = Math.Max((int)(200 * s), Body.ClientSize.Width - editX - left);
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

        private void ResetToDefault()
        {
            _loading = true;
            try
            {
                DateTime today = DateTime.Today;
                _numMonth.Value = today.Month;
                _numYear.Value = Math.Min(PayrollText.MaxYear, Math.Max(PayrollText.MinYear, today.Year));
                _nameEditedByUser = false;
                UpdateGeneratedName();
                _txtNote.Text = "";
                ClearError();
            }
            finally
            {
                _loading = false;
            }
            _original = Snapshot();
        }

        private void OnPeriodFieldChanged(object sender, EventArgs e)
        {
            if (_loading)
                return;
            ClearError();
            if (!_nameEditedByUser)
                UpdateGeneratedName();
        }

        private void OnNameTextChanged(object sender, EventArgs e)
        {
            if (_loading || _updatingAutoName)
                return;
            ClearError();
            _nameEditedByUser = true;
        }

        private void UpdateGeneratedName()
        {
            int month = (int)_numMonth.Value;
            int year = (int)_numYear.Value;
            _updatingAutoName = true;
            try
            {
                _txtName.Text = PayrollText.FormatPeriodName(month, year);
            }
            finally
            {
                _updatingAutoName = false;
            }
        }

        private string Snapshot()
        {
            return ((int)_numMonth.Value).ToString()
                + "|" + ((int)_numYear.Value).ToString()
                + "|" + (_txtName.Text ?? "")
                + "|" + (_txtNote.Text ?? "");
        }

        private bool IsDirty
        {
            get { return Snapshot() != _original; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter || keyData == (Keys.Control | Keys.L))
            {
                DoSave();
                return true;
            }
            if (keyData == (Keys.Control | Keys.S))
            {
                DoSaveAndNew();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool SaveCore()
        {
            int month = (int)_numMonth.Value;
            int year = (int)_numYear.Value;
            string name = (_txtName.Text ?? "").Trim();
            string note = (_txtNote.Text ?? "").Trim();

            string nameErr = PayrollRules.ValidatePeriodName(name);
            if (nameErr != null)
            {
                ShowError(nameErr);
                _txtName.Focus();
                return false;
            }

            PayrollCreateResult res;
            try
            {
                res = _service.CreatePeriod(month, year, name, note);
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
            AnySaved = true;
            return true;
        }

        internal bool DoSave()
        {
            if (!SaveCore())
                return false;
            _closingAfterSave = true;
            DialogResult = DialogResult.OK;
            Close();
            return true;
        }

        internal bool DoSaveAndNew()
        {
            if (!SaveCore())
                return false;
            ResetToDefault();
            _numMonth.Focus();
            return true;
        }

        internal bool DoSaveAndPrint()
        {
            if (!SaveCore())
                return false;

            PayrollPeriodRow created = CreatedPeriod;
            if (created != null && _session != null)
            {
                using (var preview = new PayrollPreviewForm(_session, _service.Mode))
                {
                    preview.Text = "Xem bảng lương — " + (created.Name ?? "");
                    preview.SelectPeriod(created.Id);
                    if (PrintPreviewOpenerForTest != null)
                        PrintPreviewOpenerForTest(preview, created);
                    else
                        preview.ShowDialog(this);
                }
            }

            _closingAfterSave = true;
            DialogResult = DialogResult.OK;
            Close();
            return true;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_closingAfterSave || !IsDirty)
                return;

            UnsavedPromptTriggeredForTest = true;
            bool discard;
            if (ConfirmDiscardOverrideForTest != null)
                discard = ConfirmDiscardOverrideForTest(PayrollText.DiscardChangesConfirm);
            else if (SuppressPopup)
                discard = true;
            else
                discard = Msg.Confirm(this, PayrollText.DiscardChangesConfirm);

            if (!discard)
                e.Cancel = true;
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
            _txtName.BackColor = t.Surface;
            _txtName.ForeColor = t.Text;
            _txtNote.BackColor = t.Surface;
            _txtNote.ForeColor = t.Text;
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
