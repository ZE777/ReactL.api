using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Constants;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Helpers;
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
        private readonly AesEncryptionHelper _aes;
        private readonly IDiscordToolService _tools;

        private const string DiscordApiBase = "https://discord.com/api/v10";

        public DiscordWebhookService(
            AppDbContext db,
            IAiService ai,
            IHttpClientFactory httpClientFactory,
            ILogger<DiscordWebhookService> logger,
            AesEncryptionHelper aes,
            IDiscordToolService tools)
        {
            _db = db;
            _ai = ai;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _aes = aes;
            _tools = tools;
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

            // 5. 呼叫 AI（function calling：模型可回工具呼叫或純文字）
            string aiReply;
            object? replyComponents = null;   // 需二次確認時附帶的按鈕元件
            int tokensIn = 0, tokensOut = 0;
            try
            {
                var prompt = BuildSystemPrompt(bot.Persona?.SystemPrompt);

                // 以 Bot 設定的模型 + 擁有者金鑰呼叫；模型須支援 tool calling
                var toolResult = await _ai.CompleteWithToolsAsync(
                    prompt, userText, _tools.GetToolDefinitions(), bot.ModelType, bot.UserId, CancellationToken.None);
                tokensIn = toolResult.TokensIn;
                tokensOut = toolResult.TokensOut;

                if (toolResult.HasToolCalls)
                {
                    // AI 決定執行管理動作 → 驗證並執行（AI 碰不到 Discord API，由後端執行）
                    var ctx = new DiscordToolContext
                    {
                        BotToken = _aes.Decrypt(bot.BotTokenEncrypted),
                        GuildId = payload.GuildId,
                        ChannelId = payload.ChannelId,
                        InvokerPermissions = ParsePermissions(payload.Member?.Permissions),
                        Resolved = payload.Data?.Resolved
                    };
                    var exec = await _tools.ExecuteAsync(toolResult.ToolCalls, ctx, CancellationToken.None);
                    aiReply = exec.Text;
                    replyComponents = exec.Components;   // 非 null 代表需二次確認，附上按鈕
                }
                else
                {
                    aiReply = string.IsNullOrWhiteSpace(toolResult.TextReply)
                        ? "（沒有可回覆的內容）"
                        : toolResult.TextReply;
                }
            }
            catch (UpstreamAiException ex)
            {
                // 上游 AI 錯誤（429 額度上限 / 400 格式錯誤等）已帶友善訊息，直接顯示讓使用者知道原因
                _logger.LogWarning(ex, "Discord Webhook: 上游 AI 錯誤，BotBindingId={Id}", botId);
                aiReply = $"⚠️ {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord Webhook: AI/工具執行失敗，BotBindingId={Id}", botId);
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

            await PatchFollowupAsync(payload.ApplicationId, payload.Token, discordReply, replyComponents);
        }

        /// <summary>組裝 system prompt：Persona 設定 + 管理助理工具使用說明</summary>
        private static string BuildSystemPrompt(string? personaPrompt)
        {
            var basePrompt = string.IsNullOrWhiteSpace(personaPrompt)
                ? "你是一個友善的 AI 助理，請用繁體中文回答使用者的問題。"
                : personaPrompt;

            return basePrompt
                + "\n\n你同時是這個 Discord 伺服器的管理助理。當使用者要求執行管理動作（例如禁言、解除禁言）時，"
                + "呼叫對應的工具；target 參數請原樣使用使用者訊息中的 <@數字> 提及格式。"
                + "若使用者只是聊天或詢問一般問題，就正常用文字回覆，不要呼叫工具。";
        }

        /// <summary>解析下指令者的權限位元字串（Discord 以十進位字串表示 64-bit 位元）</summary>
        private static ulong ParsePermissions(string? permissions) =>
            ulong.TryParse(permissions, out var value) ? value : 0;

        /// <summary>
        /// PATCH Discord deferred 訊息，以 AI 回應取代原始「思考中...」狀態。
        /// components 非 null 時附帶按鈕（二次確認）；傳空陣列可清除既有按鈕。
        /// interaction token 有效期為 15 分鐘，正常情況下遠在此時限內完成。
        /// </summary>
        private async Task PatchFollowupAsync(string applicationId, string interactionToken, string content, object? components = null)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{DiscordApiBase}/webhooks/{applicationId}/{interactionToken}/messages/@original";
                // components 非 null 才帶入欄位（避免覆蓋；傳 [] 則清除按鈕）
                object payload = components is null ? new { content } : new { content, components };
                var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

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

        /// <summary>
        /// 處理二次確認按鈕（MESSAGE_COMPONENT）：依 custom_id 執行或取消動作，並更新原訊息（移除按鈕）。
        /// 控制器已先回應 type 6（DEFERRED_UPDATE_MESSAGE），此處於背景完成執行並 PATCH 原訊息。
        /// </summary>
        public async Task ProcessComponentAsync(Guid botId, DiscordInteractionPayload payload, CancellationToken cancellationToken)
        {
            var bot = await _db.BotBindings.Where(b => b.Id == botId).FirstOrDefaultAsync(cancellationToken);
            if (bot is null || !bot.IsEnabled) return;

            var customId = payload.Data?.CustomId ?? string.Empty;
            var ctx = new DiscordToolContext
            {
                BotToken = _aes.Decrypt(bot.BotTokenEncrypted),
                GuildId = payload.GuildId,
                ChannelId = payload.ChannelId,
                InvokerPermissions = ParsePermissions(payload.Member?.Permissions)
            };

            string resultText;
            try
            {
                resultText = await _tools.ExecuteConfirmedAsync(customId, ctx, CancellationToken.None)
                             ?? "（無效的確認）";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord 確認動作執行失敗，BotBindingId={Id} CustomId={CustomId}", botId, customId);
                resultText = "❌ 執行失敗，請稍後再試。";
            }

            // 更新原訊息為結果，並以空 components 清除按鈕（避免重複點擊）
            await PatchFollowupAsync(payload.ApplicationId, payload.Token, resultText, Array.Empty<object>());
        }
    }
}