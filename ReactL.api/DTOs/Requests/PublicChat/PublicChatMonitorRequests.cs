namespace ReactL.api.DTOs.Requests.PublicChat
{
    /// <summary>前台聊天監控：對話列表查詢參數（以 SessionId 分組）</summary>
    public class PublicChatConversationQueryParams
    {
        /// <summary>關鍵字篩選：比對存取碼字串或標籤</summary>
        public string? Search { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 30;
    }

    /// <summary>前台聊天監控：訊息列表查詢參數</summary>
    public class PublicChatMessageQueryParams
    {
        /// <summary>對話工作階段識別碼（必填，指定要查看的對話）</summary>
        public string? SessionId { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}