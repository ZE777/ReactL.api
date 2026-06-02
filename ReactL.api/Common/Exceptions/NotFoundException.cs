namespace ReactL.api.Common.Exceptions
{
    /// <summary>
    /// 資源不存在例外，對應 HTTP 404 Not Found
    /// 典型使用場景：查詢單筆資源時找不到（GetByIdAsync 回傳 null 時拋出）
    /// </summary>
    public class NotFoundException : AppException
    {
        /// <param name="resourceName">資源名稱，例如 "Persona"、"Conversation"</param>
        /// <param name="resourceId">查詢用的 ID 值</param>
        public NotFoundException(string resourceName, object resourceId)
            : base($"{resourceName} '{resourceId}' 不存在", 404, $"{resourceName.ToUpperInvariant()}_NOT_FOUND")
        { }
    }
}
