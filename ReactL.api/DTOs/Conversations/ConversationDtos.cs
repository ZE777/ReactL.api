using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Conversations
{
    // ── 對話 ─────────────────────────────────────────────────────────────────

    /// <summary>對話列表項目（不含完整訊息內容）</summary>
    public class ConversationListItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public bool IsPublic { get; set; }
        public string? ShareSlug { get; set; }
        public Guid? PersonaId { get; set; }
        public string? PersonaName { get; set; }
        /// <summary>訊息總筆數，供前端顯示對話長度</summary>
        public int MessageCount { get; set; }
        /// <summary>最後一則訊息的前 100 字，供清單預覽用</summary>
        public string? LastMessagePreview { get; set; }
        /// <summary>最後一則訊息的角色（user / assistant）</summary>
        public string? LastMessageRole { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>對話詳情（含完整訊息列表）</summary>
    public class ConversationDetailResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public bool IsPublic { get; set; }
        public string? ShareSlug { get; set; }
        public Guid? PersonaId { get; set; }
        public string? PersonaName { get; set; }
        public List<MessageResponse> Messages { get; set; } = [];
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

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

    // ── 訊息 ─────────────────────────────────────────────────────────────────

    /// <summary>訊息回應</summary>
    public class MessageResponse
    {
        public Guid Id { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int TokensIn { get; set; }
        public int TokensOut { get; set; }
        public DateTime CreatedAt { get; set; }
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
