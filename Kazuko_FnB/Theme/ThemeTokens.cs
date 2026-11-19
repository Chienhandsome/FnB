using System.Drawing;

namespace Kazuko_FnB.Theme
{
    /// <summary>
    /// Một bảng màu = một bộ token. Màn hình nghiệp vụ đọc màu từ đây,
    /// không hard-code màu trực tiếp.
    /// </summary>
    public sealed class ThemeTokens
    {
        public string Name { get; set; }

        // Thông tin hiển thị trên thẻ ở màn "Giao diện chương trình".
        public string Tag { get; set; }
        public string Audience { get; set; }
        public string Description { get; set; }

        public Color Background { get; set; }
        public Color Surface { get; set; }
        public Color Text { get; set; }
        public Color TextMuted { get; set; }
        public Color Primary { get; set; }
        public Color OnPrimary { get; set; }
        public Color Accent { get; set; }
        public Color Border { get; set; }
        public Color Danger { get; set; }
        public Color Success { get; set; }
    }
}
