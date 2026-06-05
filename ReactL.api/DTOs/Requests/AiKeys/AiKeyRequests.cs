using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Requests.AiKeys
{
    /// <summary>新增或更新某供應商的 AI Key（依 ProviderId upsert）</summary>
    public class UpsertAiKeyRequest
    {
        /// <summary>供應商識別碼，必須是 AiSettings.Providers 中已設定的 Id</summary>
        [Required(ErrorMessage = "請指定供應商")]
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>API Key 明文（後端會驗證、加密後儲存，不回傳原值）</summary>
        [Required(ErrorMessage = "請填寫 API Key")]
        public string ApiKey { get; set; } = string.Empty;
    }
}
