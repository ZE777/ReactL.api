using ReactL.api.DTOs.Ai;
using ReactL.api.DTOs.Requests.Webhooks;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>執行工具所需的情境（來自 interaction payload，已解析）</summary>
    public class DiscordToolContext
    {
        /// <summary>該 Bot 的 Token 明文（呼叫端解密後傳入）</summary>
        public string BotToken { get; set; } = string.Empty;

        /// <summary>伺服器 ID；DM 情境為 null（管理動作不適用）</summary>
        public string? GuildId { get; set; }

        /// <summary>互動所在頻道 ID（purge / slowmode 未指定頻道時的預設目標）</summary>
        public string? ChannelId { get; set; }

        /// <summary>下指令者的已計算權限位元</summary>
        public ulong InvokerPermissions { get; set; }

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
    }
}