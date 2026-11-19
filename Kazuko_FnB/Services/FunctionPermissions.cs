using System.Collections.Generic;

namespace Kazuko_FnB.Services
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
}
