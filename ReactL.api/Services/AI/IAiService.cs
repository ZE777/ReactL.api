using ReactL.api.DTOs.Ai;

namespace ReactL.api.Services.Ai
{
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
        /// 非串流單次呼叫，用於 AI 強化 Prompt（不需要 SSE）
        /// </summary>
        Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
    }
}
