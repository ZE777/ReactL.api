using ReactL.api.DTOs.Personas;

namespace ReactL.api.Services.Personas
{
    public interface IPersonaService
    {
        /// <summary>取得可用的 Persona 清單（系統內建 + 當前使用者的自訂）</summary>
        Task<List<PersonaListItem>> GetListAsync(Guid userId);

        /// <summary>取得單筆 Persona 詳情</summary>
        Task<PersonaDetailResponse> GetByIdAsync(Guid id, Guid userId);

        /// <summary>建立新 Persona，並自動建立版本 1 快照</summary>
        Task<PersonaDetailResponse> CreateAsync(Guid userId, CreatePersonaRequest request);

        /// <summary>更新 Persona，並將舊版本存入 PersonaVersions</summary>
        Task<PersonaDetailResponse> UpdateAsync(Guid id, Guid userId, UpdatePersonaRequest request);

        /// <summary>軟刪除 Persona（系統內建角色不可刪除）</summary>
        Task DeleteAsync(Guid id, Guid userId);

        /// <summary>取得指定 Persona 的所有版本快照清單</summary>
        Task<List<PersonaVersionItem>> GetVersionsAsync(Guid personaId, Guid userId);

        /// <summary>取得單一版本快照詳情</summary>
        Task<PersonaVersionDetailResponse> GetVersionDetailAsync(Guid personaId, Guid versionId, Guid userId);

        /// <summary>
        /// 回滾至指定版本
        /// 回滾也視為一次修改，會再建立新的版本快照（版本號遞增）
        /// </summary>
        Task<PersonaDetailResponse> RollbackAsync(Guid personaId, Guid versionId, Guid userId);

        /// <summary>呼叫 AI 對 System Prompt 進行強化建議（不修改 DB）</summary>
        Task<EnhancePromptResponse> EnhancePromptAsync(EnhancePromptRequest request);
    }
}
