using ReactL.api.Models.Auth;
using ReactL.api.Models.Base;

namespace ReactL.api.Models.Ai
{
    /// <summary>
    /// AI 供應商 API 金鑰，採混合 BYOK 設計：
    /// - 系統預設 Key：UserId = 系統用戶、IsSystem = true，由啟動 seeder 從 appsettings 加密寫入，
    ///   作為公開聊天室與未自帶 Key 使用者的 fallback。
    /// - 使用者自帶 Key：UserId = 該使用者，覆蓋系統預設，隔離自己的額度。
    /// 金鑰以 AES-256-CBC 加密儲存（複用 AesEncryptionHelper），前端僅顯示後 4 碼。
    /// </summary>
    public class AiKey : AuditableEntity
    {
        /// <summary>所屬使用者；系統預設 Key 指向系統用戶</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → Users ON DELETE CASCADE</remarks>
        public Guid UserId { get; set; }

        /// <summary>供應商識別碼，對應 AiSettings.Providers[].Id（如 "groq"）</summary>
        /// <remarks>nvarchar(50) · NOT NULL</remarks>
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>AES-256-CBC 加密後的 API Key（Base64）</summary>
        /// <remarks>nvarchar(700) · NOT NULL</remarks>
        public string EncryptedKey { get; set; } = string.Empty;

        /// <summary>Key 明文後 4 碼，供前端顯示（例如 "••••a1b2"），避免列表逐筆解密</summary>
        /// <remarks>nvarchar(8) · NOT NULL</remarks>
        public string KeyLastFour { get; set; } = string.Empty;

        /// <summary>啟用狀態，false 時解析時略過此 Key</summary>
        /// <remarks>bit · NOT NULL · DEFAULT 1</remarks>
        public bool IsActive { get; set; } = true;

        /// <summary>是否為系統預設 Key（fallback 來源，不開放一般使用者管理）</summary>
        /// <remarks>bit · NOT NULL · DEFAULT 0</remarks>
        public bool IsSystem { get; set; } = false;

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public User User { get; set; } = null!;
    }
}