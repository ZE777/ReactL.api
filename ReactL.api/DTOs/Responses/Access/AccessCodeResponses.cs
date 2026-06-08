namespace ReactL.api.DTOs.Responses.Access
{
    /// <summary>後台存取碼回應（含今日用量）</summary>
    public class AccessCodeResponse
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

    /// <summary>前台存取碼狀態回應（剩餘額度）</summary>
    public class PublicAccessStatusResponse
    {
        /// <summary>本站是否要求存取碼</summary>
        public bool RequireAccessCode { get; set; }

        /// <summary>提供的碼是否有效</summary>
        public bool Valid { get; set; }

        public string? Label { get; set; }
        public int DailyTokenLimit { get; set; }
        public int UsedToday { get; set; }

        /// <summary>今日剩餘 token；null = 不限制</summary>
        public int? Remaining { get; set; }
    }
}
