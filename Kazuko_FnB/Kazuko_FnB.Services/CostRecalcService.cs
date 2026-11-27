using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ Tính lại giá vốn (docs/specs/tinh-lai-gia-von.md): đọc, tính (<see cref="CostRecalcEngine"/>) và ghi
    /// trong MỘT transaction; lỗi giữa chừng thì rollback, không để dữ liệu dở dang. Ghi lưu vết một dòng.
    /// </summary>
    public sealed class CostRecalcService
    {
        private readonly UserSession _session;
        private readonly CostRecalcRepository _repo;
        private readonly ActivityLogger _activityLogger;

        public CostRecalcService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Chưa có phiên làm việc hoặc đường dẫn CSDL.", "session");
            _session = session;
            _repo = new CostRecalcRepository(session.DatabasePath);
            string account = !string.IsNullOrEmpty(session.UserName) ? session.UserName : (session.DisplayName ?? "");
            _activityLogger = new ActivityLogger(session.DatabasePath, account, session.UserId);
        }

        public bool CanView { get { return AppSession.Can(_session, CostRecalcConstants.FunctionName, RoleModes.View); } }
        public bool CanRun { get { return AppSession.Can(_session, CostRecalcConstants.FunctionName, RoleModes.Edit); } }

        /// <summary>Chạy tính lại theo yêu cầu của người dùng (cần quyền Sửa).</summary>
        public CostRecalcResult Run(Action<int, int> progress, Action testHookAfterWrite = null)
        {
            if (!CanRun)
                return CostRecalcResult.Fail(CostRecalcConstants.NoPermission);
            return RunCore(progress, testHookAfterWrite, true);
        }

        /// <summary>
        /// Chạy tự động sau khi lưu/sửa/xóa phiếu nhập kho khi cấu hình TuDongTinhGiaVon bật.
        /// Không ném lỗi: trả null khi không cần chạy hoặc chạy xong, trả câu cảnh báo khi lỗi (không làm hỏng phiếu đã lưu).
        /// </summary>
        public static string RunAutoAfterPurchase(UserSession session)
        {
            if (!SystemConfig.TuDongTinhGiaVon)
                return null;
            try
            {
                CostRecalcResult r = new CostRecalcService(session).RunCore(null, null, false);
                return r.Ok ? null : CostRecalcConstants.AutoFailedWarning;
            }
            catch (Exception)
            {
                return CostRecalcConstants.AutoFailedWarning;
            }
        }

        private CostRecalcResult RunCore(Action<int, int> progress, Action testHookAfterWrite, bool writeLog)
        {
            CostRecalcResult outcome = null;
            _repo.RunInTransaction((conn, tx) =>
            {
                List<CostProduct> products = CostRecalcRepository.LoadProductsIn(conn, tx);
                List<CostRecipeLine> recipes = CostRecalcRepository.LoadRecipesIn(conn, tx);
                List<CostLine> lines = CostRecalcRepository.LoadLinesIn(conn, tx);

                CostRecalcPlan plan = CostRecalcEngine.Compute(products, recipes, lines, progress);

                foreach (CostLineUpdate u in plan.LineUpdates)
                    CostRecalcRepository.UpdateLineCostIn(conn, tx, u.LineId, u.NewCost);
                foreach (CostProductUpdate u in plan.ProductUpdates)
                    CostRecalcRepository.UpdateProductCostIn(conn, tx, u.ProductId, u.NewCost);

                if (writeLog)
                {
                    DateTime serverNow = InvoiceNumberRepository.GetServerTimestamp(conn, tx);
                    _activityLogger.WriteBatch(conn, tx, new[]
                    {
                        new ActivityLogRequest
                        {
                            FunctionName = CostRecalcConstants.FunctionName,
                            Kind = ActivityKind.Khac,
                            BillId = "",
                            TableName = "",
                            Note = Clip(CostRecalcConstants.Done(plan.ProductUpdates.Count, plan.LineUpdates.Count)),
                            Account = _session.UserName,
                            UserId = _session.UserId,
                            Time = serverNow
                        }
                    });
                }

                if (testHookAfterWrite != null)
                    testHookAfterWrite();

                outcome = new CostRecalcResult
                {
                    Ok = true,
                    ProductsChanged = plan.ProductUpdates.Count,
                    LinesChanged = plan.LineUpdates.Count,
                    Message = lines.Count == 0
                        ? CostRecalcConstants.NoData
                        : CostRecalcConstants.Done(plan.ProductUpdates.Count, plan.LineUpdates.Count)
                };
            });
            return outcome ?? CostRecalcResult.Fail("Không thể tính lại giá vốn.");
        }

        private static string Clip(string text)
        {
            text = text ?? "";
            return text.Length > 255 ? text.Substring(0, 255) : text;
        }
    }
}
