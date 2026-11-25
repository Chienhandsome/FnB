using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void BusinessResultRepositoryTests(string db, BusinessResultFixture fx)
        {
            var repo = new BusinessResultRepository(db);
            BusinessResultPeriodRange prev = BusinessResultCalculator.CalculatePreviousPeriod(fx.From, fx.To);

            // 1. Admin để trống cửa hàng: kiểm mọi dòng so với SỐ TÍNH TAY cho CẢ kỳ này & kỳ trước!
            BusinessResultRawData adminRaw = repo.LoadRawData(new BusinessResultFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = "", UserId = "selftest", IsAdmin = true
            }, prev);
            List<BusinessResultRow> rows = BusinessResultCalculator.BuildRows(adminRaw);

            BusinessResultRow rDt = FindBrRow(rows, 0, "DOANH THU");
            BusinessResultRow rRevNg = FindBrRow(rows, 1, "(Chưa phân nhóm)");
            BusinessResultRow rRevCf = FindBrRow(rows, 1, "KQKD-01-Cà phê");
            BusinessResultRow rRevTr = FindBrRow(rows, 1, "KQKD-02-Trà");
            BusinessResultRow rRevBk = FindBrRow(rows, 1, "KQKD-03-Bánh cũ");
            BusinessResultRow rRevZ0 = FindBrRow(rows, 1, "KQKD-04-Nhóm 0");
            BusinessResultRow rGg = FindBrRow(rows, 1, "Giảm giá");
            BusinessResultRow rPdv = FindBrRow(rows, 1, "Phí dịch vụ");
            BusinessResultRow rThue = FindBrRow(rows, 1, "Thuế");

            BusinessResultRow rCp = FindBrRow(rows, 2, "CHI PHÍ");
            BusinessResultRow rDp = FindBrRow(rows, 3, "Định phí");
            BusinessResultRow rDp1 = FindBrRow(rows, 4, fx.FixedReason1Name);
            BusinessResultRow rDp2 = FindBrRow(rows, 4, fx.FixedReason2Name);
            BusinessResultRow rBp = FindBrRow(rows, 5, "Biến phí");
            BusinessResultRow rVarNg = FindBrRow(rows, 6, "(Chưa phân nhóm)");
            BusinessResultRow rVarCf = FindBrRow(rows, 6, "KQKD-01-Cà phê");
            BusinessResultRow rVarTr = FindBrRow(rows, 6, "KQKD-02-Trà");
            BusinessResultRow rVarBk = FindBrRow(rows, 6, "KQKD-03-Bánh cũ");
            BusinessResultRow rVarZ0 = FindBrRow(rows, 6, "KQKD-04-Nhóm 0");
            BusinessResultRow rCk = FindBrRow(rows, 7, "Chi khác");
            BusinessResultRow rCk1 = FindBrRow(rows, 8, fx.OtherReason1Name);
            BusinessResultRow rCk2 = FindBrRow(rows, 8, fx.OtherReason2Name);
            BusinessResultRow rPl = FindBrRow(rows, 9, "LÃI/LỖ");

            bool revOk = MatchesPair(rDt, 2200000m, 2710000m)
                && MatchesPair(rRevNg, 200000m, 200000m)
                && MatchesPair(rRevCf, 1090000m, 350000m)
                && MatchesPair(rRevTr, 390000m, 2000000m)
                && MatchesPair(rRevBk, 0m, 150000m)
                && rRevZ0 == null
                && MatchesPair(rGg, 90000m, 35000m)
                && MatchesPair(rPdv, 75000m, 17000m)
                && MatchesPair(rThue, 135000m, 48000m);

            Check("KQKD Repo (§7/§2/C2/Q10/Q11 khối DOANH THU 2 kỳ khớp số tính tay: gồm 31/03 2tr, giờ lẻ 15:30, LOAI 5/6, XVT+combo vào nhóm DT, ẩn nhóm 0/0, giữ nhóm chỉ có kỳ trước, loại bill xấu)",
                revOk,
                rDt == null ? "null" : ("DT=" + rDt.CurrentValue + "/" + rDt.PreviousValue + ", CF=" + (rRevCf == null ? 0 : rRevCf.CurrentValue) + ", TR=" + (rRevTr == null ? 0 : rRevTr.CurrentValue) + "/" + (rRevTr == null ? 0 : rRevTr.PreviousValue)));

            int fixedDetailCount = rows.FindAll(r => r.Loai == 4).Count;
            int otherDetailCount = rows.FindAll(r => r.Loai == 8).Count;
            // DP = 500k + 250k = 750k (trước 500k + 120k = 620k); BP = 614k (trước 720k);
            // CK = 200k + 80k = 280k (trước 130k + 160k = 290k); CP = 1.644k (trước 1.630k);
            // LÃI/LỖ = 2.200k - 1.644k = 556k (trước 2.710k - 1.630k = 1.080k; tăng giảm = -524k ▼).
            bool costAndPlOk = MatchesPair(rCp, 1644000m, 1630000m)
                && MatchesPair(rDp, 750000m, 620000m)
                && MatchesPair(rDp1, 500000m, 500000m)
                && MatchesPair(rDp2, 250000m, 120000m)
                && fixedDetailCount == fx.FixedReasonTotalCount && fixedDetailCount >= 8
                && MatchesPair(rBp, 614000m, 720000m)
                && MatchesPair(rVarNg, 60000m, 70000m)
                && MatchesPair(rVarCf, 380000m, 120000m)
                && MatchesPair(rVarTr, 174000m, 500000m)
                && MatchesPair(rVarBk, 0m, 30000m)
                && rVarZ0 == null
                && MatchesPair(rCk, 280000m, 290000m)
                && MatchesPair(rCk1, 200000m, 130000m)
                && MatchesPair(rCk2, 80000m, 160000m)
                && otherDetailCount == fx.OtherReasonTotalCount && otherDetailCount >= 7
                && MatchesPair(rPl, 556000m, 1080000m)
                && rPl.ChangeAmount == -524000m && rPl.TrendSymbol == "▼";

            Check("KQKD Repo (§7/§2/Q15 khối CHI PHÍ & LÃI/LỖ 2 kỳ khớp số tính tay: biến phí (SLXUAT-SLNHAP)*D.GIAVON loại XVT/combo, gồm LOAI 5/6, đủ lý do LOAILYDO 1/2 kể cả 0/0, loại lý do loại khác)",
                costAndPlOk,
                "CP=" + (rCp == null ? 0 : rCp.CurrentValue) + "/" + (rCp == null ? 0 : rCp.PreviousValue)
                + ", BP=" + (rBp == null ? 0 : rBp.CurrentValue) + "/" + (rBp == null ? 0 : rBp.PreviousValue)
                + ", DPcnt=" + fixedDetailCount + ", CKcnt=" + otherDetailCount
                + ", PL=" + (rPl == null ? 0 : rPl.CurrentValue) + "/" + (rPl == null ? 0 : rPl.PreviousValue));

            // Kiểm thứ tự LOAI tăng dần 0..9 và không có dòng Tiền giờ.
            bool loaiAscOk = rows.Count > 0 && !rows.Exists(r => r.Content == "Tiền giờ");
            for (int i = 1; loaiAscOk && i < rows.Count; i++)
                if (rows[i].Loai < rows[i - 1].Loai)
                    loaiAscOk = false;
            Check("KQKD Repo (§7/Q14/C8 thứ tự LOAI tăng dần 0..9, không có Tiền giờ)", loaiAscOk);

            // 2. Cửa hàng: Admin chọn StoreA vs User1Store để trống (đều chỉ thấy StoreA ở cả bill, nhóm, biến phí và phiếu chi!).
            List<BusinessResultRow> adminStoreA = BusinessResultCalculator.BuildRows(repo.LoadRawData(new BusinessResultFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = fx.StoreAId, UserId = "selftest", IsAdmin = true
            }, prev));
            List<BusinessResultRow> user1Blank = BusinessResultCalculator.BuildRows(repo.LoadRawData(new BusinessResultFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = "", UserId = fx.User1StoreId, IsAdmin = false
            }, prev));
            Check("KQKD Repo (§7/P2/Q13a lọc cửa hàng Store A cho Admin và user 1 CH để trống: khớp cả bill, nhóm, biến phí và phiếu chi)",
                MatchesStoreAOnly(adminStoreA) && MatchesStoreAOnly(user1Blank));

            // 3. User1Store (thuộc CH A) chọn CH B → ra 0 ở mọi dòng; đối chứng Admin chọn CH B có đủ số liệu.
            List<BusinessResultRow> adminStoreB = BusinessResultCalculator.BuildRows(repo.LoadRawData(new BusinessResultFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = fx.StoreBId, UserId = "selftest", IsAdmin = true
            }, prev));
            List<BusinessResultRow> user1PickB = BusinessResultCalculator.BuildRows(repo.LoadRawData(new BusinessResultFilter
            {
                FromDate = fx.From, ToDate = fx.To, StoreId = fx.StoreBId, UserId = fx.User1StoreId, IsAdmin = false
            }, prev));

            // Store B: DT 400k/210k; DP = 150k+70k=220k (trước 100k+30k=130k); BP = 100k/70k;
            // CK = 80k+30k=110k (trước 40k+50k=90k); CP = 430k/290k; LÃI/LỖ = -30k/-80k.
            bool adminBOk = MatchesPair(FindBrRow(adminStoreB, 0, "DOANH THU"), 400000m, 210000m)
                && MatchesPair(FindBrRow(adminStoreB, 1, "KQKD-01-Cà phê"), 380000m, 0m)
                && MatchesPair(FindBrRow(adminStoreB, 3, "Định phí"), 220000m, 130000m)
                && MatchesPair(FindBrRow(adminStoreB, 4, fx.FixedReason1Name), 150000m, 100000m)
                && MatchesPair(FindBrRow(adminStoreB, 4, fx.FixedReason2Name), 70000m, 30000m)
                && MatchesPair(FindBrRow(adminStoreB, 5, "Biến phí"), 100000m, 70000m)
                && MatchesPair(FindBrRow(adminStoreB, 7, "Chi khác"), 110000m, 90000m)
                && MatchesPair(FindBrRow(adminStoreB, 8, fx.OtherReason1Name), 80000m, 40000m)
                && MatchesPair(FindBrRow(adminStoreB, 8, fx.OtherReason2Name), 30000m, 50000m)
                && MatchesPair(FindBrRow(adminStoreB, 2, "CHI PHÍ"), 430000m, 290000m)
                && MatchesPair(FindBrRow(adminStoreB, 9, "LÃI/LỖ"), -30000m, -80000m);

            bool user1PickBZero = MatchesPair(FindBrRow(user1PickB, 0, "DOANH THU"), 0m, 0m)
                && MatchesPair(FindBrRow(user1PickB, 3, "Định phí"), 0m, 0m)
                && MatchesPair(FindBrRow(user1PickB, 5, "Biến phí"), 0m, 0m)
                && MatchesPair(FindBrRow(user1PickB, 7, "Chi khác"), 0m, 0m)
                && MatchesPair(FindBrRow(user1PickB, 2, "CHI PHÍ"), 0m, 0m)
                && MatchesPair(FindBrRow(user1PickB, 9, "LÃI/LỖ"), 0m, 0m)
                && user1PickB.FindAll(r => r.Loai == 6).Count == 0
                && user1PickB.FindAll(r => r.Loai == 1).Count == 3;

            Check("KQKD Repo (§7/P2/Q13a user CH A chọn CH B ra 0 ở mọi dòng; đối chứng Admin chọn CH B có đủ bill, nhóm, biến phí, phiếu chi)",
                adminBOk && user1PickBZero);

            // 4. Ca C3: hai kỳ trùng ngày (01/10–25/11/2006, 1 bill 10/10 bán 300.000 giá vốn 100.000).
            DateTime c3From = new DateTime(2006, 10, 1);
            DateTime c3To = new DateTime(2006, 11, 25);
            BusinessResultPeriodRange c3Prev = BusinessResultCalculator.CalculatePreviousPeriod(c3From, c3To);
            List<BusinessResultRow> c3Rows = BusinessResultCalculator.BuildRows(repo.LoadRawData(new BusinessResultFilter
            {
                FromDate = c3From, ToDate = c3To, StoreId = fx.StoreAId, UserId = "selftest", IsAdmin = true
            }, c3Prev));

            bool c3Ok = c3Prev.FromDate == new DateTime(2006, 9, 1) && c3Prev.ToDate == new DateTime(2006, 10, 25)
                && MatchesPair(FindBrRow(c3Rows, 0, "DOANH THU"), 300000m, 300000m)
                && MatchesPair(FindBrRow(c3Rows, 1, "KQKD-01-Cà phê"), 300000m, 300000m)
                && MatchesPair(FindBrRow(c3Rows, 5, "Biến phí"), 100000m, 100000m)
                && MatchesPair(FindBrRow(c3Rows, 6, "KQKD-01-Cà phê"), 100000m, 100000m)
                && MatchesPair(FindBrRow(c3Rows, 2, "CHI PHÍ"), 100000m, 100000m)
                && MatchesPair(FindBrRow(c3Rows, 9, "LÃI/LỖ"), 200000m, 200000m);
            Check("KQKD Repo (§7/C3 hai kỳ trùng ngày 01/10–25/11): bill 10/10 bán 300k/vốn 100k hiện ở cả 2 cột DT, nhóm, biến phí và LÃI/LỖ kỳ trước = 200.000",
                c3Ok);
        }

        private static bool MatchesStoreAOnly(List<BusinessResultRow> rows)
        {
            // Store A: DT 1.800k/2.500k; DP = 350k+180k=530k (trước 400k+90k=490k); BP = 514k/650k;
            // CK = 120k+50k=170k (trước 90k+110k=200k); CP = 1.214k/1.340k; LÃI/LỖ = 586k/1.160k.
            return MatchesPair(FindBrRow(rows, 0, "DOANH THU"), 1800000m, 2500000m)
                && MatchesPair(FindBrRow(rows, 1, "(Chưa phân nhóm)"), 200000m, 0m)
                && MatchesPair(FindBrRow(rows, 1, "KQKD-01-Cà phê"), 710000m, 350000m)
                && MatchesPair(FindBrRow(rows, 1, "KQKD-02-Trà"), 390000m, 2000000m)
                && MatchesPair(FindBrRow(rows, 1, "KQKD-03-Bánh cũ"), 0m, 150000m)
                && MatchesPair(FindBrRow(rows, 1, "Giảm giá"), 70000m, 25000m)
                && MatchesPair(FindBrRow(rows, 1, "Phí dịch vụ"), 60000m, 12000m)
                && MatchesPair(FindBrRow(rows, 1, "Thuế"), 110000m, 33000m)
                && MatchesPair(FindBrRow(rows, 3, "Định phí"), 530000m, 490000m)
                && MatchesPair(FindBrRow(rows, 5, "Biến phí"), 514000m, 650000m)
                && MatchesPair(FindBrRow(rows, 6, "KQKD-01-Cà phê"), 280000m, 120000m)
                && MatchesPair(FindBrRow(rows, 7, "Chi khác"), 170000m, 200000m)
                && MatchesPair(FindBrRow(rows, 2, "CHI PHÍ"), 1214000m, 1340000m)
                && MatchesPair(FindBrRow(rows, 9, "LÃI/LỖ"), 586000m, 1160000m);
        }

        private static BusinessResultRow FindBrRow(List<BusinessResultRow> rows, int loai, string content)
        {
            return rows.Find(r => r.Loai == loai && r.Content == content);
        }

        private static bool MatchesPair(BusinessResultRow row, decimal cur, decimal prev)
        {
            return row != null && row.CurrentValue == cur && row.PreviousValue == prev
                && row.ChangeAmount == (cur - prev);
        }
    }
}
