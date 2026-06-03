using Microsoft.EntityFrameworkCore;
using ReactL.api.Data;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Monitor;
using ReactL.api.DTOs.Responses.Monitor;

namespace ReactL.api.Services.Monitor
{
    /// <summary>監控服務實作（統計資料是計算後的彙總，直接回傳 DTO）</summary>
    public class MonitorService : IMonitorService
    {
        private readonly AppDbContext _db;

        public MonitorService(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>取得分頁的外部訊息列表（透過 BotBinding 確認訊息屬於當前使用者）</summary>
        public async Task<PagedResponse<ExternalMessageListItem>> GetExternalMessagesAsync(
            Guid userId, MonitorQueryParams query)
        {
            // 透過 BotBinding 確認訊息屬於當前使用者
            var q = _db.ExternalMessages
                .AsNoTracking()
                .Where(em => em.BotBinding.UserId == userId);

            if (!string.IsNullOrWhiteSpace(query.Platform))
                q = q.Where(em => em.Platform == query.Platform);

            if (query.From.HasValue)
                q = q.Where(em => em.CreatedAt >= query.From.Value);

            if (query.To.HasValue)
                q = q.Where(em => em.CreatedAt <= query.To.Value);

            if (!string.IsNullOrWhiteSpace(query.ExternalUserId))
                q = q.Where(em => em.ExternalUserId == query.ExternalUserId);

            var total = await q.CountAsync();
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);

            var items = await q
                .OrderByDescending(em => em.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(em => new ExternalMessageListItem
                {
                    Id = em.Id,
                    Platform = em.Platform,
                    BotName = em.BotBinding.BotName,
                    ExternalUserId = em.ExternalUserId,
                    ExternalChannelId = em.ExternalChannelId,
                    Role = em.Role,
                    // 截斷長訊息，避免列表資料量過大（EF Core Expression Tree 不支援 range，用 Substring）
                    ContentPreview = em.Content.Length > 100
                        ? em.Content.Substring(0, 100) + "…"
                        : em.Content,
                    TokensIn = em.TokensIn,
                    TokensOut = em.TokensOut,
                    CreatedAt = em.CreatedAt
                })
                .ToListAsync();

            return new PagedResponse<ExternalMessageListItem>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <summary>取得 Token 用量統計總覽（Dashboard 卡片 + 圖表資料）</summary>
        public async Task<StatsSummary> GetTokenStatsAsync(Guid userId, StatsQueryParams query)
        {
            var q = _db.TokenUsageStats
                .AsNoTracking()
                .Where(t => t.UserId == userId);

            if (query.From.HasValue)
                q = q.Where(t => t.Date >= DateOnly.FromDateTime(query.From.Value));

            if (query.To.HasValue)
                q = q.Where(t => t.Date <= DateOnly.FromDateTime(query.To.Value));

            if (!string.IsNullOrWhiteSpace(query.Source))
                q = q.Where(t => t.Source == query.Source);

            var stats = await q.ToListAsync();

            return new StatsSummary
            {
                TotalRequests = stats.Sum(t => t.RequestCount),
                TotalTokensIn = stats.Sum(t => t.TokensIn),
                TotalTokensOut = stats.Sum(t => t.TokensOut),
                ByDate = stats
                    .GroupBy(t => t.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new TokenStatsByDate
                    {
                        Date = g.Key,
                        TokensIn = g.Sum(t => t.TokensIn),
                        TokensOut = g.Sum(t => t.TokensOut),
                        RequestCount = g.Sum(t => t.RequestCount)
                    })
                    .ToList(),
                ByModel = stats
                    .GroupBy(t => t.ModelType)
                    .Select(g => new TokenStatsByModel
                    {
                        ModelType = g.Key,
                        TokensIn = g.Sum(t => t.TokensIn),
                        TokensOut = g.Sum(t => t.TokensOut),
                        RequestCount = g.Sum(t => t.RequestCount)
                    })
                    .ToList()
            };
        }
    }
}
