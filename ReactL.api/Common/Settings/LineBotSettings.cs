namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// LINE Bot 串接設定
    /// 對應 appsettings.json 的 "LineBotSettings" section
    /// 敏感欄位（ChannelSecret、ChannelAccessToken）必須透過 User Secrets 或環境變數設定，禁止寫入版控
    /// </summary>
    public class LineBotSettings
    {
        /// <summary>LINE Channel ID，在 LINE Developers Console 建立 Messaging API Channel 後取得</summary>
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>
        /// Webhook URL，LINE 平台收到訊息後會 POST 到此網址
        /// 必須是 HTTPS，且在 LINE Developers Console 中設定
        /// 格式：https://{AppSettings.BaseUrl}/api/v1/linebot/webhook
        /// </summary>
        public string WebhookUrl { get; set; } = string.Empty;

        /// <summary>
        /// Channel Secret，用於驗證 Webhook 請求確實來自 LINE 平台（簽名驗證）
        /// 設定方式：dotnet user-secrets set "LineBotSettings:ChannelSecret" "your-secret"
        /// </summary>
        public string ChannelSecret { get; set; } = string.Empty;

        /// <summary>
        /// Channel Access Token，用於呼叫 LINE Messaging API 回傳訊息給使用者
        /// 設定方式：dotnet user-secrets set "LineBotSettings:ChannelAccessToken" "your-token"
        /// </summary>
        public string ChannelAccessToken { get; set; } = string.Empty;
    }
}
