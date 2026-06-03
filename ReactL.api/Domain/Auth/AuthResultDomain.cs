namespace ReactL.api.Domain.Auth
{
    /// <summary>登入 / 註冊成功後的業務結果</summary>
    public class AuthResultDomain
    {
        /// <summary>JWT 存取 Token</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>Token 到期時間（UTC）</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>登入使用者摘要</summary>
        public UserDomain User { get; set; } = null!;
    }
}