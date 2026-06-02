namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// CORS 跨域設定
    /// 對應 appsettings.json 的 "CorsSettings" section
    /// </summary>
    public class CorsSettings
    {
        /// <summary>
        /// 允許跨域存取的前端來源清單
        /// 開發環境：http://localhost:3000（Next.js）、http://localhost:5173（Vite）
        /// 生產環境：在 appsettings.Production.json 或環境變數中加入 IIS 站台 URL
        /// </summary>
        public string[] AllowedOrigins { get; set; } = [];
    }
}
