using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "CHỌN PHÒNG" (§1.2, ảnh 05): ô Lọc, lưới Chọn · Phòng · Khu vực · Loại phòng, OK / Hủy bỏ.
    /// Nhiều bàn = đặt theo đoàn (§3.1). Nhãn Phòng/Bàn đọc theo cấu hình (§5.7).
    /// </summary>
    internal sealed class PickRoomDialog : ThemedDialog
    {
        private readonly BookingRepository _repo;
        private readonly TextBox _filter = new TextBox();
        private readonly DataGridView _grid = new DataGridView();
        private List<BookingRoomItem> _rooms = new List<BookingRoomItem>();
        private readonly HashSet<string> _selected;

        /// <summary>ID các bàn đã chọn (theo thứ tự trên lưới). Chỉ có khi DialogResult = OK.</summary>
        public List<string> SelectedRoomIds { get; private set; }

        /// <summary>Tên các bàn đã chọn (theo thứ tự trên lưới), để hiện vào txtBan.</summary>
        public List<string> SelectedRoomNames { get; private set; }

        public PickRoomDialog(UserSession session, List<string> selectedIds)
            : base("CHỌN " + BookingRules.RoomLabel.ToUpper(new CultureInfo("vi-VN")),
                "Tick một hay nhiều bàn — nhiều bàn là đặt theo đoàn.", 620, 460)
        {
            _repo = new BookingRepository(session.DatabasePath);
            _selected = new HashSet<string>(selectedIds ?? new List<string>(), StringComparer.Ordinal);
            SelectedRoomIds = new List<string>();
            SelectedRoomNames = new List<string>();

            float s = Dpi;
            var filterRow = new Panel { Dock = DockStyle.Top, Height = (int)(34 * s) };
            var lbl = new Label { Text = "Lọc", AutoSize = true, Location = new Point(0, (int)(8 * s)) };
            _filter.SetBounds((int)(44 * s), (int)(4 * s), (int)(420 * s), (int)(24 * s));
            _filter.Font = new Font("Segoe UI", 10f);
            _filter.TextChanged += delegate { RebuildRows(); };
            filterRow.Controls.Add(lbl);
            filterRow.Controls.Add(_filter);
            Body.Controls.Add(filterRow);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(30 * s);
            _grid.RowTemplate.Height = (int)(26 * s);
            _grid.Font = new Font("Segoe UI", 10f);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "sel", HeaderText = "Chọn", Width = (int)(56 * s) });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "room", HeaderText = BookingRules.RoomLabel, Width = (int)(150 * s), ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "area", HeaderText = "Khu vực", Width = (int)(150 * s), ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "Loại phòng", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            Body.Controls.Add(_grid);

            FlatButton cancel = AddButton("Hủy bỏ", null, delegate { Close(); });
            AddButton("OK", "primary", delegate { Accept(); });
            CancelButton = cancel;

            LoadRooms();
            Load += delegate { _filter.Focus(); };
        }

        private void LoadRooms()
        {
            try
            {
                _rooms = _repo.LoadRooms();
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không đọc được danh sách bàn: " + ex.Message);
                _rooms = new List<BookingRoomItem>();
            }
            RebuildRows();
        }

        private void RebuildRows()
        {
            string f = (_filter.Text ?? "").Trim();
            var vi = new CultureInfo("vi-VN").CompareInfo;
            _grid.Rows.Clear();
            foreach (BookingRoomItem room in _rooms)
            {
                if (f.Length > 0
                    && vi.IndexOf(room.Name ?? "", f, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0
                    && vi.IndexOf(room.AreaName ?? "", f, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0)
                    continue;
                int i = _grid.Rows.Add(_selected.Contains(room.Id), room.Name, room.AreaName, room.TypeName);
                _grid.Rows[i].Tag = room;
            }
            ApplyDialogTheme();
        }

        private void Accept()
        {
            var ids = new List<string>();
            var names = new List<string>();
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var room = r.Tag as BookingRoomItem;
                if (room == null)
                    continue;
                object v = r.Cells["sel"].Value;
                if (v is bool && (bool)v)
                {
                    ids.Add(room.Id);
                    names.Add(room.Name);
                }
            }
            // Bàn đã chọn nhưng bị lọc ẩn vẫn giữ (không mất khi gõ lọc rồi OK).
            foreach (BookingRoomItem room in _rooms)
            {
                if (_selected.Contains(room.Id) && !ids.Contains(room.Id))
                {
                    bool visible = false;
                    foreach (DataGridViewRow r in _grid.Rows)
                    {
                        if (r.Tag == room)
                        {
                            visible = true;
                            break;
                        }
                    }
                    if (!visible)
                    {
                        ids.Add(room.Id);
                        names.Add(room.Name);
                    }
                }
            }
            SelectedRoomIds = ids;
            SelectedRoomNames = names;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            if (_grid != null)
                ThemeApplier.ApplyGrid(_grid, ThemeManager.Current);
        }
    }
}
