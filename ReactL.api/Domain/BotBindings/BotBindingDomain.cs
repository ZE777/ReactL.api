namespace ReactL.api.Domain.BotBindings
{
    /// <summary>Bot 綁定業務物件（不含加密 Token 原文，只有後 4 碼）</summary>
    public class BotBindingDomain
    {
        /// <summary>Bot 綁定唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>所屬使用者 ID</summary>
        public Guid UserId { get; set; }

        /// <summary>指定的 Persona ID，null 表示未指定角色</summary>
        public Guid? PersonaId { get; set; }

        /// <summary>綁定的 Persona 名稱（JOIN 取得）</summary>
        public string? PersonaName { get; set; }

        /// <summary>平台：line / discord</summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>Bot 顯示名稱</summary>
        public string BotName { get; set; } = string.Empty;

        /// <summary>Token 後 4 碼，供前端顯示（例如 "••••3f9a"）</summary>
        public string TokenLastFour { get; set; } = string.Empty;

        /// <summary>使用的 AI 模型，格式為 providerId:modelId</summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>Bot 是否啟用</summary>
        public bool IsEnabled { get; set; }

        /// <summary>此 Bot 專用的 Webhook 基礎 URL；null 代表沿用系統預設</summary>
        public string? WebhookBaseUrl { get; set; }

        /// <summary>動態組裝的完整 Webhook URL，由 Service 層計算後填入</summary>
        public string WebhookUrl { get; set; } = string.Empty;

        /// <summary>Discord Application ID（Discord 平台專用）</summary>
        public string? DiscordApplicationId { get; set; }

        /// <summary>Discord Ed25519 Public Key（Discord 平台專用）</summary>
        public string? DiscordPublicKey { get; set; }

        /// <summary>
        /// Bot 憑證/設定驗證結果（LINE/Discord 共用）：true=有效、false=無效、null=尚未驗證。
        /// 此值持久化於 DB，列表查詢也會帶出，供前端標示「設定無效」狀態。
        /// </summary>
        public bool? CredentialValid { get; set; }

        /// <summary>
        /// 憑證驗證失敗時的原因（人類可讀），僅在建立/更新/換 Token 回應即時填入、不持久化。
        /// 供前端以 Modal 顯示具體錯誤讓使用者修正。
        /// </summary>
        public string? CredentialError { get; set; }

        /// <summary>Bot 綁定建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Bot 綁定最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }
}
