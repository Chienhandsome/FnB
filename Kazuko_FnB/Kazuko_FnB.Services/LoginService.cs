using System;
using System.Collections.Generic;
using FirebirdSql.Data.FirebirdClient;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Services
{
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
                        CurrentStore = stores.Count > 0 ? stores[0] : null,
                        Permissions = repo.GetFunctionPermissions(u.Id, u.IsAdmin)
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
