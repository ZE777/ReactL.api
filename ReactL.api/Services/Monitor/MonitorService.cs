using Microsoft.EntityFrameworkCore;
using ReactL.api.Data;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Monitor;
using ReactL.api.DTOs.Responses.Monitor;

namespace ReactL.api.Services.Monitor
{
    public class MonitorService : IMonitorService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<MonitorService> _logger;

        public MonitorService(AppDbContext db, ILogger<MonitorService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<PagedResponse<ExternalMessageListItem>> GetExternalMessagesAsync(
            Guid userId, MonitorQueryParams query)
        {
            var q = _db.ExternalMessages
                .AsNoTracking()
                .Where(em => em.BotBinding.UserId == userId);

            if (!string.IsNullOrWhiteSpace(query.Platform))
                q = q.Where(em => em.Platform == query.Platform);

            if (query.BotBindingId.HasValue)
                q = q.Where(em => em.BotBindingId == query.BotBindingId.Value);

            if (query.From.HasValue)
                q = q.Where(em => em.CreatedAt >= query.From.Value);

            if (query.To.HasValue)
                q = q.Where(em => em.CreatedAt <= query.To.Value);

            if (!string.IsNullOrWhiteSpace(query.ExternalUserId))
                q = q.Where(em => em.ExternalUserId == query.ExternalUserId);

            var total = await q.CountAsync();
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);

            bool isChatMode = !string.IsNullOrWhiteSpace(query.ExternalUserId);

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
                    ContentPreview = isChatMode || em.Content.Length <= 100
                        ? em.Content
                        : em.Content.Substring(0, 100) + "...",
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

        public async Task<PagedResponse<ConversationSummary>> GetConversationsAsync(
            Guid userId, ConversationQueryParams query)
        {
            var q = _db.ExternalMessages
                .AsNoTracking()
                .Where(em => em.BotBinding.UserId == userId);

            if (!string.IsNullOrWhiteSpace(query.Platform))
                q = q.Where(em => em.Platform == query.Platform);

            if (query.BotBindingId.HasValue)
                q = q.Where(em => em.BotBindingId == query.BotBindingId.Value);

            var grouped = q
                .GroupBy(em => new
                {
                    em.Platform,
                    BotName = em.BotBinding.BotName,
                    em.ExternalUserId,
                    em.ExternalChannelId
                })
                .Select(g => new ConversationSummary
                {
                    Platform = g.Key.Platform,
                    BotName = g.Key.BotName,
                    ExternalUserId = g.Key.ExternalUserId,
                    ExternalChannelId = g.Key.ExternalChannelId,
                    MessageCount = g.Count(),
                    LastMessageAt = g.Max(em => em.CreatedAt)
                });

            var total = await grouped.CountAsync();
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);

            var items = await grouped
                .OrderByDescending(c => c.LastMessageAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 補上暱稱與頭像（若資料庫尚未執行 add_sender_profile.sql 遷移則略過）
            if (items.Count > 0)
            {
                try
                {
                    var userIds = items.Select(i => i.ExternalUserId).Distinct().ToList();

                    var profileRows = await _db.ExternalMessages
                        .AsNoTracking()
                        .Where(em => em.BotBinding.UserId == userId
                                  && userIds.Contains(em.ExternalUserId)
                                  && em.Role == "user"
                                  && em.SenderName != null)
                        .Select(em => new
                        {
                            em.ExternalUserId,
                            em.SenderName,
                            em.SenderAvatarUrl,
                            em.CreatedAt
                        })
                        .ToListAsync();

                    var profileMap = profileRows
                        .GroupBy(p => p.ExternalUserId)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderByDescending(p => p.CreatedAt).First());

                    foreach (var item in items)
                    {
                        if (profileMap.TryGetValue(item.ExternalUserId, out var profile))
                        {
                            item.SenderName = profile.SenderName;
                            item.SenderAvatarUrl = profile.SenderAvatarUrl;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 資料庫尚未執行 add_sender_profile.sql 遷移時，欄位不存在會拋例外
                    // 優雅降級：對話列表仍正常顯示，僅暱稱與頭像留空
                    _logger.LogWarning(ex, "無法取得傳送者 Profile，請確認已執行 add_sender_profile.sql");
                }
            }

            return new PagedResponse<ConversationSummary>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

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
                        RequestCount = g.Sum(t => t.RequestCount),
                        BySource = g.GroupBy(t => t.Source)
                            .Select(sg => new TokenStatsBySource
                            {
                                Source = sg.Key,
                                TokensIn = sg.Sum(t => t.TokensIn),
                                TokensOut = sg.Sum(t => t.TokensOut),
                                RequestCount = sg.Sum(t => t.RequestCount)
                            })
                            .ToList()
                    })
                    .ToList(),
                BySource = stats
                    .GroupBy(t => t.Source)
                    .Select(g => new TokenStatsBySource
                    {
                        Source = g.Key,
                        TokensIn = g.Sum(t => t.TokensIn),
                        TokensOut = g.Sum(t => t.TokensOut),
                        RequestCount = g.Sum(t => t.RequestCount)
                    })
                    .ToList()
            };
        }
    }
}
