namespace ReactL.api.Domain.Monitor
{
    /// <summary>外部平台訊息業務物件（含 BotName JOIN 欄位）</summary>
    public class ExternalMessageDomain
    {
        /// <summary>訊息唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>所屬 Bot 綁定 ID</summary>
        public Guid BotBindingId { get; set; }

        /// <summary>所屬 Bot 名稱（JOIN 取得）</summary>
        public string BotName { get; set; } = string.Empty;

        /// <summary>訊息來源平台：line / discord</summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>外部平台的使用者識別碼</summary>
        public string ExternalUserId { get; set; } = string.Empty;

        /// <summary>外部平台的頻道識別碼（群組訊息時有值）</summary>
        public string? ExternalChannelId { get; set; }

        /// <summary>訊息角色：user / assistant</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>訊息完整內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>截斷後的預覽（最多 100 字元）</summary>
        public string ContentPreview { get; set; } = string.Empty;

        /// <summary>輸入 Token 數</summary>
        public int TokensIn { get; set; }

        /// <summary>輸出 Token 數</summary>
        public int TokensOut { get; set; }

        /// <summary>訊息建立時間</summary>
        public DateTime CreatedAt { get; set; }
    }
}
