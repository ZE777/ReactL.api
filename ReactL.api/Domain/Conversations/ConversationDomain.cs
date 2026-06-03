namespace ReactL.api.Domain.Conversations
{
    /// <summary>對話業務物件（含完整訊息列表與 Persona 資訊）</summary>
    public class ConversationDomain
    {
        /// <summary>對話唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>所屬使用者 ID</summary>
        public Guid UserId { get; set; }

        /// <summary>使用的 Persona ID，null 表示未指定角色</summary>
        public Guid? PersonaId { get; set; }

        /// <summary>對話使用的 Persona 名稱（JOIN 取得）</summary>
        public string? PersonaName { get; set; }

        /// <summary>對話使用的 Persona Emoji</summary>
        public string? PersonaEmoji { get; set; }

        /// <summary>對話標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>使用的 AI 模型，格式為 providerId:modelId</summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>是否釘選於清單頂端</summary>
        public bool IsPinned { get; set; }

        /// <summary>是否開放公開分享連結</summary>
        public bool IsPublic { get; set; }

        /// <summary>軟刪除狀態，分享頁顯示封存提示時需要</summary>
        public bool IsDeleted { get; set; }

        /// <summary>公開分享的短碼，用於產生分享 URL</summary>
        public string? ShareSlug { get; set; }

        /// <summary>訊息總筆數（清單預覽用）</summary>
        public int MessageCount { get; set; }

        /// <summary>最後一則訊息的前 100 字預覽</summary>
        public string? LastMessagePreview { get; set; }

        /// <summary>最後一則訊息的角色（user / assistant）</summary>
        public string? LastMessageRole { get; set; }

        /// <summary>完整訊息列表（詳情頁使用，清單不會填入）</summary>
        public List<MessageDomain> Messages { get; set; } = [];

        /// <summary>對話建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>對話最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }
}