using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Constants;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Helpers;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.DTOs.Requests.AiKeys;
using ReactL.api.DTOs.Responses.AiKeys;
using ReactL.api.Models.Ai;
using ReactL.api.Models.Auth;

namespace ReactL.api.Services.Ai
{
    /// <summary>AI Key 管理服務實作（混合 BYOK）</summary>
    public class AiKeyService : IAiKeyService
    {
        private readonly AppDbContext _db;
        private readonly AesEncryptionHelper _aes;
        private readonly AiSettings _aiSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AiKeyService> _logger;

        public AiKeyService(
            AppDbContext db,
            AesEncryptionHelper aes,
            IOptions<AiSettings> aiSettings,
            IHttpClientFactory httpClientFactory,
            ILogger<AiKeyService> logger)
        {
            _db = db;
            _aes = aes;
            _aiSettings = aiSettings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<AiKeyResponse>> GetMyKeysAsync(Guid userId)
        {
            var keys = await _db.AiKeys
                .AsNoTracking()
                .Where(k => k.UserId == userId)
                .OrderBy(k => k.ProviderId)
                .ToListAsync();

            return keys.Select(ToResponse).ToList();
        }

        public async Task<AiKeyResponse> UpsertAsync(Guid userId, UpsertAiKeyRequest request)
        {
            var providerId = request.ProviderId.Trim();
            var apiKey = request.ApiKey.Trim();

            // 供應商必須是已設定的清單之一
            var provider = _aiSettings.Providers.FirstOrDefault(p => p.Id == providerId)
                ?? throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["providerId"] = [$"未知的供應商：{providerId}"]
                });

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["apiKey"] = ["API Key 不可為空"]
                });

            // 儲存前先驗證金鑰有效性（呼叫供應商 /models）
            await ValidateKeyAsync(provider, apiKey);

            var existing = await _db.AiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.ProviderId == providerId);

            if (existing != null)
            {
                existing.EncryptedKey = _aes.Encrypt(apiKey);
                existing.KeyLastFour = AesEncryptionHelper.GetLastChars(apiKey);
                existing.IsActive = true;
                _logger.LogInformation("使用者 {UserId} 更新 {Provider} 的 AI Key", userId, providerId);
            }
            else
            {
                existing = new AiKey
                {
                    UserId = userId,
                    ProviderId = providerId,
                    EncryptedKey = _aes.Encrypt(apiKey),
                    KeyLastFour = AesEncryptionHelper.GetLastChars(apiKey),
                    IsActive = true,
                    IsSystem = false
                };
                _db.AiKeys.Add(existing);
                _logger.LogInformation("使用者 {UserId} 新增 {Provider} 的 AI Key", userId, providerId);
            }

            await _db.SaveChangesAsync();
            return ToResponse(existing);
        }

        public async Task DeleteAsync(Guid userId, string providerId)
        {
            var key = await _db.AiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.ProviderId == providerId)
                ?? throw new NotFoundException("AiKey", providerId);

            _db.AiKeys.Remove(key);
            await _db.SaveChangesAsync();
            _logger.LogInformation("使用者 {UserId} 刪除 {Provider} 的 AI Key", userId, providerId);
        }

        public async Task<string?> ResolveKeyAsync(Guid? ownerUserId, string providerId, bool allowSystemFallback = true, CancellationToken cancellationToken = default)
        {
            // 1) 使用者自帶的啟用中 Key
            if (ownerUserId is Guid uid)
            {
                var userKey = await _db.AiKeys.AsNoTracking()
                    .FirstOrDefaultAsync(k => k.UserId == uid && k.ProviderId == providerId && k.IsActive, cancellationToken);
                if (userKey != null)
                    return _aes.Decrypt(userKey.EncryptedKey);
            }

            // 後台強制：不允許 fallback 時，沒有自帶 Key 就回 null（由呼叫端擋下，避免白嫖系統 key）
            if (!allowSystemFallback)
                return null;

            // 2) 系統預設 Key（DB）
            var sysKey = await _db.AiKeys.AsNoTracking()
                .FirstOrDefaultAsync(k => k.UserId == SystemUser.Id && k.ProviderId == providerId && k.IsActive && k.IsSystem, cancellationToken);
            if (sysKey != null)
                return _aes.Decrypt(sysKey.EncryptedKey);

            // 3) appsettings fallback（種子尚未寫入時的保險）
            return _aiSettings.ProviderKeys.GetValueOrDefault(providerId);
        }

        public async Task SeedSystemKeysAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSystemUserAsync(cancellationToken);

            foreach (var provider in _aiSettings.Providers)
            {
                var configKey = _aiSettings.ProviderKeys.GetValueOrDefault(provider.Id);
                if (string.IsNullOrWhiteSpace(configKey))
                    continue; // appsettings 沒設定此供應商的 Key → 無從種子

                var exists = await _db.AiKeys
                    .AnyAsync(k => k.UserId == SystemUser.Id && k.ProviderId == provider.Id, cancellationToken);
                if (exists)
                    continue; // 冪等：已種子過則略過

                _db.AiKeys.Add(new AiKey
                {
                    UserId = SystemUser.Id,
                    ProviderId = provider.Id,
                    EncryptedKey = _aes.Encrypt(configKey.Trim()),
                    KeyLastFour = AesEncryptionHelper.GetLastChars(configKey.Trim()),
                    IsActive = true,
                    IsSystem = true
                });
                _logger.LogInformation("已種子系統預設 AI Key：{Provider}", provider.Id);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        // ── 私有輔助 ──────────────────────────────────────────────────────

        /// <summary>確保系統用戶存在（持有系統預設 Key 與 Official Persona），不可登入</summary>
        private async Task EnsureSystemUserAsync(CancellationToken cancellationToken)
        {
            var exists = await _db.Users.AnyAsync(u => u.Id == SystemUser.Id, cancellationToken);
            if (exists) return;

            _db.Users.Add(new User
            {
                Id = SystemUser.Id,
                Email = SystemUser.Email,
                PasswordHash = SystemUser.UnusablePasswordHash,
                DisplayName = SystemUser.DisplayName,
                Role = "User",
                IsActive = false
            });
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("已建立系統用戶 {UserId}", SystemUser.Id);
        }

        /// <summary>呼叫供應商 /models 驗證金鑰；401/403 視為無效並擋下，其他異常從寬放行</summary>
        private async Task ValidateKeyAsync(AiProviderConfig provider, string apiKey)
        {
            try
            {
                var client = _httpClientFactory.CreateClient($"ai:{provider.Id}");
                using var req = new HttpRequestMessage(HttpMethod.Get, "models");
                // 以待驗證的新 Key 覆蓋預設 Authorization（request 層 header 優先於 DefaultRequestHeaders）
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                using var resp = await client.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                    return;

                var status = (int)resp.StatusCode;
                if (status is 401 or 403)
                    throw new ValidationException(new Dictionary<string, string[]>
                    {
                        ["apiKey"] = [$"{provider.DisplayName} 金鑰無效或權限不足，請確認後重試"]
                    });

                // 其他狀態（404 不支援 /models、429 限流…）不視為金鑰錯誤，從寬放行但記錄
                _logger.LogWarning("驗證 {Provider} 金鑰時收到非預期狀態 {Status}，從寬放行", provider.Id, status);
            }
            catch (ValidationException)
            {
                throw; // 驗證失敗照實往上丟
            }
            catch (Exception ex)
            {
                // 網路/逾時等問題不應阻擋使用者存 Key（避免驗證服務不穩就無法設定）
                _logger.LogWarning(ex, "驗證 {Provider} 金鑰時發生例外，從寬放行", provider.Id);
            }
        }

        private AiKeyResponse ToResponse(AiKey k) => new()
        {
            Id = k.Id,
            ProviderId = k.ProviderId,
            ProviderDisplayName = _aiSettings.Providers.FirstOrDefault(p => p.Id == k.ProviderId)?.DisplayName ?? k.ProviderId,
            KeyLastFour = k.KeyLastFour,
            IsActive = k.IsActive,
            IsSystem = k.IsSystem,
            CreatedAt = k.CreatedAt,
            UpdatedAt = k.UpdatedAt
        };
    }
}