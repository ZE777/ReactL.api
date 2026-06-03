namespace ReactL.api.Models.Base
{
    /// <summary>
    /// 所有 Entity 的最基底類別，提供主鍵與建立時間
    /// 適用於不需要修改追蹤的唯讀記錄（如 Messages、PersonaVersions、TokenUsageStats）
    /// </summary>
    public abstract class BaseEntity
    {
        /// <summary>
        /// 主鍵，使用 GUID 確保分散式環境唯一性
        /// 由應用程式層產生（EF Core 預設行為），避免依賴資料庫自增
        /// </summary>
        /// <remarks>uniqueidentifier · NOT NULL · 主鍵PK</remarks>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>建立時間，由 AppDbContext.SaveChangesAsync 統一設定為 UTC 時間</summary>
        /// <remarks>datetime2 · NOT NULL</remarks>
        public DateTime CreatedAt { get; set; }
    }
}
