using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp nhập chi tiết báo giá từ Excel (docs/specs/bao-gia-ban-hang.md §2.3):
    /// chọn file (.xlsx/.xls), gán cột (Mã hàng/Tên hàng, Số lượng, ĐVT khi 2 ĐVT, Đơn giá, % giảm),
    /// xem trước và kiểm tra bằng QuoteExcelImporter. Có lỗi thì gom lại hiện một lần, không nhập dòng nào.
    /// </summary>
    internal sealed class QuoteExcelImportDialog : ThemedDialog
    {
        private readonly IList<QuoteSaleItem> _activeItems;
        private readonly IList<LookupItem> _activeUnits;
        private readonly bool _useProductCode;
        private readonly bool _useTwoUnits;

        private readonly TextBox _txtFile = new TextBox { ReadOnly = true };
        private readonly FlatButton _btnBrowse = new FlatButton { Text = "Chọn tệp..." };
        private readonly ComboBox _cboItem = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _cboQty = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _cboUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _cboPrice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _cboDisc = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DataGridView _previewGrid = new DataGridView();

        private QuoteExcelSheetData _sheet = new QuoteExcelSheetData();

        public QuoteExcelImportResult Result { get; private set; }
        internal bool SuppressPopup { get; set; }
        internal string LastWarningMessageForTest { get; private set; }

        public QuoteExcelImportDialog(
            IList<QuoteSaleItem> activeItems,
            IList<LookupItem> activeUnits,
            bool useProductCode,
            bool useTwoUnits)
            : base("NHẬP CHI TIẾT TỪ EXCEL", "Chọn tệp Excel và gán cột tương ứng.", 720, 500)
        {
            _activeItems = activeItems ?? new List<QuoteSaleItem>();
            _activeUnits = activeUnits ?? new List<LookupItem>();
            _useProductCode = useProductCode;
            _useTwoUnits = useTwoUnits;

            BuildUi();

            FlatButton cancel = AddButton("Hủy bỏ", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Nhập dữ liệu", "primary", delegate { RunImport(); });
            CancelButton = cancel;
        }

        private void BuildUi()
        {
            float s = Dpi;
            Font bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);

            var filePanel = new Panel { Dock = DockStyle.Top, Height = (int)(42 * s) };
            var lblFile = new Label { Text = "Tệp Excel", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = bodyFont };
            lblFile.SetBounds(0, (int)(6 * s), (int)(86 * s), (int)(26 * s));
            _txtFile.Font = bodyFont;
            _txtFile.SetBounds((int)(90 * s), (int)(6 * s), (int)(470 * s), (int)(26 * s));
            _txtFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _btnBrowse.Font = bodyFont;
            _btnBrowse.SetBounds((int)(568 * s), (int)(3 * s), (int)(116 * s), (int)(34 * s));
            _btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnBrowse.Click += delegate { BrowseFile(); };
            filePanel.Controls.Add(lblFile);
            filePanel.Controls.Add(_txtFile);
            filePanel.Controls.Add(_btnBrowse);

            int rowCount = _useTwoUnits ? 3 : 2;
            var mapGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                RowCount = rowCount,
                Height = (int)(rowCount * 38 * s) + (int)(8 * s),
                Padding = new Padding(0, (int)(4 * s), 0, (int)(4 * s))
            };
            mapGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110 * s));
            mapGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mapGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110 * s));
            mapGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < rowCount; i++)
                mapGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36 * s));

            foreach (ComboBox cbo in new[] { _cboItem, _cboQty, _cboUnit, _cboPrice, _cboDisc })
            {
                cbo.Font = bodyFont;
                cbo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            }

            string itemLabel = _useProductCode ? "Mã hàng *" : "Tên hàng *";
            mapGrid.Controls.Add(MakeLabel(itemLabel, bodyFont), 0, 0);
            mapGrid.Controls.Add(_cboItem, 1, 0);
            mapGrid.Controls.Add(MakeLabel("Số lượng *", bodyFont), 2, 0);
            mapGrid.Controls.Add(_cboQty, 3, 0);

            mapGrid.Controls.Add(MakeLabel("Đơn giá", bodyFont), 0, 1);
            mapGrid.Controls.Add(_cboPrice, 1, 1);
            mapGrid.Controls.Add(MakeLabel("% giảm", bodyFont), 2, 1);
            mapGrid.Controls.Add(_cboDisc, 3, 1);

            if (_useTwoUnits)
            {
                mapGrid.Controls.Add(MakeLabel("ĐVT", bodyFont), 0, 2);
                mapGrid.Controls.Add(_cboUnit, 1, 2);
            }

            _previewGrid.Dock = DockStyle.Fill;
            _previewGrid.ReadOnly = true;
            _previewGrid.AllowUserToAddRows = false;
            _previewGrid.AllowUserToDeleteRows = false;
            _previewGrid.RowHeadersVisible = false;
            _previewGrid.Font = bodyFont;
            _previewGrid.ColumnHeadersDefaultCellStyle.Font = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _previewGrid.ColumnHeadersHeight = (int)(32 * s);
            _previewGrid.RowTemplate.Height = (int)(28 * s);

            Body.Controls.Add(_previewGrid);
            Body.Controls.Add(mapGrid);
            Body.Controls.Add(filePanel);
        }

        private static Label MakeLabel(string text, Font font)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = font
            };
        }

        private void BrowseFile()
        {
            using (var ofd = new OpenFileDialog
            {
                Title = "Chọn tệp Excel báo giá",
                Filter = "Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Tất cả (*.*)|*.*"
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;
                LoadExcelFile(ofd.FileName);
            }
        }

        internal void LoadExcelFile(string filePath)
        {
            try
            {
                _sheet = QuoteExcelReader.ReadFirstSheet(filePath);
                _txtFile.Text = filePath;
            }
            catch (Exception ex)
            {
                ShowWarning("Không đọc được tệp Excel: " + ex.Message);
                return;
            }

            PopulateColumnCombos();
            PopulatePreview();
        }

        private void PopulateColumnCombos()
        {
            foreach (ComboBox cbo in new[] { _cboItem, _cboQty, _cboUnit, _cboPrice, _cboDisc })
            {
                cbo.Items.Clear();
                cbo.Items.Add(new ColumnChoice(-1, "(Không chọn)"));
                for (int i = 0; i < _sheet.Headers.Count; i++)
                    cbo.Items.Add(new ColumnChoice(i, _sheet.Headers[i]));
                cbo.SelectedIndex = 0;
            }

            QuoteExcelColumnMap detected = QuoteExcelImporter.AutoDetectColumns(_sheet.Headers, _useProductCode, _useTwoUnits);
            SelectColumnChoice(_cboItem, detected.ItemCol);
            SelectColumnChoice(_cboQty, detected.QuantityCol);
            if (_useTwoUnits)
                SelectColumnChoice(_cboUnit, detected.UnitCol);
            SelectColumnChoice(_cboPrice, detected.PriceCol);
            SelectColumnChoice(_cboDisc, detected.DiscountPctCol);
        }

        private static void SelectColumnChoice(ComboBox cbo, int colIndex)
        {
            int target = colIndex >= 0 ? colIndex + 1 : 0;
            if (target >= 0 && target < cbo.Items.Count)
                cbo.SelectedIndex = target;
        }

        private void PopulatePreview()
        {
            _previewGrid.Columns.Clear();
            _previewGrid.Rows.Clear();
            for (int i = 0; i < _sheet.Headers.Count; i++)
                _previewGrid.Columns.Add("c" + i, _sheet.Headers[i]);

            int limit = Math.Min(20, _sheet.Rows.Count);
            for (int r = 0; r < limit; r++)
            {
                QuoteExcelCell[] row = _sheet.Rows[r];
                var textRow = new object[row.Length];
                for (int c = 0; c < row.Length; c++)
                    textRow[c] = row[c] == null ? "" : (row[c].Text ?? "");
                _previewGrid.Rows.Add(textRow);
            }
        }

        private void RunImport()
        {
            var map = new QuoteExcelColumnMap
            {
                ItemCol = SelectedIndexOf(_cboItem),
                QuantityCol = SelectedIndexOf(_cboQty),
                UnitCol = _useTwoUnits ? SelectedIndexOf(_cboUnit) : -1,
                PriceCol = SelectedIndexOf(_cboPrice),
                DiscountPctCol = SelectedIndexOf(_cboDisc)
            };

            QuoteExcelImportResult res = QuoteExcelImporter.Import(
                _sheet.Rows, map, _useProductCode, _useTwoUnits, _activeItems, _activeUnits);
            if (!res.Ok)
            {
                ShowWarning(string.Join(Environment.NewLine, res.Errors.ToArray()));
                return;
            }
            Result = res;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static int SelectedIndexOf(ComboBox cbo)
        {
            var choice = cbo.SelectedItem as ColumnChoice;
            return choice == null ? -1 : choice.ColumnIndex;
        }

        private void ShowWarning(string message)
        {
            LastWarningMessageForTest = message;
            if (!SuppressPopup)
                Msg.Warning(this, message);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            if (_previewGrid != null)
                ThemeApplier.ApplyGrid(_previewGrid, ThemeManager.Current);
        }

        private sealed class ColumnChoice
        {
            public readonly int ColumnIndex;
            private readonly string _text;

            public ColumnChoice(int index, string text)
            {
                ColumnIndex = index;
                _text = text;
            }

            public override string ToString()
            {
                return _text;
            }
        }
    }
}
