using ReactL.api.Domain.PromptTemplates;
using ReactL.api.DTOs.Requests.PromptTemplates;

namespace ReactL.api.Services.PromptTemplates
{
    /// <summary>Prompt 模板服務介面</summary>
    public interface IPromptTemplateService
    {
        /// <summary>取得使用者的模板清單（支援分類篩選與關鍵字搜尋）</summary>
        Task<List<PromptTemplateDomain>> GetListAsync(Guid userId, PromptTemplateQueryParams query);

        /// <summary>取得模板詳情</summary>
        Task<PromptTemplateDomain> GetByIdAsync(Guid id, Guid userId);

        /// <summary>建立模板</summary>
        Task<PromptTemplateDomain> CreateAsync(Guid userId, CreatePromptTemplateRequest request);

        /// <summary>更新模板</summary>
        Task<PromptTemplateDomain> UpdateAsync(Guid id, Guid userId, UpdatePromptTemplateRequest request);

        /// <summary>軟刪除模板</summary>
        Task DeleteAsync(Guid id, Guid userId);

        /// <summary>記錄使用次數 +1（前端套用模板到對話框時呼叫）</summary>
        Task IncrementUsageAsync(Guid id, Guid userId);
    }
}
