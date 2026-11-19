using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Kiểm thử tự động nhóm Hệ thống trên BẢN SAO DATA.FDB (lệnh --selftest &lt;db&gt; &lt;file kết quả&gt;).
    /// Chạy các ca trong docs/specs/he-thong.md mục 6; so sánh schema trước/sau để chứng minh không có DDL.
    /// Không bao giờ chạy trên file gốc: người chạy phải truyền bản sao.
    /// </summary>
    public static class SelfTest
    {
        private static readonly List<string> Lines = new List<string>();
        private static int _pass, _fail;

        private static void Check(string name, bool ok, string detail = null)
        {
            if (ok) _pass++; else _fail++;
            Lines.Add((ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
        }

        public static int Run(string databasePath, string outFile)
        {
            Lines.Clear();
            _pass = 0;
            _fail = 0;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest",
                    UserName = "selftest",
                    DisplayName = "selftest",
                    IsAdmin = true,
                    DatabasePath = databasePath,
                    Permissions = new FunctionPermissions(true)
                };
                string schemaBefore = Schema(databasePath);

                ConfigTests(databasePath);
                RunExtra(databasePath);

                string schemaAfter = Schema(databasePath);
                Check("Không có DDL (schema trước = sau)", schemaBefore == schemaAfter);
            }
            catch (Exception ex)
            {
                Check("Ngoại lệ không mong đợi", false, ex.GetType().Name + ": " + ex.Message);
            }
            Lines.Add("");
            Lines.Add("TỔNG: " + _pass + " đạt, " + _fail + " lỗi");
            File.WriteAllText(outFile, string.Join(Environment.NewLine, Lines.ToArray()), Encoding.UTF8);
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>Danh sách bảng/cột người dùng (để so sánh trước/sau).</summary>
        private static string Schema(string db)
        {
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT TRIM(R.RDB$RELATION_NAME), TRIM(F.RDB$FIELD_NAME), F.RDB$FIELD_SOURCE " +
                    "FROM RDB$RELATIONS R JOIN RDB$RELATION_FIELDS F ON F.RDB$RELATION_NAME = R.RDB$RELATION_NAME " +
                    "WHERE R.RDB$SYSTEM_FLAG = 0 ORDER BY 1, 2");
                var sb = new StringBuilder();
                foreach (DataRow r in dt.Rows)
                    sb.Append(r[0]).Append('.').Append(r[1]).Append(':').Append(r[2]).Append(';');
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ H3 Cấu hình

        private static readonly string[] SampleKeys =
        {
            "ChoPhepTrungTenKhachHang", "CachTinhDiem", "DoanhSoTuongUngVoi1Diem", "TuDongNangCapThanhVienKhiDatHanMuc",
            "SoFileSaoLuu", "LamTronTien", "CoTienGio", "KichHoatLuuVetHoatDong", "ThoiGianChoPhepHuyBill", "CompanyName"
        };

        private static void ConfigTests(string db)
        {
            SystemConfig.Reload(db);
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                foreach (string key in SampleKeys)
                {
                    DataTable dt = DbHelper.Table(conn, null,
                        "SELECT DATATYPE, INTVALUE, TEXTVALUE FROM SCONFIG WHERE NAME = @N", "@N", key);
                    if (dt.Rows.Count == 0)
                    {
                        Check("SystemConfig đọc " + key + " (không có dòng → mặc định)", SystemConfig.GetInt(key) == 0);
                        continue;
                    }
                    int dataType = DbHelper.Int(dt.Rows[0][0]);
                    if (dataType == 1)
                        Check("SystemConfig đọc " + key, SystemConfig.GetText(key) == (DbHelper.Str(dt.Rows[0][2]) ?? ""));
                    else
                        Check("SystemConfig đọc " + key, SystemConfig.GetInt(key) == DbHelper.Int(dt.Rows[0][1]));
                }
            }

            // Ghi và đọc lại: ô số, ô tích, ô chữ; rồi hoàn lại giá trị cũ.
            var repo = new ConfigRepository(db);
            int oldBackup = SystemConfig.GetInt("SoFileSaoLuu");
            bool oldTrung = SystemConfig.GetBool("ChoPhepTrungTenKhachHang");
            string oldCompany = SystemConfig.GetText("CompanyName");
            int written = repo.Save(new[]
            {
                new ConfigChange { Name = "SoFileSaoLuu", DataType = 3, IntValue = 7 },
                new ConfigChange { Name = "ChoPhepTrungTenKhachHang", DataType = 3, IntValue = oldTrung ? 0 : 30 },
                new ConfigChange { Name = "CompanyName", DataType = 1, TextValue = "CÔNG TY THỬ NGHIỆM" },
                new ConfigChange { Name = "KhongCoKhoaNay", DataType = 3, IntValue = 1 }
            }, "selftest");
            SystemConfig.Reload(db);
            Check("Save ghi đúng số khóa (khóa chưa có dòng bị bỏ qua)", written == 3, "written=" + written);
            Check("Ghi ô số", SystemConfig.GetInt("SoFileSaoLuu") == 7);
            Check("Ghi ô tích 30/0", SystemConfig.GetBool("ChoPhepTrungTenKhachHang") == !oldTrung);
            Check("Ghi ô chữ (tiếng Việt có dấu)", SystemConfig.GetText("CompanyName") == "CÔNG TY THỬ NGHIỆM");
            repo.Save(new[]
            {
                new ConfigChange { Name = "SoFileSaoLuu", DataType = 3, IntValue = oldBackup },
                new ConfigChange { Name = "ChoPhepTrungTenKhachHang", DataType = 3, IntValue = oldTrung ? 30 : 0 },
                new ConfigChange { Name = "CompanyName", DataType = 1, TextValue = oldCompany }
            }, "selftest");
            SystemConfig.Reload(db);
            Check("Hoàn lại giá trị cũ", SystemConfig.GetInt("SoFileSaoLuu") == oldBackup && SystemConfig.GetText("CompanyName") == oldCompany);

            // ViewModel: sửa một ô tích + một ô số → IsDirty → Save → đọc lại → hoàn lại.
            var vm = new ConfigViewModel(db);
            Check("ViewModel mới nạp chưa có thay đổi", !vm.IsDirty);
            bool oldGiam = (bool)vm.Get("ChoPhepNhapGiamGia");
            decimal oldHuy = (decimal)vm.Get("ThoiGianChoPhepHuyBill");
            vm.Set("ChoPhepNhapGiamGia", !oldGiam);
            vm.Set("ThoiGianChoPhepHuyBill", oldHuy + 5m);
            Check("ViewModel báo có thay đổi", vm.IsDirty);
            int n = vm.Save("selftest");
            Check("ViewModel ghi đúng 2 khóa", n == 2, "n=" + n);
            Check("Sau Save SystemConfig thấy giá trị mới", SystemConfig.GetBool("ChoPhepNhapGiamGia") == !oldGiam && SystemConfig.GetInt("ThoiGianChoPhepHuyBill") == (int)oldHuy + 5);
            Check("Sau Save không còn thay đổi", !vm.IsDirty);
            vm.Set("ChoPhepNhapGiamGia", oldGiam);
            vm.Set("ThoiGianChoPhepHuyBill", oldHuy);
            vm.Save("selftest");
            Check("Hoàn lại qua ViewModel", SystemConfig.GetBool("ChoPhepNhapGiamGia") == oldGiam && SystemConfig.GetInt("ThoiGianChoPhepHuyBill") == (int)oldHuy);
            Check("Khóa giao diện cũ không hiện trên màn", vm.AllItems.TrueForAll(i => i.Name != "GiaoDienChuongTrinh" && i.Name != "DoToChuHienThi"));

            List<ConfigGroup> groups = repo.LoadVisibleGroups();
            Check("Nhóm hiển thị không có nhóm Ẩn", groups.Count > 0 && !groups.Exists(g => g.Name == "Ẩn"), "n=" + groups.Count);
            List<ConfigItem> items = repo.LoadItems();
            var badCombo = items.FindAll(i => i.Control == ConfigControl.Combo && i.ComboItems.Length == 0);
            Check("Mọi ô combo có danh sách mục", badCombo.Count == 0, string.Join(",", badCombo.ConvertAll(i => i.Name).ToArray()));
            Check("Khóa nội bộ (không nhãn) không hiện", !items.Exists(i => i.Name == "SharedCode" || i.Name == "CompliedCode" || i.Name == "DatabaseVersion"));
        }

        // Các bước sau (ảnh, người dùng, quyền, mật khẩu) đăng ký thêm ở đây.
        private static void RunExtra(string db)
        {
            ImageTests(db);
            UserTests(db);
            RoleTests(db);
            PasswordTests(db);
        }

        // ------------------------------------------------------------------ H5 Đổi mật khẩu

        private static void PasswordTests(string db)
        {
            Check("Mật khẩu cũ sai → chặn", PasswordPolicy.Check("abc", "x", "n", "n") == PasswordCheck.WrongOld);
            Check("Nhập lại không khớp → chặn", PasswordPolicy.Check("abc", "abc", "n1", "n2") == PasswordCheck.Mismatch);
            Check("Mới trùng cũ → chặn", PasswordPolicy.Check("abc", "abc", "abc", "abc") == PasswordCheck.SameAsOld);
            Check("Mới để trống → hỏi xác nhận (cho phép)", PasswordPolicy.Check("abc", "abc", "", "") == PasswordCheck.EmptyNeedsConfirm);
            Check("Admin mật khẩu rỗng đổi sang rỗng → trùng cũ, chặn", PasswordPolicy.Check("", "", "", "") == PasswordCheck.SameAsOld);
            Check("Hợp lệ", PasswordPolicy.Check("abc", "abc", "mới 1", "mới 1") == PasswordCheck.Ok);
            Check("Phân biệt hoa thường", PasswordPolicy.Check("abc", "ABC", "n", "n") == PasswordCheck.WrongOld);
            Check("Không giới hạn độ dài tối thiểu (1 ký tự hợp lệ)", PasswordPolicy.Check("abcdef", "abcdef", "1", "1") == PasswordCheck.Ok);

            var users = new UserAdminRepository(db);
            var groups = new GroupRepository(db);
            string gid = groups.Save(new GroupRow { Name = "NHÓM MẬT KHẨU" }, "selftest");
            string uid = users.Save(new UserRow { Name = "MK THỬ", UserName = "mk.thu", Password = "cu", GroupId = gid }, new string[0], "selftest");
            users.SetPassword(uid, "moi", "selftest");
            Check("Đổi mật khẩu ghi vào DB (chữ thường)", users.GetPassword(uid) == "moi");
            users.Delete(uid);
            groups.Delete(gid);
        }

        // ------------------------------------------------------------------ H2 Phân quyền

        private static void RoleTests(string db)
        {
            // Quy tắc đổi cờ.
            Check("RoleLogic: tích Thêm kéo theo Xem (0x50)", RoleLogic.SetBit(0, RoleModes.Add, true) == 0x50);
            Check("RoleLogic: bỏ Xem = khóa", RoleLogic.SetBit(0xF0, RoleModes.View, false) == 0);
            Check("RoleLogic: bỏ ô Khóa → Xem (0x10)", RoleLogic.SetLocked(0, false) == 0x10);
            Check("RoleLogic: tích Khóa → 0", RoleLogic.SetLocked(0xF0, true) == 0);
            Check("RoleLogic: Tất cả = 0xF0; chức năng chỉ-xem = 0x10", RoleLogic.SetAll(0x10, true, false) == 0xF0 && RoleLogic.SetAll(0, true, true) == 0x10);
            Check("RoleLogic: bỏ Sửa giữ Xem + Thêm", RoleLogic.SetBit(0x70, RoleModes.Edit, false) == 0x50);

            var roles = new RoleRepository(db);
            var groups = new GroupRepository(db);
            var users = new UserAdminRepository(db);
            List<FunctionRow> funcs = roles.LoadFunctions();
            Check("Đủ 81 chức năng trong ma trận", funcs.Count == 81, "n=" + funcs.Count);
            Check("Có chức năng chỉ-xem (NOTE=View) và chức năng đủ quyền", funcs.Exists(f => f.ViewOnly) && funcs.Exists(f => !f.ViewOnly));

            FunctionRow login = funcs.Find(f => f.Id == LoginService.LoginFunctionId);
            FunctionRow kh = funcs.Find(f => f.Name == "Danh mục khách hàng");
            Check("Có chức năng Đăng nhập và Danh mục khách hàng", login != null && kh != null);
            if (login == null || kh == null)
                return;

            string gid = groups.Save(new GroupRow { Name = "NHÓM QUYỀN THỬ" }, "selftest");
            string uid = users.Save(new UserRow { Name = "QUYỀN THỬ", UserName = "quyen.thu", Password = "q", GroupId = gid }, new string[0], "selftest");
            var svc = new LoginService();

            Check("Nhóm mới chưa có quyền: không đăng nhập được", svc.Login(db, "quyen.thu", "q").Code == LoginResultCode.NoLoginPermission);

            roles.SaveModes(gid, new Dictionary<string, int> { { login.Id, 0x10 }, { kh.Id, 0x50 } }, "selftest");
            Dictionary<string, int> modes = roles.LoadModes(gid);
            Check("Ghi quyền rồi đọc lại đúng cờ", modes[login.Id] == 0x10 && modes[kh.Id] == 0x50);
            LoginResult ok = svc.Login(db, "quyen.thu", "q");
            Check("Có quyền Đăng nhập → đăng nhập được", ok.Success);
            if (ok.Success)
            {
                FunctionPermissions p = ok.Session.Permissions;
                Check("Quyền khách hàng: Xem + Thêm, không Sửa/Xóa",
                    p.Can("Danh mục khách hàng", RoleModes.View) && p.Can("Danh mục khách hàng", RoleModes.Add) &&
                    !p.Can("Danh mục khách hàng", RoleModes.Edit) && !p.Can("Danh mục khách hàng", RoleModes.Remove));
                Check("Chức năng không có dòng quyền = khóa", !p.AllowsName("Danh mục mặt hàng") && p.ModeByName("Danh mục mặt hàng") == 0);
            }

            roles.SaveModes(gid, new Dictionary<string, int> { { kh.Id, 0xF0 } }, "selftest");
            Check("Cập nhật dòng có sẵn (không thêm dòng trùng)", roles.LoadModes(gid)[kh.Id] == 0xF0);
            using (FbConnection conn = KazukoConnection.Open(db))
                Check("Mỗi (nhóm, chức năng) chỉ một dòng",
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM SGROUPROLE WHERE SGROUPUSERID = @G AND SFUNCTIONID = @F", "@G", gid, "@F", kh.Id)) == 1);

            roles.SaveModes(gid, new Dictionary<string, int> { { login.Id, 0 } }, "selftest");
            Check("Khóa Đăng nhập → không đăng nhập được", svc.Login(db, "quyen.thu", "q").Code == LoginResultCode.NoLoginPermission);

            // Quyền xem báo cáo.
            List<ReportRow> reps = roles.LoadReports().FindAll(r => !r.IsFolder);
            Check("Có báo cáo để phân quyền (126 đang dùng)", reps.Count >= 100, "n=" + reps.Count);
            roles.SetReportsAllowed(gid, new[] { reps[0].Id, reps[1].Id }, true, "selftest");
            HashSet<string> allowed = roles.LoadAllowedReports(gid);
            Check("Cho xem 2 báo cáo", allowed.Count == 2 && allowed.Contains(reps[0].Id));
            roles.SetReportsAllowed(gid, new[] { reps[0].Id }, true, "selftest");
            Check("Cho xem lần nữa không nhân đôi dòng", roles.LoadAllowedReports(gid).Count == 2);
            roles.SetReportsAllowed(gid, new[] { reps[0].Id }, false, "selftest");
            Check("Bỏ quyền xem 1 báo cáo", roles.LoadAllowedReports(gid).Count == 1);

            users.Delete(uid);
            groups.Delete(gid);
            using (FbConnection conn = KazukoConnection.Open(db))
                Check("Xóa nhóm kéo theo dòng quyền sử dụng và quyền báo cáo",
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM SGROUPROLE WHERE SGROUPUSERID = @G", "@G", gid)) == 0 &&
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM SREPORTROLE WHERE SGROUPUSERID = @G", "@G", gid)) == 0);
        }

        // ------------------------------------------------------------------ H1/H2 Người dùng và nhóm

        private static void UserTests(string db)
        {
            var groups = new GroupRepository(db);
            var users = new UserAdminRepository(db);

            Check("Danh sách người dùng không có Admin", users.LoadUsers(null).TrueForAll(u => u.UserName != "Admin"));
            GroupRow thuNgan = groups.LoadAll().Find(g => g.Name == "Thu ngân");
            Check("Có nhóm Thu ngân", thuNgan != null);
            if (thuNgan != null)
            {
                int n = groups.CountReferences(thuNgan.Id);
                Check("Không xóa được nhóm còn tài khoản: đếm (1) như app gốc", n == 1, "n=" + n);
                Check("Thông báo xóa nhóm đúng chữ gốc",
                    ReferenceChecker.BlockedMessage("nhóm người dùng", "Thu ngân", n) ==
                    "Bạn không thể xóa nhóm người dùng 'Thu ngân' vì còn (1) dữ liệu khác liên kết tới 'Thu ngân'");
            }

            // Nhóm mới: AUTOID/SORTORDER theo quy ước, chặn trùng tên.
            int maxAuto;
            using (FbConnection conn = KazukoConnection.Open(db))
                maxAuto = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT MAX(AUTOID) FROM SGROUPUSER"));
            string gid = groups.Save(new GroupRow { Name = "NHÓM THỬ", Note = "ghi chú thử", ImageId = null }, "selftest");
            GroupRow g1 = groups.LoadAll().Find(g => g.Id == gid);
            Check("Thêm nhóm", g1 != null && g1.Name == "NHÓM THỬ" && g1.Note == "ghi chú thử");
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                var dt = DbHelper.Table(conn, null, "SELECT AUTOID, SORTORDER, ITEMTYPE, STATUS FROM SGROUPUSER WHERE ID = @I", "@I", gid);
                Check("Nhóm mới: AUTOID = lớn nhất + 1, SORTORDER dạng ZZZ###, STATUS 30",
                    DbHelper.Int(dt.Rows[0][0]) == maxAuto + 1 && (DbHelper.Str(dt.Rows[0][1]) ?? "").StartsWith("ZZZ") && DbHelper.Int(dt.Rows[0][3]) == 30,
                    "autoid=" + dt.Rows[0][0] + " sort=" + dt.Rows[0][1]);
            }
            Check("Chặn trùng tên nhóm (không phân biệt hoa thường)", groups.IsNameTaken("nhóm thử", null) && !groups.IsNameTaken("nhóm thử", gid));
            groups.Save(new GroupRow { Id = gid, Name = "NHÓM THỬ 2", Note = "", ImageId = null }, "selftest");
            Check("Sửa nhóm", groups.LoadAll().Exists(g => g.Id == gid && g.Name == "NHÓM THỬ 2"));

            // Tài khoản mới + cửa hàng.
            var stores = users.LoadStores(null);
            Check("Thêm mới: mọi cửa hàng được tích sẵn", stores.Count > 0 && stores.TrueForAll(x => x.Access));
            var storeIds = new List<string>();
            foreach (StoreAccess st in stores) storeIds.Add(st.StoreId);
            string uid = users.Save(new UserRow { Name = "NGƯỜI THỬ", UserName = "nguoi.thu", Password = "p1", Email = "t@t", GroupId = gid }, storeIds, "selftest");
            UserRow u1 = users.Get(uid);
            Check("Thêm tài khoản (tiếng Việt có dấu)", u1 != null && u1.Name == "NGƯỜI THỬ" && u1.UserName == "nguoi.thu" && u1.GroupId == gid);
            Check("Ghi quyền cửa hàng đúng số dòng", users.LoadStores(uid).FindAll(x => x.Access).Count == storeIds.Count);
            Check("Trùng tài khoản/họ tên bị phát hiện (không phân biệt hoa thường)", users.IsTaken("USERNAME", "NGUOI.THU", null) && users.IsTaken("NAME", "người thử", null));
            Check("Sửa chính tài khoản đó không báo trùng", !users.IsTaken("USERNAME", "nguoi.thu", uid) && !users.IsTaken("NAME", "NGƯỜI THỬ", uid));
            Check("Tài khoản hiện trong danh sách nhóm", users.LoadUsers(gid).Exists(x => x.Id == uid));

            // Sửa giữ nguyên các cột không có trên form.
            using (FbConnection conn = KazukoConnection.Open(db))
            using (FbTransaction tx = conn.BeginTransaction())
            {
                DbHelper.Exec(conn, tx, "UPDATE SUSER SET CARDCODE = 'THE-123', SMTP = 'smtp.x', PORT = 25, VIETINPUT = 30 WHERE ID = @I", "@I", uid);
                tx.Commit();
            }
            users.Save(new UserRow { Id = uid, Name = "NGƯỜI THỬ", UserName = "nguoi.thu", Password = "p2", Email = "x@x", GroupId = gid }, new string[0], "selftest");
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                var dt = DbHelper.Table(conn, null, "SELECT CARDCODE, SMTP, PORT, VIETINPUT, \"PASSWORD\", EMAIL, ISADMIN FROM SUSER WHERE ID = @I", "@I", uid);
                Check("Sửa giữ nguyên CARDCODE/SMTP/PORT/VIETINPUT",
                    DbHelper.Str(dt.Rows[0][0]) == "THE-123" && DbHelper.Str(dt.Rows[0][1]) == "smtp.x" && DbHelper.Int(dt.Rows[0][2]) == 25 && DbHelper.Int(dt.Rows[0][3]) == 30);
                Check("Sửa ghi mật khẩu/email mới", DbHelper.Str(dt.Rows[0][4]) == "p2" && DbHelper.Str(dt.Rows[0][5]) == "x@x");
            }
            Check("Bỏ tích hết cửa hàng → không còn dòng cửa hàng", users.LoadStores(uid).TrueForAll(x => !x.Access));

            // Đổi mật khẩu.
            Check("GetPassword đọc đúng", users.GetPassword(uid) == "p2");
            users.SetPassword(uid, "p3", "selftest");
            Check("SetPassword ghi mật khẩu mới (chữ thường)", users.GetPassword(uid) == "p3");

            // Xóa: nhóm còn tài khoản bị chặn; xóa tài khoản rồi xóa nhóm.
            Check("Nhóm thử còn 1 tài khoản → chặn xóa", groups.CountReferences(gid) == 1);
            Check("Tài khoản chưa có hóa đơn → xóa được", users.CountReferences(uid) == 0);
            users.Delete(uid);
            Check("Xóa tài khoản", users.Get(uid) == null);
            Check("Nhóm hết tài khoản → xóa được", groups.CountReferences(gid) == 0);
            groups.Delete(gid);
            Check("Xóa nhóm", !groups.LoadAll().Exists(g => g.Id == gid));
            using (FbConnection conn = KazukoConnection.Open(db))
                Check("Xóa nhóm kéo theo dòng quyền (không còn mồ côi)",
                    DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM SGROUPROLE WHERE SGROUPUSERID = @G", "@G", gid)) == 0);
        }

        // ------------------------------------------------------------------ H4 Ảnh

        private static byte[] SamplePng(System.Drawing.Color c)
        {
            using (var bmp = new System.Drawing.Bitmap(8, 8))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    g.Clear(c);
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        private static void ImageTests(string db)
        {
            var repo = new ImageRepository(db);
            int before = repo.LoadAll().Count;
            byte[] a = SamplePng(System.Drawing.Color.Red), b = SamplePng(System.Drawing.Color.Blue);
            string id = repo.Add("ẢNH THỬ", a, "selftest");
            Check("Thêm ảnh: số ảnh +1", repo.LoadAll().Count == before + 1);
            Check("Ảnh đọc lại giống ảnh đã ghi", Equal(repo.Get(id), a));
            repo.Replace(id, b, "selftest");
            Check("Thay thế ảnh", Equal(repo.Get(id), b));
            Check("Ảnh mới chưa ai dùng (CountUsage = 0)", repo.CountUsage(id) == 0);

            string usedId;
            using (FbConnection conn = KazukoConnection.Open(db))
                usedId = DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT FIRST 1 SIMAGEID FROM SGROUPUSER WHERE SIMAGEID IS NOT NULL"));
            Check("Ảnh đang được nhóm người dùng dùng thì CountUsage > 0", usedId != null && repo.CountUsage(usedId) > 0);
            Check("Thông báo chặn xóa đúng chữ gốc",
                ReferenceChecker.BlockedMessage("ảnh", "Sao", 3) == "Bạn không thể xóa ảnh 'Sao' vì còn (3) dữ liệu khác liên kết tới 'Sao'");

            repo.Delete(id);
            Check("Xóa ảnh không còn dùng", repo.LoadAll().Count == before && repo.Get(id) == null);
        }

        private static bool Equal(byte[] x, byte[] y)
        {
            if (x == null || y == null || x.Length != y.Length)
                return false;
            for (int i = 0; i < x.Length; i++)
                if (x[i] != y[i])
                    return false;
            return true;
        }
    }
}
