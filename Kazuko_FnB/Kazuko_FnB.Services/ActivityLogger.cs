using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Dịch vụ ghi và đọc lưu vết hoạt động (TLUUVET), docs/specs/luu-vet-hoat-dong.md §2.5, §3.1, §3.4.
    /// Chỉ ghi khi KichHoatLuuVetHoatDong = 30; cắt ngắn TAIKHOAN/CHUCNANG &gt; 36 ký tự (Q5);
    /// hỗ trợ ghi nhiều dòng trong một transaction.
    /// </summary>
    public sealed class ActivityLogger
    {
        private readonly string _databasePath;
        private readonly string _defaultUserName;
        private readonly string _defaultUserId;
        private readonly ActivityLogRepository _repository;

        public ActivityLogger(string databasePath, string userName = null, string userId = null)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Chưa có đường dẫn cơ sở dữ liệu.", "databasePath");
            _databasePath = databasePath;
            _defaultUserName = userName;
            _defaultUserId = userId;
            _repository = new ActivityLogRepository(databasePath);
        }

        /// <summary>Hợp đồng gọi dùng chung cho các màn nghiệp vụ (spec §2.5), dùng phiên AppSession.Current.</summary>
        public static bool Track(
            string chucNang,
            ActivityKind phanLoai,
            string soDonHangId,
            string ban,
            string noiDung,
            string tenHang = "",
            decimal soLuong = 0m,
            decimal donGia = 0m,
            decimal thanhTien = 0m)
        {
            string db = AppSession.DatabasePath;
            if (string.IsNullOrEmpty(db))
                return false;
            var logger = new ActivityLogger(db, CurrentUserName(), AppSession.UserId);
            return logger.Write(chucNang, phanLoai, soDonHangId, ban, noiDung, tenHang, soLuong, donGia, thanhTien);
        }

        /// <summary>Ghi nhiều dòng lưu vết trong một transaction (dùng phiên AppSession.Current).</summary>
        public static int TrackBatch(IEnumerable<ActivityLogRequest> requests)
        {
            string db = AppSession.DatabasePath;
            if (string.IsNullOrEmpty(db))
                return 0;
            var logger = new ActivityLogger(db, CurrentUserName(), AppSession.UserId);
            return logger.WriteBatch(requests);
        }

        /// <summary>Ghi nhiều dòng lưu vết trong transaction đã mở sẵn (chỉ ghi khi cờ bật).</summary>
        public static int TrackBatch(FbConnection conn, FbTransaction tx, IEnumerable<ActivityLogRequest> requests,
            string databasePath = null)
        {
            string db = databasePath ?? AppSession.DatabasePath;
            if (string.IsNullOrEmpty(db) || !IsEnabledFor(db))
                return 0;
            List<ActivityLogEntry> entries = BuildEntries(requests, CurrentUserName(), AppSession.UserId);
            if (entries.Count == 0)
                return 0;
            return ActivityLogRepository.InsertBatch(conn, tx, entries);
        }

        /// <summary>Kiểm tra cờ KichHoatLuuVetHoatDong = 30 cho DB của logger này.</summary>
        public bool IsEnabled()
        {
            return IsEnabledFor(_databasePath);
        }

        /// <summary>Đọc lại trực tiếp từ DB (dùng khi mở màn Lưu vết để cảnh báo kịp thời sau khi đổi cấu hình).</summary>
        public bool IsEnabledFresh()
        {
            return _repository.IsLoggingEnabled();
        }

        public bool Write(
            string chucNang,
            ActivityKind phanLoai,
            string soDonHangId,
            string ban,
            string noiDung,
            string tenHang = "",
            decimal soLuong = 0m,
            decimal donGia = 0m,
            decimal thanhTien = 0m)
        {
            int written = WriteBatch(new[]
            {
                new ActivityLogRequest
                {
                    FunctionName = chucNang,
                    Kind = phanLoai,
                    BillId = soDonHangId,
                    TableName = ban,
                    Note = noiDung,
                    ItemName = tenHang,
                    Quantity = soLuong,
                    UnitPrice = donGia,
                    Amount = thanhTien
                }
            });
            return written > 0;
        }

        /// <summary>Ghi danh sách lưu vết trong một transaction; không ghi khi cờ KichHoatLuuVetHoatDong ≠ 30.</summary>
        public int WriteBatch(IEnumerable<ActivityLogRequest> requests)
        {
            if (requests == null || !IsEnabled())
                return 0;
            List<ActivityLogEntry> entries = BuildEntries(requests, ResolveDefaultUserName(), ResolveDefaultUserId());
            if (entries.Count == 0)
                return 0;
            return _repository.InsertBatch(entries);
        }

        /// <summary>Ghi danh sách lưu vết vào transaction có sẵn; không ghi khi cờ KichHoatLuuVetHoatDong ≠ 30.</summary>
        public int WriteBatch(FbConnection conn, FbTransaction tx, IEnumerable<ActivityLogRequest> requests)
        {
            if (requests == null || !IsEnabled())
                return 0;
            List<ActivityLogEntry> entries = BuildEntries(requests, ResolveDefaultUserName(), ResolveDefaultUserId());
            if (entries.Count == 0)
                return 0;
            return ActivityLogRepository.InsertBatch(conn, tx, entries);
        }

        public DateTime GetServerDate()
        {
            return _repository.GetServerTime().Date;
        }

        public List<ActivityBillItem> LoadBills(DateTime fromDate, DateTime toDate, string billNumber)
        {
            return _repository.LoadBills(fromDate, toDate, billNumber);
        }

        public List<ActivityLogEntry> LoadLogs(string billId, string accountName)
        {
            return _repository.LoadLogs(billId, accountName);
        }

        public List<ActivityAccountItem> LoadAccounts()
        {
            return _repository.LoadAccounts();
        }

        /// <summary>
        /// Xóa toàn bộ lưu vết (spec §3.4). Kiểm tra quyền admin và mật khẩu tại lớp Services (không chỉ ở UI).
        /// </summary>
        public int DeleteAll(UserSession session, string password)
        {
            if (session == null || !session.IsAdmin)
                throw new UnauthorizedAccessException("Chỉ tài khoản quản trị mới được xóa lưu vết hoạt động.");
            if (!IsDeletePasswordValid(password))
                throw new InvalidOperationException("Mật khẩu xác nhận xóa lưu vết không đúng.");
            return _repository.DeleteAll();
        }

        // ------------------------------------------------------------------ Luật thuần (dễ kiểm bằng selftest)

        /// <summary>Cắt chuỗi vượt quá độ dài cột (Q5: TAIKHOAN/CHUCNANG tối đa 36 ký tự, ghi cảnh báo vào log).</summary>
        public static string TruncateField(string value, int maxLength, string fieldName, out bool truncated)
        {
            string text = value ?? "";
            if (maxLength > 0 && text.Length > maxLength)
            {
                truncated = true;
                string cut = text.Substring(0, maxLength);
                Trace.TraceWarning("TLUUVET.{0} vượt quá {1} ký tự (gốc {2} ký tự), đã cắt ngắn còn '{3}'.",
                    fieldName, maxLength, text.Length, cut);
                return cut;
            }
            truncated = false;
            return text;
        }

        public static string TruncateField(string value, int maxLength, string fieldName)
        {
            bool ignored;
            return TruncateField(value, maxLength, fieldName, out ignored);
        }

        /// <summary>Nút Xóa lưu vết chỉ hiện khi là admin VÀ giữ Ctrl+Shift lúc mở màn (spec §3.4).</summary>
        public static bool CanShowDeleteButton(UserSession session, bool ctrlShiftHeld)
        {
            return session != null && session.IsAdmin && ctrlShiftHeld;
        }

        /// <summary>Mật khẩu xóa lưu vết: so chuỗi thô với "9876" (spec §3.4, Q1).</summary>
        public static bool IsDeletePasswordValid(string password)
        {
            return string.Equals(password, ActivityConstants.DeletePassword, StringComparison.Ordinal);
        }

        /// <summary>Lọc phía lưới phải theo chuỗi gõ ở ô "Lọc dữ liệu" trên 9 cột hiển thị (không phân biệt hoa/thường/dấu).</summary>
        public static bool MatchesFilter(ActivityLogEntry entry, string query)
        {
            if (entry == null)
                return false;
            string q = CatalogRepository.Fold(query ?? "").Trim();
            if (q.Length == 0)
                return true;

            string[] fields =
            {
                entry.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                entry.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                entry.BillNumber ?? "",
                entry.Note ?? "",
                entry.Account ?? "",
                entry.Device ?? "",
                entry.TableName ?? "",
                entry.FunctionName ?? "",
                entry.KindDisplayName ?? ""
            };
            for (int i = 0; i < fields.Length; i++)
            {
                if (CatalogRepository.Fold(fields[i]).IndexOf(q, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ Nội bộ

        private static bool IsEnabledFor(string databasePath)
        {
            if (string.Equals(databasePath, AppSession.DatabasePath, StringComparison.OrdinalIgnoreCase))
                return SystemConfig.KichHoatLuuVetHoatDong;
            return new ActivityLogRepository(databasePath).IsLoggingEnabled();
        }

        private string ResolveDefaultUserName()
        {
            if (!string.IsNullOrEmpty(_defaultUserName))
                return _defaultUserName;
            return CurrentUserName();
        }

        private string ResolveDefaultUserId()
        {
            if (!string.IsNullOrEmpty(_defaultUserId))
                return _defaultUserId;
            return AppSession.UserId ?? "";
        }

        private static string CurrentUserName()
        {
            if (AppSession.Current == null)
                return "";
            if (!string.IsNullOrEmpty(AppSession.Current.UserName))
                return AppSession.Current.UserName;
            return AppSession.Current.DisplayName ?? "";
        }

        private static List<ActivityLogEntry> BuildEntries(IEnumerable<ActivityLogRequest> requests,
            string defaultUserName, string defaultUserId)
        {
            var result = new List<ActivityLogEntry>();
            foreach (ActivityLogRequest req in requests)
            {
                if (req == null)
                    continue;
                string rawAccount = req.Account ?? defaultUserName ?? "";
                string rawFunc = req.FunctionName ?? "";
                string device = string.IsNullOrEmpty(req.Device) ? Environment.MachineName : req.Device;

                result.Add(new ActivityLogEntry
                {
                    Time = req.Time ?? DateTime.MinValue,
                    Date = req.Time.HasValue ? req.Time.Value.Date : DateTime.MinValue,
                    BillId = req.BillId ?? "",
                    TableName = req.TableName ?? "",
                    Note = req.Note ?? "",
                    Account = TruncateField(rawAccount, ActivityConstants.MaxAccountLength, "TAIKHOAN"),
                    Device = device ?? "",
                    FunctionName = TruncateField(rawFunc, ActivityConstants.MaxFunctionLength, "CHUCNANG"),
                    KindValue = (int)req.Kind,
                    ItemName = req.ItemName ?? "",
                    Quantity = req.Quantity,
                    UnitPrice = req.UnitPrice,
                    Amount = req.Amount,
                    CreatedById = req.UserId ?? defaultUserId ?? ""
                });
            }
            return result;
        }
    }
}
