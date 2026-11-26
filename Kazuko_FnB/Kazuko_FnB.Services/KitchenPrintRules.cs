using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Giao diện lưu/đọc cấu hình máy in bếp và danh sách máy in cài đặt trên máy.
    /// Selftest thay bằng bản bộ nhớ để kiểm mọi nhánh mà không đụng đĩa/registry hay máy in Windows thật.
    /// </summary>
    public interface IKitchenPrinterStore
    {
        List<string> GetInstalledPrinters();
        string GetConfiguredPrinter(string key);
        void SetConfiguredPrinter(string key, string printerName);
    }

    /// <summary>
    /// Lưu cấu hình máy in bếp vào file cục bộ trong %LOCALAPPDATA%\KazukoFnB\kitchen-printers.dat
    /// (thay thế EasyReg của app gốc mà không sửa registry hay CSDL).
    /// </summary>
    public sealed class FileKitchenPrinterStore : IKitchenPrinterStore
    {
        private readonly Func<List<string>> _installedPrintersProvider;
        private readonly string _filePath;

        public FileKitchenPrinterStore(Func<List<string>> installedPrintersProvider, string filePath = null)
        {
            _installedPrintersProvider = installedPrintersProvider;
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                _filePath = filePath;
            }
            else
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KazukoFnB");
                _filePath = Path.Combine(dir, "kitchen-printers.dat");
            }
        }

        public List<string> GetInstalledPrinters()
        {
            if (_installedPrintersProvider == null)
                return new List<string>();
            try
            {
                return _installedPrintersProvider() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public string GetConfiguredPrinter(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";
            Dictionary<string, string> map = LoadAll();
            string raw;
            if (!map.TryGetValue(key.Trim(), out raw) || string.IsNullOrWhiteSpace(raw))
                return "";
            List<string> installed = GetInstalledPrinters();
            for (int i = 0; i < installed.Count; i++)
            {
                if (string.Equals(installed[i], raw.Trim(), StringComparison.OrdinalIgnoreCase))
                    return installed[i];
            }
            return "";
        }

        public void SetConfiguredPrinter(string key, string printerName)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            Dictionary<string, string> map = LoadAll();
            map[key.Trim()] = (printerName ?? "").Trim();
            SaveAll(map);
        }

        private Dictionary<string, string> LoadAll()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(_filePath))
                    return map;
                string[] lines = File.ReadAllLines(_filePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (k.Length > 0)
                        map[k] = v;
                }
            }
            catch
            {
            }
            return map;
        }

        private void SaveAll(Dictionary<string, string> map)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var lines = new List<string>(map.Count);
                foreach (KeyValuePair<string, string> kv in map)
                    lines.Add(kv.Key + "=" + (kv.Value ?? ""));
                File.WriteAllLines(_filePath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Luật nghiệp vụ thuần (không SQL, không WinForms) cho in bếp/chế biến (theo goc-InCheBien-code.cs.txt &amp;
    /// pos-tap-nghiep-vu-dat-phong-in-bep.md §3) và phân tuyến máy in hóa đơn theo khu vực (goc-PrintInvoice).
    /// </summary>
    public static class KitchenPrintRules
    {
        public static string AreaInvoicePrinterKey(string areaId)
        {
            return "IN_HD_KV_" + (areaId ?? "");
        }

        public static string ResolveKey(bool byArea, string areaId, string drinkTypeId)
        {
            if (byArea)
            {
                string prefix = (areaId ?? "") + "_";
                switch (drinkTypeId)
                {
                    case ServicePrintConstants.DrinkTypeFood: return prefix + "AN";
                    case ServicePrintConstants.DrinkTypeDrink: return prefix + "UONG";
                    case ServicePrintConstants.DrinkTypeOther: return prefix + "KHAC";
                    case ServicePrintConstants.DrinkTypeService: return prefix + "DICHVU";
                    default: return prefix + "UONG";
                }
            }
            switch (drinkTypeId)
            {
                case ServicePrintConstants.DrinkTypeFood: return ServicePrintConstants.KeyPrinterFood;
                case ServicePrintConstants.DrinkTypeDrink: return ServicePrintConstants.KeyPrinterDrink;
                case ServicePrintConstants.DrinkTypeOther: return ServicePrintConstants.KeyPrinterOther;
                case ServicePrintConstants.DrinkTypeService: return ServicePrintConstants.KeyPrinterService;
                default: return ServicePrintConstants.KeyPrinterDrink;
            }
        }

        /// <summary>
        /// Tính chênh lệch số lượng cần in xuống bếp (InCheBienTs.GetSqlInCheBien):
        /// cộng SL trên bill trừ SL đã ghi trong TINCHEBIEN, nhóm theo (Note, TenHang, LoaiDo, DonVi [, DonGia]),
        /// chỉ giữ các nhóm có |100 * SUM| > 1 (lệch hơn 0.01). SL dương = gọi thêm, SL âm = giảm/hủy.
        /// </summary>
        public static List<KitchenPendingItem> ComputePendingItems(
            IList<KitchenRawLine> billLines,
            IList<KitchenRawLine> printedLines,
            bool includeUnitPrice)
        {
            var map = new Dictionary<string, KitchenPendingItem>(StringComparer.Ordinal);
            var order = new List<string>();

            Accumulate(map, order, billLines, 1m, includeUnitPrice);
            Accumulate(map, order, printedLines, -1m, includeUnitPrice);

            var result = new List<KitchenPendingItem>();
            for (int i = 0; i < order.Count; i++)
            {
                KitchenPendingItem item = map[order[i]];
                if (Math.Abs(100m * item.Quantity) > 1m)
                    result.Add(item);
            }
            return result;
        }

        private static void Accumulate(
            Dictionary<string, KitchenPendingItem> map,
            List<string> order,
            IList<KitchenRawLine> lines,
            decimal sign,
            bool includeUnitPrice)
        {
            if (lines == null)
                return;

            for (int i = 0; i < lines.Count; i++)
            {
                KitchenRawLine r = lines[i];
                if (r == null)
                    continue;
                string note = (r.Note ?? "").Trim();
                string name = (r.ItemName ?? "").Trim();
                string drinkType = string.IsNullOrEmpty(r.DrinkTypeId) ? ServicePrintConstants.DrinkTypeDrink : r.DrinkTypeId.Trim();
                string unitId = (r.UnitId ?? "").Trim();
                decimal price = includeUnitPrice ? r.UnitPrice : 0m;
                string key = note + "\u001F" + name + "\u001F" + drinkType + "\u001F" + unitId
                    + (includeUnitPrice ? ("\u001F" + price.ToString(CultureInfo.InvariantCulture)) : "");

                KitchenPendingItem acc;
                if (!map.TryGetValue(key, out acc))
                {
                    acc = new KitchenPendingItem
                    {
                        ItemName = name,
                        Note = note,
                        UnitId = unitId,
                        UnitName = r.UnitName ?? "",
                        DrinkTypeId = drinkType,
                        UnitPrice = price,
                        Quantity = 0m
                    };
                    map[key] = acc;
                    order.Add(key);
                }
                else if (string.IsNullOrEmpty(acc.UnitName) && !string.IsNullOrEmpty(r.UnitName))
                {
                    acc.UnitName = r.UnitName;
                }
                acc.Quantity += sign * r.Quantity;
            }
        }

        /// <summary>
        /// Phân nhóm các dòng cần in thành danh sách phiếu bếp theo máy in &amp; cấu hình InDoAn/InDoUong/InDoKhac/InDichVu,
        /// InRiengDoAnUong, InPhaCheTheoKhuVuc (theo đúng goc-InCheBien-code.cs.txt dòng 106–226).
        /// </summary>
        public static List<KitchenPrintBatch> BuildBatches(
            KitchenPrintSettings settings,
            string areaId,
            IList<KitchenPendingItem> pendingItems,
            IKitchenPrinterStore store,
            out bool needsPrinterSetup)
        {
            needsPrinterSetup = false;
            var batches = new List<KitchenPrintBatch>();
            if (settings == null || !settings.Enabled)
                return batches;

            var enabledTypes = new List<TypeSlot>();
            if (settings.PrintFood)
                enabledTypes.Add(new TypeSlot(ServicePrintConstants.DrinkTypeFood, "đồ ăn", ReadPrinter(store, settings.ByArea, areaId, ServicePrintConstants.DrinkTypeFood)));
            if (settings.PrintDrink)
                enabledTypes.Add(new TypeSlot(ServicePrintConstants.DrinkTypeDrink, "đồ uống", ReadPrinter(store, settings.ByArea, areaId, ServicePrintConstants.DrinkTypeDrink)));
            if (settings.PrintOther)
                enabledTypes.Add(new TypeSlot(ServicePrintConstants.DrinkTypeOther, "đồ khác", ReadPrinter(store, settings.ByArea, areaId, ServicePrintConstants.DrinkTypeOther)));
            if (settings.PrintService)
                enabledTypes.Add(new TypeSlot(ServicePrintConstants.DrinkTypeService, "dịch vụ", ReadPrinter(store, settings.ByArea, areaId, ServicePrintConstants.DrinkTypeService)));

            if (enabledTypes.Count == 0)
            {
                needsPrinterSetup = true;
                return batches;
            }

            var merged = new List<MergedSlot>();
            if (!settings.SeparateFoodAndDrink)
            {
                var byPrinter = new Dictionary<string, MergedSlot>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < enabledTypes.Count; i++)
                {
                    TypeSlot slot = enabledTypes[i];
                    string key = (slot.PrinterName ?? "").Trim();
                    MergedSlot existing;
                    if (byPrinter.TryGetValue(key, out existing))
                    {
                        existing.Caption += ", " + slot.Caption;
                        existing.DrinkTypeIds.Add(slot.DrinkTypeId);
                    }
                    else
                    {
                        var created = new MergedSlot
                        {
                            PrinterName = key,
                            Caption = slot.Caption
                        };
                        created.DrinkTypeIds.Add(slot.DrinkTypeId);
                        byPrinter[key] = created;
                        merged.Add(created);
                    }
                }
                needsPrinterSetup = byPrinter.Count == 0 || (byPrinter.Count == 1 && byPrinter.ContainsKey(""));
            }
            else
            {
                for (int i = 0; i < enabledTypes.Count; i++)
                {
                    TypeSlot slot = enabledTypes[i];
                    if (string.IsNullOrWhiteSpace(slot.PrinterName))
                        needsPrinterSetup = true;
                    var created = new MergedSlot
                    {
                        PrinterName = (slot.PrinterName ?? "").Trim(),
                        Caption = slot.Caption
                    };
                    created.DrinkTypeIds.Add(slot.DrinkTypeId);
                    merged.Add(created);
                }
            }

            if (needsPrinterSetup || pendingItems == null || pendingItems.Count == 0)
                return batches;

            for (int i = 0; i < merged.Count; i++)
            {
                MergedSlot m = merged[i];
                var items = new List<KitchenPendingItem>();
                for (int j = 0; j < pendingItems.Count; j++)
                {
                    KitchenPendingItem it = pendingItems[j];
                    if (it != null && m.DrinkTypeIds.Contains(it.DrinkTypeId))
                        items.Add(it);
                }
                if (items.Count == 0)
                    continue;

                batches.Add(new KitchenPrintBatch
                {
                    PrinterName = m.PrinterName,
                    Caption = CapitalizeFirst(m.Caption),
                    DrinkTypeIds = new List<string>(m.DrinkTypeIds),
                    Items = items
                });
            }

            return batches;
        }

        /// <summary>Dựng bảng cấu hình máy in bếp cho màn thiết lập (theo goc-InCheBien-code.cs.txt LoadSetup).</summary>
        public static List<KitchenPrinterSetupRow> BuildSetupRows(
            KitchenPrintSettings settings,
            IList<ServiceAreaInfo> areas,
            IKitchenPrinterStore store)
        {
            var rows = new List<KitchenPrinterSetupRow>();
            if (settings == null)
                return rows;

            if (settings.ByArea)
            {
                if (areas == null)
                    return rows;
                for (int i = 0; i < areas.Count; i++)
                {
                    ServiceAreaInfo area = areas[i];
                    if (area == null)
                        continue;
                    string areaLabel = area.Name ?? "";
                    AddSetupRowIf(rows, settings.PrintFood, ref areaLabel, "Đồ ăn", ResolveKey(true, area.Id, ServicePrintConstants.DrinkTypeFood), store);
                    AddSetupRowIf(rows, settings.PrintDrink, ref areaLabel, "Đồ uống", ResolveKey(true, area.Id, ServicePrintConstants.DrinkTypeDrink), store);
                    AddSetupRowIf(rows, settings.PrintOther, ref areaLabel, "Đồ khác", ResolveKey(true, area.Id, ServicePrintConstants.DrinkTypeOther), store);
                    AddSetupRowIf(rows, settings.PrintService, ref areaLabel, "Dịch vụ", ResolveKey(true, area.Id, ServicePrintConstants.DrinkTypeService), store);
                }
            }
            else
            {
                string emptyArea = "";
                AddSetupRowIf(rows, settings.PrintFood, ref emptyArea, "Đồ ăn", ServicePrintConstants.KeyPrinterFood, store);
                AddSetupRowIf(rows, settings.PrintDrink, ref emptyArea, "Đồ uống", ServicePrintConstants.KeyPrinterDrink, store);
                AddSetupRowIf(rows, settings.PrintOther, ref emptyArea, "Đồ khác", ServicePrintConstants.KeyPrinterOther, store);
                AddSetupRowIf(rows, settings.PrintService, ref emptyArea, "Dịch vụ", ServicePrintConstants.KeyPrinterService, store);
            }

            return rows;
        }

        /// <summary>
        /// Phân tuyến máy in hóa đơn theo cấu hình InHoaDonTheoKhuVuc (goc-PrintInvoice-nhahang.cs.txt dòng 55–66 &amp; 180–184).
        /// </summary>
        public static string ResolveInvoicePrinter(
            InvoiceAreaPrintMode mode,
            bool isTempPrint,
            bool isReprint,
            string defaultPrinter,
            string areaPrinter,
            out string secondCopyPrinter)
        {
            secondCopyPrinter = null;
            string def = (defaultPrinter ?? "").Trim();
            string area = string.IsNullOrWhiteSpace(areaPrinter) ? def : areaPrinter.Trim();

            if (mode == InvoiceAreaPrintMode.ByArea)
                return area;
            if (mode == InvoiceAreaPrintMode.TempByAreaBillAtCounter && isTempPrint)
                return area;
            if (mode == InvoiceAreaPrintMode.OneAtCounterOneByArea && !isReprint && !string.IsNullOrWhiteSpace(areaPrinter))
                secondCopyPrinter = area;
            return def;
        }

        private static void AddSetupRowIf(
            List<KitchenPrinterSetupRow> rows,
            bool enabled,
            ref string areaName,
            string drinkLabel,
            string key,
            IKitchenPrinterStore store)
        {
            if (!enabled)
                return;
            rows.Add(new KitchenPrinterSetupRow
            {
                AreaName = areaName,
                DrinkTypeLabel = drinkLabel,
                ConfigKey = key,
                PrinterName = store != null ? (store.GetConfiguredPrinter(key) ?? "") : ""
            });
            areaName = "";
        }

        private static string ReadPrinter(IKitchenPrinterStore store, bool byArea, string areaId, string drinkTypeId)
        {
            if (store == null)
                return "";
            return store.GetConfiguredPrinter(ResolveKey(byArea, areaId, drinkTypeId)) ?? "";
        }

        private static string CapitalizeFirst(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            string s = text.Trim();
            return s.Substring(0, 1).ToUpper(CultureInfo.CurrentCulture) + s.Substring(1);
        }

        private sealed class TypeSlot
        {
            public readonly string DrinkTypeId;
            public readonly string Caption;
            public readonly string PrinterName;

            public TypeSlot(string drinkTypeId, string caption, string printerName)
            {
                DrinkTypeId = drinkTypeId;
                Caption = caption;
                PrinterName = printerName ?? "";
            }
        }

        private sealed class MergedSlot
        {
            public string PrinterName;
            public string Caption;
            public readonly List<string> DrinkTypeIds = new List<string>();
        }
    }
}
