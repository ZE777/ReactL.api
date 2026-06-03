using System.ComponentModel.DataAnnotations;
using ReactL.api.DTOs.Responses.Personas;

namespace ReactL.api.DTOs.Requests.Personas
{
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

        /// <summary>true = 開放前台訪客選用此角色</summary>
        public bool IsBuiltin { get; set; } = false;
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

        /// <summary>true = 開放前台訪客選用此角色</summary>
        public bool IsBuiltin { get; set; } = false;
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
}