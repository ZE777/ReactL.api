using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Requests.Ai
{
    /// <summary>AI 對話請求（觸發 SSE 串流）</summary>
    public class ChatRequest
    {
        [Required]
        [DefaultValue("00000000-0000-0000-0000-000000000000")]
        public Guid ConversationId { get; set; }

        [Required(ErrorMessage = "訊息內容為必填")]
        public string UserMessage { get; set; } = string.Empty;

        /// <summary>
        /// 覆蓋對話的模型設定，null 表示沿用對話建立時的預設模型。
        /// 格式：{providerId}:{modelId}，完整清單見 GET /api/v1/ai/providers。
        /// Groq：groq:llama-3.3-70b-versatile｜groq:llama-3.1-8b-instant｜groq:deepseek-r1-distill-llama-70b
        /// Mistral：mistral:mistral-small-latest｜mistral:mistral-large-latest｜mistral:open-mistral-7b
        /// Cerebras：cerebras:llama-3.3-70b｜cerebras:qwen-3-32b
        /// SambaNova：sambanova:Meta-Llama-3.3-70B-Instruct｜sambanova:Qwen2.5-72B-Instruct
        /// </summary>
        [MaxLength(50)]
        [DefaultValue("groq:llama-3.3-70b-versatile")]
        public string? ModelOverride { get; set; }
    }

    /// <summary>前台公開聊天請求（無需登入，不寫入 DB）</summary>
    public class PublicChatRequest
    {
        /// <summary>指定使用的 Persona，null 表示無角色設定</summary>
        public Guid? PersonaId { get; set; }

        /// <summary>AI 模型，格式 providerId:modelId</summary>
        [Required]
        [MaxLength(50)]
        public string ModelType { get; set; } = "groq:llama-3.3-70b-versatile";

        /// <summary>前端維護的對話歷史（不含當前這則），最多 100 則防止超長 context 攻擊</summary>
        [MaxLength(100, ErrorMessage = "對話歷史不可超過 100 則")]
        public List<PublicChatMessage> Messages { get; set; } = [];

        /// <summary>本次使用者輸入</summary>
        [Required(ErrorMessage = "訊息內容為必填")]
        [MaxLength(4000, ErrorMessage = "訊息內容不可超過 4000 字元")]
        public string UserMessage { get; set; } = string.Empty;
    }

    /// <summary>前台對話歷史的單則訊息</summary>
    public class PublicChatMessage
    {
        [Required]
        [RegularExpression("^(user|assistant)$", ErrorMessage = "Role 僅支援 user / assistant")]
        public string Role { get; set; } = string.Empty;

        /// <summary>單則訊息最多 8000 字，防止對話歷史佔用過多 token</summary>
        [Required]
        [MaxLength(8000, ErrorMessage = "訊息內容不可超過 8000 字元")]
        public string Content { get; set; } = string.Empty;
    }
}