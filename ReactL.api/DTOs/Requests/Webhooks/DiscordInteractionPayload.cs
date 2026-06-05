using System.Text.Json.Serialization;

namespace ReactL.api.DTOs.Requests.Webhooks
{
    /// <summary>Discord Interactions API 請求根物件</summary>
    public class DiscordInteractionPayload
    {
        /// <summary>互動類型：1=PING, 2=APPLICATION_COMMAND</summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>互動 Token，用於後續 followup API 呼叫（有效期 15 分鐘）</summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("application_id")]
        public string ApplicationId { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public DiscordInteractionData? Data { get; set; }

        /// <summary>伺服器成員資訊（來自 Guild 的請求）</summary>
        [JsonPropertyName("member")]
        public DiscordMember? Member { get; set; }

        /// <summary>使用者資訊（DM 的請求，Guild 時此欄為 null）</summary>
        [JsonPropertyName("user")]
        public DiscordUser? User { get; set; }

        [JsonPropertyName("channel_id")]
        public string? ChannelId { get; set; }

        [JsonPropertyName("guild_id")]
        public string? GuildId { get; set; }
    }

    /// <summary>APPLICATION_COMMAND 指令資料</summary>
    public class DiscordInteractionData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("options")]
        public List<DiscordCommandOption>? Options { get; set; }
    }

    /// <summary>
    /// slash command 的參數值
    /// Value 使用 object? 讓 System.Text.Json 保留為 JsonElement，
    /// 呼叫端再依需求轉型（.ToString() 對字串型參數可直接取得無引號文字）
    /// </summary>
    public class DiscordCommandOption
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public object? Value { get; set; }
    }

    /// <summary>Discord 伺服器成員（含巢狀使用者資訊）</summary>
    public class DiscordMember
    {
        [JsonPropertyName("user")]
        public DiscordUser? User { get; set; }
    }

    /// <summary>Discord 使用者</summary>
    public class DiscordUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>頭像 hash，完整 URL 為 https://cdn.discordapp.com/avatars/{id}/{avatar}.png</summary>
        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
    }
}