using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI
{
    /// <summary>
    /// Phần bộ lọc Từ/Đến ngày + Nhân viên, thanh tổng cộng Thưởng/Phạt, menu ngữ cảnh và bảng màu
    /// cho chế độ Thưởng phạt của <see cref="CatalogView"/>.
    /// </summary>
    internal sealed partial class CatalogView
    {
        private void BuildRewardPenaltyUi()
        {
            float s = DeviceDpi / 96f;
            _rpFilterBar.Height = (int)(44 * s);
            _lblRpFrom.Font = _count.Font;
            _lblRpTo.Font = _count.Font;
            _lblRpEmp.Font = _count.Font;
            _dtRpFrom.Inner.Font = _grid.Font;
            _dtRpTo.Inner.Font = _grid.Font;
            _cboRpEmp.Font = _grid.Font;
            _btnRpFilter.Font = _count.Font;

            _dtRpFrom.ValueChanged += delegate
            {
                if (!_updatingRpFilters && _rewardPenaltyMode)
                    ReloadRewardPenaltyRowsOnly();
            };
            _dtRpTo.ValueChanged += delegate
            {
                if (!_updatingRpFilters && _rewardPenaltyMode)
                    ReloadRewardPenaltyRowsOnly();
            };
            _cboRpEmp.SelectedIndexChanged += delegate
            {
                if (!_updatingRpFilters && _rewardPenaltyMode)
                    ReloadRewardPenaltyRowsOnly();
            };
            _btnRpFilter.Click += delegate
            {
                if (_rewardPenaltyMode)
                    ReloadRewardPenaltyRowsOnly();
            };

            _rpFilterBar.Controls.Add(_lblRpFrom);
            _rpFilterBar.Controls.Add(_dtRpFrom);
            _rpFilterBar.Controls.Add(_lblRpTo);
            _rpFilterBar.Controls.Add(_dtRpTo);
            _rpFilterBar.Controls.Add(_lblRpEmp);
            _rpFilterBar.Controls.Add(_cboRpEmp);
            _rpFilterBar.Controls.Add(_btnRpFilter);
            _rpFilterBar.Resize += delegate { LayoutRewardPenaltyFilterBar(); };
            _split.Panel2.Controls.Add(_rpFilterBar);

            _rpTotalFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _rpTotalBar.Height = (int)(32 * s);
            _lblRpTotalCaption.Font = _rpTotalFont;
            _lblRpTotalValues.Font = _rpTotalFont;
            _rpTotalBar.Controls.Add(_lblRpTotalCaption);
            _rpTotalBar.Controls.Add(_lblRpTotalValues);
            _rpTotalBar.Resize += delegate { LayoutRewardPenaltyTotalBar(); };
            _split.Panel2.Controls.Add(_rpTotalBar);
            if (_split.Panel2.Controls.Count > 1)
                _split.Panel2.Controls.SetChildIndex(_rpTotalBar, 1);

            _rpGridMenu.AddItem("Thêm mới", Keys.Insert, delegate { AddRewardPenalty(); });
            _rpGridMenu.AddItem("Chỉnh sửa", Keys.F4, delegate { EditRewardPenalty(); });
            _rpGridMenu.AddItem("Xem chi tiết", delegate { OpenRewardPenaltyFromGrid(); });
            _rpGridMenu.AddItem("Xóa", Keys.Delete, delegate { DeleteRewardPenalty(); });
            _rpGridMenu.AddSeparator();
            _rpGridMenu.AddItem("Refresh", delegate { Reload(); });
            _rpGridMenu.AddItem("Xuất excel", delegate { ExportGridToExcel(); });
            _rpGridMenu.AddSeparator();
            _rpGridMenu.AddItem("Sao chép ô", delegate { CopyCell(); });
            _rpGridMenu.AddItem("Sao chép vùng chọn", delegate { CopySelection(); });
            _rpGridMenu.AddItem("Tự động dãn cột", delegate { AutoFitColumns(); });
            _rpGridMenu.Opening += delegate { UpdateRewardPenaltyGridMenuState(); };
        }

        private void PopulateRewardPenaltyEmployeeFilter()
        {
            string prevKey = SelectedRpEmployeeId();
            _updatingRpFilters = true;
            try
            {
                _cboRpEmp.Items.Clear();
                _cboRpEmp.Items.Add(new Choice("", "Tất cả nhân viên"));
                foreach (LookupItem emp in _rpEmployees)
                    _cboRpEmp.Items.Add(new Choice(emp.Id, emp.Name));
                int sel = 0;
                if (!string.IsNullOrEmpty(prevKey))
                {
                    for (int i = 1; i < _cboRpEmp.Items.Count; i++)
                    {
                        var ch = _cboRpEmp.Items[i] as Choice;
                        if (ch != null && string.Equals(ch.Key, prevKey, StringComparison.Ordinal))
                        {
                            sel = i;
                            break;
                        }
                    }
                }
                _cboRpEmp.SelectedIndex = sel;
            }
            finally
            {
                _updatingRpFilters = false;
            }
        }

        private string SelectedRpEmployeeId()
        {
            var ch = _cboRpEmp.SelectedItem as Choice;
            return ch == null ? "" : (ch.Key ?? "");
        }

        private void LayoutRewardPenaltyFilterBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(10 * s);
            int h = _rpFilterBar.ClientSize.Height;
            int ctrlH = (int)(30 * s);
            int y = Math.Max(0, (h - ctrlH) / 2);

            int fromLblW = TextRenderer.MeasureText(_lblRpFrom.Text, _lblRpFrom.Font).Width + (int)(8 * s);
            int toLblW = TextRenderer.MeasureText(_lblRpTo.Text, _lblRpTo.Font).Width + (int)(8 * s);
            int empLblW = TextRenderer.MeasureText(_lblRpEmp.Text, _lblRpEmp.Font).Width + (int)(8 * s);
            int dateW = (int)(135 * s);
            int empW = (int)(220 * s);
            int btnW = TextRenderer.MeasureText(_btnRpFilter.Text, _btnRpFilter.Font).Width + (int)(24 * s);

            int x = pad;
            _lblRpFrom.SetBounds(x, 0, fromLblW, h);
            x = _lblRpFrom.Right + (int)(4 * s);
            _dtRpFrom.SetBounds(x, y, dateW, ctrlH);
            x = _dtRpFrom.Right + (int)(12 * s);

            _lblRpTo.SetBounds(x, 0, toLblW, h);
            x = _lblRpTo.Right + (int)(4 * s);
            _dtRpTo.SetBounds(x, y, dateW, ctrlH);
            x = _dtRpTo.Right + (int)(12 * s);

            _lblRpEmp.SetBounds(x, 0, empLblW, h);
            x = _lblRpEmp.Right + (int)(4 * s);
            int cboH = Math.Min(ctrlH, Math.Max((int)(24 * s), _cboRpEmp.PreferredHeight));
            _cboRpEmp.SetBounds(x, Math.Max(0, (h - cboH) / 2), empW, cboH);
            x = _cboRpEmp.Right + (int)(10 * s);

            _btnRpFilter.SetBounds(x, y, btnW, ctrlH);
        }

        private void LayoutRewardPenaltyTotalBar()
        {
            float s = DeviceDpi / 96f;
            int pad = (int)(12 * s);
            int w = _rpTotalBar.ClientSize.Width;
            int h = _rpTotalBar.ClientSize.Height;
            int valW = Math.Min((int)(460 * s), Math.Max((int)(220 * s), w * 2 / 3));
            _lblRpTotalValues.SetBounds(Math.Max(pad, w - pad - valW), 0, valW, h);
            _lblRpTotalCaption.SetBounds(pad, 0, Math.Max(0, _lblRpTotalValues.Left - pad - (int)(8 * s)), h);
        }

        private void UpdateRewardPenaltyTotalBar()
        {
            if (!_rewardPenaltyMode)
            {
                _rpTotalBar.Visible = false;
                return;
            }
            decimal totalBonus = 0m;
            decimal totalPenalty = 0m;
            int count = 0;
            if (_view != null)
            {
                count = _view.Count;
                for (int i = 0; i < _view.Count; i++)
                {
                    object b = _view[i]["rp_bonus"];
                    object p = _view[i]["rp_penalty"];
                    if (b != null && !(b is DBNull))
                        totalBonus += Convert.ToDecimal(b);
                    if (p != null && !(p is DBNull))
                        totalPenalty += Convert.ToDecimal(p);
                }
            }
            _rpTotalBonus = totalBonus;
            _rpTotalPenalty = totalPenalty;
            CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
            _lblRpTotalCaption.Text = "Tổng cộng (" + count + " phiếu):";
            _lblRpTotalValues.Text = "Tổng thưởng: " + totalBonus.ToString("#,##0.##", vi)
                + "   |   Tổng phạt: " + totalPenalty.ToString("#,##0.##", vi);
            LayoutRewardPenaltyTotalBar();
        }

        private void UpdateRewardPenaltyGridMenuState()
        {
            bool hasRow = CurrentRewardPenaltyId() != null;
            foreach (ToolStripItem item in _rpGridMenu.Items)
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
                        mi.Visible = !_canEdit && _canViewRewardPenalty;
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

        private void ApplyRewardPenaltyTheme(ThemeTokens t, Color page)
        {
            _rpFilterBar.BackColor = page;
            _lblRpFrom.BackColor = Color.Transparent;
            _lblRpFrom.ForeColor = t.Text;
            _lblRpTo.BackColor = Color.Transparent;
            _lblRpTo.ForeColor = t.Text;
            _lblRpEmp.BackColor = Color.Transparent;
            _lblRpEmp.ForeColor = t.Text;
            _dtRpFrom.ApplyTheme(t, page);
            _dtRpTo.ApplyTheme(t, page);
            _cboRpEmp.BackColor = t.Surface;
            _cboRpEmp.ForeColor = t.Text;
            _btnRpFilter.SurfaceColor = page;
            _btnRpFilter.FillColor = t.Surface;
            _btnRpFilter.TextColor = t.Text;
            _btnRpFilter.BorderColor = Draw.StrongBorder(t);
            _btnRpFilter.Invalidate();

            _rpTotalBar.BackColor = Draw.Mix(t.Surface, t.Primary, 0.08f);
            _lblRpTotalCaption.BackColor = Color.Transparent;
            _lblRpTotalCaption.ForeColor = t.Text;
            _lblRpTotalValues.BackColor = Color.Transparent;
            _lblRpTotalValues.ForeColor = t.Primary;
            _rpFilterBar.Invalidate(true);
            _rpTotalBar.Invalidate(true);
        }

        private void DisposeRewardPenaltyUi()
        {
            if (_rpTotalFont != null)
                _rpTotalFont.Dispose();
            _rpGridMenu.Dispose();
        }
    }
}
