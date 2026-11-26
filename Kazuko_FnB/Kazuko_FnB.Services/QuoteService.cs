using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ nghiệp vụ Báo giá bán hàng: kiểm quyền "Báo giá" qua AppSession.Can ở tầng thao tác
    /// (không chỉ ẩn nút UI), kiểm ràng buộc đầu phiếu/chi tiết, gọi QuoteRepository và ghi lưu vết ActivityLogger.
    /// </summary>
    public sealed class QuoteService
    {
        private readonly UserSession _session;
        private readonly QuoteRepository _repo;

        public QuoteService(UserSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.DatabasePath))
                throw new ArgumentException("Phiên đăng nhập chưa có đường dẫn CSDL.", "session");
            _session = session;
            _repo = new QuoteRepository(session.DatabasePath);
        }

        public bool CanView { get { return AppSession.Can(_session, QuoteConstants.FunctionName, RoleModes.View); } }
        public bool CanAdd { get { return AppSession.Can(_session, QuoteConstants.FunctionName, RoleModes.Add); } }
        public bool CanEdit { get { return AppSession.Can(_session, QuoteConstants.FunctionName, RoleModes.Edit); } }
        public bool CanRemove { get { return AppSession.Can(_session, QuoteConstants.FunctionName, RoleModes.Remove); } }

        public List<QuoteListRow> LoadQuotes(QuoteFilter filter)
        {
            if (!CanView)
                return new List<QuoteListRow>();
            return _repo.LoadQuotes(filter);
        }

        public QuoteRow LoadQuote(string quoteId)
        {
            if (!CanView)
                return null;
            return _repo.LoadQuote(quoteId);
        }

        public List<QuoteLine> LoadLines(string quoteId)
        {
            if (!CanView)
                return new List<QuoteLine>();
            return _repo.LoadLines(quoteId);
        }

        public List<QuoteSaleItem> LoadSaleItems(bool sortByCode)
        {
            return _repo.LoadSaleItems(sortByCode);
        }

        public List<LookupItem> LoadUnits()
        {
            return _repo.LoadUnits();
        }

        public QuoteCustomerInfo LoadCustomer(string customerId)
        {
            return _repo.LoadCustomer(customerId);
        }

        public string GenerateNumber(DateTime? forDate = null)
        {
            return _repo.GenerateNumber(forDate);
        }

        public QuoteSaveResult SaveQuote(QuoteRow row, List<QuoteLine> lines)
        {
            if (row == null)
                return QuoteSaveResult.Fail("Thiếu dữ liệu báo giá.");

            bool isNew = string.IsNullOrEmpty(row.Id);
            if (isNew && !CanAdd)
                return QuoteSaveResult.Fail(QuoteText.NoPermission);
            if (!isNew && !CanEdit)
                return QuoteSaveResult.Fail(QuoteText.NoPermission);

            if (string.IsNullOrWhiteSpace(row.Name))
                return QuoteSaveResult.Fail(QuoteText.Required("Số phiếu"));

            if (lines != null)
            {
                foreach (QuoteLine line in lines)
                {
                    if (line == null || line.Quantity <= 0m)
                        return QuoteSaveResult.Fail(QuoteText.EnterQuantity);
                }
            }

            QuoteSaveResult result = _repo.SaveQuote(row, lines, _session.UserId);
            if (result.Ok)
            {
                string actionNote = (isNew ? "Thêm mới báo giá: " : "Cập nhật báo giá: ") + (row.Name ?? "");
                TryWriteActivityLog(result.SavedId, actionNote, row.TongCong);
            }
            return result;
        }

        public QuoteDeleteResult DeleteQuote(string quoteId, string quoteNumber = null)
        {
            if (!CanRemove)
                return QuoteDeleteResult.Fail(QuoteText.NoPermission);
            if (string.IsNullOrEmpty(quoteId))
                return new QuoteDeleteResult { Ok = true };

            QuoteDeleteResult result = _repo.DeleteQuote(quoteId);
            if (result.Ok)
            {
                string actionNote = "Xóa báo giá: " + (string.IsNullOrEmpty(quoteNumber) ? quoteId : quoteNumber);
                TryWriteActivityLog(quoteId, actionNote, 0m);
            }
            return result;
        }

        private void TryWriteActivityLog(string quoteId, string note, decimal totalAmount)
        {
            try
            {
                string userName = !string.IsNullOrEmpty(_session.UserName)
                    ? _session.UserName
                    : (_session.DisplayName ?? "");
                var logger = new ActivityLogger(_session.DatabasePath, userName, _session.UserId);
                logger.Write(
                    QuoteConstants.FunctionName,
                    ActivityKind.Khac,
                    quoteId ?? "",
                    "",
                    note ?? "",
                    "",
                    0m,
                    0m,
                    totalAmount);
            }
            catch (DataAccessException)
            {
                // Lỗi ghi lưu vết không làm hỏng giao dịch báo giá đã lưu/xóa thành công.
            }
        }
    }
}
