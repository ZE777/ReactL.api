namespace ReactL.api.Common.Exceptions
{
    /// <summary>
    /// 資料衝突例外，對應 HTTP 409 Conflict
    /// 典型使用場景：Email 已被註冊、Persona 名稱重複、Bot 已存在相同 Platform + Token 組合
    /// </summary>
    public class ConflictException : AppException
    {
        public ConflictException(string message, string errorCode = "CONFLICT")
            : base(message, 409, errorCode)
        { }
    }
}
