using System;
using System.IO;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Tab "Cơ sở dữ liệu" (chế độ máy này): kiểm tra kết nối, tạo DB mới từ TEMPLATE.FDB.
    /// </summary>
    public sealed class DatabaseService
    {
        public string TemplatePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TEMPLATE.FDB"); }
        }

        /// <summary>Trả về null nếu kết nối được, ngược lại là thông báo lỗi.</summary>
        public string TestConnection(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                return "Chưa chọn tệp dữ liệu.";
            if (!File.Exists(databasePath))
                return "Không tìm thấy tệp dữ liệu: " + databasePath;
            try
            {
                using (var conn = KazukoConnection.Open(databasePath))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM SUSER";
                    cmd.ExecuteScalar();
                }
                return null;
            }
            catch (FbException ex)
            {
                return "Không mở được dữ liệu: " + ex.Message;
            }
        }

        /// <summary>Chép TEMPLATE.FDB ra đường dẫn mới. Không ghi đè tệp đã có.</summary>
        public string CreateFromTemplate(string targetPath)
        {
            if (!File.Exists(TemplatePath))
                return "Thiếu tệp mẫu TEMPLATE.FDB cạnh chương trình.";
            if (File.Exists(targetPath))
                return "Tệp đã tồn tại. Chọn tên khác để không ghi đè dữ liệu cũ.";
            try
            {
                string dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(TemplatePath, targetPath, false);
                File.SetAttributes(targetPath, FileAttributes.Normal);
                return null;
            }
            catch (IOException ex)
            {
                return "Không tạo được dữ liệu: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                return "Không có quyền ghi vào thư mục này: " + ex.Message;
            }
        }
    }
}
