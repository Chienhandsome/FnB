namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Phiên đăng nhập hiện tại cho các màn hình/dialog (không cần truyền tay qua nhiều lớp).
    /// Đặt khi mở khung chính; xóa khi đóng.
    /// </summary>
    public static class AppSession
    {
        public static UserSession Current { get; set; }

        public static string DatabasePath
        {
            get { return Current == null ? null : Current.DatabasePath; }
        }

        public static string UserId
        {
            get { return Current == null ? null : Current.UserId; }
        }

        /// <summary>Có cờ quyền <paramref name="bit"/> trên chức năng <paramref name="functionName"/>? Chưa đăng nhập = không.</summary>
        public static bool Can(string functionName, int bit)
        {
            if (Current == null)
                return false;
            if (Current.IsAdmin)
                return true;
            return Current.Permissions != null && Current.Permissions.Can(functionName, bit);
        }
    }
}
