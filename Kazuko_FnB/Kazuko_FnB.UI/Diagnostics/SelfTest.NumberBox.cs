using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;
using LayoutKind = Kazuko_FnB.Domain.LayoutKind;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private const int WM_CHAR = 0x0102;
        private const int EM_SETSEL = 0x00B1;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static void NumberBoxTests(string db)
        {
            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string cultureName in new[] { "en-US", "vi-VN" })
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    NumberInputRuleTests(cultureName);
                    NumberBoxControlTests(cultureName);
                }
                NumberBoxDialogTests(db);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
            }
        }

        private static void FocusControlReal(Form form, Control target)
        {
            form.ActiveControl = target;
            target.Focus();
            Application.DoEvents();
        }

        private static void TypeCharsIntoNumberBox(Form form, NumberBox box, string text, bool replaceAll)
        {
            FocusControlReal(form, box);
            TextBox edit = box.InnerEditBox;
            IntPtr hwnd = edit.Handle;
            if (replaceAll)
            {
                SendMessage(hwnd, EM_SETSEL, IntPtr.Zero, (IntPtr)(-1));
            }
            foreach (char ch in text)
            {
                SendMessage(hwnd, WM_CHAR, (IntPtr)ch, IntPtr.Zero);
            }
            Application.DoEvents();
        }

        private static void AssignEditTextIntoNumberBox(Form form, NumberBox box, string text)
        {
            FocusControlReal(form, box);
            TextBox edit = box.InnerEditBox;
            edit.Text = text;
            Application.DoEvents();
        }

        private static void NumberInputRuleTests(string cultureTag)
        {
            // A1: bảng N1 (đọc số), định dạng hiển thị/sửa và làm tròn N4/N5 độc lập CurrentCulture.
            decimal v1600, vDot, vComma, v15, vNeg, vBad;
            bool n1Ok = NumberInput.TryParse("1600", out v1600) && v1600 == 1600m
                && NumberInput.TryParse("2.5", out vDot) && vDot == 2.5m
                && NumberInput.TryParse("2,5", out vComma) && vComma == 2.5m
                && NumberInput.TryParse("15.000", out v15) && v15 == 15m
                && NumberInput.TryParse("-12.5", out vNeg) && vNeg == -12.5m
                && !NumberInput.TryParse("1.500.000", out vBad)
                && !NumberInput.TryParse("1,500.5", out vBad)
                && !NumberInput.TryParse("abc", out vBad)
                && !NumberInput.TryParse("", out vBad)
                && !NumberInput.TryParse("   ", out vBad);

            bool fmtOk = NumberInput.FormatForDisplay(15000m, 2, true) == "15.000"
                && NumberInput.FormatForDisplay(15000.00m, 2, true) == "15.000"
                && NumberInput.FormatForDisplay(15000m, 2, false) == "15000"
                && NumberInput.FormatForDisplay(2.50m, 2, true) == "2,5"
                && NumberInput.FormatForDisplay(15.5m, 2, true) == "15,5"
                && NumberInput.FormatForDisplay(33.333333m, 2, true) == "33,33"
                && NumberInput.FormatForDisplay(-0.001m, 2, true) == "0"
                && NumberInput.FormatForDisplay(-15000.5m, 2, true) == "-15.000,5"
                && NumberInput.FormatForEdit(1600m, 2) == "1600"
                && NumberInput.FormatForEdit(2.50m, 2) == "2,5"
                && NumberInput.NormalizeEditInput("2.5") == "2,5"
                && NumberInput.NormalizeEditInput("1.500.000") == "1,500,000";

            decimal r1128, r1125, rNegMid, rIntOk, rIntBad;
            bool n4n5Ok = NumberInput.TryParseForBox("1.128", 2, false, out r1128) && r1128 == 1.13m
                && NumberInput.TryParseForBox("1.125", 2, false, out r1125) && r1125 == 1.13m
                && NumberInput.TryParseForBox("-1.125", 2, false, out rNegMid) && rNegMid == -1.13m
                && NumberInput.TryParseForBox("15.000", 0, true, out rIntOk) && rIntOk == 15m
                && !NumberInput.TryParseForBox("2.5", 0, true, out rIntBad)
                && !NumberInput.TryParseForBox("2,5", 0, true, out rIntBad);

            Check("Nhập số P1 [A1 thuần " + cultureTag + "]: TryParse N1, FormatForDisplay/Edit vi-VN, N4 AwayFromZero & N5 số nguyên",
                n1Ok && fmtOk && n4n5Ok,
                "n1=" + n1Ok + ", fmt=" + fmtOk + ", n4n5=" + n4n5Ok);
        }

        private static void NumberBoxControlTests(string cultureTag)
        {
            using (var form = new Form())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Size = new Size(420, 320);

                var decBox = new NumberBox
                {
                    DecimalPlaces = 2,
                    ThousandsSeparator = true,
                    Minimum = 0m,
                    Maximum = 1000000m,
                    Increment = 1m,
                    Location = new Point(10, 10),
                    Width = 140
                };
                var intBox = new NumberBox
                {
                    DecimalPlaces = 0,
                    ThousandsSeparator = false,
                    Minimum = 1m,
                    Maximum = 100m,
                    Value = 2m,
                    Location = new Point(10, 45),
                    Width = 140
                };
                var negBox = new NumberBox
                {
                    DecimalPlaces = 2,
                    ThousandsSeparator = true,
                    Minimum = -1000m,
                    Maximum = 1000m,
                    Location = new Point(10, 80),
                    Width = 140
                };
                var noSepBox = new NumberBox
                {
                    DecimalPlaces = 2,
                    ThousandsSeparator = false,
                    Minimum = 0m,
                    Maximum = 100000m,
                    Location = new Point(10, 115),
                    Width = 140
                };
                var roBox = new NumberBox
                {
                    DecimalPlaces = 2,
                    ThousandsSeparator = true,
                    Minimum = 0m,
                    Maximum = 1000000000m,
                    ReadOnly = true,
                    Value = 150000m,
                    Location = new Point(10, 150),
                    Width = 140
                };
                var other = new TextBox { Location = new Point(180, 10), Width = 140 };

                form.Controls.AddRange(new Control[] { decBox, intBox, negBox, noSepBox, roBox, other });
                form.Show();
                Application.DoEvents();

                try
                {
                    // A2 + A2b: gõ phím thật 2.5 -> đang gõ hiện "2,5", rời ô Value = 2.5m; gõ 2,5 -> 2.5m.
                    TypeCharsIntoNumberBox(form, decBox, "2.5", true);
                    string typingDot = decBox.Text;
                    FocusControlReal(form, other);
                    bool dotOk = typingDot == "2,5" && decBox.Value == 2.5m && decBox.Text == "2,5";

                    TypeCharsIntoNumberBox(form, decBox, "2,5", true);
                    string typingComma = decBox.Text;
                    FocusControlReal(form, other);
                    bool commaOk = typingComma == "2,5" && decBox.Value == 2.5m && decBox.Text == "2,5";

                    Check("Nhập số P1 [A2/A2b " + cultureTag + "]: WM_CHAR 2.5 hiện ngay '2,5' lúc gõ & ra 2,5; 2,5 ra 2,5",
                        dotOk && commaOk,
                        "dotText=" + typingDot + ", val=" + decBox.Value + ", commaText=" + typingComma);

                    // A2 + A3: 1600 -> rời ô hiện "1.600", vào lại hiện "1600"; ô tắt ThousandsSeparator hiện "1600"; ô ReadOnly hiện "150.000".
                    TypeCharsIntoNumberBox(form, decBox, "1600", true);
                    FocusControlReal(form, other);
                    bool disp1600Ok = decBox.Value == 1600m && decBox.Text == "1.600";
                    FocusControlReal(form, decBox);
                    string reEdit1600 = decBox.Text;
                    FocusControlReal(form, other);

                    TypeCharsIntoNumberBox(form, noSepBox, "1600", true);
                    FocusControlReal(form, other);
                    bool noSepOk = noSepBox.Value == 1600m && noSepBox.Text == "1600";
                    bool roOk = roBox.Text == "150.000";

                    Check("Nhập số P1 [A2/A3 " + cultureTag + "]: 1600 hiện '1.600' khi rời và '1600' khi vào lại; tắt ngăn nghìn & chỉ đọc đúng",
                        disp1600Ok && reEdit1600 == "1600" && noSepOk && roOk,
                        "disp=" + decBox.Text + ", reEdit=" + reEdit1600 + ", noSep=" + noSepBox.Text + ", ro=" + roBox.Text);

                    // A1/A2b/A6: 15.000 -> 15; gõ 1.500.000 không nuốt dấu phẩy thứ hai và rời ô giữ 15; dán 2.5 -> '2,5'; dán 1.500.000 / 'abc' / '' giữ số cũ.
                    TypeCharsIntoNumberBox(form, decBox, "15.000", true);
                    FocusControlReal(form, other);
                    bool v15Ok = decBox.Value == 15m && decBox.Text == "15";

                    TypeCharsIntoNumberBox(form, decBox, "1.500.000", true);
                    string typingTwoDot = decBox.Text;
                    FocusControlReal(form, other);
                    bool twoDotOk = typingTwoDot == "1,500,000" && decBox.Value == 15m && decBox.Text == "15";

                    AssignEditTextIntoNumberBox(form, decBox, "2.5");
                    string pasteDot = decBox.Text;
                    FocusControlReal(form, other);
                    bool pasteDotOk = pasteDot == "2,5" && decBox.Value == 2.5m && decBox.Text == "2,5";

                    AssignEditTextIntoNumberBox(form, decBox, "1.500.000");
                    string pasteTwoDot = decBox.Text;
                    FocusControlReal(form, other);
                    bool pasteTwoDotOk = pasteTwoDot == "1,500,000" && decBox.Value == 2.5m;

                    AssignEditTextIntoNumberBox(form, decBox, "abc");
                    FocusControlReal(form, other);
                    bool alphaKeepOk = decBox.Value == 2.5m && decBox.Text == "2,5";

                    AssignEditTextIntoNumberBox(form, decBox, "");
                    FocusControlReal(form, other);
                    bool emptyKeepOk = decBox.Value == 2.5m && decBox.Text == "2,5";

                    // Lọc phím chữ cái qua WM_CHAR: gõ 'a' không lọt vào TextBox.
                    TypeCharsIntoNumberBox(form, decBox, "5a", true);
                    string filteredTyping = decBox.Text;
                    FocusControlReal(form, other);
                    bool filterKeyOk = filteredTyping == "5" && decBox.Value == 5m;

                    Check("Nhập số P1 [A2b/A6 " + cultureTag + "]: 15.000->15; 1.500.000 giữ đủ 2 dấu & giữ số cũ; dán đổi '.'->','; chữ/trống giữ số cũ",
                        v15Ok && twoDotOk && pasteDotOk && pasteTwoDotOk && alphaKeepOk && emptyKeepOk && filterKeyOk,
                        "15=" + v15Ok + ", 2d=" + typingTwoDot + "/" + twoDotOk + ", pDot=" + pasteDotOk
                        + ", p2d=" + pasteTwoDotOk + ", abc=" + alphaKeepOk + ", emp=" + emptyKeepOk + ", flt=" + filteredTyping);

                    // A10 (N4): 1.128 -> làm tròn AwayFromZero thành 1,13 (Value 1.13m và Text "1,13").
                    TypeCharsIntoNumberBox(form, decBox, "1.128", true);
                    FocusControlReal(form, other);
                    bool n4Ok = decBox.Value == 1.13m && decBox.Text == "1,13";

                    // A10 (N5): ô số nguyên gõ 2.5 giữ số cũ 2, không gọi ValueChanged.
                    int intChanges = 0;
                    intBox.ValueChanged += delegate { intChanges++; };
                    TypeCharsIntoNumberBox(form, intBox, "2.5", true);
                    FocusControlReal(form, other);
                    bool n5Ok = intBox.Value == 2m && intBox.Text == "2" && intChanges == 0;

                    Check("Nhập số P1 [A10 N4/N5 " + cultureTag + "]: 1.128->1,13 (AwayFromZero); ô số nguyên gõ 2.5 giữ 2 & không gọi ValueChanged",
                        n4Ok && n5Ok, "n4=" + decBox.Value + "/" + decBox.Text + ", n5=" + intBox.Value + "/" + intBox.Text + ", ch=" + intChanges);

                    // A3/A4: gán 33.333333m bằng code -> giữ nguyên Value, vào ra không sửa thì Value còn nguyên & ValueChanged không gọi.
                    decBox.Value = 33.333333m;
                    int codeValChanges = 0;
                    EventHandler countHandler = delegate { codeValChanges++; };
                    decBox.ValueChanged += countHandler;
                    string dispCodeVal = decBox.Text;
                    FocusControlReal(form, decBox);
                    FocusControlReal(form, other);
                    decBox.ValueChanged -= countHandler;
                    bool keepExactOk = dispCodeVal == "33,33" && decBox.Value == 33.333333m && codeValChanges == 0;

                    Check("Nhập số P1 [A3/A4 " + cultureTag + "]: gán 33.333333m bằng code, vào ra không sửa giữ nguyên từng chữ số & không gọi ValueChanged",
                        keepExactOk, "disp=" + dispCodeVal + ", val=" + decBox.Value + ", ch=" + codeValChanges);

                    // A9: đọc .Value khi đang gõ dở (chưa rời ô) ra đúng số theo N1; nút mũi tên Up/Down sau khi gõ dở.
                    TypeCharsIntoNumberBox(form, decBox, "7.5", true);
                    decimal liveRead = decBox.Value;
                    TypeCharsIntoNumberBox(form, decBox, "2.5", true);
                    decBox.UpButton();
                    decimal afterUp = decBox.Value;
                    TypeCharsIntoNumberBox(form, decBox, "4.5", true);
                    decBox.DownButton();
                    decimal afterDown = decBox.Value;
                    FocusControlReal(form, other);

                    Check("Nhập số P1 [A9 " + cultureTag + "]: đọc .Value khi đang gõ dở (7.5->7,5) và Up/DownButton sau khi gõ dở (2.5->3,5; 4.5->3,5)",
                        liveRead == 7.5m && afterUp == 3.5m && afterDown == 3.5m,
                        "live=" + liveRead + ", up=" + afterUp + ", down=" + afterDown);

                    // Min/max kẹp, giá trị 0, số âm ở ô cho âm.
                    TypeCharsIntoNumberBox(form, negBox, "-12.5", true);
                    FocusControlReal(form, other);
                    bool negOk = negBox.Value == -12.5m && negBox.Text == "-12,5";
                    TypeCharsIntoNumberBox(form, negBox, "0", true);
                    FocusControlReal(form, other);
                    bool zeroOk = negBox.Value == 0m && negBox.Text == "0";
                    TypeCharsIntoNumberBox(form, negBox, "5000", true);
                    FocusControlReal(form, other);
                    bool maxClampOk = negBox.Value == 1000m && negBox.Text == "1.000";
                    TypeCharsIntoNumberBox(form, negBox, "-5000", true);
                    FocusControlReal(form, other);
                    bool minClampOk = negBox.Value == -1000m && negBox.Text == "-1.000";

                    // Gỡ control đang focus + đang gõ dở "9.5" khỏi form rồi Dispose:
                    // Controls.Remove làm control rời focus -> được phép đọc theo N1 ra đúng 9,5 (không bao giờ là 95),
                    // ValueChanged <= 1, và sau Dispose không gọi thêm ValueChanged, không ném lỗi.
                    bool removeDisposeOk = false;
                    int removeChanges = 0;
                    int removeChangesDuringDispose = 0;
                    decimal removeVal = -1m;
                    try
                    {
                        using (var tempBox = new NumberBox { DecimalPlaces = 2, Minimum = 0m, Maximum = 100m, Value = 1m })
                        {
                            form.Controls.Add(tempBox);
                            tempBox.ValueChanged += delegate { removeChanges++; };
                            TypeCharsIntoNumberBox(form, tempBox, "9.5", true);
                            form.Controls.Remove(tempBox);
                            removeVal = tempBox.Value;
                            int beforeDispose = removeChanges;
                            tempBox.Dispose();
                            decimal valAfterDispose = tempBox.Value;
                            removeChangesDuringDispose = removeChanges - beforeDispose;
                            removeDisposeOk = removeVal == 9.5m && valAfterDispose == 9.5m
                                && removeVal != 95m && removeChanges <= 1 && removeChangesDuringDispose == 0;
                        }
                    }
                    catch (Exception)
                    {
                        removeDisposeOk = false;
                    }

                    // Dispose trực tiếp control đang focus + đang gõ dở "9.5" (không Remove trước):
                    // không ném lỗi, không đọc theo culture Windows (không bao giờ thành 95), ValueChanged <= 1 và sau Dispose không gọi thêm.
                    bool directDisposeOk = false;
                    int directChanges = 0;
                    int directChangesAfterDispose = 0;
                    decimal directVal = -1m;
                    var directBox = new NumberBox { DecimalPlaces = 2, Minimum = 0m, Maximum = 100m, Value = 1m };
                    form.Controls.Add(directBox);
                    directBox.ValueChanged += delegate { directChanges++; };
                    TypeCharsIntoNumberBox(form, directBox, "9.5", true);
                    try
                    {
                        directBox.Dispose();
                        int afterDispose = directChanges;
                        directVal = directBox.Value;
                        directChangesAfterDispose = directChanges - afterDispose;
                        directDisposeOk = (directVal == 1m || directVal == 9.5m) && directVal != 95m
                            && directChanges <= 1 && directChangesAfterDispose == 0;
                    }
                    catch (Exception)
                    {
                        directDisposeOk = false;
                    }

                    Check("Nhập số P1 [biên/âm/0/gỡ & Dispose " + cultureTag + "]: -12.5, 0, kẹp (-1000..1000), gỡ khỏi form ra 9,5 & Dispose trực tiếp an toàn",
                        negOk && zeroOk && maxClampOk && minClampOk && removeDisposeOk && directDisposeOk,
                        "neg=" + negOk + ", zero=" + zeroOk + ", max=" + maxClampOk + ", min=" + minClampOk
                        + ", rm=" + removeDisposeOk + "(v=" + removeVal + ",ch=" + removeChanges + ",dispCh=" + removeChangesDuringDispose + ")"
                        + ", dir=" + directDisposeOk + "(v=" + directVal + ",ch=" + directChanges + ",after=" + directChangesAfterDispose + ")");
                }
                finally
                {
                    form.Close();
                }
            }
        }

        private static void NumberBoxThousandsDialogTests(UserSession session, string bookingId, string cultureTag)
        {
            // 1. BookingEditDialog: ô Số lượng cạnh nút Thêm gõ 1600 -> rời ô hiện "1.600" & Value 1600, vào lại hiện "1600".
            bool bookingOk = false;
            string bookingDetail = "";
            if (!string.IsNullOrEmpty(bookingId))
            {
                using (BookingEditDialog dlg = OpenOffscreenBookingDialog(session, bookingId))
                {
                    try
                    {
                        var qtyBox = dlg.QtyBoxForTest as NumberBox;
                        if (qtyBox != null)
                        {
                            TypeCharsIntoNumberBox(dlg, qtyBox, "1600", true);
                            FocusControlReal(dlg, dlg.TienGiamGiaBoxForTest);
                            string afterLeave = qtyBox.Text;
                            decimal val = qtyBox.Value;
                            FocusControlReal(dlg, qtyBox);
                            string reEdit = qtyBox.Text;
                            bookingOk = qtyBox.ThousandsSeparator && val == 1600m && afterLeave == "1.600" && reEdit == "1600";
                            bookingDetail = afterLeave + "/" + reEdit + "/" + val;
                        }
                    }
                    finally { dlg.Close(); }
                }
            }

            // 2. QuoteEditDialog: ô Số lượng cạnh nút Thêm gõ 1600 -> rời ô hiện "1.600" & Value 1600, vào lại hiện "1600".
            bool quoteOk = false;
            string quoteDetail = "";
            using (QuoteEditDialog dlg = QuoteEditDialog.CreateForPreview(session))
            {
                dlg.StartPosition = FormStartPosition.Manual;
                dlg.Location = new Point(-20000, -20000);
                dlg.Show();
                Application.DoEvents();
                try
                {
                    var qtyBox = dlg.QtyBoxForTest as NumberBox;
                    if (qtyBox != null)
                    {
                        TypeCharsIntoNumberBox(dlg, qtyBox, "1600", true);
                        FocusControlReal(dlg, dlg.SearchBoxForTest);
                        string afterLeave = qtyBox.Text;
                        decimal val = qtyBox.Value;
                        FocusControlReal(dlg, qtyBox);
                        string reEdit = qtyBox.Text;
                        quoteOk = qtyBox.ThousandsSeparator && val == 1600m && afterLeave == "1.600" && reEdit == "1600";
                        quoteDetail = afterLeave + "/" + reEdit + "/" + val;
                    }
                }
                finally { dlg.Close(); }
            }

            // 3. QuantityDialog tạo với 1600: hiện "1.600" khi không đang sửa, "1600" khi đang sửa.
            bool qtyDlgOk = false;
            string qtyDlgDetail = "";
            var sampleItem = new BookingSaleItem { Name = "SP", UnitName = "Ly", BigUnitId = "U2", BigUnitName = "Thùng" };
            using (var qdlg = new QuantityDialog(sampleItem, 1600m))
            {
                NumberBox oddBox = FindFirstNumberBox(qdlg);
                string beforeShow = oddBox == null ? "" : oddBox.Text;
                qdlg.StartPosition = FormStartPosition.Manual;
                qdlg.Location = new Point(-20000, -20000);
                qdlg.Show();
                Application.DoEvents();
                try
                {
                    if (oddBox != null)
                    {
                        FocusControlReal(qdlg, oddBox);
                        string whileFocused = oddBox.Text;
                        Control other = FindFirstOtherSelectable(qdlg, oddBox);
                        if (other != null)
                            FocusControlReal(qdlg, other);
                        string afterLeave = oddBox.Text;
                        qtyDlgOk = other != null && oddBox.ThousandsSeparator && oddBox.Value == 1600m
                            && beforeShow == "1.600" && whileFocused == "1600" && afterLeave == "1.600";
                        qtyDlgDetail = beforeShow + "/" + whileFocused + "/" + afterLeave;
                    }
                }
                finally { qdlg.Close(); }
            }

            // 4. DynamicForm khai ThousandsSeparator = False vẫn hiện ngăn nghìn "15.000".
            bool dynOk = false;
            string dynDetail = "";
            var layout = new ScreenLayout { Width = 360, Height = 100, Table = "TEST" };
            var numCtrl = new LayoutControl { Name = "numSL", Kind = LayoutKind.Number, X = 80, Y = 10, Width = 140, Height = 24, Enabled = true, TabIndex = 0 };
            numCtrl.Props["DecimalPlaces"] = "0";
            numCtrl.Props["ThousandsSeparator"] = "False";
            layout.Controls.Add(numCtrl);
            layout.Maps.Add(new FieldMap { DataField = "SOLUONG", EditControl = "numSL" });
            using (var host = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), Size = new Size(380, 140) })
            {
                var other = new TextBox { Location = new Point(10, 70), Width = 100 };
                var df = new DynamicForm(layout) { Dock = DockStyle.Top, Height = 60 };
                host.Controls.Add(other);
                host.Controls.Add(df);
                host.Show();
                Application.DoEvents();
                try
                {
                    var box = df.Editor("numSL") as NumberBox;
                    if (box != null)
                    {
                        TypeCharsIntoNumberBox(host, box, "15000", true);
                        FocusControlReal(host, other);
                        dynOk = box.ThousandsSeparator && box.Value == 15000m && box.Text == "15.000";
                        dynDetail = box.ThousandsSeparator + "/" + box.Text + "/" + box.Value;
                    }
                }
                finally { host.Close(); }
            }

            Check("Nhập số P1 [N2 ngăn nghìn dialog " + cultureTag + "]: Booking/Quote SL 1600->'1.600'/'1600', QuantityDialog 1600->'1.600', DynamicForm False->'15.000'",
                bookingOk && quoteOk && qtyDlgOk && dynOk,
                "bk=" + bookingDetail + ", qt=" + quoteDetail + ", qd=" + qtyDlgDetail + ", df=" + dynDetail);
        }
    }
}
