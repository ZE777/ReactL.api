using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Personas
{
    // ── 列表與詳情回應 ────────────────────────────────────────────────────────

    /// <summary>Persona 列表項目（輕量版，不含完整 SystemPrompt）</summary>
    public class PersonaListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Emoji { get; set; }
        public int CurrentVersion { get; set; }
        public bool IsBuiltin { get; set; }
        public Guid? UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Persona 詳情（含完整 SystemPrompt 和 PromptSections）</summary>
    public class PersonaDetailResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Emoji { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public string? PromptSections { get; set; }
        public int CurrentVersion { get; set; }
        public bool IsBuiltin { get; set; }
        public Guid? UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>版本快照摘要（含 SystemPrompt 供列表預覽）</summary>
    public class PersonaVersionItem
    {
        public Guid Id { get; set; }
        public int Version { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public string? ChangeNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>版本快照詳情（含完整 Prompt 內容，用於回滾預覽）</summary>
    public class PersonaVersionDetailResponse
    {
        public Guid Id { get; set; }
        public int Version { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public string? PromptSections { get; set; }
        public string? ChangeNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ── 寫入請求 ─────────────────────────────────────────────────────────────

    /// <summary>建立 Persona 請求</summary>
    public class CreatePersonaRequest
    {
        [Required(ErrorMessage = "角色名稱為必填")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Emoji { get; set; }

        [Required(ErrorMessage = "System Prompt 為必填")]
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>Prompt Builder 各區塊 JSON，可為 null（純文字模式）</summary>
        public string? PromptSections { get; set; }
    }

    /// <summary>更新 Persona 請求（同時產生版本快照）</summary>
    public class UpdatePersonaRequest
    {
        [Required(ErrorMessage = "角色名稱為必填")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Emoji { get; set; }

        [Required(ErrorMessage = "System Prompt 為必填")]
        public string SystemPrompt { get; set; } = string.Empty;

        public string? PromptSections { get; set; }

        /// <summary>此次修改說明，寫入 PersonaVersions.ChangeNote</summary>
        [MaxLength(500)]
        public string? ChangeNote { get; set; }
    }

    /// <summary>Prompt 各區塊欄位結構，用於 AI 強化的輸入與輸出</summary>
    public class PromptSectionsDto
    {
        /// <summary>角色定義</summary>
        public string? Role { get; set; }

        /// <summary>背景說明</summary>
        public string? Background { get; set; }

        /// <summary>任務範圍</summary>
        public string? Task { get; set; }

        /// <summary>回應格式</summary>
        public string? Format { get; set; }

        /// <summary>行為限制</summary>
        public string? Constraints { get; set; }

        /// <summary>範例</summary>
        public string? Examples { get; set; }
    }

    /// <summary>AI 強化 Prompt 請求（以各區塊傳入，AI 個別強化後回傳）</summary>
    public class EnhancePromptRequest
    {
        [Required]
        public PromptSectionsDto Sections { get; set; } = new();

        /// <summary>強化方向提示，例如「讓回應更簡潔」、「增加繁體中文約束」</summary>
        [MaxLength(500)]
        public string? Instruction { get; set; }
    }

    /// <summary>AI 強化 Prompt 回應（各區塊分別回傳，不會自動存入 DB）</summary>
    public class EnhancePromptResponse
    {
        /// <summary>AI 強化後各區塊的結果</summary>
        public PromptSectionsDto Sections { get; set; } = new();
    }
}
