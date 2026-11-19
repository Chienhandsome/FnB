using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một dòng của danh sách chọn (ô tra cứu).</summary>
    public sealed class LookupItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>Địa chỉ (chỉ có ở bảng có cột DIACHI: khách hàng, nhà cung cấp); null nếu không có.</summary>
        public string Address { get; set; }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    public sealed class GroupRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string ImageId { get; set; }
    }

    public sealed class ImageRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
    }

    /// <summary>Một chức năng (SFUNCTION) hiển thị trong ma trận phân quyền.</summary>
    public sealed class FunctionRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
        /// <summary>Chức năng chỉ có quyền Có/Không (SFUNCTION.NOTE = 'View'): chỉ có cột Khóa + Xem.</summary>
        public bool ViewOnly { get; set; }
    }

    /// <summary>Một báo cáo hoặc thư mục báo cáo (SREPORT) cho phân quyền xem báo cáo.</summary>
    public sealed class ReportRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public bool IsFolder { get; set; }
    }

    /// <summary>Tài khoản trên form "Tài khoản người dùng" (chỉ các cột có trên form).</summary>
    public sealed class UserRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string GroupId { get; set; }
        public string EmployeeId { get; set; }
    }

    /// <summary>Một cửa hàng và việc người dùng có được truy cập hay không.</summary>
    public sealed class StoreAccess
    {
        public string StoreId { get; set; }
        public string StoreName { get; set; }
        public bool Access { get; set; }
    }

    public sealed class UserRecord
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool IsAdmin { get; set; }
        public string GroupId { get; set; }
    }

    public sealed class StoreRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
