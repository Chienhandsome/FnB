using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void OrderControlTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_kso",
                    UserName = "selftest_kso",
                    DisplayName = "Selftest KiemSoatOrder",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                OrderControlPureAnalyzerTests();
                OrderControlPermissionAndMenuTests(db);
                OrderControlRepoAndUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static string OcSingleLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        // ------------------------------------------------------------------ 1. Hàm thuần (spec §3 ca 1–4, 7)

        private static void OrderControlPureAnalyzerTests()
        {
            DateTime dayD = new DateTime(2005, 6, 10);

            // Spec §3 ca 1: Ba hóa đơn "1,2", "4", "5,7" -> Đã dùng 1,2,4,5,7; Thiếu 3 và 6; max=7, min=1, used=5, missing=2, dup=0.
            OrderControlAnalysisResult r1 = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "b1", BillNumber = "HD01", BillDate = dayD, OrderNumbers = "1,2" },
                new OrderControlBillRow { BillId = "b2", BillNumber = "HD02", BillDate = dayD, OrderNumbers = "4" },
                new OrderControlBillRow { BillId = "b3", BillNumber = "HD03", BillDate = dayD, OrderNumbers = "5,7" }
            });
            bool ca1UsedOk = r1.UsedRows.Count == 5
                && r1.UsedRows[0].OrderNumber == 1 && r1.UsedRows[0].BillDisplay == "HD01"
                && r1.UsedRows[1].OrderNumber == 2 && r1.UsedRows[1].BillDisplay == "HD01"
                && r1.UsedRows[2].OrderNumber == 4 && r1.UsedRows[2].BillDisplay == "HD02"
                && r1.UsedRows[3].OrderNumber == 5 && r1.UsedRows[3].BillDisplay == "HD03"
                && r1.UsedRows[4].OrderNumber == 7 && r1.UsedRows[4].BillDisplay == "HD03";
            bool ca1IssuesOk = r1.IssueRows.Count == 2
                && r1.IssueRows[0].Kind == OrderControlIssueKind.Missing && r1.IssueRows[0].Message == "Thiếu order 3"
                && r1.IssueRows[1].Kind == OrderControlIssueKind.Missing && r1.IssueRows[1].Message == "Thiếu order 6";
            bool ca1SumOk = r1.Summary.MaxOrder == 7 && r1.Summary.MinOrder == 1
                && r1.Summary.UsedCount == 5 && r1.Summary.MissingCount == 2L
                && r1.Summary.DuplicateCount == 0 && r1.Summary.InvalidCount == 0;
            Check("Kiểm soát order §3 #1 (hàm thuần '1,2', '4', '5,7'): 5 đã dùng, thiếu 3 & 6, max=7 min=1 thiếu=2 trùng=0",
                ca1UsedOk && ca1IssuesOk && ca1SumOk,
                OcSingleLine("used=" + ca1UsedOk + ", iss=" + ca1IssuesOk + ", sum=" + ca1SumOk
                    + ", cnt=" + r1.UsedRows.Count + "/" + r1.IssueRows.Count));

            // Spec §3 ca 2: Dải thiếu nhiều số "1" và "5" -> "Thiếu order 2-4", thiếu 3 số.
            OrderControlAnalysisResult r2 = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "b1", BillNumber = "HD01", BillDate = dayD, OrderNumbers = "1" },
                new OrderControlBillRow { BillId = "b2", BillNumber = "HD02", BillDate = dayD, OrderNumbers = "5" }
            });
            bool ca2Ok = r2.UsedRows.Count == 2
                && r2.IssueRows.Count == 1
                && r2.IssueRows[0].Kind == OrderControlIssueKind.Missing
                && r2.IssueRows[0].Message == "Thiếu order 2-4"
                && r2.IssueRows[0].MissingStart == 2 && r2.IssueRows[0].MissingEnd == 4 && r2.IssueRows[0].MissingCount == 3L
                && r2.Summary.MissingCount == 3L && r2.Summary.MinOrder == 1 && r2.Summary.MaxOrder == 5;
            Check("Kiểm soát order §3 #2 (hàm thuần dải thiếu '1' và '5'): 'Thiếu order 2-4', thiếu 3 số",
                ca2Ok,
                OcSingleLine("iss=" + (r2.IssueRows.Count > 0 ? r2.IssueRows[0].Message : "none") + ", miss=" + r2.Summary.MissingCount));

            // Spec §3 ca 3: Trùng — bill A "2" (ngày sớm hơn) và B "2,3" (truyền ngược thứ tự để kiểm sắp theo ngày).
            OrderControlAnalysisResult r3 = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "bB", BillNumber = "B", BillDate = dayD.AddHours(14), OrderNumbers = "2,3" },
                new OrderControlBillRow { BillId = "bA", BillNumber = "A", BillDate = dayD.AddHours(9), OrderNumbers = "2" }
            });
            bool ca3UsedOk = r3.UsedRows.Count == 2
                && r3.UsedRows[0].OrderNumber == 2 && r3.UsedRows[0].IsDuplicate && r3.UsedRows[0].BillDisplay == "A (trùng)"
                && r3.UsedRows[1].OrderNumber == 3 && !r3.UsedRows[1].IsDuplicate && r3.UsedRows[1].BillDisplay == "B";
            bool ca3DupOk = r3.IssueRows.Count == 1
                && r3.IssueRows[0].Kind == OrderControlIssueKind.Duplicate
                && r3.IssueRows[0].Message == "Trùng số order 2 (bill 'A', 'B')"
                && r3.Summary.DuplicateCount == 1 && r3.Summary.UsedCount == 2 && r3.Summary.MissingCount == 0L;
            // Trùng hai lần trong cùng một hóa đơn: bill C "4,4".
            OrderControlAnalysisResult r3Same = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "bC", BillNumber = "C", BillDate = dayD, OrderNumbers = "4,4" }
            });
            bool ca3SameOk = r3Same.UsedRows.Count == 1 && r3Same.UsedRows[0].BillDisplay == "C (trùng)"
                && r3Same.IssueRows.Count == 1 && r3Same.IssueRows[0].Message == "Trùng số order 4 (bill 'C', 'C')"
                && r3Same.Summary.DuplicateCount == 1 && r3Same.Summary.UsedCount == 1;
            Check("Kiểm soát order §3 #3 (hàm thuần trùng A='2', B='2,3' & trùng trong cùng bill): liệt kê đủ bill, (trùng), không Thiếu",
                ca3UsedOk && ca3DupOk && ca3SameOk,
                OcSingleLine("used=" + ca3UsedOk + ", dup=" + ca3DupOk + ", same=" + ca3SameOk
                    + ", msg=" + (r3.IssueRows.Count > 0 ? r3.IssueRows[0].Message : "none")));

            // Spec §3 ca 4: Viết sai ("12,abc", "1.5", "٣", "-5", "+1"), khoảng trắng "1, 2 , 3", phần rỗng "1,,2".
            OrderControlAnalysisResult r4Err = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "bX", BillNumber = "X", BillDate = dayD, OrderNumbers = "12,abc,1.5,\u0663,-5,+1" }
            });
            bool ca4InvalidOk = r4Err.UsedRows.Count == 1 && r4Err.UsedRows[0].OrderNumber == 12
                && r4Err.Summary.UsedCount == 1 && r4Err.Summary.InvalidCount == 5
                && r4Err.IssueRows.Count == 5
                && r4Err.IssueRows[0].Kind == OrderControlIssueKind.Invalid
                && r4Err.IssueRows[0].Message == "Lỗi số order 'abc' ở bill 'X'"
                && r4Err.IssueRows[1].Message == "Lỗi số order '1.5' ở bill 'X'"
                && r4Err.IssueRows[2].Message == "Lỗi số order '\u0663' ở bill 'X'"
                && r4Err.IssueRows[3].Message == "Lỗi số order '-5' ở bill 'X'"
                && r4Err.IssueRows[4].Message == "Lỗi số order '+1' ở bill 'X'";

            OrderControlAnalysisResult r4Space = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "bS", BillNumber = "S", BillDate = dayD, OrderNumbers = " 1, 2 , 3 " }
            });
            bool ca4SpaceOk = r4Space.UsedRows.Count == 3
                && r4Space.UsedRows[0].OrderNumber == 1 && r4Space.UsedRows[1].OrderNumber == 2 && r4Space.UsedRows[2].OrderNumber == 3
                && r4Space.IssueRows.Count == 0 && r4Space.Summary.InvalidCount == 0;

            OrderControlAnalysisResult r4Empty = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "bE", BillNumber = "E", BillDate = dayD, OrderNumbers = "1,,2," }
            });
            bool ca4EmptyOk = r4Empty.UsedRows.Count == 2
                && r4Empty.UsedRows[0].OrderNumber == 1 && r4Empty.UsedRows[1].OrderNumber == 2
                && r4Empty.IssueRows.Count == 0 && r4Empty.Summary.InvalidCount == 0;

            Check("Kiểm soát order §3 #4 (hàm thuần viết sai, khoảng trắng, phần rỗng): 'abc'/'1.5'/'٣'/'-5' viết sai, 12 vẫn tính, '1, 2 , 3' & '1,,2' hợp lệ",
                ca4InvalidOk && ca4SpaceOk && ca4EmptyOk,
                OcSingleLine("inv=" + ca4InvalidOk + ", sp=" + ca4SpaceOk + ", emp=" + ca4EmptyOk));

            // Spec §3 ca 7: Vượt int.MaxValue coi là viết sai (không crash); dải rộng 1.000.000 số thiếu tính bằng phép trừ không treo.
            OrderControlAnalysisResult r7Over = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "bO", BillNumber = "OV", BillDate = dayD, OrderNumbers = "2147483647,2147483648,999999999999999999999" }
            });
            bool ca7OverOk = r7Over.UsedRows.Count == 1 && r7Over.UsedRows[0].OrderNumber == int.MaxValue
                && r7Over.Summary.InvalidCount == 2 && r7Over.IssueRows.Count == 2
                && r7Over.IssueRows[0].Message == "Lỗi số order '2147483648' ở bill 'OV'"
                && r7Over.IssueRows[1].Message == "Lỗi số order '999999999999999999999' ở bill 'OV'";

            var sw = Stopwatch.StartNew();
            OrderControlAnalysisResult r7Huge = OrderControlAnalyzer.Analyze(new[]
            {
                new OrderControlBillRow { BillId = "bH1", BillNumber = "H1", BillDate = dayD, OrderNumbers = "1" },
                new OrderControlBillRow { BillId = "bH2", BillNumber = "H2", BillDate = dayD, OrderNumbers = "1000002" }
            });
            sw.Stop();
            bool ca7HugeOk = r7Huge.UsedRows.Count == 2
                && r7Huge.IssueRows.Count == 1
                && r7Huge.IssueRows[0].Message == "Thiếu order 2-1000001"
                && r7Huge.Summary.MissingCount == 1000000L
                && sw.ElapsedMilliseconds < 500;

            Check("Kiểm soát order §3 #7 (hàm thuần vượt int & dải thiếu 1.000.000 số): vượt int là viết sai, dải lớn O(1) không treo",
                ca7OverOk && ca7HugeOk,
                OcSingleLine("over=" + ca7OverOk + ", huge=" + ca7HugeOk + ", ms=" + sw.ElapsedMilliseconds));
        }

        // ------------------------------------------------------------------ 2. Phân quyền & Menu (spec §3 ca 8)

        private static void OrderControlPermissionAndMenuTests(string db)
        {
            var adminSess = new UserSession
            {
                UserId = "u_kso_adm", UserName = "u_kso_adm", DisplayName = "Admin KSO",
                IsAdmin = true, DatabasePath = db, Permissions = new FunctionPermissions(true)
            };
            var permsByName = new FunctionPermissions(false);
            permsByName.Add("", OrderControlConstants.FunctionName, RoleModes.View);
            var viewByNameSess = new UserSession
            {
                UserId = "u_kso_vn", UserName = "u_kso_vn", DisplayName = "View Name KSO",
                IsAdmin = false, DatabasePath = db, Permissions = permsByName
            };
            var permsById = new FunctionPermissions(false);
            permsById.Add(OrderControlConstants.FunctionId, "", RoleModes.View);
            var viewByIdSess = new UserSession
            {
                UserId = "u_kso_vi", UserName = "u_kso_vi", DisplayName = "View Id KSO",
                IsAdmin = false, DatabasePath = db, Permissions = permsById
            };
            var deniedSess = new UserSession
            {
                UserId = "u_kso_no", UserName = "u_kso_no", DisplayName = "Denied KSO",
                IsAdmin = false, DatabasePath = db, Permissions = new FunctionPermissions(false)
            };

            MenuModel adminMenu = MainMenuBuilder.Build(adminSess);
            MenuNode adminNode = OcFindMenuLeaf(adminMenu.Roots, n => ScreenFactory.IsOrderControlMenu(n));
            MenuNode otherHiddenStillHidden = OcFindMenuLeaf(adminMenu.Roots, n => string.Equals((n.Title ?? "").Trim(), "Dịch vụ MTB", StringComparison.OrdinalIgnoreCase));
            MenuNode viewNameNode = OcFindMenuLeaf(MainMenuBuilder.Build(viewByNameSess).Roots, n => ScreenFactory.IsOrderControlMenu(n));
            MenuNode viewIdNode = OcFindMenuLeaf(MainMenuBuilder.Build(viewByIdSess).Roots, n => ScreenFactory.IsOrderControlMenu(n));
            MenuNode deniedNode = OcFindMenuLeaf(MainMenuBuilder.Build(deniedSess).Roots, n => ScreenFactory.IsOrderControlMenu(n));

            bool foldOk = ScreenFactory.IsOrderControlMenu(new MenuNode { Title = "Kiểm soát order" })
                && ScreenFactory.IsOrderControlMenu(new MenuNode { Title = "kiem soat order" })
                && ScreenFactory.IsOrderControlMenu(new MenuNode { Title = "KIỂM SOÁT ORDER" })
                && ScreenFactory.IsOrderControlMenu(new MenuNode { Title = "Khác", FunctionId = OrderControlConstants.FunctionId })
                && !ScreenFactory.IsOrderControlMenu(new MenuNode { Title = "Quản lý bán hàng" });

            bool opensRealView = false;
            if (adminNode != null)
            {
                using (System.Windows.Forms.Control created = ScreenFactory.CreateView(adminNode, adminSess))
                    opensRealView = created is OrderControlView;
            }

            // Kiểm tra ánh xạ quyền DB SGROUPROLE (Quản lý/Kế toán có Xem, Phục vụ bị khóa = 0).
            bool dbRolesOk = false;
            string roleDetail = "";
            using (var conn = KazukoConnection.Open(db))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT G.NAME, R.MODE FROM SGROUPROLE R " +
                    "INNER JOIN SGROUPUSER G ON G.ID = R.SGROUPUSERID " +
                    "WHERE R.SFUNCTIONID = @FnId",
                    "@FnId", OrderControlConstants.FunctionId);
                int ql = -1, kt = -1, pv = -1;
                foreach (DataRow r in dt.Rows)
                {
                    string gname = (DbHelper.Str(r["NAME"]) ?? "").Trim();
                    int mode = DbHelper.Int(r["MODE"]);
                    if (gname == "Quản lý") ql = mode;
                    else if (gname == "Kế toán") kt = mode;
                    else if (gname == "Phục vụ") pv = mode;
                }
                dbRolesOk = (ql & RoleModes.View) != 0 && (kt & RoleModes.View) != 0 && pv == 0;
                roleDetail = "QL=" + ql + "/KT=" + kt + "/PV=" + pv;
            }

            bool svcPermOk = OrderControlService.CanView(adminSess)
                && OrderControlService.CanView(viewByNameSess)
                && OrderControlService.CanView(viewByIdSess)
                && !OrderControlService.CanView(deniedSess);
            string deniedErr;
            var deniedSvc = new OrderControlService(deniedSess);
            OrderControlAnalysisResult deniedRes = deniedSvc.LoadAnalysis(new DateTime(2005, 6, 10), new DateTime(2005, 6, 10), out deniedErr);
            bool svcBlockedOk = deniedErr == OrderControlConstants.NoPermissionMessage && !deniedRes.HasAnyData;

            Check("Kiểm soát order §3 #8 (menu, quyền Xem & ScreenFactory): Admin/có quyền thấy và mở OrderControlView, không quyền ẩn, không đổi menu ẩn khác",
                adminNode != null && otherHiddenStillHidden == null
                && viewNameNode != null && viewIdNode != null && deniedNode == null
                && foldOk && opensRealView && dbRolesOk && svcPermOk && svcBlockedOk,
                OcSingleLine("adm=" + (adminNode != null) + ", mtbHid=" + (otherHiddenStillHidden == null)
                    + ", vn=" + (viewNameNode != null) + ", vi=" + (viewIdNode != null)
                    + ", denHid=" + (deniedNode == null) + ", fold=" + foldOk
                    + ", view=" + opensRealView + ", dbRole=" + dbRolesOk + "(" + roleDetail + "), svcBlk=" + svcBlockedOk));
        }

        private static MenuNode OcFindMenuLeaf(IEnumerable<MenuNode> nodes, Predicate<MenuNode> match)
        {
            if (nodes == null)
                return null;
            foreach (MenuNode n in nodes)
            {
                if (n == null)
                    continue;
                if (n.IsLeaf && match(n))
                    return n;
                MenuNode found = OcFindMenuLeaf(n.Children, match);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
