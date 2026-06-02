using ReactL.api.DTOs.BotBindings;

namespace ReactL.api.Services.BotBindings
{
    public interface IBotBindingService
    {
        Task<List<BotBindingListItem>> GetListAsync(Guid userId);
        Task<BotBindingDetailResponse> GetByIdAsync(Guid id, Guid userId);
        Task<BotBindingDetailResponse> CreateAsync(Guid userId, CreateBotBindingRequest request);
        Task<BotBindingDetailResponse> UpdateAsync(Guid id, Guid userId, UpdateBotBindingRequest request);
        Task DeleteAsync(Guid id, Guid userId);

        /// <summary>更換 Bot Token（AES 重新加密後存回 DB）</summary>
        Task<BotBindingDetailResponse> RotateTokenAsync(Guid id, Guid userId, RotateTokenRequest request);
    }
}
