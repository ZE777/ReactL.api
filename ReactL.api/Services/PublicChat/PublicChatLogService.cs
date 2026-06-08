using Microsoft.EntityFrameworkCore;
using ReactL.api.Data;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.PublicChat;
using ReactL.api.DTOs.Responses.PublicChat;
using ReactL.api.Models.PublicChat;

namespace ReactL.api.Services.PublicChat
{
    /// <summary>前台公開聊天記錄服務實作</summary>
    public class PublicChatLogService : IPublicChatLogService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<PublicChatLogService> _logger;

        public PublicChatLogService(AppDbContext db, ILogger<PublicChatLogService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ── 串流寫入（best-effort）────────────────────────────────────────────
        public async Task LogMessageAsync(
            string? sessionId, Guid? accessCodeId, string? accessCodeText,
            string role, string content, string? modelType, Guid? personaId,
            int tokensIn, int tokensOut, CancellationToken cancellationToken)
        {
            try
            {
                _db.PublicChatLogs.Add(new PublicChatLog
                {
                    // 前端未帶 session 時以 "(unknown)" 分組，避免遺失記錄
                    SessionId = string.IsNullOrWhiteSpace(sessionId) ? "(unknown)" : sessionId.Trim(),
                    AccessCodeId = accessCodeId,
                    AccessCodeText = accessCodeText,
                    Role = role,
                    Content = content,
                    ModelType = modelType,
                    PersonaId = personaId,
                    TokensIn = tokensIn,
                    TokensOut = tokensOut,
                });
                // 使用 None：前端斷線取消時仍要寫入這筆記錄
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // 記錄前台聊天為輔助功能，失敗不可影響聊天回應
                _logger.LogWarning(ex, "寫入前台聊天記錄失敗（session={SessionId}, role={Role}）", sessionId, role);
            }
        }

        // ── 後台查詢：對話列表（以 SessionId 分組）──────────────────────────────
        public async Task<PagedResponse<PublicChatConversationSummary>> GetConversationsAsync(
            PublicChatConversationQueryParams query)
        {
            var q = _db.PublicChatLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(l => (l.AccessCodeText != null && l.AccessCodeText.Contains(s)) || l.SessionId.Contains(s));
            }

            // 同一 session 的存取碼固定，groupBy 後一筆 = 一段對話
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);

            var groups = q.GroupBy(l => new { l.SessionId, l.AccessCodeId, l.AccessCodeText });
            var total = await groups.CountAsync();

            var rows = await groups
                .Select(g => new
                {
                    g.Key.SessionId,
                    g.Key.AccessCodeId,
                    g.Key.AccessCodeText,
                    MessageCount = g.Count(),
                    TotalTokens = g.Sum(l => l.TokensIn + l.TokensOut),
                    FirstMessageAt = g.Min(l => l.CreatedAt),
                    LastMessageAt = g.Max(l => l.CreatedAt),
                })
                .OrderByDescending(g => g.LastMessageAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 補上存取碼目前標籤（碼已刪除時 AccessCodeId 為 NULL，標籤留空）
            var codeIds = rows.Where(r => r.AccessCodeId.HasValue).Select(r => r.AccessCodeId!.Value).Distinct().ToList();
            var labelMap = codeIds.Count == 0
                ? new Dictionary<Guid, string?>()
                : await _db.AccessCodes.AsNoTracking()
                    .Where(a => codeIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, a => a.Label);

            // 補上每段對話「最近一次」使用的角色與模型（僅取本頁可見 session，查詢有界）
            var sessionIds = rows.Select(r => r.SessionId).Distinct().ToList();
            var metaRows = await _db.PublicChatLogs.AsNoTracking()
                .Where(l => sessionIds.Contains(l.SessionId))
                .Select(l => new { l.SessionId, l.PersonaId, l.ModelType, l.CreatedAt })
                .ToListAsync();
            var latestMeta = metaRows
                .GroupBy(m => m.SessionId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First());

            // 角色名稱（角色已刪除時查不到 → 留空，僅顯示模型）
            var personaIds = latestMeta.Values
                .Where(m => m.PersonaId.HasValue).Select(m => m.PersonaId!.Value).Distinct().ToList();
            var personaNames = personaIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _db.Personas.AsNoTracking()
                    .Where(p => personaIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Name);

            var items = rows.Select(r =>
            {
                latestMeta.TryGetValue(r.SessionId, out var meta);
                string? personaName = meta?.PersonaId is Guid pid && personaNames.TryGetValue(pid, out var pn) ? pn : null;
                return new PublicChatConversationSummary
                {
                    SessionId = r.SessionId,
                    AccessCodeText = r.AccessCodeText,
                    AccessCodeLabel = r.AccessCodeId.HasValue && labelMap.TryGetValue(r.AccessCodeId.Value, out var lbl) ? lbl : null,
                    PersonaName = personaName,
                    ModelType = meta?.ModelType,
                    MessageCount = r.MessageCount,
                    TotalTokens = r.TotalTokens,
                    FirstMessageAt = r.FirstMessageAt,
                    LastMessageAt = r.LastMessageAt,
                };
            }).ToList();

            return new PagedResponse<PublicChatConversationSummary>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
            };
        }

        // ── 後台查詢：對話訊息明細 ──────────────────────────────────────────────
        public async Task<PagedResponse<PublicChatLogItem>> GetMessagesAsync(PublicChatMessageQueryParams query)
        {
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);

            // 未指定 session 則回空集合（不全表掃描）
            if (string.IsNullOrWhiteSpace(query.SessionId))
            {
                return new PagedResponse<PublicChatLogItem> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
            }

            var q = _db.PublicChatLogs.AsNoTracking().Where(l => l.SessionId == query.SessionId);

            var total = await q.CountAsync();

            var raw = await q
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id,
                    l.Role,
                    l.Content,
                    l.PersonaId,
                    l.ModelType,
                    l.TokensIn,
                    l.TokensOut,
                    l.CreatedAt,
                })
                .ToListAsync();

            // 每則訊息當下的角色名稱（角色已刪除時查不到 → 留空）
            var personaIds = raw.Where(r => r.PersonaId.HasValue).Select(r => r.PersonaId!.Value).Distinct().ToList();
            var personaNames = personaIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _db.Personas.AsNoTracking()
                    .Where(p => personaIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Name);

            var items = raw.Select(r => new PublicChatLogItem
            {
                Id = r.Id,
                Role = r.Role,
                Content = r.Content,
                PersonaName = r.PersonaId is Guid pid && personaNames.TryGetValue(pid, out var pn) ? pn : null,
                ModelType = r.ModelType,
                TokensIn = r.TokensIn,
                TokensOut = r.TokensOut,
                CreatedAt = r.CreatedAt,
            }).ToList();

            return new PagedResponse<PublicChatLogItem>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
            };
        }

        // ── 前台：訪客取回自己的歷史對話 ────────────────────────────────────────
        public async Task<List<PublicChatHistoryItem>> GetHistoryAsync(
            string? accessCode, string? sessionId, Guid? personaId, CancellationToken cancellationToken)
        {
            if (personaId is null) return [];

            var q = _db.PublicChatLogs.AsNoTracking().Where(l => l.PersonaId == personaId);

            // 一人一碼：有碼以碼識別（跨裝置），否則退回 sessionId（同瀏覽器）
            if (!string.IsNullOrWhiteSpace(accessCode))
                q = q.Where(l => l.AccessCodeText == accessCode);
            else if (!string.IsNullOrWhiteSpace(sessionId))
                q = q.Where(l => l.SessionId == sessionId);
            else
                return [];

            // 取最近 100 則後轉為時間升序（由舊到新）
            var rows = await q
                .OrderByDescending(l => l.CreatedAt)
                .Take(100)
                .Select(l => new PublicChatHistoryItem { Role = l.Role, Content = l.Content, CreatedAt = l.CreatedAt })
                .ToListAsync(cancellationToken);
            rows.Reverse();
            return rows;
        }

        // ── 逾期清除 ────────────────────────────────────────────────────────────
        public async Task<int> PurgeExpiredAsync(int retentionDays, CancellationToken cancellationToken)
        {
            if (retentionDays <= 0) return 0; // 0 = 永久保留

            var cutoff = DateTime.Now.AddDays(-retentionDays);
            return await _db.PublicChatLogs
                .Where(l => l.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}