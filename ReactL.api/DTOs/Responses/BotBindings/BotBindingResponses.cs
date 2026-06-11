namespace ReactL.api.DTOs.Responses.BotBindings
{
    /// <summary>Bot 綁定列表項目（不含 Token，只顯示後 4 碼）</summary>
    public class BotBindingListItem
    {
        /// <summary>Bot 綁定唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>平台：line / discord</summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>Bot 顯示名稱</summary>
        public string BotName { get; set; } = string.Empty;

        /// <summary>Token 後 4 碼，例如 "3f9a"，前端顯示為 "••••3f9a"</summary>
        public string TokenLastFour { get; set; } = string.Empty;

        /// <summary>使用的 AI 模型</summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>Bot 是否啟用</summary>
        public bool IsEnabled { get; set; }

        /// <summary>使用的 Persona ID</summary>
        public Guid? PersonaId { get; set; }

        /// <summary>使用的 Persona 名稱（JOIN 取得）</summary>
        public string? PersonaName { get; set; }

        /// <summary>組裝好的完整 Webhook URL，前端直接複製使用</summary>
        public string WebhookUrl { get; set; } = string.Empty;

        /// <summary>此 Bot 專用的 Webhook 基礎 URL；null 代表使用系統預設</summary>
        public string? WebhookBaseUrl { get; set; }

        /// <summary>Discord Application ID（Discord 平台專用）</summary>
        public string? DiscordApplicationId { get; set; }

        /// <summary>Discord Application Public Key（Discord 平台專用）</summary>
        public string? DiscordPublicKey { get; set; }

        /// <summary>信任系統成員人數（列表摘要顯示用）</summary>
        public int TrustedUserCount { get; set; }

        /// <summary>
        /// Bot 憑證/設定驗證結果（持久化，LINE/Discord 共用）：true=有效、false=無效、null=尚未驗證。
        /// 前端依此在列表標示「設定無效」，重新整理後仍會顯示。
        /// </summary>
        public bool? CredentialValid { get; set; }

        /// <summary>Bot 綁定建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Bot 綁定最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>信任系統的單一成員（後台回應）</summary>
    public class TrustedUserResponse
    {
        /// <summary>對象的 Discord User ID</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>名稱／顯示稱呼</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>關係（自訂情感標籤，例如「主人」「爹地」「朋友」）</summary>
        public string? Tier { get; set; }

        /// <summary>系統角色：'owner'（主人/管理者）或 'trusted'（信任者）</summary>
        public string SystemRole { get; set; } = "trusted";

        /// <summary>授權來源："admin"（後台）或操作的主人 Discord User ID（對話路徑）</summary>
        public string? GrantedBy { get; set; }

        /// <summary>加入時間</summary>
        public DateTime GrantedAt { get; set; }
    }

    /// <summary>LINE Bot 月訊息用量（來自 LINE Messaging API）</summary>
    public class LineQuotaResponse
    {
        /// <summary>用量類型：limited（有上限）/ none（無限制方案）</summary>
        public string QuotaType { get; set; } = string.Empty;

        /// <summary>月用量上限；QuotaType = "none" 時為 null</summary>
        public int? Limit { get; set; }

        /// <summary>本月已送出訊息數</summary>
        public int TotalUsage { get; set; }

        /// <summary>本月剩餘量；QuotaType = "none" 時為 null</summary>
        public int? Remaining { get; set; }
    }

    /// <summary>Bot 綁定詳情（同樣不回傳明文 Token）</summary>
    public class BotBindingDetailResponse
    {
        /// <summary>Bot 綁定唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>平台：line / discord</summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>Bot 顯示名稱</summary>
        public string BotName { get; set; } = string.Empty;

        /// <summary>Token 後 4 碼，例如 "3f9a"，前端顯示為 "••••3f9a"</summary>
        public string TokenLastFour { get; set; } = string.Empty;

        /// <summary>使用的 AI 模型</summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>Bot 是否啟用</summary>
        public bool IsEnabled { get; set; }

        /// <summary>使用的 Persona ID</summary>
        public Guid? PersonaId { get; set; }

        /// <summary>使用的 Persona 名稱（JOIN 取得）</summary>
        public string? PersonaName { get; set; }

        /// <summary>組裝好的完整 Webhook URL，前端直接複製使用</summary>
        public string WebhookUrl { get; set; } = string.Empty;

        /// <summary>此 Bot 專用的 Webhook 基礎 URL；null 代表使用系統預設</summary>
        public string? WebhookBaseUrl { get; set; }

        /// <summary>Discord Application ID（Discord 平台專用）</summary>
        public string? DiscordApplicationId { get; set; }

        /// <summary>Discord Application Public Key（Discord 平台專用）</summary>
        public string? DiscordPublicKey { get; set; }

        /// <summary>信任系統成員人數</summary>
        public int TrustedUserCount { get; set; }

        /// <summary>
        /// Bot 憑證/設定驗證結果（持久化，LINE/Discord 共用）：true=有效、false=無效、null=尚未驗證。
        /// </summary>
        public bool? CredentialValid { get; set; }

        /// <summary>憑證驗證失敗原因（人類可讀，不持久化），供前端 Modal 顯示</summary>
        public string? CredentialError { get; set; }

        /// <summary>Bot 綁定建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Bot 綁定最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }
}