using System;
using System.Collections.Generic;
using System.Linq;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ chỉ đọc cho màn Tồn kho (docs/specs/ton-kho.md):
    /// kiểm tra quyền "Tồn kho" (Xem) và "Xem giá nhập", giới hạn kho theo cửa hàng người dùng,
    /// đọc tồn theo kho bằng công thức thống nhất với ServiceOrderRepository.GetStockBalanceIn.
    /// </summary>
    public sealed class InventoryService
    {
        private readonly UserSession _session;
        private readonly InventoryRepository _repo;

        public InventoryService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new InventoryRepository(session.DatabasePath);
        }

        public bool CanView { get { return InventoryRules.CanView(_session); } }
        public bool CanViewBuyPrice { get { return InventoryRules.CanViewBuyPrice(_session); } }

        /// <summary>Danh sách kho được phép xem của người dùng hiện tại.</summary>
        public List<WarehouseRow> LoadAllowedWarehouses()
        {
            if (!CanView)
                return new List<WarehouseRow>();
            return _repo.LoadAllowedWarehouses(_session.UserId, _session.IsAdmin, SessionStoreIds());
        }

        /// <summary>Xác định kho mặc định cho phiên đăng nhập hiện tại.</summary>
        public string ResolveDefaultWarehouseId(IList<WarehouseRow> allowedWarehouses)
        {
            string storeId = _session.CurrentStore == null ? null : _session.CurrentStore.Id;
            string storeDefaultWh = string.IsNullOrEmpty(storeId)
                ? null
                : _repo.GetStoreDefaultWarehouseId(storeId);
            return InventoryRules.ResolveDefaultWarehouseId(allowedWarehouses, storeId, storeDefaultWh);
        }

        public List<ProductGroupRow> LoadProductGroups()
        {
            if (!CanView)
                return new List<ProductGroupRow>();
            return _repo.LoadProductGroups();
        }

        public List<LookupItem> LoadStockProductTypes()
        {
            if (!CanView)
                return new List<LookupItem>();
            return _repo.LoadStockProductTypes();
        }

        /// <summary>
        /// Tải toàn bộ mặt hàng tồn kho của một kho (đã tính 2 ĐVT, giá trị, tông màu, chưa áp bộ lọc cục bộ).
        /// Chặn non-admin truy vấn kho ngoài cửa hàng được gán; xóa trắng các cột giá khi không có quyền Xem giá nhập.
        /// </summary>
        public List<InventoryItemRow> LoadWarehouseItems(string warehouseId)
        {
            if (!CanView || string.IsNullOrWhiteSpace(warehouseId))
                return new List<InventoryItemRow>();

            if (!_session.IsAdmin)
            {
                List<WarehouseRow> allowed = LoadAllowedWarehouses();
                if (!allowed.Exists(w => string.Equals(w.Id, warehouseId, StringComparison.Ordinal)))
                    return new List<InventoryItemRow>();
            }

            bool sortByCode = SystemConfig.SapXepThuTuTheo == 1;
            List<InventoryItemRow> rows = _repo.LoadStockItems(warehouseId, sortByCode);
            InventoryRules.ApplyMetrics(rows);

            if (!CanViewBuyPrice)
                StripPriceFields(rows);

            return rows;
        }

        /// <summary>Tải và áp bộ lọc đầy đủ cho màn Tồn kho (hỗ trợ cả mục "Tất cả các kho" cho Tồn nhiều kho).</summary>
        public List<InventoryItemRow> LoadItems(InventoryFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            List<InventoryItemRow> all;
            if (InventoryRules.IsAllWarehousesSelection(filter.WarehouseId))
            {
                Dictionary<string, Dictionary<string, decimal>> ignored;
                all = LoadItemsForMode(InventoryConstants.AllWarehousesId, true, out ignored);
            }
            else
            {
                all = LoadWarehouseItems(filter.WarehouseId);
            }
            if (all.Count == 0)
                return all;
            return all.FindAll(r => InventoryRules.MatchesFilter(r, filter));
        }

        /// <summary>
        /// Tải danh sách tồn kho theo chế độ màn hình (màn Tồn kho thường hoặc Tồn nhiều kho).
        /// Ở chế độ Tồn nhiều kho, <paramref name="stockByWarehouse"/> trả thêm tồn chi tiết từng kho được phép
        /// (warehouseId -&gt; (productId -&gt; tồn)) từ cùng một lượt đọc DB và cùng một công thức tồn.
        /// </summary>
        public List<InventoryItemRow> LoadItemsForMode(
            string warehouseId,
            bool allWarehousesMode,
            out Dictionary<string, Dictionary<string, decimal>> stockByWarehouse)
        {
            stockByWarehouse = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal);
            if (!CanView)
                return new List<InventoryItemRow>();

            if (!allWarehousesMode)
                return LoadWarehouseItems(warehouseId);

            List<WarehouseRow> allowed = LoadAllowedWarehouses();
            if (allowed.Count == 0)
                return new List<InventoryItemRow>();

            List<string> allowedIds = allowed.Select(w => w.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (allowedIds.Count == 0)
                return new List<InventoryItemRow>();

            bool isAllSelected = InventoryRules.IsAllWarehousesSelection(warehouseId);
            string selectedWhId = null;
            if (!isAllSelected)
            {
                if (string.IsNullOrWhiteSpace(warehouseId)
                    || !allowedIds.Exists(id => string.Equals(id, warehouseId, StringComparison.Ordinal)))
                {
                    return new List<InventoryItemRow>();
                }
                selectedWhId = warehouseId;
            }

            bool sortByCode = SystemConfig.SapXepThuTuTheo == 1;
            List<InventoryItemRow> rows = _repo.LoadStockItemsForWarehouses(
                allowedIds, sortByCode, out stockByWarehouse, selectedWhId);
            InventoryRules.ApplyMetrics(rows);

            if (!CanViewBuyPrice)
                StripPriceFields(rows);

            return rows;
        }

        /// <summary>
        /// Dựng bảng tồn chi tiết theo kho cho một mặt hàng đang chọn từ dữ liệu tồn theo kho đã nạp sẵn.
        /// Không truy vấn lặp DB khi người dùng đổi dòng chọn trên lưới chính.
        /// </summary>
        public List<InventoryWarehouseDetailRow> BuildProductWarehouseDetails(
            InventoryItemRow item,
            IList<WarehouseRow> allowedWarehouses,
            IDictionary<string, Dictionary<string, decimal>> stockByWarehouse,
            bool hideZeroStock)
        {
            if (!CanView || item == null || string.IsNullOrEmpty(item.ProductId)
                || allowedWarehouses == null || allowedWarehouses.Count == 0)
            {
                return new List<InventoryWarehouseDetailRow>();
            }

            var perWh = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (stockByWarehouse != null)
            {
                foreach (WarehouseRow w in allowedWarehouses)
                {
                    if (w == null || string.IsNullOrEmpty(w.Id))
                        continue;
                    Dictionary<string, decimal> whMap;
                    decimal qty = 0m;
                    if (stockByWarehouse.TryGetValue(w.Id, out whMap) && whMap != null)
                        whMap.TryGetValue(item.ProductId, out qty);
                    perWh[w.Id] = qty;
                }
            }
            return InventoryRules.BuildWarehouseDetails(item, allowedWarehouses, perWh, hideZeroStock, CanViewBuyPrice);
        }

        /// <summary>
        /// Tải tồn chi tiết theo từng kho được phép cho một mặt hàng trực tiếp từ DB qua công thức tồn dùng chung.
        /// </summary>
        public List<InventoryWarehouseDetailRow> LoadProductWarehouseDetails(string productId, bool hideZeroStock)
        {
            if (!CanView || string.IsNullOrEmpty(productId))
                return new List<InventoryWarehouseDetailRow>();

            List<WarehouseRow> allowed = LoadAllowedWarehouses();
            if (allowed.Count == 0)
                return new List<InventoryWarehouseDetailRow>();

            InventoryItemRow item = _repo.LoadStockItemById(productId);
            if (item == null)
                return new List<InventoryWarehouseDetailRow>();

            List<string> allowedIds = allowed.Select(w => w.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
            Dictionary<string, decimal> perWh = _repo.LoadProductStockByWarehouses(productId, allowedIds);
            return InventoryRules.BuildWarehouseDetails(item, allowed, perWh, hideZeroStock, CanViewBuyPrice);
        }

        /// <summary>Đọc tồn kho một mặt hàng qua hàm tồn dùng chung ServiceOrderRepository.GetStockBalanceIn.</summary>
        public decimal GetStockBalance(string productId, string warehouseId)
        {
            if (!CanView || string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(warehouseId))
                return 0m;
            if (!_session.IsAdmin)
            {
                List<WarehouseRow> allowed = LoadAllowedWarehouses();
                if (!allowed.Exists(w => string.Equals(w.Id, warehouseId, StringComparison.Ordinal)))
                    return 0m;
            }
            return _repo.GetStockBalance(productId, warehouseId);
        }

        private static void StripPriceFields(List<InventoryItemRow> rows)
        {
            foreach (InventoryItemRow r in rows)
            {
                r.BuyPrice = 0m;
                r.CostPrice = 0m;
                r.SellPrice = 0m;
                r.CostValue = 0m;
                r.SellValue = 0m;
            }
        }

        private List<string> SessionStoreIds()
        {
            if (_session.Stores == null || _session.Stores.Count == 0)
                return null;
            return _session.Stores.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
        }
    }
}
