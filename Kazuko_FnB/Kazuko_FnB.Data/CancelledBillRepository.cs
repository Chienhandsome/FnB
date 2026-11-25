using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc danh mục hóa đơn hủy (TDONHANGHUY và TDONHANGHUYCHITIET) theo docs/specs/danh-muc-hoa-don-huy.md.
    /// Chỉ đọc, không có câu SQL ghi; mọi truy vấn đều tham số hóa; hiện mọi STATUS (Q16).
    /// </summary>
    public sealed class CancelledBillRepository
    {
        private readonly string _databasePath;

        public CancelledBillRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>
        /// Đọc danh sách hóa đơn hủy, lọc theo NGAYHUY trong khoảng [from.Date, to.Date + 1 ngày).
        /// Đầu nào null thì bỏ điều kiện đầu đó; cả hai null thì tải toàn bộ mọi STATUS.
        /// Sắp tăng dần theo NGAYHUY, GIOHUY, NAME.
        /// </summary>
        public List<CancelledBillRow> LoadBills(DateTime? from, DateTime? to)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                var conditions = new List<string>(2);
                var args = new List<object>(4);

                if (from.HasValue)
                {
                    conditions.Add("NGAYHUY >= @From");
                    args.Add("@From");
                    args.Add(from.Value.Date);
                }
                if (to.HasValue)
                {
                    conditions.Add("NGAYHUY < @ToExclusive");
                    args.Add("@ToExclusive");
                    args.Add(to.Value.Date.AddDays(1));
                }

                string sql =
                    "SELECT ID, NAME, NOTE, NGAY, KHACHHANG, NHANVEN, THUNGAN, " +
                    "GIOHUY, NGAYHUY, DOITRA, DATHANHTOAN, GIOTHANHTOAN, TRALAI, " +
                    "TIENHANG, TILETHUE, TIENTHUE, TILEGIAMGIA, TIENGIAMGIA, " +
                    "PHIVANCHUYEN, THANHTOANBOI, LYDOHUY, TIENGIO, PHIDICHVU, " +
                    "TDONHANGID, STATUS " +
                    "FROM TDONHANGHUY" +
                    (conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions.ToArray()) : "") +
                    " ORDER BY NGAYHUY, GIOHUY, NAME";

                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                var list = new List<CancelledBillRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new CancelledBillRow
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]),
                        Note = DbHelper.Str(r["NOTE"]),
                        Ngay = ReadDateTime(r["NGAY"]),
                        KhachHang = DbHelper.Str(r["KHACHHANG"]),
                        NhanVien = DbHelper.Str(r["NHANVEN"]),
                        ThuNgan = DbHelper.Str(r["THUNGAN"]),
                        GioHuy = ReadDateTime(r["GIOHUY"]),
                        NgayHuy = ReadDateTime(r["NGAYHUY"]),
                        DoiTra = ReadDecimal(r["DOITRA"]),
                        DaThanhToan = ReadInt(r["DATHANHTOAN"]),
                        GioThanhToan = ReadDateTime(r["GIOTHANHTOAN"]),
                        TraLai = ReadDecimal(r["TRALAI"]),
                        TienHang = ReadDecimal(r["TIENHANG"]),
                        TiLeThue = ReadDecimal(r["TILETHUE"]),
                        TienThue = ReadDecimal(r["TIENTHUE"]),
                        TiLeGiamGia = ReadDecimal(r["TILEGIAMGIA"]),
                        TienGiamGia = ReadDecimal(r["TIENGIAMGIA"]),
                        PhiVanChuyen = ReadDecimal(r["PHIVANCHUYEN"]),
                        ThanhToanBoi = DbHelper.Str(r["THANHTOANBOI"]),
                        LyDoHuy = DbHelper.Str(r["LYDOHUY"]),
                        TienGio = ReadDecimal(r["TIENGIO"]),
                        PhiDichVu = ReadDecimal(r["PHIDICHVU"]),
                        OriginalOrderId = DbHelper.Str(r["TDONHANGID"]),
                        Status = ReadInt(r["STATUS"])
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Đọc các dòng chi tiết của một bản hóa đơn hủy (TDONHANGHUYCHITIET theo TDONHANGHUYID).
        /// Sắp tăng dần theo TIMECREATED, ID. Truyền rỗng trả danh sách trống.
        /// </summary>
        public List<CancelledBillLine> LoadLines(string billHuyId)
        {
            if (string.IsNullOrEmpty(billHuyId))
                return new List<CancelledBillLine>();

            return DbHelper.Read(_databasePath, conn =>
            {
                const string sql =
                    "SELECT ID, TDONHANGHUYID, NOTE, MAHANG, TENHANG, DVT, " +
                    "SOLUONG, DONGIA, THANHTIEN, TIMECREATED " +
                    "FROM TDONHANGHUYCHITIET " +
                    "WHERE TDONHANGHUYID = @BillHuyId " +
                    "ORDER BY TIMECREATED, ID";

                DataTable dt = DbHelper.Table(conn, null, sql, "@BillHuyId", billHuyId);
                var list = new List<CancelledBillLine>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new CancelledBillLine
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        CancelledBillId = DbHelper.Str(r["TDONHANGHUYID"]) ?? "",
                        Note = DbHelper.Str(r["NOTE"]),
                        MaHang = DbHelper.Str(r["MAHANG"]),
                        TenHang = DbHelper.Str(r["TENHANG"]),
                        Dvt = DbHelper.Str(r["DVT"]),
                        SoLuong = ReadDecimal(r["SOLUONG"]),
                        DonGia = ReadDecimal(r["DONGIA"]),
                        ThanhTien = ReadDecimal(r["THANHTIEN"]),
                        TimeCreated = ReadDateTime(r["TIMECREATED"])
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

        private static int? ReadInt(object v)
        {
            return v == null || v is DBNull ? (int?)null : Convert.ToInt32(v);
        }
    }
}
