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

        /// <summary>Bot 綁定建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Bot 綁定最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }
}
