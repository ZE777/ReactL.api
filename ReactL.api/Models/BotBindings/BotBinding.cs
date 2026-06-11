using ReactL.api.Models.Auth;
using ReactL.api.Models.Base;
using ReactL.api.Models.External;
using ReactL.api.Models.Personas;

namespace ReactL.api.Models.BotBindings
{
    /// <summary>
    /// Bot 平台綁定設定，記錄 LINE / Discord Bot 的連線資訊與對應的 AI Persona
    /// Token 和 ChannelSecret 以 AES-256-CBC 加密儲存，前端回應時只顯示後 4 碼
    /// </summary>
    public class BotBinding : SoftDeletableEntity
    {
        /// <summary>所屬使用者，Bot 只對建立者可見</summary>
        /// <remarks>uniqueidentifier · NOT NULL · FK → Users</remarks>
        public Guid UserId { get; set; }

        /// <summary>
        /// 使用的 Persona ID，null 表示不套用特定角色，使用預設行為
        /// 刪除 Persona 時此欄位設為 null（配合 EF Fluent API 設定 ON DELETE SET NULL）
        /// </summary>
        /// <remarks>uniqueidentifier · NULL · FK → Personas ON DELETE SET NULL</remarks>
        public Guid? PersonaId { get; set; }

        /// <summary>串接平台，參見 Common.Constants.Platform 常數</summary>
        /// <remarks>nvarchar(20) · NOT NULL</remarks>
        public string Platform { get; set; } = string.Empty;

        /// <summary>Bot 的顯示名稱，用於後台識別</summary>
        /// <remarks>nvarchar(100) · NOT NULL</remarks>
        public string BotName { get; set; } = string.Empty;

        /// <summary>
        /// AES-256-CBC 加密後的 Bot Token，以 Base64 字串儲存
        /// 解密由 AesEncryptionHelper 處理，Key 和 IV 來自 EncryptionSettings
        /// </summary>
        /// <remarks>nvarchar(700) · NOT NULL</remarks>
        public string BotTokenEncrypted { get; set; } = string.Empty;

        /// <summary>
        /// AES-256-CBC 加密後的 Channel Secret（LINE 平台專用）
        /// Discord 不需要此欄位
        /// </summary>
        /// <remarks>nvarchar(700) · NULL</remarks>
        public string? ChannelSecretEncrypted { get; set; }

        /// <summary>
        /// Token 明文的後 4 碼，寫入時從明文截取並儲存
        /// 避免列表查詢時 N 次解密，直接回傳此欄位給前端顯示（例如 "••••3f9a"）
        /// </summary>
        /// <remarks>nvarchar(4) · NOT NULL</remarks>
        public string TokenLastFour { get; set; } = string.Empty;

        /// <summary>使用的 AI 模型識別碼，例如 "gpt-4o-mini"、"gemini-2.0-flash"</summary>
        /// <remarks>nvarchar(50) · NOT NULL</remarks>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>Bot 啟用狀態，false 時 Webhook 收到訊息不會回應</summary>
        /// <remarks>bit · NOT NULL · DEFAULT 1</remarks>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 此 Bot 專用的 Webhook 基礎 URL（例如 https://api.yourdomain.com）
        /// null 表示沿用 AppSettings.BaseUrl 作為預設值
        /// 多個 Bot 部署在不同伺服器時，各自填入不同網址互不干擾
        /// </summary>
        /// <remarks>nvarchar(500) · NULL</remarks>
        public string? WebhookBaseUrl { get; set; }

        /// <summary>Discord Application ID（Discord 平台專用）</summary>
        /// <remarks>nvarchar(50) · NULL</remarks>
        public string? DiscordApplicationId { get; set; }

        /// <summary>Discord Application Public Key，用於 Ed25519 驗簽（Discord 平台專用）</summary>
        /// <remarks>nvarchar(100) · NULL</remarks>
        public string? DiscordPublicKey { get; set; }

        /// <summary>
        /// Bot 憑證/設定最近一次驗證是否通過（持久化，LINE 與 Discord 共用，供前端標示「設定無效」狀態）。
        /// - Discord：/chat 指令註冊成功 = Token / Application ID 有效
        /// - LINE：呼叫 GET /v2/bot/info 成功 = Channel Access Token 有效
        /// true=有效、false=無效（需修正後重新儲存）、null=尚未驗證。
        /// </summary>
        /// <remarks>bit · NULL</remarks>
        public bool? CredentialValid { get; set; }

        /// <summary>
        /// 信任系統成員名單，以 JSON 陣列儲存：
        /// [{ "id", "label", "tier", "systemRole", "grantedBy", "grantedAt" }]。
        /// systemRole=owner 的成員（可多人）才能維護名單；主人與信任者皆為此清單成員。
        /// 兩條維護路徑共用此欄位（後台 CRUD / Discord 對話）。null 或空字串視為空名單。
        /// </summary>
        /// <remarks>nvarchar(max) · NULL</remarks>
        public string? TrustedUsersJson { get; set; }

        // ── 導航屬性 ──────────────────────────────────────────────────────
        public User User { get; set; } = null!;
        public Persona? Persona { get; set; }
        public ICollection<ExternalMessage> ExternalMessages { get; set; } = [];
    }
}
