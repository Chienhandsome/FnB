using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "THÊM NHANH BÀN VÀ KHU VỰC" (spec 1.4/2.2/3.11): mỗi dòng là một khu vực đang dùng với
    /// BATDAU (tiền tố), TUSO, DENSO (số đầu, số cuối) và cột DULIEU chỉ đọc (ví dụ "B01 ... B12").
    /// Ô "Chiều dài vùng số" tự cập nhật bằng số chữ số của DENSO lớn nhất; số được đệm 0 theo độ dài này.
    /// Bấm Thực hiện: kiểm tra TOÀN BỘ trước khi ghi dòng đầu (trùng trong DB / trùng trong lần sinh);
    /// TUSO = DENSO sinh đúng 1 bàn (Q6), TUSO &gt; DENSO báo lỗi "Khoảng số không hợp lệ".
    /// Bàn mới: CACHTINHGIO = theo khu vực, DONGIA = 0, TIENMOBAN = 0.
    /// </summary>
    internal sealed class QuickAddRoomDialog : ThemedDialog
    {
        private readonly RoomRepository _roomRepo;
        private readonly List<AreaRow> _areas = new List<AreaRow>();

        private readonly DataGridView _grid = new DataGridView();
        private readonly NumericUpDown _digitLength = new NumericUpDown();
        private readonly FlatButton _btnRun;

        /// <summary>Số bàn đã tạo (0 nếu hủy/lỗi).</summary>
        public int CreatedCount { get; private set; }

        public QuickAddRoomDialog(UserSession session)
            : base("THÊM NHANH BÀN VÀ KHU VỰC", "Thêm nhanh bàn theo khoảng số cho nhiều khu vực", 640, 420)
        {
            _roomRepo = new RoomRepository(session.DatabasePath);
            _areas.AddRange(new AreaRepository(session.DatabasePath).LoadAll());

            FlatButton exit = AddButton("Thoát", null, delegate { Close(); });
            _btnRun = AddButton("Thực hiện", "primary", delegate { Run(); });
            CancelButton = exit;

            float s = Dpi;
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = (int)(36 * s) };
            var lblDigits = new Label { Text = "Chiều dài vùng số", AutoSize = false, TabStop = false };
            lblDigits.SetBounds((int)(4 * s), (int)(8 * s), (int)(130 * s), (int)(20 * s));
            _digitLength.SetBounds((int)(140 * s), (int)(6 * s), (int)(70 * s), (int)(20 * s));
            _digitLength.Minimum = 1m;
            _digitLength.Maximum = 10m;
            _digitLength.Value = 2m;
            _digitLength.ValueChanged += delegate
            {
                if (!_loading)
                    RefreshPreviews();
            };
            bottom.Controls.Add(lblDigits);
            bottom.Controls.Add(_digitLength);
            Body.Controls.Add(bottom);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = (int)(34 * s);
            _grid.RowTemplate.Height = (int)(28 * s);
            _grid.Font = new Font("Segoe UI", 10f);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colArea",
                HeaderText = "Khu vực",
                Width = (int)(160 * s),
                ReadOnly = true
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFrom",
                HeaderText = "Bàn từ số",
                Width = (int)(100 * s)
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTo",
                HeaderText = "Bàn đến số",
                Width = (int)(100 * s)
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPrefix",
                HeaderText = "Bắt đầu bằng",
                Width = (int)(110 * s)
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPreview",
                HeaderText = "Dữ liệu mẫu",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });
            _grid.CellValueChanged += delegate { OnGridChanged(); };
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            Body.Controls.Add(_grid);

            foreach (AreaRow a in _areas)
            {
                int i = _grid.Rows.Add(a.Name, "", "", "", "");
                _grid.Rows[i].Tag = a.Id;
            }
            if (_areas.Count == 0)
            {
                _btnRun.Enabled = false;
                Msg.Warning(this, "Chưa có khu vực nào. Hãy tạo khu vực trước.");
            }
        }

        private bool _loading;

        private void OnGridChanged()
        {
            if (_loading)
                return;
            // DENSO đổi → chiều dài số tự cập nhật bằng số chữ số của DENSO lớn nhất (1.4).
            int maxDen = 0;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                int to;
                if (TryInt(r.Cells["colTo"].Value, out to) && to > maxDen)
                    maxDen = to;
            }
            _loading = true;
            try
            {
                _digitLength.Value = Math.Max(1, RoomRules.DigitsOf(maxDen));
            }
            finally
            {
                _loading = false;
            }
            RefreshPreviews();
        }

        private void RefreshPreviews()
        {
            int digits = (int)_digitLength.Value;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                int from, to;
                bool hasFrom = TryInt(r.Cells["colFrom"].Value, out from);
                bool hasTo = TryInt(r.Cells["colTo"].Value, out to);
                if (!hasFrom || !hasTo)
                {
                    r.Cells["colPreview"].Value = "";
                    continue;
                }
                string rangeError = RoomRules.ValidateRange(from, to);
                if (rangeError != null)
                {
                    r.Cells["colPreview"].Value = rangeError;
                    continue;
                }
                string prefix = Convert.ToString(r.Cells["colPrefix"].Value, CultureInfo.InvariantCulture) ?? "";
                r.Cells["colPreview"].Value = RoomRules.PreviewNames(RoomRules.GenerateNames(prefix, from, to, digits));
            }
        }

        private static bool TryInt(object v, out int n)
        {
            n = 0;
            if (v == null)
                return false;
            return int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out n);
        }

        private void Run()
        {
            // Thêm nhanh theo quyền Thêm (quyết định 21/11) — tự kiểm, không chỉ dựa vào nút ở CatalogView.
            if (!AppSession.Can(RoomRules.FunctionName, RoleModes.Add))
            {
                Msg.Warning(this, "Bạn không có quyền thêm bàn.");
                return;
            }
            _grid.EndEdit();
            var requests = new List<QuickAddRequest>();
            var allNames = new List<string>();
            int digits = (int)_digitLength.Value;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                string areaId = r.Tag as string;
                if (string.IsNullOrEmpty(areaId))
                    continue;
                object fromCell = r.Cells["colFrom"].Value;
                object toCell = r.Cells["colTo"].Value;
                bool hasFrom = fromCell != null && Convert.ToString(fromCell, CultureInfo.InvariantCulture).Trim().Length > 0;
                bool hasTo = toCell != null && Convert.ToString(toCell, CultureInfo.InvariantCulture).Trim().Length > 0;
                if (!hasFrom && !hasTo)
                    continue; // Dòng không nhập gì → bỏ qua.
                int from, to;
                if (!hasFrom || !hasTo || !TryInt(fromCell, out from) || !TryInt(toCell, out to))
                {
                    Msg.Warning(this, "Nhập đủ số bắt đầu và số kết thúc cho khu vực '" +
                        Convert.ToString(r.Cells["colArea"].Value, CultureInfo.InvariantCulture) + "'.");
                    _grid.CurrentCell = r.Cells["colFrom"];
                    _grid.BeginEdit(true);
                    return;
                }
                string rangeError = RoomRules.ValidateRange(from, to);
                if (rangeError != null)
                {
                    Msg.Warning(this, rangeError);
                    _grid.CurrentCell = r.Cells["colTo"];
                    _grid.BeginEdit(true);
                    return;
                }
                string prefix = Convert.ToString(r.Cells["colPrefix"].Value, CultureInfo.InvariantCulture) ?? "";
                List<string> names = RoomRules.GenerateNames(prefix.Trim(), from, to, digits);
                allNames.AddRange(names);
                requests.Add(new QuickAddRequest { AreaId = areaId, Names = names });
            }
            if (requests.Count == 0)
            {
                Msg.Warning(this, "Chưa nhập khoảng số cho khu vực nào.");
                return;
            }
            // Trùng trong chính lần sinh → dừng trước khi ghi (2.2).
            string dup = RoomRules.FindBatchDuplicate(allNames);
            if (dup != null)
            {
                Msg.Warning(this, RoomText.QuickAddDuplicate(dup));
                return;
            }
            // Repo kiểm trùng trong DB rồi ghi tất cả trong một transaction (2.2/3.11).
            QuickAddResult result;
            try
            {
                result = _roomRepo.QuickAddRooms(requests, AppSession.UserId);
            }
            catch (DataAccessException ex)
            {
                Msg.Error(this, "Không ghi được dữ liệu: " + ex.Message);
                return;
            }
            if (!result.Ok)
            {
                Msg.Warning(this, result.ErrorMessage);
                return;
            }
            CreatedCount = result.CreatedCount;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _grid.Font.Dispose();
                _grid.ColumnHeadersDefaultCellStyle.Font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
