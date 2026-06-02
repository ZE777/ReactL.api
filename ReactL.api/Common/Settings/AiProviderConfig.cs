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
    }
}
