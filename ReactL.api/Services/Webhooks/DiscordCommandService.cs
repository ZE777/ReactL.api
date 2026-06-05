using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>Discord 應用程式指令（Slash Command）註冊服務實作</summary>
    public class DiscordCommandService : IDiscordCommandService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DiscordCommandService> _logger;

        private const string DiscordApiBase = "https://discord.com/api/v10";

        // Discord 指令選項型別：3 = STRING（參見 Discord API ApplicationCommandOptionType）
        private const int OptionTypeString = 3;
        // Discord 指令型別：1 = CHAT_INPUT（斜線指令）
        private const int CommandTypeChatInput = 1;

        public DiscordCommandService(IHttpClientFactory httpClientFactory, ILogger<DiscordCommandService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DiscordCommandRegistrationResult> RegisterChatCommandAsync(string applicationId, string botToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(applicationId) || string.IsNullOrWhiteSpace(botToken))
            {
                _logger.LogWarning("Discord 指令註冊略過：ApplicationId 或 Token 為空");
                return new DiscordCommandRegistrationResult(false, "缺少 Bot Token 或 Application ID");
            }

            // 指令定義需與 DiscordWebhookService 解析邏輯一致：指令含 message 字串參數
            // 採 PUT 全量覆寫（bulk overwrite）Global 指令，具冪等性，重複註冊不會產生重複指令
            var commands = new[]
            {
                new
                {
                    name = "chat",
                    description = "與 AI 助理對話",
                    type = CommandTypeChatInput,
                    options = new[]
                    {
                        new
                        {
                            name = "message",
                            description = "你想問的問題",
                            type = OptionTypeString,
                            required = true
                        }
                    }
                }
            };

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());

                var url  = $"{DiscordApiBase}/applications/{applicationId.Trim()}/commands";
                var body = new StringContent(JsonSerializer.Serialize(commands), Encoding.UTF8, "application/json");

                var response = await client.PutAsync(url, body, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Discord Global /chat 指令註冊成功 ApplicationId={ApplicationId}", applicationId);
                    return new DiscordCommandRegistrationResult(true, null);
                }

                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Discord 指令註冊失敗 {Status} ApplicationId={ApplicationId}: {Error}",
                    (int)response.StatusCode, applicationId, error);
                return new DiscordCommandRegistrationResult(false, ToFriendlyReason((int)response.StatusCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord 指令註冊例外 ApplicationId={ApplicationId}", applicationId);
                return new DiscordCommandRegistrationResult(false, "無法連線到 Discord，請確認網路後重試");
            }
        }

        /// <summary>將 Discord 回應的 HTTP 狀態碼轉成使用者看得懂的失敗原因</summary>
        private static string ToFriendlyReason(int statusCode) => statusCode switch
        {
            401 => "Bot Token 無效（請確認 Token 是否正確或已被 Reset）",
            403 => "權限不足，無法為此 Application 註冊指令",
            404 => "找不到此 Application（請確認 Discord Application ID 是否正確）",
            429 => "Discord 請求過於頻繁（rate limit），請稍後重試",
            _   => $"Discord 註冊失敗（HTTP {statusCode}），請確認 Token 與 Application ID"
        };
    }
}