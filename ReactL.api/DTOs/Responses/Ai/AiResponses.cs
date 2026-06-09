namespace ReactL.api.DTOs.Responses.Ai
{
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

    /// <summary>Token 用量統計</summary>
    public class TokenUsage
    {
        /// <summary>輸入 Token 數</summary>
        public int TokensIn { get; set; }

        /// <summary>輸出 Token 數</summary>
        public int TokensOut { get; set; }
    }

    /// <summary>AI 模型資訊</summary>
    public class AiModelDto
    {
        /// <summary>模型唯一識別碼</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>模型顯示名稱</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>是否推薦用於 function-calling 場景（前端顯示「推薦」標籤、非推薦時提示）</summary>
        public bool RecommendedForTools { get; set; }
    }

    /// <summary>AI 提供商資訊（含可用模型清單）</summary>
    public class AiProviderDto
    {
        /// <summary>提供商唯一識別碼</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>提供商顯示名稱</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>是否已設定 API Key（未設定則無法使用）</summary>
        public bool IsConfigured { get; set; }

        /// <summary>該提供商的可用模型清單</summary>
        public List<AiModelDto> Models { get; set; } = new();
    }
}
