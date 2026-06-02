namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// Discord Bot 串接設定
    /// 對應 appsettings.json 的 "DiscordSettings" section
    /// 敏感欄位 BotToken 必須透過 User Secrets 或環境變數設定，禁止寫入版控
    /// </summary>
    public class DiscordSettings
    {
        /// <summary>Discord 應用程式 ID，在 Discord Developer Portal 建立 App 時取得</summary>
        public string ApplicationId { get; set; } = string.Empty;

        /// <summary>
        /// 指定的伺服器（Guild）ID
        /// 開發測試時指向測試伺服器；生產環境改為正式伺服器 ID
        /// </summary>
        public string GuildId { get; set; } = string.Empty;

        /// <summary>
        /// Discord Webhook URL，用於從後端主動推送訊息到指定頻道
        /// 在 Discord 頻道設定中建立 Webhook 後取得
        /// </summary>
        public string WebhookUrl { get; set; } = string.Empty;

        /// <summary>
        /// Discord Bot Token，用於 Bot 身份驗證與 API 呼叫
        /// 設定方式：dotnet user-secrets set "DiscordSettings:BotToken" "your-token"
        /// </summary>
        public string BotToken { get; set; } = string.Empty;
    }
}
