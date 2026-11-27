using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Kiểm tra tồn kho cho Xuất khác (LOAI = 2) và Chuyển kho (LOAI = 3) dùng duy nhất
    /// <see cref="ServiceOrderRepository.GetStockBalanceIn"/> (docs/specs/phieu-xuat-chuyen-kho.md §2, §3 T3, §4, §5).
    /// </summary>
    public sealed partial class StockDocumentService
    {
        private string CheckStockOnSaveIn(
            FbConnection conn,
            FbTransaction tx,
            StockDocumentRow existing,
            WarehouseRow sourceWh,
            StockDocumentRow newRow)
        {
            if (SystemConfig.ChoPhepAmKho)
                return null;

            string excludeId = existing == null ? "" : (existing.Id ?? "");

            // 1. Kiểm tồn kho xuất (nguồn): tổng số lượng đã quy đổi (SLXUAT cho xuất khác; SLNHAP cho chuyển kho — T3)
            // không được vượt tồn hiện có khi kho xuất không cho phép âm kho.
            if (sourceWh != null && sourceWh.AllowNegativeStock == 0)
            {
                var reqByProduct = new Dictionary<string, decimal>(StringComparer.Ordinal);
                var nameByProduct = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (StockDocumentLine l in newRow.Lines)
                {
                    if (l == null || string.IsNullOrEmpty(l.ProductId))
                        continue;
                    decimal prev;
                    reqByProduct.TryGetValue(l.ProductId, out prev);
                    reqByProduct[l.ProductId] = prev + l.ConvertedQuantity;
                    if (!nameByProduct.ContainsKey(l.ProductId) && !string.IsNullOrEmpty(l.ProductName))
                        nameByProduct[l.ProductId] = l.ProductName;
                }

                foreach (KeyValuePair<string, decimal> kv in reqByProduct)
                {
                    decimal available = ServiceOrderRepository.GetStockBalanceIn(
                        conn, tx, kv.Key, sourceWh.Id, excludeId);
                    if (kv.Value > available)
                    {
                        string itemName;
                        if (!nameByProduct.TryGetValue(kv.Key, out itemName) || string.IsNullOrEmpty(itemName))
                            itemName = PurchaseRepository.LoadProductNameIn(conn, tx, kv.Key);
                        return StockDocumentConstants.InsufficientStock(itemName, available, kv.Value);
                    }
                }
            }

            // 2. Với chuyển kho khi sửa: kiểm thêm kho nhập cũ đề phòng giảm/xóa dòng hoặc đổi kho đích
            // làm tồn kho nhập cũ thành âm (spec §5).
            if (_kind == StockDocumentKind.Transfer && existing != null)
                return CheckTransferOldDestNegativeIn(conn, tx, existing, newRow);

            return null;
        }

        private string CheckStockOnDeleteIn(FbConnection conn, FbTransaction tx, StockDocumentRow existing)
        {
            if (SystemConfig.ChoPhepAmKho || existing == null)
                return null;
            // Xóa phiếu Xuất khác chỉ tăng tồn kho xuất, không thể gây âm kho.
            if (_kind != StockDocumentKind.Transfer)
                return null;
            return CheckTransferOldDestNegativeIn(conn, tx, existing, null);
        }

        private static string CheckTransferOldDestNegativeIn(
            FbConnection conn,
            FbTransaction tx,
            StockDocumentRow existing,
            StockDocumentRow newRow)
        {
            if (existing == null || existing.Lines == null || existing.Lines.Count == 0)
                return null;
            string oldDestId = existing.DestWarehouseId;
            if (string.IsNullOrEmpty(oldDestId))
                return null;

            WarehouseRow oldDestWh = WarehouseRepository.LoadByIdIn(conn, tx, oldDestId);
            if (oldDestWh == null || oldDestWh.AllowNegativeStock != 0)
                return null;

            // Tổng SLNHAP cũ ở kho đích cũ theo từng mặt hàng.
            var oldByProduct = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var nameByProduct = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (StockDocumentLine l in existing.Lines)
            {
                if (l == null || string.IsNullOrEmpty(l.ProductId))
                    continue;
                decimal prev;
                oldByProduct.TryGetValue(l.ProductId, out prev);
                oldByProduct[l.ProductId] = prev + l.ConvertedQuantity;
                if (!nameByProduct.ContainsKey(l.ProductId) && !string.IsNullOrEmpty(l.ProductName))
                    nameByProduct[l.ProductId] = l.ProductName;
            }

            // Tổng thay đổi mới tại đúng kho oldDestId (dương nếu vẫn là kho nhập, âm nếu bị đảo thành kho xuất, 0 nếu xóa/đổi sang kho khác).
            var newDeltaByProduct = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (newRow != null && newRow.Lines != null)
            {
                decimal sign = 0m;
                if (string.Equals(newRow.DestWarehouseId, oldDestId, StringComparison.Ordinal))
                    sign = 1m;
                else if (string.Equals(newRow.SourceWarehouseId, oldDestId, StringComparison.Ordinal))
                    sign = -1m;

                if (sign != 0m)
                {
                    foreach (StockDocumentLine l in newRow.Lines)
                    {
                        if (l == null || string.IsNullOrEmpty(l.ProductId))
                            continue;
                        decimal prev;
                        newDeltaByProduct.TryGetValue(l.ProductId, out prev);
                        newDeltaByProduct[l.ProductId] = prev + sign * l.ConvertedQuantity;
                    }
                }
            }

            foreach (KeyValuePair<string, decimal> kv in oldByProduct)
            {
                decimal newDelta;
                newDeltaByProduct.TryGetValue(kv.Key, out newDelta);
                if (newDelta >= kv.Value)
                    continue;

                decimal currentStock = ServiceOrderRepository.GetStockBalanceIn(conn, tx, kv.Key, oldDestId, "");
                decimal stockWithoutOrder = ServiceOrderRepository.GetStockBalanceIn(conn, tx, kv.Key, oldDestId, existing.Id);
                decimal afterStock = stockWithoutOrder + newDelta;
                if (afterStock < 0m)
                {
                    string itemName;
                    if (!nameByProduct.TryGetValue(kv.Key, out itemName) || string.IsNullOrEmpty(itemName))
                        itemName = PurchaseRepository.LoadProductNameIn(conn, tx, kv.Key);
                    return PurchaseConstants.NegativeStockBlocked(itemName, currentStock, afterStock);
                }
            }

            return null;
        }
    }
}
