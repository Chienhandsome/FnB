using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        // ------------------------------------------------------------------ 4. Hủy bill trên bàn (§3.8, Q20, §7)

        /// <summary>
        /// Hủy hóa đơn đang mở trên bàn trong MỘT transaction duy nhất:
        /// - Quyền HuyHoaDon (CanLogin).
        /// - Chặn nếu bill đã thanh toán/đã hủy, bàn bị máy khác giữ, bill thuộc nhóm thanh toán chung (NHOMGUID),
        ///   quá thời gian ThoiGianChoPhepHuyBill phút từ BATDAU, hoặc có phiếu thu tạm ứng còn hiệu lực (Q20).
        /// - Khi NhapLyDoKhiHuyHoaDon = 30 bắt buộc nhập lý do; ngược lại mặc định "Hủy hóa đơn".
        /// - Ghi TDONHANGHUY + TDONHANGHUYCHITIET (bỏ dòng XUATVATTU=30 và combo con, BAN = tên bàn lúc hủy),
        ///   xóa cứng TINCHEBIEN + TDONHANGCHITIET + TDONHANG (trigger P3 tự trả DBAN.TDONHANGID về NULL),
        ///   nhả khóa bàn và ghi TLUUVET (HuyBill) trong cùng transaction. Lỗi ở bất kỳ bước nào đều rollback sạch.
        /// - Không xóa phiếu thu đặt cọc (TDATHANGID) theo quy-tien.md §4.2.
        /// </summary>
        public TableTransferResult CancelBill(CancelBillRequest req, UserSession overrideSession = null)
        {
            if (req == null || string.IsNullOrEmpty(req.BillId))
                return TableTransferResult.Fail(ServiceOrderConstants.SelectTableFirst);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncHuyHoaDon))
                return TableTransferResult.ReLogin(ServiceOrderConstants.FuncHuyHoaDon);

            bool requireReason = SystemConfig.NhapLyDoKhiHuyHoaDon;
            string trimmedReason = (req.Reason ?? "").Trim();
            if (requireReason && trimmedReason.Length == 0)
                return TableTransferResult.Fail(ServiceTransferConstants.CancelReasonRequired);
            string finalReason = trimmedReason.Length > 0 ? trimmedReason : ServiceTransferConstants.CancelDefaultReason;

            bool checkLock = SystemConfig.HeThongChayNhieuMayTram;
            int maxCancelMinutes = SystemConfig.ThoiGianChoPhepHuyBill;

            TableTransferResult outcome = null;
            TransferRepo().RunInTransaction((conn, tx) =>
            {
                ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, tx, req.BillId);
                if (bill == null)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.CancelBillAlreadyDeleted);
                    return;
                }
                if (bill.PaidFlag != 0)
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableAlreadyPaidBlocked);
                    return;
                }

                string tableId = !string.IsNullOrEmpty(req.TableId) ? req.TableId : bill.TableId;
                int tmpPrint;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, bill.Id, tableId, out tmpPrint))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.TableAlreadyPaidBlocked);
                    return;
                }

                string tableName = bill.TableName ?? "";
                if (string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(tableId))
                {
                    ServiceTableInfo tbl = ServiceTransferRepository.LoadTableIn(conn, tx, tableId);
                    if (tbl != null)
                        tableName = tbl.Name;
                }

                if (checkLock && !string.IsNullOrEmpty(tableId))
                {
                    string holder;
                    if (ServiceOrderRepository.IsTableLockedByOtherIn(conn, tx, tableId, actor.UserName, out holder))
                    {
                        outcome = TableTransferResult.Fail(ServiceTransferConstants.TableLockedBy(tableName, holder));
                        return;
                    }
                }

                // §3.8 & §7: chặn hủy bill thuộc nhóm thanh toán chung (NHOMGUID không rỗng).
                if (!string.IsNullOrEmpty(bill.GroupGuid))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.CancelBlockedInGroup);
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);

                // §3.8 & §7: chặn nếu quá ThoiGianChoPhepHuyBill phút từ BATDAU.
                if (!TableTransferRules.IsWithinCancelWindow(bill.BatDau, serverNow, maxCancelMinutes))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.CancelBlockedExpired(maxCancelMinutes));
                    return;
                }

                // Q20 (§3.8 & §7): chặn hủy nếu bill có phiếu thu tạm ứng còn hiệu lực (STATUS <> -30).
                if (ServiceTransferRepository.HasActiveAdvanceVoucherIn(conn, tx, bill.Id))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.CancelBlockedHasAdvance(tableName));
                    return;
                }

                // A1: chặn hủy nếu bill mở từ phiếu đặt còn phiếu thu cọc đặt trước còn hiệu lực (STATUS <> -30).
                List<string> activeDeposits = ServiceTransferRepository.GetActiveBookingDepositNumbersIn(conn, tx, bill.BookingId);
                if (activeDeposits.Count > 0)
                {
                    outcome = TableTransferResult.Fail(ServiceOrderConstants.CancelBlockedHasBookingDeposit(string.Join(", ", activeDeposits.ToArray())));
                    return;
                }

                string cashierName = !string.IsNullOrEmpty(actor.DisplayName) ? actor.DisplayName : (actor.UserName ?? "");

                string huyId = ServiceTransferRepository.InsertCancelledBillSnapshotIn(
                    conn, tx, bill.Id, tableName, finalReason, cashierName, actor.UserId, serverNow);
                if (string.IsNullOrEmpty(huyId))
                {
                    outcome = TableTransferResult.Fail(ServiceTransferConstants.CancelBillAlreadyDeleted);
                    return;
                }

                if (req.TestHookAfterSnapshot != null)
                    req.TestHookAfterSnapshot();

                ServiceTransferRepository.HardDeleteBillIn(conn, tx, bill.Id);
                ServiceTransferRepository.TransferTableLockIn(conn, tx, tableId, null, actor.UserName, false);

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.HuyBill,
                        BillId = bill.Id,
                        TableName = tableName,
                        Note = string.Format("Hủy hóa đơn {0} trên bàn {1} (Lý do: {2})", bill.Name, tableName, finalReason),
                        Amount = bill.TongCong,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = serverNow
                    }
                });

                outcome = new TableTransferResult
                {
                    Ok = true,
                    SourceBillId = bill.Id,
                    TargetTableId = tableId,
                    CancelledBillHuyId = huyId
                };
            });

            return outcome ?? TableTransferResult.Fail("Không thể hủy hóa đơn.");
        }
    }
}
