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
    /// Hộp thoại Thêm / Sửa / Xem Danh mục tài khoản ngân hàng (DTAIKHOANNGANHANG, docs/specs/phieu-thu-chi.md §3).
    /// Kiểm trùng tên (UNICODE_CI_AI).
    /// </summary>
    internal sealed class BankAccountEditDialog : ThemedDialog
    {
        private readonly UserSession _session;
        private readonly BankAccountRepository _repo;
        private readonly bool _readOnly;

        private BankAccountRow _row;
        private bool _isNew;
        private bool _loadFailed;

        private readonly TextBox _name = new TextBox { MaxLength = VoucherConstants.MaxTextLength };
        private readonly TextBox _note = new TextBox { Multiline = true, MaxLength = VoucherConstants.MaxTextLength, ScrollBars = ScrollBars.Vertical };
        private readonly Label _lblError = new Label { AutoSize = false, Visible = false, UseMnemonic = false };

        private readonly FlatButton _btnSave;
        private readonly FlatButton _btnSaveNew;
        private readonly FlatButton _btnClose;

        public string SavedId { get; private set; }
        public bool AnySaved { get; private set; }

        // Hook kiểm thử UI (selftest).
        internal bool SuppressPopup { get; set; }
        internal string LastErrorForTest { get; private set; }
        internal bool IsReadOnlyForTest { get { return _readOnly; } }
        internal TextBox NameBoxForTest { get { return _name; } }
        internal TextBox NoteBoxForTest { get { return _note; } }
        internal FlatButton SaveButtonForTest { get { return _btnSave; } }
        internal FlatButton SaveNewButtonForTest { get { return _btnSaveNew; } }

        public BankAccountEditDialog(UserSession session, string accountId, bool readOnly = false)
            : base("TÀI KHOẢN NGÂN HÀNG - THÊM MỚI", "Thông tin tài khoản ngân hàng", 480, 260)
        {
            _session = session ?? AppSession.Current;
            _repo = new BankAccountRepository(_session.DatabasePath);
            _isNew = string.IsNullOrEmpty(accountId);
            _readOnly = readOnly || (_isNew
                ? !VoucherRules.CanBankAccountCatalog(_session, RoleModes.Add)
                : !VoucherRules.CanBankAccountCatalog(_session, RoleModes.Edit));

            FitBody(480, 152);
            Body.AutoScroll = false;

            _btnClose = AddButton("Thoát", null, delegate { Close(); });
            _btnSaveNew = AddButton("Lưu & Mới", null, delegate { Save(true); });
            _btnSave = AddButton("Lưu", "primary", delegate { Save(false); });
            CancelButton = _btnClose;

            BuildFields();

            if (_isNew)
                InitNewRecord();
            else
                LoadExistingRecord(accountId);

            ApplyState();
            ApplyDialogTheme();

            Shown += delegate
            {
                if (_loadFailed)
                {
                    ShowError(VoucherConstants.BankNotFound);
                    Close();
                    return;
                }
                if (!_readOnly)
                {
                    _name.Focus();
                    _name.SelectAll();
                }
            };
        }

        private void BuildFields()
        {
            float s = Dpi;
            int tab = 0;
            PlaceRow("Tên tài khoản *", _name, 14, 26, ref tab, s);
            PlaceRow("Ghi chú", _note, 52, 62, ref tab, s);

            _lblError.SetBounds((int)(14 * s), (int)(122 * s), (int)(452 * s), (int)(22 * s));
            _lblError.TextAlign = ContentAlignment.MiddleLeft;
            Body.Controls.Add(_lblError);
        }

        private void PlaceRow(string caption, Control editor, int y, int h, ref int tabIndex, float s)
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
            lbl.SetBounds((int)(14 * s), (int)(y * s), (int)(120 * s), (int)(Math.Min(h, 26) * s));
            editor.SetBounds((int)(138 * s), (int)(y * s), (int)(328 * s), (int)(h * s));
            editor.TabIndex = tabIndex++;
            Body.Controls.Add(lbl);
            Body.Controls.Add(editor);
        }

        private void InitNewRecord()
        {
            _isNew = true;
            _row = new BankAccountRow { Status = 30 };
            SetTitle("TÀI KHOẢN NGÂN HÀNG - THÊM MỚI");
            _name.Text = "";
            _note.Text = "";
            ClearError();
        }

        private void LoadExistingRecord(string accountId)
        {
            _isNew = false;
            _row = _repo.LoadById(accountId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = new BankAccountRow();
                return;
            }
            SetTitle(_readOnly ? "TÀI KHOẢN NGÂN HÀNG - XEM CHI TIẾT" : "TÀI KHOẢN NGÂN HÀNG - SỬA");
            _name.Text = _row.Name ?? "";
            _note.Text = _row.Note ?? "";
            ClearError();
        }

        private void ApplyState()
        {
            _name.ReadOnly = _readOnly;
            _note.ReadOnly = _readOnly;
            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly;
            SubtitleText = _readOnly
                ? "Chế độ chỉ xem"
                : "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Control | Keys.L))
                {
                    Save(false);
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S))
                {
                    Save(true);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Save(bool saveAndNew)
        {
            if (_readOnly)
                return;

            _row.Name = (_name.Text ?? "").Trim();
            _row.Note = (_note.Text ?? "").Trim();

            SimpleCatalogSaveResult res;
            try
            {
                res = _repo.Save(_row, _session.UserId ?? "admin");
            }
            catch (DataAccessException ex)
            {
                ShowError("Không ghi được dữ liệu: " + ex.Message);
                return;
            }

            if (!res.Ok)
            {
                ShowError(res.ErrorMessage);
                _name.Focus();
                _name.SelectAll();
                return;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;

            if (saveAndNew)
            {
                InitNewRecord();
                ApplyState();
                _name.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowError(string message)
        {
            LastErrorForTest = message;
            _lblError.Text = message ?? "";
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
            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }
    }
}
