using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi khu vực (DKHUVUC, docs/specs/danh-muc-ban-khu-vuc.md §1.3/2.1/3.3/3.4/3.5/3.9).
    /// Khu vực mới: STATUS = 30, DGIAMATHANGID = '0', CACHTINHGIO = theo giờ, DONGIA/TIENMOBAN = 0,
    /// PARENTID = '', PARENTDIR = '', SORTORDER = 'ZZZZ', AUTOID = MAX + 1 (DKHUVUC phẳng, TABLETYPE = 0).
    /// Khi SỬA chỉ ghi các cột có trên dialog + USERMODIFIEDID/TIMEMODIFIED — các cột cây/khung và
    /// thiết kế (GRIDSIZE, WIDTH, BACKCOLOR…) không nằm trong câu UPDATE nên giữ nguyên.
    /// Lưu khu vực + bảng giá/truy cập/mặt hàng mặc định trong MỘT transaction (5.2).
    /// Xóa là XÓA HẲN (Q3, Chiến 21/11 — bỏ thùng rác cả app, không STATUS = 0); chặn khi còn bàn (Q4).
    /// Mọi câu SQL tham số hóa; không DDL.
    /// </summary>
    public sealed class AreaRepository
    {
        private const int ActiveStatus = 30;

        /// <summary>ID của form "Hóa đơn bán hàng" — combo Mẫu hóa đơn chỉ liệt kê mẫu có SFORMID này (3.4).</summary>
        public const string BillFormId = "5f029a5b-8ec2-4229-bd1d-03fed9569db2";

        private readonly string _databasePath;

        public AreaRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        private const string SelectList =
            "ID, NAME, STEMPLATEID, DKHOHANGID, DGIAMATHANGID, DGIATHEOGIOID, TUGIO, DENGIO, " +
            "TIENMOBAN, DONGIA, DBANGGIAID, CACHTINHGIO, DBIEUTUONGID, SIMAGEID, TABHIENTHI, NOTE, STATUS, " +
            "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED";

        /// <summary>Mọi khu vực đang dùng (STATUS = 30), xếp theo tên — dữ liệu cho cây khu vực.</summary>
        public List<AreaRow> LoadAll()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectList + " FROM DKHUVUC WHERE STATUS = @S ORDER BY NAME", "@S", ActiveStatus);
                var list = new List<AreaRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một khu vực (mọi STATUS); null nếu không có.</summary>
        public AreaRow Load(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectList + " FROM DKHUVUC WHERE ID = @I", "@I", id);
                return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
            });
        }

        /// <summary>Mẫu hóa đơn để chọn (STEMPLATE có SFORMID = form "Hóa đơn bán hàng", 3.4). Mẫu đang dùng của khu vực vẫn hiện dù không khớp lọc.</summary>
        public List<LookupItem> LoadTemplates(string currentId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM STEMPLATE WHERE STATUS = @S AND (SFORMID = @F OR ID = @C) ORDER BY NAME",
                    "@S", ActiveStatus, "@F", BillFormId, "@C", currentId ?? "");
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        /// <summary>Mọi biểu tượng (DBIEUTUONG đang dùng) kèm ảnh xem trước.</summary>
        public List<AreaIconRow> LoadIcons()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, SIMAGEID FROM DBIEUTUONG WHERE STATUS = @S ORDER BY NAME", "@S", ActiveStatus);
                var list = new List<AreaIconRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new AreaIconRow
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        ImageId = DbHelper.Str(r["SIMAGEID"])
                    });
                return list;
            });
        }

        /// <summary>Ảnh của một biểu tượng: cột ANH của DBIEUTUONG, fallback sang ảnh SIMAGE trỏ tới (ptLogo, §1.3).</summary>
        public byte[] GetIconImage(string iconId)
        {
            if (string.IsNullOrEmpty(iconId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                object blob = DbHelper.Scalar(conn, null,
                    "SELECT ANH FROM DBIEUTUONG WHERE ID = @I", "@I", iconId);
                byte[] data = blob as byte[];
                if (data != null && data.Length > 0)
                    return data;
                object imageId = DbHelper.Scalar(conn, null,
                    "SELECT SIMAGEID FROM DBIEUTUONG WHERE ID = @I", "@I", iconId);
                string simageId = DbHelper.Str(imageId);
                if (string.IsNullOrEmpty(simageId))
                    return null;
                return DbHelper.Scalar(conn, null,
                    "SELECT IMAGE FROM SIMAGE WHERE ID = @I", "@I", simageId) as byte[];
            });
        }

        /// <summary>Người dùng không phải admin (ISADMIN = 1 là admin — theo LoginService/UserAdminRepository của clone) để lập trang "Truy cập".</summary>
        public List<LookupItem> LoadUsersForAccess()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM SUSER WHERE STATUS = @S AND COALESCE(ISADMIN, 0) <> 1 ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        /// <summary>ID người dùng đang được cấp quyền truy cập khu vực (DTKTHEOKHUVUC).</summary>
        public List<string> LoadAccessUserIds(string areaId)
        {
            if (string.IsNullOrEmpty(areaId))
                return new List<string>();
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT SUSERID FROM DTKTHEOKHUVUC WHERE DKHUVUCID = @A AND STATUS = @S",
                    "@A", areaId, "@S", ActiveStatus);
                var list = new List<string>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    string id = DbHelper.Str(r["SUSERID"]);
                    if (!string.IsNullOrEmpty(id))
                        list.Add(id);
                }
                return list;
            });
        }

        /// <summary>Bảng giá đang áp dụng cho khu vực (DBANGGIATHEOKHUVUC, trang "Bảng giá áp dụng" §1.3).</summary>
        public List<PriceAssignment> LoadPriceAssignments(string areaId)
        {
            if (string.IsNullOrEmpty(areaId))
                return new List<PriceAssignment>();
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT B.DBANGGIAID, P.NAME FROM DBANGGIATHEOKHUVUC B " +
                    "LEFT JOIN DBANGGIA P ON P.ID = B.DBANGGIAID " +
                    "WHERE B.DKHUVUCID = @A AND B.STATUS = @S ORDER BY P.NAME",
                    "@A", areaId, "@S", ActiveStatus);
                var list = new List<PriceAssignment>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new PriceAssignment
                    {
                        PriceListId = DbHelper.Str(r["DBANGGIAID"]),
                        PriceListName = DbHelper.Str(r["NAME"]) ?? ""
                    });
                return list;
            });
        }

        /// <summary>Mặt hàng mặc định của khu vực (DMATHANGMACDINH, trang "Mặt hàng mặc định" §1.3).</summary>
        public List<AreaDefaultItem> LoadDefaultItems(string areaId)
        {
            if (string.IsNullOrEmpty(areaId))
                return new List<AreaDefaultItem>();
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT M.DMATHANGID, M.SOLUONG, P.NAME FROM DMATHANGMACDINH M " +
                    "LEFT JOIN DMATHANG P ON P.ID = M.DMATHANGID " +
                    "WHERE M.DKHUVUCID = @A AND M.STATUS = @S ORDER BY P.NAME",
                    "@A", areaId, "@S", ActiveStatus);
                var list = new List<AreaDefaultItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new AreaDefaultItem
                    {
                        ProductId = DbHelper.Str(r["DMATHANGID"]),
                        ProductName = DbHelper.Str(r["NAME"]) ?? "",
                        Quantity = DbHelper.Dec(r["SOLUONG"])
                    });
                return list;
            });
        }

        /// <summary>Tên khu vực đã tồn tại chưa (không phân biệt hoa/thường, cắt khoảng trắng, trùng toàn bảng — Q5, đã xác minh ở ảnh 06)?</summary>
        public bool IsNameTaken(string name, string excludeId)
        {
            string norm = ProductText.NormalizeName(name);
            if (norm.Length == 0)
                return false;
            return DbHelper.Read(_databasePath, conn => IsNameTakenIn(conn, null, norm, excludeId));
        }

        /// <summary>Số bàn đang dùng thuộc khu vực (để chặn xóa khu vực còn bàn, Q4).</summary>
        public int CountRooms(string areaId)
        {
            if (string.IsNullOrEmpty(areaId))
                return 0;
            return DbHelper.Read(_databasePath, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DBAN WHERE DKHUVUCID = @A AND STATUS = @S",
                    "@A", areaId, "@S", ActiveStatus)));
        }

        /// <summary>
        /// Lưu khu vực (thêm/sửa) trong MỘT transaction gồm cả bảng giá, truy cập và mặt hàng mặc định (5.2);
        /// kiểm trùng tên trong cùng transaction. Trả ID đã lưu; lỗi thì trả ErrorMessage (null khi lưu được).
        /// </summary>
        public AreaSaveResult Save(AreaRow row, List<PriceAssignment> prices, List<string> accessUserIds,
            List<AreaDefaultItem> defaultItems, string actorId)
        {
            var result = new AreaSaveResult();
            if (row == null)
            {
                result.ErrorMessage = "Không có dữ liệu khu vực.";
                return result;
            }
            if (prices == null)
                prices = new List<PriceAssignment>();
            if (accessUserIds == null)
                accessUserIds = new List<string>();
            if (defaultItems == null)
                defaultItems = new List<AreaDefaultItem>();

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
                    result.ErrorMessage = RoomText.AreaNameEmpty;
                    return;
                }
                string excludeId = isNew ? "" : row.Id;
                if (IsNameTakenIn(conn, tx, name, excludeId))
                {
                    result.ErrorMessage = RoomText.AreaNameDuplicate(name);
                    return;
                }

                // Kiểm pass → mới gán Id/dấu vết.
                row.Name = name;
                DateTime now = DbHelper.Now();
                if (isNew)
                {
                    row.Id = DbHelper.NewId();
                    row.Status = ActiveStatus;
                    row.CreatedById = actorId;
                    row.CreatedAt = now;
                    row.ModifiedById = null;
                    row.ModifiedAt = null;
                    int autoId = NextAutoId(conn, tx);
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DKHUVUC (ID, NAME, STEMPLATEID, DKHOHANGID, DGIAMATHANGID, DGIATHEOGIOID, TUGIO, DENGIO, " +
                        "TIENMOBAN, DONGIA, DBANGGIAID, CACHTINHGIO, DBIEUTUONGID, SIMAGEID, TABHIENTHI, NOTE, STATUS, " +
                        "PARENTID, PARENTDIR, ITEMTYPE, AUTOID, SORTORDER, " +
                        "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                        "VALUES (@ID, @NAME, @STEMPLATEID, @DKHOHANGID, @DGIAMATHANGID, @DGIATHEOGIOID, @TUGIO, @DENGIO, " +
                        "@TIENMOBAN, @DONGIA, @DBANGGIAID, @CACHTINHGIO, @DBIEUTUONGID, @SIMAGEID, @TABHIENTHI, @NOTE, @STATUS, " +
                        "'', '', NULL, @AUTOID, 'ZZZZ', " +
                        "@USERCREATEDID, @TIMECREATED, NULL, NULL)",
                        AreaParams(row, autoId));
                }
                else
                {
                    row.ModifiedById = actorId;
                    row.ModifiedAt = now;
                    int updated = DbHelper.Exec(conn, tx,
                        "UPDATE DKHUVUC SET NAME = @NAME, STEMPLATEID = @STEMPLATEID, DKHOHANGID = @DKHOHANGID, " +
                        "DGIAMATHANGID = @DGIAMATHANGID, DGIATHEOGIOID = @DGIATHEOGIOID, TUGIO = @TUGIO, DENGIO = @DENGIO, " +
                        "TIENMOBAN = @TIENMOBAN, DONGIA = @DONGIA, DBANGGIAID = @DBANGGIAID, CACHTINHGIO = @CACHTINHGIO, " +
                        "DBIEUTUONGID = @DBIEUTUONGID, SIMAGEID = @SIMAGEID, TABHIENTHI = @TABHIENTHI, NOTE = @NOTE, " +
                        "USERMODIFIEDID = @USERMODIFIEDID, TIMEMODIFIED = @TIMEMODIFIED WHERE ID = @ID",
                        AreaParams(row, 0));
                    if (updated == 0)
                    {
                        // Dòng đã bị xóa (hoặc Id không tồn tại): không ghi tiếp các bảng con, không báo thành công.
                        result.ErrorMessage = RoomText.AreaGone;
                        return;
                    }
                }

                // Ba bảng con: xóa hết dòng cũ rồi ghi lại (cùng transaction, 5.2).
                DbHelper.Exec(conn, tx, "DELETE FROM DBANGGIATHEOKHUVUC WHERE DKHUVUCID = @A", "@A", row.Id);
                foreach (PriceAssignment p in prices)
                {
                    if (string.IsNullOrEmpty(p.PriceListId))
                        continue;
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DBANGGIATHEOKHUVUC (ID, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED, DKHUVUCID, DBANGGIAID) " +
                        "VALUES (@I, NULL, @S, @U, @T, NULL, NULL, @A, @P)",
                        "@I", DbHelper.NewId(), "@S", ActiveStatus, "@U", actorId, "@T", now,
                        "@A", row.Id, "@P", p.PriceListId);
                }

                DbHelper.Exec(conn, tx, "DELETE FROM DTKTHEOKHUVUC WHERE DKHUVUCID = @A", "@A", row.Id);
                foreach (string userId in accessUserIds)
                {
                    if (string.IsNullOrEmpty(userId))
                        continue;
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DTKTHEOKHUVUC (ID, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED, DKHUVUCID, SUSERID) " +
                        "VALUES (@I, NULL, @S, @U, @T, NULL, NULL, @A, @U2)",
                        "@I", DbHelper.NewId(), "@S", ActiveStatus, "@U", actorId, "@T", now,
                        "@A", row.Id, "@U2", userId);
                }

                DbHelper.Exec(conn, tx, "DELETE FROM DMATHANGMACDINH WHERE DKHUVUCID = @A", "@A", row.Id);
                foreach (AreaDefaultItem item in defaultItems)
                {
                    if (string.IsNullOrEmpty(item.ProductId) || item.Quantity <= 0m)
                        continue;
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DMATHANGMACDINH (ID, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED, DKHUVUCID, DMATHANGID, SOLUONG) " +
                        "VALUES (@I, NULL, @S, @U, @T, NULL, NULL, @A, @P, @Q)",
                        "@I", DbHelper.NewId(), "@S", ActiveStatus, "@U", actorId, "@T", now,
                        "@A", row.Id, "@P", item.ProductId, "@Q", item.Quantity);
                }

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
        private static void ResetNew(AreaRow row, string oldName)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
            row.Name = oldName;
        }

        /// <summary>
        /// Xóa HẲN khu vực. Bị chặn khi còn bàn đang dùng (Q4). Các dòng bảng giá/truy cập/mặt hàng mặc định
        /// gắn kèm bị xóa theo trong cùng transaction. Trả null khi xóa được, ngược lại trả thông báo chặn.
        /// </summary>
        public string Delete(string areaId)
        {
            if (string.IsNullOrEmpty(areaId))
                return "Không có khu vực để xóa.";
            AreaRow row = Load(areaId);
            if (row == null)
                return RoomText.AreaGone;
            int rooms = CountRooms(areaId);
            if (rooms > 0)
                return ReferenceChecker.BlockedMessage("khu vực", row.Name, rooms);
            string error = null;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                // Kiểm lại trong transaction (không để bàn mới chen vào giữa lúc kiểm và lúc xóa).
                int again = DbHelper.Int(DbHelper.Scalar(conn, tx,
                    "SELECT COUNT(*) FROM DBAN WHERE DKHUVUCID = @A AND STATUS = @S",
                    "@A", areaId, "@S", ActiveStatus));
                if (again > 0)
                {
                    error = ReferenceChecker.BlockedMessage("khu vực", row.Name, again);
                    return;
                }
                DbHelper.Exec(conn, tx, "DELETE FROM DBANGGIATHEOKHUVUC WHERE DKHUVUCID = @A", "@A", areaId);
                DbHelper.Exec(conn, tx, "DELETE FROM DTKTHEOKHUVUC WHERE DKHUVUCID = @A", "@A", areaId);
                DbHelper.Exec(conn, tx, "DELETE FROM DMATHANGMACDINH WHERE DKHUVUCID = @A", "@A", areaId);
                DbHelper.Exec(conn, tx, "DELETE FROM DKHUVUC WHERE ID = @I", "@I", areaId);
            });
            return error;
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DKHUVUC");
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

        private static int NextAutoId(FbConnection conn, FbTransaction tx)
        {
            object v = DbHelper.Scalar(conn, tx, "SELECT MAX(AUTOID) FROM DKHUVUC");
            return v == null ? 1 : Convert.ToInt32(v) + 1;
        }

        private static object[] AreaParams(AreaRow row, int autoId)
        {
            return new object[]
            {
                "@ID", row.Id,
                "@NAME", row.Name,
                "@STEMPLATEID", string.IsNullOrEmpty(row.TemplateId) ? null : row.TemplateId,
                "@DKHOHANGID", string.IsNullOrEmpty(row.WarehouseId) ? null : row.WarehouseId,
                "@DGIAMATHANGID", string.IsNullOrEmpty(row.PriceLevelId) ? "0" : row.PriceLevelId,
                "@DGIATHEOGIOID", string.IsNullOrEmpty(row.HourlyPriceId) ? null : row.HourlyPriceId,
                "@TUGIO", row.FromHour,
                "@DENGIO", row.ToHour,
                "@TIENMOBAN", row.OpenAmount,
                "@DONGIA", row.UnitPrice,
                "@DBANGGIAID", string.IsNullOrEmpty(row.PriceListId) ? null : row.PriceListId,
                "@CACHTINHGIO", row.CalcHour,
                "@DBIEUTUONGID", string.IsNullOrEmpty(row.IconId) ? null : row.IconId,
                "@SIMAGEID", string.IsNullOrEmpty(row.ImageId) ? null : row.ImageId,
                "@TABHIENTHI", row.ShowTab,
                "@NOTE", row.Note,
                "@STATUS", row.Status,
                "@AUTOID", autoId,
                "@USERCREATEDID", row.CreatedById,
                "@TIMECREATED", row.CreatedAt,
                "@USERMODIFIEDID", row.ModifiedById,
                "@TIMEMODIFIED", row.ModifiedAt
            };
        }

        private static AreaRow RowFrom(DataRow r)
        {
            return new AreaRow
            {
                Id = DbHelper.Str(r["ID"]),
                Name = DbHelper.Str(r["NAME"]) ?? "",
                TemplateId = DbHelper.Str(r["STEMPLATEID"]),
                WarehouseId = DbHelper.Str(r["DKHOHANGID"]),
                PriceLevelId = DbHelper.Str(r["DGIAMATHANGID"]) ?? "0",
                HourlyPriceId = DbHelper.Str(r["DGIATHEOGIOID"]),
                FromHour = DateN(r["TUGIO"]),
                ToHour = DateN(r["DENGIO"]),
                OpenAmount = DbHelper.Dec(r["TIENMOBAN"]),
                UnitPrice = DbHelper.Dec(r["DONGIA"]),
                PriceListId = DbHelper.Str(r["DBANGGIAID"]),
                CalcHour = DbHelper.Int(r["CACHTINHGIO"]),
                IconId = DbHelper.Str(r["DBIEUTUONGID"]),
                ImageId = DbHelper.Str(r["SIMAGEID"]),
                ShowTab = DbHelper.Int(r["TABHIENTHI"]),
                Note = DbHelper.Str(r["NOTE"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = DateN(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = DateN(r["TIMEMODIFIED"])
            };
        }

        private static DateTime? DateN(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
