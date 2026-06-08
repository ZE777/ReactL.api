using ReactL.api.Models.Base;

namespace ReactL.api.Models.Access
{
    /// <summary>
    /// 公開聊天室存取碼（邀請碼）。前台訪客須持有效存取碼才能使用公開聊天，
    /// 並以每碼每日 token 上限控制用量，避免匿名者無限消耗系統金鑰額度。
    /// 邀請連結即「前台網址 + ?code=Code」。
    /// </summary>
    public class AccessCode : AuditableEntity
    {
        /// <summary>存取碼（隨機產生，唯一），前台以 X-Access-Code 標頭或 ?code= 帶入</summary>
        /// <remarks>nvarchar(32) · NOT NULL · UNIQUE (UX_AccessCodes_Code)</remarks>
        public string Code { get; set; } = string.Empty;

        /// <summary>備註標籤（給誰／用途），僅後台顯示</summary>
        /// <remarks>nvarchar(100) · NULL</remarks>
        public string? Label { get; set; }

        /// <summary>每日 token（輸入+輸出）上限；0 表示不限制</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int DailyTokenLimit { get; set; } = 0;

        /// <summary>到期時間，null 表示永不過期</summary>
        /// <remarks>datetime2 · NULL</remarks>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>啟用狀態，false 時前台無法使用此碼</summary>
        /// <remarks>bit · NOT NULL · DEFAULT 1</remarks>
        public bool IsActive { get; set; } = true;

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public ICollection<AccessCodeUsage> Usages { get; set; } = [];
    }
}
