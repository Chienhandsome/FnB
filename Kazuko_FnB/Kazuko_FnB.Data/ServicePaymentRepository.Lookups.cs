using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    public sealed partial class ServicePaymentRepository
    {
        /// <summary>Danh sách voucher còn hiệu lực và chưa dùng (khi LuaChonVoucherTuDanhSach = 30).</summary>
        public List<VoucherInfo> LoadAvailableVouchers(IList<string> excludeBillIds = null)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DateTime serverDate = InvoiceNumberRepository.GetServerTimestamp(conn, null).Date;
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, COALESCE(GIATRI, 0) AS GIATRI, HANSUDUNG " +
                    "FROM DVOUCHER WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<VoucherInfo>();
                foreach (DataRow r in dt.Rows)
                {
                    VoucherInfo info = BuildValidatedVoucherIn(conn, null, r, serverDate, excludeBillIds);
                    if (info.IsValid)
                        list.Add(info);
                }
                return list;
            });
        }

        public List<LookupItem> LoadBankAccounts()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DTAIKHOANNGANHANG WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        public List<LookupItem> LoadEmployees()
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME FROM DNHANVIEN WHERE STATUS = @S ORDER BY NAME",
                    "@S", ActiveStatus);
                var list = new List<LookupItem>(dt.Rows.Count);
                foreach (DataRow r in dt.Rows)
                    list.Add(new LookupItem { Id = DbHelper.Str(r["ID"]), Name = DbHelper.Str(r["NAME"]) ?? "" });
                return list;
            });
        }

        /// <summary>Tải danh sách bill chưa thanh toán cùng NHOMGUID (sắp BATDAU, NAME, ID).</summary>
        public static List<ServiceOrderHeader> LoadGroupBillsIn(FbConnection conn, FbTransaction tx, string groupGuid)
        {
            var list = new List<ServiceOrderHeader>();
            if (string.IsNullOrEmpty(groupGuid))
                return list;

            DataTable dt = DbHelper.Table(conn, tx,
                "SELECT ID FROM TDONHANG WHERE NHOMGUID = @G AND COALESCE(DATHANHTOAN, 0) = 0 " +
                "AND COALESCE(STATUS, 0) <> @Del ORDER BY BATDAU, NAME, ID",
                "@G", groupGuid, "@Del", DeletedStatus);
            foreach (DataRow r in dt.Rows)
            {
                string id = DbHelper.Str(r["ID"]);
                if (!string.IsNullOrEmpty(id))
                {
                    ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, tx, id);
                    if (bill != null)
                        list.Add(bill);
                }
            }
            return list;
        }

        /// <summary>Danh sách các bàn đang có khách (bill chưa thanh toán, không bị máy khác giữ) cho Thanh toán chung (§3.7.3).</summary>
        public List<BusyTableForGroupItem> LoadBusyTablesForGroup(string currentUserName, bool checkMultiStationLock)
        {
            return DbHelper.Read(_databasePath, conn =>
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT B.ID AS TABLEID, B.NAME AS TABLENAME, A.NAME AS AREANAME, " +
                    "H.ID AS BILLID, H.NAME AS BILLNAME, H.BATDAU, COALESCE(H.TONGCONG, 0) AS TONGCONG, " +
                    "COALESCE(K.NAME, '') AS CUSTNAME, COALESCE(H.NHOMGUID, '') AS NHOMGUID, " +
                    "B.TAIKHOANGIU, " +
                    "CASE WHEN B.TAIKHOANGIU IS NOT NULL AND TRIM(B.TAIKHOANGIU) <> '' " +
                    "AND B.GIULUC > DATEADD(@ExpireSec SECOND TO CURRENT_TIMESTAMP) THEN 1 ELSE 0 END AS LOCKACTIVE " +
                    "FROM DBAN B " +
                    "INNER JOIN DKHUVUC A ON A.ID = B.DKHUVUCID AND A.STATUS = @S " +
                    "INNER JOIN TDONHANG H ON H.ID = B.TDONHANGID AND COALESCE(H.DATHANHTOAN, 0) = 0 AND COALESCE(H.STATUS, 0) <> @Del " +
                    "LEFT JOIN DKHACHHANG K ON K.ID = H.DKHACHHANGID " +
                    "WHERE B.STATUS = @S ORDER BY A.SORTORDER, A.NAME, B.NAME",
                    "@ExpireSec", -ServiceOrderConstants.TableLockExpireSeconds,
                    "@S", ActiveStatus,
                    "@Del", DeletedStatus);

                var list = new List<BusyTableForGroupItem>(dt.Rows.Count);
                string me = (currentUserName ?? "").Trim();
                foreach (DataRow r in dt.Rows)
                {
                    if (checkMultiStationLock && DbHelper.Int(r["LOCKACTIVE"]) == 1)
                    {
                        string holder = (DbHelper.Str(r["TAIKHOANGIU"]) ?? "").Trim();
                        if (holder.Length > 0 && !string.Equals(holder, me, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    list.Add(new BusyTableForGroupItem
                    {
                        TableId = DbHelper.Str(r["TABLEID"]) ?? "",
                        TableName = DbHelper.Str(r["TABLENAME"]) ?? "",
                        AreaName = DbHelper.Str(r["AREANAME"]) ?? "",
                        BillId = DbHelper.Str(r["BILLID"]) ?? "",
                        BillName = DbHelper.Str(r["BILLNAME"]) ?? "",
                        BatDau = ReadDateTime(r["BATDAU"]) ?? DateTime.Now,
                        TongCong = DbHelper.Dec(r["TONGCONG"]),
                        CustomerName = DbHelper.Str(r["CUSTNAME"]) ?? "",
                        ExistingGroupGuid = DbHelper.Str(r["NHOMGUID"]) ?? ""
                    });
                }
                return list;
            });
        }
    }
}
