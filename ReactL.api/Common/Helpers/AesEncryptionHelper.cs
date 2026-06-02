using ReactL.api.Common.Settings;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace ReactL.api.Common.Helpers
{
    /// <summary>
    /// AES-256-CBC 雙向加解密工具，專門用於 BotBinding 的 Token / ChannelSecret 加密儲存
    /// 注入方式：builder.Services.AddSingleton&lt;AesEncryptionHelper&gt;()
    /// </summary>
    public class AesEncryptionHelper
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        /// <param name="encryptionSettings">從 appsettings 或 User Secrets 讀取的加密設定</param>
        public AesEncryptionHelper(IOptions<EncryptionSettings> encryptionSettings)
        {
            var settings = encryptionSettings.Value;

            // Key 和 IV 以 Base64 儲存於設定檔，此處解碼為 byte[]
            // 若設定值為空，代表尚未透過 User Secrets 設定，啟動時會在驗證階段失敗
            _key = Convert.FromBase64String(settings.Key);
            _iv = Convert.FromBase64String(settings.Iv);

            // 驗證 Key 長度必須為 32 bytes（AES-256），IV 必須為 16 bytes（AES block size）
            if (_key.Length != 32)
                throw new InvalidOperationException("EncryptionSettings:Key 必須為 32 bytes（256 bits）的 Base64 字串");
            if (_iv.Length != 16)
                throw new InvalidOperationException("EncryptionSettings:Iv 必須為 16 bytes（128 bits）的 Base64 字串");
        }

        /// <summary>
        /// 將明文字串以 AES-256-CBC 加密，回傳 Base64 編碼的密文
        /// </summary>
        /// <param name="plainText">要加密的明文，例如 LINE Channel Access Token</param>
        public string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }

        /// <summary>
        /// 將 Base64 密文以 AES-256-CBC 解密，回傳原始明文
        /// </summary>
        /// <param name="cipherText">Encrypt 方法產生的 Base64 密文</param>
        public string Decrypt(string cipherText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// 取得明文的後 N 碼，用於儲存 TokenLastFour
        /// </summary>
        /// <param name="plainText">原始明文 Token</param>
        /// <param name="count">擷取的尾碼長度，預設 4</param>
        public static string GetLastChars(string plainText, int count = 4) =>
            plainText.Length >= count
                ? plainText[^count..]
                : plainText;
    }
}
