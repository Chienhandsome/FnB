using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Luật thuần tự gán cột, kiểm tra ánh xạ cột, phân tích số chặt và nhập dòng Excel cho Báo giá bán hàng
    /// (docs/specs/bao-gia-ban-hang.md §2.3, §5.0 P3). Không phụ thuộc UI hay Aspose.Cells.
    /// </summary>
    public static class QuoteExcelImporter
    {
        private static readonly string[] DiscountForbiddenForPrice = { "giảm", "giam", "chiết khấu", "chiet khau", "discount" };

        /// <summary>Kiểm tra cột bắt buộc trước khi đọc dòng. Trả về null nếu hợp lệ.</summary>
        public static string ValidateMapping(QuoteExcelColumnMap map, bool useProductCode)
        {
            if (map == null || map.ItemCol < 0)
                return useProductCode ? QuoteText.SelectColumnCode : QuoteText.SelectColumnName;
            if (map.QuantityCol < 0)
                return QuoteText.SelectColumnQuantity;
            return null;
        }

        /// <summary>
        /// Tự gán cột theo danh sách tiêu đề: ưu tiên theo thứ tự từ khóa trên toàn bộ tiêu đề;
        /// cột đã gán làm % giảm hoặc tiêu đề chứa "giảm"/"chiết khấu" không bao giờ bị gán làm Đơn giá.
        /// </summary>
        public static QuoteExcelColumnMap AutoDetectColumns(IList<string> headers, bool useProductCode, bool useTwoUnits)
        {
            var map = new QuoteExcelColumnMap();
            if (headers == null || headers.Count == 0)
                return map;

            var used = new HashSet<int>();
            string[] itemKeys = useProductCode
                ? new[] { "mã hàng", "ma hang", "mã", "code" }
                : new[] { "tên hàng", "ten hang", "mặt hàng", "mat hang", "name" };
            map.ItemCol = FindBestColumn(headers, itemKeys, used, null);
            if (map.ItemCol >= 0) used.Add(map.ItemCol);

            map.QuantityCol = FindBestColumn(headers, new[] { "số lượng", "so luong", "sl", "qty", "quantity" }, used, null);
            if (map.QuantityCol >= 0) used.Add(map.QuantityCol);

            if (useTwoUnits)
            {
                map.UnitCol = FindBestColumn(headers, new[] { "đơn vị tính", "don vi tinh", "đvt", "dvt", "unit" }, used, null);
                if (map.UnitCol >= 0) used.Add(map.UnitCol);
            }

            map.DiscountPctCol = FindBestColumn(headers,
                new[] { "% giảm", "tỉ lệ giảm", "ti le giam", "giảm giá", "giam gia", "chiết khấu", "chiet khau", "giảm", "giam", "discount" },
                used, null);
            if (map.DiscountPctCol >= 0) used.Add(map.DiscountPctCol);

            map.PriceCol = FindBestColumn(headers,
                new[] { "đơn giá", "don gia", "giá bán", "gia ban", "giá", "gia", "price" },
                used, DiscountForbiddenForPrice);
            return map;
        }

        private static int FindBestColumn(IList<string> headers, string[] keywords, HashSet<int> used, string[] forbidden)
        {
            for (int k = 0; k < keywords.Length; k++)
            {
                string kw = keywords[k];
                for (int i = 0; i < headers.Count; i++)
                {
                    if (used.Contains(i))
                        continue;
                    string h = (headers[i] ?? "").Trim().ToLowerInvariant();
                    if (h.Length == 0 || ContainsAny(h, forbidden))
                        continue;
                    if (h.IndexOf(kw, StringComparison.Ordinal) >= 0)
                        return i;
                }
            }
            return -1;
        }

        private static bool ContainsAny(string text, string[] terms)
        {
            if (terms == null)
                return false;
            for (int i = 0; i < terms.Length; i++)
            {
                if (text.IndexOf(terms[i], StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>Tiện ích gọi Import với mọi ô là ô chữ (dùng cho test thuần ô chữ).</summary>
        public static QuoteExcelImportResult Import(
            IList<string[]> rows,
            QuoteExcelColumnMap map,
            bool useProductCode,
            bool useTwoUnits,
            IList<QuoteSaleItem> activeItems,
            IList<LookupItem> activeUnits)
        {
            var typed = new List<QuoteExcelCell[]>();
            if (rows != null)
            {
                foreach (string[] r in rows)
                {
                    if (r == null)
                    {
                        typed.Add(null);
                        continue;
                    }
                    var cells = new QuoteExcelCell[r.Length];
                    for (int i = 0; i < r.Length; i++)
                        cells[i] = QuoteExcelCell.TextCell(r[i]);
                    typed.Add(cells);
                }
            }
            return Import(typed, map, useProductCode, useTwoUnits, activeItems, activeUnits);
        }

        /// <summary>
        /// Phân tích các dòng dữ liệu (có cờ IsNumeric từng ô) thành danh sách QuoteLine.
        /// - Chỉ khớp trên danh sách mặt hàng STATUS = 30, không phân biệt hoa thường, trùng khóa lấy dòng đầu.
        /// - Bỏ qua dòng thiếu mã/tên hoặc SL trống / SL = 0. SL âm bỏ qua và đếm vào SkippedNegativeCount.
        /// - Số không đọc được, số mơ hồ (nhóm nghìn một dấu ở cột SL/% giảm), đơn giá âm, % giảm ngoài 0..100
        ///   đều ghi lỗi và hủy toàn bộ đợt nhập (Ok = false, Lines rỗng).
        /// - Ô trống ở Đơn giá hoặc % giảm = 0. Dòng trả về KHÔNG gộp với nhau hay với dòng có sẵn.
        /// </summary>
        public static QuoteExcelImportResult Import(
            IList<QuoteExcelCell[]> rows,
            QuoteExcelColumnMap map,
            bool useProductCode,
            bool useTwoUnits,
            IList<QuoteSaleItem> activeItems,
            IList<LookupItem> activeUnits)
        {
            var result = new QuoteExcelImportResult();
            string mapError = ValidateMapping(map, useProductCode);
            if (mapError != null)
            {
                result.Ok = false;
                result.Errors.Add(mapError);
                return result;
            }

            Dictionary<string, QuoteSaleItem> itemByKey = BuildItemLookup(activeItems, useProductCode);
            Dictionary<string, LookupItem> unitByName = BuildUnitLookup(activeUnits);
            var parsed = new List<QuoteLine>();

            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    QuoteExcelCell[] row = rows[i];
                    string itemKey = ReadCellText(row, map.ItemCol).Trim();
                    if (itemKey.Length == 0)
                    {
                        result.SkippedEmptyOrZeroCount++;
                        continue;
                    }

                    QuoteExcelCell qtyCell = ReadCell(row, map.QuantityCol);
                    string rawQty = (qtyCell == null ? "" : (qtyCell.Text ?? "")).Trim();
                    if (rawQty.Length == 0)
                    {
                        result.SkippedEmptyOrZeroCount++;
                        continue;
                    }

                    decimal qty;
                    if (!TryParseExcelNumber(qtyCell, false, out qty))
                    {
                        result.Errors.Add(QuoteText.ExcelInvalidNumber(rawQty, "Số lượng", itemKey));
                        continue;
                    }
                    if (qty == 0m)
                    {
                        result.SkippedEmptyOrZeroCount++;
                        continue;
                    }
                    if (qty < 0m)
                    {
                        result.SkippedNegativeCount++;
                        continue;
                    }

                    QuoteSaleItem item;
                    if (!itemByKey.TryGetValue(itemKey, out item))
                    {
                        result.Errors.Add(QuoteText.ItemNotFound(itemKey));
                        continue;
                    }

                    bool rowHasError = false;
                    string unitId = item.UnitId;
                    string unitName = item.UnitName ?? "";
                    if (useTwoUnits && map.UnitCol >= 0)
                    {
                        string unitText = ReadCellText(row, map.UnitCol).Trim();
                        if (unitText.Length > 0)
                        {
                            LookupItem unit;
                            if (!unitByName.TryGetValue(unitText, out unit))
                            {
                                result.Errors.Add(QuoteText.UnitNotFound(unitText, itemKey));
                                rowHasError = true;
                            }
                            else if (string.Equals(unit.Id, item.UnitId, StringComparison.Ordinal))
                            {
                                unitId = item.UnitId;
                                unitName = item.UnitName ?? unit.Name;
                            }
                            else if (item.HasBigUnit && string.Equals(unit.Id, item.BigUnitId, StringComparison.Ordinal))
                            {
                                unitId = item.BigUnitId;
                                unitName = item.BigUnitName ?? unit.Name;
                            }
                            else
                            {
                                result.Errors.Add(QuoteText.ItemNoUnit(itemKey, unitText));
                                rowHasError = true;
                            }
                        }
                    }

                    decimal price = 0m;
                    if (map.PriceCol >= 0)
                    {
                        QuoteExcelCell priceCell = ReadCell(row, map.PriceCol);
                        string rawPrice = (priceCell == null ? "" : (priceCell.Text ?? "")).Trim();
                        if (rawPrice.Length > 0)
                        {
                            if (!TryParseExcelNumber(priceCell, true, out price))
                            {
                                result.Errors.Add(QuoteText.ExcelInvalidNumber(rawPrice, "Đơn giá", itemKey));
                                rowHasError = true;
                            }
                            else if (price < 0m)
                            {
                                result.Errors.Add(QuoteText.ExcelInvalidValue(rawPrice, "Đơn giá", itemKey));
                                rowHasError = true;
                            }
                        }
                    }

                    decimal disc = 0m;
                    if (map.DiscountPctCol >= 0)
                    {
                        QuoteExcelCell discCell = ReadCell(row, map.DiscountPctCol);
                        string rawDisc = (discCell == null ? "" : (discCell.Text ?? "")).Trim();
                        if (rawDisc.Length > 0)
                        {
                            if (!TryParseExcelNumber(discCell, false, out disc))
                            {
                                result.Errors.Add(QuoteText.ExcelInvalidNumber(rawDisc, "% giảm", itemKey));
                                rowHasError = true;
                            }
                            else if (disc < 0m || disc > 100m)
                            {
                                result.Errors.Add(QuoteText.ExcelInvalidValue(rawDisc, "% giảm", itemKey));
                                rowHasError = true;
                            }
                        }
                    }

                    if (rowHasError)
                        continue;

                    parsed.Add(new QuoteLine
                    {
                        ProductId = item.Id,
                        ProductCode = item.Code ?? "",
                        ProductName = item.Name ?? "",
                        UnitId = unitId,
                        UnitName = unitName,
                        Quantity = qty,
                        Price = price,
                        DiscountPct = disc,
                        Amount = QuoteCalculator.CalcLineAmount(qty, price, disc),
                        Note = ""
                    });
                }
            }

            if (result.Errors.Count > 0)
            {
                result.Ok = false;
                result.Lines.Clear();
                return result;
            }

            result.Ok = true;
            result.Lines = parsed;
            return result;
        }

        /// <summary>
        /// Phân tích giá trị số của một ô Excel không rỗng.
        /// - Ô số (IsNumeric = true): đọc trực tiếp theo CultureInfo.InvariantCulture (vd. 1.125 không bị coi là mơ hồ).
        /// - Ô chữ (IsNumeric = false):
        ///   * Cột Đơn giá (allowThousandsGrouping = true): nhóm nghìn 1..3 + (.3)+ hoặc (,3)+ là số nguyên;
        ///     có cả hai dấu . và , thì dấu sau cùng là thập phân, phần nhóm trước phải đúng 3 chữ số.
        ///   * Cột Số lượng / % giảm (allowThousandsGrouping = false): nhóm nghìn một dấu như "1.250" là mơ hồ -> lỗi.
        ///   * Còn lại: số nguyên, hoặc một dấu . hay , là thập phân khi phần sau KHÔNG đúng 3 chữ số.
        /// </summary>
        public static bool TryParseExcelNumber(QuoteExcelCell cell, bool allowThousandsGrouping, out decimal value)
        {
            value = 0m;
            if (cell == null)
                return false;
            string s = (cell.Text ?? "").Trim();
            if (s.Length == 0)
                return false;

            if (cell.IsNumeric)
                return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

            decimal sign = 1m;
            string body = s;
            if (body[0] == '-' || body[0] == '+')
            {
                if (body[0] == '-')
                    sign = -1m;
                body = body.Substring(1);
                if (body.Length == 0)
                    return false;
            }

            int dotCount = 0, commaCount = 0, lastDot = -1, lastComma = -1;
            for (int i = 0; i < body.Length; i++)
            {
                char ch = body[i];
                if (ch >= '0' && ch <= '9')
                    continue;
                if (ch == '.') { dotCount++; lastDot = i; }
                else if (ch == ',') { commaCount++; lastComma = i; }
                else return false;
            }

            string normalized;
            if (dotCount == 0 && commaCount == 0)
            {
                normalized = body;
            }
            else if (dotCount > 0 && commaCount > 0)
            {
                char decSep = lastDot > lastComma ? '.' : ',';
                char grpSep = decSep == '.' ? ',' : '.';
                int decCount = decSep == '.' ? dotCount : commaCount;
                int decIdx = decSep == '.' ? lastDot : lastComma;
                int lastGrpIdx = decSep == '.' ? lastComma : lastDot;
                if (decCount != 1 || lastGrpIdx > decIdx)
                    return false;
                string intPart = body.Substring(0, decIdx);
                string fracPart = body.Substring(decIdx + 1);
                if (fracPart.Length == 0 || !IsValidThousandsGroups(intPart.Split(grpSep)))
                    return false;
                normalized = intPart.Replace(grpSep.ToString(), "") + "." + fracPart;
            }
            else
            {
                char sep = dotCount > 0 ? '.' : ',';
                string[] parts = body.Split(sep);
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i].Length == 0)
                        return false;

                if (parts.Length >= 3)
                {
                    if (!allowThousandsGrouping || !IsValidThousandsGroups(parts))
                        return false;
                    normalized = string.Concat(parts);
                }
                else
                {
                    // Đúng 1 dấu . hoặc , (parts.Length == 2).
                    if (parts[1].Length == 3)
                    {
                        if (!allowThousandsGrouping || parts[0].Length > 3)
                            return false;
                        normalized = parts[0] + parts[1];
                    }
                    else
                    {
                        normalized = parts[0] + "." + parts[1];
                    }
                }
            }

            decimal parsed;
            if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out parsed))
                return false;
            value = sign * parsed;
            return true;
        }

        private static bool IsValidThousandsGroups(string[] groups)
        {
            if (groups == null || groups.Length < 2)
                return false;
            if (groups[0].Length < 1 || groups[0].Length > 3)
                return false;
            for (int i = 1; i < groups.Length; i++)
                if (groups[i].Length != 3)
                    return false;
            return true;
        }

        private static Dictionary<string, QuoteSaleItem> BuildItemLookup(IList<QuoteSaleItem> items, bool useProductCode)
        {
            var dict = new Dictionary<string, QuoteSaleItem>(StringComparer.OrdinalIgnoreCase);
            if (items == null) return dict;
            foreach (QuoteSaleItem item in items)
            {
                if (item == null) continue;
                string key = ((useProductCode ? item.Code : item.Name) ?? "").Trim();
                if (key.Length > 0 && !dict.ContainsKey(key))
                    dict[key] = item;
            }
            return dict;
        }

        private static Dictionary<string, LookupItem> BuildUnitLookup(IList<LookupItem> units)
        {
            var dict = new Dictionary<string, LookupItem>(StringComparer.OrdinalIgnoreCase);
            if (units == null) return dict;
            foreach (LookupItem u in units)
            {
                if (u == null) continue;
                string key = (u.Name ?? "").Trim();
                if (key.Length > 0 && !dict.ContainsKey(key))
                    dict[key] = u;
            }
            return dict;
        }

        private static QuoteExcelCell ReadCell(QuoteExcelCell[] row, int colIndex)
        {
            if (row == null || colIndex < 0 || colIndex >= row.Length)
                return null;
            return row[colIndex];
        }

        private static string ReadCellText(QuoteExcelCell[] row, int colIndex)
        {
            QuoteExcelCell cell = ReadCell(row, colIndex);
            return cell == null ? "" : (cell.Text ?? "");
        }
    }
}
