namespace ReactL.api.DTOs.Common
{
    /// <summary>
    /// 統一 API 回應包裝類別，所有端點回傳資料都用此類別包裝
    /// 前端 axios interceptor 統一解析此格式，不需要每個頁面各自判斷
    /// </summary>
    /// <typeparam name="T">回應資料的型別</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>操作是否成功</summary>
        public bool Success { get; init; }

        /// <summary>回應資料，失敗時為 null</summary>
        public T? Data { get; init; }

        /// <summary>訊息，成功時通常為 null，失敗時為錯誤說明</summary>
        public string? Message { get; init; }

        /// <summary>建立成功回應</summary>
        public static ApiResponse<T> Ok(T data) =>
            new() { Success = true, Data = data };

        /// <summary>建立成功回應，附帶訊息（用於操作完成的提示，例如「刪除成功」）</summary>
        public static ApiResponse<T> Ok(T data, string message) =>
            new() { Success = true, Data = data, Message = message };

        /// <summary>建立失敗回應</summary>
        public static ApiResponse<T> Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
