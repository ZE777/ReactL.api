using ReactL.api.DTOs.Conversations;

namespace ReactL.api.Services.Conversations
{
    public interface IConversationService
    {
        Task<List<ConversationListItem>> GetListAsync(Guid userId);
        Task<ConversationDetailResponse> GetByIdAsync(Guid id, Guid userId);

        /// <summary>依 ShareSlug 取得公開對話（不需登入）</summary>
        Task<ConversationDetailResponse> GetBySlugAsync(string slug);

        Task<ConversationDetailResponse> CreateAsync(Guid userId, CreateConversationRequest request);
        Task<ConversationDetailResponse> UpdateAsync(Guid id, Guid userId, UpdateConversationRequest request);
        Task DeleteAsync(Guid id, Guid userId);

        /// <summary>新增單筆訊息（AI 回應後呼叫，寫入 assistant 訊息）</summary>
        Task<MessageResponse> AddMessageAsync(Guid conversationId, Guid userId, AddMessageRequest request);

        /// <summary>刪除最後一筆 assistant 訊息（Regenerate 前清除舊回應）</summary>
        Task DeleteLastAssistantMessageAsync(Guid conversationId, Guid userId);
    }
}
