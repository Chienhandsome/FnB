using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>Khung chứa màn "Tồn kho" (hoặc "Tồn nhiều kho" khi truyền tùy chọn "all"), dùng cho <c>--render-screen preview.inventory</c>.</summary>
    internal sealed class InventoryPreviewForm : Form, IScreenPreview
    {
        private readonly UserSession _session;
        private InventoryView _view;

        public InventoryPreviewForm(UserSession session) : this(session, false)
        {
        }

        public InventoryPreviewForm(UserSession session, bool allWarehousesMode)
        {
            _session = session;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1360, 760);
            MountView(allWarehousesMode);
            BackColor = ThemeManager.Current.Background;
        }

        private void MountView(bool allWarehousesMode)
        {
            if (_view != null)
            {
                Controls.Remove(_view);
                _view.Dispose();
            }
            Text = allWarehousesMode ? InventoryConstants.AllWarehousesMenuTitle : InventoryConstants.FunctionName;
            _view = new InventoryView(_session, allWarehousesMode) { Dock = DockStyle.Fill, SuppressPopup = true };
            Controls.Add(_view);
            _view.ApplyTheme(ThemeManager.Current);
        }

        internal InventoryView ViewForTest
        {
            get { return _view; }
        }

        public void Preview(string option)
        {
            if (string.Equals(option, "all", StringComparison.OrdinalIgnoreCase) && !_view.IsAllWarehousesModeForTest)
                MountView(true);
            else
                _view.ReloadAll();
        }
    }
}
