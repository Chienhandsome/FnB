using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Một cột hiển thị trên lưới danh mục, dựng từ metadata <c>SCOLUMN</c>.</summary>
    public sealed class CatalogColumn
    {
        public string Name { get; set; }
        public string Caption { get; set; }
        /// <summary>SCOLUMN.CONTROLTYPE: Text, Number, CheckBox, Lookup, Date, RadioButton…</summary>
        public string ControlType { get; set; }
        public string Format { get; set; }
        /// <summary>Tên bảng được tham chiếu (cột Lookup); null nếu không phải Lookup.</summary>
        public string RefTable { get; set; }
        /// <summary>Độ rộng gợi ý ở 96 DPI (0 = chưa có, view tự chọn).</summary>
        public int Width { get; set; }
        public Type ClrType { get; set; }

        public bool IsLookup
        {
            get { return RefTable != null; }
        }
    }

    /// <summary>Định nghĩa một màn danh mục (bảng, cột, cột nhóm, bảng cây nhóm).</summary>
    public sealed class CatalogDefinition
    {
        public CatalogDefinition()
        {
            Columns = new List<CatalogColumn>();
        }

        public string StableDescId { get; set; }
        public string TableName { get; set; }
        public string Caption { get; set; }
        /// <summary>STABLEDESC.TABLETYPE: 0 = danh mục phẳng, 2 = cây nhóm + lưới.</summary>
        public int TableType { get; set; }
        /// <summary>Cột của bảng này trỏ tới nhóm (vd. DNHOMMATHANGID); null nếu không có cây.</summary>
        public string CategoryColumn { get; set; }
        /// <summary>Bảng cây nhóm (vd. DNHOMMATHANG); null nếu không có cây.</summary>
        public string CategoryTable { get; set; }
        public List<CatalogColumn> Columns { get; private set; }
    }

    public sealed class CatalogNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public string SortOrder { get; set; }
    }

    public sealed class CatalogRow
    {
        public string Id { get; set; }
        public string CategoryId { get; set; }
        /// <summary>Giá trị hiển thị theo thứ tự <see cref="CatalogDefinition.Columns"/> (đã đổi Lookup thành tên).</summary>
        public object[] Values { get; set; }
    }

    public sealed class CatalogData
    {
        public CatalogData()
        {
            Rows = new List<CatalogRow>();
            Nodes = new List<CatalogNode>();
        }

        public List<CatalogRow> Rows { get; private set; }
        public List<CatalogNode> Nodes { get; private set; }
    }

    /// <summary>Một cột trong cấu hình lưới <c>SGRID</c> của app gốc.</summary>
    public sealed class GridColumnSpec
    {
        public string Name { get; set; }
        public int Width { get; set; }
        public bool Visible { get; set; }
    }
}
