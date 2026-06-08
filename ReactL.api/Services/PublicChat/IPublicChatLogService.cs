using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.PublicChat;
using ReactL.api.DTOs.Responses.PublicChat;

namespace ReactL.api.Services.PublicChat
{
    /// <summary>前台公開聊天記錄服務：串流時寫入記錄、後台 Admin 查詢、逾期清除</summary>
    public interface IPublicChatLogService
    {
        /// <summary>記錄一則前台聊天訊息（best-effort，不應影響聊天串流）</summary>
        Task LogMessageAsync(
            string? sessionId,
            Guid? accessCodeId,
            string? accessCodeText,
            string role,
            string content,
            string? modelType,
            Guid? personaId,
            int tokensIn,
            int tokensOut,
            CancellationToken cancellationToken);

        /// <summary>後台：前台聊天對話列表（以 SessionId 分組）</summary>
        Task<PagedResponse<PublicChatConversationSummary>> GetConversationsAsync(PublicChatConversationQueryParams query);

        /// <summary>後台：指定對話的完整訊息記錄</summary>
        Task<PagedResponse<PublicChatLogItem>> GetMessagesAsync(PublicChatMessageQueryParams query);

        /// <summary>
        /// 前台訪客取回自己的歷史對話（指定角色）。
        /// 一人一碼：優先以存取碼識別（跨裝置／瀏覽器皆可看到）；無碼的匿名情況退回以 sessionId 識別。
        /// </summary>
        Task<List<PublicChatHistoryItem>> GetHistoryAsync(string? accessCode, string? sessionId, Guid? personaId, CancellationToken cancellationToken);

        /// <summary>清除逾保留天數的記錄，回傳刪除筆數</summary>
        Task<int> PurgeExpiredAsync(int retentionDays, CancellationToken cancellationToken);
    }
}