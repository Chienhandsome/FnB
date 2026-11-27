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

        /// <summary>Tải và áp bộ lọc đầy đủ cho màn Tồn kho.</summary>
        public List<InventoryItemRow> LoadItems(InventoryFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            List<InventoryItemRow> all = LoadWarehouseItems(filter.WarehouseId);
            if (all.Count == 0)
                return all;
            return all.FindAll(r => InventoryRules.MatchesFilter(r, filter));
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
