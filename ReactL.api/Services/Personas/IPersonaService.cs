using ReactL.api.Domain.Personas;
using ReactL.api.DTOs.Requests.Personas;
using ReactL.api.DTOs.Responses.Personas;

namespace ReactL.api.Services.Personas
{
    /// <summary>Persona 服務介面</summary>
    public interface IPersonaService
    {
        /// <summary>取得可用的 Persona 清單。isAdmin=true 時含系統內建（Official）+ 本人；非 Admin 僅本人自訂（內建角色僅限 Admin）</summary>
        Task<List<PersonaDomain>> GetListAsync(Guid userId, bool isAdmin);

        /// <summary>取得單筆 Persona 詳情。內建（Official）僅 Admin 可存取</summary>
        Task<PersonaDomain> GetByIdAsync(Guid id, Guid userId, bool isAdmin);

        /// <summary>建立新 Persona（同時建立初始版本快照）。isAdmin=false 時忽略 IsBuiltin 與 ModelType（公開於前台、前台模型僅 Admin 可設）</summary>
        Task<PersonaDomain> CreateAsync(Guid userId, CreatePersonaRequest request, bool isAdmin);

        /// <summary>更新 Persona（自動建立版本快照，版本號遞增）。isAdmin=false 時不更動 IsBuiltin 與 ModelType（公開於前台、前台模型僅 Admin 可設）</summary>
        Task<PersonaDomain> UpdateAsync(Guid id, Guid userId, UpdatePersonaRequest request, bool isAdmin);

        /// <summary>軟刪除 Persona，系統內建不可刪除（內建僅 Admin 可存取）</summary>
        Task DeleteAsync(Guid id, Guid userId, bool isAdmin);

        /// <summary>取得指定 Persona 的版本快照清單（內建僅 Admin 可存取）</summary>
        Task<List<PersonaVersionDomain>> GetVersionsAsync(Guid personaId, Guid userId, bool isAdmin);

        /// <summary>取得單一版本快照詳情（內建僅 Admin 可存取）</summary>
        Task<PersonaVersionDomain> GetVersionDetailAsync(Guid personaId, Guid versionId, Guid userId, bool isAdmin);

        /// <summary>回滾至指定版本（回滾本身也會產生新版本快照；內建僅 Admin 可存取）</summary>
        Task<PersonaDomain> RollbackAsync(Guid personaId, Guid versionId, Guid userId, bool isAdmin);

        /// <summary>取得開放前台顯示的 Persona 清單（isBuiltin=true）</summary>
        Task<List<PersonaDomain>> GetPublicPersonasAsync();

        /// <summary>
        /// AI 強化 System Prompt（預覽用，不修改 DB）
        /// Request / Response 直接用 DTO 型別，因為是 AI 協議物件
        /// </summary>
        Task<EnhancePromptResponse> EnhancePromptAsync(Guid userId, EnhancePromptRequest request);
    }
}
