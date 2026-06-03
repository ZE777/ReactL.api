using ReactL.api.Models.Base;
using ReactL.api.Models.BotBindings;
using ReactL.api.Models.Conversations;
using ReactL.api.Models.Personas;
using ReactL.api.Models.PromptTemplates;
using ReactL.api.Models.Stats;

namespace ReactL.api.Models.Auth
{
    /// <summary>系統使用者，管理後台的操作人員帳號</summary>
    public class User : AuditableEntity
    {
        /// <summary>登入 Email，唯一索引</summary>
        /// <remarks>nvarchar(256) · NOT NULL · UNIQUE (IX_Users_Email)</remarks>
        public string Email { get; set; } = string.Empty;

        /// <summary>bcrypt 加密後的密碼雜湊，不儲存明文</summary>
        /// <remarks>nvarchar(500) · NOT NULL</remarks>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>顯示名稱，用於前台 UI 顯示</summary>
        /// <remarks>nvarchar(100) · NOT NULL</remarks>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 角色，目前支援 "User" / "Admin"
        /// 預留多租戶或細粒度權限擴充空間
        /// </summary>
        /// <remarks>nvarchar(20) · NOT NULL · DEFAULT 'User'</remarks>
        public string Role { get; set; } = "User";

        /// <summary>帳號啟用狀態，false 時登入會被拒絕但資料保留</summary>
        /// <remarks>bit · NOT NULL · DEFAULT 1</remarks>
        public bool IsActive { get; set; } = true;

        /// <summary>最後成功登入時間，用於安全審計</summary>
        /// <remarks>datetime2 · NULL</remarks>
        public DateTime? LastLoginAt { get; set; }

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public ICollection<Persona> Personas { get; set; } = [];
        public ICollection<PromptTemplate> PromptTemplates { get; set; } = [];
        public ICollection<BotBinding> BotBindings { get; set; } = [];
        public ICollection<Conversation> Conversations { get; set; } = [];
        public ICollection<TokenUsageStat> TokenUsageStats { get; set; } = [];
    }
}