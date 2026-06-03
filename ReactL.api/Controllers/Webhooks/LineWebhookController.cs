using Microsoft.AspNetCore.Mvc;
using ReactL.api.Services.Webhooks;
using System.Text;

namespace ReactL.api.Controllers.Webhooks
{
    /// <summary>LINE Messaging API Webhook 接收端點（不需要 JWT 驗證）</summary>
    [ApiController]
    [Route("webhooks")]
    public class LineWebhookController : ControllerBase
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LineWebhookController> _logger;

        public LineWebhookController(
            IServiceScopeFactory scopeFactory,
            ILogger<LineWebhookController> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// LINE Webhook 入口，對應後台 Bot 卡片顯示的路徑 /webhooks/line/{botId}
        /// LINE 平台要求 1 秒內回傳 200，因此讀取簽名後立即回傳，
        /// AI 呼叫與 Reply 在獨立的 DI Scope 背景執行，避免 IIS IOCP 因連線關閉中止 socket 操作。
        /// </summary>
        [HttpPost("line/{botId:guid}")]
        public async Task<IActionResult> LineWebhook(Guid botId, CancellationToken cancellationToken)
        {
            // 讀取原始 body 字串（HMAC-SHA256 簽名驗證必須基於原始 bytes，不可讓框架先解析）
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);

            var signature = Request.Headers["X-Line-Signature"].ToString();

            _logger.LogInformation("LINE Webhook 收到請求 BotId={BotId}", botId);

            // 在獨立 Scope 背景處理，確保 LINE 連線關閉後仍能完成 AI 呼叫與回覆
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ILineWebhookService>();
                    _logger.LogInformation("LINE Webhook 背景任務啟動，BotId={BotId}", botId);
                    await service.HandleAsync(botId, signature, rawBody, CancellationToken.None);
                    _logger.LogInformation("LINE Webhook 背景任務完成，BotId={BotId}", botId);
                }
                catch (Exception ex)
                {
                    // 即使內部處理失敗，200 已回傳，LINE 不會重複 Retry
                    _logger.LogError(ex, "LINE Webhook 背景處理發生例外，BotId={BotId}", botId);
                }
            });

            // 立即回傳 200，不等待 AI 完成，避免 LINE 1 秒逾時後斷線
            return Ok();
        }
    }
}