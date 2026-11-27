using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ Xuất lại định lượng (docs/specs/xuat-lai-dinh-luong.md):
    /// Xem trước (chỉ đọc), Thực hiện xóa và sinh lại dòng XUATVATTU = 30 trong MỘT transaction
    /// bằng đúng hàm của luồng thanh toán (<see cref="ServicePaymentRepository.ComputeMaterialExportsIn"/>,
    /// <see cref="ServicePaymentRepository.DeleteOldMaterialExportsIn"/>, <see cref="ServicePaymentRepository.InsertMaterialExportLineIn"/>),
    /// báo số mặt hàng tồn âm, ghi một dòng lưu vết, rồi tính lại giá vốn bằng <see cref="CostRecalcService.RunAfterRegenerate"/>.
    /// </summary>
    public sealed class MaterialRegenService
    {
        private readonly UserSession _session;
        private readonly MaterialRegenRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public MaterialRegenService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new MaterialRegenRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView
        {
            get
            {
                return AppSession.Can(_session, MaterialRegenConstants.FunctionName, RoleModes.View)
                    || CanRun;
            }
        }

        public bool CanRun
        {
            get { return AppSession.Can(_session, MaterialRegenConstants.FunctionName, RoleModes.Edit); }
        }

        public DateTime GetServerDate()
        {
            return _repo.GetServerDate();
        }

        public List<StoreRecord> LoadAllowedStores()
        {
            return _repo.LoadAllowedStores(_session.UserId, _session.IsAdmin, SessionStoreIds());
        }

        /// <summary>Xem trước số hóa đơn đủ điều kiện, số dòng xuất vật tư sẽ xóa và sẽ sinh lại; KHÔNG ghi gì.</summary>
        public MaterialRegenPreview Preview(MaterialRegenRequest request)
        {
            if (!CanView)
                return MaterialRegenPreview.Fail(MaterialRegenConstants.NoViewPermission);
            if (request == null)
                return MaterialRegenPreview.Fail(MaterialRegenConstants.DateRequired);

            string dateErr = MaterialRegenRules.ValidateDateRange(request.FromDate, request.ToDate);
            if (dateErr != null)
                return MaterialRegenPreview.Fail(dateErr);

            DateTime from = request.FromDate.Value.Date;
            DateTime to = request.ToDate.Value.Date;
            IList<string> sessStores = SessionStoreIds();

            return _repo.Read(conn =>
            {
                HashSet<string> allowedStores = MaterialRegenRepository.ResolveAllowedStoreIdsIn(
                    conn, null, _session.UserId, _session.IsAdmin, sessStores);
                List<string> billIds = MaterialRegenRepository.LoadEligibleBillIdsIn(
                    conn, null, from, to, request.StoreId, _session.IsAdmin, allowedStores);

                int existingLines = 0;
                int newLines = 0;
                foreach (string billId in billIds)
                {
                    existingLines += MaterialRegenRepository.LoadExistingExportKeysIn(conn, null, billId).Count;
                    ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, null, billId);
                    if (bill == null)
                        continue;
                    NormalizeLineWarehouses(bill);
                    List<MaterialExportItem> exports = ServicePaymentRepository.ComputeMaterialExportsIn(conn, null, bill);
                    newLines += exports.Count;
                }

                return new MaterialRegenPreview
                {
                    Ok = true,
                    FromDate = from,
                    ToDate = to,
                    EligibleBillCount = billIds.Count,
                    ExistingExportLineCount = existingLines,
                    NewExportLineCount = newLines
                };
            });
        }

        /// <summary>
        /// Thực hiện xuất lại định lượng trong MỘT transaction (lỗi giữa chừng rollback toàn bộ),
        /// ghi một dòng lưu vết trong cùng transaction, rồi tính lại giá vốn sau khi commit.
        /// </summary>
        public MaterialRegenResult Execute(
            MaterialRegenRequest request,
            Action<int, int> progress = null,
            Action<string> testHookAfterBill = null,
            Func<UserSession, string> costRecalcRunner = null)
        {
            if (!CanRun)
                return MaterialRegenResult.Fail(MaterialRegenConstants.NoPermission);
            if (request == null)
                return MaterialRegenResult.Fail(MaterialRegenConstants.DateRequired);

            string dateErr = MaterialRegenRules.ValidateDateRange(request.FromDate, request.ToDate);
            if (dateErr != null)
                return MaterialRegenResult.Fail(dateErr);

            if (!MaterialRegenRules.IsConfirmWordValid(request.ConfirmText))
                return MaterialRegenResult.Fail(MaterialRegenConstants.ConfirmWordInvalid);

            DateTime from = request.FromDate.Value.Date;
            DateTime to = request.ToDate.Value.Date;
            IList<string> sessStores = SessionStoreIds();

            int processedBills = 0;
            int deletedLines = 0;
            int insertedLines = 0;
            int negStockCount = 0;
            List<string> negSampleNames = new List<string>();

            _repo.RunInTransaction((conn, tx) =>
            {
                DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                HashSet<string> allowedStores = MaterialRegenRepository.ResolveAllowedStoreIdsIn(
                    conn, tx, _session.UserId, _session.IsAdmin, sessStores);
                List<string> billIds = MaterialRegenRepository.LoadEligibleBillIdsIn(
                    conn, tx, from, to, request.StoreId, _session.IsAdmin, allowedStores);

                var touchedKeys = new HashSet<string>(StringComparer.Ordinal);
                int total = billIds.Count;
                for (int i = 0; i < total; i++)
                {
                    string billId = billIds[i];
                    List<string> oldKeys = MaterialRegenRepository.LoadExistingExportKeysIn(conn, tx, billId);
                    deletedLines += oldKeys.Count;
                    foreach (string k in oldKeys)
                        touchedKeys.Add(k);

                    ServiceOrderHeader bill = ServiceOrderRepository.LoadBillIn(conn, tx, billId);
                    if (bill != null)
                    {
                        NormalizeLineWarehouses(bill);
                        List<MaterialExportItem> exports = ServicePaymentRepository.ComputeMaterialExportsIn(conn, tx, bill);
                        ServicePaymentRepository.DeleteOldMaterialExportsIn(conn, tx, bill.Id);
                        foreach (MaterialExportItem exp in exports)
                        {
                            ServicePaymentRepository.InsertMaterialExportLineIn(conn, tx, bill.Id, exp, _session.UserId, serverNow);
                            if (!string.IsNullOrEmpty(exp.MaterialId) && !string.IsNullOrEmpty(exp.WarehouseId))
                                touchedKeys.Add(exp.MaterialId + "|" + exp.WarehouseId);
                            insertedLines++;
                        }
                    }

                    processedBills++;
                    if (testHookAfterBill != null)
                        testHookAfterBill(billId);
                    if (progress != null)
                        progress(processedBills, total);
                }

                negStockCount = MaterialRegenRepository.CountNegativeStockProductsIn(conn, tx, touchedKeys, out negSampleNames);

                string clippedStore = MaterialRegenRules.Clip((request.StoreName ?? "").Trim(), MaterialRegenConstants.MaxNoteLength);
                string logNote = MaterialRegenRules.BuildActivityNote(
                    from, to, clippedStore, processedBills, deletedLines, insertedLines, negStockCount);
                string account = !string.IsNullOrEmpty(_session.UserName) ? _session.UserName : (_session.DisplayName ?? "");
                _activityLogger.WriteBatch(conn, tx, new[]
                {
                    new ActivityLogRequest
                    {
                        FunctionName = MaterialRegenConstants.FunctionName,
                        Kind = ActivityKind.Khac,
                        BillId = "",
                        TableName = clippedStore,
                        Note = logNote,
                        Account = account,
                        UserId = _session.UserId,
                        Time = serverNow
                    }
                });
            });

            // Sau commit: tính lại giá vốn bất kể cấu hình TuDongTinhGiaVon; lỗi chỉ cảnh báo, không làm hỏng kết quả đã ghi.
            string costWarning;
            try
            {
                costWarning = costRecalcRunner != null
                    ? costRecalcRunner(_session)
                    : CostRecalcService.RunAfterRegenerate(_session);
            }
            catch (Exception)
            {
                costWarning = MaterialRegenConstants.CostRecalcFailedWarning;
            }

            var result = new MaterialRegenResult
            {
                Ok = true,
                FromDate = from,
                ToDate = to,
                ProcessedBillCount = processedBills,
                DeletedLineCount = deletedLines,
                InsertedLineCount = insertedLines,
                NegativeStockProductCount = negStockCount,
                CostWarning = costWarning
            };
            if (negSampleNames != null && negSampleNames.Count > 0)
                result.NegativeStockSampleNames.AddRange(negSampleNames);
            result.Message = MaterialRegenRules.BuildResultMessage(result);
            return result;
        }

        /// <summary>Đảm bảo dòng bán có DKHOHANGID rỗng ("") quy về null để ComputeMaterialExportsIn dùng kho của khu vực bàn (bill.WarehouseId).</summary>
        private static void NormalizeLineWarehouses(ServiceOrderHeader bill)
        {
            if (bill == null || bill.Lines == null)
                return;
            foreach (ServiceOrderLine line in bill.Lines)
            {
                if (line != null && string.IsNullOrEmpty(line.WarehouseId))
                    line.WarehouseId = null;
            }
        }

        private IList<string> SessionStoreIds()
        {
            if (_session.Stores == null || _session.Stores.Count == 0)
                return null;
            var ids = new List<string>(_session.Stores.Count);
            foreach (StoreRecord s in _session.Stores)
            {
                if (s != null && !string.IsNullOrEmpty(s.Id))
                    ids.Add(s.Id);
            }
            return ids;
        }
    }
}
