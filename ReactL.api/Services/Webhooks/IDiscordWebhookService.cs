using ReactL.api.DTOs.Requests.Webhooks;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>Discord Webhook 業務邏輯介面</summary>
    public interface IDiscordWebhookService
    {
        /// <summary>
        /// 處理 Discord APPLICATION_COMMAND 互動：
        /// 儲存訊息 → 呼叫 AI → 儲存 AI 回應 → 更新 Token 統計 → PATCH Discord deferred 訊息
        /// </summary>
        /// <param name="botId">BotBinding 主鍵，從 URL 路由取得</param>
        /// <param name="payload">Discord Interactions API 解析後的請求物件</param>
        Task ProcessCommandAsync(Guid botId, DiscordInteractionPayload payload, CancellationToken cancellationToken);
    }
}