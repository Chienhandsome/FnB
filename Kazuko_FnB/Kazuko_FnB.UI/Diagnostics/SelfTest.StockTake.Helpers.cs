using System;
using System.Collections.Generic;
using System.Data;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static bool VerifyStockTakeCostFormulaParticipation(
            string db, PurchaseService purSvc, StockTakeService kkSvc,
            string whId, string prodId, string unitId, string tag, List<string> createdOrderIds)
        {
            PurchaseSaveResult p = purSvc.Save(new PurchaseRow
            {
                Name = "PN-GVKK-" + tag,
                Date = DateTime.Today,
                WarehouseId = whId,
                Lines = new List<PurchaseLine>
                {
                    new PurchaseLine { ProductId = prodId, ProductName = "Hạt Cà Phê " + tag, UnitId = unitId, BaseUnitId = unitId, RawQuantity = 10m, UnitPrice = 10000m }
                }
            });
            if (!p.Ok) return false;
            createdOrderIds.Add(p.SavedId);

            // Kiểm kê #1: tồn 10, thực tế 7 -> SLXUAT=3.
            var kkShort = new StockTakeRow
            {
                Name = "KK-GV1-" + tag, Date = DateTime.Today, WarehouseId = whId,
                Lines = new List<StockTakeLine>
                {
                    new StockTakeLine { ProductId = prodId, ProductName = "Hạt Cà Phê " + tag, UnitId = unitId, BaseUnitId = unitId, SystemQuantity = 10m, RawActualQuantity = 7m }
                }
            };
            StockTakeSaveResult rShort = kkSvc.Save(kkShort);
            if (!rShort.Ok) return false;
            createdOrderIds.Add(rShort.SavedId);

            // Kiểm kê #2: tồn 7, thực tế 9 -> SLNHAP=2.
            var kkSurp = new StockTakeRow
            {
                Name = "KK-GV2-" + tag, Date = DateTime.Today, WarehouseId = whId,
                Lines = new List<StockTakeLine>
                {
                    new StockTakeLine { ProductId = prodId, ProductName = "Hạt Cà Phê " + tag, UnitId = unitId, BaseUnitId = unitId, SystemQuantity = 7m, RawActualQuantity = 9m }
                }
            };
            StockTakeSaveResult rSurp = kkSvc.Save(kkSurp);
            if (!rSurp.Ok) return false;
            createdOrderIds.Add(rSurp.SavedId);

            // Chạy đúng công thức Tính lại giá vốn theo tháng (docs/specs/tinh-lai-gia-von.md §2 bước 3) trên các dòng DB vừa lưu.
            DataTable dt = DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                "SELECT COALESCE(H.LOAI, 0) AS LOAI, COALESCE(C.SLNHAP, 0) AS SLNHAP, " +
                "COALESCE(C.SLXUAT, 0) AS SLXUAT, COALESCE(C.THANHTIEN, 0) AS THANHTIEN " +
                "FROM TDONHANGCHITIET C INNER JOIN TDONHANG H ON H.ID = C.TDONHANGID " +
                "WHERE C.DMATHANGID = @P AND COALESCE(H.STATUS, 0) = 30 AND COALESCE(C.STATUS, 0) = 30 " +
                "ORDER BY H.NGAY, H.TIMECREATED, C.TIMECREATED",
                "@P", prodId));

            decimal ton = 0m, giaTri = 0m;
            foreach (DataRow r in dt.Rows)
            {
                int loai = DbHelper.Int(r["LOAI"]);
                if (loai == 1 || loai == 97)
                {
                    ton += DbHelper.Dec(r["SLNHAP"]);
                    giaTri += DbHelper.Dec(r["THANHTIEN"]);
                }
            }
            decimal giaVon = ton == 0m ? 0m : Math.Round(giaTri / ton, 2, MidpointRounding.AwayFromZero);
            decimal tonAfterShort = -1m, giaTriAfterShort = -1m;
            int kkSeen = 0;
            foreach (DataRow r in dt.Rows)
            {
                int loai = DbHelper.Int(r["LOAI"]);
                if (loai != 1 && loai != 97)
                {
                    decimal delta = DbHelper.Dec(r["SLXUAT"]) - DbHelper.Dec(r["SLNHAP"]);
                    ton -= delta;
                    giaTri -= delta * giaVon;
                    if (loai == 4)
                    {
                        kkSeen++;
                        if (kkSeen == 1)
                        {
                            tonAfterShort = ton;
                            giaTriAfterShort = giaTri;
                        }
                    }
                }
            }
            return giaVon == 10000m && kkSeen == 2
                && tonAfterShort == 7m && giaTriAfterShort == 70000m
                && ton == 9m && giaTri == 90000m;
        }

        private static DataTable ReadStockTakeDetailRows(string db, string orderId)
        {
            return DbHelper.Read(db, conn => DbHelper.Table(conn, null,
                "SELECT DMATHANGID, COALESCE(SLHETHONG, 0) AS SLHETHONG, COALESCE(SLTHUCTE, 0) AS SLTHUCTE, " +
                "COALESCE(SLNHAP, 0) AS SLNHAP, COALESCE(SLNHAPCHUAQUYDOI, 0) AS SLNHAPRAW, " +
                "COALESCE(SLXUAT, 0) AS SLXUAT, COALESCE(SLXUATCHUAQUYDOI, 0) AS SLXUATRAW " +
                "FROM TDONHANGCHITIET WHERE TDONHANGID = @O AND COALESCE(STATUS, 0) = 30",
                "@O", orderId));
        }

        private static bool MatchDetailRow(
            DataTable dt, string prodId, decimal sysQty, decimal actualQty,
            decimal slNhap, decimal slNhapRaw, decimal slXuat, decimal slXuatRaw)
        {
            foreach (DataRow r in dt.Rows)
            {
                if (DbHelper.Str(r["DMATHANGID"]) != prodId)
                    continue;
                return DbHelper.Dec(r["SLHETHONG"]) == sysQty
                    && DbHelper.Dec(r["SLTHUCTE"]) == actualQty
                    && DbHelper.Dec(r["SLNHAP"]) == slNhap
                    && DbHelper.Dec(r["SLNHAPRAW"]) == slNhapRaw
                    && DbHelper.Dec(r["SLXUAT"]) == slXuat
                    && DbHelper.Dec(r["SLXUATRAW"]) == slXuatRaw;
            }
            return false;
        }
    }
}
