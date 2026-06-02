using System.Security.Claims;

namespace ReactL.api.Common.Extensions
{
    /// <summary>
    /// ClaimsPrincipal 擴充方法，統一從 JWT Token 中取出使用者資訊
    /// 避免各 Controller 重複撰寫 FindFirst(ClaimTypes.NameIdentifier) 等程式碼
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// 取得當前登入使用者的 UserId（Guid）
        /// JWT 簽發時需將 UserId 存入 ClaimTypes.NameIdentifier
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">Token 中找不到 UserId Claim 時拋出</exception>
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // JWT 中缺少 UserId 不應該在正常流程發生；若發生代表 Token 生成有問題
            if (string.IsNullOrEmpty(idClaim) || !Guid.TryParse(idClaim, out var userId))
                throw new UnauthorizedAccessException("JWT Token 中缺少有效的 UserId");

            return userId;
        }

        /// <summary>取得當前登入使用者的 Email</summary>
        public static string GetEmail(this ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        /// <summary>取得當前登入使用者的角色（Role）</summary>
        public static string GetRole(this ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        /// <summary>判斷當前使用者是否為 Admin</summary>
        public static bool IsAdmin(this ClaimsPrincipal user) =>
            user.GetRole() == "Admin";
    }
}
