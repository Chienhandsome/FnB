using System.Collections.Generic;
using System.Windows.Forms;

namespace Kazuko_FnB.Services
{
    public enum MenuAction
    {
        OpenTab,
        Logout,
        Exit
    }

    /// <summary>Một mục menu / nút thanh công cụ / lối tắt, dựng từ SMENU và các mục cố định.</summary>
    public sealed class MenuNode
    {
        public MenuNode()
        {
            Children = new List<MenuNode>();
        }

        public string Id { get; set; }
        /// <summary>Tên hiển thị (đã bỏ '&amp;' của DB và đổi chữ HOA thành chữ thường đầu câu).</summary>
        public string Title { get; set; }
        /// <summary>Tên gốc trong DB, giữ để đối chiếu.</summary>
        public string RawName { get; set; }
        public string SortOrder { get; set; }
        public string ParentId { get; set; }
        public List<MenuNode> Children { get; private set; }
        public bool IsSeparator { get; set; }
        public bool Hidden { get; set; }
        /// <summary>SFUNCTION.ID kiểm quyền (từ SMENU.SFUNCTIONID); rỗng = không kiểm.</summary>
        public string FunctionId { get; set; }
        /// <summary>Tên SFUNCTION kiểm quyền cho mục cố định trong mã; rỗng = không kiểm.</summary>
        public string FunctionName { get; set; }
        public Keys Shortcut { get; set; }
        public bool InToolbar { get; set; }
        public int ToolbarIndex { get; set; }
        public string ToolbarTitle { get; set; }
        /// <summary>Tên đầy đủ khi Title đã được rút gọn để hiển thị (vd. nút thanh công cụ); dùng cho tooltip và chọn icon.</summary>
        public string FullTitle { get; set; }
        public MenuAction Action { get; set; }
        /// <summary>SMENU.STABLEDESCID: bảng nhập liệu dựng màn danh mục chung; rỗng nếu mục này không dựa vào bảng.</summary>
        public string StableDescId { get; set; }
        /// <summary>SMENU.OPERATION: 0 = mở danh mục thường; số khác = thao tác riêng (vd. 7 = gửi tin nhắn).</summary>
        public int Operation { get; set; }
        /// <summary>SMENU.LOAI: với phiếu thu/chi, 0 = phiếu thu, 1 = phiếu chi.</summary>
        public int Loai { get; set; }

        public bool IsLeaf
        {
            get { return Children.Count == 0; }
        }
    }

    /// <summary>Nhóm thẻ trên màn "Truy cập nhanh".</summary>
    public sealed class QuickGroup
    {
        public QuickGroup()
        {
            Items = new List<MenuNode>();
        }

        public string Title { get; set; }
        public string Glyph { get; set; }
        public List<MenuNode> Items { get; private set; }
    }

    public sealed class MenuModel
    {
        public MenuModel()
        {
            Roots = new List<MenuNode>();
            Toolbar = new List<MenuNode>();
            QuickGroups = new List<QuickGroup>();
        }

        public List<MenuNode> Roots { get; private set; }
        public List<MenuNode> Toolbar { get; private set; }
        public List<QuickGroup> QuickGroups { get; private set; }
    }
}
