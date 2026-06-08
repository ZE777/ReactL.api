namespace ReactL.api.Services.Webhooks
{
    /// <summary>
    /// Discord 管理動作執行結果。
    /// Success=是否成功；Error=失敗時的人類可讀原因（成功為 null），供回覆使用者。
    /// </summary>
    public record ModerationResult(bool Success, string? Error);

    /// <summary>
    /// Discord 伺服器管理動作服務（手刻 HTTP 呼叫 REST API）。
    /// 每個方法對應一個 Discord 端點，以 Bot Token 於 header 驗證，無狀態、支援多租戶各自 Token。
    /// </summary>
    public interface IDiscordModerationService
    {
        /// <summary>禁言（timeout）成員指定秒數。PATCH /guilds/{g}/members/{u} → communication_disabled_until</summary>
        Task<ModerationResult> TimeoutMemberAsync(
            string botToken, string guildId, string userId, int seconds, string? reason, CancellationToken cancellationToken = default);

        /// <summary>解除禁言。PATCH /guilds/{g}/members/{u} → communication_disabled_until = null</summary>
        Task<ModerationResult> RemoveTimeoutAsync(
            string botToken, string guildId, string userId, string? reason, CancellationToken cancellationToken = default);

        /// <summary>移動成員到指定語音頻道。PATCH member → channel_id</summary>
        Task<ModerationResult> MoveMemberAsync(
            string botToken, string guildId, string userId, string channelId, string? reason, CancellationToken cancellationToken = default);

        /// <summary>將成員踢出語音（中斷連線）。PATCH member → channel_id = null</summary>
        Task<ModerationResult> DisconnectVoiceAsync(
            string botToken, string guildId, string userId, string? reason, CancellationToken cancellationToken = default);

        /// <summary>語音禁麥 / 解除禁麥。PATCH member → mute</summary>
        Task<ModerationResult> SetVoiceMuteAsync(
            string botToken, string guildId, string userId, bool mute, string? reason, CancellationToken cancellationToken = default);

        /// <summary>語音禁聽 / 解除禁聽。PATCH member → deaf</summary>
        Task<ModerationResult> SetVoiceDeafAsync(
            string botToken, string guildId, string userId, bool deaf, string? reason, CancellationToken cancellationToken = default);

        /// <summary>變更成員暱稱（nick；傳 null 清除暱稱）。PATCH member → nick</summary>
        Task<ModerationResult> SetNicknameAsync(
            string botToken, string guildId, string userId, string? nickname, string? reason, CancellationToken cancellationToken = default);

        /// <summary>解除封鎖。DELETE /guilds/{g}/bans/{u}</summary>
        Task<ModerationResult> UnbanMemberAsync(
            string botToken, string guildId, string userId, string? reason, CancellationToken cancellationToken = default);

        /// <summary>在指定頻道發送訊息。POST /channels/{c}/messages</summary>
        Task<ModerationResult> SendMessageAsync(
            string botToken, string channelId, string content, CancellationToken cancellationToken = default);

        /// <summary>踢出成員。DELETE /guilds/{g}/members/{u}（需二次確認）</summary>
        Task<ModerationResult> KickMemberAsync(
            string botToken, string guildId, string userId, string? reason, CancellationToken cancellationToken = default);

        /// <summary>封鎖成員。PUT /guilds/{g}/bans/{u} → delete_message_seconds（需二次確認）</summary>
        Task<ModerationResult> BanMemberAsync(
            string botToken, string guildId, string userId, int deleteMessageDays, string? reason, CancellationToken cancellationToken = default);

        /// <summary>賦予成員身分組。PUT /guilds/{g}/members/{u}/roles/{r}（需二次確認）</summary>
        Task<ModerationResult> AddRoleAsync(
            string botToken, string guildId, string userId, string roleId, string? reason, CancellationToken cancellationToken = default);

        /// <summary>移除成員身分組。DELETE /guilds/{g}/members/{u}/roles/{r}（需二次確認）</summary>
        Task<ModerationResult> RemoveRoleAsync(
            string botToken, string guildId, string userId, string roleId, string? reason, CancellationToken cancellationToken = default);

        /// <summary>設定頻道慢速模式秒數。PATCH /channels/{c} → rate_limit_per_user（需二次確認）</summary>
        Task<ModerationResult> SetSlowmodeAsync(
            string botToken, string channelId, int seconds, string? reason, CancellationToken cancellationToken = default);

        /// <summary>批次刪除頻道最近 N 則訊息（自動抓取後 bulk-delete；需二次確認）</summary>
        Task<ModerationResult> PurgeMessagesAsync(
            string botToken, string channelId, int count, CancellationToken cancellationToken = default);
    }
}