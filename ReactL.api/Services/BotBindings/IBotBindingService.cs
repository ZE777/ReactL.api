using ReactL.api.Domain.BotBindings;
using ReactL.api.DTOs.Requests.BotBindings;
using ReactL.api.DTOs.Responses.BotBindings;

namespace ReactL.api.Services.BotBindings
{
    /// <summary>Bot 綁定服務介面</summary>
    public interface IBotBindingService
    {
        /// <summary>取得使用者的 Bot 綁定清單</summary>
        Task<List<BotBindingDomain>> GetListAsync(Guid userId);

        /// <summary>取得 Bot 綁定詳情</summary>
        Task<BotBindingDomain> GetByIdAsync(Guid id, Guid userId);

        /// <summary>建立 Bot 綁定（Token 由後端 AES 加密後儲存）</summary>
        Task<BotBindingDomain> CreateAsync(Guid userId, CreateBotBindingRequest request);

        /// <summary>更新 Bot 設定（名稱、模型、Persona、啟用狀態）</summary>
        Task<BotBindingDomain> UpdateAsync(Guid id, Guid userId, UpdateBotBindingRequest request);

        /// <summary>軟刪除 Bot 綁定</summary>
        Task DeleteAsync(Guid id, Guid userId);

        /// <summary>更換 Bot Token（AES 重新加密後存回 DB）</summary>
        Task<BotBindingDomain> RotateTokenAsync(Guid id, Guid userId, RotateTokenRequest request);

        /// <summary>查詢 LINE Bot 本月訊息用量（呼叫 LINE Messaging API）</summary>
        Task<LineQuotaResponse> GetLineQuotaAsync(Guid id, Guid userId);
    }
}