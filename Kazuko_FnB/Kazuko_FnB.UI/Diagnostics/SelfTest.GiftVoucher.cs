using System;
using System.Collections.Generic;
using System.Globalization;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;

namespace Kazuko_FnB.UI.Diagnostics
{
    public static partial class SelfTest
    {
        private static void GiftVoucherTests(string db)
        {
            GiftVoucherPureRuleTests();
            GiftVoucherGroupAndCrudDbTests(db);
            GiftVoucherPaymentValidationDbTests(db);
            GiftVoucherUiTests(db);
        }

        private static void GiftVoucherPureRuleTests()
        {
            DateTime today = new DateTime(2026, 6, 15);
            var valid = new GiftVoucherRow
            {
                Code = "VC-001",
                Value = 100000m,
                IssueDate = today,
                ExpiryDate = today.AddDays(30),
                Note = "Ghi chú"
            };
            Check("Voucher §4 (quy tắc thuần): hợp lệ khi mã có, giá trị > 0, hạn >= phát hành",
                GiftVoucherRules.ValidateVoucher(valid, false).Ok);

            var emptyCode = new GiftVoucherRow { Code = "   ", Value = 50000m };
            GiftVoucherSaveResult rEmpty = GiftVoucherRules.ValidateVoucher(emptyCode, false);
            Check("Voucher §4 (quy tắc thuần): chặn mã trống",
                !rEmpty.Ok && rEmpty.HasCodeError && rEmpty.ErrorMessage == GiftVoucherText.CodeEmpty,
                SingleLine(rEmpty.ErrorMessage));

            GiftVoucherSaveResult rDup = GiftVoucherRules.ValidateVoucher(valid, true);
            Check("Voucher §4 (quy tắc thuần): chặn mã trùng",
                !rDup.Ok && rDup.HasCodeError && rDup.ErrorMessage == GiftVoucherText.CodeDuplicate(valid.Code),
                SingleLine(rDup.ErrorMessage));

            GiftVoucherSaveResult rVal0 = GiftVoucherRules.ValidateVoucher(new GiftVoucherRow { Code = "V0", Value = 0m }, false);
            GiftVoucherSaveResult rValNeg = GiftVoucherRules.ValidateVoucher(new GiftVoucherRow { Code = "VN", Value = -5000m }, false);
            Check("Voucher §4 (quy tắc thuần): chặn giá trị <= 0",
                !rVal0.Ok && rVal0.HasValueError && !rValNeg.Ok && rValNeg.HasValueError,
                SingleLine(rVal0.ErrorMessage));

            var badDates = new GiftVoucherRow { Code = "VD", Value = 50000m, IssueDate = today, ExpiryDate = today.AddDays(-1) };
            GiftVoucherSaveResult rDates = GiftVoucherRules.ValidateVoucher(badDates, false);
            var sameDay = new GiftVoucherRow { Code = "VS", Value = 50000m, IssueDate = today, ExpiryDate = today };
            Check("Voucher §4 (quy tắc thuần): chặn hạn sử dụng trước ngày phát hành, cho phép cùng ngày",
                !rDates.Ok && rDates.HasDateError && rDates.ErrorMessage == GiftVoucherText.ExpiryBeforeIssue
                && GiftVoucherRules.ValidateVoucher(sameDay, false).Ok,
                SingleLine(rDates.ErrorMessage));

            var updatedNoteOnly = new GiftVoucherRow
            {
                Code = valid.Code,
                Value = valid.Value,
                IssueDate = valid.IssueDate,
                ExpiryDate = valid.ExpiryDate,
                GroupId = valid.GroupId,
                PartnerId = valid.PartnerId,
                Note = "Đổi ghi chú"
            };
            var updatedValue = new GiftVoucherRow
            {
                Code = valid.Code,
                Value = 200000m,
                IssueDate = valid.IssueDate,
                ExpiryDate = valid.ExpiryDate,
                Note = valid.Note
            };
            Check("Voucher §4 (quy tắc thuần): phát hiện thay đổi trường khóa khi đã dùng, cho phép chỉ sửa ghi chú",
                !GiftVoucherRules.HasLockedFieldChanges(valid, updatedNoteOnly)
                && GiftVoucherRules.HasLockedFieldChanges(valid, updatedValue));

            var usedRow = new GiftVoucherRow { Code = "VU", Value = 50000m, PaidBillName = "HD001" };
            var futureRow = new GiftVoucherRow { Code = "VF", Value = 50000m, IssueDate = today.AddDays(1) };
            var expiredRow = new GiftVoucherRow { Code = "VE", Value = 50000m, ExpiryDate = today.AddDays(-1) };
            Check("Voucher §4 (quy tắc thuần): IsUsableOn loại đã dùng/chưa phát hành/hết hạn và giữ hợp lệ",
                GiftVoucherRules.IsUsableOn(valid, today)
                && !GiftVoucherRules.IsUsableOn(usedRow, today)
                && !GiftVoucherRules.IsUsableOn(futureRow, today)
                && !GiftVoucherRules.IsUsableOn(expiredRow, today));

            string longText = new string('A', 300);
            Check("Voucher §4 (quy tắc thuần): ClipLogText cắt đúng 255 ký tự tránh tràn TLUUVET",
                GiftVoucherRules.ClipLogText(longText).Length == GiftVoucherText.MaxTextLength);

            var grpEmpty = GiftVoucherRules.ValidateGroup(new GiftVoucherGroupRow { Name = "" }, false);
            var grpDup = GiftVoucherRules.ValidateGroup(new GiftVoucherGroupRow { Name = "VIP" }, true);
            Check("Voucher §4 (quy tắc thuần): kiểm tra tên nhóm trống và trùng",
                !grpEmpty.Ok && !grpDup.Ok);
        }

        private static void GiftVoucherGroupAndCrudDbTests(string db)
        {
            var svc = new GiftVoucherService(AppSession.Current);
            var grpSvc = new GiftVoucherGroupService(AppSession.Current);
            var repo = new GiftVoucherRepository(db);
            var grpRepo = new GiftVoucherGroupRepository(db);
            string tag = DbHelper.NewId().Substring(0, 8);
            DateTime today = svc.GetServerDate();

            string parentId = null;
            string childId = null;
            string sameNameOtherParentId = null;
            string longGroupId = null;
            string emptyGrpId = null;
            string vUnusedId = null;
            string vUnpaidRefId = null;
            string vPaidRefId = null;
            string unpaidBillId = null;
            string paidBillId = null;
            string partnerId = null;

            try
            {
                // 1. Nhóm cha / con, kiểm trùng trong cùng cha (UNICODE_CI_AI) nhưng khác cha vẫn cho phép.
                var parent = GiftVoucherGroupRow.NewDefault();
                parent.Name = "Nhóm Voucher Cha " + tag;
                parent.Note = "Ghi chú cha";
                GiftVoucherGroupSaveResult pRes = grpSvc.Save(parent);
                parentId = pRes.SavedId;

                var dupSameParent = GiftVoucherGroupRow.NewDefault();
                dupSameParent.Name = "nhom voucher cha " + tag;
                GiftVoucherGroupSaveResult dupSameRes = grpSvc.Save(dupSameParent);

                var child = GiftVoucherGroupRow.NewDefault();
                child.Name = "Nhóm Voucher Cha " + tag; // cùng tên nhưng khác cha -> hợp lệ!
                child.ParentId = parentId;
                GiftVoucherGroupSaveResult cRes = grpSvc.Save(child);
                childId = cRes.SavedId;

                var dupChild = GiftVoucherGroupRow.NewDefault();
                dupChild.Name = "NHOM VOUCHER CHA " + tag;
                dupChild.ParentId = parentId;
                GiftVoucherGroupSaveResult dupChildRes = grpSvc.Save(dupChild);

                Check("Voucher §4 (nhóm DB): thêm cha/con, chặn trùng UNICODE_CI_AI trong cùng cha, cho phép cùng tên khác cha",
                    pRes.Ok && !dupSameRes.Ok && cRes.Ok && !dupChildRes.Ok,
                    SingleLine((dupSameRes.ErrorMessage ?? "") + " | " + (dupChildRes.ErrorMessage ?? "")));

                // 2. Tên nhóm đúng 255 ký tự không tràn cột khi ghi TLUUVET.
                string name255 = ("G255_" + tag + "_").PadRight(255, 'X');
                var longGrp = GiftVoucherGroupRow.NewDefault();
                longGrp.Name = name255;
                GiftVoucherGroupSaveResult longRes = grpSvc.Save(longGrp);
                longGroupId = longRes.SavedId;
                int logLongCount = CountActivityLogsForBill(db, longGroupId);
                Check("Voucher §4 (nhóm 255 ký tự): lưu nhóm 255 ký tự và ghi TLUUVET không tràn VARCHAR(255)",
                    longRes.Ok && !string.IsNullOrEmpty(longGroupId) && logLongCount >= 1,
                    "ok=" + longRes.Ok + ", logs=" + logLongCount);

                // 3. Đối tác phân phối (khách hàng) + Thêm / Sửa / Trùng mã UNICODE_CI_AI / Xóa voucher chưa dùng.
                partnerId = DbHelper.NewId();
                BookingExec(db,
                    "INSERT INTO DKHACHHANG (ID, MAKHACH, NAME, STATUS, USERCREATEDID, TIMECREATED) " +
                    "VALUES (@I, @C, @N, 30, 'selftest', @T)",
                    "@I", partnerId, "@C", "KVC" + tag, "@N", "Đối tác Voucher " + tag, "@T", DateTime.Now);

                var v1 = new GiftVoucherRow
                {
                    Code = "VC-ĐẸP-" + tag,
                    Value = 100000m,
                    IssueDate = today,
                    ExpiryDate = today.AddDays(30),
                    GroupId = childId,
                    PartnerId = partnerId,
                    Note = "Ban đầu"
                };
                GiftVoucherSaveResult v1Save = svc.Save(v1);
                vUnusedId = v1Save.SavedId;

                GiftVoucherSaveResult vDupCode = svc.Save(new GiftVoucherRow
                {
                    Code = "vc-dep-" + tag,
                    Value = 50000m
                });

                GiftVoucherRow loaded1 = svc.LoadById(vUnusedId);
                loaded1.Value = 150000m;
                loaded1.Note = "Đã sửa";
                GiftVoucherSaveResult v1Edit = svc.Save(loaded1);

                // Chặn xóa nhóm còn voucher và nhóm còn nhóm con.
                GiftVoucherGroupDeleteResult delChildBlocked = grpSvc.Delete(childId);
                GiftVoucherGroupDeleteResult delParentBlocked = grpSvc.Delete(parentId);

                // Xóa cứng voucher chưa dùng -> thành công + ghi lưu vết; sau đó xóa nhóm rỗng.
                GiftVoucherDeleteResult delV1 = svc.Delete(vUnusedId);
                bool v1Gone = repo.LoadById(vUnusedId) == null;
                int v1Logs = CountActivityLogsForBill(db, vUnusedId);
                vUnusedId = null;

                var emptyGrp = GiftVoucherGroupRow.NewDefault();
                emptyGrp.Name = "Nhóm Rỗng " + tag;
                emptyGrpId = grpSvc.Save(emptyGrp).SavedId;
                GiftVoucherGroupDeleteResult delEmpty = grpSvc.Delete(emptyGrpId);
                bool emptyGone = grpRepo.LoadById(emptyGrpId) == null;
                emptyGrpId = null;

                Check("Voucher §4 (CRUD + chặn xóa nhóm): thêm/sửa/trùng mã UNICODE_CI_AI, chặn xóa nhóm còn voucher & nhóm con, xóa cứng voucher & nhóm rỗng kèm TLUUVET",
                    v1Save.Ok && !vDupCode.Ok && v1Edit.Ok
                    && !delChildBlocked.Ok && delChildBlocked.VoucherCount == 1
                    && delChildBlocked.ErrorMessage.IndexOf("nhóm còn 1 voucher", StringComparison.Ordinal) >= 0
                    && !delParentBlocked.Ok && delParentBlocked.ChildGroupCount == 1
                    && delParentBlocked.ErrorMessage.IndexOf("nhóm còn 1 nhóm con", StringComparison.Ordinal) >= 0
                    && delV1.Ok && v1Gone && v1Logs >= 3
                    && delEmpty.Ok && emptyGone,
                    SingleLine((delChildBlocked.ErrorMessage ?? "") + " | " + (delParentBlocked.ErrorMessage ?? "")));

                // 4. Voucher gắn hóa đơn CHƯA thanh toán vs ĐÃ thanh toán:
                // - Đã dùng chỉ tính hóa đơn đã thanh toán; nhưng chặn XÓA và chặn sửa mã/giá trị/hạn ở cả hai; chỉ sửa được Ghi chú.
                vUnpaidRefId = svc.Save(new GiftVoucherRow
                {
                    Code = "VC-UNPAID-" + tag,
                    Value = 80000m,
                    IssueDate = today,
                    ExpiryDate = today.AddDays(10),
                    GroupId = childId,
                    Note = "Gốc chưa TT"
                }).SavedId;
                unpaidBillId = DbHelper.NewId();
                string unpaidBillNo = "HD-UNPAID-" + tag;
                InsertGiftVoucherBillRef(db, unpaidBillId, unpaidBillNo, vUnpaidRefId, 0, 30);

                GiftVoucherRow loadedUnpaid = svc.LoadById(vUnpaidRefId);
                GiftVoucherDeleteResult delUnpaidBlocked = svc.Delete(vUnpaidRefId);
                loadedUnpaid.Value = 90000m;
                GiftVoucherSaveResult editUnpaidValBlocked = svc.Save(loadedUnpaid);
                loadedUnpaid.Value = 80000m;
                loadedUnpaid.Note = "Sửa ghi chú OK";
                GiftVoucherSaveResult editUnpaidNoteOk = svc.Save(loadedUnpaid);

                vPaidRefId = svc.Save(new GiftVoucherRow
                {
                    Code = "VC-PAID-" + tag,
                    Value = 120000m,
                    IssueDate = today,
                    ExpiryDate = today.AddDays(10),
                    GroupId = childId,
                    Note = "Gốc đã TT"
                }).SavedId;
                paidBillId = DbHelper.NewId();
                string paidBillNo = "HD-PAID-" + tag;
                InsertGiftVoucherBillRef(db, paidBillId, paidBillNo, vPaidRefId, 30, 30);

                GiftVoucherRow loadedPaid = svc.LoadById(vPaidRefId);
                GiftVoucherDeleteResult delPaidBlocked = svc.Delete(vPaidRefId);
                loadedPaid.Code = "VC-PAID-NEW-" + tag;
                GiftVoucherSaveResult editPaidCodeBlocked = svc.Save(loadedPaid);
                loadedPaid.Code = "VC-PAID-" + tag;
                loadedPaid.Note = "Ghi chú mới sau dùng";
                GiftVoucherSaveResult editPaidNoteOk = svc.Save(loadedPaid);

                Check("Voucher §4 (tham chiếu hóa đơn): Đã dùng chỉ tính bill đã TT; cả bill chưa TT và đã TT đều chặn xóa (đúng số HĐ) & chặn sửa mã/giá trị, chỉ cho sửa ghi chú",
                    !loadedUnpaid.IsUsed && loadedUnpaid.HasAnyBillRef
                    && !delUnpaidBlocked.Ok && delUnpaidBlocked.ErrorMessage == GiftVoucherText.DeleteBlockedByBill(unpaidBillNo)
                    && !editUnpaidValBlocked.Ok && editUnpaidValBlocked.HasLockedFieldError && editUnpaidNoteOk.Ok
                    && loadedPaid.IsUsed && loadedPaid.PaidBillName == paidBillNo
                    && !delPaidBlocked.Ok && delPaidBlocked.ErrorMessage == GiftVoucherText.DeleteBlockedByBill(paidBillNo)
                    && !editPaidCodeBlocked.Ok && editPaidCodeBlocked.HasLockedFieldError && editPaidNoteOk.Ok,
                    SingleLine((delUnpaidBlocked.ErrorMessage ?? "") + " | " + (delPaidBlocked.ErrorMessage ?? "")));

                // 5. Rollback nguyên tử (cả DVOUCHER/DNHOMVOUCHER lẫn TLUUVET đều hoàn tác khi hook ném lỗi trước commit).
                var vRollback = new GiftVoucherRow { Code = "VC-RB-" + tag, Value = 50000m };
                bool vThrew = false;
                try
                {
                    svc.Save(vRollback, delegate { throw new InvalidOperationException("sim-voucher-rollback"); });
                }
                catch (InvalidOperationException)
                {
                    vThrew = true;
                }
                bool vRbClean = vThrew && string.IsNullOrEmpty(vRollback.Id) && !repo.IsCodeTaken("VC-RB-" + tag, "");

                var gRollback = GiftVoucherGroupRow.NewDefault();
                gRollback.Name = "Nhóm RB " + tag;
                bool gThrew = false;
                try
                {
                    grpSvc.Save(gRollback, delegate { throw new InvalidOperationException("sim-group-rollback"); });
                }
                catch (InvalidOperationException)
                {
                    gThrew = true;
                }
                bool gRbClean = gThrew && string.IsNullOrEmpty(gRollback.Id) && !grpRepo.IsNameTaken("Nhóm RB " + tag, "", "");

                Check("Voucher §4 (rollback transaction): lỗi trước commit hoàn tác toàn bộ bản ghi lẫn TLUUVET",
                    vRbClean && gRbClean,
                    "vRb=" + vRbClean + ", gRb=" + gRbClean);
            }
            finally
            {
                if (unpaidBillId != null) SafeDeleteById(db, "TDONHANG", unpaidBillId);
                if (paidBillId != null) SafeDeleteById(db, "TDONHANG", paidBillId);
                foreach (string vid in new[] { vUnusedId, vUnpaidRefId, vPaidRefId })
                {
                    if (vid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "DVOUCHER", vid);
                }
                foreach (string gid in new[] { emptyGrpId, sameNameOtherParentId, longGroupId, childId, parentId })
                {
                    if (gid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", gid);
                    SafeDeleteById(db, "DNHOMVOUCHER", gid);
                }
                if (partnerId != null) SafeDeleteById(db, "DKHACHHANG", partnerId);
            }
        }

        private static void GiftVoucherPaymentValidationDbTests(string db)
        {
            var vSvc = new GiftVoucherService(AppSession.Current);
            var tableSvc = new TableService(AppSession.Current);
            string tag = DbHelper.NewId().Substring(0, 8);
            DateTime today = vSvc.GetServerDate();

            string vFutureId = null;
            string vExpiredId = null;
            string vCancelledBillId = null;
            string vUnpaidBillId = null;
            string vPaidBillId = null;
            string billCancelledId = null;
            string billUnpaidId = null;
            string billPaidId = null;

            try
            {
                DateTime futureDate = today.AddDays(5);
                string futureCode = "VC-FUT-" + tag;
                vFutureId = vSvc.Save(new GiftVoucherRow
                {
                    Code = futureCode,
                    Value = 50000m,
                    IssueDate = futureDate,
                    ExpiryDate = futureDate.AddDays(30)
                }).SavedId;

                string expiredCode = "VC-EXP-" + tag;
                vExpiredId = vSvc.Save(new GiftVoucherRow
                {
                    Code = expiredCode,
                    Value = 60000m,
                    IssueDate = today.AddDays(-10),
                    ExpiryDate = today.AddDays(-1)
                }).SavedId;

                string cancelCode = "VC-CXL-" + tag;
                vCancelledBillId = vSvc.Save(new GiftVoucherRow
                {
                    Code = cancelCode,
                    Value = 70000m,
                    IssueDate = today.AddDays(-1),
                    ExpiryDate = today.AddDays(10)
                }).SavedId;
                billCancelledId = DbHelper.NewId();
                InsertGiftVoucherBillRef(db, billCancelledId, "HD-CXL-" + tag, vCancelledBillId, 30, -30);

                string unpaidCode = "VC-UNP-" + tag;
                vUnpaidBillId = vSvc.Save(new GiftVoucherRow
                {
                    Code = unpaidCode,
                    Value = 80000m,
                    IssueDate = today.AddDays(-1),
                    ExpiryDate = today.AddDays(10)
                }).SavedId;
                billUnpaidId = DbHelper.NewId();
                InsertGiftVoucherBillRef(db, billUnpaidId, "HD-UNP-" + tag, vUnpaidBillId, 0, 30);

                string paidCode = "VC-USE-" + tag;
                vPaidBillId = vSvc.Save(new GiftVoucherRow
                {
                    Code = paidCode,
                    Value = 90000m,
                    IssueDate = today.AddDays(-1),
                    ExpiryDate = today.AddDays(10)
                }).SavedId;
                billPaidId = DbHelper.NewId();
                InsertGiftVoucherBillRef(db, billPaidId, "HD-USE-" + tag, vPaidBillId, 30, 30);

                // 1. Mã rỗng & không tồn tại.
                VoucherInfo rEmpty = tableSvc.ValidateVoucherCode("   ", null);
                VoucherInfo rMissing = tableSvc.ValidateVoucherCode("KHONG-TON-TAI-" + tag, null);
                Check("Voucher §3/§4 (thanh toán): mã rỗng và không tồn tại trả đúng thông báo spec",
                    !rEmpty.IsValid && rEmpty.Error == "Mời bạn nhập mã voucher trước"
                    && !rMissing.IsValid && rMissing.Error == "Mã voucher 'KHONG-TON-TAI-" + tag + "' không tồn tại trong hệ thống",
                    SingleLine((rEmpty.Error ?? "") + " | " + (rMissing.Error ?? "")));

                // 2. Chưa đến ngày phát hành & đã quá hạn.
                string expectedFutureMsg = string.Format(CultureInfo.InvariantCulture,
                    "Voucher '{0}' chưa đến ngày phát hành ({1:dd/MM/yyyy})", futureCode, futureDate);
                VoucherInfo rFuture = tableSvc.ValidateVoucherCode(futureCode, null);
                VoucherInfo rExpired = tableSvc.ValidateVoucherCode(expiredCode, null);
                List<VoucherInfo> avail = tableSvc.LoadAvailableVouchers(null);
                bool excludedBadFromList = !avail.Exists(x => x.Id == vFutureId)
                    && !avail.Exists(x => x.Id == vExpiredId)
                    && !avail.Exists(x => x.Id == vPaidBillId);

                Check("Voucher §3/§4 (thanh toán): chưa đến ngày phát hành & quá hạn báo đúng thông báo và bị loại khỏi danh sách chọn",
                    !rFuture.IsValid && rFuture.Error == expectedFutureMsg
                    && !rExpired.IsValid && rExpired.Error == "Mã voucher đã quá hạn sử dụng"
                    && excludedBadFromList,
                    SingleLine((rFuture.Error ?? "") + " | " + (rExpired.Error ?? "")));

                // 3. Bill hủy (STATUS = -30) và bill chưa thanh toán (DATHANHTOAN = 0) -> voucher VẪN dùng lại được.
                VoucherInfo rCancelled = tableSvc.ValidateVoucherCode(cancelCode, null);
                VoucherInfo rUnpaid = tableSvc.ValidateVoucherCode(unpaidCode, null);
                bool includedReusableInList = avail.Exists(x => x.Id == vCancelledBillId)
                    && avail.Exists(x => x.Id == vUnpaidBillId);
                Check("Voucher §3/§4 (thanh toán): voucher ở bill hủy hoặc chưa thanh toán vẫn dùng được",
                    rCancelled.IsValid && rCancelled.Value == 70000m
                    && rUnpaid.IsValid && rUnpaid.Value == 80000m
                    && includedReusableInList,
                    SingleLine((rCancelled.Error ?? "ok") + " | " + (rUnpaid.Error ?? "ok")));

                // 4. Bill đã thanh toán -> bill khác bị chặn; tự loại trừ chính bill hiện tại (sửa thanh toán / Điều chỉnh hóa đơn) -> hợp lệ.
                VoucherInfo rUsedOther = tableSvc.ValidateVoucherCode(paidCode, null);
                VoucherInfo rSelfByCode = tableSvc.ValidateVoucherCode(paidCode, new[] { billPaidId });
                VoucherInfo rSelfById = new ServicePaymentRepository(db).Read(conn =>
                    ServicePaymentRepository.ValidateVoucherByIdIn(conn, null, vPaidBillId, today, new[] { billPaidId }));
                List<VoucherInfo> availForSelf = tableSvc.LoadAvailableVouchers(new[] { billPaidId });

                Check("Voucher §3/§4 (thanh toán & điều chỉnh HĐ): bill đã thanh toán chặn bill khác đúng thông báo, nhưng loại trừ chính bill hiện tại",
                    !rUsedOther.IsValid && rUsedOther.Error == "Mã voucher đã được sử dụng"
                    && rSelfByCode.IsValid && rSelfById.IsValid && availForSelf.Exists(x => x.Id == vPaidBillId),
                    SingleLine(rUsedOther.Error ?? ""));
            }
            finally
            {
                foreach (string bid in new[] { billCancelledId, billUnpaidId, billPaidId })
                    if (bid != null) SafeDeleteById(db, "TDONHANG", bid);
                foreach (string vid in new[] { vFutureId, vExpiredId, vCancelledBillId, vUnpaidBillId, vPaidBillId })
                {
                    if (vid == null) continue;
                    BookingExec(db, "DELETE FROM TLUUVET WHERE SODONHANG = @I", "@I", vid);
                    SafeDeleteById(db, "DVOUCHER", vid);
                }
            }
        }

        private static void InsertGiftVoucherBillRef(string db, string billId, string billName, string voucherId, int paidFlag, int status)
        {
            DateTime now = DateTime.Now;
            BookingExec(db,
                "INSERT INTO TDONHANG (ID, NAME, NGAY, LOAI, TONGCONG, DVOUCHERID, DATHANHTOAN, STATUS, USERCREATEDID, TIMECREATED) " +
                "VALUES (@I, @N, @D, 0, 100000, @V, @P, @S, 'selftest', @T)",
                "@I", billId,
                "@N", billName,
                "@D", now.Date,
                "@V", voucherId,
                "@P", paidFlag,
                "@S", status,
                "@T", now);
        }

        private static int CountActivityLogsForBill(string db, string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return 0;
            return DbHelper.Read(db, conn => DbHelper.Int(DbHelper.Scalar(conn, null,
                "SELECT COUNT(*) FROM TLUUVET WHERE SODONHANG = @I", "@I", billId)));
        }
    }
}
