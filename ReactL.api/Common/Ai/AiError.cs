namespace ReactL.api.Common.Ai
{
    /// <summary>
    /// AI 上游錯誤的分類。串流（SSE chunk 類型）與非串流（UpstreamAiException）共用同一套對應，
    /// 確保前台公開聊天、後台聊天室、LINE / Discord Bot 對「同一種錯誤」呈現一致的訊息與行為。
    /// </summary>
    public enum AiErrorKind
    {
        /// <summary>429：限流 / 免費額度上限（可重試）</summary>
        RateLimit,
        /// <summary>401：金鑰無效或過期</summary>
        Auth,
        /// <summary>403：權限不足</summary>
        Forbidden,
        /// <summary>404 或 model_decommissioned / model_not_found：模型不存在/已下架</summary>
        ModelNotFound,
        /// <summary>413：內容超過 token 上限</summary>
        ContentTooLong,
        /// <summary>408 / 504：逾時（可重試）</summary>
        Timeout,
        /// <summary>502：連線失敗（可重試）</summary>
        Network,
        /// <summary>5xx：上游內部錯誤（可重試）</summary>
        ServerError,
        /// <summary>400：請求格式錯誤（多為模型輸出的工具指令有誤）</summary>
        BadRequest,
        /// <summary>未分類</summary>
        Unknown,
    }

    /// <summary>單一 AI 錯誤的分類結果：含友善訊息、是否可重試、以及對應的 SSE chunk 類型。</summary>
    /// <param name="Kind">錯誤分類</param>
    /// <param name="FriendlyMessage">對使用者友善的中文訊息（各載體直接顯示）</param>
    /// <param name="Retryable">是否值得自動重試（429 / 408 / 5xx 等暫時性錯誤）</param>
    public sealed record AiError(AiErrorKind Kind, string FriendlyMessage, bool Retryable)
    {
        /// <summary>SSE 串流的 chunk 類型：限流獨立成 "rate_limit"（前端可特別提示），其餘一律 "error"。</summary>
        public string ChunkType => Kind == AiErrorKind.RateLimit ? "rate_limit" : "error";
    }

    /// <summary>
    /// AI 上游錯誤分類器——所有「狀態碼/例外 → 友善訊息」的對應只存在這裡（單一事實來源）。
    /// </summary>
    public static class AiErrorClassifier
    {
        /// <summary>可重試的狀態碼：429（限流）、408（請求逾時）、所有 5xx（伺服器端暫時性錯誤）。</summary>
        public static bool IsTransientStatus(int statusCode) =>
            statusCode is 429 or 408 || statusCode >= 500;

        /// <summary>
        /// 依上游回傳的狀態碼（與可選的 error code）分類，產生友善訊息。
        /// </summary>
        public static AiError Classify(int statusCode, string providerDisplay, string? errorCode = null)
        {
            // 部分供應商以 4xx + code 表示模型下架/不存在，優先於狀態碼判斷
            if (errorCode is "model_decommissioned" or "model_not_found")
                return new AiError(AiErrorKind.ModelNotFound,
                    $"{providerDisplay} 的模型已下架或不存在，請切換其他模型", false);

            return statusCode switch
            {
                400 => new AiError(AiErrorKind.BadRequest,
                    $"{providerDisplay} 無法處理此請求（多為模型產生的指令格式有誤），請重試或改用較強的模型", false),
                401 => new AiError(AiErrorKind.Auth,
                    $"{providerDisplay} API Key 無效或已過期，請至設定頁更新 Key", false),
                403 => new AiError(AiErrorKind.Forbidden,
                    $"沒有存取 {providerDisplay} 的權限，請確認帳號方案或 API Key 權限", false),
                404 => new AiError(AiErrorKind.ModelNotFound,
                    $"{providerDisplay} 找不到指定的模型，請切換其他模型", false),
                408 => new AiError(AiErrorKind.Timeout,
                    $"{providerDisplay} 回應逾時，請稍後再試", true),
                413 => new AiError(AiErrorKind.ContentTooLong,
                    $"內容過長，已超過 {providerDisplay} 的 token 上限，請縮短內容或開新對話再試", false),
                429 => new AiError(AiErrorKind.RateLimit,
                    $"{providerDisplay} 免費額度已達上限，請稍後再試或切換其他模型", true),
                >= 500 => new AiError(AiErrorKind.ServerError,
                    $"{providerDisplay} 服務暫時不可用，請稍後再試", true),
                _ => new AiError(AiErrorKind.Unknown,
                    $"AI 服務暫時無法使用（{statusCode}）", false),
            };
        }

        /// <summary>逾時例外（TaskCanceledException 等）→ 統一逾時錯誤。</summary>
        public static AiError Timeout(string providerDisplay) =>
            new AiError(AiErrorKind.Timeout, $"{providerDisplay} 回應逾時，請稍後再試或切換其他模型", true);

        /// <summary>網路/連線例外（HttpRequestException 等）→ 統一連線錯誤。</summary>
        public static AiError Network() =>
            new AiError(AiErrorKind.Network, "AI 服務連線失敗，請稍後再試", true);
    }
}
