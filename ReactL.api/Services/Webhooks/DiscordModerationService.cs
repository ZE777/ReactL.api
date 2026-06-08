using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>Discord 伺服器管理動作實作（手刻 HTTP，沿用 DiscordWebhookService 的範式）</summary>
    public class DiscordModerationService : IDiscordModerationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DiscordModerationService> _logger;

        private const string DiscordApiBase = "https://discord.com/api/v10";

        public DiscordModerationService(IHttpClientFactory httpClientFactory, ILogger<DiscordModerationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<ModerationResult> TimeoutMemberAsync(
            string botToken, string guildId, string userId, int seconds, string? reason, CancellationToken cancellationToken = default)
        {
            // Discord 以 communication_disabled_until（ISO8601）表示禁言到期時間
            var until = DateTimeOffset.UtcNow.AddSeconds(seconds).ToString("o");
            return PatchMemberAsync(botToken, guildId, userId, new { communication_disabled_until = until }, reason, "禁言", cancellationToken);
        }

        /// <inheritdoc />
        public Task<ModerationResult> RemoveTimeoutAsync(
            string botToken, string guildId, string userId, string? reason, CancellationToken cancellationToken = default)
        {
            // null 即解除禁言
            return PatchMemberAsync(botToken, guildId, userId, new { communication_disabled_until = (string?)null }, reason, "解除禁言", cancellationToken);
        }

        /// <inheritdoc />
        public Task<ModerationResult> MoveMemberAsync(string botToken, string guildId, string userId, string channelId, string? reason, CancellationToken cancellationToken = default)
            => PatchMemberAsync(botToken, guildId, userId, new { channel_id = channelId }, reason, "移動語音", cancellationToken);

        /// <inheritdoc />
        public Task<ModerationResult> DisconnectVoiceAsync(string botToken, string guildId, string userId, string? reason, CancellationToken cancellationToken = default)
            => PatchMemberAsync(botToken, guildId, userId, new { channel_id = (string?)null }, reason, "中斷語音", cancellationToken);

        /// <inheritdoc />
        public Task<ModerationResult> SetVoiceMuteAsync(string botToken, string guildId, string userId, bool mute, string? reason, CancellationToken cancellationToken = default)
            => PatchMemberAsync(botToken, guildId, userId, new { mute }, reason, mute ? "語音禁麥" : "解除禁麥", cancellationToken);

        /// <inheritdoc />
        public Task<ModerationResult> SetVoiceDeafAsync(string botToken, string guildId, string userId, bool deaf, string? reason, CancellationToken cancellationToken = default)
            => PatchMemberAsync(botToken, guildId, userId, new { deaf }, reason, deaf ? "語音禁聽" : "解除禁聽", cancellationToken);

        /// <inheritdoc />
        public Task<ModerationResult> SetNicknameAsync(string botToken, string guildId, string userId, string? nickname, string? reason, CancellationToken cancellationToken = default)
            => PatchMemberAsync(botToken, guildId, userId, new { nick = nickname }, reason, "變更暱稱", cancellationToken);

        /// <inheritdoc />
        public async Task<ModerationResult> UnbanMemberAsync(string botToken, string guildId, string userId, string? reason, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{DiscordApiBase}/guilds/{guildId}/bans/{userId}";

                using var req = new HttpRequestMessage(HttpMethod.Delete, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());
                if (!string.IsNullOrWhiteSpace(reason))
                    req.Headers.TryAddWithoutValidation("X-Audit-Log-Reason", Uri.EscapeDataString(reason));

                using var resp = await client.SendAsync(req, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Discord 解除封鎖成功 Guild={GuildId} User={UserId}", guildId, userId);
                    return new ModerationResult(true, null);
                }

                var error = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Discord 解除封鎖失敗 {Status} Guild={GuildId} User={UserId}: {Error}",
                    (int)resp.StatusCode, guildId, userId, error);
                // 404 對解除封鎖代表「該使用者本來就沒被封鎖」
                return new ModerationResult(false,
                    resp.StatusCode == HttpStatusCode.NotFound ? "該使用者目前並未被封鎖" : ToFriendlyReason(resp.StatusCode, "解除封鎖"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord 解除封鎖例外 Guild={GuildId} User={UserId}", guildId, userId);
                return new ModerationResult(false, "無法連線到 Discord，請稍後再試");
            }
        }

        /// <inheritdoc />
        public async Task<ModerationResult> SendMessageAsync(string botToken, string channelId, string content, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{DiscordApiBase}/channels/{channelId}/messages";

                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { content }), Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());

                using var resp = await client.SendAsync(req, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Discord 發送訊息成功 Channel={ChannelId}", channelId);
                    return new ModerationResult(true, null);
                }

                var error = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Discord 發送訊息失敗 {Status} Channel={ChannelId}: {Error}", (int)resp.StatusCode, channelId, error);
                return new ModerationResult(false, ToFriendlyReason(resp.StatusCode, "發送訊息"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord 發送訊息例外 Channel={ChannelId}", channelId);
                return new ModerationResult(false, "無法連線到 Discord，請稍後再試");
            }
        }

        /// <inheritdoc />
        public Task<ModerationResult> KickMemberAsync(string botToken, string guildId, string userId, string? reason, CancellationToken cancellationToken = default)
            => SendSimpleAsync(botToken, HttpMethod.Delete, $"/guilds/{guildId}/members/{userId}", null, reason, "踢出成員", cancellationToken);

        /// <inheritdoc />
        public Task<ModerationResult> BanMemberAsync(string botToken, string guildId, string userId, int deleteMessageDays, string? reason, CancellationToken cancellationToken = default)
        {
            var seconds = Math.Clamp(deleteMessageDays, 0, 7) * 86400;
            return SendSimpleAsync(botToken, HttpMethod.Put, $"/guilds/{guildId}/bans/{userId}", new { delete_message_seconds = seconds }, reason, "封鎖成員", cancellationToken);
        }

        /// <inheritdoc />
        public Task<ModerationResult> AddRoleAsync(string botToken, string guildId, string userId, string roleId, string? reason, CancellationToken cancellationToken = default)
            => SendSimpleAsync(botToken, HttpMethod.Put, $"/guilds/{guildId}/members/{userId}/roles/{roleId}", null, reason, "賦予身分組", cancellationToken);

        /// <inheritdoc />
        public Task<ModerationResult> RemoveRoleAsync(string botToken, string guildId, string userId, string roleId, string? reason, CancellationToken cancellationToken = default)
            => SendSimpleAsync(botToken, HttpMethod.Delete, $"/guilds/{guildId}/members/{userId}/roles/{roleId}", null, reason, "移除身分組", cancellationToken);

        /// <inheritdoc />
        public Task<ModerationResult> SetSlowmodeAsync(string botToken, string channelId, int seconds, string? reason, CancellationToken cancellationToken = default)
            => SendSimpleAsync(botToken, HttpMethod.Patch, $"/channels/{channelId}", new { rate_limit_per_user = Math.Clamp(seconds, 0, 21600) }, reason, "設定慢速模式", cancellationToken);

        /// <inheritdoc />
        public async Task<ModerationResult> PurgeMessagesAsync(string botToken, string channelId, int count, CancellationToken cancellationToken = default)
        {
            count = Math.Clamp(count, 1, 100);
            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1. 抓取最近 count 則訊息
                using var getReq = new HttpRequestMessage(HttpMethod.Get, $"{DiscordApiBase}/channels/{channelId}/messages?limit={count}");
                getReq.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());
                using var getResp = await client.SendAsync(getReq, cancellationToken);
                if (!getResp.IsSuccessStatusCode)
                    return new ModerationResult(false, ToFriendlyReason(getResp.StatusCode, "批次刪除"));

                using var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync(cancellationToken));
                var ids = doc.RootElement.EnumerateArray()
                    .Select(m => m.TryGetProperty("id", out var id) ? id.GetString() : null)
                    .Where(id => id is not null).Select(id => id!).ToList();

                if (ids.Count == 0) return new ModerationResult(true, "沒有可刪除的訊息");

                // 2. bulk-delete 需 2~100 則且 14 天內；只有 1 則時改用單則刪除
                HttpResponseMessage delResp;
                if (ids.Count == 1)
                {
                    using var single = new HttpRequestMessage(HttpMethod.Delete, $"{DiscordApiBase}/channels/{channelId}/messages/{ids[0]}");
                    single.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());
                    delResp = await client.SendAsync(single, cancellationToken);
                }
                else
                {
                    using var bulk = new HttpRequestMessage(HttpMethod.Post, $"{DiscordApiBase}/channels/{channelId}/messages/bulk-delete")
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new { messages = ids }), Encoding.UTF8, "application/json")
                    };
                    bulk.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());
                    delResp = await client.SendAsync(bulk, cancellationToken);
                }

                using (delResp)
                {
                    if (delResp.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Discord 批次刪除成功 Channel={ChannelId} Count={Count}", channelId, ids.Count);
                        return new ModerationResult(true, $"已刪除 {ids.Count} 則訊息");
                    }
                    var err = await delResp.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Discord 批次刪除失敗 {Status} Channel={ChannelId}: {Error}", (int)delResp.StatusCode, channelId, err);
                    return new ModerationResult(false, ToFriendlyReason(delResp.StatusCode, "批次刪除") + "（僅能刪除 14 天內的訊息）");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord 批次刪除例外 Channel={ChannelId}", channelId);
                return new ModerationResult(false, "無法連線到 Discord，請稍後再試");
            }
        }

        /// <summary>通用送出：PUT/DELETE/PATCH 任意端點，可選 JSON body 與稽核原因</summary>
        private async Task<ModerationResult> SendSimpleAsync(
            string botToken, HttpMethod method, string path, object? payload, string? reason, string action, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                using var req = new HttpRequestMessage(method, $"{DiscordApiBase}{path}");
                if (payload is not null)
                    req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());
                if (!string.IsNullOrWhiteSpace(reason))
                    req.Headers.TryAddWithoutValidation("X-Audit-Log-Reason", Uri.EscapeDataString(reason));

                using var resp = await client.SendAsync(req, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Discord {Action} 成功 {Path}", action, path);
                    return new ModerationResult(true, null);
                }

                var error = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Discord {Action} 失敗 {Status} {Path}: {Error}", action, (int)resp.StatusCode, path, error);
                return new ModerationResult(false, ToFriendlyReason(resp.StatusCode, action));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord {Action} 例外 {Path}", action, path);
                return new ModerationResult(false, "無法連線到 Discord，請稍後再試");
            }
        }

        /// <summary>對 guild member 發 PATCH，統一處理驗證 header、稽核原因與錯誤對應</summary>
        private async Task<ModerationResult> PatchMemberAsync(
            string botToken, string guildId, string userId, object payload, string? reason, string action, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{DiscordApiBase}/guilds/{guildId}/members/{userId}";

                using var req = new HttpRequestMessage(HttpMethod.Patch, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());
                if (!string.IsNullOrWhiteSpace(reason))
                    req.Headers.TryAddWithoutValidation("X-Audit-Log-Reason", Uri.EscapeDataString(reason));

                using var resp = await client.SendAsync(req, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Discord {Action} 成功 Guild={GuildId} User={UserId}", action, guildId, userId);
                    return new ModerationResult(true, null);
                }

                var statusCode = (int)resp.StatusCode;
                var error = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Discord {Action} 失敗 {Status} Guild={GuildId} User={UserId}: {Error}",
                    action, statusCode, guildId, userId, error);
                return new ModerationResult(false, ToFriendlyReason(resp.StatusCode, action));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord {Action} 例外 Guild={GuildId} User={UserId}", action, guildId, userId);
                return new ModerationResult(false, "無法連線到 Discord，請稍後再試");
            }
        }

        private static string ToFriendlyReason(HttpStatusCode status, string action) => (int)status switch
        {
            400 => $"無法{action}：參數不正確或對象狀態不符（例如不在語音頻道、暱稱過長）",
            403 => $"無法{action}：Bot 權限不足或身分組階級低於對象（無法對擁有者/管理員/更高身分組動作）",
            404 => $"無法{action}：找不到該成員（可能已離開伺服器）",
            429 => $"無法{action}：請求過於頻繁，請稍後再試",
            _ => $"無法{action}（Discord 回應 {(int)status}）"
        };
    }
}