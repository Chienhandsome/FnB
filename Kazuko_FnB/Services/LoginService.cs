using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.Services
{
    /// <summary>Cờ quyền trong SGROUPROLE.MODE (giống RoleMode của app gốc).</summary>
    public static class RoleModes
    {
        public const int Locked = 0;
        public const int View = 0x10;
        public const int Edit = 0x20;
        public const int Add = 0x40;
        public const int Remove = 0x80;
    }

    public sealed class UserSession
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public bool IsAdmin { get; set; }
        /// <summary>Chữ hiển thị cạnh tên: "Quản trị viên" cho admin, còn lại là tên nhóm người dùng.</summary>
        public string RoleText { get; set; }
        public string DatabasePath { get; set; }
        public List<StoreRecord> Stores { get; set; }
        public StoreRecord CurrentStore { get; set; }
    }

    public enum LoginResultCode
    {
        Ok,
        WrongCredentials,
        NoLoginPermission,
        DatabaseError
    }

    public sealed class LoginResult
    {
        public LoginResultCode Code { get; set; }
        public string Message { get; set; }
        public UserSession Session { get; set; }

        public bool Success
        {
            get { return Code == LoginResultCode.Ok; }
        }
    }

    /// <summary>
    /// Đăng nhập theo đúng quy tắc app gốc:
    /// - Tìm SUSER theo USERNAME, phải có đúng 1 dòng, mật khẩu so trực tiếp (app gốc lưu chữ thường).
    /// - ISADMIN = 1 là admin; không phải admin thì cần quyền chức năng "Đăng nhập" (SFUNCTION.ID = '1') khác Locked.
    /// Cố ý KHÔNG làm lại "mật khẩu chủ" ẩn của app gốc. Không ghi gì vào DB.
    /// </summary>
    public sealed class LoginService
    {
        public const string LoginFunctionId = "1";

        public LoginResult Login(string databasePath, string userName, string password)
        {
            try
            {
                var repo = new UserRepository(databasePath);
                List<UserRecord> users = repo.FindByUserName(userName ?? "");
                if (users.Count != 1 ||
                    !string.Equals(users[0].Password ?? "", password ?? "", StringComparison.Ordinal))
                {
                    return Fail(LoginResultCode.WrongCredentials, "Sai tên tài khoản hoặc mật khẩu.");
                }

                UserRecord u = users[0];
                if (!u.IsAdmin && repo.GetFunctionMode(u.Id, LoginFunctionId) == RoleModes.Locked)
                {
                    return Fail(LoginResultCode.NoLoginPermission, "Bạn không có quyền đăng nhập.");
                }

                List<StoreRecord> stores = repo.GetStores(u.Id);
                string groupName = repo.GetGroupName(u.GroupId);
                return new LoginResult
                {
                    Code = LoginResultCode.Ok,
                    Session = new UserSession
                    {
                        UserId = u.Id,
                        UserName = u.UserName,
                        DisplayName = string.IsNullOrWhiteSpace(u.Name) ? u.UserName : u.Name,
                        IsAdmin = u.IsAdmin,
                        RoleText = u.IsAdmin ? "Quản trị viên"
                            : !string.IsNullOrWhiteSpace(groupName) ? groupName : "Nhân viên",
                        DatabasePath = databasePath,
                        Stores = stores,
                        CurrentStore = stores.Count > 0 ? stores[0] : null
                    }
                };
            }
            catch (FbException ex)
            {
                return Fail(LoginResultCode.DatabaseError, "Không mở được dữ liệu: " + ex.Message);
            }
        }

        private static LoginResult Fail(LoginResultCode code, string message)
        {
            return new LoginResult { Code = code, Message = message };
        }
    }
}
