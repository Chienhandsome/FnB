using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Truy vấn chỉ đọc dữ liệu mặt hàng (DMATHANG + các bảng danh mục liên kết) theo danh sách ID
    /// phục vụ dựng dữ liệu in mã vạch. Mọi câu SQL đều tham số hóa, không ghi CSDL.
    /// Quy tắc N3: khi <c>includeCostColumns</c> là false, câu SQL KHÔNG đọc các cột GIANHAP, GIAVON, HOAHONG.
    /// </summary>
    public sealed class BarcodePrintRepository
    {
        private readonly string _databasePath;

        public string LastExecutedSql { get; private set; }

        public BarcodePrintRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
        }

        /// <summary>
        /// Đọc lại thông tin mặt hàng đang hoạt động (STATUS = 30) theo danh sách ID.
        /// Mặt hàng đã xóa hoặc không còn hoạt động sẽ không có trong từ điển trả về.
        /// </summary>
        public Dictionary<string, BarcodeProductRecord> LoadProductsByIds(IEnumerable<string> productIds, bool includeCostColumns)
        {
            var result = new Dictionary<string, BarcodeProductRecord>(StringComparer.Ordinal);
            List<string> distinct = CollectDistinctIds(productIds);
            if (distinct.Count == 0)
            {
                LastExecutedSql = "";
                return result;
            }

            string lastSql = "";
            DbHelper.Read(_databasePath, conn =>
            {
                bool hasManufacturer = CatalogRepository.TableExists(conn, "DHANGSANXUAT");
                bool hasConsignment = CatalogRepository.TableExists(conn, "DDOITACKYGUI");

                const int chunkSize = 200;
                for (int start = 0; start < distinct.Count; start += chunkSize)
                {
                    int len = Math.Min(chunkSize, distinct.Count - start);
                    var placeholders = new string[len];
                    var args = new object[(len + 1) * 2];
                    for (int i = 0; i < len; i++)
                    {
                        string paramName = "@Id" + i;
                        placeholders[i] = paramName;
                        args[i * 2] = paramName;
                        args[i * 2 + 1] = distinct[start + i];
                    }
                    args[len * 2] = "@Status";
                    args[len * 2 + 1] = PrintConstants.ActiveStatus;

                    string sql = BuildSelectSql(includeCostColumns, hasManufacturer, hasConsignment, placeholders);
                    lastSql = sql;
                    DataTable dt = DbHelper.Table(conn, null, sql, args);
                    foreach (DataRow r in dt.Rows)
                    {
                        BarcodeProductRecord rec = MapRow(r, includeCostColumns);
                        if (!string.IsNullOrEmpty(rec.Id))
                            result[rec.Id] = rec;
                    }
                }
                return 0;
            });

            LastExecutedSql = lastSql;
            return result;
        }

        /// <summary>Dựng câu SELECT tham số hóa; khi includeCostColumns=false không đưa GIANHAP/GIAVON/HOAHONG vào SQL.</summary>
        public static string BuildSelectSql(
            bool includeCostColumns,
            bool hasManufacturerTable,
            bool hasConsignmentTable,
            IList<string> idParamNames)
        {
            var sb = new StringBuilder();
            sb.Append("SELECT ");
            sb.Append("M.ID, M.CODE, M.NAME, M.NOTE, ");
            sb.Append("M.GIABAN, M.GIABAN2, M.GIABAN3, M.GIABAN4, ");
            if (includeCostColumns)
                sb.Append("M.GIANHAP, M.GIAVON, M.HOAHONG, ");
            sb.Append("M.TONTOITHIEU, M.TONTOIDA, M.BAOHANH, M.MASANCO, ");
            sb.Append("M.QUYDOI, M.GIABANCHAN, M.MACDINHGIAMGIA, M.MACDINHGIAMTIEN, ");
            sb.Append("G.NAME AS DNHOMMATHANG_NAME, ");
            sb.Append("U.NAME AS DDONVITINH_NAME, ");
            sb.Append("UB.NAME AS DDONVITINHCHANID_NAME, ");
            sb.Append(hasManufacturerTable ? "HSX.NAME AS DHANGSANXUAT_NAME, " : "CAST('' AS VARCHAR(1)) AS DHANGSANXUAT_NAME, ");
            sb.Append(hasConsignmentTable ? "DT.NAME AS DDOITACKYGUI_NAME " : "CAST('' AS VARCHAR(1)) AS DDOITACKYGUI_NAME ");

            sb.Append("FROM DMATHANG M ");
            sb.Append("LEFT JOIN DNHOMMATHANG G ON G.ID = M.DNHOMMATHANGID ");
            sb.Append("LEFT JOIN DDONVITINH U ON U.ID = M.DDONVITINHID ");
            sb.Append("LEFT JOIN DDONVITINH UB ON UB.ID = M.DDONVITINHCHANID ");
            if (hasManufacturerTable)
                sb.Append("LEFT JOIN DHANGSANXUAT HSX ON HSX.ID = M.DHANGSANXUATID ");
            if (hasConsignmentTable)
                sb.Append("LEFT JOIN DDOITACKYGUI DT ON DT.ID = M.DDOITACKYGUIID ");

            sb.Append("WHERE M.STATUS = @Status AND M.ID IN (");
            for (int i = 0; i < idParamNames.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(idParamNames[i]);
            }
            sb.Append(")");
            return sb.ToString();
        }

        private static BarcodeProductRecord MapRow(DataRow r, bool includeCostColumns)
        {
            return new BarcodeProductRecord
            {
                Id = DbHelper.Str(r["ID"]) ?? "",
                Code = DbHelper.Str(r["CODE"]) ?? "",
                Name = DbHelper.Str(r["NAME"]) ?? "",
                Note = DbHelper.Str(r["NOTE"]) ?? "",
                GroupName = DbHelper.Str(r["DNHOMMATHANG_NAME"]) ?? "",
                UnitName = DbHelper.Str(r["DDONVITINH_NAME"]) ?? "",
                SellPrice = DbHelper.Dec(r["GIABAN"]),
                SellPrice2 = DbHelper.Dec(r["GIABAN2"]),
                SellPrice3 = DbHelper.Dec(r["GIABAN3"]),
                SellPrice4 = DbHelper.Dec(r["GIABAN4"]),
                BuyPrice = includeCostColumns ? DbHelper.Dec(r["GIANHAP"]) : 0m,
                CostPrice = includeCostColumns ? DbHelper.Dec(r["GIAVON"]) : 0m,
                Commission = includeCostColumns ? DbHelper.Dec(r["HOAHONG"]) : 0m,
                MinStock = DbHelper.Dec(r["TONTOITHIEU"]),
                MaxStock = DbHelper.Dec(r["TONTOIDA"]),
                Warranty = DbHelper.Str(r["BAOHANH"]) ?? "",
                Barcode = DbHelper.Str(r["MASANCO"]) ?? "",
                ManufacturerName = DbHelper.Str(r["DHANGSANXUAT_NAME"]) ?? "",
                BigUnitName = DbHelper.Str(r["DDONVITINHCHANID_NAME"]) ?? "",
                QuyDoi = DbHelper.Dec(r["QUYDOI"]),
                BigSellPrice = DbHelper.Dec(r["GIABANCHAN"]),
                ConsignmentName = DbHelper.Str(r["DDOITACKYGUI_NAME"]) ?? "",
                DefaultDiscountRate = DbHelper.Dec(r["MACDINHGIAMGIA"]),
                DefaultDiscountAmount = DbHelper.Dec(r["MACDINHGIAMTIEN"])
            };
        }

        private static List<string> CollectDistinctIds(IEnumerable<string> ids)
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (ids == null)
                return list;
            foreach (string raw in ids)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                string id = raw.Trim();
                if (seen.Add(id))
                    list.Add(id);
            }
            return list;
        }
    }
}
