using System;
using System.Drawing;
using System.Windows.Forms;
using Kazuko_FnB.UI.Controls;

namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp "Chọn ảnh": chọn file ảnh (jpg, gif, bmp, png), xem trước, Đồng ý / Hủy / Bỏ ảnh.
    /// Kết quả: <see cref="Result"/> = ảnh mới (byte[]); <see cref="Removed"/> = người dùng chọn "Bỏ ảnh".
    /// </summary>
    internal sealed class ImagePickerDialog : ThemedDialog
    {
        private readonly PictureBox _pic = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };

        public byte[] Result { get; private set; }
        public bool Removed { get; private set; }

        public ImagePickerDialog(string title, byte[] current)
            : base(title ?? "Chọn ảnh", "Chọn một file ảnh (jpg, gif, bmp, png)", 480, 420)
        {
            float s = Dpi;
            Result = current;

            CancelButton = AddButton("Hủy", null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Bỏ ảnh", null, delegate { Removed = true; Result = null; DialogResult = DialogResult.OK; Close(); });
            FlatButton ok = AddButton("Đồng ý", "primary", delegate { DialogResult = DialogResult.OK; Close(); });
            AcceptButton = ok;

            var choose = new FlatButton
            {
                Text = "Chọn ảnh…",
                Dock = DockStyle.Bottom,
                Height = (int)(38 * s),
                Radius = (int)(7 * s)
            };
            choose.Click += delegate { ChooseFile(); };

            Body.Controls.Add(_pic);
            Body.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = (int)(8 * s) });
            Body.Controls.Add(choose);
            ShowCurrent();
        }

        private void ChooseFile()
        {
            using (var dlg = new OpenFileDialog { Filter = ImageUtil.FileFilter })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    byte[] data = ImageUtil.ReadValidated(dlg.FileName);
                    Result = data;
                    Removed = false;
                    ShowCurrent();
                }
                catch (Exception)
                {
                    Msg.Warning(this, ImageUtil.LoadError);
                }
            }
        }

        private void ShowCurrent()
        {
            Image old = _pic.Image;
            _pic.Image = null;
            if (old != null)
                old.Dispose();
            try
            {
                _pic.Image = ImageUtil.FromBytes(Result);
            }
            catch (Exception)
            {
                _pic.Image = null;
            }
        }

        /// <summary>Mở hộp chọn ảnh. OK → trả về true; <paramref name="data"/> = ảnh mới, hoặc null nếu bỏ ảnh.</summary>
        public static bool Pick(IWin32Window owner, string title, byte[] current, out byte[] data)
        {
            using (var dlg = new ImagePickerDialog(title, current))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK)
                {
                    data = current;
                    return false;
                }
                data = dlg.Result;
                return true;
            }
        }
    }
}
