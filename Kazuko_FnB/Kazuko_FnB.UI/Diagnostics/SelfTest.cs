using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Services;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI.Diagnostics
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
            ThemeTests();
            CatalogTests(db);
            PhieuThuTests(db);
            ImageTests(db);
            UserTests(db);
            RoleTests(db);
            PasswordTests(db);
            ProductTests(db);
        }

        // ------------------------------------------------------------------ H6 Danh mục mặt hàng

        private static ProductValidationInput ValidBrewInput()
        {
            return new ProductValidationInput
            {
                TypeId = "1",
                TypeHasRecipe = true,
                UseTwoUnits = false,
                QuyDoi = 0m,
                UnitId = "u1",
                BigUnitId = null,
                Lines = new List<RecipeValidationLine>
                {
                    new RecipeValidationLine { MaterialId = "m1", MaterialTypeId = "2", Quantity = 1m }
                }
            };
        }

        private static void ProductTests(string db)
        {
            // Luật thuần 2.1 (dừng ở lỗi đầu, quyết định 5.6 gộp #1/#2).
            ProductValidationInput input = ValidBrewInput();
            input.UseTwoUnits = true;
            input.BigUnitId = "u2";
            input.QuyDoi = 0m;
            ProductValidationResult vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #1 thiếu quy đổi → chặn + focus ô Quy đổi", vr.Error == "Mời bạn nhập quy đổi" && vr.FocusQuyDoi, vr.Error);
            input.QuyDoi = 5m;
            input.BigUnitId = "u1";
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #2 ĐVT lẻ = ĐVT chẵn → chặn", vr.Error == "Đơn vị lẻ và đơn vị chẵn không được trùng nhau", vr.Error);
            input.QuyDoi = 0m;
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 gộp #1/#2: dừng ở lỗi đầu (#1)", vr.Error == "Mời bạn nhập quy đổi", vr.Error);
            input = ValidBrewInput();
            input.TypeId = "5";
            input.Lines[0].MaterialTypeId = "1"; // tránh #8: Combo mở chứa nguyên liệu loại 2 sẽ bị chặn
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #3 Combo mở chỉ cảnh báo, vẫn lưu", vr.IsValid && vr.Warning == "Phiên bản hiện tại mới hỗ trợ combo mở ở giao diện quản trị", vr.Warning);
            input = ValidBrewInput();
            input.TypeId = "0";
            input.TypeHasRecipe = false;
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #4 loại 0/2/6 có định lượng → chặn", vr.Error == "Mặt hàng này không có vật tư / định lượng đi kèm", vr.Error);
            input = ValidBrewInput();
            input.Lines = new List<RecipeValidationLine>();
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #5 pha chế không có định lượng → chặn", vr.Error == "Vui lòng nhập vật tư/định lượng cho mặt hàng", vr.Error);
            input = ValidBrewInput();
            input.Lines[0].Quantity = 0m;
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #6 số lượng 0 → chặn đúng dòng", vr.Error == "Mời bạn nhập số lượng cho định lượng" && vr.ErrorLineIndex == 0, vr.Error);
            input = ValidBrewInput();
            input.Lines[0].MaterialId = null;
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #7 chưa chọn vật tư → chặn", vr.Error == "Mời bạn nhập vật tư cho định lượng", vr.Error);
            input = ValidBrewInput();
            input.TypeId = "4";
            vr = ProductRules.ValidateBeforeSave(input);
            Check("2.1 #8 Combo chứa nguyên liệu → chặn", vr.Error == "Combo không được phép chứa nguyên vật liệu", vr.Error);
            Check("2.1 pha chế hợp lệ → lưu được", ProductRules.ValidateBeforeSave(ValidBrewInput()).IsValid);

            // Sinh mã 3.5 + công thức giá 3.4 + chuyển loại 3.2.
            string code = ProductRules.GenerateCode("BIA", 7, new Random(1));
            bool codeOk = code != null && code.Length == 7 && code.StartsWith("BIA", StringComparison.Ordinal);
            if (codeOk)
            {
                for (int i = 3; i < code.Length; i++)
                    codeOk = codeOk && char.IsDigit(code[i]);
            }
            Check("3.5 sinh mã = tiền tố + số ngẫu nhiên, đủ 7 ký tự", codeOk, code);
            Check("3.5 nhóm không có tiền tố → không sinh mã", ProductRules.GenerateCode("", 7, new Random(1)) == null);
            Check("3.5 sinh mã trùng hết → null sau 10 lần thử",
                ProductRules.GenerateUniqueCode("BIA", 7, delegate { return true; }, new Random(1)) == null);
            string unique = ProductRules.GenerateUniqueCode("BIA", 7, delegate { return false; }, new Random(1));
            Check("3.5 sinh mã không trùng → trả mã hợp lệ", unique != null && unique.Length == 7 && unique.StartsWith("BIA", StringComparison.Ordinal), unique);
            Check("3.4 giá nhập lẻ = GIANHAP / QUYDOI", ProductMath.UnitBuyPrice(12000m, 12m, true) == 1000m);
            Check("3.4 không có ĐVT chẵn → giá nhập lẻ = GIANHAP", ProductMath.UnitBuyPrice(12000m, null, false) == 12000m);
            Check("3.4 thành tiền nhập = SL × giá lẻ", ProductMath.AmountBuy(2m, 1000m) == 2000m);
            Check("3.4 thành tiền vốn = SL × giá vốn", ProductMath.AmountCost(2m, 8000m) == 16000m);
            decimal totalBuy, totalCost;
            ProductRules.Totals(new[]
            {
                new RecipeLine { AmountBuy = 2000m, AmountCost = 16000m },
                new RecipeLine { AmountBuy = 500m, AmountCost = 4000m }
            }, out totalBuy, out totalCost);
            Check("3.4 tổng định lượng", totalBuy == 2500m && totalCost == 20000m, totalBuy + "/" + totalCost);
            Check("3.2 thêm dòng cho loại không định lượng → chuyển pha chế", ProductRules.SwitchTypeForRecipe("0", false) == "1");
            Check("3.2 loại đã có định lượng giữ nguyên", ProductRules.SwitchTypeForRecipe("4", true) == "4");
            Check("5.7 chỉ tạo mới được loại 0–3 qua dialog", ProductRules.CanCreateType("0") && ProductRules.CanCreateType("3") && !ProductRules.CanCreateType("4") && !ProductRules.CanCreateType("7"));

            // Quyền 4: Thu ngân Xem + Thêm (0x50), không Sửa/Xóa, không Xem giá nhập.
            var perms = new FunctionPermissions(false);
            perms.Add("9bcaf32e-e0c6-4b62-8ab4-65afb965bd2e", "Danh mục mặt hàng", 0x50);
            Check("4 Thu ngân: Xem + Thêm", perms.Can("Danh mục mặt hàng", RoleModes.View) && perms.Can("Danh mục mặt hàng", RoleModes.Add));
            Check("4 Thu ngân: không Sửa/Xóa", !perms.Can("Danh mục mặt hàng", RoleModes.Edit) && !perms.Can("Danh mục mặt hàng", RoleModes.Remove));
            Check("4 Thu ngân: không có Xem giá nhập", !perms.Can("Xem giá nhập", RoleModes.View));
            Check("4 không có dòng quyền = khóa", !new FunctionPermissions(false).Can("Danh mục mặt hàng", RoleModes.View));

            // DB: tạo nhóm/vật tư/mặt hàng pha chế → kiểm lưu, trùng, cascade giá, chặn xóa, dọn sạch.
            var productRepo = new ProductRepository(db);
            var groupRepo = new ProductGroupRepository(db);
            List<LookupItem> units = new FormRepository(db).LoadAll("DDONVITINH");
            Check("Có đơn vị tính để thử", units.Count > 0, "n=" + units.Count);
            if (units.Count == 0)
                return;
            string unitId = units[0].Id;
            string groupId = null;
            string materialId = null;
            string brewId = null;
            try
            {
                string gerr;
                groupId = groupRepo.Save(new ProductGroupRow { Name = "NHÓM THỬ DMH", Code = "THU" }, "selftest", out gerr);
                Check("Tạo nhóm thử", gerr == null && groupId != null, gerr);
                if (groupId == null)
                    return;
                var options = new ProductSaveOptions { AllowDuplicateName = false, CodeRequired = true, AutoUpdateBuyPrice = true, AutoUpdateCostPrice = true };
                var material = ProductRow.NewDefault();
                material.Name = "VẬT TƯ THỬ DMH";
                material.TypeId = "2";
                material.UnitId = unitId;
                material.GroupId = groupId;
                material.Code = "VTDMH001";
                material.BuyPrice = 12000m;
                material.CostPrice = 8000m;
                ProductSaveResult saved = productRepo.SaveProduct(material, new List<RecipeLine>(), "selftest", options);
                Check("Lưu vật tư", saved.Ok && saved.SavedId != null, saved.ErrorMessage);
                materialId = saved.SavedId;
                if (materialId == null)
                    return;
                ProductRow back = productRepo.LoadProduct(materialId);
                Check("3.1b thêm mới ghi đúng STATUS/ID/người tạo", back != null && back.Status == 30 && back.CreatedById == "selftest" && back.CreatedAt.HasValue);
                var dupName = ProductRow.NewDefault();
                dupName.Name = "VẬT TƯ THỬ DMH";
                dupName.TypeId = "2";
                dupName.UnitId = unitId;
                dupName.GroupId = groupId;
                dupName.Code = "VTDMH002";
                ProductSaveResult dup = productRepo.SaveProduct(dupName, new List<RecipeLine>(), "selftest", options);
                Check("Trùng tên bị chặn (nguyên văn)", !dup.Ok && dup.ErrorMessage == ProductText.NameDuplicate("VẬT TƯ THỬ DMH"), dup.ErrorMessage);
                var dupCode = ProductRow.NewDefault();
                dupCode.Name = "VẬT TƯ KHÁC DMH";
                dupCode.TypeId = "2";
                dupCode.UnitId = unitId;
                dupCode.GroupId = groupId;
                dupCode.Code = "VTDMH001";
                dup = productRepo.SaveProduct(dupCode, new List<RecipeLine>(), "selftest", options);
                Check("Trùng mã bị chặn (nguyên văn)", !dup.Ok && dup.ErrorMessage == ProductText.CodeDuplicate("VTDMH001"), dup.ErrorMessage);

                // Hồi quy: thêm mới → trùng tên (lỗi) → sửa tên → lưu lại phải ghi thật, không để định lượng mồ côi.
                var retry = ProductRow.NewDefault();
                retry.Name = "VẬT TƯ THỬ DMH";
                retry.TypeId = "1";
                retry.UnitId = unitId;
                retry.GroupId = groupId;
                retry.Code = "MHDMH0RT";
                var retryLines = new List<RecipeLine> { new RecipeLine { MaterialId = materialId, Quantity = 1m } };
                ProductSaveResult r1 = productRepo.SaveProduct(retry, retryLines, "selftest", options);
                Check("Hồi quy: lưu mới trùng tên → lỗi và row.Id vẫn rỗng", !r1.Ok && string.IsNullOrEmpty(retry.Id), "Id=" + retry.Id);
                retry.Name = "MẶT HÀNG THỬ LẠI DMH";
                ProductSaveResult r2 = productRepo.SaveProduct(retry, retryLines, "selftest", options);
                Check("Hồi quy: sửa tên rồi lưu lại → Ok", r2.Ok && !string.IsNullOrEmpty(r2.SavedId), r2.ErrorMessage);
                if (r2.SavedId != null)
                {
                    ProductRow retryBack = productRepo.LoadProduct(r2.SavedId);
                    Check("Hồi quy: ghi thật vào DMATHANG (STATUS 30, đúng tên)", retryBack != null && retryBack.Status == 30 && retryBack.Name == "MẶT HÀNG THỬ LẠI DMH");
                    int retryLinesN = productRepo.LoadRecipeLines(r2.SavedId).Count;
                    Check("Hồi quy: định lượng đúng 1 dòng", retryLinesN == 1, "n=" + retryLinesN);
                    productRepo.DeleteProduct(r2.SavedId);
                }
                int retryOrphans = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DDINHLUONG D WHERE D.DVATTUID = @M AND NOT EXISTS (SELECT 1 FROM DMATHANG P WHERE P.ID = D.DMATHANGID)", "@M", materialId)));
                Check("Hồi quy: không có dòng DDINHLUONG mồ côi", retryOrphans == 0, "n=" + retryOrphans);
                var ghost = ProductRow.NewDefault();
                ghost.Id = "KHONG-TON-TAI-DMH";
                ghost.Name = "MA DMH";
                ghost.Code = "MADMH001";
                ghost.TypeId = "2";
                ghost.UnitId = unitId;
                ghost.GroupId = groupId;
                ProductSaveResult rg = productRepo.SaveProduct(ghost, new List<RecipeLine>(), "selftest", options);
                Check("Sửa dòng không tồn tại → lỗi, không báo Ok", !rg.Ok && rg.ErrorMessage == "Mặt hàng không còn tồn tại.", rg.ErrorMessage);

                var brew = ProductRow.NewDefault();
                brew.Name = "MẶT HÀNG THỬ DMH";
                brew.TypeId = "1";
                brew.UnitId = unitId;
                brew.GroupId = groupId;
                brew.Code = "MHDMH001";
                brew.SellPrice = 30000m;
                ProductSaveResult savedBrew = productRepo.SaveProduct(brew,
                    new List<RecipeLine> { new RecipeLine { MaterialId = materialId, Quantity = 2m } }, "selftest", options);
                Check("Lưu mặt hàng pha chế + định lượng", savedBrew.Ok && savedBrew.SavedId != null, savedBrew.ErrorMessage);
                brewId = savedBrew.SavedId;
                if (brewId == null)
                    return;
                ProductRow brewBack = productRepo.LoadProduct(brewId);
                Check("3.4 GIANHAP = tổng thành tiền nhập (2 × 12000)", brewBack != null && brewBack.BuyPrice == 24000m, brewBack == null ? "null" : brewBack.BuyPrice.ToString());
                Check("3.4 GIAVON = tổng thành tiền vốn (2 × 8000)", brewBack != null && brewBack.CostPrice == 16000m, brewBack == null ? "null" : brewBack.CostPrice.ToString());
                List<RecipeLine> lines = productRepo.LoadRecipeLines(brewId);
                Check("Định lượng đọc lại đủ dòng", lines.Count == 1 && lines[0].MaterialId == materialId && lines[0].Quantity == 2m, "n=" + lines.Count);

                ProductDeleteResult blocked = productRepo.DeleteProduct(materialId);
                Check("3.6 xóa vật tư còn tham chiếu → chặn", !blocked.Ok, blocked.ErrorMessage);
                ProductDeleteResult deletedBrew = productRepo.DeleteProduct(brewId);
                Check("Xóa hẳn mặt hàng pha chế", deletedBrew.Ok, deletedBrew.ErrorMessage);
                brewId = null;
                int orphans = DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DDINHLUONG WHERE DMATHANGID = @P", "@P", savedBrew.SavedId)));
                Check("Xóa không để lại dòng định lượng mồ côi", orphans == 0, "n=" + orphans);
                ProductDeleteResult deletedMat = productRepo.DeleteProduct(materialId);
                Check("Xóa vật tư sau khi hết tham chiếu", deletedMat.Ok, deletedMat.ErrorMessage);
                materialId = null;
                ProductDeleteResult km = productRepo.DeleteProduct(ProductRepository.SystemProductId);
                Check("Không xóa được mặt hàng hệ thống 'Khuyến mại tự động'", !km.Ok, km.ErrorMessage);
                string delGroup = groupRepo.Delete(groupId);
                Check("Xóa nhóm thử", delGroup == null, delGroup);
                groupId = null;
            }
            finally
            {
                try { if (brewId != null) productRepo.DeleteProduct(brewId); } catch { }
                try { if (materialId != null) productRepo.DeleteProduct(materialId); } catch { }
                try { if (groupId != null) groupRepo.Delete(groupId); } catch { }
            }
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
            Check("LamTronTien: 1499 → 1000 (bội 1000)", Kazuko_FnB.Services.MoneyRounding.RoundToStep(1499m, 1000m) == 1000m);
            Check("LamTronTien: 1500 → 2000 (hòa làm tròn xa 0)", Kazuko_FnB.Services.MoneyRounding.RoundToStep(1500m, 1000m) == 2000m);
            Check("LamTronTien: -1500 → -2000 (xa 0 cả số âm)", Kazuko_FnB.Services.MoneyRounding.RoundToStep(-1500m, 1000m) == -2000m);
            Check("LamTronTien: 0 → 0", Kazuko_FnB.Services.MoneyRounding.RoundToStep(0m, 1000m) == 0m);
            bool stepThrew = false;
            try { Kazuko_FnB.Services.MoneyRounding.RoundToStep(1m, 0m); }
            catch (ArgumentOutOfRangeException) { stepThrew = true; }
            Check("LamTronTien: bước 0 ném ArgumentOutOfRangeException", stepThrew);

            // Thành phần UI dùng chung (không cần hiện cửa sổ).
            try
            {
                using (var cm = new Kazuko_FnB.UI.Controls.ThemedContextMenu())
                {
                    cm.AddItem("Mục A", null);
                    cm.AddSeparator();
                    cm.AddItem("Mục B", System.Windows.Forms.Keys.F5, null);
                    Check("ThemedContextMenu: 2 mục + 1 vạch ngăn", cm.Items.Count == 3);
                    Check("ThemedContextMenu: dùng ThemedMenuRenderer của menu chính", cm.Renderer is Kazuko_FnB.UI.Controls.ThemedMenuRenderer);
                    Check("ThemedContextMenu: mục B có phím tắt F5", ((System.Windows.Forms.ToolStripMenuItem)cm.Items[2]).ShortcutKeys == System.Windows.Forms.Keys.F5);
                }
                using (var panel = new System.Windows.Forms.Panel())
                {
                    var ib = new Kazuko_FnB.UI.Controls.InputBox();
                    var plain = new System.Windows.Forms.Button();
                    panel.Controls.Add(ib);
                    panel.Controls.Add(plain);
                    Kazuko_FnB.Theme.ThemeTokens t0 = Kazuko_FnB.Theme.ThemeManager.Current;
                    Kazuko_FnB.UI.ThemeApplier.Apply(panel, t0);
                    Check("ThemeApplier tô InputBox theo token (focus = Primary, chữ = Text)", ib.FocusColor == t0.Primary && ib.TextColor == t0.Text);
                    Check("ThemeApplier tô Button thô theo theme (nền = Surface, phẳng)", plain.BackColor == t0.Surface && plain.FlatStyle == System.Windows.Forms.FlatStyle.Flat);
                }
                using (var f = new Kazuko_FnB.UI.BaseForm())
                    Check("BaseForm: font Segoe UI 10pt, AutoScaleMode.None",
                        f.Font.Name == "Segoe UI" && Math.Abs(f.Font.Size - 10f) < 0.01f && f.AutoScaleMode == System.Windows.Forms.AutoScaleMode.None);
            }
            catch (Exception ex)
            {
                Check("Thành phần UI dùng chung dựng được", false, ex.GetType().Name + ": " + ex.Message);
            }

            var roles = new RoleRepository(db);
            var groups = new GroupRepository(db);
            var users = new UserAdminRepository(db);
            List<FunctionRow> funcs = roles.LoadFunctions();
            Check("Đủ 86 chức năng trong ma trận", funcs.Count == 86, "n=" + funcs.Count);
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

        // ------------------------------------------------------------------ Bản thử Phiếu thu (layout từ SFORM.AELAYOUT)

        private static void PhieuThuTests(string db)
        {
            // Luật thuần (SFORM.CODE của form gốc).
            ObjectFieldState s0 = PhieuThuRules.ForObjectType(0), s1 = PhieuThuRules.ForObjectType(1),
                s2 = PhieuThuRules.ForObjectType(2), s3 = PhieuThuRules.ForObjectType(3), sx = PhieuThuRules.ForObjectType(-1);
            Check("Loại đối tượng 0 (khác): bật tên và địa chỉ, tắt nhân viên/khách/NCC",
                s0.Name && s0.Address && !s0.Employee && !s0.Customer && !s0.Supplier);
            Check("Loại đối tượng 1 (nhân viên): chỉ bật ô nhân viên", s1.Employee && !s1.Customer && !s1.Supplier && !s1.Name && !s1.Address);
            Check("Loại đối tượng 2 (khách hàng): chỉ bật ô khách hàng", s2.Customer && !s2.Employee && !s2.Supplier && !s2.Name && !s2.Address);
            Check("Loại đối tượng 3 (nhà cung cấp): chỉ bật ô nhà cung cấp", s3.Supplier && !s3.Employee && !s3.Customer && !s3.Name && !s3.Address);
            Check("Chưa chọn loại: tắt hết", !sx.Name && !sx.Address && !sx.Employee && !sx.Customer && !sx.Supplier);
            string n, a;
            PhieuThuRules.FillFromEmployee("Bùi Thu Minh", out n, out a);
            Check("Chọn nhân viên: tên = tên nhân viên, địa chỉ trống", n == "Bùi Thu Minh" && a == "");
            PhieuThuRules.FillFromParty("Anh Chung", "12 Xuân Thủy", out n, out a);
            Check("Chọn khách/nhà cung cấp: tên và địa chỉ của họ", n == "Anh Chung" && a == "12 Xuân Thủy");
            PhieuThuRules.FillFromParty(null, null, out n, out a);
            Check("Bỏ chọn: xóa tên và địa chỉ", n == "" && a == "");
            Check("Tích chuyển khoản bật ô tài khoản ngân hàng", PhieuThuRules.BankAccountEnabled(true) && !PhieuThuRules.BankAccountEnabled(false));
            Check("Chuyển khoản mà chưa chọn tài khoản: báo đúng chữ gốc",
                PhieuThuRules.Validate(null, true, false) == "Mời bạn chọn tài khoản ngân hàng");
            Check("Chuyển khoản đã chọn tài khoản hoặc không chuyển khoản: hợp lệ",
                PhieuThuRules.Validate(null, true, true) == null && PhieuThuRules.Validate(new List<string>(), false, false) == null);
            Check("Thiếu trường bắt buộc: báo tên trường",
                PhieuThuRules.Validate(new List<string> { "Số phiếu", "Cửa hàng" }, false, false) == "Mời bạn nhập: Số phiếu, Cửa hàng");

            // Layout đọc từ DB.
            var repo = new FormRepository(db);
            ScreenLayout lay = repo.LoadLayout("Phiếu thu");
            Check("Đọc được layout form Phiếu thu từ SFORM.AELAYOUT", lay != null);
            if (lay == null)
                return;
            Check("Layout: bảng đích TTHUCHI, kích thước gốc 527x379", lay.Table == "TTHUCHI" && lay.Width == 527 && lay.Height == 379,
                lay.Table + " " + lay.Width + "x" + lay.Height);
            Check("Layout: 21 dòng ánh xạ trường", lay.Maps.Count == 21, "n=" + lay.Maps.Count);
            var required = new List<string>();
            foreach (FieldMap m in lay.Maps)
                if (m.Required)
                    required.Add(m.DataField);
            required.Sort();
            Check("Layout: đúng 3 trường bắt buộc (CUAHANG, NAME, THU)", string.Join(",", required.ToArray()) == "DCUAHANGID,NAME,THU", string.Join(",", required.ToArray()));
            int editors = lay.Controls.FindAll(c => c.Kind == LayoutKind.Text || c.Kind == LayoutKind.Number || c.Kind == LayoutKind.Date
                || c.Kind == LayoutKind.Combo || c.Kind == LayoutKind.Lookup || c.Kind == LayoutKind.Check).Count;
            Check("Layout: 17 ô nhập và ô tích", editors == 17, "n=" + editors);
            LayoutControl cbo = lay.Find("cboLOAIDOITUONG");
            Check("Layout: ô Loại đối tượng có 4 mục đúng thứ tự",
                cbo != null && string.Join("|", cbo.Items.ToArray()) == "Đối tượng khác|Nhân viên|Khách hàng|Nhà cung cấp");
            LayoutControl kh = lay.Find("lueDKHACHHANGID");
            Check("Layout: ô Khách hàng lúc đầu bị tắt", kh != null && !kh.Enabled);

            Dictionary<string, string> tables = repo.LookupTables("TTHUCHI");
            Check("Ô tra cứu trỏ đúng bảng (lý do, khách hàng, nhân viên, nhà cung cấp, cửa hàng, tài khoản ngân hàng)",
                tables.ContainsKey("DLYDOTHUCHIID") && tables["DLYDOTHUCHIID"] == "DLYDOTHUCHI"
                && tables["DKHACHHANGID"] == "DKHACHHANG" && tables["DNHANVIENID"] == "DNHANVIEN" && tables["DNHACUNGCAPID"] == "DNHACUNGCAP"
                && tables["DCUAHANGID"] == "DCUAHANG" && tables["DTAIKHOANNGANHANGID"] == "DTAIKHOANNGANHANG");

            int reasons, allCustomers, userStores;
            string anyUser;
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                reasons = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DLYDOTHUCHI WHERE STATUS = 30 AND LALYDOTHU = 30"));
                allCustomers = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM DKHACHHANG WHERE STATUS = 30"));
                anyUser = DbHelper.Str(DbHelper.Scalar(conn, null, "SELECT FIRST 1 SUSERID FROM TNGUOIDUNGTHEOCUAHANG"));
                userStores = anyUser == null ? 0 : DbHelper.Int(DbHelper.Scalar(conn, null,
                    "SELECT COUNT(*) FROM DCUAHANG WHERE STATUS = 30 AND ID IN (SELECT DCUAHANGID FROM TNGUOIDUNGTHEOCUAHANG WHERE SUSERID = @u)", "@u", anyUser));
            }
            Check("Danh sách lý do thu = số dòng LALYDOTHU = 30", repo.LoadIncomeReasons().Count == reasons, "n=" + reasons);
            Check("Danh sách khách hàng = số dòng đang dùng", repo.LoadAll("DKHACHHANG").Count == allCustomers, "n=" + allCustomers);
            if (anyUser != null)
                Check("Cửa hàng của người dùng không phải admin = các cửa hàng được gán", repo.LoadStoresOfUser(anyUser).Count == userStores, "n=" + userStores);

            // Mục menu: "Tạo phiếu thu" mở bản thử, "Tạo phiếu chi" thì chưa.
            MenuNode thu = null, chi = null;
            foreach (MenuNode node in Flatten(new MenuRepository(db).LoadTree()))
            {
                if (node.Title == "Tạo phiếu thu") thu = node;
                if (node.Title == "Tạo phiếu chi") chi = node;
            }
            UserSession sess = AppSession.Current;
            Check("Menu \"Tạo phiếu thu\" mở bản thử Phiếu thu", thu != null && UI.Dialogs.ScreenFactory.IsNewIncomeVoucher(thu, sess));
            Check("Menu \"Tạo phiếu chi\" chưa có màn (vẫn là tab tạm)", chi != null && !UI.Dialogs.ScreenFactory.IsNewIncomeVoucher(chi, sess));

            // Dựng màn từ layout (không cần cửa sổ).
            try
            {
                using (var form = new UI.DynamicForm(lay))
                {
                    int count = 0;
                    foreach (string nm in form.EditorNames)
                        count++;
                    Check("Màn dựng đủ 17 ô nhập từ layout", count == 17, "n=" + count);
                    Check("Mọi control trong layout đều dựng được (không bỏ sót)", form.Skipped.Count == 0, string.Join(",", new List<string>(form.Skipped).ToArray()));
                    var lblNum = form.LabelOf("txtNAME");
                    Check("Trường bắt buộc có dấu * ở nhãn", lblNum != null && lblNum.Text.EndsWith("*"));
                    Check("Ô tra cứu và ô chọn dựng đúng loại", form.Editor("lueDKHACHHANGID") is UI.LookupCombo && form.Editor("cboLOAIDOITUONG") is System.Windows.Forms.ComboBox);
                }
            }
            catch (Exception ex)
            {
                Check("Dựng màn từ layout", false, ex.GetType().Name + ": " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ Danh mục dùng chung (xem)

        private static readonly string[] CatalogTables =
        {
            "DBANGGIA", "DCALAMVIEC", "DCUAHANG", "DKHOHANG", "DLOAIPHONG", "DLYDOTHUCHI", "DNHANVIEN", "DTAIKHOANNGANHANG",
            "DBAN", "DDOTKHUYENMAI", "DKHACHHANG", "DMATHANG", "DMATKHAUWIFI", "DNHACUNGCAP", "DTHETRATRUOC", "DVOUCHER"
        };

        private static void CatalogTests(string db)
        {
            // Hàm thuần (không cần DB).
            Check("Tìm không dấu: 'Đặng Văn Ơi' → 'dang van oi'", CatalogRepository.Fold("Đặng Văn Ơi") == "dang van oi");
            Check("Tìm không dấu: 'Cà phê Sữa' → 'ca phe sua'", CatalogRepository.Fold("Cà phê Sữa") == "ca phe sua");
            Check("Thoát ký tự đặc biệt khi lọc", CatalogRepository.EscapeLike("a'b[c]*%") == "a''b[[]c[]][*][%]");

            List<GridColumnSpec> g = CatalogRepository.ParseGrid("CODE|63|true|NAME|217|false|NOTE|56|true ");
            Check("Đọc cấu hình lưới SGRID (3 cột, cột 2 ẩn, độ rộng 217)",
                g.Count == 3 && g[0].Name == "CODE" && g[0].Visible && g[1].Name == "NAME" && !g[1].Visible && g[1].Width == 217 && g[2].Visible);

            var nodes = new Dictionary<string, CatalogNode>
            {
                { "a", new CatalogNode { Id = "a", ParentId = null } },
                { "b", new CatalogNode { Id = "b", ParentId = "a" } },
                { "c", new CatalogNode { Id = "c", ParentId = "b" } }
            };
            Check("Đường đi nhóm cha → con", CatalogRepository.CategoryPath(nodes, "c") == "|a|b|c|");
            nodes["a"].ParentId = "c";
            Check("Vòng lặp nhóm không treo máy", CatalogRepository.CategoryPath(nodes, "c").Length > 0);

            // Trên DB.
            var repo = new CatalogRepository(db);
            var before = new Dictionary<string, string>();
            foreach (string t in CatalogTables)
                before[t] = Stamp(db, t);

            int treeCount = 0;
            foreach (string t in CatalogTables)
            {
                string id = repo.FindIdByTable(t);
                Check("[" + t + "] có định nghĩa trong STABLEDESC", id != null);
                if (id == null)
                    continue;
                Check("[" + t + "] dùng được màn danh mục chung", repo.IsSupported(id));
                CatalogDefinition def = repo.LoadDefinition(id);
                Check("[" + t + "] có cột hiển thị (kể cả cột tên)", def != null && def.Columns.Count > 0 && def.Columns.Exists(c => c.Name == "NAME"),
                    def == null ? "null" : def.Columns.Count + " cột");
                if (def == null)
                    continue;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                CatalogData data = repo.Load(def);
                sw.Stop();
                int expected;
                using (FbConnection conn = KazukoConnection.Open(db))
                    expected = DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM \"" + t + "\" WHERE STATUS = 30"));
                Check("[" + t + "] số dòng hiện = số dòng STATUS = 30", data.Rows.Count == expected, data.Rows.Count + " / " + expected + ", đọc " + sw.ElapsedMilliseconds + " ms");
                if (def.CategoryTable != null)
                {
                    treeCount++;
                    Check("[" + t + "] cây nhóm đọc được", data.Nodes.Count >= 0 && def.CategoryColumn != null);
                }
            }
            Check("Có 4 danh mục dạng cây nhóm (bàn, đợt khuyến mại, khách hàng, mặt hàng)", treeCount == 4, "n=" + treeCount);

            string khId = repo.FindIdByTable("DKHACHHANG");
            if (khId != null)
            {
                CatalogDefinition kh = repo.LoadDefinition(khId);
                Check("Khách hàng: thứ tự cột theo SGRID của app gốc (Tên, Nhóm, Mã khách…), ghi chú ẩn",
                    kh.Columns.Count == 10 && kh.Columns[0].Name == "NAME" && kh.Columns[1].Name == "DNHOMKHACHHANGID" && kh.Columns[2].Name == "MAKHACH"
                    && !kh.Columns.Exists(c => c.Name == "NOTE"), kh.Columns.Count + " cột");
                Check("Khách hàng: không còn cột CMND (POS TAP đã bỏ)", !kh.Columns.Exists(c => c.Name == "SOCMND"));
            }

            // Menu: số mục dùng được màn chung.
            int supported = 0;
            foreach (MenuNode n in Flatten(new MenuRepository(db).LoadTree()))
                if (n.Operation == 0 && !string.IsNullOrEmpty(n.StableDescId) && repo.IsSupported(n.StableDescId))
                    supported++;
            Check("Menu có 16 mục dùng màn danh mục chung", supported == 16, "n=" + supported);

            bool unchanged = true;
            var changed = new List<string>();
            foreach (string t in CatalogTables)
                if (Stamp(db, t) != before[t])
                {
                    unchanged = false;
                    changed.Add(t);
                }
            Check("Mở danh mục không ghi gì vào DB (số dòng và TIMEMODIFIED không đổi)", unchanged, string.Join(",", changed.ToArray()));
        }

        private static string Stamp(string db, string table)
        {
            using (FbConnection conn = KazukoConnection.Open(db))
            {
                DataTable t = DbHelper.Table(conn, null, "SELECT COUNT(*), MAX(TIMEMODIFIED) FROM \"" + table + "\"");
                return Convert.ToString(t.Rows[0][0]) + "|" + Convert.ToString(t.Rows[0][1]);
            }
        }

        private static IEnumerable<MenuNode> Flatten(IEnumerable<MenuNode> roots)
        {
            foreach (MenuNode n in roots)
            {
                yield return n;
                foreach (MenuNode c in Flatten(n.Children))
                    yield return c;
            }
        }

        // ------------------------------------------------------------------ Giao diện (bảng màu)

        private static void ThemeTests()
        {
            var all = new List<Theme.ThemeTokens>(Theme.BuiltInThemes.All());
            Check("Có đúng 4 bảng màu", all.Count == 4, "n=" + all.Count);

            var names = new HashSet<string>();
            bool unique = true;
            foreach (Theme.ThemeTokens t in all)
                unique &= names.Add(t.Name);
            Check("Tên các bảng màu không trùng nhau", unique);
            Check("Bảng màu mặc định là Cam Nắng", Theme.BuiltInThemes.Default.Name == "Cam Nắng");
            Check("Tên bảng màu cũ (Nền tối) rơi về mặc định", ReferenceEquals(Theme.BuiltInThemes.Find("Nền tối"), Theme.BuiltInThemes.Default));
            Check("Tên rỗng rơi về mặc định", ReferenceEquals(Theme.BuiltInThemes.Find(null), Theme.BuiltInThemes.Default));

            foreach (Theme.ThemeTokens t in all)
            {
                bool light = Theme.ContrastChecker.Luminance(t.Background) > 0.8 && Theme.ContrastChecker.Luminance(t.Surface) > 0.8
                    && Theme.ContrastChecker.Luminance(t.Text) < 0.1;
                Check("Bảng màu \"" + t.Name + "\" là nền sáng", light);

                bool hasInfo = !string.IsNullOrWhiteSpace(t.Tag) && !string.IsNullOrWhiteSpace(t.Audience) && !string.IsNullOrWhiteSpace(t.Description);
                Check("Bảng màu \"" + t.Name + "\" có nhãn, đối tượng, mô tả", hasInfo);

                var fails = new List<string>();
                double min = double.MaxValue;
                foreach (Theme.ContrastPair p in Theme.ContrastChecker.Evaluate(t))
                {
                    min = Math.Min(min, p.Ratio);
                    if (!p.Pass)
                        fails.Add(p.Name + " " + p.Ratio.ToString("0.00") + "<" + p.Minimum.ToString("0.0"));
                }
                Check("Bảng màu \"" + t.Name + "\" đạt tương phản AA", fails.Count == 0,
                    fails.Count == 0 ? "thấp nhất " + min.ToString("0.00") : string.Join("; ", fails.ToArray()));
            }
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
