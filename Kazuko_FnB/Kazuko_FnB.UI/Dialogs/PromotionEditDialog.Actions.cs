using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần nạp dữ liệu, kiểm tra thay đổi, lưu/phân quyền/phím tắt và bảng màu của <see cref="PromotionEditDialog"/>.</summary>
    internal sealed partial class PromotionEditDialog
    {
        private void LoadTypes()
        {
            _types = _service.LoadTypes();
            _cboType.Items.Clear();
            foreach (PromotionTypeItem item in _types)
                _cboType.Items.Add(item);
        }

        private void InitNewRecord()
        {
            _isNew = true;
            _existingDetailCount = 0;
            _row = PromotionRow.NewDefault();
            _row.FromDate = _service.GetServerDate();
            _details = new List<PromotionDetailRow>();
            if (_types.Count > 0)
            {
                _row.TypeId = _types[0].Id;
                _row.TypeName = _types[0].Name;
            }
            SetTitle("ĐỢT KHUYẾN MẠI - THÊM MỚI");
            BindRowToControls();
        }

        private void LoadExistingRecord(string promotionId)
        {
            _isNew = false;
            _row = _service.LoadById(promotionId);
            if (_row == null)
            {
                _loadFailed = true;
                _row = PromotionRow.NewDefault();
                _details = new List<PromotionDetailRow>();
                return;
            }
            _details = _service.LoadDetails(promotionId);
            _existingDetailCount = Math.Max(_row.DetailCount, _details.Count);
            SetTitle(_readOnly ? "ĐỢT KHUYẾN MẠI - XEM CHI TIẾT" : "ĐỢT KHUYẾN MẠI - SỬA");
            BindRowToControls();
        }

        private void BindRowToControls()
        {
            _loading = true;
            try
            {
                _txtName.Text = _row.Name ?? "";
                SelectTypeById(_row.TypeId);
                _dtFrom.NullableValue = _row.FromDate;
                _dtTo.NullableValue = _row.ToDate;
                _chkStopped.Checked = _row.IsStopped;
                _txtNote.Text = _row.Note ?? "";
                decimal pct = _row.DiscountPercent;
                if (pct < 0m) pct = 0m;
                if (pct > 100m) pct = 100m;
                _numHeaderPercent.Value = pct;
                SwitchDetailSection(CurrentKind());
                ClearError();
            }
            finally
            {
                _loading = false;
            }
        }

        private void SelectTypeById(string typeId)
        {
            for (int i = 0; i < _cboType.Items.Count; i++)
            {
                var item = _cboType.Items[i] as PromotionTypeItem;
                if (item != null && string.Equals(item.Id, typeId, StringComparison.Ordinal))
                {
                    _cboType.SelectedIndex = i;
                    return;
                }
            }
            _cboType.SelectedIndex = -1;
        }

        private void HookHeaderEvents()
        {
            _txtName.TextChanged += delegate { OnFieldChanged(); };
            _dtFrom.ValueChanged += delegate { OnFieldChanged(); };
            _dtTo.ValueChanged += delegate { OnFieldChanged(); };
            _chkStopped.CheckedChanged += delegate { OnFieldChanged(); };
            _txtNote.TextChanged += delegate { OnFieldChanged(); };
            _numHeaderPercent.ValueChanged += delegate { OnFieldChanged(); };
            _cboType.SelectedIndexChanged += delegate
            {
                if (_loading)
                    return;
                _details.Clear();
                SwitchDetailSection(CurrentKind());
                OnFieldChanged();
            };
        }

        private void OnFieldChanged()
        {
            if (_loading)
                return;
            ClearError();
            UpdateButtons();
        }

        private void ApplyPermissions()
        {
            bool lockType = _readOnly || (!_isNew && _existingDetailCount > 0);
            _txtName.ReadOnly = _readOnly;
            _cboType.Enabled = !lockType;
            _dtFrom.Enabled = !_readOnly;
            _dtFrom.Inner.ReadOnly = _readOnly;
            _dtTo.Enabled = !_readOnly;
            _dtTo.Inner.ReadOnly = _readOnly;
            _chkStopped.Enabled = !_readOnly;
            _txtNote.ReadOnly = _readOnly;
            _numHeaderPercent.ReadOnly = _readOnly;
            _numHeaderPercent.Enabled = !_readOnly;
            _detailGrid.ReadOnly = _readOnly;

            ConfigureDetailToolbarForKind(CurrentKind());
            UpdateDetailToolbarButtons();

            _btnSave.Visible = !_readOnly;
            _btnSaveNew.Visible = !_readOnly && _service.CanAdd;
            _btnSaveClose.Visible = !_readOnly;

            if (_readOnly)
                SubtitleText = "Chế độ chỉ xem (không có quyền sửa)";
            else if (!_isNew && _existingDetailCount > 0)
                SubtitleText = "Đã có chi tiết nên không đổi loại hình (Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
            else
                SubtitleText = "(Ctrl+L: Lưu, Ctrl+S: Lưu & Mới, ESC: Thoát)";
        }

        private string Snapshot()
        {
            PromotionTypeItem t = SelectedTypeItem();
            var sb = new StringBuilder();
            sb.Append(_txtName.Text).Append('|');
            sb.Append(t == null ? "" : t.Id).Append('|');
            sb.Append(_dtFrom.NullableValue.HasValue ? _dtFrom.NullableValue.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "").Append('|');
            sb.Append(_dtTo.NullableValue.HasValue ? _dtTo.NullableValue.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "").Append('|');
            sb.Append(_chkStopped.Checked ? "1" : "0").Append('|');
            sb.Append(_numHeaderPercent.Value.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(_txtNote.Text).Append('|');
            foreach (PromotionDetailRow d in _details)
            {
                sb.Append(d.ProductGroupId).Append(',')
                  .Append(d.ProductId).Append(',')
                  .Append(d.GiftProductId).Append(',')
                  .Append(d.DiscountPercent.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(d.SalePrice.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(d.OrderValue.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(d.ItemQuantity.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(d.BuyQuantity.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(d.GiftQuantity.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(d.Note).Append(';');
            }
            return sb.ToString();
        }

        private bool Dirty
        {
            get { return !_readOnly && Snapshot() != _original; }
        }

        private void UpdateButtons()
        {
            if (_readOnly)
                return;
            bool canSave = Dirty || _isNew;
            _btnSave.Enabled = canSave;
            _btnSaveNew.Enabled = canSave && _service.CanAdd;
            _btnSaveClose.Enabled = canSave;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_readOnly)
            {
                if (keyData == (Keys.Control | Keys.L))
                {
                    if (_btnSave.Enabled) SaveStay();
                    return true;
                }
                if (keyData == (Keys.Control | Keys.S))
                {
                    if (_btnSaveNew.Visible && _btnSaveNew.Enabled) SaveAndNew();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void SaveStay() { DoSave(); }

        private void SaveAndNew()
        {
            if (!DoSave())
                return;
            InitNewRecord();
            ApplyPermissions();
            ApplyDialogTheme();
            _original = Snapshot();
            UpdateButtons();
            _txtName.Focus();
        }

        private void SaveAndClose()
        {
            if (!DoSave())
                return;
            DialogResult = DialogResult.OK;
            Close();
        }

        internal bool DoSave()
        {
            if (_readOnly)
                return false;

            _detailGrid.EndEdit();
            _dtFrom.Commit();
            _dtTo.Commit();

            PromotionTypeItem typeItem = SelectedTypeItem();
            _row.Name = _txtName.Text ?? "";
            _row.TypeId = typeItem == null ? null : typeItem.Id;
            _row.TypeName = typeItem == null ? "" : typeItem.Name;
            _row.FromDate = _dtFrom.NullableValue;
            _row.ToDate = _dtTo.NullableValue;
            _row.IsStopped = _chkStopped.Checked;
            _row.Note = _txtNote.Text ?? "";
            _row.DiscountPercent = CurrentKind() == PromotionKind.TotalBillPercent ? _numHeaderPercent.Value : 0m;

            PromotionSaveResult res;
            try
            {
                res = _service.Save(_row, _details);
            }
            catch (DataAccessException ex)
            {
                ShowValidationError("Không ghi được dữ liệu: " + ex.Message);
                return false;
            }

            if (!res.Ok)
            {
                ShowValidationError(res.ErrorMessage);
                if (res.HasNameError && !_txtName.ReadOnly) { _txtName.Focus(); _txtName.SelectAll(); }
                else if (res.HasTypeError && _cboType.Enabled) { _cboType.Focus(); }
                else if (res.HasDateError && _dtTo.Enabled) { _dtTo.Focus(); }
                else if (res.HasDiscountError && _numHeaderPercent.Enabled) { _numHeaderPercent.Focus(); }
                else if (res.HasDetailError && res.ErrorLineIndex >= 0 && res.ErrorLineIndex < _detailGrid.Rows.Count)
                {
                    _detailGrid.CurrentCell = _detailGrid.Rows[res.ErrorLineIndex].Cells[0];
                    _detailGrid.Focus();
                }
                return false;
            }

            ClearError();
            SavedId = res.SavedId;
            _row.Id = res.SavedId;
            AnySaved = true;
            _isNew = false;
            _existingDetailCount = CurrentKind() == PromotionKind.TotalBillPercent ? 0 : _details.Count;
            SetTitle("ĐỢT KHUYẾN MẠI - SỬA");
            ApplyPermissions();
            ApplyDialogTheme();
            _original = Snapshot();
            UpdateButtons();
            return true;
        }

        private void ShowValidationError(string message)
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

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_readOnly || DialogResult == DialogResult.OK || !Dirty || SuppressPopup)
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

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            Color disabledBack = Draw.Mix(t.Background, t.TextMuted, 0.14f);

            ApplyDateTheme(_dtFrom, t, disabledBack);
            ApplyDateTheme(_dtTo, t, disabledBack);

            _txtName.BackColor = _txtName.ReadOnly ? disabledBack : t.Surface;
            _cboType.BackColor = _cboType.Enabled ? t.Surface : disabledBack;
            _txtNote.BackColor = _txtNote.ReadOnly ? disabledBack : t.Surface;
            _numHeaderPercent.BackColor = _numHeaderPercent.Enabled && !_numHeaderPercent.ReadOnly ? t.Surface : disabledBack;

            _lblError.BackColor = Color.Transparent;
            _lblError.ForeColor = t.Danger;
        }

        private static void ApplyDateTheme(DateBox box, ThemeTokens t, Color disabledBack)
        {
            box.ApplyTheme(t, t.Background);
            if (!box.Enabled)
            {
                box.FieldColor = disabledBack;
                box.Inner.BackColor = disabledBack;
                box.Invalidate();
            }
        }
    }
}
