using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using FastReport;
using FastReport.Barcode;
using FastReport.Table;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class BarcodePrintFixture
        {
            public string GroupId;
            public string UnitId;
            public string Product1Id;
            public string Product2Id;
            public string Product3Id;
            public string EmptyCodeProductId;
            public string DeletedProductId;
        }

        private sealed class BarcodeSlotMap
        {
            public int LabelsPerRow;
            public string[] NameObjNames;
            public string[] PriceObjNames;
            public string[] BarcodeObjNames;
            public string[] CodeTextObjNames;
        }

        private static BarcodePrintFixture CreateBarcodePrintFixture(string db)
        {
            var fx = new BarcodePrintFixture
            {
                GroupId = DbHelper.NewId(),
                UnitId = DbHelper.NewId(),
                Product1Id = DbHelper.NewId(),
                Product2Id = DbHelper.NewId(),
                Product3Id = DbHelper.NewId(),
                EmptyCodeProductId = DbHelper.NewId(),
                DeletedProductId = DbHelper.NewId()
            };

            BookingExec(db,
                "INSERT INTO \"DNHOMMATHANG\" (\"ID\", \"NAME\", \"CODE\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @c, 30, 'selftest_p4')",
                "@id", fx.GroupId, "@n", "NHÓM MÃ VẠCH P4", "@c", "P4GRP");
            BookingExec(db,
                "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest_p4')",
                "@id", fx.UnitId, "@n", "Ly");

            InsertBarcodeTestProduct(db, fx.Product1Id, "8931001", "Cà phê sữa đá P4", fx.GroupId, fx.UnitId, 25000m, 111111m, 122222m, 1333m);
            InsertBarcodeTestProduct(db, fx.Product2Id, "8931002", "Trà đào cam sả P4", fx.GroupId, fx.UnitId, 35000m, 211111m, 222222m, 2333m);
            InsertBarcodeTestProduct(db, fx.Product3Id, "8931003", "Bánh flan phô mai P4", fx.GroupId, fx.UnitId, 45000m, 311111m, 322222m, 3333m);
            InsertBarcodeTestProduct(db, fx.EmptyCodeProductId, "", "Nước suối không mã P4", fx.GroupId, fx.UnitId, 10000m, 5000m, 5500m, 100m);
            InsertBarcodeTestProduct(db, fx.DeletedProductId, "8931099", "Mặt hàng sắp xóa P4", fx.GroupId, fx.UnitId, 15000m, 7000m, 7500m, 200m);

            return fx;
        }

        private static void InsertBarcodeTestProduct(
            string db,
            string id,
            string code,
            string name,
            string groupId,
            string unitId,
            decimal sellPrice,
            decimal buyPrice,
            decimal costPrice,
            decimal commission)
        {
            BookingExec(db,
                "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", " +
                "\"GIABAN\", \"GIABAN2\", \"GIABAN3\", \"GIABAN4\", \"GIANHAP\", \"GIAVON\", \"HOAHONG\", " +
                "\"TONTOITHIEU\", \"TONTOIDA\", \"BAOHANH\", \"NOTE\", \"STATUS\", \"USERCREATEDID\") " +
                "VALUES (@id, @c, @n, @g, @u, @gb, @gb2, @gb3, @gb4, @gn, @gv, @hh, 2, 20, '12T', 'Ghi chú P4', 30, 'selftest_p4')",
                "@id", id, "@c", code, "@n", name, "@g", groupId, "@u", unitId,
                "@gb", sellPrice, "@gb2", sellPrice + 2000m, "@gb3", sellPrice + 4000m, "@gb4", sellPrice + 6000m,
                "@gn", buyPrice, "@gv", costPrice, "@hh", commission);
        }

        private static void CleanupBarcodePrintFixture(string db, BarcodePrintFixture fx)
        {
            if (fx == null)
                return;
            string[] productIds = { fx.Product1Id, fx.Product2Id, fx.Product3Id, fx.EmptyCodeProductId, fx.DeletedProductId };
            for (int i = 0; i < productIds.Length; i++)
            {
                try { BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" = @id", "@id", productIds[i]); } catch { }
            }
            try { BookingExec(db, "DELETE FROM \"DNHOMMATHANG\" WHERE \"ID\" = @id", "@id", fx.GroupId); } catch { }
            try { BookingExec(db, "DELETE FROM \"DDONVITINH\" WHERE \"ID\" = @id", "@id", fx.UnitId); } catch { }
        }

        private static string SnapshotDmathang(string db, string skipId = null)
        {
            return DbHelper.Read(db, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, CODE, NAME, GIABAN, GIANHAP, GIAVON, STATUS FROM DMATHANG ORDER BY ID");
                var sb = new StringBuilder();
                foreach (DataRow r in dt.Rows)
                {
                    if (skipId != null && DbHelper.Str(r["ID"]) == skipId)
                        continue;
                    sb.Append(DbHelper.Str(r["ID"])).Append('|')
                      .Append(DbHelper.Str(r["CODE"])).Append('|')
                      .Append(DbHelper.Str(r["NAME"])).Append('|')
                      .Append(DbHelper.Dec(r["GIABAN"]).ToString(CultureInfo.InvariantCulture)).Append('|')
                      .Append(DbHelper.Dec(r["GIANHAP"]).ToString(CultureInfo.InvariantCulture)).Append('|')
                      .Append(DbHelper.Dec(r["GIAVON"]).ToString(CultureInfo.InvariantCulture)).Append('|')
                      .Append(DbHelper.Int(r["STATUS"])).Append('\n');
                }
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                    return sb.Length + ":" + BitConverter.ToString(hash).Replace("-", "");
                }
            });
        }

        private static BarcodeSlotMap ParseBarcodeTemplateSlots(PrintTemplateRecord template, int labelsPerRow)
        {
            var map = new BarcodeSlotMap
            {
                LabelsPerRow = labelsPerRow,
                NameObjNames = new string[labelsPerRow],
                PriceObjNames = new string[labelsPerRow],
                BarcodeObjNames = new string[labelsPerRow],
                CodeTextObjNames = new string[labelsPerRow]
            };

            string xml = template == null ? "" : (template.TemplateXml ?? "");
            if (string.IsNullOrWhiteSpace(xml))
                return map;

            List<TemplateObjInfo> textObjs = ParseTemplateObjectMap(xml);
            for (int k = 0; k < labelsPerRow; k++)
            {
                string nameToken = "NAME" + k.ToString(CultureInfo.InvariantCulture);
                string priceToken = "GIABAN" + k.ToString(CultureInfo.InvariantCulture);
                string codeToken = "CODE" + k.ToString(CultureInfo.InvariantCulture);

                map.NameObjNames[k] = FindObjName(textObjs, "Data", nameToken, null);
                map.PriceObjNames[k] = FindObjName(textObjs, "Data", priceToken, null);
                map.CodeTextObjNames[k] = FindObjName(textObjs, "Data", codeToken, null);
            }

            var doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList bcNodes = doc.GetElementsByTagName("BarcodeObject");
            for (int i = 0; i < bcNodes.Count; i++)
            {
                string objName = Attr(bcNodes[i], "Name");
                string dataCol = Attr(bcNodes[i], "DataColumn");
                string textExpr = Attr(bcNodes[i], "Text");
                if (string.IsNullOrEmpty(objName))
                    continue;

                for (int k = 0; k < labelsPerRow; k++)
                {
                    string codeToken = "CODE" + k.ToString(CultureInfo.InvariantCulture);
                    if (string.IsNullOrEmpty(map.BarcodeObjNames[k])
                        && (ContainsFieldToken(dataCol, codeToken) || ContainsFieldToken(textExpr, codeToken)))
                    {
                        map.BarcodeObjNames[k] = objName;
                    }
                }
            }
            return map;
        }

        private static List<KeyValuePair<string, string>> ExtractPreparedBarcodes(Report preparedReport)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (preparedReport == null || preparedReport.PreparedPages == null)
                return result;

            var visited = new HashSet<object>();
            int count = preparedReport.PreparedPages.Count;
            for (int p = 0; p < count; p++)
            {
                ReportPage page = preparedReport.PreparedPages.GetPage(p);
                if (page == null)
                    continue;
                foreach (Base obj in page.AllObjects)
                {
                    var table = obj as TableObject;
                    if (table != null)
                    {
                        for (int r = 0; r < table.RowCount; r++)
                        {
                            for (int c = 0; c < table.ColumnCount; c++)
                            {
                                TableCell cell = table[c, r];
                                if (cell == null || cell.ChildObjects == null)
                                    continue;
                                foreach (Base child in cell.ChildObjects)
                                {
                                    var childBc = child as BarcodeObject;
                                    if (childBc != null && visited.Add(childBc))
                                        result.Add(new KeyValuePair<string, string>(childBc.Name ?? "", childBc.Text ?? ""));
                                }
                            }
                        }
                        continue;
                    }

                    var bc = obj as BarcodeObject;
                    if (bc != null && visited.Add(bc))
                        result.Add(new KeyValuePair<string, string>(bc.Name ?? "", bc.Text ?? ""));
                }
            }
            return result;
        }

        private static bool TryExtractBarcodePrice(string raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string[] lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                int idx = line.IndexOf("Giá", StringComparison.OrdinalIgnoreCase);
                string candidate = idx >= 0 ? line.Substring(idx + 3) : line;
                string numPart = ExtractDigitSpan(candidate);
                if (!string.IsNullOrEmpty(numPart) && TryParseReportNumber(numPart, out value))
                    return true;
            }
            return false;
        }

        private static string ExtractDigitSpan(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            int start = -1;
            int end = -1;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (start < 0)
                {
                    if ((ch >= '0' && ch <= '9') || ch == '-')
                    {
                        start = i;
                        end = i;
                    }
                }
                else
                {
                    if ((ch >= '0' && ch <= '9') || ch == '.' || ch == ',')
                        end = i;
                    else
                        break;
                }
            }
            return start >= 0 ? s.Substring(start, end - start + 1) : "";
        }

        private static bool MatchesBarcodeText(string actual, string expectedCode)
        {
            string a = (actual ?? "").Trim();
            string e = (expectedCode ?? "").Trim();
            if (e.Length == 0)
                return a.Length == 0;
            if (a.Length == 0)
                return false;
            if (string.Equals(a, e, StringComparison.Ordinal))
                return true;
            if (a.StartsWith(e, StringComparison.Ordinal))
                return true;
            string trimmed = a.TrimStart('0');
            return trimmed.StartsWith(e, StringComparison.Ordinal);
        }

        private static bool ControlTreeHasAnyText(Control root, params string[] forbiddenSubstrings)
        {
            if (root == null)
                return false;
            string txt = root.Text ?? "";
            for (int i = 0; i < forbiddenSubstrings.Length; i++)
            {
                if (txt.IndexOf(forbiddenSubstrings[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            foreach (Control child in root.Controls)
            {
                if (ControlTreeHasAnyText(child, forbiddenSubstrings))
                    return true;
            }
            return false;
        }
    }
}
