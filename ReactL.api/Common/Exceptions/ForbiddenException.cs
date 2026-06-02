namespace ReactL.api.Common.Exceptions
{
    /// <summary>
    /// 存取被拒例外，對應 HTTP 403 Forbidden
    /// 典型使用場景：已登入但試圖操作他人的資源（Persona、Conversation、BotBinding）
    /// 注意：與 UnauthorizedException（401）的區別在於，403 是「已知身份但無權限」，401 是「身份不明」
    /// </summary>
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message = "您沒有權限執行此操作")
            : base(message, 403, "FORBIDDEN")
        { }
    }
}
