using System.Collections.Generic;
using System.Data;
using System;

namespace Kazuko_FnB.Domain
{
    /// <summary>Kiểu điều khiển của một khóa cấu hình (SCONFIG.CONTROLTYPE).</summary>
    public enum ConfigControl
    {
        Number = 3,
        Text = 5,
        MultiLine = 6,
        Image = 7,
        Lookup = 8,
        Check = 9,
        Combo = 10
    }

    /// <summary>Một dòng SCONFIG hiển thị trên màn Cấu hình.</summary>
    public sealed class ConfigItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Caption { get; set; }
        public string GroupId { get; set; }
        /// <summary>0 = ô chính; 30 = ô phụ nằm dưới ô cha ngay trước nó.</summary>
        public int Tab { get; set; }
        public int SortOrder { get; set; }
        /// <summary>1 chuỗi, 3 số, 5 blob (SCONFIG.DATATYPE).</summary>
        public int DataType { get; set; }
        public int ControlType { get; set; }
        public int IntValue { get; set; }
        public decimal DecimalValue { get; set; }
        public string TextValue { get; set; }
        public string RefTableId { get; set; }
        public string MoreDetail { get; set; }
        /// <summary>Combo: danh sách mục (mỗi dòng một mục; giá trị lưu = chỉ số). Lookup: bộ lọc dạng CỘT = 'guid'.</summary>
        public string OtherConfig { get; set; }

        public ConfigControl Control
        {
            get { return (ConfigControl)ControlType; }
        }

        public string[] ComboItems
        {
            get
            {
                if (string.IsNullOrEmpty(OtherConfig))
                    return new string[0];
                return OtherConfig.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.None);
            }
        }
    }

    public sealed class ConfigGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SortOrder { get; set; }
    }

    /// <summary>Giá trị gọn của một khóa cho lớp SystemConfig.</summary>
    public sealed class ConfigValue
    {
        public int DataType { get; set; }
        public int IntValue { get; set; }
        public decimal DecimalValue { get; set; }
        public string TextValue { get; set; }
    }

    /// <summary>Một thay đổi cần ghi vào SCONFIG (theo NAME).</summary>
    public sealed class ConfigChange
    {
        public string Name { get; set; }
        public int DataType { get; set; }
        public int IntValue { get; set; }
        public decimal DecimalValue { get; set; }
        public string TextValue { get; set; }
        public byte[] BlobValue { get; set; }
        public bool IsBlob { get; set; }
    }
}
