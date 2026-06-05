using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Constants;
using ReactL.api.Common.Helpers;
using ReactL.api.Data;
using ReactL.api.DTOs.Requests.Webhooks;
using ReactL.api.Models.External;
using ReactL.api.Models.Stats;
using ReactL.api.Services.Ai;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>LINE Webhook 業務邏輯實作</summary>
    public class LineWebhookService : ILineWebhookService
    {
        private readonly AppDbContext _db;
        private readonly AesEncryptionHelper _aes;
        private readonly IAiService _ai;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LineWebhookService> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public LineWebhookService(
            AppDbContext db,
            AesEncryptionHelper aes,
            IAiService ai,
            IHttpClientFactory httpClientFactory,
            ILogger<LineWebhookService> logger)
        {
            _db = db;
            _aes = aes;
            _ai = ai;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task HandleAsync(Guid botId, string signature, string rawBody, CancellationToken cancellationToken)
        {
            // 1. 查詢 BotBinding（含 Persona，用於取得 System Prompt）
            var bot = await _db.BotBindings
                .Include(b => b.Persona)
                .Where(b => b.Id == botId)
                .FirstOrDefaultAsync(cancellationToken);

            if (bot is null)
            {
                _logger.LogWarning("LINE Webhook: Bot {BotId} 不存在", botId);
                return;
            }

            if (!bot.IsEnabled)
            {
                _logger.LogInformation("LINE Webhook: Bot {BotId} 已停用，略過", botId);
                return;
            }

            // 2. 驗證 LINE HMAC-SHA256 簽名（防偽造請求）
            if (string.IsNullOrEmpty(bot.ChannelSecretEncrypted))
            {
                _logger.LogWarning("LINE Webhook: Bot {BotId} 缺少 ChannelSecret，無法驗簽", botId);
                return;
            }

            var channelSecret = _aes.Decrypt(bot.ChannelSecretEncrypted);
            if (!ValidateSignature(rawBody, signature, channelSecret))
            {
                _logger.LogWarning("LINE Webhook: Bot {BotId} 簽名驗證失敗（可能為偽造請求）", botId);
                return;
            }

            // 3. 解析 Payload
            LineWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<LineWebhookPayload>(rawBody, _jsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LINE Webhook: Bot {BotId} Payload 解析失敗", botId);
                return;
            }

            if (payload?.Events is not { Count: > 0 })
                return;

            var channelAccessToken = _aes.Decrypt(bot.BotTokenEncrypted);
            var systemPrompt = bot.Persona?.SystemPrompt;

            // 4. 依序處理文字訊息事件
            foreach (var ev in payload.Events)
            {
                if (ev.Type != "message" || ev.Message?.Type != "text")
                    continue;

                var userText = ev.Message.Text;
                var replyToken = ev.ReplyToken;
                var lineUserId = ev.Source?.UserId ?? "unknown";

                if (string.IsNullOrWhiteSpace(userText) || string.IsNullOrWhiteSpace(replyToken))
                    continue;

                await ProcessTextMessageAsync(
                    bot.Id, bot.UserId, bot.ModelType,
                    channelAccessToken, lineUserId, userText, replyToken,
                    systemPrompt, cancellationToken);
            }
        }

        // ── 私有方法 ─────────────────────────────────────────────────────────

        private async Task ProcessTextMessageAsync(
            Guid botBindingId,
            Guid botUserId,
            string modelType,
            string channelAccessToken,
            string lineUserId,
            string userText,
            string replyToken,
            string? systemPrompt,
            CancellationToken cancellationToken)
        {
            // 從 LINE Profile API 取得使用者暱稱與頭像（失敗不中斷流程）
            var (senderName, senderAvatarUrl) = await FetchLineProfileAsync(channelAccessToken, lineUserId);

            // 儲存使用者訊息至監控紀錄（含暱稱與頭像）
            _db.ExternalMessages.Add(new ExternalMessage
            {
                BotBindingId = botBindingId,
                Platform = Platform.Line,
                ExternalUserId = lineUserId,
                SenderName = senderName,
                SenderAvatarUrl = senderAvatarUrl,
                Role = MessageRole.User,
                Content = userText
            });
            await _db.SaveChangesAsync(CancellationToken.None);

            // 呼叫 AI（非串流，Webhook 不支援 SSE），同時取得 Token 用量
            string aiReply;
            int tokensIn = 0, tokensOut = 0;
            try
            {
                // 若 Bot 未設定 Persona，使用預設提示語
                var prompt = string.IsNullOrWhiteSpace(systemPrompt)
                    ? "你是一個友善的 AI 助理，請用繁體中文回答使用者的問題。"
                    : systemPrompt;

                // LINE 關閉 HTTP 連線後 cancellationToken 會被觸發，改用 None 讓 AI 呼叫持續到完成
                // 以 Bot 擁有者的金鑰呼叫 AI（自帶 → 系統預設）
                (aiReply, tokensIn, tokensOut) = await _ai.CompleteWithUsageAsync(prompt, userText, botUserId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LINE Webhook: AI 呼叫失敗，BotBindingId={Id}", botBindingId);
                aiReply = "抱歉，AI 服務暫時無法回應，請稍後再試。";
            }

            // 儲存 AI 回應至監控紀錄（含 Token 用量）
            _db.ExternalMessages.Add(new ExternalMessage
            {
                BotBindingId = botBindingId,
                Platform = Platform.Line,
                ExternalUserId = lineUserId,
                Role = MessageRole.Assistant,
                Content = aiReply,
                TokensIn = tokensIn,
                TokensOut = tokensOut
            });
            await _db.SaveChangesAsync(CancellationToken.None);

            // 更新每日 Token 統計（UPSERT，Source = 'line'）
            if (tokensIn > 0 || tokensOut > 0)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var stat = await _db.TokenUsageStats.FirstOrDefaultAsync(
                    s => s.UserId == botUserId && s.Date == today && s.ModelType == modelType && s.Source == TokenSource.Line,
                    CancellationToken.None);

                if (stat is null)
                {
                    _db.TokenUsageStats.Add(new TokenUsageStat
                    {
                        UserId       = botUserId,
                        Date         = today,
                        ModelType    = modelType,
                        Source       = TokenSource.Line,
                        TokensIn     = tokensIn,
                        TokensOut    = tokensOut,
                        RequestCount = 1,
                    });
                }
                else
                {
                    stat.TokensIn     += tokensIn;
                    stat.TokensOut    += tokensOut;
                    stat.RequestCount += 1;
                }
                await _db.SaveChangesAsync(CancellationToken.None);
            }

            // 透過 LINE Reply API 回傳訊息（replyToken 30 秒後失效，同樣不依賴 HTTP 請求的 CT）
            await ReplyToLineAsync(channelAccessToken, replyToken, aiReply);
        }

        private async Task ReplyToLineAsync(
            string channelAccessToken,
            string replyToken,
            string message)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", channelAccessToken);

                var body = new
                {
                    replyToken,
                    messages = new[] { new { type = "text", text = message } }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync(
                    "https://api.line.me/v2/bot/message/reply",
                    content,
                    CancellationToken.None);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    _logger.LogError("LINE Reply API 回傳 {Status}: {Error}", (int)response.StatusCode, error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LINE Reply API 呼叫例外");
            }
        }

        /// <summary>
        /// 呼叫 LINE Profile API 取得使用者暱稱與頭像 URL
        /// 失敗時回傳 (null, null)，不中斷訊息處理流程
        /// </summary>
        private async Task<(string? DisplayName, string? PictureUrl)> FetchLineProfileAsync(
            string channelAccessToken, string lineUserId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", channelAccessToken);

                var response = await client.GetAsync(
                    $"https://api.line.me/v2/bot/profile/{lineUserId}",
                    CancellationToken.None);

                if (!response.IsSuccessStatusCode)
                    return (null, null);

                var json = await response.Content.ReadAsStringAsync(CancellationToken.None);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var displayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                var pictureUrl  = root.TryGetProperty("pictureUrl",  out var pu) ? pu.GetString() : null;

                return (displayName, pictureUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LINE Profile API 呼叫失敗，LineUserId={UserId}", lineUserId);
                return (null, null);
            }
        }

        /// <summary>
        /// 驗證 LINE 簽名：HMAC-SHA256(rawBody, ChannelSecret) → Base64 = X-Line-Signature
        /// </summary>
        private static bool ValidateSignature(string rawBody, string signature, string channelSecret)
        {
            if (string.IsNullOrEmpty(signature)) return false;

            var key = Encoding.UTF8.GetBytes(channelSecret);
            var body = Encoding.UTF8.GetBytes(rawBody);

            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(body);
            var expected = Convert.ToBase64String(hash);

            return string.Equals(expected, signature, StringComparison.Ordinal);
        }
    }
}