namespace ReactL.api.Common.Exceptions
{
    /// <summary>
    /// 未授權例外，對應 HTTP 401 Unauthorized
    /// 典型使用場景：登入密碼錯誤、帳號不存在、Token 驗證失敗時主動拋出
    /// JWT 過期由 ASP.NET Core 內建機制回傳 401，通常不需要手動拋此例外
    /// </summary>
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message = "帳號或密碼錯誤")
            : base(message, 401, "UNAUTHORIZED")
        { }
    }
}
