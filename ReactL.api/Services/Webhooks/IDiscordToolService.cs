using ReactL.api.DTOs.Ai;
using ReactL.api.DTOs.Requests.Webhooks;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>執行工具所需的情境（來自 interaction payload，已解析）</summary>
    public class DiscordToolContext
    {
        /// <summary>該 BotBinding 的 Id（信任名單讀寫的 key）</summary>
        public Guid BotBindingId { get; set; }

        /// <summary>該 Bot 的 Token 明文（呼叫端解密後傳入）</summary>
        public string BotToken { get; set; } = string.Empty;

        /// <summary>伺服器 ID；DM 情境為 null（管理動作不適用）</summary>
        public string? GuildId { get; set; }

        /// <summary>互動所在頻道 ID（purge / slowmode 未指定頻道時的預設目標）</summary>
        public string? ChannelId { get; set; }

        /// <summary>下指令者的已計算權限位元</summary>
        public ulong InvokerPermissions { get; set; }

        /// <summary>下指令／點擊者的 Discord 使用者 ID（批次排除發起人、審計用）</summary>
        public string? InvokerUserId { get; set; }

        /// <summary>interaction 解析出的提及實體（驗證/取得目標 ID 用）</summary>
        public DiscordResolved? Resolved { get; set; }
    }

    /// <summary>
    /// 工具執行結果。Text=要顯示的內容；Components=可選的 Discord 訊息元件（如二次確認按鈕），
    /// 非 null 時代表此回覆需附帶按鈕（中高風險動作的確認）。
    /// </summary>
    public record ToolExecutionResult(string Text, object? Components = null);

    /// <summary>
    /// Discord function-calling 工具服務：提供給 AI 的白名單工具定義，並負責驗證 + 執行工具呼叫。
    /// AI 只能從這裡提供的工具中挑選，無法指定任意 Discord API。
    /// </summary>
    public interface IDiscordToolService
    {
        /// <summary>提供給 AI 的白名單工具定義</summary>
        IReadOnlyList<AiFunctionTool> GetToolDefinitions();

        /// <summary>
        /// 執行 AI 回傳的工具呼叫。低風險動作直接執行回傳文字；
        /// 中高風險動作回傳「確認訊息 + 按鈕元件」（Components 非 null），待使用者按確認後才真正執行。
        /// </summary>
        Task<ToolExecutionResult> ExecuteAsync(IReadOnlyList<AiToolCall> calls, DiscordToolContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// 使用者按下確認按鈕後，依按鈕 custom_id 執行先前暫存的動作，回傳結果文字。
        /// 會重新檢查點擊者的權限。回傳 null 代表此 custom_id 非本服務的確認動作（例如「取消」）。
        /// </summary>
        Task<string?> ExecuteConfirmedAsync(string customId, DiscordToolContext context, CancellationToken cancellationToken = default);

        /// <summary>該工具是否為唯讀（agent 多步迴圈可自動執行並回灌結果，不需確認）。</summary>
        bool IsReadOnly(string toolName);

        /// <summary>
        /// 執行唯讀工具，回傳要回灌給模型的文字。
        /// sinceMinutes / maxMessages 僅供 fetch_recent_messages 使用（呼叫端已做時間窗上限驗證）。
        /// </summary>
        Task<string> ExecuteReadOnlyAsync(AiToolCall call, DiscordToolContext context, int sinceMinutes, int maxMessages, CancellationToken cancellationToken = default);

        /// <summary>該工具是否為「批次動作」（對多個對象執行，需多選確認）。</summary>
        bool IsBatchTool(string toolName);

        /// <summary>是否為「僅多步 agent 模式」可用的工具（fetch_recent_messages / 批次工具）；單輪模式應排除。</summary>
        bool IsAgentOnlyTool(string toolName);

        /// <summary>
        /// 為批次動作組「多選確認」：過濾並暫存對象清單（token），回傳含 User Select + 執行/取消按鈕的訊息。
        /// 使用者可取消勾選要排除的人，按〔執行〕後才真正執行。
        /// </summary>
        Task<ToolExecutionResult> BuildBatchConfirmationAsync(AiToolCall call, DiscordToolContext context, CancellationToken cancellationToken = default);

        /// <summary>多選變更時更新暫存的勾選名單（僅限原候選，防止亂加對象）。</summary>
        void UpdateBatchSelection(string customId, IReadOnlyList<string>? selectedIds);
    }
}