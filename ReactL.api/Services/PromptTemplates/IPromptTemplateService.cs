using ReactL.api.DTOs.PromptTemplates;

namespace ReactL.api.Services.PromptTemplates
{
    public interface IPromptTemplateService
    {
        Task<List<PromptTemplateListItem>> GetListAsync(Guid userId, PromptTemplateQueryParams query);
        Task<PromptTemplateDetailResponse> GetByIdAsync(Guid id, Guid userId);
        Task<PromptTemplateDetailResponse> CreateAsync(Guid userId, CreatePromptTemplateRequest request);
        Task<PromptTemplateDetailResponse> UpdateAsync(Guid id, Guid userId, UpdatePromptTemplateRequest request);
        Task DeleteAsync(Guid id, Guid userId);

        /// <summary>記錄使用次數 +1（前端套用模板到對話框時呼叫）</summary>
        Task IncrementUsageAsync(Guid id, Guid userId);
    }
}
