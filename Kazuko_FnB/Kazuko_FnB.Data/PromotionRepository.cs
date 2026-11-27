using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi Danh mục đợt khuyến mại (DDOTKHUYENMAI), chi tiết (DDOTKHUYENMAICHITIET) và
    /// danh sách loại hình hiện có (DLOAIHINHKHUYENMAI) theo docs/specs/danh-muc-khuyen-mai.md §2, §3, §8.
    /// Không đụng các cột tiền giờ/giờ trên DDOTKHUYENMAI; SQL tham số hóa; không DDL.
    /// </summary>
    public sealed class PromotionRepository
    {
        public const int ActiveStatus = PromotionText.ActiveStatus;

        private const string SelectHeaderSql =
            "SELECT P.ID, P.NAME, P.NOTE, P.DLOAIHINHKHUYENMAIID, P.TUNGAY, P.DENNGAY, " +
            "COALESCE(P.NGUNGAPDUNG, 0) AS NGUNGAPDUNG, COALESCE(P.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
            "COALESCE(P.STATUS, 0) AS STATUS, P.USERCREATEDID, P.TIMECREATED, P.USERMODIFIEDID, P.TIMEMODIFIED, " +
            "COALESCE(T.NAME, '') AS TYPENAME, " +
            "COALESCE(UC.NAME, '') AS CREATEDBY, COALESCE(UM.NAME, '') AS MODIFIEDBY, " +
            "(SELECT COUNT(*) FROM DDOTKHUYENMAICHITIET D WHERE D.DDOTKHUYENMAIID = P.ID AND D.STATUS = 30) AS DETAILCOUNT " +
            "FROM DDOTKHUYENMAI P " +
            "LEFT JOIN DLOAIHINHKHUYENMAI T ON T.ID = P.DLOAIHINHKHUYENMAIID " +
            "LEFT JOIN SUSER UC ON UC.ID = P.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = P.USERMODIFIEDID ";

        private const string SelectDetailSql =
            "SELECT D.ID, D.DDOTKHUYENMAIID, D.DNHOMMATHANGID, COALESCE(D.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
            "D.DMATHANGID, COALESCE(D.GIABAN, 0) AS GIABAN, COALESCE(D.GIATRIDONHANG, 0) AS GIATRIDONHANG, " +
            "COALESCE(D.SOLUONGMATHANG, 0) AS SOLUONGMATHANG, COALESCE(D.SOLUONGMUA, 0) AS SOLUONGMUA, " +
            "COALESCE(D.SOLUONGTANG, 0) AS SOLUONGTANG, D.DMATHANGTANGID, D.NOTE, COALESCE(D.STATUS, 0) AS STATUS, " +
            "COALESCE(G.NAME, '') AS GROUPNAME, " +
            "COALESCE(M.CODE, '') AS PRODCODE, COALESCE(M.NAME, '') AS PRODNAME, " +
            "COALESCE(UM.NAME, '') AS PRODUNIT, COALESCE(M.GIABAN, 0) AS PRODPRICE, " +
            "COALESCE(MG.NAME, '') AS GIFTNAME, COALESCE(UG.NAME, '') AS GIFTUNIT, " +
            "COALESCE(MG.GIABAN, 0) AS GIFTPRICE " +
            "FROM DDOTKHUYENMAICHITIET D " +
            "LEFT JOIN DNHOMMATHANG G ON G.ID = D.DNHOMMATHANGID " +
            "LEFT JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
            "LEFT JOIN DDONVITINH UM ON UM.ID = M.DDONVITINHID " +
            "LEFT JOIN DMATHANG MG ON MG.ID = D.DMATHANGTANGID " +
            "LEFT JOIN DDONVITINH UG ON UG.ID = MG.DDONVITINHID ";

        private readonly string _databasePath;

        public PromotionRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn => InvoiceNumberRepository.GetServerTimestamp(conn, null).Date);
        }

        /// <summary>Các loại hình khuyến mại hiện có trong DB (STATUS = 30); không tự thêm dòng loại.</summary>
        public List<PromotionTypeItem> LoadTypes()
        {
            return DbHelper.Read(_databasePath, conn => LoadTypesIn(conn, null));
        }

        public static List<PromotionTypeItem> LoadTypesIn(FbConnection conn, FbTransaction tx)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME FROM DLOAIHINHKHUYENMAI WHERE STATUS = @S ORDER BY NAME",
                "@S", ActiveStatus);
            var list = new List<PromotionTypeItem>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new PromotionTypeItem
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Name = DbHelper.Str(r["NAME"]) ?? ""
                });
            }
            return list;
        }

        public static PromotionTypeItem LoadTypeByIdIn(FbConnection conn, FbTransaction tx, string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
                return null;
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID, NAME FROM DLOAIHINHKHUYENMAI WHERE ID = @I",
                "@I", typeId);
            if (dt.Rows.Count == 0)
                return null;
            return new PromotionTypeItem
            {
                Id = DbHelper.Str(dt.Rows[0]["ID"]) ?? "",
                Name = DbHelper.Str(dt.Rows[0]["NAME"]) ?? ""
            };
        }

        /// <summary>Mọi đợt khuyến mại đang hoạt động (STATUS = 30), xếp theo Từ ngày giảm dần rồi Tên.</summary>
        public List<PromotionRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    SelectHeaderSql + "WHERE P.STATUS = @S ORDER BY P.TUNGAY DESC, P.NAME",
                    "@S", ActiveStatus);
                var list = new List<PromotionRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(HeaderFrom(r));
                return list;
            });
        }

        /// <summary>Một đợt khuyến mại theo ID (mọi STATUS); null nếu không tồn tại.</summary>
        public PromotionRow LoadById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadByIdIn(conn, null, id));
        }

        public static PromotionRow LoadByIdIn(FbConnection conn, FbTransaction tx, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            DataTable dt = DbHelper.Table(conn, tx, SelectHeaderSql + "WHERE P.ID = @I", "@I", id);
            return dt.Rows.Count == 0 ? null : HeaderFrom(dt.Rows[0]);
        }

        /// <summary>Danh sách chi tiết của một đợt khuyến mại (STATUS = 30), giữ nguyên thứ tự ghi.</summary>
        public List<PromotionDetailRow> LoadDetails(string promotionId)
        {
            if (string.IsNullOrEmpty(promotionId))
                return new List<PromotionDetailRow>();
            return DbHelper.Read(_databasePath, conn => LoadDetailsIn(conn, null, promotionId));
        }

        public static List<PromotionDetailRow> LoadDetailsIn(FbConnection conn, FbTransaction tx, string promotionId)
        {
            if (string.IsNullOrEmpty(promotionId))
                return new List<PromotionDetailRow>();
            DataTable dt = DbHelper.Table(conn, tx,
                SelectDetailSql + "WHERE D.DDOTKHUYENMAIID = @P AND D.STATUS = @S ORDER BY D.TIMECREATED, D.ID",
                "@P", promotionId, "@S", ActiveStatus);
            var list = new List<PromotionDetailRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
                list.Add(DetailFrom(r));
            return list;
        }

        public static int CountDetailsIn(FbConnection conn, FbTransaction tx, string promotionId)
        {
            if (string.IsNullOrEmpty(promotionId))
                return 0;
            return DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DDOTKHUYENMAICHITIET WHERE DDOTKHUYENMAIID = @P AND STATUS = @S",
                "@P", promotionId, "@S", ActiveStatus));
        }

        /// <summary>Các mặt hàng đang dùng (STATUS = 30, loại mặt hàng hệ thống 'KM') cho hộp chọn mặt hàng.</summary>
        public List<PromotionProductItem> LoadProductsForPicker()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT M.ID, COALESCE(M.CODE, '') AS CODE, COALESCE(M.NAME, '') AS NAME, " +
                    "COALESCE(U.NAME, '') AS UNITNAME, COALESCE(M.GIABAN, 0) AS GIABAN " +
                    "FROM DMATHANG M " +
                    "LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
                    "WHERE M.STATUS = @S AND M.ID <> @KM " +
                    "ORDER BY M.NAME, M.CODE",
                    "@S", ActiveStatus, "@KM", ProductRepository.SystemProductId);
                var list = new List<PromotionProductItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new PromotionProductItem
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Code = DbHelper.Str(r["CODE"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                        SellPrice = DbHelper.Dec(r["GIABAN"])
                    });
                }
                return list;
            });
        }

        /// <summary>Tên đợt đã tồn tại chưa (so theo UNICODE_CI_AI, không cắt khoảng trắng)?</summary>
        public static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string target = PromotionText.FoldNoTrim(name);
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DDOTKHUYENMAI WHERE NAME IS NOT NULL");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(PromotionText.FoldNoTrim(DbHelper.Str(r["NAME"])), target, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Đọc các đợt đang dùng (STATUS = 30, không ngừng áp dụng) cùng loại để kiểm tra giao ngày.</summary>
        public static List<PromotionRow> LoadActiveByTypeIn(FbConnection conn, FbTransaction tx, string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
                return new List<PromotionRow>();
            DataTable dt = DbHelper.Table(conn, tx,
                SelectHeaderSql + "WHERE P.STATUS = @S AND P.DLOAIHINHKHUYENMAIID = @T AND COALESCE(P.NGUNGAPDUNG, 0) <> @STOP ORDER BY P.NAME",
                "@S", ActiveStatus, "@T", typeId, "@STOP", PromotionText.FlagOn);
            var list = new List<PromotionRow>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
                list.Add(HeaderFrom(r));
            return list;
        }

        public static void InsertHeaderIn(FbConnection conn, FbTransaction tx, PromotionRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DDOTKHUYENMAI (ID, NAME, NOTE, STATUS, DLOAIHINHKHUYENMAIID, TUNGAY, DENNGAY, " +
                "NGUNGAPDUNG, TILEGIAMGIA, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @NAME, @NOTE, @ST, @TYPEID, @FROM, @TO, @STOP, @PCT, @UC, @TC, NULL, NULL)",
                "@ID", row.Id,
                "@NAME", row.Name ?? "",
                "@NOTE", NullIfEmpty(row.Note),
                "@ST", row.Status,
                "@TYPEID", row.TypeId,
                "@FROM", row.FromDate.HasValue ? (object)row.FromDate.Value.Date : null,
                "@TO", row.ToDate.HasValue ? (object)row.ToDate.Value.Date : null,
                "@STOP", row.StoppedFlag,
                "@PCT", row.DiscountPercent,
                "@UC", row.CreatedById ?? "",
                "@TC", row.CreatedAt);
        }

        public static int UpdateHeaderIn(FbConnection conn, FbTransaction tx, PromotionRow row)
        {
            // Giữ nguyên các cột tiền giờ/giờ trên DDOTKHUYENMAI (không ghi đè).
            return DbHelper.Exec(conn, tx,
                "UPDATE DDOTKHUYENMAI SET NAME = @NAME, NOTE = @NOTE, DLOAIHINHKHUYENMAIID = @TYPEID, " +
                "TUNGAY = @FROM, DENNGAY = @TO, NGUNGAPDUNG = @STOP, TILEGIAMGIA = @PCT, " +
                "USERMODIFIEDID = @UM, TIMEMODIFIED = @TM WHERE ID = @ID",
                "@NAME", row.Name ?? "",
                "@NOTE", NullIfEmpty(row.Note),
                "@TYPEID", row.TypeId,
                "@FROM", row.FromDate.HasValue ? (object)row.FromDate.Value.Date : null,
                "@TO", row.ToDate.HasValue ? (object)row.ToDate.Value.Date : null,
                "@STOP", row.StoppedFlag,
                "@PCT", row.DiscountPercent,
                "@UM", row.ModifiedById ?? "",
                "@TM", row.ModifiedAt,
                "@ID", row.Id);
        }

        public static void DeleteDetailsIn(FbConnection conn, FbTransaction tx, string promotionId)
        {
            DbHelper.Exec(conn, tx, "DELETE FROM DDOTKHUYENMAICHITIET WHERE DDOTKHUYENMAIID = @P", "@P", promotionId);
        }

        public static void InsertDetailsIn(
            FbConnection conn,
            FbTransaction tx,
            string promotionId,
            IList<PromotionDetailRow> details,
            string actorId,
            DateTime serverNow)
        {
            if (details == null || details.Count == 0)
                return;
            for (int i = 0; i < details.Count; i++)
            {
                PromotionDetailRow d = details[i];
                string lineId = DbHelper.NewId();
                d.Id = lineId;
                d.PromotionId = promotionId;
                d.Status = ActiveStatus;
                DateTime lineTime = serverNow.AddSeconds(i);
                DbHelper.Exec(conn, tx,
                    "INSERT INTO DDOTKHUYENMAICHITIET (ID, DDOTKHUYENMAIID, DNHOMMATHANGID, TILEGIAMGIA, " +
                    "DMATHANGID, GIABAN, GIATRIDONHANG, SOLUONGMATHANG, SOLUONGMUA, SOLUONGTANG, " +
                    "DMATHANGTANGID, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                    "VALUES (@ID, @PROMO, @GRP, @PCT, @PROD, @PRICE, @VAL, @QTY, @BUY, @GIFTQTY, " +
                    "@GIFTPROD, @NOTE, @ST, @UC, @TC, NULL, NULL)",
                    "@ID", lineId,
                    "@PROMO", promotionId,
                    "@GRP", NullIfEmpty(d.ProductGroupId),
                    "@PCT", d.DiscountPercent,
                    "@PROD", NullIfEmpty(d.ProductId),
                    "@PRICE", d.SalePrice,
                    "@VAL", d.OrderValue,
                    "@QTY", d.ItemQuantity,
                    "@BUY", d.BuyQuantity,
                    "@GIFTQTY", d.GiftQuantity,
                    "@GIFTPROD", NullIfEmpty(d.GiftProductId),
                    "@NOTE", NullIfEmpty(d.Note),
                    "@ST", ActiveStatus,
                    "@UC", actorId ?? "",
                    "@TC", lineTime);
            }
        }

        public static int DeleteHeaderIn(FbConnection conn, FbTransaction tx, string id)
        {
            return DbHelper.Exec(conn, tx, "DELETE FROM DDOTKHUYENMAI WHERE ID = @I", "@I", id);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static DateTime? ReadDate(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }

        private static PromotionRow HeaderFrom(DataRow r)
        {
            return new PromotionRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                TypeId = DbHelper.Str(r["DLOAIHINHKHUYENMAIID"]),
                TypeName = DbHelper.Str(r["TYPENAME"]) ?? "",
                FromDate = ReadDate(r["TUNGAY"]),
                ToDate = ReadDate(r["DENNGAY"]),
                StoppedFlag = DbHelper.Int(r["NGUNGAPDUNG"]),
                DiscountPercent = DbHelper.Dec(r["TILEGIAMGIA"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = ReadDate(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = ReadDate(r["TIMEMODIFIED"]),
                CreatedByName = DbHelper.Str(r["CREATEDBY"]) ?? "",
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"]) ?? "",
                DetailCount = DbHelper.Int(r["DETAILCOUNT"])
            };
        }

        private static PromotionDetailRow DetailFrom(DataRow r)
        {
            return new PromotionDetailRow
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                PromotionId = DbHelper.Str(r["DDOTKHUYENMAIID"]) ?? "",
                ProductGroupId = DbHelper.Str(r["DNHOMMATHANGID"]),
                ProductGroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                DiscountPercent = DbHelper.Dec(r["TILEGIAMGIA"]),
                ProductId = DbHelper.Str(r["DMATHANGID"]),
                ProductCode = DbHelper.Str(r["PRODCODE"]) ?? "",
                ProductName = DbHelper.Str(r["PRODNAME"]) ?? "",
                ProductUnitName = DbHelper.Str(r["PRODUNIT"]) ?? "",
                ProductSellPrice = DbHelper.Dec(r["PRODPRICE"]),
                SalePrice = DbHelper.Dec(r["GIABAN"]),
                OrderValue = DbHelper.Dec(r["GIATRIDONHANG"]),
                ItemQuantity = DbHelper.Dec(r["SOLUONGMATHANG"]),
                BuyQuantity = DbHelper.Dec(r["SOLUONGMUA"]),
                GiftQuantity = DbHelper.Dec(r["SOLUONGTANG"]),
                GiftProductId = DbHelper.Str(r["DMATHANGTANGID"]),
                GiftProductName = DbHelper.Str(r["GIFTNAME"]) ?? "",
                GiftProductUnitName = DbHelper.Str(r["GIFTUNIT"]) ?? "",
                GiftProductSellPrice = DbHelper.Dec(r["GIFTPRICE"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Status = DbHelper.Int(r["STATUS"])
            };
        }
    }
}
