namespace ReactL.api.Models.Base
{
    /// <summary>
    /// 可修改 Entity 的基底類別，繼承 BaseEntity 並加入最後修改時間
    /// 適用於有 CRUD 操作的主要資源（User、Conversation 等）
    /// </summary>
    public abstract class AuditableEntity : BaseEntity
    {
        /// <summary>最後更新時間，由 AppDbContext.SaveChangesAsync 於每次修改時自動更新為 UTC</summary>
        /// <remarks>datetime2 · NOT NULL</remarks>
        public DateTime UpdatedAt { get; set; }
    }
}