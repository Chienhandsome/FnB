using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc cho màn "Kiểm soát order" (docs/specs/kiem-soat-order.md §2, §3 #5).
    /// Đọc hóa đơn bán TDONHANG.LOAI = 0, STATUS = 30 có SOORDER không rỗng trong khoảng [fromDate, toDate]
    /// (biên đóng, gồm cả hóa đơn đã thanh toán và chưa thanh toán).
    /// Người không phải admin chỉ thấy hóa đơn thuộc cửa hàng được gán trong TNGUOIDUNGTHEOCUAHANG.
    /// Không bao giờ ghi vào CSDL.
    /// </summary>
    public sealed class OrderControlRepository
    {
        private readonly string _databasePath;

        public OrderControlRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>Ngày hiện tại theo máy chủ Firebird (CURRENT_TIMESTAMP).</summary>
        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null, "SELECT CURRENT_TIMESTAMP FROM RDB$DATABASE");
                return v == null || v is DBNull ? DbHelper.Now().Date : Convert.ToDateTime(v).Date;
            });
        }

        /// <summary>
        /// Tải danh sách hóa đơn bán có số order giấy (SOORDER khác rỗng) trong khoảng [fromDate, toDate] biên đóng.
        /// Không lọc DATHANHTOAN (như spec §2 / T1); lọc LOAI = 0, STATUS = 30 và giới hạn cửa hàng cho user thường.
        /// </summary>
        public List<OrderControlBillRow> LoadOrderBills(DateTime fromDate, DateTime toDate, string userId, bool isAdmin)
        {
            var scope = new BillReportScope
            {
                FromDate = fromDate.Date,
                ToDate = toDate.Date,
                StoreId = "",
                UserId = userId ?? "",
                IsAdmin = isAdmin
            };

            var conditions = new List<string>(8);
            var args = new List<object>(16);

            conditions.Add("H.LOAI = @OcLoai");
            args.Add("@OcLoai");
            args.Add(OrderControlConstants.SaleOrderLoai);

            conditions.Add("H.STATUS = @OcStatus");
            args.Add("@OcStatus");
            args.Add(OrderControlConstants.ActiveStatus);

            conditions.Add("H.SOORDER IS NOT NULL AND TRIM(H.SOORDER) <> ''");

            BillReportScopeSql.AppendDateAndStoreWhere("H", scope, conditions, args, "Oc");

            string sql =
                "SELECT H.ID, H.NAME, H.NGAY, H.SOORDER " +
                "FROM TDONHANG H " +
                "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                "ORDER BY H.NGAY, H.NAME, H.ID";

            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
                var list = new List<OrderControlBillRow>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    DateTime billDate = ReadDate(r["NGAY"], fromDate.Date);
                    list.Add(new OrderControlBillRow
                    {
                        BillId = DbHelper.Str(r["ID"]) ?? "",
                        BillNumber = DbHelper.Str(r["NAME"]) ?? "",
                        BillDate = billDate,
                        OrderNumbers = DbHelper.Str(r["SOORDER"]) ?? ""
                    });
                }
                return list;
            });
        }

        private static DateTime ReadDate(object value, DateTime fallback)
        {
            if (value == null || value is DBNull)
                return fallback;
            if (value is DateTime)
                return (DateTime)value;
            return Convert.ToDateTime(value);
        }
    }
}
