using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn đọc dữ liệu cho màn "Sử dụng dịch vụ" (docs/specs/su-dung-dich-vu.md §3.1–§3.5, §6.5, §6.6).
    /// Phần ghi và kiểm tồn/khóa bàn đặt ở ServiceOrderRepository.Write.cs. Mọi SQL tham số hóa, không DDL.
    /// </summary>
    public sealed partial class ServiceOrderRepository
    {
        private const int ActiveStatus = 30;
        private readonly string _databasePath;

        public ServiceOrderRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public DateTime GetServerTimestamp()
        {
            return DbHelper.Read(_databasePath, conn => InvoiceNumberRepository.GetServerTimestamp(conn, null));
        }

        /// <summary>
        /// Khu vực đang dùng (DKHUVUC.STATUS = 30), sắp theo SORTORDER, NAME.
        /// Nếu bật PhanQuyenTruyCapTheoKhuVuc và user không phải admin -> chỉ lấy khu vực trong DTKTHEOKHUVUC.
        /// </summary>
        public List<ServiceAreaInfo> LoadAreas(string userId, bool isAdmin, bool restrictByArea)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt;
                if (restrictByArea && !isAdmin)
                {
                    if (string.IsNullOrEmpty(userId))
                        return new List<ServiceAreaInfo>();
                    dt = DbHelper.Table(conn, null,
                        "SELECT K.ID, K.NAME, K.DKHOHANGID, K.DGIAMATHANGID, K.DBIEUTUONGID, " +
                        "COALESCE(K.TABHIENTHI, 0) AS TABHIENTHI, COALESCE(K.SORTORDER, '') AS SORTORDER " +
                        "FROM DKHUVUC K " +
                        "WHERE K.STATUS = @S AND EXISTS (" +
                        "SELECT 1 FROM DTKTHEOKHUVUC T WHERE T.DKHUVUCID = K.ID AND T.SUSERID = @U AND T.STATUS = @S) " +
                        "ORDER BY K.SORTORDER, K.NAME",
                        "@S", ActiveStatus, "@U", userId);
                }
                else
                {
                    dt = DbHelper.Table(conn, null,
                        "SELECT K.ID, K.NAME, K.DKHOHANGID, K.DGIAMATHANGID, K.DBIEUTUONGID, " +
                        "COALESCE(K.TABHIENTHI, 0) AS TABHIENTHI, COALESCE(K.SORTORDER, '') AS SORTORDER " +
                        "FROM DKHUVUC K WHERE K.STATUS = @S ORDER BY K.SORTORDER, K.NAME",
                        "@S", ActiveStatus);
                }

                var list = new List<ServiceAreaInfo>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new ServiceAreaInfo
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        WarehouseId = DbHelper.Str(r["DKHOHANGID"]),
                        PriceLevelId = DbHelper.Str(r["DGIAMATHANGID"]) ?? "0",
                        IconId = DbHelper.Str(r["DBIEUTUONGID"]),
                        ShowTab = DbHelper.Int(r["TABHIENTHI"]),
                        SortOrder = DbHelper.Str(r["SORTORDER"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Danh sách bàn đang dùng (DBAN.STATUS = 30 AND DKHUVUC.STATUS = 30), sắp theo DBAN.NAME (§3.1).
        /// Kèm thông tin bill chưa thanh toán, sửa chữa chưa xong (TSUACHUA), đặt bàn hôm nay ±30 phút (TDATHANG),
        /// và khóa bàn nhiều máy trạm (GIULUC trong 120 giây gần nhất).
        /// </summary>
        public List<ServiceTableInfo> LoadTables(string areaId, string currentUserName)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                var args = new List<object>
                {
                    "@S", ActiveStatus,
                    "@ExpireSec", -ServiceOrderConstants.TableLockExpireSeconds
                };
                string areaFilter = "";
                if (!string.IsNullOrEmpty(areaId))
                {
                    areaFilter = "AND B.DKHUVUCID = @AreaId ";
                    args.Add("@AreaId");
                    args.Add(areaId);
                }

                string sql =
                    "SELECT B.ID, B.NAME, B.DKHUVUCID, K.NAME AS AREANAME, K.DKHOHANGID AS AREAKHO, " +
                    "K.DGIAMATHANGID AS AREAPRICE, B.DNHOMHIENTHIID, G.NAME AS GROUPNAME, " +
                    "COALESCE(B.TRANGTHAI, 0) AS TRANGTHAI, B.TAIKHOANGIU, B.GIULUC, " +
                    "CASE WHEN B.TAIKHOANGIU IS NOT NULL AND TRIM(B.TAIKHOANGIU) <> '' " +
                    "AND B.GIULUC > DATEADD(@ExpireSec SECOND TO CURRENT_TIMESTAMP) THEN 1 ELSE 0 END AS LOCKACTIVE, " +
                    "H.ID AS BILLID, H.NOTE AS BILLNOTE, H.BATDAU AS BILLSTART, COALESCE(H.SOLANINTAMTINH, 0) AS SOLANIN, " +
                    "(SELECT COUNT(*) FROM TSUACHUA SC WHERE SC.DBANID = B.ID AND COALESCE(SC.DASUAXONG, 0) = 0) AS SUACHUACNT, " +
                    "(SELECT FIRST 1 D.ID FROM TDATHANG D WHERE D.DBANID = B.ID AND D.STATUS = @S " +
                    "AND NOT EXISTS (SELECT 1 FROM TDONHANG H2 WHERE H2.TDATHANGID = D.ID) " +
                    "AND D.TUGIO BETWEEN DATEADD(-30 MINUTE TO CURRENT_TIMESTAMP) AND DATEADD(30 MINUTE TO CURRENT_TIMESTAMP) " +
                    "ORDER BY D.TUGIO) AS BOOKINGID, " +
                    "(SELECT FIRST 1 COALESCE(D.TENKHACH, '') FROM TDATHANG D WHERE D.DBANID = B.ID AND D.STATUS = @S " +
                    "AND NOT EXISTS (SELECT 1 FROM TDONHANG H2 WHERE H2.TDATHANGID = D.ID) " +
                    "AND D.TUGIO BETWEEN DATEADD(-30 MINUTE TO CURRENT_TIMESTAMP) AND DATEADD(30 MINUTE TO CURRENT_TIMESTAMP) " +
                    "ORDER BY D.TUGIO) AS BOOKINGGUEST, " +
                    "(SELECT FIRST 1 COALESCE(D.DIENTHOAI, '') FROM TDATHANG D WHERE D.DBANID = B.ID AND D.STATUS = @S " +
                    "AND NOT EXISTS (SELECT 1 FROM TDONHANG H2 WHERE H2.TDATHANGID = D.ID) " +
                    "AND D.TUGIO BETWEEN DATEADD(-30 MINUTE TO CURRENT_TIMESTAMP) AND DATEADD(30 MINUTE TO CURRENT_TIMESTAMP) " +
                    "ORDER BY D.TUGIO) AS BOOKINGPHONE, " +
                    "(SELECT FIRST 1 D.TUGIO FROM TDATHANG D WHERE D.DBANID = B.ID AND D.STATUS = @S " +
                    "AND NOT EXISTS (SELECT 1 FROM TDONHANG H2 WHERE H2.TDATHANGID = D.ID) " +
                    "AND D.TUGIO BETWEEN DATEADD(-30 MINUTE TO CURRENT_TIMESTAMP) AND DATEADD(30 MINUTE TO CURRENT_TIMESTAMP) " +
                    "ORDER BY D.TUGIO) AS BOOKINGTIME " +
                    "FROM DBAN B " +
                    "INNER JOIN DKHUVUC K ON K.ID = B.DKHUVUCID AND K.STATUS = @S " +
                    "LEFT JOIN DNHOMHIENTHI G ON G.ID = B.DNHOMHIENTHIID " +
                    "LEFT JOIN TDONHANG H ON H.ID = B.TDONHANGID AND COALESCE(H.DATHANHTOAN, 0) = 0 " +
                    "WHERE B.STATUS = @S " + areaFilter +
                    "ORDER BY B.NAME";

                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                var list = new List<ServiceTableInfo>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    string lockedBy = (DbHelper.Str(r["TAIKHOANGIU"]) ?? "").Trim();
                    bool lockActive = DbHelper.Int(r["LOCKACTIVE"]) == 1;
                    bool lockedByOther = lockActive && lockedBy.Length > 0
                        && !string.Equals(lockedBy, (currentUserName ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

                    string bookingId = DbHelper.Str(r["BOOKINGID"]);
                    string reservationText = "";
                    if (!string.IsNullOrEmpty(bookingId))
                    {
                        string guest = (DbHelper.Str(r["BOOKINGGUEST"]) ?? "").Trim();
                        string phone = (DbHelper.Str(r["BOOKINGPHONE"]) ?? "").Trim();
                        DateTime? bTime = ReadDateTime(r["BOOKINGTIME"]);
                        string timePart = bTime.HasValue ? bTime.Value.ToString("HH:mm") : "";
                        reservationText = phone.Length > 0
                            ? string.Format("{0} [{1}]-{2}", guest, phone, timePart)
                            : string.Format("{0}-{1}", guest, timePart);
                    }

                    list.Add(new ServiceTableInfo
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        AreaId = DbHelper.Str(r["DKHUVUCID"]) ?? "",
                        AreaName = DbHelper.Str(r["AREANAME"]) ?? "",
                        AreaWarehouseId = DbHelper.Str(r["AREAKHO"]),
                        AreaPriceLevelId = DbHelper.Str(r["AREAPRICE"]) ?? "0",
                        DisplayGroupId = DbHelper.Str(r["DNHOMHIENTHIID"]),
                        DisplayGroupName = DbHelper.Str(r["GROUPNAME"]) ?? "",
                        TableFlag = DbHelper.Int(r["TRANGTHAI"]),
                        ActiveBillId = DbHelper.Str(r["BILLID"]),
                        BillNote = DbHelper.Str(r["BILLNOTE"]) ?? "",
                        BillStartTime = ReadDateTime(r["BILLSTART"]),
                        TempPrintCount = DbHelper.Int(r["SOLANIN"]),
                        LockedByAccount = lockedBy,
                        LockedAt = ReadDateTime(r["GIULUC"]),
                        IsLockedByOther = lockedByOther,
                        UnderRepairCount = DbHelper.Int(r["SUACHUACNT"]),
                        ReservationBookingId = bookingId,
                        ReservationText = reservationText
                    });
                }
                return list;
            });
        }

        /// <summary>Đọc một bàn kèm khu vực; null nếu không tồn tại hoặc không còn hoạt động.</summary>
        public ServiceTableInfo LoadTable(string tableId, string currentUserName)
        {
            if (string.IsNullOrEmpty(tableId))
                return null;
            List<ServiceTableInfo> all = LoadTables(null, currentUserName);
            return all.Find(t => string.Equals(t.Id, tableId, StringComparison.Ordinal));
        }

        /// <summary>Tải hóa đơn theo ID kèm các dòng chi tiết và tổng BILLKHAC cùng NHOMGUID.</summary>
        public ServiceOrderHeader LoadBill(string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadBillIn(conn, null, billId));
        }

        /// <summary>Tải hóa đơn chưa thanh toán đang gắn trên bàn (theo DBAN.TDONHANGID).</summary>
        public ServiceOrderHeader LoadActiveBillByTable(string tableId)
        {
            if (string.IsNullOrEmpty(tableId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
            {
                string billId = DbHelper.Str(DbHelper.Scalar(conn, null,
                    "SELECT H.ID FROM DBAN B INNER JOIN TDONHANG H ON H.ID = B.TDONHANGID " +
                    "WHERE B.ID = @T AND COALESCE(H.DATHANHTOAN, 0) = 0",
                    "@T", tableId));
                return string.IsNullOrEmpty(billId) ? null : LoadBillIn(conn, null, billId);
            });
        }

        public static ServiceOrderHeader LoadBillIn(FbConnection conn, FbTransaction tx, string billId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.ID, H.NAME, COALESCE(H.SOHD, 0) AS SOHD, H.NGAY, H.BATDAU, " +
                "H.DBANID, B.NAME AS TABLENAME, B.DKHUVUCID AS AREAID, A.NAME AS AREANAME, " +
                "A.DKHOHANGID AS AREAKHO, A.DGIAMATHANGID AS AREAPRICE, " +
                "H.DKHACHHANGID, K.NAME AS CUSTNAME, H.DCUAHANGID, COALESCE(H.SOKHACH, 1) AS SOKHACH, " +
                "COALESCE(H.DATHANHTOAN, 0) AS DATHANHTOAN, COALESCE(H.SOLANINTAMTINH, 0) AS SOLANIN, " +
                "H.NHOMGUID, H.TDATHANGID, H.NOTE, " +
                "COALESCE(H.TIENHANG, 0) AS TIENHANG, COALESCE(H.TIENHANGCHUAGIAM, 0) AS TIENHANGCHUAGIAM, " +
                "COALESCE(H.GIAMGIAMATHANG, 0) AS GIAMGIAMATHANG, " +
                "COALESCE(H.TILEGIAMGIA, 0) AS TILEGIAMGIA, COALESCE(H.TIENGIAMGIA, 0) AS TIENGIAMGIA, " +
                "COALESCE(H.GIAMTHEOTIEN, 0) AS GIAMTHEOTIEN, COALESCE(H.GIAMTONGTHEOTIEN, 0) AS GIAMTONGTHEOTIEN, " +
                "COALESCE(H.TILEPHIDICHVU, 0) AS TILEPHIDICHVU, COALESCE(H.PHIDICHVU, 0) AS PHIDICHVU, " +
                "COALESCE(H.PHIDICHVUTHEOTIEN, 0) AS PHIDICHVUTHEOTIEN, " +
                "COALESCE(H.TILETHUE, 0) AS TILETHUE, COALESCE(H.TIENTHUE, 0) AS TIENTHUE, " +
                "COALESCE(H.TONGCONG, 0) AS TONGCONG " +
                "FROM TDONHANG H " +
                "LEFT JOIN DBAN B ON B.ID = H.DBANID " +
                "LEFT JOIN DKHUVUC A ON A.ID = B.DKHUVUCID " +
                "LEFT JOIN DKHACHHANG K ON K.ID = H.DKHACHHANGID " +
                "WHERE H.ID = @I",
                "@I", billId);

            if (dt.Rows.Count == 0)
                return null;

            DataRow r = dt.Rows[0];
            string groupGuid = DbHelper.Str(r["NHOMGUID"]) ?? "";
            decimal billKhac = 0m;
            if (!string.IsNullOrEmpty(groupGuid))
            {
                billKhac = DbHelper.Dec(DbHelper.Scalar(conn, tx,
                    "SELECT COALESCE(SUM(COALESCE(TONGCONG, 0)), 0) FROM TDONHANG " +
                    "WHERE NHOMGUID = @G AND ID <> @I AND COALESCE(DATHANHTOAN, 0) = 0",
                    "@G", groupGuid, "@I", billId));
            }

            decimal tongCong = DbHelper.Dec(r["TONGCONG"]);
            var header = new ServiceOrderHeader
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                SoHd = DbHelper.Int(r["SOHD"]),
                Ngay = ReadDateTime(r["NGAY"]) ?? DateTime.Today,
                BatDau = ReadDateTime(r["BATDAU"]) ?? DateTime.Now,
                TableId = DbHelper.Str(r["DBANID"]) ?? "",
                TableName = DbHelper.Str(r["TABLENAME"]) ?? "",
                AreaId = DbHelper.Str(r["AREAID"]) ?? "",
                AreaName = DbHelper.Str(r["AREANAME"]) ?? "",
                WarehouseId = DbHelper.Str(r["AREAKHO"]),
                PriceLevelId = DbHelper.Str(r["AREAPRICE"]) ?? "0",
                CustomerId = DbHelper.Str(r["DKHACHHANGID"]),
                CustomerName = DbHelper.Str(r["CUSTNAME"]) ?? "",
                StoreId = DbHelper.Str(r["DCUAHANGID"]),
                GuestCount = Math.Max(1, DbHelper.Int(r["SOKHACH"])),
                PaidFlag = DbHelper.Int(r["DATHANHTOAN"]),
                TempPrintCount = DbHelper.Int(r["SOLANIN"]),
                GroupGuid = groupGuid,
                BookingId = DbHelper.Str(r["TDATHANGID"]),
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                TienHang = DbHelper.Dec(r["TIENHANG"]),
                TienHangChuaGiam = DbHelper.Dec(r["TIENHANGCHUAGIAM"]),
                GiamGiaMatHang = DbHelper.Dec(r["GIAMGIAMATHANG"]),
                TileGiamGia = DbHelper.Dec(r["TILEGIAMGIA"]),
                TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                GiamTheoTien = DbHelper.Int(r["GIAMTHEOTIEN"]) == ActiveStatus,
                BillDiscountFromPromo = DbHelper.Int(r["GIAMTONGTHEOTIEN"]) == ActiveStatus,
                TilePhiDichVu = DbHelper.Dec(r["TILEPHIDICHVU"]),
                PhiDichVu = DbHelper.Dec(r["PHIDICHVU"]),
                PhiDichVuTheoTien = DbHelper.Int(r["PHIDICHVUTHEOTIEN"]) == ActiveStatus,
                TileThue = DbHelper.Dec(r["TILETHUE"]),
                TienThue = DbHelper.Dec(r["TIENTHUE"]),
                TongCong = tongCong,
                BillKhac = billKhac,
                TienPhaiTra = tongCong + billKhac,
                Lines = LoadLinesIn(conn, tx, billId)
            };
            return header;
        }

        public static List<ServiceOrderLine> LoadLinesIn(FbConnection conn, FbTransaction tx, string billId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT D.ID, D.TDONHANGID, D.DMATHANGID, D.TENHANG, D.DDONVITINHID, U.NAME AS UNITNAME, " +
                "D.DKHOHANGID, COALESCE(D.SLXUATCHUAQUYDOI, COALESCE(D.SLXUAT, 0)) AS QTY, " +
                "COALESCE(D.DONGIA, 0) AS DONGIA, COALESCE(D.GIAVON, 0) AS GIAVON, " +
                "COALESCE(D.TILEGIAMGIA, 0) AS TILEGIAMGIA, COALESCE(D.TIENGIAMGIA, 0) AS TIENGIAMGIA, " +
                "COALESCE(D.GIAMTHEOTIEN, 0) AS GIAMTHEOTIEN, COALESCE(D.THANHTIEN, 0) AS THANHTIEN, " +
                "D.NOTE, D.COMBOID, D.COMBOPARENTID, COALESCE(D.COMBOSL, 0) AS COMBOSL, " +
                "COALESCE(D.XUATVATTU, 0) AS XUATVATTU, COALESCE(D.KHUYENMAI, 0) AS KHUYENMAI, D.TIMECREATED, " +
                "COALESCE(M.DNHOMMATHANGID, '') AS PGROUPID, COALESCE(M.DLOAIMATHANGID, '0') AS PTYPE, COALESCE(G.DLOAIDOID, '1') AS DRINKTYPE " +
                "FROM TDONHANGCHITIET D " +
                "LEFT JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                "LEFT JOIN DNHOMMATHANG G ON G.ID = M.DNHOMMATHANGID " +
                "LEFT JOIN DDONVITINH U ON U.ID = D.DDONVITINHID " +
                "WHERE D.TDONHANGID = @B AND COALESCE(D.XUATVATTU, 0) = 0 " +
                "ORDER BY D.TIMECREATED, D.ID",
                "@B", billId);

            var list = new List<ServiceOrderLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new ServiceOrderLine
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    OrderId = DbHelper.Str(r["TDONHANGID"]) ?? "",
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    ProductGroupId = DbHelper.Str(r["PGROUPID"]) ?? "",
                    ProductTypeId = DbHelper.Str(r["PTYPE"]) ?? "0",
                    DrinkTypeId = DbHelper.Str(r["DRINKTYPE"]) ?? "1",
                    ItemName = DbHelper.Str(r["TENHANG"]) ?? "",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]),
                    UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                    WarehouseId = DbHelper.Str(r["DKHOHANGID"]),
                    Quantity = DbHelper.Dec(r["QTY"]),
                    UnitPrice = DbHelper.Dec(r["DONGIA"]),
                    CostPrice = DbHelper.Dec(r["GIAVON"]),
                    DiscountRate = DbHelper.Dec(r["TILEGIAMGIA"]),
                    DiscountAmount = DbHelper.Dec(r["TIENGIAMGIA"]),
                    DiscountByMoney = DbHelper.Int(r["GIAMTHEOTIEN"]) == ActiveStatus,
                    Amount = DbHelper.Dec(r["THANHTIEN"]),
                    Note = DbHelper.Str(r["NOTE"]) ?? "",
                    ComboId = DbHelper.Str(r["COMBOID"]) ?? "",
                    ComboParentId = DbHelper.Str(r["COMBOPARENTID"]) ?? "",
                    ComboQty = DbHelper.Dec(r["COMBOSL"]),
                    XuatVatTu = DbHelper.Int(r["XUATVATTU"]),
                    KhuyenMai = DbHelper.Int(r["KHUYENMAI"]),
                    TimeCreated = ReadDateTime(r["TIMECREATED"])
                });
            }
            return list;
        }

        /// <summary>Nhóm mặt hàng bán (DNHOMMATHANG STATUS = 30, loại bỏ DLOAIDOID = '4' là nguyên liệu).</summary>
        public List<ProductGroupRow> LoadMenuGroups()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, PARENTID, DLOAIDOID, SORTORDER FROM DNHOMMATHANG " +
                    "WHERE STATUS = @S AND COALESCE(DLOAIDOID, '') <> '4' ORDER BY SORTORDER, NAME",
                    "@S", ActiveStatus);
                var list = new List<ProductGroupRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new ProductGroupRow
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? "",
                        ParentId = DbHelper.Str(r["PARENTID"]) ?? "",
                        DrinkTypeId = DbHelper.Str(r["DLOAIDOID"]) ?? "",
                        SortOrder = DbHelper.Str(r["SORTORDER"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>Danh sách mặt hàng bán (STATUS = 30, loại nguyên liệu '2' và mặt hàng hệ thống 'KM').</summary>
        public List<ServiceMenuItem> LoadMenuItems(bool sortByCode)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                string order = sortByCode ? "M.CODE, M.NAME" : "M.NAME";
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT M.ID, M.CODE, M.NAME, M.DNHOMMATHANGID, COALESCE(G.DLOAIDOID, '1') AS DRINKTYPE, " +
                    "COALESCE(M.DLOAIMATHANGID, '0') AS PTYPE, M.DDONVITINHID, U.NAME AS UNITNAME, " +
                    "COALESCE(M.GIABAN, 0) AS GIABAN, COALESCE(M.GIABAN2, 0) AS GIABAN2, " +
                    "COALESCE(M.GIABAN3, 0) AS GIABAN3, COALESCE(M.GIABAN4, 0) AS GIABAN4, " +
                    "COALESCE(M.GIAVON, 0) AS GIAVON, COALESCE(M.GIATHEOTHOIGIA, 0) AS GIATHEOTHOIGIA, " +
                    "COALESCE(M.TAMKHOA, 0) AS TAMKHOA, COALESCE(T.COTONKHO, 0) AS COTONKHO, M.STATUS " +
                    "FROM DMATHANG M " +
                    "LEFT JOIN DNHOMMATHANG G ON G.ID = M.DNHOMMATHANGID " +
                    "LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
                    "LEFT JOIN DLOAIMATHANG T ON T.ID = COALESCE(NULLIF(M.DLOAIMATHANGID, ''), '0') " +
                    "WHERE M.STATUS = @S AND M.ID <> 'KM' AND COALESCE(M.DLOAIMATHANGID, '0') <> '2' " +
                    "ORDER BY " + order,
                    "@S", ActiveStatus);

                var list = new List<ServiceMenuItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(MenuItemFrom(r));
                return list;
            });
        }

        /// <summary>Đọc một mặt hàng (mọi STATUS để phân biệt đã xóa vs tạm khóa theo §3.4).</summary>
        public ServiceMenuItem LoadProduct(string productId)
        {
            if (string.IsNullOrEmpty(productId))
                return null;
            return DbHelper.Read(_databasePath, conn => LoadProductIn(conn, null, productId));
        }

        public static ServiceMenuItem LoadProductIn(FbConnection conn, FbTransaction tx, string productId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT M.ID, M.CODE, M.NAME, M.DNHOMMATHANGID, COALESCE(G.DLOAIDOID, '1') AS DRINKTYPE, " +
                "COALESCE(NULLIF(M.DLOAIMATHANGID, ''), '0') AS PTYPE, M.DDONVITINHID, U.NAME AS UNITNAME, " +
                "COALESCE(M.GIABAN, 0) AS GIABAN, COALESCE(M.GIABAN2, 0) AS GIABAN2, " +
                "COALESCE(M.GIABAN3, 0) AS GIABAN3, COALESCE(M.GIABAN4, 0) AS GIABAN4, " +
                "COALESCE(M.GIAVON, 0) AS GIAVON, COALESCE(M.GIATHEOTHOIGIA, 0) AS GIATHEOTHOIGIA, " +
                "COALESCE(M.TAMKHOA, 0) AS TAMKHOA, COALESCE(T.COTONKHO, 0) AS COTONKHO, M.STATUS " +
                "FROM DMATHANG M " +
                "LEFT JOIN DNHOMMATHANG G ON G.ID = M.DNHOMMATHANGID " +
                "LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
                "LEFT JOIN DLOAIMATHANG T ON T.ID = COALESCE(NULLIF(M.DLOAIMATHANGID, ''), '0') " +
                "WHERE M.ID = @I",
                "@I", productId);
            return dt.Rows.Count == 0 ? null : MenuItemFrom(dt.Rows[0]);
        }

        public static List<ServiceRecipeComponent> LoadRecipeIn(FbConnection conn, FbTransaction tx, string productId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT D.DVATTUID, COALESCE(D.SOLUONG, 0) AS SOLUONG, " +
                "M.NAME, COALESCE(NULLIF(M.DLOAIMATHANGID, ''), '0') AS PTYPE, " +
                "M.DDONVITINHID, U.NAME AS UNITNAME, COALESCE(M.GIAVON, 0) AS GIAVON, " +
                "COALESCE(T.COTONKHO, 0) AS COTONKHO " +
                "FROM DDINHLUONG D " +
                "INNER JOIN DMATHANG M ON M.ID = D.DVATTUID " +
                "LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID " +
                "LEFT JOIN DLOAIMATHANG T ON T.ID = COALESCE(NULLIF(M.DLOAIMATHANGID, ''), '0') " +
                "WHERE D.DMATHANGID = @P AND D.STATUS = @S " +
                "ORDER BY D.TIMECREATED, D.ID",
                "@P", productId, "@S", ActiveStatus);

            var list = new List<ServiceRecipeComponent>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new ServiceRecipeComponent
                {
                    MaterialId = DbHelper.Str(r["DVATTUID"]) ?? "",
                    MaterialName = DbHelper.Str(r["NAME"]) ?? "",
                    MaterialTypeId = DbHelper.Str(r["PTYPE"]) ?? "0",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]),
                    UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                    Quantity = DbHelper.Dec(r["SOLUONG"]),
                    CostPrice = DbHelper.Dec(r["GIAVON"]),
                    HasStock = DbHelper.Int(r["COTONKHO"]) != 0
                });
            }
            return list;
        }

        private static ServiceMenuItem MenuItemFrom(DataRow r)
        {
            return new ServiceMenuItem
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Code = DbHelper.Str(r["CODE"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                GroupId = DbHelper.Str(r["DNHOMMATHANGID"]) ?? "",
                DrinkTypeId = DbHelper.Str(r["DRINKTYPE"]) ?? "1",
                ProductTypeId = DbHelper.Str(r["PTYPE"]) ?? "0",
                UnitId = DbHelper.Str(r["DDONVITINHID"]),
                UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                SellPrice = DbHelper.Dec(r["GIABAN"]),
                SellPrice2 = DbHelper.Dec(r["GIABAN2"]),
                SellPrice3 = DbHelper.Dec(r["GIABAN3"]),
                SellPrice4 = DbHelper.Dec(r["GIABAN4"]),
                CostPrice = DbHelper.Dec(r["GIAVON"]),
                PriceByTime = DbHelper.Int(r["GIATHEOTHOIGIA"]) == ActiveStatus,
                TempLocked = DbHelper.Int(r["TAMKHOA"]) == ActiveStatus,
                HasStock = DbHelper.Int(r["COTONKHO"]) != 0,
                Status = DbHelper.Int(r["STATUS"])
            };
        }

        private static DateTime? ReadDateTime(object v)
        {
            if (v == null || v is DBNull)
                return null;
            return Convert.ToDateTime(v);
        }
    }
}
