namespace ReactL.api.DTOs.Responses.Auth
{
    /// <summary>認證成功回應，含 JWT Token 與使用者基本資訊</summary>
    public class AuthResponse
    {
        /// <summary>JWT 存取 Token</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>Token 到期時間（UTC）</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>登入使用者摘要</summary>
        public UserInfo User { get; set; } = null!;
    }

    /// <summary>Token Payload 中嵌入的使用者摘要，避免前端額外呼叫 /user/profile</summary>
    public class UserInfo
    {
        /// <summary>使用者唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>登入 Email</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>顯示名稱</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>角色：User / Admin</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>是否必須變更密碼（種子 Admin 首登強制改密）</summary>
        public bool MustChangePassword { get; set; }
    }
}