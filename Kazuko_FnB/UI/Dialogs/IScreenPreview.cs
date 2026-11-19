namespace Kazuko_FnB.UI.Dialogs
{
    /// <summary>
    /// Hộp thoại hỗ trợ lệnh --render-screen: nhận tùy chọn dạng chữ (ví dụ "tab=2", "group=Bán hàng")
    /// để chuyển sang trạng thái cần chụp trước khi lưu ảnh.
    /// </summary>
    internal interface IScreenPreview
    {
        void Preview(string option);
    }
}
