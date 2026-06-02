using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Exceptions;
using ReactL.api.Data;
using ReactL.api.DTOs.PromptTemplates;
using ReactL.api.Models.PromptTemplates;

namespace ReactL.api.Services.PromptTemplates
{
    public class PromptTemplateService : IPromptTemplateService
    {
        private readonly AppDbContext _db;

        public PromptTemplateService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<PromptTemplateListItem>> GetListAsync(Guid userId, PromptTemplateQueryParams query)
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
                .Select(t => new PromptTemplateListItem
                {
                    Id = t.Id,
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

        public async Task<PromptTemplateDetailResponse> GetByIdAsync(Guid id, Guid userId)
        {
            var t = await _db.PromptTemplates
                .AsNoTracking()
                .Where(t => t.Id == id && t.UserId == userId)
                .Select(t => new PromptTemplateDetailResponse
                {
                    Id = t.Id,
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

        public async Task<PromptTemplateDetailResponse> CreateAsync(Guid userId, CreatePromptTemplateRequest request)
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

        public async Task<PromptTemplateDetailResponse> UpdateAsync(Guid id, Guid userId, UpdatePromptTemplateRequest request)
        {
            var template = await GetOwnedAsync(id, userId);

            template.Title = request.Title;
            template.Content = request.Content;
            template.Category = request.Category;
            template.Tags = request.Tags;

            await _db.SaveChangesAsync();
            return await GetByIdAsync(id, userId);
        }

        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var template = await GetOwnedAsync(id, userId);
            template.IsDeleted = true;
            template.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task IncrementUsageAsync(Guid id, Guid userId)
        {
            var template = await GetOwnedAsync(id, userId);
            template.UsageCount += 1;
            await _db.SaveChangesAsync();
        }

        private async Task<PromptTemplate> GetOwnedAsync(Guid id, Guid userId)
        {
            var template = await _db.PromptTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId)
                ?? throw new NotFoundException("PromptTemplate", id);
            return template;
        }
    }
}
