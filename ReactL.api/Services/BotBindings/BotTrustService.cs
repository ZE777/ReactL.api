using Microsoft.EntityFrameworkCore;
using ReactL.api.Data;
using ReactL.api.Models.BotBindings;
using System.Text.Json;

namespace ReactL.api.Services.BotBindings
{
    /// <summary>信任名單讀寫實作（JSON 存於 BotBinding.TrustedUsersJson）。</summary>
    public class BotTrustService : IBotTrustService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<BotTrustService> _logger;

        // 一份名單的上限，避免 prompt 無限膨脹 / 惡意灌爆
        private const int MaxTrustedUsers = 100;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public BotTrustService(AppDbContext db, ILogger<BotTrustService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<TrustedUser>> GetListAsync(Guid botBindingId, CancellationToken ct = default)
        {
            var json = await _db.BotBindings
                .Where(b => b.Id == botBindingId)
                .Select(b => b.TrustedUsersJson)
                .FirstOrDefaultAsync(ct);

            return Parse(json);
        }

        public async Task<TrustedUser?> FindAsync(Guid botBindingId, string discordUserId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(discordUserId)) return null;
            var list = await GetListAsync(botBindingId, ct);
            return list.FirstOrDefault(t => t.Id == discordUserId);
        }

        public async Task<bool> IsOwnerAsync(Guid botBindingId, string? discordUserId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(discordUserId)) return false;
            var m = await FindAsync(botBindingId, discordUserId, ct);
            return m is not null && m.SystemRole == TrustRole.Owner;
        }

        public async Task<(bool Added, TrustedUser? User)> AddAsync(Guid botBindingId, TrustedUser user, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(user.Id)) return (false, null);

            // 取已追蹤實體以便回寫（注意：呼叫端的 DbContext 為同一 scoped 實例）
            var binding = await _db.BotBindings.FirstOrDefaultAsync(b => b.Id == botBindingId, ct);
            if (binding is null) return (false, null);

            user.SystemRole = TrustRole.Normalize(user.SystemRole);

            var list = Parse(binding.TrustedUsersJson);
            var existing = list.FirstOrDefault(t => t.Id == user.Id);

            bool added;
            if (existing is null)
            {
                if (list.Count >= MaxTrustedUsers)
                {
                    _logger.LogWarning("信任名單已達上限 {Max}，BotBindingId={Id}", MaxTrustedUsers, botBindingId);
                    return (false, null);
                }
                list.Add(user);
                added = true;
            }
            else
            {
                // 已存在 → 更新名稱/關係/系統角色（保留原 GrantedAt/GrantedBy）
                existing.Label = string.IsNullOrWhiteSpace(user.Label) ? existing.Label : user.Label;
                existing.Tier = user.Tier ?? existing.Tier;
                existing.SystemRole = user.SystemRole;
                user = existing;
                added = false;
            }

            binding.TrustedUsersJson = Serialize(list);
            await _db.SaveChangesAsync(ct);
            return (added, user);
        }

        public async Task<bool> RemoveAsync(Guid botBindingId, string discordUserId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(discordUserId)) return false;

            var binding = await _db.BotBindings.FirstOrDefaultAsync(b => b.Id == botBindingId, ct);
            if (binding is null) return false;

            var list = Parse(binding.TrustedUsersJson);
            var removed = list.RemoveAll(t => t.Id == discordUserId) > 0;
            if (!removed) return false;

            binding.TrustedUsersJson = list.Count == 0 ? null : Serialize(list);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ── JSON 共用 ──────────────────────────────────────────────────────
        private static List<TrustedUser> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<TrustedUser>();
            try
            {
                return JsonSerializer.Deserialize<List<TrustedUser>>(json, JsonOpts) ?? new List<TrustedUser>();
            }
            catch
            {
                return new List<TrustedUser>();
            }
        }

        private static string Serialize(List<TrustedUser> list) => JsonSerializer.Serialize(list, JsonOpts);
    }
}