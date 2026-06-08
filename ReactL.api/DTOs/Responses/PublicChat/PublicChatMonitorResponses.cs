namespace ReactL.api.DTOs.Responses.PublicChat
{
    /// <summary>前台訪客自己的歷史對話單則（依 sessionId + 角色取回）</summary>
    public class PublicChatHistoryItem
    {
        /// <summary>訊息角色：user / assistant</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>訊息內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>傳送時間</summary>
        public DateTime CreatedAt { get; set; }
    }


    /// <summary>前台聊天監控：對話列表項目（以 SessionId 分組）</summary>
    public class PublicChatConversationSummary
    {
        /// <summary>對話工作階段識別碼</summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>存取碼字串快照（匿名為 null）</summary>
        public string? AccessCodeText { get; set; }

        /// <summary>存取碼目前的備註標籤（JOIN AccessCodes 取得；碼已刪除或匿名時為 null）</summary>
        public string? AccessCodeLabel { get; set; }

        /// <summary>此對話最近一次使用的角色名稱（JOIN Personas；無角色或角色已刪除時為 null）</summary>
        public string? PersonaName { get; set; }

        /// <summary>此對話最近一次使用的 AI 模型（格式 providerId:modelId）</summary>
        public string? ModelType { get; set; }

        /// <summary>此對話的訊息總數（user + assistant）</summary>
        public int MessageCount { get; set; }

        /// <summary>此對話累計的 Token 用量（輸入 + 輸出）</summary>
        public int TotalTokens { get; set; }

        /// <summary>第一則訊息時間</summary>
        public DateTime FirstMessageAt { get; set; }

        /// <summary>最後一則訊息時間</summary>
        public DateTime LastMessageAt { get; set; }
    }

    /// <summary>前台聊天監控：單則訊息</summary>
    public class PublicChatLogItem
    {
        public Guid Id { get; set; }

        /// <summary>訊息角色：user / assistant</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>完整訊息內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>此則訊息當下使用的角色（Persona）名稱；無角色或已刪除時為 null</summary>
        public string? PersonaName { get; set; }

        /// <summary>使用的 AI 模型</summary>
        public string? ModelType { get; set; }

        /// <summary>輸入 Token 數</summary>
        public int TokensIn { get; set; }

        /// <summary>輸出 Token 數</summary>
        public int TokensOut { get; set; }

        /// <summary>訊息建立時間</summary>
        public DateTime CreatedAt { get; set; }
    }
}