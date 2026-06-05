namespace ReactL.api.Common.Exceptions
{
    /// <summary>
    /// 上游 AI 服務暫時不可用例外，對應 HTTP 503 Service Unavailable
    /// 典型使用場景：非串流呼叫 AI（enhance、webhook、標題生成）時，
    /// 上游回傳 429/5xx 或逾時，且自動重試後仍失敗。
    /// 與單純的 HttpRequestException（→ 502 原始訊息）不同，此例外攜帶
    /// 對使用者友善的中文訊息，由 ExceptionMiddleware 直接回傳給前端顯示。
    /// </summary>
    public class UpstreamAiException : AppException
    {
        public UpstreamAiException(string message = "AI 服務暫時無法使用，請稍後再試")
            : base(message, 503, "AI_UPSTREAM_UNAVAILABLE")
        { }
    }
}