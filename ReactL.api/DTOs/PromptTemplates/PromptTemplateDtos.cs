using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.PromptTemplates
{
    /// <summary>Prompt 模板列表項目</summary>
    public class PromptTemplateListItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Tags { get; set; }
        public int UsageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Prompt 模板詳情（含完整 Content）</summary>
    public class PromptTemplateDetailResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Tags { get; set; }
        public int UsageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>建立模板請求</summary>
    public class CreatePromptTemplateRequest
    {
        [Required(ErrorMessage = "標題為必填")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "內容為必填")]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Category { get; set; } = "其他";

        /// <summary>逗號分隔的標籤字串，例如 "寫作,翻譯"</summary>
        [MaxLength(500)]
        public string? Tags { get; set; }
    }

    /// <summary>更新模板請求</summary>
    public class UpdatePromptTemplateRequest
    {
        [Required(ErrorMessage = "標題為必填")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "內容為必填")]
        public string Content { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Category { get; set; } = "其他";

        [MaxLength(500)]
        public string? Tags { get; set; }
    }

    /// <summary>模板清單查詢參數</summary>
    public class PromptTemplateQueryParams
    {
        /// <summary>依分類篩選，null 表示全部</summary>
        public string? Category { get; set; }

        /// <summary>關鍵字搜尋（比對 Title 和 Tags）</summary>
        public string? Keyword { get; set; }
    }
}
