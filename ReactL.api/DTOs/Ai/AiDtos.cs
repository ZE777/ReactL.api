using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Ai
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

    /// <summary>SSE 串流的單一 chunk，前端以 EventSource 接收</summary>
    public class ChatStreamChunk
    {
        /// <summary>chunk 類型：delta / done / error / rate_limit</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>delta 類型時為 AI 回應的文字片段</summary>
        public string? Content { get; set; }

        /// <summary>done 類型時含完整 token 用量</summary>
        public TokenUsage? Usage { get; set; }
    }

    public class TokenUsage
    {
        public int TokensIn { get; set; }
        public int TokensOut { get; set; }
    }

    public class AiModelDto
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class AiProviderDto
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsConfigured { get; set; }
        public List<AiModelDto> Models { get; set; } = new();
    }
}
