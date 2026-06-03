namespace ReactL.api.DTOs.Responses.Conversations
{
    /// <summary>對話列表項目（不含完整訊息內容）</summary>
    public class ConversationListItem
    {
        /// <summary>對話唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>對話標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>使用的 AI 模型</summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>是否釘選於清單頂端</summary>
        public bool IsPinned { get; set; }

        /// <summary>是否開放公開分享連結</summary>
        public bool IsPublic { get; set; }

        /// <summary>公開分享短碼</summary>
        public string? ShareSlug { get; set; }

        /// <summary>使用的 Persona ID</summary>
        public Guid? PersonaId { get; set; }

        /// <summary>使用的 Persona 名稱（JOIN 取得）</summary>
        public string? PersonaName { get; set; }

        /// <summary>訊息總筆數，供前端顯示對話長度</summary>
        public int MessageCount { get; set; }

        /// <summary>最後一則訊息的前 100 字，供清單預覽用</summary>
        public string? LastMessagePreview { get; set; }

        /// <summary>最後一則訊息的角色（user / assistant）</summary>
        public string? LastMessageRole { get; set; }

        /// <summary>對話建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>對話最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>對話詳情（含完整訊息列表）</summary>
    public class ConversationDetailResponse
    {
        /// <summary>對話唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>對話標題</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>使用的 AI 模型</summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>是否釘選於清單頂端</summary>
        public bool IsPinned { get; set; }

        /// <summary>是否開放公開分享連結</summary>
        public bool IsPublic { get; set; }

        /// <summary>軟刪除旗標，分享頁用來判斷是否顯示「已封存」提示</summary>
        public bool IsDeleted { get; set; }

        /// <summary>公開分享短碼</summary>
        public string? ShareSlug { get; set; }

        /// <summary>使用的 Persona ID</summary>
        public Guid? PersonaId { get; set; }

        /// <summary>使用的 Persona 名稱（JOIN 取得）</summary>
        public string? PersonaName { get; set; }

        /// <summary>使用的 Persona Emoji</summary>
        public string? PersonaEmoji { get; set; }

        /// <summary>完整訊息列表</summary>
        public List<MessageResponse> Messages { get; set; } = [];

        /// <summary>對話建立時間</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>對話最後更新時間</summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>訊息回應</summary>
    public class MessageResponse
    {
        /// <summary>訊息唯一識別碼</summary>
        public Guid Id { get; set; }

        /// <summary>角色：user / assistant / system</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>訊息完整內容</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>輸入 Token 數</summary>
        public int TokensIn { get; set; }

        /// <summary>輸出 Token 數</summary>
        public int TokensOut { get; set; }

        /// <summary>訊息建立時間</summary>
        public DateTime CreatedAt { get; set; }
    }
}
