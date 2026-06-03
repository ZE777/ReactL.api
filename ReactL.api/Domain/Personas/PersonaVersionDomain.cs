namespace ReactL.api.Domain.Personas
{
    /// <summary>Persona 版本快照業務物件</summary>
    public class PersonaVersionDomain
    {
        /// <summary>版本快照唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>所屬 Persona ID</summary>
        public Guid PersonaId { get; set; }

        /// <summary>版本號（從 1 開始遞增）</summary>
        public int Version { get; set; }

        /// <summary>此版本的 System Prompt 內容</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>此版本的 Prompt Builder 各區塊 JSON</summary>
        public string? PromptSections { get; set; }

        /// <summary>此版本的修改說明</summary>
        public string? ChangeNote { get; set; }

        /// <summary>版本快照建立時間</summary>
        public DateTime CreatedAt { get; set; }
    }
}
