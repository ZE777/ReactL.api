using ReactL.api.Domain.Personas;
using ReactL.api.DTOs.Requests.Personas;
using ReactL.api.DTOs.Responses.Personas;

namespace ReactL.api.Services.Personas
{
    /// <summary>Persona 服務介面</summary>
    public interface IPersonaService
    {
        /// <summary>取得可用的 Persona 清單（系統內建 + 本人自訂）</summary>
        Task<List<PersonaDomain>> GetListAsync(Guid userId);

        /// <summary>取得單筆 Persona 詳情</summary>
        Task<PersonaDomain> GetByIdAsync(Guid id, Guid userId);

        /// <summary>建立新 Persona（同時建立初始版本快照）</summary>
        Task<PersonaDomain> CreateAsync(Guid userId, CreatePersonaRequest request);

        /// <summary>更新 Persona（自動建立版本快照，版本號遞增）</summary>
        Task<PersonaDomain> UpdateAsync(Guid id, Guid userId, UpdatePersonaRequest request);

        /// <summary>軟刪除 Persona，系統內建不可刪除</summary>
        Task DeleteAsync(Guid id, Guid userId);

        /// <summary>取得指定 Persona 的版本快照清單</summary>
        Task<List<PersonaVersionDomain>> GetVersionsAsync(Guid personaId, Guid userId);

        /// <summary>取得單一版本快照詳情</summary>
        Task<PersonaVersionDomain> GetVersionDetailAsync(Guid personaId, Guid versionId, Guid userId);

        /// <summary>回滾至指定版本（回滾本身也會產生新版本快照）</summary>
        Task<PersonaDomain> RollbackAsync(Guid personaId, Guid versionId, Guid userId);

        /// <summary>取得開放前台顯示的 Persona 清單（isBuiltin=true）</summary>
        Task<List<PersonaDomain>> GetPublicPersonasAsync();

        /// <summary>
        /// AI 強化 System Prompt（預覽用，不修改 DB）
        /// Request / Response 直接用 DTO 型別，因為是 AI 協議物件
        /// </summary>
        Task<EnhancePromptResponse> EnhancePromptAsync(EnhancePromptRequest request);
    }
}
