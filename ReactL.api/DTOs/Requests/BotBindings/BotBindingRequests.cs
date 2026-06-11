using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Requests.BotBindings
{
    /// <summary>建立 Bot 綁定請求</summary>
    public class CreateBotBindingRequest
    {
        [Required(ErrorMessage = "平台為必填")]
        [RegularExpression("^(line|discord)$", ErrorMessage = "Platform 僅支援 'line' 或 'discord'")]
        public string Platform { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bot 名稱為必填")]
        [MaxLength(100)]
        public string BotName { get; set; } = string.Empty;

        /// <summary>Bot Token 明文，後端加密後儲存，不會原樣保存</summary>
        [Required(ErrorMessage = "Bot Token 為必填")]
        public string BotToken { get; set; } = string.Empty;

        /// <summary>LINE Channel Secret 明文（LINE 平台必填，Discord 可不填）</summary>
        public string? ChannelSecret { get; set; }

        [Required(ErrorMessage = "模型為必填")]
        [MaxLength(50)]
        public string ModelType { get; set; } = string.Empty;

        public Guid? PersonaId { get; set; }

        /// <summary>此 Bot 專用的 Webhook 基礎 URL（選填）；留空則使用系統預設 AppSettings.BaseUrl</summary>
        [MaxLength(500)]
        [Url(ErrorMessage = "Webhook 基礎 URL 格式不正確，需以 http:// 或 https:// 開頭")]
        public string? WebhookBaseUrl { get; set; }

        /// <summary>Discord Application ID（Discord 平台必填）</summary>
        [MaxLength(50)]
        public string? DiscordApplicationId { get; set; }

        /// <summary>Discord Application Public Key（Discord 平台必填，用於 Ed25519 驗簽）</summary>
        [MaxLength(100)]
        public string? DiscordPublicKey { get; set; }
    }

    /// <summary>更新 Bot 綁定請求（不更新 Token，Token 另有端點更換）</summary>
    public class UpdateBotBindingRequest
    {
        [Required(ErrorMessage = "Bot 名稱為必填")]
        [MaxLength(100)]
        public string BotName { get; set; } = string.Empty;

        [Required(ErrorMessage = "模型為必填")]
        [MaxLength(50)]
        public string ModelType { get; set; } = string.Empty;

        public Guid? PersonaId { get; set; }
        public bool IsEnabled { get; set; }

        /// <summary>此 Bot 專用的 Webhook 基礎 URL（選填）；null 清除並回復使用系統預設</summary>
        [MaxLength(500)]
        [Url(ErrorMessage = "Webhook 基礎 URL 格式不正確，需以 http:// 或 https:// 開頭")]
        public string? WebhookBaseUrl { get; set; }

        /// <summary>Discord Application ID（Discord 平台專用）</summary>
        [MaxLength(50)]
        public string? DiscordApplicationId { get; set; }

        /// <summary>Discord Application Public Key（Discord 平台專用）</summary>
        [MaxLength(100)]
        public string? DiscordPublicKey { get; set; }
    }

    /// <summary>更換 Token 請求（單獨端點，避免每次更新設定都要重輸 Token）</summary>
    public class RotateTokenRequest
    {
        [Required(ErrorMessage = "新 Token 為必填")]
        public string NewToken { get; set; } = string.Empty;

        /// <summary>LINE 平台需同步更換 ChannelSecret</summary>
        public string? NewChannelSecret { get; set; }
    }

    /// <summary>新增/更新信任系統成員請求（後台路徑）</summary>
    public class AddTrustedUserRequest
    {
        /// <summary>對象的 Discord User ID（17~20 位數字）</summary>
        [Required(ErrorMessage = "Discord User ID 為必填")]
        [RegularExpression(@"^\d{17,20}$", ErrorMessage = "Discord User ID 需為 17~20 位數字")]
        public string DiscordUserId { get; set; } = string.Empty;

        /// <summary>名稱／顯示稱呼</summary>
        [MaxLength(100)]
        public string? Label { get; set; }

        /// <summary>關係（自訂情感標籤，例如「主人」「爹地」「朋友」）</summary>
        [MaxLength(50)]
        public string? Tier { get; set; }

        /// <summary>系統角色：'owner'（主人/管理者）或 'trusted'（信任者）；其他值一律視為 trusted</summary>
        [MaxLength(20)]
        public string? SystemRole { get; set; }
    }
}
