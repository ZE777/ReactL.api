namespace ReactL.api.Services.Webhooks
{
    /// <summary>
    /// LINE 憑證驗證結果。
    /// Success=Channel Access Token 是否有效；Error=失敗時的人類可讀原因（成功為 null）。
    /// </summary>
    public record LineCredentialResult(bool Success, string? Error);

    /// <summary>LINE 憑證（Channel Access Token）驗證服務介面</summary>
    public interface ILineCredentialService
    {
        /// <summary>
        /// 以 Channel Access Token 呼叫 LINE GET /v2/bot/info 驗證其有效性。
        /// 200 = 有效；401 = Token 無效；其他狀態從寬視為「無法確認」而非失敗。
        /// 註：Channel Secret 無法獨立驗證（僅用於 Webhook 簽名驗證），故此處只驗 Token。
        /// </summary>
        /// <param name="channelAccessToken">LINE Channel Access Token 明文（呼叫端解密後傳入）</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>驗證結果（成功與否 + 失敗原因）</returns>
        Task<LineCredentialResult> ValidateAsync(string channelAccessToken, CancellationToken cancellationToken = default);
    }
}