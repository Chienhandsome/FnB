using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using FastReport;
using FastReport.Table;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class KqkdPreparedRow
        {
            public string ItemNumber;
            public string Content;
            public string PctDt;
            public string CurrentText;
            public string PctMain;
            public string PctThird;
            public string SymbolText;
            public string SymbolFontName;
            public string ChangeText;
            public string PrevText;
            public int ColumnCount;
        }

        private static Dictionary<string, string> SnapshotSreport(string db)
        {
            return DbHelper.Read(db, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, LASTTEMPLATEID FROM SREPORT ORDER BY ID");
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["ID"]) ?? "";
                    map[id] = (DbHelper.Str(r["NAME"]) ?? "") + "|" + (DbHelper.Str(r["LASTTEMPLATEID"]) ?? "");
                }
                return map;
            });
        }

        private static Dictionary<string, string> SnapshotSreportTemplate(string db)
        {
            return DbHelper.Read(db, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, SREPORTID, TEMPLATE FROM SREPORTTEMPLATE ORDER BY ID");
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                using (SHA256 sha = SHA256.Create())
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        string id = DbHelper.Str(r["ID"]) ?? "";
                        object raw = r["TEMPLATE"];
                        byte[] bytes;
                        if (raw == null || raw is DBNull)
                            bytes = new byte[0];
                        else if (raw is byte[])
                            bytes = (byte[])raw;
                        else
                            bytes = Encoding.UTF8.GetBytes(Convert.ToString(raw) ?? "");
                        string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                        map[id] = (DbHelper.Str(r["NAME"]) ?? "") + "|" + (DbHelper.Str(r["SREPORTID"]) ?? "") + "|"
                            + bytes.Length + "|" + hash;
                    }
                }
                return map;
            });
        }

        /// <summary>
        /// Đọc các dòng dữ liệu thật trên trang đã Prepare của mẫu KQKD (bỏ qua dòng tiêu đề/chú thích có cột đầu rỗng),
        /// giữ nguyên thứ tự hiển thị, chữ từng ô và tên Font của ô biểu tượng BIEUTUONG.
        /// </summary>
        private static List<KqkdPreparedRow> ExtractKqkdPreparedRows(Report preparedReport)
        {
            var result = new List<KqkdPreparedRow>();
            if (preparedReport == null || preparedReport.PreparedPages == null)
                return result;

            int pageCount = preparedReport.PreparedPages.Count;
            for (int p = 0; p < pageCount; p++)
            {
                ReportPage page = preparedReport.PreparedPages.GetPage(p);
                if (page == null)
                    continue;

                foreach (Base obj in page.AllObjects)
                {
                    var table = obj as TableObject;
                    if (table == null || (table.ColumnCount != 9 && table.ColumnCount != 10))
                        continue;

                    for (int r = 0; r < table.RowCount; r++)
                    {
                        string itemNum = CellStr(table, 0, r).Trim();
                        if (itemNum.Length == 0)
                            continue;

                        if (table.ColumnCount == 9)
                        {
                            TableCell symCell = table[5, r];
                            result.Add(new KqkdPreparedRow
                            {
                                ItemNumber = itemNum,
                                Content = CellStr(table, 1, r).Trim(),
                                PctDt = "",
                                CurrentText = CellStr(table, 2, r).Trim(),
                                PctMain = CellStr(table, 3, r).Trim(),
                                PctThird = CellStr(table, 4, r).Trim(),
                                SymbolText = symCell == null ? "" : (symCell.Text ?? "").Trim(),
                                SymbolFontName = symCell != null && symCell.Font != null ? (symCell.Font.Name ?? "") : "",
                                ChangeText = CellStr(table, 6, r).Trim(),
                                PrevText = CellStr(table, 8, r).Trim(),
                                ColumnCount = 9
                            });
                        }
                        else
                        {
                            TableCell symCell = table[6, r];
                            result.Add(new KqkdPreparedRow
                            {
                                ItemNumber = itemNum,
                                Content = CellStr(table, 1, r).Trim(),
                                PctDt = CellStr(table, 2, r).Trim(),
                                CurrentText = CellStr(table, 3, r).Trim(),
                                PctMain = CellStr(table, 4, r).Trim(),
                                PctThird = CellStr(table, 5, r).Trim(),
                                SymbolText = symCell == null ? "" : (symCell.Text ?? "").Trim(),
                                SymbolFontName = symCell != null && symCell.Font != null ? (symCell.Font.Name ?? "") : "",
                                ChangeText = CellStr(table, 7, r).Trim(),
                                PrevText = CellStr(table, 9, r).Trim(),
                                ColumnCount = 10
                            });
                        }
                    }
                }
            }
            return result;
        }

        private static string CellStr(TableObject table, int col, int row)
        {
            if (table == null || col < 0 || col >= table.ColumnCount || row < 0 || row >= table.RowCount)
                return "";
            TableCell cell = table[col, row];
            return cell == null ? "" : (cell.Text ?? "");
        }

        private static int FindKqkdRow(List<KqkdPreparedRow> rows, string content, int startIndex)
        {
            if (rows == null || startIndex < 0)
                return -1;
            for (int i = startIndex; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Content, content, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static bool CheckPreparedCellNumber(
            List<KeyValuePair<string, string>> pageTexts,
            string cellName,
            decimal expected,
            out string actualText)
        {
            actualText = FindLastObjectText(pageTexts, cellName);
            decimal parsed;
            return TryParseCellNumber(actualText, out parsed) && parsed == expected;
        }

        private static bool CheckPreparedDecimalEquals(string text, decimal expected)
        {
            decimal parsed;
            return TryParseCellNumber(text, out parsed) && parsed == expected;
        }

        private static void TestEmbeddedCommonBehaviors(string db)
        {
            var adminSession = new UserSession
            {
                UserId = "selftest", UserName = "selftest", DisplayName = "Quản trị",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };

            // 1. DailyActivityView: thiếu LASTTEMPLATEID, mẫu hỏng, khóa tải chồng, Dispose sau 5 lần tải, đổi theme lúc chạy.
            using (var host = MakeOffscreenHost())
            {
                var view = new DailyActivityView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    int disposedStart = view.DisposedReportCountForTest;
                    for (int i = 0; i < 5; i++)
                    {
                        view.LoadButtonForTest.PerformClick();
                        Application.DoEvents();
                    }
                    bool dispose5Ok = view.DisposedReportCountForTest == disposedStart + 5
                        && view.CurrentReportForTest != null
                        && ReferenceEquals(view.CurrentReportForTest.Preview, view.PreviewControlForTest);

                    bool disabledDuringLoad = false;
                    int countInsideHook = 0;
                    int countAfterNestedCall = 0;
                    view.OnBeforePrepareForTest = delegate
                    {
                        disabledDuringLoad = !view.LoadButtonForTest.Enabled;
                        countInsideHook = view.LoadCountForTest;
                        view.ReloadAll();
                        countAfterNestedCall = view.LoadCountForTest;
                    };
                    view.LoadButtonForTest.PerformClick();
                    view.OnBeforePrepareForTest = null;
                    bool busyLockOk = disabledDuringLoad && countAfterNestedCall == countInsideHook && view.LoadButtonForTest.Enabled;

                    view.TemplateLoaderForTest = delegate (string rid, string rname, out string err)
                    {
                        err = PrintTemplateRepository.FormatMissingLastTemplateIdError(rname);
                        return null;
                    };
                    view.LoadButtonForTest.PerformClick();
                    string errMissingLast = view.LastErrorMessageForTest;

                    view.TemplateLoaderForTest = delegate (string rid, string rname, out string err)
                    {
                        err = null;
                        return new PrintTemplateRecord { Id = "bad", Name = "Mẫu CTHD hỏng", TemplateXml = "<Report><Loi" };
                    };
                    view.LoadButtonForTest.PerformClick();
                    string errCorrupt = view.LastErrorMessageForTest;
                    view.TemplateLoaderForTest = null;

                    bool errOk = !string.IsNullOrEmpty(errMissingLast)
                        && errMissingLast.IndexOf(DailyActivityConstants.FunctionName, StringComparison.Ordinal) >= 0
                        && !string.IsNullOrEmpty(errCorrupt)
                        && errCorrupt.IndexOf("Mẫu CTHD hỏng", StringComparison.Ordinal) >= 0;

                    ThemeManager.Apply(BuiltInThemes.Ocean);
                    Application.DoEvents();
                    bool themeOk = view.BackColor == BuiltInThemes.Ocean.Background
                        && view.TopBarForTest.BackColor == BuiltInThemes.Ocean.Background
                        && view.LoadButtonForTest.FillColor == BuiltInThemes.Ocean.Primary;

                    Check("In P1b Chi tiết HĐ ngày (T6 lỗi mẫu/thiếu LASTTEMPLATEID, khóa tải chồng, Dispose 5 lần, đổi theme lúc chạy)",
                        dispose5Ok && busyLockOk && errOk && themeOk,
                        "disp=" + dispose5Ok + ", lock=" + busyLockOk + ", err=" + errOk + ", theme=" + themeOk);
                }
                finally { host.Close(); }
            }

            // 2. BusinessResultView: thiếu LASTTEMPLATEID, mẫu hỏng, khóa tải chồng, Dispose sau 5 lần tải, đổi theme lúc chạy.
            using (var host = MakeOffscreenHost())
            {
                var view = new BusinessResultView(adminSession) { Dock = DockStyle.Fill, SuppressErrorPopup = true };
                host.Controls.Add(view);
                host.Show();
                Application.DoEvents();
                try
                {
                    int disposedStart = view.DisposedReportCountForTest;
                    for (int i = 0; i < 5; i++)
                    {
                        view.LoadButtonForTest.PerformClick();
                        Application.DoEvents();
                    }
                    bool dispose5Ok = view.DisposedReportCountForTest == disposedStart + 5
                        && view.CurrentReportForTest != null
                        && ReferenceEquals(view.CurrentReportForTest.Preview, view.PreviewControlForTest);

                    bool disabledDuringLoad = false;
                    int countInsideHook = 0;
                    int countAfterNestedCall = 0;
                    view.OnBeforePrepareForTest = delegate
                    {
                        disabledDuringLoad = !view.LoadButtonForTest.Enabled;
                        countInsideHook = view.LoadCountForTest;
                        view.ReloadAll();
                        countAfterNestedCall = view.LoadCountForTest;
                    };
                    view.LoadButtonForTest.PerformClick();
                    view.OnBeforePrepareForTest = null;
                    bool busyLockOk = disabledDuringLoad && countAfterNestedCall == countInsideHook && view.LoadButtonForTest.Enabled;

                    view.TemplateLoaderForTest = delegate (string rid, string rname, out string err)
                    {
                        err = PrintTemplateRepository.FormatMissingLastTemplateIdError(rname);
                        return null;
                    };
                    view.LoadButtonForTest.PerformClick();
                    string errMissingLast = view.LastErrorMessageForTest;

                    view.TemplateLoaderForTest = delegate (string rid, string rname, out string err)
                    {
                        err = null;
                        return new PrintTemplateRecord { Id = "bad", Name = "Mẫu KQKD hỏng", TemplateXml = "<Report><Loi" };
                    };
                    view.LoadButtonForTest.PerformClick();
                    string errCorrupt = view.LastErrorMessageForTest;
                    view.TemplateLoaderForTest = null;

                    bool errOk = !string.IsNullOrEmpty(errMissingLast)
                        && errMissingLast.IndexOf(BusinessResultConstants.FunctionName, StringComparison.Ordinal) >= 0
                        && !string.IsNullOrEmpty(errCorrupt)
                        && errCorrupt.IndexOf("Mẫu KQKD hỏng", StringComparison.Ordinal) >= 0;

                    ThemeManager.Apply(BuiltInThemes.Ocean);
                    Application.DoEvents();
                    bool themeOk = view.BackColor == BuiltInThemes.Ocean.Background
                        && view.TopBarForTest.BackColor == BuiltInThemes.Ocean.Background
                        && view.LoadButtonForTest.FillColor == BuiltInThemes.Ocean.Primary;

                    Check("In P1b KQKD (T6 lỗi mẫu/thiếu LASTTEMPLATEID, khóa tải chồng, Dispose 5 lần, đổi theme lúc chạy)",
                        dispose5Ok && busyLockOk && errOk && themeOk,
                        "disp=" + dispose5Ok + ", lock=" + busyLockOk + ", err=" + errOk + ", theme=" + themeOk);
                }
                finally { host.Close(); }
            }

            // 3. preview.dailyactivity & preview.businessresult qua ScreenFactory.Create (--render-screen).
            bool pvDailyOk = CheckEmbeddedPreviewForm("preview.dailyactivity", adminSession);
            bool pvKqkdOk = CheckEmbeddedPreviewForm("preview.businessresult", adminSession);
            Check("In P1b Nhúng (--render-screen preview.dailyactivity & preview.businessresult): dựng trang FastReport nhúng không lỗi",
                pvDailyOk && pvKqkdOk, "daily=" + pvDailyOk + ", kqkd=" + pvKqkdOk);
        }

        private static bool CheckEmbeddedPreviewForm(string previewId, UserSession session)
        {
            using (Form pvForm = Dialogs.ScreenFactory.Create(previewId, session))
            {
                if (pvForm == null)
                    return false;
                pvForm.StartPosition = FormStartPosition.Manual;
                pvForm.ShowInTaskbar = false;
                pvForm.Location = new Point(-20000, -20000);
                pvForm.Show();
                Application.DoEvents();
                try
                {
                    var pv = pvForm as Dialogs.IScreenPreview;
                    if (pv != null)
                        pv.Preview("bottom");
                    pvForm.PerformLayout();
                    Application.DoEvents();

                    var dailyForm = pvForm as Dialogs.DailyActivityPreviewForm;
                    if (dailyForm != null)
                        return dailyForm.ViewForTest.CurrentReportForTest != null
                            && dailyForm.ViewForTest.CurrentReportForTest.PreparedPages.Count > 0;
                    var brForm = pvForm as Dialogs.BusinessResultPreviewForm;
                    if (brForm != null)
                        return brForm.ViewForTest.CurrentReportForTest != null
                            && brForm.ViewForTest.CurrentReportForTest.PreparedPages.Count > 0;
                    return false;
                }
                finally { pvForm.Close(); }
            }
        }
    }
}
