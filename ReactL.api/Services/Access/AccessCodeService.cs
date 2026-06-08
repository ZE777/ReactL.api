using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Constants;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.Models.Access;
using ReactL.api.Models.Stats;
using System.Security.Cryptography;

namespace ReactL.api.Services.Access
{
    /// <summary>存取碼服務實作</summary>
    public class AccessCodeService : IAccessCodeService
    {
        private readonly AppDbContext _db;
        private readonly PublicChatSettings _settings;
        private readonly ILogger<AccessCodeService> _logger;

        // 排除易混淆字元（I/O/0/1），降低人工輸入錯誤
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 10;

        public AccessCodeService(AppDbContext db, IOptions<PublicChatSettings> settings, ILogger<AccessCodeService> logger)
        {
            _db = db;
            _settings = settings.Value;
            _logger = logger;
        }

        // ── 前台閘門 ──────────────────────────────────────────────────────────
        public async Task<AccessGateResult> ValidateForChatAsync(string? code, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            // 全域系統金鑰每日預算護欄（不論是否要求存取碼都先擋）
            if (_settings.GlobalDailyTokenBudget > 0)
            {
                var globalUsed = await _db.TokenUsageStats
                    .Where(s => s.UserId == SystemUser.Id && s.Source == TokenSource.Web && s.Date == today)
                    .Select(s => (int?)(s.TokensIn + s.TokensOut))
                    .SumAsync(cancellationToken) ?? 0;
                if (globalUsed >= _settings.GlobalDailyTokenBudget)
                    return AccessGateResult.Deny("quota_exceeded", "今日體驗額度已用完，請明天再試");
            }

            // 不要求存取碼：允許（若有帶有效碼仍記錄到該碼）
            if (!_settings.RequireAccessCode)
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    var optional = await FindUsableCodeAsync(code, cancellationToken);
                    return AccessGateResult.Allow(optional?.Id);
                }
                return AccessGateResult.Allow(null);
            }

            // 要求存取碼
            if (string.IsNullOrWhiteSpace(code))
                return AccessGateResult.Deny("error", "需要存取碼才能使用，請向管理員索取邀請連結");

            var ac = await FindUsableCodeAsync(code, cancellationToken);
            if (ac == null)
                return AccessGateResult.Deny("error", "存取碼無效、已停用或已過期");

            // 每碼每日 token 配額
            if (ac.DailyTokenLimit > 0)
            {
                var used = await _db.AccessCodeUsages
                    .Where(u => u.AccessCodeId == ac.Id && u.Date == today)
                    .Select(u => (int?)(u.TokensIn + u.TokensOut))
                    .SumAsync(cancellationToken) ?? 0;
                if (used >= ac.DailyTokenLimit)
                    return AccessGateResult.Deny("quota_exceeded", "此存取碼今日額度已用完，請明天再試");
            }

            return AccessGateResult.Allow(ac.Id);
        }

        // ── 用量記錄（best-effort，使用 None token 確保前端斷線也會寫入）────────
        public async Task RecordUsageAsync(Guid? accessCodeId, string modelType, int tokensIn, int tokensOut, CancellationToken cancellationToken)
        {
            if (tokensIn <= 0 && tokensOut <= 0) return;

            var today = DateOnly.FromDateTime(DateTime.Now);

            // per-code 每日用量 UPSERT
            if (accessCodeId is Guid id)
            {
                var usage = await _db.AccessCodeUsages
                    .FirstOrDefaultAsync(u => u.AccessCodeId == id && u.Date == today, CancellationToken.None);
                if (usage == null)
                {
                    _db.AccessCodeUsages.Add(new AccessCodeUsage
                    {
                        AccessCodeId = id,
                        Date = today,
                        TokensIn = tokensIn,
                        TokensOut = tokensOut,
                        RequestCount = 1,
                    });
                }
                else
                {
                    usage.TokensIn += tokensIn;
                    usage.TokensOut += tokensOut;
                    usage.RequestCount += 1;
                }
            }

            // 全域系統用量 UPSERT（掛在系統用戶 + Source=web，亦會出現在統計頁）
            var stat = await _db.TokenUsageStats.FirstOrDefaultAsync(
                s => s.UserId == SystemUser.Id && s.Date == today && s.ModelType == modelType && s.Source == TokenSource.Web,
                CancellationToken.None);
            if (stat == null)
            {
                _db.TokenUsageStats.Add(new TokenUsageStat
                {
                    UserId = SystemUser.Id,
                    Date = today,
                    ModelType = modelType,
                    Source = TokenSource.Web,
                    TokensIn = tokensIn,
                    TokensOut = tokensOut,
                    RequestCount = 1,
                });
            }
            else
            {
                stat.TokensIn += tokensIn;
                stat.TokensOut += tokensOut;
                stat.RequestCount += 1;
            }

            await _db.SaveChangesAsync(CancellationToken.None);
        }

        // ── 前台狀態查詢 ──────────────────────────────────────────────────────
        public async Task<AccessCodeStatus> GetStatusAsync(string? code, CancellationToken cancellationToken)
        {
            var status = new AccessCodeStatus
            {
                RequireAccessCode = _settings.RequireAccessCode,
                LogRetentionDays = _settings.LogRetentionDays,
            };
            if (string.IsNullOrWhiteSpace(code)) return status;

            var ac = await FindUsableCodeAsync(code, cancellationToken);
            if (ac == null) return status; // Valid 維持 false

            var today = DateOnly.FromDateTime(DateTime.Now);
            var used = await _db.AccessCodeUsages
                .Where(u => u.AccessCodeId == ac.Id && u.Date == today)
                .Select(u => (int?)(u.TokensIn + u.TokensOut))
                .SumAsync(cancellationToken) ?? 0;

            status.Valid = true;
            status.Label = ac.Label;
            status.DailyTokenLimit = ac.DailyTokenLimit;
            status.UsedToday = used;
            status.Remaining = ac.DailyTokenLimit > 0 ? Math.Max(0, ac.DailyTokenLimit - used) : null;
            return status;
        }

        // ── 後台 CRUD ──────────────────────────────────────────────────────────
        public async Task<List<AccessCodeListItem>> ListAsync(CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var codes = await _db.AccessCodes.AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(cancellationToken);

            var usageMap = await _db.AccessCodeUsages.AsNoTracking()
                .Where(u => u.Date == today)
                .ToDictionaryAsync(u => u.AccessCodeId, cancellationToken);

            return codes.Select(a =>
            {
                usageMap.TryGetValue(a.Id, out var u);
                return ToListItem(a, u?.TokensIn + u?.TokensOut ?? 0, u?.RequestCount ?? 0);
            }).ToList();
        }

        public async Task<AccessCodeListItem> CreateAsync(string? label, int? dailyTokenLimit, DateTime? expiresAt, CancellationToken cancellationToken)
        {
            var entity = new AccessCode
            {
                Code = await GenerateUniqueCodeAsync(cancellationToken),
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                DailyTokenLimit = dailyTokenLimit ?? _settings.DefaultDailyTokenLimit,
                ExpiresAt = expiresAt,
                IsActive = true,
            };
            _db.AccessCodes.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("已建立存取碼 {Code}（每日上限 {Limit}）", entity.Code, entity.DailyTokenLimit);
            return ToListItem(entity, 0, 0);
        }

        public async Task<AccessCodeListItem> UpdateAsync(Guid id, string? label, int dailyTokenLimit, DateTime? expiresAt, CancellationToken cancellationToken)
        {
            var entity = await _db.AccessCodes.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                ?? throw new NotFoundException("AccessCode", id);

            entity.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            entity.DailyTokenLimit = dailyTokenLimit;
            entity.ExpiresAt = expiresAt;
            await _db.SaveChangesAsync(cancellationToken);
            return ToListItem(entity, 0, 0);
        }

        public async Task<AccessCodeListItem> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
        {
            var entity = await _db.AccessCodes.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                ?? throw new NotFoundException("AccessCode", id);

            entity.IsActive = isActive;
            await _db.SaveChangesAsync(cancellationToken);
            return ToListItem(entity, 0, 0);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var entity = await _db.AccessCodes.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                ?? throw new NotFoundException("AccessCode", id);

            _db.AccessCodes.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ── 內部工具 ──────────────────────────────────────────────────────────
        private async Task<AccessCode?> FindUsableCodeAsync(string code, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            return await _db.AccessCodes.AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.Code == code && a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now),
                    cancellationToken);
        }

        private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var code = GenerateCode();
                var exists = await _db.AccessCodes.AnyAsync(a => a.Code == code, cancellationToken);
                if (!exists) return code;
            }
            // 極端碰撞情境的保險：附加時間戳記確保唯一
            return GenerateCode() + DateTime.Now.Ticks.ToString()[^4..];
        }

        private static string GenerateCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(CodeLength);
            var chars = new char[CodeLength];
            for (var i = 0; i < CodeLength; i++)
                chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
            return new string(chars);
        }

        private static AccessCodeListItem ToListItem(AccessCode a, int usedTokensToday, int requestsToday) => new()
        {
            Id = a.Id,
            Code = a.Code,
            Label = a.Label,
            DailyTokenLimit = a.DailyTokenLimit,
            ExpiresAt = a.ExpiresAt,
            IsActive = a.IsActive,
            CreatedAt = a.CreatedAt,
            UsedTokensToday = usedTokensToday,
            RequestsToday = requestsToday,
        };
    }
}
