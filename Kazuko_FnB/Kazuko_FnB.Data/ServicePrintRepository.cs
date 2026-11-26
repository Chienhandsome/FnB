using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn đọc/ghi phục vụ In hóa đơn thanh toán, In tạm tính, In lại bill và In bếp/chế biến (patch sddv-d).
    /// Mọi câu SQL đều tham số hóa; chỉ đọc số tiền đã lưu từ TDONHANG/TDONHANGCHITIET (không tính lại).
    /// </summary>
    public sealed class ServicePrintRepository
    {
        private const int ActiveStatus = 30;
        private const int DeletedStatus = -30;
        private readonly string _databasePath;

        public ServicePrintRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        public void RunInTransaction(Action<FbConnection, FbTransaction> work)
        {
            DbHelper.InTransaction(_databasePath, work);
        }

        public T Read<T>(Func<FbConnection, T> work)
        {
            return DbHelper.Read(_databasePath, work);
        }

        /// <summary>
        /// Đọc đầy đủ dữ liệu hóa đơn (đầu phiếu, dòng cha Table0, dòng con ComboChild, bảng cộng gộp InCongGop)
        /// từ CSDL để đổ vào các mẫu in STEMPLATE của màn Hóa đơn bán hàng.
        /// </summary>
        public ServiceInvoicePrintDocument LoadInvoiceForPrint(
            string billId,
            bool includeOrderAdvance,
            int pointCalcMode,
            int revenuePerPoint)
        {
            if (string.IsNullOrEmpty(billId))
                return null;
            return DbHelper.Read(_databasePath, conn =>
                LoadInvoiceForPrintIn(conn, null, billId, includeOrderAdvance, pointCalcMode, revenuePerPoint));
        }

        public static ServiceInvoicePrintDocument LoadInvoiceForPrintIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            bool includeOrderAdvance,
            int pointCalcMode,
            int revenuePerPoint)
        {
            if (string.IsNullOrEmpty(billId))
                return null;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT H.ID, H.NAME, H.NOTE, H.NGAY, H.BATDAU, H.KETTHUC, H.GIOTHANHTOAN, " +
                "COALESCE(H.SOKHACH, 1) AS SOKHACH, COALESCE(H.SOTT, 0) AS SOTT, " +
                "COALESCE(H.DATHANHTOAN, 0) AS DATHANHTOAN, " +
                "COALESCE(H.SOLANINTAMTINH, 0) AS SOLANINTAMTINH, " +
                "COALESCE(H.LANINHOADON, 0) AS LANINHOADON, " +
                "H.DBANID, B.NAME AS TABLENAME, B.DKHUVUCID AS AREAID, " +
                "A.NAME AS AREANAME, A.STEMPLATEID AS AREATEMPLATEID, " +
                "H.DKHACHHANGID, K.NAME AS CUSTNAME, " +
                "H.DNHANVIENXUATID, NV.NAME AS EMPNAME, " +
                "H.PASSWIFI, H.TDATHANGID, " +
                "COALESCE(H.TIENHANG, 0) AS TIENHANG, " +
                "COALESCE(H.TIENHANGCHUAGIAM, 0) AS TIENHANGCHUAGIAM, " +
                "COALESCE(H.GIAMGIAMATHANG, 0) AS GIAMGIAMATHANG, " +
                "COALESCE(H.TIENGIO, 0) AS TIENGIO, " +
                "COALESCE(H.TILEGIAMGIA, 0) AS TILEGIAMGIA, COALESCE(H.TIENGIAMGIA, 0) AS TIENGIAMGIA, " +
                "COALESCE(H.TILEGIAMGIAGIO, 0) AS TILEGIAMGIAGIO, COALESCE(H.TIENGIAMGIAGIO, 0) AS TIENGIAMGIAGIO, " +
                "COALESCE(H.TILEGIAMGIATONG, 0) AS TILEGIAMGIATONG, COALESCE(H.TIENGIAMGIATONG, 0) AS TIENGIAMGIATONG, " +
                "COALESCE(H.TILEPHIDICHVU, 0) AS TILEPHIDICHVU, COALESCE(H.PHIDICHVU, 0) AS PHIDICHVU, " +
                "COALESCE(H.TILETHUE, 0) AS TILETHUE, COALESCE(H.TIENTHUE, 0) AS TIENTHUE, " +
                "COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                "COALESCE(H.VOUCHER, 0) AS VOUCHER, COALESCE(H.THE, 0) AS THE, " +
                "COALESCE(H.CHUYENKHOAN, 0) AS CHUYENKHOAN, COALESCE(H.THETRATRUOC, 0) AS THETRATRUOC, " +
                "COALESCE(H.TRUTICHLUY, 0) AS TRUTICHLUY, COALESCE(H.TIENMAT, 0) AS TIENMAT, " +
                "COALESCE(H.KHACHDUA, 0) AS KHACHDUA, COALESCE(H.TRALAI, 0) AS TRALAI, " +
                "COALESCE(H.TIENTHANHTOAN, 0) AS TIENTHANHTOAN, COALESCE(H.CONLAI, 0) AS CONLAI, " +
                "COALESCE(H.CONGNO, 0) AS CONGNO, COALESCE(H.DIEM, 0) AS DIEM, " +
                "COALESCE(H.DIEMGIAM, 0) AS DIEMGIAM, COALESCE(H.DOITRA, 0) AS DOITRA, " +
                "COALESCE(H.NOCU, 0) AS NOCU, COALESCE(H.DATTRUOC, 0) AS DATTRUOC " +
                "FROM TDONHANG H " +
                "LEFT JOIN DBAN B ON B.ID = H.DBANID " +
                "LEFT JOIN DKHUVUC A ON A.ID = B.DKHUVUCID " +
                "LEFT JOIN DKHACHHANG K ON K.ID = H.DKHACHHANGID " +
                "LEFT JOIN DNHANVIEN NV ON NV.ID = H.DNHANVIENXUATID " +
                "WHERE H.ID = @I AND COALESCE(H.STATUS, 0) <> @Del",
                "@I", billId, "@Del", DeletedStatus);

            if (dt.Rows.Count == 0)
                return null;

            DataRow r = dt.Rows[0];
            int paidFlag = DbHelper.Int(r["DATHANHTOAN"]);
            string custId = DbHelper.Str(r["DKHACHHANGID"]);
            string bookingId = DbHelper.Str(r["TDATHANGID"]);
            decimal datTruoc = DbHelper.Dec(r["DATTRUOC"]);
            decimal noCu = DbHelper.Dec(r["NOCU"]);

            if (paidFlag == 0)
            {
                if (datTruoc == 0m)
                    datTruoc = ServicePaymentRepository.GetDepositIn(conn, tx, billId, bookingId, includeOrderAdvance);
                if (noCu == 0m && !string.IsNullOrEmpty(custId))
                    noCu = ServicePaymentRepository.GetOldDebtIn(conn, tx, custId, new[] { billId });
            }

            decimal totalPoints = !string.IsNullOrEmpty(custId)
                ? ServicePaymentRepository.GetCustomerPointsIn(conn, tx, custId, pointCalcMode, revenuePerPoint, null)
                : 0m;

            var doc = new ServiceInvoicePrintDocument
            {
                BillId = DbHelper.Str(r["ID"]) ?? "",
                BillName = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                Ngay = ReadDateTime(r["NGAY"]) ?? DateTime.Today,
                BatDau = ReadDateTime(r["BATDAU"]) ?? DateTime.Now,
                KetThuc = ReadDateTime(r["KETTHUC"]),
                GioThanhToan = ReadDateTime(r["GIOTHANHTOAN"]),
                GuestCount = Math.Max(1, DbHelper.Int(r["SOKHACH"])),
                SequenceInDay = DbHelper.Int(r["SOTT"]),
                PaidFlag = paidFlag,
                TempPrintCount = DbHelper.Int(r["SOLANINTAMTINH"]),
                InvoicePrintCount = DbHelper.Int(r["LANINHOADON"]),
                TableId = DbHelper.Str(r["DBANID"]) ?? "",
                TableName = DbHelper.Str(r["TABLENAME"]) ?? "",
                AreaId = DbHelper.Str(r["AREAID"]) ?? "",
                AreaName = DbHelper.Str(r["AREANAME"]) ?? "",
                AreaTemplateId = DbHelper.Str(r["AREATEMPLATEID"]) ?? "",
                CustomerId = custId ?? "",
                CustomerName = DbHelper.Str(r["CUSTNAME"]) ?? "",
                EmployeeId = DbHelper.Str(r["DNHANVIENXUATID"]) ?? "",
                EmployeeName = DbHelper.Str(r["EMPNAME"]) ?? "",
                PassWifi = DbHelper.Str(r["PASSWIFI"]) ?? "",
                TienHang = DbHelper.Dec(r["TIENHANG"]),
                TienHangChuaGiam = DbHelper.Dec(r["TIENHANGCHUAGIAM"]),
                GiamGiaMatHang = DbHelper.Dec(r["GIAMGIAMATHANG"]),
                TienGio = DbHelper.Dec(r["TIENGIO"]),
                TileGiamGia = DbHelper.Dec(r["TILEGIAMGIA"]),
                TienGiamGia = DbHelper.Dec(r["TIENGIAMGIA"]),
                TileGiamGiaGio = DbHelper.Dec(r["TILEGIAMGIAGIO"]),
                TienGiamGiaGio = DbHelper.Dec(r["TIENGIAMGIAGIO"]),
                TileGiamGiaTong = DbHelper.Dec(r["TILEGIAMGIATONG"]),
                TienGiamGiaTong = DbHelper.Dec(r["TIENGIAMGIATONG"]),
                TilePhiDichVu = DbHelper.Dec(r["TILEPHIDICHVU"]),
                PhiDichVu = DbHelper.Dec(r["PHIDICHVU"]),
                TileThue = DbHelper.Dec(r["TILETHUE"]),
                TienThue = DbHelper.Dec(r["TIENTHUE"]),
                TongCong = DbHelper.Dec(r["TONGCONG"]),
                Voucher = DbHelper.Dec(r["VOUCHER"]),
                The = DbHelper.Dec(r["THE"]),
                ChuyenKhoan = DbHelper.Dec(r["CHUYENKHOAN"]),
                TheTraTruoc = DbHelper.Dec(r["THETRATRUOC"]),
                TruTichLuy = DbHelper.Dec(r["TRUTICHLUY"]),
                TienMat = DbHelper.Dec(r["TIENMAT"]),
                KhachDua = DbHelper.Dec(r["KHACHDUA"]),
                TraLai = DbHelper.Dec(r["TRALAI"]),
                TienThanhToan = DbHelper.Dec(r["TIENTHANHTOAN"]),
                ConLai = DbHelper.Dec(r["CONLAI"]),
                CongNo = DbHelper.Dec(r["CONGNO"]),
                Diem = DbHelper.Dec(r["DIEM"]),
                DiemGiam = DbHelper.Dec(r["DIEMGIAM"]),
                DoiTra = DbHelper.Dec(r["DOITRA"]),
                NoCu = noCu,
                DatTruoc = datTruoc,
                CustomerTotalPoints = totalPoints
            };

            LoadInvoiceLinesIn(conn, tx, billId, doc);
            return doc;
        }

        private static void LoadInvoiceLinesIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            ServiceInvoicePrintDocument doc)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT D.ID, D.NOTE, D.DMATHANGID, " +
                "COALESCE(M.NAME, D.TENHANG, '') AS DMATHANG_NAME, " +
                "COALESCE(M.CODE, '') AS DMATHANG_CODE, " +
                "COALESCE(M.TENTIENGANH, '') AS DMATHANG_TENTIENGANH, " +
                "COALESCE(M.DLOAIMATHANGID, '0') AS DMATHANG_DLOAIMATHANGID, " +
                "D.DDONVITINHID, COALESCE(U.NAME, '') AS DDONVITINH_NAME, " +
                "COALESCE(D.BAOHANH, M.BAOHANH, '') AS BAOHANH, " +
                "COALESCE(D.TENHANG, M.NAME, '') AS TENHANG, " +
                "COALESCE(D.TILEGIAMGIA, 0) AS TILEGIAMGIA, " +
                "COALESCE(D.TIENGIAMGIA, 0) AS TIENGIAMGIA, " +
                "COALESCE(D.THANHTIEN, 0) AS THANHTIEN, " +
                "COALESCE(D.DONGIA, 0) AS DONGIA, " +
                "COALESCE(D.SLNHAP, 0) AS SLNHAP, " +
                "COALESCE(D.SLXUAT, 0) AS SLXUAT, " +
                "COALESCE(D.SLNHAPCHUAQUYDOI, COALESCE(D.SLNHAP, 0)) AS SLNHAPCHUAQUYDOI, " +
                "COALESCE(D.SLXUATCHUAQUYDOI, COALESCE(D.SLXUAT, 0)) AS SLXUATCHUAQUYDOI, " +
                "COALESCE(D.COMBOID, '') AS COMBOID, " +
                "COALESCE(D.COMBOPARENTID, '') AS COMBOPARENTID, " +
                "D.TUGIO, D.DENGIO " +
                "FROM TDONHANGCHITIET D " +
                "LEFT JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                "LEFT JOIN DDONVITINH U ON U.ID = D.DDONVITINHID " +
                "WHERE D.TDONHANGID = @B AND COALESCE(D.XUATVATTU, 0) = 0 " +
                "ORDER BY D.TIMECREATED, D.ID",
                "@B", billId);

            var grouped = new Dictionary<string, ServiceInvoiceConsolidatedLine>(StringComparer.Ordinal);
            var groupOrder = new List<string>();

            foreach (DataRow r in dt.Rows)
            {
                var line = new ServiceInvoicePrintLine
                {
                    Id = DbHelper.Str(r["ID"]) ?? "",
                    Note = DbHelper.Str(r["NOTE"]) ?? "",
                    ProductId = DbHelper.Str(r["DMATHANGID"]) ?? "",
                    ProductName = DbHelper.Str(r["DMATHANG_NAME"]) ?? "",
                    ProductCode = DbHelper.Str(r["DMATHANG_CODE"]) ?? "",
                    EnglishName = DbHelper.Str(r["DMATHANG_TENTIENGANH"]) ?? "",
                    ProductTypeId = DbHelper.Str(r["DMATHANG_DLOAIMATHANGID"]) ?? "0",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]) ?? "",
                    UnitName = DbHelper.Str(r["DDONVITINH_NAME"]) ?? "",
                    Warranty = DbHelper.Str(r["BAOHANH"]) ?? "",
                    ItemName = DbHelper.Str(r["TENHANG"]) ?? "",
                    DiscountRate = DbHelper.Dec(r["TILEGIAMGIA"]),
                    DiscountAmount = DbHelper.Dec(r["TIENGIAMGIA"]),
                    Amount = DbHelper.Dec(r["THANHTIEN"]),
                    UnitPrice = DbHelper.Dec(r["DONGIA"]),
                    SlNhap = DbHelper.Dec(r["SLNHAP"]),
                    SlXuat = DbHelper.Dec(r["SLXUAT"]),
                    SlNhapChuaQuyDoi = DbHelper.Dec(r["SLNHAPCHUAQUYDOI"]),
                    SlXuatChuaQuyDoi = DbHelper.Dec(r["SLXUATCHUAQUYDOI"]),
                    ComboId = (DbHelper.Str(r["COMBOID"]) ?? "").Trim(),
                    ComboParentId = (DbHelper.Str(r["COMBOPARENTID"]) ?? "").Trim(),
                    TuGio = ReadDateTime(r["TUGIO"]),
                    DenGio = ReadDateTime(r["DENGIO"])
                };

                if (line.ComboParentId.Length > 0)
                {
                    doc.ComboChildLines.Add(line);
                }
                else
                {
                    doc.ParentLines.Add(line);
                    string gKey = line.ItemName + "\u001F" + line.UnitId + "\u001F" + line.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    ServiceInvoiceConsolidatedLine agg;
                    if (!grouped.TryGetValue(gKey, out agg))
                    {
                        agg = new ServiceInvoiceConsolidatedLine
                        {
                            ItemName = line.ItemName,
                            UnitName = line.UnitName,
                            UnitPrice = line.UnitPrice
                        };
                        grouped[gKey] = agg;
                        groupOrder.Add(gKey);
                    }
                    agg.Quantity += line.SlXuat - line.SlNhap;
                    agg.Amount += line.Amount;
                }
            }

            foreach (string gKey in groupOrder)
                doc.ConsolidatedLines.Add(grouped[gKey]);
        }

        /// <summary>Tăng SOLANINTAMTINH thêm 1 và ghi INTAMTINHLUC = giờ máy chủ cho bill chưa thanh toán.</summary>
        public static int IncrementTempPrintIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            string actorId,
            DateTime serverNow)
        {
            return DbHelper.Exec(conn, tx,
                "UPDATE TDONHANG SET SOLANINTAMTINH = COALESCE(SOLANINTAMTINH, 0) + 1, " +
                "INTAMTINHLUC = @Now, USERMODIFIEDID = @U, TIMEMODIFIED = @Now " +
                "WHERE ID = @B AND COALESCE(DATHANHTOAN, 0) = 0",
                "@Now", serverNow, "@U", actorId ?? "", "@B", billId);
        }

        /// <summary>Tìm bill đã thanh toán gần nhất (ưu tiên đúng bàn đang chọn, nếu không có thì gần nhất toàn hệ thống).</summary>
        public string FindLatestPaidBillId(string preferredTableId)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                if (!string.IsNullOrEmpty(preferredTableId))
                {
                    string byTable = DbHelper.Str(DbHelper.Scalar(conn, null,
                        "SELECT FIRST 1 ID FROM TDONHANG " +
                        "WHERE COALESCE(LOAI, 0) = 0 AND COALESCE(DATHANHTOAN, 0) = 30 " +
                        "AND COALESCE(STATUS, 0) <> @Del AND DBANID = @T " +
                        "ORDER BY GIOTHANHTOAN DESC, TIMEMODIFIED DESC, TIMECREATED DESC",
                        "@Del", DeletedStatus, "@T", preferredTableId));
                    if (!string.IsNullOrEmpty(byTable))
                        return byTable;
                }

                return DbHelper.Str(DbHelper.Scalar(conn, null,
                    "SELECT FIRST 1 ID FROM TDONHANG " +
                    "WHERE COALESCE(LOAI, 0) = 0 AND COALESCE(DATHANHTOAN, 0) = 30 " +
                    "AND COALESCE(STATUS, 0) <> @Del " +
                    "ORDER BY GIOTHANHTOAN DESC, TIMEMODIFIED DESC, TIMECREATED DESC",
                    "@Del", DeletedStatus)) ?? "";
            });
        }

        // ------------------------------------------------------------------ In bếp / chế biến (TINCHEBIEN)

        /// <summary>Đọc các dòng món cần chế biến trên bill (loại bỏ XUATVATTU = 30 và các loại 4, 5, 7 như gốc).</summary>
        public static List<KitchenRawLine> LoadKitchenBillLinesIn(FbConnection conn, FbTransaction tx, string billId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT COALESCE(D.TENHANG, M.NAME, '') AS TENHANG, " +
                "COALESCE(D.NOTE, '') AS NOTE, " +
                "COALESCE(D.DDONVITINHID, '') AS DDONVITINHID, " +
                "COALESCE(U.NAME, '') AS UNITNAME, " +
                "COALESCE(NULLIF(G.DLOAIDOID, ''), '1') AS DLOAIDOID, " +
                "COALESCE(D.DONGIA, 0) AS DONGIA, " +
                "COALESCE(D.SLXUATCHUAQUYDOI, COALESCE(D.SLXUAT, 0)) AS QTY " +
                "FROM TDONHANGCHITIET D " +
                "LEFT JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                "LEFT JOIN DNHOMMATHANG G ON G.ID = M.DNHOMMATHANGID " +
                "LEFT JOIN DDONVITINH U ON U.ID = D.DDONVITINHID " +
                "WHERE D.TDONHANGID = @B " +
                "AND COALESCE(D.XUATVATTU, 0) = 0 " +
                "AND COALESCE(M.DLOAIMATHANGID, '0') NOT IN ('4', '5', '7') " +
                "ORDER BY D.TIMECREATED, D.ID",
                "@B", billId);

            return ReadKitchenRawLines(dt);
        }

        /// <summary>Đọc các dòng đã ghi nhận in xuống bếp của bill trong bảng TINCHEBIEN.</summary>
        public static List<KitchenRawLine> LoadKitchenPrintedLinesIn(FbConnection conn, FbTransaction tx, string billId)
        {
            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT COALESCE(T.TENHANG, '') AS TENHANG, " +
                "COALESCE(T.NOTE, '') AS NOTE, " +
                "COALESCE(T.DDONVITINHID, '') AS DDONVITINHID, " +
                "COALESCE(U.NAME, '') AS UNITNAME, " +
                "COALESCE(NULLIF(T.DLOAIDOID, ''), '1') AS DLOAIDOID, " +
                "COALESCE(T.DONGIA, 0) AS DONGIA, " +
                "COALESCE(T.SOLUONG, 0) AS QTY " +
                "FROM TINCHEBIEN T " +
                "LEFT JOIN DDONVITINH U ON U.ID = T.DDONVITINHID " +
                "WHERE T.TDONHANGID = @B " +
                "ORDER BY T.TIMECREATED, T.ID",
                "@B", billId);

            return ReadKitchenRawLines(dt);
        }

        public static int GetNextKitchenPrintSeqIn(FbConnection conn, FbTransaction tx, string billId)
        {
            return DbHelper.Int(DbHelper.Scalar(conn, tx,
                "SELECT COALESCE(MAX(LANSO), 0) + 1 FROM TINCHEBIEN WHERE TDONHANGID = @B",
                "@B", billId));
        }

        /// <summary>Ghi nhận các dòng đã in chế biến vào bảng TINCHEBIEN (LuuInCheBien).</summary>
        public static void InsertKitchenPrintedItemsIn(
            FbConnection conn,
            FbTransaction tx,
            string billId,
            IList<KitchenPendingItem> items,
            int printSeq,
            string actorId,
            DateTime serverNow)
        {
            if (items == null || items.Count == 0 || string.IsNullOrEmpty(billId))
                return;

            for (int i = 0; i < items.Count; i++)
            {
                KitchenPendingItem item = items[i];
                if (item == null || item.Quantity == 0m)
                    continue;
                DbHelper.Exec(conn, tx,
                    "INSERT INTO TINCHEBIEN (" +
                    "ID, TDONHANGID, TENHANG, SOLUONG, NOTE, DDONVITINHID, DLOAIDOID, LANSO, DONGIA, " +
                    "STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@ID, @B, @TEN, @SL, @NOTE, @UID, @DL, @LANSO, @DG, @ST, @U, @T)",
                    "@ID", DbHelper.NewId(),
                    "@B", billId,
                    "@TEN", item.ItemName ?? "",
                    "@SL", item.Quantity,
                    "@NOTE", string.IsNullOrEmpty(item.Note) ? null : item.Note,
                    "@UID", string.IsNullOrEmpty(item.UnitId) ? null : item.UnitId,
                    "@DL", string.IsNullOrEmpty(item.DrinkTypeId) ? ServicePrintConstants.DrinkTypeDrink : item.DrinkTypeId,
                    "@LANSO", printSeq,
                    "@DG", item.UnitPrice,
                    "@ST", ActiveStatus,
                    "@U", actorId ?? "",
                    "@T", serverNow);
            }
        }

        private static List<KitchenRawLine> ReadKitchenRawLines(DataTable dt)
        {
            var list = new List<KitchenRawLine>(dt.Rows.Count);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new KitchenRawLine
                {
                    ItemName = DbHelper.Str(r["TENHANG"]) ?? "",
                    Note = DbHelper.Str(r["NOTE"]) ?? "",
                    UnitId = DbHelper.Str(r["DDONVITINHID"]) ?? "",
                    UnitName = DbHelper.Str(r["UNITNAME"]) ?? "",
                    DrinkTypeId = DbHelper.Str(r["DLOAIDOID"]) ?? ServicePrintConstants.DrinkTypeDrink,
                    UnitPrice = DbHelper.Dec(r["DONGIA"]),
                    Quantity = DbHelper.Dec(r["QTY"])
                });
            }
            return list;
        }

        private static DateTime? ReadDateTime(object v)
        {
            if (v == null || v is DBNull)
                return null;
            return Convert.ToDateTime(v);
        }
    }
}
