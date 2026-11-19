namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Quy tắc đổi cờ quyền trong ma trận phân quyền (SGROUPROLE.MODE). Khóa và Xem loại trừ nhau; Thêm/Sửa/Xóa kéo theo Xem;
    /// bỏ Xem = khóa; chức năng chỉ-xem chỉ có Khóa/Xem.
    /// </summary>
    public static class RoleLogic
    {
        public const int AllBits = RoleModes.View | RoleModes.Edit | RoleModes.Add | RoleModes.Remove;

        public static bool IsLocked(int mode)
        {
            return (mode & RoleModes.View) == 0;
        }

        public static bool Has(int mode, int bit)
        {
            return (mode & bit) == bit;
        }

        /// <summary>Tích "Tất cả": đủ bốn cờ (chức năng chỉ-xem: chỉ Xem).</summary>
        public static bool IsAll(int mode, bool viewOnly)
        {
            return viewOnly ? Has(mode, RoleModes.View) : (mode & AllBits) == AllBits;
        }

        /// <summary>Tích/bỏ ô Khóa.</summary>
        public static int SetLocked(int mode, bool locked)
        {
            return locked ? RoleModes.Locked : (IsLocked(mode) ? RoleModes.View : mode);
        }

        /// <summary>Tích/bỏ một cờ Xem/Thêm/Sửa/Xóa.</summary>
        public static int SetBit(int mode, int bit, bool on)
        {
            if (bit == RoleModes.View)
                return on ? (mode | RoleModes.View) : RoleModes.Locked;
            return on ? (mode | bit | RoleModes.View) : (mode & ~bit);
        }

        /// <summary>Tích/bỏ ô "Tất cả".</summary>
        public static int SetAll(int mode, bool all, bool viewOnly)
        {
            if (all)
                return viewOnly ? RoleModes.View : AllBits;
            return RoleModes.View;
        }
    }
}
