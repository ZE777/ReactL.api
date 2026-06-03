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

        /// <summary>Bot 綁定建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Bot 綁定最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
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

        /// <summary>Bot 綁定建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Bot 綁定最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }
}