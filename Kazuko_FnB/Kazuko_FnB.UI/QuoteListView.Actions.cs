using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần thao tác dữ liệu, menu ngữ cảnh, phím tắt và áp theme cho QuoteListView.</summary>
    internal sealed partial class QuoteListView
    {
        private void BuildGridMenu()
        {
            _gridMenu = new ThemedContextMenu();
            ToolStripMenuItem add = _gridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddQuote(); });
            ToolStripMenuItem edit = _gridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditQuote(); });
            ToolStripMenuItem del = _gridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteQuote(); });
            _gridMenu.AddSeparator();
            _gridMenu.AddItem("Làm mới", delegate { Reload(); });
            _gridMenu.AddItem("In danh sách", delegate { ShowInfo(QuoteText.Unsupported); });
            _gridMenu.AddSeparator();
            ToolStripMenuItem copy = _gridMenu.AddItem("Sao chép", null);
            var copyCell = new ToolStripMenuItem("Sao chép ô") { Font = _gridMenu.Font };
            copyCell.Click += delegate { DetailGridMenu.CopyCell(_grid); };
            var copySel = new ToolStripMenuItem("Sao chép vùng chọn") { Font = _gridMenu.Font };
            copySel.Click += delegate { DetailGridMenu.CopySelection(_grid); };
            copy.DropDownItems.Add(copyCell);
            copy.DropDownItems.Add(copySel);
            ToolStripMenuItem columns = _gridMenu.AddItem("Cột hiển thị", null);
            _gridMenu.AddItem("Tự động dãn cột", delegate { DetailGridMenu.AutoFit(_grid); });

            _gridMenu.Opening += delegate
            {
                bool hasRow = CurrentQuoteId() != null;
                add.Enabled = _service.CanAdd;
                edit.Enabled = _service.CanEdit && hasRow;
                del.Enabled = _service.CanRemove && hasRow;
                copy.Enabled = _grid.CurrentCell != null;
                DetailGridMenu.FillColumnsMenu(columns, _grid, _gridMenu.Font);
            };
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0)
            {
                _grid.ClearSelection();
                int col = hit.ColumnIndex >= 0 ? hit.ColumnIndex : 0;
                _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[col];
                _grid.Rows[hit.RowIndex].Selected = true;
            }
            else
            {
                _grid.ClearSelection();
            }
            _gridMenu.Show(_grid, e.Location);
        }

        private void PositionEmpty()
        {
            _empty.SetBounds(0, _grid.Height / 2 - 15, _grid.Width, 30);
        }

        private void DrawRowNumber(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_grid.RowHeadersVisible)
                return;
            string text = (e.RowIndex + 1).ToString(CultureInfo.InvariantCulture);
            var bounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, _grid.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, text, _grid.Font, bounds, ThemeManager.Current.TextMuted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private QuoteFilter CurrentFilter()
        {
            string custId = _customer.SelectedId;
            if (string.IsNullOrEmpty(custId))
                custId = null;
            return new QuoteFilter
            {
                FromDate = _dtFrom.Value.Date,
                ToDate = _dtTo.Value.Date,
                NumberText = _txtNumber.Text,
                CustomerId = custId
            };
        }

        public void Reload()
        {
            _loading = true;
            try
            {
                _rows = _service.LoadQuotes(CurrentFilter());
                _empty.Visible = _rows.Count == 0;
                _empty.Text = _service.CanView
                    ? "Không có phiếu báo giá nào trong khoảng lọc."
                    : QuoteText.NoPermission;
            }
            catch (DataAccessException ex)
            {
                _rows = new List<QuoteListRow>();
                _empty.Visible = true;
                _empty.Text = "Không đọc được dữ liệu: " + ex.Message;
            }
            FillGrid();
            _count.Text = _rows.Count + " phiếu";
            _loading = false;
            UpdateButtons();
            UpdateInfo();
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();
            foreach (QuoteListRow row in _rows)
            {
                int i = _grid.Rows.Add(
                    row.Ngay, row.Name, row.TenKhach, row.DiaChi, row.DienThoai,
                    row.TienHang, row.TienGiamGia, row.TongCong,
                    row.Email, row.TienThue, row.PhiVanChuyen, row.Note);
                _grid.Rows[i].Tag = row;
            }
            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
        }

        private List<string> RowIds()
        {
            var ids = new List<string>(_rows.Count);
            foreach (QuoteListRow r in _rows)
                ids.Add(r.Id);
            return ids;
        }

        private QuoteListRow CurrentQuoteRow()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as QuoteListRow;
        }

        private string CurrentQuoteId()
        {
            QuoteListRow row = CurrentQuoteRow();
            return row == null ? null : row.Id;
        }

        private void UpdateInfo()
        {
            QuoteListRow row = CurrentQuoteRow();
            _lblCreated.Text = row == null ? "" : FormatAuditTime(row.CreatedAt);
            _lblCreatedBy.Text = row == null ? "" : (row.CreatedByName ?? "");
            _lblModified.Text = row == null ? "" : FormatAuditTime(row.ModifiedAt);
            _lblModifiedBy.Text = row == null ? "" : (row.ModifiedByName ?? "");

            _detailGrid.Rows.Clear();
            if (row == null || string.IsNullOrEmpty(row.Id))
                return;
            try
            {
                List<QuoteLine> lines = _service.LoadLines(row.Id);
                foreach (QuoteLine l in lines)
                    _detailGrid.Rows.Add(l.ProductCode, l.ProductName, l.UnitName, l.Quantity, l.Price, l.DiscountPct, l.Amount, l.Note);
            }
            catch (DataAccessException)
            {
                // Lưới chi tiết giữ trống nếu đọc lỗi.
            }
        }

        private static string FormatAuditTime(DateTime? t)
        {
            return t.HasValue ? t.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "";
        }

        private void AddQuote()
        {
            if (!_service.CanAdd)
            {
                ShowWarning(QuoteText.NoPermission);
                return;
            }
            using (var dlg = new QuoteEditDialog(_session, null, RowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void EditQuote()
        {
            if (!_service.CanEdit)
            {
                ShowWarning(QuoteText.NoPermission);
                return;
            }
            string id = CurrentQuoteId();
            if (string.IsNullOrEmpty(id))
                return;
            using (var dlg = new QuoteEditDialog(_session, id, RowIds()))
            {
                dlg.ShowDialog(this);
                if (dlg.SavedId != null || dlg.Deleted)
                    ReloadAndSelect(dlg.SavedId);
            }
        }

        private void DeleteQuote()
        {
            if (!_service.CanRemove)
            {
                ShowWarning(QuoteText.NoPermission);
                return;
            }
            QuoteListRow row = CurrentQuoteRow();
            if (row == null || string.IsNullOrEmpty(row.Id))
                return;
            if (!SuppressPopup && !Msg.Confirm(this, QuoteText.DeleteConfirm))
                return;
            QuoteDeleteResult result;
            try
            {
                result = _service.DeleteQuote(row.Id, row.Name);
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
            ReloadAndSelect(null);
        }

        private void ReloadAndSelect(string id)
        {
            Reload();
            if (string.IsNullOrEmpty(id))
                return;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var row = r.Tag as QuoteListRow;
                if (row != null && row.Id == id)
                {
                    r.Selected = true;
                    _grid.CurrentCell = r.Cells[0];
                    UpdateInfo();
                    break;
                }
            }
        }

        private void UpdateButtons()
        {
            bool hasRow = CurrentQuoteId() != null;
            _btnAdd.Enabled = _service.CanAdd;
            _btnEdit.Enabled = _service.CanEdit && hasRow;
            _btnRemove.Enabled = _service.CanRemove && hasRow;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            bool inEditor = sender is TextBox || sender is ComboBox || sender is DateTimePicker;
            if (e.KeyCode == Keys.F3)
            {
                Reload();
                _txtNumber.Inner.Focus();
                _txtNumber.Inner.SelectAll();
                e.Handled = true;
                return;
            }
            if (inEditor)
                return;
            if (e.KeyCode == Keys.Insert && _service.CanAdd)
            {
                AddQuote();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4)
            {
                EditQuote();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete && _service.CanRemove)
            {
                DeleteQuote();
                e.Handled = true;
            }
        }

        public void Preview(string option)
        {
            if (string.IsNullOrEmpty(option))
                return;
            if (option.StartsWith("from=", StringComparison.OrdinalIgnoreCase))
            {
                _dtFrom.Inner.Text = option.Substring(5);
                _dtFrom.Commit();
                Reload();
            }
            else if (option.StartsWith("to=", StringComparison.OrdinalIgnoreCase))
            {
                _dtTo.Inner.Text = option.Substring(3);
                _dtTo.Commit();
                Reload();
            }
            else if (option.StartsWith("select=", StringComparison.OrdinalIgnoreCase))
            {
                int idx;
                if (int.TryParse(option.Substring(7), out idx) && idx >= 0 && idx < _grid.Rows.Count)
                {
                    _grid.ClearSelection();
                    _grid.Rows[idx].Selected = true;
                    _grid.CurrentCell = _grid.Rows[idx].Cells[0];
                    UpdateInfo();
                }
            }
        }

        private void ShowInfo(string message)
        {
            LastInfoMessageForTest = message;
            if (!SuppressPopup)
                Msg.Info(this, message);
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplyTheme(ThemeManager.Current);
        }

        public void ApplyTheme(ThemeTokens t)
        {
            ThemeApplier.Apply(this, t);
            BackColor = t.Background;
            _bar.BackColor = t.Background;
            _count.BackColor = Color.Transparent;
            _count.ForeColor = t.TextMuted;
            _empty.BackColor = _grid.BackgroundColor;
            _empty.ForeColor = t.TextMuted;
            _split.BackColor = Draw.StrongBorder(t);
            _split.Panel1.BackColor = t.Background;
            _split.Panel2.BackColor = t.Background;
            _rightSplit.BackColor = Draw.StrongBorder(t);
            _rightSplit.Panel1.BackColor = t.Background;
            _rightSplit.Panel2.BackColor = t.Background;
            foreach (FieldFrame f in _frames)
                f.ApplyTheme(t, t.Background);
            _infoTabs.BackColor = t.Background;
            _infoTabs.ForeColor = t.Text;
            foreach (TabPage p in _infoTabs.TabPages)
                p.BackColor = t.Background;
            ThemeApplier.ApplyGrid(_grid, t);
            ThemeApplier.ApplyGrid(_detailGrid, t);
            UpdateButtons();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                if (_gridMenu != null)
                    _gridMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
