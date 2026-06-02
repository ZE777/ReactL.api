using ReactL.api.DTOs.Users;

namespace ReactL.api.Services.Users
{
    public interface IUserService
    {
        /// <summary>取得目前登入使用者的個人資料</summary>
        Task<UserProfileResponse> GetProfileAsync(Guid userId);

        /// <summary>更新顯示名稱</summary>
        Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);

        /// <summary>
        /// 修改密碼
        /// 先驗證目前密碼正確才允許更新，防止 CSRF 攻擊
        /// </summary>
        Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    }
}
