namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// JWT（JSON Web Token）認證設定
    /// 對應 appsettings.json 的 "JwtSettings" section
    /// 敏感欄位 SecretKey 必須透過 User Secrets 或環境變數設定，禁止寫入版控
    /// </summary>
    public class JwtSettings
    {
        /// <summary>Token 的發行者，驗證時用來確認 Token 來源正確</summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>Token 的預期受眾，驗證時確認 Token 是否發給本系統</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>Token 有效時間（分鐘），預設 60 分鐘</summary>
        public int ExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// HMAC 簽名用的密鑰，長度建議 256 bit（32 字元）以上
        /// 設定方式：dotnet user-secrets set "JwtSettings:SecretKey" "your-secret"
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;
    }
}
