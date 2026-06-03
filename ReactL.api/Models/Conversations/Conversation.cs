using ReactL.api.Models.Auth;
using ReactL.api.Models.Base;
using ReactL.api.Models.Personas;

namespace ReactL.api.Models.Conversations
{
    /// <summary>對話記錄，由管理後台發起的測試對話</summary>
    public class Conversation : SoftDeletableEntity
    {
        /// <summary>對話所屬使用者</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → Users</remarks>
        public Guid UserId { get; set; }

        /// <summary>
        /// 使用的 Persona ID，null 表示未指定角色
        /// 刪除 Persona 時此欄位設為 null
        /// </summary>
        /// <remarks>uniqueidentifier · NULL · FK → Personas ON DELETE SET NULL</remarks>
        public Guid? PersonaId { get; set; }

        /// <summary>對話標題，預設「新對話」，可由使用者修改或由 AI 自動生成</summary>
        /// <remarks>nvarchar(200) · NOT NULL · DEFAULT '新對話'</remarks>
        public string Title { get; set; } = "新對話";

        /// <summary>此對話使用的 AI 模型識別碼</summary>
        /// <remarks>nvarchar(50) · NOT NULL</remarks>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>是否釘選，釘選的對話在列表排序中優先顯示</summary>
        /// <remarks>bit · NOT NULL · DEFAULT 0</remarks>
        public bool IsPinned { get; set; } = false;

        /// <summary>是否公開分享，true 時可透過 ShareSlug 讓他人讀取</summary>
        /// <remarks>bit · NOT NULL · DEFAULT 0</remarks>
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// 公開分享的短碼，例如 "a3b9x"
        /// IsPublic = true 時由系統生成，用於 GET /conversations/share/{slug} 端點
        /// </summary>
        /// <remarks>nvarchar(20) · NULL · UNIQUE FILTERED（僅 IS NOT NULL 時生效）</remarks>
        public string? ShareSlug { get; set; }

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public User User { get; set; } = null!;
        public Persona? Persona { get; set; }
        public ICollection<Message> Messages { get; set; } = [];
    }
}