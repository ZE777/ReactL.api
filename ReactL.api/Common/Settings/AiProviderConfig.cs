namespace ReactL.api.Common.Settings
{
    public class AiProviderConfig
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public List<AiModelConfig> Models { get; set; } = new();
        /// <summary>是否支援 stream_options.include_usage（Groq 支援，Cerebras / SambaNova 不支援）</summary>
        public bool SupportsStreamUsage { get; set; } = false;
    }

    public class AiModelConfig
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 是否推薦用於需要 function-calling 的場景（如 Discord agent 工具）。
        /// 推薦＝工具呼叫穩定且免費額度足夠；非推薦的模型可能因能力/額度導致非預期結果。
        /// </summary>
        public bool RecommendedForTools { get; set; } = false;
    }
}
