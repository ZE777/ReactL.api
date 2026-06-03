using ReactL.api.Domain.Auth;
using ReactL.api.DTOs.Requests.Auth;

namespace ReactL.api.Services.Auth
{
    /// <summary>認證服務介面</summary>
    public interface IAuthService
    {
        /// <summary>
        /// 註冊新帳號並回傳業務結果（含 JWT Token）
        /// 若 Email 已存在則拋出 ConflictException
        /// </summary>
        Task<AuthResultDomain> RegisterAsync(RegisterRequest request);

        /// <summary>
        /// 驗證帳號密碼並回傳業務結果（含 JWT Token）
        /// Email 不存在或密碼錯誤都回傳 UnauthorizedException（避免洩漏帳號是否存在）
        /// </summary>
        Task<AuthResultDomain> LoginAsync(LoginRequest request);
    }
}
