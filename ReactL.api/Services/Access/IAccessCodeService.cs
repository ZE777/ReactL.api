namespace ReactL.api.Services.Access
{
    /// <summary>公開聊天存取碼閘門判定結果</summary>
    public class AccessGateResult
    {
        /// <summary>是否允許進行本次聊天</summary>
        public bool Allowed { get; init; }

        /// <summary>命中的存取碼 Id（用於串流結束後記錄用量）；不要求存取碼時可能為 null</summary>
        public Guid? AccessCodeId { get; init; }

        /// <summary>拒絕時回給前端的 SSE chunk 類型："error" 或 "quota_exceeded"</summary>
        public string ChunkType { get; init; } = "error";

        /// <summary>拒絕時的提示訊息</summary>
        public string? Message { get; init; }

        public static AccessGateResult Allow(Guid? accessCodeId) =>
            new() { Allowed = true, AccessCodeId = accessCodeId };

        public static AccessGateResult Deny(string chunkType, string message) =>
            new() { Allowed = false, ChunkType = chunkType, Message = message };
    }

    /// <summary>前台存取碼狀態（供前端 gate 顯示剩餘額度）</summary>
    public class AccessCodeStatus
    {
        /// <summary>本站是否要求存取碼</summary>
        public bool RequireAccessCode { get; set; }

        /// <summary>聊天記錄保留天數（逾期自動清除）；0 = 永久保留</summary>
        public int LogRetentionDays { get; set; }

        /// <summary>提供的碼是否有效（可用）</summary>
        public bool Valid { get; set; }

        /// <summary>備註標籤</summary>
        public string? Label { get; set; }

        /// <summary>每日 token 上限；0 = 不限制</summary>
        public int DailyTokenLimit { get; set; }

        /// <summary>今日已使用 token</summary>
        public int UsedToday { get; set; }

        /// <summary>今日剩餘 token；null = 不限制</summary>
        public int? Remaining { get; set; }
    }

    /// <summary>後台存取碼列表項目（含今日用量）</summary>
    public class AccessCodeListItem
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Label { get; set; }
        public int DailyTokenLimit { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UsedTokensToday { get; set; }
        public int RequestsToday { get; set; }
    }

    /// <summary>存取碼服務：前台閘門判定 / 用量記錄 / 後台 CRUD</summary>
    public interface IAccessCodeService
    {
        /// <summary>公開聊天前置檢查：存取碼有效性 + 每日/全域配額</summary>
        Task<AccessGateResult> ValidateForChatAsync(string? code, CancellationToken cancellationToken);

        /// <summary>串流結束後記錄用量（per-code 與全域系統用量），best-effort</summary>
        Task RecordUsageAsync(Guid? accessCodeId, string modelType, int tokensIn, int tokensOut, CancellationToken cancellationToken);

        /// <summary>前台查詢存取碼狀態（剩餘額度）</summary>
        Task<AccessCodeStatus> GetStatusAsync(string? code, CancellationToken cancellationToken);

        // ── 後台 CRUD ──────────────────────────────────────────────────────
        Task<List<AccessCodeListItem>> ListAsync(CancellationToken cancellationToken);
        Task<AccessCodeListItem> CreateAsync(string? label, int? dailyTokenLimit, DateTime? expiresAt, CancellationToken cancellationToken);
        Task<AccessCodeListItem> UpdateAsync(Guid id, string? label, int dailyTokenLimit, DateTime? expiresAt, CancellationToken cancellationToken);
        Task<AccessCodeListItem> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    }
}
