namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// AES-256-CBC 加密設定，用於 Bot Token / ChannelSecret 的可逆加密儲存
    /// 對應 appsettings.json 的 "EncryptionSettings" section
    /// Key 和 IV 屬於高度敏感資訊，禁止寫入任何設定檔，必須透過 User Secrets 或環境變數設定
    /// </summary>
    public class EncryptionSettings
    {
        /// <summary>
        /// AES-256 加密金鑰，需為 32 bytes（256 bits），以 Base64 編碼儲存
        /// 設定方式：dotnet user-secrets set "EncryptionSettings:Key" "base64-encoded-32-bytes"
        /// 生成範例（PowerShell）：[Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 }))
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// AES-CBC 初始向量，需為 16 bytes（128 bits），以 Base64 編碼儲存
        /// 設定方式：dotnet user-secrets set "EncryptionSettings:Iv" "base64-encoded-16-bytes"
        /// </summary>
        public string Iv { get; set; } = string.Empty;
    }
}
