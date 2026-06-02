namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// AI API 串接設定
    /// 對應 appsettings.json 的 "AiSettings" section
    /// 敏感欄位 ProviderKeys 必須透過 User Secrets 或環境變數設定，禁止寫入版控
    /// </summary>
    public class AiSettings
    {
        /// <summary>
        /// 預設使用的 AI 模型，格式為 "providerId:modelId"，例如 "groq:llama-3.3-70b-versatile"
        /// </summary>
        public string DefaultModel { get; set; } = "groq:llama-3.3-70b-versatile";

        /// <summary>單次 AI 回應的最大 Token 數，控制回應長度與費用</summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>
        /// 呼叫 AI API 的逾時秒數
        /// AI 回應通常較慢，建議設定 30 秒以上
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// AI API 呼叫失敗時的自動重試次數
        /// 僅針對可重試的錯誤（5xx、網路逾時），4xx 錯誤不重試
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 每個提供商的 API 金鑰，key 為 provider Id（如 "groq"），value 為 API key
        /// 設定方式：dotnet user-secrets set "AiSettings:ProviderKeys:groq" "gsk_..."
        /// </summary>
        public Dictionary<string, string> ProviderKeys { get; set; } = new();

        /// <summary>可用的 AI 提供商清單，從 appsettings.json 讀取</summary>
        public List<AiProviderConfig> Providers { get; set; } = new();
    }
}
