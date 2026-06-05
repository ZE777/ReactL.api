namespace ReactL.api.Services.Webhooks
{
    /// <summary>
    /// Discord 指令註冊結果。
    /// Success=是否成功；Error=失敗時的人類可讀原因（成功為 null），供前端 Modal 顯示。
    /// </summary>
    public record DiscordCommandRegistrationResult(bool Success, string? Error);

    /// <summary>Discord 應用程式指令（Slash Command）註冊服務介面</summary>
    public interface IDiscordCommandService
    {
        /// <summary>
        /// 為指定 Discord Application 註冊 Global 的 /chat 指令。
        /// Global 指令對 Bot 加入的所有伺服器與私訊皆生效，建立當下不需要 GuildId，
        /// 使用者只要邀請 Bot 進伺服器即可使用（首次註冊最久約數分鐘 ~ 1 小時傳播）。
        /// 失敗時回傳 Success=false 並附上原因，呼叫端不應因此中斷 Bot 建立流程。
        /// </summary>
        /// <param name="applicationId">Discord Application ID</param>
        /// <param name="botToken">Bot Token 明文（呼叫端解密後傳入）</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>註冊結果（成功與否 + 失敗原因）</returns>
        Task<DiscordCommandRegistrationResult> RegisterChatCommandAsync(string applicationId, string botToken, CancellationToken cancellationToken = default);
    }
}