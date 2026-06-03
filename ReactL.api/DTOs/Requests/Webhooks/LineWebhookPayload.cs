using System.Text.Json.Serialization;

namespace ReactL.api.DTOs.Requests.Webhooks
{
    /// <summary>LINE Webhook 根物件</summary>
    public class LineWebhookPayload
    {
        [JsonPropertyName("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonPropertyName("events")]
        public List<LineEvent> Events { get; set; } = [];
    }

    /// <summary>LINE 事件（message / follow / unfollow / postback …）</summary>
    public class LineEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>用於回覆訊息的一次性 Token，30 秒後失效</summary>
        [JsonPropertyName("replyToken")]
        public string? ReplyToken { get; set; }

        [JsonPropertyName("source")]
        public LineSource? Source { get; set; }

        [JsonPropertyName("message")]
        public LineMessage? Message { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;
    }

    /// <summary>LINE 訊息來源（user / group / room）</summary>
    public class LineSource
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("groupId")]
        public string? GroupId { get; set; }
    }

    /// <summary>LINE 訊息內容</summary>
    public class LineMessage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}