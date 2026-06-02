namespace ReactL.api.Models.Base
{
    /// <summary>
    /// 支援軟刪除的 Entity 基底類別，繼承 AuditableEntity 並加入刪除旗標
    /// 適用於需要保留歷史稽核記錄的資源（Persona、BotBinding、Conversation 等）
    /// AppDbContext 會為這類 Entity 套用 Global Query Filter（!IsDeleted），查詢時自動排除已刪除資料
    /// 如需查詢已刪除資料，在查詢鏈加上 .IgnoreQueryFilters()
    /// </summary>
    public abstract class SoftDeletableEntity : AuditableEntity
    {
        /// <summary>軟刪除旗標，true 表示已刪除但保留資料庫記錄</summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>刪除時間，執行軟刪除時由 Service 層設定</summary>
        public DateTime? DeletedAt { get; set; }
    }
}
