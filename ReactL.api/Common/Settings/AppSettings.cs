namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// 應用程式基本設定
    /// 對應 appsettings.json 的 "AppSettings" section
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// 應用程式對外的基礎 URL
        /// 用於 CORS、Webhook Callback、Email 連結等需要完整網址的場景
        /// 生產環境改為 IIS 站台的實際 URL（例如 https://api.yourdomain.com）
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>應用程式顯示名稱，用於 Swagger 文件標題、Log 識別等</summary>
        public string AppName { get; set; } = "ReactL Prompt Studio";
    }
}
