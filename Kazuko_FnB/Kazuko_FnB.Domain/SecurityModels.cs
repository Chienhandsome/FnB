using System.Collections.Generic;
using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>
    /// Quyền chức năng của người dùng (SGROUPROLE.MODE theo SFUNCTION). Admin thấy tất cả.
    /// Chức năng không có dòng quyền = Locked (0), giống app gốc.
    /// </summary>
    public sealed class FunctionPermissions
    {
        /// <summary>Đủ bốn cờ Xem/Sửa/Thêm/Xóa (dành cho admin).</summary>
        public const int AllBits = RoleModes.View | RoleModes.Edit | RoleModes.Add | RoleModes.Remove;

        private readonly Dictionary<string, int> _byId = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>();

        public FunctionPermissions(bool isAdmin)
        {
            IsAdmin = isAdmin;
        }

        public bool IsAdmin { get; private set; }

        public void Add(string functionId, string functionName, int mode)
        {
            if (!string.IsNullOrEmpty(functionId))
                _byId[functionId] = mode;
            if (!string.IsNullOrEmpty(functionName))
                _byName[functionName] = mode;
        }

        /// <summary>Mục không gắn chức năng thì luôn cho phép.</summary>
        public bool AllowsId(string functionId)
        {
            if (IsAdmin || string.IsNullOrEmpty(functionId))
                return true;
            int m;
            return _byId.TryGetValue(functionId, out m) && m != RoleModes.Locked;
        }

        public bool AllowsName(string functionName)
        {
            if (IsAdmin || string.IsNullOrEmpty(functionName))
                return true;
            int m;
            return _byName.TryGetValue(functionName, out m) && m != RoleModes.Locked;
        }

        /// <summary>Cờ quyền (RoleModes) theo tên chức năng. Admin có đủ cờ; không có dòng = 0.</summary>
        public int ModeByName(string functionName)
        {
            if (IsAdmin)
                return AllBits;
            int m;
            return !string.IsNullOrEmpty(functionName) && _byName.TryGetValue(functionName, out m) ? m : RoleModes.Locked;
        }

        /// <summary>Cờ quyền (RoleModes) theo ID chức năng.</summary>
        public int ModeById(string functionId)
        {
            if (IsAdmin)
                return AllBits;
            int m;
            return !string.IsNullOrEmpty(functionId) && _byId.TryGetValue(functionId, out m) ? m : RoleModes.Locked;
        }

        /// <summary>Có cờ <paramref name="bit"/> (RoleModes.View/Edit/Add/Remove) trên chức năng tên này không.</summary>
        public bool Can(string functionName, int bit)
        {
            return (ModeByName(functionName) & bit) == bit;
        }
    }

    /// <summary>Cờ quyền trong SGROUPROLE.MODE (giống RoleMode của app gốc).</summary>
    public static class RoleModes
    {
        public const int Locked = 0;
        public const int View = 0x10;
        public const int Edit = 0x20;
        public const int Add = 0x40;
        public const int Remove = 0x80;
    }

    public sealed class UserSession
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public bool IsAdmin { get; set; }
        /// <summary>Chữ hiển thị cạnh tên: "Quản trị viên" cho admin, còn lại là tên nhóm người dùng.</summary>
        public string RoleText { get; set; }
        public string DatabasePath { get; set; }
        public List<StoreRecord> Stores { get; set; }
        public StoreRecord CurrentStore { get; set; }
        /// <summary>Quyền chức năng của người dùng (nạp một lần lúc đăng nhập). Có thể null ở lệnh render thử.</summary>
        public FunctionPermissions Permissions { get; set; }
    }

    public enum LoginResultCode
    {
        Ok,
        WrongCredentials,
        NoLoginPermission,
        DatabaseError
    }

    public sealed class LoginResult
    {
        public LoginResultCode Code { get; set; }
        public string Message { get; set; }
        public UserSession Session { get; set; }

        public bool Success
        {
            get { return Code == LoginResultCode.Ok; }
        }
    }

    public enum PasswordCheck
    {
        Ok,
        /// <summary>Mật khẩu cũ nhập sai.</summary>
        WrongOld,
        /// <summary>Mật khẩu mới và ô nhập lại không giống nhau.</summary>
        Mismatch,
        /// <summary>Mật khẩu mới trùng mật khẩu cũ (bị chặn).</summary>
        SameAsOld,
        /// <summary>Mật khẩu mới để trống: cho phép nhưng phải hỏi xác nhận (như app gốc).</summary>
        EmptyNeedsConfirm
    }
}
