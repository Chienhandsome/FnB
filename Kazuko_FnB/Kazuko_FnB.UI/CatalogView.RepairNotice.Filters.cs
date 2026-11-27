using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần bộ lọc Từ/Đến ngày (để trống = tất cả) + "Chỉ chưa sửa xong", menu ngữ cảnh và bảng màu
    /// cho chế độ Thông báo sửa chữa của <see cref="CatalogView"/>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private void BuildRepairNoticeUi()
        {
            float s = DeviceDpi / 96f;
            _rnFilterBar.Height = (int)(44 * s);
            _lblRnFrom.Font = _count.Font;
            _lblRnTo.Font = _count.Font;
            _dtRnFrom.Inner.Font = _grid.Font;
            _dtRnTo.Inner.Font = _grid.Font;
            _chkRnUnfixedOnly.Font = _count.Font;
            _btnRnFilter.Font = _count.Font;

            _dtRnFrom.ValueChanged += delegate
            {
                if (_repairNoticeMode)
                    ReloadRepairNoticeRowsOnly();
            };
            _dtRnTo.ValueChanged += delegate
            {
                if (_repairNoticeMode)
                    ReloadRepairNoticeRowsOnly();
            };
            _chkRnUnfixedOnly.CheckedChanged += delegate
            {
                if (_repairNoticeMode)
                    ReloadRepairNoticeRowsOnly();
            };
            _btnRnFilter.Click += delegate
            {
                if (_repairNoticeMode)
                    ReloadRepairNoticeRowsOnly();
            };

            _rnFilterBar.Controls.Add(_lblRnFrom);
            _rnFilterBar.Controls.Add(_dtRnFrom);
            _rnFilterBar.Controls.Add(_lblRnTo);
            _rnFilterBar.Controls.Add(_dtRnTo);
            _rnFilterBar.Controls.Add(_chkRnUnfixedOnly);
            _rnFilterBar.Controls.Add(_btnRnFilter);
            _rnFilterBar.Resize += delegate { LayoutRepairNoticeFilterBar(); };
            _split.Panel2.Controls.Add(_rnFilterBar);

            _rnGridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddRepairNotice(); });
            _rnGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditRepairNotice(); });
            _rnGridMenu.AddItem("Xem chi tiết", delegate { OpenRepairNoticeFromGrid(); });
            _rnGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteRepairNotice(); });
            _rnGridMenu.AddSeparator();
            _rnGridMenu.AddItem("Refresh", delegate { Reload(); });
            _rnGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _rnGridMenu.AddSeparator();
            _rnGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _rnGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _rnGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _rnGridMenu.Opening += delegate { UpdateRepairNoticeGridMenuState(); };
        }

        private void LayoutRepairNoticeFilterBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(10 * s);
            int h = _rnFilterBar.ClientSize.Height;
            int ctrlH = (int)(30 * s);
            int y = Math.Max(0, (h - ctrlH) / 2);

            int fromLblW = TextRenderer.MeasureText(_lblRnFrom.Text, _lblRnFrom.Font).Width + (int)(8 * s);
            int toLblW = TextRenderer.MeasureText(_lblRnTo.Text, _lblRnTo.Font).Width + (int)(8 * s);
            int dateW = (int)(135 * s);
            Size chkSz = _chkRnUnfixedOnly.GetPreferredSize(Size.Empty);
            int btnW = TextRenderer.MeasureText(_btnRnFilter.Text, _btnRnFilter.Font).Width + (int)(24 * s);

            int x = pad;
            _lblRnFrom.SetBounds(x, 0, fromLblW, h);
            x = _lblRnFrom.Right + (int)(4 * s);
            _dtRnFrom.SetBounds(x, y, dateW, ctrlH);
            x = _dtRnFrom.Right + (int)(12 * s);

            _lblRnTo.SetBounds(x, 0, toLblW, h);
            x = _lblRnTo.Right + (int)(4 * s);
            _dtRnTo.SetBounds(x, y, dateW, ctrlH);
            x = _dtRnTo.Right + (int)(16 * s);

            int chkH = Math.Min(ctrlH, chkSz.Height);
            _chkRnUnfixedOnly.SetBounds(x, Math.Max(0, (h - chkH) / 2), chkSz.Width, chkH);
            x = _chkRnUnfixedOnly.Right + (int)(12 * s);

            _btnRnFilter.SetBounds(x, y, btnW, ctrlH);
        }

        private void UpdateRepairNoticeGridMenuState()
        {
            bool hasRow = CurrentRepairNoticeId() != null;
            foreach (ToolStripItem item in _rnGridMenu.Items)
            {
                var mi = item as ToolStripMenuItem;
                if (mi == null)
                    continue;
                switch (mi.Text)
                {
                    case "Thêm mới":
                        mi.Visible = _canAdd;
                        mi.Enabled = _canAdd;
                        break;
                    case "Chỉnh sửa":
                        mi.Visible = _canEdit;
                        mi.Enabled = _canEdit && hasRow;
                        break;
                    case "Xem chi tiết":
                        mi.Visible = !_canEdit && _canViewRepairNotice;
                        mi.Enabled = hasRow;
                        break;
                    case "Xóa":
                        mi.Visible = _canRemove;
                        mi.Enabled = _canRemove && hasRow;
                        break;
                    case "Sao chép ô":
                    case "Sao chép vùng chọn":
                        mi.Enabled = hasRow;
                        break;
                }
            }
        }

        private void ApplyRepairNoticeTheme(ThemeTokens t, Color page)
        {
            _rnFilterBar.BackColor = page;
            _lblRnFrom.BackColor = Color.Transparent;
            _lblRnFrom.ForeColor = t.Text;
            _lblRnTo.BackColor = Color.Transparent;
            _lblRnTo.ForeColor = t.Text;
            _dtRnFrom.ApplyTheme(t, page);
            _dtRnTo.ApplyTheme(t, page);
            _chkRnUnfixedOnly.BackColor = Color.Transparent;
            _chkRnUnfixedOnly.ForeColor = t.Text;
            _btnRnFilter.SurfaceColor = page;
            _btnRnFilter.FillColor = t.Surface;
            _btnRnFilter.TextColor = t.Text;
            _btnRnFilter.BorderColor = Draw.StrongBorder(t);
            _btnRnFilter.Invalidate();
            _rnFilterBar.Invalidate(true);
        }

        private void DisposeRepairNoticeUi()
        {
            _rnGridMenu.Dispose();
        }
    }
}
