using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc dữ liệu cho màn Tồn quỹ (docs/specs/quy-tien.md). Chỉ SELECT. Công thức tiền nằm ở lớp Services.
    /// Lấy mọi giao dịch đến hết ngày "Đến" vì tồn đầu kỳ cần cả lịch sử.
    /// </summary>
    public sealed class FundRepository
    {
        // Giống app gốc: bill không ghi cửa hàng thì lấy cửa hàng của kho nhập/xuất.
        private const string OrderStore =
            "COALESCE(NULLIF(H.DCUAHANGID, ''), (SELECT K.DCUAHANGID FROM DKHOHANG K " +
            "WHERE K.ID = COALESCE(NULLIF(H.DKHONHAPID, ''), H.DKHOXUATID)))";

        private readonly string _databasePath;

        public FundRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public FundData Load(DateTime to)
        {
            return DbHelper.Read(_databasePath, conn => LoadIn(conn, null, to));
        }

        /// <summary>Đọc dữ liệu quỹ trong transaction hiện có (dùng chung cho màn Tồn quỹ và kiểm tra chi vượt quỹ).</summary>
        public static FundData LoadIn(FbConnection conn, FbTransaction tx, DateTime to)
        {
            DateTime end = to.Date.AddDays(1);
            var data = new FundData
            {
                Sources = new List<FundSource>(),
                Accounts = new List<FundAccount>(),
                Stores = new List<StoreRecord>()
            };
            LoadOrders(conn, tx, end, data.Sources);
            LoadVouchers(conn, tx, end, data.Sources);

            foreach (DataRow r in DbHelper.Table(conn, tx,
                         "SELECT ID, NAME, STATUS FROM DTAIKHOANNGANHANG ORDER BY NAME").Rows)
                data.Accounts.Add(new FundAccount
                {
                    Id = DbHelper.Str(r["ID"]),
                    Name = DbHelper.Str(r["NAME"]),
                    Active = DbHelper.Int(r["STATUS"]) == CatalogRepository.ActiveStatus
                });

            foreach (DataRow r in DbHelper.Table(conn, tx,
                         "SELECT ID, NAME FROM DCUAHANG WHERE STATUS = @S ORDER BY NAME",
                         "@S", CatalogRepository.ActiveStatus).Rows)
                data.Stores.Add(new StoreRecord { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) });
            return data;
        }

        /// <summary>Hóa đơn bán đã thanh toán, phiếu nhập và phiếu xuất trả (bỏ bản ghi đã xóa mềm).</summary>
        private static void LoadOrders(FbConnection conn, FbTransaction tx, DateTime end, List<FundSource> sources)
        {
            DataTable table = DbHelper.Table(conn, tx,
                "SELECT H.NAME, H.NGAY, H.GIOTHANHTOAN, H.TIMECREATED, H.LOAI, H.STATUS, H.DATHANHTOAN, " +
                "H.TIENMAT, H.CHUYENKHOAN, H.THE, H.TIENTHANHTOAN, H.DTAIKHOANNGANHANGID, H.DNHACUNGCAPID, " +
                OrderStore + " AS CUAHANG FROM TDONHANG H " +
                "WHERE COALESCE(H.STATUS, 0) <> @Deleted AND (" +
                "(COALESCE(H.LOAI, 0) = 0 AND H.DATHANHTOAN = 30 " +
                "AND COALESCE(H.GIOTHANHTOAN, H.NGAY, H.TIMECREATED) < @End) " +
                "OR (H.LOAI IN (1, 2) AND COALESCE(H.NGAY, H.TIMECREATED) < @End))",
                "@Deleted", -30, "@End", end);

            foreach (DataRow r in table.Rows)
            {
                int type = DbHelper.Int(r["LOAI"]);
                var source = new FundSource
                {
                    Kind = type == 0 ? FundSourceKind.Sale : FundSourceKind.Stock,
                    Number = DbHelper.Str(r["NAME"]),
                    DocumentDate = Time(r["NGAY"]) ?? Time(r["TIMECREATED"]) ?? DateTime.MinValue,
                    PaidTime = type == 0 ? Time(r["GIOTHANHTOAN"]) : null,
                    CreatedTime = Time(r["TIMECREATED"]),
                    StoreId = DbHelper.Str(r["CUAHANG"]) ?? ""
                };
                if (type == 0)
                    source.Bill = new FundBill
                    {
                        Type = type,
                        Status = DbHelper.Int(r["STATUS"]),
                        PaidFlag = DbHelper.Int(r["DATHANHTOAN"]),
                        Cash = DbHelper.Dec(r["TIENMAT"]),
                        Transfer = DbHelper.Dec(r["CHUYENKHOAN"]),
                        Card = DbHelper.Dec(r["THE"]),
                        AccountId = DbHelper.Str(r["DTAIKHOANNGANHANGID"])
                    };
                else
                    source.Stock = new FundStockDocument
                    {
                        Type = type,
                        Status = DbHelper.Int(r["STATUS"]),
                        SupplierId = DbHelper.Str(r["DNHACUNGCAPID"]),
                        PaidAmount = DbHelper.Dec(r["TIENTHANHTOAN"]),
                        Transfer = DbHelper.Dec(r["CHUYENKHOAN"]),
                        AccountId = DbHelper.Str(r["DTAIKHOANNGANHANGID"])
                    };
                sources.Add(source);
            }
        }

        /// <summary>Phiếu thu/chi. Phiếu số dư đầu kỳ luôn được lấy, kể cả sau ngày "Đến", vì nó quyết định mốc.</summary>
        private static void LoadVouchers(FbConnection conn, FbTransaction tx, DateTime end, List<FundSource> sources)
        {
            DataTable table = DbHelper.Table(conn, tx,
                "SELECT NAME, NGAY, TIMECREATED, STATUS, THU, CHI, CHUYENKHOAN, DTAIKHOANNGANHANGID, " +
                "DCUAHANGID, DLYDOTHUCHIID FROM TTHUCHI " +
                "WHERE COALESCE(STATUS, 0) <> @Deleted " +
                "AND (COALESCE(NGAY, TIMECREATED) < @End OR DLYDOTHUCHIID = @Opening)",
                "@Deleted", -30, "@End", end, "@Opening", FundReasons.OpeningBalanceId);

            foreach (DataRow r in table.Rows)
                sources.Add(new FundSource
                {
                    Kind = FundSourceKind.Voucher,
                    Number = DbHelper.Str(r["NAME"]),
                    DocumentDate = Time(r["NGAY"]) ?? Time(r["TIMECREATED"]) ?? DateTime.MinValue,
                    CreatedTime = Time(r["TIMECREATED"]),
                    StoreId = DbHelper.Str(r["DCUAHANGID"]) ?? "",
                    ReasonId = DbHelper.Str(r["DLYDOTHUCHIID"]),
                    Voucher = new FundVoucher
                    {
                        Status = DbHelper.Int(r["STATUS"]),
                        Income = DbHelper.Dec(r["THU"]),
                        Expense = DbHelper.Dec(r["CHI"]),
                        TransferFlag = DbHelper.Int(r["CHUYENKHOAN"]),
                        AccountId = DbHelper.Str(r["DTAIKHOANNGANHANGID"])
                    }
                });
        }

        private static DateTime? Time(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
