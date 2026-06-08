using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSec.Cryptography;
using ReactL.api.Data;
using ReactL.api.DTOs.Requests.Webhooks;
using ReactL.api.Services.Webhooks;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Controllers.Webhooks
{
    /// <summary>Discord Interactions Endpoint（不需要 JWT 驗證）</summary>
    [ApiController]
    [Route("webhooks")]
    public class DiscordWebhookController : ControllerBase
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DiscordWebhookController> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

        public DiscordWebhookController(
            IServiceScopeFactory scopeFactory,
            ILogger<DiscordWebhookController> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        /// <summary>
        /// Discord Interactions Endpoint，路徑對應後台 Bot 卡片顯示的 /webhooks/discord/{botId}
        /// Discord 設定 Interactions Endpoint URL 時，先以 PING (type:1) 驗證端點有效性；
        /// APPLICATION_COMMAND (type:2) 採 deferred 回應立即回傳，AI 在背景處理後 PATCH 原始訊息
        /// </summary>
        [HttpPost("discord/{botId:guid}")]
        public async Task<IActionResult> DiscordWebhook(Guid botId, CancellationToken cancellationToken)
        {
            // 讀取原始 body（Ed25519 驗簽必須基於原始 bytes，不可讓框架先解析）
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody   = await reader.ReadToEndAsync(cancellationToken);
            var signature = Request.Headers["X-Signature-Ed25519"].ToString();
            var timestamp = Request.Headers["X-Signature-Timestamp"].ToString();

            _logger.LogInformation("Discord Webhook 收到請求 BotId={BotId}", botId);

            // 從 DB 取出此 Bot 的 DiscordPublicKey（每筆 Bot 獨立儲存）
            string? publicKeyHex;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                publicKeyHex = await db.BotBindings
                    .AsNoTracking()
                    .Where(b => b.Id == botId)
                    .Select(b => b.DiscordPublicKey)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            // Ed25519 簽名驗證（偽造請求一律回傳 401）
            if (!VerifySignature(rawBody, signature, timestamp, publicKeyHex, botId))
            {
                _logger.LogWarning("Discord Webhook: BotId={BotId} 簽名驗證失敗（可能為偽造請求）", botId);
                return Unauthorized("Invalid request signature");
            }

            DiscordInteractionPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<DiscordInteractionPayload>(rawBody, _jsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord Webhook: Payload 解析失敗，BotId={BotId}", botId);
                return BadRequest();
            }

            if (payload is null)
                return BadRequest();

            // PING 驗證：Discord 設定 Interactions Endpoint URL 時的握手請求，必須回傳 PONG
            if (payload.Type == 1)
            {
                _logger.LogInformation("Discord Webhook PING，BotId={BotId}", botId);
                return Ok(new { type = 1 });
            }

            // APPLICATION_COMMAND：立即回傳 deferred（type:5），AI 在背景完成後 PATCH 訊息
            if (payload.Type == 2)
            {
                var capturedPayload = payload;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var service     = scope.ServiceProvider.GetRequiredService<IDiscordWebhookService>();
                        _logger.LogInformation("Discord Webhook 背景任務啟動，BotId={BotId}", botId);
                        await service.ProcessCommandAsync(botId, capturedPayload, CancellationToken.None);
                        _logger.LogInformation("Discord Webhook 背景任務完成，BotId={BotId}", botId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Discord Webhook 背景處理例外，BotId={BotId}", botId);
                    }
                });

                // type:5 = DEFERRED_CHANNEL_MESSAGE_WITH_SOURCE，Discord 顯示「思考中...」等待後續 PATCH
                return Ok(new { type = 5 });
            }

            // MESSAGE_COMPONENT（type:3，二次確認按鈕）：先回 type 6 ACK，背景執行後 PATCH 原訊息
            if (payload.Type == 3)
            {
                var capturedPayload = payload;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var service     = scope.ServiceProvider.GetRequiredService<IDiscordWebhookService>();
                        await service.ProcessComponentAsync(botId, capturedPayload, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Discord Webhook 元件互動背景處理例外，BotId={BotId}", botId);
                    }
                });

                // type:6 = DEFERRED_UPDATE_MESSAGE，先 ACK 並保留原訊息，稍後 PATCH 為結果
                return Ok(new { type = 6 });
            }

            _logger.LogInformation("Discord Webhook: 不支援的 type={Type}，BotId={BotId}", payload.Type, botId);
            return BadRequest();
        }

        /// <summary>
        /// 驗證 Discord Ed25519 簽名
        /// 驗證訊息 = UTF8(timestamp + rawBody)；公鑰從 BotBinding.DiscordPublicKey 取得（每筆 Bot 獨立）
        /// PublicKey 未設定時僅記錄警告並放行（僅限開發階段，上線前務必在後台填入）
        /// </summary>
        private bool VerifySignature(string rawBody, string signature, string timestamp, string? publicKeyHex, Guid botId)
        {
            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp))
                return false;

            if (string.IsNullOrEmpty(publicKeyHex))
            {
                _logger.LogWarning("BotId={BotId} 的 DiscordPublicKey 未設定，跳過 Ed25519 驗簽（僅限本機開發，上線前務必在後台填入）", botId);
                return true;
            }

            try
            {
                var publicKeyBytes = Convert.FromHexString(publicKeyHex);
                var signatureBytes = Convert.FromHexString(signature);
                var messageBytes   = Encoding.UTF8.GetBytes(timestamp + rawBody);

                var algorithm = SignatureAlgorithm.Ed25519;
                var publicKey = PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
                return algorithm.Verify(publicKey, messageBytes, signatureBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord Ed25519 簽名驗證例外，BotId={BotId}", botId);
                return false;
            }
        }
    }
}