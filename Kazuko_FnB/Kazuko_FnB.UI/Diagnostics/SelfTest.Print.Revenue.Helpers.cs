using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using FastReport;
using FastReport.Table;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class TemplateObjInfo
        {
            public string Name;
            public string BandKind;
            public string RawText;
            public string ExpandedText;
        }

        private static List<TemplateObjInfo> ParseTemplateObjectMap(string templateXml)
        {
            var result = new List<TemplateObjInfo>();
            if (string.IsNullOrWhiteSpace(templateXml))
                return result;

            var doc = new XmlDocument();
            doc.LoadXml(templateXml);

            var totals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            XmlNodeList totalNodes = doc.GetElementsByTagName("Total");
            for (int i = 0; i < totalNodes.Count; i++)
            {
                string name = Attr(totalNodes[i], "Name");
                if (name.Length > 0)
                    totals[name] = Attr(totalNodes[i], "Expression");
            }

            CollectTemplateObjects(doc.GetElementsByTagName("TableCell"), totals, result);
            CollectTemplateObjects(doc.GetElementsByTagName("TextObject"), totals, result);
            return result;
        }

        private static void CollectTemplateObjects(XmlNodeList nodes, Dictionary<string, string> totals, List<TemplateObjInfo> into)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                string name = Attr(nodes[i], "Name");
                if (name.Length == 0)
                    continue;
                string raw = Attr(nodes[i], "Text");
                into.Add(new TemplateObjInfo
                {
                    Name = name,
                    BandKind = ClassifyBandKind(nodes[i]),
                    RawText = raw,
                    ExpandedText = ExpandTotals(raw, totals)
                });
            }
        }

        private static string ClassifyBandKind(XmlNode node)
        {
            for (XmlNode cur = node == null ? null : node.ParentNode; cur != null; cur = cur.ParentNode)
            {
                string n = cur.Name ?? "";
                if (n == "DataBand") return "Data";
                if (n == "ReportSummaryBand" || n == "DataFooterBand" || n == "GroupFooterBand" || n == "PageFooterBand") return "Summary";
                if (n == "ReportTitleBand" || n == "PageHeaderBand" || n == "GroupHeaderBand" || n == "DataHeaderBand") return "Header";
            }
            return "Other";
        }

        private static string ExpandTotals(string expr, Dictionary<string, string> totals)
        {
            string cur = expr ?? "";
            if (cur.IndexOf('[') < 0 || totals.Count == 0)
                return cur;

            var ordered = new List<KeyValuePair<string, string>>(totals);
            ordered.Sort(delegate (KeyValuePair<string, string> a, KeyValuePair<string, string> b)
            {
                return b.Key.Length.CompareTo(a.Key.Length);
            });

            for (int pass = 0; pass < 3; pass++)
            {
                bool changed = false;
                for (int k = 0; k < ordered.Count; k++)
                {
                    string key = ordered[k].Key;
                    string val = ordered[k].Value;
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val) || ContainsFieldToken(val, key))
                        continue;

                    string next = ReplaceFieldToken(cur, key, "(" + val + ")");
                    if (!string.Equals(next, cur, StringComparison.Ordinal))
                    {
                        cur = next;
                        changed = true;
                    }
                }
                if (!changed)
                    break;
            }
            return cur;
        }

        private static string ReplaceFieldToken(string expr, string token, string replacement)
        {
            int idx = expr.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return expr;
            var sb = new System.Text.StringBuilder(expr.Length + 32);
            int pos = 0;
            while (idx >= 0)
            {
                bool leftOk = idx == 0 || !IsIdentChar(expr[idx - 1]);
                int end = idx + token.Length;
                bool rightOk = end == expr.Length || !IsIdentChar(expr[end]);
                if (leftOk && rightOk)
                {
                    sb.Append(expr, pos, idx - pos);
                    sb.Append(replacement);
                    pos = end;
                    idx = expr.IndexOf(token, pos, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    idx = expr.IndexOf(token, idx + 1, StringComparison.OrdinalIgnoreCase);
                }
            }
            if (pos == 0)
                return expr;
            sb.Append(expr, pos, expr.Length - pos);
            return sb.ToString();
        }

        private static string FindObjName(List<TemplateObjInfo> map, string bandKind, string field, string excludeField)
        {
            for (int i = 0; i < map.Count; i++)
            {
                if (map[i].BandKind != bandKind)
                    continue;
                if (!ContainsFieldToken(map[i].ExpandedText, field))
                    continue;
                if (!string.IsNullOrEmpty(excludeField) && ContainsFieldToken(map[i].ExpandedText, excludeField))
                    continue;
                return map[i].Name;
            }
            return "";
        }

        private static string FindObjNameBoth(List<TemplateObjInfo> map, string bandKind, string fieldA, string fieldB)
        {
            for (int i = 0; i < map.Count; i++)
            {
                if (map[i].BandKind == bandKind
                    && ContainsFieldToken(map[i].ExpandedText, fieldA)
                    && ContainsFieldToken(map[i].ExpandedText, fieldB))
                    return map[i].Name;
            }
            return "";
        }

        private static bool ContainsFieldToken(string expr, string token)
        {
            if (string.IsNullOrEmpty(expr) || string.IsNullOrEmpty(token))
                return false;
            int idx = expr.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                bool leftOk = idx == 0 || !IsIdentChar(expr[idx - 1]);
                int end = idx + token.Length;
                bool rightOk = end == expr.Length || !IsIdentChar(expr[end]);
                if (leftOk && rightOk)
                    return true;
                idx = expr.IndexOf(token, idx + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static bool IsIdentChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static bool VerifyHeaderDates(
            List<TemplateObjInfo> objMap,
            List<KeyValuePair<string, string>> pageTexts,
            DateTime fromDate,
            DateTime toDate)
        {
            var headerNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < objMap.Count; i++)
            {
                if (objMap[i].BandKind == "Header"
                    || ContainsFieldToken(objMap[i].RawText, "TuNgay")
                    || ContainsFieldToken(objMap[i].RawText, "DenNgay"))
                    headerNames.Add(objMap[i].Name);
            }

            var joined = new List<string>();
            for (int i = 0; i < pageTexts.Count; i++)
            {
                if (headerNames.Contains(pageTexts[i].Key))
                    joined.Add(pageTexts[i].Value ?? "");
            }
            string allHeader = string.Join(" ", joined.ToArray());
            return ContainsDateText(allHeader, fromDate) && ContainsDateText(allHeader, toDate);
        }

        private static bool ContainsDateText(string text, DateTime date)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            return text.IndexOf(date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal) >= 0
                || text.IndexOf(date.ToString("d/M/yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal) >= 0
                || text.IndexOf(date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal) >= 0
                || text.IndexOf(date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture), StringComparison.Ordinal) >= 0
                || text.IndexOf(date.ToString("d", CultureInfo.CurrentCulture), StringComparison.Ordinal) >= 0;
        }

        private static List<List<KeyValuePair<string, string>>> ExtractPreparedRows(Report preparedReport)
        {
            var rows = new List<List<KeyValuePair<string, string>>>();
            if (preparedReport == null || preparedReport.PreparedPages == null)
                return rows;
            for (int p = 0; p < preparedReport.PreparedPages.Count; p++)
            {
                ReportPage page = preparedReport.PreparedPages.GetPage(p);
                if (page == null)
                    continue;
                var parentOrder = new List<object>();
                var byParent = new Dictionary<object, List<TextObject>>();

                foreach (Base obj in page.AllObjects)
                {
                    var table = obj as TableObject;
                    if (table != null)
                    {
                        for (int r = 0; r < table.RowCount; r++)
                        {
                            var row = new List<KeyValuePair<string, string>>(table.ColumnCount);
                            for (int c = 0; c < table.ColumnCount; c++)
                            {
                                TableCell cell = table[c, r];
                                if (cell != null)
                                    row.Add(new KeyValuePair<string, string>(cell.Name ?? "", cell.Text ?? ""));
                            }
                            if (row.Count > 0)
                                rows.Add(row);
                        }
                        continue;
                    }

                    var t = obj as TextObject;
                    if (t != null && !(t is TableCell))
                    {
                        object key = t.Parent ?? (object)page;
                        List<TextObject> bucket;
                        if (!byParent.TryGetValue(key, out bucket))
                        {
                            bucket = new List<TextObject>();
                            byParent[key] = bucket;
                            parentOrder.Add(key);
                        }
                        bucket.Add(t);
                    }
                }

                for (int g = 0; g < parentOrder.Count; g++)
                {
                    List<TextObject> texts = byParent[parentOrder[g]];
                    texts.Sort(delegate (TextObject a, TextObject b)
                    {
                        int dy = Math.Abs(a.Top - b.Top) <= 6f ? 0 : a.Top.CompareTo(b.Top);
                        return dy != 0 ? dy : a.Left.CompareTo(b.Left);
                    });
                    var line = new List<KeyValuePair<string, string>>();
                    float lineTop = texts[0].Top;
                    for (int i = 0; i < texts.Count; i++)
                    {
                        if (Math.Abs(texts[i].Top - lineTop) > 6f)
                        {
                            rows.Add(line);
                            line = new List<KeyValuePair<string, string>>();
                            lineTop = texts[i].Top;
                        }
                        line.Add(new KeyValuePair<string, string>(texts[i].Name ?? "", texts[i].Text ?? ""));
                    }
                    if (line.Count > 0)
                        rows.Add(line);
                }
            }
            return rows;
        }

        private static List<KeyValuePair<string, string>> FindTotalVisualRow(List<List<KeyValuePair<string, string>>> rows, string summaryObjName)
        {
            if (!string.IsNullOrEmpty(summaryObjName))
            {
                for (int r = rows.Count - 1; r >= 0; r--)
                {
                    for (int c = 0; c < rows[r].Count; c++)
                    {
                        if (string.Equals(rows[r][c].Key, summaryObjName, StringComparison.Ordinal))
                            return rows[r];
                    }
                }
            }
            for (int r = rows.Count - 1; r >= 0; r--)
            {
                if (ExtractRowNumbers(rows[r]).Count == 0)
                    continue;
                for (int c = 0; c < rows[r].Count; c++)
                {
                    if ((rows[r][c].Value ?? "").IndexOf("TỔNG", StringComparison.OrdinalIgnoreCase) >= 0)
                        return rows[r];
                }
            }
            return null;
        }

        private static List<decimal> ExtractRowNumbers(List<KeyValuePair<string, string>> row)
        {
            var nums = new List<decimal>();
            if (row == null)
                return nums;
            for (int i = 0; i < row.Count; i++)
            {
                decimal v;
                if (TryParseCellNumber(row[i].Value, out v))
                    nums.Add(v);
            }
            return nums;
        }

        private static List<decimal> ExtractExcelRowNumbers(List<ExcelCellVal> row)
        {
            var nums = new List<decimal>();
            if (row == null)
                return nums;
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i].NumericValue.HasValue)
                    nums.Add(row[i].NumericValue.Value);
                else
                {
                    decimal v;
                    if (TryParseReportNumber(row[i].Text, out v))
                        nums.Add(v);
                }
            }
            return nums;
        }

        private static List<ExcelCellVal> FindExcelNumericRowIgnoreCase(List<List<ExcelCellVal>> rows, string needle)
        {
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (ExtractExcelRowNumbers(rows[i]).Count == 0)
                    continue;
                for (int c = 0; c < rows[i].Count; c++)
                {
                    if ((rows[i][c].Text ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        return rows[i];
                }
            }
            return null;
        }

        private static List<decimal> ExtractNonZeroNumbers(List<decimal> nums)
        {
            var res = new List<decimal>();
            for (int i = 0; i < nums.Count; i++)
                if (nums[i] != 0m) res.Add(nums[i]);
            return res;
        }

        private static bool SequenceEquals(List<decimal> actual, decimal[] expected)
        {
            if (actual == null || expected == null || actual.Count != expected.Length)
                return false;
            for (int i = 0; i < expected.Length; i++)
                if (actual[i] != expected[i]) return false;
            return true;
        }

        private static List<string> FindAllObjectTexts(List<KeyValuePair<string, string>> items, string objectName)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(objectName))
                return list;
            for (int i = 0; i < items.Count; i++)
                if (string.Equals(items[i].Key, objectName, StringComparison.Ordinal))
                    list.Add(items[i].Value ?? "");
            return list;
        }

        private static string FindLastObjectText(List<KeyValuePair<string, string>> items, string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return "";
            for (int i = items.Count - 1; i >= 0; i--)
                if (string.Equals(items[i].Key, objectName, StringComparison.Ordinal))
                    return items[i].Value ?? "";
            return "";
        }

        private static bool TryParseCellNumber(string raw, out decimal value)
        {
            if (TryParseReportNumber(raw, out value))
                return true;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                int colon = raw.LastIndexOf(':');
                if (colon >= 0 && colon + 1 < raw.Length)
                    return TryParseReportNumber(raw.Substring(colon + 1), out value);
            }
            value = 0m;
            return false;
        }

        private static string Attr(XmlNode node, string name)
        {
            if (node == null || node.Attributes == null)
                return "";
            XmlAttribute a = node.Attributes[name];
            return a == null ? "" : (a.Value ?? "");
        }

        private static void SelectStoreInCombo(ComboBox combo, string storeId)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var st = combo.Items[i] as StoreRecord;
                if (st != null && string.Equals(st.Id, storeId, StringComparison.Ordinal))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string MakeSafeFileName(string name)
        {
            string s = string.IsNullOrWhiteSpace(name) ? "report" : name.Trim();
            foreach (char ch in Path.GetInvalidFileNameChars())
                s = s.Replace(ch, '_');
            return s;
        }

        private static string FormatDecimals(List<decimal> nums)
        {
            var parts = new List<string>(nums.Count);
            for (int i = 0; i < nums.Count; i++)
                parts.Add(nums[i].ToString(CultureInfo.InvariantCulture));
            return "[" + string.Join(", ", parts.ToArray()) + "]";
        }

        private static string DumpMatchingTexts(List<KeyValuePair<string, string>> items, string needle)
        {
            var parts = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                if ((items[i].Value ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    parts.Add(items[i].Key + "=" + items[i].Value);
            }
            return string.Join(" | ", parts.ToArray());
        }
    }
}
