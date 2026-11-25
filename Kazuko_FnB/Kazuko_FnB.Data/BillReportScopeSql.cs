using System;
using System.Collections.Generic;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Xây điều kiện WHERE chuẩn cho phạm vi hóa đơn bán hàng đã thanh toán trên bảng TDONHANG:
    /// LOAI = 0, DATHANHTOAN = 30, STATUS = 30, NGAY trong [FromDate, ToDate + 1 ngày),
    /// và lọc cửa hàng theo quyền user (TNGUOIDUNGTHEOCUAHANG).
    /// Dùng chung một chỗ cho lưới mặt hàng bán, khối tổng, dialog chi tiết và các báo cáo sau (Thống kê doanh thu).
    /// Mọi giá trị đều truyền bằng tham số SQL, không ghép chuỗi.
    /// </summary>
    public static class BillReportScopeSql
    {
        /// <summary>
        /// Bổ sung điều kiện phạm vi bill vào <paramref name="conditions"/> và cặp tên–giá trị vào <paramref name="args"/>.
        /// <paramref name="alias"/> là bí danh bảng TDONHANG (ví dụ "H").
        /// </summary>
        public static void AppendWhere(
            string alias,
            BillReportScope scope,
            List<string> conditions,
            List<object> args,
            string paramPrefix = "")
        {
            if (string.IsNullOrEmpty(alias))
                throw new ArgumentException("Cần bí danh bảng TDONHANG.", "alias");
            if (scope == null)
                throw new ArgumentNullException("scope");
            if (conditions == null)
                throw new ArgumentNullException("conditions");
            if (args == null)
                throw new ArgumentNullException("args");

            string p = paramPrefix ?? "";
            string pLoai = "@" + p + "BillLoai";
            string pPaid = "@" + p + "BillPaid";
            string pStatus = "@" + p + "BillStatus";
            string pFrom = "@" + p + "BillFrom";
            string pToExcl = "@" + p + "BillToExcl";

            conditions.Add(alias + ".LOAI = " + pLoai);
            args.Add(pLoai);
            args.Add(BillReportScope.SaleOrderLoai);

            conditions.Add(alias + ".DATHANHTOAN = " + pPaid);
            args.Add(pPaid);
            args.Add(BillReportScope.PaidFlagValue);

            conditions.Add(alias + ".STATUS = " + pStatus);
            args.Add(pStatus);
            args.Add(BillReportScope.ActiveStatus);

            conditions.Add(alias + ".NGAY >= " + pFrom);
            args.Add(pFrom);
            args.Add(scope.FromDate.Date);

            conditions.Add(alias + ".NGAY < " + pToExcl);
            args.Add(pToExcl);
            args.Add(scope.ToDate.Date.AddDays(1));

            bool hasSelectedStore = !string.IsNullOrEmpty(scope.StoreId);
            if (hasSelectedStore)
            {
                string pStore = "@" + p + "BillStoreId";
                conditions.Add(alias + ".DCUAHANGID = " + pStore);
                args.Add(pStore);
                args.Add(scope.StoreId);
            }

            if (!scope.IsAdmin)
            {
                // User thường: khi không chọn cửa hàng CHỈ thấy cửa hàng có trong TNGUOIDUNGTHEOCUAHANG;
                // khi có chọn cũng chỉ thấy nếu thuộc cửa hàng của mình. Không có dòng nào → rỗng.
                string pUser = "@" + p + "BillUserId";
                conditions.Add(
                    "EXISTS (SELECT 1 FROM TNGUOIDUNGTHEOCUAHANG UCH " +
                    "WHERE UCH.SUSERID = " + pUser + " AND UCH.DCUAHANGID = " + alias + ".DCUAHANGID)");
                args.Add(pUser);
                args.Add(scope.UserId ?? "");
            }
        }
    }
}
