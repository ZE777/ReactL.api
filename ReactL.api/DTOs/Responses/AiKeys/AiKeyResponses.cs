namespace ReactL.api.DTOs.Responses.AiKeys
{
    /// <summary>AI Key 回應（永不回傳金鑰原文，只回後 4 碼與中繼資訊）</summary>
    public class AiKeyResponse
    {
        public Guid Id { get; set; }

        /// <summary>供應商識別碼，如 "groq"</summary>
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>供應商顯示名稱，如 "Groq"（找不到時回傳 ProviderId）</summary>
        public string ProviderDisplayName { get; set; } = string.Empty;

        /// <summary>金鑰後 4 碼，前端顯示用</summary>
        public string KeyLastFour { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        /// <summary>是否為系統預設 Key（一般使用者清單不會出現，保留欄位供未來管理介面使用）</summary>
        public bool IsSystem { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
