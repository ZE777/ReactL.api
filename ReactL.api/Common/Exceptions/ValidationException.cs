namespace ReactL.api.Common.Exceptions
{
    /// <summary>
    /// 輸入驗證失敗例外，對應 HTTP 422 Unprocessable Entity
    /// Details 欄位為 Dictionary 格式，包含各欄位的錯誤訊息，與前端錯誤顯示格式一致
    /// 典型使用場景：Service 層執行業務規則驗證失敗（格式驗證由 ModelState 處理，這裡負責業務規則）
    /// </summary>
    public class ValidationException : AppException
    {
        /// <param name="errors">欄位錯誤清單，格式為 { "欄位名稱": ["錯誤訊息1", "錯誤訊息2"] }</param>
        public ValidationException(Dictionary<string, string[]> errors)
            : base("輸入資料驗證失敗", 422, "VALIDATION_FAILED", errors)
        { }

        /// <summary>單一欄位單一錯誤的簡易建構子</summary>
        public ValidationException(string fieldName, string errorMessage)
            : base("輸入資料驗證失敗", 422, "VALIDATION_FAILED",
                new Dictionary<string, string[]> { { fieldName, [errorMessage] } })
        { }
    }
}
