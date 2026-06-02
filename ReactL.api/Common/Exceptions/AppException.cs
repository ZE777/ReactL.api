namespace ReactL.api.Common.Exceptions
{
    /// <summary>
    /// 應用程式自訂例外基底類別
    /// 所有業務邏輯例外都應繼承此類別，讓 ExceptionMiddleware 能識別並對應正確的 HTTP 狀態碼
    /// 不繼承此類別的例外（如 DbUpdateException、NullReferenceException）會被視為非預期錯誤，回傳 500
    /// </summary>
    public class AppException : Exception
    {
        /// <summary>對應的 HTTP 狀態碼，由子類別設定</summary>
        public int StatusCode { get; }

        /// <summary>
        /// 機器可讀的錯誤碼，供前端判斷錯誤類型並顯示對應訊息
        /// 命名規則：{RESOURCE}_{ACTION}，例如 "PERSONA_NOT_FOUND"、"EMAIL_ALREADY_EXISTS"
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>額外的錯誤細節，ValidationException 用此欄位回傳欄位錯誤清單</summary>
        public object? Details { get; }

        public AppException(string message, int statusCode, string errorCode, object? details = null)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            Details = details;
        }
    }
}
