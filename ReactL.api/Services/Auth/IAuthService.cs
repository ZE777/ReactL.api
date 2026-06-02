using ReactL.api.DTOs.Auth;

namespace ReactL.api.Services.Auth
{
    public interface IAuthService
    {
        /// <summary>
        /// 註冊新帳號並回傳 JWT Token
        /// 若 Email 已存在則拋出 ConflictException
        /// </summary>
        Task<AuthResponse> RegisterAsync(RegisterRequest request);

        /// <summary>
        /// 驗證帳號密碼並回傳 JWT Token
        /// Email 不存在或密碼錯誤都回傳 UnauthorizedException（避免洩漏帳號是否存在）
        /// </summary>
        Task<AuthResponse> LoginAsync(LoginRequest request);
    }
}
