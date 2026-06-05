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
        /// 前台公開聊天 SSE 串流，無需登入、不寫入 DB
        /// 前端負責維護對話歷史並每次完整傳入
        /// </summary>
        IAsyncEnumerable<ChatStreamChunk> PublicChatStreamAsync(
            PublicChatRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        /// 非串流單次呼叫，用於 AI 強化 Prompt（不需要 SSE）。
        /// ownerUserId 為金鑰歸屬使用者，null 表示走系統預設金鑰。
        /// </summary>
        Task<string> CompleteAsync(string systemPrompt, string userPrompt, Guid? ownerUserId = null, CancellationToken cancellationToken = default, bool allowSystemFallback = true);

        /// <summary>
        /// 非串流單次呼叫，同時回傳 Token 用量，供 Webhook 等外部觸發場景寫入統計。
        /// ownerUserId 為金鑰歸屬使用者（Webhook 傳 Bot 擁有者），null 表示走系統預設金鑰。
        /// </summary>
        Task<(string Reply, int TokensIn, int TokensOut)> CompleteWithUsageAsync(
            string systemPrompt, string userPrompt, Guid? ownerUserId = null, CancellationToken cancellationToken = default, bool allowSystemFallback = true);
    }
}
