using System;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class BillAdjustService
    {
        /// <summary>
        /// Hủy hóa đơn đã thanh toán ở màn "Quản lý bán hàng" trong MỘT transaction duy nhất
        /// (docs/specs/quan-ly-ban-hang-dieu-chinh-hoa-don.md §2.4, §3.2, §5 P4, §6.3, §7):
        /// - Quyền HuyHoaDon (CanLogin).
        /// - Không giới hạn thời gian ThoiGianChoPhepHuyBill.
        /// - Chặn bill thuộc nhóm thanh toán chung (NHOMGUID khác rỗng, §6.3).
        /// - Nếu bill có phiếu thu/chi liên quan (STATUS &lt;&gt; -30) và chưa xác nhận: trả NeedsVoucherWarningConfirmation
        ///   (cho hủy sau khi cảnh báo, KHÔNG xóa phiếu thu/chi — §6.3).
        /// - NhapLyDoKhiHuyHoaDon = 30 bắt buộc có lý do; ngược lại dùng "Hủy hóa đơn".
        /// - Lưu TDONHANGHUY (TIENGIAMGIA ghi tiền theo P4) + TDONHANGHUYCHITIET (bỏ XUATVATTU=30 và combo con),
        ///   xóa cứng TINCHEBIEN + TDONHANGCHITIET + TDONHANG, ghi TLUUVET với CHUCNANG = "Quản lý bán hàng" (P4).
        /// </summary>
        public BillCancelPaidResult CancelPaidBill(BillCancelPaidRequest req, UserSession overrideSession = null)
        {
            if (req == null || string.IsNullOrEmpty(req.BillId))
                return BillCancelPaidResult.Fail(SalesManageConstants.SelectBillFirst);

            UserSession actor = overrideSession ?? _session;
            if (!TableService.HasFunction(actor, ServiceOrderConstants.FuncHuyHoaDon))
                return BillCancelPaidResult.ReLogin(ServiceOrderConstants.FuncHuyHoaDon);

            bool requireReason = SystemConfig.NhapLyDoKhiHuyHoaDon;
            string trimmedReason = (req.Reason ?? "").Trim();
            string finalReason = trimmedReason.Length > 0 ? trimmedReason : ServiceTransferConstants.CancelDefaultReason;

            BillCancelPaidResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, tx, req.BillId);
                if (bill == null)
                {
                    outcome = BillCancelPaidResult.Fail(ServiceTransferConstants.CancelBillAlreadyDeleted);
                    return;
                }
                if (bill.PaidFlag != 30)
                {
                    outcome = BillCancelPaidResult.Fail(SalesManageConstants.BillNotPaidOrMissing);
                    return;
                }
                if (!string.IsNullOrEmpty(bill.GroupGuid))
                {
                    outcome = BillCancelPaidResult.Fail(SalesManageConstants.GroupBillCancelBlocked);
                    return;
                }
                if (requireReason && trimmedReason.Length == 0)
                {
                    outcome = BillCancelPaidResult.Fail(ServiceTransferConstants.CancelReasonRequired);
                    return;
                }

                // §6.3: Hủy bill đã thanh toán có cọc/tạm ứng/phiếu thu chi -> cảnh báo nhắc kiểm tra, vẫn cho hủy, không xóa phiếu.
                BillRelatedVouchersSummary vouchers = SalesManageRepository.GetRelatedVouchersSummaryIn(
                    conn, tx, bill.Id, bill.BookingId);
                if (vouchers.Count > 0 && !req.ConfirmRelatedVouchers)
                {
                    outcome = new BillCancelPaidResult
                    {
                        Ok = false,
                        NeedsVoucherWarningConfirmation = true,
                        RelatedVouchers = vouchers,
                        ErrorMessage = SalesManageConstants.RelatedVouchersCancelWarning(
                            vouchers.Count, vouchers.TotalIncome, vouchers.TotalExpense)
                    };
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                string cashierName = !string.IsNullOrEmpty(actor.DisplayName) ? actor.DisplayName : (actor.UserName ?? "");

                string huyId = ServiceTransferRepository.InsertCancelledBillSnapshotIn(
                    conn, tx, bill.Id, bill.TableName ?? "", finalReason, cashierName, actor.UserId, serverNow);
                if (string.IsNullOrEmpty(huyId))
                {
                    outcome = BillCancelPaidResult.Fail(ServiceTransferConstants.CancelBillAlreadyDeleted);
                    return;
                }

                if (req.TestHookAfterSnapshot != null)
                    req.TestHookAfterSnapshot();

                ServiceTransferRepository.HardDeleteBillIn(conn, tx, bill.Id);

                // P4 (§6.3): lưu vết hủy ở màn này ghi đúng CHUCNANG = "Quản lý bán hàng".
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncQuanLyBanHang,
                        Kind = ActivityKind.HuyBill,
                        BillId = bill.Id,
                        TableName = bill.TableName ?? "",
                        Note = string.Format("Hủy đơn hàng {0} (Lý do: {1})", bill.Name, finalReason),
                        Amount = bill.TongCong,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = serverNow
                    }
                });

                outcome = new BillCancelPaidResult
                {
                    Ok = true,
                    RelatedVouchers = vouchers,
                    CancelledBillHuyId = huyId
                };
            });

            return outcome ?? BillCancelPaidResult.Fail("Không thể hủy hóa đơn.");
        }
    }
}
