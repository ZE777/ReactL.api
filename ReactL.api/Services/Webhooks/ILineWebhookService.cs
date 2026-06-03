namespace ReactL.api.Services.Webhooks
{
    /// <summary>LINE Webhook 業務邏輯介面</summary>
    public interface ILineWebhookService
    {
        /// <summary>
        /// 驗證簽名並處理 LINE 送來的事件，最終透過 LINE Reply API 回覆 AI 訊息
        /// </summary>
        /// <param name="botId">BotBinding 主鍵，從 URL 路由取得</param>
        /// <param name="signature">X-Line-Signature header 值（HMAC-SHA256 Base64）</param>
        /// <param name="rawBody">原始 request body 字串（簽名驗證必須用原始 bytes）</param>
        Task HandleAsync(Guid botId, string signature, string rawBody, CancellationToken cancellationToken);
    }
}