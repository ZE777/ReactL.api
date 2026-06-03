using ReactL.api.Domain.Auth;
using ReactL.api.DTOs.Requests.Users;

namespace ReactL.api.Services.Users
{
    /// <summary>使用者服務介面</summary>
    public interface IUserService
    {
        /// <summary>取得目前登入使用者的業務物件</summary>
        Task<UserDomain> GetProfileAsync(Guid userId);

        /// <summary>更新顯示名稱，回傳更新後的使用者業務物件</summary>
        Task<UserDomain> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);

        /// <summary>
        /// 修改密碼
        /// 先驗證目前密碼正確才允許更新，防止 CSRF 攻擊
        /// </summary>
        Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    }
}
