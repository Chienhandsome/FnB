using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi bàn (DBAN, docs/specs/danh-muc-ban-khu-vuc.md §1.2/2.1/3.1/3.9/3.11).
    /// Bàn mới: STATUS = 30, CACHTINHGIO = theo khu vực, DONGIA/TIENMOBAN = 0.
    /// Khi SỬA chỉ ghi các cột có trên dialog + USERMODIFIEDID/TIMEMODIFIED — các cột thiết kế
    /// (POSLEFT, WIDTH…) và runtime (TRANGTHAI, TDONHANGID…) không nằm trong câu UPDATE nên giữ nguyên.
    /// Xóa là XÓA HẲN (Q3, Chiến 21/11 — bỏ thùng rác cả app, không STATUS = 0).
    /// Mọi câu SQL tham số hóa; không DDL.
    /// </summary>
    public sealed class RoomRepository
    {
        private const int ActiveStatus = 30;

        private readonly string _databasePath;

        public RoomRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        private const string SelectList =
            "R.ID, R.NAME, R.DKHUVUCID, R.DBANGGIAID, R.DONGIA, R.CACHTINHGIO, R.DNHOMHIENTHIID, " +
            "R.TIENMOBAN, R.DLOAIPHONGID, R.NOTE, R.IPCONGTAC, R.STATUS, " +
            "R.USERCREATEDID, R.TIMECREATED, R.USERMODIFIEDID, R.TIMEMODIFIED, " +
            "A.NAME AS AREANAME, G.NAME AS GROUPNAME, T.NAME AS TYPENAME, P.NAME AS PRICENAME";

        private const string SelectFrom =
            "FROM DBAN R " +
            "LEFT JOIN DKHUVUC A ON A.ID = R.DKHUVUCID " +
            "LEFT JOIN DNHOMHIENTHI G ON G.ID = R.DNHOMHIENTHIID " +
            "LEFT JOIN DLOAIPHONG T ON T.ID = R.DLOAIPHONGID " +
            "LEFT JOIN DBANGGIA P ON P.ID = R.DBANGGIAID ";

        /// <summary>Mọi bàn đang dùng (STATUS = 30), xếp theo tên — dữ liệu cho lưới bàn.</summary>
        public List<RoomRow> LoadRooms()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectList + " " + SelectFrom + "WHERE R.STATUS = @S ORDER BY R.NAME",
                    "@S", ActiveStatus);
                var list = new List<RoomRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(RowFrom(r));
                return list;
            });
        }

        /// <summary>Một bàn (mọi STATUS); null nếu không có.</summary>
        public RoomRow LoadRoom(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT " + SelectList + " " + SelectFrom + "WHERE R.ID = @I", "@I", id);
                return dt.Rows.Count == 0 ? null : RowFrom(dt.Rows[0]);
            });
        }

        /// <summary>Bảng giá đang áp dụng cho bàn (DBANGGIATHEOBAN, trang "Bảng giá áp dụng" §1.2).</summary>
        public List<PriceAssignment> LoadPriceAssignments(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
                return new List<PriceAssignment>();
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT B.DBANGGIAID, P.NAME FROM DBANGGIATHEOBAN B " +
                    "LEFT JOIN DBANGGIA P ON P.ID = B.DBANGGIAID " +
                    "WHERE B.DBANID = @R AND B.STATUS = @S ORDER BY P.NAME",
                    "@R", roomId, "@S", ActiveStatus);
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

        /// <summary>Tên bàn đã tồn tại chưa (không phân biệt hoa/thường, cắt khoảng trắng, trùng toàn bảng — Q5)?</summary>
        public bool IsNameTaken(string name, string excludeId)
        {
            string norm = ProductText.NormalizeName(name);
            if (norm.Length == 0)
                return false;
            return DbHelper.Read(_databasePath, conn => IsNameTakenIn(conn, null, norm, excludeId));
        }

        /// <summary>
        /// Lưu bàn (thêm/sửa) trong một transaction; kiểm trùng tên trong cùng transaction.
        /// Bảng giá áp dụng ghi cùng transaction (5.2). Trả kết quả kèm ID đã lưu.
        /// </summary>
        public RoomSaveResult SaveRoom(RoomRow row, List<PriceAssignment> prices, string actorId)
        {
            var result = new RoomSaveResult();
            if (row == null)
            {
                result.ErrorMessage = "Không có dữ liệu bàn.";
                return result;
            }
            if (prices == null)
                prices = new List<PriceAssignment>();

            // Trạng thái bản ghi trước khi lưu: nếu lỗi/ném ngoại lệ thì trả lại y nguyên để lần lưu sau
            // vẫn được coi là THÊM MỚI (không để lại Id "ma" làm repo tưởng là sửa) — mẫu SaveProduct.
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
                    result.ErrorMessage = RoomText.RoomNameEmpty;
                    return;
                }
                if (string.IsNullOrEmpty(row.AreaId))
                {
                    result.ErrorMessage = RoomText.RoomAreaEmpty;
                    return;
                }
                string excludeId = isNew ? "" : row.Id;
                if (IsNameTakenIn(conn, tx, name, excludeId))
                {
                    result.ErrorMessage = RoomText.RoomNameDuplicate(name);
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
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DBAN (ID, NAME, DKHUVUCID, DBANGGIAID, DONGIA, CACHTINHGIO, DNHOMHIENTHIID, " +
                        "TIENMOBAN, DLOAIPHONGID, NOTE, IPCONGTAC, STATUS, " +
                        "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                        "VALUES (@ID, @NAME, @DKHUVUCID, @DBANGGIAID, @DONGIA, @CACHTINHGIO, @DNHOMHIENTHIID, " +
                        "@TIENMOBAN, @DLOAIPHONGID, @NOTE, @IPCONGTAC, @STATUS, " +
                        "@USERCREATEDID, @TIMECREATED, NULL, NULL)",
                        RoomParams(row));
                }
                else
                {
                    row.ModifiedById = actorId;
                    row.ModifiedAt = DbHelper.Now();
                    int updated = DbHelper.Exec(conn, tx,
                        "UPDATE DBAN SET NAME = @NAME, DKHUVUCID = @DKHUVUCID, DBANGGIAID = @DBANGGIAID, " +
                        "DONGIA = @DONGIA, CACHTINHGIO = @CACHTINHGIO, DNHOMHIENTHIID = @DNHOMHIENTHIID, " +
                        "TIENMOBAN = @TIENMOBAN, DLOAIPHONGID = @DLOAIPHONGID, NOTE = @NOTE, IPCONGTAC = @IPCONGTAC, " +
                        "USERMODIFIEDID = @USERMODIFIEDID, TIMEMODIFIED = @TIMEMODIFIED WHERE ID = @ID",
                        RoomParams(row));
                    if (updated == 0)
                    {
                        // Dòng đã bị xóa (hoặc Id không tồn tại): không ghi tiếp bảng giá, không báo thành công.
                        result.ErrorMessage = RoomText.RoomGone;
                        return;
                    }
                }

                // Bảng giá áp dụng: xóa hết dòng cũ rồi ghi lại (cùng transaction, 5.2).
                DbHelper.Exec(conn, tx, "DELETE FROM DBANGGIATHEOBAN WHERE DBANID = @R", "@R", row.Id);
                foreach (PriceAssignment p in prices)
                {
                    if (string.IsNullOrEmpty(p.PriceListId))
                        continue;
                    DbHelper.Exec(conn, tx,
                        "INSERT INTO DBANGGIATHEOBAN (ID, NOTE, STATUS, USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED, DBANID, DBANGGIAID) " +
                        "VALUES (@I, NULL, @S, @U, @T, NULL, NULL, @R, @P)",
                        "@I", DbHelper.NewId(), "@S", ActiveStatus, "@U", actorId, "@T", DbHelper.Now(),
                        "@R", row.Id, "@P", p.PriceListId);
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
        private static void ResetNew(RoomRow row, string oldName)
        {
            row.Id = null;
            row.CreatedById = null;
            row.CreatedAt = null;
            row.Name = oldName;
        }

        private static bool IsNameTakenIn(FbConnection conn, FbTransaction tx, string name, string excludeId)
        {
            DataTable dt = DbHelper.Table(conn, tx, "SELECT ID, NAME FROM DBAN");
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

        private static object[] RoomParams(RoomRow row)
        {
            return new object[]
            {
                "@ID", row.Id,
                "@NAME", row.Name,
                "@DKHUVUCID", string.IsNullOrEmpty(row.AreaId) ? null : row.AreaId,
                "@DBANGGIAID", string.IsNullOrEmpty(row.PriceListId) ? null : row.PriceListId,
                "@DONGIA", row.UnitPrice,
                "@CACHTINHGIO", row.CalcHour,
                "@DNHOMHIENTHIID", string.IsNullOrEmpty(row.DisplayGroupId) ? null : row.DisplayGroupId,
                "@TIENMOBAN", row.OpenAmount,
                "@DLOAIPHONGID", string.IsNullOrEmpty(row.RoomTypeId) ? null : row.RoomTypeId,
                "@NOTE", row.Note,
                "@IPCONGTAC", string.IsNullOrEmpty(row.SwitchIp) ? null : row.SwitchIp,
                "@STATUS", row.Status,
                "@USERCREATEDID", row.CreatedById,
                "@TIMECREATED", row.CreatedAt,
                "@USERMODIFIEDID", row.ModifiedById,
                "@TIMEMODIFIED", row.ModifiedAt
            };
        }

        // ------------------------------------------------------------------ Xóa (Q3/Q4)

        /// <summary>
        /// Xóa HẲN bàn (không thùng rác, Q3): kiểm tham chiếu trước, rồi xóa bảng giá gắn kèm + chính bàn,
        /// tất cả trong CÙNG một giao dịch (không để tham chiếu mới chen vào giữa hai bước).
        /// </summary>
        public RoomDeleteResult DeleteRoom(string id)
        {
            var result = new RoomDeleteResult { Blocks = new List<ReferenceBlock>() };
            if (string.IsNullOrEmpty(id))
            {
                result.ErrorMessage = "Không có bàn để xóa.";
                return result;
            }

            string name = DbHelper.Read(_databasePath, conn =>
                DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT NAME FROM DBAN WHERE ID = @I", "@I", id)));
            if (name == null)
            {
                result.ErrorMessage = RoomText.RoomGone;
                return result;
            }

            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                result.Blocks = CheckRoomReferencesIn(conn, tx, id);
                foreach (ReferenceBlock b in result.Blocks)
                {
                    if (b.Count > 0)
                    {
                        result.ErrorMessage = ReferenceChecker.BlockedMessage("bàn", name, b.Count);
                        return;
                    }
                }
                DbHelper.Exec(conn, tx, "DELETE FROM DBANGGIATHEOBAN WHERE DBANID = @R", "@R", id);
                DbHelper.Exec(conn, tx, "DELETE FROM DBAN WHERE ID = @I", "@I", id);
                result.Ok = true;
            });
            return result;
        }

        /// <summary>
        /// Các tham chiếu đang chặn xóa bàn (Q4, mặc định tạm):
        /// đơn chưa thanh toán (TDONHANG.DATHANHTOAN = 0) trỏ tới bàn, hoặc bàn đang có TDONHANGID
        /// (đơn đang mở — trigger TDONHANG_TRIGGER giữ cột này đồng bộ với đơn chưa thanh toán).
        /// Trả đủ các mục (kể cả 0). Đơn đã thanh toán không chặn.
        /// </summary>
        public List<ReferenceBlock> CheckRoomReferences(string id)
        {
            return DbHelper.Read(_databasePath, conn => CheckRoomReferencesIn(conn, null, id));
        }

        private static List<ReferenceBlock> CheckRoomReferencesIn(FbConnection conn, FbTransaction tx, string id)
        {
            var blocks = new List<ReferenceBlock>();
            int unpaid = DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COUNT(*) FROM TDONHANG WHERE DBANID = @R AND COALESCE(DATHANHTOAN, 0) = 0",
                "@R", id));
            blocks.Add(new ReferenceBlock { TableName = "TDONHANG", ColumnName = "DBANID", Count = unpaid });
            object current = DbHelper.Scalar(conn, tx,
                "SELECT TDONHANGID FROM DBAN WHERE ID = @R", "@R", id);
            string currentId = DbHelper.Str(current);
            blocks.Add(new ReferenceBlock
            {
                TableName = "DBAN",
                ColumnName = "TDONHANGID",
                Count = string.IsNullOrEmpty(currentId) ? 0 : 1
            });
            return blocks;
        }

        // ------------------------------------------------------------------ Thêm nhanh (2.2/3.11)

        /// <summary>
        /// "Thêm nhanh bàn": kiểm tra TOÀN BỘ tên (trong DB và trong chính lần sinh) trước khi ghi dòng đầu
        /// (spec 2.2/3.11), rồi ghi mọi bàn của mọi khu vực trong MỘT transaction.
        /// Bàn mới: CACHTINHGIO = theo khu vực, DONGIA = 0, TIENMOBAN = 0, STATUS = 30.
        /// </summary>
        public QuickAddResult QuickAddRooms(List<QuickAddRequest> requests, string actorId)
        {
            var result = new QuickAddResult();
            if (requests == null || requests.Count == 0)
            {
                result.ErrorMessage = "Không có khu vực nào để thêm bàn.";
                return result;
            }
            int total = 0;
            foreach (QuickAddRequest req in requests)
                if (req != null && req.Names != null)
                    total += req.Names.Count;
            if (total == 0)
            {
                result.ErrorMessage = "Không có bàn nào để thêm.";
                return result;
            }

            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                // Kiểm toàn bộ trước khi ghi: trùng trong DB → lỗi "đã tồn tại trong dữ liệu";
                // trùng trong lần sinh → lỗi "bị trùng lặp". Dừng ở tên lỗi đầu, không ghi gì.
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (QuickAddRequest req in requests)
                {
                    if (req == null || req.Names == null)
                        continue;
                    foreach (string raw in req.Names)
                    {
                        string name = ProductText.NormalizeName(raw);
                        if (name.Length == 0)
                            continue;
                        if (!seen.Add(name))
                        {
                            result.ErrorMessage = RoomText.QuickAddDuplicate(name);
                            return;
                        }
                        if (IsNameTakenIn(conn, tx, name, ""))
                        {
                            result.ErrorMessage = RoomText.QuickAddExists(name);
                            return;
                        }
                    }
                }

                int created = 0;
                DateTime now = DbHelper.Now();
                foreach (QuickAddRequest req in requests)
                {
                    if (req == null || req.Names == null || string.IsNullOrEmpty(req.AreaId))
                        continue;
                    foreach (string raw in req.Names)
                    {
                        string name = ProductText.NormalizeName(raw);
                        if (name.Length == 0)
                            continue;
                        DbHelper.Exec(conn, tx,
                            "INSERT INTO DBAN (ID, NAME, DKHUVUCID, DBANGGIAID, DONGIA, CACHTINHGIO, DNHOMHIENTHIID, " +
                            "TIENMOBAN, DLOAIPHONGID, NOTE, IPCONGTAC, STATUS, " +
                            "USERCREATEDID, TIMECREATED, USERMODIFIEDID, TIMEMODIFIED) " +
                            "VALUES (@ID, @NAME, @DKHUVUCID, NULL, 0, @CACHTINHGIO, NULL, 0, NULL, NULL, NULL, @STATUS, " +
                            "@USERCREATEDID, @TIMECREATED, NULL, NULL)",
                            "@ID", DbHelper.NewId(), "@NAME", name, "@DKHUVUCID", req.AreaId,
                            "@CACHTINHGIO", RoomCalc.ByArea, "@STATUS", ActiveStatus,
                            "@USERCREATEDID", actorId, "@TIMECREATED", now);
                        created++;
                    }
                }
                result.Ok = true;
                result.CreatedCount = created;
            });
            return result;
        }

        // ------------------------------------------------------------------ Mapping

        private static RoomRow RowFrom(DataRow r)
        {
            return new RoomRow
            {
                Id = DbHelper.Str(r["ID"]),
                Name = DbHelper.Str(r["NAME"]) ?? "",
                AreaId = DbHelper.Str(r["DKHUVUCID"]),
                PriceListId = DbHelper.Str(r["DBANGGIAID"]),
                UnitPrice = DbHelper.Dec(r["DONGIA"]),
                CalcHour = DbHelper.Int(r["CACHTINHGIO"]),
                DisplayGroupId = DbHelper.Str(r["DNHOMHIENTHIID"]),
                OpenAmount = DbHelper.Dec(r["TIENMOBAN"]),
                RoomTypeId = DbHelper.Str(r["DLOAIPHONGID"]),
                Note = DbHelper.Str(r["NOTE"]),
                SwitchIp = DbHelper.Str(r["IPCONGTAC"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = DateN(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = DateN(r["TIMEMODIFIED"]),
                AreaName = DbHelper.Str(r["AREANAME"]) ?? "",
                DisplayGroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                RoomTypeName = DbHelper.Str(r["TYPENAME"]) ?? "",
                PriceListName = DbHelper.Str(r["PRICENAME"]) ?? ""
            };
        }

        private static DateTime? DateN(object v)
        {
            return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
