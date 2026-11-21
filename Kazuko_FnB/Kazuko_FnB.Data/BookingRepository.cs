using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc/ghi phiếu đặt hàng (TDATHANG + TDATHANGCHITIET), theo docs/specs/khach-dat-hang.md.
    /// Đoàn (§3.1): mỗi bàn một dòng TDATHANG, chung GUID; lưu/xóa đoàn trong MỘT transaction.
    /// SQL tham số hóa 100%; kiểm trùng số phiếu TRƯỚC khi gán Id mới (bài học review patch 1).
    /// </summary>
    public sealed class BookingRepository
    {
        public const string BookingFunctionId = "97487cfa-66f6-415e-9046-9a245c61f2ae";

        private readonly string _databasePath;

        public BookingRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        // ------------------------------------------------------------------ Đọc

        /// <summary>Danh sách phiếu đặt (mỗi dòng TDATHANG một hàng, §6.2 Q6), lọc theo §1.1.</summary>
        public List<BookingListRow> LoadBookings(BookingFilter filter)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                string sql =
                    "SELECT B.\"ID\", B.\"GUID\", B.\"NGAY\", B.\"NAME\", B.\"TENKHACH\", B.\"DIACHI\", B.\"DIENTHOAI\", " +
                    "B.\"EMAIL\", B.\"TONGCONG\", B.\"TIENHANG\", B.\"TIENGIAMGIA\", B.\"TILEGIAMGIA\", B.\"NOTE\", " +
                    "B.\"TUGIO\", B.\"DENGIO\", B.\"GIODAT\", B.\"DKHACHHANGID\", " +
                    "M.\"NAME\" AS METHODNAME, P.\"NAME\" AS PURPOSENAME, R.\"NAME\" AS ROOMNAME, C.\"NAME\" AS CUSTOMERNAME " +
                    "FROM \"TDATHANG\" B " +
                    "LEFT JOIN \"DPHUONGTHUCDAT\" M ON M.\"ID\" = B.\"DPHUONGTHUCDATID\" " +
                    "LEFT JOIN \"DMUCDICHDAT\" P ON P.\"ID\" = B.\"DMUCDICHDATID\" " +
                    "LEFT JOIN \"DBAN\" R ON R.\"ID\" = B.\"DBANID\" " +
                    "LEFT JOIN \"DKHACHHANG\" C ON C.\"ID\" = B.\"DKHACHHANGID\" " +
                    "WHERE B.\"STATUS\" = 30 AND B.\"NGAY\" >= @from AND B.\"NGAY\" < @toNext";
                var args = new List<object> { "@from", filter.FromDate.Date, "@toNext", filter.ToDate.Date.AddDays(1) };
                if (!string.IsNullOrWhiteSpace(filter.NumberText))
                {
                    sql += " AND B.\"NAME\" CONTAINING @num";
                    args.Add("@num"); args.Add(filter.NumberText.Trim());
                }
                if (!string.IsNullOrEmpty(filter.CustomerId))
                {
                    sql += " AND B.\"DKHACHHANGID\" = @cust";
                    args.Add("@cust"); args.Add(filter.CustomerId);
                }
                if (filter.MethodId != null)
                {
                    if (filter.MethodId.Length == 0)
                    {
                        sql += " AND (B.\"DPHUONGTHUCDATID\" IS NULL OR B.\"DPHUONGTHUCDATID\" = '')";
                    }
                    else
                    {
                        sql += " AND B.\"DPHUONGTHUCDATID\" = @method";
                        args.Add("@method"); args.Add(filter.MethodId);
                    }
                }
                sql += " ORDER BY B.\"NGAY\" DESC, B.\"NAME\"";
                DataTable t = DbHelper.Table(conn, null, sql, args.ToArray());
                var list = new List<BookingListRow>(t.Rows.Count);
                foreach (DataRow r in t.Rows)
                {
                    list.Add(new BookingListRow
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Guid = DbHelper.Str(r["GUID"]),
                        Ngay = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                        Name = DbHelper.Str(r["NAME"]),
                        TenKhach = DbHelper.Str(r["TENKHACH"]),
                        DiaChi = DbHelper.Str(r["DIACHI"]),
                        DienThoai = DbHelper.Str(r["DIENTHOAI"]),
                        Email = DbHelper.Str(r["EMAIL"]),
                        TongCong = DbHelper.Dec(r["TONGCONG"]),
                        TienHang = DbHelper.Dec(r["TIENHANG"]),
                        TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                        TileGiamGia = DbHelper.Dec(r["TILEGIAMGIA"]),
                        Note = DbHelper.Str(r["NOTE"]),
                        TuGio = r["TUGIO"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TUGIO"]),
                        DenGio = r["DENGIO"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["DENGIO"]),
                        GioDat = r["GIODAT"] is DBNull ? DateTime.Now : Convert.ToDateTime(r["GIODAT"]),
                        MethodName = DbHelper.Str(r["METHODNAME"]),
                        PurposeName = DbHelper.Str(r["PURPOSENAME"]),
                        RoomName = DbHelper.Str(r["ROOMNAME"]),
                        CustomerName = DbHelper.Str(r["CUSTOMERNAME"])
                    });
                }
                return list;
            });
        }

        /// <summary>Một phiếu đặt (đầu phiếu + các dòng cùng đoàn + chi tiết của dòng này). Null nếu không còn.</summary>
        public BookingRow LoadBooking(string bookingId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable t = DbHelper.Table(conn, null, HeaderColumns() + " FROM \"TDATHANG\" WHERE \"ID\" = @id AND \"STATUS\" = 30", "@id", bookingId);
                if (t.Rows.Count == 0)
                    return null;
                BookingRow row = ReadHeader(t.Rows[0]);
                row.GroupRows = LoadGroupRows(conn, null, row.Guid);
                row.Lines = LoadLinesCore(conn, null, bookingId);
                return row;
            });
        }

        /// <summary>Chi tiết của một dòng TDATHANG (kèm tên hàng/đơn vị để hiển thị).</summary>
        public List<BookingLine> LoadLines(string bookingId)
        {
            return DbHelper.Read(_databasePath, conn => LoadLinesCore(conn, null, bookingId));
        }

        /// <summary>Bàn để chọn vào phiếu (hộp CHỌN PHÒNG): bàn đang dùng, kèm khu vực + loại phòng.</summary>
        public List<BookingRoomItem> LoadRooms()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable t = DbHelper.Table(conn, null,
                    "SELECT B.\"ID\", B.\"NAME\", A.\"NAME\" AS AREANAME, T.\"NAME\" AS TYPENAME " +
                    "FROM \"DBAN\" B LEFT JOIN \"DKHUVUC\" A ON A.\"ID\" = B.\"DKHUVUCID\" " +
                    "LEFT JOIN \"DLOAIPHONG\" T ON T.\"ID\" = B.\"DLOAIPHONGID\" " +
                    "WHERE B.\"STATUS\" = 30 ORDER BY A.\"NAME\", B.\"NAME\"");
                var list = new List<BookingRoomItem>(t.Rows.Count);
                foreach (DataRow r in t.Rows)
                {
                    list.Add(new BookingRoomItem
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Name = DbHelper.Str(r["NAME"]),
                        AreaName = DbHelper.Str(r["AREANAME"]),
                        TypeName = DbHelper.Str(r["TYPENAME"])
                    });
                }
                return list;
            });
        }

        /// <summary>Mặt hàng để chọn vào phiếu (lưới tìm mặt hàng, §1.2). Sắp theo CODE hoặc NAME (§3.8).</summary>
        public List<BookingSaleItem> LoadSaleItems(bool sortByCode)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable t = DbHelper.Table(conn, null,
                    "SELECT P.\"ID\", P.\"CODE\", P.\"NAME\", P.\"DDONVITINHID\", U1.\"NAME\" AS UNITNAME, " +
                    "P.\"DDONVITINHCHANID\", U2.\"NAME\" AS BIGUNITNAME, P.\"QUYDOI\", " +
                    "P.\"GIABAN\", P.\"GIABAN2\", P.\"GIABAN3\", P.\"GIABAN4\", P.\"GIABANCHAN\", P.\"DLOAIMATHANGID\" " +
                    "FROM \"DMATHANG\" P " +
                    "LEFT JOIN \"DDONVITINH\" U1 ON U1.\"ID\" = P.\"DDONVITINHID\" " +
                    "LEFT JOIN \"DDONVITINH\" U2 ON U2.\"ID\" = P.\"DDONVITINHCHANID\" " +
                    "WHERE P.\"STATUS\" = 30 ORDER BY " + (sortByCode ? "P.\"CODE\"" : "P.\"NAME\""));
                var list = new List<BookingSaleItem>(t.Rows.Count);
                foreach (DataRow r in t.Rows)
                {
                    list.Add(new BookingSaleItem
                    {
                        Id = DbHelper.Str(r["ID"]),
                        Code = DbHelper.Str(r["CODE"]),
                        Name = DbHelper.Str(r["NAME"]),
                        UnitId = DbHelper.Str(r["DDONVITINHID"]),
                        UnitName = DbHelper.Str(r["UNITNAME"]),
                        BigUnitId = DbHelper.Str(r["DDONVITINHCHANID"]),
                        BigUnitName = DbHelper.Str(r["BIGUNITNAME"]),
                        QuyDoi = DbHelper.Dec(r["QUYDOI"]),
                        SellPrice = DbHelper.Dec(r["GIABAN"]),
                        SellPrice2 = DbHelper.Dec(r["GIABAN2"]),
                        SellPrice3 = DbHelper.Dec(r["GIABAN3"]),
                        SellPrice4 = DbHelper.Dec(r["GIABAN4"]),
                        BigSellPrice = DbHelper.Dec(r["GIABANCHAN"]),
                        TypeId = DbHelper.Str(r["DLOAIMATHANGID"])
                    });
                }
                return list;
            });
        }

        /// <summary>Khách hàng để điền tên/địa chỉ/điện thoại/email + loại giá khi chọn khách. Null nếu không có.</summary>
        public BookingCustomerInfo LoadCustomer(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable t = DbHelper.Table(conn, null,
                    "SELECT \"ID\", \"NAME\", \"DIACHI\", \"DIENTHOAI\", \"EMAIL\", \"GIABAN\" " +
                    "FROM \"DKHACHHANG\" WHERE \"ID\" = @id AND \"STATUS\" = 30", "@id", customerId);
                if (t.Rows.Count == 0)
                    return null;
                DataRow r = t.Rows[0];
                return new BookingCustomerInfo
                {
                    Id = DbHelper.Str(r["ID"]),
                    Name = DbHelper.Str(r["NAME"]),
                    Address = DbHelper.Str(r["DIACHI"]),
                    Phone = DbHelper.Str(r["DIENTHOAI"]),
                    Email = DbHelper.Str(r["EMAIL"]),
                    PriceType = DbHelper.Int(r["GIABAN"])
                };
            });
        }

        /// <summary>Số hóa đơn của đơn hàng trỏ tới phiếu đặt này (nhãn "Số hóa đơn", §1.2); null nếu chưa chuyển.</summary>
        public string LoadInvoiceName(string bookingId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null,
                    "SELECT FIRST 1 \"NAME\" FROM \"TDONHANG\" WHERE \"TDATHANGID\" = @id AND \"STATUS\" = 30 ORDER BY \"TIMECREATED\" DESC",
                    "@id", bookingId);
                return DbHelper.Str(v);
            });
        }

        // ------------------------------------------------------------------ Sinh số

        /// <summary>Số phiếu tiếp theo theo STABLEDESC.NOTEMPLATE ("DH(yy)/(*****)"), đọc tại thời điểm gọi.</summary>
        public string GenerateNumber()
        {
            return DbHelper.Read(_databasePath, conn => GenerateNumberCore(conn, null, null));
        }

        private string GenerateNumberCore(FbConnection conn, FbTransaction tx, HashSet<string> reserved)
        {
            object tpl = DbHelper.Scalar(conn, tx,
                "SELECT \"NOTEMPLATE\" FROM \"STABLEDESC\" WHERE \"NAME\" = 'TDATHANG'");
            string template = DbHelper.Str(tpl);
            if (string.IsNullOrWhiteSpace(template))
                throw new InvalidDataException("STABLEDESC của TDATHANG chưa có NOTEMPLATE.");
            DateTime today = DateTime.Today;
            string prefix = DocumentNumber.LiteralPrefix(template, today);
            DataTable t = DbHelper.Table(conn, tx,
                "SELECT \"NAME\" FROM \"TDATHANG\" WHERE \"NAME\" STARTING WITH @p", "@p", prefix);
            var names = new string[t.Rows.Count];
            for (int i = 0; i < t.Rows.Count; i++)
                names[i] = DbHelper.Str(t.Rows[i]["NAME"]);
            int seq = DocumentNumber.NextSequence(names, prefix);
            string candidate;
            do
            {
                candidate = DocumentNumber.Format(template, today, seq);
                seq++;
            }
            while (reserved != null && reserved.Contains(candidate));
            return candidate;
        }

        // ------------------------------------------------------------------ Lưu đoàn

        /// <summary>
        /// Lưu phiếu đặt + đoàn (§3.1) + chi tiết trong MỘT transaction (§5.1).
        /// roomIds: bàn đã chọn theo thứ tự (dòng đang sửa lấy bàn đầu nếu bàn cũ bị bỏ, BeforeSave #2).
        /// </summary>
        public BookingSaveResult SaveBooking(BookingRow row, List<BookingLine> lines, List<string> roomIds, string userId)
        {
            if (row == null || roomIds == null || roomIds.Count == 0)
                throw new ArgumentException("Phiếu đặt phải có ít nhất một bàn.");
            if (lines == null)
                lines = new List<BookingLine>();
            BookingSaveResult result = null;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                DateTime now = DbHelper.Now();
                bool isNew = string.IsNullOrEmpty(row.Id);
                string guid;
                List<GroupRowRef> existing;
                if (isNew)
                {
                    // Kiểm trùng TRƯỚC khi gán Id mới (bài học review patch 1).
                    object dup = DbHelper.Scalar(conn, tx,
                        "SELECT FIRST 1 \"ID\" FROM \"TDATHANG\" WHERE \"NAME\" = @n", "@n", row.Name);
                    if (dup != null)
                    {
                        result = BookingSaveResult.Fail(BookingText.NumberDuplicate(row.Name));
                        return;
                    }
                    row.Id = DbHelper.NewId();
                    guid = DbHelper.NewId();
                    row.Guid = guid;
                    existing = new List<GroupRowRef>();
                }
                else
                {
                    object g = DbHelper.Scalar(conn, tx,
                        "SELECT \"GUID\" FROM \"TDATHANG\" WHERE \"ID\" = @id", "@id", row.Id);
                    if (g == null)
                    {
                        result = BookingSaveResult.Fail(BookingText.NotFound);
                        return;
                    }
                    guid = DbHelper.Str(g);
                    row.Guid = guid;
                    object dup = DbHelper.Scalar(conn, tx,
                        "SELECT FIRST 1 \"ID\" FROM \"TDATHANG\" WHERE \"NAME\" = @n AND \"ID\" <> @id",
                        "@n", row.Name, "@id", row.Id);
                    if (dup != null)
                    {
                        result = BookingSaveResult.Fail(BookingText.NumberDuplicate(row.Name));
                        return;
                    }
                    existing = LoadGroupRows(conn, tx, guid);
                }

                GroupChangePlan plan = BookingRules.PlanGroupChanges(existing, row.Id, roomIds);
                var reserved = new HashSet<string>(StringComparer.Ordinal) { row.Name };
                var groupIds = new List<string>();

                // Dòng đang sửa: thêm mới hoặc cập nhật (guard UPDATE 0 dòng như ProductRepository).
                if (isNew)
                {
                    InsertRow(conn, tx, row.Id, row.Name, plan.CurrentRoomId, row, guid, userId, now);
                }
                else if (UpdateRow(conn, tx, row.Id, row.Name, plan.CurrentRoomId, row, userId, now) == 0)
                {
                    result = BookingSaveResult.Fail(BookingText.NotFound);
                    return;
                }
                groupIds.Add(row.Id);

                // Dòng giữ nguyên bàn: chép thông tin chung (giữ số phiếu + bàn cũ).
                foreach (GroupRowRef keep in plan.Keep)
                {
                    if (keep.Id == row.Id)
                        continue;
                    UpdateRow(conn, tx, keep.Id, null, null, row, userId, now);
                    groupIds.Add(keep.Id);
                }
                // Tái dùng dòng bàn bị bỏ cho bàn mới (giữ số phiếu cũ, đổi bàn).
                foreach (GroupRowRef reuse in plan.Reuse)
                {
                    UpdateRow(conn, tx, reuse.Id, null, reuse.RoomId, row, userId, now);
                    groupIds.Add(reuse.Id);
                }
                // Bàn mới chưa có dòng: tạo dòng mới, sinh số phiếu mới trong transaction.
                foreach (string roomId in plan.CreateRoomIds)
                {
                    string id = DbHelper.NewId();
                    string number = GenerateNumberCore(conn, tx, reserved);
                    reserved.Add(number);
                    InsertRow(conn, tx, id, number, roomId, row, guid, userId, now);
                    groupIds.Add(id);
                }
                // Dòng thừa không tái dùng được: xóa chi tiết rồi xóa dòng (§3.1 bước 3).
                foreach (string id in plan.DeleteIds)
                {
                    DbHelper.Exec(conn, tx, "DELETE FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" = @id", "@id", id);
                    DbHelper.Exec(conn, tx, "DELETE FROM \"TDATHANG\" WHERE \"ID\" = @id", "@id", id);
                }
                // Chép lại toàn bộ chi tiết cho từng dòng trong đoàn (§3.1 bước 4).
                foreach (string id in groupIds)
                {
                    DbHelper.Exec(conn, tx, "DELETE FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" = @id", "@id", id);
                    foreach (BookingLine line in lines)
                        InsertLine(conn, tx, id, line, userId, now);
                }

                result = new BookingSaveResult { Ok = true, SavedId = row.Id, GroupIds = groupIds };
            });
            return result ?? BookingSaveResult.Fail("Không ghi được dữ liệu.");
        }

        // ------------------------------------------------------------------ Xóa đoàn

        /// <summary>
        /// Xóa hẳn cả đoàn + chi tiết (§3.4); chặn khi bất kỳ dòng nào trong đoàn còn
        /// tham chiếu ở TDONHANG / TLUUTAM / TTHUCHI. Không thùng rác (luật chốt cả app).
        /// </summary>
        public BookingDeleteResult DeleteBooking(string bookingId)
        {
            BookingDeleteResult result = null;
            DbHelper.InTransaction(_databasePath, (conn, tx) =>
            {
                object g = DbHelper.Scalar(conn, tx,
                    "SELECT \"GUID\" FROM \"TDATHANG\" WHERE \"ID\" = @id", "@id", bookingId);
                if (g == null)
                {
                    result = new BookingDeleteResult { Ok = true };
                    return;
                }
                List<GroupRowRef> group = LoadGroupRows(conn, tx, DbHelper.Str(g));
                if (group.Count == 0)
                {
                    result = new BookingDeleteResult { Ok = true };
                    return;
                }
                string inList = InList(group.Count);
                var args = new List<object>();
                for (int i = 0; i < group.Count; i++)
                {
                    args.Add("@p" + i);
                    args.Add(group[i].Id);
                }
                string[] refTables = { "TDONHANG", "TLUUTAM", "TTHUCHI" };
                foreach (string table in refTables)
                {
                    object n = DbHelper.Scalar(conn, tx,
                        "SELECT COUNT(*) FROM \"" + table + "\" WHERE \"TDATHANGID\" IN (" + inList + ")",
                        args.ToArray());
                    if (DbHelper.Int(n) > 0)
                    {
                        result = BookingDeleteResult.Fail(BookingText.DeleteBlocked);
                        return;
                    }
                }
                DbHelper.Exec(conn, tx,
                    "DELETE FROM \"TDATHANGCHITIET\" WHERE \"TDATHANGID\" IN (" + inList + ")", args.ToArray());
                DbHelper.Exec(conn, tx,
                    "DELETE FROM \"TDATHANG\" WHERE \"ID\" IN (" + inList + ")", args.ToArray());
                result = new BookingDeleteResult { Ok = true };
            });
            return result ?? BookingDeleteResult.Fail("Không xóa được dữ liệu.");
        }

        // ------------------------------------------------------------------ Nội bộ

        private static string InList(int count)
        {
            var parts = new string[count];
            for (int i = 0; i < count; i++)
                parts[i] = "@p" + i;
            return string.Join(",", parts);
        }

        private static string HeaderColumns()
        {
            return "SELECT \"ID\", \"GUID\", \"NAME\", \"NGAY\", \"GIODAT\", \"TUGIO\", \"DENGIO\", \"TUNGAY\", \"DENNGAY\", " +
                "\"DKHACHHANGID\", \"TENKHACH\", \"DIACHI\", \"DIENTHOAI\", \"EMAIL\", \"DPHUONGTHUCDATID\", " +
                "\"DMUCDICHDATID\", \"NOTE\", \"MAUSAC\", \"TIENHANG\", \"TILEGIAMGIA\", \"TIENGIAMGIA\", \"GIAMTHEOTIEN\", " +
                "\"TILETHUE\", \"TIENTHUE\", \"PHIVANCHUYEN\", \"TONGCONG\", \"LOAI\", \"LOAIGIA\", \"STATUS\", " +
                "\"USERCREATEDID\", \"TIMECREATED\", \"USERMODIFIEDID\", \"TIMEMODIFIED\"";
        }

        private static BookingRow ReadHeader(DataRow r)
        {
            return new BookingRow
            {
                Id = DbHelper.Str(r["ID"]),
                Guid = DbHelper.Str(r["GUID"]),
                Name = DbHelper.Str(r["NAME"]),
                Ngay = r["NGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["NGAY"]),
                GioDat = r["GIODAT"] is DBNull ? DateTime.Now : Convert.ToDateTime(r["GIODAT"]),
                TuGio = r["TUGIO"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TUGIO"]),
                DenGio = r["DENGIO"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["DENGIO"]),
                TuNgay = r["TUNGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["TUNGAY"]),
                DenNgay = r["DENNGAY"] is DBNull ? DateTime.Today : Convert.ToDateTime(r["DENNGAY"]),
                CustomerId = DbHelper.Str(r["DKHACHHANGID"]),
                TenKhach = DbHelper.Str(r["TENKHACH"]),
                DiaChi = DbHelper.Str(r["DIACHI"]),
                DienThoai = DbHelper.Str(r["DIENTHOAI"]),
                Email = DbHelper.Str(r["EMAIL"]),
                MethodId = DbHelper.Str(r["DPHUONGTHUCDATID"]),
                PurposeId = DbHelper.Str(r["DMUCDICHDATID"]),
                Note = DbHelper.Str(r["NOTE"]),
                ColorArgb = DbHelper.Int(r["MAUSAC"]),
                TienHang = DbHelper.Dec(r["TIENHANG"]),
                TileGiamGia = DbHelper.Dec(r["TILEGIAMGIA"]),
                TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                GiamTheoTien = DbHelper.Int(r["GIAMTHEOTIEN"]) == CatalogRepository.ActiveStatus,
                TileThue = DbHelper.Dec(r["TILETHUE"]),
                TienThue = DbHelper.Dec(r["TIENTHUE"]),
                PhiVanChuyen = DbHelper.Dec(r["PHIVANCHUYEN"]),
                TongCong = DbHelper.Dec(r["TONGCONG"]),
                Loai = DbHelper.Int(r["LOAI"]),
                LoaiGia = DbHelper.Int(r["LOAIGIA"]),
                Status = DbHelper.Int(r["STATUS"]),
                CreatedById = DbHelper.Str(r["USERCREATEDID"]),
                CreatedAt = r["TIMECREATED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMECREATED"]),
                ModifiedById = DbHelper.Str(r["USERMODIFIEDID"]),
                ModifiedAt = r["TIMEMODIFIED"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["TIMEMODIFIED"])
            };
        }

        private static List<GroupRowRef> LoadGroupRows(FbConnection conn, FbTransaction tx, string guid)
        {
            DataTable t = DbHelper.Table(conn, tx,
                "SELECT \"ID\", \"DBANID\", \"NAME\" FROM \"TDATHANG\" WHERE \"GUID\" = @g AND \"STATUS\" = 30 ORDER BY \"NAME\"",
                "@g", guid);
            var list = new List<GroupRowRef>(t.Rows.Count);
            foreach (DataRow r in t.Rows)
            {
                list.Add(new GroupRowRef
                {
                    Id = DbHelper.Str(r["ID"]),
                    RoomId = DbHelper.Str(r["DBANID"]),
                    Name = DbHelper.Str(r["NAME"])
                });
            }
            return list;
        }

        private static List<BookingLine> LoadLinesCore(FbConnection conn, FbTransaction tx, string bookingId)
        {
            DataTable t = DbHelper.Table(conn, tx,
                "SELECT L.\"ID\", L.\"DMATHANGID\", L.\"DDONVITINHID\", L.\"SOLUONG\", L.\"DONGIA\", " +
                "L.\"TILEGIAMGIA\", L.\"THANHTIEN\", L.\"NOTE\", P.\"NAME\" AS PRODUCTNAME, U.\"NAME\" AS UNITNAME " +
                "FROM \"TDATHANGCHITIET\" L " +
                "LEFT JOIN \"DMATHANG\" P ON P.\"ID\" = L.\"DMATHANGID\" " +
                "LEFT JOIN \"DDONVITINH\" U ON U.\"ID\" = L.\"DDONVITINHID\" " +
                "WHERE L.\"TDATHANGID\" = @id AND L.\"STATUS\" = 30 ORDER BY L.\"TIMECREATED\", L.\"ID\"",
                "@id", bookingId);
            var list = new List<BookingLine>(t.Rows.Count);
            foreach (DataRow r in t.Rows)
            {
                list.Add(new BookingLine
                {
                    Id = DbHelper.Str(r["ID"]),
                    BookingId = bookingId,
                    ProductId = DbHelper.Str(r["DMATHANGID"]),
                    ProductName = DbHelper.Str(r["PRODUCTNAME"]),
                    UnitId = DbHelper.Str(r["DDONVITINHID"]),
                    UnitName = DbHelper.Str(r["UNITNAME"]),
                    Quantity = DbHelper.Dec(r["SOLUONG"]),
                    Price = DbHelper.Dec(r["DONGIA"]),
                    DiscountPct = DbHelper.Dec(r["TILEGIAMGIA"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"]),
                    Note = DbHelper.Str(r["NOTE"])
                });
            }
            return list;
        }

        private static void InsertRow(FbConnection conn, FbTransaction tx, string id, string name, string roomId,
            BookingRow row, string guid, string userId, DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO \"TDATHANG\" (\"ID\", \"GUID\", \"NAME\", \"NGAY\", \"GIODAT\", \"TUGIO\", \"DENGIO\", " +
                "\"TUNGAY\", \"DENNGAY\", \"DKHACHHANGID\", \"TENKHACH\", \"DIACHI\", \"DIENTHOAI\", \"EMAIL\", " +
                "\"DPHUONGTHUCDATID\", \"DMUCDICHDATID\", \"NOTE\", \"MAUSAC\", \"TIENHANG\", \"TILEGIAMGIA\", " +
                "\"TIENGIAMGIA\", \"GIAMTHEOTIEN\", \"TILETHUE\", \"TIENTHUE\", \"PHIVANCHUYEN\", \"TONGCONG\", " +
                "\"LOAI\", \"LOAIGIA\", \"DBANID\", \"STATUS\", \"USERCREATEDID\", \"TIMECREATED\") " +
                "VALUES (@id, @guid, @name, @ngay, @giodat, @tugio, @dengio, @tungay, @denngay, @cust, " +
                "@ten, @diachi, @dt, @email, @method, @purpose, @note, @color, @tienhang, @tilegg, @tiengg, " +
                "@giamtheotien, @tilethue, @tienthue, @phi, @tong, @loai, @loaigia, @room, 30, @user, @now)",
                "@id", id, "@guid", guid, "@name", name, "@ngay", row.Ngay, "@giodat", row.GioDat,
                "@tugio", (object)row.TuGio ?? DBNull.Value, "@dengio", (object)row.DenGio ?? DBNull.Value,
                "@tungay", row.TuNgay, "@denngay", row.DenNgay, "@cust", row.CustomerId,
                "@ten", row.TenKhach, "@diachi", row.DiaChi, "@dt", row.DienThoai, "@email", row.Email,
                "@method", row.MethodId, "@purpose", row.PurposeId, "@note", row.Note,
                "@color", row.ColorArgb, "@tienhang", row.TienHang, "@tilegg", row.TileGiamGia,
                "@tiengg", row.TienGiamGia, "@giamtheotien", row.GiamTheoTien ? 30 : 0,
                "@tilethue", row.TileThue, "@tienthue", row.TienThue, "@phi", row.PhiVanChuyen,
                "@tong", row.TongCong, "@loai", row.Loai, "@loaigia", row.LoaiGia,
                "@room", roomId, "@user", userId, "@now", now);
        }

        /// <summary>
        /// Cập nhật thông tin chung của một dòng trong đoàn. name/roomId = null thì giữ nguyên.
        /// Trả số dòng bị ảnh hưởng (0 = dòng không còn, để gọi báo NotFound như ProductRepository).
        /// </summary>
        private static int UpdateRow(FbConnection conn, FbTransaction tx, string id, string name, string roomId,
            BookingRow row, string userId, DateTime now)
        {
            string sql =
                "UPDATE \"TDATHANG\" SET \"NGAY\" = @ngay, \"GIODAT\" = @giodat, \"TUGIO\" = @tugio, \"DENGIO\" = @dengio, " +
                "\"TUNGAY\" = @tungay, \"DENNGAY\" = @denngay, \"DKHACHHANGID\" = @cust, \"TENKHACH\" = @ten, " +
                "\"DIACHI\" = @diachi, \"DIENTHOAI\" = @dt, \"EMAIL\" = @email, \"DPHUONGTHUCDATID\" = @method, " +
                "\"DMUCDICHDATID\" = @purpose, \"NOTE\" = @note, \"MAUSAC\" = @color, \"TIENHANG\" = @tienhang, " +
                "\"TILEGIAMGIA\" = @tilegg, \"TIENGIAMGIA\" = @tiengg, \"GIAMTHEOTIEN\" = @giamtheotien, " +
                "\"TILETHUE\" = @tilethue, \"TIENTHUE\" = @tienthue, \"PHIVANCHUYEN\" = @phi, \"TONGCONG\" = @tong, " +
                "\"LOAI\" = @loai, \"LOAIGIA\" = @loaigia, \"USERMODIFIEDID\" = @user, \"TIMEMODIFIED\" = @now";
            var args = new List<object>
            {
                "@ngay", row.Ngay, "@giodat", row.GioDat,
                "@tugio", (object)row.TuGio ?? DBNull.Value, "@dengio", (object)row.DenGio ?? DBNull.Value,
                "@tungay", row.TuNgay, "@denngay", row.DenNgay, "@cust", row.CustomerId,
                "@ten", row.TenKhach, "@diachi", row.DiaChi, "@dt", row.DienThoai, "@email", row.Email,
                "@method", row.MethodId, "@purpose", row.PurposeId, "@note", row.Note,
                "@color", row.ColorArgb, "@tienhang", row.TienHang, "@tilegg", row.TileGiamGia,
                "@tiengg", row.TienGiamGia, "@giamtheotien", row.GiamTheoTien ? 30 : 0,
                "@tilethue", row.TileThue, "@tienthue", row.TienThue, "@phi", row.PhiVanChuyen,
                "@tong", row.TongCong, "@loai", row.Loai, "@loaigia", row.LoaiGia,
                "@user", userId, "@now", now, "@id", id
            };
            if (name != null)
            {
                sql += ", \"NAME\" = @name";
                args.Add("@name"); args.Add(name);
            }
            if (roomId != null)
            {
                sql += ", \"DBANID\" = @room";
                args.Add("@room"); args.Add(roomId);
            }
            sql += " WHERE \"ID\" = @id";
            return DbHelper.Exec(conn, tx, sql, args.ToArray());
        }

        private static void InsertLine(FbConnection conn, FbTransaction tx, string bookingId, BookingLine line,
            string userId, DateTime now)
        {
            DbHelper.Exec(conn, tx,
                "INSERT INTO \"TDATHANGCHITIET\" (\"ID\", \"TDATHANGID\", \"DMATHANGID\", \"DDONVITINHID\", " +
                "\"SOLUONG\", \"DONGIA\", \"TILEGIAMGIA\", \"THANHTIEN\", \"NOTE\", \"STATUS\", " +
                "\"USERCREATEDID\", \"TIMECREATED\") " +
                "VALUES (@id, @booking, @product, @unit, @qty, @price, @disc, @amount, @note, 30, @user, @now)",
                "@id", DbHelper.NewId(), "@booking", bookingId, "@product", line.ProductId,
                "@unit", line.UnitId, "@qty", line.Quantity, "@price", line.Price,
                "@disc", line.DiscountPct, "@amount", line.Amount, "@note", line.Note,
                "@user", userId, "@now", now);
        }
    }
}
