using ReactL.api.Models.Base;

namespace ReactL.api.Models.PublicChat
{
    /// <summary>
    /// 前台公開聊天（存取碼進來的訪客對話）的訊息記錄。
    /// 每則 user / assistant 訊息各記一筆，供後台 Admin 監控前台聊天室。
    /// 以 SessionId 將同一訪客的連續對話分組（前端 localStorage 產生並以 X-Chat-Session 帶入）。
    /// 唯讀記錄（不修改），逾 PublicChatSettings.LogRetentionDays 天由背景服務清除。
    /// </summary>
    public class PublicChatLog : BaseEntity
    {
        /// <summary>
        /// 前端產生的對話工作階段識別碼，將同一訪客的連續訊息分組成一段對話。
        /// 對應監控頁的「對話」單位（類比 ExternalMessage.ExternalUserId）。
        /// </summary>
        /// <remarks>nvarchar(64) · NOT NULL</remarks>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 命中的存取碼 Id；不要求存取碼或匿名時為 null。
        /// 存取碼被刪除時設為 NULL（保留歷史記錄），顯示改用 AccessCodeText 快照。
        /// </summary>
        /// <remarks>uniqueidentifier · NULL · FK → AccessCodes(Id) ON DELETE SET NULL</remarks>
        public Guid? AccessCodeId { get; set; }

        /// <summary>
        /// 存取碼字串快照（記錄當下的碼），即使存取碼後被刪除仍可辨識來源；匿名時為 null。
        /// </summary>
        /// <remarks>nvarchar(32) · NULL</remarks>
        public string? AccessCodeText { get; set; }

        /// <summary>訊息角色（user / assistant）</summary>
        /// <remarks>nvarchar(20) · NOT NULL</remarks>
        public string Role { get; set; } = string.Empty;

        /// <summary>訊息完整內容</summary>
        /// <remarks>nvarchar(max) · NOT NULL</remarks>
        public string Content { get; set; } = string.Empty;

        /// <summary>使用的 AI 模型（格式 providerId:modelId）</summary>
        /// <remarks>nvarchar(50) · NULL</remarks>
        public string? ModelType { get; set; }

        /// <summary>使用的 Persona Id（公開角色），無角色時為 null</summary>
        /// <remarks>uniqueidentifier · NULL</remarks>
        public Guid? PersonaId { get; set; }

        /// <summary>輸入 Token 數（僅 assistant 訊息有值）</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensIn { get; set; } = 0;

        /// <summary>輸出 Token 數（僅 assistant 訊息有值）</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensOut { get; set; } = 0;
    }
}