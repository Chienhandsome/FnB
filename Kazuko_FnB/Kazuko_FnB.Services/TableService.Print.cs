using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    public sealed partial class TableService
    {
        private ServicePrintRepository PrintRepo()
        {
            return new ServicePrintRepository(_databasePath);
        }

        // ------------------------------------------------------------------ Quyền in (§4)

        public bool CanTempPrintPermission(UserSession overrideSession = null)
        {
            return HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncInTamTinh);
        }

        public bool CanReprintBill(UserSession overrideSession = null)
        {
            return HasFunction(overrideSession ?? _session, ServiceOrderConstants.FuncInLaiBill);
        }

        // ------------------------------------------------------------------ Đọc dữ liệu hóa đơn & ghi nhận in tạm tính

        public ServiceInvoicePrintDocument LoadInvoiceForPrint(string billId)
        {
            return PrintRepo().LoadInvoiceForPrint(
                billId,
                SystemConfig.SuDungChucNangTamUngTrongDonHang,
                SystemConfig.CachTinhDiem,
                SystemConfig.DoanhSoTuongUngVoi1Diem);
        }

        public string FindLatestPaidBillId(string preferredTableId = null)
        {
            return PrintRepo().FindLatestPaidBillId(preferredTableId);
        }

        /// <summary>
        /// Chuẩn bị xem trước hóa đơn ("In thử bill (F8)" trong hộp thanh toán):
        /// kiểm quyền InTamTinh, kiểm DuLieuHoaDonOk và đọc dữ liệu từ CSDL, KHÔNG thay đổi DB.
        /// </summary>
        public TempPrintPrepareResult PrepareInvoicePreviewOnly(string billId, UserSession overrideSession = null)
        {
            if (string.IsNullOrEmpty(billId))
                return TempPrintPrepareResult.Fail(ServiceOrderConstants.SelectTableFirst);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncInTamTinh))
                return TempPrintPrepareResult.ReLogin(ServiceOrderConstants.FuncInTamTinh);

            decimal roundStep = SystemConfig.LamTronTien;
            bool includeAdvance = SystemConfig.SuDungChucNangTamUngTrongDonHang;
            int pointMode = SystemConfig.CachTinhDiem;
            int revPerPoint = SystemConfig.DoanhSoTuongUngVoi1Diem;

            return PrintRepo().Read(conn =>
            {
                ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, null, billId);
                if (bill == null)
                    return TempPrintPrepareResult.Fail(ServicePrintConstants.BillNotFoundForPrint);

                ServiceOrderValidationResult val = OrderCalculator.ValidateInvoice(bill, bill.Lines, roundStep);
                if (!val.IsValid)
                    return TempPrintPrepareResult.Fail(val.ErrorMessage);

                ServiceInvoicePrintDocument doc = ServicePrintRepository.LoadInvoiceForPrintIn(
                    conn, null, billId, includeAdvance, pointMode, revPerPoint);
                if (doc == null)
                    return TempPrintPrepareResult.Fail(ServicePrintConstants.BillNotFoundForPrint);

                return new TempPrintPrepareResult
                {
                    Ok = true,
                    NewTempPrintCount = doc.TempPrintCount,
                    Document = doc
                };
            });
        }

        /// <summary>
        /// Thực hiện phần nghiệp vụ DB của "In tạm tính" (§3.7, §4, goc-PrintInvoice-nhahang):
        /// kiểm quyền InTamTinh, kiểm bill chưa thanh toán + DuLieuHoaDonOk, kiểm SoLanInTamTinhToiDa,
        /// tăng SOLANINTAMTINH + ghi INTAMTINHLUC, ghi lưu vết TLUUVET (InBill), đọc lại dữ liệu mới từ CSDL.
        /// </summary>
        public TempPrintPrepareResult PrepareAndRecordTempPrint(
            string billId,
            string tableId,
            UserSession overrideSession = null)
        {
            if (string.IsNullOrEmpty(billId))
                return TempPrintPrepareResult.Fail(ServiceOrderConstants.SelectTableFirst);

            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncInTamTinh))
                return TempPrintPrepareResult.ReLogin(ServiceOrderConstants.FuncInTamTinh);

            int maxTempPrints = SystemConfig.SoLanInTamTinhToiDa;
            decimal roundStep = SystemConfig.LamTronTien;
            bool includeAdvance = SystemConfig.SuDungChucNangTamUngTrongDonHang;
            int pointMode = SystemConfig.CachTinhDiem;
            int revPerPoint = SystemConfig.DoanhSoTuongUngVoi1Diem;

            TempPrintPrepareResult outcome = null;
            PrintRepo().RunInTransaction((conn, tx) =>
            {
                int currentTempPrints;
                if (!ServiceOrderRepository.IsBillEditableIn(conn, tx, billId, tableId, out currentTempPrints))
                {
                    outcome = TempPrintPrepareResult.Fail(ServiceOrderConstants.BillAlreadyPaid);
                    return;
                }

                ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                if (bill == null)
                {
                    outcome = TempPrintPrepareResult.Fail(ServicePrintConstants.BillNotFoundForPrint);
                    return;
                }

                bool hasItem = bill.Lines != null && bill.Lines.Exists(l => l != null && !l.IsMaterialExport && !l.IsComboChild);
                if (!hasItem)
                {
                    outcome = TempPrintPrepareResult.Fail(ServicePaymentConstants.EmptyBillError);
                    return;
                }

                ServiceOrderValidationResult val = OrderCalculator.ValidateInvoice(bill, bill.Lines, roundStep);
                if (!val.IsValid)
                {
                    outcome = TempPrintPrepareResult.Fail(val.ErrorMessage);
                    return;
                }

                if (maxTempPrints > 0 && currentTempPrints >= maxTempPrints)
                {
                    outcome = TempPrintPrepareResult.Fail(ServicePrintConstants.MaxTempPrintReached(maxTempPrints));
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                int updated = ServicePrintRepository.IncrementTempPrintIn(conn, tx, billId, actor.UserId, serverNow);
                if (updated != 1)
                {
                    outcome = TempPrintPrepareResult.Fail(ServiceOrderConstants.BillAlreadyPaid);
                    return;
                }

                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = ActivityConstants.FuncSuDungDichVu,
                        Kind = ActivityKind.InBill,
                        BillId = bill.Id,
                        TableName = bill.TableName,
                        Note = "In tạm tính hóa đơn " + bill.Name + " (lần " + (currentTempPrints + 1) + ")",
                        Amount = bill.TongCong,
                        Account = actor.UserName,
                        UserId = actor.UserId,
                        Time = serverNow
                    }
                });

                ServiceInvoicePrintDocument doc = ServicePrintRepository.LoadInvoiceForPrintIn(
                    conn, tx, billId, includeAdvance, pointMode, revPerPoint);

                outcome = new TempPrintPrepareResult
                {
                    Ok = true,
                    NewTempPrintCount = currentTempPrints + 1,
                    Document = doc
                };
            });

            return outcome ?? TempPrintPrepareResult.Fail("Không thể in tạm tính hóa đơn.");
        }

        /// <summary>
        /// Kiểm tra quyền InLaiBill và đọc lại dữ liệu bill đã thanh toán từ CSDL (không thay đổi DB).
        /// </summary>
        public TempPrintPrepareResult PrepareReprintPaidBill(string billId, UserSession overrideSession = null)
        {
            UserSession actor = overrideSession ?? _session;
            if (!HasFunction(actor, ServiceOrderConstants.FuncInLaiBill))
                return TempPrintPrepareResult.ReLogin(ServiceOrderConstants.FuncInLaiBill);

            if (string.IsNullOrEmpty(billId))
                return TempPrintPrepareResult.Fail(ServicePrintConstants.NoPaidBillToReprint);

            ServiceInvoicePrintDocument doc = LoadInvoiceForPrint(billId);
            if (doc == null)
                return TempPrintPrepareResult.Fail(ServicePrintConstants.BillNotFoundForPrint);

            return new TempPrintPrepareResult
            {
                Ok = true,
                NewTempPrintCount = doc.TempPrintCount,
                Document = doc
            };
        }

        // ------------------------------------------------------------------ In bếp / chế biến (§3.10, §6.6, goc-InCheBien)

        public static KitchenPrintSettings ReadKitchenSettings()
        {
            return new KitchenPrintSettings
            {
                Enabled = SystemConfig.SuDungChucNangInXuongBep,
                PrintFood = SystemConfig.InDoAn,
                PrintDrink = SystemConfig.InDoUong,
                PrintService = SystemConfig.InDichVu,
                PrintOther = SystemConfig.InDoKhac,
                SeparateFoodAndDrink = SystemConfig.InRiengDoAnUong,
                ByArea = SystemConfig.InPhaCheTheoKhuVuc,
                IncludeUnitPrice = SystemConfig.InDonGiaTrenMauInBep,
                Copies = Math.Max(1, Math.Min(5, SystemConfig.SoLienInCheBien)),
                OneItemPerSlip = SystemConfig.InMoiDoRa1To,
                ExtraCounterCopy = SystemConfig.InThem1LienTaiQuay,
                LogActivity = SystemConfig.LuuVetInCheBien
            };
        }

        /// <summary>
        /// Lập kế hoạch in bếp cho một bill đang mở: kiểm tra trạng thái bill, cấu hình in bếp, máy in đã thiết lập
        /// và tính danh sách món chênh lệch chưa in so với bảng TINCHEBIEN.
        /// </summary>
        public KitchenPrintPlan BuildKitchenPrintPlan(string billId, IKitchenPrinterStore printerStore)
        {
            var plan = new KitchenPrintPlan { BillId = billId };
            if (string.IsNullOrEmpty(billId))
            {
                plan.ErrorMessage = ServiceOrderConstants.SelectTableFirst;
                return plan;
            }

            KitchenPrintSettings settings = ReadKitchenSettings();
            if (!settings.Enabled)
            {
                plan.ErrorMessage = ServicePrintConstants.KitchenFeatureDisabled;
                return plan;
            }

            return PrintRepo().Read(conn =>
            {
                ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, null, billId);
                if (bill == null)
                {
                    plan.ErrorMessage = ServicePrintConstants.BillNotFoundForPrint;
                    return plan;
                }
                if (bill.PaidFlag != 0)
                {
                    plan.ErrorMessage = ServicePrintConstants.KitchenBillAlreadyPaid;
                    return plan;
                }

                plan.BillName = bill.Name ?? "";
                plan.TableName = bill.TableName ?? "";
                plan.AreaId = bill.AreaId ?? "";
                plan.AreaName = bill.AreaName ?? "";

                List<KitchenRawLine> billLines = ServicePrintRepository.LoadKitchenBillLinesIn(conn, null, billId);
                List<KitchenRawLine> printedLines = ServicePrintRepository.LoadKitchenPrintedLinesIn(conn, null, billId);
                List<KitchenPendingItem> pending = KitchenPrintRules.ComputePendingItems(
                    billLines, printedLines, settings.IncludeUnitPrice);

                bool needsSetup;
                List<KitchenPrintBatch> batches = KitchenPrintRules.BuildBatches(
                    settings, plan.AreaId, pending, printerStore, out needsSetup);

                if (needsSetup)
                {
                    plan.NeedsPrinterSetup = true;
                    plan.ErrorMessage = ServicePrintConstants.KitchenPrinterNotConfigured;
                    return plan;
                }

                if (pending.Count == 0 || batches.Count == 0)
                {
                    plan.NoItemsToPrint = true;
                    plan.ErrorMessage = ServicePrintConstants.KitchenNoItemToPrint;
                    return plan;
                }

                plan.Batches = batches;
                plan.Ok = true;
                return plan;
            });
        }

        public int GetNextKitchenPrintSeq(string billId)
        {
            if (string.IsNullOrEmpty(billId))
                return 1;
            return PrintRepo().Read(conn => ServicePrintRepository.GetNextKitchenPrintSeqIn(conn, null, billId));
        }

        /// <summary>
        /// Ghi nhận các dòng đã in vào TINCHEBIEN sau khi phát lệnh in (LuuInCheBien) và ghi TLUUVET nếu bật.
        /// Trả về thông báo lỗi nếu hóa đơn đã thanh toán giữa chừng, ngược lại trả null.
        /// </summary>
        public string RecordKitchenPrintedBatch(
            string billId,
            string tableName,
            KitchenPrintBatch batch,
            int printSeq,
            bool autoPrint)
        {
            if (string.IsNullOrEmpty(billId) || batch == null || batch.Items == null || batch.Items.Count == 0)
                return null;

            string error = null;
            bool logDetail = SystemConfig.LuuVetInCheBien;
            PrintRepo().RunInTransaction((conn, tx) =>
            {
                ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                if (bill == null || bill.PaidFlag != 0)
                {
                    error = ServicePrintConstants.KitchenBillAlreadyPaid;
                    return;
                }

                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                ServicePrintRepository.InsertKitchenPrintedItemsIn(
                    conn, tx, billId, batch.Items, printSeq, _session.UserId, serverNow);

                var logs = new List<ActivityLogRequest>();
                logs.Add(new ActivityLogRequest
                {
                    FunctionName = ActivityConstants.FuncSuDungDichVu,
                    Kind = ActivityKind.InCheBien,
                    BillId = billId,
                    TableName = string.IsNullOrEmpty(tableName) ? bill.TableName : tableName,
                    Note = (autoPrint ? "In pha chế tự động: " : "In pha chế: ") + (batch.Caption ?? ""),
                    Account = _session.UserName,
                    UserId = _session.UserId,
                    Time = serverNow
                });
                if (logDetail)
                {
                    for (int i = 0; i < batch.Items.Count; i++)
                    {
                        KitchenPendingItem it = batch.Items[i];
                        logs.Add(new ActivityLogRequest
                        {
                            FunctionName = ActivityConstants.FuncSuDungDichVu,
                            Kind = ActivityKind.InCheBien,
                            BillId = billId,
                            TableName = string.IsNullOrEmpty(tableName) ? bill.TableName : tableName,
                            Note = "In chế biến (" + (batch.PrinterName ?? "") + "): " + it.ItemName,
                            ItemName = it.ItemName,
                            Quantity = it.Quantity,
                            UnitPrice = it.UnitPrice,
                            Account = _session.UserName,
                            UserId = _session.UserId,
                            Time = serverNow
                        });
                    }
                }
                _activityLogger.WriteBatch(conn, tx, logs);
            });
            return error;
        }
    }
}
