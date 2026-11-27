using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc dữ liệu tính giá vốn và ghi giá vốn mới (docs/specs/tinh-lai-gia-von.md).
    /// Chỉ lấy phiếu STATUS = 30 và bill bán (LOAI = 0) đã thanh toán, cùng công thức tồn chung của
    /// <c>ServiceOrderRepository.GetStockBalanceIn</c>. Không DDL; mọi câu SQL tham số hóa.
    /// </summary>
    public sealed class CostRecalcRepository
    {
        private readonly string _databasePath;

        public CostRecalcRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        /// <summary>Mặt hàng đang dùng (bỏ mặt hàng hệ thống KM). Loại để trống xem như '0'.</summary>
        public static List<CostProduct> LoadProductsIn(FbConnection conn, FbTransaction tx)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, DLOAIMATHANGID, COALESCE(GIAVON, 0) AS GIAVON FROM DMATHANG " +
                "WHERE STATUS = @S AND ID <> @Sys",
                "@S", CostRecalcConstants.ActiveStatus, "@Sys", ProductRepository.SystemProductId);
            var list = new List<CostProduct>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                string type = DbHelper.Str(r["DLOAIMATHANGID"]);
                list.Add(new CostProduct
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    TypeId = string.IsNullOrEmpty(type) ? "0" : type,
                    CurrentCost = DbHelper.Dec(r["GIAVON"])
                });
            }
            return list;
        }

        /// <summary>Công thức (DDINHLUONG) của các món.</summary>
        public static List<CostRecipeLine> LoadRecipesIn(FbConnection conn, FbTransaction tx)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT DMATHANGID, DVATTUID, COALESCE(SOLUONG, 0) AS SOLUONG FROM DDINHLUONG " +
                "WHERE DMATHANGID IS NOT NULL AND DVATTUID IS NOT NULL");
            var list = new List<CostRecipeLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
                list.Add(new CostRecipeLine
                {
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    IngredientId = DbHelper.Str(r["DVATTUID"]) ?? "",
                    Quantity = DbHelper.Dec(r["SOLUONG"])
                });
            return list;
        }

        /// <summary>
        /// Dòng chứng từ của phiếu STATUS = 30 và (LOAI khác 0 hoặc bill bán đã thanh toán),
        /// sắp theo ngày, giờ tạo (cùng điều kiện công thức tồn chung).
        /// </summary>
        public static List<CostLine> LoadLinesIn(FbConnection conn, FbTransaction tx)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT C.ID, C.DMATHANGID, COALESCE(H.LOAI, 0) AS LOAI, H.NGAY, " +
                "COALESCE(C.SLNHAP, 0) AS SLNHAP, COALESCE(C.SLXUAT, 0) AS SLXUAT, " +
                "COALESCE(C.THANHTIEN, 0) AS THANHTIEN, COALESCE(C.GIAVON, 0) AS GIAVON " +
                "FROM TDONHANGCHITIET C INNER JOIN TDONHANG H ON H.ID = C.TDONHANGID " +
                "WHERE C.DMATHANGID IS NOT NULL AND H.NGAY IS NOT NULL AND COALESCE(H.STATUS, 0) = @S " +
                "AND (COALESCE(H.LOAI, 0) <> 0 OR COALESCE(H.DATHANHTOAN, 0) = @Paid) " +
                "ORDER BY H.NGAY, H.TIMECREATED, C.ID",
                "@S", CostRecalcConstants.ActiveStatus, "@Paid", CostRecalcConstants.ActiveStatus);
            var list = new List<CostLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
                list.Add(new CostLine
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    OrderType = DbHelper.Int(r["LOAI"]),
                    Date = Convert.ToDateTime(r["NGAY"]),
                    QtyIn = DbHelper.Dec(r["SLNHAP"]),
                    QtyOut = DbHelper.Dec(r["SLXUAT"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"]),
                    CurrentCost = DbHelper.Dec(r["GIAVON"])
                });
            return list;
        }

        public static void UpdateLineCostIn(FbConnection conn, FbTransaction tx, string lineId, decimal cost)
        {
            DbHelper.Exec(conn, tx, "UPDATE TDONHANGCHITIET SET GIAVON = @G WHERE ID = @I",
                "@G", cost, "@I", lineId);
        }

        public static void UpdateProductCostIn(FbConnection conn, FbTransaction tx, string productId, decimal cost)
        {
            DbHelper.Exec(conn, tx, "UPDATE DMATHANG SET GIAVON = @G WHERE ID = @I",
                "@G", cost, "@I", productId);
        }
    }
}
