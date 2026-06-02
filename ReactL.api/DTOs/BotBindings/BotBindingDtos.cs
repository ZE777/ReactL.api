using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.BotBindings
{
    /// <summary>Bot 綁定列表項目（不含 Token，只顯示後 4 碼）</summary>
    public class BotBindingListItem
    {
        public Guid Id { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string BotName { get; set; } = string.Empty;
        /// <summary>Token 後 4 碼，例如 "3f9a"，前端顯示為 "••••3f9a"</summary>
        public string TokenLastFour { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public Guid? PersonaId { get; set; }
        public string? PersonaName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Bot 綁定詳情（同樣不回傳明文 Token）</summary>
    public class BotBindingDetailResponse
    {
        public Guid Id { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string BotName { get; set; } = string.Empty;
        public string TokenLastFour { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public Guid? PersonaId { get; set; }
        public string? PersonaName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

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
    }

    /// <summary>更換 Token 請求（單獨端點，避免每次更新設定都要重輸 Token）</summary>
    public class RotateTokenRequest
    {
        [Required(ErrorMessage = "新 Token 為必填")]
        public string NewToken { get; set; } = string.Empty;

        /// <summary>LINE 平台需同步更換 ChannelSecret</summary>
        public string? NewChannelSecret { get; set; }
    }
}
