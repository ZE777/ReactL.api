using ReactL.api.Models.Base;

namespace ReactL.api.Models.Conversations
{
    /// <summary>
    /// 管理後台測試對話中的單筆訊息
    /// 不使用軟刪除：訊息刪除為硬刪除，對話刪除時 CASCADE DELETE 自動清除所有訊息
    /// </summary>
    public class Message : BaseEntity
    {
        /// <summary>所屬對話 ID，CASCADE DELETE</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → Conversations ON DELETE CASCADE</remarks>
        public Guid ConversationId { get; set; }

        /// <summary>訊息角色，參見 Common.Constants.MessageRole 常數（user / assistant / system）</summary>
        /// <remarks>nvarchar(20) · NOT NULL</remarks>
        public string Role { get; set; } = string.Empty;

        /// <summary>訊息內容</summary>
        /// <remarks>nvarchar(max) · NOT NULL</remarks>
        public string Content { get; set; } = string.Empty;

        /// <summary>此次 AI 呼叫消耗的輸入 Token 數（Prompt Token），user 訊息時記錄</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensIn { get; set; } = 0;

        /// <summary>此次 AI 呼叫消耗的輸出 Token 數（Completion Token），assistant 訊息時記錄</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensOut { get; set; } = 0;

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public Conversation Conversation { get; set; } = null!;
    }
}
