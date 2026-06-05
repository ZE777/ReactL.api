namespace ReactL.api.DTOs.Responses.Personas
{
    /// <summary>Persona 列表項目（輕量版，不含完整 SystemPrompt）</summary>
    public class PersonaListItem
    {
        /// <summary>Persona 唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>Persona 顯示名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Persona 代表 Emoji</summary>
        public string? Emoji { get; set; }

        /// <summary>目前版本號</summary>
        public int CurrentVersion { get; set; }

        /// <summary>是否開放前台訪客選用</summary>
        public bool IsBuiltin { get; set; }

        /// <summary>'Official' = 系統內建 | 'User' = 使用者自訂</summary>
        public string BuiltinGroup { get; set; } = "User";

        /// <summary>所屬使用者 ID</summary>
        public Guid UserId { get; set; }

        /// <summary>Persona 建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Persona 最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Persona 詳情（含完整 SystemPrompt 和 PromptSections）</summary>
    public class PersonaDetailResponse
    {
        /// <summary>Persona 唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>Persona 顯示名稱</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Persona 代表 Emoji</summary>
        public string? Emoji { get; set; }

        /// <summary>AI 角色的 System Prompt 完整內容</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>Prompt Builder 各區塊 JSON</summary>
        public string? PromptSections { get; set; }

        /// <summary>目前版本號</summary>
        public int CurrentVersion { get; set; }

        /// <summary>是否開放前台訪客選用</summary>
        public bool IsBuiltin { get; set; }

        /// <summary>'Official' = 系統內建 | 'User' = 使用者自訂</summary>
        public string BuiltinGroup { get; set; } = "User";

        /// <summary>所屬使用者 ID</summary>
        public Guid UserId { get; set; }

        /// <summary>Persona 建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Persona 最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>版本快照摘要（含 SystemPrompt 供列表預覽）</summary>
    public class PersonaVersionItem
    {
        /// <summary>版本快照唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>版本號</summary>
        public int Version { get; set; }

        /// <summary>此版本的 System Prompt 內容</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>此版本的修改說明</summary>
        public string? ChangeNote { get; set; }

        /// <summary>版本快照建立時間</summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>版本快照詳情（含完整 Prompt 內容，用於回滾預覽）</summary>
    public class PersonaVersionDetailResponse
    {
        /// <summary>版本快照唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>版本號</summary>
        public int Version { get; set; }

        /// <summary>此版本的 System Prompt 完整內容</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>此版本的 Prompt Builder 各區塊 JSON</summary>
        public string? PromptSections { get; set; }

        /// <summary>此版本的修改說明</summary>
        public string? ChangeNote { get; set; }

        /// <summary>版本快照建立時間</summary>
        public DateTime CreatedAt { get; set; }
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

    /// <summary>AI 強化 Prompt 回應（各區塊分別回傳，不會自動存入 DB）</summary>
    public class EnhancePromptResponse
    {
        /// <summary>AI 強化後各區塊的結果</summary>
        public PromptSectionsDto Sections { get; set; } = new();
    }
}
