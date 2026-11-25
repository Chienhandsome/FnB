using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc cho báo cáo "Tổng hợp kết quả kinh doanh" (docs/specs/tong-hop-ket-qua-kinh-doanh.md).
    /// Mọi câu SQL tham số hóa; dùng <see cref="BillReportScopeSql.AppendDateAndStoreWhere"/> độc lập cho từng kỳ
    /// để các bản ghi nằm trong phần giao của hai kỳ (C3) được cộng vào cả hai kỳ.
    /// </summary>
    public sealed class BusinessResultRepository
    {
        private readonly string _databasePath;

        public BusinessResultRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>Ngày hiện tại theo máy chủ CSDL Firebird (CURRENT_TIMESTAMP).</summary>
        public DateTime GetServerDate()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                object v = DbHelper.Scalar(conn, null, "SELECT CURRENT_TIMESTAMP FROM RDB$DATABASE");
                return v == null || v is DBNull ? DbHelper.Now().Date : Convert.ToDateTime(v).Date;
            });
        }

        /// <summary>
        /// Danh sách cửa hàng được phép cho combo lọc: admin thấy tất cả cửa hàng đang dùng;
        /// user thường chỉ thấy cửa hàng gắn trong TNGUOIDUNGTHEOCUAHANG.
        /// </summary>
        public List<StoreRecord> LoadAllowedStores(string userId, bool isAdmin)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt;
                if (isAdmin)
                {
                    dt = DbHelper.Table(conn, null,
                        "SELECT ID, NAME FROM DCUAHANG WHERE STATUS = @S ORDER BY NAME",
                        "@S", BillReportScope.ActiveStatus);
                }
                else
                {
                    if (string.IsNullOrEmpty(userId))
                        return new List<StoreRecord>();
                    dt = DbHelper.Table(conn, null,
                        "SELECT DISTINCT C.ID, C.NAME " +
                        "FROM TNGUOIDUNGTHEOCUAHANG T " +
                        "INNER JOIN DCUAHANG C ON C.ID = T.DCUAHANGID " +
                        "WHERE T.SUSERID = @UserId AND C.STATUS = @S " +
                        "ORDER BY C.NAME",
                        "@UserId", userId,
                        "@S", BillReportScope.ActiveStatus);
                }

                var list = new List<StoreRecord>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new StoreRecord
                    {
                        Id = DbHelper.Str(r["ID"]) ?? "",
                        Name = DbHelper.Str(r["NAME"]) ?? ""
                    });
                }
                return list;
            });
        }

        /// <summary>
        /// Tải toàn bộ số liệu hai kỳ (kỳ này và kỳ trước) trong một phiên kết nối chỉ đọc.
        /// Hai kỳ luôn truy vấn độc lập nhau (không dùng CASE WHEN ... ELSE).
        /// </summary>
        public BusinessResultRawData LoadRawData(
            BusinessResultFilter filter,
            BusinessResultPeriodRange previousPeriod)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            if (previousPeriod == null)
                throw new ArgumentNullException("previousPeriod");

            DateTime curFrom = filter.FromDate.Date;
            DateTime curTo = filter.ToDate.Date;
            DateTime prevFrom = previousPeriod.FromDate.Date;
            DateTime prevTo = previousPeriod.ToDate.Date;

            BillReportScope curScope = filter.ToBillScope(curFrom, curTo);
            BillReportScope prevScope = filter.ToBillScope(prevFrom, prevTo);

            return DbHelper.Read(_databasePath, conn =>
            {
                BusinessResultBillTotals curBills = LoadBillTotals(conn, curScope, "CurB");
                BusinessResultBillTotals prevBills = LoadBillTotals(conn, prevScope, "PrevB");

                Dictionary<string, decimal> curRevGroups = LoadRevenueGroupsForPeriod(conn, curScope, "CurRg");
                Dictionary<string, decimal> prevRevGroups = LoadRevenueGroupsForPeriod(conn, prevScope, "PrevRg");
                List<BusinessResultNamedAmount> revGroups = MergeNonZeroGroups(curRevGroups, prevRevGroups);

                Dictionary<string, decimal> curVarGroups = LoadVariableCostGroupsForPeriod(conn, curScope, "CurVg");
                Dictionary<string, decimal> prevVarGroups = LoadVariableCostGroupsForPeriod(conn, prevScope, "PrevVg");
                List<BusinessResultNamedAmount> varGroups = MergeNonZeroGroups(curVarGroups, prevVarGroups);

                List<BusinessResultNamedAmount> fixedReasons;
                List<BusinessResultNamedAmount> otherReasons;
                LoadExpenseReasons(conn, curScope, prevScope, out fixedReasons, out otherReasons);

                return new BusinessResultRawData
                {
                    CurrentPeriod = new BusinessResultPeriodRange { FromDate = curFrom, ToDate = curTo },
                    PreviousPeriod = new BusinessResultPeriodRange { FromDate = prevFrom, ToDate = prevTo },
                    CurrentBills = curBills,
                    PreviousBills = prevBills,
                    RevenueGroups = revGroups,
                    VariableCostGroups = varGroups,
                    FixedCostReasons = fixedReasons,
                    OtherCostReasons = otherReasons
                };
            });
        }

        /// <summary>
        /// Tổng bill trong một kỳ:
        /// ((LOAI = 0 AND DATHANHTOAN = 30) OR LOAI = 5 OR LOAI = 6) AND STATUS = 30.
        /// </summary>
        private static BusinessResultBillTotals LoadBillTotals(FbConnection conn, BillReportScope scope, string prefix)
        {
            var conditions = new List<string>(8);
            var args = new List<object>(16);

            AppendBillTypeWith56AndStatus("H", conditions, args, prefix);
            BillReportScopeSql.AppendDateAndStoreWhere("H", scope, conditions, args, prefix);

            string sql =
                "SELECT " +
                "COALESCE(SUM(COALESCE(H.TONGCONG, 0)), 0) AS DOANHTHU, " +
                "COALESCE(SUM(COALESCE(H.TIENGIAMGIATONG, 0)), 0) AS GIAMGIA, " +
                "COALESCE(SUM(COALESCE(H.PHIDICHVU, 0)), 0) AS PHIDICHVU, " +
                "COALESCE(SUM(COALESCE(H.TIENTHUE, 0)), 0) AS THUE " +
                "FROM TDONHANG H " +
                "WHERE " + string.Join(" AND ", conditions.ToArray());

            DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
            if (dt.Rows.Count == 0)
                return new BusinessResultBillTotals();

            DataRow r = dt.Rows[0];
            return new BusinessResultBillTotals
            {
                Revenue = DbHelper.Dec(r["DOANHTHU"]),
                Discount = DbHelper.Dec(r["GIAMGIA"]),
                ServiceFee = DbHelper.Dec(r["PHIDICHVU"]),
                Tax = DbHelper.Dec(r["THUE"])
            };
        }

        /// <summary>
        /// Nhóm doanh thu trong một kỳ: SUM(TDONHANGCHITIET.THANHTIEN) chỉ bill LOAI = 0 AND DATHANHTOAN = 30 AND STATUS = 30.
        /// KHÔNG lọc XUATVATTU hay COMBOPARENTID. INNER JOIN DMATHANG, LEFT JOIN DNHOMMATHANG.
        /// </summary>
        private static Dictionary<string, decimal> LoadRevenueGroupsForPeriod(
            FbConnection conn,
            BillReportScope scope,
            string prefix)
        {
            var conditions = new List<string>(8);
            var args = new List<object>(16);

            string pL0 = "@" + prefix + "L0";
            string pPaid = "@" + prefix + "Paid";
            string pSt = "@" + prefix + "St";

            conditions.Add("H.LOAI = " + pL0);
            args.Add(pL0);
            args.Add(BillReportScope.SaleOrderLoai);

            conditions.Add("H.DATHANHTOAN = " + pPaid);
            args.Add(pPaid);
            args.Add(BillReportScope.PaidFlagValue);

            conditions.Add("H.STATUS = " + pSt);
            args.Add(pSt);
            args.Add(BillReportScope.ActiveStatus);

            BillReportScopeSql.AppendDateAndStoreWhere("H", scope, conditions, args, prefix);

            string sql =
                "SELECT N.NAME AS NHOM, " +
                "COALESCE(SUM(COALESCE(D.THANHTIEN, 0)), 0) AS TONGTIEN " +
                "FROM TDONHANGCHITIET D " +
                "INNER JOIN TDONHANG H ON H.ID = D.TDONHANGID " +
                "INNER JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                "LEFT JOIN DNHOMMATHANG N ON N.ID = M.DNHOMMATHANGID " +
                "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                "GROUP BY N.NAME";

            return ReadGroupMap(conn, sql, args);
        }

        /// <summary>
        /// Biến phí theo nhóm trong một kỳ:
        /// SUM((COALESCE(SLXUAT,0) - COALESCE(SLNHAP,0)) * COALESCE(D.GIAVON,0));
        /// bill ((LOAI = 0 AND DATHANHTOAN = 30) OR LOAI = 5 OR LOAI = 6) AND STATUS = 30;
        /// loại xuất vật tư (COALESCE(XUATVATTU,0) = 0) và combo con (COALESCE(COMBOPARENTID,'') = '').
        /// </summary>
        private static Dictionary<string, decimal> LoadVariableCostGroupsForPeriod(
            FbConnection conn,
            BillReportScope scope,
            string prefix)
        {
            var conditions = new List<string>(10);
            var args = new List<object>(20);

            AppendBillTypeWith56AndStatus("H", conditions, args, prefix);
            conditions.Add("COALESCE(D.XUATVATTU, 0) = 0");
            conditions.Add("COALESCE(D.COMBOPARENTID, '') = ''");
            BillReportScopeSql.AppendDateAndStoreWhere("H", scope, conditions, args, prefix);

            string sql =
                "SELECT N.NAME AS NHOM, " +
                "COALESCE(SUM((COALESCE(D.SLXUAT, 0) - COALESCE(D.SLNHAP, 0)) * COALESCE(D.GIAVON, 0)), 0) AS TONGTIEN " +
                "FROM TDONHANGCHITIET D " +
                "INNER JOIN TDONHANG H ON H.ID = D.TDONHANGID " +
                "INNER JOIN DMATHANG M ON M.ID = D.DMATHANGID " +
                "LEFT JOIN DNHOMMATHANG N ON N.ID = M.DNHOMMATHANGID " +
                "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                "GROUP BY N.NAME";

            return ReadGroupMap(conn, sql, args);
        }

        /// <summary>
        /// Định phí (LOAILYDO = 1) và Chi khác (LOAILYDO = 2):
        /// đi từ DLYDOTHUCHI (không ORDER BY, không lọc thêm để giữ nguyên thứ tự gốc và hiện đủ cả 0/0);
        /// mỗi kỳ cộng SUM(COALESCE(TTHUCHI.CHI, 0)) độc lập theo DLYDOTHUCHIID, không lọc STATUS/LOAI/LATAMUNG,
        /// áp lọc ngày + cửa hàng + ràng buộc user cho TTHUCHI.DCUAHANGID.
        /// </summary>
        private static void LoadExpenseReasons(
            FbConnection conn,
            BillReportScope curScope,
            BillReportScope prevScope,
            out List<BusinessResultNamedAmount> fixedReasons,
            out List<BusinessResultNamedAmount> otherReasons)
        {
            DataTable dtReasons = DbHelper.Table(conn, null,
                "SELECT L.ID, L.NAME, L.LOAILYDO " +
                "FROM DLYDOTHUCHI L " +
                "WHERE L.LOAILYDO = @L1 OR L.LOAILYDO = @L2",
                "@L1", BusinessResultConstants.ReasonLoaiFixedCost,
                "@L2", BusinessResultConstants.ReasonLoaiOtherCost);

            Dictionary<string, decimal> curMap = LoadExpenseTotalsForPeriod(conn, curScope, "CurTc");
            Dictionary<string, decimal> prevMap = LoadExpenseTotalsForPeriod(conn, prevScope, "PrevTc");

            fixedReasons = new List<BusinessResultNamedAmount>();
            otherReasons = new List<BusinessResultNamedAmount>();

            foreach (DataRow r in dtReasons.Rows)
            {
                string id = DbHelper.Str(r["ID"]) ?? "";
                string name = DbHelper.Str(r["NAME"]) ?? "";
                int loaiLyDo = DbHelper.Int(r["LOAILYDO"]);

                decimal curVal;
                curMap.TryGetValue(id, out curVal);
                decimal prevVal;
                prevMap.TryGetValue(id, out prevVal);

                var item = new BusinessResultNamedAmount
                {
                    Id = id,
                    Name = name,
                    CurrentAmount = curVal,
                    PreviousAmount = prevVal
                };

                if (loaiLyDo == BusinessResultConstants.ReasonLoaiFixedCost)
                    fixedReasons.Add(item);
                else if (loaiLyDo == BusinessResultConstants.ReasonLoaiOtherCost)
                    otherReasons.Add(item);
            }
        }

        private static Dictionary<string, decimal> LoadExpenseTotalsForPeriod(
            FbConnection conn,
            BillReportScope scope,
            string prefix)
        {
            var conditions = new List<string>(8);
            var args = new List<object>(16);

            string pL1 = "@" + prefix + "LyDo1";
            string pL2 = "@" + prefix + "LyDo2";
            conditions.Add("(L.LOAILYDO = " + pL1 + " OR L.LOAILYDO = " + pL2 + ")");
            args.Add(pL1);
            args.Add(BusinessResultConstants.ReasonLoaiFixedCost);
            args.Add(pL2);
            args.Add(BusinessResultConstants.ReasonLoaiOtherCost);

            BillReportScopeSql.AppendDateAndStoreWhere("T", scope, conditions, args, prefix);

            string sql =
                "SELECT T.DLYDOTHUCHIID, " +
                "COALESCE(SUM(COALESCE(T.CHI, 0)), 0) AS TONGCHI " +
                "FROM TTHUCHI T " +
                "INNER JOIN DLYDOTHUCHI L ON L.ID = T.DLYDOTHUCHIID " +
                "WHERE " + string.Join(" AND ", conditions.ToArray()) + " " +
                "GROUP BY T.DLYDOTHUCHIID";

            DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
            var map = new Dictionary<string, decimal>(dt.Rows.Count, StringComparer.Ordinal);
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["DLYDOTHUCHIID"]) ?? "";
                if (id.Length == 0)
                    continue;
                decimal val = DbHelper.Dec(r["TONGCHI"]);
                decimal existing;
                map[id] = map.TryGetValue(id, out existing) ? existing + val : val;
            }
            return map;
        }

        private static void AppendBillTypeWith56AndStatus(
            string alias,
            List<string> conditions,
            List<object> args,
            string prefix)
        {
            string pL0 = "@" + prefix + "L0";
            string pPaid = "@" + prefix + "Paid";
            string pL5 = "@" + prefix + "L5";
            string pL6 = "@" + prefix + "L6";
            string pSt = "@" + prefix + "St";

            conditions.Add(
                "((" + alias + ".LOAI = " + pL0 + " AND " + alias + ".DATHANHTOAN = " + pPaid + ") " +
                "OR " + alias + ".LOAI = " + pL5 + " " +
                "OR " + alias + ".LOAI = " + pL6 + ")");
            args.Add(pL0);
            args.Add(BillReportScope.SaleOrderLoai);
            args.Add(pPaid);
            args.Add(BillReportScope.PaidFlagValue);
            args.Add(pL5);
            args.Add(BusinessResultConstants.BillLoai5);
            args.Add(pL6);
            args.Add(BusinessResultConstants.BillLoai6);

            conditions.Add(alias + ".STATUS = " + pSt);
            args.Add(pSt);
            args.Add(BillReportScope.ActiveStatus);
        }

        private static Dictionary<string, decimal> ReadGroupMap(
            FbConnection conn,
            string sql,
            List<object> args)
        {
            DataTable dt = DbHelper.Table(conn, null, sql, args.ToArray());
            var map = new Dictionary<string, decimal>(dt.Rows.Count, StringComparer.Ordinal);
            foreach (DataRow r in dt.Rows)
            {
                string rawName = r["NHOM"] == null || r["NHOM"] is DBNull ? null : Convert.ToString(r["NHOM"]);
                string name = string.IsNullOrEmpty(rawName) ? BusinessResultConstants.UngroupedName : rawName;
                decimal val = DbHelper.Dec(r["TONGTIEN"]);
                decimal existing;
                map[name] = map.TryGetValue(name, out existing) ? existing + val : val;
            }
            return map;
        }

        private static List<BusinessResultNamedAmount> MergeNonZeroGroups(
            Dictionary<string, decimal> curMap,
            Dictionary<string, decimal> prevMap)
        {
            var keys = new HashSet<string>(curMap.Keys, StringComparer.Ordinal);
            foreach (string k in prevMap.Keys)
                keys.Add(k);

            var names = new List<string>(keys);
            names.Sort(StringComparer.Ordinal);

            var list = new List<BusinessResultNamedAmount>(names.Count);
            foreach (string name in names)
            {
                decimal curVal;
                curMap.TryGetValue(name, out curVal);
                decimal prevVal;
                prevMap.TryGetValue(name, out prevVal);
                if (curVal == 0m && prevVal == 0m)
                    continue;
                list.Add(new BusinessResultNamedAmount
                {
                    Name = name,
                    CurrentAmount = curVal,
                    PreviousAmount = prevVal
                });
            }
            return list;
        }
    }
}
