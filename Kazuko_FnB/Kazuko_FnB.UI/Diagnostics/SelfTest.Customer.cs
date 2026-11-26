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
        private static void CustomerTests(string db)
        {
            UserSession savedSession = AppSession.Current;
            try
            {
                AppSession.Current = new UserSession
                {
                    UserId = "selftest_kh",
                    UserName = "selftest_kh",
                    DisplayName = "Selftest KH",
                    IsAdmin = true,
                    DatabasePath = db,
                    Permissions = new FunctionPermissions(true)
                };

                CustomerRulesPureTests();
                CustomerRepositoryTests(db);
                CustomerGroupAndPerfTests(db);
                CustomerUiTests(db);
            }
            finally
            {
                AppSession.Current = savedSession;
            }
        }

        private static void CustomerRulesPureTests()
        {
            // §6 / §9.0.2 Q3: sinh mã tự động KH + 5 số, bỏ qua mã nhập tay không đúng mẫu.
            Check("KH §6/Q3 (hàm thuần): bảng trống sinh KH00001",
                CustomerRules.NextCode(new string[0]) == "KH00001");
            Check("KH §6/Q3 (hàm thuần): sau KH00005 và mã tay 001 vẫn sinh KH00006",
                CustomerRules.NextCode(new[] { "KH00001", "KH00005", "001", "VIP99", "KH-A" }) == "KH00006");
            Check("KH §6/Q3 (hàm thuần): không phân biệt hoa/thường tiền tố kh",
                CustomerRules.NextCode(new[] { "kh00012" }) == "KH00013");

            // §6 / §9.0.2 Q4: so trùng collation UNICODE_CI_AI, không cắt khoảng trắng.
            Check("KH §6/Q4 (so trùng): an = AN = ân, Đặng = dang",
                CustomerRules.AreDuplicate("an", "AN")
                && CustomerRules.AreDuplicate("an", "ân")
                && CustomerRules.AreDuplicate("Đặng", "dang"));
            Check("KH §6/Q4 (so trùng): không cắt khoảng trắng ('  an  ' != 'an')",
                !CustomerRules.AreDuplicate("  an  ", "an")
                && !CustomerRules.AreDuplicate("an  ", "an"));

            // §6 / §9.0 Q5: điểm tích lũy ban đầu chỉ nhận số nguyên >= 0.
            Check("KH §6/Q5 (điểm ban đầu): chặn số âm và số thập phân, nhận số nguyên >= 0",
                CustomerRules.IsValidInitialPoints(0m)
                && CustomerRules.IsValidInitialPoints(150m)
                && !CustomerRules.IsValidInitialPoints(-1m)
                && !CustomerRules.IsValidInitialPoints(12.5m));

            // §9.0.2 Q4: gộp lỗi trùng Tên + Mã chung một thông báo, Tên trước.
            CustomerSaveResult bothDup = CustomerRules.ValidateCustomer(
                new CustomerRow { Name = "Nguyễn An", Code = "KH00001", InitialPoints = 0, PriceType = 0 },
                true, true, false);
            string expectedBoth = CustomerText.NameDuplicate("Nguyễn An") + Environment.NewLine + CustomerText.CodeDuplicate("KH00001");
            Check("KH §6/Q4 (gộp lỗi trùng Tên và Mã): Tên trước, đúng câu gốc",
                !bothDup.Ok && bothDup.ErrorMessage == expectedBoth,
                SingleLine(bothDup.ErrorMessage));

            // §6 / §9.0 Q9: tỉ lệ giảm nhóm ngoài 0..100 bị chặn.
            CustomerGroupRow badHigh = CustomerGroupRow.NewDefault();
            badHigh.Name = "Nhóm lỗi cao";
            badHigh.DiscountGoods = 100.01m;
            CustomerGroupRow badLow = CustomerGroupRow.NewDefault();
            badLow.Name = "Nhóm lỗi âm";
            badLow.DiscountFood = -0.5m;
            Check("KH §6/Q9 (tỉ lệ giảm nhóm): chặn > 100% và < 0%",
                !CustomerRules.ValidateGroup(badHigh, false).Ok
                && !CustomerRules.ValidateGroup(badLow, false).Ok);

            // §4.2 / §9.0 A2: ẩn/hiện ô tỉ lệ giảm nhóm ở 3 trạng thái cấu hình.
            CustomerGroupDiscountVisibility vNoHour = CustomerRules.GetDiscountVisibility(false, false);
            CustomerGroupDiscountVisibility vWithHour = CustomerRules.GetDiscountVisibility(true, false);
            CustomerGroupDiscountVisibility vMerged = CustomerRules.GetDiscountVisibility(true, true);
            Check("KH §6/A2 (ẩn hiện ô tỉ lệ giảm): Tỉ lệ giảm giá luôn hiện; tiền giờ/tiền hàng đúng cấu hình",
                vNoHour.ShowTotalDiscount && vNoHour.ShowGoodsDiscount && !vNoHour.ShowHourDiscount
                && vWithHour.ShowTotalDiscount && vWithHour.ShowGoodsDiscount && vWithHour.ShowHourDiscount
                && vMerged.ShowTotalDiscount && !vMerged.ShowGoodsDiscount && !vMerged.ShowHourDiscount);
        }

        private static void CustomerRepositoryTests(string db)
        {
            var repo = new CustomerRepository(db);
            var groupRepo = new CustomerGroupRepository(db);
            var createdCustomerIds = new List<string>();
            string groupId = null;
            string empId = null;
            string provId = null;
            string cardId = null;
            string tag = DbHelper.NewId().Substring(0, 8);

            try
            {
                // 1. Mã tự sinh KH00001..KH00006 trên DB thật.
                string codeBefore = repo.NextCode();
                var minRow = CustomerRow.NewDefault();
                minRow.Code = codeBefore;
                minRow.Name = "Khách tối thiểu " + tag;
                CustomerSaveResult minRes = repo.SaveCustomer(minRow, "selftest_kh", new CustomerSaveOptions());
                if (minRes.Ok) createdCustomerIds.Add(minRes.SavedId);
                CustomerRow minLoaded = minRes.Ok ? repo.LoadCustomer(minRes.SavedId) : null;
                Guid parsedGuid;
                Check("KH §6 (thêm tối thiểu Mã + Tên): STATUS=30, USERCREATEDID/TIMECREATED, GUID hợp lệ, điểm=0, giá=0",
                    minRes.Ok && minLoaded != null
                    && Guid.TryParse(minLoaded.Id, out parsedGuid)
                    && minLoaded.Status == 30
                    && minLoaded.CreatedById == "selftest_kh"
                    && minLoaded.CreatedAt.HasValue
                    && minLoaded.InitialPoints == 0
                    && minLoaded.PriceType == 0,
                    minRes.ErrorMessage ?? "ok");

                // Mã kế tiếp sau KH00005 và sau khi thêm mã nhập tay '001'.
                var kh5 = CustomerRow.NewDefault();
                kh5.Code = "KH98005";
                kh5.Name = "Khách mã 98005 " + tag;
                CustomerSaveResult r5 = repo.SaveCustomer(kh5, "selftest_kh", new CustomerSaveOptions());
                if (r5.Ok) createdCustomerIds.Add(r5.SavedId);
                string nextAfter5 = repo.NextCode();

                var manual = CustomerRow.NewDefault();
                manual.Code = "001-" + tag;
                manual.Name = "Khách mã tay " + tag;
                CustomerSaveResult rManual = repo.SaveCustomer(manual, "selftest_kh", new CustomerSaveOptions());
                if (rManual.Ok) createdCustomerIds.Add(rManual.SavedId);
                string nextAfterManual = repo.NextCode();
                Check("KH §6/Q3 (mã khách tự sinh trên DB): tăng sau KH98005 thành KH98006, mã tay không làm nhảy số",
                    r5.Ok && rManual.Ok && nextAfter5 == "KH98006" && nextAfterManual == "KH98006",
                    "after5=" + nextAfter5 + ", afterManual=" + nextAfterManual);

                // 2. Để trống Tên / Mã → báo lỗi bắt buộc, không ghi dòng.
                int countBeforeEmpty = CountTableRows(db, "DKHACHHANG");
                CustomerSaveResult emptyName = repo.SaveCustomer(new CustomerRow { Code = "KHE1" + tag, Name = "   " }, "selftest_kh", new CustomerSaveOptions());
                CustomerSaveResult emptyCode = repo.SaveCustomer(new CustomerRow { Code = "", Name = "Không mã " + tag }, "selftest_kh", new CustomerSaveOptions());
                int countAfterEmpty = CountTableRows(db, "DKHACHHANG");
                Check("KH §6 (bắt buộc Tên và Mã): để trống bị chặn, không ghi dòng",
                    !emptyName.Ok && emptyName.ErrorMessage == CustomerText.NameEmpty
                    && !emptyCode.Ok && emptyCode.ErrorMessage == CustomerText.CodeEmpty
                    && countBeforeEmpty == countAfterEmpty,
                    "nErr=" + SingleLine(emptyName.ErrorMessage) + "; cErr=" + SingleLine(emptyCode.ErrorMessage));

                // 3. Trùng Mã, trùng Tên theo collation (an/AN/ân), khoảng trắng giữ nguyên, cấu hình ChoPhepTrungTenKhachHang=30, sửa chính mình.
                var baseCust = CustomerRow.NewDefault();
                baseCust.Code = "KH_C_" + tag;
                baseCust.Name = "an " + tag;
                CustomerSaveResult baseRes = repo.SaveCustomer(baseCust, "selftest_kh", new CustomerSaveOptions());
                if (baseRes.Ok) createdCustomerIds.Add(baseRes.SavedId);
                if (baseRes.Ok) baseCust.Id = baseRes.SavedId;

                CustomerSaveResult dupCode = repo.SaveCustomer(
                    new CustomerRow { Code = "kh_c_" + tag, Name = "Tên khác " + tag },
                    "selftest_kh", new CustomerSaveOptions { AllowDuplicateName = false });
                CustomerSaveResult dupNameAccent = repo.SaveCustomer(
                    new CustomerRow { Code = "KH_D1_" + tag, Name = "ÂN " + tag },
                    "selftest_kh", new CustomerSaveOptions { AllowDuplicateName = false });
                CustomerSaveResult dupBoth = repo.SaveCustomer(
                    new CustomerRow { Code = "KH_C_" + tag, Name = "AN " + tag },
                    "selftest_kh", new CustomerSaveOptions { AllowDuplicateName = false });

                var paddedName = CustomerRow.NewDefault();
                paddedName.Code = "KH_P_" + tag;
                paddedName.Name = "  an " + tag + "  ";
                CustomerSaveResult paddedRes = repo.SaveCustomer(paddedName, "selftest_kh", new CustomerSaveOptions { AllowDuplicateName = false });
                if (paddedRes.Ok) createdCustomerIds.Add(paddedRes.SavedId);

                // Sửa chính khách đó (giữ nguyên Mã/Tên) không bị báo trùng.
                DateTime? origCreatedAt = repo.LoadCustomer(baseCust.Id).CreatedAt;
                string origCreatedBy = repo.LoadCustomer(baseCust.Id).CreatedById;
                baseCust.Phone = "0901234567";
                CustomerSaveResult selfEditRes = repo.SaveCustomer(baseCust, "selftest_edit", new CustomerSaveOptions { AllowDuplicateName = false });
                CustomerRow afterEdit = repo.LoadCustomer(baseCust.Id);

                var allowDupNameRow = CustomerRow.NewDefault();
                allowDupNameRow.Code = "KH_A_" + tag;
                allowDupNameRow.Name = "an " + tag;
                CustomerSaveResult allowDupRes = repo.SaveCustomer(allowDupNameRow, "selftest_kh", new CustomerSaveOptions { AllowDuplicateName = true });
                if (allowDupRes.Ok) createdCustomerIds.Add(allowDupRes.SavedId);


                Check("KH §6/Q4 (kiểm trùng Mã và Tên): chặn trùng Mã/Tên (UNICODE_CI_AI), giữ khoảng trắng, cho trùng khi bật cấu hình, sửa chính mình qua",
                    baseRes.Ok && !dupCode.Ok && dupCode.HasCodeError
                    && !dupNameAccent.Ok && dupNameAccent.HasNameError
                    && !dupBoth.Ok && dupBoth.ErrorMessage.IndexOf("Tên khách hàng", StringComparison.Ordinal) < dupBoth.ErrorMessage.IndexOf("Mã khách", StringComparison.Ordinal)
                    && paddedRes.Ok && allowDupRes.Ok && selfEditRes.Ok,
                    "both=" + SingleLine(dupBoth.ErrorMessage));

                Check("KH §6 (sửa khách lưu vết): cập nhật TIMEMODIFIED/USERMODIFIEDID, giữ nguyên TIMECREATED/USERCREATEDID",
                    afterEdit != null && afterEdit.ModifiedById == "selftest_edit" && afterEdit.ModifiedAt.HasValue
                    && afterEdit.CreatedById == origCreatedBy && afterEdit.CreatedAt == origCreatedAt
                    && afterEdit.Phone == "0901234567");

                // 4. Độ dài 255 ký tự lưu được; 256 ký tự bị chặn; UTF-8 có dấu đúng.
                string name255 = "Nguyễn Văn Ánh " + new string('ê', 255 - 15);
                string name256 = new string('ă', 256);
                var r255 = CustomerRow.NewDefault();
                r255.Code = "KH255" + tag;
                r255.Name = name255;
                CustomerSaveResult res255 = repo.SaveCustomer(r255, "selftest_kh", new CustomerSaveOptions());
                if (res255.Ok) createdCustomerIds.Add(res255.SavedId);
                CustomerRow loaded255 = res255.Ok ? repo.LoadCustomer(res255.SavedId) : null;
                CustomerSaveResult res256 = repo.SaveCustomer(
                    new CustomerRow { Code = "KH256" + tag, Name = name256 }, "selftest_kh", new CustomerSaveOptions());
                Check("KH §6 (độ dài 255/256 và UTF-8): 255 ký tự tiếng Việt lưu & đọc đúng; 256 ký tự bị chặn",
                    res255.Ok && loaded255 != null && loaded255.Name == name255 && !res256.Ok,
                    "len=" + (loaded255 == null ? -1 : loaded255.Name.Length) + "; err256=" + SingleLine(res256.ErrorMessage));

                // 5. Đủ 15 trường lưu rồi mở lại khớp hết (Nhóm, Nhân viên, Tỉnh thành, Thẻ trả trước, Ngày sinh, Điểm, Giá bán...).
                CustomerGroupRow grp = CustomerGroupRow.NewDefault();
                grp.Name = "Nhóm 15T " + tag;
                CustomerGroupSaveResult gRes = groupRepo.Save(grp, "selftest_kh");
                groupId = gRes.SavedId;
                empId = DbHelper.NewId();
                provId = DbHelper.NewId();
                cardId = DbHelper.NewId();
                BookingExec(db, "INSERT INTO DNHANVIEN (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kh')", "@I", empId, "@N", "NVKD " + tag);
                BookingExec(db, "INSERT INTO DTINHTHANH (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kh')", "@I", provId, "@N", "Hà Nội " + tag);
                BookingExec(db, "INSERT INTO DTHETRATRUOC (ID, NAME, STATUS, USERCREATEDID) VALUES (@I, @N, 30, 'selftest_kh')", "@I", cardId, "@N", "Thẻ VIP " + tag);

                var full = new CustomerRow
                {
                    Code = "KHF15" + tag,
                    Name = "Công ty TNHH Ăn Uống " + tag,
                    GroupId = groupId,
                    Address = "123 Lê Lợi, Q.1",
                    Phone = "0988777666",
                    Email = "khach@kazuko.vn",
                    TaxCode = "0102030405",
                    EmployeeId = empId,
                    Birthday = new DateTime(1995, 8, 20),
                    InitialPoints = 250,
                    PriceType = 2,
                    ProvinceId = provId,
                    Facebook = "fb.com/kazuko",
                    PrepaidCardId = cardId,
                    Note = "Khách thân thiết ghi chú UTF-8"
                };
                CustomerSaveResult fullRes = repo.SaveCustomer(full, "selftest_kh", new CustomerSaveOptions());
                if (fullRes.Ok) createdCustomerIds.Add(fullRes.SavedId);
                CustomerRow back = fullRes.Ok ? repo.LoadCustomer(fullRes.SavedId) : null;
                Check("KH §6 (đủ 15 trường lưu và đọc lại): mọi trường khớp 100%",
                    fullRes.Ok && back != null
                    && back.Code == full.Code && back.Name == full.Name
                    && back.GroupId == groupId && back.GroupName == grp.Name
                    && back.Address == full.Address && back.Phone == full.Phone
                    && back.Email == full.Email && back.TaxCode == full.TaxCode
                    && back.EmployeeId == empId && back.EmployeeName == "NVKD " + tag
                    && back.Birthday == new DateTime(1995, 8, 20)
                    && back.InitialPoints == 250 && back.PriceType == 2
                    && back.ProvinceId == provId && back.ProvinceName == "Hà Nội " + tag
                    && back.Facebook == full.Facebook
                    && back.PrepaidCardId == cardId && back.PrepaidCardName == "Thẻ VIP " + tag
                    && back.Note == full.Note,
                    fullRes.ErrorMessage ?? "ok");

                // 6. Truy vấn LoadLookup dùng chung (§3, §4.1: Mã, Tên, Địa chỉ, Điện thoại).
                List<CustomerLookupItem> lookup = repo.LoadLookup();
                CustomerLookupItem lk = lookup.Find(x => x.Id == full.Id);
                Check("KH §3/§4.1 (LoadLookup dùng chung): trả đủ Mã, Tên, Địa chỉ, Điện thoại, Giá bán, Thẻ",
                    lk != null && lk.Code == full.Code && lk.Name == full.Name
                    && lk.Address == full.Address && lk.Phone == full.Phone
                    && lk.GroupId == groupId && lk.PriceType == 2 && lk.PrepaidCardId == cardId);

                // 7. Xóa khách chưa phát sinh → xóa cứng khỏi DB.
                CustomerDeleteResult delUnused = repo.DeleteCustomer(minRow.Id);
                bool goneAfterDelete = repo.LoadCustomer(minRow.Id) == null;
                Check("KH §6/§9.0 (xóa khách chưa phát sinh): xóa cứng thành công khỏi DKHACHHANG",
                    delUnused.Ok && goneAfterDelete, SingleLine(delUnused.ErrorMessage));

                // 8. Xóa khách đã có phát sinh ở TỪNG bảng trong 7 bảng §4.4 bị chặn.
                CheckDeleteBlockedOnAllSevenTables(db, repo, full.Id, tag);
            }
            finally
            {
                foreach (string cid in createdCustomerIds)
                    SafeDeleteById(db, "DKHACHHANG", cid);
                if (groupId != null) SafeDeleteById(db, "DNHOMKHACHHANG", groupId);
                if (empId != null) SafeDeleteById(db, "DNHANVIEN", empId);
                if (provId != null) SafeDeleteById(db, "DTINHTHANH", provId);
                if (cardId != null) SafeDeleteById(db, "DTHETRATRUOC", cardId);
            }
        }

        private static void CheckDeleteBlockedOnAllSevenTables(string db, CustomerRepository repo, string customerId, string tag)
        {
            var failed = new List<string>();
            foreach (string table in CustomerRepository.ReferenceTables)
            {
                string refId = DbHelper.NewId();
                try
                {
                    InsertCustomerRefRow(db, table, refId, customerId, "REF-" + table + "-" + tag);
                    CustomerDeleteResult del = repo.DeleteCustomer(customerId);
                    bool stillExists = repo.LoadCustomer(customerId) != null;
                    bool hasBlock = HasBlock(del.Blocks, table);
                    if (del.Ok || !stillExists || !hasBlock)
                        failed.Add(table + "(ok=" + del.Ok + ",exists=" + stillExists + ",block=" + hasBlock + ")");
                }
                catch (Exception ex)
                {
                    failed.Add(table + "(" + SingleLine(ex.Message) + ")");
                }
                finally
                {
                    SafeDeleteById(db, table, refId);
                }
            }
            Check("KH §6/§9.0 (chặn xóa khách có phát sinh): chặn đủ 7 bảng §4.4 (TDONHANG, TDATHANG, TBAOGIA, TTHUCHI, TTANGGIAMDIEM, TLUUTAM, DVOUCHER)",
                failed.Count == 0,
                failed.Count == 0 ? "7/7 bảng chặn đúng" : string.Join("; ", failed.ToArray()));
        }

        /// <summary>Chèn 1 dòng tham chiếu DKHACHHANGID vào bảng thử (tự điền mọi cột NOT NULL theo metadata Firebird).</summary>
        private static void InsertCustomerRefRow(string db, string table, string id, string customerId, string name)
        {
            DbHelper.InTransaction(db, (conn, tx) =>
            {
                DataTable meta = DbHelper.Table(conn, tx,
                    "SELECT TRIM(rf.RDB$FIELD_NAME) AS COL, f.RDB$FIELD_TYPE AS FTYPE " +
                    "FROM RDB$RELATION_FIELDS rf " +
                    "JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE " +
                    "WHERE rf.RDB$RELATION_NAME = @T AND (rf.RDB$NULL_FLAG = 1 OR TRIM(rf.RDB$FIELD_NAME) IN ('ID','NAME','STATUS','USERCREATEDID','DKHACHHANGID'))",
                    "@T", table);

                var cols = new List<string>();
                var vals = new List<string>();
                var args = new List<object>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DataRow r in meta.Rows)
                {
                    string col = DbHelper.Str(r["COL"]);
                    if (string.IsNullOrEmpty(col) || !seen.Add(col))
                        continue;
                    int ftype = DbHelper.Int(r["FTYPE"]);
                    string param = "@P" + cols.Count;
                    cols.Add("\"" + col + "\"");
                    vals.Add(param);
                    args.Add(param);
                    args.Add(DefaultRefValue(col, ftype, id, customerId, name));
                }

                string sql = "INSERT INTO \"" + table + "\" (" + string.Join(", ", cols.ToArray()) +
                             ") VALUES (" + string.Join(", ", vals.ToArray()) + ")";
                DbHelper.Exec(conn, tx, sql, args.ToArray());
            });
        }

        private static object DefaultRefValue(string col, int ftype, string id, string customerId, string name)
        {
            switch (col.ToUpperInvariant())
            {
                case "ID": return id;
                case "NAME": return name;
                case "STATUS": return 30;
                case "USERCREATEDID": return "selftest_kh";
                case "DKHACHHANGID": return customerId;
            }
            // Firebird RDB$FIELD_TYPE: 7=SMALLINT, 8=INTEGER, 16=INT64, 10=FLOAT, 27=DOUBLE, 12=DATE, 13=TIME, 35=TIMESTAMP.
            if (ftype == 7 || ftype == 8 || ftype == 16 || ftype == 10 || ftype == 27)
                return 0;
            if (ftype == 12 || ftype == 35)
                return DateTime.Today;
            if (ftype == 13)
                return TimeSpan.Zero;
            return "";
        }

        private static int CountTableRows(string db, string table)
        {
            return DbHelper.Read(db, conn =>
                DbHelper.Int(DbHelper.Scalar(conn, null, "SELECT COUNT(*) FROM \"" + table + "\"")));
        }

        private static void SafeDeleteById(string db, string table, string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            try
            {
                BookingExec(db, "DELETE FROM \"" + table + "\" WHERE \"ID\" = @I", "@I", id);
            }
            catch
            {
            }
        }

        private static string SingleLine(string text)
        {
            return (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
