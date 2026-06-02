namespace ReactL.api.DTOs.Monitor
{
    /// <summary>監控頁：外部平台訊息列表項目</summary>
    public class ExternalMessageListItem
    {
        public Guid Id { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string BotName { get; set; } = string.Empty;
        public string ExternalUserId { get; set; } = string.Empty;
        public string? ExternalChannelId { get; set; }
        public string Role { get; set; } = string.Empty;
        /// <summary>截斷後的內容預覽（最多 100 字元），完整內容需另呼叫詳情 API</summary>
        public string ContentPreview { get; set; } = string.Empty;
        public int TokensIn { get; set; }
        public int TokensOut { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>監控頁查詢參數</summary>
    public class MonitorQueryParams
    {
        /// <summary>平台篩選：line / discord，null 表示全部</summary>
        public string? Platform { get; set; }

        /// <summary>開始時間（UTC）</summary>
        public DateTime? From { get; set; }

        /// <summary>結束時間（UTC）</summary>
        public DateTime? To { get; set; }

        /// <summary>外部使用者 ID 篩選</summary>
        public string? ExternalUserId { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    // ── Token 用量統計 ────────────────────────────────────────────────────────

    /// <summary>依日期彙總的 Token 用量（圖表資料）</summary>
    public class TokenStatsByDate
    {
        public DateOnly Date { get; set; }
        public int TokensIn { get; set; }
        public int TokensOut { get; set; }
        public int RequestCount { get; set; }
    }

    /// <summary>依模型彙總的 Token 用量</summary>
    public class TokenStatsByModel
    {
        public string ModelType { get; set; } = string.Empty;
        public int TokensIn { get; set; }
        public int TokensOut { get; set; }
        public int RequestCount { get; set; }
    }

    /// <summary>Token 統計查詢參數</summary>
    public class StatsQueryParams
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        /// <summary>來源篩選：admin / web / line / discord</summary>
        public string? Source { get; set; }
    }

    /// <summary>統計總覽（Dashboard 卡片用）</summary>
    public class StatsSummary
    {
        public int TotalRequests { get; set; }
        public int TotalTokensIn { get; set; }
        public int TotalTokensOut { get; set; }
        public List<TokenStatsByDate> ByDate { get; set; } = [];
        public List<TokenStatsByModel> ByModel { get; set; } = [];
    }
}
