using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Kiểu hiển thị của một cột trên lưới tab liên quan (pane dưới lưới danh mục).</summary>
    public enum DetailKind
    {
        Text,
        Money,
        Date,
        DateTime,
        Bool
    }

    /// <summary>Một cột của lưới tab liên quan: tên cột trong kết quả truy vấn + tiêu đề + kiểu hiển thị.</summary>
    public sealed class DetailColumn
    {
        public string Field { get; set; }
        public string Header { get; set; }
        public DetailKind Kind { get; set; }
        public int Width { get; set; }
    }

    /// <summary>
    /// Một tab liên quan dưới lưới danh mục (như KryptonNavigator của app gốc, No1TreeGrid.LoadReferences):
    /// tab = một bảng trỏ tới bảng đang xem qua cột khóa ngoại (SCOLUMN.REFTABLEID), tiêu đề lấy từ SFORM
    /// (FORMTYPE = 0, kèm LOAI) hoặc từ STABLEDESC.DESCRIPTION khi không có form.
    /// </summary>
    public sealed class DetailTab
    {
        public string Title { get; set; }
        public string TableName { get; set; }
        public string TableId { get; set; }
        /// <summary>Cột khóa ngoại của bảng tab trỏ tới dòng đang chọn (vd. DBANID, TDATHANGID).</summary>
        public string KeyColumn { get; set; }
        /// <summary>LOAI của form tạo ra tab; -1 = không lọc theo LOAI.</summary>
        public int Loai { get; set; }
        public List<DetailColumn> Columns { get; set; }
    }
}
