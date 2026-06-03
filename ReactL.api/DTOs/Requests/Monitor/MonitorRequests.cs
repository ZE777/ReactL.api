namespace ReactL.api.DTOs.Requests.Monitor
{
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

    /// <summary>對話列表查詢參數（以使用者 ID 為單位分組）</summary>
    public class ConversationQueryParams
    {
        /// <summary>平台篩選：line / discord，null 表示全部</summary>
        public string? Platform { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 30;
    }

    /// <summary>Token 統計查詢參數</summary>
    public class StatsQueryParams
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        /// <summary>來源篩選：admin / web / line / discord</summary>
        public string? Source { get; set; }
    }
}