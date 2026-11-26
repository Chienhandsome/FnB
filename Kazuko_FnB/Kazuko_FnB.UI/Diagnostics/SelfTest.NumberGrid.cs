using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private const int GRID_WM_KEYDOWN = 0x0100;
        private const int GRID_WM_KEYUP = 0x0101;

        private static readonly MethodInfo ControlOnKeyPressMethod =
            typeof(Control).GetMethod("OnKeyPress", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void NumberGridTests(string db)
        {
            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string cultureName in new[] { "en-US", "vi-VN" })
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    NumberGridRuleTests(cultureName);
                    NumberGridExcelN7Tests(cultureName);
                }
                NumberGridBookingAndQuoteTests(db);
                NumberGridProductAndAreaTests(db);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
            }
        }

        private static TextBox BeginGridEditReal(DataGridView grid, int rowIndex, string colName)
        {
            grid.Focus();
            grid.CurrentCell = grid.Rows[rowIndex].Cells[colName];
            bool began = grid.BeginEdit(true);
            Application.DoEvents();
            return began ? grid.EditingControl as TextBox : null;
        }

        private static void TypeCharsIntoGridEditor(TextBox editor, string text, bool replaceAll)
        {
            if (editor == null)
                return;
            IntPtr hwnd = editor.Handle;
            if (replaceAll)
                SendMessage(hwnd, EM_SETSEL, IntPtr.Zero, (IntPtr)(-1));
            foreach (char ch in text)
            {
                bool keyPressSeen = false;
                bool keyPressHandled = false;
                char keyPressChar = ch;
                KeyPressEventHandler probe = delegate(object s, KeyPressEventArgs e)
                {
                    keyPressSeen = true;
                    keyPressHandled = e.Handled;
                    keyPressChar = e.KeyChar;
                };
                int beforeLen = editor.TextLength;
                int beforeSelStart = editor.SelectionStart;
                int beforeSelLen = editor.SelectionLength;
                editor.KeyPress += probe;
                try
                {
                    IntPtr lParam = ch == ' ' ? (IntPtr)0x00390001 : (IntPtr)0x00000001;
                    if (ch == ' ')
                        SendMessage(hwnd, GRID_WM_KEYDOWN, (IntPtr)0x20, lParam);
                    SendMessage(hwnd, WM_CHAR, (IntPtr)ch, lParam);
                    if (ch == ' ')
                        SendMessage(hwnd, GRID_WM_KEYUP, (IntPtr)0x20, new IntPtr(unchecked((int)0xC0390001)));
                }
                finally
                {
                    editor.KeyPress -= probe;
                }

                // Với phím Space (0x20), nếu thông điệp Win32 giả lập trên cửa sổ ngoài màn hình không chèn được
                // dù không có handler KeyPress nào từ chối, chạy đúng OnKeyPress thật của Form (khi KeyPreview) và
                // ô soạn thảo rồi mới chèn qua SelectedText (bắn TextChanged thật và cập nhật trạng thái bẩn của lưới).
                if (ch == ' ')
                {
                    bool inserted = editor.TextLength == beforeLen - beforeSelLen + 1
                        && editor.SelectionStart == beforeSelStart + 1
                        && beforeSelStart < editor.TextLength
                        && editor.Text[beforeSelStart] == keyPressChar;
                    if (!inserted && !keyPressHandled)
                    {
                        var kpe = new KeyPressEventArgs(keyPressSeen ? keyPressChar : ch);
                        if (!keyPressSeen && ControlOnKeyPressMethod != null)
                        {
                            Form owner = editor.FindForm();
                            if (owner != null && owner.KeyPreview)
                                ControlOnKeyPressMethod.Invoke(owner, new object[] { kpe });
                            if (!kpe.Handled)
                                ControlOnKeyPressMethod.Invoke(editor, new object[] { kpe });
                        }
                        if (!kpe.Handled)
                            editor.SelectedText = kpe.KeyChar.ToString();
                    }
                }
            }
            Application.DoEvents();
        }

        private static void PasteTextIntoGridEditor(TextBox editor, string text, bool replaceAll)
        {
            if (editor == null)
                return;
            if (replaceAll)
                editor.SelectAll();
            editor.SelectedText = text;
            Application.DoEvents();
        }

        private static void LeaveGridCellReal(DataGridView grid, int rowIndex, string otherColName)
        {
            if (!string.IsNullOrEmpty(otherColName) && grid.Columns.Contains(otherColName))
                grid.CurrentCell = grid.Rows[rowIndex].Cells[otherColName];
            else
                grid.EndEdit();
            Application.DoEvents();
        }

        private static int CountChar(string s, char ch)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == ch) n++;
            return n;
        }

        private static string FormatDataErrorSuffix(string detail)
        {
            return string.IsNullOrEmpty(detail) ? "" : ", deErr=[" + detail + "]";
        }

        private static void NumberGridRuleTests(string cultureTag)
        {
            // N1/N4 thuần + Clone() giữ MaxDecimalPlaces.
            bool exceedOk = NumberInput.WouldExceedGridDecimalPlaces("2,12", 4, 0, '8', 2)
                && !NumberInput.WouldExceedGridDecimalPlaces("2,1", 3, 0, '2', 2)
                && !NumberInput.WouldExceedGridDecimalPlaces("2,12", 1, 0, '5', 2)
                && !NumberInput.WouldExceedGridDecimalPlaces("2,12", 2, 2, '8', 2)
                && !NumberInput.WouldExceedGridDecimalPlaces("1,50,", 5, 0, '0', 2)
                && NumberInput.WouldExceedGridDecimalPlaces("0,1234", 6, 0, '5', 4)
                && !NumberInput.WouldExceedGridDecimalPlaces("0,12", 4, 0, '5', 4);

            bool normOk = NumberInput.NormalizeGridEditText("2.128", 2) == "2,12"
                && NumberInput.NormalizeGridEditText("2,128", 2) == "2,12"
                && NumberInput.NormalizeGridEditText("1,2345", 2) == "1,23"
                && NumberInput.NormalizeGridEditText("0.12345", 4) == "0,1234"
                && NumberInput.NormalizeGridEditText("0.125", 4) == "0,125"
                && NumberInput.NormalizeGridEditText("1.500.000", 2) == "1,500,000"
                && NumberInput.NormalizeGridEditText("1,500.5", 2) == "1,500,5"
                && NumberInput.NormalizeGridEditText("2.128abc", 2) == "2,128abc";

            var cell4 = new NumericEditGridCell(4);
            var clone4 = cell4.Clone() as NumericEditGridCell;
            var cell2 = new NumericEditGridCell(2);
            var clone2 = cell2.Clone() as NumericEditGridCell;
            bool cloneOk = clone4 != null && clone4.MaxDecimalPlaces == 4
                && clone2 != null && clone2.MaxDecimalPlaces == 2;

            Check("Nhập số P2 [A1/A10 thuần lưới " + cultureTag + "]: chặn/cắt N4 (2 & 4 lẻ), giữ đủ 2 dấu cho 1.500.000, Clone() giữ MaxDecimalPlaces",
                exceedOk && normOk && cloneOk,
                "exceed=" + exceedOk + ", norm=" + normOk + ", clone=" + cloneOk);
        }

        private static void NumberGridExcelN7Tests(string cultureTag)
        {
            // A11: kiểm đủ luật N7 ở QuoteExcelImporter.TryParseExcelNumber và Import.
            decimal v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, vNum, vBad;
            bool parseOk =
                QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("1.250"), false, out v1) && v1 == 1250m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("1.250"), true, out v2) && v2 == 1250m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("0.125"), false, out v3) && v3 == 0.125m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("0.125"), true, out v4) && v4 == 0.125m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("0,125"), false, out v5) && v5 == 0.125m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("0,125"), true, out v6) && v6 == 0.125m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("-0.125"), false, out v7) && v7 == -0.125m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("-0,125"), true, out v8) && v8 == -0.125m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("15,000"), true, out v9) && v9 == 15000m
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("1.500.000"), true, out v10) && v10 == 1500000m
                && !QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("1,250"), false, out vBad)
                && !QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("15,000"), false, out vBad)
                && !QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.TextCell("1.500.000"), false, out vBad)
                && QuoteExcelImporter.TryParseExcelNumber(QuoteExcelCell.NumberCell(1.125m), false, out vNum) && vNum == 1.125m;

            var items = new List<QuoteSaleItem>
            {
                new QuoteSaleItem { Id = "P1", Code = "N7A", Name = "Hàng N7", UnitId = "U1", UnitName = "Ly", SellPrice = 10000m }
            };
            var units = new List<LookupItem> { new LookupItem { Id = "U1", Name = "Ly" } };
            var map = new QuoteExcelColumnMap { ItemCol = 0, QuantityCol = 1, PriceCol = 2, DiscountPctCol = 3 };

            QuoteExcelImportResult okRes = QuoteExcelImporter.Import(
                new List<string[]>
                {
                    new[] { "N7A", "1.250", "15,000", "0.125" },
                    new[] { "N7A", "0.125", "0,125", "0,125" }
                },
                map, true, false, items, units);

            QuoteExcelImportResult discOverRes = QuoteExcelImporter.Import(
                new List<string[]> { new[] { "N7A", "2", "10000", "1.250" } },
                map, true, false, items, units);

            QuoteExcelImportResult commaQtyErrRes = QuoteExcelImporter.Import(
                new List<string[]> { new[] { "N7A", "1,250", "10000", "0" } },
                map, true, false, items, units);

            bool importOk = okRes.Ok && okRes.Lines.Count == 2
                && okRes.Lines[0].Quantity == 1250m && okRes.Lines[0].Price == 15000m && okRes.Lines[0].DiscountPct == 0.125m
                && okRes.Lines[1].Quantity == 0.125m && okRes.Lines[1].Price == 0.125m && okRes.Lines[1].DiscountPct == 0.125m
                && !discOverRes.Ok && discOverRes.Errors.Count == 1
                && discOverRes.Errors[0] == QuoteText.ExcelInvalidValue("1.250", "% giảm", "N7A")
                && !commaQtyErrRes.Ok && commaQtyErrRes.Errors.Count == 1
                && commaQtyErrRes.Errors[0] == QuoteText.ExcelInvalidNumber("1,250", "Số lượng", "N7A");

            Check("Nhập số P2 [A11 Excel N7 " + cultureTag + "]: 1.250->1250 mọi cột (% giảm bị chặn 0..100), 0.125/0,125->0,125, 1,250 SL lỗi, ô số giữ nguyên",
                parseOk && importOk, "parse=" + parseOk + ", import=" + importOk);
        }
    }
}
