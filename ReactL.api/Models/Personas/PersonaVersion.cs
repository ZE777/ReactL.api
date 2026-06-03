using ReactL.api.Models.Base;

namespace ReactL.api.Models.Personas
{
    /// <summary>
    /// Persona 版本快照，每次修改 Persona 時建立一筆版本記錄
    /// 版本為唯讀，只新增不修改；回滾時以舊版本內容建立新版本（不覆寫歷史）
    /// </summary>
    public class PersonaVersion : BaseEntity
    {
        /// <summary>所屬 Persona 的 ID，設定 CASCADE DELETE：刪除 Persona 時自動清除所有版本</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → Personas ON DELETE CASCADE</remarks>
        public Guid PersonaId { get; set; }

        /// <summary>版本號，從 1 開始遞增，與 Persona.CurrentVersion 中的對應版本相符</summary>
        /// <remarks>int · NOT NULL · UNIQUE 複合 (PersonaId, Version)</remarks>
        public int Version { get; set; }

        /// <summary>此版本的完整 System Prompt 快照</summary>
        /// <remarks>nvarchar(max) · NOT NULL</remarks>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>此版本的 Prompt Builder 各區塊原始內容快照（JSON）</summary>
        /// <remarks>nvarchar(max) · NULL</remarks>
        public string? PromptSections { get; set; }

        /// <summary>使用者輸入的此次修改說明，例如「調整回覆語氣」</summary>
        /// <remarks>nvarchar(500) · NULL</remarks>
        public string? ChangeNote { get; set; }

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public Persona Persona { get; set; } = null!;
    }
}