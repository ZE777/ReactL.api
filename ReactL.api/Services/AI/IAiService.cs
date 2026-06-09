using ReactL.api.DTOs.Ai;
using ReactL.api.DTOs.Requests.Ai;
using ReactL.api.DTOs.Responses.Ai;

namespace ReactL.api.Services.Ai
{
    /// <summary>AI 服務介面（SSE 串流協議物件，直接回傳 DTO）</summary>
    public interface IAiService
    {
        /// <summary>
        /// 發起 AI 對話並以 SSE 串流方式回傳，同時將訊息寫入 DB
        /// </summary>
        /// <param name="request">對話請求</param>
        /// <param name="userId">發起請求的使用者 ID，用於驗證對話歸屬</param>
        /// <param name="cancellationToken">前端斷線時取消請求，避免繼續消耗 API 費用</param>
        IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(
            ChatRequest request,
            Guid userId,
            CancellationToken cancellationToken);

        /// <summary>
        /// 前台公開聊天 SSE 串流，無需登入；訊息會寫入 PublicChatLogs 供後台 Admin 監控
        /// 前端負責維護對話歷史並每次完整傳入
        /// </summary>
        /// <param name="accessCode">前台帶入的存取碼（X-Access-Code 標頭）；用於閘門驗證與每日配額</param>
        /// <param name="sessionId">前台帶入的對話工作階段 Id（X-Chat-Session 標頭）；用於後台監控分組</param>
        IAsyncEnumerable<ChatStreamChunk> PublicChatStreamAsync(
            PublicChatRequest request,
            string? accessCode,
            string? sessionId,
            CancellationToken cancellationToken);

        /// <summary>
        /// 非串流單次呼叫，用於 AI 強化 Prompt（不需要 SSE）。
        /// ownerUserId 為金鑰歸屬使用者，null 表示走系統預設金鑰。
        /// </summary>
        Task<string> CompleteAsync(string systemPrompt, string userPrompt, Guid? ownerUserId = null, CancellationToken cancellationToken = default, bool allowSystemFallback = true);

        /// <summary>
        /// 非串流單次呼叫，同時回傳 Token 用量，供 Webhook 等外部觸發場景寫入統計。
        /// ownerUserId 為金鑰歸屬使用者（Webhook 傳 Bot 擁有者），null 表示走系統預設金鑰。
        /// modelType 為 "providerId:modelId"，傳入則用該模型（如 LINE Bot 的設定模型）；null 則用系統預設模型。
        /// </summary>
        Task<(string Reply, int TokensIn, int TokensOut)> CompleteWithUsageAsync(
            string systemPrompt, string userPrompt, Guid? ownerUserId = null, CancellationToken cancellationToken = default, bool allowSystemFallback = true, string? modelType = null);

        /// <summary>
        /// 帶工具（function calling）的非串流呼叫。模型可選擇回傳工具呼叫或純文字。
        /// 用於 Discord Bot 以自然語言觸發伺服器管理動作；modelType 為該 Bot 的設定模型（須支援 tool calling）。
        /// </summary>
        Task<AiToolResult> CompleteWithToolsAsync(
            string systemPrompt, string userPrompt, IReadOnlyList<AiFunctionTool> tools,
            string modelType, Guid? ownerUserId = null, CancellationToken cancellationToken = default, bool allowSystemFallback = true);

        /// <summary>
        /// 多步（agentic）工具呼叫迴圈。模型可「呼叫唯讀工具 → 看結果 → 再決策」，
        /// 用於「依條件找人/找訊息再執行動作」。迴圈內自動執行「唯讀工具」並回灌結果；
        /// 遇到「動作工具」（executeReadOnlyTool 回 null）即停止，將該回合工具呼叫放入結果交呼叫端執行。
        /// 受 maxSteps 與 maxTokensBudget（累計輸入+輸出）雙重上限保護。
        /// </summary>
        /// <param name="executeTool">
        /// 處置單一工具呼叫：唯讀工具→<see cref="AgentToolResponse.FromReadOnly"/>（回灌續推）；
        /// 動作工具→<see cref="AgentToolResponse.Action"/>（停止、交還呼叫端）；
        /// 硬停止（如超出規範）→<see cref="AgentToolResponse.Stop"/>（直接以該文字結束）。
        /// </param>
        Task<AiAgentResult> RunToolAgentAsync(
            string systemPrompt, string userPrompt, IReadOnlyList<AiFunctionTool> tools,
            string modelType, Guid? ownerUserId,
            Func<AiToolCall, CancellationToken, Task<AgentToolResponse>> executeTool,
            int maxSteps, int maxTokensBudget,
            CancellationToken cancellationToken = default);
    }
}