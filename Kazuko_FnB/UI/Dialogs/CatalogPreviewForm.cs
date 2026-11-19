using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Services;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Khung cửa sổ chứa một màn danh mục, chỉ để chụp bằng <c>--render-screen catalog:TÊNBẢNG</c> (không có mục menu).
    /// Tùy chọn: "search=chữ" nhập vào ô tìm; "node=Tên nhóm" chọn nhóm trên cây.
    /// </summary>
    internal sealed class CatalogPreviewForm : Form, IScreenPreview
    {
        private readonly CatalogView _view;

        public CatalogPreviewForm(UserSession session, string tableName)
        {
            string id = new CatalogRepository(session.DatabasePath).FindIdByTable(tableName);
            if (id == null)
                throw new ArgumentException("Không có danh mục: " + tableName);

            AutoScaleMode = AutoScaleMode.None;
            Text = tableName;
            ClientSize = new Size(1300, 720);
            _view = new CatalogView(session.DatabasePath, id, tableName) { Dock = DockStyle.Fill };
            Controls.Add(_view);
            ThemeManager.ThemeChanged += OnThemeChanged;
            Apply();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            Apply();
        }

        private void Apply()
        {
            ThemeTokens t = ThemeManager.Current;
            BackColor = t.Background;
            _view.ApplyTheme(t, t.Background);
        }

        public void Preview(string option)
        {
            if (option == null)
                return;
            if (option.StartsWith("search=", StringComparison.Ordinal))
                _view.SetSearch(option.Substring(7));
            else if (option.StartsWith("node=", StringComparison.Ordinal))
                _view.SelectCategory(option.Substring(5));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}
