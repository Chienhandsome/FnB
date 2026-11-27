using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ chỉ đọc cho màn "Kiểm soát order": kiểm tra quyền Xem, kiểm tra khoảng ngày,
    /// tải hóa đơn từ <see cref="OrderControlRepository"/> và phân tích qua <see cref="OrderControlAnalyzer"/>.
    /// </summary>
    public sealed class OrderControlService
    {
        private readonly UserSession _session;
        private readonly OrderControlRepository _repository;

        public OrderControlService(UserSession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            _session = session;
            _repository = new OrderControlRepository(session.DatabasePath);
        }

        /// <summary>Kiểm tra quyền Xem chức năng "Kiểm soát order" (Admin luôn có quyền).</summary>
        public static bool CanView(UserSession session)
        {
            if (session == null)
                return false;
            if (session.IsAdmin)
                return true;
            if (session.Permissions == null)
                return false;
            bool byName = (session.Permissions.ModeByName(OrderControlConstants.FunctionName) & RoleModes.View) == RoleModes.View;
            if (byName)
                return true;
            return (session.Permissions.ModeById(OrderControlConstants.FunctionId) & RoleModes.View) == RoleModes.View;
        }

        public bool CanView()
        {
            return CanView(_session);
        }

        /// <summary>Ngày hiện tại theo máy chủ DB (dùng làm mặc định Từ ngày / Đến ngày).</summary>
        public DateTime GetServerDate()
        {
            return _repository.GetServerDate();
        }

        /// <summary>
        /// Tải hóa đơn trong khoảng [fromDate, toDate] và phân tích số order.
        /// Trả kết quả rỗng kèm thông báo trong <paramref name="error"/> nếu không có quyền hoặc khoảng ngày không hợp lệ.
        /// </summary>
        public OrderControlAnalysisResult LoadAnalysis(DateTime fromDate, DateTime toDate, out string error)
        {
            error = null;
            if (!CanView())
            {
                error = OrderControlConstants.NoPermissionMessage;
                return new OrderControlAnalysisResult();
            }

            string dateError = OrderControlAnalyzer.ValidateDateRange(fromDate, toDate);
            if (dateError != null)
            {
                error = dateError;
                return new OrderControlAnalysisResult();
            }

            List<OrderControlBillRow> bills = _repository.LoadOrderBills(
                fromDate.Date,
                toDate.Date,
                _session.UserId,
                _session.IsAdmin);
            return OrderControlAnalyzer.Analyze(bills);
        }
    }
}
