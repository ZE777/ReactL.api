using ReactL.api.Models.Auth;
using ReactL.api.Models.Base;
using ReactL.api.Models.BotBindings;
using ReactL.api.Models.Conversations;

namespace ReactL.api.Models.Personas
{
    /// <summary>
    /// AI 角色（Persona），定義 AI 的性格、背景和行為規則
    /// 支援版本控制：每次修改都會在 PersonaVersions 建立快照，可回滾至任意版本
    /// </summary>
    public class Persona : SoftDeletableEntity
    {
        /// <summary>
        /// 所屬使用者 ID，null 表示系統內建角色
        /// 系統內建角色對所有使用者可見，不可刪除（IsBuiltin = true）
        /// </summary>
        /// <remarks>uniqueidentifier · NULL · FK → Users ON DELETE SET NULL</remarks>
        public Guid? UserId { get; set; }

        /// <summary>角色名稱</summary>
        /// <remarks>nvarchar(100) · NOT NULL</remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>前台顯示用的 Emoji 圖示，例如 "🤖"</summary>
        /// <remarks>nvarchar(10) · NULL</remarks>
        public string? Emoji { get; set; }

        /// <summary>
        /// 當前使用中的完整 System Prompt
        /// 此欄位為最新版本的內容，歷史快照存於 PersonaVersions
        /// </summary>
        /// <remarks>nvarchar(max) · NOT NULL</remarks>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Prompt Builder 各區塊的原始內容，以 JSON 格式儲存
        /// 結構：{ role, background, task, format, constraints, examples }
        /// </summary>
        /// <remarks>nvarchar(max) · NULL</remarks>
        public string? PromptSections { get; set; }

        /// <summary>當前版本號，每次 PATCH 後遞增，對應 PersonaVersions 中的最新版本</summary>
        /// <remarks>int · NOT NULL · DEFAULT 1</remarks>
        public int CurrentVersion { get; set; } = 1;

        /// <summary>是否為系統內建角色，true 時不可刪除</summary>
        /// <remarks>bit · NOT NULL · DEFAULT 0</remarks>
        public bool IsBuiltin { get; set; } = false;

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public User? User { get; set; }
        public ICollection<PersonaVersion> Versions { get; set; } = [];
        public ICollection<BotBinding> BotBindings { get; set; } = [];
        public ICollection<Conversation> Conversations { get; set; } = [];
    }
}