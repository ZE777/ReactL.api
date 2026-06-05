namespace ReactL.api.Domain.Personas
{
    /// <summary>AI 角色業務物件（含完整 Prompt 內容）</summary>
    public class PersonaDomain
    {
        /// <summary>Persona 唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>所屬使用者 ID，Official Persona 指向系統用戶</summary>
        public Guid UserId { get; set; }

        /// <summary>'Official' = 系統內建 | 'User' = 使用者自訂</summary>
        public string BuiltinGroup { get; set; } = "User";

        /// <summary>Persona 顯示名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Persona 代表 Emoji</summary>
        public string? Emoji { get; set; }

        /// <summary>AI 角色的 System Prompt 完整內容</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>Prompt Builder 各區塊 JSON</summary>
        public string? PromptSections { get; set; }

        /// <summary>目前版本號，每次更新後遞增</summary>
        public int CurrentVersion { get; set; }

        /// <summary>true = 開放前台訪客選用此角色</summary>
        public bool IsBuiltin { get; set; }

        /// <summary>Persona 建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Persona 最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }
}
