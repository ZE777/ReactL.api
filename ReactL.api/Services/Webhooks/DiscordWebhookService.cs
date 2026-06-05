using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Constants;
using ReactL.api.Data;
using ReactL.api.DTOs.Requests.Webhooks;
using ReactL.api.Models.External;
using ReactL.api.Models.Stats;
using ReactL.api.Services.Ai;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>Discord Webhook 業務邏輯實作</summary>
    public class DiscordWebhookService : IDiscordWebhookService
    {
        private readonly AppDbContext _db;
        private readonly IAiService _ai;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DiscordWebhookService> _logger;

        private const string DiscordApiBase = "https://discord.com/api/v10";

        public DiscordWebhookService(
            AppDbContext db,
            IAiService ai,
            IHttpClientFactory httpClientFactory,
            ILogger<DiscordWebhookService> logger)
        {
            _db = db;
            _ai = ai;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task ProcessCommandAsync(Guid botId, DiscordInteractionPayload payload, CancellationToken cancellationToken)
        {
            // 1. 查詢 BotBinding（含 Persona，用於取得 System Prompt）
            var bot = await _db.BotBindings
                .Include(b => b.Persona)
                .Where(b => b.Id == botId)
                .FirstOrDefaultAsync(cancellationToken);

            if (bot is null)
            {
                _logger.LogWarning("Discord Webhook: Bot {BotId} 不存在", botId);
                return;
            }

            if (!bot.IsEnabled)
            {
                _logger.LogInformation("Discord Webhook: Bot {BotId} 已停用，略過", botId);
                return;
            }

            // 2. 從 payload 取出使用者資訊（Guild 用 member.user，DM 用 user）
            var discordUser = payload.Member?.User ?? payload.User;
            if (discordUser is null)
            {
                _logger.LogWarning("Discord Webhook: Bot {BotId} 無法取得使用者資訊", botId);
                return;
            }

            var discordUserId = discordUser.Id;
            var senderName    = discordUser.Username;
            var senderAvatar  = discordUser.Avatar is not null
                ? $"https://cdn.discordapp.com/avatars/{discordUserId}/{discordUser.Avatar}.png"
                : null;

            // 3. 從指令 options 取出 message 參數（/chat message:<text>）
            var userText = payload.Data?.Options
                ?.FirstOrDefault(o => o.Name == "message")
                ?.Value?.ToString();

            if (string.IsNullOrWhiteSpace(userText))
            {
                _logger.LogWarning("Discord Webhook: Bot {BotId} 指令缺少 message 參數", botId);
                await PatchFollowupAsync(payload.ApplicationId, payload.Token, "⚠️ 請提供訊息內容，使用方式：`/chat message:你的問題`");
                return;
            }

            // 4. 儲存使用者訊息至監控紀錄
            _db.ExternalMessages.Add(new ExternalMessage
            {
                BotBindingId      = botId,
                Platform          = Platform.Discord,
                ExternalUserId    = discordUserId,
                ExternalChannelId = payload.ChannelId,
                SenderName        = senderName,
                SenderAvatarUrl   = senderAvatar,
                Role              = MessageRole.User,
                Content           = userText
            });
            await _db.SaveChangesAsync(CancellationToken.None);

            // 5. 呼叫 AI（非串流，Webhook 不支援 SSE）
            string aiReply;
            int tokensIn = 0, tokensOut = 0;
            try
            {
                var prompt = string.IsNullOrWhiteSpace(bot.Persona?.SystemPrompt)
                    ? "你是一個友善的 AI 助理，請用繁體中文回答使用者的問題。"
                    : bot.Persona.SystemPrompt;

                // 以 Bot 擁有者的金鑰呼叫 AI（自帶 → 系統預設）
                (aiReply, tokensIn, tokensOut) = await _ai.CompleteWithUsageAsync(prompt, userText, bot.UserId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord Webhook: AI 呼叫失敗，BotBindingId={Id}", botId);
                aiReply = "抱歉，AI 服務暫時無法回應，請稍後再試。";
            }

            // 6. 儲存 AI 回應至監控紀錄
            _db.ExternalMessages.Add(new ExternalMessage
            {
                BotBindingId      = botId,
                Platform          = Platform.Discord,
                ExternalUserId    = discordUserId,
                ExternalChannelId = payload.ChannelId,
                Role              = MessageRole.Assistant,
                Content           = aiReply,
                TokensIn          = tokensIn,
                TokensOut         = tokensOut
            });
            await _db.SaveChangesAsync(CancellationToken.None);

            // 7. 更新每日 Token 統計（UPSERT，Source = 'discord'）
            if (tokensIn > 0 || tokensOut > 0)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var stat = await _db.TokenUsageStats.FirstOrDefaultAsync(
                    s => s.UserId == bot.UserId && s.Date == today && s.ModelType == bot.ModelType && s.Source == TokenSource.Discord,
                    CancellationToken.None);

                if (stat is null)
                {
                    _db.TokenUsageStats.Add(new TokenUsageStat
                    {
                        UserId       = bot.UserId,
                        Date         = today,
                        ModelType    = bot.ModelType,
                        Source       = TokenSource.Discord,
                        TokensIn     = tokensIn,
                        TokensOut    = tokensOut,
                        RequestCount = 1
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

            // 8. PATCH Discord deferred 訊息（將「思考中...」替換成 AI 回應）
            // Discord 單則訊息上限 2000 字元，超過則截斷並加提示
            var discordReply = aiReply.Length > 1950
                ? aiReply[..1950] + "\n*(訊息過長，已截斷)*"
                : aiReply;

            await PatchFollowupAsync(payload.ApplicationId, payload.Token, discordReply);
        }

        /// <summary>
        /// PATCH Discord deferred 訊息，以 AI 回應取代原始「思考中...」狀態
        /// interaction token 有效期為 15 分鐘，正常情況下 AI 回應遠在此時限內完成
        /// </summary>
        private async Task PatchFollowupAsync(string applicationId, string interactionToken, string content)
        {
            try
            {
                var client  = _httpClientFactory.CreateClient();
                var url     = $"{DiscordApiBase}/webhooks/{applicationId}/{interactionToken}/messages/@original";
                var payload = new { content };
                var body    = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.PatchAsync(url, body, CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    _logger.LogError("Discord followup PATCH 失敗 {Status}: {Error}", (int)response.StatusCode, error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord followup PATCH 例外");
            }
        }
    }
}