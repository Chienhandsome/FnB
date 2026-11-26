using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private sealed class QuoteTestFixture
        {
            public string GroupId;
            public string UnitOddId;
            public string UnitEvenId;
            public string Product1Id;
            public string Product2Id;
            public string CustomerId;
            public List<string> CreatedQuoteIds = new List<string>();
        }

        private static void QuoteRepoAndUiTests(string db)
        {
            var fx = new QuoteTestFixture
            {
                GroupId = DbHelper.NewId(),
                UnitOddId = DbHelper.NewId(),
                UnitEvenId = DbHelper.NewId(),
                Product1Id = DbHelper.NewId(),
                Product2Id = DbHelper.NewId(),
                CustomerId = DbHelper.NewId()
            };

            UserSession savedSession = AppSession.Current;
            try
            {
                SeedQuoteFixture(db, fx);

                var adminSession = new UserSession
                {
                    UserId = "selftest",
                    UserName = "selftest",
                    DisplayName = "Quản trị",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };
                AppSession.Current = adminSession;
                SystemConfig.Reload(db);

                QuoteRepositoryAndPermissionTests(db, adminSession, fx);
                QuoteMenuRouteTests(db, adminSession);
                QuoteUiTests(db, adminSession, fx);
            }
            finally
            {
                AppSession.Current = savedSession;
                CleanupQuoteFixture(db, fx);
            }
        }

        private static void SeedQuoteFixture(string db, QuoteTestFixture fx)
        {
            BookingExec(db,
                "INSERT INTO \"DNHOMMATHANG\" (\"ID\", \"NAME\", \"CODE\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, @c, 30, 'selftest')",
                "@id", fx.GroupId, "@n", "NHÓM THỬ BÁO GIÁ", "@c", "BGGRP");
            BookingExec(db,
                "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                "@id", fx.UnitOddId, "@n", "LonBG");
            BookingExec(db,
                "INSERT INTO \"DDONVITINH\" (\"ID\", \"NAME\", \"STATUS\", \"USERCREATEDID\") VALUES (@id, @n, 30, 'selftest')",
                "@id", fx.UnitEvenId, "@n", "ThùngBG");
            BookingExec(db,
                "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"GIABAN\", \"STATUS\", \"USERCREATEDID\") " +
                "VALUES (@id, @c, @n, @g, @u, @p, 30, 'selftest')",
                "@id", fx.Product1Id, "@c", "BGITEM1", "@n", "Mặt hàng BG 1 ĐVT", "@g", fx.GroupId, "@u", fx.UnitOddId, "@p", 235000m);
            BookingExec(db,
                "INSERT INTO \"DMATHANG\" (\"ID\", \"CODE\", \"NAME\", \"DNHOMMATHANGID\", \"DDONVITINHID\", \"DDONVITINHCHANID\", \"QUYDOI\", \"GIABAN\", \"GIABANCHAN\", \"STATUS\", \"USERCREATEDID\") " +
                "VALUES (@id, @c, @n, @g, @u1, @u2, 24, @p1, @p2, 30, 'selftest')",
                "@id", fx.Product2Id, "@c", "BGITEM2", "@n", "Mặt hàng BG 2 ĐVT", "@g", fx.GroupId,
                "@u1", fx.UnitOddId, "@u2", fx.UnitEvenId, "@p1", 15000m, "@p2", 340000m);
            BookingExec(db,
                "INSERT INTO \"DKHACHHANG\" (\"ID\", \"NAME\", \"DIACHI\", \"DIENTHOAI\", \"EMAIL\", \"STATUS\", \"USERCREATEDID\") " +
                "VALUES (@id, @n, @dc, @dt, @em, 30, 'selftest')",
                "@id", fx.CustomerId, "@n", "Khách hàng BG Thử", "@dc", "123 Lê Lợi", "@dt", "0909123456", "@em", "bg@example.com");
        }

        private static void CleanupQuoteFixture(string db, QuoteTestFixture fx)
        {
            foreach (string qid in fx.CreatedQuoteIds)
            {
                BookingExec(db, "DELETE FROM \"TLUUVET\" WHERE \"SODONHANG\" = @id", "@id", qid);
                BookingExec(db, "DELETE FROM \"TBAOGIACHITIET\" WHERE \"TBAOGIAID\" = @id", "@id", qid);
                BookingExec(db, "DELETE FROM \"TBAOGIA\" WHERE \"ID\" = @id", "@id", qid);
            }
            BookingExec(db, "DELETE FROM \"DMATHANG\" WHERE \"ID\" IN (@a, @b)", "@a", fx.Product1Id, "@b", fx.Product2Id);
            BookingExec(db, "DELETE FROM \"DNHOMMATHANG\" WHERE \"ID\" = @id", "@id", fx.GroupId);
            BookingExec(db, "DELETE FROM \"DKHACHHANG\" WHERE \"ID\" = @id", "@id", fx.CustomerId);
            BookingExec(db, "DELETE FROM \"DDONVITINH\" WHERE \"ID\" IN (@a, @b)", "@a", fx.UnitOddId, "@b", fx.UnitEvenId);
        }

        private static void QuoteRepositoryAndPermissionTests(string db, UserSession adminSession, QuoteTestFixture fx)
        {
            var svc = new QuoteService(adminSession);

            // 1. Sinh số BGyy/##### và quay vòng theo năm.
            string num2026 = svc.GenerateNumber(new DateTime(2026, 11, 25));
            string num2029 = svc.GenerateNumber(new DateTime(2029, 1, 5));
            Check("Báo giá §7 sinh số phiếu BGyy/##### và quay vòng theo năm",
                num2026.StartsWith("BG26/", StringComparison.Ordinal) && num2026.Length == 10
                && num2029 == "BG29/00001",
                "2026=" + num2026 + ", 2029=" + num2029);

            // 2. Lưu phiếu + chi tiết trong 1 transaction, kiểm LOAI = 0 và GIAMTHEOTIEN = 30.
            var row = QuoteRow.NewDefault();
            row.Name = num2026;
            row.Ngay = new DateTime(2026, 11, 25);
            row.CustomerId = fx.CustomerId;
            row.TenKhach = "Khách hàng BG Thử";
            row.DiaChi = "123 Lê Lợi";
            row.DienThoai = "0909123456";
            row.Email = "bg@example.com";
            row.Note = "Ghi chú thử";
            row.TienHang = 235000m;
            row.GiamTheoTien = true;
            row.TienGiamGia = 15000m;
            row.TileGiamGia = 100m * 15000m / 235000m;
            row.TileThue = 0m;
            row.TienThue = 0m;
            row.PhiVanChuyen = 5000m;
            row.TongCong = 225000m;
            var lines = new List<QuoteLine>
            {
                new QuoteLine
                {
                    ProductId = fx.Product1Id, UnitId = fx.UnitOddId,
                    Quantity = 1m, Price = 235000m, DiscountPct = 0m, Amount = 235000m, Note = "dòng 1"
                }
            };
            QuoteSaveResult saved = svc.SaveQuote(row, lines);
            if (saved.Ok && saved.SavedId != null)
                fx.CreatedQuoteIds.Add(saved.SavedId);

            QuoteRow reloaded = saved.Ok ? svc.LoadQuote(saved.SavedId) : null;
            Check("Báo giá §7 lưu 1 transaction (LOAI=0, GIAMTHEOTIEN=30, đủ dòng chi tiết)",
                saved.Ok && reloaded != null && reloaded.Name == num2026 && reloaded.Loai == 0
                && reloaded.GiamTheoTien && reloaded.TongCong == 225000m
                && reloaded.Lines != null && reloaded.Lines.Count == 1 && reloaded.Lines[0].Quantity == 1m);

            // Kiểm số tiếp theo tăng lên +1.
            string nextNum = svc.GenerateNumber(new DateTime(2026, 11, 25));
            Check("Báo giá §7 số phiếu tiếp theo không trùng và tăng tuần tự",
                nextNum != num2026 && nextNum.StartsWith("BG26/", StringComparison.Ordinal),
                "cur=" + num2026 + ", next=" + nextNum);

            // Kiểm trùng số phiếu trong cùng transaction.
            var dupRow = QuoteRow.NewDefault();
            dupRow.Name = num2026;
            QuoteSaveResult dupRes = svc.SaveQuote(dupRow, new List<QuoteLine>());
            Check("Báo giá §7 kiểm trùng số phiếu khi lưu",
                !dupRes.Ok && dupRes.ErrorMessage == QuoteText.NumberDuplicate(num2026), dupRes.ErrorMessage);

            // Chặn lưu dòng có SL <= 0 ở tầng service (P3).
            var badQtyRow = QuoteRow.NewDefault();
            badQtyRow.Name = nextNum;
            QuoteSaveResult badQtyRes = svc.SaveQuote(badQtyRow, new List<QuoteLine>
            {
                new QuoteLine { ProductId = fx.Product1Id, UnitId = fx.UnitOddId, Quantity = 0m, Price = 10000m }
            });
            Check("Báo giá §5.0 P3 tầng thao tác chặn lưu dòng SL <= 0",
                !badQtyRes.Ok && badQtyRes.ErrorMessage == QuoteText.EnterQuantity);

            // Kiểm sửa phiếu: đổi GIAMTHEOTIEN = false (0) và cập nhật chi tiết.
            reloaded.GiamTheoTien = false;
            reloaded.TileGiamGia = 10m;
            reloaded.TienGiamGia = 24000m;
            reloaded.TongCong = 216000m;
            reloaded.Lines.Add(new QuoteLine
            {
                ProductId = fx.Product2Id, UnitId = fx.UnitEvenId,
                Quantity = 2m, Price = 340000m, DiscountPct = 0m, Amount = 680000m
            });
            QuoteSaveResult editRes = svc.SaveQuote(reloaded, reloaded.Lines);
            QuoteRow afterEdit = svc.LoadQuote(saved.SavedId);
            Check("Báo giá §7 sửa phiếu ghi đúng cờ GIAMTHEOTIEN=0 và cập nhật chi tiết",
                editRes.Ok && afterEdit != null && !afterEdit.GiamTheoTien && afterEdit.Lines.Count == 2);

            // Kiểm phân quyền "Báo giá" ở tầng thao tác (không chỉ ẩn nút).
            var viewOnlyPerms = new FunctionPermissions(false);
            viewOnlyPerms.Add(QuoteConstants.FunctionId, QuoteConstants.FunctionName, RoleModes.View);
            var viewOnlySession = new UserSession
            {
                UserId = "u_view", UserName = "u_view", IsAdmin = false,
                DatabasePath = db, Permissions = viewOnlyPerms
            };
            var viewOnlySvc = new QuoteService(viewOnlySession);
            var addTry = QuoteRow.NewDefault();
            addTry.Name = nextNum;
            QuoteSaveResult addBlocked = viewOnlySvc.SaveQuote(addTry, new List<QuoteLine>());
            QuoteSaveResult editBlocked = viewOnlySvc.SaveQuote(afterEdit, afterEdit.Lines);
            QuoteDeleteResult delBlocked = viewOnlySvc.DeleteQuote(saved.SavedId, afterEdit.Name);
            Check("Báo giá §7 quyền 'Báo giá' kiểm ở tầng thao tác: chỉ có Xem thì bị chặn Thêm/Sửa/Xóa",
                viewOnlySvc.LoadQuote(saved.SavedId) != null
                && !addBlocked.Ok && addBlocked.ErrorMessage == QuoteText.NoPermission
                && !editBlocked.Ok && editBlocked.ErrorMessage == QuoteText.NoPermission
                && !delBlocked.Ok && delBlocked.ErrorMessage == QuoteText.NoPermission);

            // Kiểm lưu vết hoạt động khi KichHoatLuuVetHoatDong = 30.
            bool logEnabled = SystemConfig.KichHoatLuuVetHoatDong;
            int logCount = CountTableRows(db, "SELECT COUNT(*) FROM \"TLUUVET\" WHERE \"SODONHANG\" = @id AND \"CHUCNANG\" = @fn",
                "@id", saved.SavedId, "@fn", QuoteConstants.FunctionName);
            Check("Báo giá §2.4 lưu vết TLUUVET qua ActivityLogger theo cờ KichHoatLuuVetHoatDong",
                logEnabled ? logCount >= 2 : logCount == 0,
                "enabled=" + logEnabled + ", logs=" + logCount);

            // Kiểm xóa cứng xóa cả TBAOGIA và TBAOGIACHITIET, không để dòng mồ côi.
            QuoteDeleteResult delOk = svc.DeleteQuote(saved.SavedId, afterEdit.Name);
            int remainHeader = CountTableRows(db, "SELECT COUNT(*) FROM \"TBAOGIA\" WHERE \"ID\" = @id", "@id", saved.SavedId);
            int remainDetail = CountTableRows(db, "SELECT COUNT(*) FROM \"TBAOGIACHITIET\" WHERE \"TBAOGIAID\" = @id", "@id", saved.SavedId);
            Check("Báo giá §7 xóa cứng xóa cả TBAOGIA và TBAOGIACHITIET, 0 bản ghi mồ côi",
                delOk.Ok && remainHeader == 0 && remainDetail == 0,
                "hdr=" + remainHeader + ", dtl=" + remainDetail);

            // Kiểm giữ nguyên thứ tự dòng chi tiết sau khi lưu và sửa (ít nhất 8 dòng, r2 #5).
            var orderRow = QuoteRow.NewDefault();
            orderRow.Name = svc.GenerateNumber(new DateTime(2026, 11, 25));
            orderRow.Ngay = new DateTime(2026, 11, 25);
            var eightLines = new List<QuoteLine>();
            for (int i = 1; i <= 8; i++)
            {
                eightLines.Add(new QuoteLine
                {
                    ProductId = (i % 2 == 1) ? fx.Product1Id : fx.Product2Id,
                    UnitId = (i % 3 == 0) ? fx.UnitEvenId : fx.UnitOddId,
                    Quantity = i,
                    Price = 10000m * i,
                    DiscountPct = 0m,
                    Amount = 10000m * i * i,
                    Note = "Dòng #" + i
                });
            }
            QuoteSaveResult orderSaved = svc.SaveQuote(orderRow, eightLines);
            if (orderSaved.Ok && orderSaved.SavedId != null)
                fx.CreatedQuoteIds.Add(orderSaved.SavedId);
            QuoteRow orderLoaded = orderSaved.Ok ? svc.LoadQuote(orderSaved.SavedId) : null;
            bool order8Ok = orderLoaded != null && orderLoaded.Lines != null && orderLoaded.Lines.Count == 8;
            for (int i = 0; order8Ok && i < 8; i++)
                order8Ok = orderLoaded.Lines[i].Note == "Dòng #" + (i + 1) && orderLoaded.Lines[i].Quantity == i + 1;

            // Đổi thứ tự (đưa dòng 8 lên đầu, tráo dòng 2 & 5, thêm dòng 9), lưu lại và kiểm lại thứ tự.
            bool reorderedOk = false;
            if (order8Ok)
            {
                var reordered = new List<QuoteLine>(orderLoaded.Lines);
                QuoteLine last = reordered[7];
                reordered.RemoveAt(7);
                reordered.Insert(0, last);
                QuoteLine tmp = reordered[2];
                reordered[2] = reordered[5];
                reordered[5] = tmp;
                reordered.Add(new QuoteLine
                {
                    ProductId = fx.Product1Id, UnitId = fx.UnitOddId,
                    Quantity = 9m, Price = 90000m, Amount = 810000m, Note = "Dòng #9"
                });
                string[] expectedNotes = new string[reordered.Count];
                for (int i = 0; i < reordered.Count; i++)
                    expectedNotes[i] = reordered[i].Note;
                QuoteSaveResult reSave = svc.SaveQuote(orderLoaded, reordered);
                QuoteRow reLoaded = reSave.Ok ? svc.LoadQuote(orderSaved.SavedId) : null;
                reorderedOk = reLoaded != null && reLoaded.Lines != null && reLoaded.Lines.Count == expectedNotes.Length;
                for (int i = 0; reorderedOk && i < expectedNotes.Length; i++)
                    reorderedOk = reLoaded.Lines[i].Note == expectedNotes[i];
            }
            Check("Báo giá §7 thứ tự 8 dòng chi tiết giữ nguyên sau lưu, sửa và đọc lại từ CSDL",
                order8Ok && reorderedOk, "save8=" + order8Ok + ", edit9=" + reorderedOk);
        }

        private static void QuoteMenuRouteTests(string db, UserSession adminSession)
        {
            // Kiểm ScreenFactory tạo đúng QuoteListView từ node menu Báo giá bán hàng (không sửa SMENU.STATUS).
            var node = new MenuNode
            {
                Id = QuoteConstants.MenuId,
                Title = "Báo giá bán hàng",
                StableDescId = QuoteConstants.StableDescId,
                Operation = 0
            };
            using (Control view = ScreenFactory.CreateView(node, adminSession))
            {
                Check("Báo giá §7 ScreenFactory tạo đúng QuoteListView từ node menu báo giá",
                    view is QuoteListView, view == null ? "null" : view.GetType().Name);
            }
        }

        private static int CountTableRows(string db, string sql, params object[] args)
        {
            using (FbConnection conn = KazukoConnection.Open(db))
                return DbHelper.Int(DbHelper.Scalar(conn, null, sql, args));
        }
    }
}
