using ReactL.api.Models.Auth;
using ReactL.api.Models.Base;

namespace ReactL.api.Models.PromptTemplates
{
    /// <summary>可複用的 Prompt 模板，供使用者在撰寫 Persona 或對話時快速套用</summary>
    public class PromptTemplate : SoftDeletableEntity
    {
        /// <summary>建立此模板的使用者</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → Users</remarks>
        public Guid UserId { get; set; }

        /// <summary>模板標題，用於列表顯示與搜尋</summary>
        /// <remarks>nvarchar(200) · NOT NULL</remarks>
        public string Title { get; set; } = string.Empty;

        /// <summary>模板完整內容</summary>
        /// <remarks>nvarchar(max) · NOT NULL</remarks>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 分類，用於前端篩選
        /// 有效值：'寫作'、'程式'、'翻譯'、'其他'
        /// </summary>
        /// <remarks>nvarchar(50) · NOT NULL · DEFAULT '其他'</remarks>
        public string Category { get; set; } = "其他";

        /// <summary>
        /// 標籤，以逗號分隔的字串儲存，例如 "精簡,技術,英文"
        /// 標籤量少（通常 1-5 個），不另建關聯表以降低查詢複雜度
        /// </summary>
        /// <remarks>nvarchar(500) · NULL</remarks>
        public string? Tags { get; set; }

        /// <summary>被使用次數，每次使用者套用此模板時 +1，用於熱門排序</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int UsageCount { get; set; } = 0;

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public User User { get; set; } = null!;
    }
}
