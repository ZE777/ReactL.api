using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Exceptions;
using ReactL.api.Data;
using ReactL.api.Domain.PromptTemplates;
using ReactL.api.DTOs.Requests.PromptTemplates;
using ReactL.api.Models.PromptTemplates;

namespace ReactL.api.Services.PromptTemplates
{
    /// <summary>Prompt 模板服務實作</summary>
    public class PromptTemplateService : IPromptTemplateService
    {
        private readonly AppDbContext _db;

        public PromptTemplateService(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>取得模板清單（支援分類篩選與關鍵字搜尋）</summary>
        public async Task<List<PromptTemplateDomain>> GetListAsync(Guid userId, PromptTemplateQueryParams query)
        {
            var q = _db.PromptTemplates
                .AsNoTracking()
                .Where(t => t.UserId == userId);

            if (!string.IsNullOrWhiteSpace(query.Category))
                q = q.Where(t => t.Category == query.Category);

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim();
                q = q.Where(t => t.Title.Contains(kw) ||
                                 (t.Tags != null && t.Tags.Contains(kw)));
            }

            return await q
                .OrderByDescending(t => t.UsageCount)
                .ThenByDescending(t => t.UpdatedAt)
                .Select(t => new PromptTemplateDomain
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    Title = t.Title,
                    Content = t.Content,
                    Category = t.Category,
                    Tags = t.Tags,
                    UsageCount = t.UsageCount,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                })
                .ToListAsync();
        }

        /// <summary>取得模板詳情</summary>
        public async Task<PromptTemplateDomain> GetByIdAsync(Guid id, Guid userId)
        {
            var t = await _db.PromptTemplates
                .AsNoTracking()
                .Where(t => t.Id == id && t.UserId == userId)
                .Select(t => new PromptTemplateDomain
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    Title = t.Title,
                    Content = t.Content,
                    Category = t.Category,
                    Tags = t.Tags,
                    UsageCount = t.UsageCount,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return t ?? throw new NotFoundException("PromptTemplate", id);
        }

        /// <summary>建立模板</summary>
        public async Task<PromptTemplateDomain> CreateAsync(Guid userId, CreatePromptTemplateRequest request)
        {
            var template = new PromptTemplate
            {
                UserId = userId,
                Title = request.Title,
                Content = request.Content,
                Category = request.Category,
                Tags = request.Tags
            };

            _db.PromptTemplates.Add(template);
            await _db.SaveChangesAsync();
            return await GetByIdAsync(template.Id, userId);
        }

        /// <summary>更新模板</summary>
        public async Task<PromptTemplateDomain> UpdateAsync(Guid id, Guid userId, UpdatePromptTemplateRequest request)
        {
            var template = await GetOwnedAsync(id, userId);

            template.Title = request.Title;
            template.Content = request.Content;
            template.Category = request.Category;
            template.Tags = request.Tags;

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        /// <summary>軟刪除模板</summary>
        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var template = await GetOwnedAsync(id, userId);
            template.IsDeleted = true;
            template.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        /// <summary>記錄模板使用次數 +1（前端套用模板到對話框時呼叫）</summary>
        public async Task IncrementUsageAsync(Guid id, Guid userId)
        {
            var template = await GetOwnedAsync(id, userId);
            template.UsageCount += 1;
            await _db.SaveChangesAsync();
        }

        /// <summary>取得使用者有所有權的 PromptTemplate（已追蹤，可直接修改）</summary>
        private async Task<PromptTemplate> GetOwnedAsync(Guid id, Guid userId)
        {
            var template = await _db.PromptTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId)
                ?? throw new NotFoundException("PromptTemplate", id);
            return template;
        }
    }
}
