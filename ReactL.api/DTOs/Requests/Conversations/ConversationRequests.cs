using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Requests.Conversations
{
    /// <summary>建立對話請求</summary>
    public class CreateConversationRequest
    {
        [MaxLength(200)]
        public string Title { get; set; } = "新對話";

        [Required(ErrorMessage = "模型為必填")]
        [MaxLength(50)]
        public string ModelType { get; set; } = string.Empty;

        public Guid? PersonaId { get; set; }
    }

    /// <summary>更新對話設定（標題、模型、釘選、分享、角色）</summary>
    public class UpdateConversationRequest
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(100)]
        public string? ModelType { get; set; }

        public bool? IsPinned { get; set; }

        /// <summary>true = 建立或保留 ShareSlug；false = 清除 ShareSlug 並關閉分享</summary>
        public bool? IsPublic { get; set; }

        /// <summary>設為 true 才會更新 PersonaId，允許傳 null 表示清除角色</summary>
        public bool UpdatePersona { get; set; }

        public Guid? PersonaId { get; set; }
    }

    /// <summary>新增訊息請求（user 角色的輸入，assistant 由 AI 回應後後端寫入）</summary>
    public class AddMessageRequest
    {
        [Required(ErrorMessage = "角色為必填")]
        [RegularExpression("^(user|assistant|system)$", ErrorMessage = "Role 僅支援 user / assistant / system")]
        public string Role { get; set; } = "user";

        [Required(ErrorMessage = "內容為必填")]
        public string Content { get; set; } = string.Empty;

        public int TokensIn { get; set; }
        public int TokensOut { get; set; }
    }
}
