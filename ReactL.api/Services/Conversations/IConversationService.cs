using ReactL.api.Domain.Conversations;
using ReactL.api.DTOs.Requests.Conversations;

namespace ReactL.api.Services.Conversations
{
    /// <summary>對話服務介面</summary>
    public interface IConversationService
    {
        /// <summary>取得使用者的對話清單（釘選優先，再依更新時間排序）</summary>
        Task<List<ConversationDomain>> GetListAsync(Guid userId);

        /// <summary>取得對話詳情（含完整訊息列表）</summary>
        Task<ConversationDomain> GetByIdAsync(Guid id, Guid userId);

        /// <summary>依 ShareSlug 取得公開對話（不需登入）</summary>
        Task<ConversationDomain> GetBySlugAsync(string slug);

        /// <summary>建立新對話</summary>
        Task<ConversationDomain> CreateAsync(Guid userId, CreateConversationRequest request);

        /// <summary>更新對話設定（標題、釘選、公開分享、Persona）</summary>
        Task<ConversationDomain> UpdateAsync(Guid id, Guid userId, UpdateConversationRequest request);

        /// <summary>軟刪除對話</summary>
        Task DeleteAsync(Guid id, Guid userId);

        /// <summary>新增單筆訊息（AI 回應後呼叫，寫入 assistant 訊息）</summary>
        Task<MessageDomain> AddMessageAsync(Guid conversationId, Guid userId, AddMessageRequest request);

        /// <summary>刪除最後一筆 assistant 訊息（Regenerate 前清除舊回應）</summary>
        Task DeleteLastAssistantMessageAsync(Guid conversationId, Guid userId);
    }
}
