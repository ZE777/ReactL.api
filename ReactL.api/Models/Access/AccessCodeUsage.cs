using ReactL.api.Models.Base;

namespace ReactL.api.Models.Access
{
    /// <summary>
    /// 存取碼每日用量彙總，按（存取碼、日期）為組合唯一鍵。
    /// 每次公開聊天完成後以 UPSERT 累加，用於每日 token 配額檢查。
    /// 只新增與更新，不刪除（除非連帶刪除存取碼）。
    /// </summary>
    public class AccessCodeUsage : BaseEntity
    {
        /// <summary>所屬存取碼</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → AccessCodes ON DELETE CASCADE</remarks>
        public Guid AccessCodeId { get; set; }

        /// <summary>統計日期（僅日期，台灣時間 CST+8）</summary>
        /// <remarks>date · NOT NULL · UNIQUE 複合 (AccessCodeId, Date)</remarks>
        public DateOnly Date { get; set; }

        /// <summary>當日累計輸入 Token 數</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensIn { get; set; } = 0;

        /// <summary>當日累計輸出 Token 數</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensOut { get; set; } = 0;

        /// <summary>當日請求次數</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int RequestCount { get; set; } = 0;

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public AccessCode AccessCode { get; set; } = null!;
    }
}
