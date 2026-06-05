using System.Net.Http.Headers;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>LINE 憑證（Channel Access Token）驗證服務實作</summary>
    public class LineCredentialService : ILineCredentialService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LineCredentialService> _logger;

        private const string LineBotInfoUrl = "https://api.line.me/v2/bot/info";

        public LineCredentialService(IHttpClientFactory httpClientFactory, ILogger<LineCredentialService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<LineCredentialResult> ValidateAsync(string channelAccessToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(channelAccessToken))
                return new LineCredentialResult(false, "缺少 Channel Access Token");

            try
            {
                var client = _httpClientFactory.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Get, LineBotInfoUrl);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", channelAccessToken.Trim());

                using var resp = await client.SendAsync(req, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("LINE 憑證驗證成功（/bot/info 200）");
                    return new LineCredentialResult(true, null);
                }

                var status = (int)resp.StatusCode;
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("LINE 憑證驗證失敗 {Status}: {Body}", status, body);

                // 401 視為 Token 明確無效；其他狀態（429 限流等）不擋下，從寬視為「無法確認」（valid=null）
                return status == 401
                    ? new LineCredentialResult(false, "LINE Channel Access Token 無效（請確認是否填入長期有效 Token 或已重新發行）")
                    : new LineCredentialResult(true, null);
            }
            catch (Exception ex)
            {
                // 網路/逾時等問題不應擋下建立，從寬視為「無法確認」
                _logger.LogWarning(ex, "LINE 憑證驗證時發生例外，從寬放行");
                return new LineCredentialResult(true, null);
            }
        }
    }
}