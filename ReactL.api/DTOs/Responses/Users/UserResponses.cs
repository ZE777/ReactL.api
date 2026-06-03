namespace ReactL.api.DTOs.Responses.Users
{
    /// <summary>使用者個人資料回應</summary>
    public class UserProfileResponse
    {
        /// <summary>使用者唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>登入 Email</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>顯示名稱</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>角色：User / Admin</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>帳號是否啟用</summary>
        public bool IsActive { get; set; }

        /// <summary>最後登入時間</summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>帳號建立時間</summary>
        public DateTime CreatedAt { get; set; }
    }
}
