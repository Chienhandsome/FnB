using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi DMATHANG + DDINHLUONG (spec docs/specs/danh-muc-mat-hang.md).
    /// Mọi câu SQL tham số hóa; lưu mặt hàng + định lượng + cập nhật giá các mặt hàng chứa nó = MỘT transaction.
    /// Không DDL, không sửa schema. Mặt hàng hệ thống 'KM' (STATUS = 60) không hiện, không sửa/xóa.
    /// </summary>
    public sealed class ProductRepository
    {
        /// <summary>Mặt hàng hệ thống "Khuyến mại tự động" (STATUS = 60).</summary>
        public const string SystemProductId = "KM";

        private const int ActiveStatus = 30;
        private const int FlagOn = 30;
        private const int FlagOff = 0;

        private readonly string _databasePath;

        public ProductRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        // ------------------------------------------------------------------ Đọc

        private const string SelectList =
            "M.ID, M.CODE, M.NAME, M.TENTIENGANH, M.NOTE, M.GIANHAP, M.GIAVON, M.GIABAN, M.GIABAN2, M.GIABAN3, M.GIABAN4, " +
            "M.TONTOITHIEU, M.TONTOIDA, M.BAOHANH, M.MASANCO, M.DHANGSANXUATID, M.HOAHONG, " +
            "M.DDONVITINHCHANID, M.GIABANCHAN, M.DDOITACKYGUIID, M.MACDINHGIAMGIA, M.MACDINHGIAMTIEN, " +
            "M.DNHOMMATHANGID, M.DDONVITINHID, M.QUYDOI, M.THEOCAN, M.COHANSUDUNG, M.SIZE, " +
            "M.DLOAIMATHANGID, M.TAMKHOA, M.GIATHEOTHOIGIA, M.MAUSAC, M.GIOHATCOMBO, " +
            "M.STATUS, M.USERCREATEDID, M.TIMECREATED, M.USERMODIFIEDID, M.TIMEMODIFIED, " +
            "G.NAME AS GROUPNAME, T.NAME AS TYPENAME, U.NAME AS UNITNAME, UB.NAME AS BIGUNITNAME, " +
            "UC.NAME AS CREATEDBY, UM.NAME AS MODIFIEDBY";

        private const string SelectFrom =
            "FROM DMATHANG M " +
            "LEFT JOIN DNHOMMATHANG G ON G.ID = M.DNHOMMATHANGID " +
            "LEFT JOIN DLOAIMATHANG T ON T.ID = M.DLOAIMATHANGID " +
            "LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
            "LEFT JOIN DDONVITINH UB ON UB.ID = M.DDONVITINHCHANID " +
            "LEFT JOIN SUSER UC ON UC.ID = M.USERCREATEDID " +
            "LEFT JOIN SUSER UM ON UM.ID = M.USERMODIFIEDID ";

        /// <summary>Mọi mặt hàng đang dùng (STATUS = 30), xếp theo tên — dữ liệu cho lưới danh mục.</summary>
        public List<ProductRow> LoadProducts()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectList + " " + SelectFrom + "WHERE M.STATUS = @S ORDER BY M.NAME", "@S", ActiveStatus);
                var list = new List<ProductRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một mặt hàng đầy đủ (kể cả ảnh ANH); null nếu không có.</summary>
        public ProductRow LoadProduct(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectList + " " + SelectFrom + "WHERE M.ID = @I", "@I", id);
                if (dt.Rows.Count == 0)
                    return null;
                ProductRow row = RowFrom(dt.Rows[0]);
                // BLOB đọc riêng bằng Scalar (như ImageRepository) để tránh lỗi khi DataAdapter nạp BLOB.
                row.Image = DbHelper.Scalar(conn, null, "SELECT ANH FROM DMATHANG WHERE ID = @I", "@I", id) as byte[];
                return row;
            });
        }

        /// <summary>Các loại mặt hàng (DLOAIMATHANG) đang dùng, xếp theo ID ('0'..'7').</summary>
        public List<ProductTypeInfo> LoadTypes()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, COBANHANG, COTONKHO, CODINHLUONG FROM DLOAIMATHANG WHERE STATUS = @S ORDER BY ID",
                    "@S", ActiveStatus);
                var list = new List<ProductTypeInfo>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new ProductTypeInfo
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Name = DbHelper.Str(r["NAME"]),
                        CanSell = DbHelper.Int(r["COBANHANG"]) != 0,
                        HasStock = DbHelper.Int(r["COTONKHO"]) != 0,
                        HasRecipe = DbHelper.Int(r["CODINHLUONG"]) != 0
                    });
                return list;
            });
        }

        /// <summary>
        /// Vật tư được chọn cho định lượng: mặt hàng loại 0 (hoặc rỗng), 2, hoặc 1; khác chính mặt hàng đang sửa.
        /// '' cũng xem như '0' (COALESCE của app gốc chỉ xử lý NULL nên ở đây liệt kê tường minh).
        /// </summary>
        public List<MaterialInfo> LoadMaterials(string excludeId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT M.ID, M.NAME, M.DLOAIMATHANGID, M.DDONVITINHID, M.GIANHAP, M.GIAVON, M.QUYDOI, " +
                    "M.DDONVITINHCHANID, U.NAME AS UNITNAME " +
                    "FROM DMATHANG M LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
                    "WHERE M.STATUS = @S " +
                    "AND (M.DLOAIMATHANGID IS NULL OR M.DLOAIMATHANGID = '' OR M.DLOAIMATHANGID IN ('0','1','2')) " +
                    "AND M.ID <> @X ORDER BY M.NAME",
                    "@S", ActiveStatus, "@X", excludeId ?? "");
                var list = new List<MaterialInfo>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(MaterialFrom(r));
                return list;
            });
        }

        /// <summary>Các dòng định lượng của một mặt hàng (kèm giá của vật tư; vật tư mồ côi vẫn trả về, đánh dấu Missing).</summary>
        public List<RecipeLine> LoadRecipeLines(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return new List<RecipeLine>();
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT D.ID, D.DVATTUID, D.SOLUONG, " +
                    "M.NAME, M.DLOAIMATHANGID, M.DDONVITINHID, M.GIANHAP, M.GIAVON, M.QUYDOI, M.DDONVITINHCHANID, " +
                    "U.NAME AS UNITNAME " +
                    "FROM DDINHLUONG D " +
                    "LEFT JOIN DMATHANG M ON M.ID = D.DVATTUID " +
                    "LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
                    "WHERE D.DMATHANGID = @P AND D.STATUS = @S ORDER BY D.TIMECREATED, D.ID",
                    "@P", productId, "@S", ActiveStatus);
                var list = new List<RecipeLine>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    bool missing = r["NAME"] is DBNull;
                    decimal buy = DbHelper.Dec(r["GIANHAP"]);
                    decimal cost = DbHelper.Dec(r["GIAVON"]);
                    decimal? quyDoi = DecN(r["QUYDOI"]);
                    bool hasBigUnit = !string.IsNullOrEmpty(DbHelper.Str(r["DDONVITINHCHANID"]));
                    decimal unitBuy = missing ? 0m : ProductMath.UnitBuyPrice(buy, quyDoi, hasBigUnit);
                    decimal qty = DbHelper.Dec(r["SOLUONG"]);
                    list.Add(new RecipeLine
                    {
                        Id = DbHelper.Str(r["ID"]),
                        MaterialId = DbHelper.Str(r["DVATTUID"]),
                        MaterialName = DbHelper.Str(r["NAME"]),
                        MaterialTypeId = DbHelper.Str(r["DLOAIMATHANGID"]),
                        MaterialUnitId = DbHelper.Str(r["DDONVITINHID"]),
                        MaterialUnitName = DbHelper.Str(r["UNITNAME"]),
                        Quantity = qty,
                        BuyPrice = unitBuy,
                        CostPrice = cost,
                        AmountBuy = ProductMath.AmountBuy(qty, unitBuy),
                        AmountCost = ProductMath.AmountCost(qty, cost),
                        MaterialQuyDoi = quyDoi,
                        MaterialHasBigUnit = hasBigUnit,
                        MaterialMissing = missing
                    });
                }
                return list;
            });
        }

        // ------------------------------------------------------------------ Lịch sử mua/bán (1.4)

        /// <summary>Lịch sử nhập mua hàng: TDONHANG.LOAI = 1, giá = TDONHANGCHITIET.DONGIA, cột thứ 3 = nhà cung cấp.</summary>
        public List<ProductHistoryRow> LoadBuyHistory(string productId)
        {
            return LoadHistory(productId, true);
        }

        /// <summary>Lịch sử xuất bán hàng: LOAI = 0 AND DATHANHTOAN = 30, cột thứ 3 = khách hàng, xếp theo NGAY, GIOTHANHTOAN.</summary>
        public List<ProductHistoryRow> LoadSellHistory(string productId)
        {
            return LoadHistory(productId, false);
        }

        private List<ProductHistoryRow> LoadHistory(string productId, bool buy)
        {
            if (string.IsNullOrEmpty(productId))
                return new List<ProductHistoryRow>();
            string partner = buy
                ? "LEFT JOIN DNHACUNGCAP N ON N.ID = H.DNHACUNGCAPID"
                : "LEFT JOIN DKHACHHANG N ON N.ID = H.DKHACHHANGID";
            string where = buy ? "H.LOAI = 1" : "H.LOAI = 0 AND H.DATHANHTOAN = 30";
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT H.NGAY, H.GIOTHANHTOAN, N.NAME AS PARTNER, C.DONGIA " +
                    "FROM TDONHANGCHITIET C INNER JOIN TDONHANG H ON H.ID = C.TDONHANGID " + partner + " " +
                    "WHERE C.DMATHANGID = @I AND C.STATUS = @S AND " + where + " ORDER BY H.NGAY, H.GIOTHANHTOAN",
                    "@I", productId, "@S", ActiveStatus);
                var list = new List<ProductHistoryRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new ProductHistoryRow
                    {
                        Date = r["NGAY"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["NGAY"]),
                        Time = FormatTime(r["GIOTHANHTOAN"]),
                        PartnerName = DbHelper.Str(r["PARTNER"]),
                        Price = DbHelper.Dec(r["DONGIA"])
                    });
                return list;
            });
        }

        private static string FormatTime(object v)
        {
            if (v == null || v is DBNull)
                return "";
            if (v is TimeSpan)
                return ((TimeSpan)v).ToString(@"hh\:mm");
            if (v is DateTime)
                return ((DateTime)v).ToString("HH:mm");
            return Convert.ToString(v);
        }

        // ------------------------------------------------------------------ Kiểm trùng (dùng ở dialog + trong transaction lúc lưu)

        /// <summary>CODE đã tồn tại chưa (trừ chính dòng đang sửa)? CODE rỗng '' không tính là trùng (Q5).</summary>
        public bool IsCodeTaken(string code, string excludeId)
        {
            if (string.IsNullOrEmpty(code))
                return false;
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DMATHANG WHERE CODE = @C AND ID <> @X", "@C", code, "@X", excludeId ?? "")) > 0);
        }

        /// <summary>Tên đã tồn tại chưa (không phân biệt hoa/thường, cắt khoảng trắng, trùng toàn bảng — Q21)?</summary>
        public bool IsNameTaken(string name, string excludeId)
        {
            string norm = ProductText.NormalizeName(name);
            if (norm.Length == 0)
                return false;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null, "SELECT ID, NAME FROM DMATHANG");
                foreach (DataRow r in dt.Rows)
                {
                    if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                        continue;
                    if (string.Equals(ProductText.NormalizeName(DbHelper.Str(r["NAME"])), norm,
                        StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            });
        }

        // ------------------------------------------------------------------ Lưu (một transaction)

        /// <summary>
        /// Lưu mặt hàng + định lượng trong MỘT transaction; kiểm trùng mã/tên trong cùng transaction;
        /// sau đó cập nhật giá nhập/vốn của mọi mặt hàng loại 1/4/5 chứa mặt hàng này làm vật tư (3.4, chỉ một cấp).
        /// </summary>
        public ProductSaveResult SaveProduct(ProductRow row, List<RecipeLine> lines, string actorId, ProductSaveOptions options)
        {
            var result = new ProductSaveResult();
            if (row == null)
            {
                result.ErrorMessage = "Không có dữ liệu mặt hàng.";
                return result;
            }
            if (options == null)
                options = new ProductSaveOptions();
            if (lines == null)
                lines = new List<RecipeLine>();

            // Trạng thái bản ghi trước khi lưu: nếu lỗi/ném ngoại lệ thì trả lại y nguyên để lần lưu sau
            // vẫn được coi là THÊM MỚI (không để lại Id "ma" làm repo tưởng là sửa).
            bool isNew = string.IsNullOrEmpty(row.Id);
            string oldName = row.Name;
            try
            {
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                // Kiểm trống/trùng TRƯỚC khi gán Id và trước khi ghi (không có dòng nào được ghi nếu lỗi).
                string name = ProductText.NormalizeName(row.Name);
                if (name.Length == 0)
                {
                    result.ErrorMessage = ProductText.NameEmpty;
                    return;
                }
                if (options.CodeRequired && string.IsNullOrWhiteSpace(row.Code))
                {
                    result.ErrorMessage = ProductText.CodeEmpty;
                    return;
                }
                string excludeId = isNew ? "" : row.Id;
                if (!string.IsNullOrEmpty(row.Code) && IsCodeTakenIn(conn, tx, row.Code, excludeId))
                {
                    result.ErrorMessage = ProductText.CodeDuplicate(row.Code);
                    return;
                }
                if (!options.AllowDuplicateName && IsNameTakenIn(conn, tx, name, excludeId))
                {
                    result.ErrorMessage = ProductText.NameDuplicate(name);
                    return;
                }

                // Kiểm pass → mới gán Id/dấu vết.
                row.Name = name;
                if (isNew)
                {
                    row.Id = DbHelper.NewId();
                    row.Status = ActiveStatus;
                    row.CreatedById = actorId;
                    row.CreatedAt = DbHelper.Now();
                    row.ModifiedById = null;
                    row.ModifiedAt = null;
                    InsertProduct(conn, tx, row);
                }
                else
                {
                    row.ModifiedById = actorId;
                    row.ModifiedAt = DbHelper.Now();
                    if (UpdateProduct(conn, tx, row) == 0)
                    {
                        // Dòng đã bị xóa (hoặc Id không tồn tại): không ghi tiếp định lượng, không báo thành công.
                        result.ErrorMessage = "Mặt hàng không còn tồn tại.";
                        return;
                    }
                }

                // Định lượng: xóa hết dòng cũ rồi ghi lại (dòng không chọn vật tư thì bỏ — dialog đã chặn ở 2.1 #7).
                DbHelper.Exec(conn, tx, "DELETE FROM DDINHLUONG WHERE DMATHANGID = @P", "@P", row.Id);
                bool hasRecipeLines = false;
                foreach (RecipeLine line in lines)
                {
                    if (string.IsNullOrEmpty(line.MaterialId))
                        continue;
                    hasRecipeLines = true;
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DDINHLUONG (ID, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED, DMATHANGID, SOLUONG, DVATTUID) " +
                        "VALUES (@I, NULL, @S, @U, @T, NULL, NULL, @P, @Q, @M)",
                        "@I", DbHelper.NewId(), "@S", ActiveStatus, "@U", actorId, "@T", DbHelper.Now(),
                        "@P", row.Id, "@Q", line.Quantity, "@M", line.MaterialId);
                }

                // Tự tính lại giá của CHÍNH mặt hàng vừa lưu khi nó là mặt hàng định lượng (3.4):
                // GIANHAP = tổng THANHTIENNHAP, GIAVON = tổng THANHTIENVON (khi cấu hình tự cập nhật = 30).
                // Dialog cũng tính tổng ở UI, nhưng repo phải bảo đảm bất biến này cho mọi đường gọi
                // SaveProduct (kể cả gọi trực tiếp không qua dialog). Chỉ chạy khi có dòng định lượng
                // thật (không có dòng thì SUM = NULL, giữ nguyên giá trên dòng mặt hàng).
                string effectiveType = row.EffectiveTypeId;
                if (hasRecipeLines && (effectiveType == "1" || effectiveType == "4" || effectiveType == "5"))
                {
                    if (options.AutoUpdateCostPrice)
                        DbHelper.Exec(conn, tx,
                            "UPDATE DMATHANG SET GIAVON = (" +
                            "SELECT SUM(D.SOLUONG * B.GIAVON) FROM DDINHLUONG D " +
                            "INNER JOIN DMATHANG B ON D.DVATTUID = B.ID WHERE D.DMATHANGID = DMATHANG.ID) " +
                            "WHERE ID = @P",
                            "@P", row.Id);
                    if (options.AutoUpdateBuyPrice)
                        DbHelper.Exec(conn, tx,
                            "UPDATE DMATHANG SET GIANHAP = (" +
                            "SELECT SUM(D.SOLUONG * B.GIANHAP / CASE WHEN COALESCE(B.QUYDOI, 0) = 0 OR COALESCE(B.DDONVITINHCHANID, '') = '' THEN 1 ELSE B.QUYDOI END) " +
                            "FROM DDINHLUONG D INNER JOIN DMATHANG B ON D.DVATTUID = B.ID WHERE D.DMATHANGID = DMATHANG.ID) " +
                            "WHERE ID = @P",
                            "@P", row.Id);
                }

                // Cập nhật giá các mặt hàng loại 1/4/5 chứa mặt hàng này (3.4) — cùng transaction.
                if (options.AutoUpdateCostPrice)
                    DbHelper.Exec(conn, tx,
                        "UPDATE DMATHANG SET GIAVON = (" +
                        "SELECT SUM(D.SOLUONG * B.GIAVON) FROM DDINHLUONG D " +
                        "INNER JOIN DMATHANG B ON D.DVATTUID = B.ID WHERE D.DMATHANGID = DMATHANG.ID) " +
                        "WHERE (DLOAIMATHANGID = '1' OR DLOAIMATHANGID = '4' OR DLOAIMATHANGID = '5') " +
                        "AND EXISTS (SELECT * FROM DDINHLUONG WHERE DVATTUID = @M AND DMATHANGID = DMATHANG.ID)",
                        "@M", row.Id);
                if (options.AutoUpdateBuyPrice)
                    DbHelper.Exec(conn, tx,
                        "UPDATE DMATHANG SET GIANHAP = (" +
                        "SELECT SUM(D.SOLUONG * B.GIANHAP / CASE WHEN COALESCE(B.QUYDOI, 0) = 0 OR COALESCE(B.DDONVITINHCHANID, '') = '' THEN 1 ELSE B.QUYDOI END) " +
                        "FROM DDINHLUONG D INNER JOIN DMATHANG B ON D.DVATTUID = B.ID WHERE D.DMATHANGID = DMATHANG.ID) " +
                        "WHERE (DLOAIMATHANGID = '1' OR DLOAIMATHANGID = '4' OR DLOAIMATHANGID = '5') " +
                        "AND EXISTS (SELECT * FROM DDINHLUONG WHERE DVATTUID = @M AND DMATHANGID = DMATHANG.ID)",
                        "@M", row.Id);

                result.Ok = true;
                result.SavedId = row.Id;
            });
            }
            catch
            {
                if (isNew)
                    ResetNew(row, oldName);
                throw;
            }
            if (!result.Ok && isNew)
                ResetNew(row, oldName);
            return result;
        }

        /// <summary>Trả đối tượng về trạng thái "chưa lưu" sau khi thêm mới thất bại.</summary>
        private static void ResetNew(ProductRow row, string oldName)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
            row.Name = oldName;
        }

        private static bool IsCodeTakenIn(FbConnection conn, FbTransaction tx, string code, string excludeId)
        {
            return DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM DMATHANG WHERE CODE = @C AND ID <> @X", "@C", code, "@X", excludeId ?? "")) > 0;
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DMATHANG");
            foreach (DataRow r in dt.Rows)
            {
                if (string.Equals(DbHelper.Str(r["ID"]), excludeId ?? "", StringComparison.Ordinal))
                    continue;
                if (string.Equals(ProductText.NormalizeName(DbHelper.Str(r["NAME"])), name,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void InsertProduct(FbConnection conn, FbTransaction tx, ProductRow row)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO DMATHANG (ID, CODE, NAME, TENTIENGANH, NOTE, GIANHAP, GIAVON, GIABAN, GIABAN2, GIABAN3, GIABAN4, " +
                "TONTOITHIEU, TONTOIDA, BAOHANH, MASANCO, DHANGSANXUATID, ANH, HOAHONG, " +
                "DDONVITINHCHANID, GIABANCHAN, DDOITACKYGUIID, MACDINHGIAMGIA, MACDINHGIAMTIEN, " +
                "DNHOMMATHANGID, DDONVITINHID, QUYDOI, THEOCAN, COHANSUDUNG, SIZE, " +
                "DLOAIMATHANGID, TAMKHOA, GIATHEOTHOIGIA, MAUSAC, GIOHATCOMBO, " +
                "STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                "VALUES (@ID, @CODE, @NAME, @EN, @NOTE, @GIANHAP, @GIAVON, @GIABAN, @GIABAN2, @GIABAN3, @GIABAN4, " +
                "@TONTOITHIEU, @TONTOIDA, @BAOHANH, @MASANCO, @DHANGSANXUATID, @ANH, @HOAHONG, " +
                "@DDONVITINHCHANID, @GIABANCHAN, @DDOITACKYGUIID, @MACDINHGIAMGIA, @MACDINHGIAMTIEN, " +
                "@DNHOMMATHANGID, @DDONVITINHID, @QUYDOI, @THEOCAN, @COHANSUDUNG, @SIZE, " +
                "@DLOAIMATHANGID, @TAMKHOA, @GIATHEOTHOIGIA, @MAUSAC, @GIOHATCOMBO, " +
                "@STATUS, @USERCREATEDID, @TIMECREATED, @USERMODIFIEDID, @TIMEMODIFIED)",
                ProductParams(row));
        }

        private static int UpdateProduct(FbConnection conn, FbTransaction tx, ProductRow row)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE DMATHANG SET CODE = @CODE, NAME = @NAME, TENTIENGANH = @EN, NOTE = @NOTE, " +
                "GIANHAP = @GIANHAP, GIAVON = @GIAVON, GIABAN = @GIABAN, GIABAN2 = @GIABAN2, GIABAN3 = @GIABAN3, GIABAN4 = @GIABAN4, " +
                "TONTOITHIEU = @TONTOITHIEU, TONTOIDA = @TONTOIDA, BAOHANH = @BAOHANH, MASANCO = @MASANCO, " +
                "DHANGSANXUATID = @DHANGSANXUATID, ANH = @ANH, HOAHONG = @HOAHONG, " +
                "DDONVITINHCHANID = @DDONVITINHCHANID, GIABANCHAN = @GIABANCHAN, DDOITACKYGUIID = @DDOITACKYGUIID, " +
                "MACDINHGIAMGIA = @MACDINHGIAMGIA, MACDINHGIAMTIEN = @MACDINHGIAMTIEN, " +
                "DNHOMMATHANGID = @DNHOMMATHANGID, DDONVITINHID = @DDONVITINHID, QUYDOI = @QUYDOI, " +
                "THEOCAN = @THEOCAN, COHANSUDUNG = @COHANSUDUNG, SIZE = @SIZE, " +
                "DLOAIMATHANGID = @DLOAIMATHANGID, TAMKHOA = @TAMKHOA, GIATHEOTHOIGIA = @GIATHEOTHOIGIA, " +
                "MAUSAC = @MAUSAC, GIOHATCOMBO = @GIOHATCOMBO, STATUS = @STATUS, " +
                "USERMODIFIEDID = @USERMODIFIEDID, TIMEMODIFIED = @TIMEMODIFIED WHERE ID = @ID",
                ProductParams(row));
        }

        private static object[] ProductParams(ProductRow row)
        {
            return new object[]
            {
                "@ID", row.Id,
                "@CODE", string.IsNullOrEmpty(row.Code) ? "" : row.Code,
                "@NAME", row.Name,
                "@EN", string.IsNullOrEmpty(row.EnglishName) ? "" : row.EnglishName,
                "@NOTE", row.Note,
                "@GIANHAP", row.BuyPrice,
                "@GIAVON", row.CostPrice,
                "@GIABAN", row.SellPrice,
                "@GIABAN2", row.SellPrice2,
                "@GIABAN3", row.SellPrice3,
                "@GIABAN4", row.SellPrice4,
                "@TONTOITHIEU", row.MinStock,
                "@TONTOIDA", row.MaxStock,
                "@BAOHANH", row.Warranty,
                "@MASANCO", row.Barcode,
                "@DHANGSANXUATID", row.ManufacturerId,
                "@ANH", row.Image,
                "@HOAHONG", row.Commission,
                "@DDONVITINHCHANID", string.IsNullOrEmpty(row.BigUnitId) ? null : row.BigUnitId,
                "@GIABANCHAN", row.BigSellPrice,
                "@DDOITACKYGUIID", row.ConsignmentId,
                "@MACDINHGIAMGIA", row.DefaultDiscountRate,
                "@MACDINHGIAMTIEN", row.DefaultDiscountAmount,
                "@DNHOMMATHANGID", row.GroupId,
                "@DDONVITINHID", row.UnitId,
                "@QUYDOI", row.QuyDoi,
                "@THEOCAN", row.ByWeight ? FlagOn : FlagOff,
                "@COHANSUDUNG", row.HasExpiry,
                "@SIZE", row.Size,
                "@DLOAIMATHANGID", row.TypeId ?? "",
                "@TAMKHOA", row.TempLocked ? FlagOn : FlagOff,
                "@GIATHEOTHOIGIA", row.PriceByTime ? FlagOn : FlagOff,
                "@MAUSAC", row.Color,
                "@GIOHATCOMBO", row.SingHours,
                "@STATUS", row.Status,
                "@USERCREATEDID", row.CreatedById,
                "@TIMECREATED", row.CreatedAt,
                "@USERMODIFIEDID", row.ModifiedById,
                "@TIMEMODIFIED", row.ModifiedAt
            };
        }

        // ------------------------------------------------------------------ Xóa (3.6)

        /// <summary>
        /// Xóa HẲN dòng (không thùng rác, Chiến 21/11): kiểm tham chiếu trước, rồi xóa dòng định lượng của nó + chính nó.
        /// Mặt hàng hệ thống 'KM' không xóa được.
        /// </summary>
        public ProductDeleteResult DeleteProduct(string id)
        {
            var result = new ProductDeleteResult { Blocks = new List<ReferenceBlock>() };
            if (string.IsNullOrEmpty(id))
            {
                result.ErrorMessage = "Không có mặt hàng để xóa.";
                return result;
            }
            if (string.Equals(id, SystemProductId, StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = "Không xóa được mặt hàng hệ thống 'Khuyến mại tự động'.";
                return result;
            }

            // Đọc tên, kiểm tham chiếu và xóa trong CÙNG một giao dịch (không để dòng đổi/tham chiếu mới chen vào giữa).
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                string name = DbHelper.Str(DbHelper.Scalar(conn, tx, "SELECT NAME FROM DMATHANG WHERE ID = @I", "@I", id));
                if (name == null)
                {
                    result.ErrorMessage = "Mặt hàng không còn tồn tại.";
                    return;
                }
                result.Blocks = CheckReferencesIn(conn, tx, id);
                foreach (ReferenceBlock b in result.Blocks)
                {
                    if (b.Count > 0)
                    {
                        result.ErrorMessage = ReferenceChecker.BlockedMessage("mặt hàng", name, b.Count);
                        return;
                    }
                }
                DbHelper.Exec(conn, tx, "DELETE FROM DDINHLUONG WHERE DMATHANGID = @P", "@P", id);
                if (DbHelper.Exec(conn, tx, "DELETE FROM DMATHANG WHERE ID = @I", "@I", id) == 0)
                {
                    result.ErrorMessage = "Mặt hàng không còn tồn tại.";
                    return;
                }
                result.Ok = true;
            });
            return result;
        }

        /// <summary>
        /// Các tham chiếu đang chặn xóa (theo STABLEDESC.REFCONFIG của DMATHANG + 2 cột bổ sung ở 5.5):
        /// TBAOGIACHITIET, TDATHANGCHITIET, TDONHANGCHITIET (LOAI 0–4),
        /// TLUUTAMCHITIET, DDOTKHUYENMAICHITIET (DMATHANGID + DMATHANGTANGID), DDINHLUONG (DVATTUID).
        /// Dòng định lượng của CHÍNH mặt hàng (DDINHLUONG.DMATHANGID) KHÔNG chặn xóa: DeleteProduct
        /// xóa chúng cùng mặt hàng trong một transaction (3.6/§7: không để lại dòng định lượng mồ côi).
        /// Chỉ chặn khi mặt hàng đang là vật tư của mặt hàng khác (DDINHLUONG.DVATTUID).
        /// Trả đủ các mục (kể cả 0).
        /// </summary>
        public List<ReferenceBlock> CheckReferences(string id)
        {
            return DbHelper.Read(_databasePath, conn => CheckReferencesIn(conn, null, id));
        }

        private static List<ReferenceBlock> CheckReferencesIn(FbConnection conn, FbTransaction tx, string id)
        {
            {
                var blocks = new List<ReferenceBlock>();
                blocks.Add(CountRef(conn, tx, "TBAOGIACHITIET", "DMATHANGID", id, null));
                blocks.Add(CountRef(conn, tx, "TDATHANGCHITIET", "DMATHANGID", id, null));
                blocks.Add(CountRef(conn, tx, "TDONHANGCHITIET", "DMATHANGID", id,
                    "AND C.TDONHANGID IN (SELECT ID FROM TDONHANG WHERE LOAI IN (0,1,2,3,4))"));
                blocks.Add(CountRef(conn, tx, "TLUUTAMCHITIET", "DMATHANGID", id, null));
                blocks.Add(CountRef(conn, tx, "DDOTKHUYENMAICHITIET", "DMATHANGID", id, null));
                // Hai cột bổ sung (5.5) — app gốc chỉ liệt kê bảng, có thể không kiểm các cột này.
                blocks.Add(CountRef(conn, tx, "DDOTKHUYENMAICHITIET", "DMATHANGTANGID", id, null));
                blocks.Add(CountRef(conn, tx, "DDINHLUONG", "DVATTUID", id, null));
                return blocks;
            }
        }

        private static ReferenceBlock CountRef(FbConnection conn, FbTransaction tx, string table, string column, string id, string extra)
        {
            string sql = "SELECT COUNT(*) FROM " + table + " C WHERE C." + column + " = @I";
            if (!string.IsNullOrEmpty(extra))
                sql += " " + extra;
            int n = DbHelper.Int(DbHelper.Scalar(conn, tx, sql, "@I", id));
            return new ReferenceBlock { TableName = table, ColumnName = column, Count = n };
        }

        // ------------------------------------------------------------------ Mapping

        private static ProductRow RowFrom(DataRow r)
        {
            return new ProductRow
            {
                Id = DbHelper.Str(r["ID"]),
                Code = DbHelper.Str(r["CODE"]) ?? "",
                Name = DbHelper.Str(r["NAME"]),
                EnglishName = DbHelper.Str(r["TENTIENGANH"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]),
                BuyPrice = DbHelper.Dec(r["GIANHAP"]),
                CostPrice = DbHelper.Dec(r["GIAVON"]),
                SellPrice = DbHelper.Dec(r["GIABAN"]),
                SellPrice2 = DbHelper.Dec(r["GIABAN2"]),
                SellPrice3 = DbHelper.Dec(r["GIABAN3"]),
                SellPrice4 = DbHelper.Dec(r["GIABAN4"]),
                MinStock = DbHelper.Dec(r["TONTOITHIEU"]),
                MaxStock = DecN(r["TONTOIDA"]),
                Warranty = DbHelper.Str(r["BAOHANH"]),
                Barcode = DbHelper.Str(r["MASANCO"]),
                ManufacturerId = DbHelper.Str(r["DHANGSANXUATID"]),
                Commission = DecN(r["HOAHONG"]),
                BigUnitId = DbHelper.Str(r["DDONVITINHCHANID"]),
                BigSellPrice = DecN(r["GIABANCHAN"]),
                ConsignmentId = DbHelper.Str(r["DDOITACKYGUIID"]),
                DefaultDiscountRate = DecN(r["MACDINHGIAMGIA"]),
                DefaultDiscountAmount = DecN(r["MACDINHGIAMTIEN"]),
                GroupId = DbHelper.Str(r["DNHOMMATHANGID"]),
                UnitId = DbHelper.Str(r["DDONVITINHID"]),
                QuyDoi = DecN(r["QUYDOI"]),
                ByWeight = DbHelper.Int(r["THEOCAN"]) != 0,
                HasExpiry = IntN(r["COHANSUDUNG"]),
                Size = DbHelper.Str(r["SIZE"]),
                TypeId = DbHelper.Str(r["DLOAIMATHANGID"]) ?? "",
                TempLocked = DbHelper.Int(r["TAMKHOA"]) != 0,
                PriceByTime = DbHelper.Int(r["GIATHEOTHOIGIA"]) != 0,
                Color = IntN(r["MAUSAC"]),
                SingHours = DecN(r["GIOHATCOMBO"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = DateN(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = DateN(r["TIMEMODIFIED"]),
                GroupName = DbHelper.Str(r["GROUPNAME"]),
                TypeName = DbHelper.Str(r["TYPENAME"]),
                UnitName = DbHelper.Str(r["UNITNAME"]),
                BigUnitName = DbHelper.Str(r["BIGUNITNAME"]),
                CreatedByName = DbHelper.Str(r["CREATEDBY"]),
                ModifiedByName = DbHelper.Str(r["MODIFIEDBY"])
            };
        }

        private static MaterialInfo MaterialFrom(DataRow r)
        {
            decimal? quyDoi = DecN(r["QUYDOI"]);
            bool hasBigUnit = !string.IsNullOrEmpty(DbHelper.Str(r["DDONVITINHCHANID"]));
            return new MaterialInfo
            {
                Id = DbHelper.Str(r["ID"]),
                Name = DbHelper.Str(r["NAME"]),
                TypeId = DbHelper.Str(r["DLOAIMATHANGID"]) ?? "",
                UnitId = DbHelper.Str(r["DDONVITINHID"]),
                UnitName = DbHelper.Str(r["UNITNAME"]),
                BuyPrice = DbHelper.Dec(r["GIANHAP"]),
                CostPrice = DbHelper.Dec(r["GIAVON"]),
                QuyDoi = quyDoi,
                HasBigUnit = hasBigUnit
            };
        }

        private static decimal? DecN(object v)
        {
            return v == null || v is DBNull ? (decimal?)null : Convert.ToDecimal(v);
        }

        private static int? IntN(object v)
        {
            return v == null || v is DBNull ? (int?)null : Convert.ToInt32(v);
        }

        private static DateTime? DateN(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
