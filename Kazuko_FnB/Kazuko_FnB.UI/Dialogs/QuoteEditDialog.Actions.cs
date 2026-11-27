using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Phần khách hàng, tính tổng, lưu/xóa, điều hướng, phím tắt và áp theme của QuoteEditDialog.</summary>
    internal sealed partial class QuoteEditDialog
    {
        private void OnCustomerChanged()
        {
            if (_loading)
                return;
            string id = _customer.SelectedId;
            if (string.IsNullOrEmpty(id))
            {
                _txtTenKhach.Text = "";
                _txtDiaChi.Text = "";
                _txtDienThoai.Text = "";
                _txtEmail.Text = "";
                EnableCustomerCtrl(true);
                OnDirtyChanged();
                return;
            }
            QuoteCustomerInfo c = null;
            try
            {
                c = _service.LoadCustomer(id);
            }
            catch (DataAccessException)
            {
                // Giữ nguyên nếu đọc lỗi.
            }
            if (c != null)
            {
                _txtTenKhach.Text = c.Name ?? "";
                _txtDiaChi.Text = c.Address ?? "";
                _txtDienThoai.Text = c.Phone ?? "";
                _txtEmail.Text = c.Email ?? "";
                EnableCustomerCtrl(false);
            }
            OnDirtyChanged();
        }

        private void EnableCustomerCtrl(bool enable)
        {
            bool canEditFields = enable && !_readOnly;
            foreach (TextBox tb in new[] { _txtTenKhach, _txtDiaChi, _txtDienThoai, _txtEmail })
            {
                tb.Enabled = canEditFields;
                tb.ReadOnly = !canEditFields;
            }
        }

        private void ApplyConfigAndPermissions()
        {
            bool taxEnabled = SystemConfig.CoThueSuat;
            _numTileThue.Enabled = taxEnabled && !_readOnly;
            _numTienThue.Enabled = taxEnabled;
            _numPhiVanChuyen.Enabled = !_readOnly;
            _numTileGiamGia.Enabled = !_readOnly;
            _numTienGiamGia.Enabled = !_readOnly;

            _dtNgay.Enabled = !_readOnly;
            _txtNumber.ReadOnly = _readOnly;
            _customer.Enabled = !_readOnly;
            EnableCustomerCtrl(string.IsNullOrEmpty(_customer.SelectedId));
            _txtNote.ReadOnly = _readOnly;

            _btnAddProduct.Enabled = !_readOnly;
            _numQty.Enabled = !_readOnly;
            _btnDeleteLine.Enabled = !_readOnly;
            _btnImportExcel.Enabled = !_readOnly;
            _detailGrid.ReadOnly = _readOnly;
        }

        private void RecalcTotals()
        {
            if (_loading)
                return;
            foreach (QuoteLine line in _lines)
                line.Amount = QuoteCalculator.CalcLineAmount(line.Quantity, line.Price, line.DiscountPct);

            QuoteTotals t = QuoteCalculator.CalcTotals(
                _lines,
                _numTileGiamGia.Value,
                _numTienGiamGia.Value,
                _discountByMoney,
                _numTileThue.Value,
                _numPhiVanChuyen.Value,
                _roundStep);
            _totals = t;
            _totalsDirty = true;

            _recalc = true;
            try
            {
                ConfigureDiscountPercentBounds(_discountByMoney);
                SetNumericDisplay(_numTienHang, t.TienHang);
                SetNumericDisplay(_numTileGiamGia, t.TileGiamGia);
                SetNumericDisplay(_numTienGiamGia, t.TienGiamGia);
                SetNumericDisplay(_numTienThue, t.TienThue);
                SetNumericDisplay(_numTongCong, t.TongCong);
            }
            finally
            {
                _recalc = false;
            }
        }

        private void ConfigureDiscountPercentBounds(bool byMoney)
        {
            if (byMoney)
            {
                _numTileGiamGia.Minimum = -999999999999m;
                _numTileGiamGia.Maximum = 999999999999m;
            }
            else
            {
                _numTileGiamGia.Minimum = 0m;
                _numTileGiamGia.Maximum = 100m;
            }
        }

        /// <summary>Gán giá trị hiển thị an toàn vào NumericUpDown, không làm ném ngoại lệ nếu vượt biên ô.</summary>
        private static void SetNumericDisplay(NumericUpDown num, decimal value)
        {
            if (value < num.Minimum)
                num.Value = num.Minimum;
            else if (value > num.Maximum)
                num.Value = num.Maximum;
            else
                num.Value = value;
        }

        private void CollectRow()
        {
            _row.Ngay = _dtNgay.Value.Date;
            _row.Name = (_txtNumber.Text ?? "").Trim();
            string custId = _customer.SelectedId;
            _row.CustomerId = string.IsNullOrEmpty(custId) ? null : custId;
            _row.TenKhach = (_txtTenKhach.Text ?? "").Trim();
            _row.DiaChi = (_txtDiaChi.Text ?? "").Trim();
            _row.DienThoai = (_txtDienThoai.Text ?? "").Trim();
            _row.Email = (_txtEmail.Text ?? "").Trim();
            _row.Note = _txtNote.Text ?? "";
            _row.TienHang = _totals.TienHang;
            _row.TileGiamGia = _totals.TileGiamGia;
            _row.TienGiamGia = _totals.TienGiamGia;
            _row.GiamTheoTien = _discountByMoney;
            _row.TileThue = _numTileThue.Value;
            _row.TienThue = _totals.TienThue;
            _row.PhiVanChuyen = _numPhiVanChuyen.Value;
            _row.TongCong = _totals.TongCong;
            _row.Loai = 0;
        }

        internal bool DoSave()
        {
            if (_readOnly)
            {
                ShowWarning(QuoteText.NoPermission);
                return false;
            }
            _detailGrid.EndEdit();
            if (_isNew || _totalsDirty)
                RecalcTotals();
            CollectRow();

            QuoteSaveResult result;
            try
            {
                result = _service.SaveQuote(_row, _lines);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không ghi được dữ liệu: " + ex.Message);
                return false;
            }
            if (!result.Ok)
            {
                ShowWarning(result.ErrorMessage);
                if (result.ErrorMessage == QuoteText.Required("Số phiếu")
                    || result.ErrorMessage == QuoteText.NumberDuplicate(_row.Name ?? ""))
                {
                    _txtNumber.Focus();
                    _txtNumber.SelectAll();
                }
                return false;
            }

            SavedId = result.SavedId;
            _row.Id = result.SavedId;
            bool wasNew = _isNew;
            _isNew = false;
            if (wasNew)
            {
                SetTitle("BÁO GIÁ - SỬA");
                _readOnly = !_service.CanEdit;
                ApplyConfigAndPermissions();
            }
            _totalsDirty = false;
            _original = Snapshot();
            UpdateSaveButtons();
            UpdateNavButtons();
            return true;
        }

        private void SaveStay() { DoSave(); }

        private void SaveAndNew()
        {
            if (!DoSave()) return;
            NewRecord();
            AfterRecordLoaded();
        }

        private void SaveAndClose()
        {
            if (!DoSave()) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DeleteCurrent()
        {
            if (_isNew || _readOnly || !_service.CanRemove)
                return;
            if (!SuppressPopup && !Msg.Confirm(this, QuoteText.DeleteConfirm))
                return;
            QuoteDeleteResult result;
            try
            {
                result = _service.DeleteQuote(_row.Id, _row.Name);
            }
            catch (DataAccessException ex)
            {
                ShowWarning("Không xóa được dữ liệu: " + ex.Message);
                return;
            }
            if (!result.Ok)
            {
                ShowWarning(result.ErrorMessage);
                return;
            }
            Deleted = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void NewBlank()
        {
            if (!_service.CanAdd)
                return;
            if (!_isNew && !ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi tạo báo giá mới không?"))
                return;
            NewRecord();
            AfterRecordLoaded();
        }

        private void Navigate(int dir)
        {
            if (_rowIds == null || _rowIds.Count == 0)
                return;
            int target = _rowIndex + dir;
            if (target < 0 || target >= _rowIds.Count)
                return;
            if (!ConfirmDiscardOrSave("Dữ liệu đã thay đổi. Bạn có muốn lưu trước khi chuyển sang báo giá khác không?"))
                return;
            _rowIndex = target;
            LoadRecord(_rowIds[target]);
            AfterRecordLoaded();
        }

        private void AfterRecordLoaded()
        {
            _original = Snapshot();
            ApplyConfigAndPermissions();
            UpdateSaveButtons();
            UpdateNavButtons();
        }

        private void OnDirtyChanged()
        {
            if (!_loading)
                UpdateSaveButtons();
        }

        private void UpdateSaveButtons()
        {
            bool dirty = Dirty;
            bool canPrintSaved = !_isNew && _row != null && !string.IsNullOrEmpty(_row.Id);
            bool canSavePrint = !_readOnly && (dirty || canPrintSaved);
            _btnSave.Enabled = dirty && !_readOnly;
            _btnSaveNew.Enabled = dirty && !_readOnly;
            _btnSaveClose.Enabled = dirty && !_readOnly;
            _btnSavePrint.Enabled = canSavePrint;
            _btnSaveViewPrint.Enabled = canSavePrint;
            _btnDelete.Enabled = !_isNew && _service.CanRemove && !_readOnly;
        }

        private void UpdateNavButtons()
        {
            _btnPrev.Enabled = _rowIds != null && _rowIndex > 0;
            _btnNext.Enabled = _rowIds != null && _rowIndex >= 0 && _rowIndex < _rowIds.Count - 1;
            _btnNew.Enabled = _service.CanAdd && !_readOnly;
        }

        private bool Dirty
        {
            get { return _original == null || Snapshot() != _original || _detailGrid.IsCurrentCellDirty; }
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append(_txtNumber.Text).Append('|');
            sb.Append(_dtNgay.Value.ToString("yyyyMMdd")).Append('|');
            sb.Append(_customer.SelectedId ?? "").Append('|');
            sb.Append(_txtTenKhach.Text).Append('|');
            sb.Append(_txtDienThoai.Text).Append('|');
            sb.Append(_txtDiaChi.Text).Append('|');
            sb.Append(_txtEmail.Text).Append('|');
            sb.Append(_txtNote.Text).Append('|');
            sb.Append(_numTileGiamGia.Value).Append('|');
            sb.Append(_numTienGiamGia.Value).Append('|');
            sb.Append(_discountByMoney).Append('|');
            sb.Append(_numTileThue.Value).Append('|');
            sb.Append(_numPhiVanChuyen.Value).Append('|');
            foreach (QuoteLine l in _lines)
            {
                sb.Append(l.ProductId).Append(':').Append(l.UnitId).Append(':');
                sb.Append(l.Quantity).Append(':').Append(l.Price).Append(':');
                sb.Append(l.DiscountPct).Append(':').Append(l.Note ?? "").Append(';');
            }
            return sb.ToString();
        }

        private bool ConfirmDiscardOrSave(string text)
        {
            if (!Dirty || SuppressPopup)
                return true;
            DialogResult answer = MessageBox.Show(this, text, "Xác nhận", MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (answer == DialogResult.Cancel) return false;
            if (answer == DialogResult.Yes) return DoSave();
            return true;
        }

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
            else if (e.Control && e.KeyCode == Keys.L) { SaveStay(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { SaveAndNew(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.N) { NewBlank(); e.Handled = true; }
            else if (e.KeyCode == Keys.F8 && _detailGrid.ContainsFocus) { DeleteLine(); e.Handled = true; }
            else if (e.KeyCode == Keys.F10) { Navigate(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.F11) { Navigate(1); e.Handled = true; }
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void ShowInfo(string message)
        {
            LastInfoMessageForTest = message;
            if (!SuppressPopup)
                Msg.Info(this, message);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            ThemeTokens t = ThemeManager.Current;
            _dtNgay.ApplyTheme(t, t.Background);
            if (_productGrid != null)
                ThemeApplier.ApplyGrid(_productGrid, t);
            if (_detailGrid != null)
                ThemeApplier.ApplyGrid(_detailGrid, t);
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
            int hdrW = TextRenderer.MeasureText(header ?? "", hdrFont).Width + (int)Math.Ceiling(26 * s);
            if (hdrW > w) w = hdrW;
            if (!string.IsNullOrEmpty(sample))
            {
                int sampleW = TextRenderer.MeasureText(sample, g.Font).Width + (int)Math.Ceiling(24 * s);
                if (sampleW > w) w = sampleW;
            }
            return w;
        }

        public static QuoteEditDialog CreateForPreview(UserSession session)
        {
            return new QuoteEditDialog(session, null, null, true)
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
                AfterRecordLoaded();
            }
        }
    }
}
