using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Helpers;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.Domain.BotBindings;
using ReactL.api.Common.Constants;
using ReactL.api.DTOs.Requests.BotBindings;
using ReactL.api.DTOs.Responses.BotBindings;
using ReactL.api.Models.BotBindings;
using ReactL.api.Services.Webhooks;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ReactL.api.Services.BotBindings
{
    /// <summary>Bot 綁定服務實作</summary>
    public class BotBindingService : IBotBindingService
    {
        private readonly AppDbContext _db;
        private readonly AesEncryptionHelper _aes;
        private readonly ILogger<BotBindingService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppSettings _appSettings;
        private readonly IDiscordCommandService _discordCommand;
        private readonly ILineCredentialService _lineCredential;

        public BotBindingService(AppDbContext db, AesEncryptionHelper aes, ILogger<BotBindingService> logger, IHttpClientFactory httpClientFactory, IOptions<AppSettings> appSettings, IDiscordCommandService discordCommand, ILineCredentialService lineCredential)
        {
            _db = db;
            _aes = aes;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _appSettings = appSettings.Value;
            _discordCommand = discordCommand;
            _lineCredential = lineCredential;
        }

        /// <summary>
        /// 依平台驗證 Bot 憑證/設定，回傳 (是否有效, 失敗原因)；無法驗證的情況回傳 (null, null)。
        /// - Discord：註冊 Global /chat 指令（成功即代表 Token / Application ID 有效），順帶完成指令註冊
        /// - LINE：呼叫 GET /v2/bot/info 驗證 Channel Access Token
        /// 驗證失敗不拋例外，避免中斷 Bot 建立/更新流程。
        /// </summary>
        private async Task<(bool? Valid, string? Error)> ValidateCredentialAsync(BotBinding binding, string botTokenPlain)
        {
            if (binding.Platform == Platform.Discord)
            {
                if (string.IsNullOrWhiteSpace(binding.DiscordApplicationId)) return (null, null);
                var r = await _discordCommand.RegisterChatCommandAsync(binding.DiscordApplicationId, botTokenPlain);
                return (r.Success, r.Error);
            }

            if (binding.Platform == Platform.Line)
            {
                var r = await _lineCredential.ValidateAsync(botTokenPlain);
                return (r.Success, r.Error);
            }

            return (null, null);
        }

        /// <summary>組裝完整 Webhook URL：優先使用 Bot 自訂 BaseUrl，否則沿用系統 BaseUrl</summary>
        private string BuildWebhookUrl(string? botWebhookBaseUrl, string platform, Guid botId)
        {
            var baseUrl = (botWebhookBaseUrl ?? _appSettings.BaseUrl).Trim().TrimEnd('/');
            return $"{baseUrl}/webhooks/{platform}/{botId}";
        }

        /// <summary>取得使用者的 Bot 綁定清單</summary>
        public async Task<List<BotBindingDomain>> GetListAsync(Guid userId)
        {
            var list = await _db.BotBindings
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BotBindingDomain
                {
                    Id = b.Id,
                    UserId = b.UserId,
                    Platform = b.Platform,
                    BotName = b.BotName,
                    TokenLastFour = b.TokenLastFour,
                    ModelType = b.ModelType,
                    IsEnabled = b.IsEnabled,
                    PersonaId = b.PersonaId,
                    PersonaName = b.Persona != null ? b.Persona.Name : null,
                    WebhookBaseUrl = b.WebhookBaseUrl,
                    DiscordApplicationId = b.DiscordApplicationId,
                    DiscordPublicKey = b.DiscordPublicKey,
                    CredentialValid = b.CredentialValid,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt
                })
                .ToListAsync();

            // WebhookUrl 在 Select 裡無法呼叫 C# 方法，於記憶體中補算
            foreach (var item in list)
                item.WebhookUrl = BuildWebhookUrl(item.WebhookBaseUrl, item.Platform, item.Id);

            return list;
        }

        /// <summary>取得 Bot 綁定詳情</summary>
        public async Task<BotBindingDomain> GetByIdAsync(Guid id, Guid userId)
        {
            var b = await _db.BotBindings
                .AsNoTracking()
                .Include(b => b.Persona)
                .Where(b => b.Id == id && b.UserId == userId)
                .Select(b => new BotBindingDomain
                {
                    Id = b.Id,
                    UserId = b.UserId,
                    Platform = b.Platform,
                    BotName = b.BotName,
                    TokenLastFour = b.TokenLastFour,
                    ModelType = b.ModelType,
                    IsEnabled = b.IsEnabled,
                    PersonaId = b.PersonaId,
                    PersonaName = b.Persona != null ? b.Persona.Name : null,
                    WebhookBaseUrl = b.WebhookBaseUrl,
                    DiscordApplicationId = b.DiscordApplicationId,
                    DiscordPublicKey = b.DiscordPublicKey,
                    CredentialValid = b.CredentialValid,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt
                })
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("BotBinding", id);

            b.WebhookUrl = BuildWebhookUrl(b.WebhookBaseUrl, b.Platform, b.Id);
            return b;
        }

        /// <summary>建立 Bot 綁定（LINE 平台必須提供 ChannelSecret，Token 由後端加密儲存）</summary>
        public async Task<BotBindingDomain> CreateAsync(Guid userId, CreateBotBindingRequest request)
        {
            // LINE 平台必須提供 ChannelSecret
            if (request.Platform == "line" && string.IsNullOrWhiteSpace(request.ChannelSecret))
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["channelSecret"] = ["LINE 平台必須提供 Channel Secret"]
                });

            var token         = request.BotToken.Trim();
            var channelSecret = request.ChannelSecret?.Trim();

            var binding = new BotBinding
            {
                UserId = userId,
                Platform = request.Platform,
                BotName = request.BotName,
                BotTokenEncrypted = _aes.Encrypt(token),
                ChannelSecretEncrypted = channelSecret != null
                    ? _aes.Encrypt(channelSecret)
                    : null,
                // 儲存後 4 碼供列表顯示，避免每筆列表都解密
                TokenLastFour = AesEncryptionHelper.GetLastChars(token),
                ModelType = request.ModelType,
                PersonaId = request.PersonaId,
                WebhookBaseUrl = string.IsNullOrWhiteSpace(request.WebhookBaseUrl) ? null : request.WebhookBaseUrl.Trim().TrimEnd('/'),
                DiscordApplicationId = string.IsNullOrWhiteSpace(request.DiscordApplicationId) ? null : request.DiscordApplicationId.Trim(),
                DiscordPublicKey = string.IsNullOrWhiteSpace(request.DiscordPublicKey) ? null : request.DiscordPublicKey.Trim()
            };

            // 建立後依平台驗證憑證（Discord 註冊 /chat；LINE 驗 /bot/info），結果一併持久化供前端標示
            // 先驗證再存，讓驗證結果（有效/無效）在單次 SaveChanges 一併寫入
            var (valid, error) = await ValidateCredentialAsync(binding, token);
            binding.CredentialValid = valid;

            _db.BotBindings.Add(binding);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Bot 綁定建立成功 UserId={UserId} BotBindingId={BotBindingId} Platform={Platform} BotName={BotName} CredentialValid={Valid}",
                userId, binding.Id, binding.Platform, binding.BotName, binding.CredentialValid);

            var domain = await GetByIdAsync(binding.Id, userId);
            domain.CredentialError = error;
            return domain;
        }

        /// <summary>更新 Bot 設定（名稱、模型、Persona、啟用狀態）</summary>
        public async Task<BotBindingDomain> UpdateAsync(Guid id, Guid userId, UpdateBotBindingRequest request)
        {
            var binding = await GetOwnedAsync(id, userId);

            binding.BotName = request.BotName;
            binding.ModelType = request.ModelType;
            binding.PersonaId = request.PersonaId;
            binding.IsEnabled = request.IsEnabled;
            // null 表示清除自訂值，回復使用系統預設 BaseUrl
            binding.WebhookBaseUrl = string.IsNullOrWhiteSpace(request.WebhookBaseUrl) ? null : request.WebhookBaseUrl.Trim().TrimEnd('/');
            binding.DiscordApplicationId = string.IsNullOrWhiteSpace(request.DiscordApplicationId) ? null : request.DiscordApplicationId.Trim();
            binding.DiscordPublicKey = string.IsNullOrWhiteSpace(request.DiscordPublicKey) ? null : request.DiscordPublicKey.Trim();

            // 編輯後重新驗證憑證（可能才補上 ApplicationId / 更換設定；更新不帶 Token，需從 DB 解密）
            var (valid, error) = await ValidateCredentialAsync(binding, _aes.Decrypt(binding.BotTokenEncrypted));
            binding.CredentialValid = valid;

            await _db.SaveChangesAsync();

            var domain = await GetByIdAsync(id, userId);
            domain.CredentialError = error;
            return domain;
        }

        /// <summary>軟刪除 Bot 綁定</summary>
        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var binding = await GetOwnedAsync(id, userId);
            binding.IsDeleted = true;
            binding.DeletedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Bot 綁定已刪除 UserId={UserId} BotBindingId={BotBindingId} Platform={Platform}",
                userId, id, binding.Platform);
        }

        /// <summary>更換 Bot Token（Token 重新 AES 加密後存回 DB）</summary>
        public async Task<BotBindingDomain> RotateTokenAsync(Guid id, Guid userId, RotateTokenRequest request)
        {
            var binding = await GetOwnedAsync(id, userId);

            var newToken = request.NewToken.Trim();
            binding.BotTokenEncrypted = _aes.Encrypt(newToken);
            binding.TokenLastFour = AesEncryptionHelper.GetLastChars(newToken);

            if (request.NewChannelSecret != null)
                binding.ChannelSecretEncrypted = _aes.Encrypt(request.NewChannelSecret.Trim());

            // Token 更換後舊 Token 失效，用新 Token 重新驗證憑證（Discord 重新註冊 /chat、LINE 驗 /bot/info）並持久化
            var (valid, error) = await ValidateCredentialAsync(binding, newToken);
            binding.CredentialValid = valid;

            await _db.SaveChangesAsync();

            _logger.LogWarning("Bot Token 已更換 UserId={UserId} BotBindingId={BotBindingId} NewTokenLastFour={TokenLastFour} CredentialValid={Valid}",
                userId, id, binding.TokenLastFour, binding.CredentialValid);

            var domain = await GetByIdAsync(id, userId);
            domain.CredentialError = error;
            return domain;
        }

        /// <summary>查詢 LINE Bot 本月訊息用量（需解密 Token 後呼叫 LINE Messaging API）</summary>
        public async Task<LineQuotaResponse> GetLineQuotaAsync(Guid id, Guid userId)
        {
            var binding = await _db.BotBindings
                .AsNoTracking()
                .Where(b => b.Id == id && b.UserId == userId)
                .Select(b => new { b.Platform, b.BotTokenEncrypted })
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("BotBinding", id);

            if (binding.Platform != "line")
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["platform"] = ["僅 LINE Bot 支援用量查詢"]
                });

            var token = _aes.Decrypt(binding.BotTokenEncrypted);
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 取用量上限
            var quotaJson = await (await client.GetAsync("https://api.line.me/v2/bot/message/quota")).Content.ReadAsStringAsync();
            using var quotaDoc = JsonDocument.Parse(quotaJson);
            var quotaType = quotaDoc.RootElement.GetProperty("type").GetString() ?? "none";
            int? limit = quotaType == "limited" && quotaDoc.RootElement.TryGetProperty("value", out var valEl)
                ? valEl.GetInt32()
                : null;

            // 取本月已用量
            var consumptionJson = await (await client.GetAsync("https://api.line.me/v2/bot/message/quota/consumption")).Content.ReadAsStringAsync();
            using var consumptionDoc = JsonDocument.Parse(consumptionJson);
            var totalUsage = consumptionDoc.RootElement.GetProperty("totalUsage").GetInt32();

            return new LineQuotaResponse
            {
                QuotaType = quotaType,
                Limit = limit,
                TotalUsage = totalUsage,
                Remaining = limit.HasValue ? Math.Max(0, limit.Value - totalUsage) : null
            };
        }

        /// <summary>取得使用者有所有權的 BotBinding（已追蹤，可直接修改）</summary>
        private async Task<BotBinding> GetOwnedAsync(Guid id, Guid userId)
        {
            return await _db.BotBindings
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId)
                ?? throw new NotFoundException("BotBinding", id);
        }
    }
}
