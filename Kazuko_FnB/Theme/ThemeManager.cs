using System;

namespace Kazuko_FnB.Theme
{
    /// <summary>
    /// Giữ theme hiện tại, phát sự kiện khi đổi. Control tự viết lắng nghe
    /// <see cref="ThemeChanged"/> để vẽ lại. "Xem thử" = Apply tạm; hủy thì Apply lại theme cũ.
    /// </summary>
    public static class ThemeManager
    {
        private static ThemeTokens _current = BuiltInThemes.Default;

        public static event EventHandler ThemeChanged;

        public static ThemeTokens Current
        {
            get { return _current; }
        }

        public static void Apply(ThemeTokens tokens)
        {
            if (tokens == null) throw new ArgumentNullException("tokens");
            _current = tokens;
            var handler = ThemeChanged;
            if (handler != null) handler(null, EventArgs.Empty);
        }
    }
}
