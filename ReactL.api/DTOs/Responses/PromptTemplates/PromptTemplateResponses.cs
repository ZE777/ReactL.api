namespace ReactL.api.DTOs.Responses.PromptTemplates
{
    /// <summary>Prompt 模板列表項目</summary>
    public class PromptTemplateListItem
    {
        /// <summary>模板唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>模板標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>模板完整內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>分類：寫作 / 程式 / 翻譯 / 其他</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>逗號分隔標籤字串</summary>
        public string? Tags { get; set; }

        /// <summary>模板被套用的次數</summary>
        public int UsageCount { get; set; }

        /// <summary>模板建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>模板最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Prompt 模板詳情（含完整 Content）</summary>
    public class PromptTemplateDetailResponse
    {
        /// <summary>模板唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>模板標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>模板完整內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>分類：寫作 / 程式 / 翻譯 / 其他</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>逗號分隔標籤字串</summary>
        public string? Tags { get; set; }

        /// <summary>模板被套用的次數</summary>
        public int UsageCount { get; set; }

        /// <summary>模板建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>模板最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }
}