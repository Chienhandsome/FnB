using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc cho màn "Thống kê mặt hàng bán" và dialog "Chi tiết bán hàng theo mặt hàng"
    /// theo mục §2.0 của docs/specs/thong-ke-mat-hang-ban.md.
    /// Không có câu SQL ghi; mọi giá trị tham số hóa; phạm vi bill dùng chung BillReportScopeSql.
    /// </summary>
    public sealed class ItemSalesRepository
    {
        private readonly string _databasePath;

        public ItemSalesRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>Ngày hiện tại theo máy chủ CSDL Firebird (CURRENT_TIMESTAMP), không dùng DateTime.Today.</summary>
        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null, "SELECT CURRENT_TIMESTAMP FROM RDB$DATABASE");
                return v == null || v is DBNull ? DbHelper.Now().Date : Convert.ToDateTime(v).Date;
            });
        }

        /// <summary>
        /// Luật sắp xếp theo §2.0: SCONFIG.SapXepThuTuTheo = 0 VÀ SCONFIG.SuDungMaHang = 30 thì sắp theo DMATHANG.CODE,
        /// ngược lại sắp theo TENHANG.
        /// </summary>
        public bool ShouldSortByCode()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT NAME, INTVALUE FROM SCONFIG WHERE NAME IN (@K1, @K2)",
                    "@K1", ItemSalesConstants.ConfigSortOrderKey,
                    "@K2", ItemSalesConstants.ConfigUseCodeKey);

                int sortOrder = 0;
                int useCode = 0;
                foreach (DataRow r in dt.Rows)
                {
                    string name = DbHelper.Str(r["NAME"]);
                    if (string.Equals(name, ItemSalesConstants.ConfigSortOrderKey, StringComparison.Ordinal))
                        sortOrder = DbHelper.Int(r["INTVALUE"]);
                    else if (string.Equals(name, ItemSalesConstants.ConfigUseCodeKey, StringComparison.Ordinal))
                        useCode = DbHelper.Int(r["INTVALUE"]);
                }
                return sortOrder == 0 && useCode == BillReportScope.ActiveStatus;
            });
        }

        /// <summary>
        /// Danh sách cửa hàng cho combo lọc: admin thấy tất cả cửa hàng đang dùng;
        /// user thường chỉ thấy cửa hàng gắn với user trong TNGUOIDUNGTHEOCUAHANG.
        /// </summary>
        public List<StoreRecord> LoadAllowedStores(string userId, bool isAdmin)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt;
                if (isAdmin)
                {
                    dt = DbHelper.Table(conn, null,
                        "SELECT ID, NAME FROM DCUAHANG WHERE STATUS = @S ORDER BY NAME",
                        "@S", BillReportScope.ActiveStatus);
                }
                else
                {
                    if (string.IsNullOrEmpty(userId))
                        return new List<StoreRecord>();
                    dt = DbHelper.Table(conn, null,
                        "SELECT DISTINCT C.ID, C.NAME " +
                        "FROM TNGUOIDUNGTHEOCUAHANG T " +
                        "INNER JOIN DCUAHANG C ON C.ID = T.DCUAHANGID " +
                        "WHERE T.SUSERID = @UserId AND C.STATUS = @S " +
                        "ORDER BY C.NAME",
                        "@UserId", userId,
                        "@S", BillReportScope.ActiveStatus);
                }

                var list = new List<StoreRecord>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new StoreRecord
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Cây nhóm mặt hàng (DNHOMMATHANG đang dùng, bỏ DLOAIDOID = '4' là nguyên liệu), sắp theo SORTORDER, NAME.
        /// </summary>
        public List<ItemSalesGroupNode> LoadProductGroups()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, PARENTID, SORTORDER " +
                    "FROM DNHOMMATHANG " +
                    "WHERE STATUS = @S AND COALESCE(DLOAIDOID, '') <> @MatDrink " +
                    "ORDER BY SORTORDER, NAME",
                    "@S", BillReportScope.ActiveStatus,
                    "@MatDrink", ItemSalesConstants.MaterialDrinkTypeId);

                var list = new List<ItemSalesGroupNode>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new ItemSalesGroupNode
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                        SortOrder = DbHelper.Str(r["SORTORDER"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Lưới mặt hàng bán theo định nghĩa chuẩn §2.0:
        /// dùng chung BillReportScopeSql, loại nguyên liệu / xuất vật tư / combo con (không lọc TDONHANGCHITIET.STATUS),
        /// gộp đúng các trường §2.0, tính số lượng ròng (SLXUATCHUAQUYDOI − SLNHAPCHUAQUYDOI), THANHTIEN, THANHTIENNHAP.
        /// LAI và TILELAI do ItemSalesCalculator tính ở lớp Services.
        /// </summary>
        public List<ItemSalesRow> LoadItemSales(BillReportScope scope, ItemSalesCostMode costMode, bool sortByCode)
        {
            if (scope == null)
                throw new ArgumentNullException("scope");

            return DbHelper.Read(_databasePath, conn =>
            {
                var conditions = new List<string>(8);
                var args = new List<object>(16);
                BillReportScopeSql.AppendWhere("H", scope, conditions, args);

                conditions.Add("COALESCE(M.DLOAIMATHANGID, '') <> @MatType");
                args.Add("@MatType");
                args.Add(ItemSalesConstants.MaterialProductTypeId);

                conditions.Add("COALESCE(D.XUATVATTU, 0) = 0");
                conditions.Add("COALESCE(D.COMBOPARENTID, '') = ''");

                string costColumn = costMode == ItemSalesCostMode.ProductBuyPrice ? "M.GIANHAP" : "D.GIAVON";
                string orderBy = sortByCode
                    ? "M.CODE, D.TENHANG, D.DONGIA, D.TILEGIAMGIA"
                    : "D.TENHANG, M.CODE, D.DONGIA, D.TILEGIAMGIA";

                string sql =
                    "SELECT " +
                    "D.DMATHANGID, M.DNHOMMATHANGID, M.CODE, M.MASANCO, D.TENHANG, D.DDONVITINHID, " +
                    "U.NAME AS DVT, " +
                    "COALESCE(D.DONGIA, 0) AS DONGIA, " +
                    "COALESCE(D.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
                    "COALESCE(D.TIENGIAMGIA, 0) AS TIENGIAMGIA, " +
                    "COALESCE(" + costColumn + ", 0) AS UNITCOST, " +
                    "SUM(COALESCE(D.SLXUATCHUAQUYDOI, 0) - COALESCE(D.SLNHAPCHUAQUYDOI, 0)) AS SOLUONG, " +
                    "SUM(COALESCE(D.THANHTIEN, 0)) AS THANHTIEN, " +
                    "SUM(COALESCE(" + costColumn + ", 0) * (COALESCE(D.SLXUAT, 0) - COALESCE(D.SLNHAP, 0))) AS THANHTIENNHAP " +
                    "FROM TDONHANGCHITIET D " +
                    "INNER JOIN TDONHANG H ON H.ID = D.TDONHANGID " +
                    "INNER JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                    "LEFT JOIN DDONVITINH U ON U.ID = D.DDONVITINHID " +
                    "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                    "GROUP BY " +
                    "D.DMATHANGID, M.DNHOMMATHANGID, M.CODE, M.MASANCO, D.TENHANG, D.DDONVITINHID, " +
                    "U.NAME, D.DONGIA, D.TILEGIAMGIA, D.TIENGIAMGIA, " + costColumn + " " +
                    "ORDER BY " + orderBy;

                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                var list = new List<ItemSalesRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new ItemSalesRow
                    {
                        ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                        GroupId = DbHelper.Str(r["DNHOMMATHANGID"]) ?? "",
                        ProductCode = DbHelper.Str(r["CODE"]) ?? "",
                        Barcode = DbHelper.Str(r["MASANCO"]) ?? "",
                        ItemName = DbHelper.Str(r["TENHANG"]) ?? "",
                        UnitId = DbHelper.Str(r["DDONVITINHID"]) ?? "",
                        UnitName = DbHelper.Str(r["DVT"]) ?? "",
                        UnitPrice = DbHelper.Dec(r["DONGIA"]),
                        DiscountRate = DbHelper.Dec(r["TILEGIAMGIA"]),
                        LineDiscountAmount = DbHelper.Dec(r["TIENGIAMGIA"]),
                        UnitCost = DbHelper.Dec(r["UNITCOST"]),
                        Quantity = DbHelper.Dec(r["SOLUONG"]),
                        SalesAmount = DbHelper.Dec(r["THANHTIEN"]),
                        CostAmount = DbHelper.Dec(r["THANHTIENNHAP"])
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Truy vấn tổng riêng trên TDONHANG trong đúng phạm vi bill (BillReportScopeSql) cho khối tổng (§2.0, §2.3):
        /// Giảm giá = Σ (COALESCE(TIENGIAMGIATONG,0) + COALESCE(GIAMGIAMATHANG,0)), Tiền giờ, Phí dịch vụ, Thuế.
        /// </summary>
        public ItemSalesBillTotals LoadBillSummaryTotals(BillReportScope scope)
        {
            if (scope == null)
                throw new ArgumentNullException("scope");

            return DbHelper.Read(_databasePath, conn =>
            {
                var conditions = new List<string>(6);
                var args = new List<object>(12);
                BillReportScopeSql.AppendWhere("H", scope, conditions, args);

                string sql =
                    "SELECT " +
                    "COALESCE(SUM(COALESCE(H.TIENGIAMGIATONG, 0) + COALESCE(H.GIAMGIAMATHANG, 0)), 0) AS TONGGIAMGIA, " +
                    "COALESCE(SUM(COALESCE(H.TIENGIO, 0)), 0) AS TIENGIO, " +
                    "COALESCE(SUM(COALESCE(H.PHIDICHVU, 0)), 0) AS PHIDICHVU, " +
                    "COALESCE(SUM(COALESCE(H.TIENTHUE, 0)), 0) AS TIENTHUE " +
                    "FROM TDONHANG H " +
                    "WHERE " + string.Join(" AND ", conditions.ToArray());

                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                if (dt.Rows.Count == 0)
                    return new ItemSalesBillTotals();

                DataRow r = dt.Rows[0];
                return new ItemSalesBillTotals
                {
                    TotalDiscount = DbHelper.Dec(r["TONGGIAMGIA"]),
                    TimeCharge = DbHelper.Dec(r["TIENGIO"]),
                    ServiceFee = DbHelper.Dec(r["PHIDICHVU"]),
                    Tax = DbHelper.Dec(r["TIENTHUE"])
                };
            });
        }

        /// <summary>
        /// Danh sách hóa đơn ở lưới trên dialog chi tiết (§2.0): cùng phạm vi bill (BillReportScopeSql),
        /// có ít nhất một dòng khớp DMATHANGID, COALESCE(DONGIA,0), COALESCE(TILEGIAMGIA,0),
        /// sắp theo GIOTHANHTOAN tăng dần.
        /// </summary>
        public List<ItemSalesBillRow> LoadDetailBills(BillReportScope scope, ItemSalesDetailKey key)
        {
            if (scope == null)
                throw new ArgumentNullException("scope");
            if (key == null || string.IsNullOrEmpty(key.ProductId))
                return new List<ItemSalesBillRow>();

            return DbHelper.Read(_databasePath, conn =>
            {
                var conditions = new List<string>(8);
                var args = new List<object>(18);
                BillReportScopeSql.AppendWhere("H", scope, conditions, args);

                conditions.Add(
                    "EXISTS (SELECT 1 FROM TDONHANGCHITIET D " +
                    "WHERE D.TDONHANGID = H.ID " +
                    "AND D.DMATHANGID = @ProductId " +
                    "AND COALESCE(D.DONGIA, 0) = @UnitPrice " +
                    "AND COALESCE(D.TILEGIAMGIA, 0) = @DiscountRate)");
                args.Add("@ProductId");
                args.Add(key.ProductId);
                args.Add("@UnitPrice");
                args.Add(key.UnitPrice);
                args.Add("@DiscountRate");
                args.Add(key.DiscountRate);

                string sql =
                    "SELECT " +
                    "H.ID, H.NOTE, H.NAME, H.NGAY, " +
                    "K.NAME AS KHACHHANG, K.MAKHACH, K.DIACHI, K.DIENTHOAI, " +
                    "H.TONGCONG, H.PHIVANCHUYEN, H.TIENGIAMGIA, H.TILEGIAMGIA, " +
                    "H.TIENTHUE, H.TILETHUE, H.TIENHANG, H.DIENGIAI, " +
                    "H.GIOTHANHTOAN, H.KHACHDUA, H.TRALAI, " +
                    "U.NAME AS THANHTOANBOI, DH.NAME AS SOPHIEUDATHANG, " +
                    "H.GIAOHANG, H.DOITRA " +
                    "FROM TDONHANG H " +
                    "LEFT JOIN DKHACHHANG K ON K.ID = H.DKHACHHANGID " +
                    "LEFT JOIN SUSER U ON U.ID = H.USERTHANHTOANID " +
                    "LEFT JOIN TDATHANG DH ON DH.ID = H.TDATHANGID " +
                    "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                    "ORDER BY H.GIOTHANHTOAN, H.NAME";

                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                var list = new List<ItemSalesBillRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new ItemSalesBillRow
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Note = DbHelper.Str(r["NOTE"]),
                        Name = DbHelper.Str(r["NAME"]),
                        Ngay = ReadDateTime(r["NGAY"]),
                        CustomerName = DbHelper.Str(r["KHACHHANG"]),
                        CustomerCode = DbHelper.Str(r["MAKHACH"]),
                        CustomerAddress = DbHelper.Str(r["DIACHI"]),
                        CustomerPhone = DbHelper.Str(r["DIENTHOAI"]),
                        TotalAmount = ReadDecimal(r["TONGCONG"]),
                        ShippingFee = ReadDecimal(r["PHIVANCHUYEN"]),
                        DiscountAmount = ReadDecimal(r["TIENGIAMGIA"]),
                        DiscountRate = ReadDecimal(r["TILEGIAMGIA"]),
                        TaxAmount = ReadDecimal(r["TIENTHUE"]),
                        TaxRate = ReadDecimal(r["TILETHUE"]),
                        GoodsAmount = ReadDecimal(r["TIENHANG"]),
                        Description = DbHelper.Str(r["DIENGIAI"]),
                        PaidTime = ReadDateTime(r["GIOTHANHTOAN"]),
                        CashTendered = ReadDecimal(r["KHACHDUA"]),
                        ChangeAmount = ReadDecimal(r["TRALAI"]),
                        PaidBy = DbHelper.Str(r["THANHTOANBOI"]),
                        BookingNumber = DbHelper.Str(r["SOPHIEUDATHANG"]),
                        Delivery = DbHelper.Str(r["GIAOHANG"]),
                        ReturnAmount = ReadDecimal(r["DOITRA"])
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Dòng chi tiết của hóa đơn đang chọn ở lưới dưới dialog chi tiết (§2.0):
        /// loại XUATVATTU và combo con (KHÔNG loại nguyên liệu), hiển thị DMATHANG.NAME, ĐVT của mặt hàng,
        /// bảo hành mặt hàng và bảo hành dòng, SLXUAT nguyên giá trị.
        /// </summary>
        public List<ItemSalesBillLine> LoadDetailBillLines(string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return new List<ItemSalesBillLine>();

            return DbHelper.Read(_databasePath, conn =>
            {
                const string sql =
                    "SELECT " +
                    "D.ID, D.DMATHANGID, D.NOTE, " +
                    "COALESCE(M.NAME, D.TENHANG) AS TENMATHANG, " +
                    "U.NAME AS DVT, M.CODE AS MAHANG, M.BAOHANH AS BAOHANHMATHANG, " +
                    "M.MASANCO, D.BAOHANH AS BAOHANHDONG, " +
                    "D.TILEGIAMGIA, D.THANHTIEN, D.DONGIA, D.SLXUAT " +
                    "FROM TDONHANGCHITIET D " +
                    "LEFT JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                    "LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
                    "WHERE D.TDONHANGID = @BillId " +
                    "AND COALESCE(D.XUATVATTU, 0) = 0 " +
                    "AND COALESCE(D.COMBOPARENTID, '') = '' " +
                    "ORDER BY D.TIMECREATED, D.ID";

                DataTable dt = DbHelper.Table(conn, null, sql, "@BillId", billId);
                var list = new List<ItemSalesBillLine>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new ItemSalesBillLine
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                        Note = DbHelper.Str(r["NOTE"]),
                        ProductName = DbHelper.Str(r["TENMATHANG"]),
                        ProductUnitName = DbHelper.Str(r["DVT"]),
                        ProductCode = DbHelper.Str(r["MAHANG"]),
                        ProductWarranty = DbHelper.Str(r["BAOHANHMATHANG"]),
                        Barcode = DbHelper.Str(r["MASANCO"]),
                        LineWarranty = DbHelper.Str(r["BAOHANHDONG"]),
                        DiscountRate = ReadDecimal(r["TILEGIAMGIA"]),
                        Amount = ReadDecimal(r["THANHTIEN"]),
                        UnitPrice = ReadDecimal(r["DONGIA"]),
                        QuantityOut = ReadDecimal(r["SLXUAT"])
                    });
                }
                return list;
            });
        }

        private static DateTime? ReadDateTime(object v)
        {
            if (v == null || v is DBNull)
                return null;
            if (v is DateTime)
                return (DateTime)v;
            if (v is TimeSpan)
                return DateTime.Today.Add((TimeSpan)v);
            return Convert.ToDateTime(v);
        }

        private static decimal? ReadDecimal(object v)
        {
            return v == null || v is DBNull ? (decimal?)null : Convert.ToDecimal(v);
        }
    }
}
