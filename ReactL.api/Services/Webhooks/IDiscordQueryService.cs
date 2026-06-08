namespace ReactL.api.Services.Webhooks
{
    /// <summary>查詢結果：Success=是否成功；Text=成功的格式化內容或失敗原因</summary>
    public record QueryResult(bool Success, string Text);

    /// <summary>
    /// Discord 伺服器唯讀查詢服務（手刻 HTTP GET）。
    /// 回傳已格式化的文字，供 AI function calling 直接回覆使用者。
    /// </summary>
    public interface IDiscordQueryService
    {
        /// <summary>QRY-01 查成員資訊（暱稱、加入時間、身分組數、禁言狀態）</summary>
        Task<QueryResult> GetMemberInfoAsync(string botToken, string guildId, string userId, CancellationToken ct = default);

        /// <summary>QRY-02 查成員的禁言 / 封鎖狀態</summary>
        Task<QueryResult> GetMemberStatusAsync(string botToken, string guildId, string userId, CancellationToken ct = default);

        /// <summary>QRY-03 列出伺服器頻道</summary>
        Task<QueryResult> ListChannelsAsync(string botToken, string guildId, int limit, CancellationToken ct = default);

        /// <summary>QRY-04 依名稱搜尋成員</summary>
        Task<QueryResult> SearchMembersAsync(string botToken, string guildId, string query, int limit, CancellationToken ct = default);

        /// <summary>QRY-05 列出伺服器身分組</summary>
        Task<QueryResult> ListRolesAsync(string botToken, string guildId, int limit, CancellationToken ct = default);

        /// <summary>SRV-01 查看最近的審核日誌</summary>
        Task<QueryResult> GetAuditLogAsync(string botToken, string guildId, int limit, CancellationToken ct = default);

        /// <summary>取得指定身分組的權限位元（用於排除特權身分組）；找不到回傳 null</summary>
        Task<ulong?> GetRolePermissionsAsync(string botToken, string guildId, string roleId, CancellationToken ct = default);
    }
}