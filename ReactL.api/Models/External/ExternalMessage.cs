using ReactL.api.Models.Base;
using ReactL.api.Models.BotBindings;

namespace ReactL.api.Models.External
{
    /// <summary>
    /// 外部平台（LINE / Discord）Bot 收到的對話訊息
    /// 每次 Webhook 觸發時記錄，用於監控頁面顯示外部使用者的對話歷史
    /// </summary>
    public class ExternalMessage : BaseEntity
    {
        /// <summary>處理此訊息的 Bot 綁定</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → BotBindings</remarks>
        public Guid BotBindingId { get; set; }

        /// <summary>
        /// 訊息來源平台（冗餘欄位）
        /// 雖然可以透過 BotBinding.Platform 取得，但直接存在此處加速 Monitor 頁的篩選查詢
        /// 避免每次篩選都 JOIN BotBindings 表
        /// </summary>
        /// <remarks>nvarchar(20) · NOT NULL · 冗餘欄位，加速複合索引 (Platform, CreatedAt)</remarks>
        public string Platform { get; set; } = string.Empty;

        /// <summary>外部平台的使用者 ID（LINE userId / Discord userId）</summary>
        /// <remarks>nvarchar(200) · NOT NULL</remarks>
        public string ExternalUserId { get; set; } = string.Empty;

        /// <summary>
        /// 外部頻道 ID（Discord 專用，對應 Discord 的 Channel ID）
        /// LINE 的對話以 userId 識別，不需要此欄位
        /// </summary>
        /// <remarks>nvarchar(200) · NULL</remarks>
        public string? ExternalChannelId { get; set; }

        /// <summary>訊息角色（user / assistant）</summary>
        /// <remarks>nvarchar(20) · NOT NULL</remarks>
        public string Role { get; set; } = string.Empty;

        /// <summary>訊息內容</summary>
        /// <remarks>nvarchar(max) · NOT NULL</remarks>
        public string Content { get; set; } = string.Empty;

        /// <summary>輸入 Token 數</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensIn { get; set; } = 0;

        /// <summary>輸出 Token 數</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensOut { get; set; } = 0;

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public BotBinding BotBinding { get; set; } = null!;
    }
}
