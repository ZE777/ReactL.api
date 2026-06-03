namespace ReactL.api.Domain.Conversations
{
    /// <summary>訊息業務物件</summary>
    public class MessageDomain
    {
        /// <summary>訊息唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>所屬對話 ID</summary>
        public Guid ConversationId { get; set; }

        /// <summary>角色：user / assistant / system</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>訊息完整內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>輸入 Token 數</summary>
        public int TokensIn { get; set; }

        /// <summary>輸出 Token 數</summary>
        public int TokensOut { get; set; }

        /// <summary>訊息建立時間</summary>
        public DateTime CreatedAt { get; set; }
    }
}