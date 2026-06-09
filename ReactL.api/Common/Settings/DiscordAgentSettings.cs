namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// Discord「多步 agent 工具」設定，對應 appsettings.json 的 "DiscordAgentSettings" section。
    /// 控制 Discord Bot 以自然語言「依條件找人 → 執行動作」的多步 function calling 行為、
    /// 成本護欄與存取控制。詳見 workflows/discord-agentic-tools-proposal.md。
    /// </summary>
    public class DiscordAgentSettings
    {
        /// <summary>功能總開關；false 時 Discord 走原本單輪 function calling（等同現況）。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>true：任何成員都能「發起掃描+提名單」（唯讀無害）；false：需具該動作權限才能發起。</summary>
        public bool AllowAnyoneInitiate { get; set; } = true;

        /// <summary>
        /// false（建議）：僅具該動作權限者可調整勾選/按執行；
        /// true：任何人皆可按（⚠️ 危險，僅供測試，正式環境請保持 false）。
        /// </summary>
        public bool AllowAnyoneConfirm { get; set; } = false;

        /// <summary>一般成員「發起掃描」的冷卻秒數，防止濫用洗額度/洗版。</summary>
        public int InitiateCooldownSeconds { get; set; } = 30;

        /// <summary>管理員（具 Moderate Members / Administrator）「發起掃描」的冷卻秒數，通常較短。</summary>
        public int InitiateCooldownSecondsForAdmin { get; set; } = 10;

        /// <summary>未指定時間時的預設掃描窗（分鐘）。</summary>
        public int DefaultScanWindowMinutes { get; set; } = 120;

        /// <summary>掃描時間窗硬上限（分鐘）；使用者要求超過此值 → 拒絕並列出規則。</summary>
        public int MaxScanWindowMinutes { get; set; } = 1440;

        /// <summary>單次最多分析的訊息則數（壓在免費層 TPM 以下的主要護欄）。</summary>
        public int MaxScanMessages { get; set; } = 120;

        /// <summary>單次批次動作最多影響人數（對齊 Discord select 25 上限）。</summary>
        public int MaxBatchTargets { get; set; } = 25;

        /// <summary>agent 迴圈最大步數，防無限/暴衝。</summary>
        public int MaxStepsPerCommand { get; set; } = 3;

        /// <summary>每道指令的 token 天花板（累計輸入+輸出，超過即停）。</summary>
        public int MaxTokensPerCommand { get; set; } = 30000;

        /// <summary>待確認批次暫存的存活時間（分鐘）。</summary>
        public int PendingTtlMinutes { get; set; } = 5;

        /// <summary>
        /// 掃描/判斷用的模型（"providerId:modelId"）；**留空 = 用該 Bot 設定的模型**（預設行為）。
        /// 填便宜模型（如 groq:llama-3.1-8b-instant）可省成本，但小模型工具呼叫較不穩，需自行權衡。
        /// </summary>
        public string ScanModel { get; set; } = "";
    }
}
