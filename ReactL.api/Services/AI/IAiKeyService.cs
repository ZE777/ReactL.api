using ReactL.api.DTOs.Requests.AiKeys;
using ReactL.api.DTOs.Responses.AiKeys;

namespace ReactL.api.Services.Ai
{
    /// <summary>AI Key 管理服務（混合 BYOK：系統預設 + 使用者自帶）</summary>
    public interface IAiKeyService
    {
        /// <summary>取得指定使用者自己的 AI Key 清單（不含系統預設 Key）</summary>
        Task<List<AiKeyResponse>> GetMyKeysAsync(Guid userId);

        /// <summary>
        /// 依 (UserId, ProviderId) 新增或更新使用者的 AI Key。
        /// 儲存前會先呼叫供應商驗證金鑰有效性；加密後存入，只回傳後 4 碼。
        /// </summary>
        Task<AiKeyResponse> UpsertAsync(Guid userId, UpsertAiKeyRequest request);

        /// <summary>刪除使用者在某供應商的 AI Key</summary>
        Task DeleteAsync(Guid userId, string providerId);

        /// <summary>
        /// 解析指定使用者在某供應商可用的金鑰明文：使用者自帶（啟用中）→ 系統預設 → appsettings fallback。
        /// allowSystemFallback=false 時（後台來源強制），沒有自帶金鑰就直接回 null，不借用系統 key。
        /// </summary>
        Task<string?> ResolveKeyAsync(Guid? ownerUserId, string providerId, bool allowSystemFallback = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// 啟動種子：將 appsettings 的 AI 預設 Key 加密寫入 DB（掛在系統用戶下、IsSystem=true）。
        /// 冪等：已存在則略過；同時確保系統用戶存在。
        /// </summary>
        Task SeedSystemKeysAsync(CancellationToken cancellationToken = default);
    }
}
