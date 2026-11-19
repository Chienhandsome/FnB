using Kazuko_FnB.Domain;
using Kazuko_FnB.Infrastructure;
using Kazuko_FnB.Services;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.UI
{
    /// <summary>Một lựa chọn (ID, tên hiển thị) cho ComboBox/ListBox; ToString trả về tên.</summary>
    internal sealed class Choice
    {
        public readonly string Key;
        public readonly string Text;

        public Choice(string key, string text)
        {
            Key = key;
            Text = text;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
