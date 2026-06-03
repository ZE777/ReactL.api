namespace ReactL.api.DTOs.Responses.Monitor
{
    /// <summary>監控頁：外部平台訊息列表項目</summary>
    public class ExternalMessageListItem
    {
        /// <summary>訊息唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>訊息來源平台：line / discord</summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>所屬 Bot 名稱（JOIN 取得）</summary>
        public string BotName { get; set; } = string.Empty;

        /// <summary>外部平台的使用者識別碼</summary>
        public string ExternalUserId { get; set; } = string.Empty;

        /// <summary>外部平台的頻道識別碼（群組訊息時有值）</summary>
        public string? ExternalChannelId { get; set; }

        /// <summary>訊息角色：user / assistant</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>截斷後的內容預覽（最多 100 字元），完整內容需另呼叫詳情 API</summary>
        public string ContentPreview { get; set; } = string.Empty;

        /// <summary>輸入 Token 數</summary>
        public int TokensIn { get; set; }

        /// <summary>輸出 Token 數</summary>
        public int TokensOut { get; set; }

        /// <summary>訊息建立時間</summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>依日期彙總的 Token 用量（圖表資料）</summary>
    public class TokenStatsByDate
    {
        /// <summary>統計日期</summary>
        public DateOnly Date { get; set; }

        /// <summary>輸入 Token 總數</summary>
        public int TokensIn { get; set; }

        /// <summary>輸出 Token 總數</summary>
        public int TokensOut { get; set; }

        /// <summary>該日請求次數</summary>
        public int RequestCount { get; set; }
    }

    /// <summary>依模型彙總的 Token 用量</summary>
    public class TokenStatsByModel
    {
        /// <summary>模型識別碼，格式為 providerId:modelId</summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>輸入 Token 總數</summary>
        public int TokensIn { get; set; }

        /// <summary>輸出 Token 總數</summary>
        public int TokensOut { get; set; }

        /// <summary>請求次數</summary>
        public int RequestCount { get; set; }
    }

    /// <summary>統計總覽（Dashboard 卡片用）</summary>
    public class StatsSummary
    {
        /// <summary>請求總次數</summary>
        public int TotalRequests { get; set; }

        /// <summary>輸入 Token 總計</summary>
        public int TotalTokensIn { get; set; }

        /// <summary>輸出 Token 總計</summary>
        public int TotalTokensOut { get; set; }

        /// <summary>依日期分組的統計資料（折線圖用）</summary>
        public List<TokenStatsByDate> ByDate { get; set; } = [];

        /// <summary>依模型分組的統計資料（圓餅圖用）</summary>
        public List<TokenStatsByModel> ByModel { get; set; } = [];
    }
}