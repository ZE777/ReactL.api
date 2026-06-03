using ReactL.api.Models.Auth;
using ReactL.api.Models.Base;

namespace ReactL.api.Models.Stats
{
    /// <summary>
    /// Token 使用量每日彙總統計，按（使用者、日期、模型、來源）為組合唯一鍵
    /// 每次 AI 呼叫完成後以 UPSERT 方式更新：存在則累加，不存在則新增
    /// 此表為分析資料，只新增與更新，不刪除
    /// </summary>
    public class TokenUsageStat : BaseEntity
    {
        /// <summary>統計所屬使用者</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → Users</remarks>
        public Guid UserId { get; set; }

        /// <summary>統計日期（僅日期，不含時間），以 UTC 日期為準</summary>
        /// <remarks>date · NOT NULL · UNIQUE 複合 (UserId, Date, ModelType, Source)</remarks>
        public DateOnly Date { get; set; }

        /// <summary>使用的 AI 模型識別碼，例如 "gpt-4o-mini"</summary>
        /// <remarks>nvarchar(50) · NOT NULL · UNIQUE 複合 (UserId, Date, ModelType, Source)</remarks>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>Token 消耗來源，參見 Common.Constants.TokenSource 常數</summary>
        /// <remarks>nvarchar(20) · NOT NULL · UNIQUE 複合 (UserId, Date, ModelType, Source)</remarks>
        public string Source { get; set; } = string.Empty;

        /// <summary>當日此模型此來源的總輸入 Token 數（累計值）</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensIn { get; set; } = 0;

        /// <summary>當日此模型此來源的總輸出 Token 數（累計值）</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int TokensOut { get; set; } = 0;

        /// <summary>當日此模型此來源的 AI 請求次數</summary>
        /// <remarks>int · NOT NULL · DEFAULT 0</remarks>
        public int RequestCount { get; set; } = 0;

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public User User { get; set; } = null!;
    }
}
