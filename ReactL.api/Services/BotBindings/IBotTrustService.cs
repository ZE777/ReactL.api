using ReactL.api.Models.BotBindings;

namespace ReactL.api.Services.BotBindings
{
    /// <summary>
    /// Bot 信任名單（動態白名單）讀寫服務 —— JSON 解析/序列化的單一來源。
    /// 由 Discord 工具（對話路徑）與後台 CRUD（後台路徑）共用，
    /// 故本服務「不負責授權」：呼叫端各自把關（對話＝主人 Discord ID 閘門；後台＝Bot 擁有者）。
    /// 以 BotBinding.Id 為 key，名單存於 BotBinding.TrustedUsersJson。
    /// </summary>
    public interface IBotTrustService
    {
        /// <summary>取得某 Bot 的信任名單（解析 JSON；無資料回空清單）。</summary>
        Task<List<TrustedUser>> GetListAsync(Guid botBindingId, CancellationToken ct = default);

        /// <summary>
        /// 加入信任對象（以 Id 去重；已存在則更新 Label/Tier）。
        /// 回傳 (是否為新增, 套用後的對象)。找不到 Bot 回 (false, null)。
        /// </summary>
        Task<(bool Added, TrustedUser? User)> AddAsync(Guid botBindingId, TrustedUser user, CancellationToken ct = default);

        /// <summary>移除信任對象（依 Discord User ID）。回傳是否確實移除了一筆。</summary>
        Task<bool> RemoveAsync(Guid botBindingId, string discordUserId, CancellationToken ct = default);

        /// <summary>判斷某 Discord 使用者是否在信任名單內（含其資料，不在回 null）。</summary>
        Task<TrustedUser?> FindAsync(Guid botBindingId, string discordUserId, CancellationToken ct = default);

        /// <summary>判斷某 Discord 使用者是否為此 Bot 的「主人」（系統角色 owner）。</summary>
        Task<bool> IsOwnerAsync(Guid botBindingId, string? discordUserId, CancellationToken ct = default);
    }
}