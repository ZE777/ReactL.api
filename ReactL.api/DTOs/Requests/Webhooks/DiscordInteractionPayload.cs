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

        /// <summary>元件互動（type 3，按鈕）的 custom_id，用於識別按下的是哪個確認按鈕</summary>
        [JsonPropertyName("custom_id")]
        public string? CustomId { get; set; }

        /// <summary>
        /// 指令參數中被提及（@user / #channel / @role）的實體，Discord 已解析好 ID 對應物件。
        /// function calling 需以此驗證/取得目標真實 ID，避免用名稱猜。
        /// </summary>
        [JsonPropertyName("resolved")]
        public DiscordResolved? Resolved { get; set; }
    }

    /// <summary>interaction 解析後的提及實體集合（key = 該實體的 Discord ID）</summary>
    public class DiscordResolved
    {
        [JsonPropertyName("users")]
        public Dictionary<string, DiscordUser>? Users { get; set; }

        [JsonPropertyName("channels")]
        public Dictionary<string, DiscordResolvedChannel>? Channels { get; set; }

        [JsonPropertyName("roles")]
        public Dictionary<string, DiscordResolvedRole>? Roles { get; set; }
    }

    /// <summary>被提及的頻道（含型別，用於驗證如「必須是語音頻道」）</summary>
    public class DiscordResolvedChannel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>頻道型別：0=文字, 2=語音, 4=分類… 參見 Discord ChannelType</summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }
    }

    /// <summary>被提及的身分組</summary>
    public class DiscordResolvedRole
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>身分組權限位元（字串），用於排除特權身分組</summary>
        [JsonPropertyName("permissions")]
        public string? Permissions { get; set; }
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

        /// <summary>
        /// 下指令者在當前頻道的「已計算權限」位元（字串）。
        /// 用於檢查呼叫者本身是否具備執行該管理動作的權限（如禁言需 MODERATE_MEMBERS）。
        /// </summary>
        [JsonPropertyName("permissions")]
        public string? Permissions { get; set; }
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